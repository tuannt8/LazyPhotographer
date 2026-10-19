#include "../include/Exporter.h"
#include <algorithm>
#include <vector>

#define NOMINMAX
#include <windows.h>
#include <wincodec.h>

#pragma comment(lib, "windowscodecs.lib")
#pragma comment(lib, "ole32.lib")

namespace ImageProcessing {

Exporter::Exporter() = default;
Exporter::~Exporter() = default;

static std::wstring toWide(const std::string& str) {
    if (str.empty()) return std::wstring();
    int sizeNeeded = MultiByteToWideChar(CP_UTF8, 0, str.c_str(), static_cast<int>(str.length()), nullptr, 0);
    std::wstring wstr(sizeNeeded, 0);
    MultiByteToWideChar(CP_UTF8, 0, str.c_str(), static_cast<int>(str.length()), &wstr[0], sizeNeeded);
    return wstr;
}

bool Exporter::exportToFile(
    const RawProcessing::RawBuffer16& buffer,
    const std::string& outputPath,
    const ExportOptions& options
) {
    if (!buffer.isValid() || outputPath.empty()) return false;

    // Resample if maxDimension is specified
    uint32_t srcW = buffer.width();
    uint32_t srcH = buffer.height();
    uint32_t targetW = srcW;
    uint32_t targetH = srcH;

    if (options.maxDimension > 0 && (srcW > options.maxDimension || srcH > options.maxDimension)) {
        if (srcW >= srcH) {
            targetW = options.maxDimension;
            targetH = static_cast<uint32_t>(srcH * (static_cast<float>(options.maxDimension) / srcW));
        } else {
            targetH = options.maxDimension;
            targetW = static_cast<uint32_t>(srcW * (static_cast<float>(options.maxDimension) / srcH));
        }
    }

    std::shared_ptr<RawProcessing::RawBuffer16> workBuffer;
    if (targetW != srcW || targetH != srcH) {
        workBuffer = buffer.createResampled(targetW, targetH);
    } else {
        workBuffer = std::make_shared<RawProcessing::RawBuffer16>(buffer);
    }

    if (!workBuffer || !workBuffer->isValid()) return false;

    HRESULT hr = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    bool coInit = SUCCEEDED(hr);

    IWICImagingFactory* pFactory = nullptr;
    hr = CoCreateInstance(CLSID_WICImagingFactory, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&pFactory));
    if (FAILED(hr) || !pFactory) {
        if (coInit) CoUninitialize();
        return false;
    }

    std::wstring widePath = toWide(outputPath);
    IWICStream* pStream = nullptr;
    hr = pFactory->CreateStream(&pStream);
    if (SUCCEEDED(hr)) {
        hr = pStream->InitializeFromFilename(widePath.c_str(), GENERIC_WRITE);
    }

    GUID containerFormat = GUID_ContainerFormatJpeg;
    if (options.format == ExportFormat::Png) {
        containerFormat = GUID_ContainerFormatPng;
    } else if (options.format == ExportFormat::Tiff16) {
        containerFormat = GUID_ContainerFormatTiff;
    }

    IWICBitmapEncoder* pEncoder = nullptr;
    if (SUCCEEDED(hr)) {
        hr = pFactory->CreateEncoder(containerFormat, nullptr, &pEncoder);
    }

    if (SUCCEEDED(hr)) {
        hr = pEncoder->Initialize(pStream, WICBitmapEncoderNoCache);
    }

    IWICBitmapFrameEncode* pFrame = nullptr;
    IPropertyBag2* pPropertyBag = nullptr;
    if (SUCCEEDED(hr)) {
        hr = pEncoder->CreateNewFrame(&pFrame, &pPropertyBag);
    }

    if (SUCCEEDED(hr) && options.format == ExportFormat::Jpeg && pPropertyBag) {
        PROPBAG2 option = { 0 };
        option.pstrName = const_cast<LPOLESTR>(L"ImageQuality");
        VARIANT varValue;
        VariantInit(&varValue);
        varValue.vt = VT_R4;
        varValue.fltVal = std::clamp(options.jpegQuality / 100.0f, 0.05f, 1.0f);
        pPropertyBag->Write(1, &option, &varValue);
    }

    if (SUCCEEDED(hr)) {
        hr = pFrame->Initialize(pPropertyBag);
    }

    if (SUCCEEDED(hr)) {
        hr = pFrame->SetSize(targetW, targetH);
    }

    bool success = false;
    if (SUCCEEDED(hr)) {
        if (options.format == ExportFormat::Tiff16 || options.bitDepth16) {
            // Write 16-bit 48bpp RGB
            WICPixelFormatGUID pixelFormat = GUID_WICPixelFormat48bppRGB;
            pFrame->SetPixelFormat(&pixelFormat);

            UINT stride = targetW * 3 * sizeof(uint16_t);
            UINT bufSize = stride * targetH;

            // In our buffer, data is uint16_t RGB
            hr = pFrame->WritePixels(targetH, stride, bufSize, reinterpret_cast<BYTE*>(workBuffer->data()));
        } else {
            // Write 8-bit 24bpp BGR (Standard JPEG/PNG)
            WICPixelFormatGUID pixelFormat = GUID_WICPixelFormat24bppBGR;
            pFrame->SetPixelFormat(&pixelFormat);

            UINT stride = targetW * 3;
            UINT bufSize = stride * targetH;
            std::vector<BYTE> bgr8(bufSize);

            #pragma omp parallel for
            for (int y = 0; y < static_cast<int>(targetH); ++y) {
                BYTE* row = bgr8.data() + y * stride;
                for (uint32_t x = 0; x < targetW; ++x) {
                    uint16_t r16 = workBuffer->getSample(x, y, 0);
                    uint16_t g16 = workBuffer->getSample(x, y, 1);
                    uint16_t b16 = workBuffer->getSample(x, y, 2);

                    row[x * 3 + 0] = static_cast<BYTE>(b16 >> 8); // B
                    row[x * 3 + 1] = static_cast<BYTE>(g16 >> 8); // G
                    row[x * 3 + 2] = static_cast<BYTE>(r16 >> 8); // R
                }
            }

            hr = pFrame->WritePixels(targetH, stride, bufSize, bgr8.data());
        }

        if (SUCCEEDED(hr)) {
            hr = pFrame->Commit();
        }
        if (SUCCEEDED(hr)) {
            hr = pEncoder->Commit();
            success = true;
        }
    }

    if (pPropertyBag) pPropertyBag->Release();
    if (pFrame) pFrame->Release();
    if (pEncoder) pEncoder->Release();
    if (pStream) pStream->Release();
    if (pFactory) pFactory->Release();

    if (coInit) CoUninitialize();
    return success;
}

} // namespace ImageProcessing
