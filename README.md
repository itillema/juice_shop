# OWASP Juice Shop — UI Test Automation

[![e2e](https://github.com/YOUR-USERNAME/juice-shop-automation/actions/workflows/e2e.yml/badge.svg)](https://github.com/YOUR-USERNAME/juice-shop-automation/actions/workflows/e2e.yml)

A UI end-to-end automation solution built on **NUnit + Playwright for .NET 10**, structured as a
four-layer **gTAA** test automation architecture, testing a containerised
[OWASP Juice Shop](https://owasp.org/www-project-juice-shop/).

The system under test runs in Docker and is pinned by image digest, so a clone of this repository
produces the same result today and in a year.

```bash
git clone <this-repo> && cd juice-shop-automation
docker compose up -d --wait     # ~15s, blocks until the app is genuinely ready
dotnet test
```

That is the whole setup. **No PowerShell 7, no Node.js, no global tools** — browsers install
themselves on first run through Playwright's .NET API.

**Prerequisites:** Docker and the .NET 10 SDK. Nothing else.

---

## What this demonstrates

| | |
|---|---|
| **Layered architecture** | Four .NET projects with a single-direction dependency graph enforced by the compiler |
| **Architecture as tests** | 6 NetArchTest rules that fail the build if a layer boundary is crossed |
| **Deterministic SUT** | Image pinned by digest, config overlay mounted, container healthcheck for a distroless image |
| **Diagnosable failures** | Playwright trace + screenshot on failure only, attached to the test result |
| **Parallel-safe** | Fixture-level parallelism, isolated browser context per test, self-provisioning test data |
| **CI/CD** | GitHub Actions with test reporting, artifact upload, and an Allure report published to Pages |

---

## Architecture

Four layers, each a separate .NET project.

| Layer | Owns | Contents |
|---|---|---|
| **Execution** | The test cases | Fixtures, categories, assembly setup, architecture rules. Also the composition root. |
| **Definition** | What the SUT is, and how an action is performed against it | Page objects, flows, test data |
| **Adaptation** | Connections to anything outside the test system | External services, protocols, integrations, data stores |
| **Utility** | The framework itself | Configuration, logging, DI wiring, reporting, run lifecycle, Playwright driver, base classes |

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  Execution     Tests/          the test cases — business intent only        │
│                Architecture/   layering rules, asserted at test time        │
│                GlobalSetup     composition root: binds every layer          │
└───────────────┬──────────────────────────────────┬──────────────────────────┘
                │                                  │
┌───────────────▼──────────────────┐  ┌────────────▼──────────────────────────┐
│  Definition                      │  │  Adaptation                           │
│    Pages/     locators, atomic   │  │    Sut/  HTTP readiness integration   │
│               interactions       │  │                                       │
│    Flows/     business actions   │  │  ← where an API client, a database    │
│    TestData/  users, generators  │  │    fixture or a queue probe goes      │
└───────────────┬──────────────────┘  └────────────┬──────────────────────────┘
                │                                  │
┌───────────────▼──────────────────────────────────▼──────────────────────────┐
│  Utility     Driver/         Playwright session, browser lifecycle          │
│              Runtime/        run lifecycle, DI composition machinery        │
│              Artifacts/      trace + screenshot capture, reporting paths    │
│              Configuration/  strongly typed, validated settings             │
│              Logging/        Serilog behind ILogger<T>, NUnit sink          │
│              Testing/        E2ETestBase                                    │
│              Sut/            ISutReadinessGate — port, implemented above    │
│                                                                             │
│              ← no project references: depends on no other layer             │
└─────────────────────────────────────────────────────────────────────────────┘
```

A test reads as a sequence of business intentions:

```csharp
[Test]
[Category(TestCategories.Smoke)]
public async Task Customer_Can_Add_Product_To_Basket()
{
    await Shop.RegisterAndLoginAsync(NewAccount());

    await Shop.SearchAsync("Apple Juice");
    await Shop.AddToBasketAsync("Apple Juice (1000ml)");

    await Shop.OpenBasketAsync();
    await Shop.Basket.ShouldContainAsync("Apple Juice (1000ml)");
    await Shop.Basket.ShouldShowQuantityAsync("Apple Juice (1000ml)", 1);
}
```

No locators, no `IPage`, no waits. Those cannot appear here even by accident: page objects and their
contracts are `internal` to the Definition assembly, so the compiler stops a test from naming one,
and an architecture rule fails the build if a test references Playwright directly.

Full rationale in [docs/architecture.md](docs/architecture.md); decisions and rejected alternatives
in the [ADRs](docs/adr/).

### The one non-obvious edge

The run lifecycle lives in Utility and has to wait for the SUT before any test starts — but talking
to the SUT is Adaptation's job. Taken literally that is a cycle. It is resolved with a port and an
adapter: `ISutReadinessGate` is declared in Utility, implemented over HTTP in Adaptation, and bound
at the composition root in Execution. Utility therefore references no other project, and the
framework never learns which protocol the SUT speaks.

---

## Running

```bash
# Everything
dotnet test

# Just the fast smoke suite
dotnet test --filter "TestCategory=Smoke"

# Layering rules only — no browser, no SUT, runs in milliseconds
dotnet test --filter "TestCategory=Architecture"

# Watch it happen in a real browser
AUTOMATION__BROWSER__HEADLESS=false dotnet test --filter "TestCategory=Smoke"
```

Convenience wrappers that also start the container: `./run.ps1` (Windows PowerShell 5.1 compatible)
and `./run.sh`.

Categories: `Smoke`, `Regression`, `Authentication`, `Catalog`, `Basket`, `Architecture`.

### Configuration

`appsettings.json` in the Execution project — it sits next to the test binary because that is where
configuration is read from. Overridable per environment (`AUTOMATION_ENVIRONMENT=Ci`) and by
environment variable, where the separator is a **double** underscore:

```bash
AUTOMATION__SUT__BASEURL=http://juice-shop:3000
AUTOMATION__BROWSER__NAME=firefox
AUTOMATION__BROWSER__HEADLESS=false
AUTOMATION__BROWSER__SLOWMOMILLISECONDS=500     # useful when demoing a run
```

Note `Browser:SessionCookies`. Juice Shop needs three cookies set before the first navigation to
suppress the welcome dialog and cookie banner and to pin the locale. They live in configuration
rather than in the driver, so the driver itself carries no knowledge of the application under test.

### When a test fails

Failures write a screenshot and a full Playwright trace, attached to the test result and uploaded by
CI:

```
src/JuiceShop.Automation.Execution/bin/Release/net10.0/artifacts/
├── screenshots/<test>.png
└── traces/<test>.zip     ← drag onto https://trace.playwright.dev
```

A trace is a DOM snapshot, network log, console log and action timeline for every step. A passing run
writes **nothing** — artifacts are cleared at the start of each run, so their presence always means
"this run produced them".

---

## Test coverage

23 tests: 17 browser-driven, 6 architecture.

| Area | Covers |
|---|---|
| Authentication | Valid sign-in, rejected credentials, sign-out, registration then sign-in |
| Catalogue | Search hit, empty state, search narrowing, price rendering, per-product searches |
| Basket | Add, quantity increment, add-twice merging, removal, distinct lines, empty basket |
| Architecture | Utility isolation, sibling isolation, Playwright confinement, page-object visibility, public-API purity |

Scope is deliberately UI end-to-end. API-level and security-challenge testing would each be a
worthwhile extension, and the Adaptation layer is where they would land — an API client added there
requires no change to the driver, the flows or the test cases.

---

## Notes on the system under test

Juice Shop is an intentionally vulnerable application. A few consequences shaped this solution:

- **Bound to `127.0.0.1`**, never `0.0.0.0`. Publishing it to your LAN would be a genuine risk.
- **`safetyMode: auto`** is left alone, so the destructive challenges stay disabled in a container.
- The image is **distroless** — no shell, no curl — so the compose healthcheck invokes the bundled
  Node binary directly. A `CMD-SHELL curl` healthcheck fails 100% of the time.
- The app **drops and re-seeds its database on every boot**, so `docker compose restart` is a full
  state reset and no volume is needed.
- Tests that need an account **register their own**, so the suite can run repeatedly against the same
  container. The seeded users ship with items already in their baskets.

---

## Licence

Test automation code: MIT. OWASP Juice Shop is MIT-licensed and belongs to the OWASP Foundation.
