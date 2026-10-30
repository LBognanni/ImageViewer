using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ImageViewer;

/// <summary>
/// The list of files being browsed and the position in it. Not thread safe: use it from the UI thread.
/// </summary>
public sealed class Navigator
{
    private const int PrefetchAhead = 4;
    private const int PrefetchBehind = 1;
    private static readonly TimeSpan RapidThreshold = TimeSpan.FromMilliseconds(150);

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    private readonly IImageWindow _window;
    private readonly Func<DateTime> _now;
    private List<string> _files = new();
    private int _index;
    private int _direction = 1;
    private DateTime _lastMove = DateTime.MinValue;

    public Navigator(IImageWindow window, Func<DateTime>? now = null)
    {
        _window = window;
        _now = now ?? (() => DateTime.UtcNow);
    }

    public event Action? CurrentChanged;

    public string? Current => _files.Count == 0 ? null : _files[_index];
    public int Count => _files.Count;

    /// <summary>
    /// The file most likely to be shown next
    /// </summary>
    public string? Upcoming => _files.Count < 2 ? null : _files[(_index + _direction + _files.Count) % _files.Count];

    /// <summary>
    /// Start showing a file right away. Its folder can follow later through <see cref="ApplyFolder"/>.
    /// </summary>
    public void Open(string fileName)
    {
        _files = new List<string> { fileName };
        _index = 0;
        _direction = 1;
        OnMoved(false);
    }

    /// <summary>
    /// Find the images in a folder. Slow on large folders, so this runs in the background.
    /// </summary>
    public static Task<List<string>> ListFolder(string directory, IReadOnlySet<string> extensions) =>
        Task.Run(() => Directory.EnumerateFiles(directory)
            .Where(f => extensions.Contains(Path.GetExtension(f)))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList());

    /// <summary>
    /// Replace the list with the folder's contents, keeping the current file on screen
    /// even if it's not one of the formats that the folder listing recognizes.
    /// </summary>
    public void ApplyFolder(List<string> files)
    {
        var current = Current;
        if (current == null)
            return;

        var index = files.FindIndex(f => f.Equals(current, PathComparison));
        if (index < 0)
        {
            index = ~files.BinarySearch(current, StringComparer.OrdinalIgnoreCase);
            files.Insert(Math.Max(0, index), current);
            index = Math.Max(0, index);
        }

        _files = files;
        _index = index;
        UpdateWindow(false);
    }

    public void Next() => Move(1);

    public void Previous() => Move(-1);

    public void Shuffle()
    {
        var current = Current;
        if (current == null)
            return;

        var shuffled = _files.ToArray();
        Random.Shared.Shuffle(shuffled);
        _files = shuffled.ToList();
        _index = _files.IndexOf(current);
        UpdateWindow(false);
    }

    /// <summary>
    /// Take the current file out of the list and move on to the next one
    /// </summary>
    public void RemoveCurrent()
    {
        if (_files.Count == 0)
            return;

        _files.RemoveAt(_index);
        if (_index >= _files.Count)
        {
            _index = 0;
        }
        _direction = 1;
        OnMoved(false);
    }

    private void Move(int direction)
    {
        if (_files.Count == 0)
            return;

        _direction = direction;
        _index = (_index + direction + _files.Count) % _files.Count;

        var now = _now();
        var rapid = now - _lastMove < RapidThreshold;
        _lastMove = now;
        OnMoved(rapid);
    }

    private void OnMoved(bool rapid)
    {
        UpdateWindow(rapid);
        CurrentChanged?.Invoke();
    }

    private void UpdateWindow(bool rapid)
    {
        _window.SetWindow(GetFilesByPriority(), rapid);
    }

    private List<string> GetFilesByPriority()
    {
        var result = new List<string>();
        if (_files.Count == 0)
            return result;

        void Add(int offset)
        {
            var file = _files[((_index + offset) % _files.Count + _files.Count) % _files.Count];
            if (!result.Contains(file))
            {
                result.Add(file);
            }
        }

        Add(0);
        Add(_direction);
        for (var i = 1; i <= PrefetchBehind; i++)
        {
            Add(-i * _direction);
        }
        for (var i = 2; i <= PrefetchAhead; i++)
        {
            Add(i * _direction);
        }
        return result;
    }
}
