using Mwda.Control.ViewModels;
using Mwda.Control.Views;

namespace Mwda.Control.Tests.Views;

public sealed class DiagnosticsViewTests
{
    [Fact]
    public void CopyDiagnosticsFormattingOmitsLocalErrorWhenNoAdapterIsConnected()
    {
        var diagnostics = new DiagnosticsViewModel();
        diagnostics.RecordError("secret-password-from-adapter-error");

        var formatted = DiagnosticsView.BuildDiagnosticsText(diagnostics, shell: null);

        Assert.DoesNotContain("secret-password-from-adapter-error", formatted, StringComparison.Ordinal);
        Assert.Contains("Endpoint: Unavailable", formatted, StringComparison.Ordinal);
        Assert.Contains("Connection state: Unavailable", formatted, StringComparison.Ordinal);
        Assert.Contains("Capabilities: None reported", formatted, StringComparison.Ordinal);
        Assert.Contains("Recent operation: Local error recorded", formatted, StringComparison.Ordinal);
        Assert.Contains("Local error: [redacted]", formatted, StringComparison.Ordinal);
    }
}
