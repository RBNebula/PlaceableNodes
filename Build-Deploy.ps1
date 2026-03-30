param(
    [string]$Configuration = "Release",
    [string]$MineMogulDir = "F:\SteamLibrary\steamapps\common\MineMogul"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $projectRoot "PlaceableNodes.csproj"
$targetFramework = "net472"
$pluginFolderName = "Placeable Nodes"

if (-not (Test-Path $projectFile)) {
    throw "Could not find project file at '$projectFile'."
}

if (-not (Test-Path $MineMogulDir)) {
    throw "MineMogulDir does not exist: '$MineMogulDir'."
}

$buildArgs = @(
    "build"
    $projectFile
    "-c"
    $Configuration
    "-p:MineMogulDir=$MineMogulDir"
)

Write-Host "Building PlaceableNodes..."
& dotnet @buildArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed."
}

$buildOutputDir = Join-Path $projectRoot ("bin\" + $Configuration + "\" + $targetFramework)
$pluginDir = Join-Path (Join-Path $MineMogulDir "BepInEx\plugins") $pluginFolderName

New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null

$filesToCopy = @(
    "PlaceableNodes.dll",
    "PlaceableNodes.pdb"
)

foreach ($fileName in $filesToCopy) {
    $sourcePath = Join-Path $buildOutputDir $fileName
    if (Test-Path $sourcePath) {
        $destinationPath = Join-Path $pluginDir $fileName
        try {
            Copy-Item $sourcePath -Destination $destinationPath -Force
        }
        catch [System.IO.IOException] {
            throw "Could not copy '$fileName' to '$destinationPath'. The file is likely locked because MineMogul is still running. Close the game, then rerun Build-Deploy.ps1."
        }
    }
}

Write-Host ""
Write-Host "Build and deployment complete."
Write-Host "Plugin directory: $pluginDir"
