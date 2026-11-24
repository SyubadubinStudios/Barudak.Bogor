using Android.App;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Avalonia;
using Avalonia.Android;
using BarudakBogor.Game.Rendering;

namespace BarudakBogor.Game;

[Application]
public sealed class MainApplication(nint javaReference, JniHandleOwnership transfer)
    : AvaloniaAndroidApplication<App>(javaReference, transfer)
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder)
            .WithInterFont();
}

[Activity(
    Label = "Barudak Bogor",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ScreenOrientation = ScreenOrientation.Landscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity
{
    private const string AssetVersion = "0.3.1-pbr";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        ExtractAssets();
        base.OnCreate(savedInstanceState);
        HideSystemBars();
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (hasFocus)
        {
            HideSystemBars();
        }
    }

    private void HideSystemBars()
    {
        if (Window?.DecorView is { } decor)
        {
#pragma warning disable CA1422 // Supports API 24-29 devices cleanly
            decor.SystemUiVisibility = (StatusBarVisibility)(
                SystemUiFlags.ImmersiveSticky
                | SystemUiFlags.Fullscreen
                | SystemUiFlags.HideNavigation
                | SystemUiFlags.LayoutStable
                | SystemUiFlags.LayoutFullscreen
                | SystemUiFlags.LayoutHideNavigation);
#pragma warning restore CA1422
        }
    }

    /// <summary>
    /// Extracts bundled GLB models and billboard art from APK assets to app-private storage so
    /// the native Three.Net loader can open them as normal files.
    /// </summary>
    private void ExtractAssets()
    {
        string root = Path.Combine(FilesDir!.AbsolutePath, "Assets");
        string modelsDir = Path.Combine(root, "Models");
        string artDir = Path.Combine(root, "Art");
        string stamp = Path.Combine(root, ".version");

        ModelLibrary.ModelDirectory = modelsDir;
        WorldRenderer.ArtDirectory = artDir;

        if (File.Exists(stamp) && File.ReadAllText(stamp) == AssetVersion)
        {
            return;
        }

        CopyAssetFolder(Assets!, "Assets/Models", modelsDir);
        CopyAssetFolder(Assets!, "Assets/Art", artDir);
        Directory.CreateDirectory(root);
        File.WriteAllText(stamp, AssetVersion);
    }

    private static void CopyAssetFolder(AssetManager assets, string assetPath, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        string[]? entries = assets.List(assetPath);
        if (entries is null)
        {
            return;
        }

        foreach (string name in entries)
        {
            string src = $"{assetPath}/{name}";
            string dst = Path.Combine(targetDir, name);
            using Stream input = assets.Open(src);
            using FileStream output = File.Create(dst);
            input.CopyTo(output);
        }
    }
}
