# MWDA Control Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Build and verify a self-contained Windows 10/11 desktop utility that configures the connected Microsoft Wireless Display Adapter through its local control protocol while containing no firmware-update workflow.

**Architecture:** A .NET 8 WPF executable uses a capability-driven MVVM shell. AdapterDiscovery finds Wi-Fi Direct candidates, IWirelessDisplayAdapterClient owns all adapter HTTP/HTTPS traffic, and typed view models expose only operations supported by the selected adapter. A fake HTTP handler and opt-in live tests keep protocol behavior verifiable without making the UI depend on the wire format.

**Tech Stack:** C#/.NET 8, WPF, System.Net.Http, System.Text.Json, xUnit, self-contained win-x64 publish.

## Global Constraints

- Use .NET 8 WPF targeting Windows 10/11 x64.
- Publish a self-contained single-file executable.
- The app does not require administrator rights for normal operation.
- The app is offline-first and sends no telemetry or analytics.
- Firmware version downloads, firmware update checks, firmware upload/flashing/signing, and automatic-update controls are absent from the product and protocol API.
- The UI never constructs adapter URLs or request bodies.
- Adapter passwords/PINs are not persisted.
- Every adapter write is cancellable, serialized per adapter, and read-back verified where supported.
- Live adapter writes are opt-in, reversible, and restore the original value in finally cleanup.

## Repository layout

The implementation uses the following focused units:

~~~text
MWDA.Control.sln
Directory.Build.props
global.json
src/Mwda.Control/
  App.xaml
  App.xaml.cs
  MainWindow.xaml
  MainWindow.xaml.cs
  Mvvm/
  Protocol/
  Discovery/
  Session/
  ViewModels/
  Views/
tests/Mwda.Control.Tests/
tests/Mwda.Control.IntegrationTests/
README.md
publish.ps1
~~~

Protocol/ owns typed adapter requests/responses and must not reference WPF. Discovery/ owns network-interface/address discovery. Session/ combines a discovered endpoint with its capability profile. ViewModels/ owns application state and commands. Views/ contains only WPF layout and binding declarations. Unit tests use fake transports; integration tests are disabled unless explicitly enabled.

---

### Task 1: Bootstrap the solution and test runner

**Files:**
- Create: global.json
- Create: Directory.Build.props
- Create: .gitignore
- Create: MWDA.Control.sln
- Create: src/Mwda.Control/Mwda.Control.csproj
- Create: tests/Mwda.Control.Tests/Mwda.Control.Tests.csproj
- Create: tests/Mwda.Control.IntegrationTests/Mwda.Control.IntegrationTests.csproj
- Create: tests/Mwda.Control.Tests/SmokeTests.cs

**Interfaces:**
- Produces a buildable WPF project at src/Mwda.Control/Mwda.Control.csproj and an xUnit unit-test project that later tasks reference.

- [ ] **Step 1: Install or select the .NET 8 SDK**

The machine currently has the .NET 8 runtime but no SDK. From the repository root, install an SDK into the repository-local tool directory if dotnet --list-sdks is empty:

~~~powershell
$repoRoot = (Get-Location).Path
$sdkRoot = Join-Path $repoRoot ".tools\dotnet"
$installer = Join-Path $env:TEMP "dotnet-install-mwda.ps1"
Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $installer
powershell -NoProfile -ExecutionPolicy Bypass -File $installer -Channel 8.0 -Quality ga -InstallDir $sdkRoot
$env:PATH = "$sdkRoot;$env:PATH"
dotnet --list-sdks
~~~

Expected: one 8.0 SDK is listed. Do not add .tools/ to Git.

- [ ] **Step 2: Create the solution and projects**

Run:

~~~powershell
dotnet new sln -n MWDA.Control
dotnet new wpf -n Mwda.Control -o src/Mwda.Control -f net8.0-windows
dotnet new xunit -n Mwda.Control.Tests -o tests/Mwda.Control.Tests -f net8.0
dotnet new xunit -n Mwda.Control.IntegrationTests -o tests/Mwda.Control.IntegrationTests -f net8.0

dotnet sln .\MWDA.Control.sln add .\src\Mwda.Control\Mwda.Control.csproj
dotnet sln .\MWDA.Control.sln add .\tests\Mwda.Control.Tests\Mwda.Control.Tests.csproj
dotnet sln .\MWDA.Control.sln add .\tests\Mwda.Control.IntegrationTests\Mwda.Control.IntegrationTests.csproj
~~~

