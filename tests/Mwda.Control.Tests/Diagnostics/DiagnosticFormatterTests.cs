using Mwda.Control.Diagnostics;

namespace Mwda.Control.Tests.Diagnostics;

public sealed class DiagnosticFormatterTests
{
    [Fact]
    public void FormatterRedactsSecrets()
    {
        var text = DiagnosticFormatter.Format(new DiagnosticSnapshot(
            "192.168.137.247",
            "WeightRoom-AD",
            "secret-pin",
            "secret-password"));

        Assert.DoesNotContain("secret-pin", text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-password", text, StringComparison.Ordinal);
        Assert.Contains("192.168.137.247", text, StringComparison.Ordinal);
        Assert.Contains("WeightRoom-AD", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatterIncludesTypedConnectionDetails()
    {
        var text = DiagnosticFormatter.Format(new DiagnosticSnapshot(
            endpoint: "http://192.168.137.247/",
            connectionState: "Connected",
            capabilities: new[] { "SetOverscan", "GetDeviceName" },
            recentOperationStatus: "Overscan read-back succeeded",
            pin: "secret-pin",
            password: "secret-password"));

        Assert.Contains("Endpoint: http://192.168.137.247/", text, StringComparison.Ordinal);
        Assert.Contains("Connection state: Connected", text, StringComparison.Ordinal);
        Assert.Contains("Capabilities: GetDeviceName, SetOverscan", text, StringComparison.Ordinal);
        Assert.Contains("Recent operation: Overscan read-back succeeded", text, StringComparison.Ordinal);
        Assert.Contains("PIN/password: [redacted]", text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-pin", text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-password", text, StringComparison.Ordinal);
    }
}
