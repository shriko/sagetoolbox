using System.Windows;
using SageToolbox.Core;
using SageToolbox.Views;

namespace SageToolbox;

/// <summary>
/// Point d'entrée. Le DPI awareness (PerMonitorV2) est géré nativement par le runtime
/// WPF .NET (par défaut depuis .NET Core 3.0), aucun manifest supplémentaire requis.
/// </summary>
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var savedTheme = ThemeManager.ParseSaved(ConfigManager.Instance.Config.Theme);
        ThemeManager.Apply(savedTheme);

        if (await UpdateManager.CheckAndPromptAsync())
        {
            Shutdown();
            return;
        }

        var authWindow = new AuthWindow();
        var authenticated = authWindow.ShowDialog() == true;

        if (!authenticated)
        {
            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnMainWindowClose;
        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}
