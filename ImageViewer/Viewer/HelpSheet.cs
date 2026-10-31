using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Input;

namespace ImageViewer;

public sealed record HelpAction(string Action, string Description);

/// <summary>
/// One line of the help. Related actions share a line when their keys allow it, for example ← → for
/// previous and next. Otherwise each action gets a line of its own.
/// </summary>
public sealed record HelpItem(string Description, params HelpAction[] Actions)
{
    public HelpItem(string action, string description) : this(description, new HelpAction(action, description))
    {
    }
}

public sealed record HelpSectionTemplate(string Title, params HelpItem[] Items);

public sealed record HelpRow(string Keys, string Description);

public sealed record HelpSection(string Title, IReadOnlyList<HelpRow> Rows);

public sealed record HelpSheet(IReadOnlyList<HelpSection> Sections, string CloseHint)
{
    public static HelpSheet Build(IEnumerable<HelpSectionTemplate> templates, Settings settings,
        IReadOnlyCollection<string> knownActions, IEnumerable<string> closeActions)
    {
        var sections = templates
            .Select(t => new HelpSection(t.Title, t.Items.SelectMany(i => Rows(i, settings)).ToList()))
            .Append(new HelpSection("Custom", CustomRows(settings, knownActions).ToList()))
            .Where(s => s.Rows.Count > 0)
            .ToList();

        var closeKeys = closeActions.SelectMany(a => KeysFor(a, settings)).Distinct();
        return new HelpSheet(sections, $"Press {string.Join(" or ", closeKeys)} to close");
    }

    private static IEnumerable<HelpRow> Rows(HelpItem item, Settings settings)
    {
        var bound = item.Actions
            .Select(a => (a.Description, Keys: KeysFor(a.Action, settings)))
            .Where(a => a.Keys.Count > 0)
            .ToList();

        var merged = bound.Count == item.Actions.Length ? MergeKeys(bound.Select(a => a.Keys).ToList()) : null;
        return merged != null
            ? new[] { new HelpRow(merged, item.Description) }
            : bound.Select(a => new HelpRow(string.Join(" / ", a.Keys), a.Description));
    }

    /// <summary>
    /// The keys for a shared line, or null if they wouldn't make sense together. Each action must have
    /// one key, so the keys line up with the description, and a long list must be a run of digits.
    /// </summary>
    private static string? MergeKeys(IReadOnlyList<IReadOnlyList<string>> keys)
    {
        if (keys.Any(k => k.Count != 1))
            return null;

        var labels = keys.Select(k => k[0]).ToList();
        if (labels.Count <= 2)
            return string.Join(" ", labels);

        var isRun = labels.All(l => l.Length == 1 && char.IsAsciiDigit(l[0]))
            && labels.Zip(labels.Skip(1)).All(p => p.Second[0] == p.First[0] + 1);
        return isRun ? $"{labels[0]}–{labels[^1]}" : null;
    }

    private static IEnumerable<HelpRow> CustomRows(Settings settings, IReadOnlyCollection<string> knownActions) =>
        settings.KeyBindings
            .Where(b => !knownActions.Contains(b.Value.Action ?? "") && !string.IsNullOrEmpty(b.Value.Command))
            .Select(b => (Keys: KeyParser.Parse(b.Key), b.Value.Command))
            .Where(b => b.Keys != null)
            .Select(b => new HelpRow(FormatKey(b.Keys!.Value),
                Path.GetFileNameWithoutExtension(FileUtilites.SplitCommand(b.Command!).exe)));

    /// <summary>
    /// The distinct labels of the keys bound to an action. Add and OemPlus both read as +, and
    /// the shifted form of a key that works unshifted is only there for some keyboard layouts.
    /// </summary>
    private static IReadOnlyList<string> KeysFor(string action, Settings settings)
    {
        var keys = settings.KeyBindings
            .Where(b => b.Value.Action == action)
            .Select(b => KeyParser.Parse(b.Key))
            .OfType<KeyCombination>()
            .ToList();

        return keys
            .Where(k => k.Modifiers != KeyModifiers.Shift || !keys.Contains(k with { Modifiers = KeyModifiers.None }))
            .Select(FormatKey)
            .Distinct()
            .ToList();
    }

    private static string FormatKey(KeyCombination keys)
    {
        var modifiers = "";
        if (keys.Modifiers.HasFlag(KeyModifiers.Control)) modifiers += "Ctrl+";
        if (keys.Modifiers.HasFlag(KeyModifiers.Alt)) modifiers += "Alt+";
        if (keys.Modifiers.HasFlag(KeyModifiers.Meta)) modifiers += "Meta+";
        if (keys.Modifiers.HasFlag(KeyModifiers.Shift)) modifiers += "⇧";
        return modifiers + FormatKey(keys.Key);
    }

    private static string FormatKey(Key key) => key switch
    {
        Key.Left => "←",
        Key.Right => "→",
        Key.Up => "↑",
        Key.Down => "↓",
        Key.Add or Key.OemPlus => "+",
        Key.Subtract or Key.OemMinus => "-",
        Key.Escape => "Esc",
        Key.Enter => "Enter",
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        _ => key.ToString()
    };
}
