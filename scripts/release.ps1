param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"

function Remove-StalePhotoFolderViewerInstallations {
    $roots = @(
        'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall',
        'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall'
    )

    foreach ($root in $roots) {
        if (-not (Test-Path $root)) {
            continue
        }

        $keys = Get-ChildItem -Path $root -ErrorAction SilentlyContinue
        foreach ($key in $keys) {
            $properties = Get-ItemProperty -Path $key.PSPath -ErrorAction SilentlyContinue
            $displayName = $properties.DisplayName
            if ($displayName -match 'Photo Folder Viewer') {
                Write-Host "Removing stale uninstall entry: $($key.Name)"
                Remove-Item -Path $key.PSPath -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }

    $existingProducts = Get-CimInstance Win32_Product -ErrorAction SilentlyContinue | Where-Object { $_.Name -match 'Photo Folder Viewer' }
    foreach ($product in $existingProducts) {
        Write-Host "Uninstalling previous Photo Folder Viewer product: $($product.Name) ($($product.Version)) [$($product.IdentifyingNumber)]"
        $null = Start-Process -FilePath 'msiexec.exe' -ArgumentList @('/x', $product.IdentifyingNumber, '/qn', '/norestart') -Wait -PassThru
    }
}

$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location $projectRoot

Remove-StalePhotoFolderViewerInstallations

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

$existingProducts = Get-CimInstance Win32_Product -ErrorAction SilentlyContinue | Where-Object { $_.Name -match "Photo Folder Viewer" }
foreach ($product in $existingProducts) {
    Write-Host "Uninstalling previous Photo Folder Viewer product: $($product.Name) ($($product.Version)) [$($product.IdentifyingNumber)]"
    $null = Start-Process -FilePath "msiexec.exe" -ArgumentList @("/x", $product.IdentifyingNumber, "/qn", "/norestart") -Wait -PassThru
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
