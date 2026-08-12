using Mwda.Control.Protocol;

namespace Mwda.Control.Session;

public static class CapabilityDetector
{
    public static async Task<CapabilityProfile> DetectAsync(
        IWirelessDisplayAdapterClient client,
        IAdvancedWirelessDisplayAdapterClient advancedClient,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(advancedClient);

        var identity = await client.GetIdentityAsync(cancellationToken);
        _ = await client.GetOverscanAsync(cancellationToken);
        _ = await client.GetPasswordProtectionAsync(cancellationToken);

        var supported = new HashSet<AdapterOperation>
        {
            AdapterOperation.GetDeviceName,
            AdapterOperation.SetDeviceName,
            AdapterOperation.GetOverscan,
            AdapterOperation.SetOverscan,
            AdapterOperation.GetPasswordProtection,
            AdapterOperation.SetPasswordProtection,
            // Restart has no safe read-only probe or read-back path. The Microsoft
            // control surface exposes it as a write-only recovery action.
            AdapterOperation.Restart,
        };

        var wallpaper = await ProbeAsync(
            () => advancedClient.GetWallpaperInfoAsync(cancellationToken),
            supported,
            AdapterOperation.GetWallpaperInfo,
            AdapterOperation.SetWallpaper);
        _ = await ProbeAsync(
            () => advancedClient.GetWiFiSettingsAsync(cancellationToken),
            supported,
            AdapterOperation.GetWiFiSettings,
            AdapterOperation.SetWiFiSettings,
            AdapterOperation.ForgetWiFi);
        _ = await ProbeAsync(
            () => advancedClient.GetHdcpStatusAsync(cancellationToken),
            supported,
            AdapterOperation.GetHdcpStatus,
            AdapterOperation.SetHdcpStatus);
        _ = await ProbeAsync(
            () => advancedClient.GetLanguageAsync(cancellationToken),
            supported,
            AdapterOperation.GetLanguage,
            AdapterOperation.SetLanguage);

        var generation = wallpaper?.ProtocolVariant == WallpaperProtocolVariant.LegacyGeneration2
            ? AdapterGeneration.Generation2
            : identity.Generation;
        return new CapabilityProfile(generation, supported);
    }

    private static async Task<T?> ProbeAsync<T>(
        Func<Task<T>> probe,
        ISet<AdapterOperation> supported,
        params AdapterOperation[] operations)
        where T : class
    {
        try
        {
            var result = await probe();
            foreach (var operation in operations)
            {
                supported.Add(operation);
            }

            return result;
        }
        catch (UnsupportedAdapterOperationException)
        {
            return null;
        }
    }
}