Set the app project to TargetFramework=net8.0-windows, UseWPF=true, Nullable=enable, ImplicitUsings=enable, PublishSingleFile=true, and PublishTrimmed=false. Add project references from both test projects to the app project. Keep production dependencies limited to the .NET base class libraries.

- [ ] **Step 3: Add repository-wide build settings and a smoke test**

Directory.Build.props must set TreatWarningsAsErrors=true, AnalysisLevel=latest, Nullable=enable, and ImplicitUsings=enable. SmokeTests.cs must contain:

~~~csharp
namespace Mwda.Control.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void TestAssemblyLoads()
    {
        Assert.NotNull(typeof(Mwda.Control.App).Assembly);
    }
}
~~~

- [ ] **Step 4: Run the initial test cycle**

Run:

~~~powershell
dotnet restore .\MWDA.Control.sln
dotnet test .\MWDA.Control.sln --configuration Release --no-restore
~~~

Expected: restore succeeds and the smoke test passes.

- [ ] **Step 5: Commit the bootstrap**

~~~powershell
git add global.json Directory.Build.props .gitignore MWDA.Control.sln src tests
git commit -m "build: bootstrap MWDA Control solution"
~~~

### Task 2: Define protocol models, validation, and capability contracts

**Files:**
- Create: src/Mwda.Control/Protocol/AdapterOperation.cs
- Create: src/Mwda.Control/Protocol/AdapterGeneration.cs
- Create: src/Mwda.Control/Protocol/AdapterModels.cs
- Create: src/Mwda.Control/Protocol/AdapterProtocolException.cs
- Create: src/Mwda.Control/Protocol/AdapterValidation.cs
- Create: src/Mwda.Control/Protocol/ProtocolJson.cs
- Create: src/Mwda.Control/Protocol/IWirelessDisplayAdapterClient.cs
- Create: src/Mwda.Control/Protocol/IAdvancedWirelessDisplayAdapterClient.cs
- Create: tests/Mwda.Control.Tests/Protocol/AdapterValidationTests.cs
- Create: tests/Mwda.Control.Tests/Protocol/ProtocolJsonTests.cs

**Interfaces:**
- Produces AdapterEndpoint, AdapterIdentity, OverscanSettings, PasswordProtectionSettings, WallpaperInfo, WifiSettings, HdcpSettings, LanguageInfo, CapabilityProfile, and IWirelessDisplayAdapterClient for Tasks 3–7.

- [ ] **Step 1: Write failing validation and model tests**

Add tests for the exact rules below:

~~~csharp
[Theory]
[InlineData("WeightRoom-AD")]
[InlineData("Room_2+(West)")]
public void ValidDeviceNameIsAccepted(string value) =>
    Assert.True(AdapterValidation.IsValidDeviceName(value));

[Theory]
[InlineData("Room West")]
[InlineData("")]
public void InvalidDeviceNameIsRejected(string value) =>
    Assert.False(AdapterValidation.IsValidDeviceName(value));

[Fact]
public void OverscanMustBeWithinAdapterRange()
{
    Assert.Throws<ArgumentOutOfRangeException>(() =>
        AdapterValidation.CreateOverscan(isAutoAdjust: false, value: -1));
}
~~~

ProtocolJsonTests must parse the three live responses already observed:

~~~json
{"DeviceName":"WeightRoom-AD"}
{"IsAutoAdjust":false,"OverscanSettingValue":0}
{"PasswordProtect":false}
~~~

The tests must also assert that missing required properties and non-JSON bodies throw AdapterProtocolException.

- [ ] **Step 2: Run the focused tests to verify failure**

~~~powershell
dotnet test .\tests\Mwda.Control.Tests\Mwda.Control.Tests.csproj --filter "FullyQualifiedName~AdapterValidationTests|FullyQualifiedName~ProtocolJsonTests"
~~~

Expected: FAIL because the protocol types and parsers do not exist.

- [ ] **Step 3: Implement the typed contracts**

Define AdapterOperation with exactly these values: GetDeviceName, SetDeviceName, GetOverscan, SetOverscan, GetPasswordProtection, SetPasswordProtection, ChangePassword, GetWallpaperInfo, SetWallpaper, GetWiFiSettings, SetWiFiSettings, ForgetWiFi, GetHdcpStatus, SetHdcpStatus, GetLanguage, SetLanguage, and Restart.

