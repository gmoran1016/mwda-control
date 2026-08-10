using Mwda.Control.Protocol;

namespace Mwda.Control.Tests.Protocol;

public sealed class ProtocolJsonTests
{
    [Fact]
    public void ParsesObservedDeviceIdentity()
    {
        var identity = ProtocolJson.ParseIdentity("""{"DeviceName":"WeightRoom-AD"}""");

        Assert.Equal("WeightRoom-AD", identity.DeviceName);
    }

    [Fact]
    public void ParsesObservedOverscanSettings()
    {
        var settings = ProtocolJson.ParseOverscan("""{"IsAutoAdjust":false,"OverscanSettingValue":0}""");

        Assert.False(settings.IsAutoAdjust);
        Assert.Equal(0, settings.Value);
    }

    [Fact]
    public void ParsesObservedPasswordProtectionSettings()
    {
        var settings = ProtocolJson.ParsePasswordProtection("""{"PasswordProtect":false}""");

        Assert.False(settings.Enabled);
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("overscan")]
    [InlineData("password-protection")]
    public void MissingRequiredPropertiesThrowAdapterProtocolException(string responseType)
    {
        Action parse = responseType switch
        {
            "identity" => () => ProtocolJson.ParseIdentity("{}"),
            "overscan" => () => ProtocolJson.ParseOverscan("{}"),
            "password-protection" => () => ProtocolJson.ParsePasswordProtection("{}"),
            _ => throw new InvalidOperationException($"Unknown response type: {responseType}"),
        };

        Assert.Throws<AdapterProtocolException>(parse);
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("overscan")]
    [InlineData("password-protection")]
    public void NonJsonBodiesThrowAdapterProtocolException(string responseType)
    {
        Action parse = responseType switch
        {
            "identity" => () => ProtocolJson.ParseIdentity("not-json"),
            "overscan" => () => ProtocolJson.ParseOverscan("not-json"),
            "password-protection" => () => ProtocolJson.ParsePasswordProtection("not-json"),
            _ => throw new InvalidOperationException($"Unknown response type: {responseType}"),
        };

        Assert.Throws<AdapterProtocolException>(parse);
    }
}
