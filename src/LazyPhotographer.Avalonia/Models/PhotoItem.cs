using System.IO;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using LazyPhotographer.Models;

namespace LazyPhotographer.Avalonia.Models
{
    public class PhotoItem : PhotoItemBase
    {
        private Bitmap? _thumbnail;

        public Bitmap? Thumbnail
        {
            get => _thumbnail;
            set => SetField(ref _thumbnail, value);
        }

        public IBrush FlagColor => Flag switch
        {
            FlagStatus.Flagged => Brushes.Gold,
            FlagStatus.Rejected => Brushes.IndianRed,
            _ => Brushes.DimGray
        };

        protected override void OnFlagChanged()
        {
            base.OnFlagChanged();
            OnPropertyChanged(nameof(FlagColor));
        }

        public static PhotoItem Create(string filePath, byte[]? thumbnailBytes)
        {
            var item = new PhotoItem
            {
                FilePath = filePath,
                ThumbnailBytes = thumbnailBytes
            };

            if (thumbnailBytes != null && thumbnailBytes.Length > 0)
            {
                using var ms = new MemoryStream(thumbnailBytes);
                item.Thumbnail = new Bitmap(ms);
            }

            return item;
        }
    }
}
