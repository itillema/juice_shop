# ADR-0005: Enforce the layering with architecture tests

## Context

Separate projects give real enforcement, but not complete enforcement. MSBuild rejects circular
references, and page objects are internal to the Definition assembly so no test can name one. What
neither catches:

- A layer taking a dependency it should not — the reference graph is only correct because someone
  remembers to keep it that way.
- The Adaptation layer starting to drive a browser instead of speaking a protocol.
- A Playwright type leaking through the Definition layer's public API, which would couple every test
  to the automation technology.

A rule that exists only in a README drifts from the code within a few sprints, if work continues.

## Decision

`ArchitectureTests` in the Execution project, using `NetArchTest.Rules` plus reflection, asserting
six rules:

1. Utility depends on no other layer.
2. Adaptation and Definition do not depend on each other, or on Execution.
3. Test cases do not depend on `Microsoft.Playwright`.
4. Adaptation does not depend on `Microsoft.Playwright`.
5. No public type resides in the `Definition.Pages` namespace.
6. No public member of the Definition assembly exposes a Playwright type — including inside `Task<T>`.

They carry `[Category("Architecture")]`, need no browser and no SUT, and finish in milliseconds.

## Verification

Rules nobody has seen fail are rules nobody should trust. Each testable rule was verified by
introducing the violation it describes and confirming the failure named the offending type:

| Violation introduced | Result |
|---|---|
| `typeof(Microsoft.Playwright.IPage)` in a test fixture | Rule 3 failed, naming `AuthenticationTests` |
| A public type added to the `Definition.Pages` namespace | Rule 5 failed with the "must be internal" message |
| Definition given a project reference to Adaptation and a type from it | Rule 2 failed, naming `ShopFlow` |

One violation could not be tested this way, and that is a stronger result than a passing test:
making a page **contract** public does not merely trip rule 5, it fails to compile, because the
contract exposes internal record types. The type system already forbids it.

## Consequences

- The layering is executable documentation; a violation fails CI, not a code review.
- One deliberate exclusion: `ArchitectureTests` is exempt from rule 3 for itself, because it names
  the forbidden namespace as a string constant and NetArchTest's IL scan cannot distinguish a
  constant naming a namespace from a genuine reference into it. Every other type in the assembly
  stays covered.
- Rules must be maintained alongside intentional architecture changes — which is the point, since it
  forces the change to be intentional.
- "No browser and no SUT" is a property of the **run lifecycle**, not of these tests. The composition
  root is a namespace-less `[SetUpFixture]`, so its `[OneTimeSetUp]` runs before every fixture in the
  assembly; while it awaited the readiness gate eagerly, all six rules failed after a 150s timeout on
  any machine without the container running — a green-on-CI, red-everywhere-else gate. It holds only
  because `AutomationRuntime` defers the gate and the browser launch to first session use.
  Nothing in the code can enforce that, so the `lint` CI job — which starts no container — runs
  `--filter "TestCategory=Architecture"`. If that step ever needs Docker, the coupling is back.
