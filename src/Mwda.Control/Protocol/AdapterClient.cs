using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace Mwda.Control.Protocol;

public sealed class AdapterClient : IWirelessDisplayAdapterClient, IDisposable
{
    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(3);

    private readonly AdapterEndpoint _endpoint;
    private readonly AdapterHttpTransport _transport;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly IReadOnlyDictionary<AdapterOperation, ProtocolWriteEncoding> _writeEncodings;

    public AdapterClient(AdapterEndpoint endpoint)
        : this(endpoint, DefaultRequestTimeout)
    {
    }

    public AdapterClient(
        AdapterEndpoint endpoint,
        TimeSpan requestTimeout,
        IReadOnlyDictionary<AdapterOperation, ProtocolWriteEncoding>? writeEncodings = null)
        : this(endpoint, new AdapterHttpTransport(requestTimeout), writeEncodings)
    {
    }

    public AdapterClient(
        AdapterEndpoint endpoint,
        HttpMessageHandler handler,
        TimeSpan requestTimeout,
        IReadOnlyDictionary<AdapterOperation, ProtocolWriteEncoding>? writeEncodings = null)
        : this(endpoint, new AdapterHttpTransport(handler, requestTimeout), writeEncodings)
    {
    }

    private AdapterClient(
        AdapterEndpoint endpoint,
        AdapterHttpTransport transport,
        IReadOnlyDictionary<AdapterOperation, ProtocolWriteEncoding>? writeEncodings)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(transport);

