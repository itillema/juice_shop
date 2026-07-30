# ADR-0001: Express the layering as four .NET projects

**Status:** Accepted

## Context

The solution is organised as a four-layer gTAA test automation architecture: Execution, Definition,
Adaptation and Utility. The layering could be expressed as folders within a single test project, or
as separate assemblies.

The responsibilities are:

| Layer | Responsibility |
|---|---|
| **Execution** | The test cases |
| **Definition** | Page objects, flows, test data |
| **Adaptation** | External services, protocols, integrations, data stores |
| **Utility** | Configuration, logging, DI wiring, reporting, run lifecycle, driver, base classes |

## Decision

Four projects, with a single-direction dependency graph:

```
Execution ──► Definition ──► Utility
    └───────► Adaptation ──► Utility
```

Utility carries no project references. Definition and Adaptation are siblings and do not reference
each other. Execution references all three, because it is the composition root.

## The problem this had to solve

The responsibilities as stated contain a cycle. The run lifecycle is Utility's, and it must wait for
the SUT before any test starts — but reaching an external service over a protocol is Adaptation's,
and Adaptation needs Utility's configuration and logging.

Resolved with a port and an adapter: `ISutReadinessGate` is declared in Utility, implemented as
`HttpSutReadinessGate` in Adaptation, and bound in `GlobalSetup` in Execution. The framework states
what it needs, the integration decides how, and the composition root connects them. Utility therefore
keeps ownership of the run lifecycle without referencing the layer that talks to the SUT.

This is the seam any future integration uses: an API client for test-data setup or a database fixture
is declared as a port where it is needed, implemented in Adaptation, and bound at the top.

## Alternatives considered

**Folders in a single project.** Simplest, no build overhead. Rejected because it makes the
architecture a naming convention: nothing prevents a test from constructing a page object or
importing Playwright, and there is no mechanism that could. The whole argument would rest on reviewer
discipline.

**Utility depending on Adaptation directly**, with the readiness call inline in the run lifecycle.
Simpler by one interface, and rejected because it inverts the point of the framework layer: Utility
would then be unusable against a SUT reached by any means other than the one hard-coded into it, and
it would drag an external protocol into the layer that is supposed to be product-agnostic.

**Adaptation with no project references at all**, taking primitives instead of Utility's configuration
and logging. Would make Adaptation independently reusable, at the cost of duplicating an options type
per integration. Rejected as not paying for itself at this size, but it is the natural next step if
the integrations ever need to ship separately.

## Consequences

- MSBuild rejects circular references outright (MSB4006 / NU1108), so the direction is enforced by
  the toolchain rather than by review.
- Page objects can be `internal` to Definition, making it impossible for a test to reach a locator.
- The composition root is the only place that knows about every layer — which is what a composition
  root is for, and it keeps every other project's reference list short and honest.
- Four projects to build instead of one; negligible at this size.
- The compiler cannot check everything, so ADR-0005 adds architecture tests for the remainder.
