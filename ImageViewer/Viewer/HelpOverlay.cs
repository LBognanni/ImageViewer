using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;

namespace ImageViewer;

/// <summary>
/// The keyboard cheat sheet. It stays up until dismissed, unlike a <see cref="MessageOverlay"/> message.
/// </summary>
public sealed class HelpOverlay : Control
{
    private const double FontSize = 14;
    private const double LineHeight = 21;
    private const double SectionGap = 14;
    private const double KeyGap = 16;
    private const double ColumnGap = 40;
    private const double Padding = 24;
    private const double EdgeGap = 16;

    private static readonly IBrush Background = new SolidColorBrush(Colors.Black, 220.0 / 255);
    private static readonly IBrush Primary = Brushes.White;
    private static readonly IBrush Secondary = new SolidColorBrush(Color.FromRgb(0xB8, 0xB8, 0xB8));
    private static readonly Typeface Bold = new(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);

    private List<Layout>? _layouts;

    public HelpOverlay()
    {
        IsHitTestVisible = false;
        Opacity = 0;
        Transitions = new Transitions
        {
            new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(120) }
        };
    }

    public HelpSheet? Sheet { get; private set; }
    public bool IsOpen { get; private set; }

    public void Show(HelpSheet sheet)
    {
        Sheet = sheet;
        _layouts = null;
        IsOpen = true;
        Opacity = 1;
        InvalidateVisual();
    }

    public void Hide()
    {
        // The sheet stays, to be drawn while it fades
        IsOpen = false;
        Opacity = 0;
    }

    public override void Render(DrawingContext context)
    {
        if (Sheet == null)
            return;

        _layouts ??= new List<Layout>
        {
            Arrange(Sheet, SplitInTwo(Sheet.Sections)),
            Arrange(Sheet, new[] { Sheet.Sections })
        };

        var layout = _layouts.MaxBy(l => FitScale(l.Size, Bounds.Size))!;
        var scale = FitScale(layout.Size, Bounds.Size);
        var left = (Bounds.Width - layout.Size.Width * scale) / 2;
        var top = (Bounds.Height - layout.Size.Height * scale) / 2;

        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(left, top)))
        {
            context.DrawRectangle(Background, null, new RoundedRect(new Rect(layout.Size), 8));
            foreach (var (text, position) in layout.Pieces)
            {
                context.DrawText(text, position);
            }
        }
    }

    /// <summary>
    /// How far to shrink the panel to fit. Ties go to the first layout, the wider one.
    /// </summary>
    internal static double FitScale(Size content, Size available) => Math.Clamp(Math.Min(
        (available.Width - EdgeGap * 2) / content.Width,
        (available.Height - EdgeGap * 2) / content.Height), 0.1, 1);

    /// <summary>
    /// Split the sections, in order, where the two columns come out closest in height
    /// </summary>
    internal static IReadOnlyList<IReadOnlyList<HelpSection>> SplitInTwo(IReadOnlyList<HelpSection> sections)
    {
        var split = Enumerable.Range(0, sections.Count + 1)
            .MinBy(i => Math.Max(TotalHeight(sections.Take(i)), TotalHeight(sections.Skip(i))));
        return new[] { sections.Take(split).ToList(), sections.Skip(split).ToList() }.Where(c => c.Count > 0).ToList();
    }

    private static double TotalHeight(IEnumerable<HelpSection> sections) =>
        sections.Sum(s => (s.Rows.Count + 1) * LineHeight + SectionGap);

    private static Layout Arrange(HelpSheet sheet, IReadOnlyList<IReadOnlyList<HelpSection>> columns)
    {
        var pieces = new List<(FormattedText, Point)>();
        var left = Padding;
        var bottom = 0.0;

        foreach (var column in columns)
        {
            var rows = column.SelectMany(s => s.Rows).ToList();
            var keyWidth = rows.Max(r => Format(r.Keys, Primary, Bold).WidthIncludingTrailingWhitespace);
            var width = column.Select(s => Format(s.Title, Primary, Bold).WidthIncludingTrailingWhitespace)
                .Concat(rows.Select(r => keyWidth + KeyGap + Format(r.Description, Secondary).WidthIncludingTrailingWhitespace))
                .Max();

            var top = Padding;
            foreach (var section in column)
            {
                pieces.Add((Format(section.Title, Primary, Bold), new Point(left, top)));
                top += LineHeight;
                foreach (var row in section.Rows)
                {
                    pieces.Add((Format(row.Keys, Primary, Bold), new Point(left, top)));
                    pieces.Add((Format(row.Description, Secondary), new Point(left + keyWidth + KeyGap, top)));
                    top += LineHeight;
                }
                top += SectionGap;
            }

            bottom = Math.Max(bottom, top);
            left += width + ColumnGap;
        }

        var hint = Format(sheet.CloseHint, Secondary);
        var panelWidth = Math.Max(left - ColumnGap + Padding, hint.Width + Padding * 2);
        pieces.Add((hint, new Point((panelWidth - hint.Width) / 2, bottom)));
        return new Layout(pieces, new Size(panelWidth, bottom + LineHeight + Padding));
    }

    private static FormattedText Format(string text, IBrush brush, Typeface? typeface = null) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface ?? Typeface.Default, FontSize, brush);

    private sealed record Layout(List<(FormattedText Text, Point Position)> Pieces, Size Size);
}
