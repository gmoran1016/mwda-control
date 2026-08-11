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
        var response = Deserialize<PairingProtectionResponse>(json, "pairing-protection settings");
        if (response.PbcModeStatus is not null)
        {
            return response.PbcModeStatus switch
            {
                "Disabled" => new PasswordProtectionSettings(true),
                "Enabled" => new PasswordProtectionSettings(false),
                _ => throw new AdapterProtocolException(
                    "The pairing-protection settings response contains an unknown PBCModeStatus value."),
            };
        }

        if (response.PasswordProtect is bool legacyPasswordProtect)
        {
            return new PasswordProtectionSettings(legacyPasswordProtect);
        }

        throw MissingOrInvalidProperty("PBCModeStatus or PasswordProtect", "pairing-protection settings");
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

    private sealed class PairingProtectionResponse
    {
        [JsonPropertyName("PBCModeStatus")]
        public string? PbcModeStatus { get; init; }

        [JsonPropertyName("PasswordProtect")]
        public bool? PasswordProtect { get; init; }
    }
}
