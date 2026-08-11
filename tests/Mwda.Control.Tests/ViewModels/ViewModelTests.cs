using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using Mwda.Control.Discovery;
using Mwda.Control.Mvvm;
using Mwda.Control.Protocol;
using Mwda.Control.Session;
using Mwda.Control.ViewModels;
using Mwda.Control.Views;

namespace Mwda.Control.Tests.ViewModels;

public sealed class ViewModelTests
{
    private static readonly DiscoveredAdapter Discovered = new(
        IPAddress.Parse("192.168.137.247"),
        "Wi-Fi Direct",
        "WeightRoom-AD",
        TimeSpan.FromMilliseconds(12),
        false);

    [Fact]
    public async Task StartupRefreshSelectsAdapterLoadsPagesAndBuildsSupportedNavigation()
    {
        var client = new RecordingClient();
        var advancedClient = new RecordingAdvancedClient();
        var session = CreateSession(client, advancedClient, CreateCapabilities(includeWifi: true));
        var discovery = new StubDiscovery(_ => Task.FromResult<IReadOnlyList<DiscoveredAdapter>>([Discovered]));
        var factory = new StubSessionFactory((_, _) => Task.FromResult(session));

        var shell = new MainWindowViewModel(discovery, factory);
        await shell.StartupRefresh;

        Assert.True(shell.Connection.IsConnected);
        Assert.Equal("WeightRoom-AD", shell.Connection.SelectedAdapter!.DeviceName);
        Assert.Equal("WeightRoom-AD", shell.Adapter.DeviceName);
        Assert.Equal(
            ["Adapter", "Display", "Network", "Connection", "About", "Diagnostics"],
            shell.NavigationItems.Select(item => item.Key));
        Assert.DoesNotContain(shell.NavigationItems, item => item.Key == "Firmware");
        Assert.Equal(1, discovery.CallCount);

        shell.Adapter.DeviceName = "Room_2+(West)";
        await shell.Adapter.SaveAsync();

        Assert.Equal(["Room_2+(West)"], client.DeviceNameWrites);
        Assert.False(shell.Adapter.IsDirty);
    }

    [Fact]
    public async Task MissingWifiCapabilityOmitsOnlyNetworkNavigation()
    {
        var session = CreateSession(
            new RecordingClient(),
            new RecordingAdvancedClient(),
            CreateCapabilities(includeWifi: false));
        var shell = CreateShell(session);

        await shell.StartupRefresh;

        Assert.Equal(
            ["Adapter", "Display", "Connection", "About", "Diagnostics"],
            shell.NavigationItems.Select(item => item.Key));
        Assert.DoesNotContain(shell.NavigationItems, item => item.Key == "Firmware");
        Assert.False(shell.Network.IsVisible);
        Assert.True(shell.Adapter.IsAvailable);
        Assert.True(shell.Display.IsAvailable);
        Assert.True(shell.ConnectionSettings.IsAvailable);
        Assert.True(shell.Diagnostics.IsAvailable);
    }

    [Fact]
    public void WpfShellCompilesAllRequiredViews()
    {
        var assembly = typeof(Mwda.Control.App).Assembly;

        foreach (var viewName in new[]
                 {
                     "DisconnectedView",
                     "AdapterView",
                     "DisplayView",
                     "NetworkView",
                     "ConnectionView",
                     "AboutView",
                     "DiagnosticsView",
                 })
        {
            Assert.NotNull(assembly.GetType($"Mwda.Control.Views.{viewName}"));
        }
    }

    [Fact]
    public void DataTemplateViewsInitializeTheirGeneratedXamlContent()
    {
        foreach (var viewName in new[]
                 {
                     "DisconnectedView",
                     "AdapterView",
                     "DisplayView",
                     "NetworkView",
                     "ConnectionView",
                     "AboutView",
                 })
        {
            var codeBehind = ReadSource($"src/Mwda.Control/Views/{viewName}.xaml.cs");
            Assert.Contains($"public {viewName}()", codeBehind);
            Assert.Contains("InitializeComponent();", codeBehind);
        }
    }

