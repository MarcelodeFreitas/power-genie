using System.Windows;
using Application = System.Windows.Application;

namespace PowerGenie.App;

public partial class App : Application
{
    private TrayIconManager? _trayIconManager;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _trayIconManager = new TrayIconManager(() => { });
        _trayIconManager.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIconManager?.Dispose();
        base.OnExit(e);
    }
}
