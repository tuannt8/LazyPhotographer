using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace LazyPhotographer.Models
{
    public abstract class PhotoItemBase : INotifyPropertyChanged
    {
        private string _filePath = string.Empty;
        private string _fileName = string.Empty;
        private FlagStatus _flag = FlagStatus.Unflagged;
        private bool _isSelected;
        private byte[]? _thumbnailBytes;
        private string _cameraModel = "FUJIFILM";
        private string _exposureInfo = "1/250s f/2.8 ISO 160";
        private uint _width;
        private uint _height;
        private int _orientationDegrees;
        private int _orientationTag = 1;
        private bool _isThumbnailLoading;
        private bool _isMetadataLoaded;

        public string FilePath
        {
            get => _filePath;
            set
            {
                if (SetField(ref _filePath, value))
                {
                    FileName = Path.GetFileName(value);
                    OnPropertyChanged(nameof(FileExtension));
                }
            }
        }

        public string FileName
        {
            get => _fileName;
            set => SetField(ref _fileName, value);
        }

        public string FileExtension => Path.GetExtension(_filePath).ToUpperInvariant();

        public FlagStatus Flag
        {
            get => _flag;
            set
            {
                if (SetField(ref _flag, value))
                {
                    OnPropertyChanged(nameof(IsFlagged));
                    OnPropertyChanged(nameof(IsRejected));
                    OnPropertyChanged(nameof(FlagIcon));
                    OnPropertyChanged(nameof(FlagColorHex));
                    OnFlagChanged();
                }
            }
        }

        protected virtual void OnFlagChanged()
        {
        }

        public bool IsFlagged => _flag == FlagStatus.Flagged;
        public bool IsRejected => _flag == FlagStatus.Rejected;

        public string FlagIcon => _flag switch
        {
            FlagStatus.Flagged => "★ Flagged",
            FlagStatus.Rejected => "✕ Rejected",
            _ => "○ Unflagged"
        };

        public string FlagColorHex => _flag switch
        {
            FlagStatus.Flagged => "#FFD700",
            FlagStatus.Rejected => "#CD5C5C",
            _ => "#696969"
        };

        public bool IsSelected
        {
            get => _isSelected;
            set => SetField(ref _isSelected, value);
        }

        public byte[]? ThumbnailBytes
        {
            get => _thumbnailBytes;
            set => SetField(ref _thumbnailBytes, value);
        }

        public string CameraModel
        {
            get => _cameraModel;
            set => SetField(ref _cameraModel, value);
        }

        public string ExposureInfo
        {
            get => _exposureInfo;
            set => SetField(ref _exposureInfo, value);
        }

        public uint Width
        {
            get => _width;
            set
            {
                if (SetField(ref _width, value))
                {
                    OnPropertyChanged(nameof(ResolutionText));
                }
            }
        }

        public uint Height
        {
            get => _height;
            set
            {
                if (SetField(ref _height, value))
                {
                    OnPropertyChanged(nameof(ResolutionText));
                }
            }
        }

        public string ResolutionText => _width > 0 && _height > 0 ? $"{_width} × {_height}" : "RAW Sensor";

        public int OrientationDegrees
        {
            get => _orientationDegrees;
            set => SetField(ref _orientationDegrees, value);
        }

        public int OrientationTag
        {
            get => _orientationTag;
            set => SetField(ref _orientationTag, value);
        }

        public bool IsThumbnailLoading
        {
            get => _isThumbnailLoading;
            set => SetField(ref _isThumbnailLoading, value);
        }

        public bool IsMetadataLoaded
        {
            get => _isMetadataLoaded;
            set => SetField(ref _isMetadataLoaded, value);
        }

        public ImageAdjustmentsModel Adjustments { get; set; } = new ImageAdjustmentsModel();

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
