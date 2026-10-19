#include "../include/RawTypes.h"
#include <cstring>
#include <algorithm>
#include <cmath>

namespace RawProcessing {

RawBuffer16::RawBuffer16() : m_width(0), m_height(0), m_channels(3) {
}

RawBuffer16::RawBuffer16(uint32_t width, uint32_t height, uint32_t channels)
    : m_width(width), m_height(height), m_channels(channels) {
    if (m_width > 0 && m_height > 0 && m_channels > 0) {
        m_data.resize(static_cast<size_t>(m_width) * m_height * m_channels, 0);
    }
}

RawBuffer16::~RawBuffer16() = default;

RawBuffer16::RawBuffer16(const RawBuffer16& other)
    : m_width(other.m_width), m_height(other.m_height), m_channels(other.m_channels), m_data(other.m_data) {
}

RawBuffer16::RawBuffer16(RawBuffer16&& other) noexcept
    : m_width(other.m_width), m_height(other.m_height), m_channels(other.m_channels), m_data(std::move(other.m_data)) {
    other.m_width = 0;
    other.m_height = 0;
    other.m_channels = 0;
}

RawBuffer16& RawBuffer16::operator=(const RawBuffer16& other) {
    if (this != &other) {
        m_width = other.m_width;
        m_height = other.m_height;
        m_channels = other.m_channels;
        m_data = other.m_data;
    }
    return *this;
}

RawBuffer16& RawBuffer16::operator=(RawBuffer16&& other) noexcept {
    if (this != &other) {
        m_width = other.m_width;
        m_height = other.m_height;
        m_channels = other.m_channels;
        m_data = std::move(other.m_data);
        other.m_width = 0;
        other.m_height = 0;
        other.m_channels = 0;
    }
    return *this;
}

void RawBuffer16::allocate(uint32_t width, uint32_t height, uint32_t channels) {
    m_width = width;
    m_height = height;
    m_channels = channels;
    if (width > 0 && height > 0 && channels > 0) {
        m_data.resize(static_cast<size_t>(width) * height * channels);
        std::fill(m_data.begin(), m_data.end(), 0);
    } else {
        m_data.clear();
    }
}

void RawBuffer16::clear() {
    m_data.clear();
    m_width = 0;
    m_height = 0;
    m_channels = 0;
}

std::shared_ptr<RawBuffer16> RawBuffer16::createResampled(uint32_t targetWidth, uint32_t targetHeight) const {
    if (!isValid() || targetWidth == 0 || targetHeight == 0) {
        return nullptr;
    }

    auto result = std::make_shared<RawBuffer16>(targetWidth, targetHeight, m_channels);
    const float xRatio = static_cast<float>(m_width) / targetWidth;
    const float yRatio = static_cast<float>(m_height) / targetHeight;

    #pragma omp parallel for
    for (int y = 0; y < static_cast<int>(targetHeight); ++y) {
        float srcY = (y + 0.5f) * yRatio - 0.5f;
        int y0 = std::clamp(static_cast<int>(std::floor(srcY)), 0, static_cast<int>(m_height - 1));
        int y1 = std::clamp(y0 + 1, 0, static_cast<int>(m_height - 1));
        float yWeight = srcY - y0;

        for (uint32_t x = 0; x < targetWidth; ++x) {
            float srcX = (x + 0.5f) * xRatio - 0.5f;
            int x0 = std::clamp(static_cast<int>(std::floor(srcX)), 0, static_cast<int>(m_width - 1));
            int x1 = std::clamp(x0 + 1, 0, static_cast<int>(m_width - 1));
            float xWeight = srcX - x0;

            for (uint32_t c = 0; c < m_channels; ++c) {
                float v00 = getSample(x0, y0, c);
                float v10 = getSample(x1, y0, c);
                float v01 = getSample(x0, y1, c);
                float v11 = getSample(x1, y1, c);

                float top = v00 * (1.0f - xWeight) + v10 * xWeight;
                float bottom = v01 * (1.0f - xWeight) + v11 * xWeight;
                float interpolated = top * (1.0f - yWeight) + bottom * yWeight;

                uint16_t outVal = static_cast<uint16_t>(std::clamp(interpolated, 0.0f, 65535.0f));
                result->setSample(x, y, c, outVal);
            }
        }
    }

    return result;
}

} // namespace RawProcessing
