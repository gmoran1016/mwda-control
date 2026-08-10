using System.IO;
using System.Net.Http;
using Mwda.Control.Mvvm;
using Mwda.Control.Protocol;
using Mwda.Control.Session;

namespace Mwda.Control.ViewModels;

public sealed class DisplaySettingsViewModel : ObservableObject
{
    private static readonly TimeSpan DefaultOperationTimeout = TimeSpan.FromSeconds(5);

    private readonly TimeSpan _operationTimeout;
    private readonly Action<Exception>? _connectionLost;
    private CancellationTokenSource? _operationCancellation;
    private AdapterSession? _session;
    private bool _isAutoAdjust;
    private bool _savedIsAutoAdjust;
    private int _overscanValue;
    private int _savedOverscanValue;
    private string? _selectedWallpaperId;
    private string? _savedWallpaperId;
    private IReadOnlyList<string> _availableWallpaperIds = [];
    private bool _supportsCustomWallpaper;
    private bool _isWallpaperSupported;
    private bool _isAvailable;
    private bool _isDirty;
    private string? _resultBanner;

    public DisplaySettingsViewModel(
        TimeSpan? operationTimeout = null,
        Action<Exception>? connectionLost = null)
    {
        _operationTimeout = operationTimeout ?? DefaultOperationTimeout;
        _connectionLost = connectionLost;
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => IsAvailable && IsDirty);
    }

    public AsyncRelayCommand SaveCommand { get; }

    public bool IsAutoAdjust
    {
        get => _isAutoAdjust;
        set
        {
            if (SetProperty(ref _isAutoAdjust, value))
            {
                UpdateDirtyState();
            }
        }
    }

    public int OverscanValue
    {
        get => _overscanValue;
        set
        {
            if (SetProperty(ref _overscanValue, value))
            {
                UpdateDirtyState();
            }
        }
    }

    public string? SelectedWallpaperId
    {
        get => _selectedWallpaperId;
        set
        {
            if (SetProperty(ref _selectedWallpaperId, value))
            {
                UpdateDirtyState();
            }
        }
    }

    public IReadOnlyList<string> AvailableWallpaperIds
    {
        get => _availableWallpaperIds;
        private set => SetProperty(ref _availableWallpaperIds, value);
    }

    public bool SupportsCustomWallpaper
    {
        get => _supportsCustomWallpaper;
        private set => SetProperty(ref _supportsCustomWallpaper, value);
    }

    public bool IsWallpaperSupported
    {
        get => _isWallpaperSupported;
        private set => SetProperty(ref _isWallpaperSupported, value);
    }

    public bool IsAvailable
    {
        get => _isAvailable;
        private set
        {
            if (SetProperty(ref _isAvailable, value))
            {
                SaveCommand.NotifyCanExecuteChanged();
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
        IsAvailable = true;
        IsWallpaperSupported =
            session.CapabilityProfile.Supports(AdapterOperation.GetWallpaperInfo) &&
            session.CapabilityProfile.Supports(AdapterOperation.SetWallpaper);

        var cancellation = BeginOperation();
        try
        {
            var overscan = session.CapabilityProfile.Supports(AdapterOperation.GetOverscan)
                ? await session.Client.GetOverscanAsync(cancellation.Token)
                : null;
            var wallpaper = IsWallpaperSupported
                ? await session.AdvancedClient.GetWallpaperInfoAsync(cancellation.Token)
                : null;

            _savedIsAutoAdjust = overscan?.IsAutoAdjust ?? false;
            _savedOverscanValue = overscan?.Value ?? 0;
            _savedWallpaperId = wallpaper?.CurrentWallpaperId;
            AvailableWallpaperIds = wallpaper?.AvailableWallpaperIds ?? [];
            SupportsCustomWallpaper = wallpaper?.SupportsCustomWallpaper ?? false;
            if (!IsDirty)
            {
                SetLoadedValues(_savedIsAutoAdjust, _savedOverscanValue, _savedWallpaperId);
            }

            ResultBanner = null;
        }
        catch (Exception exception)
        {
            HandleFailure(exception);
        }
        finally
        {
            EndOperation(cancellation);
        }
    }

    public async Task SaveAsync()
    {
        var session = _session;
        if (session is null)
        {
            ResultBanner = "Disconnected. Reconnect before saving.";
            return;
        }

        var requestedOverscan = new OverscanSettings(IsAutoAdjust, OverscanValue);
        var requestedWallpaper = SelectedWallpaperId;
        var cancellation = BeginOperation();
        try
        {
            if (requestedOverscan.IsAutoAdjust != _savedIsAutoAdjust ||
                requestedOverscan.Value != _savedOverscanValue)
            {
                if (!session.CapabilityProfile.Supports(AdapterOperation.SetOverscan))
                {
                    throw new InvalidOperationException("Overscan settings are unavailable on this adapter.");
                }

                await session.Client.SetOverscanAsync(requestedOverscan, cancellation.Token);
            }

            if (!string.Equals(requestedWallpaper, _savedWallpaperId, StringComparison.Ordinal))
            {
                if (!IsWallpaperSupported || requestedWallpaper is null)
                {
                    throw new InvalidOperationException("Wallpaper settings are unavailable on this adapter.");
                }

                await session.AdvancedClient.SetPredefinedWallpaperAsync(
                    requestedWallpaper,
                    cancellation.Token);
            }

            _savedIsAutoAdjust = requestedOverscan.IsAutoAdjust;
            _savedOverscanValue = requestedOverscan.Value;
            _savedWallpaperId = requestedWallpaper;
            IsDirty = false;
            ResultBanner = "Applied.";
        }
        catch (Exception exception)
        {
            IsDirty = true;
            HandleFailure(exception);
        }
        finally
        {
            EndOperation(cancellation);
        }
    }

    public async Task UploadCustomWallpaperAsync(Stream image, string fileName, string contentType)
    {
        ArgumentNullException.ThrowIfNull(image);
        var session = _session;
        if (session is null)
        {
            ResultBanner = "Disconnected. Reconnect before uploading.";
            return;
        }

        var cancellation = BeginOperation();
        try
        {
            if (!IsWallpaperSupported || !SupportsCustomWallpaper)
            {
                throw new InvalidOperationException("Custom wallpaper is unavailable on this adapter.");
            }

            await session.AdvancedClient.UploadCustomWallpaperAsync(
                image,
                fileName,
                contentType,
                cancellation.Token);
            ResultBanner = "Applied.";
        }
        catch (Exception exception)
        {
            HandleFailure(exception);
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
        IsAvailable = false;
        ResultBanner = "Disconnected. Unsaved edits were preserved.";
    }

    private void SetLoadedValues(bool isAutoAdjust, int overscanValue, string? wallpaperId)
    {
        SetProperty(ref _isAutoAdjust, isAutoAdjust, nameof(IsAutoAdjust));
        SetProperty(ref _overscanValue, overscanValue, nameof(OverscanValue));
        SetProperty(ref _selectedWallpaperId, wallpaperId, nameof(SelectedWallpaperId));
        IsDirty = false;
    }

    private void UpdateDirtyState()
    {
        IsDirty =
            IsAutoAdjust != _savedIsAutoAdjust ||
            OverscanValue != _savedOverscanValue ||
            !string.Equals(SelectedWallpaperId, _savedWallpaperId, StringComparison.Ordinal);
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

    private void HandleFailure(Exception exception)
    {
        if (exception is OperationCanceledException or HttpRequestException or TimeoutException)
        {
            _connectionLost?.Invoke(exception);
            ResultBanner =
                "Adapter not reachable; reconnect through Windows wireless display settings and try again.";
            return;
        }

        ResultBanner = exception.Message;
    }
}
