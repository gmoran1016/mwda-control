using System.Windows.Input;

namespace Mwda.Control.Mvvm;

public sealed class AsyncRelayCommand : ObservableObject, ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private int _isExecuting;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool IsExecuting => Volatile.Read(ref _isExecuting) != 0;

    public bool CanExecute(object? parameter) => !IsExecuting && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter) => await ExecuteAsync();

    public async Task ExecuteAsync()
    {
        if (!CanExecute(null) || Interlocked.CompareExchange(ref _isExecuting, 1, 0) != 0)
        {
            return;
        }

        NotifyExecutionChanged();
        try
        {
            await _execute();
        }
        finally
        {
            Interlocked.Exchange(ref _isExecuting, 0);
            NotifyExecutionChanged();
        }
    }

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private void NotifyExecutionChanged()
    {
        OnPropertyChanged(nameof(IsExecuting));
        NotifyCanExecuteChanged();
    }
}
