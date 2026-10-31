using System.Linq;
using Avalonia;
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
        CollectionAssert.AreEqual(new[] { "2", "2", "4" }, columns[0].Select(s => s.Title));
        CollectionAssert.AreEqual(new[] { "3", "6" }, columns[1].Select(s => s.Title));
    }

    [Test]
    public void ASingleSectionMakesOneColumn()
    {
        ClassicAssert.AreEqual(1, HelpOverlay.SplitInTwo(new[] { Section(5) }).Count);
    }

    [Test]
    public void APanelThatFitsKeepsItsSize()
    {
        ClassicAssert.AreEqual(1, HelpOverlay.FitScale(new Size(100, 100), new Size(1000, 1000)));
    }

    [TestCase(100, 200, 1000, 150)]
    [TestCase(500, 100, 300, 1000)]
    public void APanelThatDoesNotFitShrinksIntoTheSpace(int width, int height, int availableWidth, int availableHeight)
    {
        var scale = HelpOverlay.FitScale(new Size(width, height), new Size(availableWidth, availableHeight));
        ClassicAssert.LessOrEqual(width * scale, availableWidth);
        ClassicAssert.LessOrEqual(height * scale, availableHeight);
    }
}
