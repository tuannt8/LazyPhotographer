#pragma once

#include "ImageAdjustments.h"
#include "AiProcessor.h"
#include "../../RawProcessing/include/RawTypes.h"
#include <memory>
#include <vector>

namespace ImageProcessing {

class ImageProcessor {
public:
    ImageProcessor();
    ~ImageProcessor();

    // Sets the raw image buffer to be processed
    void setSourceBuffer(std::shared_ptr<RawProcessing::RawBuffer16> sourceBuffer);
    std::shared_ptr<RawProcessing::RawBuffer16> getSourceBuffer() const { return m_sourceBuffer; }

    // Renders the adjusted image into an 8-bit RGBA buffer for immediate UI display
    bool renderToRgba8(const Adjustments& adj, uint8_t* outRgbaBuffer, uint32_t width, uint32_t height, uint32_t stride);

    // Renders the adjusted image into a 16-bit RGB/RGBA buffer for high bit-depth export
    bool renderToBuffer16(const Adjustments& adj, RawProcessing::RawBuffer16& outBuffer);

    // Calculates 256-bin histogram for R, G, B, and Luminance channels
    bool computeHistogram(const Adjustments& adj, HistogramData& outHistogram);

    // AI Processor access
    AiProcessor& getAiProcessor() { return m_aiProcessor; }

private:
    std::shared_ptr<RawProcessing::RawBuffer16> m_sourceBuffer;
    AiProcessor m_aiProcessor;

    // Helper functions for tone processing
    static inline float applyExposure(float val, float ev);
    static inline float applyToneCurve(float val, float contrast, float highlights, float shadows, float whites, float blacks);
    static inline void rgbToHsl(float r, float g, float b, float& h, float& s, float& l);
    static inline void hslToRgb(float h, float s, float l, float& r, float& g, float& b);
};

} // namespace ImageProcessing
