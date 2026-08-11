using System.ComponentModel;
using Mwda.Control.Mvvm;
using Mwda.Control.Protocol;

namespace Mwda.Control.ViewModels;

public sealed class AboutViewModel : ObservableObject
{
    private readonly DiagnosticsViewModel _diagnostics;

    public AboutViewModel(DiagnosticsViewModel diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        _diagnostics = diagnostics;
        _diagnostics.PropertyChanged += DiagnosticsPropertyChanged;
    }

    public bool IsAvailable => _diagnostics.IsAvailable;

    public AdapterIdentity? Identity => _diagnostics.Identity;

    public CapabilityProfile? Capabilities => _diagnostics.Capabilities;

    public string? AdapterAddress => _diagnostics.AdapterAddress;

    private void DiagnosticsPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(DiagnosticsViewModel.IsAvailable)
            or nameof(DiagnosticsViewModel.Identity)
            or nameof(DiagnosticsViewModel.Capabilities)
            or nameof(DiagnosticsViewModel.AdapterAddress))
        {
            OnPropertyChanged(args.PropertyName);
        }
    }
}
