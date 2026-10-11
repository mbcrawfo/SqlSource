# TD-0039, duplicated and uneven patterns in the tests - design

Date: 2026-10-11

Resolves [TD-0039](../../tech-debt/TD-0039-duplicated-and-uneven-patterns-in-the-tests.md).  The item lists eight findings of a maintainability review of `tests/`.  This document says what each becomes, in which pull request, and records the decisions the item left open.  The numbers 1 to 8 below are the item's own.

## Goal

Each helper, constant and fixture text the item names has one definition, each test class the item names covers one subject, and a failing test names its case.  A later change to a message, a fixture or the generated file's header then touches one place in the tests, and phase 3's describer tests find the helpers they need in place of a class to copy from.

Success is:

- Nothing under `src/` changes.
- No test is lost or weakened: the test cases before and after are the same, apart from the class a test is in and the eleven cases of item 6, which become facts with names of their own.
- `tests/SqlSource.Tests/Generator/` still compiles against Roslyn 4.8.0 with C# 12.
- The allocation budgets of `SqlFileParserAllocationTests`, `PathResolverAllocationTests` and `SidecarReaderAllocationTests` are not touched.
- When the last pull request merges, `docs/tech-debt/TD-0039-*.md` and its row are gone, and no file outside `docs/superpowers/` names the id.

Not in scope: a test that is missing, which is [TD-0037](../../tech-debt/TD-0037-small-gaps-of-the-log-and-of-the-tests-of-sub-phase-2-6.md) and others; a helper for a duplicate the item does not name.

## What the review found

All eight findings hold on `main` at `33f3586`.  Five counts of the item are off, none in a way that changes the fix:

| Item | The item says | Found |
|----|----|----|
| 3 | `TestClock`'s seed is a literal in seven assertions | Four hold the seed.  Three hold the seed plus what the test advanced the clock by: `...12.912Z`, `...11.462Z`, `...11.487Z` |
| 3 | Fifty-five `+ ".json"` | Fifty-seven, twenty-six of them in `FilterRunTests` |
| 3 | The secret marker and connection value a dozen times | Ten declarations, four values |
| 4 | `new ServerInfo(..., "FakeDriver", null, Empty)` seven times | Five: two in `SecretTests`, three in `DatabaseRunsTests` |
| 5 | The triple `.ShouldBeEmpty()` twenty-seven times | Twenty-seven in `GeneratedSourceTests`, and six more in `TokenValidationTests`, `GeneratorParameterLevelTests`, `SettingLevelTests` and `DialectTests` |

Three of the item's proposed fixes do not hold as written:

- **`SqlDiagnostics.HelpLinkBase` in `tests/SqlSource.Tests` (1).**  The constant is the product's.  `SqlDiagnosticsTests` and `ToolDiagnosticsTests` are the two tests that say what the address is; compared with the constant, they would compare the product with itself.
- **`Hosts.Create` everywhere (7).**  `CliRun.CreateHost` gives a host its writers, describers, exchange and clock, and `Hosts.Create` has no parameter for any of them.  `ToolHost` is a record, and `DatabaseRunsTests` already writes `Hosts.Create(...) with { ... }`.
- **`ToolRoot` on `Hosts` (2).**  A command is not a host, and `ToolRoot()` is another name for `Cli.BuildCommands()`.

## Decisions

| Question | Choice |
|----|----|
| Shape of the work | One spec and three pull requests, merged in order: the helpers of the tool's tests, the structure of the tool's tests, the generator's tests.  Each has its own plan, written when its work starts.  Each rewrites TD-0039 to what remains, keeping the item's numbers; the last deletes it. |
| Item 1, the address in the descriptor tests | The two tests of `tests/SqlSource.Tests` keep their literal.  The tool's tests share one. |
| Item 2, `ToolRoot` | Deleted.  Its callers call `Cli.BuildCommands()`. |
| Item 7, one way to build a host | `Hosts.Create` is the one, with no new parameter.  `CliRun` sets what differs with `with`. |
| Item 8, `TestSidecar` and `TestSidecars` | Both: `Snapshot/TestSidecars.cs` is linked into the tool's tests, and the tool's `TestSidecar` is renamed `SidecarFiles`, since it writes and reads files on disk.  The rename is in pull request 1, where item 3 adds members to the class. |
| Item 5, how a split class shares its helpers | A static helper for the parser's tests, where the helpers hold no state; a scene, composed as `DescribeScene` is, for `DatabaseRunsTests`, where they hold a folder and a describer.  No base class. |
| Item 5, how many classes | The item lists eight subjects of `SqlFileParserTests`.  Three small ones join a neighbour, so that no class has fewer than about fifteen tests. |

## Pull request 1: the helpers of the tool's tests (items 1, 2, 3, 4, 7 and the sidecar of 8)

