# ADR-0004: Run tests on the host against a containerised, digest-pinned SUT

**Status:** Accepted

## Context

The suite must run identically on any machine from a fresh clone. The SUT can be containerised with
the test runner either on the host or in a second container.

Juice Shop's packaging constrains the options:

- The runtime image is **distroless** (`gcr.io/distroless/nodejs24-debian13`, `USER 65532`): no shell,
  no curl, no wget.
- It defines **no `HEALTHCHECK`**, and takes 20–60s to become ready because it drops and re-seeds its
  entire SQLite database on every boot.
- `latest` and `v20.1.1` carry **different digests** despite being the same release.

## Decision

`docker-compose.yml` runs only the SUT. Tests run on the host.

- Pin by digest: `bkimminich/juice-shop:v20.1.1@sha256:cd58d79c...`
- Bind to `127.0.0.1:3000:3000`, never `0.0.0.0`.
- Define a healthcheck in exec form invoking the bundled Node binary, so
  `docker compose up --wait` means "ready", not "process started".
- Mount `config/e2e.yml` and set `NODE_ENV=e2e` for deterministic, notification-free behaviour.
- Re-check readiness from inside the run via `HttpSutReadinessGate` in the Adaptation layer, reached
  through the `ISutReadinessGate` port so the framework never learns the protocol.

## Alternatives considered

**Containerise the runner too** (`mcr.microsoft.com/playwright/dotnet`). Genuinely self-contained and
removes the .NET SDK prerequisite. Rejected as the default because debugging a failing test in an IDE
becomes materially harder, which is the single most common thing a reviewer will try. It also demands
the image's Playwright version exactly match the NuGet version, and the official Playwright images are
not published as multi-arch manifests — Apple Silicon needs an explicit `-arm64` tag. Straightforward
to add later as a compose profile.

**GitHub Actions service containers** instead of compose. Rejected: service containers gate on the
image's own `HEALTHCHECK`, which this image does not have, so the job would start against a port that
is open but not serving. Compose also means one file serves both local development and CI.

## Consequences

- Prerequisites are Docker and the .NET 10 SDK. Nothing else — notably **not** PowerShell 7, which is
  absent from a default Windows install and would break the one-command promise (see ADR-0002's
  shell-free browser install).
- Reproducible across time: a pinned digest cannot be re-pushed underneath the suite.
- `docker compose restart` is a complete state reset, so no volume is needed — and mounting one at
  `/juice-shop/data` would only risk permission errors against UID 65532.
- The healthcheck is coupled to an undocumented path (`/nodejs/bin/node`). Verified against the image;
  `HttpSutReadinessGate` is the backstop if a future image moves it.
