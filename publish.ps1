[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot

dotnet publish .\src\Mwda.Control\Mwda.Control.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false --output .\artifacts\publish\win-x64
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$executablePath = Join-Path $PSScriptRoot 'artifacts\publish\win-x64\Mwda.Control.exe'
if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw "The published executable was not found at $executablePath."
}

Write-Output "Published executable: $executablePath"
