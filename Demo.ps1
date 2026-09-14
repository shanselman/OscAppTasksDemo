param(
    [switch]$BuildOnly,
    [switch]$Test,
    [ValidateSet('x64', 'ARM64')][string]$Platform = 'x64'
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

function Assert-Exit([string]$Step) {
    if ($LASTEXITCODE -ne 0) { throw "$Step failed with exit code $LASTEXITCODE" }
}

if ($Test) {
    dotnet build .\OscTasks.Agent -v quiet
    Assert-Exit 'Agent build'
    dotnet run --project .\OscTasks.Tests -- .\OscTasks.Agent\bin\Debug\net10.0\OscTasks.Agent.dll
    Assert-Exit 'Parser/lifecycle/process tests'
    exit 0
}

$cli = Get-Command winapp -ErrorAction Stop
$version = (& $cli.Source --version).Trim()
if ([version]($version -split '-')[0] -lt [version]'0.3') {
    throw "winapp $version is too old. Run /winui-setup or resolve the installed current CLI alias."
}
if ((Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock').AllowDevelopmentWithoutDevLicense -ne 1) {
    throw 'Developer Mode must be enabled. Run /winui-setup.'
}

$rid = 'win-' + $Platform.ToLowerInvariant()
dotnet publish .\OscTasks.Agent -c Debug -r $rid --self-contained false -o .\artifacts\agent -v quiet
Assert-Exit 'Agent publish'
dotnet build .\OscTasks.Host -p:Platform=$Platform -p:RuntimeIdentifier=$rid -v minimal
Assert-Exit 'Packaged host build'
if ($BuildOnly) { exit 0 }
$output = Join-Path $PSScriptRoot "OscTasks.Host\bin\$Platform\Debug\net10.0-windows10.0.26100.0\$rid"
winapp run $output --debug-output
Assert-Exit 'Packaged host launch'
