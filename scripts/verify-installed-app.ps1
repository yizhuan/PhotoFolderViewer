param(
    [string]$InstallerPath = ""
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location $projectRoot

if ([string]::IsNullOrWhiteSpace($InstallerPath)) {
    $latest = Get-ChildItem -Path (Join-Path $projectRoot "artifacts\installer") -Filter "PhotoFolderViewer-*-win-x64.msi" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($null -eq $latest) {
        throw "No MSI found under artifacts\\installer"
    }

    $InstallerPath = $latest.FullName
}

if (-not (Test-Path $InstallerPath)) {
    throw "Installer not found: $InstallerPath"
}

Write-Host "Installing: $InstallerPath"
$installProc = Start-Process -FilePath "msiexec.exe" -ArgumentList @("/i", "`"$InstallerPath`"", "/passive", "/norestart") -Wait -PassThru
if ($installProc.ExitCode -ne 0) {
    throw "MSI install failed with exit code $($installProc.ExitCode)"
}

$exePath = "C:\Program Files\Photo Folder Viewer\PhotoFolderViewer.exe"
if (-not (Test-Path $exePath)) {
    throw "Installed EXE not found: $exePath"
}

$installedVersion = (Get-Item $exePath).VersionInfo.FileVersion
Write-Host "Installed EXE version: $installedVersion"

$shortcutTop = "C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Photo Folder Viewer.lnk"
$shortcutNested = "C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Photo Folder Viewer\Photo Folder Viewer.lnk"
Write-Host "Top-level shortcut exists: $(Test-Path $shortcutTop)"
Write-Host "Nested shortcut exists: $(Test-Path $shortcutNested)"

$before = Get-Date
Start-Process -FilePath $exePath
Start-Sleep -Seconds 3

$proc = Get-Process -Name PhotoFolderViewer -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $proc) {
    Write-Host "Process did not remain running."
} else {
    Write-Host "Process running. PID: $($proc.Id)"
    Stop-Process -Id $proc.Id -Force
}

$recentErrors = Get-WinEvent -FilterHashtable @{ LogName = "Application"; StartTime = $before } -ErrorAction SilentlyContinue |
    Where-Object {
        ($_.ProviderName -eq ".NET Runtime" -or $_.ProviderName -eq "Application Error") -and
        $_.Message -match "PhotoFolderViewer|DllNotFoundException|KERNELBASE"
    }

if ($recentErrors) {
    Write-Host "Recent related errors detected:" 
    $recentErrors | Select-Object TimeCreated, ProviderName, Message | Format-List
} else {
    Write-Host "No recent .NET/Application Error entries related to PhotoFolderViewer."
}
