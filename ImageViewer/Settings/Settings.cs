using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace ImageViewer;

public class Settings
{
    private Dictionary<string, KeyBinding>? _keyBindings;
    private Dictionary<KeyCombination, KeyBinding>? _keyBindingsLookup;

    public bool StartInFullScreen { get; set; }
    public decimal DefaultZoom { get; set; }

    public Settings()
    {
        DefaultZoom = 0;
        StartInFullScreen = false;

        KeyBindings = new Dictionary<string, KeyBinding>()
        {
            { "A", new KeyBinding("AutoPlay") },
            { "Add", new KeyBinding("AutoPlayFaster") },
            { "OemPlus", new KeyBinding("AutoPlayFaster") },
            { "Shift|OemPlus", new KeyBinding("AutoPlayFaster") },
            { "B", new KeyBinding("ToggleBorderless") },
            { "C", new KeyBinding("CopyFileName") },
            { "D", new KeyBinding("DeleteCurrentFile") },
            { "D1", new KeyBinding("ToggleZoom") },
            { "D2", new KeyBinding("Transparency1") },
            { "D3", new KeyBinding("Transparency2") },
            { "D4", new KeyBinding("Transparency3") },
            { "D5", new KeyBinding("Transparency4") },
            { "D6", new KeyBinding("Transparency5") },
            { "Escape", new KeyBinding("ExitFullScreenOrClose") },
            { "F", new KeyBinding("ToggleFullScreen") },
            { "H", new KeyBinding("ToggleHelpMessage") },
            { "Left", new KeyBinding("PreviousImage") },
            { "O", new KeyBinding("ReOpen") },
            { "P", new KeyBinding("ShowPreferences") },
            { "Q", new KeyBinding("Close") },
            { "R", new KeyBinding("RotateImage") },
            { "Shift|R", new KeyBinding("RotateImageInv") },
            { "Right", new KeyBinding("NextImage") },
            { "S", new KeyBinding("Shuffle") },
            { "Subtract", new KeyBinding("AutoPlaySlower") },
            { "OemMinus", new KeyBinding("AutoPlaySlower") },
            { "T", new KeyBinding("ToggleTopMost") },
            { "W", new KeyBinding("ToggleClickThrough") }
        };
    }

    public bool TryGetKeyBindingFor(KeyCombination keys, out KeyBinding? keyBinding) =>
        _keyBindingsLookup!.TryGetValue(keys, out keyBinding);

    public Dictionary<string, KeyBinding> KeyBindings
    {
        get => _keyBindings!;
        set
        {
            _keyBindings = value ?? throw new ArgumentNullException(nameof(KeyBindings));

            // Two strings can now parse to the same keys, so the last one wins instead of throwing
            _keyBindingsLookup = new Dictionary<KeyCombination, KeyBinding>();
            foreach (var (keys, binding) in value.Select(x => (KeyParser.Parse(x.Key), x.Value)))
            {
                if (keys != null)
                {
                    _keyBindingsLookup[keys.Value] = binding;
                }
            }
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true,
    AllowTrailingCommas = true, ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip)]
[JsonSerializable(typeof(Settings))]
[JsonSerializable(typeof(Dictionary<string, DateTime>))]
internal partial class AppJsonContext : JsonSerializerContext
{
}
