using Mwda.Control.Mvvm;
using Mwda.Control.Protocol;
using Mwda.Control.Session;

namespace Mwda.Control.ViewModels;

public sealed class DiagnosticsViewModel : ObservableObject
{
    private bool _isAvailable;
    private AdapterIdentity? _identity;
    private CapabilityProfile? _capabilities;
    private string? _adapterAddress;
    private string? _lastError;

    public bool IsAvailable
    {
        get => _isAvailable;
        private set => SetProperty(ref _isAvailable, value);
    }

    public AdapterIdentity? Identity
    {
        get => _identity;
        private set => SetProperty(ref _identity, value);
    }

    public CapabilityProfile? Capabilities
    {
        get => _capabilities;
        private set => SetProperty(ref _capabilities, value);
    }

    public string? AdapterAddress
    {
        get => _adapterAddress;
        private set => SetProperty(ref _adapterAddress, value);
    }

    public string? LastError
    {
        get => _lastError;
        private set => SetProperty(ref _lastError, value);
    }

    public void Load(AdapterSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        Identity = session.AdapterIdentity;
        Capabilities = session.CapabilityProfile;
        AdapterAddress = session.DiscoveredAdapter.IpAddress.ToString();
        LastError = null;
        IsAvailable = true;
    }

    public void RecordError(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        LastError = message;
    }

    public void Disconnect()
    {
        IsAvailable = false;
    }
}