    [Fact]
    public async Task NetworkViewModelExposesCurrentAndTypedSsidOptionsAndUsesSsidValidation()
    {
        var availableSsidsProperty = typeof(NetworkSettingsViewModel).GetProperty("AvailableSsids");
        Assert.NotNull(availableSsidsProperty);
        Assert.Null(availableSsidsProperty!.GetSetMethod());

        var viewModel = new NetworkSettingsViewModel();
        var writeCount = 0;
        var advancedClient = new RecordingAdvancedClient
        {
            SetWiFiSettings = (_, _) =>
            {
                writeCount++;
                return Task.CompletedTask;
            },
        };
        await viewModel.LoadAsync(
            CreateSession(new RecordingClient(), advancedClient, CreateCapabilities(includeWifi: true)));

        var initialOptions = Assert.IsAssignableFrom<IReadOnlyList<string>>(
            availableSsidsProperty!.GetValue(viewModel));
        Assert.Equal(["GymNet"], initialOptions);

        viewModel.Ssid = "TrainingNet";

        var editedOptions = Assert.IsAssignableFrom<IReadOnlyList<string>>(
            availableSsidsProperty.GetValue(viewModel));
        Assert.Equal(["GymNet", "TrainingNet"], editedOptions);

        viewModel.Ssid = string.Empty;
        await viewModel.SaveAsync();
        Assert.Equal(0, writeCount);
        Assert.True(viewModel.IsDirty);
        Assert.Equal("Enter a Wi-Fi network name.", viewModel.ResultBanner);

        var rule = new SsidValidationRule();
        Assert.False(rule.Validate(string.Empty, CultureInfo.InvariantCulture).IsValid);
        Assert.False(rule.Validate("  ", CultureInfo.InvariantCulture).IsValid);
        Assert.True(rule.Validate("TrainingNet", CultureInfo.InvariantCulture).IsValid);

        var networkView = ReadSource("src/Mwda.Control/Views/NetworkView.xaml");
        Assert.Contains("ItemsSource=\"{Binding AvailableSsids}\"", networkView);
        Assert.Contains("<validation:SsidValidationRule />", networkView);
    }

    [Fact]
    public async Task ShellUsesTypedSelectedPageViewModelAndImplicitTypedTemplates()
    {
        var selectedPageViewModelProperty = typeof(MainWindowViewModel).GetProperty("SelectedPageViewModel");
        Assert.NotNull(selectedPageViewModelProperty);
        Assert.Equal(typeof(ObservableObject), typeof(NavigationItem).GetProperty("Page")?.PropertyType);

        var shell = CreateShell(
            CreateSession(new RecordingClient(), new RecordingAdvancedClient(), CreateCapabilities(includeWifi: true)));
        await shell.StartupRefresh;

        Assert.Same(shell.Adapter, selectedPageViewModelProperty!.GetValue(shell));

        var aboutItem = shell.NavigationItems.Single(item => item.Key == "About");
        shell.SelectedPage = aboutItem;
        Assert.IsType<AboutViewModel>(aboutItem.Page);
        Assert.Same(aboutItem.Page, selectedPageViewModelProperty.GetValue(shell));
        Assert.IsType<DiagnosticsViewModel>(shell.NavigationItems.Single(item => item.Key == "Diagnostics").Page);

        var theme = ReadSource("src/Mwda.Control/Resources/Theme.xaml");
        foreach (var viewModelName in new[]
                 {
                     "AdapterSettingsViewModel",
                     "DisplaySettingsViewModel",
                     "NetworkSettingsViewModel",
                     "ConnectionSettingsViewModel",
                     "AboutViewModel",
                     "DiagnosticsViewModel",
                     "ConnectionViewModel",
                 })
        {
            Assert.Contains($"<DataTemplate DataType=\"{{x:Type vm:{viewModelName}}}\">", theme);
        }

        Assert.DoesNotContain("DataTrigger Binding=\"{Binding Key}\"", theme);
        var mainWindow = ReadSource("src/Mwda.Control/MainWindow.xaml");
        Assert.Contains("Content=\"{Binding SelectedPageViewModel}\"", mainWindow);
        Assert.DoesNotContain("<ContentControl Content=\"{Binding SelectedPage}\"", mainWindow);
    }

