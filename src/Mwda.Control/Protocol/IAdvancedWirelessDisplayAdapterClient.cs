using System.IO;

namespace Mwda.Control.Protocol;

public interface IAdvancedWirelessDisplayAdapterClient
{
    Task<WallpaperInfo> GetWallpaperInfoAsync(CancellationToken cancellationToken = default);

    Task SetPredefinedWallpaperAsync(string wallpaperId, CancellationToken cancellationToken = default);

    Task UploadCustomWallpaperAsync(
        Stream image,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<WifiSettings> GetWiFiSettingsAsync(CancellationToken cancellationToken = default);

    Task SetWiFiSettingsAsync(WifiSettings settings, CancellationToken cancellationToken = default);

    Task ForgetWiFiAsync(CancellationToken cancellationToken = default);

    Task<HdcpSettings> GetHdcpStatusAsync(CancellationToken cancellationToken = default);

    Task SetHdcpStatusAsync(bool enabled, CancellationToken cancellationToken = default);

    Task<LanguageInfo> GetLanguageAsync(CancellationToken cancellationToken = default);

    Task SetLanguageAsync(string languageTag, CancellationToken cancellationToken = default);

    Task RestartAsync(CancellationToken cancellationToken = default);
}
