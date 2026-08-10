using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Mwda.Control.Discovery;

public interface INetworkCandidateSource
{
    IReadOnlyList<NetworkCandidate> GetCandidates();
}

public sealed record NetworkCandidate
{
    public NetworkCandidate(
        string interfaceAlias,
        string interfaceDescription,
        OperationalStatus operationalStatus,
        IPAddress interfaceAddress,
        IEnumerable<IPAddress>? neighborAddresses = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(interfaceAlias);
        ArgumentNullException.ThrowIfNull(interfaceDescription);
        ArgumentNullException.ThrowIfNull(interfaceAddress);

        InterfaceAlias = interfaceAlias;
        InterfaceDescription = interfaceDescription;
        OperationalStatus = operationalStatus;
        InterfaceAddress = interfaceAddress;
        NeighborAddresses = neighborAddresses?.ToArray() ?? [];
    }

    public string InterfaceAlias { get; }

    public string InterfaceDescription { get; }

    public OperationalStatus OperationalStatus { get; }

    public IPAddress InterfaceAddress { get; }

    public IReadOnlyList<IPAddress> NeighborAddresses { get; }
}

public sealed class NetworkInterfaceCandidateSource : INetworkCandidateSource
{
    public IReadOnlyList<NetworkCandidate> GetCandidates()
    {
        var candidates = new List<NetworkCandidate>();

        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            try
            {
                var properties = networkInterface.GetIPProperties();
                foreach (var address in properties.UnicastAddresses)
                {
                    if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    candidates.Add(new NetworkCandidate(
                        networkInterface.Name,
                        networkInterface.Description,
                        networkInterface.OperationalStatus,
                        address.Address));
                }
            }
            catch (NetworkInformationException)
            {
                // An interface can disappear while Windows is enumerating it.
            }
        }

        return candidates;
    }
}
