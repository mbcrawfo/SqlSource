# TD-0080 items 6 and 14: typed SQL Server scripts and the log's event names, implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A test of the SQL Server session writes each call it scripts through a builder of that call, as `PostgresScript`'s tests do, and a test of the log reads an event's name through `LogReader`.

**Architecture:** `SqlServerScript` gains one builder per call of `SqlServerCallNames` but `open`, each taking that call's request and answer types, and `NoParametersThenColumns(sql, answer)` for the pair "an empty parameters answer, then a columns answer"; `Add` becomes private.  The session tests name the calls they assert through the alias `Calls = SqlServerCallNames` (a `using static` would clash with `DescribeRequests.Declared`), and `PostgresScript` names its calls from `PostgresCallNames`.  `LogReader` gains `EventOf(JsonElement)` and `Events(path, name)`, which replace `GetProperty("event").GetString()` and the local `NameOf` of `tests/DatabaseTests/Postgres/Describer/DescribeCommandTests.cs`.

**Tech Stack:** .NET 10 SDK, xunit v3, Shouldly.

**Spec:** `docs/tech-debt/TD-0080-duplicated-arrangements-and-uneven-conventions-in-tests.md`, items 6 and 14 of "Problem" and of "Proposed fix".

## Global Constraints

- No test checks anything different afterwards.
- A log event's name that the session tests assert, such as `"column"` or `"table-match"`, is an event and not a call, and stays a literal: only the names of calls come from `*CallNames`.
- Every commit passes `./pre-commit-validation.sh`; the push passes `--full`.  Only items 6 and 14 of TD-0080 are struck.

## Review Focus

- The builder of `operands` takes `SqlServerColumnsRequest` and `SqlServerColumnsAnswer`, as the call does.
- `Events` returns the lines in the order of the log, so a test that reads `events[0]` reads the same line.
- `NameOf` gave `""` for an event without a name and `EventOf` gives `null`; every caller compares with a name, so the result is the same.

---

### Task 1: Items 6 and 14

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/SqlServerScript.cs`, `PostgresScript.cs`, `Describing/SqlServer/SqlServerSession*Tests.cs`, `Describing/SqlServer/SqlServerEventsSessionTests.cs`, `tests/SqlSource.Tool.Testing/LogReader.cs`, `tests/SqlSource.Tool.Tests/Commands/CliTests.cs`, `Logging/RunLog*Tests.cs`, `tests/DatabaseTests/Postgres/Describer/DescribeCommandTests.cs`, TD-0080.

- [ ] **Step 1:** Add the builders and `NoParametersThenColumns`, make `Add` private, and convert every `.Add("<call>", ...)`.
- [ ] **Step 2:** Add `EventOf` and `Events`, and convert every read of `"event"`.
- [ ] **Step 3:** Run `dotnet test --project tests/SqlSource.Tool.Tests`; expected: all pass with the same count.  `DescribeCommandTests` runs in `./pre-commit-validation.sh`.
- [ ] **Step 4:** Strike items 6 and 14; `./format.sh`, `./pre-commit-validation.sh`, commit.