Every helper stays at the root of `tests/SqlSource.Tool.Tests`, in the namespace `SqlSource.Tool.Tests`, as `tests/AGENTS.md` says.  No test moves and none is renamed.

### The help link once (1)

`Expected.See(id)` gives the whole line that ends a diagnostic, `    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc207` and a line end, for an id such as `SQLSRC207`.  The literal is the test project's own and is not read from the product.  `DescribeScene.See` goes, and so do the constants of `ReporterTests`, `FilterTests`, `DatabaseRunsTests`, `SidecarStoreTests` and `RunProjectsTests` and the inline copies of `DescribeTests` and `CliTests`.

### One `Plans` (2)

- `Plans.Result(params TestProject[])` returns the `RunPlanResult`, and `Plans.Of` is its `Plan`.  `RunPlannerTests` and `RunPlannerErrorTests` lose their `Plan`.
- `Plans.TypeNamed(name, arguments = "")` is the type with the attribute that both classes build with `Type(...)`.  The item's name, `Plans.Type(name, arguments)`, is taken by the constant `Plans.Type`.
- The project factories of the two classes stay: they differ from `Plans.Postgres` in what the type claims, which is what those tests are about.
- `ConnectionArguments.Parse(text)` is the one `Parse` of `ConnectionTests` and `DatabaseRunsTests`.
- `ToolRoot()` goes from `ConnectionTests` and `UsageCheckTests`.

### Fixture text, sidecars, the clock and secrets (3)

- `SqlTexts` holds the `Users.sql` text with `GetUser` and `GetInvoice`, and each variant under a name that says how it differs.  Only texts that are the same merge; a text that differs for the sake of its test stays in its class.  `SqlTexts.WithInvoiceEdited(text)` is the edit `Replace("SELECT 2;", "SELECT 22;")`.
- `SidecarFiles.Empty(formatVersion, toolVersion)` is the text of a sidecar with no entry, the tool's own versions unless given, and `SidecarFiles.Newer` is the one of format 2 and tool `9.9.9` that five tests write.  `SidecarFiles.Old` is the `DateTime` of four classes.
- `TestClock.StartIso` is the seed as a log writes it, for the four assertions that hold it.  The three that hold the seed plus an offset stay as they are: each says a time the test worked out.
- `Secrets` names the four values: the word, the marker, and the two connection values.  Two of them become one only where no test depends on the difference.
- Every `+ ".json"` becomes `SidecarFormat.PathFor`.

### What `FakeDescriber` lacks (4)

- `FakeDescriber.Rows` is the description of `CheckTests` and `CheckFilterTests`.
- `FakeDescriber.ServerWith(version = "16.4", driverVersion = null)` is the `ServerInfo` of the fake driver.
- `FakeDescriber.Failure` gains an overload with `step` and `help`, for the six five-argument `DescribeFailure`.

### One way to build a host (7)

- `CliRun.CreateHost` is `Hosts.Create(...) with { Out, Error, Describers, Exchange, Time }`.
- `DescribeTests` and `CliTests` hold a `CliRun` in a field and dispose it, as the other classes do.  xunit makes an instance of the class for each test, so each test still has a run of its own.
- The four tests of `DescribeTests` that build a postgres project by hand use `Plans.Postgres`, where it gives the same project.

### `SidecarFiles` (8, in part)

`TestSidecar` is renamed `SidecarFiles`.  `tests/SqlSource.Tests/Snapshot/TestSidecars.cs` is compiled into `tests/SqlSource.Tool.Tests` by its path, as the three harness files are, and `SidecarFiles.EntryFor` calls `TestSidecars.Entry` with the query's name, hash and database.  `tests/AGENTS.md` names the fourth linked file in the same commit.

## Pull request 2: the structure of the tool's tests (item 6 and `DatabaseRunsTests` of item 5)

### One fact for each kind (6)

Each theory whose row is a kind that a `switch` or an `if` turns into an arrangement becomes one fact for each kind.  A fact arranges its own case and calls a private method that holds what the cases share.

| Class | Theory | Facts |
|----|----|----|
| `DescribeRunTests` | `Run_SidecarThatIsNotThisTools_IsWrittenAgainWithEveryQueryDescribed` | another tool version; a lower format version; a merge conflict |
| `CheckTests` | `Check_SidecarThatCannotBeRead_IsOneDifferenceAtTheSidecar` | a merge conflict; a directory |
| `ExitCodeTests` | `Run_ErrorWithALogAskedFor_ExitsOneWithoutTheLine` | the option; the variable; a log that cannot be opened; a log that is a file of the run |
| `RunLogTests` | `Run_LogThatIsAFileOfTheRun_IsSqlsrc218AndTheFileIsAsItWas` | the option; the variable |

A theory whose rows are data that the test passes on, such as a format version or a command line, stays a theory.

### `DatabaseRunsTests` by subject (5)

