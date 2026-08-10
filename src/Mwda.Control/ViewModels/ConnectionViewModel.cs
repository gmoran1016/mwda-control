using Mwda.Control.Discovery;
using Mwda.Control.Mvvm;
using Mwda.Control.Session;

namespace Mwda.Control.ViewModels;

public sealed class ConnectionViewModel : ObservableObject
{
    private static readonly TimeSpan DefaultOperationTimeout = TimeSpan.FromSeconds(10);

    private readonly IAdapterDiscovery _discovery;
    private readonly IAdapterSessionFactory _sessionFactory;
    private readonly Func<AdapterSession, CancellationToken, Task> _connected;
    private readonly Action _disconnected;
    private readonly TimeSpan _operationTimeout;
    private CancellationTokenSource? _refreshCancellation;
    private AdapterSession? _session;
    private DiscoveredAdapter? _selectedAdapter;
    private bool _isConnected;
    private string _connectionState = "Disconnected";
    private string? _resultBanner;

    public ConnectionViewModel(
        IAdapterDiscovery discovery,
        IAdapterSessionFactory sessionFactory,
        Func<AdapterSession, CancellationToken, Task>? connected = null,
        Action? disconnected = null,
        TimeSpan? operationTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(sessionFactory);
        _discovery = discovery;
        _sessionFactory = sessionFactory;
        _connected = connected ?? ((_, _) => Task.CompletedTask);
        _disconnected = disconnected ?? (() => { });
        _operationTimeout = operationTimeout ?? DefaultOperationTimeout;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
    }

    public AsyncRelayCommand RefreshCommand { get; }

    public DiscoveredAdapter? SelectedAdapter
    {
        get => _selectedAdapter;
        private set => SetProperty(ref _selectedAdapter, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        private set => SetProperty(ref _isConnected, value);
    }

    public string ConnectionState
    {
        get => _connectionState;
        private set => SetProperty(ref _connectionState, value);
    }

    public string? ResultBanner
    {
        get => _resultBanner;
        private set => SetProperty(ref _resultBanner, value);
    }

    public async Task RefreshAsync()
    {
        var cancellation = new CancellationTokenSource(_operationTimeout);
        var previousCancellation = Interlocked.Exchange(ref _refreshCancellation, cancellation);
        previousCancellation?.Cancel();
        AdapterSession? candidateSession = null;

        ConnectionState = "Searching";
        ResultBanner = null;
        try
        {
            var adapters = await _discovery.DiscoverAsync(cancellation.Token);
            if (adapters.Count == 0)
            {
                if (ReferenceEquals(_refreshCancellation, cancellation))
                {
                    PublishDisconnected("No adapter was found. Connect through Windows wireless display settings and refresh.");
                }

                return;
            }

            var selectedAdapter = adapters[0];
            candidateSession = await _sessionFactory.CreateAsync(selectedAdapter, cancellation.Token);
            if (!ReferenceEquals(_refreshCancellation, cancellation))
            {
                return;
            }

            SelectedAdapter = selectedAdapter;
            await _connected(candidateSession, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();

            var previousSession = Interlocked.Exchange(ref _session, candidateSession);
            candidateSession = null;
            previousSession?.Dispose();
            IsConnected = true;
            ConnectionState = "Connected";
            ResultBanner = $"Connected to {selectedAdapter.DeviceName}.";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (ReferenceEquals(_refreshCancellation, cancellation))
            {
                PublishDisconnected(CreateNotReachableMessage());
            }
        }
        catch (Exception exception)
        {
            if (ReferenceEquals(_refreshCancellation, cancellation))
            {
                PublishDisconnected($"{CreateNotReachableMessage()} {exception.Message}");
            }
        }
        finally
        {
            candidateSession?.Dispose();
            Interlocked.CompareExchange(ref _refreshCancellation, null, cancellation);
            cancellation.Dispose();
        }
    }

    public void HandleConnectionLoss(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _refreshCancellation?.Cancel();
        PublishDisconnected($"{CreateNotReachableMessage()} {exception.Message}");
    }

    private void PublishDisconnected(string message)
    {
        var session = Interlocked.Exchange(ref _session, null);
        IsConnected = false;
        ConnectionState = "Disconnected";
        SelectedAdapter = null;
        ResultBanner = message;
        _disconnected();
        session?.Dispose();
    }

    private static string CreateNotReachableMessage() =>
        "Adapter not reachable; reconnect through Windows wireless display settings and try again.";
}
