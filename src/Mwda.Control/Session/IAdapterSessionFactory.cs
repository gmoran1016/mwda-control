using Mwda.Control.Discovery;

namespace Mwda.Control.Session;

public interface IAdapterSessionFactory
{
    Task<AdapterSession> CreateAsync(
        DiscoveredAdapter discoveredAdapter,
        CancellationToken cancellationToken = default);
}
