#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds bgfx-shared-lib + shaderc/texturec/geometryc for win-x64 (MSVC).

.DESCRIPTION
    Invokes the upstream genie + msbuild flow with --with-shared-lib --with-tools,
    then stages the produced binaries into artifacts/native/win-x64/ and
    artifacts/tools/win-x64/ with canonical filenames.

.PARAMETER Configuration
    Release (default) or Debug. Bgfx.Net ships Release only.
#>
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',

    # genie action — defaults to vs2022 (matches CI), but local dev with newer VS can pass vs2026.
    [ValidateSet('vs2019', 'vs2022', 'vs2026')]
    [string]$VsAction = 'vs2022'
)
$ErrorActionPreference = 'Stop'
$repo = Resolve-Path "$PSScriptRoot/.."
$bgfx = Join-Path $repo 'external/bgfx'
$genie = Join-Path $repo 'external/bx/tools/bin/windows/genie.exe'

if (!(Test-Path $genie)) {
    Write-Error "genie.exe not found at $genie. Did bootstrap.ps1 init submodules?"
}

# Load MSVC environment if msbuild isn't already on PATH.
if (-not (Get-Command msbuild.exe -ErrorAction SilentlyContinue)) {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (!(Test-Path $vswhere)) {
        Write-Error "vswhere.exe not found. Install Visual Studio 2019+ with Desktop C++ workload, or run from a Developer PowerShell."
    }
    $vsInstall = & $vswhere -latest -property installationPath
    if (-not $vsInstall) { Write-Error "No Visual Studio installation found via vswhere." }
    $devShellModule = Join-Path $vsInstall 'Common7\Tools\Microsoft.VisualStudio.DevShell.dll'
    if (!(Test-Path $devShellModule)) { Write-Error "DevShell module not found at $devShellModule" }
    Write-Host "[build-native-win] Loading VS dev shell from $vsInstall"
    Import-Module $devShellModule
    Enter-VsDevShell -VsInstallPath $vsInstall -SkipAutomaticLocation -DevCmdArguments '-arch=x64 -host_arch=x64' | Out-Null
}

Write-Host "[build-native-win] genie --with-shared-lib --with-tools $VsAction"
Push-Location $bgfx
try {
    & $genie --with-shared-lib --with-tools $VsAction
    if ($LASTEXITCODE -ne 0) { Write-Error "genie failed: $LASTEXITCODE" }

    # Build each project directly. Passing multi-target /t: to a solution applies the
    # target to every project (each errors with "target does not exist"), and building
    # the whole solution builds 64 projects we don't ship. So we target the four we need.
    $projDir = ".build\projects\$VsAction"
    foreach ($proj in @('bgfx-shared-lib', 'shaderc', 'texturec', 'geometryc')) {
        $vcx = "$projDir\$proj.vcxproj"
        if (!(Test-Path $vcx)) { Write-Error "Missing project: $vcx" }
        Write-Host "[build-native-win] msbuild $vcx /p:Configuration=$Configuration /p:Platform=x64"
        & msbuild $vcx /m /v:minimal "/p:Configuration=$Configuration" /p:Platform=x64
        if ($LASTEXITCODE -ne 0) { Write-Error "msbuild $proj failed: $LASTEXITCODE" }
    }
}
finally {
    Pop-Location
}

# Locate produced binaries — bgfx names them <target><Configuration>.<ext>.
$binDir = Join-Path $bgfx ".build\win64_$VsAction\bin"
$sharedDll = Get-ChildItem -Path $binDir -Filter "bgfx-shared-lib$Configuration.dll" -ErrorAction Stop | Select-Object -First 1
$shaderc = Get-ChildItem -Path $binDir -Filter "shaderc$Configuration.exe" -ErrorAction Stop | Select-Object -First 1
$texturec = Get-ChildItem -Path $binDir -Filter "texturec$Configuration.exe" -ErrorAction Stop | Select-Object -First 1
$geometryc = Get-ChildItem -Path $binDir -Filter "geometryc$Configuration.exe" -ErrorAction Stop | Select-Object -First 1

$nativeOut = Join-Path $repo 'artifacts/native/win-x64'
$toolsOut = Join-Path $repo 'artifacts/tools/win-x64'
New-Item -ItemType Directory -Path $nativeOut -Force | Out-Null
New-Item -ItemType Directory -Path $toolsOut -Force | Out-Null

Copy-Item -Path $sharedDll.FullName -Destination (Join-Path $nativeOut 'bgfx.dll') -Force
Copy-Item -Path $shaderc.FullName   -Destination (Join-Path $toolsOut 'shaderc.exe') -Force
Copy-Item -Path $texturec.FullName  -Destination (Join-Path $toolsOut 'texturec.exe') -Force
Copy-Item -Path $geometryc.FullName -Destination (Join-Path $toolsOut 'geometryc.exe') -Force

# Stage RID-agnostic shader headers so consumer .sc files can #include them.
$includeOut = Join-Path $repo 'artifacts/tools/include'
New-Item -ItemType Directory -Path $includeOut -Force | Out-Null
Copy-Item -Path (Join-Path $bgfx 'src/bgfx_shader.sh')              -Destination $includeOut -Force
Copy-Item -Path (Join-Path $bgfx 'src/bgfx_compute.sh')             -Destination $includeOut -Force
Copy-Item -Path (Join-Path $bgfx 'examples/common/common.sh')       -Destination $includeOut -Force
Copy-Item -Path (Join-Path $bgfx 'examples/common/shaderlib.sh')    -Destination $includeOut -Force

Write-Host "[build-native-win] Staged win-x64 artifacts:"
Get-ChildItem $nativeOut, $toolsOut | Format-Table Name, Length

# Symbol export check — fail fast if bgfx_init isn't exported.
$dumpbin = (Get-Command dumpbin.exe -ErrorAction SilentlyContinue)?.Path
if ($dumpbin) {
    $exports = & $dumpbin /exports (Join-Path $nativeOut 'bgfx.dll')
    if (-not ($exports -match '\bbgfx_init\b')) {
        Write-Error "bgfx.dll does not export bgfx_init — build is broken."
    }
    Write-Host "[build-native-win] symbol check OK: bgfx_init exported."
}
else {
    Write-Warning "dumpbin.exe not in PATH; skipping symbol-export check."
}

# Reset $LASTEXITCODE — Format-Table / Get-ChildItem and dumpbin parse can leave it
# non-zero even on success. Without this, pwsh exits with whatever was leftover.
$global:LASTEXITCODE = 0
exit 0
