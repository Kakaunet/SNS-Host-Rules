param([string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Among Us", [switch]$Install)
$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "SNSHostRules.csproj"
dotnet build $project -c Release -p:GameDir="$GameDir"
if ($LASTEXITCODE -ne 0) { throw "Build failed; previous DLL is untouched." }
$dll = Join-Path $PSScriptRoot "bin\Release\net6.0\SNSHostRules.dll"
if (-not (Test-Path $dll)) { throw "Build returned success but no SNSHostRules.dll was found." }
Write-Host "Build OK: $dll"
if ($Install) {
    $pluginDir = Join-Path $GameDir "BepInEx\plugins"
    if (-not (Test-Path $pluginDir)) { throw "BepInEx plugins folder missing: $pluginDir" }
    $dest = Join-Path $pluginDir "SNSHostRules.dll"
    if (Test-Path $dest) {
        $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $backup = Join-Path $pluginDir "SNSHostRules-backup-$stamp.dll.disabled"
        Copy-Item $dest $backup -ErrorAction Stop
        Write-Host "Previous DLL backed up (disabled): $backup"
    }
    Copy-Item $dll $dest -Force
    Write-Host "Installed: $dest"
    Write-Host "Start the game to check SNS patch and mod-stamp logs."
} else {
    Write-Host "Build only. For backed-up installation use: .\build-and-check.ps1 -Install"
}
