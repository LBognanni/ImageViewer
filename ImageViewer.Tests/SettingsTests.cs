using NUnit.Framework.Legacy;
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
        ClassicAssert.AreEqual(new KeyCombination(key, modifiers), KeyParser.Parse(text));
    }

    [TestCase("")]
    [TestCase("Shift")]
    [TestCase("NotAKey")]
    [TestCase("A|B")]
    public void InvalidKeysParseToNull(string text)
    {
        ClassicAssert.IsNull(KeyParser.Parse(text));
    }

    [Test]
    public void AllDefaultKeysParse()
    {
        var settings = new Settings();
        foreach (var key in settings.KeyBindings.Keys)
        {
            ClassicAssert.IsNotNull(KeyParser.Parse(key), key);
        }
        ClassicAssert.IsTrue(settings.TryGetKeyBindingFor(new KeyCombination(Key.R, KeyModifiers.Shift), out var binding));
        ClassicAssert.AreEqual("RotateImageInv", binding!.Action);
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

        ClassicAssert.IsTrue(settings.StartInFullScreen);
        ClassicAssert.AreEqual(1m, settings.DefaultZoom);
        ClassicAssert.IsFalse(settings.TransparentWhenOutOfFocus);
        ClassicAssert.AreEqual(2, settings.KeyBindings.Count, "saved bindings replace the defaults");
        ClassicAssert.IsTrue(settings.TryGetKeyBindingFor(new KeyCombination(Key.OemPlus, KeyModifiers.None), out var binding));
        ClassicAssert.AreEqual("NextImage", binding!.Action);
        ClassicAssert.IsTrue(settings.KeyBindings["X"].IsDeleteAction);
    }

    [Test]
    public async Task SavedSettingsCanBeReadBack()
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory().FullName, "nested", "settings.json");
        var sut = new SettingsJsonStorage(path);
        var settings = new Settings { DefaultZoom = 1, StartInFullScreen = true };

        await sut.SaveSettings(settings);
        var loaded = await sut.LoadSettings();

        ClassicAssert.AreEqual(1m, loaded.DefaultZoom);
        ClassicAssert.IsTrue(loaded.StartInFullScreen);
        ClassicAssert.AreEqual(settings.KeyBindings.Count, loaded.KeyBindings.Count);
    }

    [Test]
    public async Task AMissingFileGivesDefaults()
    {
        var sut = new SettingsJsonStorage(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        var settings = await sut.LoadSettings();
        ClassicAssert.IsTrue(settings.TransparentWhenOutOfFocus);
    }
}
