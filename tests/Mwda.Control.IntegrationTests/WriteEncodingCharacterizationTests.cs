using Mwda.Control.Protocol;

namespace Mwda.Control.IntegrationTests;

public sealed class WriteEncodingCharacterizationTests
{
    [Fact]
    public async Task SelectionTriesClosedCandidatesAndRecordsAcceptanceBeforeRestore()
    {
        var characterization = new WriteEncodingCharacterization();
        var attempts = new List<ProtocolWriteEncoding>();
        var restorations = new List<ProtocolWriteEncoding>();
        var acceptedWasRecordedBeforeRestore = false;

        var accepted = await characterization.SelectAndRecordAsync(
            AdapterOperation.SetDeviceName,
            candidate =>
            {
                attempts.Add(candidate);
                return Task.FromResult(candidate == ProtocolWriteEncoding.QueryParameters);
            },
            candidate =>
            {
                restorations.Add(candidate);
                if (candidate == ProtocolWriteEncoding.QueryParameters)
                {
                    acceptedWasRecordedBeforeRestore =
                        characterization.AcceptedEncodings.TryGetValue(
                            AdapterOperation.SetDeviceName,
                            out var recorded) &&
                        recorded == candidate;
                }

                return Task.CompletedTask;
            });

        Assert.Equal(ProtocolWriteEncoding.QueryParameters, accepted);
        Assert.Equal(
            new[]
            {
                ProtocolWriteEncoding.FormUrlEncoded,
                ProtocolWriteEncoding.Json,
                ProtocolWriteEncoding.QueryParameters,
            },
            attempts);
        Assert.Equal(attempts, restorations);
        Assert.True(acceptedWasRecordedBeforeRestore);
        Assert.Equal(
            ProtocolWriteEncoding.QueryParameters,
            characterization.AcceptedEncodings[AdapterOperation.SetDeviceName]);
    }

    [Fact]
    public async Task RejectedCandidatesAreRestoredButNotRecorded()
    {
        var characterization = new WriteEncodingCharacterization();
        var restorations = new List<ProtocolWriteEncoding>();

        var exception = await Assert.ThrowsAsync<AdapterProtocolException>(
            () => characterization.SelectAndRecordAsync(
                AdapterOperation.SetOverscan,
                _ => Task.FromResult(false),
                candidate =>
                {
                    restorations.Add(candidate);
                    return Task.CompletedTask;
                }));

        Assert.Contains(nameof(AdapterOperation.SetOverscan), exception.Message);
        Assert.Equal(
            new[]
            {
                ProtocolWriteEncoding.FormUrlEncoded,
                ProtocolWriteEncoding.Json,
                ProtocolWriteEncoding.QueryParameters,
            },
            restorations);
        Assert.Empty(characterization.AcceptedEncodings);
    }

    [Fact]
    public async Task AcceptedEncodingsAreRetainedIndependentlyPerOperation()
    {
        var characterization = new WriteEncodingCharacterization();

        await characterization.SelectAndRecordAsync(
            AdapterOperation.SetDeviceName,
            candidate => Task.FromResult(candidate == ProtocolWriteEncoding.FormUrlEncoded),
            _ => Task.CompletedTask);
        await characterization.SelectAndRecordAsync(
            AdapterOperation.SetOverscan,
            candidate => Task.FromResult(candidate == ProtocolWriteEncoding.Json),
            _ => Task.CompletedTask);

        Assert.Equal(2, characterization.AcceptedEncodings.Count);
        Assert.Equal(
            ProtocolWriteEncoding.FormUrlEncoded,
            characterization.AcceptedEncodings[AdapterOperation.SetDeviceName]);
        Assert.Equal(
            ProtocolWriteEncoding.Json,
            characterization.AcceptedEncodings[AdapterOperation.SetOverscan]);
    }
}
