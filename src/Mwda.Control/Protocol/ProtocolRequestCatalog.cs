using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Mwda.Control.Protocol;

public enum ProtocolWriteEncoding
{
    FormUrlEncoded,
    Json,
    QueryParameters,
}

public static class ProtocolRequestCatalog
{
    private const string ControlPath = "/cgi-bin/msupload.sh";

    private static readonly IReadOnlyList<ProtocolWriteEncoding> CandidateEncodings =
        Array.AsReadOnly(
            new[]
            {
                ProtocolWriteEncoding.FormUrlEncoded,
                ProtocolWriteEncoding.Json,
                ProtocolWriteEncoding.QueryParameters,
            });

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static IReadOnlyList<ProtocolWriteEncoding> WriteEncodingCandidates => CandidateEncodings;

    public static HttpRequestMessage CreateReadRequest(
        AdapterEndpoint endpoint,
        AdapterOperation operation)
    {
        var action = operation switch
        {
            AdapterOperation.GetDeviceName => "GetDeviceName",
            AdapterOperation.GetOverscan => "GetOverscanSetting",
            AdapterOperation.GetPasswordProtection => "GetPasswordProtectState",
            _ => throw UnsupportedOperation(operation),
        };

        return CreateRequest(endpoint, action, Array.Empty<KeyValuePair<string, object>>(), encoding: null);
    }

    public static HttpRequestMessage CreateSetDeviceNameRequest(
        AdapterEndpoint endpoint,
        string deviceName,
        ProtocolWriteEncoding? encoding = null) =>
        CreateWriteRequest(
            endpoint,
            AdapterOperation.SetDeviceName,
            new[] { Field("NewDeviceName", deviceName) },
            encoding);

    public static HttpRequestMessage CreateSetOverscanRequest(
        AdapterEndpoint endpoint,
        OverscanSettings settings,
        ProtocolWriteEncoding? encoding = null) =>
        CreateWriteRequest(
            endpoint,
            AdapterOperation.SetOverscan,
            new[]
            {
                Field("IsAutoAdjust", settings.IsAutoAdjust),
                Field("OverscanSettingValue", settings.Value),
            },
            encoding);

    public static HttpRequestMessage CreateSetPasswordProtectionRequest(
        AdapterEndpoint endpoint,
        bool enabled,
        ProtocolWriteEncoding? encoding = null) =>
        CreateWriteRequest(
            endpoint,
            AdapterOperation.SetPasswordProtection,
            new[] { Field("PasswordProtect", enabled) },
            encoding);

    public static ProtocolWriteEncoding GetRecordedWriteEncoding(AdapterOperation operation) =>
        operation switch
        {
            AdapterOperation.SetDeviceName or
            AdapterOperation.SetOverscan or
            AdapterOperation.SetPasswordProtection => ProtocolWriteEncoding.QueryParameters,
            _ => throw UnsupportedOperation(operation),
        };

    public static AdapterOperation GetReadBackOperation(AdapterOperation operation) =>
        operation switch
        {
            AdapterOperation.SetDeviceName => AdapterOperation.GetDeviceName,
            AdapterOperation.SetOverscan => AdapterOperation.GetOverscan,
            AdapterOperation.SetPasswordProtection => AdapterOperation.GetPasswordProtection,
            _ => throw UnsupportedOperation(operation),
        };

    private static HttpRequestMessage CreateWriteRequest(
        AdapterEndpoint endpoint,
        AdapterOperation operation,
        IReadOnlyList<KeyValuePair<string, object>> fields,
        ProtocolWriteEncoding? encoding)
    {
        var action = operation switch
        {
            AdapterOperation.SetDeviceName => "SetDeviceName",
            AdapterOperation.SetOverscan => "SetOverscanSetting",
            AdapterOperation.SetPasswordProtection => "SetPasswordProtect",
            _ => throw UnsupportedOperation(operation),
        };

        return CreateRequest(
            endpoint,
            action,
            fields,
            encoding ?? GetRecordedWriteEncoding(operation));
    }

    private static HttpRequestMessage CreateRequest(
        AdapterEndpoint endpoint,
        string action,
        IReadOnlyList<KeyValuePair<string, object>> fields,
        ProtocolWriteEncoding? encoding)
    {
        ValidateEndpoint(endpoint);

        var actionQuery = $"Action={Uri.EscapeDataString(action)}";
        if (encoding is null)
        {
            return new HttpRequestMessage(
                HttpMethod.Get,
                BuildUri(endpoint, $"{ControlPath}?{actionQuery}"));
        }

        var encodedFields = EncodeFields(fields);
        if (encoding == ProtocolWriteEncoding.QueryParameters)
        {
            var query = encodedFields.Length == 0
                ? actionQuery
                : $"{actionQuery}&{encodedFields}";
            return new HttpRequestMessage(
                HttpMethod.Get,
                BuildUri(endpoint, $"{ControlPath}?{query}"));
        }

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildUri(endpoint, $"{ControlPath}?{actionQuery}"));
        request.Content = encoding switch
        {
            ProtocolWriteEncoding.FormUrlEncoded =>
                new StringContent(encodedFields, Encoding.UTF8, "application/x-www-form-urlencoded"),
            ProtocolWriteEncoding.Json =>
                new StringContent(CreateJsonBody(fields), Encoding.UTF8, "application/json"),
            _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, "Unknown write encoding."),
        };

        return request;
    }

    private static string EncodeFields(IEnumerable<KeyValuePair<string, object>> fields) =>
        string.Join(
            "&",
            fields.Select(
                field => $"{Uri.EscapeDataString(field.Key)}={Uri.EscapeDataString(FormatValue(field.Value))}"));

    private static string CreateJsonBody(IEnumerable<KeyValuePair<string, object>> fields)
    {
        var values = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            values.Add(field.Key, field.Value);
        }

        return JsonSerializer.Serialize(values, JsonOptions);
    }

    private static string FormatValue(object value) => value switch
    {
        bool boolean => boolean ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    private static Uri BuildUri(AdapterEndpoint endpoint, string pathAndQuery)
    {
        var authority = new Uri(endpoint.BaseAddress.GetLeftPart(UriPartial.Authority));
        return new Uri(authority, pathAndQuery);
    }

    private static void ValidateEndpoint(AdapterEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!endpoint.BaseAddress.IsAbsoluteUri ||
            (endpoint.BaseAddress.Scheme != Uri.UriSchemeHttp &&
             endpoint.BaseAddress.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "The adapter endpoint must be an absolute HTTP or HTTPS URI.",
                nameof(endpoint));
        }
    }

    private static KeyValuePair<string, object> Field(string name, object value) => new(name, value);

    private static ArgumentOutOfRangeException UnsupportedOperation(AdapterOperation operation) =>
        new(nameof(operation), operation, "The operation is not a characterized core-settings operation.");
}
