using System;
using System.Runtime.InteropServices;

namespace LazyPhotographer.Interop
{
    public static class NativeMethods
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

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public static extern int LazyPhoto_GetLastError([Out] char[] outBuffer, uint maxLen);
    }
}
