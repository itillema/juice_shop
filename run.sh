#!/usr/bin/env bash
#
# Starts the system under test and runs the automated test suite.
#
# Deliberately thin. It only starts the container and calls `dotnet test` — browser installation,
# SUT readiness and artifact handling are the framework's job, not a shell script's, so the same
# behaviour applies whether you run this script, `dotnet test` directly, or the tests from an IDE.
#
# Usage:
#   ./run.sh
#   ./run.sh --filter "TestCategory=Smoke"
#   ./run.sh --headed

set -euo pipefail
cd "$(dirname "$0")"

FILTER=""
while [[ $# -gt 0 ]]; do
    case "$1" in
        --filter) FILTER="$2"; shift 2 ;;
        --headed) export AUTOMATION__BROWSER__HEADLESS=false; shift ;;
        *) echo "Unknown option: $1" >&2; exit 2 ;;
    esac
done

echo "==> Starting OWASP Juice Shop"
# --wait blocks until the healthcheck passes. Without the healthcheck defined in
# docker-compose.yml it would only wait for "running", which is ~30s too early.
docker compose up -d --wait --wait-timeout 240

echo "==> Building"
dotnet build -c Release

echo "==> Running tests"
TEST_ARGS=(test -c Release --no-build --settings .runsettings --logger "trx;LogFileName=test-results.trx")
[[ -n "$FILTER" ]] && TEST_ARGS+=(--filter "$FILTER")

set +e
dotnet "${TEST_ARGS[@]}"
TEST_EXIT=$?
set -e

echo
if [[ $TEST_EXIT -eq 0 ]]; then
    echo "All tests passed."
else
    echo "Tests failed. Traces and screenshots:"
    echo "  src/JuiceShop.Automation.Execution/bin/Release/net10.0/artifacts/"
    echo "  View a trace by dragging the .zip onto https://trace.playwright.dev"
fi

echo
echo "Juice Shop is still running. Stop it with: docker compose down"

exit $TEST_EXIT
