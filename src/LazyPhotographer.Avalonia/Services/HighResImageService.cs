using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using LazyPhotographer.Avalonia.Models;
using LazyPhotographer.Interop;
using LazyPhotographer.Models;

namespace LazyPhotographer.Avalonia.Services
{
    public static class HighResImageService
    {
        public static async Task<Bitmap?> LoadHighResImageAsync(PhotoItem item, int maxDimension, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(item.FilePath)) return null;

            // 1. Check in-memory High-Res cache
            if (HighResCache.Shared.TryGet(item.FilePath, out var cached))
            {
                return cached;
            }

            if (ct.IsCancellationRequested) return null;

            return await Task.Run(() =>
            {
                if (ct.IsCancellationRequested) return null;

                byte[]? imageBytes = null;
                string ext = Path.GetExtension(item.FilePath).ToLowerInvariant();

                if (ext == ".raf")
                {
                    // Extract high-resolution embedded preview
                    imageBytes = NativeImageBridge.ExtractEmbeddedPreview(item.FilePath, out uint pW, out uint pH);

                    if (imageBytes == null || imageBytes.Length == 0)
                    {
                        using var bridge = new NativeImageBridge();
                        if (bridge.LoadRaw(item.FilePath, out var rawInfo, halfSize: false))
                        {
                            uint targetW = Math.Min(rawInfo.Width > 0 ? rawInfo.Width : 1920, 2560);
                            uint targetH = (uint)(targetW * 2 / 3);
                            imageBytes = bridge.RenderPreviewBmpBytes(new ImageAdjustmentsModel().ToNative(), targetW, targetH);
                        }
                    }
                }
                else if (File.Exists(item.FilePath))
                {
                    try
                    {
                        imageBytes = File.ReadAllBytes(item.FilePath);
                    }
                    catch
                    {
                        // File read error
                    }
                }

                if (imageBytes == null || imageBytes.Length == 0) return null;
                if (ct.IsCancellationRequested) return null;

                // Decode, apply metadata rotation, and downscale to maxDimension
                var bitmap = ThumbnailService.CreateRotatedThumbnailBitmap(imageBytes, item.OrientationDegrees, maxDimension);
                if (bitmap != null && !ct.IsCancellationRequested)
                {
                    HighResCache.Shared.Put(item.FilePath, bitmap);
                }

                return bitmap;
            }, ct);
        }
    }
}
