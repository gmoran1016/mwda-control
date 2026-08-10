using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mwda.Control.Protocol;

public static class ProtocolJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static AdapterIdentity ParseIdentity(string json)
    {
        var response = Deserialize<IdentityResponse>(json, "adapter identity");
        if (!AdapterValidation.IsValidDeviceName(response.DeviceName))
        {
            throw MissingOrInvalidProperty("DeviceName", "adapter identity");
        }

        return new AdapterIdentity(response.DeviceName!);
    }

    public static OverscanSettings ParseOverscan(string json)
    {
        var response = Deserialize<OverscanResponse>(json, "overscan settings");
        if (response.IsAutoAdjust is null)
        {
            throw MissingOrInvalidProperty("IsAutoAdjust", "overscan settings");
        }

        if (response.OverscanSettingValue is null)
        {
            throw MissingOrInvalidProperty("OverscanSettingValue", "overscan settings");
        }

        try
        {
            return AdapterValidation.CreateOverscan(
                response.IsAutoAdjust.Value,
                response.OverscanSettingValue.Value);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new AdapterProtocolException("The overscan response contains an out-of-range value.", exception);
        }
    }

    public static PasswordProtectionSettings ParsePasswordProtection(string json)
    {
        var response = Deserialize<PasswordProtectionResponse>(json, "password-protection settings");
        if (response.PasswordProtect is null)
        {
            throw MissingOrInvalidProperty("PasswordProtect", "password-protection settings");
        }

        return new PasswordProtectionSettings(response.PasswordProtect.Value);
    }

    private static T Deserialize<T>(string json, string responseName)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, SerializerOptions)
                ?? throw new AdapterProtocolException($"The {responseName} response is empty.");
        }
        catch (JsonException exception)
        {
            throw new AdapterProtocolException($"The {responseName} response is not valid JSON.", exception);
        }
    }

    private static AdapterProtocolException MissingOrInvalidProperty(
        string propertyName,
        string responseName) =>
        new($"The {responseName} response is missing a valid {propertyName} property.");

    private sealed class IdentityResponse
    {
        [JsonPropertyName("DeviceName")]
        public string? DeviceName { get; init; }
    }

    private sealed class OverscanResponse
    {
        [JsonPropertyName("IsAutoAdjust")]
        public bool? IsAutoAdjust { get; init; }

        [JsonPropertyName("OverscanSettingValue")]
        public int? OverscanSettingValue { get; init; }
    }

    private sealed class PasswordProtectionResponse
    {
        [JsonPropertyName("PasswordProtect")]
        public bool? PasswordProtect { get; init; }
    }
}
