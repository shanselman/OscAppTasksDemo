[CmdletBinding()]
param(
    [ValidateSet('Terminal', 'Standalone')][string]$Mode = 'Terminal',
    [ValidateSet('title-only', 'conversation', 'success', 'failure', 'indeterminate', 'warning', 'unknown', 'crash')]
    [string]$Scenario = 'conversation',
    [ValidateRange(0, 10000)][int]$DelayMs = 1600,
    [ValidateSet('x64', 'ARM64')][string]$Platform = 'x64',
    [switch]$Build,
    [switch]$ValidateOnly
)
$ErrorActionPreference = 'Stop'
if ($Mode -eq 'Standalone' -and ($PSBoundParameters.ContainsKey('Scenario') -or $PSBoundParameters.ContainsKey('DelayMs'))) {
    throw 'Standalone scenarios are selected in the UI. Scenario and DelayMs apply only to Terminal mode.'
}

$buildScript = Join-Path $PSScriptRoot 'Demo.ps1'
$agent = Join-Path $PSScriptRoot 'artifacts\agent\OscTasks.Agent.exe'
$rid = 'win-' + $Platform.ToLowerInvariant()
$hostOutput = Join-Path $PSScriptRoot "OscTasks.Host\bin\$Platform\Debug\net10.0-windows10.0.26100.0\$rid"
$agentArguments = @($Scenario, '--delay-ms', $DelayMs.ToString([Globalization.CultureInfo]::InvariantCulture))
$buildArguments = if ($Mode -eq 'Terminal') { @('-AgentOnly', '-Platform', $Platform) } else { @('-BuildOnly', '-Platform', $Platform) }
if ($ValidateOnly) {
    [pscustomobject]@{
        Mode = $Mode
        Executable = $(if ($Mode -eq 'Terminal') { $agent } else { 'winapp' })
        Arguments = $(if ($Mode -eq 'Terminal') { $agentArguments } else { @('run', $hostOutput, '--debug-output') })
        BuildRequested = [bool]$Build
        BuildScript = $buildScript
        BuildArguments = $buildArguments
    } | ConvertTo-Json -Depth 3
    return
}

Push-Location $PSScriptRoot
try {
    if ($Build) {
        if ($Mode -eq 'Terminal') { & $buildScript -AgentOnly -Platform $Platform }
        else { & $buildScript -BuildOnly -Platform $Platform }
        if ($LASTEXITCODE -ne 0) { throw "Demo build failed with exit code $LASTEXITCODE" }
    }
    if ($Mode -eq 'Terminal') {
        if (-not (Test-Path -LiteralPath $agent -PathType Leaf)) {
            throw 'Published agent missing. Run .\Show-Demo.ps1 -Build to publish it first.'
        }
        Write-Host 'Marker-free agent fixture: this script neither starts Terminal nor supplies shell lifecycle.'
        Write-Host 'Use an integrated shell in your custom Terminal; enable title publication separately only if appropriate.'
        & $agent @agentArguments
    }
    else {
        if (-not (Test-Path -LiteralPath (Join-Path $hostOutput 'OscTasks.Host.exe') -PathType Leaf)) {
            throw 'Packaged host build missing. Run .\Show-Demo.ps1 -Mode Standalone -Build first.'
        }
        Get-Command winapp -ErrorAction Stop | Out-Null
        & winapp run $hostOutput --debug-output
    }
    $result = $LASTEXITCODE
}
finally {
    Pop-Location
}
exit $result
