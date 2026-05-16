#!/usr/bin/env pwsh
# Copies the upstream bgfx C# bindings into src/Bgfx.Net/Generated/bgfx.raw.cs
# and updates the BgfxRevision assembly metadata to record the pinned commit SHA.
$ErrorActionPreference = 'Stop'
$repo = Resolve-Path "$PSScriptRoot/.."

$src = Join-Path $repo 'external/bgfx/bindings/cs/bgfx.cs'
$dstDir = Join-Path $repo 'src/Bgfx.Net/Generated'
$dst = Join-Path $dstDir 'bgfx.raw.cs'
$assemblyInfo = Join-Path $repo 'src/Bgfx.Net/AssemblyInfo.cs'

if (!(Test-Path $src)) {
    Write-Error "Upstream bindings not found at $src. Run build/bootstrap.ps1 first."
}

New-Item -ItemType Directory -Path $dstDir -Force | Out-Null
Copy-Item -Path $src -Destination $dst -Force
Write-Host "[sync-bindings] Copied $src -> $dst"

$sha = (git -C (Join-Path $repo 'external/bgfx') rev-parse HEAD).Trim()
Write-Host "[sync-bindings] bgfx pinned at $sha"

if (Test-Path $assemblyInfo) {
    $content = Get-Content -Raw $assemblyInfo
    $updated = $content -replace 'BgfxRevision",\s*"[^"]*"', "BgfxRevision`", `"$sha`""
    if ($updated -ne $content) {
        Set-Content -Path $assemblyInfo -Value $updated -NoNewline
        Write-Host "[sync-bindings] Updated BgfxRevision in $assemblyInfo"
    }
}
