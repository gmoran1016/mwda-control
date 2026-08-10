using Mwda.Control.Discovery;
using Mwda.Control.Protocol;

namespace Mwda.Control.Session;

public sealed class AdapterSessionFactory : IAdapterSessionFactory
{
    private readonly Func<AdapterEndpoint, IWirelessDisplayAdapterClient> _createClient;
    private readonly Func<AdapterEndpoint, IAdvancedWirelessDisplayAdapterClient> _createAdvancedClient;

    public AdapterSessionFactory()
        : this(
            endpoint => new AdapterClient(endpoint),
            endpoint => new AdvancedAdapterClient(endpoint))
    {
    }

    public AdapterSessionFactory(
        Func<AdapterEndpoint, IWirelessDisplayAdapterClient> createClient,
        Func<AdapterEndpoint, IAdvancedWirelessDisplayAdapterClient> createAdvancedClient)
    {
        ArgumentNullException.ThrowIfNull(createClient);
        ArgumentNullException.ThrowIfNull(createAdvancedClient);
        _createClient = createClient;
        _createAdvancedClient = createAdvancedClient;
    }

    public async Task<AdapterSession> CreateAsync(
        DiscoveredAdapter discoveredAdapter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(discoveredAdapter);

        var client = _createClient(discoveredAdapter.Endpoint);
        var advancedClient = _createAdvancedClient(discoveredAdapter.Endpoint);
        try
        {
            var identity = await client.GetIdentityAsync(cancellationToken);
            var capabilities = await CapabilityDetector.DetectAsync(
                client,
                advancedClient,
                cancellationToken);
            return new AdapterSession(
                discoveredAdapter,
                identity,
                capabilities,
                client,
                advancedClient);
        }
        catch
        {
            if (client is IDisposable clientDisposable)
            {
                clientDisposable.Dispose();
            }

            if (!ReferenceEquals(client, advancedClient) && advancedClient is IDisposable advancedDisposable)
            {
                advancedDisposable.Dispose();
            }

            throw;
        }
    }
}
