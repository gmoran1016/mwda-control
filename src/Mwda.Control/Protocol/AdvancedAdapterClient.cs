using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mwda.Control.Protocol;

public sealed class AdvancedAdapterClient : IAdvancedWirelessDisplayAdapterClient, IDisposable
{
    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(3);
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly AdapterEndpoint _endpoint;
    private readonly AdapterHttpTransport _transport;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public AdvancedAdapterClient(AdapterEndpoint endpoint)
        : this(endpoint, DefaultRequestTimeout)
    {
    }

    public AdvancedAdapterClient(AdapterEndpoint endpoint, TimeSpan requestTimeout)
        : this(endpoint, new AdapterHttpTransport(requestTimeout))
    {
    }

    public AdvancedAdapterClient(
        AdapterEndpoint endpoint,
        HttpMessageHandler handler,
        TimeSpan requestTimeout)
        : this(endpoint, new AdapterHttpTransport(handler, requestTimeout))
    {
    }

    private AdvancedAdapterClient(AdapterEndpoint endpoint, AdapterHttpTransport transport)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(transport);
        _endpoint = endpoint;
        _transport = transport;
    }

    public Task<WallpaperInfo> GetWallpaperInfoAsync(CancellationToken cancellationToken = default) =>
        ReadAsync(
            AdapterOperation.GetWallpaperInfo,
            ParseWallpaperInfo,
            cancellationToken);

    public async Task SetPredefinedWallpaperAsync(
        string wallpaperId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(wallpaperId) || wallpaperId.Length > 64)
        {
            throw new ArgumentException("The wallpaper identifier is invalid.", nameof(wallpaperId));
        }

        await ExecuteWriteAsync(
            AdapterOperation.SetWallpaper,
            _ => Task.FromResult(
                ProtocolRequestCatalog.CreateSetPredefinedWallpaperRequest(_endpoint, wallpaperId)),
            async token =>
            {
                var readBack = await GetWallpaperInfoAsync(token);
                if (!string.Equals(readBack.CurrentWallpaperId, wallpaperId, StringComparison.Ordinal))
                {
                    throw ProtocolFailure(
                        AdapterOperation.SetWallpaper,
                        HttpStatusCode.OK,
                        "The exact wallpaper read-back did not match the requested identifier.");
                }
            },
            cancellationToken);
    }

    public Task UploadCustomWallpaperAsync(
        Stream image,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default) =>
        ExecuteWriteAsync(
            AdapterOperation.SetWallpaper,
            token => ProtocolRequestCatalog.CreateUploadWallpaperRequestAsync(
                _endpoint,
                image,
                fileName,
                contentType,
                token),
            verifyReadBack: null,
            cancellationToken);

    public Task<WifiSettings> GetWiFiSettingsAsync(CancellationToken cancellationToken = default) =>
        ReadAsync(
            AdapterOperation.GetWiFiSettings,
            ParseWifiSettings,
            cancellationToken);

    public async Task SetWiFiSettingsAsync(
        WifiSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (string.IsNullOrWhiteSpace(settings.Ssid))
        {
            throw new ArgumentException("The Wi-Fi SSID is required.", nameof(settings));
        }

        await ExecuteWriteAsync(
            AdapterOperation.SetWiFiSettings,
            _ => Task.FromResult(ProtocolRequestCatalog.CreateSetWiFiSettingsRequest(_endpoint, settings)),
            async token =>
            {
                var readBack = await GetWiFiSettingsAsync(token);
                if (!string.Equals(readBack.Ssid, settings.Ssid, StringComparison.Ordinal) ||
                    readBack.IsConnected != settings.IsConnected)
                {
                    throw ProtocolFailure(
                        AdapterOperation.SetWiFiSettings,
                        HttpStatusCode.OK,
                        "The exact Wi-Fi read-back did not match the requested settings.");
                }
            },
            cancellationToken);
    }

    public Task ForgetWiFiAsync(CancellationToken cancellationToken = default) =>
        ExecuteWriteAsync(
            AdapterOperation.ForgetWiFi,
            _ => Task.FromResult(ProtocolRequestCatalog.CreateForgetWiFiRequest(_endpoint)),
            async token =>
            {
                var readBack = await GetWiFiSettingsAsync(token);
                if (readBack.IsConnected)
                {
                    throw ProtocolFailure(
                        AdapterOperation.ForgetWiFi,
                        HttpStatusCode.OK,
                        "The Wi-Fi read-back remained connected after forgetting the network.");
                }
            },
            cancellationToken);

    public Task<HdcpSettings> GetHdcpStatusAsync(CancellationToken cancellationToken = default) =>
        ReadAsync(
            AdapterOperation.GetHdcpStatus,
            ParseHdcpSettings,
            cancellationToken);

    public Task SetHdcpStatusAsync(
        bool enabled,
        CancellationToken cancellationToken = default) =>
        ExecuteWriteAsync(
            AdapterOperation.SetHdcpStatus,
            _ => Task.FromResult(ProtocolRequestCatalog.CreateSetHdcpStatusRequest(_endpoint, enabled)),
            async token =>
            {
                var readBack = await GetHdcpStatusAsync(token);
                if (readBack.Enabled != enabled)
                {
                    throw ProtocolFailure(
                        AdapterOperation.SetHdcpStatus,
                        HttpStatusCode.OK,
                        "The exact HDCP read-back did not match the requested value.");
                }
            },
            cancellationToken);

    public Task<LanguageInfo> GetLanguageAsync(CancellationToken cancellationToken = default) =>
        ReadAsync(
            AdapterOperation.GetLanguage,
            ParseLanguageInfo,
            cancellationToken);

    public Task SetLanguageAsync(
        string languageTag,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(languageTag))
        {
            throw new ArgumentException("The language tag is required.", nameof(languageTag));
        }

        return ExecuteWriteAsync(
            AdapterOperation.SetLanguage,
            _ => Task.FromResult(ProtocolRequestCatalog.CreateSetLanguageRequest(_endpoint, languageTag)),
            async token =>
            {
                var readBack = await GetLanguageAsync(token);
                if (!string.Equals(readBack.LanguageTag, languageTag, StringComparison.OrdinalIgnoreCase))
                {
                    throw ProtocolFailure(
                        AdapterOperation.SetLanguage,
                        HttpStatusCode.OK,
                        "The exact language read-back did not match the requested value.");
                }
            },
            cancellationToken);
    }

    public Task RestartAsync(CancellationToken cancellationToken = default) =>
        ExecuteWriteAsync(
            AdapterOperation.Restart,
            _ => Task.FromResult(ProtocolRequestCatalog.CreateRestartRequest(_endpoint)),
            verifyReadBack: null,
            cancellationToken);

    public void Dispose()
    {
        _writeLock.Dispose();
        _transport.Dispose();
    }

    private async Task<T> ReadAsync<T>(
        AdapterOperation operation,
        Func<string, T> parse,
        CancellationToken cancellationToken)
    {
        using var request = ProtocolRequestCatalog.CreateReadRequest(_endpoint, operation);
        var response = await _transport.SendAsync(request, cancellationToken);
        EnsureSuccess(operation, response);

        try
        {
            return parse(response.Body);
        }
        catch (Exception exception) when (exception is JsonException or AdapterProtocolException)
        {
            throw UnsupportedFailure(
                operation,
                response.StatusCode,
                response.Body,
                "The read response did not match the required schema.",
                exception);
        }
    }

    private async Task ExecuteWriteAsync(
        AdapterOperation operation,
        Func<CancellationToken, Task<HttpRequestMessage>> createRequest,
        Func<CancellationToken, Task>? verifyReadBack,
        CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            using var request = await createRequest(cancellationToken);
            var response = await _transport.SendAsync(request, cancellationToken);
            EnsureSuccess(operation, response);
            ValidateWriteResponse(operation, response);
            if (verifyReadBack is not null)
            {
                await verifyReadBack(cancellationToken);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static WallpaperInfo ParseWallpaperInfo(string json)
    {
        var response = Deserialize<WallpaperResponse>(json, "wallpaper");
        if (string.IsNullOrWhiteSpace(response.WallpaperId) ||
            response.AvailableWallpaperIds is null ||
            response.AvailableWallpaperIds.Count == 0 ||
            response.AvailableWallpaperIds.Any(string.IsNullOrWhiteSpace) ||
            !response.AvailableWallpaperIds.Contains(response.WallpaperId, StringComparer.Ordinal) ||
            response.SupportsCustomWallpaper is null)
        {
            throw SchemaFailure(
                "WallpaperID/AvailableWallpaperIDs/SupportsCustomWallpaper",
                "wallpaper");
        }

        return new WallpaperInfo(
            response.WallpaperId,
            response.AvailableWallpaperIds.AsReadOnly(),
            response.SupportsCustomWallpaper.Value);
    }

    private static WifiSettings ParseWifiSettings(string json)
    {
        var response = Deserialize<WifiResponse>(json, "Wi-Fi");
        if (response.WifiSsid is null || string.IsNullOrWhiteSpace(response.ConnectionStatus))
        {
            throw SchemaFailure("WiFiSsid/ConnectionStatus", "Wi-Fi");
        }

        var isConnected = response.ConnectionStatus switch
        {
            var value when value.Equals("Connected", StringComparison.OrdinalIgnoreCase) => true,
            var value when value.Equals("Disconnected", StringComparison.OrdinalIgnoreCase) => false,
            _ => throw SchemaFailure("ConnectionStatus", "Wi-Fi"),
        };
        if (isConnected && string.IsNullOrWhiteSpace(response.WifiSsid))
        {
            throw SchemaFailure("WiFiSsid", "connected Wi-Fi");
        }

        return new WifiSettings(response.WifiSsid, isConnected);
    }

    private static HdcpSettings ParseHdcpSettings(string json)
    {
        var response = Deserialize<HdcpResponse>(json, "HDCP");
        return response.HdcpStatus is bool enabled
            ? new HdcpSettings(enabled)
            : throw SchemaFailure("HdcpStatus", "HDCP");
    }

    private static LanguageInfo ParseLanguageInfo(string json)
    {
        var response = Deserialize<LanguageResponse>(json, "language");
        if (string.IsNullOrWhiteSpace(response.CurrentLanguage) ||
            response.LanguageCodes is null ||
            response.LanguageCodes.Count == 0 ||
            response.LanguageCodes.Any(string.IsNullOrWhiteSpace) ||
            !response.LanguageCodes.Contains(response.CurrentLanguage, StringComparer.OrdinalIgnoreCase))
        {
            throw SchemaFailure("CurrentLanguage/LanguageCode", "language");
        }

        return new LanguageInfo(response.CurrentLanguage, response.LanguageCodes.AsReadOnly());
    }

    private static T Deserialize<T>(string json, string responseName)
        where T : class =>
        JsonSerializer.Deserialize<T>(json, SerializerOptions)
        ?? throw new AdapterProtocolException($"The {responseName} response was empty.");

    private static AdapterProtocolException SchemaFailure(string propertyName, string responseName) =>
        new($"The {responseName} response is missing a valid {propertyName} property.");

    private static void EnsureSuccess(AdapterOperation operation, AdapterHttpResponse response)
    {
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NotImplemented)
        {
            throw UnsupportedFailure(
                operation,
                response.StatusCode,
                response.Body,
                "The adapter does not expose this operation.");
        }

        if ((int)response.StatusCode is < 200 or > 299)
        {
            throw ProtocolFailure(
                operation,
                response.StatusCode,
                response.Body,
                "The adapter returned a non-success status.");
        }
    }

    private static void ValidateWriteResponse(AdapterOperation operation, AdapterHttpResponse response)
    {
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

    private static UnsupportedAdapterOperationException UnsupportedFailure(
        AdapterOperation operation,
        HttpStatusCode statusCode,
        string body,
        string detail,
        Exception? innerException = null)
    {
        var message = CreateFailureMessage(operation, statusCode, body, detail);
        return innerException is null
            ? new UnsupportedAdapterOperationException(operation, statusCode, message)
            : new UnsupportedAdapterOperationException(operation, statusCode, message, innerException);
    }

    private static AdapterProtocolException ProtocolFailure(
        AdapterOperation operation,
        HttpStatusCode statusCode,
        string detail) =>
        ProtocolFailure(operation, statusCode, string.Empty, detail);

    private static AdapterProtocolException ProtocolFailure(
        AdapterOperation operation,
        HttpStatusCode statusCode,
        string body,
        string detail,
        Exception? innerException = null)
    {
        var message = CreateFailureMessage(operation, statusCode, body, detail);
        return innerException is null
            ? new AdapterProtocolException(message)
            : new AdapterProtocolException(message, innerException);
    }

    private static string CreateFailureMessage(
        AdapterOperation operation,
        HttpStatusCode statusCode,
        string body,
        string detail)
    {
        var prefixLength = Math.Min(body.Length, 128);
        var redactedPrefix = body.Length == 0
            ? "<empty>"
            : $"<redacted {prefixLength} of {body.Length} characters>";
        return $"{operation} failed with HTTP {(int)statusCode} ({statusCode}). {detail} " +
               $"Redacted body prefix: {redactedPrefix}.";
    }

    private sealed class WallpaperResponse
    {
        [JsonPropertyName("WallpaperID")]
        public string? WallpaperId { get; init; }

        [JsonPropertyName("AvailableWallpaperIDs")]
        public List<string>? AvailableWallpaperIds { get; init; }

        [JsonPropertyName("SupportsCustomWallpaper")]
        public bool? SupportsCustomWallpaper { get; init; }
    }

    private sealed class WifiResponse
    {
        [JsonPropertyName("WiFiSsid")]
        public string? WifiSsid { get; init; }

        [JsonPropertyName("ConnectionStatus")]
        public string? ConnectionStatus { get; init; }
    }

    private sealed class HdcpResponse
    {
        [JsonPropertyName("HdcpStatus")]
        public bool? HdcpStatus { get; init; }
    }

    private sealed class LanguageResponse
    {
        [JsonPropertyName("CurrentLanguage")]
        public string? CurrentLanguage { get; init; }

        [JsonPropertyName("LanguageCode")]
        public List<string>? LanguageCodes { get; init; }
    }
}
