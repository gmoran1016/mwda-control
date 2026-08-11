using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Mwda.Control.ViewModels;

namespace Mwda.Control.Views;

public partial class ConnectionView : UserControl
{
    public ConnectionView()
    {
        InitializeComponent();
    }

    private void OpenWirelessDisplaySettingsClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ConnectionSettingsViewModel viewModel)
        {
            return;
        }

        Process.Start(
            new ProcessStartInfo(viewModel.WindowsWirelessDisplaySettingsUri.AbsoluteUri)
            {
                UseShellExecute = true,
            });
    }
}
