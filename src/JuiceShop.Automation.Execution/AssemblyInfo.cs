using NUnit.Framework;

// Fixtures run in parallel with each other; tests within a fixture run sequentially.
//
// This is the correct level for browser automation. Each test gets its own browser context, but
// fixture instances hold per-test state (the session, the flow facade), so parallelising *within*
// a fixture would race on those fields. ParallelScope.Children and ParallelScope.All are the
// documented failure mode here and produce cross-test interference that reads as flakiness.
[assembly: Parallelizable(ParallelScope.Fixtures)]

// Worker count is overridable from .runsettings or the command line:
//   dotnet test -- NUnit.NumberOfTestWorkers=8
// Browser memory scales with workers at roughly 150-300MB per Chromium instance.
[assembly: LevelOfParallelism(4)]
