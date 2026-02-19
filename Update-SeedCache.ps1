# Update-SeedCache.ps1
# Copies the most recently modified cache file from the add-in's local
# cache directory into the project as SeedCache.json, bumping it to
# the current CacheVersion.
#
# Usage:
#   .\Update-SeedCache.ps1              # picks the newest cache file
#   .\Update-SeedCache.ps1 -Filter "*Prod*"  # picks newest matching a pattern

param(
    [string]$Filter = "*.json"
)

$cacheDir = Join-Path $env:LOCALAPPDATA "ProAppAddInSdeSearch\Cache"
$destination = Join-Path $PSScriptRoot "ProAppAddInSdeSearch\SeedCache.json"

if (-not (Test-Path $cacheDir)) {
    Write-Error "Cache directory not found: $cacheDir"
    Write-Error "Load data in the add-in first to generate a cache file."
    exit 1
}

# Find the most recently written cache file
$latest = Get-ChildItem -Path $cacheDir -Filter $Filter |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $latest) {
    Write-Error "No cache files found in $cacheDir matching '$Filter'"
    exit 1
}

Write-Host "Source:      $($latest.FullName)"
Write-Host "Size:        $([math]::Round($latest.Length / 1KB, 1)) KB"
Write-Host "Last write:  $($latest.LastWriteTime)"

# Read the current CacheVersion from the ViewModel source
$vmPath = Join-Path $PSScriptRoot "ProAppAddInSdeSearch\SdeSearchPaneViewModel.cs"
$versionMatch = Select-String -Path $vmPath -Pattern 'CurrentCacheVersion\s*=\s*(\d+)' |
    Select-Object -First 1
if ($versionMatch) {
    $currentVersion = [int]$versionMatch.Matches[0].Groups[1].Value
    Write-Host "Cache version: $currentVersion (from source)"
} else {
    Write-Warning "Could not detect CurrentCacheVersion from source; copying as-is."
    Copy-Item $latest.FullName $destination -Force
    Write-Host "`nDone. Copied to: $destination"
    exit 0
}

# Read the cache JSON, update the version, and write to SeedCache.json
$json = Get-Content $latest.FullName -Raw
$obj = $json | ConvertFrom-Json
$obj.CacheVersion = $currentVersion
$obj | ConvertTo-Json -Depth 10 -Compress | Set-Content $destination -Encoding UTF8

Write-Host "`nDone. SeedCache.json updated at: $destination"
