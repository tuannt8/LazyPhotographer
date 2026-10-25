using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using LazyPhotographer.Avalonia.Controls;
using LazyPhotographer.Avalonia.Models;

namespace LazyPhotographer.Avalonia.Services
{
    public static class ThumbnailLoader
    {
        private static readonly object _lock = new();
        private static readonly LinkedList<LoadRequest> _pendingQueue = new();
        private static readonly HashSet<string> _queuedFilePaths = new(StringComparer.OrdinalIgnoreCase);
        private static readonly SemaphoreSlim _concurrencyLimiter = new(3, 3);
        private static int _activeWorkerCount = 0;
        private const int MaxWorkers = 3;

        public record LoadRequest(PhotoItem Photo, WeakReference<FastThumbnailControl> ControlRef, CancellationTokenSource Cts);

        public static void RequestLoad(PhotoItem photo, FastThumbnailControl control)
        {
            if (photo.Thumbnail != null) return;

            // 1. Check in-memory cache first (instant)
            if (ThumbnailCache.Shared.TryGet(photo.FilePath, out var cached))
            {
                photo.Thumbnail = cached;
                return;
            }

            lock (_lock)
            {
                if (_queuedFilePaths.Contains(photo.FilePath)) return;
                _queuedFilePaths.Add(photo.FilePath);

                var cts = new CancellationTokenSource();
                var request = new LoadRequest(photo, new WeakReference<FastThumbnailControl>(control), cts);

                // LIFO: Add to front so currently visible items are processed first
                _pendingQueue.AddFirst(request);

                StartWorkersIfNeeded();
            }
        }

        private static void StartWorkersIfNeeded()
        {
            lock (_lock)
            {
                while (_activeWorkerCount < MaxWorkers && _pendingQueue.Count > 0)
                {
                    _activeWorkerCount++;
                    Task.Run(ProcessQueueAsync);
                }
            }
        }

        private static async Task ProcessQueueAsync()
        {
            while (true)
            {
                LoadRequest? req = null;
                lock (_lock)
                {
                    if (_pendingQueue.First != null)
                    {
                        req = _pendingQueue.First.Value;
                        _pendingQueue.RemoveFirst();
                    }
                    else
                    {
                        _activeWorkerCount--;
                        return;
                    }
                }

                if (req == null) return;

                await _concurrencyLimiter.WaitAsync();
                try
                {
                    if (req.Cts.IsCancellationRequested) continue;

                    // Check if control is still alive and attached to visual tree
                    if (req.ControlRef.TryGetTarget(out var control))
                    {
                        if (!control.IsControlAttached)
                        {
                            // Item scrolled out of view / detached: skip decoding!
                            continue;
                        }
                    }

                    // Check cache again (another worker might have decoded it)
                    if (ThumbnailCache.Shared.TryGet(req.Photo.FilePath, out var cached))
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            req.Photo.Thumbnail = cached;
                        }, DispatcherPriority.Background);
                        continue;
                    }

                    // Decode & apply rotation from metadata on background thread
                    var bitmap = await ThumbnailService.LoadThumbnailAsync(req.Photo, req.Cts.Token);
                    if (bitmap != null && !req.Cts.IsCancellationRequested)
                    {
                        // Refresh UI at Background priority so render & scroll threads are never blocked
                        Dispatcher.UIThread.Post(() =>
                        {
                            if (!req.Cts.IsCancellationRequested)
                            {
                                req.Photo.Thumbnail = bitmap;
                            }
                        }, DispatcherPriority.Background);
                    }
                }
                catch
                {
                    // Ignore background load failures
                }
                finally
                {
                    lock (_lock)
                    {
                        _queuedFilePaths.Remove(req.Photo.FilePath);
                    }
                    _concurrencyLimiter.Release();
                }

                // Small yield to prevent thread saturation
                await Task.Yield();
            }
        }

        public static void CancelLoad(string filePath)
        {
            lock (_lock)
            {
                var node = _pendingQueue.First;
                while (node != null)
                {
                    var next = node.Next;
                    if (string.Equals(node.Value.Photo.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                    {
                        node.Value.Cts.Cancel();
                        _pendingQueue.Remove(node);
                        _queuedFilePaths.Remove(filePath);
                        break;
                    }
                    node = next;
                }
            }
        }

        public static void Clear()
        {
            lock (_lock)
            {
                foreach (var req in _pendingQueue)
                {
                    req.Cts.Cancel();
                }
                _pendingQueue.Clear();
                _queuedFilePaths.Clear();
            }
        }
    }
}
