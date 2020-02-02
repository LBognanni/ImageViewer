using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ImageViewer.Rendering
{
    public class MessageRenderer
    {

        private Timer _timer = null;
        private float _alpha;
        private string _text;
        private bool _fadingIn = false;
        private int _interval = 0;

        private const int MESSAGE_FADE_INTERVAL = 20;
        private const float MESSAGE_FADE_AMOUNT = 0.1f;

        private Control _controlToInvalidate;

        public MessageRenderer(Control controlToInvalidate)
        {
            _controlToInvalidate = controlToInvalidate;
        }

        public bool HasMessage
        {
            get
            {
                return !string.IsNullOrEmpty(_text);
            }
        }

        public void HideMessage()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _fadingIn = false;
                _timer.Interval = MESSAGE_FADE_INTERVAL;
                _timer.Start();
            }
        }

        public void ShowMessage(string msg, int timeoutMS)
        {
            _text = msg;
            _alpha = 0;
            _fadingIn = true;

            _timer = new Timer();
            _timer.Tick += pMessageTimer_Tick;
            _interval = timeoutMS;
            _timer.Interval = MESSAGE_FADE_INTERVAL;
            _timer.Start();
        }

        private void pMessageTimer_Tick(object sender, EventArgs e)
        {

            if (_fadingIn)
            {
                _alpha += MESSAGE_FADE_AMOUNT;

                if (_alpha >= 1)
                {
                    _alpha = 1;
                    _timer.Stop();
                    _timer.Interval = _interval;
                    _fadingIn = false;
                    _timer.Start();
                }
            }
            else
            {
                if (_timer.Interval != MESSAGE_FADE_INTERVAL)
                {
                    _timer.Stop();
                    _timer.Interval = MESSAGE_FADE_INTERVAL;
                    _timer.Start();
                }

                _alpha -= MESSAGE_FADE_AMOUNT;

                if (_alpha <= 0)
                {
                    _text = "";
                    _timer.Stop();
                }
            }
            _controlToInvalidate.Invalidate();
        }

        public void Render(Graphics g)
        {
            if (!HasMessage)
                return;

            if (!string.IsNullOrEmpty(_text))
            {
                var strSize = g.MeasureString(_text, _controlToInvalidate.Font);
                var textCenter = new RectangleF(
                        (_controlToInvalidate.Width / 2) - (strSize.Width / 2) - 20,
                        (_controlToInvalidate.Height - strSize.Height - 60),
                        strSize.Width + 40,
                        strSize.Height + 40
                    );

                using (SolidBrush black = new SolidBrush(Color.FromArgb((int)(_alpha * 200.0f), 0, 0, 0)))
                {
                    g.FillRectangle(black, textCenter);
                }

                using (SolidBrush white = new SolidBrush(Color.FromArgb((int)(_alpha * 255.0f), 255, 255, 255)))
                {
                    g.DrawString(_text, _controlToInvalidate.Font, white, textCenter.Left + 20, textCenter.Top + 20);
                }
            }

        }


    }
}
