# OWASP Juice Shop — UI Test Automation

A UI end-to-end automation solution built on NUnit and Playwright for .NET 10, structured as a four-layer gTAA test automation architecture, testing a containerised instance of [OWASP Juice Shop](https://owasp.org/www-project-juice-shop/).

The system under test is pinned by image digest and every NuGet version is pinned centrally, so a
clone of this repository runs against the same application, with the same dependencies, today and in
a year.

```bash
git clone https://github.com/itillema/juice_shop.git
cd juice_shop
docker compose up -d --wait     # blocks on the healthcheck: ~10-15s warm, 20-60s cold
dotnet test
```

That's the whole setup. No PowerShell 7, Node.js, nor global tools. The configured browser installs
itself on first run through Playwright's .NET API, which is what keeps `playwright.ps1` — and the
separate PowerShell 7 download it needs — out of the picture.

**Prerequisites:** Docker with Compose v2, and a .NET 10 SDK. `global.json` asks for 10.0.100 and
rolls forward within 10.0.x. A first run also pulls the image — 116 MB over the wire, 338 MB on
disk — and Juice Shop re-seeds its SQLite database on every boot, which is what the wait is for.

---

## Scope of the Project

| Concern | Approach |
|---|---|
| **Layered architecture** | Four .NET projects. MSBuild rejects a reference cycle, `internal` seals the page objects off, and the architecture tests assert the direction |
| **Architecture as tests** | 6 rules — 5 written with NetArchTest, 1 a reflection sweep — that fail the test run if a layer boundary is crossed |
| **Deterministic SUT** | Image pinned by digest, config overlay mounted read-only, container healthcheck for a distroless image |
| **Diagnosable failures** | Playwright trace + screenshot on failure only, attached to the test result |
| **Parallel-safe** | Fixture-level parallelism at 4 workers, isolated browser context per test, self-provisioning test data |
| **CI/CD** | Two GitHub Actions jobs: the suite with test reporting, failure-artifact upload and an Allure report; plus formatting, analysers and the architecture rules with no container running |

---

## Architecture

Four layers, each a separate .NET project.

| Layer | Owns | In this repository |
|---|---|---|
| **Execution** | The test cases | Fixtures, categories, `JuiceShopTest`, the architecture rules, and `GlobalSetup` — the composition root. `appsettings.json` ships here too |
| **Definition** | What the SUT is, and how an action is performed against it | `Pages/` page objects and contracts, `Flows/` business actions and assertion facades, `TestData/` credentials and generators |
| **Adaptation** | Non-browser connections to anything outside the test system | One HTTP readiness probe today. This is the seam where an API client, a data store or a queue probe would land |
| **Utility** | The framework itself | Configuration, logging, DI wiring, artifact capture and reporting paths, run lifecycle, the Playwright driver, `E2ETestBase`, and the ports it declares |

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  Execution     Tests/          the test cases — business intent only        │
│                Architecture/   layering rules, asserted at test time        │
│                GlobalSetup     composition root: adds Adaptation and        │
│                                Definition to the container Utility builds   │
└───────────────┬──────────────────────────────────┬──────────────────────────┘
                │                                  │
┌───────────────▼──────────────────┐  ┌────────────▼──────────────────────────┐
│  Definition                      │  │  Adaptation                           │
│    Pages/     locators, atomic   │  │    Sut/  HTTP readiness probe —       │
│               interactions       │  │          its only occupant today      │
│    Flows/     business actions   │  │                                       │
│               and assertions     │  │  ← where an API client, a database    │
│    TestData/  users, generators  │  │    fixture or a queue probe goes      │
└───────────────┬──────────────────┘  └────────────┬──────────────────────────┘
                │                                  │
┌───────────────▼──────────────────────────────────▼──────────────────────────┐
│  Utility     Driver/         Playwright session, browser lifecycle          │
│              Runtime/        run lifecycle, DI composition machinery        │
│              Artifacts/      trace + screenshot capture, reporting paths    │
│              Configuration/  strongly typed settings, bound at run start    │
│              Logging/        Serilog behind ILogger<T>, NUnit sink          │
│              Testing/        E2ETestBase                                    │
│              Sut/            ISutReadinessGate — port, implemented above    │
│                                                                             │
│              ← no project references: depends on no other layer             │
└─────────────────────────────────────────────────────────────────────────────┘
```

Execution references all three projects below it; Definition and Adaptation reference only Utility,
and never each other; Utility references nothing.

A test reads as a sequence of business intentions:

```csharp
private const string Product = "Apple Juice (1000ml)";

[Test]
[Category(TestCategories.Smoke)]
public async Task Customer_Can_Add_Product_To_Basket()
{
    await Shop.RegisterAndLoginAsync(NewAccount());

    await Shop.SearchAsync("Apple Juice");
    await Shop.AddToBasketAsync(Product);

    await Shop.OpenBasketAsync();
    await Shop.Basket.ShouldContainAsync(Product);
    await Shop.Basket.ShouldShowQuantityAsync(Product, 1);
}
```

No locators, no `IPage`, no waits. Those cannot appear here even by accident: page objects and their
contracts are `internal` to the Definition assembly, so the compiler stops a test from naming one,
and an architecture test fails the run if anything else in the Execution assembly reaches for
Playwright directly.

### The one non-obvious edge

The run lifecycle lives in Utility and has to wait for the SUT before the first browser-driven test
starts, but talking to the SUT is Adaptation's job. Taken literally, that is a cycle. It is resolved
with a port and an adapter: `ISutReadinessGate` is declared in Utility, implemented over HTTP in
Adaptation, and bound at the composition root in Execution. Utility therefore references no other
project, and the framework never learns which protocol the SUT speaks.

The wait is lazy — it sits behind a `Lazy<Task<…>>` that only `E2ETestBase` forces — which is what
lets the architecture rules run with no container at all.

---

## Running

```bash
# Everything — 23 tests
dotnet test

# Just the smoke suite — 6 tests, about 12s
dotnet test --filter "TestCategory=Smoke"

# Layering rules only — no browser, no SUT, runs in milliseconds
dotnet test --filter "TestCategory=Architecture"
```

Watching it happen in a real browser is an environment variable, so it is shell-specific:

```bash
AUTOMATION__BROWSER__HEADLESS=false dotnet test --filter "TestCategory=Smoke"
```

```powershell
$env:AUTOMATION__BROWSER__HEADLESS = 'false'; dotnet test --filter "TestCategory=Smoke"
# or simply: ./run.ps1 -Headed -Filter "TestCategory=Smoke"
```

`./run.ps1` (Windows PowerShell 5.1 compatible) and `./run.sh` run the whole sequence: start the
container and wait for it to report healthy, abort if a file under `src/` is hidden by `.gitignore`,
build Release, then run the suite and write a `.trx`. Both take a filter (`-Filter` / `--filter`) and
a headed switch (`-Headed` / `--headed`), and both need `git` on the PATH.

**Categories**: `Smoke` (6 tests), `Regression` (2), `Authentication` (4), `Catalog` (7), `Basket`
(6), `Architecture` (6). A seventh, `Flaky`, is the quarantine category — instability is excluded by
filter rather than hidden behind `[Retry]` — and nothing carries it today. Note the spelling
`Catalog`: a filter that matches nothing still exits 0.

### Configuration

`appsettings.json` in the Execution project — it sits next to the test binary because that is where
configuration is read from. Precedence is `appsettings.json`, then
`appsettings.<AUTOMATION_ENVIRONMENT>.json` if it exists (`Local` when the variable is unset, which
CI sets to `Ci`), then environment variables. `AUTOMATION_ENVIRONMENT` takes a single underscore and
only selects the overlay file; the settings keys themselves nest with a **double** underscore:

```bash
AUTOMATION__SUT__BASEURL=http://127.0.0.1:3000        # the default, matching the published port
AUTOMATION__SUT__READINESSTIMEOUTSECONDS=240          # the only thing appsettings.Ci.json raises
AUTOMATION__BROWSER__NAME=firefox                     # or webkit — installs on first use
AUTOMATION__BROWSER__HEADLESS=false
AUTOMATION__BROWSER__SLOWMOMILLISECONDS=500           # useful when demoing a run
AUTOMATION__BROWSER__SKIPBROWSERINSTALL=true          # browsers already present, or no network
AUTOMATION__ARTIFACTS__CAPTURETRACE=false             # also CAPTURESCREENSHOT, CAPTUREVIDEO
```

Settings bind to a validated object, so an override that breaks a stated rule fails before the first
test rather than halfway through a run, and the message names the key to go and change:

```
Automation:Sut:BaseUrl: BaseUrl must be an http or https URL with a host, e.g. http://127.0.0.1:3000.;
Automation:Browser:Name: Browser must be chromium, firefox or webkit.
```

Validation is recursive and covers every section, which the stock `ValidateDataAnnotations` does not
— it checks the root object only. It checks the shape of a value, not its truth: a well-formed
`Automation:Sut:HealthPath` pointing at a route that answers 200 for everything, or a malformed entry
in `Automation:Browser:SessionCookies`, still surfaces inside a test. See `AutomationSettingsValidator`.

Note `Automation:Browser:SessionCookies`. Three cookies are seeded into every session before its
first navigation: two dismiss the welcome dialog and cookie banner that otherwise swallow the first
click, and the third pins the locale, without which the app may render in another language and every
text-based selector fails in a way that looks machine-specific. They live in configuration rather
than in the driver, so the driver carries no application-specific cookie logic — the one thing it
does hard-code is the `en-US` context locale.

### When a test fails

A failing browser test writes a screenshot and a full Playwright trace, attaches both to the test
result, and CI uploads them:

```
src/JuiceShop.Automation.Execution/bin/<Configuration>/net10.0/
├── artifacts/
│   ├── screenshots/<test>.png     ← sanitised NUnit FullName, last 120 characters
│   ├── traces/<test>.zip          ← drag onto https://trace.playwright.dev
│   └── logs/run-<date>.log        ← written every run, kept across runs
└── allure-results/
```

`dotnet test` builds Debug, so that is where the quickstart puts them; `run.ps1`, `run.sh` and CI all
build Release. The `.trx` goes to `artifacts/test-results/` at the repo root, per `.runsettings`.

A trace is a DOM snapshot, network log, console log and action timeline for every step. Tracing runs
for every test and is discarded on a pass, so a passing run produces no screenshot and no trace — it
still writes the run log and its Allure results. Screenshots, traces, videos and `allure-results` are
deleted at the start of each run, so their presence means this run produced them. Two caveats: the
clearing is per build configuration, and it happens on any run, including a
`TestCategory=Architecture` one that will delete the trace from the browser run before it. A run
that dies before the browser is up — the SUT never became ready, or the browser failed to launch —
produces neither file.

---

## Test coverage

23 tests: 17 browser-driven, 6 architecture. That is 21 methods; `Seeded_Products_Are_Findable`
carries three `[TestCase]` rows.

| Area | Covers |
|---|---|
| Authentication | Valid sign-in, rejected credentials, sign-out, registration then sign-in |
| Catalog | Search hit, empty state, search narrowing, price rendering, three per-product searches |
| Basket | Add, quantity increment, add-twice merging, removal, distinct lines, empty basket |
| Architecture | Utility isolation, sibling isolation, Playwright kept out of the test cases, Playwright kept out of Adaptation, page-object visibility, no Playwright type on Definition's public API |

Scope is deliberately UI end-to-end. API-level and security-challenge testing would each be a
worthwhile extension, and the Adaptation layer is where they would land. A flow cannot consume an
Adaptation client directly — Definition references only Utility — so one would arrive through a port,
exactly as the readiness gate does, and require no change to the driver or the test cases.

---

## Notes on the system under test

Juice Shop is an intentionally vulnerable application. A few consequences shaped this solution:

- **Bound to `127.0.0.1`**, never `0.0.0.0`. Publishing it to your LAN would be a genuine risk.
- **`safetyMode: auto`** is pinned explicitly in the overlay — the image's own default, rather than
  the `disabled` that `NODE_ENV=unsafe` and `ctf` set — so the app auto-detects the container and the
  genuinely destructive challenges stay disabled.
- The image is **distroless** — no shell, no curl — so the compose healthcheck invokes the bundled
  Node binary directly. A `CMD-SHELL curl` healthcheck fails 100% of the time.
- The app **drops and re-seeds its database on every boot**, so `docker compose restart` is a full
  state reset and no data volume is needed.
- Tests that create state **register their own account** — all six basket tests and the registration
  journey — so the suite can run repeatedly against the same container. Several of the seeded users ship
  with items already in their baskets. The sign-in and sign-out tests deliberately reuse the seeded
  `jim@juice-sh.op`, which is safe because they assert only on session state.

---

## Documentation

- [docs/architecture.md](docs/architecture.md) — the full rationale: the layering, the dependency
  cycle and how it is broken, the synchronisation patterns that made the suite trustworthy, and test
  isolation.
- [docs/adr/](docs/adr/) — five ADRs recording each decision and the alternatives rejected.

---

## Licence

OWASP Juice Shop is MIT-licensed, copyright Bjoern Kimminich and the OWASP Juice Shop contributors. No licence is declared for the
test automation code in this repository.
