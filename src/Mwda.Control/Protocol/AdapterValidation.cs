namespace Mwda.Control.Protocol;

public static class AdapterValidation
{
    public const int MinimumOverscanValue = 0;
    public const int MaximumOverscanValue = 15;

    public static bool IsValidDeviceName(string? value) =>
        !string.IsNullOrEmpty(value) && value.All(IsAllowedDeviceNameCharacter);

    public static OverscanSettings CreateOverscan(bool isAutoAdjust, int value)
    {
        if (value is < MinimumOverscanValue or > MaximumOverscanValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"Overscan must be between {MinimumOverscanValue} and {MaximumOverscanValue}.");
        }

        return new OverscanSettings(isAutoAdjust, value);
    }

    private static bool IsAllowedDeviceNameCharacter(char value) =>
        char.IsAsciiLetterOrDigit(value)
        || value is '-' or '_' or '+' or '(' or ')' or '[' or ']' or '{' or '}';
}
