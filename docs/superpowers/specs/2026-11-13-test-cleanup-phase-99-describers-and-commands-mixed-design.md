# Test cleanup phase 99: tests/DatabaseTests/Postgres/Describer + tests/DatabaseTests/SqlServer/Describer + tests/SqlSource.Tool.Tests/Commands, mixed actions

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folders: `tests/DatabaseTests/Postgres/Describer`, `tests/DatabaseTests/SqlServer/Describer`, `tests/SqlSource.Tool.Tests/Commands`
- Findings: 6 (1 delete, 1 fold, 1 loosen, 3 demote)
- Kind: mixed: its files hold findings of several kinds, or a finding ties them to such a file
- Changed lines: about 113
- Guard: mutation (tool-root, tool-describing-sqlserver), with covers and run time for 5 findings

## Files

- `docs/tech-debt/TD-0089-theories-that-branch-on-their-row.md`
- `tests/DatabaseTests/Postgres/Describer/SessionTests.cs`
- `tests/DatabaseTests/SqlServer/Describer/SessionTests.cs`
- `tests/SqlSource.Tool.Tests/Commands/SecretTests.cs`
- `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerDescriberTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests.Commands-2-07

- test: `tests/SqlSource.Tool.Tests/Commands/SecretTests.cs` | `SecretTests.Run_SecretTypedAfterDatabase_IsNeverPrinted` | rows: all
- action: delete
- evidence: measured (1 row, kills 3, alone 0, timeouts under it 57; cover: `CheckFilterTests.Check_AfterADescribeWithTheSameFilterThatSucceeded_ExitsZero`) and read (SecretTests.cs:341-350 runs `describe --database s3cret-marker` and asserts that neither output holds the marker; `SummaryTests.Run_DatabaseFarFromEveryDatabase_IsNamedByItsPosition` at SummaryTests.cs:130-138 runs the same command line, asserts its whole standard output, which names the token by its position, and that the error does not hold the marker, and its comment states the secret rule.  The summary line is built in src/SqlSource.Tool/Describing/RunSummary.cs:137, outside the tool-root scope, so the numbers say nothing of it; the delete rests on reading.  The comment at SecretTests.cs:347, "SummaryTests holds the exact line.", points at the cover and records no decision; the documents searched name neither test); blind: no
- cover: `tests/SqlSource.Tool.Tests/Commands/SummaryTests.cs` | `SummaryTests.Run_DatabaseFarFromEveryDatabase_IsNamedByItsPosition`
- proposal: Delete the test; the summary test asserts the same command line more strictly.
- verdict: confirmed
- verifier's reason: SecretTests.cs:341-350 runs `describe --database s3cret-marker` and asserts that neither stream holds the marker.  `SummaryTests.Run_DatabaseFarFromEveryDatabase_IsNamedByItsPosition` (SummaryTests.cs:130-138) runs the same command line, asserts the whole standard output and that the error lacks the marker, and its comment states the secret rule; the scenes differ only in their queries, which no query of a database filter that matches nothing reaches.  `kills` in tool-root: the test's 3 kills (Cli.cs:196:20 x2, PlanSteps.cs:258:48) are in the cover's 72, and its 57 timeouts are all under the cover.  The comment at SecretTests.cs:347 points to SummaryTests and records nothing; `SecretTests` in src/SqlSource.Tool/AGENTS.md:42 asks for a case for each new place that prints a describer's text, which this test is not, and no document names it.

### F-SqlSource.Tool.Tests.Commands-2-06

- test: `tests/SqlSource.Tool.Tests/Commands/SecretTests.cs` | `SecretTests.Run_DescriberThatReturnsTheValue_PrintsTheMarkInEachPlace` | rows: `[InlineData("1")]` (line 515)
- action: fold
- evidence: measured (2 rows, kills 45, alone 0, timeouts under it 54; rows needed 1) and read (the row sets `SQLSOURCE_DEBUG`, which only `UnexpectedFailure.Of` reads (src/SqlSource.Tool/Reporting/UnexpectedFailure.cs:35), to add the trace of an exception; this test makes both queries fail through `FakeDescriber.Failure` (SecretTests.cs:525-533) and throws nothing, so the run never reaches that code and the row runs what the `null` row runs.  The other two debug theories of the class, `Run_DescriberThatThrowsWithTheValue_PrintsTheMarkInItsPlace` and `Run_DescriberThatPutsTheValueInItsDescription_EndsTheRunAndWritesNoSidecar`, end in SQLSRC200, where the row does change the output.  No comment or document makes the row a record); blind: no
- cover: `tests/SqlSource.Tool.Tests/Commands/SecretTests.cs` | `SecretTests.Run_DescriberThatReturnsTheValue_PrintsTheMarkInEachPlace` (its `null` row); `tests/SqlSource.Tool.Tests/Commands/SecretTests.cs` | `SecretTests.Run_DescriberThatThrowsWithTheValue_PrintsTheMarkInItsPlace`
- proposal: Drop the `"1"` row and the debug branch, and make the test a fact.
- verdict: confirmed
- verifier's reason: SecretTests.cs:513-546 sets `SQLSOURCE_DEBUG` for the `"1"` row; the variable is read only by `UnexpectedFailure.Of` (src/SqlSource.Tool/Reporting/UnexpectedFailure.cs:35), called from Cli.cs:98 and :238, WatchSession.cs:206 and DatabaseRuns.cs:447, each on an exception.  This test returns failures through `FakeDescriber.Failures` (FakeDescriber.cs:271), throws nothing, and with `Good` failing writes no entry, so the description check that ends in SQLSRC200 never fires; both rows run the same code and assert the same text.  `Run_DescriberThatThrowsWithTheValue_PrintsTheMarkInItsPlace` (:486-511) is where the row changes the output.  docs/tech-debt/TD-0089-theories-that-branch-on-their-row.md names "three tests over `debug`" of SecretTests.cs as theories with an `if` on their row, which is a note on their shape and no record of a limit, regression or decision; the cleanup must update that item in the same change.
- condition: The verifier adds: `docs/tech-debt/TD-0089-theories-that-branch-on-their-row.md:20` names "three tests over `debug`" of `SecretTests.cs`; update that item in the same change, since this test stops being one.

### F-DatabaseTests.SqlServer-1-01

- test: `tests/DatabaseTests/SqlServer/Describer/SessionTests.cs` | `SessionTests.OpenAsync_Container_GivesTheVersionTheDriverAndTheSettings` | rows: all
- action: loosen
- evidence: read (SessionTests.cs:35 holds `server.DriverVersion.ShouldNotBeNull().ShouldStartWith("7.1.1")`, a live read of the informational version of the loaded Microsoft.Data.SqlClient (src/SqlSource.Tool/Describing/SqlServer/SqlServerConnection.cs:57-61), so it is the package pin of Directory.Packages.props:23, whose comment names only examples/SqlServerExamples/README.md as coupled to it; a bump of SqlClient with no change of behaviour fails this test.  The PostgreSQL twin, tests/DatabaseTests/Postgres/Describer/SessionTests.cs:32, compares with the loaded driver's own version instead.  The mapping of an answer's driver version to the session is already held by the scripted tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerSessionTests.cs:28); blind: no
- cover: none
- proposal: Compare `DriverVersion` with the version of the SqlClient assembly the test has loaded (its informational version without the `+commit`), as the PostgreSQL twin does with Npgsql, or with a semantic-version shape, so that moving the SqlClient pin no longer fails it.
- verdict: confirmed
- verifier's reason: tests/DatabaseTests/SqlServer/Describer/SessionTests.cs:35 holds `DriverVersion.ShouldStartWith("7.1.1")`, and the value is read live from the loaded assembly's informational version (src/SqlSource.Tool/Describing/SqlServer/SqlServerConnection.cs:57-61), so moving the pin at Directory.Packages.props:23 fails it with no change of behaviour.  The scripted unit test tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerSessionTests.cs:28 takes its "7.1.1" from the script (tests/SqlSource.Tool.Tests/SqlServerScript.cs:33), not from the assembly, so it keeps the mapping and does not move with the pin.  The PostgreSQL twin compares with the loaded driver (tests/DatabaseTests/Postgres/Describer/SessionTests.cs:32).  No comment above the test or its class, and no AGENTS.md, readme, docs/architecture, tech-debt or deferred item, names this assertion as a deliberate trip-wire on the pin; the pin's comment names only examples/SqlServerExamples/README.md.  The finding reads the test rightly.

### F-DatabaseTests.SqlServer-1-02

- test: `tests/DatabaseTests/SqlServer/Describer/SessionTests.cs` | `SessionTests.OpenAsync_UnreadableConnectionString_IsSqlsrc224WithTheFixedLine` | rows: all
- action: demote
- evidence: read (SessionTests.cs:107-116 opens "Data Source=127.0.0.1,1;Password=<marker>;Nonsense=<marker>", which SqlClient refuses before any connection, so the container is never reached; the unit theory tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerDescriberTests.cs:68-69, row `"Data Source=127.0.0.1,1;Nonsense=" + Secrets.Marker`, already asserts SQLSRC224, the line "SqlClient cannot read the connection string" (= ConnectionErrors.UnreadableConnectionString, src/SqlSource.Tool/Describing/SqlServer/ConnectionErrors.cs:41) and no marker in the arguments or lines.  What the container test adds and the unit does not is that the capture holds no marker, the step is null and the arguments are the logical name.  No AGENTS.md, readme, docs/architecture, tech-debt or deferred item names the test; the readme names SessionTests only as "what each refusal of it reports"); blind: no
- cover: `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerDescriberTests.cs` | `SqlServerDescriberTests.OpenAsync_ConnectionStringRefusedBeforeOpening_IsSqlsrc224WithTheFixedLine`
- proposal: Open through a `CapturingExchange` in that unit theory and assert that the capture holds no marker, the step is null and the arguments are the database's name, then delete the container test, which needs no server.
- verdict: confirmed
- verifier's reason: SessionTests.cs:107-116 opens "Data Source=127.0.0.1,1;Password=<marker>;Nonsense=<marker>", which SqlConnectionStringBuilder refuses on the unknown keyword before any connection (docs/architecture/sqlserver-describer.md:51, `UnreadableConnectionString`), so the container is never reached and no engine behaviour is proved.  The unit theory tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerDescriberTests.cs:65-125, row `"Data Source=127.0.0.1,1;Nonsense=" + Secrets.Marker` at :69, already asserts SQLSRC224, the same fixed line and no marker in the arguments or lines, with the same LiveExchange; what it lacks (Arguments equal to the logical name, Step null, the connection text absent, no marker in the capture, SessionTests.cs:253-263 and :115) is what the finding's proposal adds through a CapturingExchange.  The tool's tests run on every OS, and this path is the builder's alone, so nothing differs by platform.  Search: tests/DatabaseTests/SqlServer/README.md:3 names `SessionTests` only as "what each refusal of it reports"; no AGENTS.md, docs/architecture, tech-debt or deferred item names the test; the comment above it (:105) explains why the fixed line is used and records no limit or regression.

### F-DatabaseTests.SqlServer-1-03

- test: `tests/DatabaseTests/SqlServer/Describer/SessionTests.cs` | `SessionTests.OpenAsync_UnreachableHost_IsSqlsrc224WithNoPartOfTheValue` | rows: all
- action: demote
- evidence: read (SessionTests.cs:74-85 opens port 1 of 127.0.0.1 with Connect Timeout=3 and never touches the container, 2.5 s, a fifth of SessionTests' 13.0 s and 3.6% of the project's 69.6 s, paid again on the floor and each nightly image.  tests/SqlSource.Tool.Tests/Commands/SecretTests.cs:137-162, `Run_SqlServerDescriberThatCannotOpen_PrintsSqlsrc224AndNothingOfTheValue`, already makes the same real SqlClient open of port 1 with no container (Connect Timeout=1) and asserts SQLSRC224 and neither the marker nor the host in the output, the log or the capture; it asserts only "    server: " of the line, where this test holds `(^|: )SqlClient could not reach the server(: ConnectionRefused)?$`.  The comment of tests/SqlSource.Tool.Tests/Describing/SqlServer/ConnectionErrorsTests.cs:8-9 says SqlException's mapping is tested against the container since SqlException has no public constructor, but this case's SqlException comes from no server, as SecretTests shows.  No AGENTS.md, readme, docs/architecture, tech-debt or deferred item names the test); blind: no
- cover: `tests/SqlSource.Tool.Tests/Commands/SecretTests.cs` | `SecretTests.Run_SqlServerDescriberThatCannotOpen_PrintsSqlsrc224AndNothingOfTheValue`
- proposal: Add to that test the assertion that the printed server line matches `SqlClient could not reach the server(: ConnectionRefused)?`, and delete the container test.
- verdict: confirmed
- verifier's reason: SessionTests.cs:74-85 opens port 1 of 127.0.0.1 and never reaches the container; tests/SqlSource.Tool.Tests/Commands/SecretTests.cs:137-162 makes the same real SqlClient open (port 1, Encrypt=False, a password of the marker) with no container and asserts SQLSRC224 and neither the marker nor the host in the output, the log or the capture, so the test needs no engine and the demote stands.  The search gave nothing: no AGENTS.md, readme, docs/architecture, tech-debt or deferred item names the test, and the comment above it (:70-72) explains the regex.  One amendment the cleanup must make, which the proposal misses: SecretTests runs in the nightly's Windows and macOS jobs (.github/workflows/nightly.yml:51-53, :84-86), where the container test never runs, and SqlClient on Windows uses its native SNI (src/SqlSource.Tool/packages.lock.json:32 brings Microsoft.Data.SqlClient.SNI.runtime), whose error may carry no SocketException, in which case ConnectionErrors.OtherAtOpen (src/SqlSource.Tool/Describing/SqlServer/ConnectionErrors.cs:131-141) gives the session-refused text rather than `SqlClient could not reach the server`.  The moved assertion of the line `(^|: )SqlClient could not reach the server(: ConnectionRefused)?$` must therefore be skipped on Windows, or accept Windows's form, so that it holds on every OS SecretTests runs on; that may be why SecretTests.cs:149 asserts only "    server: " today.
- condition: The verifier's amendment: `SecretTests` runs in the nightly's Windows and macOS jobs, where SqlClient's native SNI may raise no `SocketException`, and `ConnectionErrors.OtherAtOpen` then gives the session-refused text.  The moved assertion of `(^|: )SqlClient could not reach the server(: ConnectionRefused)?$` is skipped on Windows, or accepts Windows's form, so that it holds on every OS `SecretTests` runs on.

### F-engines-03

- test: `tests/DatabaseTests/Postgres/Describer/SessionTests.cs` | `SessionTests.OpenAsync_UnreachableHost_IsSqlsrc224WithAFixedTextAndTheSocketsError` | rows: all
- action: demote
- evidence: read (twin fault of F-DatabaseTests.SqlServer-1-03; the Postgres.Describer-2 slice has no finding on it.  SessionTests.cs:76-86 opens `Host=127.0.0.1;Port=1` and never reaches the container, yet sits in `UsesPostgres`, so running it starts PostgreSQL (0.001 s of its own).  tests/SqlSource.Tool.Tests/Commands/SecretTests.cs:107-132, `Run_PostgresDescriberThatCannotOpen_PrintsSqlsrc224AndNothingOfTheValue`, makes the same real Npgsql open of port 1 with no container and asserts SQLSRC224, the line `    server: Npgsql could not reach the server: ConnectionRefused`, and neither the marker nor `127.0.0.1` in the output, the log or the capture; it runs in the nightly on every OS, so the Windows caveat of F-DatabaseTests.SqlServer-1-03's verdict does not arise here.  The mapping is also held by tests/SqlSource.Tool.Tests/Describing/Postgres/ConnectionErrorsTests.cs:70-80.  The container test adds only that the username `sqlsrc_unreached` is named nowhere.  src/SqlSource.Tool/Describing/Postgres/AGENTS.md keeps for `tests/DatabaseTests/Postgres/Describer/` "what only a server can show".  No AGENTS.md, readme, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names the test; site/diagnostics.md:2079 documents the line, which the cover keeps; the comment above it (:76) explains the port.  No mutation scope ran the slice.); blind: no
- cover: `tests/SqlSource.Tool.Tests/Commands/SecretTests.cs` | `SecretTests.Run_PostgresDescriberThatCannotOpen_PrintsSqlsrc224AndNothingOfTheValue`
- proposal: Add a username to that test's connection string and assert it is in neither the output, the log nor the capture, then delete the container test, as F-DatabaseTests.SqlServer-1-03 does for SQL Server's.
- verdict: confirmed
- verifier's reason: SessionTests.cs:76-86 (tests/DatabaseTests/Postgres/Describer/) opens `Host=127.0.0.1;Port=1`, never touches the container (`ShouldNotOpenAsync`, :368-384, uses only the constant `PostgresDatabases.Database`), yet is in `UsesPostgres`.  The cover tests/SqlSource.Tool.Tests/Commands/SecretTests.cs:107-132 makes the same real Npgsql open of port 1 (Timeout=5, password the marker) through `PostgresDescriber` with no container, and asserts SQLSRC224 and no marker in out or error (`ShouldHold`, :56-66), the line `    server: Npgsql could not reach the server: ConnectionRefused` (:121), and neither marker nor `127.0.0.1` in the error, the log or the capture; the mapping is held exactly at the unit level by tests/SqlSource.Tool.Tests/Describing/Postgres/ConnectionErrorsTests.cs:70-80.  After the proposal's username, what the container test adds is only that `ServerLines` is exactly that one line (the cover uses `ShouldContain`) and `Arguments`/`Step` (:374-376); the cleanup may assert those too, but they are the describer's own code, not the server's.  src/SqlSource.Tool/Describing/Postgres/AGENTS.md:25 keeps the database folder for "what only a server can show", which this is not.  The finding is right that the saving in time is nil (0.001 s, evidence/DatabaseTests.Postgres.Describer-2.md:86) and the container is shared; the gain is that a test needing no engine leaves the engine project, as the twin F-DatabaseTests.SqlServer-1-03 (confirmed) does.  Search: no AGENTS.md, readme, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names the test; the comment (:76) explains the port.  The Windows caveat of the SQL Server verdict does not apply: the cover already asserts the exact line on every OS it runs on.
- condition: The verifier adds that the cleanup may also assert that `ServerLines` is exactly the one line and the `Arguments` and `Step` of the capture, which the container test held; the Windows caveat of F-DatabaseTests.SqlServer-1-03 does not arise for Npgsql.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-root`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-99/tool-root/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Commands.|FullyQualifiedName~SqlSource.Tool.Tests.OutcomeTests.|FullyQualifiedName~SqlSource.Tool.Tests.ToolHostTests.|FullyQualifiedName~SqlSource.Tool.Tests.UsageValueTests.' '*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-99/tool-root/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Commands.|FullyQualifiedName~SqlSource.Tool.Tests.OutcomeTests.|FullyQualifiedName~SqlSource.Tool.Tests.ToolHostTests.|FullyQualifiedName~SqlSource.Tool.Tests.UsageValueTests.' '*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-99/tool-root/before/reports/mutation-report.json artifacts/test-cleanup/phase-99/tool-root/after/reports/mutation-report.json
```

Scope `tool-describing-sqlserver`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-99/tool-describing-sqlserver/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Describing.SqlServer.' 'Describing/SqlServer/**/*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-99/tool-describing-sqlserver/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Describing.SqlServer.' 'Describing/SqlServer/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-99/tool-describing-sqlserver/before/reports/mutation-report.json artifacts/test-cleanup/phase-99/tool-describing-sqlserver/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-99/tool-root/before/reports/mutation-report.json SecretTests.Run_SqlServerDescriberThatCannotOpen_PrintsSqlsrc224AndNothingOfTheValue
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-99/tool-root/before/reports/mutation-report.json SecretTests.Run_DescriberThatReturnsTheValue_PrintsTheMarkInEachPlace
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-99/tool-root/before/reports/mutation-report.json SecretTests.Run_SecretTypedAfterDatabase_IsNeverPrinted
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-99/tool-root/before/reports/mutation-report.json SecretTests.Run_PostgresDescriberThatCannotOpen_PrintsSqlsrc224AndNothingOfTheValue
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-99/tool-describing-sqlserver/before/reports/mutation-report.json SqlServerDescriberTests.OpenAsync_ConnectionStringRefusedBeforeOpening_IsSqlsrc224WithTheFixedLine
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-root`, `tool-describing-sqlserver`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-99/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-99/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-99/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-99/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-99/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-99/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

