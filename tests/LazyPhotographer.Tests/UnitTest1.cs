using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using LazyPhotographer.Avalonia;
using LazyPhotographer.Avalonia.Models;
using LazyPhotographer.Avalonia.Services;
using LazyPhotographer.Avalonia.ViewModels;
using LazyPhotographer.Common.Metadata;
using LazyPhotographer.Common.Services;
using LazyPhotographer.Interop;
using LazyPhotographer.Models;
using SkiaSharp;
using Xunit;

namespace LazyPhotographer.Tests
{
    public class MetadataAndThumbnailTests
    {
        static MetadataAndThumbnailTests()
        {
            try
            {
                AppBuilder.Configure<App>()
                    .UsePlatformDetect()
                    .SetupWithoutStarting();
            }
            catch
            {
                // Already initialized
            }
        }

        [Fact]
        public void OrientationTag_MapsCorrectlyToDegrees()
        {
            Assert.Equal(0, FastMetadataReader.OrientationTagToDegrees(1));
            Assert.Equal(0, FastMetadataReader.OrientationTagToDegrees(2));
            Assert.Equal(180, FastMetadataReader.OrientationTagToDegrees(3));
            Assert.Equal(0, FastMetadataReader.OrientationTagToDegrees(4));
            Assert.Equal(0, FastMetadataReader.OrientationTagToDegrees(5));
            Assert.Equal(90, FastMetadataReader.OrientationTagToDegrees(6));
            Assert.Equal(0, FastMetadataReader.OrientationTagToDegrees(7));
            Assert.Equal(270, FastMetadataReader.OrientationTagToDegrees(8));
        }

        [Fact]
        public void FastMetadataReader_ReadsSyntheticRafMetadata()
        {
            string tempRaf = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.RAF");
            try
            {
                bool created = NativeImageBridge.CreateSampleRaf(tempRaf, "FUJIFILM X-T5", 1400, 933);
                Assert.True(created, "Failed to create synthetic RAF for test");

                var meta = FastMetadataReader.ReadMetadata(tempRaf);
                Assert.Contains("X-T5", meta.CameraModel);
                Assert.Equal("FUJIFILM", meta.CameraMake);
                Assert.True(meta.Width > 0, "Width should be > 0");
                Assert.True(meta.Height > 0, "Height should be > 0");
                Assert.False(string.IsNullOrEmpty(meta.ExposureInfo));
            }
            finally
            {
                if (File.Exists(tempRaf)) File.Delete(tempRaf);
            }
        }

        [Fact]
        public void ThumbnailCache_CleanUpIfFull_FollowsLruEviction()
        {
            var cache = new ThumbnailCache(maxCapacity: 3);

            var b1 = CreateDummyBitmap(10, 10);
            var b2 = CreateDummyBitmap(10, 10);
            var b3 = CreateDummyBitmap(10, 10);
            var b4 = CreateDummyBitmap(10, 10);

            cache.Put("file1.jpg", b1);
            cache.Put("file2.jpg", b2);
            cache.Put("file3.jpg", b3);

            Assert.True(cache.TryGet("file1.jpg", out var retrieved1));
            Assert.Same(b1, retrieved1);

            // Accessing file1 moves it to MRU. Now file2 is the oldest (LRU).
            // Inserting file4 should evict file2.
            cache.Put("file4.jpg", b4);

            Assert.True(cache.TryGet("file1.jpg", out _));
            Assert.False(cache.TryGet("file2.jpg", out _), "file2 should have been evicted when cache was full");
            Assert.True(cache.TryGet("file3.jpg", out _));
            Assert.True(cache.TryGet("file4.jpg", out _));

            // Test Clear
            cache.Clear();
            Assert.False(cache.TryGet("file1.jpg", out _));
            Assert.False(cache.TryGet("file3.jpg", out _));
            Assert.False(cache.TryGet("file4.jpg", out _));
        }

