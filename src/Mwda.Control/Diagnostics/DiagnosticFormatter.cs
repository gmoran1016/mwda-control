using System.Text;

namespace Mwda.Control.Diagnostics;

public static class DiagnosticFormatter
{
    public static string Format(DiagnosticSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var capabilities = snapshot.Capabilities.Count == 0
            ? "None reported"
            : string.Join(", ", snapshot.Capabilities);

        return new StringBuilder()
            .AppendLine("MWDA Control diagnostics")
            .AppendLine($"Endpoint: {snapshot.Endpoint}")
            .AppendLine($"Adapter: {snapshot.AdapterName}")
            .AppendLine($"Connection state: {snapshot.ConnectionState}")
            .AppendLine($"Capabilities: {capabilities}")
            .AppendLine($"Recent operation: {snapshot.RecentOperationStatus}")
            .AppendLine("PIN/password: [redacted]")
            .ToString();
    }
}
