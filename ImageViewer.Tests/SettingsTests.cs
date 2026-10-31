using System.IO;
using System.Threading.Tasks;
using Avalonia.Input;
using NUnit.Framework;

namespace ImageViewer.Tests;

[TestFixture]
public class SettingsTests
{
    [TestCase("R", Key.R, KeyModifiers.None)]
    [TestCase("Shift|R", Key.R, KeyModifiers.Shift)]
    [TestCase("Control|Shift|C", Key.C, KeyModifiers.Control | KeyModifiers.Shift)]
    [TestCase("ControlKey|C", Key.C, KeyModifiers.Control)]
    [TestCase("Menu|F4", Key.F4, KeyModifiers.Alt)]
    [TestCase("Add", Key.Add, KeyModifiers.None)]
    [TestCase("D1", Key.D1, KeyModifiers.None)]
    [TestCase("Oemplus", Key.OemPlus, KeyModifiers.None)]
    [TestCase("Oemcomma", Key.OemComma, KeyModifiers.None)]
    [TestCase("Next", Key.PageDown, KeyModifiers.None)]
    public void ParsesKeysWrittenForWinForms(string text, Key key, KeyModifiers modifiers)
    {
        Assert.That(KeyParser.Parse(text), Is.EqualTo(new KeyCombination(key, modifiers)));
    }

    [TestCase("")]
    [TestCase("Shift")]
    [TestCase("NotAKey")]
    [TestCase("A|B")]
    public void InvalidKeysParseToNull(string text)
    {
        Assert.That(KeyParser.Parse(text), Is.Null);
    }

    [Test]
    public void AllDefaultKeysParse()
    {
        var settings = new Settings();
        foreach (var key in settings.KeyBindings.Keys)
        {
            Assert.That(KeyParser.Parse(key), Is.Not.Null, key);
        }
        Assert.That(settings.TryGetKeyBindingFor(new KeyCombination(Key.R, KeyModifiers.Shift), out var binding), Is.True);
        Assert.That(binding!.Action, Is.EqualTo("RotateImageInv"));
    }

    [Test]
    public async Task LoadsAFileSavedByTheWinFormsVersion()
    {
        var path = Path.GetTempFileName();
        // Newtonsoft's output, with the byte order mark that the old StreamWriter added
        await File.WriteAllTextAsync(path, """
            {
              "StartInFullScreen": true,
              "DefaultZoom": 1.0,
              "TransparentWhenOutOfFocus": false,
              "KeyBindings": {
                "X": {
                  "Action": null,
                  "IsDeleteAction": true,
                  "Command": "\"c:\\tools\\my tool.exe\" --file {0}"
                },
                "Oemplus": {
                  "Action": "NextImage",
                  "IsDeleteAction": false,
                  "Command": null
                }
              }
            }
            """, new System.Text.UTF8Encoding(true));

        var settings = await new SettingsJsonStorage(path).LoadSettings();

        Assert.That(settings.StartInFullScreen, Is.True);
        Assert.That(settings.DefaultZoom, Is.EqualTo(1m));
        Assert.That(settings.KeyBindings.Count, Is.EqualTo(2), "saved bindings replace the defaults");
        Assert.That(settings.TryGetKeyBindingFor(new KeyCombination(Key.OemPlus, KeyModifiers.None), out var binding), Is.True);
        Assert.That(binding!.Action, Is.EqualTo("NextImage"));
        Assert.That(settings.KeyBindings["X"].IsDeleteAction, Is.True);
    }

    [Test]
    public async Task SavedSettingsCanBeReadBack()
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory().FullName, "nested", "settings.json");
        var sut = new SettingsJsonStorage(path);
        var settings = new Settings { DefaultZoom = 1, StartInFullScreen = true };

        await sut.SaveSettings(settings);
        var loaded = await sut.LoadSettings();

        Assert.That(loaded.DefaultZoom, Is.EqualTo(1m));
        Assert.That(loaded.StartInFullScreen, Is.True);
        Assert.That(loaded.KeyBindings.Count, Is.EqualTo(settings.KeyBindings.Count));
    }

    [Test]
    public async Task AMissingFileGivesDefaults()
    {
        var sut = new SettingsJsonStorage(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        var settings = await sut.LoadSettings();
        Assert.That(settings.DefaultZoom, Is.EqualTo(0m));
    }
}
