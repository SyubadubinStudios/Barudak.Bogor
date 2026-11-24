namespace BarudakBogor.Game;

/// <summary>Optional desktop command-line launch flags (capture tour / benchmark).</summary>
public static class LaunchOptions
{
    public static string? CaptureDirectory { get; set; }

    public static string[] CaptureOnly { get; set; } = [];

    public static string? BenchFile { get; set; }

    public static bool Scripted => CaptureDirectory is not null || BenchFile is not null;
}
