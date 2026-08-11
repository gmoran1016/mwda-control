using Mwda.Control.Protocol;

namespace Mwda.Control.Tests.Protocol;

public sealed class AdapterValidationTests
{
    [Theory]
    [InlineData("WeightRoom-AD")]
    [InlineData("Room_2+(West)")]
    [InlineData("Room[2]{West}")]
    public void ValidDeviceNameIsAccepted(string value) =>
        Assert.True(AdapterValidation.IsValidDeviceName(value));

    [Theory]
    [InlineData("Room West")]
    [InlineData("")]
    public void InvalidDeviceNameIsRejected(string value) =>
        Assert.False(AdapterValidation.IsValidDeviceName(value));

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    public void OverscanAcceptsTheLegacyAdapterRange(int value)
    {
        var settings = AdapterValidation.CreateOverscan(isAutoAdjust: false, value);

        Assert.Equal(value, settings.Value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(16)]
    public void OverscanRejectsValuesOutsideTheLegacyAdapterRange(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AdapterValidation.CreateOverscan(isAutoAdjust: false, value));
    }
}