        _endpoint = endpoint;
        _transport = transport;
        _writeEncodings = CopyAndValidateWriteEncodings(writeEncodings);
    }

    public async Task<AdapterIdentity> GetIdentityAsync(CancellationToken cancellationToken = default)
    {
        var result = await ReadIdentityAsync(
            AdapterOperation.GetDeviceName,
            AdapterOperation.GetDeviceName,
            cancellationToken);
        return result.Value;
    }

    public async Task<OverscanSettings> GetOverscanAsync(CancellationToken cancellationToken = default)
    {
        var result = await ReadOverscanAsync(
            AdapterOperation.GetOverscan,
            AdapterOperation.GetOverscan,
            cancellationToken);
        return result.Value;
    }

    public async Task<PasswordProtectionSettings> GetPasswordProtectionAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await ReadPasswordProtectionAsync(
            AdapterOperation.GetPasswordProtection,
            AdapterOperation.GetPasswordProtection,
            cancellationToken);
        return result.Value;
    }

    public async Task SetDeviceNameAsync(
        string deviceName,
        CancellationToken cancellationToken = default)
    {
        if (!AdapterValidation.IsValidDeviceName(deviceName))
        {
            throw new ArgumentException("The device name contains unsupported characters.", nameof(deviceName));
        }

        await ExecuteWriteAsync(
            AdapterOperation.SetDeviceName,
            encoding => ProtocolRequestCatalog.CreateSetDeviceNameRequest(_endpoint, deviceName, encoding),
            async token =>
            {
                var readBack = await ReadIdentityAsync(
                    ProtocolRequestCatalog.GetReadBackOperation(AdapterOperation.SetDeviceName),
                    AdapterOperation.SetDeviceName,
                    token);
                if (!string.Equals(readBack.Value.DeviceName, deviceName, StringComparison.Ordinal))
                {
                    throw ProtocolFailure(
                        AdapterOperation.SetDeviceName,
                        readBack.Response.StatusCode,
                        readBack.Response.Body,
                        "The exact device-name read-back did not match the requested value.");
                }
            },
            cancellationToken);
    }

    public async Task SetOverscanAsync(
        OverscanSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _ = AdapterValidation.CreateOverscan(settings.IsAutoAdjust, settings.Value);

        await ExecuteWriteAsync(
            AdapterOperation.SetOverscan,
            encoding => ProtocolRequestCatalog.CreateSetOverscanRequest(_endpoint, settings, encoding),
            async token =>
            {
                var readBack = await ReadOverscanAsync(
                    ProtocolRequestCatalog.GetReadBackOperation(AdapterOperation.SetOverscan),
                    AdapterOperation.SetOverscan,
                    token);
                if (readBack.Value != settings)
                {
                    throw ProtocolFailure(
                        AdapterOperation.SetOverscan,
                        readBack.Response.StatusCode,
                        readBack.Response.Body,
                        "The exact overscan read-back did not match the requested value.");
                }
            },
            cancellationToken);
    }

    public async Task SetPasswordProtectionAsync(
        bool enabled,
        string? password,
        CancellationToken cancellationToken = default)
    {
        if (password is not null)
        {
            throw new ArgumentException(
                "The characterized pairing-protection operation does not transmit a password.",
                nameof(password));
        }

        await ExecuteWriteAsync(
            AdapterOperation.SetPasswordProtection,
            encoding => ProtocolRequestCatalog.CreateSetPasswordProtectionRequest(_endpoint, enabled, encoding),
            async token =>
            {
                var readBack = await ReadPasswordProtectionAsync(
                    ProtocolRequestCatalog.GetReadBackOperation(AdapterOperation.SetPasswordProtection),
                    AdapterOperation.SetPasswordProtection,
                    token);
                if (readBack.Value.Enabled != enabled)
                {
                    throw ProtocolFailure(
                        AdapterOperation.SetPasswordProtection,
                        readBack.Response.StatusCode,
                        readBack.Response.Body,
                        "The exact pairing-protection read-back did not match the requested value.");
                }
            },
            cancellationToken);
    }

    public async Task<CapabilityProfile> DetectCapabilitiesAsync(
        CancellationToken cancellationToken = default)
    {
        _ = await GetIdentityAsync(cancellationToken);
        _ = await GetOverscanAsync(cancellationToken);
        _ = await GetPasswordProtectionAsync(cancellationToken);

        return new CapabilityProfile(
            AdapterGeneration.Unknown,
            new HashSet<AdapterOperation>
            {
                AdapterOperation.GetDeviceName,
                AdapterOperation.SetDeviceName,
                AdapterOperation.GetOverscan,
                AdapterOperation.SetOverscan,
                AdapterOperation.GetPasswordProtection,
                AdapterOperation.SetPasswordProtection,
            });
    }

    public void Dispose()
    {
        _writeLock.Dispose();
        _transport.Dispose();
    }

    private async Task ExecuteWriteAsync(
        AdapterOperation operation,
        Func<ProtocolWriteEncoding, HttpRequestMessage> createRequest,
        Func<CancellationToken, Task> verifyReadBack,
        CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            using var request = createRequest(GetWriteEncoding(operation));
            var response = await _transport.SendAsync(request, cancellationToken);
            EnsureSuccess(operation, response);
            ValidateWriteResponse(operation, response);
            await verifyReadBack(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private Task<ReadResult<AdapterIdentity>> ReadIdentityAsync(
        AdapterOperation readOperation,
        AdapterOperation contextOperation,
        CancellationToken cancellationToken) =>
        ReadAsync(
            readOperation,
            contextOperation,
            ProtocolJson.ParseIdentity,
            cancellationToken);

    private Task<ReadResult<OverscanSettings>> ReadOverscanAsync(
        AdapterOperation readOperation,
        AdapterOperation contextOperation,
        CancellationToken cancellationToken) =>
        ReadAsync(
            readOperation,
            contextOperation,
            ProtocolJson.ParseOverscan,
            cancellationToken);

    private Task<ReadResult<PasswordProtectionSettings>> ReadPasswordProtectionAsync(
        AdapterOperation readOperation,
        AdapterOperation contextOperation,
        CancellationToken cancellationToken) =>
        ReadAsync(
            readOperation,
            contextOperation,
            ProtocolJson.ParsePasswordProtection,
            cancellationToken);

    private async Task<ReadResult<T>> ReadAsync<T>(
        AdapterOperation readOperation,
        AdapterOperation contextOperation,
        Func<string, T> parse,
        CancellationToken cancellationToken)
    {
        using var request = ProtocolRequestCatalog.CreateReadRequest(_endpoint, readOperation);
        var response = await _transport.SendAsync(request, cancellationToken);
        EnsureSuccess(contextOperation, response);

        try
        {
            return new ReadResult<T>(parse(response.Body), response);
        }
        catch (AdapterProtocolException exception)
        {
            throw ProtocolFailure(
                contextOperation,
                response.StatusCode,
                response.Body,
                "The response shape was malformed.",
                exception);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw ProtocolFailure(
                contextOperation,
                response.StatusCode,
                response.Body,
                "The response shape was unexpected.",
                exception);
        }
    }

    private ProtocolWriteEncoding GetWriteEncoding(AdapterOperation operation) =>
        _writeEncodings.TryGetValue(operation, out var encoding)
            ? encoding
            : ProtocolRequestCatalog.GetRecordedWriteEncoding(operation);

    private static void EnsureSuccess(AdapterOperation operation, AdapterHttpResponse response)
    {
        if ((int)response.StatusCode is < 200 or > 299)
        {
            throw ProtocolFailure(
                operation,
                response.StatusCode,
                response.Body,
                "The adapter returned a non-success status.");
        }
    }

    private static void ValidateWriteResponse(
        AdapterOperation operation,
        AdapterHttpResponse response)
    {
        if (string.IsNullOrWhiteSpace(response.Body))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(response.Body);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw ProtocolFailure(
                    operation,
                    response.StatusCode,
                    response.Body,
                    "The successful write response was not a JSON object.");
            }

            if (document.RootElement.TryGetProperty("ErrorCode", out var errorCode) &&
                errorCode.TryGetInt32(out var numericErrorCode) &&
                numericErrorCode != 0)
            {
                throw ProtocolFailure(
                    operation,
                    response.StatusCode,
                    response.Body,
                    $"The adapter returned error code {numericErrorCode}.");
            }
        }
        catch (JsonException exception)
        {
            throw ProtocolFailure(
                operation,
                response.StatusCode,
                response.Body,
                "The successful write response was not valid JSON.",
                exception);
        }
    }

    private static AdapterProtocolException ProtocolFailure(
        AdapterOperation operation,
        HttpStatusCode statusCode,
        string body,
        string detail,
        Exception? innerException = null)
    {
        var message =
            $"{operation} failed with HTTP {(int)statusCode} ({statusCode}). {detail} " +
            $"Redacted body prefix: {CreateRedactedBodyPrefix(body)}.";
        return innerException is null
            ? new AdapterProtocolException(message)
            : new AdapterProtocolException(message, innerException);
    }

    private static string CreateRedactedBodyPrefix(string body)
    {
        const int maximumPrefixLength = 128;
        if (body.Length == 0)
        {
            return "<empty>";
        }

        var prefixLength = Math.Min(body.Length, maximumPrefixLength);
        return $"<redacted {prefixLength} of {body.Length} characters>";
    }

    private static IReadOnlyDictionary<AdapterOperation, ProtocolWriteEncoding> CopyAndValidateWriteEncodings(
        IReadOnlyDictionary<AdapterOperation, ProtocolWriteEncoding>? writeEncodings)
    {
        if (writeEncodings is null)
        {
            return new Dictionary<AdapterOperation, ProtocolWriteEncoding>();
        }

        var copy = new Dictionary<AdapterOperation, ProtocolWriteEncoding>();
        foreach (var pair in writeEncodings)
        {
            _ = ProtocolRequestCatalog.GetReadBackOperation(pair.Key);
            if (!ProtocolRequestCatalog.WriteEncodingCandidates.Contains(pair.Value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(writeEncodings),
                    pair.Value,
                    "The write encoding is outside the closed characterization candidate set.");
            }

            copy.Add(pair.Key, pair.Value);
        }

        return copy;
    }

    private sealed record ReadResult<T>(T Value, AdapterHttpResponse Response);
}
