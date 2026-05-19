using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media.Imaging;

namespace PhotoFolderViewer.Services;

public sealed class ImageCacheService
{
    private const int MaxFullImageCacheSize = 16;
    private const int MaxThumbnailCacheSize = 512;

    private readonly object _fullCacheLock = new();
    private readonly object _thumbCacheLock = new();

    private readonly Dictionary<string, BitmapSource> _fullCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, BitmapSource> _thumbCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly LinkedList<string> _fullLru = new();
    private readonly LinkedList<string> _thumbLru = new();

    private readonly ConcurrentDictionary<string, Task<BitmapSource>> _inFlightFullLoads = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Task<BitmapSource>> _inFlightThumbLoads = new(StringComparer.OrdinalIgnoreCase);

    public Task<BitmapSource> GetOrLoadFullAsync(string path, CancellationToken cancellationToken)
    {
        if (TryGetFromCache(path, _fullCache, _fullLru, _fullCacheLock, out var cached))
        {
            return Task.FromResult(cached);
        }

        var loadTask = _inFlightFullLoads.GetOrAdd(path, _ => LoadFullImageInternalAsync(path, cancellationToken));
        return AwaitAndFinalizeAsync(path, loadTask, _inFlightFullLoads, _fullCache, _fullLru, _fullCacheLock, MaxFullImageCacheSize, cancellationToken);
    }

    public Task<BitmapSource> GetOrLoadThumbnailAsync(string path, int decodePixelWidth, CancellationToken cancellationToken)
    {
        if (TryGetFromCache(path, _thumbCache, _thumbLru, _thumbCacheLock, out var cached))
        {
            return Task.FromResult(cached);
        }

        var loadTask = _inFlightThumbLoads.GetOrAdd(path, _ => LoadThumbnailInternalAsync(path, decodePixelWidth, cancellationToken));
        return AwaitAndFinalizeAsync(path, loadTask, _inFlightThumbLoads, _thumbCache, _thumbLru, _thumbCacheLock, MaxThumbnailCacheSize, cancellationToken);
    }

    private static bool TryGetFromCache(
        string path,
        Dictionary<string, BitmapSource> cache,
        LinkedList<string> lru,
        object cacheLock,
        out BitmapSource image)
    {
        lock (cacheLock)
        {
            if (cache.TryGetValue(path, out image!))
            {
                TouchLru(lru, path);
                return true;
            }
        }

        image = null!;
        return false;
    }

    private static async Task<BitmapSource> AwaitAndFinalizeAsync(
        string path,
        Task<BitmapSource> loadTask,
        ConcurrentDictionary<string, Task<BitmapSource>> inFlight,
        Dictionary<string, BitmapSource> cache,
        LinkedList<string> lru,
        object cacheLock,
        int maxSize,
        CancellationToken cancellationToken)
    {
        try
        {
            var image = await loadTask.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            lock (cacheLock)
            {
                cache[path] = image;
                TouchLru(lru, path);
                TrimCache(cache, lru, maxSize);
            }

            return image;
        }
        finally
        {
            inFlight.TryRemove(path, out _);
        }
    }

    private static Task<BitmapSource> LoadFullImageInternalAsync(string path, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var fs = File.OpenRead(path);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.StreamSource = fs;
            image.EndInit();
            image.Freeze();
            return (BitmapSource)image;
        }, cancellationToken);
    }

    private static Task<BitmapSource> LoadThumbnailInternalAsync(string path, int decodePixelWidth, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var fs = File.OpenRead(path);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.DecodePixelWidth = decodePixelWidth;
            image.StreamSource = fs;
            image.EndInit();
            image.Freeze();
            return (BitmapSource)image;
        }, cancellationToken);
    }

    private static void TouchLru(LinkedList<string> lru, string key)
    {
        var existing = lru.Find(key);
        if (existing is not null)
        {
            lru.Remove(existing);
        }

        lru.AddFirst(key);
    }

    private static void TrimCache(Dictionary<string, BitmapSource> cache, LinkedList<string> lru, int maxSize)
    {
        while (cache.Count > maxSize && lru.Last is not null)
        {
            var victimKey = lru.Last.Value;
            lru.RemoveLast();
            cache.Remove(victimKey);
        }
    }
}
