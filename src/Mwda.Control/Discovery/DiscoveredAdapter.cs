using System.Net;
using Mwda.Control.Protocol;

namespace Mwda.Control.Discovery;

public sealed record DiscoveredAdapter(
    IPAddress IpAddress,
    string InterfaceAlias,
    string DeviceName,
    TimeSpan ResponseTime,
    bool IsLastKnown)
{
    public AdapterEndpoint Endpoint =>
        new(new UriBuilder(Uri.UriSchemeHttp, IpAddress.ToString()).Uri);
}
