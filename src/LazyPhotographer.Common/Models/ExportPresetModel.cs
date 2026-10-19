namespace LazyPhotographer.Models
{
    public class ExportPresetModel
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ExportFileFormat Format { get; set; } = ExportFileFormat.Jpeg;
        public int JpegQuality { get; set; } = 90;
        public uint MaxDimension { get; set; } = 0; // 0 = Full resolution
        public bool BitDepth16 { get; set; } = false;

        public static ExportPresetModel[] GetDefaultPresets()
        {
            return new[]
            {
                new ExportPresetModel
                {
                    Name = "16-Bit Master TIFF (Archive / Print)",
                    Description = "Full resolution uncompressed 16-bit RGB TIFF with maximum fidelity",
                    Format = ExportFileFormat.Tiff16,
                    MaxDimension = 0,
                    BitDepth16 = true
                },
                new ExportPresetModel
                {
                    Name = "High Quality JPEG (Print / Display)",
                    Description = "Full resolution 100% quality JPEG in sRGB color space",
                    Format = ExportFileFormat.Jpeg,
                    JpegQuality = 100,
                    MaxDimension = 0,
                    BitDepth16 = false
                },
                new ExportPresetModel
                {
                    Name = "Web & Social (2048px, 85% JPEG)",
                    Description = "Optimized for Instagram, web portfolios, and fast sharing",
                    Format = ExportFileFormat.Jpeg,
                    JpegQuality = 85,
                    MaxDimension = 2048,
                    BitDepth16 = false
                },
                new ExportPresetModel
                {
                    Name = "Lossless PNG",
                    Description = "Lossless 8-bit PNG graphic format",
                    Format = ExportFileFormat.Png,
                    MaxDimension = 0,
                    BitDepth16 = false
                }
            };
        }
    }
}
