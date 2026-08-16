# Rebuild RimPipe.dll for release/commit.
# Usage:
#   powershell -ExecutionPolicy Bypass -File tools/rebuild-dll.ps1
#   powershell -ExecutionPolicy Bypass -File tools/rebuild-dll.ps1 -Configuration Debug
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    dotnet build "Source/RimPipe/RimPipe.csproj" --configuration $Configuration -v:minimal
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE"
    }
    Write-Host "RimPipe.dll rebuilt ($Configuration) -> Assemblies/RimPipe.dll"
}
finally {
    Pop-Location
}
