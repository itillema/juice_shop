<#
.SYNOPSIS
    Starts the system under test and runs the automated test suite.

.DESCRIPTION
    Deliberately thin. It only starts the container and calls `dotnet test` — browser installation,
    SUT readiness and artifact handling are the framework's job, not a shell script's, so the same
    behaviour applies whether you run this script, `dotnet test` directly, or the tests from an IDE.

    Written for Windows PowerShell 5.1, which is what Windows actually ships. It intentionally does
    not require PowerShell 7 (`pwsh`), because requiring a separate install would undercut the
    point of a one-command start.

.PARAMETER Filter
    NUnit filter expression, e.g. "TestCategory=Smoke".

.PARAMETER Headed
    Run with a visible browser instead of headless.

.EXAMPLE
    .\run.ps1
    .\run.ps1 -Filter "TestCategory=Smoke"
    .\run.ps1 -Headed
#>
[CmdletBinding()]
param(
    [string] $Filter,
    [switch] $Headed
)

$ErrorActionPreference = 'Stop'
Set-Location -Path $PSScriptRoot

Write-Host '==> Starting OWASP Juice Shop' -ForegroundColor Cyan
# --wait blocks until the healthcheck passes. Without the healthcheck defined in
# docker-compose.yml it would only wait for "running", which is ~30s too early.
docker compose up -d --wait --wait-timeout 240
if ($LASTEXITCODE -ne 0) {
    throw "Juice Shop did not become healthy. Check 'docker compose logs juice-shop'."
}

Write-Host '==> Building' -ForegroundColor Cyan
dotnet build -c Release
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

if ($Headed) {
    # Environment variables override appsettings.json. Double underscore is the section separator.
    $env:AUTOMATION__BROWSER__HEADLESS = 'false'
}

$testArgs = @('test', '-c', 'Release', '--no-build', '--settings', '.runsettings',
              '--logger', 'trx;LogFileName=test-results.trx')
if ($Filter) { $testArgs += @('--filter', $Filter) }

Write-Host '==> Running tests' -ForegroundColor Cyan
& dotnet $testArgs
$testExitCode = $LASTEXITCODE

Write-Host ''
if ($testExitCode -eq 0) {
    Write-Host 'All tests passed.' -ForegroundColor Green
} else {
    Write-Host 'Tests failed. Traces and screenshots:' -ForegroundColor Yellow
    Write-Host '  src\JuiceShop.Automation.Execution\bin\Release\net10.0\artifacts\'
    Write-Host '  View a trace by dragging the .zip onto https://trace.playwright.dev'
}

Write-Host ''
Write-Host 'Juice Shop is still running. Stop it with: docker compose down' -ForegroundColor DarkGray

exit $testExitCode
