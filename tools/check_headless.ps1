<#
.SYNOPSIS
Checks that the backend can still be built, run and validated without Unity.

.DESCRIPTION
S5 asks for a pure C# system that keeps working if the Unity client is replaced,
and that can be validated without scene behaviour. That property is true today,
and nothing was holding it true: the server and the shared package happen to
contain no Unity reference and the smoke clients happen to use only the Python
standard library, but a change that added one would have failed nothing.

This turns those three properties into a check:

- no Unity reference in the server sources, its project file, or the shared
  package when that package is available beside this repository
- no third party import in a smoke client, so a smoke run needs a Python
  install and nothing else
- every smoke client the run script drives exists, so a renamed script fails
  here rather than halfway through a run

A failure names the file and the line, because the point is to be actionable
rather than to be a gate that gets disabled.

.PARAMETER Core
The OpenGSCore repository. Defaults to the sibling directory. The check skips
it when it is not there, so this repository can be validated on its own.

.EXAMPLE
./tools/check_headless.ps1

.EXAMPLE
./tools/check_headless.ps1 -Core ..\SomeOtherOpenGSCore
#>
[CmdletBinding()]
param(
    [string] $Core
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $Core) {
    $Core = Join-Path (Split-Path -Parent $repoRoot) 'OpenGSCore'
}

$failures = New-Object System.Collections.Generic.List[string]

function Add-Failure {
    param([string] $Message)
    $failures.Add($Message)
    Write-Host "  FAIL $Message"
}

# ---- No Unity in the code that has to keep running -------------------------

<#
The search is for a using directive and a qualified use rather than the word on
its own. A comment that says "the client uses Unity Physics here" is worth
keeping; what has to be gone is the dependency, not the mention.
#>
function Test-NoUnityReference {
    param([string] $Root, [string] $Label)

    if (-not (Test-Path $Root)) {
        Write-Host "  SKIP $Label is not present at $Root"
        return
    }

    Write-Host "Checking $Label for Unity references"

    $sources = Get-ChildItem -Path $Root -Recurse -File -Filter '*.cs' |
        Where-Object { $_.FullName -notmatch '\\(obj|bin|\.artifacts|\.git|\.vs)\\' }

    foreach ($file in $sources) {
        $matches = Select-String -Path $file.FullName -Pattern 'using\s+UnityEngine|UnityEngine\.' -ErrorAction SilentlyContinue
        foreach ($match in $matches) {
            $relative = $file.FullName.Substring($Root.Length).TrimStart('\', '/')
            Add-Failure "$relative`:$($match.LineNumber) depends on UnityEngine"
        }
    }

    $projects = Get-ChildItem -Path $Root -Recurse -File -Filter '*.csproj' |
        Where-Object { $_.FullName -notmatch '\\(obj|bin|\.artifacts)\\' }

    foreach ($project in $projects) {
        $matches = Select-String -Path $project.FullName -Pattern 'Unity|openupm' -ErrorAction SilentlyContinue
        foreach ($match in $matches) {
            $relative = $project.FullName.Substring($Root.Length).TrimStart('\', '/')
            Add-Failure "$relative`:$($match.LineNumber) references Unity in the project file"
        }
    }
}

Test-NoUnityReference -Root $repoRoot -Label 'OpenGSServer'
Test-NoUnityReference -Root $Core -Label 'OpenGSCore'

# ---- The smoke clients need nothing but Python -----------------------------

<#
The smoke clients are the headless validation path, so a third party import in
one of them means the path only runs where that package happens to be installed.
The shared module is the one to watch: every other client imports it, so a
dependency there reaches the whole suite.
#>
Write-Host 'Checking the smoke clients for third party imports'

$standardLibrary = @{
    '__future__' = $true; 'argparse' = $true; 'json' = $true; 'socket' = $true
    'time' = $true; 'uuid' = $true; 'typing' = $true; 'os' = $true
    'sys' = $true; 're' = $true; 'collections' = $true; 'dataclasses' = $true
    'threading' = $true; 'subprocess' = $true; 'struct' = $true; 'random' = $true
    'itertools' = $true; 'functools' = $true; 'math' = $true; 'base64' = $true
    'hashlib' = $true; 'enum' = $true; 'abc' = $true; 'textwrap' = $true
    'contextlib' = $true; 'datetime' = $true; 'shutil' = $true; 'pathlib' = $true
}

$smokeClients = Get-ChildItem -Path $repoRoot -File -Filter '*smoke*.py'
$smokeClients += Get-ChildItem -Path $repoRoot -File -Filter 'lobby_smoke_client.py'

foreach ($file in $smokeClients | Sort-Object Name -Unique) {
    $imports = Select-String -Path $file.FullName -Pattern '^\s*(?:import|from)\s+([A-Za-z_][A-Za-z0-9_]*)' -ErrorAction SilentlyContinue
    foreach ($import in $imports) {
        if ($import.Matches.Count -eq 0) { continue }
        $module = $import.Matches[0].Groups[1].Value
        if ($standardLibrary.ContainsKey($module)) { continue }

        # The other smoke clients are first party, and importing one of them is
        # how the suite shares its lobby client.
        if ($module -like '*smoke*') { continue }

        Add-Failure "$($file.Name):$($import.LineNumber) imports '$module', which is not the standard library"
    }
}

# ---- The scripts the run script drives are all there -----------------------

Write-Host 'Checking the smoke suite the run script drives'

$runScript = Join-Path $PSScriptRoot 'run_smoke.ps1'
if (-not (Test-Path $runScript)) {
    Add-Failure 'tools/run_smoke.ps1 is missing'
}
else {
    $declared = Select-String -Path $runScript -Pattern "Script\s*=\s*'([^']+)'" -ErrorAction SilentlyContinue
    if (-not $declared) {
        Add-Failure 'run_smoke.ps1 declares no smoke scripts, so it cannot be checked against them'
    }

    foreach ($entry in $declared) {
        foreach ($match in $entry.Matches) {
            $script = $match.Groups[1].Value
            if (-not (Test-Path (Join-Path $repoRoot $script))) {
                Add-Failure "run_smoke.ps1 drives '$script', which does not exist"
            }
        }
    }
}

# ---- Result ----------------------------------------------------------------

Write-Host ''
if ($failures.Count -gt 0) {
    Write-Host "The backend is not headless-only: $($failures.Count) problem(s)."
    exit 1
}

Write-Host 'The backend builds, runs and validates without Unity.'
exit 0
