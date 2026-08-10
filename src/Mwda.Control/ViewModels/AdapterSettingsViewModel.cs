using System.Net.Http;
using Mwda.Control.Mvvm;
using Mwda.Control.Protocol;
using Mwda.Control.Session;

namespace Mwda.Control.ViewModels;

public sealed class AdapterSettingsViewModel : ObservableObject
{
    private static readonly TimeSpan DefaultOperationTimeout = TimeSpan.FromSeconds(5);

    private readonly TimeSpan _operationTimeout;
    private readonly Action<Exception>? _connectionLost;
    private CancellationTokenSource? _operationCancellation;
    private AdapterSession? _session;
    private string _deviceName = string.Empty;
    private string _savedDeviceName = string.Empty;
    private bool _passwordProtectionEnabled;
    private bool _savedPasswordProtectionEnabled;
    private string? _password;
    private string? _selectedLanguageTag;
    private string? _savedLanguageTag;
    private IReadOnlyList<string> _availableLanguageTags = [];
    private bool _isPasswordProtectionSupported;
    private bool _isLanguageSupported;
    private bool _isAvailable;
    private bool _isDirty;
    private string? _resultBanner;

    public AdapterSettingsViewModel(
        TimeSpan? operationTimeout = null,
        Action<Exception>? connectionLost = null)
    {
        _operationTimeout = operationTimeout ?? DefaultOperationTimeout;
        _connectionLost = connectionLost;
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => IsAvailable && IsDirty);
    }

    public AsyncRelayCommand SaveCommand { get; }

    public string DeviceName
    {
        get => _deviceName;
        set
        {
            if (SetProperty(ref _deviceName, value))
            {
                UpdateDirtyState();
            }
        }
    }

    public bool PasswordProtectionEnabled
    {
        get => _passwordProtectionEnabled;
        set
        {
            if (SetProperty(ref _passwordProtectionEnabled, value))
            {
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

    public string? SelectedLanguageTag
    {
        get => _selectedLanguageTag;
        set
        {
            if (SetProperty(ref _selectedLanguageTag, value))
            {
                UpdateDirtyState();
            }
        }
    }

    public IReadOnlyList<string> AvailableLanguageTags
    {
        get => _availableLanguageTags;
        private set => SetProperty(ref _availableLanguageTags, value);
    }

    public bool IsPasswordProtectionSupported
    {
        get => _isPasswordProtectionSupported;
        private set => SetProperty(ref _isPasswordProtectionSupported, value);
    }

    public bool IsLanguageSupported
    {
        get => _isLanguageSupported;
        private set => SetProperty(ref _isLanguageSupported, value);
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
        IsPasswordProtectionSupported =
            session.CapabilityProfile.Supports(AdapterOperation.GetPasswordProtection) &&
            session.CapabilityProfile.Supports(AdapterOperation.SetPasswordProtection);
        IsLanguageSupported =
            session.CapabilityProfile.Supports(AdapterOperation.GetLanguage) &&
            session.CapabilityProfile.Supports(AdapterOperation.SetLanguage);

        var cancellation = BeginOperation();
        try
        {
            var protection = IsPasswordProtectionSupported
                ? await session.Client.GetPasswordProtectionAsync(cancellation.Token)
                : null;
            var language = IsLanguageSupported
                ? await session.AdvancedClient.GetLanguageAsync(cancellation.Token)
                : null;

            _savedDeviceName = session.AdapterIdentity.DeviceName;
            _savedPasswordProtectionEnabled = protection?.Enabled ?? false;
            _savedLanguageTag = language?.LanguageTag;
            AvailableLanguageTags = language?.AvailableLanguageTags ?? [];
            if (!IsDirty)
            {
                SetLoadedValues(
                    _savedDeviceName,
                    _savedPasswordProtectionEnabled,
                    _savedLanguageTag);
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

        var requestedName = DeviceName;
        var requestedProtection = PasswordProtectionEnabled;
        var requestedPassword = Password;
        var requestedLanguage = SelectedLanguageTag;
        var cancellation = BeginOperation();
        try
        {
            if (!string.Equals(requestedName, _savedDeviceName, StringComparison.Ordinal))
            {
                await session.Client.SetDeviceNameAsync(requestedName, cancellation.Token);
            }

            if (requestedProtection != _savedPasswordProtectionEnabled || requestedPassword is not null)
            {
                if (!IsPasswordProtectionSupported)
                {
                    throw new InvalidOperationException("Pairing protection is unavailable on this adapter.");
                }

                await session.Client.SetPasswordProtectionAsync(
                    requestedProtection,
                    requestedPassword,
                    cancellation.Token);
            }

            if (!string.Equals(requestedLanguage, _savedLanguageTag, StringComparison.OrdinalIgnoreCase))
            {
                if (!IsLanguageSupported || requestedLanguage is null)
                {
                    throw new InvalidOperationException("Language settings are unavailable on this adapter.");
                }

                await session.AdvancedClient.SetLanguageAsync(requestedLanguage, cancellation.Token);
            }

            _savedDeviceName = requestedName;
            _savedPasswordProtectionEnabled = requestedProtection;
            _savedLanguageTag = requestedLanguage;
            SetProperty(ref _password, null, nameof(Password));
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

    public void Disconnect()
    {
        _operationCancellation?.Cancel();
        _session = null;
        IsAvailable = false;
        ResultBanner = "Disconnected. Unsaved edits were preserved.";
    }

    private void SetLoadedValues(string deviceName, bool protectionEnabled, string? languageTag)
    {
        SetProperty(ref _deviceName, deviceName, nameof(DeviceName));
        SetProperty(ref _passwordProtectionEnabled, protectionEnabled, nameof(PasswordProtectionEnabled));
        SetProperty(ref _selectedLanguageTag, languageTag, nameof(SelectedLanguageTag));
        SetProperty(ref _password, null, nameof(Password));
        IsDirty = false;
    }

    private void UpdateDirtyState()
    {
        IsDirty =
            !string.Equals(DeviceName, _savedDeviceName, StringComparison.Ordinal) ||
            PasswordProtectionEnabled != _savedPasswordProtectionEnabled ||
            Password is not null ||
            !string.Equals(SelectedLanguageTag, _savedLanguageTag, StringComparison.OrdinalIgnoreCase);
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
