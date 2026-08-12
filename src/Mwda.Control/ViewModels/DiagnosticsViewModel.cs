using System.ComponentModel;
using Mwda.Control.Mvvm;
using Mwda.Control.Protocol;
using Mwda.Control.Session;

namespace Mwda.Control.ViewModels;

public sealed class DiagnosticsViewModel : ObservableObject
{
    private static readonly TimeSpan DefaultOperationTimeout = TimeSpan.FromSeconds(5);

    private readonly TimeSpan _operationTimeout;
    private readonly Action<Exception>? _connectionLost;
    private readonly Action? _adapterRestarted;
    private CancellationTokenSource? _operationCancellation;
    private AdapterSession? _session;
    private bool _isAvailable;
    private bool _isRestartSupported;
    private AdapterIdentity? _identity;
    private CapabilityProfile? _capabilities;
    private string? _adapterAddress;
    private string? _lastError;
    private string? _resultBanner;

    public DiagnosticsViewModel(
        TimeSpan? operationTimeout = null,
        Action<Exception>? connectionLost = null,
        Action? adapterRestarted = null)
    {
        _operationTimeout = operationTimeout ?? DefaultOperationTimeout;
        _connectionLost = connectionLost;
        _adapterRestarted = adapterRestarted;
        RestartCommand = new AsyncRelayCommand(
            RestartAsync,
            () => IsAvailable && IsRestartSupported);
        RestartCommand.PropertyChanged += RestartCommandPropertyChanged;
    }

    public AsyncRelayCommand RestartCommand { get; }

    public bool IsAvailable
    {
        get => _isAvailable;
        private set
        {
            if (SetProperty(ref _isAvailable, value))
            {
                RestartCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(CanRestart));
            }
        }
    }

    public bool IsRestartSupported
    {
        get => _isRestartSupported;
        private set
        {
            if (SetProperty(ref _isRestartSupported, value))
            {
                RestartCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(CanRestart));
            }
        }
    }

    public bool CanRestart =>
        IsAvailable && IsRestartSupported && !RestartCommand.IsExecuting;

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

    public string? ResultBanner
    {
        get => _resultBanner;
        private set => SetProperty(ref _resultBanner, value);
    }

    public void Load(AdapterSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        Identity = session.AdapterIdentity;
        Capabilities = session.CapabilityProfile;
        AdapterAddress = session.DiscoveredAdapter.IpAddress.ToString();
        IsRestartSupported = session.CapabilityProfile.Supports(AdapterOperation.Restart);
        LastError = null;
        ResultBanner = null;
        IsAvailable = true;
    }

    public async Task RestartAsync()
    {
        var session = _session;
        if (session is null || !IsRestartSupported)
        {
            ResultBanner = "Disconnected. Reconnect before restarting the adapter.";
            return;
        }

        var cancellation = BeginOperation();
        try
        {
            await session.AdvancedClient.RestartAsync(cancellation.Token);
            _adapterRestarted?.Invoke();
            ResultBanner =
                "Restart requested. Reconnect through Windows wireless display settings, then refresh.";
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

    public void RecordError(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        LastError = message;
    }

    public void Disconnect()
    {
        _operationCancellation?.Cancel();
        _session = null;
        IsRestartSupported = false;
        IsAvailable = false;
        ResultBanner = "Disconnected. Reconnect before restarting the adapter.";
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

    private void RestartCommandPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(AsyncRelayCommand.IsExecuting))
        {
            OnPropertyChanged(nameof(CanRestart));
        }
    }
}
