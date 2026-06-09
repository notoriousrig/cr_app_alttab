# Builds a single-file, self-contained AltTabCustom.exe (no .NET runtime needed
# on the target machine, no admin rights required to build or run).
#
# Usage (from a PowerShell prompt in the repo root):
#   ./build.ps1            # publish self-contained single-file exe
#   ./build.ps1 -Run       # ...and launch it afterwards
#
# Output: ./publish/AltTabCustom.exe

param(
    [switch]$Run
)

$ErrorActionPreference = "Stop"

$project = "src/AltTabCustom/AltTabCustom.csproj"
$outDir  = "publish"

dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o $outDir

$exe = Join-Path $outDir "AltTabCustom.exe"
Write-Host "`nBuilt: $exe" -ForegroundColor Green

if ($Run) {
    Write-Host "Launching..." -ForegroundColor Cyan
    Start-Process $exe
}
