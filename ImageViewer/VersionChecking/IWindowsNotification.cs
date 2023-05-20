using Microsoft.Toolkit.Uwp.Notifications;

namespace ImageViewer;

public interface IWindowsNotification
{
    void Send(string title, string message, string buttonText, string url);
}

public class WindowsNotification: IWindowsNotification
{
    public void Send(string title, string message, string buttonText, string url)
    {
        new ToastContentBuilder()
            .AddText(title)
            .AddText(message)
            .AddButton(buttonText, ToastActivationType.Protocol, url)
            .Show();
    }
}