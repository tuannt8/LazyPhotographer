using System;

namespace LazyPhotographer.Common.Models
{
    public class AppSettings
    {
        public int ThumbnailCacheCapacity { get; set; } = 200;
        public int HighResCacheCapacity { get; set; } = 20;
        public int HighResMaxDimension { get; set; } = 2560;
    }
}
