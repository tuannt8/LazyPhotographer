#include "../include/CsBindingExport.h"
#include "../../ImageProcessing/include/ImageProcessor.h"
#include "../../ImageProcessing/include/Exporter.h"
#include "../../RawProcessing/include/RafReader.h"
#include <memory>
#include <string>
#include <vector>
#include <windows.h>

namespace {

thread_local std::wstring g_lastError;

void setLastError(const std::wstring& err) {
    g_lastError = err;
}

std::string toUtf8(const wchar_t* wstr) {
    if (!wstr || !*wstr) return std::string();
    int size = WideCharToMultiByte(CP_UTF8, 0, wstr, -1, nullptr, 0, nullptr, nullptr);
    if (size <= 0) return std::string();
    std::string result(size - 1, 0);
    WideCharToMultiByte(CP_UTF8, 0, wstr, -1, &result[0], size, nullptr, nullptr);
    return result;
}

std::wstring toWide(const std::string& str) {
    if (str.empty()) return std::wstring();
    int size = MultiByteToWideChar(CP_UTF8, 0, str.c_str(), -1, nullptr, 0);
    if (size <= 0) return std::wstring();
    std::wstring result(size - 1, 0);
    MultiByteToWideChar(CP_UTF8, 0, str.c_str(), -1, &result[0], size);
    return result;
}

struct EngineContext {
    std::unique_ptr<RawProcessing::RafReader> rawReader;
    std::unique_ptr<ImageProcessing::ImageProcessor> imageProcessor;
    RawProcessing::RawMetadata metadata;
    std::shared_ptr<RawProcessing::RawBuffer16> currentRawBuffer;

    EngineContext() {
        rawReader = std::make_unique<RawProcessing::RafReader>();
        imageProcessor = std::make_unique<ImageProcessing::ImageProcessor>();
    }
};

void convertAdjustments(const NativeAdjustments& src, ImageProcessing::Adjustments& dst) {
    dst.exposure = src.exposure;
    dst.temperature = src.temperature;
    dst.tint = src.tint;
    dst.contrast = src.contrast;
    dst.highlights = src.highlights;
    dst.shadows = src.shadows;
    dst.whites = src.whites;
    dst.blacks = src.blacks;
    dst.saturation = src.saturation;
    dst.vibrance = src.vibrance;
    dst.sharpness = src.sharpness;
    dst.denoise = src.denoise;
    dst.enableAiAutoTone = (src.enableAiAutoTone != 0);
    dst.aiDenoiseStrength = src.aiDenoiseStrength;
    dst.enableAiDetailEnhance = (src.enableAiDetailEnhance != 0);
}

void convertAdjustments(const ImageProcessing::Adjustments& src, NativeAdjustments& dst) {
    dst.exposure = src.exposure;
    dst.temperature = src.temperature;
    dst.tint = src.tint;
    dst.contrast = src.contrast;
    dst.highlights = src.highlights;
    dst.shadows = src.shadows;
    dst.whites = src.whites;
    dst.blacks = src.blacks;
    dst.saturation = src.saturation;
    dst.vibrance = src.vibrance;
    dst.sharpness = src.sharpness;
    dst.denoise = src.denoise;
    dst.enableAiAutoTone = src.enableAiAutoTone ? 1 : 0;
    dst.aiDenoiseStrength = src.aiDenoiseStrength;
    dst.enableAiDetailEnhance = src.enableAiDetailEnhance ? 1 : 0;
}

} // anonymous namespace

CS_API void* LazyPhoto_CreateEngine() {
    try {
        return new EngineContext();
    } catch (...) {
        return nullptr;
    }
}

CS_API void LazyPhoto_DestroyEngine(void* engine) {
    if (engine) {
        delete static_cast<EngineContext*>(engine);
    }
}

