param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location $projectRoot

$projectFile = Join-Path $projectRoot "PhotoFolderViewer.csproj"
if (-not (Test-Path $projectFile)) {
    throw "Project file not found: $projectFile"
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$projectXml = Get-Content -Path $projectFile
    $resolvedVersion = $projectXml.Project.PropertyGroup.Version | Select-Object -First 1
    if (-not [string]::IsNullOrWhiteSpace($resolvedVersion)) {
        $Version = $resolvedVersion
    }
}

$publishArgs = @(
    "publish"
    "-c", $Configuration
    "-r", $RuntimeIdentifier
    "--self-contained", "true"
    "-p:PublishSingleFile=true"
    "-p:PublishReadyToRun=true"
    "-p:IncludeNativeLibrariesForSelfExtract=true"
    "-p:IncludeAllContentForSelfExtract=true"
)

if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $publishArgs += "-p:Version=$Version"
}

Write-Host "Publishing release build..."
dotnet @publishArgs

$msiScript = Join-Path $PSScriptRoot "build-msi.ps1"
if (-not (Test-Path $msiScript)) {
    throw "MSI script not found: $msiScript"
}

$msiArgs = @{
    Configuration = $Configuration
    RuntimeIdentifier = $RuntimeIdentifier
}

if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $msiArgs.Version = $Version
}

Write-Host "Creating MSI installer..."
& $msiScript @msiArgs

Write-Host "Release pipeline complete."
