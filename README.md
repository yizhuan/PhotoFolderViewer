# Photo Folder Viewer

Photo Folder Viewer is a .NET WPF desktop app for browsing large photo folders with fast keyboard navigation, thumbnail virtualization, and async image preloading.

## Requirements

- Windows 10/11
- .NET SDK 8.0+ (9.0 works as well)
- PowerShell 5.1+ or PowerShell 7+

## Run Locally

```powershell
Set-Location .\PhotoFolderViewer
dotnet run -- "C:\Path\To\ImageOrFolder"
```

If the argument is an image path, the app opens that image and uses its parent folder as the active gallery.

## Build (Debug)

```powershell
Set-Location .\PhotoFolderViewer
dotnet build
```

## Release Publish (Single-File, Self-Contained)

This uses the requested settings:

```powershell
Set-Location .\PhotoFolderViewer
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true
```

Publish output:

- `bin/Release/net8.0-windows/win-x64/publish/PhotoFolderViewer.exe`

## Create MSI Installer Locally

### One-command pipeline (publish + MSI)

```powershell
Set-Location .\PhotoFolderViewer
powershell -ExecutionPolicy Bypass -File .\scripts\release.ps1 -Configuration Release -RuntimeIdentifier win-x64
```

### MSI-only (uses existing publish output)

```powershell
Set-Location .\PhotoFolderViewer
powershell -ExecutionPolicy Bypass -File .\scripts\build-msi.ps1 -Configuration Release -RuntimeIdentifier win-x64
```

### One-command cut release (bump version + publish + MSI + git tag)

```powershell
Set-Location .\PhotoFolderViewer
powershell -ExecutionPolicy Bypass -File .\scripts\cut-release.ps1 -Version 1.0.3
```

Optional tag push:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\cut-release.ps1 -Version 1.0.3 -PushTag
```

Skip git tag creation:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\cut-release.ps1 -Version 1.0.3 -NoTag
```

Installer output:

- `artifacts/installer/*.msi`
- `artifacts/installer/*.cab`
- `artifacts/installer/*.wixpdb`

## Versioning

- Project version is defined in `PhotoFolderViewer.csproj` (`<Version>`).
- MSI scripts auto-read this version if `-Version` is not passed.
- You can override version explicitly:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release.ps1 -Version 1.0.1
```

- `cut-release.ps1` updates `<Version>`, `<FileVersion>`, and `<AssemblyVersion>` before building.

## File Association Behavior

The MSI installer registers file associations so double-clicking these extensions opens Photo Folder Viewer:

- `.jpg`, `.jpeg`, `.png`, `.bmp`, `.gif`, `.tif`, `.tiff`, `.webp`

## GitHub Actions Release

Workflow file:

- `.github/workflows/release-msi.yml`

Triggers:

- Manual: `workflow_dispatch` (optional version input)
- Tag push: `v*` (for example `v1.0.1`)

Tag release flow:

```powershell
git tag v1.0.1
git push origin v1.0.1
```

On tag builds, the workflow creates installer artifacts and publishes them to the GitHub Release.
