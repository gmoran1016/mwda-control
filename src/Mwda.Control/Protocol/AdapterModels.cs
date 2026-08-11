namespace Mwda.Control.Protocol;

public sealed record AdapterEndpoint(Uri BaseAddress);

public sealed record AdapterIdentity(
    string DeviceName,
    AdapterGeneration Generation = AdapterGeneration.Unknown,
    string? Model = null,
    string? FirmwareVersion = null,
    string? MacAddress = null);

public sealed record OverscanSettings(bool IsAutoAdjust, int Value);

public sealed record PasswordProtectionSettings(bool Enabled);

public enum WallpaperProtocolVariant
{
    Modern,
    LegacyGeneration2,
}

public sealed record WallpaperInfo(
    string? CurrentWallpaperId,
    IReadOnlyList<string> AvailableWallpaperIds,
    bool SupportsCustomWallpaper,
    WallpaperProtocolVariant ProtocolVariant = WallpaperProtocolVariant.Modern);

public sealed record WifiSettings(string Ssid, bool IsConnected, string? Password = null);

public sealed record HdcpSettings(bool Enabled);

public sealed record LanguageInfo(
    string LanguageTag,
    IReadOnlyList<string> AvailableLanguageTags);

public static class AdapterModelNames
{
    public const string Generation2 =
        "Microsoft Wireless Display Adapter (with Microsoft 4 Square logo)";
}

public sealed record CapabilityProfile(
    AdapterGeneration Generation,
    IReadOnlySet<AdapterOperation> SupportedOperations)
{
    public bool Supports(AdapterOperation operation) => SupportedOperations.Contains(operation);
}
