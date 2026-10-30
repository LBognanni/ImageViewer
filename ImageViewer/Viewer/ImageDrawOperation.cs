using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace ImageViewer;

/// <summary>
/// GPU textures for recent images. A 45 MP image drawn from CPU memory costs over 100ms on
/// every frame; as a texture it costs one upload, and then well under a millisecond per frame.
/// Only touch this from the render thread.
/// </summary>
public sealed class TextureCache
{
    private const int Capacity = 4;
    private readonly List<(ImageHandle Handle, SKImage Texture)> _textures = new();

    public SKImage? Get(ImageHandle handle, GRContext context)
    {
        var index = _textures.FindIndex(t => t.Handle == handle);
        if (index >= 0)
        {
            var entry = _textures[index];
            _textures.RemoveAt(index);
            _textures.Add(entry);
            return entry.Texture;
        }

        if (!handle.TryAcquire())
            return null;

        // Null when the image is larger than the GPU's texture size limit
        var texture = handle.Image.ToTextureImage(context, true);
        if (texture == null)
        {
            handle.Release();
            return null;
        }

        _textures.Add((handle, texture));
        if (_textures.Count > Capacity)
        {
            var (oldHandle, oldTexture) = _textures[0];
            _textures.RemoveAt(0);
            oldTexture.Dispose();
            oldHandle.Release();
        }
        return texture;
    }
}

/// <summary>
/// Draws one frame on the render thread. Owns a reference to each handle it's given.
/// </summary>
public sealed class ImageDrawOperation : ICustomDrawOperation
{
    private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    private readonly ImageHandle? _image;
    private readonly ImageHandle? _preload;
    private readonly SKMatrix _matrix;
    private readonly SKColor _background;
    private readonly double _scaling;
    private readonly double _opacity;
    private readonly TextureCache _textures;

    public ImageDrawOperation(Rect bounds, ImageHandle? image, ImageHandle? preload, SKMatrix matrix, SKColor background,
        double scaling, double opacity, TextureCache textures)
    {
        _opacity = opacity;
        Bounds = bounds;
        _image = image;
        _preload = preload;
        _matrix = matrix;
        _background = background;
        _scaling = scaling;
        _textures = textures;
    }

    public Rect Bounds { get; }

    public bool HitTest(Point p) => true;

    public bool Equals(ICustomDrawOperation? other) => false;

    public void Dispose()
    {
        _image?.Release();
        _preload?.Release();
    }

    public void Render(ImmediateDrawingContext context)
    {
        var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (feature == null)
            return;

        using var lease = feature.Lease();
        var canvas = lease.SkCanvas;

        // Window.Opacity doesn't reach custom drawing. A layer fades the background
        // and the image as one, so one doesn't show through the other.
        using var layerPaint = _opacity < 1 ? new SKPaint { Color = SKColors.White.WithAlpha((byte)(_opacity * 255)) } : null;
        if (layerPaint != null)
        {
            // Nothing else clears the window, and a fade over the last frame would look like no fade
            canvas.Clear(SKColors.Transparent);
            canvas.SaveLayer(layerPaint);
        }

        using (var paint = new SKPaint { Color = _background })
        {
            canvas.DrawRect(0, 0, (float)Bounds.Width, (float)Bounds.Height, paint);
        }

        if (_image != null)
        {
            var image = (lease.GrContext is GRContext gr ? _textures.Get(_image, gr) : null) ?? _image.Image;
            canvas.Save();
            canvas.Scale((float)(1 / _scaling));
            canvas.Concat(_matrix);
            canvas.DrawImage(image, 0, 0, Sampling);
            canvas.Restore();
        }

        if (layerPaint != null)
        {
            canvas.Restore();
        }

        if (_preload != null && lease.GrContext is GRContext context2)
        {
            _textures.Get(_preload, context2);
        }
    }
}
