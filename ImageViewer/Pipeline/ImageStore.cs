using System.Collections.Generic;
using System.Linq;

namespace ImageViewer;

/// <summary>
/// Decoded images held to a byte budget. Eviction goes by distance from the current image, not
/// by last use: while browsing, what matters is what comes next. Not thread safe; the pipeline
/// serializes access.
/// </summary>
public sealed class ImageStore
{
    private const int Unranked = int.MaxValue;

    private readonly Dictionary<(string Path, ImageTier Tier), ImageHandle> _entries = new();
    private readonly long _budgetBytes;
    private IReadOnlyDictionary<string, int> _ranks = new Dictionary<string, int>();
    private long _bytes;

    public ImageStore(long budgetBytes)
    {
        _budgetBytes = budgetBytes;
    }

    public long Bytes => _bytes;

    /// <summary>
    /// Set each file's distance from the current image. Rank 0 is the current image, which
    /// is never evicted, even if it alone is over budget.
    /// </summary>
    public void SetRanks(IReadOnlyDictionary<string, int> ranks)
    {
        _ranks = ranks;
        Evict();
    }

    public void Add(ImageHandle handle)
    {
        RemoveEntry((handle.Path, handle.Tier));
        if (handle.Tier != ImageTier.Instant)
        {
            RemoveEntry((handle.Path, ImageTier.Instant));
        }
        _entries[(handle.Path, handle.Tier)] = handle;
        _bytes += handle.Bytes;
        Evict();
    }

    public bool Has(string path, ImageTier tier)
    {
        for (var t = ImageTier.Full; t >= tier; t--)
        {
            if (_entries.ContainsKey((path, t)))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Get the best image held for a file. The caller owns a reference and must release it.
    /// </summary>
    public ImageHandle? AcquireBest(string path)
    {
        for (var t = ImageTier.Full; t >= ImageTier.Instant; t--)
        {
            if (_entries.TryGetValue((path, t), out var handle) && handle.TryAcquire())
                return handle;
        }
        return null;
    }

    public void Remove(string path)
    {
        for (var t = ImageTier.Instant; t <= ImageTier.Full; t++)
        {
            RemoveEntry((path, t));
        }
    }

    private void Evict()
    {
        while (_bytes > _budgetBytes)
        {
            // Furthest first, and of equally far entries, the largest frees the most
            var victim = _entries
                .Where(e => Rank(e.Key.Path) > 0)
                .OrderByDescending(e => Rank(e.Key.Path))
                .ThenByDescending(e => e.Value.Bytes)
                .Select(e => (KeyValuePair<(string, ImageTier), ImageHandle>?)e)
                .FirstOrDefault();
            if (victim == null)
                return;
            RemoveEntry(victim.Value.Key);
        }
    }

    private int Rank(string path) => _ranks.TryGetValue(path, out var rank) ? rank : Unranked;

    private void RemoveEntry((string, ImageTier) key)
    {
        if (_entries.Remove(key, out var handle))
        {
            _bytes -= handle.Bytes;
            handle.Release();
        }
    }
}