CS_API int32_t LazyPhoto_LoadRaw(void* engine, const wchar_t* filePath, NativeRawInfo* outInfo, int32_t halfSize) {
    if (!engine || !filePath || !outInfo) {
        setLastError(L"Invalid parameters to LazyPhoto_LoadRaw");
        return 0;
    }

    auto* ctx = static_cast<EngineContext*>(engine);
    std::string pathUtf8 = toUtf8(filePath);

    try {
        if (!ctx->rawReader->canRead(pathUtf8)) {
            setLastError(L"File is not a supported RAW format");
            return 0;
        }

        RawProcessing::RawMetadata meta;
        if (!ctx->rawReader->readMetadata(pathUtf8, meta)) {
            setLastError(L"Failed to read RAW metadata");
            return 0;
        }

        auto rawBuffer = std::make_shared<RawProcessing::RawBuffer16>();
        if (!ctx->rawReader->decodeRaw16(pathUtf8, *rawBuffer, meta, halfSize != 0)) {
            setLastError(L"Failed to decode RAW sensor/preview data");
            return 0;
        }

        ctx->metadata = meta;
        ctx->currentRawBuffer = rawBuffer;
        ctx->imageProcessor->setSourceBuffer(rawBuffer);

        // Populate outInfo
        std::memset(outInfo, 0, sizeof(NativeRawInfo));
        std::wstring wMake = toWide(meta.cameraMake);
        std::wstring wModel = toWide(meta.cameraModel);
        std::wstring wExp = toWide(meta.exposureTimeString);

        wcsncpy_s(outInfo->cameraMake, wMake.c_str(), _TRUNCATE);
        wcsncpy_s(outInfo->cameraModel, wModel.c_str(), _TRUNCATE);
        wcsncpy_s(outInfo->exposureTimeString, wExp.c_str(), _TRUNCATE);

        outInfo->iso = meta.iso;
        outInfo->fNumber = meta.fNumber;
        outInfo->focalLength = meta.focalLength;
        outInfo->width = rawBuffer->width();
        outInfo->height = rawBuffer->height();
        outInfo->bitDepth = meta.bitDepth;
        outInfo->hasEmbeddedPreview = meta.hasEmbeddedPreview ? 1 : 0;
        outInfo->previewWidth = meta.previewWidth;
        outInfo->previewHeight = meta.previewHeight;

        return 1;
    } catch (const std::exception& ex) {
        setLastError(toWide(ex.what()));
        return 0;
    } catch (...) {
        setLastError(L"Unknown exception during LazyPhoto_LoadRaw");
        return 0;
    }
}

CS_API int32_t LazyPhoto_ExtractEmbeddedPreview(const wchar_t* filePath, uint8_t* outJpegBuffer, uint32_t maxBufferSize, uint32_t* outActualSize, uint32_t* outWidth, uint32_t* outHeight) {
    if (!filePath || !outActualSize) {
        setLastError(L"Invalid parameters to LazyPhoto_ExtractEmbeddedPreview");
        return 0;
    }

    try {
        RawProcessing::RafReader reader;
        std::string pathUtf8 = toUtf8(filePath);

        std::vector<uint8_t> jpegBytes;
        uint32_t w = 0, h = 0;
        if (!reader.extractEmbeddedPreview(pathUtf8, jpegBytes, w, h)) {
            setLastError(L"No embedded preview found in RAW file");
            return 0;
        }

        *outActualSize = static_cast<uint32_t>(jpegBytes.size());
        if (outWidth) *outWidth = w;
        if (outHeight) *outHeight = h;

        if (outJpegBuffer && maxBufferSize >= jpegBytes.size()) {
            std::memcpy(outJpegBuffer, jpegBytes.data(), jpegBytes.size());
        }

        return 1;
    } catch (...) {
        setLastError(L"Exception in LazyPhoto_ExtractEmbeddedPreview");
        return 0;
    }
}

CS_API int32_t LazyPhoto_RenderToRgba8(void* engine, const NativeAdjustments* adj, uint8_t* outRgbaBuffer, uint32_t width, uint32_t height, uint32_t stride) {
    if (!engine || !adj || !outRgbaBuffer || width == 0 || height == 0) {
        setLastError(L"Invalid parameters to LazyPhoto_RenderToRgba8");
        return 0;
    }

    auto* ctx = static_cast<EngineContext*>(engine);
    try {
        ImageProcessing::Adjustments adjustments;
        convertAdjustments(*adj, adjustments);

        if (!ctx->imageProcessor->renderToRgba8(adjustments, outRgbaBuffer, width, height, stride)) {
            setLastError(L"Failed to render to RGBA8 buffer");
            return 0;
        }
        return 1;
    } catch (...) {
        setLastError(L"Exception in LazyPhoto_RenderToRgba8");
        return 0;
    }
}

