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

        // Restart has no read-back/restoration path, so live coverage never invokes it.
        Assert.False(profile.Supports(AdapterOperation.Restart));
    }
}
