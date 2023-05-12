using System;
using FreeImageAPI;
using System.Drawing;
using System.Linq;
using System.Diagnostics;
using ImageViewer.Properties;

namespace ImageViewer
{
    public class ImageLoader : IImageLoader
    {
        public ImageMeta LoadImage(string fileName)
        {
            var bmp = LoadBitmap(fileName);
            Debug.WriteLine("Slow done!");
            return new ImageMeta
            {
                Image = bmp,
                FileName = fileName,
                IsFullResolution = true,
                ActualHeight = bmp?.Height ?? 0,
                ActualWidth = bmp?.Width ?? 0,
                AverageColor = GetAverageColor(bmp)
            };
        }

        private Color GetAverageColor(Bitmap bmp)
        {
            if(bmp == null)
            {
                return Color.Black;
            }

            if (bmp.Width < 2)
            {
                return bmp.GetPixel(0, 0);
            }

            var colors = new[]
            {
                bmp.GetPixel(0,0),
                bmp.GetPixel(0,bmp.Height-1),
                bmp.GetPixel(bmp.Width-1,0),
                bmp.GetPixel(bmp.Width-1,bmp.Height-1),
                bmp.GetPixel(bmp.Width/2,0),
                bmp.GetPixel(0,bmp.Height/2),
                bmp.GetPixel(bmp.Width/2,bmp.Height-1),
                bmp.GetPixel(bmp.Width-1,bmp.Height/2),
            };

            return Color.FromArgb(
                (int)colors.Average(c => c.R),
                (int)colors.Average(c => c.G),
                (int)colors.Average(c => c.B)
                );
        }


        /// <summary>
        /// Load a FreeImage bitmap specifying different flags according to the file extension
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private Bitmap LoadBitmap(string fileName)
        {
            FREE_IMAGE_LOAD_FLAGS flags = FREE_IMAGE_LOAD_FLAGS.DEFAULT;
            using var counter = new PerformanceTimer(nameof(ImageLoader));

            // Rotate Jpegs if possible
            if (fileName.EndsWith("jpg", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith("jpeg", StringComparison.OrdinalIgnoreCase))
            {
                flags = FREE_IMAGE_LOAD_FLAGS.JPEG_ACCURATE | FREE_IMAGE_LOAD_FLAGS.JPEG_EXIFROTATE;
            }

            // Load the image from disk
            try
            {
                var bmp = new FreeImageBitmap(fileName, flags);

                // Convert the image to bitmap
                if (bmp.ImageType != FREE_IMAGE_TYPE.FIT_BITMAP)
                {
                    bmp.ConvertType(FREE_IMAGE_TYPE.FIT_BITMAP, true);
                }

                // ensure the bitmap is accessible
                bmp.GetPixel(0, 0);

                return bmp.ToBitmap();
            }
            catch
            {
                try
                {
                    return Image.FromFile(fileName) as Bitmap;
                }
                catch
                {
                    return Properties.Resources.ResourceManager.GetObject("broken") as Bitmap;
                }
            }
        }

    }
}