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
        };

        await ProbeAsync(
            () => advancedClient.GetWallpaperInfoAsync(cancellationToken),
            supported,
            AdapterOperation.GetWallpaperInfo,
            AdapterOperation.SetWallpaper);
        await ProbeAsync(
            () => advancedClient.GetWiFiSettingsAsync(cancellationToken),
            supported,
            AdapterOperation.GetWiFiSettings,
            AdapterOperation.SetWiFiSettings,
            AdapterOperation.ForgetWiFi);
        await ProbeAsync(
            () => advancedClient.GetHdcpStatusAsync(cancellationToken),
            supported,
            AdapterOperation.GetHdcpStatus,
            AdapterOperation.SetHdcpStatus);
        await ProbeAsync(
            () => advancedClient.GetLanguageAsync(cancellationToken),
            supported,
            AdapterOperation.GetLanguage,
            AdapterOperation.SetLanguage);

        return new CapabilityProfile(identity.Generation, supported);
    }

    private static async Task ProbeAsync<T>(
        Func<Task<T>> probe,
        ISet<AdapterOperation> supported,
        params AdapterOperation[] operations)
    {
        try
        {
            _ = await probe();
            foreach (var operation in operations)
            {
                supported.Add(operation);
            }
        }
        catch (UnsupportedAdapterOperationException)
        {
        }
    }
}