| Class | Subject |
|----|----|
| `DatabaseRunsTests` | Sessions and connections: one session for each database, `SQLSRC213` to `SQLSRC216`, and a database with nothing to describe |
| `DatabaseRunsFailureTests` | A session that cannot be opened and a query that fails: how each is printed, and that the connection is not in it |
| `DatabaseRunsFaultTests` | A describer that breaks its contract: one that throws, one that returns nothing, and a description that holds the connection value |
| `DatabaseRunsCancellationTests` | A run that is cancelled |

`DatabaseRunsScene`, at the root of the project, holds what the four share: the folder, the describer, the exchange, the environment, the writer of errors, the project and `RunAsync`.  It is disposable and composed as `DescribeScene` is.

`Of_TextThatHoldsTheSecretTwice_HasTheMarkInBothPlaces`, a fact about `Redaction.Of`, moves to `RedactionTests`.

## Pull request 3: the generator's tests (the rest of items 5 and 8)

### `SqlFileParserTests` by subject (5)

| Class | Subject |
|----|----|
| `SqlFileParserTests` | Blocks, names and the shape of a name, the preamble, summaries, errors as a whole, line endings and equality |
| `SqlFileParserTokenTests` | Tokens as segments, reserved names, `token` markers and their defaults, `token-ignore`, and the kept form |
| `SqlFileParserParameterTests` | Parameters, declared variables and `param` markers |
| `SqlFileParserDialectTests` | The dialect of the project, dialect markers and continued strings |
| `SqlFileParserMarkerTests` | The scope and the errors of generator parameters, and the output, database and model markers |

`Parsing/ParseOf.cs` holds the helpers that stand between the tests today: `Blocks`, `Errors`, `ReadOf`, `Sql`, `Kept`, `Block`, `Tokens`, `TokenNames`, `Parameters` and `Nullability`.  No test is renamed.

### `GeneratedSourceTests` by subject (5)

| Class | Subject |
|----|----|
| `GeneratedSourceTests` | The layout of a generated file: the location of the constants, each kind of type, nesting and namespaces |
| `GeneratedSourceOrderTests` | The order of members, what `Path` selects, Windows paths, and a file that two types share |
| `GeneratedSourceMethodTests` | A query with tokens |
| `GeneratedSourceEscapingTests` | SQL and names that need escaping, in code and in an XML comment |
| `GeneratedSourceWarningTests` | Generated code has no warning |
| `HintNameTests` | The names of generated files, and those that would collide |

All six are in `tests/SqlSource.Tests/Generator/`, which `tests/SqlSource.Tests.RoslynFloor` compiles whole.

### What the harness gains (5)

Both are in `GeneratorHarness.cs`, which all three projects compile, and use nothing that Roslyn 4.8.0 lacks:

- `GeneratorRun.ShouldBeClean()` is the three assertions that `Diagnostics`, `CompilationErrors` and `GeneratedCodeWarnings` are empty, in all thirty-three places.
- `GeneratedFile.Header` is the three lines that start a generated file, in the eleven snapshots of `GeneratedSourceTests`, `TypeEmitterTests` and `FileDiagnosticsTests`.  It is a literal of the tests and is not read from the product, so the snapshots still say what the header is.

### Small duplicates (8, the rest)

- `TextSpans.Of(text, value, last: false)` is both `SpanOf`: the first occurrence, or the last.
- `TestModels.Literal(text)` is `Literal` of `SqlFileReaderTests` and `Text` of `MethodWriterTests`.
- `TestModels.Query(...)` is `TypeEmitterTests.Query`, and the five `SqlQuery` built by hand use it where its parameters cover what the test sets.

## Testing

The work changes tests and nothing they test, so what holds it is that the same cases run and pass.

- Each pull request records the output of `dotnet test --list-tests` for each project it touches, before and after.  Pull request 1 leaves it the same.  Pull request 2 changes the class of the tests of `DatabaseRunsTests` and of the one fact that moves, and replaces eleven rows with eleven facts.  Pull request 3 changes the class of the tests it moves, in `SqlSource.Tests` and in `SqlSource.Tests.RoslynFloor`.
- Every test passes, and `./pre-commit-validation.sh` passes, before each commit.
- A helper that decides something gets a test of its own, in its project: `Expected.See` at the root of the tool's tests, beside `TestProjectTests`, and `TextSpans.Of` with and without `last` in `tests/SqlSource.Tests/Parsing/`.  A helper that only names a value gets none.

## Documents

- `tests/AGENTS.md` changes in the commit that links `Snapshot/TestSidecars.cs`, and in any commit that moves or renames a file it names.
- `docs/tech-debt/TD-0039-*.md` and its row in `docs/tech-debt/README.md` are rewritten by pull requests 1 and 2 to what remains, and deleted by pull request 3.
- `README.md`, `CONTRIBUTING.md`, `docs/diagnostics.md` and `docs/publishing.md` do not change.
- No new tech debt is expected.  What a pull request finds and does not fix gets its own item, as the core rules say.
