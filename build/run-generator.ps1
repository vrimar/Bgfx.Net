#!/usr/bin/env pwsh
# Builds the Bgfx.Net.Generator tool and runs it to produce bgfx.g.cs from bgfx.raw.cs.
$ErrorActionPreference = 'Stop'
$repo = Resolve-Path "$PSScriptRoot/.."

$generatorProj = Join-Path $repo 'src/Bgfx.Net.Generator/Bgfx.Net.Generator.csproj'
$raw = Join-Path $repo 'src/Bgfx.Net/Generated/bgfx.raw.cs'
$out = Join-Path $repo 'src/Bgfx.Net/Generated/bgfx.g.cs'

if (!(Test-Path $raw)) {
    Write-Error "bgfx.raw.cs not found at $raw. Run build/sync-bindings.ps1 first."
}

Write-Host "[run-generator] Building generator and emitting bgfx.g.cs"
& dotnet run --project $generatorProj --configuration Release -- $raw $out
if ($LASTEXITCODE -ne 0) {
    Write-Error "Generator failed with exit code $LASTEXITCODE"
}
