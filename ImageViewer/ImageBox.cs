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

        public int MaximumCachedFiles { get; set; }

        public ImageBox()
        {
            _cache = new TwoStepImageCache(new ImageLoader(), new QuickImageLoader(), this, 5);
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

        public async Task LoadImage(string fileName)
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
                var (w, h) = rotation switch
                {
                    0 => (_image.ActualWidth, _image.ActualHeight),
                    180 => (_image.ActualWidth, _image.ActualHeight),
                    _ => (_image.ActualHeight, _image.ActualWidth),
                };

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
                    }
                }

                int newWidth = (int)((decimal)_image.ActualWidth * zoom);
                int newHeight = (int)((decimal)_image.ActualHeight * zoom);

                if (_image?.Image != null)
                {
                    g.TranslateTransform(this.Width / 2, this.Height / 2);
                    g.TranslateTransform(_pan.X, _pan.Y);
                    g.RotateTransform(rotation);
                    g.TranslateTransform(-(newWidth / 2), -(newHeight / 2));
                    g.DrawImage(_image.Image, 0, 0, newWidth, newHeight);
                    g.ResetTransform();
                }
            }

            // Draw the message
            if(!string.IsNullOrEmpty(pMessageText))
            {
                var strSize = g.MeasureString(pMessageText, this.Font);
                var textCenter = new RectangleF(
                        (this.Width / 2) - (strSize.Width / 2) - 20,
                        (this.Height - strSize.Height - 60),
                        strSize.Width + 40,
                        strSize.Height + 40
                    );

                using (SolidBrush black = new SolidBrush(Color.FromArgb((int)(pMessageAlpha * 200.0f), 0, 0, 0)))
                {
                    g.FillRectangle(black, textCenter);
                }

                using (SolidBrush white = new SolidBrush(Color.FromArgb((int)(pMessageAlpha * 255.0f), 255, 255, 255)))
                {
                    g.DrawString(pMessageText, this.Font, white, textCenter.Left + 20, textCenter.Top + 20);
                }
            }
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

        private Timer pMessageTimer = null;
        private float pMessageAlpha;
        private string pMessageText;
        private bool pMessageFadingIn = false;
        private int pTimerInterval = 0;
        private ImageMeta _image;

        private const int MESSAGE_FADE_INTERVAL = 50;
        private const float MESSAGE_FADE_AMOUNT = 0.2f;

        public bool HasMessage
        {
            get
            {
                return !string.IsNullOrEmpty(pMessageText);
            }
        }

        public void HideMessage()
        {
            if(pMessageTimer!= null)
            {
                pMessageTimer.Stop();
                pMessageFadingIn = false;
                pMessageTimer.Interval = MESSAGE_FADE_INTERVAL;
                pMessageTimer.Start();
            }
        }

        public void ShowMessage(string msg, int timeoutMS)
        {
            pMessageText = msg;
            pMessageAlpha = 0;
            pMessageFadingIn = true;

            pMessageTimer = new Timer();
            pMessageTimer.Tick += pMessageTimer_Tick;
            pTimerInterval = timeoutMS;
            pMessageTimer.Interval = MESSAGE_FADE_INTERVAL;
            pMessageTimer.Start();
        }

        private void pMessageTimer_Tick(object sender, EventArgs e)
        {

            if (pMessageFadingIn)
            {
                pMessageAlpha += MESSAGE_FADE_AMOUNT;

                if (pMessageAlpha >= 1)
                {
                    pMessageAlpha = 1;
                    pMessageTimer.Stop();
                    pMessageTimer.Interval = pTimerInterval;
                    pMessageFadingIn = false;
                    pMessageTimer.Start();
                }
            }
            else
            {
                if (pMessageTimer.Interval != MESSAGE_FADE_INTERVAL)
                {
                    pMessageTimer.Stop();
                    pMessageTimer.Interval = MESSAGE_FADE_INTERVAL;
                    pMessageTimer.Start();
                }

                pMessageAlpha -= MESSAGE_FADE_AMOUNT;

                if (pMessageAlpha <= 0)
                {
                    pMessageText = "";
                    pMessageTimer.Stop();
                }
            }
            Invalidate();
        }

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
            Render(true);
        }
    }
}
