using System.Collections.ObjectModel;
using Mwda.Control.Protocol;

namespace Mwda.Control.Diagnostics;

public sealed record DiagnosticSnapshot
{
    public DiagnosticSnapshot(
        string endpoint,
        string adapterName,
        string? pin,
        string? password,
        string? localError = null)
    {
        Endpoint = RequireText(endpoint, nameof(endpoint));
        AdapterName = RequireText(adapterName, nameof(adapterName));
        Pin = pin;
        Password = password;
        LocalError = localError;
        ConnectionState = "Unknown";
        Capabilities = Array.Empty<string>();
        RecentOperationStatus = "None recorded";
    }

    public DiagnosticSnapshot(
        string endpoint,
        string connectionState,
        IEnumerable<string> capabilities,
        string recentOperationStatus,
        string? pin = null,
        string? password = null,
        string? localError = null)
        : this(endpoint, "Unavailable", pin, password, localError)
    {
        ConnectionState = RequireText(connectionState, nameof(connectionState));
        Capabilities = NormalizeCapabilities(capabilities);
        RecentOperationStatus = RequireText(
            recentOperationStatus,
            nameof(recentOperationStatus));
    }

    public DiagnosticSnapshot(
        string endpoint,
        string adapterName,
        string? pin,
        string? password,
        string connectionState,
        IEnumerable<string> capabilities,
        string recentOperationStatus,
        string? localError = null)
        : this(endpoint, adapterName, pin, password, localError)
    {
        ConnectionState = RequireText(connectionState, nameof(connectionState));
        Capabilities = NormalizeCapabilities(capabilities);
        RecentOperationStatus = RequireText(
            recentOperationStatus,
            nameof(recentOperationStatus));
    }

    public DiagnosticSnapshot(
        string endpoint,
        string connectionState,
        IEnumerable<AdapterOperation> capabilities,
        string recentOperationStatus,
        string? pin = null,
        string? password = null,
        string? localError = null)
        : this(
            endpoint,
            connectionState,
            capabilities.Select(operation => operation.ToString()),
            recentOperationStatus,
            pin,
            password,
            localError)
    {
    }

    public string Endpoint { get; }

    public string AdapterName { get; }

    public string? Pin { get; }

    public string? Password { get; }

    public string? LocalError { get; }

    public string ConnectionState { get; }

    public IReadOnlyList<string> Capabilities { get; }

    public string RecentOperationStatus { get; }

    private static IReadOnlyList<string> NormalizeCapabilities(
        IEnumerable<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        var normalized = capabilities
            .Select((capability, index) => RequireText(capability, $"capabilities[{index}]"))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new ReadOnlyCollection<string>(normalized);
    }

    private static string RequireText(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value;
    }
}
