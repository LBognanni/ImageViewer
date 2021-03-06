﻿using ImageViewer.Rendering;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ImageViewer
{
    public class ImageBox : Control, IReceiveImage, IRenderControl
    {
        public Point Pan { get; set; } = new Point(0, 0);
        Brush _backgroundBrush = Brushes.Black;
        
        private TwoStepImageCache _cache;
        private MessageRenderer _messageRenderer;
        private ImageRenderer _imageRenderer;
        private ImageMeta _image;

        public ImageBox()
        {
            _cache = new TwoStepImageCache(new ImageLoader(), new QuickImageLoader(), this, 5);
            _messageRenderer = new MessageRenderer(this);
            _imageRenderer = new ImageRenderer(this);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        public void ShowMessage(string s, int timeoutMS) => _messageRenderer.ShowMessage(s, timeoutMS);
        public bool HasMessage => _messageRenderer.HasMessage;
        public void HideMessage() => _messageRenderer.HideMessage();

        private decimal _zoom = 0;
        public decimal Zoom
        {
            get => _zoom;
            set
            {
                _zoom = value;
                Invalidate();
            }
        }

        private int _rotation = 0;
        public int Rotation
        {
            get => _rotation;
            internal set
            {
                _rotation = value;
                while (_rotation >= 360)
                {
                    _rotation -= 360;
                }
                while(_rotation<0)
                {
                    _rotation += 360;
                }
                Invalidate();
            }
        }


        public void ResetTransform()
        {
            Pan = new Point(0, 0);
        }


        public void LoadImage(string fileName)
        {
            if (fileName == null)
                return;

            SetImage(_cache.GetOrLoadImage(fileName));
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
                _imageRenderer.Render(g, _image);
            }
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

        private bool _mouseDown = false;
        private Point _lastPoint;
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if ((e.Button == MouseButtons.Left) && (_zoom != 0))
            {
                _mouseDown = true;
                _lastPoint = e.Location;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if ((e.Button == MouseButtons.Left) && (_mouseDown))
            {
                var dx = _lastPoint.X - e.Location.X;
                var dy = _lastPoint.Y - e.Location.Y;
                Pan = new Point(Pan.X - dx, Pan.Y - dy);
                _lastPoint = e.Location;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if(e.Button== MouseButtons.Left)
            {
                _mouseDown = false;
            }
            base.OnMouseUp(e);
        }

        public void ReceiveImage(ImageMeta img)
        {
            _imageRenderer.InvalidateCache();
            SetImage(img);
        }

        private void SetImage(ImageMeta img)
        {
            _image = img;
            _backgroundBrush.Dispose();
            _backgroundBrush = new SolidBrush(img.AverageColor);
            _zoom = 0;
            Pan = new Point(0, 0);
            _rotation = 0;
            Invalidate();
        }

        public void CacheImage(string fileName)
        {
            //Task.Run(() => _cache.LoadImageAsync(fileName));
        }
    }
}
