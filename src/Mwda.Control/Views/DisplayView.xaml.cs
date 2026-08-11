using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Mwda.Control.ViewModels;

namespace Mwda.Control.Views;

public partial class DisplayView : UserControl
{
    public DisplayView()
    {
        InitializeComponent();
    }

    private async void UploadCustomWallpaperClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DisplaySettingsViewModel viewModel)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "Image files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png",
            CheckFileExists = true,
            Multiselect = false,
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }

        var contentType = Path.GetExtension(dialog.FileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => null,
        };
        if (contentType is null)
        {
            return;
        }

        using var image = File.OpenRead(dialog.FileName);
        await viewModel.UploadCustomWallpaperAsync(
            image,
            Path.GetFileName(dialog.FileName),
            contentType);
    }
}
