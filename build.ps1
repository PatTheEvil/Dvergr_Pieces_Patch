<#
.SYNOPSIS
  Builds the Thunderstore package for the Dvergr Pieces Valheim 1.0 patch.

.DESCRIPTION
  Compiles the plugin, verifies it against the installed game, and zips it.
  Nothing of Tequila's is compiled, patched on disk, or packaged: his mod is a
  Thunderstore dependency and is repaired in memory at runtime.

.EXAMPLE
  .\build.ps1
#>
[CmdletBinding()]
param(
    [string]$Managed,
    [string]$BepInExCore,
    [string]$Csc,
    [string]$OutDir  = "$PSScriptRoot\build",
    [string]$DistDir = "$PSScriptRoot\dist",
    [switch]$SkipVerify
)

$ErrorActionPreference = 'Stop'

# ---- locate the game ------------------------------------------------------
if (-not $Managed) {
    foreach ($root in @(
        "${env:ProgramFiles(x86)}\Steam\steamapps\common\Valheim"
        "$env:ProgramFiles\Steam\steamapps\common\Valheim"
    )) {
        $p = Join-Path $root 'valheim_Data\Managed'
        if (Test-Path $p) { $Managed = $p; break }
    }
    if (-not $Managed) {
        $vdf = "${env:ProgramFiles(x86)}\Steam\steamapps\libraryfolders.vdf"
        if (Test-Path $vdf) {
            foreach ($line in (Select-String -Path $vdf -Pattern '"path"').Line) {
                # backslashes in libraryfolders.vdf are doubled; collapse them
                $bs  = [string][char]92
                $lib = ($line -split '"')[3].Replace($bs + $bs, $bs)
                $p = Join-Path $lib 'steamapps\common\Valheim\valheim_Data\Managed'
                if (Test-Path $p) { $Managed = $p; break }
            }
        }
    }
}
if (-not $Managed -or -not (Test-Path $Managed)) {
    throw "Valheim Managed folder not found. Pass -Managed <...\valheim_Data\Managed>"
}

# ---- locate BepInEx core --------------------------------------------------
if (-not $BepInExCore) {
    foreach ($c in @(
        "$env:APPDATA\com.kesomannen.gale\valheim\profiles\*\BepInEx\core"
        "$env:APPDATA\Thunderstore Mod Manager\DataFolder\Valheim\profiles\*\BepInEx\core"
        "$env:APPDATA\r2modmanPlus-local\Valheim\profiles\*\BepInEx\core"
        (Join-Path (Split-Path -Parent (Split-Path -Parent $Managed)) 'BepInEx\core')
    )) {
        $hit = Get-ChildItem $c -Directory -ErrorAction SilentlyContinue |
               Where-Object { Test-Path (Join-Path $_.FullName 'BepInEx.dll') } |
               Select-Object -First 1
        if ($hit) { $BepInExCore = $hit.FullName; break }
    }
}
if (-not $BepInExCore -or -not (Test-Path (Join-Path $BepInExCore 'BepInEx.dll'))) {
    throw "BepInEx core folder not found. Pass -BepInExCore <...\BepInEx\core>"
}

# ---- locate a C# compiler -------------------------------------------------
if (-not $Csc) {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $vs = & $vswhere -latest -property installationPath 2>$null
        if ($vs) {
            $cand = Join-Path $vs 'MSBuild\Current\Bin\Roslyn\csc.exe'
            if (Test-Path $cand) { $Csc = $cand }
        }
    }
    if (-not $Csc) {
        $cand = Get-ChildItem "$env:ProgramFiles\Microsoft Visual Studio","${env:ProgramFiles(x86)}\Microsoft Visual Studio" `
                    -Recurse -Filter csc.exe -Depth 6 -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($cand) { $Csc = $cand.FullName }
    }
}
if (-not $Csc -or -not (Test-Path $Csc)) {
    throw "csc.exe not found. Install the .NET desktop workload, or pass -Csc <path to csc.exe>"
}

Write-Host "Game    : $Managed"
Write-Host "BepInEx : $BepInExCore"
Write-Host "Compiler: $Csc"
Write-Host ""

# ---- clean ----------------------------------------------------------------
if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }
New-Item -ItemType Directory -Force $OutDir | Out-Null
$pkg = Join-Path $OutDir 'package'
New-Item -ItemType Directory -Force $pkg | Out-Null

# ---- compile --------------------------------------------------------------
Write-Host "--- compiling DvergrPiecesPatch.dll"
$refs = @(
    "$BepInExCore\BepInEx.dll"
    "$BepInExCore\0Harmony.dll"
    "$Managed\mscorlib.dll"
    "$Managed\System.dll"
    "$Managed\System.Core.dll"
    "$Managed\netstandard.dll"
    "$Managed\assembly_valheim.dll"
    "$Managed\UnityEngine.dll"
    "$Managed\UnityEngine.CoreModule.dll"
)
$cscArgs = @('/target:library', '/nostdlib+', '/noconfig', '/optimize+',
             "/out:$pkg\DvergrPiecesPatch.dll")
foreach ($r in $refs) { $cscArgs += "/reference:$r" }
$cscArgs += (Get-ChildItem "$PSScriptRoot\src\*.cs" | ForEach-Object { $_.FullName })

& $Csc $cscArgs
if ($LASTEXITCODE -ne 0) { throw "compilation failed" }
Write-Host ("  built {0:N0} bytes" -f (Get-Item "$pkg\DvergrPiecesPatch.dll").Length)
Write-Host ""

# ---- verify ---------------------------------------------------------------
if (-not $SkipVerify) {
    Write-Host "--- verifying against Valheim"
    & "$PSScriptRoot\tools\verify-compat.ps1" -Dll "$pkg\DvergrPiecesPatch.dll" `
        -Managed $Managed -CecilDll (Join-Path $BepInExCore 'Mono.Cecil.dll')
    if ($LASTEXITCODE -ne 0) { throw "verification failed -- not packaging" }
    Write-Host ""
}

# ---- assemble and zip -----------------------------------------------------
Copy-Item "$PSScriptRoot\package\manifest.json"  $pkg
Copy-Item "$PSScriptRoot\package\icon.png"       $pkg
Copy-Item "$PSScriptRoot\package\README.md"      $pkg
Copy-Item "$PSScriptRoot\package\CHANGELOG.md"   $pkg

$manifest = Get-Content "$PSScriptRoot\package\manifest.json" -Raw | ConvertFrom-Json
$zipName  = "$($manifest.name)-$($manifest.version_number).zip"

if (-not (Test-Path $DistDir)) { New-Item -ItemType Directory -Force $DistDir | Out-Null }
$zip = Join-Path $DistDir $zipName
if (Test-Path $zip) { Remove-Item $zip -Force }

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $pkg, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $false)

if (-not (Test-Path $zip)) { throw "packaging failed: $zip was not written" }

Write-Host ("--- {0}  ({1:N0} KB)" -f $zip, ((Get-Item $zip).Length / 1KB)) -ForegroundColor Green
