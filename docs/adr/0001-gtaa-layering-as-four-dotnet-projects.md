# ADR-0001: Express the gTAA layering as four .NET projects

**Status:** Accepted

## Context

The solution is to be organised around the ISTQB generic Test Automation Architecture. The layering
could be expressed as folders within a single test project, or as separate assemblies.

Research into the current syllabus surfaced two things that affect how this is presented:

- CTAL-TAE **v2.0** (GA 2024-05-03) presents generation, definition, execution and adaptation as
  **capabilities** (§3.1.1), not layers. Only *test adaptation layer* survives as a keyword. The 2016
  edition, which does say "layers", was retired for English exams in June 2025.
- **"Utility" is not a gTAA layer** in either edition. It is a practitioner convention.

v2.0 also warns directly against over-layering (§3.1.3): *"By introducing a layer for each single
purpose, the design can become complicated. Therefore, it is recommended to keep the number of TAF
layers low."*

## Decision

Four projects: `Utility`, `Adaptation`, `Execution`, `Definition`, with references forming a strict
chain `Definition → Execution → Adaptation → Utility`.

Document the terminology precisely rather than quietly using the 2016 vocabulary: map the projects
onto both the 2016 four-capability model and the v2.0 three-layer TAF model, and state explicitly that
Utility is the realisation of the configuration-management concern (2016 §3.1.6 / v2.0 §5.1.2) plus
the data-derivation slice of Test Generation (§3.1.2) — not a fifth layer.

## Alternatives considered

**Folders in a single project.** Simplest, and no build overhead. Rejected because it makes the
architecture a naming convention: nothing prevents a test from constructing a page object or importing
Playwright, and there is no mechanism that could. The entire argument would rest on reviewer
discipline.

**Seven projects, one per capability plus generation and reporting.** Rejected on the syllabus's own
advice to keep the layer count low. Test Generation in particular would be a project holding two
classes.

## Consequences

- MSBuild rejects circular references outright (MSB4006 / NU1108), so the direction is enforced by the
  toolchain rather than by review.
- Page objects can be `internal`, which makes it impossible for a test to reach a locator.
- Four projects to build instead of one — negligible at this size.
- The compiler cannot check everything (a test can still call transitively-available public types), so
  ADR-0005 adds architecture tests for the remainder.
- Presenting the terminology accurately is a strength rather than a liability: being the candidate who
  knows v2.0 recast the layers beats being corrected on it.
