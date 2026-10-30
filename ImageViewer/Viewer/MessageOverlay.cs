using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace ImageViewer;

/// <summary>
/// Fading on-screen messages. This is its own control so that a fading message doesn't redraw the image.
/// </summary>
public sealed class MessageOverlay : Control
{
    private static readonly TimeSpan FadeInterval = TimeSpan.FromMilliseconds(20);
    private const double FadeAmount = 0.1;
    private const double FontSize = 14;

    private readonly DispatcherTimer _timer = new();
    private double _alpha;
    private bool _fadingIn;
    private TimeSpan _timeout;

    public MessageOverlay()
    {
        IsHitTestVisible = false;
        _timer.Tick += OnTick;
    }

    public string? Text { get; private set; }
    public bool HasMessage => !string.IsNullOrEmpty(Text);

    public void HideMessage()
    {
        _fadingIn = false;
        _timer.Interval = FadeInterval;
        _timer.Start();
    }

    public void ShowMessage(string message, int timeoutMs)
    {
        Text = message;
        _alpha = 0;
        _fadingIn = true;
        _timeout = TimeSpan.FromMilliseconds(timeoutMs);
        _timer.Interval = FadeInterval;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_fadingIn)
        {
            _alpha += FadeAmount;
            if (_alpha >= 1)
            {
                _alpha = 1;
                _fadingIn = false;
                _timer.Interval = _timeout;
            }
        }
        else
        {
            _timer.Interval = FadeInterval;
            _alpha -= FadeAmount;
            if (_alpha <= 0)
            {
                Text = "";
                _timer.Stop();
            }
        }
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (!HasMessage)
            return;

        var white = new SolidColorBrush(Colors.White, _alpha);
        var pieces = PositionPieces(Text!, white, out var size);
        var left = (Bounds.Width - size.Width) / 2;
        var top = Bounds.Height - size.Height - 20;

        var black = new SolidColorBrush(Colors.Black, _alpha * 200 / 255);
        context.FillRectangle(black, new Rect(left - 20, top - 20, size.Width + 40, size.Height + 40));

        foreach (var (text, position) in pieces)
        {
            context.DrawText(text, new Point(left + position.X, top + position.Y));
        }
    }

    /// <summary>
    /// Lay out a message. Tabs separate columns, which line up across lines.
    /// </summary>
    private static List<(FormattedText Text, Point Position)> PositionPieces(string message, IBrush brush, out Size size)
    {
        var lines = message
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('\t', StringSplitOptions.RemoveEmptyEntries).Select(s => Format(s, brush)).ToList())
            .ToList();

        var columnCount = lines.Max(l => l.Count);
        var columnWidths = Enumerable.Range(0, columnCount)
            .Select(i => lines.Max(l => i < l.Count ? l[i].WidthIncludingTrailingWhitespace : 0))
            .ToList();
        const double columnGap = 12;

        var pieces = new List<(FormattedText, Point)>();
        var top = 0.0;
        foreach (var line in lines)
        {
            var left = 0.0;
            for (var i = 0; i < line.Count; i++)
            {
                pieces.Add((line[i], new Point(left, top)));
                left += columnWidths[i] + columnGap;
            }
            top += line.Max(p => p.Height) * (lines.Count > 1 ? 1.2 : 1);
        }

        size = new Size(columnWidths.Sum() + columnGap * (columnCount - 1), top);
        return pieces;
    }

    private static FormattedText Format(string text, IBrush brush) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, FontSize, brush);
}
