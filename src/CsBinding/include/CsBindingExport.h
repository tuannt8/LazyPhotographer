#pragma once

#include <cstdint>

#ifdef CSBINDING_EXPORTS
#define CS_API extern "C" __declspec(dllexport)
#else
#define CS_API extern "C" __declspec(dllimport)
#endif

#pragma pack(push, 8)

struct NativeRawInfo {
    wchar_t cameraMake[64];
    wchar_t cameraModel[64];
    wchar_t exposureTimeString[32];
    uint32_t iso;
    float fNumber;
    float focalLength;
    uint32_t width;
    uint32_t height;
    uint32_t bitDepth;
    int32_t hasEmbeddedPreview;
    uint32_t previewWidth;
    uint32_t previewHeight;
};

struct NativeAdjustments {
    float exposure;
    float temperature;
    float tint;
    float contrast;
    float highlights;
    float shadows;
    float whites;
    float blacks;
    float saturation;
    float vibrance;
    float sharpness;
    float denoise;
    int32_t enableAiAutoTone;
    float aiDenoiseStrength;
    int32_t enableAiDetailEnhance;
};

struct NativeHistogram {
    uint32_t rBins[256];
    uint32_t gBins[256];
    uint32_t bBins[256];
    uint32_t lumaBins[256];
    uint32_t maxCount;
};

struct NativeExportOptions {
    int32_t format;       // 0 = Jpeg, 1 = Png, 2 = Tiff16
    int32_t jpegQuality;  // 1 to 100
    uint32_t maxDimension;// 0 = keep original
    int32_t bitDepth16;   // 1 for 16-bit
};

#pragma pack(pop)

CS_API void* LazyPhoto_CreateEngine();
CS_API void LazyPhoto_DestroyEngine(void* engine);

CS_API int32_t LazyPhoto_LoadRaw(void* engine, const wchar_t* filePath, NativeRawInfo* outInfo, int32_t halfSize);
CS_API int32_t LazyPhoto_ExtractEmbeddedPreview(const wchar_t* filePath, uint8_t* outJpegBuffer, uint32_t maxBufferSize, uint32_t* outActualSize, uint32_t* outWidth, uint32_t* outHeight);

CS_API int32_t LazyPhoto_RenderToRgba8(void* engine, const NativeAdjustments* adj, uint8_t* outRgbaBuffer, uint32_t width, uint32_t height, uint32_t stride);
CS_API int32_t LazyPhoto_ComputeHistogram(void* engine, const NativeAdjustments* adj, NativeHistogram* outHist);
CS_API int32_t LazyPhoto_AnalyzeAutoTone(void* engine, NativeAdjustments* outAdj);

CS_API int32_t LazyPhoto_ExportImage(void* engine, const wchar_t* outputPath, const NativeAdjustments* adj, const NativeExportOptions* options);

CS_API int32_t LazyPhoto_CreateSampleRaf(const wchar_t* outputPath, const wchar_t* cameraModel, uint32_t width, uint32_t height);
CS_API int32_t LazyPhoto_GetLastError(wchar_t* outBuffer, uint32_t maxLen);
