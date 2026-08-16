# Verify that building the same source twice produces identical RimPipe.dll.
# Usage:
#   powershell -ExecutionPolicy Bypass -File tools/verify-deterministic.ps1
#   powershell -ExecutionPolicy Bypass -File tools/verify-deterministic.ps1 -Configuration Debug
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$outA = Join-Path $env:TEMP "rimpipe_det_a"
$outB = Join-Path $env:TEMP "rimpipe_det_b"

Push-Location $root
try {
    Remove-Item -Recurse -Force $outA, $outB -ErrorAction SilentlyContinue
    dotnet build "Source/RimPipe/RimPipe.csproj" --configuration $Configuration -v:minimal -t:Rebuild -p:OutputPath=$outA -p:AppendTargetFrameworkToOutputPath=false
    if ($LASTEXITCODE -ne 0) { throw "first build failed" }
    dotnet build "Source/RimPipe/RimPipe.csproj" --configuration $Configuration -v:minimal -t:Rebuild -p:OutputPath=$outB -p:AppendTargetFrameworkToOutputPath=false
    if ($LASTEXITCODE -ne 0) { throw "second build failed" }

    $hashA = (Get-FileHash "$outA/RimPipe.dll").Hash
    $hashB = (Get-FileHash "$outB/RimPipe.dll").Hash
    if ($hashA -ne $hashB) {
        Write-Host "hashA=$hashA"
        Write-Host "hashB=$hashB"
        throw "Deterministic build check failed: same source produced different DLL bytes"
    }
    Write-Host "Deterministic OK: $hashA"
}
finally {
    Pop-Location
}
