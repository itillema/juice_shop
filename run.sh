#!/usr/bin/env bash
#
# Starts the system under test and runs the automated test suite.
#
# Thin by design: browser install, SUT readiness and artifacts are the framework's job, so this
# script, a bare `dotnet test` and an IDE run all behave the same.
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
# --wait blocks on the healthcheck; without it, "running" is ~30s too early.
docker compose up -d --wait --wait-timeout 240

echo "==> Checking for source files hidden by .gitignore"
# Only works locally, where an ignored file still exists on disk — CI never checks one out.
# MSBuild ignores .gitignore, so the build below would compile a file CI can never see.
shadowed=$(git ls-files --others --ignored --exclude-standard -- 'src/*' \
    ':(exclude)src/*/bin/*' ':(exclude)src/*/obj/*')
if [[ -n "$shadowed" ]]; then
    echo "These files exist on disk but .gitignore excludes them. They will never reach CI:" >&2
    echo "$shadowed" >&2
    echo "Anchor the offending .gitignore pattern with a leading slash before continuing." >&2
    exit 1
fi

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
