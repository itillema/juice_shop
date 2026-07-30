# ADR-0002: Own the browser lifecycle instead of inheriting `PageTest`

**Status:** Accepted

## Context

`Microsoft.Playwright.NUnit` ships base classes — `PageTest`, `ContextTest`, `BrowserTest`,
`PlaywrightTest` — that manage the Playwright lifecycle. `PageTest` is the conventional starting point
for a .NET Playwright suite and gives a ready `IPage` per test.

Two properties of these classes conflict with a layered design:

1. They occupy C#'s single inheritance slot. `MyBase : PageTest` is the only way to combine them with
   a custom base; there is no interface or fixture-injection escape hatch. The decision is effectively
   irreversible once fixtures are written.
2. `PageTest` exposes `IPage Page` as a public property. Every test class in the solution would
   inherit a Playwright type on its surface, which defeats the confinement this architecture exists to
   demonstrate.

The usual argument for accepting that cost is that the base classes provide automatic failure
artifacts. Reading their source (`WorkerAwareTest`, `ContextTest`, `PageTest`) shows they contain **no
tracing, screenshot or video code at all** — that behaviour belongs to the JavaScript runner's
`trace: 'on-first-retry'` configuration, which has no .NET equivalent. The official .NET documentation
tells you to write the `Tracing.StartAsync` / `StopAsync` teardown yourself either way.

## Decision

Reference `Microsoft.Playwright` only — not `Microsoft.Playwright.NUnit` — and own the lifecycle in
the Utility layer, which is where the driver belongs: `PlaywrightSessionFactory` holds one
`IPlaywright` and one `IBrowser` for the run, and `E2ETestBase` takes a fresh `IBrowserContext` per
test.

## What is actually given up

| Feature | Replacement |
|---|---|
| Worker-scoped browser reuse and recycling | One shared browser, context per test. Contexts cost milliseconds; browsers cost ~1s. |
| `<Playwright>` `.runsettings` binding | `appsettings.json` bound and validated in the Utility layer — which also sidesteps the trap where enabling Microsoft.Testing.Platform makes that runsettings section silently do nothing. |
| `Expect()` instance helpers | `using static Microsoft.Playwright.Assertions;` — the same methods. |
| Default `data-testid` test-id attribute | Not needed; Juice Shop uses stable `id` and `aria-label` attributes. |
| Automatic failure artifacts | Never existed. `ArtifactCollector` provides them. |

## Consequences

- No Playwright type reaches the test cases. Page objects in Definition drive the browser through
  the session; everything above them is free of it, and `ArchitectureTests` enforces that.
- Artifact policy is explicit and controllable: trace always started, written only on failure.
- Slightly more lifecycle code to own, and the constraint that `[SetUp]` ordering between base and
  derived classes must be understood (NUnit runs base first, which `JuiceShopTest` relies on).
- Anyone arriving from a conventional Playwright .NET repository will expect `PageTest` and needs this
  ADR to explain its absence.
