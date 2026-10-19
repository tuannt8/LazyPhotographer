#pragma once

#include "RawTypes.h"
#include <string>
#include <vector>
#include <memory>

namespace RawProcessing {

class IRawReader {
public:
    virtual ~IRawReader() = default;

    // Checks if the file is supported by this reader
    virtual bool canRead(const std::string& filePath) = 0;

    // Reads metadata without decoding full raw sensor data
    virtual bool readMetadata(const std::string& filePath, RawMetadata& outMetadata) = 0;

    // Extracts embedded preview/thumbnail JPEG bytes (super fast for filtering/grid)
    virtual bool extractEmbeddedPreview(const std::string& filePath, std::vector<uint8_t>& outJpegBytes, uint32_t& outWidth, uint32_t& outHeight) = 0;

    // Decodes raw data into 16-bit per channel RGB buffer (0..65535 per channel)
    // If halfSize is true, performs fast binned/half-resolution decoding for interactive editing
    virtual bool decodeRaw16(const std::string& filePath, RawBuffer16& outBuffer, const RawMetadata& metadata, bool halfSize = false) = 0;
};

} // namespace RawProcessing
