#pragma once

#include "ImageAdjustments.h"
#include "../../RawProcessing/include/RawTypes.h"
#include <string>
#include <memory>

namespace ImageProcessing {

class AiProcessor {
public:
    AiProcessor();
    ~AiProcessor();

    // Scene intelligence: Analyzes dynamic range, entropy, and color cast to suggest optimal adjustments
    bool analyzeAndAutoTone(const RawProcessing::RawBuffer16& input, Adjustments& outAdjustments);

    // AI Denoising: Edge-preserving adaptive spatial and chroma noise filter
    bool applyAiDenoise(RawProcessing::RawBuffer16& buffer, float strength);

    // AI Detail & Texture Enhancer: High-frequency neural unsharp mask without halos
    bool applyAiDetailEnhance(RawProcessing::RawBuffer16& buffer, float strength);

    // Extensible ONNX Runtime hook for deep learning models (Super-res, segmentation, etc.)
    bool loadOnnxModel(const std::string& modelPath);
    bool runOnnxInference(const RawProcessing::RawBuffer16& input, RawProcessing::RawBuffer16& output);
    bool isModelLoaded() const { return m_modelLoaded; }

private:
    bool m_modelLoaded = false;
    std::string m_loadedModelPath;
};

} // namespace ImageProcessing
