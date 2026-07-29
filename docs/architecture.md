# Architecture

## Why a layered architecture at all

Most Playwright repositories are organised as `pages/`, `fixtures/`, `helpers/`, `tests/`. That works,
and for a suite of twenty specs it is the right answer — this design would be over-engineering.

The difference the gTAA makes is not the boxes; it is that it names **dependency direction** and
splits the one box that always rots. `helpers/` has no rule attached to it, so it accumulates SUT
knowledge, driver access, configuration and assertions until every layer imports it and nothing can be
reused or replaced independently. The gTAA replaces that single bucket with named seams and one
rule — *test scripts never call the core libraries directly* — which in .NET becomes a compile-time
constraint rather than a code-review convention.

The honest limit: this pays off at multi-product, multi-team, long-lived scale, which is exactly the
motivation ISTQB gives for it. On a small suite it is ceremony. This repository uses it because
demonstrating and defending the architecture *is* the point.

---

## Mapping to the standard

The four projects are named for the layers in the request. Two things need stating precisely, because
an interviewer holding the current syllabus will notice otherwise.

**1. v2.0 recast the layers as capabilities.** CTAL-TAE v2.0 (GA 2024-05-03) presents test generation,
definition, execution and adaptation under the heading *"Capabilities provided by test automation
tools and libraries"* (§3.1.1). Only *test adaptation layer* remains in the chapter keyword list and
index. The 2016 edition, which does say "layers" (§3.1.1–3.1.5), was retired for English exams on
2025-06-12.

**2. "Utility" is not a gTAA layer** in either edition. It is a practitioner addition. What it
actually corresponds to is the configuration-management concern.

### The mapping

| Project | 2016 four-layer model | v2.0 three-layer TAF model (§3.1.3) | Contents |
|---|---|---|---|
| **Utility** | *Not a layer.* Configuration management of a TAS (§3.1.6) + the data-derivation part of Test Generation (§3.1.2) | Core libraries | `IConfiguration` binding and validation, Serilog behind `ILogger<T>`, NUnit log sink, DI registration, artifact paths, credentials, Bogus builders |
| **Adaptation** | Test Adaptation Layer (§3.1.5) | Core libraries + business logic | `IBrowserSession`, `ISessionFactory`, session bootstrapper, page objects (`internal`), browser installer |
| **Execution** | Test Execution Layer (§3.1.4) | Between core libraries and test scripts | `AutomationRuntime` composition root, `SutReadinessGate`, `E2ETestBase`, `ArtifactCollector` |
| **Definition** | Test Definition Layer (§3.1.3) | Test scripts + business logic | `ShopFlow` and the assertion facades (flow model pattern, §3.1.5), the `[Test]` methods, `ArchitectureTests` |

Test Generation is implemented only as its data-derivation slice (`RegistrationDataFactory`). There is
no model-based test generation here, and v2.0 §3.1.1 explicitly marks generation as optional.

---

## Dependency direction

```
Definition  →  Execution  →  Adaptation  →  Utility
```

Enforced three ways, in increasing order of what they can catch:

1. **MSBuild** rejects a circular `ProjectReference` graph outright (MSB4006 / NU1108). A
   folder-per-layer design inside one project gets none of this — that is the answer to *"why four
   projects instead of four folders?"*
2. **`internal` visibility.** Every Playwright-touching page object is `internal sealed`. No test can
   reach a locator, even deliberately.
3. **`ArchitectureTests`** covers what the compiler cannot see — see below.

### The rules that are tested

| Rule | Why it matters |
|---|---|
| Only Adaptation depends on `Microsoft.Playwright` | The whole point of the abstraction. If Execution needs a Playwright type, `IBrowserSession` has been bypassed. |
| Test fixtures do not reference the Adaptation assembly | v2.0 §3.1.3: *"no direct calls should be made to the core libraries from test scripts."* |
| Layer dependencies point one way | Utility depends on nothing; Adaptation not on Execution; Execution not on Definition. |
| Page objects are not publicly visible | A public page object would let a test bypass the contract. |
| Adaptation's public API exposes no Playwright types | Reflection over every exported member's parameters, returns and properties, unwrapping `Task<T>`. |

