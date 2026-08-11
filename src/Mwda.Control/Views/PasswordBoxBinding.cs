using System.Windows;
using System.Windows.Controls;

namespace Mwda.Control.Views;

public static class PasswordBoxBinding
{
    static PasswordBoxBinding()
    {
        EventManager.RegisterClassHandler(
            typeof(PasswordBox),
            PasswordBox.PasswordChangedEvent,
            new RoutedEventHandler(PasswordBoxOnPasswordChanged));
    }

    public static readonly DependencyProperty PasswordProperty =
        DependencyProperty.RegisterAttached(
            "Password",
            typeof(string),
            typeof(PasswordBoxBinding),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnPasswordChanged));

    private static readonly DependencyProperty IsUpdatingProperty =
        DependencyProperty.RegisterAttached(
            "IsUpdating",
            typeof(bool),
            typeof(PasswordBoxBinding),
            new PropertyMetadata(false));

    public static string? GetPassword(DependencyObject target) =>
        (string?)target.GetValue(PasswordProperty);

    public static void SetPassword(DependencyObject target, string? value) =>
        target.SetValue(PasswordProperty, value);

    private static bool GetIsUpdating(DependencyObject target) =>
        (bool)target.GetValue(IsUpdatingProperty);

    private static void SetIsUpdating(DependencyObject target, bool value) =>
        target.SetValue(IsUpdatingProperty, value);

    private static void OnPasswordChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not PasswordBox passwordBox)
        {
            return;
        }

        if (GetIsUpdating(passwordBox))
        {
            return;
        }

        SetIsUpdating(passwordBox, true);
        try
        {
            passwordBox.Password = e.NewValue as string ?? string.Empty;
        }
        finally
        {
            SetIsUpdating(passwordBox, false);
        }
    }

    private static void PasswordBoxOnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not PasswordBox passwordBox ||
            passwordBox.ReadLocalValue(PasswordProperty) == DependencyProperty.UnsetValue ||
            GetIsUpdating(passwordBox))
        {
            return;
        }

        SetIsUpdating(passwordBox, true);
        try
        {
            SetPassword(passwordBox, string.IsNullOrEmpty(passwordBox.Password) ? null : passwordBox.Password);
        }
        finally
        {
            SetIsUpdating(passwordBox, false);
        }
    }
}
