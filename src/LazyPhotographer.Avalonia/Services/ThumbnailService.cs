using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using LazyPhotographer.Avalonia.Models;
using LazyPhotographer.Interop;
using LazyPhotographer.Models;
using SkiaSharp;

namespace LazyPhotographer.Avalonia.Services
{
    public static class ThumbnailService
    {
        private static readonly SemaphoreSlim _semaphore = new(Math.Max(2, Environment.ProcessorCount / 2), Math.Max(2, Environment.ProcessorCount / 2));

        public static async Task<Bitmap?> LoadThumbnailAsync(PhotoItem item, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(item.FilePath)) return null;

            // 1. Check in-memory cache first
            if (ThumbnailCache.Shared.TryGet(item.FilePath, out var cached))
            {
                return cached;
            }

            if (ct.IsCancellationRequested) return null;

            // 2. Throttle concurrency with semaphore
            await _semaphore.WaitAsync(ct);
            try
            {
                if (ct.IsCancellationRequested) return null;

                // Double check cache
                if (ThumbnailCache.Shared.TryGet(item.FilePath, out cached))
                {
                    return cached;
                }

                return await Task.Run(() =>
                {
                    if (ct.IsCancellationRequested) return null;

                    byte[]? imageBytes = null;
                    string ext = Path.GetExtension(item.FilePath).ToLowerInvariant();

                    if (ext == ".raf")
                    {
                        // Fast embedded preview extraction
                        imageBytes = NativeImageBridge.ExtractEmbeddedPreview(item.FilePath, out uint pW, out uint pH);

                        if (imageBytes == null || imageBytes.Length == 0)
                        {
                            using var bridge = new NativeImageBridge();
                            if (bridge.LoadRaw(item.FilePath, out var rawInfo, halfSize: true))
                            {
                                imageBytes = bridge.RenderPreviewBmpBytes(new ImageAdjustmentsModel().ToNative(), 360, 240);
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
                            // File read failure
                        }
                    }

                    if (imageBytes == null || imageBytes.Length == 0) return null;
                    if (ct.IsCancellationRequested) return null;

                    // Apply rotation and downsampling to thumbnail size
                    var bitmap = CreateRotatedThumbnailBitmap(imageBytes, item.OrientationDegrees, 360);
                    if (bitmap != null)
                    {
                        ThumbnailCache.Shared.Put(item.FilePath, bitmap);
                    }

                    return bitmap;
                }, ct);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public static Bitmap? CreateRotatedThumbnailBitmap(byte[] imageBytes, int rotationDegrees, int maxDimension = 360)
        {
            try
            {
                using var inputStream = new MemoryStream(imageBytes);
                using var orig = SKBitmap.Decode(inputStream);
                if (orig == null) return null;

                int srcW = orig.Width;
                int srcH = orig.Height;
                float scale = 1f;

                if (maxDimension > 0 && (srcW > maxDimension || srcH > maxDimension))
                {
                    scale = (float)maxDimension / Math.Max(srcW, srcH);
                }

                int destW = Math.Max(1, (int)(srcW * scale));
                int destH = Math.Max(1, (int)(srcH * scale));

                int normDegrees = (rotationDegrees % 360 + 360) % 360;
                bool swapDims = (normDegrees == 90 || normDegrees == 270);

                int outW = swapDims ? destH : destW;
                int outH = swapDims ? destW : destH;

                using var rotatedBitmap = new SKBitmap(outW, outH, orig.ColorType, orig.AlphaType);
                using (var canvas = new SKCanvas(rotatedBitmap))
                {
                    canvas.Clear(SKColors.Transparent);
                    canvas.Translate(outW / 2f, outH / 2f);

                    if (normDegrees != 0)
                    {
                        canvas.RotateDegrees(normDegrees);
                    }

                    var destRect = new SKRect(-destW / 2f, -destH / 2f, destW / 2f, destH / 2f);
                    using var paint = new SKPaint
                    {
                        IsAntialias = true
                    };
                    canvas.DrawBitmap(orig, destRect, new SKSamplingOptions(SKFilterMode.Linear), paint);
                }

                using var image = SKImage.FromBitmap(rotatedBitmap);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, 85);
                if (data == null) return null;

                using var ms = new MemoryStream(data.ToArray());
                return new Bitmap(ms);
            }
            catch
            {
                // Fallback to direct Avalonia Bitmap decode if Skia transform fails
                try
                {
                    using var ms = new MemoryStream(imageBytes);
                    return new Bitmap(ms);
                }
                catch
                {
                    return null;
                }
            }
        }
    }
}
