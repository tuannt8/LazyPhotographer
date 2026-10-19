#include "../include/ImageProcessor.h"
#include <cmath>
#include <algorithm>
#include <cstring>

namespace ImageProcessing {

ImageProcessor::ImageProcessor() = default;
ImageProcessor::~ImageProcessor() = default;

void ImageProcessor::setSourceBuffer(std::shared_ptr<RawProcessing::RawBuffer16> sourceBuffer) {
    m_sourceBuffer = sourceBuffer;
}

inline float ImageProcessor::applyExposure(float val, float ev) {
    if (std::abs(ev) < 0.001f) return val;
    return val * std::pow(2.0f, ev);
}

inline float ImageProcessor::applyToneCurve(float val, float contrast, float highlights, float shadows, float whites, float blacks) {
    // Linear to sRGB approximation for tone curve application
    float norm = std::clamp(val, 0.0f, 1.0f);

    // Apply Contrast (centered at 0.5)
    if (std::abs(contrast) > 0.01f) {
        float cFactor = (contrast / 100.0f) * 0.7f;
        norm = 0.5f + (norm - 0.5f) * (1.0f + cFactor);
        norm = std::clamp(norm, 0.0f, 1.0f);
    }

    // Shadows & Highlights
    if (std::abs(shadows) > 0.01f && norm < 0.5f) {
        float shadowWeight = std::pow(1.0f - (norm / 0.5f), 1.5f);
        norm += (shadows / 100.0f) * 0.25f * shadowWeight;
    }
    if (std::abs(highlights) > 0.01f && norm > 0.5f) {
        float highlightWeight = std::pow((norm - 0.5f) / 0.5f, 1.5f);
        norm += (highlights / 100.0f) * 0.25f * highlightWeight;
    }

    // Whites & Blacks
    if (std::abs(blacks) > 0.01f && norm < 0.25f) {
        float bWeight = (0.25f - norm) / 0.25f;
        norm += (blacks / 100.0f) * 0.15f * bWeight;
    }
    if (std::abs(whites) > 0.01f && norm > 0.75f) {
        float wWeight = (norm - 0.75f) / 0.25f;
        norm += (whites / 100.0f) * 0.15f * wWeight;
    }

    return std::clamp(norm, 0.0f, 1.0f);
}

void ImageProcessor::rgbToHsl(float r, float g, float b, float& h, float& s, float& l) {
    float maxVal = std::max({r, g, b});
    float minVal = std::min({r, g, b});
    float delta = maxVal - minVal;

    l = (maxVal + minVal) * 0.5f;

    if (delta < 0.00001f) {
        s = 0.0f;
        h = 0.0f;
    } else {
        s = (l > 0.5f) ? (delta / (2.0f - maxVal - minVal)) : (delta / (maxVal + minVal));
        if (maxVal == r) {
            h = (g - b) / delta + (g < b ? 6.0f : 0.0f);
        } else if (maxVal == g) {
            h = (b - r) / delta + 2.0f;
        } else {
            h = (r - g) / delta + 4.0f;
        }
        h /= 6.0f;
    }
}

static inline float hueToRgb(float p, float q, float t) {
    if (t < 0.0f) t += 1.0f;
    if (t > 1.0f) t -= 1.0f;
    if (t < 1.0f / 6.0f) return p + (q - p) * 6.0f * t;
    if (t < 1.0f / 2.0f) return q;
    if (t < 2.0f / 3.0f) return p + (q - p) * (2.0f / 3.0f - t) * 6.0f;
    return p;
}

void ImageProcessor::hslToRgb(float h, float s, float l, float& r, float& g, float& b) {
    if (s < 0.00001f) {
        r = g = b = l;
    } else {
        float q = l < 0.5f ? l * (1.0f + s) : l + s - l * s;
        float p = 2.0f * l - q;
        r = hueToRgb(p, q, h + 1.0f / 3.0f);
        g = hueToRgb(p, q, h);
        b = hueToRgb(p, q, h - 1.0f / 3.0f);
    }
}

bool ImageProcessor::renderToRgba8(const Adjustments& adj, uint8_t* outRgbaBuffer, uint32_t width, uint32_t height, uint32_t stride) {
    if (!m_sourceBuffer || !m_sourceBuffer->isValid() || !outRgbaBuffer) return false;

    // Resample if destination dimensions differ
    std::shared_ptr<RawProcessing::RawBuffer16> workBuffer;
    if (m_sourceBuffer->width() != width || m_sourceBuffer->height() != height) {
        workBuffer = m_sourceBuffer->createResampled(width, height);
    } else {
        workBuffer = std::make_shared<RawProcessing::RawBuffer16>(*m_sourceBuffer);
    }

    if (!workBuffer || !workBuffer->isValid()) return false;

    // Apply AI Denoise if requested
    if (adj.aiDenoiseStrength > 0.01f) {
        m_aiProcessor.applyAiDenoise(*workBuffer, adj.aiDenoiseStrength);
    }
    // Apply AI Detail Enhancement if requested
    if (adj.enableAiDetailEnhance) {
        m_aiProcessor.applyAiDetailEnhance(*workBuffer, 0.5f);
    }

    // Precalculate temperature & tint multipliers
    // Temperature: warm adds R, subtracts B. Tint: magenta adds R+B, subtracts G.
    float tempFactor = adj.temperature / 100.0f;
    float tintFactor = adj.tint / 100.0f;

    float rGain = 1.0f + tempFactor * 0.25f + tintFactor * 0.15f;
    float gGain = 1.0f - tintFactor * 0.25f;
    float bGain = 1.0f - tempFactor * 0.25f + tintFactor * 0.15f;

    float satFactor = 1.0f + (adj.saturation / 100.0f);
    float vibFactor = (adj.vibrance / 100.0f);

    #pragma omp parallel for
    for (int y = 0; y < static_cast<int>(height); ++y) {
        uint8_t* outRow = outRgbaBuffer + y * stride;
        for (uint32_t x = 0; x < width; ++x) {
            float r = workBuffer->getSample(x, y, 0) / 65535.0f;
            float g = workBuffer->getSample(x, y, 1) / 65535.0f;
            float b = workBuffer->getSample(x, y, 2) / 65535.0f;

            // 1. White balance
            r *= rGain;
            g *= gGain;
            b *= bGain;

            // 2. Exposure
            r = applyExposure(r, adj.exposure);
            g = applyExposure(g, adj.exposure);
            b = applyExposure(b, adj.exposure);

            // 3. Contrast, Highlights, Shadows, Whites, Blacks
            r = applyToneCurve(r, adj.contrast, adj.highlights, adj.shadows, adj.whites, adj.blacks);
            g = applyToneCurve(g, adj.contrast, adj.highlights, adj.shadows, adj.whites, adj.blacks);
            b = applyToneCurve(b, adj.contrast, adj.highlights, adj.shadows, adj.whites, adj.blacks);

            // 4. Vibrance & Saturation in HSL
            if (std::abs(satFactor - 1.0f) > 0.001f || std::abs(vibFactor) > 0.001f) {
                float hVal, sVal, lVal;
                rgbToHsl(r, g, b, hVal, sVal, lVal);
                
                // Vibrance boosts low-saturated colors more than already saturated ones
                if (std::abs(vibFactor) > 0.001f) {
                    float vibScale = (1.0f - sVal);
                    sVal += vibFactor * vibScale * 0.5f;
                }
                sVal *= satFactor;
                sVal = std::clamp(sVal, 0.0f, 1.0f);

                hslToRgb(hVal, sVal, lVal, r, g, b);
            }

            // RGBA8 output
            outRow[x * 4 + 0] = static_cast<uint8_t>(std::clamp(r * 255.0f, 0.0f, 255.0f));
            outRow[x * 4 + 1] = static_cast<uint8_t>(std::clamp(g * 255.0f, 0.0f, 255.0f));
            outRow[x * 4 + 2] = static_cast<uint8_t>(std::clamp(b * 255.0f, 0.0f, 255.0f));
            outRow[x * 4 + 3] = 255; // Alpha
        }
    }

    return true;
}

bool ImageProcessor::renderToBuffer16(const Adjustments& adj, RawProcessing::RawBuffer16& outBuffer) {
    if (!m_sourceBuffer || !m_sourceBuffer->isValid()) return false;

    const uint32_t width = m_sourceBuffer->width();
    const uint32_t height = m_sourceBuffer->height();
    outBuffer.allocate(width, height, 3);

    float tempFactor = adj.temperature / 100.0f;
    float tintFactor = adj.tint / 100.0f;
    float rGain = 1.0f + tempFactor * 0.25f + tintFactor * 0.15f;
    float gGain = 1.0f - tintFactor * 0.25f;
    float bGain = 1.0f - tempFactor * 0.25f + tintFactor * 0.15f;

    float satFactor = 1.0f + (adj.saturation / 100.0f);
    float vibFactor = (adj.vibrance / 100.0f);

    #pragma omp parallel for
    for (int y = 0; y < static_cast<int>(height); ++y) {
        for (uint32_t x = 0; x < width; ++x) {
            float r = m_sourceBuffer->getSample(x, y, 0) / 65535.0f;
            float g = m_sourceBuffer->getSample(x, y, 1) / 65535.0f;
            float b = m_sourceBuffer->getSample(x, y, 2) / 65535.0f;

            r *= rGain;
            g *= gGain;
            b *= bGain;

            r = applyExposure(r, adj.exposure);
            g = applyExposure(g, adj.exposure);
            b = applyExposure(b, adj.exposure);

            r = applyToneCurve(r, adj.contrast, adj.highlights, adj.shadows, adj.whites, adj.blacks);
            g = applyToneCurve(g, adj.contrast, adj.highlights, adj.shadows, adj.whites, adj.blacks);
            b = applyToneCurve(b, adj.contrast, adj.highlights, adj.shadows, adj.whites, adj.blacks);

            if (std::abs(satFactor - 1.0f) > 0.001f || std::abs(vibFactor) > 0.001f) {
                float hVal, sVal, lVal;
                rgbToHsl(r, g, b, hVal, sVal, lVal);
                if (std::abs(vibFactor) > 0.001f) {
                    sVal += vibFactor * (1.0f - sVal) * 0.5f;
                }
                sVal *= satFactor;
                sVal = std::clamp(sVal, 0.0f, 1.0f);
                hslToRgb(hVal, sVal, lVal, r, g, b);
            }

            outBuffer.setSample(x, y, 0, static_cast<uint16_t>(std::clamp(r * 65535.0f, 0.0f, 65535.0f)));
            outBuffer.setSample(x, y, 1, static_cast<uint16_t>(std::clamp(g * 65535.0f, 0.0f, 65535.0f)));
            outBuffer.setSample(x, y, 2, static_cast<uint16_t>(std::clamp(b * 65535.0f, 0.0f, 65535.0f)));
        }
    }

    // Apply AI Denoise if requested
    if (adj.aiDenoiseStrength > 0.01f) {
        m_aiProcessor.applyAiDenoise(outBuffer, adj.aiDenoiseStrength);
    }
    // Apply AI Detail Enhancement if requested
    if (adj.enableAiDetailEnhance) {
        m_aiProcessor.applyAiDetailEnhance(outBuffer, 0.5f);
    }

    return true;
}

bool ImageProcessor::computeHistogram(const Adjustments& adj, HistogramData& outHistogram) {
    if (!m_sourceBuffer || !m_sourceBuffer->isValid()) return false;

    std::memset(&outHistogram, 0, sizeof(HistogramData));

    const uint32_t w = m_sourceBuffer->width();
    const uint32_t h = m_sourceBuffer->height();
    const size_t totalPixels = static_cast<size_t>(w) * h;
    const size_t step = std::max<size_t>(1, totalPixels / 50000); // 50k samples for live smooth 60fps histogram

    float tempFactor = adj.temperature / 100.0f;
    float tintFactor = adj.tint / 100.0f;
    float rGain = 1.0f + tempFactor * 0.25f + tintFactor * 0.15f;
    float gGain = 1.0f - tintFactor * 0.25f;
    float bGain = 1.0f - tempFactor * 0.25f + tintFactor * 0.15f;

    for (uint32_t y = 0; y < h; y += static_cast<uint32_t>(std::sqrt(step))) {
        for (uint32_t x = 0; x < w; x += static_cast<uint32_t>(std::sqrt(step))) {
            float r = m_sourceBuffer->getSample(x, y, 0) / 65535.0f * rGain;
            float g = m_sourceBuffer->getSample(x, y, 1) / 65535.0f * gGain;
            float b = m_sourceBuffer->getSample(x, y, 2) / 65535.0f * bGain;

            r = applyExposure(r, adj.exposure);
            g = applyExposure(g, adj.exposure);
            b = applyExposure(b, adj.exposure);

            r = applyToneCurve(r, adj.contrast, adj.highlights, adj.shadows, adj.whites, adj.blacks);
            g = applyToneCurve(g, adj.contrast, adj.highlights, adj.shadows, adj.whites, adj.blacks);
            b = applyToneCurve(b, adj.contrast, adj.highlights, adj.shadows, adj.whites, adj.blacks);

            uint8_t r8 = static_cast<uint8_t>(std::clamp(r * 255.0f, 0.0f, 255.0f));
            uint8_t g8 = static_cast<uint8_t>(std::clamp(g * 255.0f, 0.0f, 255.0f));
            uint8_t b8 = static_cast<uint8_t>(std::clamp(b * 255.0f, 0.0f, 255.0f));
            uint8_t l8 = static_cast<uint8_t>(std::clamp((0.2126f * r + 0.7152f * g + 0.0722f * b) * 255.0f, 0.0f, 255.0f));

            outHistogram.rBins[r8]++;
            outHistogram.gBins[g8]++;
            outHistogram.bBins[b8]++;
            outHistogram.lumaBins[l8]++;

            outHistogram.maxCount = std::max({outHistogram.maxCount, outHistogram.rBins[r8], outHistogram.gBins[g8], outHistogram.bBins[b8]});
        }
    }

    return true;
}

} // namespace ImageProcessing