### Covers and run time

Mutation cannot guard these findings, whose test is blind, reaches no tested mutant, or has its subject outside the scope: F-DatabaseTests.SqlServer-1-01, F-DatabaseTests.SqlServer-1-02, F-DatabaseTests.SqlServer-1-03, F-SqlSource.Tool.Tests.Commands-2-07, F-engines-03.  For them, and for the database tests of the phase if it has any, the pull request lists each removed test or row beside its named cover, and the test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-DatabaseTests.SqlServer-1-02 | demote | `SessionTests.OpenAsync_UnreadableConnectionString_IsSqlsrc224WithTheFixedLine` | `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerDescriberTests.cs`: `SqlServerDescriberTests.OpenAsync_ConnectionStringRefusedBeforeOpening_IsSqlsrc224WithTheFixedLine` |
| F-DatabaseTests.SqlServer-1-03 | demote | `SessionTests.OpenAsync_UnreachableHost_IsSqlsrc224WithNoPartOfTheValue` | `tests/SqlSource.Tool.Tests/Commands/SecretTests.cs`: `SecretTests.Run_SqlServerDescriberThatCannotOpen_PrintsSqlsrc224AndNothingOfTheValue` |
| F-SqlSource.Tool.Tests.Commands-2-07 | delete | `SecretTests.Run_SecretTypedAfterDatabase_IsNeverPrinted` | `tests/SqlSource.Tool.Tests/Commands/SummaryTests.cs`: `SummaryTests.Run_DatabaseFarFromEveryDatabase_IsNamedByItsPosition` |
| F-engines-03 | demote | `SessionTests.OpenAsync_UnreachableHost_IsSqlsrc224WithAFixedTextAndTheSocketsError` | `tests/SqlSource.Tool.Tests/Commands/SecretTests.cs`: `SecretTests.Run_PostgresDescriberThatCannotOpen_PrintsSqlsrc224AndNothingOfTheValue` |

