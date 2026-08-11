using System.Globalization;
using System.Windows.Controls;
using Mwda.Control.Protocol;

namespace Mwda.Control.Views;

public sealed class DeviceNameValidationRule : ValidationRule
{
    public override ValidationResult Validate(object? value, CultureInfo cultureInfo)
    {
        var text = value as string ?? Convert.ToString(value, cultureInfo);
        return AdapterValidation.IsValidDeviceName(text)
            ? ValidationResult.ValidResult
            : new ValidationResult(
                false,
                "Use at least one letter or number and only adapter-supported name characters.");
    }
}

public sealed class PasswordValidationRule : ValidationRule
{
    private const int MaximumPasswordLength = 128;

    public override ValidationResult Validate(object? value, CultureInfo cultureInfo)
    {
        var text = value as string ?? Convert.ToString(value, cultureInfo) ?? string.Empty;
        return text.Length <= MaximumPasswordLength
            ? ValidationResult.ValidResult
            : new ValidationResult(false, "The password is too long.");
    }
}

public sealed class OverscanValidationRule : ValidationRule
{
    public override ValidationResult Validate(object? value, CultureInfo cultureInfo)
    {
        if (!int.TryParse(Convert.ToString(value, cultureInfo), out var overscan))
        {
            return new ValidationResult(false, "Enter a whole-number overscan value.");
        }

        return overscan is >= AdapterValidation.MinimumOverscanValue and <= AdapterValidation.MaximumOverscanValue
            ? ValidationResult.ValidResult
            : new ValidationResult(
                false,
                $"Enter a value from {AdapterValidation.MinimumOverscanValue} to {AdapterValidation.MaximumOverscanValue}.");
    }
}

public sealed class SsidValidationRule : ValidationRule
{
    public override ValidationResult Validate(object? value, CultureInfo cultureInfo)
    {
        var text = value as string ?? Convert.ToString(value, cultureInfo);
        return string.IsNullOrWhiteSpace(text)
            ? new ValidationResult(false, "Enter a Wi-Fi network name.")
            : ValidationResult.ValidResult;
    }
}