    [Fact]
    public void AboutAndDiagnosticsBannersUseRuntimeBindings()
    {
        var aboutView = ReadSource("src/Mwda.Control/Views/AboutView.xaml");
        Assert.DoesNotContain("Text=\"•  {Binding}\"", aboutView);
        Assert.Contains("<TextBlock Text=\"{Binding}\"", aboutView);

        var diagnosticsView = ReadSource("src/Mwda.Control/Views/DiagnosticsView.xaml");
        Assert.Contains(
            "DataContext=\"{Binding DataContext.Connection, RelativeSource={RelativeSource AncestorType={x:Type Window}}}\"",
            diagnosticsView);
        Assert.Contains("Text=\"{Binding ResultBanner}\"", diagnosticsView);
        Assert.Contains(
            "Text=\"{Binding LastError, Mode=OneWay, TargetNullValue=No local errors recorded}\"",
            diagnosticsView);
        Assert.Contains("<Border Style=\"{StaticResource StatusBannerStyle}\"", diagnosticsView);
    }

    [Fact]
    public void DiagnosticsSurfaceDisplaysReportedIdentityDetails()
    {
        var diagnosticsView = ReadSource("src/Mwda.Control/Views/DiagnosticsView.xaml");

        Assert.Contains(
            "Text=\"{Binding Identity.Model, TargetNullValue=Unavailable}\"",
            diagnosticsView);
        Assert.Contains(
            "Text=\"{Binding Identity.Generation}\"",
            diagnosticsView);
        Assert.Contains(
            "Text=\"{Binding Identity.FirmwareVersion, TargetNullValue=Unavailable}\"",
            diagnosticsView);
        Assert.Contains(
            "Text=\"{Binding Identity.MacAddress, TargetNullValue=Unavailable}\"",
            diagnosticsView);
    }

    [Fact]
    public void DisconnectedSurfaceUsesOpaqueBackground()
    {
        var mainWindow = ReadSource("src/Mwda.Control/MainWindow.xaml");
        var zIndex = mainWindow.IndexOf("Panel.ZIndex=\"10\"", StringComparison.Ordinal);

        Assert.True(zIndex >= 0);
        var elementStart = mainWindow.LastIndexOf('<', zIndex);
        Assert.True(elementStart >= 0);
        Assert.StartsWith("<Border", mainWindow[elementStart..zIndex], StringComparison.Ordinal);
        Assert.Contains(
            "Background=\"{DynamicResource WindowBackgroundBrush}\"",
            mainWindow[elementStart..]);
        Assert.Contains(
            "<ContentControl Content=\"{Binding Connection}\" />",
            mainWindow[elementStart..]);
    }

    [Fact]
    public async Task PairingSettingsExposeOnlyTheCharacterizedBooleanOperation()
    {
        Assert.Null(typeof(AdapterSettingsViewModel).GetProperty("Password"));

        var client = new RecordingClient();
        var viewModel = new AdapterSettingsViewModel();
        await viewModel.LoadAsync(CreateSession(client, new RecordingAdvancedClient(), CoreCapabilities()));
        viewModel.PasswordProtectionEnabled = true;

        await viewModel.SaveAsync();

        Assert.Equal([(true, (string?)null)], client.PasswordProtectionWrites);
        var adapterView = ReadSource("src/Mwda.Control/Views/AdapterView.xaml");
        Assert.DoesNotContain("<PasswordBox", adapterView);
        Assert.DoesNotContain("Pairing password (optional)", adapterView);
        Assert.Contains(
            "The adapter's PIN can be enabled or disabled here. This app does not change the PIN value itself.",
            adapterView);
    }

    [Fact]
    public async Task ShellDisposesItsActiveSessionIdempotently()
    {
        var client = new RecordingClient();
        var advancedClient = new RecordingAdvancedClient();
        var shell = CreateShell(CreateSession(client, advancedClient, CoreCapabilities()));
        await shell.StartupRefresh;

        var dispose = typeof(MainWindowViewModel).GetMethod("Dispose", Type.EmptyTypes);
        Assert.NotNull(dispose);
        dispose!.Invoke(shell, null);
        dispose.Invoke(shell, null);

        Assert.Equal(1, client.DisposeCount);
        Assert.Equal(1, advancedClient.DisposeCount);
    }

