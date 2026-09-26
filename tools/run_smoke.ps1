<#
.SYNOPSIS
Runs the OpenGSServer smoke suite against a temporary local server.

.DESCRIPTION
Builds the server, starts it on disposable ports, waits for the lobby
listener, runs every lobby/mission smoke client, and always stops the
server again. Nothing in this script changes server behaviour; it only
repeats the existing manual steps in a single command.

.PARAMETER Configuration
Build configuration. Defaults to Debug.

.PARAMETER Only
Run only the named smoke client. Defaults to all of them.

.EXAMPLE
.\run_smoke.ps1

.EXAMPLE
.\run_smoke.ps1 -Only mission
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Debug',
    [string[]] $Only = @()
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot 'OpenGSServer.sln'

# Disposable ports so a developer can keep a real server running.
$lobbyPort = 64010
$matchTcpPort = 64011
$matchUdpPort = 64012
$managementPort = 64013
$logFile = Join-Path $repoRoot ('smoke_server_{0}.log' -f (Get-Date -Format 'yyyyMMdd_HHmmss'))

$smokes = @(
    @{ Name = 'lobby'; Script = 'lobby_smoke_client.py' },
    @{ Name = 'two_player_loading'; Script = 'two_player_loading_smoke.py' },
    @{ Name = 'mission'; Script = 'mission_room_lifecycle_smoke.py' }
    @{ Name = 'reconnect'; Script = 'reconnect_smoke.py' }
)

if ($Only.Count -gt 0) {
    $smokes = $smokes | Where-Object { $Only -contains $_.Name }
    if ($smokes.Count -eq 0) {
        throw "No smoke client matched -Only. Valid names: lobby, two_player_loading, mission, reconnect"
    }
}

function Stop-SmokeServer {
    param([object] $Process)

    if ($null -eq $Process) { return }
    if ($Process.HasExited) { return }
    Write-Host 'Stopping smoke server...'
    try {
        $null = $Process.CloseMainWindow()
    } catch {
        # The server runs without a console window in some hosts; fall through.
    }
    if (-not $Process.WaitForExit(10000)) {
        $Process.Kill()
        $Process.WaitForExit()
    }
}

function Test-LobbyPort {
    param([int] $Port, [int] $TimeoutSeconds = 60)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $client = [System.Net.Sockets.TcpClient]::new()
            $client.Connect('127.0.0.1', $Port)
            $client.Close()
            return $true
        } catch {
            Start-Sleep -Milliseconds 300
        }
    }
    return $false
}

$server = $null
$results = @()
$exitCode = 0

try {
    Write-Host 'Building solution...'
    & dotnet build $solution --configuration $Configuration --nologo
    if ($LASTEXITCODE -ne 0) {
        throw 'Build failed.'
    }

    $dll = Join-Path $repoRoot ('bin\{0}\net10.0-windows7.0\win-x64\OpenGSServer.dll' -f $Configuration)
    if (-not (Test-Path $dll)) {
        throw "Server assembly not found at $dll"
    }

    Write-Host "Starting server (lobby $lobbyPort). Log: $logFile"
    $server = Start-Process -FilePath 'dotnet' -ArgumentList @(
        $dll,
        '--lobby-port', $lobbyPort,
        '--match-tcp-port', $matchTcpPort,
        '--match-udp-port', $matchUdpPort,
        '--management-port', $managementPort,
        '--no-console'
    ) -PassThru -NoNewWindow -RedirectStandardOutput $logFile -RedirectStandardError "$logFile.err"

    if (-not (Test-LobbyPort -Port $lobbyPort)) {
        throw "Lobby port $lobbyPort did not open. See $logFile"
    }

    foreach ($smoke in $smokes) {
        $script = Join-Path $repoRoot $smoke.Script
        Write-Host ''
        Write-Host ("=== {0} ===" -f $smoke.Name)
        & python $script --host 127.0.0.1 --port $lobbyPort
        $results += [pscustomobject]@{ Name = $smoke.Name; ExitCode = $LASTEXITCODE }
        if ($LASTEXITCODE -ne 0) {
            $exitCode = 1
        }
    }
} catch {
    Write-Host "Smoke suite aborted: $_" -ForegroundColor Red
    $exitCode = 1
} finally {
    Stop-SmokeServer -Process $server
    foreach ($file in @($logFile, "$logFile.err")) {
        if (Test-Path $file) { Remove-Item $file -Force }
    }
}

Write-Host ''
Write-Host '--- smoke results ---'
foreach ($result in $results) {
    $status = if ($result.ExitCode -eq 0) { 'PASS' } else { 'FAIL' }
    Write-Host ("{0,-20} {1}" -f $result.Name, $status)
}

exit $exitCode
