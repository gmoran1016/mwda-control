namespace Mwda.Control.Protocol;

public interface IWirelessDisplayAdapterClient
{
    Task<AdapterIdentity> GetIdentityAsync(CancellationToken cancellationToken = default);

    Task<OverscanSettings> GetOverscanAsync(CancellationToken cancellationToken = default);

    Task SetOverscanAsync(OverscanSettings settings, CancellationToken cancellationToken = default);

    Task<PasswordProtectionSettings> GetPasswordProtectionAsync(CancellationToken cancellationToken = default);

    Task SetPasswordProtectionAsync(
        bool enabled,
        string? password,
        CancellationToken cancellationToken = default);

    Task SetDeviceNameAsync(string deviceName, CancellationToken cancellationToken = default);

    Task<CapabilityProfile> DetectCapabilitiesAsync(CancellationToken cancellationToken = default);
}