        [Fact]
        public void ThumbnailService_AppliesRotationFromMetadata()
        {
            // Generate a 100x50 horizontal image
            byte[] jpegBytes;
            using (var bmp = new SKBitmap(100, 50))
            {
                using var canvas = new SKCanvas(bmp);
                canvas.Clear(SKColors.Blue);
                using var img = SKImage.FromBitmap(bmp);
                using var data = img.Encode(SKEncodedImageFormat.Jpeg, 90);
                jpegBytes = data.ToArray();
            }

            // Test 0 degrees rotation
            var normalBmp = ThumbnailService.CreateRotatedThumbnailBitmap(jpegBytes, 0, maxDimension: 360);
            Assert.NotNull(normalBmp);
            Assert.Equal(100, normalBmp.PixelSize.Width);
            Assert.Equal(50, normalBmp.PixelSize.Height);

            // Test 90 degrees rotation -> dimensions must swap (50x100)
            var rot90Bmp = ThumbnailService.CreateRotatedThumbnailBitmap(jpegBytes, 90, maxDimension: 360);
            Assert.NotNull(rot90Bmp);
            Assert.Equal(50, rot90Bmp.PixelSize.Width);
            Assert.Equal(100, rot90Bmp.PixelSize.Height);

            // Test 180 degrees rotation -> dimensions stay 100x50
            var rot180Bmp = ThumbnailService.CreateRotatedThumbnailBitmap(jpegBytes, 180, maxDimension: 360);
            Assert.NotNull(rot180Bmp);
            Assert.Equal(100, rot180Bmp.PixelSize.Width);
            Assert.Equal(50, rot180Bmp.PixelSize.Height);

            // Test 270 degrees rotation -> dimensions must swap (50x100)
            var rot270Bmp = ThumbnailService.CreateRotatedThumbnailBitmap(jpegBytes, 270, maxDimension: 360);
            Assert.NotNull(rot270Bmp);
            Assert.Equal(50, rot270Bmp.PixelSize.Width);
            Assert.Equal(100, rot270Bmp.PixelSize.Height);
        }

        [Fact]
        public async Task ThumbnailService_CancelsIfCancellationTokenTriggered()
        {
            var item = new PhotoItem
            {
                FilePath = "nonexistent_file.jpg"
            };

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var result = await ThumbnailService.LoadThumbnailAsync(item, cts.Token);
            Assert.Null(result);
        }

        [Fact]
        public void PhotoItem_FlagAndPickToggle_UpdatesProperties()
        {
            var photo = new PhotoItem
            {
                FilePath = @"C:\Photos\DSC_0001.RAF",
                Flag = FlagStatus.Unflagged
            };

            Assert.False(photo.IsFlagged);
            Assert.Equal("○ Unflagged", photo.FlagIcon);

            // Toggle to flagged
            photo.Flag = FlagStatus.Flagged;
            Assert.True(photo.IsFlagged);
            Assert.Equal("★ Flagged", photo.FlagIcon);

            // Toggle back to unflagged
            photo.Flag = FlagStatus.Unflagged;
            Assert.False(photo.IsFlagged);
            Assert.Equal("○ Unflagged", photo.FlagIcon);
        }

        [Fact]
        public void MainViewModel_MatchesFilter_FiltersCorrectly()
        {
            var vm = new MainViewModel();
            var unflaggedPhoto = PhotoItem.Create("test1.jpg", null);
            unflaggedPhoto.Flag = FlagStatus.Unflagged;

            var flaggedPhoto = PhotoItem.Create("test2.jpg", null);
            flaggedPhoto.Flag = FlagStatus.Flagged;

            var rejectedPhoto = PhotoItem.Create("test3.jpg", null);
            rejectedPhoto.Flag = FlagStatus.Rejected;

            vm.FilterSelectionMode = FilterSelection.All;
            Assert.True(vm.MatchesFilter(unflaggedPhoto));
            Assert.True(vm.MatchesFilter(flaggedPhoto));
            Assert.True(vm.MatchesFilter(rejectedPhoto));

            vm.FilterSelectionMode = FilterSelection.FlaggedOnly;
            Assert.False(vm.MatchesFilter(unflaggedPhoto));
            Assert.True(vm.MatchesFilter(flaggedPhoto));
            Assert.False(vm.MatchesFilter(rejectedPhoto));

            vm.FilterSelectionMode = FilterSelection.UnflaggedOnly;
            Assert.True(vm.MatchesFilter(unflaggedPhoto));
            Assert.False(vm.MatchesFilter(flaggedPhoto));
            Assert.False(vm.MatchesFilter(rejectedPhoto));

            vm.FilterSelectionMode = FilterSelection.RejectedOnly;
            Assert.False(vm.MatchesFilter(unflaggedPhoto));
            Assert.False(vm.MatchesFilter(flaggedPhoto));
            Assert.True(vm.MatchesFilter(rejectedPhoto));
        }

        [Fact]
        public void MainViewModel_TogglePickPhoto_TogglesState()
        {
            var vm = new MainViewModel();
            var photo = PhotoItem.Create("test.jpg", null);
            vm.Photos.Add(photo);
            vm.SelectedPhoto = photo;

            Assert.False(photo.IsFlagged);

            vm.TogglePickPhoto(photo);
            Assert.True(photo.IsFlagged);
            Assert.Contains("Flagged (Picked)", vm.StatusMessage);

            vm.TogglePickPhoto(photo);
            Assert.False(photo.IsFlagged);
            Assert.Contains("Unflagged", vm.StatusMessage);
        }

