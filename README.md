# OWASP Juice Shop — UI Test Automation

[![e2e](https://github.com/YOUR-USERNAME/juice-shop-automation/actions/workflows/e2e.yml/badge.svg)](https://github.com/YOUR-USERNAME/juice-shop-automation/actions/workflows/e2e.yml)

A UI end-to-end automation solution built on **NUnit + Playwright for .NET 10**, structured around the
**ISTQB generic Test Automation Architecture (gTAA)**, testing a containerised
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
| **Layered architecture** | Four .NET projects mapped onto the gTAA, with dependency direction enforced by the compiler |
| **Architecture as tests** | 5 NetArchTest rules that fail the build if a layer boundary is crossed |
| **Deterministic SUT** | Image pinned by digest, config overlay mounted, container healthcheck for a distroless image |
| **Diagnosable failures** | Playwright trace + screenshot on failure only, attached to the test result |
| **Parallel-safe** | Fixture-level parallelism, isolated browser context per test, self-provisioning test data |
| **CI/CD** | GitHub Actions with test reporting, artifact upload, and an Allure report published to Pages |

---

## Architecture

```
┌───────────────────────────────────────────────────────────────────────┐
│  Definition      Flows/   business actions — the vocabulary of a test │
│                  Tests/   the test cases themselves                   │
└──────────────────────────────┬────────────────────────────────────────┘
                               │ depends on
┌──────────────────────────────▼────────────────────────────────────────┐
│  Execution       run lifecycle, SUT readiness, artifact capture,      │
│                  DI composition root, reporting integration           │
└──────────────────────────────┬────────────────────────────────────────┘
                               │ depends on
┌──────────────────────────────▼────────────────────────────────────────┐
│  Adaptation      Playwright driver + page objects                     │
│                  ← the ONLY project referencing Microsoft.Playwright  │
└──────────────────────────────┬────────────────────────────────────────┘
                               │ depends on
┌──────────────────────────────▼────────────────────────────────────────┐
│  Utility         configuration, logging, DI wiring, test data         │
└───────────────────────────────────────────────────────────────────────┘
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

No locators, no `IPage`, no waits. Those cannot appear here even by accident — the page objects are
`internal`, and an architecture test fails the build if a fixture references the Adaptation assembly.

Full rationale, including the gTAA mapping and where this deviates from the syllabus, is in
[docs/architecture.md](docs/architecture.md). Design decisions are recorded as
[ADRs](docs/adr/).

---

## A note on gTAA terminology

This solution is organised as Adaptation / Definition / Execution / Utility, and it is worth being
precise about how that maps to the standard, because it is not one-to-one:

- **ISTQB CTAL-TAE v2.0** (the current syllabus, GA May 2024) presents test generation, definition,
  execution and adaptation as **capabilities**, not layers — §3.1.1 is headed *"Capabilities provided
  by test automation tools and libraries"*. Only *test adaptation layer* survives as a keyword. The
  2016 edition, which does call them layers, was retired for English exams in June 2025.
- **"Utility" is not a gTAA layer** in either edition. It is a practitioner convention. Here it is the
  realisation of the **configuration-management concern** (2016 §3.1.6 / v2.0 §5.1.2), plus the
  data-derivation slice of the **Test Generation** capability (2016 §3.1.2).
- v2.0's examinable *layering* model is a different, three-layer one (§3.1.3): **test scripts /
  business logic / core libraries**, with the rule *"no direct calls should be made to the core
  libraries from test scripts."*

The four projects satisfy both models simultaneously; see
[docs/architecture.md](docs/architecture.md) for the mapping table. That last rule is not a
convention here — `ArchitectureTests` turns it into a failing build.

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

`appsettings.json` in the Definition project, overridable per environment
(`AUTOMATION_ENVIRONMENT=Ci`) and by environment variable. The separator is a **double** underscore:

```bash
AUTOMATION__SUT__BASEURL=http://juice-shop:3000
AUTOMATION__BROWSER__NAME=firefox
AUTOMATION__BROWSER__HEADLESS=false
AUTOMATION__BROWSER__SLOWMOMILLISECONDS=500     # useful when demoing a run
```

### When a test fails

Failures write a screenshot and a full Playwright trace, attached to the test result and uploaded by
CI:

```
src/JuiceShop.Automation.Definition/bin/Release/net10.0/artifacts/
├── screenshots/<test>.png
└── traces/<test>.zip     ← drag onto https://trace.playwright.dev
```

A trace is a DOM snapshot, network log, console log and action timeline for every step. A passing run
writes **nothing** — artifacts are cleared at the start of each run, so their presence always means
"this run produced them".

---

## Test coverage

22 tests: 17 browser-driven, 5 architecture.

| Area | Covers |
|---|---|
| Authentication | Valid sign-in, rejected credentials, sign-out, registration then sign-in |
| Catalogue | Search hit, empty state, search narrowing, price rendering, per-product searches |
| Basket | Add, quantity increment, add-twice merging, removal, distinct lines, empty basket |
| Architecture | Playwright confinement, layer direction, page-object visibility, public-API purity, test-script isolation |

Scope is deliberately UI end-to-end. API-level and security-challenge testing would each be a
worthwhile extension; the Adaptation layer is where an API adapter would slot in without touching a
single test case.

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
