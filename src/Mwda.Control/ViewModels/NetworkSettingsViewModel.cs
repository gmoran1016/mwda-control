using Mwda.Control.Mvvm;
using Mwda.Control.Protocol;
using Mwda.Control.Session;

namespace Mwda.Control.ViewModels;

public sealed class NetworkSettingsViewModel : ObservableObject
{
    private static readonly TimeSpan DefaultOperationTimeout = TimeSpan.FromSeconds(5);

    private readonly TimeSpan _operationTimeout;
    private readonly Action<Exception>? _connectionLost;
    private CancellationTokenSource? _operationCancellation;
    private AdapterSession? _session;
    private string _ssid = string.Empty;
    private IReadOnlyList<string> _availableSsids = [];
    private string _savedSsid = string.Empty;
    private string? _password;
    private bool _isConnected;
    private bool _isVisible;
    private bool _isDirty;
    private string? _resultBanner;

    public NetworkSettingsViewModel(
        TimeSpan? operationTimeout = null,
        Action<Exception>? connectionLost = null)
    {
        _operationTimeout = operationTimeout ?? DefaultOperationTimeout;
        _connectionLost = connectionLost;
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => IsVisible && IsDirty);
        ForgetCommand = new AsyncRelayCommand(ForgetAsync, () => IsVisible && IsConnected);
    }

    public AsyncRelayCommand SaveCommand { get; }

    public AsyncRelayCommand ForgetCommand { get; }

    public IReadOnlyList<string> AvailableSsids
    {
        get => _availableSsids;
        private set => SetProperty(ref _availableSsids, value);
    }

    public string Ssid
    {
        get => _ssid;
        set
        {
            if (SetProperty(ref _ssid, value))
            {
                AddAvailableSsid(value);
                UpdateDirtyState();
            }
        }
    }

    public string? Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                UpdateDirtyState();
            }
        }
    }

    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (SetProperty(ref _isConnected, value))
            {
                ForgetCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        private set
        {
            if (SetProperty(ref _isVisible, value))
            {
                SaveCommand.NotifyCanExecuteChanged();
                ForgetCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (SetProperty(ref _isDirty, value))
            {
                SaveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string? ResultBanner
    {
        get => _resultBanner;
        private set => SetProperty(ref _resultBanner, value);
    }

    public async Task LoadAsync(AdapterSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        IsVisible = session.CapabilityProfile.Supports(AdapterOperation.GetWiFiSettings);
        if (!IsVisible)
        {
            if (!IsDirty)
            {
                AvailableSsids = [];
            }

            ResultBanner = "Network settings are unavailable on this adapter.";
            return;
        }

        var cancellation = BeginOperation();
        try
        {
            var settings = await session.AdvancedClient.GetWiFiSettingsAsync(cancellation.Token);
            _savedSsid = settings.Ssid;
            if (!IsDirty)
            {
                AvailableSsids = [];
            }

            AddAvailableSsid(settings.Ssid);
            IsConnected = settings.IsConnected;
            if (!IsDirty)
            {
                SetProperty(ref _ssid, settings.Ssid, nameof(Ssid));
                SetProperty(ref _password, null, nameof(Password));
                IsDirty = false;
            }

            AddAvailableSsid(Ssid);

            ResultBanner = null;
        }
        catch (Exception exception)
        {
            HandleFailure(exception, cancellation);
        }
        finally
        {
            EndOperation(cancellation);
        }
    }

    public async Task SaveAsync()
    {
        var session = _session;
        if (session is null || !IsVisible)
        {
            ResultBanner = "Network settings are unavailable while disconnected.";
            return;
        }

        var requestedSsid = Ssid;
        var requestedPassword = Password;
        if (string.IsNullOrWhiteSpace(requestedSsid))
        {
            IsDirty = true;
            ResultBanner = "Enter a Wi-Fi network name.";
            return;
        }

        var cancellation = BeginOperation();
        try
        {
            if (!session.CapabilityProfile.Supports(AdapterOperation.SetWiFiSettings))
            {
                throw new InvalidOperationException("Network changes are unavailable on this adapter.");
            }

            await session.AdvancedClient.SetWiFiSettingsAsync(
                new WifiSettings(requestedSsid, true, requestedPassword),
                cancellation.Token);
            _savedSsid = requestedSsid;
            SetProperty(ref _password, null, nameof(Password));
            IsConnected = true;
            IsDirty = false;
            ResultBanner = "Applied.";
        }
        catch (Exception exception)
        {
            IsDirty = true;
            HandleFailure(exception, cancellation);
        }
        finally
        {
            EndOperation(cancellation);
        }
    }

    public async Task ForgetAsync()
    {
        var session = _session;
        if (session is null || !IsVisible)
        {
            ResultBanner = "Network settings are unavailable while disconnected.";
            return;
        }

        var cancellation = BeginOperation();
        try
        {
            if (!session.CapabilityProfile.Supports(AdapterOperation.ForgetWiFi))
            {
                throw new InvalidOperationException("Forgetting the network is unavailable on this adapter.");
            }

            await session.AdvancedClient.ForgetWiFiAsync(cancellation.Token);
            IsConnected = false;
            ResultBanner = "Applied.";
        }
        catch (Exception exception)
        {
            HandleFailure(exception, cancellation);
        }
        finally
        {
            EndOperation(cancellation);
        }
    }

    public void Disconnect()
    {
        _operationCancellation?.Cancel();
        _session = null;
        IsConnected = false;
        IsVisible = false;
        ResultBanner = "Disconnected. Unsaved edits were preserved.";
    }

    private void UpdateDirtyState()
    {
        IsDirty = !string.Equals(Ssid, _savedSsid, StringComparison.Ordinal) || Password is not null;
    }

    private void AddAvailableSsid(string? ssid)
    {
        if (string.IsNullOrWhiteSpace(ssid) ||
            _availableSsids.Any(option => string.Equals(option, ssid, StringComparison.Ordinal)))
        {
            return;
        }

        AvailableSsids = [.. _availableSsids, ssid];
    }

    private CancellationTokenSource BeginOperation()
    {
        var cancellation = new CancellationTokenSource(_operationTimeout);
        var previous = Interlocked.Exchange(ref _operationCancellation, cancellation);
        previous?.Cancel();
        return cancellation;
    }

    private void EndOperation(CancellationTokenSource cancellation)
    {
        Interlocked.CompareExchange(ref _operationCancellation, null, cancellation);
        cancellation.Dispose();
    }

    private void HandleFailure(Exception exception, CancellationTokenSource cancellation)
    {
        if (exception is AdapterTransportException)
        {
            _connectionLost?.Invoke(exception);
            ResultBanner =
                "Adapter not reachable; reconnect through Windows wireless display settings and try again.";
            return;
        }

        if (exception is OperationCanceledException && cancellation.IsCancellationRequested)
        {
            if (ReferenceEquals(_operationCancellation, cancellation))
            {
                ResultBanner = "Operation cancelled.";
            }

            return;
        }

        ResultBanner = exception.Message;
    }
}
