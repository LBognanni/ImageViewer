using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ImageViewer.Rendering
{
    public class ImageRenderer
    {
        private Bitmap _optimizedScreenSizeImage;

        private IRenderControl _control;
        private string _optimizedImageFileName = "";
        private int _optimizedImageRotation = 0;

        public ImageRenderer(IRenderControl control)
        {
            _control = control;
        }
        
        public void Render(Graphics g, ImageMeta image)
        {
            decimal zoom = _control.Zoom;
            int w, h;
            bool useOptimizedImage = false;

            if ((_control.Rotation == 0) || (_control.Rotation == 180))
            {
                w = image.ActualWidth;
                h = image.ActualHeight;
            }
            else
            {
                w = image.ActualHeight;
                h = image.ActualWidth;
            }

            if (zoom == 0)
            {
                // "Best fit"
                if ((w < _control.Width) && (h < _control.Height))
                {
                    zoom = 1;
                }
                else
                {
                    decimal propW = (decimal)_control.Width / (decimal)w;
                    decimal propH = (decimal)_control.Height / (decimal)h;

                    zoom = Math.Min(propW, propH);
                    useOptimizedImage = true;
                }
            }

            
            Bitmap imageToPaint = image.Image;

            if (imageToPaint != null)
            {
                int newWidth = (int)((decimal)image.ActualWidth * zoom);
                int newHeight = (int)((decimal)image.ActualHeight * zoom);
                if (useOptimizedImage)
                {
                    imageToPaint = GetOptimizedImage(image, newWidth, newHeight);
                }
                g.TranslateTransform(_control.Width / 2, _control.Height / 2);
                g.TranslateTransform(_control.Pan.X, _control.Pan.Y);
                g.RotateTransform(_control.Rotation);
                g.TranslateTransform(-(newWidth / 2), -(newHeight / 2));
                try
                {
                    g.DrawImage(imageToPaint, 0, 0, newWidth, newHeight);
                }
                catch
                {
                }
                g.ResetTransform();
            }

        }

        private Bitmap GetOptimizedImage(ImageMeta image, int newWidth, int newHeight)
        {
            if ((_optimizedImageFileName != image.FileName) || (_optimizedImageRotation != _control.Rotation))
            {
                InvalidateCache();
                _optimizedScreenSizeImage = ResizeBitmap(image.Image, newWidth, newHeight);
                _optimizedImageFileName = image.FileName;
                _optimizedImageRotation = _control.Rotation;
            }
            return _optimizedScreenSizeImage;
        }

        private Bitmap ResizeBitmap(Bitmap imageImage, int newWidth, int newHeight)
        {
            var dest = new Bitmap(newWidth, newHeight);
            using var g = Graphics.FromImage(dest);
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(imageImage, Rect(dest.Size), Rect(imageImage.Size), GraphicsUnit.Pixel);
            return dest;
        }

        private Rectangle Rect(Size sz) => new(Point.Empty, sz);

        internal void InvalidateCache()
        {
            if(_optimizedScreenSizeImage!=null)
            {
                _optimizedScreenSizeImage.Dispose();
                _optimizedScreenSizeImage = null;
                _optimizedImageFileName = "";
            }
        }
    }
}
