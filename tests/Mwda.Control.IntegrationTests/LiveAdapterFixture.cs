using System.Net;
using Mwda.Control.Protocol;

namespace Mwda.Control.IntegrationTests;

public sealed class LiveAdapterFixture : IDisposable
{
    private const string RunLiveTestsVariable = "MWDA_RUN_LIVE_TESTS";
    private const string AdapterIpVariable = "MWDA_ADAPTER_IP";

    private LiveAdapterFixture(AdapterEndpoint endpoint)
    {
        Endpoint = endpoint;
        Client = new AdapterClient(endpoint);
    }

    public AdapterEndpoint Endpoint { get; }

    public AdapterClient Client { get; }

    public static string? GetSkipReason()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable(RunLiveTestsVariable),
                "1",
                StringComparison.Ordinal))
        {
            return $"Set {RunLiveTestsVariable}=1 to opt in to adapter mutation tests.";
        }

        var addressValue = Environment.GetEnvironmentVariable(AdapterIpVariable);
        if (!IPAddress.TryParse(addressValue, out _))
        {
            return $"Set {AdapterIpVariable} to a valid adapter IP address.";
        }

        return null;
    }

    public static bool TryCreate(out LiveAdapterFixture? fixture)
    {
        fixture = null;
        if (GetSkipReason() is not null)
        {
            return false;
        }

        var address = IPAddress.Parse(Environment.GetEnvironmentVariable(AdapterIpVariable)!);
        var endpoint = new AdapterEndpoint(
            new UriBuilder(Uri.UriSchemeHttp, address.ToString()).Uri);
        fixture = new LiveAdapterFixture(endpoint);
        return true;
    }

    public void Dispose() => Client.Dispose();
}

public sealed class LiveAdapterFactAttribute : FactAttribute
{
    public LiveAdapterFactAttribute()
    {
        Skip = LiveAdapterFixture.GetSkipReason();
    }
}
