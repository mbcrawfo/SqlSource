# AGENTS.md - src/SqlSource.Tool

How the tool works: [`docs/architecture/tool.md`](../../docs/architecture/tool.md).
How the PostgreSQL describer works: [`docs/architecture/postgres-describer.md`](../../docs/architecture/postgres-describer.md) and [`docs/architecture/postgres-nullability.md`](../../docs/architecture/postgres-nullability.md).
How the SQL Server describer works: [`docs/architecture/sqlserver-describer.md`](../../docs/architecture/sqlserver-describer.md).

The `sqlsource` command, packed as the .NET tool `SqlSource.Tool`.  Its tests, and where each kind goes, are in [`tests/README.md`](../../tests/README.md).

## The project

- **The tool shares the generator's code and never copies it.**  It references `src/SqlSource/SqlSource.csproj` and sees its internals ([folders](../../docs/architecture/tool.md#folders)).  Code that both need goes in the generator's project, where `netstandard2.0` and Roslyn 4.8.0 bound it.
- **It targets `net8.0` with `RollForward` `Major`, and references a current Roslyn by `VersionOverride`.**  Do not raise the target: a user with only the .NET 8 SDK must be able to run it.  `tools/check-tool-install.sh` runs the packed tool on the newest runtime the machine has.
- **The tool writes English**, and `SatelliteResourceLanguages` keeps other languages out of the package.  A test compares no text that System.CommandLine wrote.
- **`README.md` here is the package's short page on nuget.org**: what the tool is for, how to install it, the commands a user runs most, and links to the manual and the repository's `README.md`, which is not packed into this package.  Keep it that short.
- **The tool's behaviour is documented in `docs/manual/tool.md`**, where a new option, environment variable, exit code or command goes; in `docs/manual/settings.md`, for the logical database setting; and in `docs/manual/postgresql.md` and `docs/manual/sqlserver.md`.

## The host

