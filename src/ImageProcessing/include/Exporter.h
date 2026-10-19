#pragma once

#include "ImageAdjustments.h"
#include "../../RawProcessing/include/RawTypes.h"
#include <string>

namespace ImageProcessing {

class Exporter {
public:
    Exporter();
    ~Exporter();

    // Exports buffer to file on disk (JPEG, PNG, or 16-bit TIFF)
    static bool exportToFile(
        const RawProcessing::RawBuffer16& buffer,
        const std::string& outputPath,
        const ExportOptions& options
    );
};

} // namespace ImageProcessing
