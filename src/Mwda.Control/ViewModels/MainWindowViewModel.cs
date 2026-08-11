using System.Collections.ObjectModel;
using Mwda.Control.Discovery;
using Mwda.Control.Mvvm;
using Mwda.Control.Session;

namespace Mwda.Control.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private NavigationItem? _selectedPage;
    private ObservableObject? _selectedPageViewModel;

    public MainWindowViewModel(
        IAdapterDiscovery discovery,
        IAdapterSessionFactory sessionFactory,
        TimeSpan? operationTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(sessionFactory);

        Adapter = new AdapterSettingsViewModel(operationTimeout, HandleConnectionLoss);
        Display = new DisplaySettingsViewModel(operationTimeout, HandleConnectionLoss);
        Network = new NetworkSettingsViewModel(operationTimeout, HandleConnectionLoss);
        ConnectionSettings = new ConnectionSettingsViewModel();
        Diagnostics = new DiagnosticsViewModel();
        About = new AboutViewModel(Diagnostics);
        Connection = new ConnectionViewModel(
            discovery,
            sessionFactory,
            LoadSessionAsync,
            DisconnectPages,
            operationTimeout);

        NavigationItems = [];
        RebuildNavigation(includeNetwork: false);
        StartupRefresh = Connection.RefreshCommand.ExecuteAsync();
    }

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    public NavigationItem? SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (SetProperty(ref _selectedPage, value))
            {
                SelectedPageViewModel = value?.Page;
            }
        }
    }

    public ObservableObject? SelectedPageViewModel
    {
        get => _selectedPageViewModel;
        private set => SetProperty(ref _selectedPageViewModel, value);
    }

    public ConnectionViewModel Connection { get; }

    public AdapterSettingsViewModel Adapter { get; }

    public DisplaySettingsViewModel Display { get; }

    public NetworkSettingsViewModel Network { get; }

    public ConnectionSettingsViewModel ConnectionSettings { get; }

    public DiagnosticsViewModel Diagnostics { get; }

    public AboutViewModel About { get; }

    public Task StartupRefresh { get; }

    public bool IsFirmwareVisible => false;

    private async Task LoadSessionAsync(AdapterSession session, CancellationToken cancellationToken)
    {
        await Task.WhenAll(
            Adapter.LoadAsync(session),
            Display.LoadAsync(session),
            Network.LoadAsync(session));
        cancellationToken.ThrowIfCancellationRequested();
        ConnectionSettings.Load(session);
        Diagnostics.Load(session);
        RebuildNavigation(Network.IsVisible);
    }

    private void HandleConnectionLoss(Exception exception)
    {
        Diagnostics.RecordError(exception.Message);
        Connection.HandleConnectionLoss(exception);
    }

    private void DisconnectPages()
    {
        Adapter.Disconnect();
        Display.Disconnect();
        Network.Disconnect();
        ConnectionSettings.Disconnect();
        Diagnostics.Disconnect();
        RebuildNavigation(includeNetwork: false);
    }

    private void RebuildNavigation(bool includeNetwork)
    {
        var selectedKey = SelectedPage?.Key;
        NavigationItems.Clear();
        NavigationItems.Add(new NavigationItem("Adapter", "Adapter", Adapter));
        NavigationItems.Add(new NavigationItem("Display", "Display", Display));
        if (includeNetwork)
        {
            NavigationItems.Add(new NavigationItem("Network", "Network", Network));
        }

        NavigationItems.Add(new NavigationItem("Connection", "Connection", ConnectionSettings));
        NavigationItems.Add(new NavigationItem("About", "About", About));
        NavigationItems.Add(new NavigationItem("Diagnostics", "Diagnostics", Diagnostics));
        SelectedPage =
            NavigationItems.FirstOrDefault(item => item.Key == selectedKey) ?? NavigationItems[0];
    }
}

public sealed record NavigationItem(string Key, string Title, ObservableObject Page);
