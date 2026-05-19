param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [switch]$NoTag,
    [switch]$PushTag
)

$ErrorActionPreference = "Stop"

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must be semantic format like 1.2.3"
}

$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location $projectRoot

$projectFile = Join-Path $projectRoot "PhotoFolderViewer.csproj"
if (-not (Test-Path $projectFile)) {
    throw "Project file not found: $projectFile"
}

[xml]$projectXml = Get-Content -Path $projectFile
$propertyGroup = $projectXml.Project.PropertyGroup | Select-Object -First 1
if ($null -eq $propertyGroup) {
    throw "PropertyGroup not found in project file."
}

if ($null -eq $propertyGroup.Version) {
    $node = $projectXml.CreateElement("Version")
    $node.InnerText = $Version
    [void]$propertyGroup.AppendChild($node)
}
else {
    $propertyGroup.Version = $Version
}

if ($null -eq $propertyGroup.FileVersion) {
    $node = $projectXml.CreateElement("FileVersion")
    $node.InnerText = "$Version.0"
    [void]$propertyGroup.AppendChild($node)
}
else {
    $propertyGroup.FileVersion = "$Version.0"
}

if ($null -eq $propertyGroup.AssemblyVersion) {
    $node = $projectXml.CreateElement("AssemblyVersion")
    $node.InnerText = "$Version.0"
    [void]$propertyGroup.AppendChild($node)
}
else {
    $propertyGroup.AssemblyVersion = "$Version.0"
}

$settings = New-Object System.Xml.XmlWriterSettings
$settings.Indent = $true
$settings.NewLineChars = "`r`n"
$settings.NewLineHandling = [System.Xml.NewLineHandling]::Replace
$settings.OmitXmlDeclaration = $true

$writer = [System.Xml.XmlWriter]::Create($projectFile, $settings)
$projectXml.Save($writer)
$writer.Dispose()

Write-Host "Updated project version metadata to $Version"

$releaseScript = Join-Path $PSScriptRoot "release.ps1"
if (-not (Test-Path $releaseScript)) {
    throw "Release script not found: $releaseScript"
}

& $releaseScript -Configuration $Configuration -RuntimeIdentifier $RuntimeIdentifier -Version $Version

if (-not $NoTag) {
    $tag = "v$Version"
    $existingTag = git tag --list $tag
    if (-not [string]::IsNullOrWhiteSpace($existingTag)) {
        throw "Git tag already exists: $tag"
    }

    git tag $tag
    Write-Host "Created git tag: $tag"

    if ($PushTag) {
        git push origin $tag
        Write-Host "Pushed git tag: $tag"
    }
}

Write-Host "Release cut complete."
