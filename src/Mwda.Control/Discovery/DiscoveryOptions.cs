using System.Net;

namespace Mwda.Control.Discovery;

public sealed record DiscoveryOptions
{
    public const int DefaultMaxConcurrentProbes = 24;

    public static readonly TimeSpan DefaultProbeTimeout = TimeSpan.FromMilliseconds(750);

    public IPAddress? LastKnownAddress { get; init; }

    public int MaxConcurrentProbes { get; init; } = DefaultMaxConcurrentProbes;

    public TimeSpan ProbeTimeout { get; init; } = DefaultProbeTimeout;
}