Define IWirelessDisplayAdapterClient with these signatures:

~~~csharp
Task<AdapterIdentity> GetIdentityAsync(CancellationToken cancellationToken = default);
Task<OverscanSettings> GetOverscanAsync(CancellationToken cancellationToken = default);
Task SetOverscanAsync(OverscanSettings settings, CancellationToken cancellationToken = default);
Task<PasswordProtectionSettings> GetPasswordProtectionAsync(CancellationToken cancellationToken = default);
Task SetPasswordProtectionAsync(bool enabled, string? password, CancellationToken cancellationToken = default);
Task SetDeviceNameAsync(string deviceName, CancellationToken cancellationToken = default);
Task<CapabilityProfile> DetectCapabilitiesAsync(CancellationToken cancellationToken = default);
~~~

Keep optional-operation methods in a separate IAdvancedWirelessDisplayAdapterClient so unsupported operations cannot be called through the basic session accidentally. Do not define a firmware operation or firmware-related enum value.

IAdvancedWirelessDisplayAdapterClient must expose these signatures:

~~~csharp
Task<WallpaperInfo> GetWallpaperInfoAsync(CancellationToken cancellationToken = default);
Task SetPredefinedWallpaperAsync(string wallpaperId, CancellationToken cancellationToken = default);
Task UploadCustomWallpaperAsync(Stream image, string fileName, string contentType, CancellationToken cancellationToken = default);
Task<WifiSettings> GetWiFiSettingsAsync(CancellationToken cancellationToken = default);
Task SetWiFiSettingsAsync(WifiSettings settings, CancellationToken cancellationToken = default);
Task ForgetWiFiAsync(CancellationToken cancellationToken = default);
Task<HdcpSettings> GetHdcpStatusAsync(CancellationToken cancellationToken = default);
Task SetHdcpStatusAsync(bool enabled, CancellationToken cancellationToken = default);
Task<LanguageInfo> GetLanguageAsync(CancellationToken cancellationToken = default);
Task SetLanguageAsync(string languageTag, CancellationToken cancellationToken = default);
Task RestartAsync(CancellationToken cancellationToken = default);
~~~

- [ ] **Step 4: Run the focused tests to verify success**

~~~powershell
dotnet test .\tests\Mwda.Control.Tests\Mwda.Control.Tests.csproj --configuration Release --filter "FullyQualifiedName~AdapterValidationTests|FullyQualifiedName~ProtocolJsonTests"
~~~

Expected: all validation and JSON tests pass.

- [ ] **Step 5: Commit the contracts**

~~~powershell
git add src/Mwda.Control/Protocol tests/Mwda.Control.Tests/Protocol
git commit -m "feat: define adapter protocol contracts"
~~~

### Task 3: Implement HTTP transport and Wi-Fi Direct discovery

**Files:**
- Create: src/Mwda.Control/Protocol/AdapterHttpTransport.cs
- Create: src/Mwda.Control/Discovery/DiscoveryOptions.cs
- Create: src/Mwda.Control/Discovery/DiscoveredAdapter.cs
- Create: src/Mwda.Control/Discovery/AdapterDiscovery.cs
- Create: src/Mwda.Control/Discovery/INetworkCandidateSource.cs
- Create: src/Mwda.Control/Discovery/IAdapterDiscovery.cs
- Create: tests/Mwda.Control.Tests/Protocol/AdapterHttpTransportTests.cs
- Create: tests/Mwda.Control.Tests/Discovery/AdapterDiscoveryTests.cs

**Interfaces:**
- AdapterHttpTransport sends typed requests with timeout and cancellation.
- AdapterDiscovery.DiscoverAsync(CancellationToken) returns IReadOnlyList<DiscoveredAdapter>.
- INetworkCandidateSource.GetCandidates() allows deterministic unit tests without scanning the real machine.
- IAdapterDiscovery.DiscoverAsync(CancellationToken) returns the discovered adapter list used by the session factory.

- [ ] **Step 1: Write failing transport tests**

Use a fake HttpMessageHandler to assert that a 200 JSON response returns body, content type, and status; a timeout produces AdapterProtocolException with operation context; and a 404 remains distinguishable from a transport failure.