These run in the same `dotnet test` as everything else and finish in milliseconds.

**This rule set has already earned its keep.** The first version of `ShopFlow` exposed the Adaptation
page contracts directly (`public IBasketPage Basket => session.Basket`). It read perfectly well and
compiled, but it meant every fixture carried a compile-time dependency on the adapters — the exact
coupling the rule forbids. The architecture test failed and named the three offending fixtures. The
fix was the assertion facades in `Flows/Assertions.cs`, which is where the layering became real rather
than nominal.

---

## Notable design decisions

Each has an ADR in [adr/](adr/). In brief:

**No `PageTest` base class.** `Microsoft.Playwright.NUnit`'s base classes occupy C#'s single
inheritance slot and expose `IPage` as a protected property, which would put a Playwright type on the
inherited surface of every test class. Reading their source shows they also do not capture traces,
screenshots or video on failure — a widespread misconception carried over from the JavaScript runner.
So the cost of rolling our own is small and mostly replaced: one shared browser with a context per
test, and configuration from `appsettings.json` instead of the `<Playwright>` runsettings node.
The solution therefore references `Microsoft.Playwright`, not `Microsoft.Playwright.NUnit`.

**Fluent C# rather than Gherkin.** Reqnroll would map more literally onto the syllabus, but it adds a
binding layer whose value depends on non-technical stakeholders actually reading the features. A
fluent facade gets the same separation with less machinery and stays refactorable.

**Readiness is checked twice.** The compose healthcheck gates `docker compose up --wait`; the
`SutReadinessGate` re-checks from inside the run. They cover different failure modes — the healthcheck
says nothing when someone points the suite at an already-running instance or a remote environment.

**No `[Retry]`.** Flaky tests are quarantined with `[Category("Flaky")]` and excluded by filter, so
the instability stays visible rather than being hidden behind a green build. `[Retry(n)]` also counts
total attempts rather than retries, does not retry unexpected exceptions unless `RetryExceptions` is
set, and does not re-run `[OneTimeSetUp]`, so fixture-level corruption survives the retry.

---

## Synchronisation: the part that decides whether a UI suite is trusted

Every failure encountered while building this was a synchronisation bug, not a locator bug. The
patterns that fixed them are worth stating, because they generalise.

**Wait for the effect, never for a duration.** Adding to the basket fires an async request. Clicking
and then navigating races it: the badge updates optimistically so the UI looks right, while the basket
page fetches and renders an empty table. The symptom reads as an application bug. The fix is
`ClickAndAwaitResponseAsync`, which ties a click's completion to the response it triggers. A
`Task.Delay` would paper over the same race and still fail on a slower CI runner.

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

**A response is not the same as the effect of that response.** This one caused the longest-lived flake
in the build, and it is worth the detail. Registration returned HTTP 201, so the account existed. The
next step filled the login form and got a 401. The obvious readings — a Bogus threading bug, SQLite
contention under parallel writes — were both wrong: a pure API probe running 20 concurrent
register-then-login cycles passed 20/20, which ruled the server out entirely.

The actual sequence was that after a successful registration Juice Shop routes *itself* to the login
page. The test filled the form during that re-render, Angular replaced the component, and the typed
values were discarded. An empty form was then submitted and correctly rejected. The failure surfaced
two or three steps later as a product card that was "not visible" — because the browser had been
redirected back to login and was no longer on the catalogue at all.

Three changes, each of which independently would have made this diagnosable:

- `RegisterAsync` waits for the registration form to be torn down before returning, so the caller
  receives a settled page.
- `SignInAsync` asserts the email field still holds what was typed before submitting, so a discarded
  fill fails immediately and accurately instead of becoming a 401.
- `AddToBasketAsync` asserts its response status, so an unauthenticated add says *"HTTP 401 — the
  sign-in had not taken effect"* rather than letting the redirect cascade into an unrelated failure.

The general lesson: when an action's completion is defined by a server response, waiting for that
response is necessary but not sufficient. The client still has to *apply* it. Wait for the effect that
the next step depends on, and assert status codes at the boundary that produced them — otherwise a
failure travels a long way from its cause before anyone sees it.

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
