using System;
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
    private readonly List<(ImageHandle Handle, SKImage Texture, bool Mipmapped)> _textures = new();

    public SKImage? Get(ImageHandle handle, GRContext context, bool mipmapped)
    {
        var index = _textures.FindIndex(t => t.Handle == handle);
        if (index >= 0)
        {
            var entry = _textures[index];
            _textures.RemoveAt(index);
            if (entry.Mipmapped || !mipmapped)
            {
                _textures.Add(entry);
                return entry.Texture;
            }
            entry.Texture.Dispose();
            handle.Release();
        }

        if (!handle.TryAcquire())
            return null;

        // Null when the image is larger than the GPU's texture size limit
        var texture = handle.Image.ToTextureImage(context, mipmapped);
        if (texture == null)
        {
            handle.Release();
            return null;
        }

        _textures.Add((handle, texture, mipmapped));
        if (_textures.Count > Capacity)
        {
            var (oldHandle, oldTexture, _) = _textures[0];
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
    /// <summary>
    /// The shader's nine samples cover a shrink of up to this much. Past it, mipmaps take over.
    /// </summary>
    private const float MaxShaderShrink = 6;

    private const string DownscaleShader = """
        uniform shader image;
        uniform float spacing;
        half4 main(float2 p) {
            half4 c = half4(0);
            for (int y = -1; y <= 1; y++) {
                for (int x = -1; x <= 1; x++) {
                    c += image.eval(p + float2(x, y) * spacing);
                }
            }
            return c / 9;
        }
        """;

    private static readonly SKSamplingOptions Linear = new(SKFilterMode.Linear);
    private static readonly SKSamplingOptions Mipmapped = new(SKFilterMode.Linear, SKMipmapMode.Linear);
    private static SKRuntimeShaderBuilder? _shader;

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
            var shrink = 1 / MathF.Sqrt(_matrix.ScaleX * _matrix.ScaleX + _matrix.SkewY * _matrix.SkewY);
            var useMipmaps = shrink > MaxShaderShrink;
            var texture = lease.GrContext is GRContext gr ? _textures.Get(_image, gr, useMipmaps) : null;

            canvas.Save();
            canvas.Scale((float)(1 / _scaling));
            canvas.Concat(_matrix);
            if (texture == null || useMipmaps)
            {
                canvas.DrawImage(texture ?? _image.Image, 0, 0, Mipmapped);
            }
            else if (shrink > 1)
            {
                DrawDownscaled(canvas, texture, shrink);
            }
            else
            {
                canvas.DrawImage(texture, 0, 0, Linear);
            }
            canvas.Restore();
        }

        if (layerPaint != null)
        {
            canvas.Restore();
        }

        if (_preload != null && lease.GrContext is GRContext context2)
        {
            _textures.Get(_preload, context2, false);
        }
    }

    /// <summary>
    /// Average several samples for each screen pixel. Building mipmaps takes about as long
    /// as the upload itself, and this needs none, for no real cost per frame.
    /// </summary>
    internal static void DrawDownscaled(SKCanvas canvas, SKImage texture, float shrink)
    {
        _shader ??= SKRuntimeEffect.BuildShader(DownscaleShader);

        using var image = texture.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, Linear);
        _shader.Children["image"] = image;
        _shader.Uniforms["spacing"] = 0.33f * shrink;
        using var shader = _shader.Build();
        using var paint = new SKPaint { Shader = shader };
        canvas.DrawRect(0, 0, texture.Width, texture.Height, paint);
    }
}