    [Fact]
    public async Task ShellDisposeCancelsInFlightDiscovery()
    {
        var discoveryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var discovery = new StubDiscovery(async cancellationToken =>
        {
            discoveryStarted.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return [];
        });
        var shell = new MainWindowViewModel(
            discovery,
            new StubSessionFactory((_, _) => throw new InvalidOperationException("No session should be created.")));
        await discoveryStarted.Task;

        var dispose = typeof(MainWindowViewModel).GetMethod("Dispose", Type.EmptyTypes);
        Assert.NotNull(dispose);
        dispose!.Invoke(shell, null);

        await shell.StartupRefresh.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.False(shell.Connection.RefreshCommand.IsExecuting);
    }

    [Fact]
    public void AppExitDisposesShellBeforeDiscovery()
    {
        var app = ReadSource("src/Mwda.Control/App.xaml.cs");
        var shellDisposeIndex = app.IndexOf("_viewModel?.Dispose()", StringComparison.Ordinal);
        var discoveryDisposeIndex = app.IndexOf("_discovery?.Dispose()", StringComparison.Ordinal);

        Assert.True(shellDisposeIndex >= 0);
        Assert.True(discoveryDisposeIndex > shellDisposeIndex);
    }

    [Fact]
    public void ConnectionSettingsExposeWindowsWirelessDisplayDiscoveryUri()
    {
        var viewModel = new ConnectionSettingsViewModel();

        Assert.Equal(
            new Uri("ms-settings-connectabledevices:devicediscovery"),
            viewModel.WindowsWirelessDisplaySettingsUri);
    }

    [Fact]
    public async Task FailedSaveKeepsAdapterEditDirtyAndSurfacesTheError()
    {
        var client = new RecordingClient
        {
            SetDeviceName = (_, _) => throw new InvalidOperationException("Adapter rejected the name."),
        };
        var viewModel = new AdapterSettingsViewModel();
        await viewModel.LoadAsync(CreateSession(client, new RecordingAdvancedClient(), CoreCapabilities()));
        viewModel.DeviceName = "Room_2+(West)";

        await viewModel.SaveAsync();

        Assert.True(viewModel.IsDirty);
        Assert.Contains("Adapter rejected the name.", viewModel.ResultBanner);
    }

    [Fact]
    public async Task SaveCommandReportsExecutingUntilTheTypedWriteCompletes()
    {
        var writeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new RecordingClient
        {
            SetDeviceName = async (_, cancellationToken) =>
            {
                writeStarted.SetResult();
                await releaseWrite.Task.WaitAsync(cancellationToken);
            },
        };
        var viewModel = new AdapterSettingsViewModel();
        await viewModel.LoadAsync(CreateSession(client, new RecordingAdvancedClient(), CoreCapabilities()));
        viewModel.DeviceName = "Room_2+(West)";

        var save = viewModel.SaveCommand.ExecuteAsync();
        await writeStarted.Task;

        Assert.True(viewModel.SaveCommand.IsExecuting);
        Assert.False(viewModel.SaveCommand.CanExecute(null));

        releaseWrite.SetResult();
        await save;

        Assert.False(viewModel.SaveCommand.IsExecuting);
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public async Task StartupRefreshReportsExecutingUntilDiscoveryCompletes()
    {
        var discoveryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDiscovery = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = CreateSession(
            new RecordingClient(),
            new RecordingAdvancedClient(),
            CoreCapabilities());
        var discovery = new StubDiscovery(async cancellationToken =>
        {
            discoveryStarted.SetResult();
            await releaseDiscovery.Task.WaitAsync(cancellationToken);
            return [Discovered];
        });
        var shell = new MainWindowViewModel(
            discovery,
            new StubSessionFactory((_, _) => Task.FromResult(session)));
        await discoveryStarted.Task;

        Assert.True(shell.Connection.RefreshCommand.IsExecuting);

        releaseDiscovery.SetResult();
        await shell.StartupRefresh;

        Assert.False(shell.Connection.RefreshCommand.IsExecuting);
    }

    [Fact]
    public async Task StartupRefreshAllowsDiscoveryAndSessionLoadingToExceedTheOriginalTenSecondBudget()
    {
        var session = CreateSession(
            new RecordingClient(),
            new RecordingAdvancedClient(),
            CoreCapabilities());
        var discovery = new StubDiscovery(async cancellationToken =>
        {
            await Task.Delay(TimeSpan.FromSeconds(6), cancellationToken);
            return [Discovered];
        });
        var factory = new StubSessionFactory(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(6), cancellationToken);
            return session;
        });

