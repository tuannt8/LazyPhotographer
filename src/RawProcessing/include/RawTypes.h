#pragma once

#include <cstdint>
#include <string>
#include <vector>
#include <memory>

namespace RawProcessing {

enum class CfaPattern {
    Unknown = 0,
    BayerRGGB,
    BayerBGGR,
    BayerGRBG,
    BayerGBRG,
    FujiXTrans6x6
};

struct RawMetadata {
    std::string cameraMake;
    std::string cameraModel;
    std::string software;
    std::string dateTime;
    uint32_t iso = 100;
    float exposureTime = 0.01f;      // seconds (e.g. 1/100)
    std::string exposureTimeString;  // e.g. "1/250s"
    float fNumber = 2.8f;            // Aperture e.g. f/2.8
    float focalLength = 35.0f;       // mm
    
    uint32_t rawWidth = 0;
    uint32_t rawHeight = 0;
    uint32_t bitDepth = 14;          // 14 or 16 bits raw
    
    CfaPattern pattern = CfaPattern::Unknown;
    
    float wbGainR = 1.0f;
    float wbGainG = 1.0f;
    float wbGainB = 1.0f;
    
    uint32_t blackLevel = 1024;
    uint32_t whiteLevel = 16383;     // 14-bit max
    
    // Embedded preview metadata
    bool hasEmbeddedPreview = false;
    uint64_t previewOffset = 0;
    uint32_t previewLength = 0;
    uint32_t previewWidth = 0;
    uint32_t previewHeight = 0;
};

// 16-bit linear/gamma image buffer (0..65535 per channel)
class RawBuffer16 {
public:
    RawBuffer16();
    RawBuffer16(uint32_t width, uint32_t height, uint32_t channels = 3);
    ~RawBuffer16();

    // Move and copy semantics
    RawBuffer16(const RawBuffer16& other);
    RawBuffer16(RawBuffer16&& other) noexcept;
    RawBuffer16& operator=(const RawBuffer16& other);
    RawBuffer16& operator=(RawBuffer16&& other) noexcept;

    void allocate(uint32_t width, uint32_t height, uint32_t channels = 3);
    void clear();

    uint32_t width() const { return m_width; }
    uint32_t height() const { return m_height; }
    uint32_t channels() const { return m_channels; }
    uint32_t stride() const { return m_width * m_channels * sizeof(uint16_t); }
    size_t pixelCount() const { return static_cast<size_t>(m_width) * m_height; }
    size_t byteSize() const { return pixelCount() * m_channels * sizeof(uint16_t); }

    uint16_t* data() { return m_data.data(); }
    const uint16_t* data() const { return m_data.data(); }

    bool isValid() const { return !m_data.empty() && m_width > 0 && m_height > 0; }

    uint16_t getSample(uint32_t x, uint32_t y, uint32_t c) const {
        if (x >= m_width || y >= m_height || c >= m_channels) return 0;
        return m_data[(y * m_width + x) * m_channels + c];
    }

    void setSample(uint32_t x, uint32_t y, uint32_t c, uint16_t val) {
        if (x < m_width && y < m_height && c < m_channels) {
            m_data[(y * m_width + x) * m_channels + c] = val;
        }
    }

    // High quality downsampling for fast preview creation
    std::shared_ptr<RawBuffer16> createResampled(uint32_t targetWidth, uint32_t targetHeight) const;

private:
    uint32_t m_width = 0;
    uint32_t m_height = 0;
    uint32_t m_channels = 3; // 3 for RGB, 4 for RGBA
    std::vector<uint16_t> m_data;
};

} // namespace RawProcessing
