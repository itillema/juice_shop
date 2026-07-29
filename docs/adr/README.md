# Architecture Decision Records

Short records of the decisions that shaped this solution, including the options rejected and why.

| # | Decision |
|---|---|
| [0001](0001-gtaa-layering-as-four-dotnet-projects.md) | Express the gTAA layering as four .NET projects |
| [0002](0002-hand-rolled-test-base-over-pagetest.md) | Own the browser lifecycle instead of inheriting `PageTest` |
| [0003](0003-fluent-business-actions-over-gherkin.md) | Express test cases as fluent C# business actions, not Gherkin |
| [0004](0004-host-run-tests-against-containerised-sut.md) | Run tests on the host against a containerised, digest-pinned SUT |
| [0005](0005-architecture-tests-enforce-the-layering.md) | Enforce the layering with architecture tests |
