using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ImageViewer;

public sealed class MessageDialog : Window
{
    private MessageDialog(string message, bool canCancel)
    {
        Title = "Image Viewer";
        Width = 460;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var ok = new Button { Content = "Ok", IsDefault = true, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
        ok.Click += (_, _) => Close(true);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { ok }
        };
        if (canCancel)
        {
            var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
            cancel.Click += (_, _) => Close(false);
            buttons.Children.Add(cancel);
        }

        Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 16,
            Children = { new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }, buttons }
        };
    }

    /// <summary>
    /// Returns true if the user pressed Ok
    /// </summary>
    public static async Task<bool> Show(Window owner, string message, bool canCancel = false)
    {
        App.EnsureDialogTheme();
        return await new MessageDialog(message, canCancel).ShowDialog<bool>(owner);
    }
}
