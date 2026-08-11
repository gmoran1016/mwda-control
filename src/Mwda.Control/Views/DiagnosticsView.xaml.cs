using System.Windows;
using System.Windows.Controls;
using Mwda.Control.Diagnostics;
using Mwda.Control.ViewModels;

namespace Mwda.Control.Views;

public partial class DiagnosticsView : UserControl
{
    public DiagnosticsView()
    {
        InitializeComponent();
    }

    private void CopyDiagnosticsClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DiagnosticsViewModel diagnostics)
        {
            return;
        }

        var shell = Window.GetWindow(this)?.DataContext as MainWindowViewModel;
        Clipboard.SetText(BuildDiagnosticsText(diagnostics, shell));
    }

    public static string BuildDiagnosticsText(
        DiagnosticsViewModel diagnostics,
        MainWindowViewModel? shell)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        var capabilities = diagnostics.Capabilities?.SupportedOperations
            .Select(operation => operation.ToString())
            ?? Array.Empty<string>();
        var snapshot = new DiagnosticSnapshot(
            endpoint: diagnostics.AdapterAddress ?? "Unavailable",
            adapterName: diagnostics.Identity?.DeviceName ?? "Unavailable",
            pin: null,
            password: null,
            connectionState: shell?.Connection.ConnectionState ?? "Unavailable",
            capabilities: capabilities,
            recentOperationStatus: SummarizeRecentOperation(
                shell?.Connection.ResultBanner,
                diagnostics.LastError),
            localError: diagnostics.LastError);

        return DiagnosticFormatter.Format(snapshot);
    }

    private static string SummarizeRecentOperation(
        string? resultBanner,
        string? localError)
    {
        if (string.IsNullOrWhiteSpace(resultBanner))
        {
            return localError is null ? "None recorded" : "Local error recorded";
        }

        if (resultBanner.Equals("Applied.", StringComparison.Ordinal))
        {
            return "Applied";
        }

        if (resultBanner.StartsWith("Connected to ", StringComparison.Ordinal))
        {
            return "Connected";
        }

        if (resultBanner.StartsWith("No adapter was found.", StringComparison.Ordinal))
        {
            return "No adapter found";
        }

        if (resultBanner.StartsWith("Adapter not reachable;", StringComparison.Ordinal))
        {
            return "Adapter not reachable";
        }

        if (resultBanner.StartsWith("Disconnected.", StringComparison.Ordinal))
        {
            return "Disconnected";
        }

        if (resultBanner.Equals("Operation cancelled.", StringComparison.Ordinal))
        {
            return "Operation cancelled";
        }

        return "Result recorded";
    }
}