- **Everything from outside comes through `ToolHost`.**  Nothing reads `Console`, `Environment`, `RuntimeInformation` or the current directory except `ToolHost.Create` and `Cli.Run` ([the host](../../docs/architecture/tool.md#the-host)).  A test gives `Cli.RunAsync` a host of its own.  What a later phase takes from outside is a new member of `ToolHost`.
- **The clock is `ToolHost.Time`.**  The time of a log's events and how long a step took come from it, never from `DateTime` or `Stopwatch`.
- **Every process goes through `ToolHost.Processes`.**  `Processes/ProcessRunner.cs` is the only code that starts one, and a test gives `tests/SqlSource.Tool.Testing/FakeProcessRunner.cs`.  A temporary folder is made in `ToolHost.TempDirectory`, and what runs at once is bounded by `ToolHost.ProcessorCount`.
- **The tool never restores and never builds a project, and writes nothing into one but its sidecars** ([evaluating a project](../../docs/architecture/tool.md#evaluating-a-project)).  Do not add `-restore`, a target of a build, or a file under a project's folder: `tests/SqlSource.Tool.Tests/Projects/FixtureProjectTests.cs` checks that a project has no `obj` afterwards.
- **A sidecar is written or deleted by `Describing/SidecarStore.cs` and by nothing else.**

## Results and errors

- **A result that may fail has one shape where files meet.**  A method that another file calls and that gives one value or one reason returns an `Outcome<TValue, TFailure>` and reports nothing: its caller does, as `DescribeCommand.PlanAsync` reports what `Projects/RunUnitFinder.cs` found.  `Outcome.cs` says what is no `Outcome`, and how one of a value that must be disposed is made.
- **Every error goes through `Reporting/Reporter.cs` with a descriptor of `ToolDiagnostics`**, or of `SqlDiagnostics` where the generator reports the same condition.  The descriptors are in the generator's project; `src/SqlSource/Diagnostics/AGENTS.md` lists the four places a new one touches.  A wrong command line is the one exception, below.
- **The exit code is `0`, `1` or `2`**, as the manual's [exit codes](../../docs/manual/tool.md#exit-codes) say.  `Cli.ExitCodeOf` takes it from the reporter, so a command reports, goes on, and never returns `1` itself.  `2` is for `SQLSRC219` alone, and a query that could not be described is "could not check" and never `SQLSRC219`.
- **Every text the tool prints and did not write itself goes through `Reporting/OneLine.cs`**, which writes a line break as a space: an argument of a message, a path, a label, the name of an unknown option.  Only the first column tells a first line from a continuation line, as `Reporter.cs` says.
- **A path in a message is a full path**, or `The path at position <n>` when its token holds `=` or `;` (`UsageValue.NamedAsPath`).
- **No error carries a hint to a log.**  A failed `describe` with no log ends with `Cli.RunAgainLine` instead.
- **An exception is `SQLSRC200`**, caught in `Cli.RunCommandAsync`, in `Cli.Run` when the host cannot be made, and in `Describing/DatabaseRuns.cs`, which rethrows a describer's as a `DescriberFaultException` without the connection's value ([describing](../../docs/architecture/tool.md#describing)).
- **No `catch (InvalidOperationException)` may stand between `Describing/DatabaseRuns.cs` and `Cli.RunCommandAsync`**: `DescriberFaultException` derives from it, so the catch would swallow the fault and the run would go on.

## A connection's value

- **A connection's value goes to the describer and nowhere else** ([where a connection's value goes](../../docs/architecture/tool.md#where-a-connections-value-goes)).  The parts that `IQueryDescriber.SecretsOf` returns and the text of `RewriteOf` are all a describer gives back that holds it, which the run uses only to remove them and to refuse a capture.
- **A type that holds the value overrides `ToString` to leave it out**, as `ConnectionArgument`, `ConnectionSecret`, `DatabaseConnection`, `OpenRequest` and `UsageValue` do: a record prints its members.  What the tool may say of a connection is where it came from, `--connection` or the name of a variable.
- **Every text of a describer that is printed, said under `--verbose` or logged has the secret removed**, whatever the describer's rule: by `Describing/Redaction.cs`, by a describer's log, `Logging/SessionRunLog.cs`, and by `RunTrace.Without` once a database's connection is found.  What the removal misses is `docs/tech-debt/TD-0034-redaction-of-a-describers-text-has-holes.md`.
- **A new event that carries what a describer gave gets a case in `tests/SqlSource.Tool.Tests/Logging/RunLogRedactionTests.cs`**, and a new place that prints it one in `tests/SqlSource.Tool.Tests/Commands/SecretTests.cs`.

## What a run says

- **A step that reports nothing writes no event either**: its caller writes it from what the step returned, as for the planner, `RunDecisions`, `FileOutcomes` and `SidecarCheck`.  `Logging/RunEvents.cs` builds every event but `run.start`, `run.end` and `diagnostic`, and says each line of `--verbose` through `RunTrace.Say`, the one writer of those lines.
- **An event's name and its members are read by people and by phase 10's bundle.**  A member is added and never renamed, and a value is one of `Logging/LogValue.cs`'s closed set ([the log](../../docs/architecture/tool.md#the-log)).

## The command line

- **A wrong command line is reported by `UsageCheck.cs`, before System.CommandLine reads it, and never repeats a token's text**, since a token can be a secret ([reading a command line](../../docs/architecture/tool.md#reading-a-command-line)).  Its lines have no id and do not go through the reporter.
- **`UsageCheck` and System.CommandLine must read a command line the same way**, or `Cli` runs nothing.  The remarks of `UsageCheck.cs` name the tests that hold them together, which a new option joins.
- **A command's own rules for its values are in the command**, as `DescribeCommand.CheckUsage`, whose lines name a position and never a token.
- **An option is declared by the rules at the top of `DescribeCommand.Create`**, so that `UsageCheck` reads it right.

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
