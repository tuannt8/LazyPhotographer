using System;
using System.Collections.Generic;
using Avalonia.Media.Imaging;
using LazyPhotographer.Common.Services;

namespace LazyPhotographer.Avalonia.Services
{
    public class ThumbnailCache
    {
        private static ThumbnailCache? _instance;
        public static ThumbnailCache Shared => _instance ??= new ThumbnailCache(SettingsService.Current.ThumbnailCacheCapacity);

        private int _maxCapacity;
        private readonly Dictionary<string, LinkedListNode<CacheItem>> _map;
        private readonly LinkedList<CacheItem> _lruList;
        private readonly object _lock = new();

        private class CacheItem
        {
            public string Key { get; }
            public Bitmap Bitmap { get; }
            public long ApproxBytes { get; }

            public CacheItem(string key, Bitmap bitmap)
            {
                Key = key;
                Bitmap = bitmap;
                ApproxBytes = bitmap.PixelSize.Width * bitmap.PixelSize.Height * 4L;
            }
        }

        public ThumbnailCache(int maxCapacity = 200)
        {
            _maxCapacity = maxCapacity;
            _map = new Dictionary<string, LinkedListNode<CacheItem>>(StringComparer.OrdinalIgnoreCase);
            _lruList = new LinkedList<CacheItem>();

            SettingsService.SettingsChanged += s =>
            {
                MaxCapacity = s.ThumbnailCacheCapacity;
            };
        }

        public int MaxCapacity
        {
            get => _maxCapacity;
            set
            {
                lock (_lock)
                {
                    _maxCapacity = Math.Max(10, value);
                    EvictExcess();
                }
            }
        }

        public int Count
        {
            get
            {
                lock (_lock) return _map.Count;
            }
        }

        public long EstimatedMemoryBytes
        {
            get
            {
                lock (_lock)
                {
                    long total = 0;
                    foreach (var item in _lruList)
                    {
                        total += item.ApproxBytes;
                    }
                    return total;
                }
            }
        }

        public bool TryGet(string key, out Bitmap? bitmap)
        {
            lock (_lock)
            {
                if (_map.TryGetValue(key, out var node))
                {
                    _lruList.Remove(node);
                    _lruList.AddFirst(node);
                    bitmap = node.Value.Bitmap;
                    return true;
                }
            }
            bitmap = null;
            return false;
        }

        public void Put(string key, Bitmap bitmap)
        {
            lock (_lock)
            {
                if (_map.TryGetValue(key, out var existingNode))
                {
                    _lruList.Remove(existingNode);
                    _lruList.AddFirst(existingNode);
                    return;
                }

                var item = new CacheItem(key, bitmap);
                var newNode = _lruList.AddFirst(item);
                _map[key] = newNode;

                EvictExcess();
            }
        }

        private void EvictExcess()
        {
            while (_map.Count > _maxCapacity && _lruList.Last != null)
            {
                var oldest = _lruList.Last.Value;
                _lruList.RemoveLast();
                _map.Remove(oldest.Key);
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _map.Clear();
                _lruList.Clear();
            }
            GC.Collect(1, GCCollectionMode.Optimized);
        }
    }
}
