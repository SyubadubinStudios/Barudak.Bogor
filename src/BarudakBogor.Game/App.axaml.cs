using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using BarudakBogor.Game.Views;

namespace BarudakBogor.Game;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainView view = new();
            Avalonia.Controls.Window window = new()
            {
                Title = "Barudak Bogor",
                Width = 1280,
                Height = 720,
                MinWidth = 960,
                MinHeight = 600,
                WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterScreen,
                Content = view,
            };
            desktop.MainWindow = window;
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = new MainView();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
