using Mwda.Control.Discovery;
using Mwda.Control.Protocol;

namespace Mwda.Control.Session;

public sealed class AdapterSession : IDisposable
{
    public AdapterSession(
        DiscoveredAdapter discoveredAdapter,
        AdapterIdentity identity,
        CapabilityProfile capabilities,
        IWirelessDisplayAdapterClient client,
        IAdvancedWirelessDisplayAdapterClient advancedClient)
    {
        ArgumentNullException.ThrowIfNull(discoveredAdapter);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(advancedClient);

        DiscoveredAdapter = discoveredAdapter;
        AdapterIdentity = identity;
        CapabilityProfile = capabilities;
        Client = client;
        AdvancedClient = advancedClient;
    }

    public DiscoveredAdapter DiscoveredAdapter { get; }

    public AdapterIdentity AdapterIdentity { get; }

    public CapabilityProfile CapabilityProfile { get; }

    public IWirelessDisplayAdapterClient Client { get; }

    public IAdvancedWirelessDisplayAdapterClient AdvancedClient { get; }

    public void Dispose()
    {
        if (Client is IDisposable clientDisposable)
        {
            clientDisposable.Dispose();
        }

        if (!ReferenceEquals(Client, AdvancedClient) && AdvancedClient is IDisposable advancedDisposable)
        {
            advancedDisposable.Dispose();
        }
    }
}
