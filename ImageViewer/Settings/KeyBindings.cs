using System;
using Avalonia.Input;

namespace ImageViewer;

public class KeyBinding
{
    public string? Action { get; set; }
    public bool IsDeleteAction { get; set; }
    public string? Command { get; set; }

    public KeyBinding()
    {
    }

    public KeyBinding(string action)
    {
        Action = action;
    }
}

public readonly record struct KeyCombination(Key Key, KeyModifiers Modifiers);

public static class KeyParser
{
    /// <summary>
    /// Parse strings like <c>Shift|R</c>. These began as names of the WinForms <c>Keys</c> enum, which
    /// differ from Avalonia's <c>Key</c> mostly in case (Oemplus, OemPlus), so matching ignores case.
    /// </summary>
    public static KeyCombination? Parse(string s)
    {
        Key? key = null;
        var modifiers = KeyModifiers.None;

        foreach (var part in s.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var modifier = ParseModifier(part);
            if (modifier != null)
            {
                modifiers |= modifier.Value;
            }
            else if (key == null && Enum.TryParse(part, true, out Key k))
            {
                key = k;
            }
            else
            {
                return null;
            }
        }

        return key == null ? null : new KeyCombination(key.Value, modifiers);
    }

    private static KeyModifiers? ParseModifier(string s) => s.ToLowerInvariant() switch
    {
        "shift" or "shiftkey" => KeyModifiers.Shift,
        "control" or "controlkey" or "ctrl" => KeyModifiers.Control,
        "alt" or "menu" => KeyModifiers.Alt,
        "meta" or "cmd" or "lwin" or "rwin" => KeyModifiers.Meta,
        _ => null
    };
}
