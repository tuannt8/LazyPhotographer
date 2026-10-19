using System;
using System.Runtime.InteropServices;

namespace LazyPhotographer.Interop
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
}
