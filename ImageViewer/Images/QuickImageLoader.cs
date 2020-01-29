using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageViewer
{
    public class QuickImageLoader : IImageLoader
    {
        public ImageMeta LoadImage(string fileName)
        {
            var bmp = LoadBitmap(fileName);
            if(bmp != null)
            {
                bmp.FileName = fileName;
                bmp.IsFullResImage = false;
                bmp.AverageColor = GetAverageColor(bmp.Image);
            }

            return bmp;
        }

        private Color GetAverageColor(Bitmap image)
        {
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
            using (Microsoft.WindowsAPICodePack.Shell.ShellFile file = Microsoft.WindowsAPICodePack.Shell.ShellFile.FromFilePath(fileName))
            {
                var img = file.Thumbnail.ExtraLargeBitmap;
                if (img == null)
                    return null;

                Console.WriteLine("Quick done!");
                return new ImageMeta
                {
                    Image = file.Thumbnail.ExtraLargeBitmap,
                    ActualWidth = (int?)file.Properties.System?.Image?.HorizontalSize?.Value ?? img.Width,
                    ActualHeight = (int?)file.Properties.System?.Image?.VerticalSize?.Value ?? img.Height
                };
            }
        }
    }

}
