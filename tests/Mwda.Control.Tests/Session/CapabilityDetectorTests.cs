using System.Net;
using System.Text;
using Mwda.Control.Discovery;
using Mwda.Control.Protocol;
using Mwda.Control.Session;

namespace Mwda.Control.Tests.Session;

public sealed class CapabilityDetectorTests
{
    private static readonly AdapterEndpoint Endpoint =
        new(new Uri("http://192.168.137.247/"));

    [Fact]
    public async Task CurrentAdapterCoreOperationsRemainSupportedWhenWifiProbeReturnsNotFound()
    {
        using var handler = new StubHttpMessageHandler(request =>
        {
            var action = GetAction(request);
            return action switch
            {
                "GetWallpaperID" => TextResponse(HttpStatusCode.NotFound, "missing"),
                "GetWallpaperId" => TextResponse(HttpStatusCode.NotFound, "missing"),
                "GetWiFiSetting" => TextResponse(HttpStatusCode.NotFound, "missing"),
                "GetHdcpStatus" => TextResponse(HttpStatusCode.NotImplemented, "missing"),
                "GetLanguage" => JsonResponse("""{"CurrentLanguage":42}"""),
                _ => throw new InvalidOperationException($"Unexpected action: {action}"),
            };
        });
        using var advanced = new AdvancedAdapterClient(Endpoint, handler, TimeSpan.FromSeconds(2));
        var basic = new SuccessfulBasicClient(AdapterGeneration.Generation2);

        var profile = await CapabilityDetector.DetectAsync(basic, advanced, CancellationToken.None);

        Assert.Equal(AdapterGeneration.Generation2, profile.Generation);
        Assert.True(profile.Supports(AdapterOperation.GetDeviceName));
        Assert.True(profile.Supports(AdapterOperation.SetDeviceName));
        Assert.True(profile.Supports(AdapterOperation.GetOverscan));
        Assert.True(profile.Supports(AdapterOperation.SetOverscan));
        Assert.True(profile.Supports(AdapterOperation.GetPasswordProtection));
        Assert.True(profile.Supports(AdapterOperation.SetPasswordProtection));
        Assert.False(profile.Supports(AdapterOperation.GetWallpaperInfo));
        Assert.False(profile.Supports(AdapterOperation.SetWallpaper));
        Assert.False(profile.Supports(AdapterOperation.GetWiFiSettings));
        Assert.False(profile.Supports(AdapterOperation.SetWiFiSettings));
        Assert.False(profile.Supports(AdapterOperation.ForgetWiFi));
        Assert.False(profile.Supports(AdapterOperation.GetHdcpStatus));
        Assert.False(profile.Supports(AdapterOperation.GetLanguage));
        Assert.False(profile.Supports(AdapterOperation.Restart));
    }

    [Fact]
    public async Task LegacyFourSquareAdapterIsReportedAsGeneration2WithWallpaperSupport()
    {
        using var handler = new StubHttpMessageHandler(request =>
        {
            var action = GetAction(request);
            return action switch
            {
                "GetWallpaperID" => JsonResponse("{\"WallpaperID\":0}"),
                "GetWallpaperId" => TextResponse(HttpStatusCode.NotFound, "missing"),
                "GetWiFiSetting" => TextResponse(HttpStatusCode.NotFound, "missing"),
                "GetHdcpStatus" => TextResponse(HttpStatusCode.NotFound, "missing"),
                "GetLanguage" => TextResponse(HttpStatusCode.NotFound, "missing"),
                _ => throw new InvalidOperationException($"Unexpected action: {action}"),
            };
        });
        var basic = new SuccessfulBasicClient(AdapterGeneration.Unknown);
        using var advanced = new AdvancedAdapterClient(Endpoint, handler, TimeSpan.FromSeconds(2));
        var factory = new AdapterSessionFactory(_ => basic, _ => advanced);
        var discovered = new DiscoveredAdapter(
            IPAddress.Parse("192.168.137.247"),
            "Wi-Fi Direct",
            "Griffin-Home",
            TimeSpan.FromMilliseconds(12),
            false);

        using var session = await factory.CreateAsync(discovered, CancellationToken.None);

        Assert.Equal(AdapterGeneration.Generation2, session.CapabilityProfile.Generation);
        Assert.Equal(
            "Microsoft Wireless Display Adapter (with Microsoft 4 Square logo)",
            session.AdapterIdentity.Model);
        Assert.True(session.CapabilityProfile.Supports(AdapterOperation.GetWallpaperInfo));
        Assert.True(session.CapabilityProfile.Supports(AdapterOperation.SetWallpaper));
    }

