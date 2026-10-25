using System;
using System.IO;
using System.Text;

namespace LazyPhotographer.Common.Metadata
{
    public class FastPhotoMetadata
    {
        public string CameraMake { get; set; } = "FUJIFILM";
        public string CameraModel { get; set; } = "FUJIFILM";
        public string ExposureInfo { get; set; } = "1/250s f/2.8 ISO 160";
        public uint Width { get; set; }
        public uint Height { get; set; }
        public int OrientationTag { get; set; } = 1;
        public int OrientationDegrees { get; set; } = 0;
    }

    public static class FastMetadataReader
    {
        public static FastPhotoMetadata ReadMetadata(string filePath)
        {
            var meta = new FastPhotoMetadata();

            try
            {
                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096);

                if (ext == ".raf")
                {
                    ReadRafMetadata(fs, meta);
                }
                else if (ext == ".jpg" || ext == ".jpeg")
                {
                    ReadJpegMetadata(fs, meta);
                }
                else if (ext == ".png")
                {
                    ReadPngMetadata(fs, meta);
                }
                else if (ext == ".tif" || ext == ".tiff")
                {
                    ReadTiffMetadata(fs, meta);
                }
            }
            catch
            {
                // Fall back to defaults
            }

            meta.OrientationDegrees = OrientationTagToDegrees(meta.OrientationTag);

            if (string.IsNullOrWhiteSpace(meta.CameraModel))
            {
                meta.CameraModel = "FUJIFILM";
            }

            if (string.IsNullOrWhiteSpace(meta.ExposureInfo))
            {
                meta.ExposureInfo = "1/250s f/2.8 ISO 160";
            }

            return meta;
        }

        public static int OrientationTagToDegrees(int orientationTag)
        {
            return orientationTag switch
            {
                3 => 180,
                6 => 90,
                8 => 270,
                _ => 0
            };
        }

        private static void ReadRafMetadata(FileStream fs, FastPhotoMetadata meta)
        {
            if (fs.Length < 120) return;

            byte[] header = new byte[120];
            int read = fs.Read(header, 0, header.Length);
            if (read < 120) return;

            string magic = Encoding.ASCII.GetString(header, 0, 16);
            if (!magic.StartsWith("FUJIFILMCCD-RAW")) return;

            // Camera model at offset 28..59
            string model = Encoding.ASCII.GetString(header, 28, 32).Trim('\0', ' ');
            if (!string.IsNullOrEmpty(model))
            {
                meta.CameraModel = model;
                meta.CameraMake = "FUJIFILM";
            }

            // Preview JPEG offset & length at bytes 84 and 88 (Big Endian)
            uint previewOffset = ReadBigEndian32(header, 84);
            uint previewLength = ReadBigEndian32(header, 88);

            if (previewOffset > 0 && previewOffset < fs.Length && previewLength > 100)
            {
                fs.Seek(previewOffset, SeekOrigin.Begin);
                int previewChunkSize = (int)Math.Min((long)previewLength, 65536);
                byte[] previewBuffer = new byte[previewChunkSize];
                int previewRead = fs.Read(previewBuffer, 0, previewChunkSize);
                if (previewRead > 4)
                {
                    using var ms = new MemoryStream(previewBuffer, 0, previewRead);
                    ReadJpegMetadataFromStream(ms, meta);
                }
            }
        }

        private static void ReadJpegMetadata(FileStream fs, FastPhotoMetadata meta)
        {
            ReadJpegMetadataFromStream(fs, meta);
        }

        private static void ReadJpegMetadataFromStream(Stream stream, FastPhotoMetadata meta)
        {
            int b1 = stream.ReadByte();
            int b2 = stream.ReadByte();
            if (b1 != 0xFF || b2 != 0xD8) return; // Not JPEG SOI

            string? make = null;
            string? model = null;
            string? exposureTime = null;
            string? fNumber = null;
            string? iso = null;

            while (stream.Position < stream.Length - 4)
            {
                int marker1 = stream.ReadByte();
                while (marker1 != 0xFF && stream.Position < stream.Length)
                {
                    marker1 = stream.ReadByte();
                }

                int marker2 = stream.ReadByte();
                while (marker2 == 0xFF && stream.Position < stream.Length)
                {
                    marker2 = stream.ReadByte();
                }

                if (marker2 == 0xD9 || marker2 == 0xDA) // EOI or SOS
                {
                    break;
                }

                int lenHi = stream.ReadByte();
                int lenLo = stream.ReadByte();
                if (lenHi < 0 || lenLo < 0) break;
                int segLen = (lenHi << 8) | lenLo;
                if (segLen < 2) break;
                int dataLen = segLen - 2;

                // APP1 (EXIF)
                if (marker2 == 0xE1 && dataLen > 6)
                {
                    byte[] app1 = new byte[dataLen];
                    int read = stream.Read(app1, 0, dataLen);
                    if (read == dataLen && Encoding.ASCII.GetString(app1, 0, 4) == "Exif")
                    {
                        ParseExifApp1(app1, meta, ref make, ref model, ref exposureTime, ref fNumber, ref iso);
                    }
                }
                // SOF0 (0xC0) or SOF2 (0xC2) for dimensions
                else if ((marker2 == 0xC0 || marker2 == 0xC2) && dataLen >= 5)
                {
                    byte[] sof = new byte[dataLen];
                    int read = stream.Read(sof, 0, dataLen);
                    if (read == dataLen && meta.Width == 0 && meta.Height == 0)
                    {
                        meta.Height = (uint)((sof[1] << 8) | sof[2]);
                        meta.Width = (uint)((sof[3] << 8) | sof[4]);
                    }
                }
                else
                {
                    stream.Seek(dataLen, SeekOrigin.Current);
                }

                // If we found both EXIF and dimensions, we can stop reading
                if (meta.Width > 0 && meta.Height > 0 && meta.OrientationTag > 1)
                {
                    break;
                }
            }

            if (!string.IsNullOrEmpty(model)) meta.CameraModel = model;
            if (!string.IsNullOrEmpty(make)) meta.CameraMake = make;

            BuildExposureString(meta, exposureTime, fNumber, iso);
        }

