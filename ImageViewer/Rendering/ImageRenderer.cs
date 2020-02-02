using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ImageViewer.Rendering
{
    public class ImageRenderer
    {
        private Bitmap _optimizedScreenSizeImage;

        private IRenderControl _control;
        private string _optimizedImageFileName;
        private int _optimizedImageRotation;

        public ImageRenderer(IRenderControl control)
        {
            _control = control;
        }
        
        public void Render(Graphics g, ImageMeta image)
        {
            decimal zoom = _control.Zoom;
            int w, h;
            bool isRotated = false;
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
                isRotated = true;
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

            int newWidth = (int)((decimal)image.ActualWidth * zoom);
            int newHeight = (int)((decimal)image.ActualHeight * zoom);
            
            Bitmap imageToPaint = image.Image;
            if (useOptimizedImage)
            {
                imageToPaint = GetOptimizedImage(image, isRotated, newWidth, newHeight);
            }

            if (imageToPaint != null)
            {
                g.TranslateTransform(_control.Width / 2, _control.Height / 2);
                g.TranslateTransform(_control.Pan.X, _control.Pan.Y);
                g.RotateTransform(_control.Rotation);
                g.TranslateTransform(-(newWidth / 2), -(newHeight / 2));
                g.DrawImage(imageToPaint, 0, 0, newWidth, newHeight);
                g.ResetTransform();
            }

        }

        private Bitmap GetOptimizedImage(ImageMeta image, bool isRotated, int newWidth, int newHeight)
        {
            if ((_optimizedImageFileName != image.FileName) || (_optimizedImageRotation != _control.Rotation))
            {
                if (_optimizedScreenSizeImage != null)
                {
                    _optimizedScreenSizeImage.Dispose();
                }
                _optimizedScreenSizeImage = new Bitmap(image.Image, newWidth, newHeight);
                _optimizedImageFileName = image.FileName;
                _optimizedImageRotation = _control.Rotation;
            }
            return _optimizedScreenSizeImage;
        }
    }
}
