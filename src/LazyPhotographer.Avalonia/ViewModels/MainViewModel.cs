using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LazyPhotographer.Avalonia.Models;
using LazyPhotographer.ViewModels;

namespace LazyPhotographer.Avalonia.ViewModels
{
    public class MainViewModel : MainViewModelBase<PhotoItem, Bitmap>
    {
        private readonly Window? _hostWindow;

        public MainViewModel(Window? hostWindow = null)
        {
            _hostWindow = hostWindow;
            InitializeSamplePhotosInBackground();
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
            Dispatcher.UIThread.Post(action);
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
