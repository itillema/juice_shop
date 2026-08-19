<#
.SYNOPSIS
    Starts the system under test and runs the automated test suite.

.DESCRIPTION
    Thin by design: browser install, SUT readiness and artifacts are the framework's job, so this
    script, a bare `dotnet test` and an IDE run all behave the same.

    Targets Windows PowerShell 5.1, not `pwsh` — requiring a separate install would undercut a
    one-command start.

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
# --wait blocks on the healthcheck; without it, "running" is ~30s too early.
docker compose up -d --wait --wait-timeout 240
if ($LASTEXITCODE -ne 0) {
    throw "Juice Shop did not become healthy. Check 'docker compose logs juice-shop'."
}

Write-Host '==> Checking for source files hidden by .gitignore' -ForegroundColor Cyan
# Only works locally, where an ignored file still exists on disk — CI never checks one out.
# MSBuild ignores .gitignore, so the build below would compile a file CI can never see.
$shadowed = git ls-files --others --ignored --exclude-standard -- 'src/*' `
    ':(exclude)src/*/bin/*' ':(exclude)src/*/obj/*'
if ($shadowed) {
    Write-Host 'These files exist on disk but .gitignore excludes them. They will never reach CI:' -ForegroundColor Red
    $shadowed | ForEach-Object { Write-Host "  $_"; git check-ignore -v -- $_ }
    throw 'Anchor the offending .gitignore pattern with a leading slash before continuing.'
}

Write-Host '==> Building' -ForegroundColor Cyan
dotnet build -c Release
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

if ($Headed) {
    # Double underscore is the section separator; env vars override appsettings.json.
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