CS_API int32_t LazyPhoto_ComputeHistogram(void* engine, const NativeAdjustments* adj, NativeHistogram* outHist) {
    if (!engine || !adj || !outHist) {
        setLastError(L"Invalid parameters to LazyPhoto_ComputeHistogram");
        return 0;
    }

    auto* ctx = static_cast<EngineContext*>(engine);
    try {
        ImageProcessing::Adjustments adjustments;
        convertAdjustments(*adj, adjustments);

        ImageProcessing::HistogramData hist;
        if (!ctx->imageProcessor->computeHistogram(adjustments, hist)) {
            setLastError(L"Failed to compute histogram");
            return 0;
        }

        std::memcpy(outHist->rBins, hist.rBins, sizeof(hist.rBins));
        std::memcpy(outHist->gBins, hist.gBins, sizeof(hist.gBins));
        std::memcpy(outHist->bBins, hist.bBins, sizeof(hist.bBins));
        std::memcpy(outHist->lumaBins, hist.lumaBins, sizeof(hist.lumaBins));
        outHist->maxCount = hist.maxCount;

        return 1;
    } catch (...) {
        setLastError(L"Exception in LazyPhoto_ComputeHistogram");
        return 0;
    }
}

CS_API int32_t LazyPhoto_AnalyzeAutoTone(void* engine, NativeAdjustments* outAdj) {
    if (!engine || !outAdj) {
        setLastError(L"Invalid parameters to LazyPhoto_AnalyzeAutoTone");
        return 0;
    }

    auto* ctx = static_cast<EngineContext*>(engine);
    if (!ctx->currentRawBuffer || !ctx->currentRawBuffer->isValid()) {
        setLastError(L"No valid raw buffer loaded to analyze");
        return 0;
    }

    try {
        ImageProcessing::Adjustments adjustments;
        if (!ctx->imageProcessor->getAiProcessor().analyzeAndAutoTone(*ctx->currentRawBuffer, adjustments)) {
            setLastError(L"Auto tone analysis failed");
            return 0;
        }

        convertAdjustments(adjustments, *outAdj);
        return 1;
    } catch (...) {
        setLastError(L"Exception in LazyPhoto_AnalyzeAutoTone");
        return 0;
    }
}

CS_API int32_t LazyPhoto_ExportImage(void* engine, const wchar_t* outputPath, const NativeAdjustments* adj, const NativeExportOptions* options) {
    if (!engine || !outputPath || !adj || !options) {
        setLastError(L"Invalid parameters to LazyPhoto_ExportImage");
        return 0;
    }

    auto* ctx = static_cast<EngineContext*>(engine);
    if (!ctx->currentRawBuffer || !ctx->currentRawBuffer->isValid()) {
        setLastError(L"No raw image loaded for export");
        return 0;
    }

    try {
        ImageProcessing::Adjustments adjustments;
        convertAdjustments(*adj, adjustments);

        RawProcessing::RawBuffer16 processedBuffer;
        if (!ctx->imageProcessor->renderToBuffer16(adjustments, processedBuffer)) {
            setLastError(L"Failed to render 16-bit buffer for export");
            return 0;
        }

        ImageProcessing::ExportOptions expOpts;
        expOpts.format = static_cast<ImageProcessing::ExportFormat>(options->format);
        expOpts.jpegQuality = options->jpegQuality;
        expOpts.maxDimension = options->maxDimension;
        expOpts.bitDepth16 = (options->bitDepth16 != 0);

        std::string pathUtf8 = toUtf8(outputPath);
        if (!ImageProcessing::Exporter::exportToFile(processedBuffer, pathUtf8, expOpts)) {
            setLastError(L"Failed to write exported file to disk");
            return 0;
        }

        return 1;
    } catch (...) {
        setLastError(L"Exception during LazyPhoto_ExportImage");
        return 0;
    }
}

CS_API int32_t LazyPhoto_CreateSampleRaf(const wchar_t* outputPath, const wchar_t* cameraModel, uint32_t width, uint32_t height) {
    if (!outputPath) return 0;
    try {
        std::string outPathUtf8 = toUtf8(outputPath);
        std::string modelUtf8 = cameraModel ? toUtf8(cameraModel) : "X-T5";
        return RawProcessing::RafReader::createSampleRaf(outPathUtf8, modelUtf8, width, height) ? 1 : 0;
    } catch (...) {
        return 0;
    }
}

CS_API int32_t LazyPhoto_GetLastError(wchar_t* outBuffer, uint32_t maxLen) {
    if (!outBuffer || maxLen == 0) return 0;
    wcsncpy_s(outBuffer, maxLen, g_lastError.c_str(), _TRUNCATE);
    return 1;
}