    [Fact]
    public async Task ValidOptionalReadSchemasEnableTheirReadAndWriteFamilies()
    {
        using var handler = new StubHttpMessageHandler(request =>
        {
            var action = GetAction(request);
            return action switch
            {
                "GetWallpaperID" => TextResponse(HttpStatusCode.NotFound, "missing"),
                "GetWallpaperId" => JsonResponse(
                    """{"WallpaperID":"4","AvailableWallpaperIDs":["1","2","3","4"],"SupportsCustomWallpaper":true}"""),
                "GetWiFiSetting" => JsonResponse(
                    """{"WiFiSsid":"GymNet","ConnectionStatus":"Connected"}"""),
                "GetHdcpStatus" => JsonResponse("""{"HdcpStatus":true}"""),
                "GetLanguage" => JsonResponse(
                    """{"CurrentLanguage":"en-US","LanguageCode":["en-US","fr-FR"]}"""),
                _ => throw new InvalidOperationException($"Unexpected action: {action}"),
            };
        });
        using var advanced = new AdvancedAdapterClient(Endpoint, handler, TimeSpan.FromSeconds(2));

        var profile = await CapabilityDetector.DetectAsync(
            new SuccessfulBasicClient(AdapterGeneration.Generation3),
            advanced,
            CancellationToken.None);

        Assert.True(profile.Supports(AdapterOperation.GetWallpaperInfo));
        Assert.True(profile.Supports(AdapterOperation.SetWallpaper));
        Assert.True(profile.Supports(AdapterOperation.GetWiFiSettings));
        Assert.True(profile.Supports(AdapterOperation.SetWiFiSettings));
        Assert.True(profile.Supports(AdapterOperation.ForgetWiFi));
        Assert.True(profile.Supports(AdapterOperation.GetHdcpStatus));
        Assert.True(profile.Supports(AdapterOperation.SetHdcpStatus));
        Assert.True(profile.Supports(AdapterOperation.GetLanguage));
        Assert.True(profile.Supports(AdapterOperation.SetLanguage));
        Assert.False(profile.Supports(AdapterOperation.Restart));
    }

    [Fact]
    public async Task SessionFactoryReturnsTypedSessionFromInjectedClients()
    {
        using var handler = new StubHttpMessageHandler(request =>
        {
            var action = GetAction(request);
            return action switch
            {
                "GetWallpaperID" => TextResponse(HttpStatusCode.NotFound, "missing"),
                "GetWallpaperId" => TextResponse(HttpStatusCode.NotFound, "missing"),
                "GetWiFiSetting" => TextResponse(HttpStatusCode.NotFound, "missing"),
                "GetHdcpStatus" => TextResponse(HttpStatusCode.NotFound, "missing"),
                "GetLanguage" => TextResponse(HttpStatusCode.NotFound, "missing"),
                _ => throw new InvalidOperationException($"Unexpected action: {action}"),
            };
        });
        var basic = new SuccessfulBasicClient(AdapterGeneration.FourK);
        var advanced = new AdvancedAdapterClient(Endpoint, handler, TimeSpan.FromSeconds(2));
        var factory = new AdapterSessionFactory(_ => basic, _ => advanced);
        var discovered = new DiscoveredAdapter(
            IPAddress.Parse("192.168.137.247"),
            "Wi-Fi Direct",
            "WeightRoom-AD",
            TimeSpan.FromMilliseconds(12),
            false);

        using var session = await factory.CreateAsync(discovered, CancellationToken.None);

        Assert.Same(discovered, session.DiscoveredAdapter);
        Assert.Equal("WeightRoom-AD", session.AdapterIdentity.DeviceName);
        Assert.Equal(AdapterGeneration.FourK, session.CapabilityProfile.Generation);
        Assert.Same(basic, session.Client);
        Assert.Same(advanced, session.AdvancedClient);
    }