        private static void ParseExifApp1(
            byte[] data,
            FastPhotoMetadata meta,
            ref string? make,
            ref string? model,
            ref string? exposureTime,
            ref string? fNumber,
            ref string? iso)
        {
            // EXIF starts after "Exif\0\0" (6 bytes)
            int tiffOffset = 6;
            if (data.Length < tiffOffset + 8) return;

            bool isLittleEndian = data[tiffOffset] == 'I' && data[tiffOffset + 1] == 'I';
            bool isBigEndian = data[tiffOffset] == 'M' && data[tiffOffset + 1] == 'M';
            if (!isLittleEndian && !isBigEndian) return;

            ushort magic = ReadUInt16(data, tiffOffset + 2, isLittleEndian);
            if (magic != 42) return;

            uint ifd0Offset = ReadUInt32(data, tiffOffset + 4, isLittleEndian);
            int currentIfd = tiffOffset + (int)ifd0Offset;
            if (currentIfd + 2 > data.Length) return;

            ushort numEntries = ReadUInt16(data, currentIfd, isLittleEndian);
            currentIfd += 2;

            uint exifSubIfdOffset = 0;

            for (int i = 0; i < numEntries && currentIfd + 12 <= data.Length; i++, currentIfd += 12)
            {
                ushort tag = ReadUInt16(data, currentIfd, isLittleEndian);
                ushort type = ReadUInt16(data, currentIfd + 2, isLittleEndian);
                uint count = ReadUInt32(data, currentIfd + 4, isLittleEndian);

                switch (tag)
                {
                    case 0x0112: // Orientation
                        meta.OrientationTag = ReadUInt16(data, currentIfd + 8, isLittleEndian);
                        break;
                    case 0x010F: // Make
                        make = ReadString(data, tiffOffset, currentIfd + 8, count, isLittleEndian);
                        break;
                    case 0x0110: // Model
                        model = ReadString(data, tiffOffset, currentIfd + 8, count, isLittleEndian);
                        break;
                    case 0x8769: // Exif SubIFD
                        exifSubIfdOffset = ReadUInt32(data, currentIfd + 8, isLittleEndian);
                        break;
                }
            }

            // Parse SubIFD if present
            if (exifSubIfdOffset > 0 && tiffOffset + (int)exifSubIfdOffset + 2 <= data.Length)
            {
                int subIfd = tiffOffset + (int)exifSubIfdOffset;
                ushort subEntries = ReadUInt16(data, subIfd, isLittleEndian);
                subIfd += 2;

                for (int i = 0; i < subEntries && subIfd + 12 <= data.Length; i++, subIfd += 12)
                {
                    ushort tag = ReadUInt16(data, subIfd, isLittleEndian);
                    ushort type = ReadUInt16(data, subIfd + 2, isLittleEndian);
                    uint count = ReadUInt32(data, subIfd + 4, isLittleEndian);

                    switch (tag)
                    {
                        case 0x829A: // ExposureTime (RATIONAL)
                            uint expOffset = ReadUInt32(data, subIfd + 8, isLittleEndian);
                            int expPos = tiffOffset + (int)expOffset;
                            if (expPos + 8 <= data.Length)
                            {
                                uint num = ReadUInt32(data, expPos, isLittleEndian);
                                uint den = ReadUInt32(data, expPos + 4, isLittleEndian);
                                if (den > 0)
                                {
                                    if (num == 1 || (den % num == 0 && num != 0))
                                    {
                                        exposureTime = $"1/{den / num}s";
                                    }
                                    else
                                    {
                                        double sec = (double)num / den;
                                        exposureTime = sec < 1.0 ? $"1/{(int)(1.0 / sec)}s" : $"{sec:F1}s";
                                    }
                                }
                            }
                            break;

                        case 0x829D: // FNumber (RATIONAL)
                            uint fOffset = ReadUInt32(data, subIfd + 8, isLittleEndian);
                            int fPos = tiffOffset + (int)fOffset;
                            if (fPos + 8 <= data.Length)
                            {
                                uint num = ReadUInt32(data, fPos, isLittleEndian);
                                uint den = ReadUInt32(data, fPos + 4, isLittleEndian);
                                if (den > 0)
                                {
                                    float fn = (float)num / den;
                                    fNumber = $"f/{fn:0.#}";
                                }
                            }
                            break;

                        case 0x8827: // ISOSpeedRatings
                            ushort isoVal = ReadUInt16(data, subIfd + 8, isLittleEndian);
                            if (isoVal > 0) iso = $"ISO {isoVal}";
                            break;

                        case 0xA002: // PixelXDimension
                            if (meta.Width == 0)
                            {
                                meta.Width = ReadUInt32OrUInt16(data, subIfd + 8, type, isLittleEndian);
                            }
                            break;

                        case 0xA003: // PixelYDimension
                            if (meta.Height == 0)
                            {
                                meta.Height = ReadUInt32OrUInt16(data, subIfd + 8, type, isLittleEndian);
                            }
                            break;
                    }
                }
            }
        }

