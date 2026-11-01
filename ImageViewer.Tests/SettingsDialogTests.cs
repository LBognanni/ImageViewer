using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Moq;
using NUnit.Framework;

namespace ImageViewer.Tests;

[TestFixture]
public class SettingsDialogTests
{
    private readonly List<Uri> _opened = new();
    private SettingsDialog _sut = null!;
    private bool _browserWorks;

    private void Open()
    {
        // The app only loads this once, and each test gets a new app
        if (!Application.Current!.Styles.OfType<FluentTheme>().Any())
        {
            Application.Current.Styles.Add(App.CreateDialogTheme());
        }

        _opened.Clear();
        _browserWorks = true;
        _sut = new SettingsDialog(new Mock<ISettingsStorage>().Object, new Settings(), new Mock<IDefaultViewer>().Object, uri =>
        {
            _opened.Add(uri);
            return Task.FromResult(_browserWorks);
        });
        _sut.Show();
        Pump();
    }

    [TearDown]
    public void TearDown() => _sut.Close();

    [AvaloniaTest]
    public void OpensOnGeneral_WithAboutInItsOwnTab()
    {
        Open();
        var tabs = _sut.GetLogicalDescendants().OfType<TabControl>().Single();
        Assert.That(tabs.Items.Cast<TabItem>().Select(t => t.Header), Is.EqualTo(new[] { "General", "About" }));
        Assert.That(tabs.SelectedIndex, Is.EqualTo(0));
    }

    [AvaloniaTest]
    public void NothingOpensUntilALinkIsClicked()
    {
        Open();
        ShowAbout();
        Assert.That(_opened, Is.Empty);
    }

    [TestCase("GitHub", "https://github.com/LBognanni/ImageViewer")]
    [TestCase("Report an issue", "https://github.com/LBognanni/ImageViewer/issues")]
    [TestCase("Releases", "https://github.com/LBognanni/ImageViewer/releases")]
    [TestCase("codemade.net","https://codemade.net/")]
    [TestCase("☕ Buy me a coffee", "https://buymeacoffee.com/codemade")]
    [AvaloniaTheory]
    public void LinksOpenTheirPages(string name, string url)
    {
        Open();
        ShowAbout();
        Click(Button(name));
        Assert.That(_opened.Single().AbsoluteUri, Is.EqualTo(url));
    }

    [AvaloniaTest]
    public void ShowsTheLinkWhenTheBrowserWontOpen()
    {
        Open();
        ShowAbout();
        _browserWorks = false;
        Click(Button("Releases"));

        var error = _sut.GetVisualDescendants().OfType<SelectableTextBlock>().Single();
        Assert.That(error.IsEffectivelyVisible, Is.True);
        Assert.That(error.Text, Does.Contain("https://github.com/LBognanni/ImageViewer/releases"));

        _browserWorks = true;
        Click(Button("Releases"));
        Assert.That(error.IsVisible, Is.False);
    }

    [AvaloniaTest]
    public void ShowsTheVersion()
    {
        var panel = new AboutPanel(_ => Task.FromResult(true), new Version(2, 1, 3, 0));
        Assert.That(panel.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text), Does.Contain("Version 2.1.3"));
    }

    [AvaloniaTest]
    public void TooltipsKeepLinksOnOneLine()
    {
        Open();
        ShowAbout();
        var tip = (ToolTip)ToolTip.GetTip(Button("Report an issue"))!;
        Assert.That(tip.MaxWidth, Is.EqualTo(double.PositiveInfinity));
        Assert.That(((TextBlock)tip.Content!).TextWrapping, Is.EqualTo(Avalonia.Media.TextWrapping.NoWrap));
    }

    [AvaloniaTest]
    public void EverythingFitsInTheWindow()
    {
        Open();
        ShowAbout();
        _browserWorks = false;
        Click(Button("Releases"));

        // The error is the lowest thing on the tab, and the buttons sit below that
        var error = _sut.GetVisualDescendants().OfType<SelectableTextBlock>().Single();
        var bottom = error.TranslatePoint(new Point(0, error.Bounds.Height), _sut)!.Value.Y;
        var ok = Button("Ok").TranslatePoint(default, _sut)!.Value.Y;
        Assert.That(bottom, Is.LessThanOrEqualTo(ok));
        Assert.That(ok + Button("Ok").Bounds.Height, Is.LessThanOrEqualTo(_sut.ClientSize.Height));
    }

    private void ShowAbout()
    {
        _sut.GetLogicalDescendants().OfType<TabControl>().Single().SelectedIndex = 1;
        Pump();
    }

    private Button Button(string name) => _sut.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == name);

    private void Click(Button button)
    {
        var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), _sut)!.Value;
        _sut.MouseDown(center, MouseButton.Left);
        _sut.MouseUp(center, MouseButton.Left);
        Pump();
    }

    private void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        _sut.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
