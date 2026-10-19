#pragma once

#include <cstdint>

namespace ImageProcessing {

struct Adjustments {
    // Basic exposure & tone
    float exposure = 0.0f;       // -5.0 to +5.0 EV stops
    float temperature = 0.0f;    // -100 (Cool/Blue) to +100 (Warm/Yellow)
    float tint = 0.0f;           // -100 (Green) to +100 (Magenta)
    float contrast = 0.0f;       // -100 to +100
    float highlights = 0.0f;     // -100 to +100
    float shadows = 0.0f;        // -100 to +100
    float whites = 0.0f;         // -100 to +100
    float blacks = 0.0f;         // -100 to +100

    // Color & presence
    float saturation = 0.0f;     // -100 to +100
    float vibrance = 0.0f;       // -100 to +100
    float sharpness = 0.0f;      // 0 to 100
    float denoise = 0.0f;        // 0 to 100

    // AI Enhancement
    bool enableAiAutoTone = false;
    float aiDenoiseStrength = 0.0f; // 0.0 to 1.0
    bool enableAiDetailEnhance = false;
};

struct HistogramData {
    uint32_t rBins[256] = {0};
    uint32_t gBins[256] = {0};
    uint32_t bBins[256] = {0};
    uint32_t lumaBins[256] = {0};
    uint32_t maxCount = 0;
};

enum class ExportFormat {
    Jpeg = 0,
    Png = 1,
    Tiff16 = 2
};

struct ExportOptions {
    ExportFormat format = ExportFormat::Jpeg;
    int jpegQuality = 90;        // 1 to 100
    uint32_t maxDimension = 0;   // 0 = keep original, or e.g. 2048
    bool bitDepth16 = false;     // For TIFF/PNG
};

} // namespace ImageProcessing
