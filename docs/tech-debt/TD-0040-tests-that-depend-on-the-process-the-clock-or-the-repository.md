# TD-0040 - Tests that depend on the process, the clock or the repository

## Problem

Three tests of `tests/SqlSource.Tool.Tests` depend on something outside their own arrangement, and can fail or pass for a reason other than the code they test.

1. **`ProcessRunnerTests` changes the environment of the test process and does not restore it.**  [`ProcessRunnerTests`](../../tests/SqlSource.Tool.Tests/Processes/ProcessRunnerTests.cs) calls `Environment.SetEnvironmentVariable` for `SQLSOURCE_TEST_REMOVED` and `SQLSOURCE_TEST_KEPT`, and xunit runs test classes in parallel in one process, so every other class that reads the environment while it runs sees them.  The class also needs `dotnet`, `sort`, `sleep` or `ping`, and `env` or `cmd` on the path, with a skip guard for `sh` alone, and one test asserts that a cancelled process ended in under fifteen seconds of wall-clock time.
2. **Concurrency tests that order their answers with `Task.Delay`.**  In [`ProjectEvaluatorTests`](../../tests/SqlSource.Tool.Tests/Projects/ProjectEvaluatorTests.cs), the test that projects are given in their order whatever ends first delays each answer by `60 - 10 * i` milliseconds, the test of how many run at once holds each for 30 milliseconds so that the count reaches the cap, and the test that two runs do not share a manifest file delays by 20; [`RunProjectsTests`](../../tests/SqlSource.Tool.Tests/Projects/RunProjectsTests.cs) delays one answer by 80 to force an order.  Under load the delays can reorder, and the test that counts what ran at once can see fewer.
3. **A fixture test runs the real MSBuild over the repository's own test project.**  `Evaluate_TestProjectOfTheGenerator...` in [`FixtureProjectTests`](../../tests/SqlSource.Tool.Tests/Projects/FixtureProjectTests.cs) walks up from the output folder to `SqlSource.slnx` and evaluates `tests/SqlSource.Tests/SqlSource.Tests.csproj` where it is.  It depends on the layout of the repository, on the live contents of `tests/SqlSource.Tests/EndToEnd/`, and on the SDK, and it throws rather than skips when run from a copy of the test output alone.

## Why it exists

1 was the shortest way to prove that `ProcessRunner` removes one variable and keeps another, and `ProcessRequest.SetVariables` was added later.  2 is the shape a `FakeProcessRunner` with a single `BeforeAnswer` hook allows.  3 is the one test that shows the tool reading a project with the package's real metadata trims, and the repository's test project was the one such project to hand.

## Impact

Low today: the suite has not flaked on the build server.  1 can make an unrelated test fail in a way that depends on scheduling, and would be very hard to trace.  2 is the kind of test that fails once a month in CI and passes on retry.  3 ties the tool's tests to a project they do not own.

## Proposed fix

1. Set the two variables through `ProcessRequest.SetVariables` alone, which the request already supports, or wrap the test in `try`/`finally`; guard each external program with `Assert.SkipUnless`; replace the wall-clock bound with a check that the process is gone.
2. Give `FakeProcessRunner` a gate built on `TaskCompletionSource`, so a test releases answers in a chosen order or holds a chosen number open, with no sleep.
3. Copy the facts the test needs into a project under `tests/SqlSource.Tool.Tests/Fixtures/Projects/`, or skip when `SqlSource.slnx` is not found.

## Trigger

The first flaky run of any of the three in CI; or when `FakeProcessRunner` next changes.
