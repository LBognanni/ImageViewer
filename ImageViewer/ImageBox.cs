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

        /// <summary>
        /// Bitmap to display on screen.
        /// Makes sure to dispose the old bitmap when assigning a new one
        /// </summary>
        protected Bitmap DisplayBitmap
        {
            get
            {
                return pDisplayBitmap;
            }
            set
            {
                // Dispose the old bitmap
                if (pDisplayBitmap != null)
                {
                    pDisplayBitmap.Dispose();
                }
                pDisplayBitmap = value;

                // Save display bitmap width and height
                if (value == null)
                {
                    pDisplayBitmapWidth = 0;
                    pDisplayBitmapHeight = 0;
                }
                else
                {
                    pDisplayBitmapWidth = value.Width;
                    pDisplayBitmapHeight = value.Height;
                }
            }
        }

        private int pDisplayBitmapWidth = 0;
        private int pDisplayBitmapHeight = 0;
        private Point pPan = new Point(0, 0);
        Brush pBackgroundBrush = Brushes.Black;

        public int MaximumCachedFiles { get; set; }

        public ImageBox()
        {
            MaximumCachedFiles = 5;
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
                DisplayBitmap = img.ToBitmap();
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
            lock(pSyncTk)
            {
                if (!_cache.Any(c => c.Key == fileName))
                {
                    _cache.Enqueue(new KeyValuePair<string, FreeImageBitmap>(fileName, bmp));

                    if (_cache.Count > MaximumCachedFiles)
                    {
                        var first = _cache.Dequeue();
                        first.Value.Dispose();
                    }
                }
            };
        }

        private FreeImageBitmap findInCache(string fileName)
        {
            var el = _cache.FirstOrDefault(e => e.Key == fileName);

            // Return null if not found
            if ((object)el == null)
                return null;

            // return element
            return el.Value;
        }

        private bool isCached(string fileName)
        {
            return _cache.Any(el => el.Key == fileName);
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
                flags = FREE_IMAGE_LOAD_FLAGS.JPEG_ACCURATE | FREE_IMAGE_LOAD_FLAGS.JPEG_EXIFROTATE;
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


        private BackgroundWorker pBwLoadImg = null;

        private FreeImageBitmap pLoadImageInternal(string fileName)
        {
            lock (pSyncTk)
            {
                FreeImageBitmap fBmp = loadBitmap(fileName);

                // Dispose previous image

                if ((pImage != null) && (!Object.ReferenceEquals(pImage, fBmp)))
                {
                    if (!_cache.Any(c => Object.ReferenceEquals(c.Value, pImage)))
                    {
                        pImage.Dispose();
                    }
                }

                return fBmp;
            }
        }

        private object pSyncTk = new object();

        private void setDisplayImage(FreeImageBitmap fBmp)
        {
            lock (pSyncTk)
            {
                pImage = fBmp;
                if ((pImage != null) && (!pImage.IsDisposed))
                {
                    // Choose the background color as an average of a few points of the picture
                    var colors = new[]
                    {
                        pImage.GetPixel(0,0),
                        pImage.GetPixel(0,pImage.Height-1),
                        pImage.GetPixel(pImage.Width-1,0),
                        pImage.GetPixel(pImage.Width-1,pImage.Height-1),
                        pImage.GetPixel(pImage.Width/2,0),
                        pImage.GetPixel(0,pImage.Height/2),
                        pImage.GetPixel(pImage.Width/2,pImage.Height-1),
                        pImage.GetPixel(pImage.Width-1,pImage.Height/2),
                    };

                    pBackgroundBrush = new SolidBrush(Color.FromArgb(
                        (int)colors.Average(c => c.R),
                        (int)colors.Average(c => c.G),
                        (int)colors.Average(c => c.B)
                        ));

                    // Render to bitmap
                    DisplayBitmap = pImage.ToBitmap();
                }
                // Render the new image
                Render(true);
            }
        }

        /// <summary>
        /// Load and display an image
        /// </summary>
        /// <param name="fileName"></param>
        public void LoadImage(string fileName)
        {
            if (fileName == null)
                return;

            // Stop any async image loading
            if (pBwLoadImg != null)
            {
                if (pBwLoadImg.IsBusy)
                {
                    pBwLoadImg.CancelAsync();
                    pImage = null;
                    pBwLoadImg = null;
                }
            }

            if (!isCached(fileName))
            {
                // Can we use a thumbnail and load asynchronously?
                // using the Windows API code pack
                // PM> Install -Package WindowsAPICodePack-Shell
                using (Microsoft.WindowsAPICodePack.Shell.ShellFile file = Microsoft.WindowsAPICodePack.Shell.ShellFile.FromFilePath(fileName))
                {
                    if (file.Thumbnail.ExtraLargeBitmap != null)
                    {
                        uint
                            w = file.Properties.System?.Image?.HorizontalSize?.Value ?? 0,
                            h = file.Properties.System?.Image?.VerticalSize?.Value ?? 0;

                        // Display the thumbnail
                        DisplayBitmap = file.Thumbnail.ExtraLargeBitmap;
                        if (w != 0 && h != 0)
                        {
                            pDisplayBitmapWidth = (int)w;
                            pDisplayBitmapHeight = (int)h;
                        }
                        Render(true);

                        // Load the image asynchronously
                        pBwLoadImg = new BackgroundWorker
                        {
                            WorkerSupportsCancellation = true
                        };
                        pBwLoadImg.DoWork += (sender, args) => {
                            args.Result = pLoadImageInternal(fileName);
                        };
                        pBwLoadImg.RunWorkerCompleted += (sender, args) =>
                        {
                            if ((!args.Cancelled) && (args.Result != null))
                            {
                                setDisplayImage(args.Result as FreeImageBitmap);
                            }
                        };
                        pBwLoadImg.RunWorkerAsync();
                        return;
                    }
                }
            }

            // Load the image synchronously (from cache or if a suitable thumbnail was not found)
            setDisplayImage(pLoadImageInternal(fileName));

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

            if (DisplayBitmap != null)
            {
                if (tZoom == 0)
                {
                    // "Best fit"
                    if ((pDisplayBitmapWidth < this.Width) && (pDisplayBitmapHeight < this.Height))
                    {
                        tZoom = 1;
                    }
                    else
                    {
                        decimal propW = (decimal)this.Width / (decimal)pDisplayBitmapWidth;
                        decimal propH = (decimal)this.Height / (decimal)pDisplayBitmapHeight;
                        tZoom = Math.Min(propW, propH);
                    }
                }

                int newWidth = (int)((decimal)pDisplayBitmapWidth * tZoom);
                int newHeight = (int)((decimal)pDisplayBitmapHeight * tZoom);

                Point pos = new Point((this.Width / 2 - (newWidth / 2)) + pPan.X, (this.Height / 2 - (newHeight / 2)) + pPan.Y);

                g.DrawImage(DisplayBitmap, pos.X, pos.Y, newWidth, newHeight);
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


        // -------------- Text message UI ----------------

        private Timer pMessageTimer = null;
        private float pMessageAlpha;
        private string pMessageText;
        private bool pMessageFadingIn = false;
        private int pTimerInterval = 0;
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

    }
}
