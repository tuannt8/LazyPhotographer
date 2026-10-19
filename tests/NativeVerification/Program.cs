using System;
using System.IO;
using System.Runtime.InteropServices;

namespace NativeVerification
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct NativeRawInfo
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string CameraMake;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string CameraModel;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string ExposureTimeString;

        public uint Iso;
        public float FNumber;
        public float FocalLength;
        public uint Width;
        public uint Height;
        public uint BitDepth;
        public int HasEmbeddedPreview;
        public uint PreviewWidth;
        public uint PreviewHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeAdjustments
    {
        public float Exposure;
        public float Temperature;
        public float Tint;
        public float Contrast;
        public float Highlights;
        public float Shadows;
        public float Whites;
        public float Blacks;
        public float Saturation;
        public float Vibrance;
        public float Sharpness;
        public float Denoise;
        public int EnableAiAutoTone;
        public float AiDenoiseStrength;
        public int EnableAiDetailEnhance;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeHistogram
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public uint[] RBins;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public uint[] GBins;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public uint[] BBins;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public uint[] LumaBins;

        public uint MaxCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeExportOptions
    {
        public int Format;        // 0 = Jpeg, 1 = Png, 2 = Tiff16
        public int JpegQuality;   // 1 to 100
        public uint MaxDimension; // 0 = keep original
        public int BitDepth16;    // 1 for 16-bit
    }

    internal static class NativeMethods
    {
        private const string DllName = "CsBinding.dll";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr LazyPhoto_CreateEngine();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void LazyPhoto_DestroyEngine(IntPtr engine);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public static extern int LazyPhoto_LoadRaw(IntPtr engine, string filePath, ref NativeRawInfo outInfo, int halfSize);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public static extern int LazyPhoto_ExtractEmbeddedPreview(
            string filePath,
            [Out] byte[]? outJpegBuffer,
            uint maxBufferSize,
            out uint outActualSize,
            out uint outWidth,
            out uint outHeight);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int LazyPhoto_RenderToRgba8(
            IntPtr engine,
            ref NativeAdjustments adj,
            [Out] byte[] outRgbaBuffer,
            uint width,
            uint height,
            uint stride);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int LazyPhoto_ComputeHistogram(
            IntPtr engine,
            ref NativeAdjustments adj,
            out NativeHistogram outHist);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int LazyPhoto_AnalyzeAutoTone(IntPtr engine, ref NativeAdjustments outAdj);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public static extern int LazyPhoto_ExportImage(
            IntPtr engine,
            string outputPath,
            ref NativeAdjustments adj,
            ref NativeExportOptions options);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public static extern int LazyPhoto_CreateSampleRaf(
            string outputPath,
            string cameraModel,
            uint width,
            uint height);
    }

    class Program
    {
        static void Main()
        {
            Console.WriteLine("=== LAZY PHOTOGRAPHER NATIVE VERIFICATION ===");

            string testRafPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_fuji_xt5.RAF");
            Console.WriteLine($"[1] Creating synthetic Fujifilm RAF file at: {testRafPath}");
            int created = NativeMethods.LazyPhoto_CreateSampleRaf(testRafPath, "FUJIFILM X-T5", 1200, 800);
            if (created != 1 || !File.Exists(testRafPath))
            {
                throw new Exception("Failed to create synthetic RAF file!");
            }
            Console.WriteLine($"    ✓ RAF created successfully. File size: {new FileInfo(testRafPath).Length:N0} bytes");

            Console.WriteLine("[2] Initializing Native Engine Context...");
            IntPtr engine = NativeMethods.LazyPhoto_CreateEngine();
            if (engine == IntPtr.Zero)
            {
                throw new Exception("Failed to create native engine context!");
            }
            Console.WriteLine("    ✓ Native engine context created.");

            Console.WriteLine("[3] Loading and decoding RAW file into 16-bit linear buffer...");
            var rawInfo = new NativeRawInfo();
            int loadRes = NativeMethods.LazyPhoto_LoadRaw(engine, testRafPath, ref rawInfo, 0);
            if (loadRes != 1)
            {
                throw new Exception("Failed to load and decode RAW file!");
            }
            Console.WriteLine($"    ✓ Camera: {rawInfo.CameraMake} {rawInfo.CameraModel}");
            Console.WriteLine($"    ✓ Resolution: {rawInfo.Width} x {rawInfo.Height}, Depth: {rawInfo.BitDepth}-bit");
            Console.WriteLine($"    ✓ Has Preview: {rawInfo.HasEmbeddedPreview != 0}");

            Console.WriteLine("[4] Extracting embedded JPEG preview directly...");
            int prevRes = NativeMethods.LazyPhoto_ExtractEmbeddedPreview(testRafPath, null, 0, out uint pSize, out uint pW, out uint pH);
            if (prevRes != 1 || pSize == 0)
            {
                throw new Exception("Failed to query embedded preview size!");
            }
            byte[] jpegBytes = new byte[pSize];
            NativeMethods.LazyPhoto_ExtractEmbeddedPreview(testRafPath, jpegBytes, pSize, out pSize, out pW, out pH);
            Console.WriteLine($"    ✓ Embedded preview extracted: {pSize:N0} bytes, dimensions {pW}x{pH}");

            Console.WriteLine("[5] Testing AI Auto-Tone & Scene Analysis...");
            var adj = new NativeAdjustments();
            int aiRes = NativeMethods.LazyPhoto_AnalyzeAutoTone(engine, ref adj);
            if (aiRes != 1)
            {
                throw new Exception("AI Auto-Tone analysis failed!");
            }
            Console.WriteLine($"    ✓ Suggested AI Exposure: {adj.Exposure:+0.00;-0.00} EV");
            Console.WriteLine($"    ✓ Suggested Contrast: {adj.Contrast:+0.0;-0.0}, Highlights: {adj.Highlights:+0.0;-0.0}, Shadows: {adj.Shadows:+0.0;-0.0}");
            Console.WriteLine($"    ✓ Color Temperature Shift: {adj.Temperature:+0.0;-0.0}, Tint: {adj.Tint:+0.0;-0.0}");

            Console.WriteLine("[6] Rendering to 8-bit RGBA display buffer (with AI Denoise enabled)...");
            adj.AiDenoiseStrength = 0.5f;
            adj.EnableAiDetailEnhance = 1;
            uint dispW = 800;
            uint dispH = 533;
            uint stride = dispW * 4;
            byte[] rgba = new byte[stride * dispH];
            int renderRes = NativeMethods.LazyPhoto_RenderToRgba8(engine, ref adj, rgba, dispW, dispH, stride);
            if (renderRes != 1)
            {
                throw new Exception("Failed to render 8-bit RGBA display buffer!");
            }
            Console.WriteLine($"    ✓ Rendered {dispW}x{dispH} display buffer ({rgba.Length:N0} bytes). Sample pixel: R={rgba[1000]}, G={rgba[1001]}, B={rgba[1002]}");

            Console.WriteLine("[7] Computing 256-bin RGB Histogram...");
            int histRes = NativeMethods.LazyPhoto_ComputeHistogram(engine, ref adj, out var hist);
            if (histRes != 1 || hist.MaxCount == 0)
            {
                throw new Exception("Failed to compute RGB histogram!");
            }
            Console.WriteLine($"    ✓ Histogram computed successfully. Peak bin count: {hist.MaxCount:N0}");

            Console.WriteLine("[8] Testing Export Pipeline (JPEG, PNG, 16-bit Master TIFF)...");
            // JPEG
            string outJpg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "export_test.jpg");
            var optJpg = new NativeExportOptions { Format = 0, JpegQuality = 90, MaxDimension = 0, BitDepth16 = 0 };
            NativeMethods.LazyPhoto_ExportImage(engine, outJpg, ref adj, ref optJpg);
            Console.WriteLine($"    ✓ Exported JPEG: {new FileInfo(outJpg).Length:N0} bytes");

            // PNG
            string outPng = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "export_test.png");
            var optPng = new NativeExportOptions { Format = 1, JpegQuality = 100, MaxDimension = 1200, BitDepth16 = 0 };
            NativeMethods.LazyPhoto_ExportImage(engine, outPng, ref adj, ref optPng);
            Console.WriteLine($"    ✓ Exported PNG: {new FileInfo(outPng).Length:N0} bytes");

            // 16-bit Master TIFF
            string outTiff = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "export_test_16bit.tif");
            var optTiff = new NativeExportOptions { Format = 2, JpegQuality = 100, MaxDimension = 0, BitDepth16 = 1 };
            NativeMethods.LazyPhoto_ExportImage(engine, outTiff, ref adj, ref optTiff);
            Console.WriteLine($"    ✓ Exported 16-Bit Master TIFF: {new FileInfo(outTiff).Length:N0} bytes");

            NativeMethods.LazyPhoto_DestroyEngine(engine);
            Console.WriteLine("=== ALL TESTS PASSED SUCCESSFULLY! ===");
        }
    }
}
