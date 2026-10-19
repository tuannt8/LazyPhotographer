using System;

namespace LazyPhotographer.Interop
{
    public sealed class NativeImageBridge : IDisposable
    {
        private IntPtr _engine;
        private bool _disposed;

        public NativeImageBridge()
        {
            _engine = NativeMethods.LazyPhoto_CreateEngine();
        }

        ~NativeImageBridge()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (_engine != IntPtr.Zero)
                {
                    NativeMethods.LazyPhoto_DestroyEngine(_engine);
                    _engine = IntPtr.Zero;
                }
                _disposed = true;
            }
        }

        public bool LoadRaw(string filePath, out NativeRawInfo rawInfo, bool halfSize = true)
        {
            rawInfo = default;
            if (_engine == IntPtr.Zero || string.IsNullOrEmpty(filePath)) return false;

            int res = NativeMethods.LazyPhoto_LoadRaw(_engine, filePath, ref rawInfo, halfSize ? 1 : 0);
            return res != 0;
        }

        public static byte[]? ExtractEmbeddedPreview(string filePath, out uint width, out uint height)
        {
            width = 0;
            height = 0;

            int res = NativeMethods.LazyPhoto_ExtractEmbeddedPreview(filePath, null, 0, out uint actualSize, out width, out height);
            if (res == 0 || actualSize == 0) return null;

            byte[] buffer = new byte[actualSize];
            res = NativeMethods.LazyPhoto_ExtractEmbeddedPreview(filePath, buffer, actualSize, out actualSize, out width, out height);
            return res != 0 ? buffer : null;
        }

        public byte[]? RenderPreviewBmpBytes(NativeAdjustments adjustments, uint width, uint height)
        {
            if (_engine == IntPtr.Zero || width == 0 || height == 0) return null;

            uint stride = width * 4;
            byte[] rgbaBuffer = new byte[stride * height];

            int res = NativeMethods.LazyPhoto_RenderToRgba8(_engine, ref adjustments, rgbaBuffer, width, height, stride);
            if (res == 0) return null;

            return CreateBmpStream(rgbaBuffer, (int)width, (int)height);
        }

        public bool ComputeHistogram(NativeAdjustments adjustments, out NativeHistogram hist)
        {
            hist = default;
            if (_engine == IntPtr.Zero) return false;
            return NativeMethods.LazyPhoto_ComputeHistogram(_engine, ref adjustments, out hist) != 0;
        }

        public bool AnalyzeAutoTone(ref NativeAdjustments adjustments)
        {
            if (_engine == IntPtr.Zero) return false;
            return NativeMethods.LazyPhoto_AnalyzeAutoTone(_engine, ref adjustments) != 0;
        }

        public bool ExportImage(string outputPath, NativeAdjustments adjustments, NativeExportOptions options)
        {
            if (_engine == IntPtr.Zero) return false;
            return NativeMethods.LazyPhoto_ExportImage(_engine, outputPath, ref adjustments, ref options) != 0;
        }

        public static bool CreateSampleRaf(string outputPath, string cameraModel, uint width, uint height)
        {
            return NativeMethods.LazyPhoto_CreateSampleRaf(outputPath, cameraModel, width, height) != 0;
        }

        public static string GetLastError()
        {
            char[] buffer = new char[512];
            if (NativeMethods.LazyPhoto_GetLastError(buffer, (uint)buffer.Length) != 0)
            {
                return new string(buffer).TrimEnd('\0');
            }
            return string.Empty;
        }

        // Creates a valid 32-bpp top-down BMP stream from RGBA buffer
        public static byte[] CreateBmpStream(byte[] rgba, int width, int height)
        {
            int imageSize = width * height * 4;
            int totalSize = 54 + imageSize;
            byte[] bmp = new byte[totalSize];

            // BITMAPFILEHEADER
            bmp[0] = (byte)'B';
            bmp[1] = (byte)'M';
            BitConverter.GetBytes(totalSize).CopyTo(bmp, 2);
            BitConverter.GetBytes(54).CopyTo(bmp, 10); // Offset to pixel data

            // BITMAPINFOHEADER
            BitConverter.GetBytes(40).CopyTo(bmp, 14); // Header size
            BitConverter.GetBytes(width).CopyTo(bmp, 18);
            BitConverter.GetBytes(-height).CopyTo(bmp, 22); // Negative height for top-down orientation
            BitConverter.GetBytes((short)1).CopyTo(bmp, 26);  // Color planes
            BitConverter.GetBytes((short)32).CopyTo(bmp, 28); // 32 bits per pixel
            BitConverter.GetBytes(0).CopyTo(bmp, 30);         // BI_RGB compression
            BitConverter.GetBytes(imageSize).CopyTo(bmp, 34);

            // Copy pixels converting RGBA to BGRA
            for (int i = 0; i < imageSize; i += 4)
            {
                byte r = rgba[i];
                byte g = rgba[i + 1];
                byte b = rgba[i + 2];
                byte a = rgba[i + 3];

                bmp[54 + i] = b;
                bmp[54 + i + 1] = g;
                bmp[54 + i + 2] = r;
                bmp[54 + i + 3] = a;
            }

            return bmp;
        }
    }
}