        [Fact]
        public async Task MainViewModel_LoadFolderPhotosAsync_PopulatesPlaceholdersImmediately()
        {
            // Create a temp folder with 3 test files
            string tempDir = Path.Combine(Path.GetTempPath(), $"lp_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                File.WriteAllText(Path.Combine(tempDir, "photo1.jpg"), "test1");
                File.WriteAllText(Path.Combine(tempDir, "photo2.jpg"), "test2");
                File.WriteAllText(Path.Combine(tempDir, "photo3.jpg"), "test3");

                var vm = new MainViewModel(uiDispatcher: a => a());
                await vm.LoadFolderPhotosAsync(tempDir);

                Assert.Equal(3, vm.Photos.Count);
                Assert.Equal(3, vm.FilteredPhotos.Count);
                Assert.NotNull(vm.SelectedPhoto);

                // All items initially have placeholders (null thumbnail, unblocked GUI)
                foreach (var photo in vm.Photos)
                {
                    Assert.Null(photo.Thumbnail);
                    Assert.False(string.IsNullOrEmpty(photo.FileName));
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public void SettingsService_SavesAndAdjustsCacheCapacities()
        {
            var initialSettings = SettingsService.Load();
            Assert.NotNull(initialSettings);

            // Change settings
            var newSettings = new LazyPhotographer.Common.Models.AppSettings
            {
                ThumbnailCacheCapacity = 150,
                HighResCacheCapacity = 35,
                HighResMaxDimension = 1920
            };

            SettingsService.Save(newSettings);

            Assert.Equal(150, ThumbnailCache.Shared.MaxCapacity);
            Assert.Equal(35, HighResCache.Shared.MaxCapacity);

            // Reload from file to verify persistence
            var reloaded = SettingsService.Load();
            Assert.Equal(150, reloaded.ThumbnailCacheCapacity);
            Assert.Equal(35, reloaded.HighResCacheCapacity);
            Assert.Equal(1920, reloaded.HighResMaxDimension);

            // Restore defaults
            newSettings.ThumbnailCacheCapacity = 200;
            newSettings.HighResCacheCapacity = 20;
            newSettings.HighResMaxDimension = 2560;
            SettingsService.Save(newSettings);
        }

        [Fact]
        public void HighResCache_FollowsLruEviction()
        {
            var cache = new HighResCache(maxCapacity: 2);
            var b1 = CreateDummyBitmap(50, 50);
            var b2 = CreateDummyBitmap(50, 50);
            var b3 = CreateDummyBitmap(50, 50);

            cache.Put("img1.jpg", b1);
            cache.Put("img2.jpg", b2);

            Assert.True(cache.TryGet("img1.jpg", out var r1));
            Assert.Same(b1, r1);

            // img1 is MRU, img2 is LRU. Adding img3 should evict img2.
            cache.Put("img3.jpg", b3);

            Assert.True(cache.TryGet("img1.jpg", out _));
            Assert.False(cache.TryGet("img2.jpg", out _), "img2 should have been evicted when cache filled");
            Assert.True(cache.TryGet("img3.jpg", out _));

            cache.Clear();
            Assert.Equal(0, cache.Count);
        }

        [Fact]
        public async Task LoupeMode_FastNavigation_ShowsLowResFirstAndCancelsIntermediateTasks()
        {
            var vm = new MainViewModel(uiDispatcher: a => a());
            var p1 = PhotoItem.Create("p1.jpg", null);
            p1.Thumbnail = CreateDummyBitmap(20, 20);

            var p2 = PhotoItem.Create("p2.jpg", null);
            p2.Thumbnail = CreateDummyBitmap(20, 20);

            var p3 = PhotoItem.Create("p3.jpg", null);
            p3.Thumbnail = CreateDummyBitmap(20, 20);

            vm.Photos.Add(p1);
            vm.Photos.Add(p2);
            vm.Photos.Add(p3);

            // Rapidly switch from p1 to p2 to p3
            vm.SelectedPhoto = p1;
            Assert.Same(p1.Thumbnail, vm.LoupeImage); // Low res first!
            Assert.False(vm.IsHighResLoaded);

            vm.SelectedPhoto = p2;
            Assert.Same(p2.Thumbnail, vm.LoupeImage); // Low res first!
            Assert.False(vm.IsHighResLoaded);

            vm.SelectedPhoto = p3;
            Assert.Same(p3.Thumbnail, vm.LoupeImage); // Low res first!
            Assert.False(vm.IsHighResLoaded);

            // Wait a brief moment to allow debounce & task to evaluate
            await Task.Delay(150);

            // Only p3 was the final landing target
            Assert.Same(p3, vm.SelectedPhoto);
        }

        private static Bitmap CreateDummyBitmap(int width, int height)
        {
            using var skBitmap = new SKBitmap(width, height);
            using var canvas = new SKCanvas(skBitmap);
            canvas.Clear(SKColors.Red);
            using var img = SKImage.FromBitmap(skBitmap);
            using var data = img.Encode(SKEncodedImageFormat.Jpeg, 80);
            using var ms = new MemoryStream(data.ToArray());
            return new Bitmap(ms);
        }
    }
}
