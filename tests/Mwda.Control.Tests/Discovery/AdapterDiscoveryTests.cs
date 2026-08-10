using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using Mwda.Control.Discovery;
using Mwda.Control.Protocol;

namespace Mwda.Control.Tests.Discovery;

public sealed class AdapterDiscoveryTests
{
    private static readonly IPAddress LocalAddress = IPAddress.Parse("192.168.137.1");

    [Fact]
    public async Task NonWifiDirectInterfaceIsRejectedWithoutProbing()
    {
        var requests = 0;
        using var handler = new DelegateHttpMessageHandler(_ =>
        {
            Interlocked.Increment(ref requests);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        using var transport = new AdapterHttpTransport(handler, TimeSpan.FromSeconds(2));
        var source = new StubNetworkCandidateSource(
            new NetworkCandidate(
                "Ethernet",
                "PCIe Ethernet Controller",
                OperationalStatus.Up,
                IPAddress.Parse("10.0.0.10"),
                [IPAddress.Parse("10.0.0.20")]));
        var discovery = new AdapterDiscovery(source, transport);

        var adapters = await discovery.DiscoverAsync(CancellationToken.None);

        Assert.Empty(adapters);
        Assert.Equal(0, requests);
    }

    [Fact]
    public async Task ProbeUsesVerifiedDeviceNamePathAndReturnsValidAdapter()
    {
        var requestedUris = new ConcurrentBag<Uri>();
        using var handler = new DelegateHttpMessageHandler(request =>
        {
            requestedUris.Add(request.RequestUri!);
            return Task.FromResult(ResponseFor(request.RequestUri!.Host));
        });
        using var transport = new AdapterHttpTransport(handler, TimeSpan.FromSeconds(2));
        var discovery = new AdapterDiscovery(
            WifiDirectSource(
                IPAddress.Parse("192.168.137.247"),
                IPAddress.Parse("192.168.137.248"),
                IPAddress.Parse("192.168.137.249")),
            transport);

        var adapters = await discovery.DiscoverAsync(CancellationToken.None);

        var adapter = Assert.Single(adapters);
        Assert.Equal(IPAddress.Parse("192.168.137.247"), adapter.IpAddress);
        Assert.Equal("Local Area Connection* 10", adapter.InterfaceAlias);
        Assert.Equal("WeightRoom-AD", adapter.DeviceName);
        Assert.Equal(new Uri("http://192.168.137.247/"), adapter.Endpoint.BaseAddress);
        Assert.All(
            requestedUris,
            uri =>
            {
                Assert.Equal("/cgi-bin/msupload.sh", uri.AbsolutePath);
                Assert.Equal("?Action=GetDeviceName", uri.Query);
            });
        Assert.Equal(253, requestedUris.Count);
    }

    [Fact]
    public async Task KnownWifiDirectSubnetQualifiesWithoutMatchingDescription()
    {
        using var handler = new DelegateHttpMessageHandler(request =>
            Task.FromResult(ResponseFor(request.RequestUri!.Host)));
        using var transport = new AdapterHttpTransport(handler, TimeSpan.FromSeconds(2));
        var source = new StubNetworkCandidateSource(
            new NetworkCandidate(
                "Local Area Connection* 10",
                "Generic Virtual Network Adapter",
                OperationalStatus.Up,
                LocalAddress));
        var discovery = new AdapterDiscovery(source, transport);

        var adapters = await discovery.DiscoverAsync(CancellationToken.None);

        Assert.Equal(IPAddress.Parse("192.168.137.247"), Assert.Single(adapters).IpAddress);
    }

    [Fact]
    public async Task ObservedIpv4NeighborOutsideSweptSubnetIsProbed()
    {
        var neighbor = IPAddress.Parse("192.168.138.247");
        using var handler = new DelegateHttpMessageHandler(request =>
            Task.FromResult(request.RequestUri!.Host == neighbor.ToString()
                ? ValidIdentityResponse("Neighbor-AD")
                : new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var transport = new AdapterHttpTransport(handler, TimeSpan.FromSeconds(2));
        var discovery = new AdapterDiscovery(WifiDirectSource(neighbor), transport);

        var adapters = await discovery.DiscoverAsync(CancellationToken.None);

        Assert.Equal(neighbor, Assert.Single(adapters).IpAddress);
    }

    [Fact]
    public async Task ResultsOrderLastKnownThenResponseTimeThenIpAddress()
    {
        using var handler = new DelegateHttpMessageHandler(async (request, cancellationToken) =>
        {
            var delay = request.RequestUri!.Host switch
            {
                "192.168.137.247" => 20,
                "192.168.137.248" => 200,
                "192.168.137.249" => 500,
                _ => 0,
            };

            if (delay == 0)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            await Task.Delay(delay, cancellationToken);
            return ValidIdentityResponse($"Adapter-{request.RequestUri.Host.Split('.')[3]}");
        });
        using var transport = new AdapterHttpTransport(handler, TimeSpan.FromSeconds(2));
        var options = new DiscoveryOptions
        {
            LastKnownAddress = IPAddress.Parse("192.168.137.249"),
            MaxConcurrentProbes = 253,
            ProbeTimeout = TimeSpan.FromSeconds(1),
        };
        var discovery = new AdapterDiscovery(WifiDirectSource(), transport, options);

        var adapters = await discovery.DiscoverAsync(CancellationToken.None);

        Assert.Equal(
            ["192.168.137.249", "192.168.137.247", "192.168.137.248"],
            adapters.Select(adapter => adapter.IpAddress.ToString()));
        Assert.True(adapters[0].IsLastKnown);
        Assert.False(adapters[1].IsLastKnown);
    }

    [Fact]
    public async Task ConcurrentProbesDoNotExceedConfiguredMaximum()
    {
        var current = 0;
        var maximum = 0;
        using var handler = new DelegateHttpMessageHandler(async (_, cancellationToken) =>
        {
            var observed = Interlocked.Increment(ref current);
            UpdateMaximum(ref maximum, observed);
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
            finally
            {
                Interlocked.Decrement(ref current);
            }
        });
        using var transport = new AdapterHttpTransport(handler, TimeSpan.FromSeconds(2));
        var options = new DiscoveryOptions
        {
            MaxConcurrentProbes = 3,
            ProbeTimeout = TimeSpan.FromSeconds(1),
        };
        var discovery = new AdapterDiscovery(WifiDirectSource(), transport, options);

        await discovery.DiscoverAsync(CancellationToken.None);

        Assert.Equal(options.MaxConcurrentProbes, maximum);
    }

    private static StubNetworkCandidateSource WifiDirectSource(params IPAddress[] neighbors) =>
        new(
            new NetworkCandidate(
                "Local Area Connection* 10",
                "Microsoft Wi-Fi Direct Virtual Adapter #2",
                OperationalStatus.Up,
                LocalAddress,
                neighbors));

    private static HttpResponseMessage ResponseFor(string host) => host switch
    {
        "192.168.137.247" => ValidIdentityResponse("WeightRoom-AD"),
        "192.168.137.248" => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not json", Encoding.UTF8, "text/html"),
        },
        "192.168.137.249" => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        },
        _ => new HttpResponseMessage(HttpStatusCode.NotFound),
    };

    private static HttpResponseMessage ValidIdentityResponse(string deviceName) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"DeviceName":"{{deviceName}}"}""",
                Encoding.UTF8,
                "text/html"),
        };

    private static void UpdateMaximum(ref int maximum, int observed)
    {
        var currentMaximum = Volatile.Read(ref maximum);
        while (observed > currentMaximum)
        {
            var prior = Interlocked.CompareExchange(ref maximum, observed, currentMaximum);
            if (prior == currentMaximum)
            {
                return;
            }

            currentMaximum = prior;
        }
    }

    private sealed class StubNetworkCandidateSource : INetworkCandidateSource
    {
        private readonly IReadOnlyList<NetworkCandidate> _candidates;

        public StubNetworkCandidateSource(params NetworkCandidate[] candidates)
        {
            _candidates = candidates;
        }

        public IReadOnlyList<NetworkCandidate> GetCandidates() => _candidates;
    }

    private sealed class DelegateHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;

        public DelegateHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send)
            : this((request, _) => send(request))
        {
        }

        public DelegateHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        {
            _send = send;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            _send(request, cancellationToken);
    }
}
