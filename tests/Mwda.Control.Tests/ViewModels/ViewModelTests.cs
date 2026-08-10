using System.Net;
using System.Net.Http;
using System.Text;
using Mwda.Control.Discovery;
using Mwda.Control.Protocol;
using Mwda.Control.Session;
using Mwda.Control.ViewModels;

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
        Assert.False(shell.Network.IsVisible);
        Assert.True(shell.Adapter.IsAvailable);
        Assert.True(shell.Display.IsAvailable);
        Assert.True(shell.ConnectionSettings.IsAvailable);
        Assert.True(shell.Diagnostics.IsAvailable);
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
    public async Task TimedOutSavePublishesDisconnectedStateAndPreservesTheEdit()
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

        Assert.False(shell.Connection.IsConnected);
        Assert.True(shell.Adapter.IsDirty);
        Assert.Equal("Room_2+(West)", shell.Adapter.DeviceName);
        Assert.Contains("not reachable", shell.Adapter.ResultBanner, StringComparison.OrdinalIgnoreCase);
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

    private sealed class RecordingClient : IWirelessDisplayAdapterClient
    {
        public Func<string, CancellationToken, Task>? SetDeviceName { get; init; }

        public List<string> DeviceNameWrites { get; } = [];

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
            CancellationToken cancellationToken = default) => Task.CompletedTask;

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
    }

    private sealed class RecordingAdvancedClient : IAdvancedWirelessDisplayAdapterClient
    {
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

        public Task SetWiFiSettingsAsync(
            WifiSettings settings,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

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
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
