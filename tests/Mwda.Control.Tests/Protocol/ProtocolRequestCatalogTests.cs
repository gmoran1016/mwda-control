using System.Net.Http.Headers;
using Mwda.Control.Protocol;

namespace Mwda.Control.Tests.Protocol;

public sealed class ProtocolRequestCatalogTests
{
    private static readonly AdapterEndpoint Endpoint =
        new(new Uri("http://192.168.137.247/"));

    public static TheoryData<AdapterOperation, string> ReadRoutes => new()
    {
        {
            AdapterOperation.GetDeviceName,
            "/cgi-bin/msupload.sh?Action=GetDeviceName"
        },
        {
            AdapterOperation.GetOverscan,
            "/cgi-bin/msupload.sh?Action=GetOverscanSetting"
        },
        {
            AdapterOperation.GetPasswordProtection,
            "/cgi-bin/msupload.sh?Action=GetPBCMode"
        },
    };

    public static TheoryData<AdapterOperation, string, AdapterOperation> RecordedCoreSettingsFixture => new()
    {
        {
            AdapterOperation.SetDeviceName,
            "/cgi-bin/msupload.sh?Action=SetDeviceName&NewDeviceName=Room%2BWest",
            AdapterOperation.GetDeviceName
        },
        {
            AdapterOperation.SetOverscan,
            "/cgi-bin/msupload.sh?Action=SetOverscanSetting&IsAutoAdjust=false&OverscanSettingValue=0",
            AdapterOperation.GetOverscan
        },
        {
            AdapterOperation.SetPasswordProtection,
            "/cgi-bin/msupload.sh?Action=SetPBCMode&PBCModeStatus=Disabled",
            AdapterOperation.GetPasswordProtection
        },
    };

    [Theory]
    [MemberData(nameof(ReadRoutes))]
    public void ReadOperationsUseObservedGetRoutes(AdapterOperation operation, string expectedPath)
    {
        using var request = ProtocolRequestCatalog.CreateReadRequest(Endpoint, operation);

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(expectedPath, request.RequestUri!.PathAndQuery);
        Assert.Null(request.Content);
    }

    [Fact]
    public void LegacyPasswordProtectionReadUsesObservedGetRoute()
    {
        using var request = ProtocolRequestCatalog.CreateLegacyPasswordProtectionReadRequest(Endpoint);

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(
            "/cgi-bin/msupload.sh?Action=GetPasswordProtectState",
            request.RequestUri!.PathAndQuery);
        Assert.Null(request.Content);
    }

    [Theory]
    [MemberData(nameof(RecordedCoreSettingsFixture))]
    public void RecordedFixtureUsesQueryEncodedGetWrites(
        AdapterOperation operation,
        string expectedPath,
        AdapterOperation expectedReadBack)
    {
        using var request = CreateRecordedWrite(operation);

        Assert.Equal(ProtocolWriteEncoding.QueryParameters, ProtocolRequestCatalog.GetRecordedWriteEncoding(operation));
        Assert.Equal(expectedReadBack, ProtocolRequestCatalog.GetReadBackOperation(operation));
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(expectedPath, request.RequestUri!.PathAndQuery);
        Assert.Null(request.Content);
    }

    [Fact]
    public void LegacyPasswordProtectionWriteUsesObservedQueryRoute()
    {
        using var request = ProtocolRequestCatalog.CreateLegacySetPasswordProtectionRequest(
            Endpoint,
            enabled: true);

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(
            "/cgi-bin/msupload.sh?Action=SetPasswordProtect&PasswordProtect=true",
            request.RequestUri!.PathAndQuery);
        Assert.Null(request.Content);
    }

    [Fact]
    public void WriteEncoderCandidateSetIsClosedAndOrdered()
    {
        Assert.Equal(
            new[]
            {
                ProtocolWriteEncoding.FormUrlEncoded,
                ProtocolWriteEncoding.Json,
                ProtocolWriteEncoding.QueryParameters,
            },
            ProtocolRequestCatalog.WriteEncodingCandidates);
    }

    [Theory]
    [InlineData(
        ProtocolWriteEncoding.FormUrlEncoded,
        "POST",
        "/cgi-bin/msupload.sh?Action=SetDeviceName",
        "application/x-www-form-urlencoded",
        "NewDeviceName=Room%2BWest")]
    [InlineData(
        ProtocolWriteEncoding.Json,
        "POST",
        "/cgi-bin/msupload.sh?Action=SetDeviceName",
        "application/json",
        "{\"NewDeviceName\":\"Room+West\"}")]
    [InlineData(
        ProtocolWriteEncoding.QueryParameters,
        "GET",
        "/cgi-bin/msupload.sh?Action=SetDeviceName&NewDeviceName=Room%2BWest",
        null,
        null)]
    public async Task DeviceNameWriteSupportsOnlyTheCharacterizationCandidates(
        ProtocolWriteEncoding encoding,
        string expectedMethod,
        string expectedPath,
        string? expectedContentType,
        string? expectedBody)
    {
        using var request = ProtocolRequestCatalog.CreateSetDeviceNameRequest(
            Endpoint,
            "Room+West",
            encoding);

        Assert.Equal(new HttpMethod(expectedMethod), request.Method);
        Assert.Equal(expectedPath, request.RequestUri!.PathAndQuery);
        Assert.Equal(expectedContentType, GetMediaType(request.Content?.Headers.ContentType));
        Assert.Equal(expectedBody, request.Content is null ? null : await request.Content.ReadAsStringAsync());
    }

    private static HttpRequestMessage CreateRecordedWrite(AdapterOperation operation) => operation switch
    {
        AdapterOperation.SetDeviceName =>
            ProtocolRequestCatalog.CreateSetDeviceNameRequest(Endpoint, "Room+West"),
        AdapterOperation.SetOverscan =>
            ProtocolRequestCatalog.CreateSetOverscanRequest(Endpoint, new OverscanSettings(false, 0)),
        AdapterOperation.SetPasswordProtection =>
            ProtocolRequestCatalog.CreateSetPasswordProtectionRequest(Endpoint, enabled: true),
        _ => throw new InvalidOperationException($"Unsupported fixture operation: {operation}"),
    };

    private static string? GetMediaType(MediaTypeHeaderValue? contentType) => contentType?.MediaType;
}
