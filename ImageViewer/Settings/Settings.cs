using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace ImageViewer;

public class Settings
{
    [JsonIgnore]
    private Dictionary<string, KeyBinding>? _keyBindings;
    [JsonIgnore]
    private Dictionary<Keys, KeyBinding>? _keyBindingsLookup;
    
    public bool StartInFullScreen { get; set; }
    public decimal DefaultZoom { get; set; }
    public bool TransparentWhenOutOfFocus { get; set; }

    public Settings()
    {
        DefaultZoom = 0;
        StartInFullScreen = false;
        TransparentWhenOutOfFocus = true;

        KeyBindings = new Dictionary<string, KeyBinding>()
        {
            { "A", new KeyBinding("AutoPlay") },
            { "Add", new KeyBinding("AutoPlayFaster") },
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
            { "T", new KeyBinding("ToggleTopMost") },
            { "W", new KeyBinding("ToggleClickThrough") }
        };
    }

    public bool TryGetKeyBindingFor(Keys keys, out KeyBinding keyBinding) =>
        _keyBindingsLookup!.TryGetValue(keys, out keyBinding);

    
    public Dictionary<string, KeyBinding> KeyBindings
    {
        get => _keyBindings!;
        set
        {
            _keyBindings = value ?? throw new ArgumentNullException(nameof(KeyBindings));
            
            _keyBindingsLookup = value
                .Select(x => new
                {
                    Key = ParseKeys(x.Key),
                    x.Value
                })
                .Where(x => x.Key != null)
                .ToDictionary(x => x.Key!.Value, x => x.Value);
        }
    }

    private Keys? ParseKeys(string s)
    {
        var keys = s.Split("|".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(
            x =>
            {
                if (Enum.TryParse(x, out Keys k))
                {
                    return (Keys?)k;
                }

                return (Keys?)null;
            }).ToList();
        
        if (keys.Any(x => x == null))
        {
            return null;
        }

        return keys.Aggregate((a, b) => a! | b!);
    }
}