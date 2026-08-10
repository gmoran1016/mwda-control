namespace Mwda.Control.Discovery;

public interface IAdapterDiscovery
{
    Task<IReadOnlyList<DiscoveredAdapter>> DiscoverAsync(CancellationToken cancellationToken);
}
