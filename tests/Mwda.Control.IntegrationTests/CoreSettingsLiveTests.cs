using Mwda.Control.Protocol;

namespace Mwda.Control.IntegrationTests;

[Trait("Category", "LiveAdapter")]
public sealed class CoreSettingsLiveTests
{
    [LiveAdapterFact]
    public async Task RecordedQueryCandidatesRequireExactLiveReadBackAndRestore()
    {
        Assert.True(LiveAdapterFixture.TryCreate(out var fixture));
        using var live = fixture!;

        var originalIdentity = await live.Client.GetIdentityAsync();
        var originalOverscan = await live.Client.GetOverscanAsync();
        var originalProtection = await live.Client.GetPasswordProtectionAsync();

        await CharacterizeDeviceNameAndRestoreAsync(live.Client, originalIdentity.DeviceName);
        await CharacterizeOverscanAndRestoreAsync(live.Client, originalOverscan);
        await CharacterizePasswordProtectionAndRestoreAsync(live.Client, originalProtection.Enabled);
    }

    private static async Task CharacterizeDeviceNameAndRestoreAsync(
        AdapterClient client,
        string originalDeviceName)
    {
        var temporaryName = $"MWDA-Test-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        try
        {
            await client.SetDeviceNameAsync(temporaryName);
            var readBack = await client.GetIdentityAsync();
            Assert.Equal(temporaryName, readBack.DeviceName);
        }
        finally
        {
            await client.SetDeviceNameAsync(originalDeviceName);
        }
    }

    private static async Task CharacterizeOverscanAndRestoreAsync(
        AdapterClient client,
        OverscanSettings original)
    {
        var temporaryValue = original.Value < AdapterValidation.MaximumOverscanValue
            ? original.Value + 1
            : original.Value - 1;
        var temporary = new OverscanSettings(original.IsAutoAdjust, temporaryValue);

        try
        {
            await client.SetOverscanAsync(temporary);
            var readBack = await client.GetOverscanAsync();
            Assert.Equal(temporary, readBack);
        }
        finally
        {
            await client.SetOverscanAsync(original);
        }
    }

    private static async Task CharacterizePasswordProtectionAndRestoreAsync(
        AdapterClient client,
        bool originallyEnabled)
    {
        var temporaryValue = !originallyEnabled;
        try
        {
            await client.SetPasswordProtectionAsync(temporaryValue, password: null);
            var readBack = await client.GetPasswordProtectionAsync();
            Assert.Equal(temporaryValue, readBack.Enabled);
        }
        finally
        {
            await client.SetPasswordProtectionAsync(originallyEnabled, password: null);
        }
    }
}
