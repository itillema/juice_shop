# ADR-0005: Enforce the layering with architecture tests

**Status:** Accepted

## Context

Separate projects give real enforcement, but not complete enforcement. MSBuild rejects circular
references and `internal` hides the page objects, yet a test can still call any public type that
reaches it transitively. In particular, `Definition` references `Adaptation` (its flows need the page
contracts), so nothing at the compiler level stops a fixture from using those contracts directly —
which is exactly what ISTQB CTAL-TAE v2.0 §3.1.3 rules out: *"no direct calls should be made to the
core libraries from test scripts."*

A rule that only exists in a README drifts from the code within a few sprints.

## Decision

`ArchitectureTests` in the Definition project, using `NetArchTest.Rules` plus reflection, asserting
five rules:

1. Only Adaptation depends on `Microsoft.Playwright`.
2. Types in the `...Definition.Tests` namespace do not reference the Adaptation assembly.
3. Layer dependencies point one way only.
4. No page object is publicly visible.
5. No public member of Adaptation exposes a Playwright type in a parameter, return or property —
   including inside `Task<T>`.

They carry `[Category("Architecture")]`, need no browser and no SUT, and finish in milliseconds.

## Evidence this was worth doing

The rules failed on their first run, against this codebase, for a real reason.

`ShopFlow` originally exposed the Adaptation page contracts directly:

```csharp
public IBasketPage Basket => _session.Basket;   // reads fine, compiles, wrong
```

Every fixture therefore had a compile-time dependency on the adapters. Rule 2 failed and named all
three offending fixtures. The fix was the assertion facades in `Flows/Assertions.cs`. That is the
difference between an architecture that is enforced and one that is merely described — the shortcut
was taken by the author, in good faith, and caught by the build rather than by a reviewer.

The rules were also verified in the other direction: injecting
`typeof(Microsoft.Playwright.IPage)` into a fixture produced a failure naming that fixture, and the
message points at the fix rather than just reporting a violation.

## Consequences

- The layering is executable documentation; a violation fails CI, not a code review.
- One deliberate exclusion: `ArchitectureTests` is exempt from rule 1 for itself, because it names the
  forbidden namespaces as string constants and NetArchTest's IL scan cannot distinguish a constant
  naming a namespace from a genuine reference into it. Everything else in the assembly stays covered.
- Rules must be maintained alongside intentional architecture changes — which is the point, since it
  forces the change to be intentional.
