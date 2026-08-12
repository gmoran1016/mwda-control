using Mwda.Control.Protocol;
using Mwda.Control.Session;

namespace Mwda.Control.IntegrationTests;

[Trait("Category", "LiveAdapter")]
public sealed class OptionalCapabilitiesLiveTests
{
    [LiveAdapterFact]
    public async Task OptionalCapabilitiesAreReadBeforeAnyMutationIsConsidered()
    {
        Assert.True(LiveAdapterFixture.TryCreate(out var fixture));
        using var live = fixture!;
        using var advanced = new AdvancedAdapterClient(live.Endpoint);

        var profile = await CapabilityDetector.DetectAsync(
            live.Client,
            advanced,
            CancellationToken.None);

        if (profile.Supports(AdapterOperation.GetWallpaperInfo))
        {
            _ = await advanced.GetWallpaperInfoAsync();
        }

        if (profile.Supports(AdapterOperation.GetWiFiSettings))
        {
            _ = await advanced.GetWiFiSettingsAsync();
        }

        if (profile.Supports(AdapterOperation.GetHdcpStatus))
        {
            _ = await advanced.GetHdcpStatusAsync();
        }

        if (profile.Supports(AdapterOperation.GetLanguage))
        {
            _ = await advanced.GetLanguageAsync();
        }

        // Restart has no safe read-only probe or restoration path, so live coverage
        // confirms only that the action is advertised and never invokes it.
        Assert.True(profile.Supports(AdapterOperation.Restart));
    }

    [LiveAdapterFact]
    public async Task LegacyCustomWallpaperUploadReadsBackAndRestoresBuiltInWallpaper()
    {
        Assert.True(LiveAdapterFixture.TryCreate(out var fixture));
        using var live = fixture!;
        using var advanced = new AdvancedAdapterClient(live.Endpoint);

        var original = await advanced.GetWallpaperInfoAsync();
        Assert.Equal(WallpaperProtocolVariant.LegacyGeneration2, original.ProtocolVariant);

        var restorationId = string.Equals(original.CurrentWallpaperId, "0", StringComparison.Ordinal)
            ? "1"
            : original.CurrentWallpaperId;
        Assert.False(string.IsNullOrWhiteSpace(restorationId));

        try
        {
            using var source = new MemoryStream(Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
            await advanced.UploadCustomWallpaperAsync(source, "live-test.png", "image/png");

            var custom = await advanced.GetWallpaperInfoAsync();
            Assert.Equal("0", custom.CurrentWallpaperId);
        }
        finally
        {
            await advanced.SetPredefinedWallpaperAsync(restorationId!);
        }

        var restored = await advanced.GetWallpaperInfoAsync();
        Assert.Equal(restorationId, restored.CurrentWallpaperId);
    }
}
