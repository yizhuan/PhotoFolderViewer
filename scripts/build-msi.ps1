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

[xml]$projectXml = Get-Content -Path $projectFile
$targetFramework = $projectXml.Project.PropertyGroup.TargetFramework | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($targetFramework)) {
    throw "TargetFramework not found in $projectFile"
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $resolvedVersion = $projectXml.Project.PropertyGroup.Version | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($resolvedVersion)) {
        $resolvedVersion = "1.0.0"
    }
    $Version = $resolvedVersion
}

$publishDir = Join-Path $projectRoot "bin\$Configuration\$targetFramework\$RuntimeIdentifier\publish"
$exePath = Join-Path $publishDir "PhotoFolderViewer.exe"

if (-not (Test-Path $exePath)) {
    Write-Host "Publish output not found. Running publish first..."
    dotnet publish -c $Configuration -r $RuntimeIdentifier --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -p:Version=$Version
}

if (-not (Test-Path $exePath)) {
    throw "Publish did not produce expected file: $exePath"
}

$manifestPath = Join-Path $projectRoot ".config\dotnet-tools.json"
if (-not (Test-Path $manifestPath)) {
    dotnet new tool-manifest
}

$toolList = dotnet tool list --local
if ($toolList -notmatch "\bwix\b") {
    dotnet tool install wix --version 4.*
}

$outputDir = Join-Path $projectRoot "artifacts\installer"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$outputMsi = Join-Path $outputDir "PhotoFolderViewer-$Version-$RuntimeIdentifier.msi"

$wixSource = Join-Path $projectRoot "Installer\Product.wxs"

Write-Host "Building MSI: $outputMsi"
dotnet tool run wix build $wixSource -arch x64 -d PublishDir=$publishDir -d ProductVersion=$Version -o $outputMsi

if (-not (Test-Path $outputMsi)) {
    throw "MSI output not created: $outputMsi"
}

Write-Host "MSI created successfully: $outputMsi"
