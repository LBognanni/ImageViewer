using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using SkiaSharp;

namespace ImageViewer;

/// <summary>
/// Shows the best available image for a file, and holds the view state: zoom, pan and rotation.
/// </summary>
public sealed class ImageSurface : Avalonia.Controls.Control
{
    private const long QuietMilliseconds = 150;

    private readonly ImagePipeline _pipeline;
    private readonly TextureCache _textures = new();
    private readonly Stopwatch _sinceShow = Stopwatch.StartNew();
    private string? _path;
    private string? _upcoming;
    private string? _preloaded;
    private ImageHandle? _image;
    private ImageHandle? _preload;
    private Point _pan;
    private decimal _zoom;
    private int _rotation;
    private bool _dragging;
    private Point _lastPoint;

    public ImageSurface(ImagePipeline pipeline)
    {
        _pipeline = pipeline;
        ClipToBounds = true;
        // Key events only flow when something in the window has focus
        Focusable = true;
    }

    public decimal DefaultZoom { get; set; }

    public decimal Zoom
    {
        get => _zoom;
        set
        {
            _zoom = value;
            RequestFullIfNeeded();
            InvalidateVisual();
        }
    }

    public int Rotation
    {
        get => _rotation;
        set
        {
            _rotation = (value % 360 + 360) % 360;
            RequestFullIfNeeded();
            InvalidateVisual();
        }
    }

    public void ResetTransform()
    {
        _pan = default;
        InvalidateVisual();
    }

    /// <summary>
    /// Switch to a file. The previous image stays up until something from the new one is ready.
    /// </summary>
    /// <param name="path">The file to show</param>
    /// <param name="upcoming">The file likely to be shown next</param>
    public void Show(string? path, string? upcoming)
    {
        _path = path;
        _upcoming = upcoming;
        _zoom = DefaultZoom;
        _pan = default;
        _rotation = 0;
        _sinceShow.Restart();
        Refresh();
    }

    /// <summary>
    /// The folder listing arrived, so now there's a next file
    /// </summary>
    public void SetUpcoming(string? upcoming)
    {
        _upcoming = upcoming;
        PreloadUpcoming();
    }

    /// <summary>
    /// A decode finished for this file
    /// </summary>
    public void OnImageReady(string path)
    {
        if (path == _path)
        {
            Refresh();
        }
        else if (path == _upcoming)
        {
            PreloadUpcoming();
        }
    }

    private void Refresh()
    {
        if (_path != null)
        {
            var best = _pipeline.AcquireBest(_path);
            if (best != null)
            {
                if (best == _image)
                {
                    best.Release();
                }
                else
                {
                    _image?.Release();
                    _image = best;
                    RequestFullIfNeeded();
                    InvalidateVisual();
                }
            }
        }
        PreloadUpcoming();
    }

    /// <summary>
    /// Upload the next image to the GPU while nothing else is going on, so even its first frame is cheap
    /// </summary>
    private void PreloadUpcoming()
    {
        if (_upcoming == null || _upcoming == _preloaded || _sinceShow.ElapsedMilliseconds < QuietMilliseconds)
            return;

        var handle = _pipeline.AcquireBest(_upcoming);
        if (handle == null)
            return;

        // Instant images get replaced in a moment, so wait for a better one
        if (handle.Tier == ImageTier.Instant)
        {
            handle.Release();
            return;
        }

        _preload?.Release();
        _preload = handle;
        _preloaded = _upcoming;
        InvalidateVisual();
    }

    private void RequestFullIfNeeded()
    {
        if (_image == null || _image.Tier == ImageTier.Full)
            return;

        var (viewWidth, viewHeight) = ViewSize();
        var zoom = ViewGeometry.EffectiveZoom(_image.Source, _rotation, _zoom, viewWidth, viewHeight);
        if (_image.Source.Width * zoom > _image.Image.Width * 1.01f)
        {
            _pipeline.RequestFull(_image.Path);
        }
    }

    protected override void OnSizeChanged(Avalonia.Controls.SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        RequestFullIfNeeded();
    }

    private double Scaling => Avalonia.Controls.TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    private (float Width, float Height) ViewSize() =>
        ((float)(Bounds.Width * Scaling), (float)(Bounds.Height * Scaling));

    public override void Render(DrawingContext context)
    {
        var scaling = Scaling;
        var (viewWidth, viewHeight) = ViewSize();
        var image = _image != null && _image.TryAcquire() ? _image : null;
        var matrix = SKMatrix.Identity;
        if (image != null)
        {
            var pan = new SKPoint((float)(_pan.X * scaling), (float)(_pan.Y * scaling));
            matrix = ViewGeometry.Transform(image.Source, image.Image.Width, image.Image.Height, _rotation, _zoom, pan,
                viewWidth, viewHeight);
        }

        context.Custom(new ImageDrawOperation(new Rect(Bounds.Size), image, _preload, matrix,
            image?.Background ?? SKColors.Black, scaling, _textures));
        _preload = null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed && _zoom != 0)
        {
            _dragging = true;
            _lastPoint = point.Position;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging)
        {
            var position = e.GetPosition(this);
            _pan += position - _lastPoint;
            _lastPoint = position;
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
    }
}
