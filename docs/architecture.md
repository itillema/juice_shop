# Architecture

## Why a layered architecture at all

Most Playwright repositories are organised as `pages/`, `fixtures/`, `helpers/`, `tests/`. That works,
and for a suite of twenty specs it is the right answer — this design would be over-engineering.

The difference a layered architecture makes is not the boxes; it is that it names **dependency
direction** and splits the one box that always rots. `helpers/` has no rule attached to it, so it
accumulates application knowledge, driver access, configuration and assertions until every layer
imports it and nothing can be reused or replaced independently. Naming the layers replaces that
bucket with seams that have rules, and in .NET those rules become compile-time constraints rather
than code-review conventions.

The honest limit: this pays off at multi-product, multi-team, long-lived scale. On a small suite it
is ceremony. This repository uses it because demonstrating and defending the architecture *is* the
point.

---

## The four layers

| Layer | Responsibility | Contents here |
|---|---|---|
| **Execution** | The test cases/scripts | `Tests/` fixtures, `TestCategories`, `Architecture/` rules, `JuiceShopTest`, `GlobalSetup`, `appsettings.json`, `allureConfig.json` |
| **Definition** | What the SUT is and how a business action is performed against it | `Pages/` page objects and contracts, `Flows/` business actions and assertion facades, `TestData/` credentials and generators |
| **Adaptation** | Connections to everything outside the test system — external services, protocols, integrations, data stores | `Sut/HttpSutReadinessGate` |
| **Utility** | The framework | `Driver/` Playwright session and browser lifecycle, `Runtime/` run lifecycle and DI machinery, `Artifacts/` capture and reporting paths, `Configuration/`, `Logging/`, `Testing/E2ETestBase`, `Sut/ISutReadinessGate` |

### What goes where, and why it is not arbitrary

The test that decides which layer a file belongs in is *what makes it change*:

- A relocated button changes a **page object**. A new step in checkout changes a **flow**. Neither
  reaches the test cases.
- A new acceptance criterion changes a **test case** and nothing else.
- Moving from Playwright to another driver, or changing how traces are captured, changes **Utility**
  and nothing else.
- Pointing at a different environment, or adding an API client for test-data setup, changes
  **Adaptation** and nothing else.

Nothing SUT-specific is left in Utility. The three cookies Juice Shop needs before its first
navigation — which suppress the welcome dialog and cookie banner and pin the locale — are
configuration values (`Browser:SessionCookies`), not driver code, so the driver is genuinely
application-agnostic.

---

## Dependency direction

```
Execution ──► Definition ──► Utility
    └───────► Adaptation ──► Utility
```

Utility has **no project references**. Definition and Adaptation are siblings that do not know about
each other. Execution sits on top and is the only project that references everything, because it is
the composition root.

### The cycle, and how it is broken

Taken literally, the layer responsibilities contain a cycle. The run lifecycle belongs to Utility,
and it must not start tests against an application that is still booting — so it needs to reach the
SUT. But reaching an external service over a protocol is Adaptation's job, and Adaptation needs
Utility's configuration and logging.

This is resolved with a port and an adapter:

- `ISutReadinessGate` is **declared in Utility** — the framework states what it needs.
- `HttpSutReadinessGate` **implements it in Adaptation** — the integration decides how.
- `GlobalSetup` **binds them in Execution** — the composition root, the one place that legitimately
  knows about every layer at once.

`AutomationRuntime` therefore resolves the gate from the container and awaits it, without Utility
ever referencing Adaptation. The same seam is how an API client or a database fixture would be
introduced later: declare the port where it is needed, implement it in Adaptation, bind it at the
top.

It awaits the gate on **first session use** rather than during assembly setup. The composition root
is a namespace-less `[SetUpFixture]`, so its `[OneTimeSetUp]` runs before *every* fixture — awaiting
the gate there charged the SUT startup cost to tests that never touch the application, and made the
architecture rules fail after a 150s readiness timeout on any machine without the container running.
`AutomationRuntime.StartAsync` now builds only configuration and the container;
`EnsureBrowsingAsync` holds the gate and the browser launch behind a `Lazy<Task<ISessionFactory>>`
that `E2ETestBase` triggers. Deriving from that base is what declares a test needs a live
application.

The alternative — putting the HTTP call directly into the lifecycle code — would drag an external
protocol into the framework layer and make Utility unusable against a SUT reached any other way.

### How the layering is enforced

Three mechanisms, in increasing order of what they can catch:

1. **MSBuild** rejects a circular `ProjectReference` graph outright (MSB4006 / NU1108). A
   folder-per-layer design inside one project gets none of this — that is the answer to *"why four
   projects instead of four folders?"*
2. **`internal` visibility.** Page objects and their contracts are internal to the Definition
   assembly. A test cannot name one, even deliberately. This turned out to be stronger than expected:
   making a page contract `public` does not merely trip an architecture rule, it fails to compile,
   because the contract exposes internal record types.
3. **`ArchitectureTests`** covers what the compiler cannot see.

### The rules that are tested

| Rule | Why it matters |
|---|---|
| Utility depends on no other layer | The framework must stay reusable against another product. |
| Adaptation and Definition do not depend on each other, or on Execution | Siblings; a flow that needs an integration goes through a port, not a shortcut. |
| Test cases do not depend on `Microsoft.Playwright` | Tests express intent, not browser mechanics. |
| Adaptation does not depend on `Microsoft.Playwright` | Adaptation speaks protocols. Driving a browser is the driver's job. |
| No public type in `Definition.Pages` | A public page object would let a test bypass the flow facade. |
| Definition's public API exposes no Playwright types | Reflection over every exported member, unwrapping `Task<T>`. |

