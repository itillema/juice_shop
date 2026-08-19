using NUnit.Framework;

// Fixtures in parallel, tests within a fixture sequential. Never Children or All — fixture
// instances hold per-test state (session, flow facade) that parallel tests would race.
[assembly: Parallelizable(ParallelScope.Fixtures)]

// Overridable: dotnet test -- NUnit.NumberOfTestWorkers=8. ~150-300MB per Chromium instance.
[assembly: LevelOfParallelism(4)]
