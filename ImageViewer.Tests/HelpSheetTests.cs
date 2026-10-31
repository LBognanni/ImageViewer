using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace ImageViewer.Tests;

[TestFixture]
public class HelpSheetTests
{
    private static readonly HelpSectionTemplate[] Templates =
    {
        new("Single", new HelpItem("One", "one")),
        new("Pair", new HelpItem("pair", new HelpAction("Back", "back"), new HelpAction("Forward", "forward"))),
        new("Levels", new HelpItem("levels", new HelpAction("Level1", "level 1"), new HelpAction("Level2", "level 2"),
            new HelpAction("Level3", "level 3")))
    };

    private static IEnumerable<HelpRow> Rows(params (string Keys, KeyBinding Binding)[] bindings)
    {
        var settings = new Settings();
        settings.KeyBindings = bindings.ToDictionary(b => b.Keys, b => b.Binding);
        var actions = Templates.SelectMany(t => t.Items).SelectMany(i => i.Actions).Select(a => a.Action).ToList();
        return HelpSheet.Build(Templates, settings, actions, new string[0]).Sections.SelectMany(s => s.Rows);
    }

    private static IEnumerable<HelpRow> Rows(params (string Keys, string Action)[] bindings) =>
        Rows(bindings.Select(b => (b.Keys, new KeyBinding(b.Action))).ToArray());

    [TestCase("Control|Shift|R", "Ctrl+⇧R")]
    [TestCase("Escape", "Esc")]
    [TestCase("D7", "7")]
    public void FormatsKeys(string keys, string expected)
    {
        Assert.That(Rows((keys, "One")), Is.EqualTo(new[] { new HelpRow(expected, "one") }));
    }

    [Test]
    public void KeysThatReadTheSameAreListedOnce()
    {
        var rows = Rows(("Add", "One"), ("OemPlus", "One"), ("Shift|OemPlus", "One"));
        Assert.That(rows, Is.EqualTo(new[] { new HelpRow("+", "one") }));
    }

    [Test]
    public void RelatedActionsShareALine()
    {
        Assert.That(Rows(("Left", "Back"), ("Right", "Forward")), Is.EqualTo(new[] { new HelpRow("← →", "pair") }));
    }

    [Test]
    public void RelatedActionsSplitWhenTheKeysWouldNotLineUp()
    {
        var rows = Rows(("Left", "Back"), ("P", "Back"), ("Right", "Forward"));
        Assert.That(rows, Is.EqualTo(new[] { new HelpRow("← / P", "back"), new HelpRow("→", "forward") }));
    }

    [Test]
    public void ARunOfDigitsSharesALine()
    {
        var rows = Rows(("D2", "Level1"), ("D3", "Level2"), ("D4", "Level3"));
        Assert.That(rows, Is.EqualTo(new[] { new HelpRow("2–4", "levels") }));
    }

    [Test]
    public void KeysThatAreNotARunSplit()
    {
        Assert.That(Rows(("D2", "Level1"), ("D3", "Level2"), ("X", "Level3")).Count(), Is.EqualTo(3));
    }

    [Test]
    public void ListsCustomCommandsByProgramName()
    {
        var rows = Rows(("G", new KeyBinding { Command = "\"/usr/bin/gimp\" {0}" }));
        Assert.That(rows, Is.EqualTo(new[] { new HelpRow("G", "gimp") }));
    }
}
