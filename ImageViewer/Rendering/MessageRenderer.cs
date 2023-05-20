using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ImageViewer.Rendering
{
    public class MessageRenderer
    {

        private Timer? _timer;
        private float _alpha;
        private string? _text;
        private bool _fadingIn;
        private int _interval;

        private const int MESSAGE_FADE_INTERVAL = 20;
        private const float MESSAGE_FADE_AMOUNT = 0.1f;

        private readonly Control _controlToInvalidate;

        public MessageRenderer(Control controlToInvalidate)
        {
            _controlToInvalidate = controlToInvalidate ?? throw new ArgumentNullException(nameof(controlToInvalidate));
        }

        public bool HasMessage => !string.IsNullOrEmpty(_text);

        public void HideMessage()
        {
            if (_timer == null)
            {
                return;
            }

            _timer.Stop();
            _fadingIn = false;
            _timer.Interval = MESSAGE_FADE_INTERVAL;
            _timer.Start();
        }

        public void ShowMessage(string msg, int timeoutMS)
        {
            _text = msg;
            _alpha = 0;
            _fadingIn = true;

            _timer = new Timer();
            _timer.Tick += MessageTimer_Tick;
            _interval = timeoutMS;
            _timer.Interval = MESSAGE_FADE_INTERVAL;
            _timer.Start();
        }

        private void MessageTimer_Tick(object sender, EventArgs e)
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
            {
                return;
            }

            if (string.IsNullOrEmpty(_text))
            {
                return;
            }

            if (_controlToInvalidate == null)
            {
                return;
            }

            var (strings, allTextRect) = PositionStringPieces(g);
            var offset = new PointF(_controlToInvalidate.Width / 2.0f - allTextRect.Width / 2.0f,
                _controlToInvalidate.Height  - allTextRect.Height  - 20.0f);


            using (var black = new SolidBrush(Color.FromArgb((int)(_alpha * 200.0f), 0, 0, 0)))
            {
                g.FillRectangle(black, allTextRect.X + offset.X - 20, allTextRect.Y + offset.Y - 20, allTextRect.Width + 40, allTextRect.Height + 40);
            }

            using (var white = new SolidBrush(Color.FromArgb((int)(_alpha * 255.0f), 255, 255, 255)))
            {
                foreach (var piece in strings)
                {
                    g.DrawString(piece.S, _controlToInvalidate.Font, white, piece.Position.X + offset.X, piece.Position.Y + offset.Y);
                }
            }

        }

        private (List<PositionedStringPiece>, RectangleF) PositionStringPieces(Graphics g)
        {
            var strings = new List<PositionedStringPiece>();
            if (_text.Contains("\t"))
            {
                var lines = _text
                    .Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => GetRect(s, g).ToList()).ToList();
                var columns = lines.Max(l => l.Count);
                var columnSizes = GetColumnSizes(lines, columns);

                var textTop = 0.0f;
                strings.AddRange(lines.SelectMany(
                    (line, iLine) =>
                    {
                        var lineLeft = 0.0f;
                        var pieces = line.Select((col, iCol) =>
                        {
                            var piece = new PositionedStringPiece(col, new PointF(lineLeft, textTop));
                            lineLeft += columnSizes[iCol];
                            return piece;
                        });
                        textTop += line.Max(x => x.Size.Height) * 1.2f;
                        return pieces;
                    }));

                float minX = float.MaxValue, minY = float.MaxValue, maxW = float.MinValue, maxH = float.MinValue;
                foreach (var piece in strings)
                {
                    if (piece.Position.X < minX)
                        minX = piece.Position.X;
                    if (piece.Position.Y < minY)
                        minY = piece.Position.Y;
                    var w = piece.Position.X + piece.Size.Width;
                    if (maxW < w)
                        maxW = w;
                    var h = piece.Position.Y + piece.Size.Height;
                    if (maxH < h)
                        maxH = h;
                }

                return (strings, new RectangleF(minX, minY, maxW, maxH));
            }

            var strSize = g.MeasureString(_text, _controlToInvalidate.Font);
            var strPos = new PointF(0,0);
            strings.Add(new PositionedStringPiece(new StringPiece(_text, 0, strSize), strPos));
            return (strings, new RectangleF(strPos, strSize));
        }

        private List<float> GetColumnSizes(List<List<StringPiece>> lines, int columns)
        {
            var columnSizes = new List<float>();

            for (var i = 0; i < columns; ++i)
            {
                columnSizes.Add(lines.Max(l => l.FirstOrDefault(x => x.Col == i)?.Size.Width ?? 0.0f));
            }

            return columnSizes;
        }

        private IEnumerable<StringPiece> GetRect(string str, Graphics g) =>
            str
                .Split("\t".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
                .Select((s, i) => new StringPiece(s, i, g.MeasureString(s, _controlToInvalidate.Font)));

        class StringPiece
        {
            public string S { get; }
            public int Col { get; }
            public SizeF Size { get; }

            public StringPiece(string s, int col, SizeF size)
            {
                S = s;
                Col = col;
                Size = size;
            }
        }

        class PositionedStringPiece : StringPiece
        {
            public PointF Position { get; }

            public PositionedStringPiece(StringPiece piece, PointF position) : base(piece.S, piece.Col, piece.Size)
            {
                Position = position;
            }
        }
    }
}
