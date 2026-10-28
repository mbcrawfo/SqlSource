# AGENTS.md - src/SqlSource.Tool/Logging

How the trace, the events and the log work: [logging and reporting](../../../docs/architecture/tool.md#logging-and-reporting).

- **`RunTrace.Say` is the one place that writes a line of `--verbose`**, each to standard output and on one line.  `RunEvents` says each line beside the event it belongs to, when it has one, so the log and `--verbose` never tell two stories.
- **A describer's events have its engine before their names, which `SessionRunLog.cs` puts there.**  A describer writes the bare name.
- **Nothing hands a log a value it may not hold.**  `arguments` of `run.start` is built by `LoggedArguments.cs` from what `UsageCheck` read, never from the text of the command line, and the log a describer writes to is a `SessionRunLog`.
- **The tests of the log are `tests/SqlSource.Tool.Tests/Logging/RunLogTests.cs`, `RunLogDestinationTests.cs` and `RunLogRedactionTests.cs`**, and those of the lines of `--verbose` are `tests/SqlSource.Tool.Tests/Commands/VerboseTests.cs`.

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
