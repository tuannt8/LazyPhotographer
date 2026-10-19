#pragma once

#include "IRawReader.h"
#include <string>
#include <vector>

namespace RawProcessing {

class RafReader : public IRawReader {
public:
    RafReader();
    ~RafReader() override;

    bool canRead(const std::string& filePath) override;
    bool readMetadata(const std::string& filePath, RawMetadata& outMetadata) override;
    bool extractEmbeddedPreview(const std::string& filePath, std::vector<uint8_t>& outJpegBytes, uint32_t& outWidth, uint32_t& outHeight) override;
    bool decodeRaw16(const std::string& filePath, RawBuffer16& outBuffer, const RawMetadata& metadata, bool halfSize = false) override;

    // Helper: Creates a synthetic Fuji RAF sample file for testing if needed
    static bool createSampleRaf(const std::string& outputPath, const std::string& cameraModel = "X-T5", uint32_t width = 1200, uint32_t height = 800);

private:
    bool parseRafHeader(const std::vector<uint8_t>& fileBytes, RawMetadata& outMetadata);
    bool extractExifMetadata(const uint8_t* jpegData, size_t jpegLen, RawMetadata& outMetadata);
    bool decodeJpegToBuffer16(const uint8_t* jpegBytes, size_t length, RawBuffer16& outBuffer, bool halfSize);
    void demosaicBayerTo16(const uint16_t* rawCfa, uint32_t width, uint32_t height, RawBuffer16& outBuffer, const RawMetadata& metadata);
};

} // namespace RawProcessing
