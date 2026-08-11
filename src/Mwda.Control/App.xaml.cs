using System.Windows;
using Mwda.Control.Discovery;
using Mwda.Control.Session;
using Mwda.Control.ViewModels;

namespace Mwda.Control;

public partial class App : Application
{
    private AdapterDiscovery? _discovery;
    private MainWindowViewModel? _viewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _discovery = new AdapterDiscovery();
        _viewModel = new MainWindowViewModel(_discovery, new AdapterSessionFactory());
        var window = new MainWindow(_viewModel);

        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _viewModel?.Dispose();
        _discovery?.Dispose();
        base.OnExit(e);
    }
}