        var shell = new MainWindowViewModel(discovery, factory);
        await shell.StartupRefresh;

        Assert.True(shell.Connection.IsConnected);
        Assert.Equal("Connected", shell.Connection.ConnectionState);
        Assert.Equal("Connected to WeightRoom-AD.", shell.Connection.ResultBanner);
    }

    [Fact]
    public async Task StartupRefreshRetriesAfterAnEmptyDiscoveryResult()
    {
        var session = CreateSession(
            new RecordingClient(),
            new RecordingAdvancedClient(),
            CoreCapabilities());
        var discoveryCalls = 0;
        var discovery = new StubDiscovery(_ =>
        {
            discoveryCalls++;
            return discoveryCalls == 1
                ? Task.FromResult<IReadOnlyList<DiscoveredAdapter>>([])
                : Task.FromResult<IReadOnlyList<DiscoveredAdapter>>([Discovered]);
        });
        var shell = new MainWindowViewModel(
            discovery,
            new StubSessionFactory((_, _) => Task.FromResult(session)));

        await shell.StartupRefresh;

        Assert.True(shell.Connection.IsConnected);
        Assert.Equal(2, discovery.CallCount);
    }

    [Fact]
    public async Task PageSaveTimeoutCancelsLocallyAndPreservesConnectionAndEdit()
    {
        var client = new RecordingClient
        {
            SetDeviceName = (_, cancellationToken) =>
                Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken),
        };
        var shell = CreateShell(
            CreateSession(client, new RecordingAdvancedClient(), CoreCapabilities()),
            TimeSpan.FromMilliseconds(50));
        await shell.StartupRefresh;
        shell.Adapter.DeviceName = "Room_2+(West)";

        await shell.Adapter.SaveAsync();

        Assert.True(shell.Connection.IsConnected);
        Assert.True(shell.Adapter.IsDirty);
        Assert.Equal("Room_2+(West)", shell.Adapter.DeviceName);
        Assert.Contains("cancelled", shell.Adapter.ResultBanner, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RealClientTransportFailureDisconnectsShellAndPreservesTheEdit()
    {
        using var handler = new StubHttpMessageHandler((request, _) =>
        {
            var action = GetAction(request);
            return action switch
            {
                "GetPBCMode" => Task.FromResult(JsonResponse("""{"PBCModeStatus":"Disabled"}""")),
                "GetOverscanSetting" => Task.FromResult(
                    JsonResponse("""{"IsAutoAdjust":false,"OverscanSettingValue":0}""")),
                "SetDeviceName" => throw new HttpRequestException("Connection refused."),
                _ => throw new InvalidOperationException($"Unexpected action: {action}"),
            };
        });
        using var client = new AdapterClient(
            new AdapterEndpoint(new Uri("http://192.168.137.247/")),
            handler,
            TimeSpan.FromSeconds(2));
        var shell = CreateShell(
            CreateSession(client, new RecordingAdvancedClient(), CoreCapabilities()));
        await shell.StartupRefresh;
        shell.Adapter.DeviceName = "Room_2+(West)";

        await shell.Adapter.SaveAsync();

        Assert.False(shell.Connection.IsConnected);
        Assert.True(shell.Adapter.IsDirty);
        Assert.Equal("Room_2+(West)", shell.Adapter.DeviceName);
        Assert.Contains("not reachable", shell.Adapter.ResultBanner, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SupersededNetworkSaveDoesNotDisconnectShellOrClearDirtyEdit()
    {
        var saveStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var advancedClient = new RecordingAdvancedClient
        {
            SetWiFiSettings = async (_, cancellationToken) =>
            {
                saveStarted.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            },
        };
        var shell = CreateShell(
            CreateSession(new RecordingClient(), advancedClient, CreateCapabilities(includeWifi: true)));
        await shell.StartupRefresh;
        shell.Network.Ssid = "TrainingNet";

        var save = shell.Network.SaveAsync();
        await saveStarted.Task;
        var forget = shell.Network.ForgetAsync();
        await Task.WhenAll(save, forget);

        Assert.True(shell.Connection.IsConnected);
        Assert.True(shell.Network.IsVisible);
        Assert.True(shell.Network.IsDirty);
        Assert.False(shell.Network.IsConnected);
        Assert.Equal("Applied.", shell.Network.ResultBanner);
        Assert.DoesNotContain(
            "not reachable",
            shell.Network.ResultBanner ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConcurrentPageSavesUseTheClientsExistingWriteSerialization()
    {
        var firstWriteStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writeEntrances = 0;
        var deviceName = "WeightRoom-AD";
        var overscan = new OverscanSettings(false, 0);

        using var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            var action = GetAction(request);
            switch (action)
            {
                case "GetDeviceName":
                    return JsonResponse($$"""{"DeviceName":"{{deviceName}}"}""");
                case "GetOverscanSetting":
                    return JsonResponse(
                        $$"""{"IsAutoAdjust":{{overscan.IsAutoAdjust.ToString().ToLowerInvariant()}},"OverscanSettingValue":{{overscan.Value}}}""");
                case "SetDeviceName":
                    Interlocked.Increment(ref writeEntrances);
                    firstWriteStarted.SetResult();
                    await releaseFirstWrite.Task.WaitAsync(cancellationToken);
                    deviceName = "Room_A";
                    return JsonResponse("{}");
                case "SetOverscanSetting":
                    Interlocked.Increment(ref writeEntrances);
                    overscan = new OverscanSettings(false, 10);
                    return JsonResponse("{}");
                default:
                    throw new InvalidOperationException($"Unexpected action: {action}");
            }
        });
        using var client = new AdapterClient(
            new AdapterEndpoint(new Uri("http://192.168.137.247/")),
            handler,
            TimeSpan.FromSeconds(2));
        using var session = CreateSession(
            client,
            new RecordingAdvancedClient(),
            new CapabilityProfile(
                AdapterGeneration.Generation2,
                new HashSet<AdapterOperation>
                {
                    AdapterOperation.GetDeviceName,
                    AdapterOperation.SetDeviceName,
                    AdapterOperation.GetOverscan,
                    AdapterOperation.SetOverscan,
                }));
        var adapter = new AdapterSettingsViewModel();
        var display = new DisplaySettingsViewModel();
        await adapter.LoadAsync(session);
        await display.LoadAsync(session);
        adapter.DeviceName = "Room_A";
        display.OverscanValue = 10;

        var adapterSave = adapter.SaveAsync();
        await firstWriteStarted.Task;
        var displaySave = display.SaveAsync();

        Assert.Equal(1, Volatile.Read(ref writeEntrances));
        Assert.False(displaySave.IsCompleted);

        releaseFirstWrite.SetResult();
        await Task.WhenAll(adapterSave, displaySave);

        Assert.Equal(2, writeEntrances);
        Assert.False(adapter.IsDirty);
        Assert.False(display.IsDirty);
    }

    private static MainWindowViewModel CreateShell(
        AdapterSession session,
        TimeSpan? operationTimeout = null) =>
        new(
            new StubDiscovery(_ => Task.FromResult<IReadOnlyList<DiscoveredAdapter>>([Discovered])),
            new StubSessionFactory((_, _) => Task.FromResult(session)),
            operationTimeout);

    private static AdapterSession CreateSession(
        IWirelessDisplayAdapterClient client,
        IAdvancedWirelessDisplayAdapterClient advancedClient,
        CapabilityProfile capabilities) =>
        new(
            Discovered,
            new AdapterIdentity("WeightRoom-AD", capabilities.Generation, "Model 1733"),
            capabilities,
            client,
            advancedClient);

    private static CapabilityProfile CoreCapabilities() =>
        new(
            AdapterGeneration.Generation2,
            new HashSet<AdapterOperation>
            {
                AdapterOperation.GetDeviceName,
                AdapterOperation.SetDeviceName,
                AdapterOperation.GetOverscan,
                AdapterOperation.SetOverscan,
                AdapterOperation.GetPasswordProtection,
                AdapterOperation.SetPasswordProtection,
            });

    private static CapabilityProfile CreateCapabilities(bool includeWifi)
    {
        var operations = new HashSet<AdapterOperation>(CoreCapabilities().SupportedOperations)
        {
            AdapterOperation.GetWallpaperInfo,
            AdapterOperation.SetWallpaper,
        };
        if (includeWifi)
        {
            operations.Add(AdapterOperation.GetWiFiSettings);
            operations.Add(AdapterOperation.SetWiFiSettings);
            operations.Add(AdapterOperation.ForgetWiFi);
        }

        return new CapabilityProfile(AdapterGeneration.Generation3, operations);
    }

    private static string ReadSource(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "src", "Mwda.Control", "Mwda.Control.csproj")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(directory!.FullName, relativePath));
    }

    private static string GetAction(HttpRequestMessage request)
    {
        var query = request.RequestUri!.Query.TrimStart('?').Split('&');
        var action = query.Single(part => part.StartsWith("Action=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(action["Action=".Length..]);
    }

    private static HttpResponseMessage JsonResponse(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    private sealed class StubDiscovery(
        Func<CancellationToken, Task<IReadOnlyList<DiscoveredAdapter>>> discover) : IAdapterDiscovery
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<DiscoveredAdapter>> DiscoverAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            return discover(cancellationToken);
        }
    }

    private sealed class StubSessionFactory(
        Func<DiscoveredAdapter, CancellationToken, Task<AdapterSession>> create) : IAdapterSessionFactory
    {
        public Task<AdapterSession> CreateAsync(
            DiscoveredAdapter discoveredAdapter,
            CancellationToken cancellationToken = default) =>
            create(discoveredAdapter, cancellationToken);
    }

    private sealed class RecordingClient : IWirelessDisplayAdapterClient, IDisposable
    {
        public Func<string, CancellationToken, Task>? SetDeviceName { get; init; }

        public List<string> DeviceNameWrites { get; } = [];

        public List<(bool Enabled, string? Password)> PasswordProtectionWrites { get; } = [];

        public int DisposeCount { get; private set; }

        public Task<AdapterIdentity> GetIdentityAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AdapterIdentity("WeightRoom-AD", AdapterGeneration.Generation3));

        public Task<OverscanSettings> GetOverscanAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new OverscanSettings(false, 0));

        public Task SetOverscanAsync(
            OverscanSettings settings,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<PasswordProtectionSettings> GetPasswordProtectionAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PasswordProtectionSettings(false));

        public Task SetPasswordProtectionAsync(
            bool enabled,
            string? password,
            CancellationToken cancellationToken = default)
        {
            PasswordProtectionWrites.Add((enabled, password));
            return Task.CompletedTask;
        }

        public async Task SetDeviceNameAsync(
            string deviceName,
            CancellationToken cancellationToken = default)
        {
            DeviceNameWrites.Add(deviceName);
            if (SetDeviceName is not null)
            {
                await SetDeviceName(deviceName, cancellationToken);
            }
        }

        public Task<CapabilityProfile> DetectCapabilitiesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CoreCapabilities());

        public void Dispose() => DisposeCount++;
    }

    private sealed class RecordingAdvancedClient : IAdvancedWirelessDisplayAdapterClient, IDisposable
    {
        public Func<WifiSettings, CancellationToken, Task>? SetWiFiSettings { get; init; }

        public int DisposeCount { get; private set; }

        public Task<WallpaperInfo> GetWallpaperInfoAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<WallpaperInfo>(new("1", ["1", "2"], true));

        public Task SetPredefinedWallpaperAsync(
            string wallpaperId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UploadCustomWallpaperAsync(
            Stream image,
            string fileName,
            string contentType,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<WifiSettings> GetWiFiSettingsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new WifiSettings("GymNet", true));

        public async Task SetWiFiSettingsAsync(
            WifiSettings settings,
            CancellationToken cancellationToken = default)
        {
            if (SetWiFiSettings is not null)
            {
                await SetWiFiSettings(settings, cancellationToken);
            }
        }

        public Task ForgetWiFiAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<HdcpSettings> GetHdcpStatusAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new HdcpSettings(false));

        public Task SetHdcpStatusAsync(
            bool enabled,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<LanguageInfo> GetLanguageAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<LanguageInfo>(new("en-US", ["en-US"]));

        public Task SetLanguageAsync(
            string languageTag,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RestartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Dispose() => DisposeCount++;
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
