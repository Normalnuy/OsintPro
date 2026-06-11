# Pack GitHub Release zip: launcher + main app
param(
    [string]$Version = "1.0.3",
    [string]$LauncherDir = "$PSScriptRoot\..\..\JustinOSINT_Launcher\JustinOSINT_Launcher\bin\Release\net10.0-windows",
    [string]$AppDir = "",
    [string]$OutDir = "$PSScriptRoot\..\release"
)

$ErrorActionPreference = "Stop"

if (-not $AppDir) {
    $AppDir = Join-Path $OutDir "JustinOSINT-$Version"
}

if (-not (Test-Path $AppDir)) {
    Write-Host "Run: dotnet publish OsintPro.UI -c Release -r win-x64 -o release/JustinOSINT-$Version"
    exit 1
}

if (-not (Test-Path "$LauncherDir\JustinOSINT_Launcher.exe")) {
    Write-Host "Build launcher first: dotnet build JustinOSINT_Launcher -c Release"
    exit 1
}

$stage = Join-Path $OutDir "stage-$Version"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage | Out-Null

Copy-Item "$AppDir\*" $stage -Recurse -Force
Copy-Item "$LauncherDir\JustinOSINT_Launcher.exe" $stage -Force
if (Test-Path "$LauncherDir\JustinOSINT_Launcher.dll") {
    Copy-Item "$LauncherDir\JustinOSINT_Launcher.dll" $stage -Force
}
if (Test-Path "$LauncherDir\JustinOSINT_Launcher.runtimeconfig.json") {
    Copy-Item "$LauncherDir\JustinOSINT_Launcher.runtimeconfig.json" $stage -Force
}

$zipPath = Join-Path $OutDir "JustinOSINT-v$Version-win-x64.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$stage\*" -DestinationPath $zipPath -Force
Remove-Item $stage -Recurse -Force

Write-Host "Done: $zipPath"