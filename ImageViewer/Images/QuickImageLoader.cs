using System.Drawing;
using System.Linq;
using ImageViewer.Properties;

namespace ImageViewer
{
    public class QuickImageLoader : IImageLoader
    {
        public ImageMeta LoadImage(string fileName)
        {
            using var counter = new PerformanceTimer(nameof(QuickImageLoader));
            var bmp = LoadBitmap(fileName);
            bmp.FileName = fileName;
            bmp.IsFullResolution = false;
            bmp.AverageColor = GetAverageColor(bmp.Image);
            return bmp;
        }

        private Color GetAverageColor(Bitmap? image)
        {
            if(image == null)
            {
                return Color.Black;
            }
            
            var colors = new[] {
                image.GetPixel(0, 0),
                image.GetPixel(0, image.Height-1),
                image.GetPixel(image.Width-1, image.Height-1),
                image.GetPixel(image.Width-1, 0)
            };

            return Color.FromArgb(
                (int)colors.Average(c => c.R),
                (int)colors.Average(c => c.G),
                (int)colors.Average(c => c.B)
                );
        }

        private ImageMeta LoadBitmap(string fileName)
        {
            // Can we use a thumbnail and load asynchronously?
            // using the Windows API code pack
            // PM> Install -Package WindowsAPICodePack-Shell
            using Microsoft.WindowsAPICodePack.Shell.ShellFile file = Microsoft.WindowsAPICodePack.Shell.ShellFile.FromFilePath(fileName);
            var img = file.Thumbnail.ExtraLargeBitmap ?? (Resources.ResourceManager.GetObject("broken") as Bitmap)!;

            return new ImageMeta(img)
            {
                ActualWidth = (int?)file.Properties.System?.Image?.HorizontalSize?.Value ?? img.Width,
                ActualHeight = (int?)file.Properties.System?.Image?.VerticalSize?.Value ?? img.Height
            };
        }
    }

}
