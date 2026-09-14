<#
.SYNOPSIS
  Cross-checks every reference a mod assembly makes into assembly_valheim.dll
  against the installed game, and reports what no longer matches.

.DESCRIPTION
  Two classes of breakage are checked, because they fail in different ways:

  1. Direct IL references (ldfld / call). A member that was renamed shows up as
     MISSING; one that kept its name but changed type or signature shows up as
     CHANGED. The second kind is the dangerous one -- the mod still compiles and
     loads, then throws at runtime.

  2. String-based reflection (AccessTools.Field / DeclaredMethod / ...). These are
     invisible to IL checking: a stale name just returns null and the calling code
     silently does nothing.

  Note on (2): the declaring Type is the ldtoken immediately BEFORE the member-name
  ldstr. Any ldtoken after it belongs to the parameter-type array, so walking back
  from the call site to the nearest ldtoken gives false positives.

.EXAMPLE
  .\verify-compat.ps1 -Dll .\build\DvergrPieces.dll
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Dll,
    [string]$Managed,
    [string]$CecilDll
)

$ErrorActionPreference = 'Stop'

if (-not $Managed) {
    foreach ($root in @(
        "$env:ProgramFiles(x86)\Steam\steamapps\common\Valheim"
        "$env:ProgramFiles\Steam\steamapps\common\Valheim"
    )) {
        $p = Join-Path $root 'valheim_Data\Managed'
        if (Test-Path $p) { $Managed = $p; break }
    }
}
if (-not $Managed -or -not (Test-Path $Managed)) {
    throw "Valheim Managed folder not found. Pass -Managed <...\valheim_Data\Managed>"
}

if (-not $CecilDll) {
    foreach ($c in @(
        "$env:APPDATA\com.kesomannen.gale\valheim\profiles\*\BepInEx\core\Mono.Cecil.dll"
        "$env:APPDATA\Thunderstore Mod Manager\DataFolder\Valheim\profiles\*\BepInEx\core\Mono.Cecil.dll"
        "$env:APPDATA\r2modmanPlus-local\Valheim\profiles\*\BepInEx\core\Mono.Cecil.dll"
    )) {
        $hit = Get-ChildItem $c -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($hit) { $CecilDll = $hit.FullName; break }
    }
}
if (-not $CecilDll) { throw "Mono.Cecil.dll not found. Pass -CecilDll." }

Add-Type -Path $CecilDll

$mod  = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Resolve-Path $Dll))
$game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $Managed 'assembly_valheim.dll'))

$gameTypes = @{}
foreach ($t in $game.MainModule.GetTypes()) { $gameTypes[$t.FullName] = $t }

$problems = 0
$seen     = @{}

function Report([string]$kind, [string]$what, [string[]]$detail) {
    $script:problems++
    Write-Host "[$kind] $what" -ForegroundColor Red
    foreach ($d in $detail) { Write-Host "    $d" }
}

foreach ($t in $mod.MainModule.GetTypes()) {
    foreach ($m in $t.Methods) {
        if (-not $m.HasBody) { continue }
        $ins = @($m.Body.Instructions)

        for ($k = 0; $k -lt $ins.Count; $k++) {
            $op = $ins[$k].Operand

            # --- 1. direct IL references -------------------------------------
            if ($op -is [Mono.Cecil.FieldReference]) {
                $dt = $op.DeclaringType.FullName
                if ($gameTypes.ContainsKey($dt)) {
                    $key = "F:$dt::$($op.Name)"
                    if (-not $seen.ContainsKey($key)) {
                        $seen[$key] = 1
                        $f = $gameTypes[$dt].Fields | Where-Object { $_.Name -eq $op.Name } | Select-Object -First 1
                        if (-not $f) {
                            Report 'FIELD MISSING' "$dt::$($op.Name)" @("used in $($t.Name)::$($m.Name)")
                        } elseif ($f.FieldType.FullName -ne $op.FieldType.FullName) {
                            Report 'FIELD TYPE CHANGED' "$dt::$($op.Name)" @(
                                "mod expects : $($op.FieldType.FullName)",
                                "game has    : $($f.FieldType.FullName)",
                                "used in     : $($t.Name)::$($m.Name)")
                        }
                    }
                }
            }

            # --- 2. string-based reflection ----------------------------------
            if ($op -is [Mono.Cecil.MethodReference] -and $op.DeclaringType.Name -eq 'AccessTools' -and
                $op.Name -match '^(Declared)?(Field|Method|Property|PropertyGetter|PropertySetter)$') {

                $nameIdx = -1
                for ($j = $k - 1; $j -ge [Math]::Max(0, $k - 20); $j--) {
                    if ($ins[$j].OpCode.Name -eq 'ldstr') { $nameIdx = $j; break }
                }
                if ($nameIdx -lt 0) { continue }
                $member = [string]$ins[$nameIdx].Operand

                $typeName = $null
                for ($j = $nameIdx - 1; $j -ge [Math]::Max(0, $nameIdx - 6); $j--) {
                    if ($ins[$j].OpCode.Name -eq 'ldtoken') { $typeName = $ins[$j].Operand.ToString(); break }
                }
                if (-not $typeName -or -not $gameTypes.ContainsKey($typeName)) { continue }

                $key = "R:$typeName::$member"
                if ($seen.ContainsKey($key)) { continue }
                $seen[$key] = 1

                $gt = $gameTypes[$typeName]
                $hit = switch -Regex ($op.Name) {
                    'Field'   { [bool]($gt.Fields     | Where-Object { $_.Name -eq $member }) }
                    'Propert' { [bool]($gt.Properties | Where-Object { $_.Name -eq $member }) }
                    default   { [bool]($gt.Methods    | Where-Object { $_.Name -eq $member }) }
                }
                if (-not $hit) {
                    Report 'REFLECTION TARGET MISSING' "$typeName::$member" @("used in $($t.Name)::$($m.Name)")
                }
            }
        }
    }
}

Write-Host ""
if ($problems -eq 0) {
    Write-Host "OK - every reference into assembly_valheim still resolves." -ForegroundColor Green
    exit 0
} else {
    Write-Host "$problems problem(s) found." -ForegroundColor Red
    exit 1
}