~~~csharp
[Fact]
public async Task TransportPreservesStatusAndBody()
{
    using var handler = new StubHttpMessageHandler(
        new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"DeviceName":"WeightRoom-AD"}""",
                Encoding.UTF8,
                "text/html")
        });
    using var transport = new AdapterHttpTransport(handler, TimeSpan.FromSeconds(2));

    var response = await transport.GetAsync(new Uri("http://192.168.137.247/test"));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Contains("WeightRoom-AD", response.Body);
}
~~~

- [ ] **Step 2: Write failing discovery tests**

Test that discovery:

1. rejects non-Wi-Fi-Direct interfaces;
2. probes each candidate with /cgi-bin/msupload.sh?Action=GetDeviceName;
3. accepts only a valid DeviceName JSON response;
4. returns the adapter IP and interface alias; and
5. limits concurrent probes to DiscoveryOptions.MaxConcurrentProbes.

- [ ] **Step 3: Run focused tests to verify failure**

~~~powershell
dotnet test .\tests\Mwda.Control.Tests\Mwda.Control.Tests.csproj --filter "FullyQualifiedName~AdapterHttpTransportTests|FullyQualifiedName~AdapterDiscoveryTests"
~~~

Expected: FAIL because transport and discovery are not implemented.

- [ ] **Step 4: Implement the transport**

Use a private HttpClient created from SocketsHttpHandler with ConnectTimeout, UseProxy=false, decompression enabled, and a per-request cancellation token. GetAsync must not throw for a normal HTTP error; it returns a typed AdapterHttpResponse so capability detection can distinguish 404/501 from connection failure. Never use a global certificate-validation bypass. The initial base address is http://<adapter-ip>/; HTTPS support is added only when the adapter profile requires it.

- [ ] **Step 5: Implement discovery**

Enumerate NetworkInterface.GetAllNetworkInterfaces(), keep interfaces that are up and either have a description containing Wi-Fi Direct Virtual Adapter or an IPv4 address in a private Wi-Fi Direct subnet, then produce candidates from:

- the app's last-known endpoint;
- IPv4 neighbors returned by the injected INetworkCandidateSource; and
- the host portion .2 through .254 of each qualifying /24 subnet, with MaxConcurrentProbes=24.

Probe candidates with a 750 ms cancellation timeout and the verified GetDeviceName path. Deduplicate by IP and return results ordered by last-known match, then response time, then IP address. Do not shell out to PowerShell or rely on the Microsoft Store app.

- [ ] **Step 6: Run focused tests to verify success**

~~~powershell
dotnet test .\tests\Mwda.Control.Tests\Mwda.Control.Tests.csproj --configuration Release --filter "FullyQualifiedName~AdapterHttpTransportTests|FullyQualifiedName~AdapterDiscoveryTests"
~~~

Expected: all transport and discovery tests pass.

- [ ] **Step 7: Commit transport and discovery**

~~~powershell
git add src/Mwda.Control/Protocol src/Mwda.Control/Discovery tests/Mwda.Control.Tests/Protocol tests/Mwda.Control.Tests/Discovery
git commit -m "feat: discover adapters over Wi-Fi Direct"
~~~

### Task 4: Implement and characterize core adapter settings

**Files:**
- Create: src/Mwda.Control/Protocol/ProtocolRequestCatalog.cs
- Create: src/Mwda.Control/Protocol/AdapterClient.cs
- Create: tests/Mwda.Control.Tests/Protocol/AdapterClientTests.cs
- Create: tests/Mwda.Control.Tests/Protocol/ProtocolRequestCatalogTests.cs
- Create: tests/Mwda.Control.IntegrationTests/LiveAdapterFixture.cs
- Create: tests/Mwda.Control.IntegrationTests/CoreSettingsLiveTests.cs

**Interfaces:**
- AdapterClient implements IWirelessDisplayAdapterClient against a discovered AdapterEndpoint.
- ProtocolRequestCatalog maps each typed operation to action name, method, content encoding, and read-back operation.
- LiveAdapterFixture provides a client only when MWDA_RUN_LIVE_TESTS=1 and MWDA_ADAPTER_IP are present.

- [ ] **Step 1: Write deterministic request-catalog tests**

Assert the read routes exactly:

~~~text
GET /cgi-bin/msupload.sh?Action=GetDeviceName
GET /cgi-bin/msupload.sh?Action=GetOverscanSetting
GET /cgi-bin/msupload.sh?Action=GetPasswordProtectState
~~~

For writes, define a closed candidate encoder set in ProtocolRequestCatalog: form-urlencoded fields, JSON object body, and query parameters. The catalog test must select one encoder per action from a recorded fixture and assert the exact method, path, content type, and body. This keeps any device-specific encoding decision in one file instead of spreading guesses through the UI.

- [ ] **Step 2: Write failing AdapterClient tests**

Using StubHttpMessageHandler, test that:

~~~csharp
var identity = await client.GetIdentityAsync();
Assert.Equal("WeightRoom-AD", identity.DeviceName);

var overscan = await client.GetOverscanAsync();
Assert.False(overscan.IsAutoAdjust);
Assert.Equal(0, overscan.Value);

var protection = await client.GetPasswordProtectionAsync();
Assert.False(protection.Enabled);
~~~

Add tests proving a write is followed by the configured read-back request, and a mismatched read-back throws AdapterProtocolException rather than reporting success.

- [ ] **Step 3: Run focused tests to verify failure**

~~~powershell
dotnet test .\tests\Mwda.Control.Tests\Mwda.Control.Tests.csproj --filter "FullyQualifiedName~AdapterClientTests|FullyQualifiedName~ProtocolRequestCatalogTests"
~~~

Expected: FAIL because the request catalog and client are not implemented.

- [ ] **Step 4: Implement the core client and request catalog**

Build action URLs with Uri.EscapeDataString, deserialize with System.Text.Json using case-insensitive property matching, and convert all unexpected response shapes into AdapterProtocolException containing operation, status, and a redacted body prefix. Serialize writes through a per-client SemaphoreSlim.

Implement these action names in the catalog:

~~~text
GetDeviceName             SetDeviceName
GetOverscanSetting        SetOverscanSetting
GetPasswordProtectState   SetPasswordProtect
~~~

The current adapter's live read operations are the acceptance baseline. The write encoder is selected by the opt-in characterization test: try the three closed encodings against a temporary value, accept only a response that parses and reads back exactly, and restore the original value in finally. Store the accepted encoding in a fixture so normal tests never probe or mutate the device.

- [ ] **Step 5: Add opt-in live tests with restoration**

CoreSettingsLiveTests must:

1. read and retain the adapter's original name, overscan, and password-protection state;
2. change the name to MWDA-Test-<timestamp> and verify it;
3. restore the original name in finally;
4. change overscan only within the value range returned by the adapter and restore it in finally; and
5. toggle password protection only after the accepted write encoding is known, then restore the original state in finally.

Run only with:

~~~powershell
$env:MWDA_RUN_LIVE_TESTS = "1"
$env:MWDA_ADAPTER_IP = "192.168.137.247"
dotnet test .\tests\Mwda.Control.IntegrationTests\Mwda.Control.IntegrationTests.csproj --configuration Release --filter "Category=LiveAdapter"
~~~

Expected: the tests pass and the adapter ends with its original configuration. Without MWDA_RUN_LIVE_TESTS=1, the live fixture must skip rather than touch the adapter.

- [ ] **Step 6: Run all unit tests and commit**

~~~powershell
dotnet test .\MWDA.Control.sln --configuration Release --filter "Category!=LiveAdapter"
git add src/Mwda.Control/Protocol tests/Mwda.Control.Tests/Protocol tests/Mwda.Control.IntegrationTests
git commit -m "feat: control core adapter settings"
~~~

### Task 5: Add optional capability operations and profile detection

**Files:**
- Create: src/Mwda.Control/Protocol/AdvancedAdapterClient.cs
- Create: src/Mwda.Control/Session/CapabilityDetector.cs
- Create: src/Mwda.Control/Session/AdapterSession.cs
- Create: src/Mwda.Control/Session/IAdapterSessionFactory.cs
- Create: tests/Mwda.Control.Tests/Session/CapabilityDetectorTests.cs
- Create: tests/Mwda.Control.Tests/Protocol/AdvancedAdapterClientTests.cs
- Create: tests/Mwda.Control.IntegrationTests/OptionalCapabilitiesLiveTests.cs

**Interfaces:**
- CapabilityDetector.DetectAsync(IWirelessDisplayAdapterClient, IAdvancedWirelessDisplayAdapterClient, CancellationToken) returns CapabilityProfile from read-only probes.
- AdapterSession contains DiscoveredAdapter, AdapterIdentity, CapabilityProfile, and the client.
- IAdapterSessionFactory.CreateAsync(DiscoveredAdapter, CancellationToken) returns AdapterSession.
- AdvancedAdapterClient exposes typed optional operations without adding firmware operations.

- [ ] **Step 1: Write capability-profile tests**

Use fake responses to prove that HTTP 404/501 or a schema mismatch marks an operation unsupported, while a valid response marks it supported. Test that the current adapter's known successful operations are supported and GetWiFiSettings is not marked supported when it returns 404.

- [ ] **Step 2: Write advanced protocol fixture tests**

Cover the exact typed methods:

~~~text
GetWallpaperInfo / SetPredefinedWallpaper / UploadCustomWallpaper
GetWiFiSettings / SetWiFiSettings / ForgetWiFi
GetHdcpStatus / SetHdcpStatus
GetLanguage / SetLanguage
Restart
~~~

Each fixture must assert a typed request and typed response; a 404/501 fixture must return UnsupportedOperation without throwing an unclassified exception. Wallpaper upload tests must verify multipart content is bounded to an allow-listed image extension and maximum byte size.

- [ ] **Step 3: Implement the capability detector and advanced client**

Use the action names already present in the installed package's configuration and the verified local control root. Keep the operation routes and encoders in ProtocolRequestCatalog. Add the Gen 3/4K Wi-Fi operations only when their read probe succeeds. Keep firmware-related actions out of the enum, catalog, client, tests, and UI.

- [ ] **Step 4: Add opt-in optional live coverage**

The live test must read optional capabilities first. For wallpaper, HDCP, language, Wi-Fi, and restart, it may perform a write only if the corresponding read-back and restoration path is available. A missing capability is a passing skip, not a failure. No test downloads or checks firmware.

- [ ] **Step 5: Run unit tests and commit**

~~~powershell
dotnet test .\MWDA.Control.sln --configuration Release --filter "Category!=LiveAdapter"
git add src/Mwda.Control/Session src/Mwda.Control/Protocol tests/Mwda.Control.Tests/Session tests/Mwda.Control.Tests/Protocol tests/Mwda.Control.IntegrationTests
git commit -m "feat: detect and expose optional adapter capabilities"
~~~

### Task 6: Build the MVVM state and commands

**Files:**
- Create: src/Mwda.Control/Mvvm/ObservableObject.cs
- Create: src/Mwda.Control/Mvvm/AsyncRelayCommand.cs
- Create: src/Mwda.Control/ViewModels/MainWindowViewModel.cs
- Create: src/Mwda.Control/ViewModels/ConnectionViewModel.cs
- Create: src/Mwda.Control/ViewModels/AdapterSettingsViewModel.cs
- Create: src/Mwda.Control/ViewModels/DisplaySettingsViewModel.cs
- Create: src/Mwda.Control/ViewModels/NetworkSettingsViewModel.cs
- Create: src/Mwda.Control/ViewModels/ConnectionSettingsViewModel.cs
- Create: src/Mwda.Control/ViewModels/DiagnosticsViewModel.cs
- Create: tests/Mwda.Control.Tests/ViewModels/ViewModelTests.cs

**Interfaces:**
- MainWindowViewModel.SelectedPage controls navigation and exposes IsFirmwareVisible=false as a constant invariant.
- ConnectionViewModel.RefreshAsync() discovers and selects an adapter session.
- Settings view models load from and save through AdapterSession without constructing protocol requests.

- [ ] **Step 1: Write failing view-model tests**

Test that:

~~~csharp
var shell = new MainWindowViewModel(fakeDiscovery, fakeSessionFactory);
Assert.DoesNotContain(shell.NavigationItems, item => item.Key == "Firmware");

await shell.Connection.RefreshAsync();
Assert.Equal("WeightRoom-AD", shell.Connection.SelectedAdapter!.DeviceName);

shell.Adapter.DeviceName = "Room_2+(West)";
await shell.Adapter.SaveAsync();
fakeClient.AssertSetDeviceName("Room_2+(West)");
~~~

Also test that an unsupported capability disables only its own page, that concurrent saves are serialized by the client, and that a failed save leaves the edit dirty with an error message.

- [ ] **Step 2: Implement observable state and commands**

Use INotifyPropertyChanged, an AsyncRelayCommand with IsExecuting, and a single CancellationTokenSource per refresh/save operation. View models may expose ResultBanner and IsDirty, but they must depend only on typed protocol/session interfaces.

- [ ] **Step 3: Implement shell navigation and session refresh**

On startup, create a disconnected session state and start one bounded discovery refresh. Selecting an adapter loads identity and capabilities. On connection loss, cancel in-flight work, preserve unsaved page edits, and publish a disconnected state. Do not launch the Store app.

- [ ] **Step 4: Implement settings-page state**

Adapter settings cover name and pairing/password. Display settings cover overscan and wallpaper. Network settings are visible only when CapabilityProfile says Wi-Fi is supported. Connection settings expose Windows wireless-display discovery through the ms-settings-connectabledevices:devicediscovery URI and never try to implement Miracast projection.

- [ ] **Step 5: Run view-model tests and commit**

~~~powershell
dotnet test .\tests\Mwda.Control.Tests\Mwda.Control.Tests.csproj --configuration Release --filter "FullyQualifiedName~ViewModelTests"
git add src/Mwda.Control/Mvvm src/Mwda.Control/ViewModels tests/Mwda.Control.Tests/ViewModels
git commit -m "feat: add capability-driven view models"
~~~

### Task 7: Create the WPF shell and settings views

**Files:**
- Modify: src/Mwda.Control/App.xaml
- Modify: src/Mwda.Control/App.xaml.cs
- Modify: src/Mwda.Control/MainWindow.xaml
- Modify: src/Mwda.Control/MainWindow.xaml.cs
- Create: src/Mwda.Control/Views/DisconnectedView.xaml
- Create: src/Mwda.Control/Views/AdapterView.xaml
- Create: src/Mwda.Control/Views/DisplayView.xaml
- Create: src/Mwda.Control/Views/NetworkView.xaml
- Create: src/Mwda.Control/Views/ConnectionView.xaml
- Create: src/Mwda.Control/Views/AboutView.xaml
- Create: src/Mwda.Control/Views/DiagnosticsView.xaml
- Create: src/Mwda.Control/Resources/Theme.xaml
- Modify: src/Mwda.Control/Mwda.Control.csproj

**Interfaces:**
- The XAML binds only to the view models from Task 6.
- MainWindow shows no firmware navigation item or firmware control.

- [ ] **Step 1: Add the failing build-time UI contract**

Add a view-model test that constructs the navigation collection and asserts the exact keys Adapter, Display, Connection, About, and Diagnostics, with Network included only when supported. Assert that the firmware key is never present.

- [ ] **Step 2: Implement the application shell**

Use a two-column Grid: a fixed-width navigation ListBox on the left and a ContentControl on the right. Add a header with adapter name, IP, connection state, and Refresh. Use DataTemplate mappings to show the corresponding views. Set AutomationProperties.Name on navigation items, fields, buttons, status banners, and the selected adapter.

- [ ] **Step 3: Implement the pages**

Use standard WPF controls only:

- Adapter: editable name, pairing-protection toggle, password fields, Save.
- Display: auto-adjust checkbox, overscan slider/text value, built-in wallpaper selector, custom image picker.
- Network: SSID list, password field, connect/forget actions; show capability explanation when absent.
- Connection: current projection status and Open Windows wireless display settings.
- About: model/generation, read-only firmware version, MAC, IP, and support details.
- Diagnostics: last probe, operation status, redacted local errors, and Copy diagnostics.

Do not add a firmware page, firmware download button, update check, or automatic-update toggle.

- [ ] **Step 4: Implement validation and result banners**

Bind ValidationRule objects to name/password/overscan fields, disable save while a request is executing, show Applied, Rejected, Unsupported, or Disconnected banners, and preserve unsaved edits after a connection loss.

- [ ] **Step 5: Build and run the app**

~~~powershell
dotnet build .\MWDA.Control.sln --configuration Debug
dotnet run --project .\src\Mwda.Control\Mwda.Control.csproj --configuration Debug
~~~

Expected: a native WPF window opens without the Microsoft Store app, the connected adapter is discoverable, and unsupported pages are clearly labeled.

- [ ] **Step 6: Commit the WPF shell**

~~~powershell
git add src/Mwda.Control/App.xaml src/Mwda.Control/App.xaml.cs src/Mwda.Control/MainWindow.xaml src/Mwda.Control/MainWindow.xaml.cs src/Mwda.Control/Views src/Mwda.Control/Resources src/Mwda.Control/Mwda.Control.csproj tests/Mwda.Control.Tests
git commit -m "feat: add adapter control WPF interface"
~~~

### Task 8: Add diagnostics, documentation, publishing, and final verification

**Files:**
- Create: src/Mwda.Control/Diagnostics/DiagnosticSnapshot.cs
- Create: src/Mwda.Control/Diagnostics/DiagnosticFormatter.cs
- Create: README.md
- Create: publish.ps1
- Create: tests/Mwda.Control.Tests/Diagnostics/DiagnosticFormatterTests.cs
- Modify: tests/Mwda.Control.IntegrationTests/CoreSettingsLiveTests.cs
- Modify: tests/Mwda.Control.IntegrationTests/OptionalCapabilitiesLiveTests.cs

**Interfaces:**
- DiagnosticFormatter.Format(DiagnosticSnapshot) returns redacted text containing endpoint, connection state, capabilities, and recent operation status without passwords/PINs.
- publish.ps1 produces artifacts/publish/win-x64/MWDA-Control.exe using the exact publish properties below.

- [ ] **Step 1: Write failing diagnostics tests**

~~~csharp
[Fact]
public void FormatterRedactsSecrets()
{
    var text = DiagnosticFormatter.Format(new DiagnosticSnapshot(
        "192.168.137.247", "WeightRoom-AD", "secret-pin", "secret-password"));

    Assert.DoesNotContain("secret-pin", text);
    Assert.DoesNotContain("secret-password", text);
    Assert.Contains("192.168.137.247", text);
}
~~~

- [ ] **Step 2: Implement local diagnostics and README**

Document prerequisites, build, debug run, live-test opt-in, and publish commands. State explicitly that the app configures the adapter but Windows performs the Miracast projection. State explicitly that firmware update functionality is intentionally absent.

- [ ] **Step 3: Implement the publish script**

publish.ps1 must run this single command:

~~~powershell
dotnet publish .\src\Mwda.Control\Mwda.Control.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false --output .\artifacts\publish\win-x64
~~~

The script must fail on a nonzero publish exit code and print the final executable path.

- [ ] **Step 4: Run the full unit suite and publish**

~~~powershell
dotnet test .\MWDA.Control.sln --configuration Release --filter "Category!=LiveAdapter"
.\publish.ps1
Test-Path .\artifacts\publish\win-x64\MWDA-Control.exe
~~~

Expected: all unit tests pass and the self-contained executable exists.

- [ ] **Step 5: Run live verification against the current adapter**

With the adapter still connected, run the opt-in core and optional tests using MWDA_ADAPTER_IP discovered from the normal discovery path. Confirm the original name WeightRoom-AD, overscan value, and password-protection state are restored after the run. Start the published executable with the Store app closed and manually verify connection, adapter name read, overscan read, password state read, disconnected refresh, and absence of firmware UI.

- [ ] **Step 6: Perform the final completion audit and commit**

Check every scope item in docs/superpowers/specs/2026-08-10-mwda-control-design.md against source, tests, and the published executable. Search the repository to prove firmware operations are absent:

~~~powershell
rg -n -i "firmware|update adapter|automatic update|\.sbin|\.sign" src tests README.md
git status --short
git add src tests README.md publish.ps1 artifacts
git commit -m "feat: publish MWDA Control replacement"
~~~

The search may find only the intentional exclusion documentation and tests that assert absence; it must not find production firmware routes, download code, upload code, or firmware UI.

## Plan self-review

- Spec coverage: Tasks 2–5 cover discovery, capability detection, core settings, optional settings, read-only adapter information, and the no-firmware boundary. Tasks 6–7 cover the shell and all settings views. Task 8 covers diagnostics, publishing, and the manual acceptance checklist.
- Placeholder scan: no unresolved-marker text or unspecified “appropriate handling” steps are used. Protocol characterization is a bounded three-encoding test with a named fixture and restoration procedure.
- Type consistency: AdapterOperation, AdapterEndpoint, IWirelessDisplayAdapterClient, CapabilityProfile, AdapterSession, and the view-model methods are named once and consumed consistently by later tasks.
- Scope: all tasks are part of one standalone replacement app; protocol, discovery, UI, and verification are separated into independently testable units.
