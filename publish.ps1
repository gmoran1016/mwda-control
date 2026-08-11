[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot

$localDotnetPath = Join-Path $PSScriptRoot '.tools\dotnet\dotnet.exe'
if (Test-Path -LiteralPath $localDotnetPath -PathType Leaf) {
    $dotnetPath = (Resolve-Path -LiteralPath $localDotnetPath).Path
} else {
    $dotnetCommand = Get-Command -Name dotnet -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -eq $dotnetCommand) {
        throw "No usable .NET SDK was found. Provide $localDotnetPath or install a .NET 8 SDK and make dotnet.exe available on PATH."
    }

    $dotnetPath = $dotnetCommand.Path
}

$sdkVersion = (& $dotnetPath --version 2>&1 | Out-String).Trim()
$sdkExitCode = $LASTEXITCODE
if ($sdkExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($sdkVersion)) {
    throw "The selected dotnet executable is not a usable .NET SDK: $dotnetPath. dotnet --version failed with exit code $sdkExitCode."
}

Write-Output "Using dotnet SDK $sdkVersion from $dotnetPath"

$publishDirectory = Join-Path $PSScriptRoot 'artifacts\publish\win-x64'
if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

& $dotnetPath publish .\src\Mwda.Control\Mwda.Control.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false --output $publishDirectory
$publishExitCode = $LASTEXITCODE
if ($publishExitCode -ne 0) {
    throw "dotnet publish failed with exit code $publishExitCode."
}

$publishedFiles = @(Get-ChildItem -LiteralPath $publishDirectory -File)
$executablePath = Join-Path $publishDirectory 'Mwda.Control.exe'
if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw "The published executable was not found at $executablePath."
}
if ($publishedFiles.Count -ne 1) {
    $fileNames = ($publishedFiles | ForEach-Object Name) -join ', '
    throw "The publish directory must contain exactly Mwda.Control.exe. Found: $fileNames"
}

Write-Output "Published executable: $executablePath"
