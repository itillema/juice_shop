# ADR-0003: Express test cases as fluent C# business actions, not Gherkin

## Decision

A fluent C# facade (`ShopFlow`) plus assertion facades, with `[Test]` methods calling it directly.

## Rationale

Gherkin's value is that non-technical stakeholders read and contribute to the features. Where that
happens it is worth its cost. Where it does not, it is a translation layer between two audiences that
are the same audience, and it adds a string-matched indirection between the test and the code, which
is worse to refactor and worse to navigate.

```csharp
await Shop.RegisterAndLoginAsync(NewAccount());
await Shop.SearchAsync("Apple Juice");
await Shop.AddToBasketAsync("Apple Juice (1000ml)");
await Shop.Basket.ShouldContainAsync("Apple Juice (1000ml)");
```

The separation Gherkin would provide is provided instead by the facade boundary, which is enforced by
the compiler rather than by convention.

## Consequences

- Fewer moving parts; no `[Binding]` classes, no step-definition ambiguity, full refactoring support.
- Reqnroll would need a DI plugin and a `[ScenarioDependencies]` factory to coexist with
  `Microsoft.Extensions.DependencyInjection`, since it uses its own BoDi container. Avoided.
- If a stakeholder audience appears later, Gherkin can be layered on top of the same flows without
  touching the page objects — the flows are already the step-definition bodies.
- The assertion facades (`Flows/Assertions.cs`) are thin forwarding types. That is a real cost, paid
  deliberately: the page contracts are internal to the Definition assembly, so a public property
  returning one would not compile. See ADR-0005.