        private static void BuildExposureString(FastPhotoMetadata meta, string? exp, string? f, string? iso)
        {
            if (string.IsNullOrEmpty(exp) && string.IsNullOrEmpty(f) && string.IsNullOrEmpty(iso)) return;

            string sExp = exp ?? "1/250s";
            string sF = f ?? "f/2.8";
            string sIso = iso ?? "ISO 160";
            meta.ExposureInfo = $"{sExp} {sF} {sIso}";
        }

        private static void ReadPngMetadata(FileStream fs, FastPhotoMetadata meta)
        {
            if (fs.Length < 24) return;
            byte[] header = new byte[24];
            if (fs.Read(header, 0, 24) == 24)
            {
                if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
                {
                    meta.Width = ReadBigEndian32(header, 16);
                    meta.Height = ReadBigEndian32(header, 20);
                    meta.CameraModel = "PNG Image";
                }
            }
        }

        private static void ReadTiffMetadata(FileStream fs, FastPhotoMetadata meta)
        {
            if (fs.Length < 8) return;
            byte[] buf = new byte[Math.Min(fs.Length, 4096)];
            int read = fs.Read(buf, 0, buf.Length);
            if (read < 8) return;

            bool isLittle = buf[0] == 'I' && buf[1] == 'I';
            bool isBig = buf[0] == 'M' && buf[1] == 'M';
            if (!isLittle && !isBig) return;

            ushort magic = ReadUInt16(buf, 2, isLittle);
            if (magic != 42) return;

            uint ifdOffset = ReadUInt32(buf, 4, isLittle);
            int pos = (int)ifdOffset;
            if (pos + 2 > read) return;

            ushort entries = ReadUInt16(buf, pos, isLittle);
            pos += 2;

            for (int i = 0; i < entries && pos + 12 <= read; i++, pos += 12)
            {
                ushort tag = ReadUInt16(buf, pos, isLittle);
                ushort type = ReadUInt16(buf, pos + 2, isLittle);
                if (tag == 0x0100) meta.Width = ReadUInt32OrUInt16(buf, pos + 8, type, isLittle);
                else if (tag == 0x0101) meta.Height = ReadUInt32OrUInt16(buf, pos + 8, type, isLittle);
                else if (tag == 0x0112) meta.OrientationTag = ReadUInt16(buf, pos + 8, isLittle);
            }
        }

        private static ushort ReadUInt16(byte[] data, int offset, bool isLittleEndian)
        {
            if (offset + 2 > data.Length) return 0;
            return isLittleEndian
                ? (ushort)(data[offset] | (data[offset + 1] << 8))
                : (ushort)((data[offset] << 8) | data[offset + 1]);
        }

        private static uint ReadUInt32(byte[] data, int offset, bool isLittleEndian)
        {
            if (offset + 4 > data.Length) return 0;
            return isLittleEndian
                ? (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24))
                : (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
        }

        private static uint ReadUInt32OrUInt16(byte[] data, int offset, ushort type, bool isLittleEndian)
        {
            if (type == 3) // SHORT
            {
                return ReadUInt16(data, offset, isLittleEndian);
            }
            return ReadUInt32(data, offset, isLittleEndian);
        }

        private static uint ReadBigEndian32(byte[] data, int offset)
        {
            if (offset + 4 > data.Length) return 0;
            return ((uint)data[offset] << 24) |
                   ((uint)data[offset + 1] << 16) |
                   ((uint)data[offset + 2] << 8) |
                   data[offset + 3];
        }

        private static string? ReadString(byte[] data, int tiffOffset, int valueOffsetInEntry, uint count, bool isLittleEndian)
        {
            if (count == 0) return null;
            int strOffset = (count <= 4)
                ? valueOffsetInEntry
                : tiffOffset + (int)ReadUInt32(data, valueOffsetInEntry, isLittleEndian);

            if (strOffset < 0 || strOffset + count > data.Length) return null;
            return Encoding.ASCII.GetString(data, strOffset, (int)count).Trim('\0', ' ');
        }
    }
}
