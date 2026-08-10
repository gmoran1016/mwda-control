using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Mwda.Control.Protocol;

namespace Mwda.Control.Discovery;

public sealed class AdapterDiscovery : IAdapterDiscovery, IDisposable
{
    private const string WifiDirectDescription = "Wi-Fi Direct Virtual Adapter";
    private const string DeviceNameProbePath = "/cgi-bin/msupload.sh?Action=GetDeviceName";

    private readonly INetworkCandidateSource _candidateSource;
    private readonly AdapterHttpTransport _transport;
    private readonly DiscoveryOptions _options;
    private readonly bool _ownsTransport;

    public AdapterDiscovery(DiscoveryOptions? options = null)
    {
        _options = options ?? new DiscoveryOptions();
        ValidateOptions(_options);
        _candidateSource = new NetworkInterfaceCandidateSource();
        _transport = new AdapterHttpTransport(_options.ProbeTimeout);
        _ownsTransport = true;
    }

    public AdapterDiscovery(
        INetworkCandidateSource candidateSource,
        AdapterHttpTransport transport,
        DiscoveryOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(candidateSource);
        ArgumentNullException.ThrowIfNull(transport);

        _options = options ?? new DiscoveryOptions();
        ValidateOptions(_options);
        _candidateSource = candidateSource;
        _transport = transport;
    }

    public async Task<IReadOnlyList<DiscoveredAdapter>> DiscoverAsync(
        CancellationToken cancellationToken)
    {
        var interfaces = _candidateSource
            .GetCandidates()
            .Where(IsWifiDirectCandidate)
            .ToArray();
        if (interfaces.Length == 0)
        {
            return [];
        }

        var candidates = BuildProbeCandidates(interfaces);
        var discovered = new ConcurrentBag<DiscoveredAdapter>();
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = _options.MaxConcurrentProbes,
        };

        await Parallel.ForEachAsync(
            candidates,
            parallelOptions,
            async (candidate, token) =>
            {
                var adapter = await ProbeAsync(candidate, token);
                if (adapter is not null)
                {
                    discovered.Add(adapter);
                }
            });

        return discovered
            .OrderByDescending(adapter => adapter.IsLastKnown)
            .ThenBy(adapter => adapter.ResponseTime)
            .ThenBy(adapter => AddressSortKey(adapter.IpAddress))
            .ToArray();
    }

    public void Dispose()
    {
        if (_ownsTransport)
        {
            _transport.Dispose();
        }
    }

    private static bool IsWifiDirectCandidate(NetworkCandidate candidate)
    {
        if (candidate.OperationalStatus != OperationalStatus.Up ||
            candidate.InterfaceAddress.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        return candidate.InterfaceDescription.Contains(
                WifiDirectDescription,
                StringComparison.OrdinalIgnoreCase) ||
            IsPrivateWifiDirectSubnet(candidate.InterfaceAddress);
    }

    private static bool IsPrivateWifiDirectSubnet(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 &&
            bytes[0] == 192 &&
            bytes[1] == 168 &&
            bytes[2] == 137;
    }

    private IReadOnlyList<ProbeCandidate> BuildProbeCandidates(
        IReadOnlyList<NetworkCandidate> interfaces)
    {
        var candidates = new Dictionary<IPAddress, ProbeCandidate>();

        if (_options.LastKnownAddress is { AddressFamily: AddressFamily.InterNetwork } lastKnown)
        {
            var matchingInterface = interfaces.FirstOrDefault(
                    candidate => IsSame24(candidate.InterfaceAddress, lastKnown)) ??
                interfaces[0];
            AddCandidate(candidates, lastKnown, matchingInterface, isLastKnown: true);
        }

        foreach (var networkInterface in interfaces)
        {
            foreach (var neighbor in networkInterface.NeighborAddresses)
            {
                if (neighbor.AddressFamily == AddressFamily.InterNetwork)
                {
                    AddCandidate(candidates, neighbor, networkInterface, isLastKnown: false);
                }
            }

            var network = networkInterface.InterfaceAddress.GetAddressBytes();
            for (var host = 2; host <= 254; host++)
            {
                var address = new IPAddress([network[0], network[1], network[2], (byte)host]);
                AddCandidate(candidates, address, networkInterface, isLastKnown: false);
            }
        }

        return candidates.Values
            .OrderByDescending(candidate => candidate.IsLastKnown)
            .ThenBy(candidate => AddressSortKey(candidate.Address))
            .ToArray();
    }

    private static void AddCandidate(
        IDictionary<IPAddress, ProbeCandidate> candidates,
        IPAddress address,
        NetworkCandidate networkInterface,
        bool isLastKnown)
    {
        if (candidates.TryGetValue(address, out var existing))
        {
            if (isLastKnown && !existing.IsLastKnown)
            {
                candidates[address] = existing with { IsLastKnown = true };
            }

            return;
        }

        candidates.Add(
            address,
            new ProbeCandidate(address, networkInterface.InterfaceAlias, isLastKnown));
    }

    private async Task<DiscoveredAdapter?> ProbeAsync(
        ProbeCandidate candidate,
        CancellationToken cancellationToken)
    {
        using var probeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        probeTimeout.CancelAfter(_options.ProbeTimeout);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var baseAddress = new UriBuilder(Uri.UriSchemeHttp, candidate.Address.ToString()).Uri;
            var response = await _transport.GetAsync(
                new Uri(baseAddress, DeviceNameProbePath),
                probeTimeout.Token);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return null;
            }

            var identity = ProtocolJson.ParseIdentity(response.Body);
            return new DiscoveredAdapter(
                candidate.Address,
                candidate.InterfaceAlias,
                identity.DeviceName,
                stopwatch.Elapsed,
                candidate.IsLastKnown);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (AdapterProtocolException)
        {
            return null;
        }
    }

    private static bool IsSame24(IPAddress left, IPAddress right)
    {
        var leftBytes = left.GetAddressBytes();
        var rightBytes = right.GetAddressBytes();
        return leftBytes.Length == 4 &&
            rightBytes.Length == 4 &&
            leftBytes[0] == rightBytes[0] &&
            leftBytes[1] == rightBytes[1] &&
            leftBytes[2] == rightBytes[2];
    }

    private static uint AddressSortKey(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return ((uint)bytes[0] << 24) |
            ((uint)bytes[1] << 16) |
            ((uint)bytes[2] << 8) |
            bytes[3];
    }

    private static void ValidateOptions(DiscoveryOptions options)
    {
        if (options.MaxConcurrentProbes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.MaxConcurrentProbes,
                "MaxConcurrentProbes must be positive.");
        }

        if (options.ProbeTimeout <= TimeSpan.Zero ||
            options.ProbeTimeout == Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.ProbeTimeout,
                "ProbeTimeout must be positive and finite.");
        }
    }

    private sealed record ProbeCandidate(
        IPAddress Address,
        string InterfaceAlias,
        bool IsLastKnown);
}
