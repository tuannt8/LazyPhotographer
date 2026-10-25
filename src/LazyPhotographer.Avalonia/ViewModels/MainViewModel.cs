using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LazyPhotographer.Avalonia.Models;
using LazyPhotographer.Avalonia.Services;
using LazyPhotographer.Common.Services;
using LazyPhotographer.ViewModels;

namespace LazyPhotographer.Avalonia.ViewModels
{
    public class MainViewModel : MainViewModelBase<PhotoItem, Bitmap>
    {
        private readonly Window? _hostWindow;
        private readonly Action<Action>? _uiDispatcher;
        private CancellationTokenSource? _loupeCts;

        public MainViewModel(Window? hostWindow = null, Action<Action>? uiDispatcher = null)
        {
            _hostWindow = hostWindow;
            _uiDispatcher = uiDispatcher;
            InitializeSamplePhotosInBackground();
            RefreshCacheStats();
        }

        protected override void OnSelectedPhotoChanged(PhotoItem? oldPhoto, PhotoItem? newPhoto)
        {
            base.OnSelectedPhotoChanged(oldPhoto, newPhoto);

            // 1. Instantly cancel any previous high-res loading task (prevents intermediate loading during fast scroll/navigation)
            _loupeCts?.Cancel();
            _loupeCts?.Dispose();
            _loupeCts = null;

            if (newPhoto == null)
            {
                LoupeImage = null;
                IsHighResLoaded = false;
                LoupeResolutionText = string.Empty;
                return;
            }

            // 2. Check High-Res cache first (instant if previously loaded)
            if (HighResCache.Shared.TryGet(newPhoto.FilePath, out var cachedHighRes) && cachedHighRes != null)
            {
                newPhoto.HighResImage = cachedHighRes;
                LoupeImage = cachedHighRes;
                IsHighResLoaded = true;
                LoupeResolutionText = $"HD ({cachedHighRes.PixelSize.Width} × {cachedHighRes.PixelSize.Height})";
                return;
            }

            // 3. SHOW LOW RES FIRST
            if (newPhoto.HighResImage != null)
            {
                LoupeImage = newPhoto.HighResImage;
                IsHighResLoaded = true;
                LoupeResolutionText = $"HD ({newPhoto.HighResImage.PixelSize.Width} × {newPhoto.HighResImage.PixelSize.Height})";
            }
            else if (newPhoto.Thumbnail != null)
            {
                LoupeImage = newPhoto.Thumbnail;
                IsHighResLoaded = false;
                LoupeResolutionText = "Preview (Loading HD...)";
            }
            else
            {
                LoupeImage = null;
                IsHighResLoaded = false;
                LoupeResolutionText = "Loading...";

                // Ensure low-res thumbnail is loaded if missing
                _ = Task.Run(async () =>
                {
                    var bm = await ThumbnailService.LoadThumbnailAsync(newPhoto, CancellationToken.None);
                    if (bm != null && SelectedPhoto == newPhoto && !IsHighResLoaded)
                    {
                        RunOnUiThread(() =>
                        {
                            if (SelectedPhoto == newPhoto && !IsHighResLoaded)
                            {
                                newPhoto.Thumbnail = bm;
                                LoupeImage ??= bm;
                            }
                        });
                    }
                });
            }

            // 4. LOAD HIGH RES LATER IN TASK WITH DEBOUNCED CANCELLATION FOR FAST NAVIGATION
            var cts = new CancellationTokenSource();
            _loupeCts = cts;
            var token = cts.Token;

            _ = Task.Run(async () =>
            {
                // Debounce by 80ms: rapid key presses/clicks skip intermediate decoding completely!
                await Task.Delay(80, token);
                if (token.IsCancellationRequested) return;

                int maxDim = SettingsService.Current.HighResMaxDimension;
                var highRes = await HighResImageService.LoadHighResImageAsync(newPhoto, maxDim, token);

                if (token.IsCancellationRequested || highRes == null) return;

                RunOnUiThread(() =>
                {
                    if (token.IsCancellationRequested || SelectedPhoto != newPhoto) return;

                    newPhoto.HighResImage = highRes;
                    LoupeImage = highRes;
                    IsHighResLoaded = true;
                    LoupeResolutionText = $"HD ({highRes.PixelSize.Width} × {highRes.PixelSize.Height})";
                    RefreshCacheStats();
                });
            }, token);
        }

        public override void RefreshCacheStats()
        {
            long thumbBytes = ThumbnailCache.Shared.EstimatedMemoryBytes;
            long highResBytes = HighResCache.Shared.EstimatedMemoryBytes;
            double mb = (thumbBytes + highResBytes) / (1024.0 * 1024.0);

            CacheStatsText = $"Thumbnails: {ThumbnailCache.Shared.Count} / {ThumbnailCache.Shared.MaxCapacity} • High-Res: {HighResCache.Shared.Count} / {HighResCache.Shared.MaxCapacity} • Memory: ~{mb:F1} MB";
        }

        public override void ClearAllCaches()
        {
            ThumbnailCache.Shared.Clear();
            HighResCache.Shared.Clear();
            ThumbnailLoader.Clear();
            RefreshCacheStats();
            StatusMessage = "All thumbnail and high-res caches cleared.";
            SettingsFeedback = "✓ Cache Cleared Successfully";
        }

        protected override PhotoItem CreatePhotoItem(string filePath, byte[]? thumbnailBytes)
        {
            return PhotoItem.Create(filePath, thumbnailBytes);
        }

        protected override Bitmap? CreateImageSourceFromBytes(byte[]? imageBytes)
        {
            if (imageBytes == null || imageBytes.Length == 0) return null;
            using var ms = new MemoryStream(imageBytes);
            return new Bitmap(ms);
        }

        protected override void RunOnUiThread(Action action)
        {
            if (_uiDispatcher != null)
            {
                _uiDispatcher(action);
                return;
            }

            if (Dispatcher.UIThread.CheckAccess())
            {
                action();
            }
            else
            {
                Dispatcher.UIThread.Post(action);
            }
        }

        protected override async Task<string?> PickFolderAsync()
        {
            if (_hostWindow != null)
            {
                var topLevel = TopLevel.GetTopLevel(_hostWindow);
                if (topLevel != null)
                {
                    var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                    {
                        Title = "Select Photo Folder",
                        AllowMultiple = false
                    });
                    var selected = folders.FirstOrDefault();
                    if (selected != null)
                    {
                        return selected.TryGetLocalPath() ?? selected.Path.LocalPath;
                    }
                }
            }
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SamplePhotos");
        }
    }
}
