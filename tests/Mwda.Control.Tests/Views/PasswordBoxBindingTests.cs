using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using Mwda.Control.Mvvm;
using Mwda.Control.Views;

namespace Mwda.Control.Tests.Views;

public sealed class PasswordBoxBindingTests
{
    [Fact]
    public void UserTextUpdatesBoundPropertyWhenInitialValueIsNull()
    {
        string? observedPassword = null;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var holder = new PasswordHolder();
                var passwordBox = new PasswordBox();
                BindingOperations.SetBinding(
                    passwordBox,
                    PasswordBoxBinding.PasswordProperty,
                    new Binding(nameof(PasswordHolder.Password))
                    {
                        Source = holder,
                        Mode = System.Windows.Data.BindingMode.TwoWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                    });

                passwordBox.Password = "entered-by-user";
                observedPassword = holder.Password;
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }

        Assert.Equal("entered-by-user", observedPassword);
    }

    private sealed class PasswordHolder : ObservableObject
    {
        private string? _password;

        public string? Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }
    }
}
