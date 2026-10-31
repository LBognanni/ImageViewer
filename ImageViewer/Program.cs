using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using SkiaSharp;

namespace ImageViewer;

static class Program
{
    /// <summary>
    /// Used until the real screen size is known. Too large only costs a slightly slower first decode.
    /// </summary>
    private static readonly SKSizeI DefaultScreen = new(3840, 2160);

    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--bench")
        {
            return Bench.Run(args[1]);
        }

        var decoder = new DecoderChain(new IImageDecoder[] { new SkiaImageDecoder() });

        var settingsStorage = new SettingsJsonStorage();
        Settings settings;
        string? settingsError = null;
        try
        {
            settings = settingsStorage.LoadSettings().Result;
            Task.Run(() => settingsStorage.SaveSettings(settings));
        }
        catch (Exception ex)
        {
            settings = new Settings();
            settingsError = GetErrorMessage(ex);
        }

        var pipeline = new ImagePipeline(decoder, ImagePipeline.DefaultBudget(), DefaultScreen, ImagePipeline.DefaultWorkers())
        {
            PrefetchFull = settings.DefaultZoom != 0
        };
        var navigator = new Navigator(pipeline);

        // Start decoding now: bringing up the UI takes longer than a scaled decode
        if (args.Length > 0 && File.Exists(args[0]))
        {
            navigator.Open(Path.GetFullPath(args[0]));
        }

        var services = new AppServices(navigator, pipeline, settingsStorage, settings, new FileBasedTempData(),
            Platform.CreateTrash(), Platform.CreateClickThrough(), Platform.CreateDefaultViewer(), decoder.Extensions, settingsError);

        var result = AppBuilder.Configure(() => new App(services))
            .UsePlatformDetect()
            .StartWithClassicDesktopLifetime(args);

        pipeline.Dispose();
        return result;
    }

    private static string GetErrorMessage(Exception exception)
    {
        if (exception.InnerException != null)
            return GetErrorMessage(exception.InnerException);
        return exception.Message;
    }
}
