using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
        CollectionAssert.AreEqual(new[] { new HelpRow(expected, "one") }, Rows((keys, "One")));
    }

    [Test]
    public void KeysThatReadTheSameAreListedOnce()
    {
        var rows = Rows(("Add", "One"), ("OemPlus", "One"), ("Shift|OemPlus", "One"));
        CollectionAssert.AreEqual(new[] { new HelpRow("+", "one") }, rows);
    }

    [Test]
    public void RelatedActionsShareALine()
    {
        CollectionAssert.AreEqual(new[] { new HelpRow("← →", "pair") }, Rows(("Left", "Back"), ("Right", "Forward")));
    }

    [Test]
    public void RelatedActionsSplitWhenTheKeysWouldNotLineUp()
    {
        var rows = Rows(("Left", "Back"), ("P", "Back"), ("Right", "Forward"));
        CollectionAssert.AreEqual(new[] { new HelpRow("← / P", "back"), new HelpRow("→", "forward") }, rows);
    }

    [Test]
    public void ARunOfDigitsSharesALine()
    {
        var rows = Rows(("D2", "Level1"), ("D3", "Level2"), ("D4", "Level3"));
        CollectionAssert.AreEqual(new[] { new HelpRow("2–4", "levels") }, rows);
    }

    [Test]
    public void KeysThatAreNotARunSplit()
    {
        ClassicAssert.AreEqual(3, Rows(("D2", "Level1"), ("D3", "Level2"), ("X", "Level3")).Count());
    }

    [Test]
    public void ListsCustomCommandsByProgramName()
    {
        var rows = Rows(("G", new KeyBinding { Command = "\"/usr/bin/gimp\" {0}" }));
        CollectionAssert.AreEqual(new[] { new HelpRow("G", "gimp") }, rows);
    }
}
