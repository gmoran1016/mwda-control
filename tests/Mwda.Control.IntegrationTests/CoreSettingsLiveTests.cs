using Mwda.Control.Protocol;

namespace Mwda.Control.IntegrationTests;

[Trait("Category", "LiveAdapter")]
public sealed class CoreSettingsLiveTests
{
    [LiveAdapterFact]
    public async Task ClosedCandidatesRequireExactLiveReadBackAndRestore()
    {
        Assert.True(LiveAdapterFixture.TryCreate(out var fixture));
        using var live = fixture!;

        var originalIdentity = await live.Client.GetIdentityAsync();
        var originalOverscan = await live.Client.GetOverscanAsync();
        var originalProtection = await live.Client.GetPasswordProtectionAsync();
        Assert.False(string.IsNullOrWhiteSpace(originalIdentity.DeviceName));

        await CharacterizeDeviceNameAndRestoreAsync(live, originalIdentity.DeviceName);
        await CharacterizeOverscanAndRestoreAsync(live, originalOverscan);
        await VerifyPairingProtectionUsesPbcModeAsync(live, originalProtection.Enabled);

        Assert.Equal(2, live.AcceptedWriteEncodings.Count);

        var finalIdentity = await live.Client.GetIdentityAsync();
        var finalOverscan = await live.Client.GetOverscanAsync();
        var finalProtection = await live.Client.GetPasswordProtectionAsync();
        Assert.Equal(originalIdentity.DeviceName, finalIdentity.DeviceName);
        Assert.Equal(originalOverscan, finalOverscan);
        Assert.Equal(originalProtection, finalProtection);
    }

    private static async Task CharacterizeDeviceNameAndRestoreAsync(
        LiveAdapterFixture live,
        string originalDeviceName)
    {
        var temporaryName = $"MWDA-Test-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        await live.CharacterizeWriteEncodingAsync(
            AdapterOperation.SetDeviceName,
            candidate => TryCandidateAsync(
                live,
                AdapterOperation.SetDeviceName,
                candidate,
                client => client.SetDeviceNameAsync(temporaryName)),
            fallback => RestoreIfNeededAsync(
                live,
                AdapterOperation.SetDeviceName,
                fallback,
                originalDeviceName,
                async client => (await client.GetIdentityAsync()).DeviceName,
                client => client.SetDeviceNameAsync(originalDeviceName)));
    }

    private static async Task CharacterizeOverscanAndRestoreAsync(
        LiveAdapterFixture live,
        OverscanSettings original)
    {
        var temporaryValue = original.Value < AdapterValidation.MaximumOverscanValue
            ? original.Value + 1
            : original.Value - 1;
        var temporary = new OverscanSettings(original.IsAutoAdjust, temporaryValue);

        await live.CharacterizeWriteEncodingAsync(
            AdapterOperation.SetOverscan,
            candidate => TryCandidateAsync(
                live,
                AdapterOperation.SetOverscan,
                candidate,
                client => client.SetOverscanAsync(temporary)),
            fallback => RestoreIfNeededAsync(
                live,
                AdapterOperation.SetOverscan,
                fallback,
                original,
                client => client.GetOverscanAsync(),
                client => client.SetOverscanAsync(original)));
    }

    private static async Task VerifyPairingProtectionUsesPbcModeAsync(
        LiveAdapterFixture live,
        bool originallyEnabled)
    {
        Assert.True(originallyEnabled, "The live Four Square-logo adapter should report PIN-only mode as enabled.");

        using var client = live.CreateCandidateClient(
            AdapterOperation.SetPasswordProtection,
            ProtocolWriteEncoding.QueryParameters);
        await client.SetPasswordProtectionAsync(enabled: true, password: null);

        var readBack = await live.Client.GetPasswordProtectionAsync();
        Assert.True(readBack.Enabled);
    }

    private static async Task<bool> TryCandidateAsync(
        LiveAdapterFixture live,
        AdapterOperation operation,
        ProtocolWriteEncoding candidate,
        Func<AdapterClient, Task> write)
    {
        using var client = live.CreateCandidateClient(operation, candidate);
        try
        {
            await write(client);
            return true;
        }
        catch (AdapterProtocolException)
        {
            return false;
        }
    }

    private static async Task RestoreIfNeededAsync<T>(
        LiveAdapterFixture live,
        AdapterOperation operation,
        ProtocolWriteEncoding fallbackEncoding,
        T original,
        Func<AdapterClient, Task<T>> read,
        Func<AdapterClient, Task> restore)
    {
        var current = await read(live.Client);
        if (EqualityComparer<T>.Default.Equals(current, original))
        {
            return;
        }

        Exception? restorationFailure = null;
        try
        {
            using var client = live.CreateRestorationClient(operation, fallbackEncoding);
            await restore(client);
        }
        catch (AdapterProtocolException exception)
        {
            restorationFailure = exception;
        }

        var restored = await read(live.Client);
        if (!EqualityComparer<T>.Default.Equals(restored, original))
        {
            var message = $"Failed to restore {operation} after a write-encoding candidate attempt.";
            throw restorationFailure is null
                ? new AdapterProtocolException(message)
                : new AdapterProtocolException(message, restorationFailure);
        }
    }
}
