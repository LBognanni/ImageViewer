using NUnit.Framework;
using NUnit.Framework.Legacy;
using SkiaSharp;

namespace ImageViewer.Tests;

/// <summary>
/// Headless windows have no GPU, so they never reach the shader. This runs it on the CPU instead.
/// </summary>
[TestFixture]
public class DownscaleShaderTests
{
    [Test]
    public void TheShaderCompiles_AndDrawsTheImage()
    {
        using var source = new SKBitmap(400, 400);
        source.Erase(SKColors.Red);
        using var image = SKImage.FromBitmap(source);
        using var target = new SKBitmap(100, 100);
        target.Erase(SKColors.Black);

        using (var canvas = new SKCanvas(target))
        {
            canvas.Scale(.25f);
            ImageDrawOperation.DrawDownscaled(canvas, image, 4);
        }

        ClassicAssert.AreEqual(SKColors.Red, target.GetPixel(50, 50));
    }

    [Test]
    public void ShrinkingAveragesFineDetail()
    {
        // Alternating black and white columns should average to grey, not alias to either
        using var source = new SKBitmap(400, 400);
        for (var y = 0; y < 400; y++)
        {
            for (var x = 0; x < 400; x++)
            {
                source.SetPixel(x, y, x % 2 == 0 ? SKColors.Black : SKColors.White);
            }
        }
        using var image = SKImage.FromBitmap(source);
        using var target = new SKBitmap(100, 100);

        using (var canvas = new SKCanvas(target))
        {
            canvas.Scale(.25f);
            ImageDrawOperation.DrawDownscaled(canvas, image, 4);
        }

        var pixel = target.GetPixel(50, 50);
        ClassicAssert.Greater(pixel.Red, 90);
        ClassicAssert.Less(pixel.Red, 165);
    }
}
