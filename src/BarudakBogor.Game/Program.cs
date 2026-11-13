using Avalonia;

namespace BarudakBogor.Game;

internal static partial class Program
{
    /// <summary>Folder for an automated screenshot tour (<c>--capture &lt;dir&gt;</c>), or null for normal play.</summary>
    public static string? CaptureDirectory { get; private set; }

    /// <summary>With <c>--capture</c>: only these stops of the tour (<c>--only ciliwung,kolam</c>), for a quick look at one change.</summary>
    public static string[] CaptureOnly { get; private set; } = [];

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

        int only = Array.IndexOf(args, "--only");
        if (only >= 0 && only + 1 < args.Length)
        {
            CaptureOnly = [.. args[only + 1].Split(',', StringSplitOptions.RemoveEmptyEntries)];
        }

        int bench = Array.IndexOf(args, "--bench");
        if (bench >= 0 && bench + 1 < args.Length)
        {
            BenchFile = args[bench + 1];
        }

        // Avalonia paces its composition with a dispatcher timer. At the default 15.6 ms Windows
        // timer resolution a frame that finishes early waits for the next grid line, which locks
        // a 14 ms frame to 30 fps. A 1 ms resolution lets it be shown when it is ready.
        bool fineTimer = OperatingSystem.IsWindows() && TimeBeginPeriod(1) == 0;
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            if (fineTimer)
            {
                _ = TimeEndPeriod(1);
            }
        }
    }

    [System.Runtime.InteropServices.LibraryImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static partial uint TimeBeginPeriod(uint milliseconds);

    [System.Runtime.InteropServices.LibraryImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static partial uint TimeEndPeriod(uint milliseconds);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
