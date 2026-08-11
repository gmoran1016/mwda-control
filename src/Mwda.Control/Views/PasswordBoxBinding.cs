using System.Windows;
using System.Windows.Controls;

namespace Mwda.Control.Views;

public static class PasswordBoxBinding
{
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

        passwordBox.PasswordChanged -= PasswordBoxOnPasswordChanged;
        passwordBox.PasswordChanged += PasswordBoxOnPasswordChanged;

        if (GetIsUpdating(passwordBox))
        {
            return;
        }

        SetIsUpdating(passwordBox, true);
        passwordBox.Password = e.NewValue as string ?? string.Empty;
        SetIsUpdating(passwordBox, false);
    }

    private static void PasswordBoxOnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not PasswordBox passwordBox || GetIsUpdating(passwordBox))
        {
            return;
        }

        SetIsUpdating(passwordBox, true);
        SetPassword(passwordBox, string.IsNullOrEmpty(passwordBox.Password) ? null : passwordBox.Password);
        SetIsUpdating(passwordBox, false);
    }
}
