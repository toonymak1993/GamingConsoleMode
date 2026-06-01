param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repoRoot "gcmloader\gcmloader.csproj"
$innoScriptPath = Join-Path $PSScriptRoot "GCMSetup.iss"
$publishDir = Join-Path $repoRoot "artifacts\gcmloader-publish"
$installerDir = Join-Path $repoRoot "artifacts\installer"
$isccPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

if (-not (Test-Path $projectPath)) {
    throw "Project file not found: $projectPath"
}

if (-not (Test-Path $isccPath)) {
    throw "Inno Setup compiler not found: $isccPath"
}

[xml]$projectXml = Get-Content $projectPath
$version = $projectXml.Project.PropertyGroup.Version | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($version)) {
    $version = "2.6.0"
}

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
New-Item -ItemType Directory -Force -Path $installerDir | Out-Null

Write-Host "Publishing gcmloader $version to $publishDir"
dotnet publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    -p:Platform=x64 `
    -p:WindowsPackageType=None `
    -p:PublishSingleFile=true `
    -p:SelfContained=true `
    -o $publishDir

Write-Host "Compiling Inno Setup installer to $installerDir"
& $isccPath `
    "/DAppVersion=$version" `
    "/DSourceDir=$publishDir" `
    "/DOutputDir=$installerDir" `
    $innoScriptPath

Write-Host "Installer build complete."
