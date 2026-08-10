using System.Collections.ObjectModel;
using System.Net;
using Mwda.Control.Protocol;

namespace Mwda.Control.IntegrationTests;

public sealed class LiveAdapterFixture : IDisposable
{
    private const string RunLiveTestsVariable = "MWDA_RUN_LIVE_TESTS";
    private const string AdapterIpVariable = "MWDA_ADAPTER_IP";

    private readonly WriteEncodingCharacterization _writeEncodingCharacterization = new();

    private LiveAdapterFixture(AdapterEndpoint endpoint)
    {
        Endpoint = endpoint;
        Client = new AdapterClient(endpoint);
    }

    public AdapterEndpoint Endpoint { get; }

    public AdapterClient Client { get; }

    public IReadOnlyDictionary<AdapterOperation, ProtocolWriteEncoding> AcceptedWriteEncodings =>
        _writeEncodingCharacterization.AcceptedEncodings;

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

    public Task<ProtocolWriteEncoding> CharacterizeWriteEncodingAsync(
        AdapterOperation operation,
        Func<ProtocolWriteEncoding, Task<bool>> tryCandidate,
        Func<ProtocolWriteEncoding, Task> restore) =>
        _writeEncodingCharacterization.SelectAndRecordAsync(operation, tryCandidate, restore);

    public AdapterClient CreateCandidateClient(
        AdapterOperation operation,
        ProtocolWriteEncoding encoding)
    {
        var encodings = new Dictionary<AdapterOperation, ProtocolWriteEncoding>(AcceptedWriteEncodings)
        {
            [operation] = encoding,
        };
        return new AdapterClient(Endpoint, TimeSpan.FromSeconds(3), encodings);
    }

    public AdapterClient CreateRestorationClient(
        AdapterOperation operation,
        ProtocolWriteEncoding fallbackEncoding)
    {
        if (AcceptedWriteEncodings.ContainsKey(operation))
        {
            return new AdapterClient(Endpoint, TimeSpan.FromSeconds(3), AcceptedWriteEncodings);
        }

        return CreateCandidateClient(operation, fallbackEncoding);
    }

    public void Dispose() => Client.Dispose();
}

internal sealed class WriteEncodingCharacterization
{
    private readonly Dictionary<AdapterOperation, ProtocolWriteEncoding> _acceptedEncodings = new();
    private readonly IReadOnlyDictionary<AdapterOperation, ProtocolWriteEncoding> _acceptedEncodingsView;

    public WriteEncodingCharacterization()
    {
        _acceptedEncodingsView = new ReadOnlyDictionary<AdapterOperation, ProtocolWriteEncoding>(
            _acceptedEncodings);
    }

    public IReadOnlyDictionary<AdapterOperation, ProtocolWriteEncoding> AcceptedEncodings =>
        _acceptedEncodingsView;

    public async Task<ProtocolWriteEncoding> SelectAndRecordAsync(
        AdapterOperation operation,
        Func<ProtocolWriteEncoding, Task<bool>> tryCandidate,
        Func<ProtocolWriteEncoding, Task> restore)
    {
        ArgumentNullException.ThrowIfNull(tryCandidate);
        ArgumentNullException.ThrowIfNull(restore);
        _ = ProtocolRequestCatalog.GetReadBackOperation(operation);

        if (_acceptedEncodings.TryGetValue(operation, out var recorded))
        {
            return recorded;
        }

        foreach (var candidate in ProtocolRequestCatalog.WriteEncodingCandidates)
        {
            var accepted = false;
            try
            {
                accepted = await tryCandidate(candidate);
                if (accepted)
                {
                    _acceptedEncodings.Add(operation, candidate);
                }
            }
            finally
            {
                var restorationEncoding = _acceptedEncodings.TryGetValue(operation, out var acceptedEncoding)
                    ? acceptedEncoding
                    : candidate;
                await restore(restorationEncoding);
            }

            if (accepted)
            {
                return candidate;
            }
        }

        throw new AdapterProtocolException(
            $"No closed write-encoding candidate was accepted for {operation} by exact read-back.");
    }
}

public sealed class LiveAdapterFactAttribute : FactAttribute
{
    public LiveAdapterFactAttribute()
    {
        Skip = LiveAdapterFixture.GetSkipReason();
    }
}
