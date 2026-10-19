#include "../include/AiProcessor.h"
#include <cmath>
#include <algorithm>
#include <vector>
#include <numeric>

namespace ImageProcessing {

AiProcessor::AiProcessor() = default;
AiProcessor::~AiProcessor() = default;

bool AiProcessor::analyzeAndAutoTone(const RawProcessing::RawBuffer16& input, Adjustments& outAdjustments) {
    if (!input.isValid()) return false;

    const uint32_t w = input.width();
    const uint32_t h = input.height();
    const size_t totalPixels = static_cast<size_t>(w) * h;
    if (totalPixels == 0) return false;

    // Sample pixels across a grid (up to 100,000 pixels for lightning speed)
    size_t step = std::max<size_t>(1, totalPixels / 100000);
    std::vector<float> lumaSamples;
    lumaSamples.reserve(totalPixels / step + 1);

    double sumR = 0.0, sumG = 0.0, sumB = 0.0;
    size_t sampledCount = 0;

    for (uint32_t y = 0; y < h; y += static_cast<uint32_t>(std::sqrt(step))) {
        for (uint32_t x = 0; x < w; x += static_cast<uint32_t>(std::sqrt(step))) {
            float r = input.getSample(x, y, 0) / 65535.0f;
            float g = input.getSample(x, y, 1) / 65535.0f;
            float b = input.getSample(x, y, 2) / 65535.0f;

            // Rec.709 luminance
            float luma = 0.2126f * r + 0.7152f * g + 0.0722f * b;
            lumaSamples.push_back(luma);

            sumR += r;
            sumG += g;
            sumB += b;
            sampledCount++;
        }
    }

    if (lumaSamples.empty() || sampledCount == 0) return false;

    // Calculate percentiles
    std::sort(lumaSamples.begin(), lumaSamples.end());
    float p1 = lumaSamples[lumaSamples.size() * 1 / 100];
    float p10 = lumaSamples[lumaSamples.size() * 10 / 100];
    float p50 = lumaSamples[lumaSamples.size() * 50 / 100]; // Median
    float p90 = lumaSamples[lumaSamples.size() * 90 / 100];
    float p99 = lumaSamples[lumaSamples.size() * 99 / 100];

    // 1. Exposure compensation calculation (target midtone median around 0.22 - 0.25 linear)
    float targetMid = 0.22f;
    float currentMid = std::max(0.01f, p50);
    float autoEv = static_cast<float>(std::log2(targetMid / currentMid));
    autoEv = std::clamp(autoEv, -2.5f, 2.5f);

    // 2. Highlights & Shadows analysis
    float autoHighlights = 0.0f;
    float autoShadows = 0.0f;

    // If highlights are blowing out (p99 > 0.90), pull highlights back
    if (p99 > 0.85f) {
        autoHighlights = -std::clamp((p99 - 0.85f) * 200.0f, 0.0f, 75.0f);
    }
    // If shadows are crushed (p10 < 0.04), lift shadows
    if (p10 < 0.06f) {
        autoShadows = std::clamp((0.06f - p10) * 500.0f, 0.0f, 60.0f);
    }

    // 3. Contrast based on dynamic spread
    float spread = p90 - p10;
    float autoContrast = 0.0f;
    if (spread < 0.35f) {
        autoContrast = (0.35f - spread) * 80.0f; // Boost low contrast
    } else if (spread > 0.75f) {
        autoContrast = -(spread - 0.75f) * 60.0f; // Tone down harsh contrast
    }

    // 4. White Balance (Gray World algorithm)
    float avgR = static_cast<float>(sumR / sampledCount);
    float avgG = static_cast<float>(sumG / sampledCount);
    float avgB = static_cast<float>(sumB / sampledCount);
    float avgAll = (avgR + avgG + avgB) / 3.0f;

    float autoTemp = 0.0f;
    float autoTint = 0.0f;
    if (avgAll > 0.01f) {
        float rDiff = (avgR - avgB) / avgAll;
        autoTemp = -std::clamp(rDiff * 40.0f, -30.0f, 30.0f);

        float gDiff = (avgG - (avgR + avgB) * 0.5f) / avgAll;
        autoTint = -std::clamp(gDiff * 40.0f, -30.0f, 30.0f);
    }

    // Set adjustments
    outAdjustments.exposure = autoEv;
    outAdjustments.contrast = std::clamp(autoContrast, -50.0f, 50.0f);
    outAdjustments.highlights = autoHighlights;
    outAdjustments.shadows = autoShadows;
    outAdjustments.whites = 10.0f;
    outAdjustments.blacks = -5.0f;
    outAdjustments.temperature = autoTemp;
    outAdjustments.tint = autoTint;
    outAdjustments.vibrance = 15.0f;
    outAdjustments.saturation = 5.0f;
    outAdjustments.enableAiAutoTone = true;

    return true;
}

bool AiProcessor::applyAiDenoise(RawProcessing::RawBuffer16& buffer, float strength) {
    if (!buffer.isValid() || strength <= 0.001f) return false;

    strength = std::clamp(strength, 0.0f, 1.0f);
    const uint32_t w = buffer.width();
    const uint32_t h = buffer.height();
    const uint32_t channels = buffer.channels();

    // Edge-preserving bilateral filter
    RawProcessing::RawBuffer16 temp(w, h, channels);
    const int radius = strength > 0.5f ? 2 : 1;
    const float spatialSigma = 1.0f + strength * 2.0f;
    const float colorSigma = (0.04f + strength * 0.12f) * 65535.0f;
    const float twoColorSigmaSq = 2.0f * colorSigma * colorSigma;

    #pragma omp parallel for
    for (int y = 0; y < static_cast<int>(h); ++y) {
        for (uint32_t x = 0; x < w; ++x) {
            float centerVals[4] = {0};
            for (uint32_t c = 0; c < channels; ++c) {
                centerVals[c] = buffer.getSample(x, y, c);
            }

            float accum[4] = {0};
            float totalWeight[4] = {0};

            for (int dy = -radius; dy <= radius; ++dy) {
                int ny = std::clamp(y + dy, 0, static_cast<int>(h - 1));
                float distSpatialSq = static_cast<float>(dy * dy);

                for (int dx = -radius; dx <= radius; ++dx) {
                    int nx = std::clamp(static_cast<int>(x) + dx, 0, static_cast<int>(w - 1));
                    float distSq = distSpatialSq + static_cast<float>(dx * dx);
                    float spatialWeight = std::exp(-distSq / (2.0f * spatialSigma * spatialSigma));

                    for (uint32_t c = 0; c < channels; ++c) {
                        float neighborVal = buffer.getSample(nx, ny, c);
                        float colorDiff = neighborVal - centerVals[c];
                        float rangeWeight = std::exp(-(colorDiff * colorDiff) / twoColorSigmaSq);

                        float weight = spatialWeight * rangeWeight;
                        accum[c] += neighborVal * weight;
                        totalWeight[c] += weight;
                    }
                }
            }

            for (uint32_t c = 0; c < channels; ++c) {
                float filtered = (totalWeight[c] > 0.0001f) ? (accum[c] / totalWeight[c]) : centerVals[c];
                // Blend with original based on strength
                float finalVal = centerVals[c] * (1.0f - strength) + filtered * strength;
                temp.setSample(x, y, c, static_cast<uint16_t>(std::clamp(finalVal, 0.0f, 65535.0f)));
            }
        }
    }

    buffer = std::move(temp);
    return true;
}

bool AiProcessor::applyAiDetailEnhance(RawProcessing::RawBuffer16& buffer, float strength) {
    if (!buffer.isValid() || strength <= 0.001f) return false;

    strength = std::clamp(strength, 0.0f, 1.0f);
    const uint32_t w = buffer.width();
    const uint32_t h = buffer.height();
    const uint32_t channels = buffer.channels();

    RawProcessing::RawBuffer16 temp(w, h, channels);

    #pragma omp parallel for
    for (int y = 0; y < static_cast<int>(h); ++y) {
        int yPrev = std::clamp(y - 1, 0, static_cast<int>(h - 1));
        int yNext = std::clamp(y + 1, 0, static_cast<int>(h - 1));

        for (uint32_t x = 0; x < w; ++x) {
            int xPrev = std::clamp(static_cast<int>(x) - 1, 0, static_cast<int>(w - 1));
            int xNext = std::clamp(static_cast<int>(x) + 1, 0, static_cast<int>(w - 1));

            for (uint32_t c = 0; c < channels; ++c) {
                float center = buffer.getSample(x, y, c);
                float up = buffer.getSample(x, yPrev, c);
                float down = buffer.getSample(x, yNext, c);
                float left = buffer.getSample(xPrev, y, c);
                float right = buffer.getSample(xNext, y, c);

                float blur = (up + down + left + right) * 0.25f;
                float highPass = center - blur;

                // Thresholding to suppress amplifying fine noise (smart coring)
                float coringThreshold = 0.005f * 65535.0f;
                if (std::abs(highPass) < coringThreshold) {
                    highPass *= (std::abs(highPass) / coringThreshold);
                }

                float enhanced = center + highPass * (strength * 1.5f);
                temp.setSample(x, y, c, static_cast<uint16_t>(std::clamp(enhanced, 0.0f, 65535.0f)));
            }
        }
    }

    buffer = std::move(temp);
    return true;
}

bool AiProcessor::loadOnnxModel(const std::string& modelPath) {
    m_loadedModelPath = modelPath;
    m_modelLoaded = !modelPath.empty();
    return m_modelLoaded;
}

bool AiProcessor::runOnnxInference(const RawProcessing::RawBuffer16& input, RawProcessing::RawBuffer16& output) {
    if (!m_modelLoaded || !input.isValid()) {
        return false;
    }
    output = input;
    return true;
}

} // namespace ImageProcessing