These run in the same `dotnet test` as everything else and finish in milliseconds. Each was verified
by deliberately introducing the violation it describes and confirming it fails with a message that
names the offending type.

---

## Notable design decisions

Each has an ADR in [adr/](adr/). In brief:

**No `PageTest` base class.** `Microsoft.Playwright.NUnit`'s base classes occupy C#'s single
inheritance slot and expose `IPage` as a protected property, which would put a browser handle on the
inherited surface of every test class. Reading their source shows they also do not capture traces,
screenshots or video on failure — a widespread misconception carried over from the JavaScript runner.
So the cost of owning the lifecycle is small and mostly replaced: one shared browser with a context
per test, and configuration from `appsettings.json` rather than the `<Playwright>` runsettings node.

**Fluent C# rather than Gherkin.** Reqnroll adds a binding layer whose value depends on
non-technical stakeholders actually reading the features. A fluent facade gets the same separation
with less machinery and stays refactorable.

**Readiness is checked twice.** The compose healthcheck gates `docker compose up --wait`; the
readiness gate re-checks from inside the run. They cover different failure modes — the healthcheck
says nothing when someone points the suite at an already-running instance or a remote environment.

**No `[Retry]`.** Flaky tests are quarantined with `[Category("Flaky")]` and excluded by filter, so
the instability stays visible rather than hidden behind a green build. `[Retry(n)]` also counts total
attempts rather than retries, does not retry unexpected exceptions unless `RetryExceptions` is set,
and does not re-run `[OneTimeSetUp]`, so fixture-level corruption survives the retry.

---

## Synchronisation: the part that decides whether a UI suite is trusted

Every failure encountered while building this was a synchronisation bug, not a locator bug. The
patterns that fixed them generalise.

**Wait for the effect, never for a duration.** Adding to the basket fires an async request. Clicking
and then navigating races it: the badge updates optimistically so the UI looks right, while the basket
page fetches and renders an empty table. The symptom reads as an application bug. The fix is
`ClickAndAwaitResponseAsync`, which ties a click's completion to the response it triggers. A
`Task.Delay` would paper over the same race and still fail on a slower CI runner.

**A response is not the same as the effect of that response.** This caused the longest-lived flake in
the build. Registration returned HTTP 201, so the account existed; the next step filled the login form
and got a 401. The obvious readings — a thread-safety bug in the data generator, SQLite contention
under parallel writes — were both wrong: a pure API probe running 20 concurrent register-then-login
cycles passed 20/20, ruling the server out entirely.

The actual sequence was that after a successful registration Juice Shop routes *itself* to the login
page. The test filled the form during that re-render, Angular replaced the component, and the typed
values were discarded. An empty form was submitted and correctly rejected. The failure surfaced two
or three steps later as a product card that was "not visible" — because the browser had been
redirected back to login and was no longer on the catalogue at all.

Three changes, each of which independently would have made this diagnosable:

- `RegisterAsync` waits for the registration form to be torn down before returning.
- `SignInAsync` asserts the email field still holds what was typed before submitting.
- `AddToBasketAsync` asserts its response status, so an unauthenticated add says *"HTTP 401 — the
  sign-in had not taken effect"* rather than letting the redirect cascade into an unrelated failure.

The general lesson: when an action's completion is defined by a server response, waiting for that
response is necessary but not sufficient — the client still has to *apply* it. Wait for the effect the
next step depends on, and assert status codes at the boundary that produced them, or a failure travels
a long way from its cause before anyone sees it.

**Assert through the layer that owns the locator.** Every page contract exposes its own assertions, so
Playwright's auto-retrying `Expect` stays where the locators are. The alternative —
`Assert.That(await locator.TextContentAsync(), Is.EqualTo(...))` — is a single-shot read with no retry
and is the most common cause of flaky .NET Playwright suites. NUnit's constraint model is used only
for already-materialised, non-DOM values.

**Retry the interaction when the framework can swallow it.** Angular Material components pass every
actionability check — visible, enabled, stable — during a window when their own click handling is not
yet armed. The click is lost silently; the element even takes focus, so a screenshot shows a focused
control with a closed panel and every locator provably correct. `OpenOverlayAsync` retries the open a
bounded number of times, checking first whether the overlay is already open so it can never toggle a
successful one shut. This retries an *interaction*, not an assertion, and cannot mask a product bug:
if the overlay never opens, the test still fails.

**Make the page object idempotent about UI state.** The search control collapses to an icon but stays
expanded after a search, so expanding it unconditionally works once and hangs on every subsequent
search in the same session.

**Prefer the accessible path when the visual one is obstructed.** Angular Material's floating label
sits over the select's hit area, so Playwright correctly reports the click as intercepted. Clicking
the arrow wrapper — clear of the label — lands first time and still passes the full actionability
check. Forcing the click would have bypassed a real overlap.

---

## Test isolation

The seeded Juice Shop customers ship with items already in their baskets, so any test asserting "the
basket contains one line" against a shared account depends on the seed and on execution order. Tests
that need an account register their own instead, which makes them order-independent, parallel-safe,
and repeatable against the same container.

The container itself needs no reset between runs: Juice Shop calls `sequelize.sync({ force: true })`
on every boot, so `docker compose restart` is a complete state reset. That is also why no volume is
mounted at `/juice-shop/data` — it would buy nothing and risk permission errors, since the container
runs as UID 65532.

Parallelism is `ParallelScope.Fixtures`. Fixtures run concurrently, tests within a fixture run
sequentially. Fixture instances hold per-test state, so `ParallelScope.Children` or `.All` would race
on those fields. Verified stable at 4 and 8 workers.
