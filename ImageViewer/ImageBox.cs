using FreeImageAPI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ImageViewer
{
    public class ImageBox : Control, IReceiveImage
    {
        public string FileName { get; set; }

        private Point _pan = new Point(0, 0);
        Brush _backgroundBrush = Brushes.Black;
        private TwoStepImageCache _cache;
        private string _optimizedImageFileName;
        private ImageMeta _image;
        private Bitmap _optimizedScreenSizeImage;
        private TextRenderer _messageRenderer;

        public int MaximumCachedFiles { get; set; }

        public ImageBox()
        {
            _cache = new TwoStepImageCache(new ImageLoader(), new QuickImageLoader(), this, 5);
            _messageRenderer = new TextRenderer(this);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        private decimal zoom = 0;
        public decimal Zoom
        {
            get => zoom;
            set
            {
                zoom = value;
                Invalidate();
            }
        }

        private int rotation = 0;
        public int Rotation
        {
            get => rotation;
            internal set
            {
                rotation = value;
                while (rotation >= 360)
                {
                    rotation -= 360;
                }
                while(rotation<0)
                {
                    rotation += 360;
                }
                Invalidate();
            }
        }

        public void ResetTransform()
        {
            _pan = new Point(0, 0);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        }

        public void LoadImage(string fileName)
        {
            if (fileName == null)
                return;

            FileName = fileName;
            SetImage(_cache.GetOrLoadImage(fileName));
        }

        // ----------------- Rendering ------------------

        /// <summary>
        /// Just call Invalidate...
        /// </summary>
        /// <param name="isNewImage"></param>
        public void Render(bool isNewImage)
        {
            // If displaying a new image, reset zoom/pan/rotate
            if(isNewImage)
            {
                zoom = 0;
                _pan = new Point(0, 0);
                rotation = 0;
            }
            Invalidate();
        }

        /// <summary>
        /// Render the picture
        /// </summary>
        /// <param name="g"></param>
        private void Render(Graphics g)
        {
            decimal zoom = Zoom;
            // Black background
            g.FillRectangle(_backgroundBrush, this.ClientRectangle);

            if (_image != null)
            {
                Bitmap imageToPaint = _image.Image;

                //if(!string.IsNullOrEmpty(pMessageText))
                //{
                //    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
                //    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
                //    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;
                //    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighSpeed;
                //}

                var (w, h) = rotation switch
                {
                    0 => (_image.ActualWidth, _image.ActualHeight),
                    180 => (_image.ActualWidth, _image.ActualHeight),
                    _ => (_image.ActualHeight, _image.ActualWidth),
                };

                bool useOptimizedImage = false;
                if (zoom == 0)
                {
                    // "Best fit"
                    if ((w < this.Width) && (h < this.Height))
                    {
                        zoom = 1;
                    }
                    else
                    {
                        decimal propW = (decimal)this.Width / (decimal)w;
                        decimal propH = (decimal)this.Height / (decimal)h;

                        zoom = Math.Min(propW, propH);
                        useOptimizedImage = true;
                    }
                }

                int newWidth = (int)((decimal)_image.ActualWidth * zoom);
                int newHeight = (int)((decimal)_image.ActualHeight * zoom);

                if (useOptimizedImage)
                {
                    if (_optimizedImageFileName != FileName)
                    {
                        if (_optimizedScreenSizeImage != null)
                        {
                            _optimizedScreenSizeImage.Dispose();
                        }
                        _optimizedScreenSizeImage = new Bitmap(_image.Image, new Size(newWidth, newHeight));
                        imageToPaint = _optimizedScreenSizeImage;
                        _optimizedImageFileName = FileName;
                    }
                    else if (_optimizedScreenSizeImage != null)
                    {
                        imageToPaint = _optimizedScreenSizeImage;
                    }
                }

                if (imageToPaint != null)
                {
                    g.TranslateTransform(this.Width / 2, this.Height / 2);
                    g.TranslateTransform(_pan.X, _pan.Y);
                    g.RotateTransform(rotation);
                    g.TranslateTransform(-(newWidth / 2), -(newHeight / 2));
                    g.DrawImage(imageToPaint, 0, 0, newWidth, newHeight);
                    g.ResetTransform();
                }
            }

            // Draw the message
            _messageRenderer.Render(g);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // Prevent background painting.
            //base.OnPaintBackground(pevent);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // Render
            Render(e.Graphics);
        }

        private bool pMouseDown = false;
        private Point pLastPoint;
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if ((e.Button == MouseButtons.Left) && (zoom != 0))
            {
                pMouseDown = true;
                pLastPoint = e.Location;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if ((e.Button == MouseButtons.Left) && (pMouseDown))
            {
                var dx = pLastPoint.X - e.Location.X;
                var dy = pLastPoint.Y - e.Location.Y;
                _pan.X -= dx;
                _pan.Y -= dy;
                pLastPoint = e.Location;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if(e.Button== MouseButtons.Left)
            {
                pMouseDown = false;
            }
            base.OnMouseUp(e);
        }


        // -------------- Text message UI ----------------
        public void ReceiveImage(ImageMeta img)
        {
            SetImage(img);
        }

        private void SetImage(ImageMeta img)
        {
            if (img.FileName != FileName)
                return;

            _image = img;
            _backgroundBrush.Dispose();
            _backgroundBrush = new SolidBrush(img.AverageColor);
            _optimizedImageFileName = "";
            Render(true);
        }
    }
}
