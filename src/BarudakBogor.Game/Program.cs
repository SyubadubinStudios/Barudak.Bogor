using Avalonia;

namespace BarudakBogor.Game;

internal static class Program
{
    /// <summary>Folder for an automated screenshot tour (<c>--capture &lt;dir&gt;</c>), or null for normal play.</summary>
    public static string? CaptureDirectory { get; private set; }

    /// <summary>File for a frame-rate report (<c>--bench &lt;file&gt;</c>): drives the same stretch at each quality level.</summary>
    public static string? BenchFile { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        int capture = Array.IndexOf(args, "--capture");
        if (capture >= 0 && capture + 1 < args.Length)
        {
            CaptureDirectory = args[capture + 1];
        }

        int bench = Array.IndexOf(args, "--bench");
        if (bench >= 0 && bench + 1 < args.Length)
        {
            BenchFile = args[bench + 1];
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