    [Fact]
    public async Task SessionFactoryDisposesBasicClientWhenAdvancedClientConstructionFails()
    {
        var basic = new DisposeTrackingBasicClient(AdapterGeneration.Generation3);
        var constructionFailure = new InvalidOperationException("Advanced client construction failed.");
        var factory = new AdapterSessionFactory(
            _ => basic,
            _ => throw constructionFailure);
        var discovered = new DiscoveredAdapter(
            IPAddress.Parse("192.168.137.247"),
            "Wi-Fi Direct",
            "WeightRoom-AD",
            TimeSpan.FromMilliseconds(12),
            false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => factory.CreateAsync(discovered, CancellationToken.None));

        Assert.Same(constructionFailure, exception);
        Assert.True(basic.IsDisposed);
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

    private static HttpResponseMessage TextResponse(HttpStatusCode status, string body) =>
        new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain"),
        };

    private sealed class SuccessfulBasicClient(AdapterGeneration generation) : IWirelessDisplayAdapterClient
    {
        public Task<AdapterIdentity> GetIdentityAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AdapterIdentity("WeightRoom-AD", generation));

        public Task<OverscanSettings> GetOverscanAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new OverscanSettings(false, 0));

        public Task<PasswordProtectionSettings> GetPasswordProtectionAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PasswordProtectionSettings(false));

        public Task SetOverscanAsync(
            OverscanSettings settings,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task SetPasswordProtectionAsync(
            bool enabled,
            string? password,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task SetDeviceNameAsync(
            string deviceName,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<CapabilityProfile> DetectCapabilitiesAsync(
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class DisposeTrackingBasicClient(
        AdapterGeneration generation) : IWirelessDisplayAdapterClient, IDisposable
    {
        private readonly SuccessfulBasicClient _inner = new(generation);

        public bool IsDisposed { get; private set; }

        public Task<AdapterIdentity> GetIdentityAsync(CancellationToken cancellationToken = default) =>
            _inner.GetIdentityAsync(cancellationToken);

        public Task<OverscanSettings> GetOverscanAsync(CancellationToken cancellationToken = default) =>
            _inner.GetOverscanAsync(cancellationToken);

        public Task<PasswordProtectionSettings> GetPasswordProtectionAsync(
            CancellationToken cancellationToken = default) =>
            _inner.GetPasswordProtectionAsync(cancellationToken);

        public Task SetOverscanAsync(
            OverscanSettings settings,
            CancellationToken cancellationToken = default) =>
            _inner.SetOverscanAsync(settings, cancellationToken);

        public Task SetPasswordProtectionAsync(
            bool enabled,
            string? password,
            CancellationToken cancellationToken = default) =>
            _inner.SetPasswordProtectionAsync(enabled, password, cancellationToken);

        public Task SetDeviceNameAsync(
            string deviceName,
            CancellationToken cancellationToken = default) =>
            _inner.SetDeviceNameAsync(deviceName, cancellationToken);

        public Task<CapabilityProfile> DetectCapabilitiesAsync(
            CancellationToken cancellationToken = default) =>
            _inner.DetectCapabilitiesAsync(cancellationToken);

        public void Dispose() => IsDisposed = true;
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(send(request));
    }
}
