using FreeImageAPI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ImageViewer2
{
    public class ImageBox : Control
    {
        public string FileName { get; }

        private FreeImageBitmap pImage;
        private Bitmap pDisplayBitmap;
        private Point pPan = new Point(0, 0);
        Brush pBackgroundBrush = Brushes.Black;

        public ImageBox()
        {
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        private decimal pZoom = 0;
        public decimal Zoom
        {
            get
            {
                return pZoom;
            }
            set
            {
                pZoom = value;
                Invalidate();
            }
        }

        private int pRotation = 0;
        public int Rotation
        {
            get
            {
                return pRotation;
            }
            internal set
            {
                pRotation = value;
                while(pRotation>=360)
                {
                    pRotation -= 360;
                }
                RotateImage();
                Invalidate();
            }
        }

        private void RotateImage()
        {
            using (var img = pImage.GetRotatedInstance(pRotation))
            {
                pDisplayBitmap = img.ToBitmap();
                Invalidate();
            }
        }

        public void ResetTransform()
        {
            pPan = new Point(0, 0);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        }

        private BackgroundWorker _bwCache = null;
        private Queue<KeyValuePair<string, FreeImageBitmap>> _cache = new Queue<KeyValuePair<string, FreeImageBitmap>>();
        internal void CacheImage(string fileName)
        {
            // Setup backgroundworker
            if(_bwCache == null)
            {
                _bwCache = new BackgroundWorker();
                _bwCache.WorkerSupportsCancellation = true;
                _bwCache.DoWork += (s, e) =>
                {
                    string fn = e.Argument.ToString();
                    if (!_cache.Any(c => c.Key == fn))
                    {
                        var bmp = loadBitmap(fn);

                        if (!_bwCache.CancellationPending)
                        {
                            addToCache(fn, bmp);
                        }
                    }
                };
            }
            if (_bwCache.IsBusy)
            {
                _bwCache.CancelAsync();
                return;
            }
            _bwCache.RunWorkerAsync(fileName);
        }

        private void addToCache(string fileName, FreeImageBitmap bmp)
        {
            lock(_cache)
            {
                if (!_cache.Any(c => c.Key == fileName))
                {
                    _cache.Enqueue(new KeyValuePair<string, FreeImageBitmap>(fileName, bmp));

                    if (_cache.Count > 3)
                    {
                        var first = _cache.Dequeue();
                        first.Value.Dispose();
                    }
                }
            };
        }

        private FreeImageBitmap findInCache(string fileName)
        {
            var els = _cache.Where(e => e.Key == fileName);
            if (els.Any())
            {
                return els.First().Value;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Load a FreeImage bitmap specifying different flags according to the file extension
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private FreeImageBitmap loadBitmap(string fileName)
        {
            // Try from the cache first
            var bmp = findInCache(fileName);
            if (bmp != null)
                return bmp;

            FREE_IMAGE_LOAD_FLAGS flags = FREE_IMAGE_LOAD_FLAGS.DEFAULT;

            // Rotate Jpegs if possible
            if (fileName.EndsWith("jpg", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith("jpeg", StringComparison.OrdinalIgnoreCase))
            {
                //flags = FREE_IMAGE_LOAD_FLAGS.JPEG_EXIFROTATE;
                flags = FREE_IMAGE_LOAD_FLAGS.JPEG_FAST | FREE_IMAGE_LOAD_FLAGS.JPEG_EXIFROTATE;
            }

            // Load the image from disk
            try
            {
                bmp = new FreeImageBitmap(fileName, flags);

                // Convert the image to bitmap
                if (bmp.ImageType != FREE_IMAGE_TYPE.FIT_BITMAP)
                {
                    bmp.ConvertType(FREE_IMAGE_TYPE.FIT_BITMAP, true);
                }

                addToCache(fileName, bmp);
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Load and display an image
        /// </summary>
        /// <param name="fileName"></param>
        public void LoadImage(string fileName)
        {
            /*
            // Load the bitmap asynchronously
            FreeImageBitmap fBmp = await new Task<FreeImageBitmap>(() =>
            {
                return loadBitmap(fileName);
            });
            */
            FreeImageBitmap fBmp = loadBitmap(fileName);

            // Dispose previous image
            if ((pImage != null) && (!Object.ReferenceEquals(pImage, fBmp)))
            {
                if (!_cache.Any(c => Object.ReferenceEquals(c.Value, pImage)))
                {
                    pImage.Dispose();
                }
            }
            pImage = fBmp;

            // Dispose the old bitmap
            if (pDisplayBitmap != null)
            {
                pDisplayBitmap.Dispose();
                pDisplayBitmap = null;
            }

            if (pImage != null)
            {
                // Choose the background color as an average of a few points of the picture
                var colors = new[]
                {
                    fBmp.GetPixel(0,0),
                    fBmp.GetPixel(0,fBmp.Height-1),
                    fBmp.GetPixel(fBmp.Width-1,0),
                    fBmp.GetPixel(fBmp.Width-1,fBmp.Height-1),
                    fBmp.GetPixel(fBmp.Width/2,0),
                    fBmp.GetPixel(0,fBmp.Height/2),
                    fBmp.GetPixel(fBmp.Width/2,fBmp.Height-1),
                    fBmp.GetPixel(fBmp.Width-1,fBmp.Height/2),
                };
                pBackgroundBrush = new SolidBrush(Color.FromArgb(
                    (int)colors.Average(c => c.R),
                    (int)colors.Average(c => c.G),
                    (int)colors.Average(c => c.B)
                    ));

                // Render to bitmap
                pDisplayBitmap = fBmp.ToBitmap();
            }

            // Render the new image
            Render(true);
        }


        /// <summary>
        /// Just call Invalidate...
        /// </summary>
        /// <param name="isNewImage"></param>
        public void Render(bool isNewImage)
        {
            // If displaying a new image, reset zoom/pan/rotate
            if(isNewImage)
            {
                pZoom = 0;
                pPan = new Point(0, 0);
                pRotation = 0;
            }
            this.Invalidate();
        }

        /// <summary>
        /// Render the picture
        /// </summary>
        /// <param name="g"></param>
        private void Render(Graphics g)
        {
            decimal tZoom = pZoom;
            // Black background
            g.FillRectangle(pBackgroundBrush, this.ClientRectangle);

            if (pDisplayBitmap != null)
            {
                if (tZoom == 0)
                {
                    // "Best fit"
                    if ((pDisplayBitmap.Width < this.Width) && (pDisplayBitmap.Height < this.Height))
                    {
                        tZoom = 1;
                    }
                    else
                    {
                        decimal propW = (decimal)this.Width / (decimal)pDisplayBitmap.Width;
                        decimal propH = (decimal)this.Height / (decimal)pDisplayBitmap.Height;
                        tZoom = Math.Min(propW, propH);
                    }
                }

                int newWidth = (int)((decimal)pDisplayBitmap.Width * tZoom);
                int newHeight = (int)((decimal)pDisplayBitmap.Height * tZoom);

                Point pos = new Point((this.Width / 2 - (newWidth / 2)) + pPan.X, (this.Height / 2 - (newHeight / 2)) + pPan.Y);

                g.DrawImage(pDisplayBitmap, pos.X, pos.Y, newWidth, newHeight);
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
            //base.OnPaintBackground(pevent);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Render(e.Graphics);
        }

        private bool pMouseDown = false;
        private Point pLastPoint;
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if ((e.Button == MouseButtons.Left) && (pZoom != 0))
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
                pPan.X -= dx;
                pPan.Y -= dy;
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

        private Timer pTimer = null;
        private float pMessageAlpha;
        private string pMessageText;
        private bool pMessageFadingIn = false;
        private int pTimerInterval = 0;

        public void ShowMessage(string msg, int timeoutMS)
        {
            pMessageText = msg;
            pMessageAlpha = 0;
            pMessageFadingIn = true;

            pTimer = new Timer();
            pTimer.Tick += PTimer_Tick;
            pTimerInterval = timeoutMS;
            pTimer.Interval = 50;
            pTimer.Start();
        }

        private void PTimer_Tick(object sender, EventArgs e)
        {

            if (pMessageFadingIn)
            {
                pMessageAlpha += 0.2f;

                if (pMessageAlpha >= 1)
                {
                    pMessageAlpha = 1;
                    pTimer.Stop();
                    pTimer.Interval = pTimerInterval;
                    pMessageFadingIn = false;
                    pTimer.Start();
                }
            }
            else
            {
                if (pTimer.Interval != 50)
                {
                    pTimer.Stop();
                    pTimer.Interval = 50;
                    pTimer.Start();
                }

                pMessageAlpha -= 0.2f;

                if (pMessageAlpha <= 0)
                {
                    pMessageText = "";
                    pTimer.Stop();
                }
            }
            Invalidate();
        }

    }
}
