using System.Windows;
using Mwda.Control.Discovery;
using Mwda.Control.Session;
using Mwda.Control.ViewModels;

namespace Mwda.Control;

public partial class App : Application
{
    private AdapterDiscovery? _discovery;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _discovery = new AdapterDiscovery();
        var viewModel = new MainWindowViewModel(_discovery, new AdapterSessionFactory());
        var window = new MainWindow(viewModel);

        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _discovery?.Dispose();
        base.OnExit(e);
    }
}
