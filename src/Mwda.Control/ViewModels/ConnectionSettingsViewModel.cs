using Mwda.Control.Mvvm;
using Mwda.Control.Session;

namespace Mwda.Control.ViewModels;

public sealed class ConnectionSettingsViewModel : ObservableObject
{
    private static readonly Uri DiscoveryUri =
        new("ms-settings-connectabledevices:devicediscovery");

    private bool _isAvailable;
    private string _connectionState = "Disconnected";
    private string? _adapterName;
    private string? _adapterAddress;

    public Uri WindowsWirelessDisplaySettingsUri => DiscoveryUri;

    public bool IsAvailable
    {
        get => _isAvailable;
        private set => SetProperty(ref _isAvailable, value);
    }

    public string ConnectionState
    {
        get => _connectionState;
        private set => SetProperty(ref _connectionState, value);
    }

    public string? AdapterName
    {
        get => _adapterName;
        private set => SetProperty(ref _adapterName, value);
    }

    public string? AdapterAddress
    {
        get => _adapterAddress;
        private set => SetProperty(ref _adapterAddress, value);
    }

    public void Load(AdapterSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        AdapterName = session.AdapterIdentity.DeviceName;
        AdapterAddress = session.DiscoveredAdapter.IpAddress.ToString();
        ConnectionState = "Connected";
        IsAvailable = true;
    }

    public void Disconnect()
    {
        ConnectionState = "Disconnected";
        IsAvailable = false;
    }
}