Run time, on one machine, before and after: `dotnet test --project tests/DatabaseTests/Postgres`, `dotnet test --project tests/DatabaseTests/SqlServer`, `dotnet test --project tests/SqlSource.Tool.Tests`.
This phase changes describer tests: run `tests/DatabaseTests/Postgres` on `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24` and `tests/DatabaseTests/SqlServer` on `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04` too, for the projects it touches, as `AGENTS.md` asks before a push.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Edits the findings and their verdicts ask for:

- F-SqlSource.Tool.Tests.Commands-2-06: The verifier adds: `docs/tech-debt/TD-0089-theories-that-branch-on-their-row.md:20` names "three tests over `debug`" of `SecretTests.cs`; update that item in the same change, since this test stops being one.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `docs/tech-debt/TD-0034-redaction-of-a-describers-text-has-holes.md`: 25
- `docs/tech-debt/TD-0089-theories-that-branch-on-their-row.md`: 9, 20
- `tests/AGENTS.md`: 38
- `tests/DatabaseTests/SqlServer/README.md`: 3
- `docs/architecture/postgres-describer.md`: 72
- `src/SqlSource.Tool/AGENTS.md`: 42

## Left out

- F-SqlSource.Tool.Tests.Commands-2-16 (keep, `SecretTests.Run_PostgresDescriberThatCannotOpen_PrintsSqlsrc224AndNothingOfTheValue`): a **keep**; the test stays as it is.
- F-SqlSource.Tool.Tests.Commands-2-17 (keep, `SecretTests.Run_ConnectionStringSplitByTheShellWithAPartThatEndsInSql_IsAWrongCommandLineThatDoesNotRepeatIt`): a **keep**; the test stays as it is.
- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
