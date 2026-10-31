using System.Linq;
using Avalonia;
using NUnit.Framework;

namespace ImageViewer.Tests;

[TestFixture]
public class HelpOverlayTests
{
    private static HelpSection Section(int rows) =>
        new(rows.ToString(), Enumerable.Repeat(new HelpRow("A", "a"), rows).ToList());

    [Test]
    public void SplitsSectionsInOrderWhereTheColumnsBalance()
    {
        var columns = HelpOverlay.SplitInTwo(new[] { 2, 2, 4, 3, 6 }.Select(Section).ToList());
        Assert.That(columns[0].Select(s => s.Title), Is.EqualTo(new[] { "2", "2", "4" }));
        Assert.That(columns[1].Select(s => s.Title), Is.EqualTo(new[] { "3", "6" }));
    }

    [Test]
    public void ASingleSectionMakesOneColumn()
    {
        Assert.That(HelpOverlay.SplitInTwo(new[] { Section(5) }).Count, Is.EqualTo(1));
    }

    [Test]
    public void APanelThatFitsKeepsItsSize()
    {
        Assert.That(HelpOverlay.FitScale(new Size(100, 100), new Size(1000, 1000)), Is.EqualTo(1));
    }

    [TestCase(100, 200, 1000, 150)]
    [TestCase(500, 100, 300, 1000)]
    public void APanelThatDoesNotFitShrinksIntoTheSpace(int width, int height, int availableWidth, int availableHeight)
    {
        var scale = HelpOverlay.FitScale(new Size(width, height), new Size(availableWidth, availableHeight));
        Assert.That(width * scale, Is.LessThanOrEqualTo(availableWidth));
        Assert.That(height * scale, Is.LessThanOrEqualTo(availableHeight));
    }
}
