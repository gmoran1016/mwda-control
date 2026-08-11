using System.Text;
using System.Windows;
using System.Windows.Controls;
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
        var supportedOperations = diagnostics.Capabilities?.SupportedOperations is { } capabilities
            ? string.Join(", ", capabilities)
            : "Unavailable";
        var text = new StringBuilder()
            .AppendLine($"Adapter: {diagnostics.Identity?.DeviceName ?? "Unavailable"}")
            .AppendLine($"Address: {diagnostics.AdapterAddress ?? "Unavailable"}")
            .AppendLine($"Connection: {shell?.Connection.ConnectionState ?? "Unavailable"}")
            .AppendLine($"Last result: {shell?.Connection.ResultBanner ?? "Unavailable"}")
            .AppendLine($"Supported controls: {supportedOperations}")
            .AppendLine($"Local error: {diagnostics.LastError ?? "None"}")
            .ToString();

        Clipboard.SetText(text);
    }
}
