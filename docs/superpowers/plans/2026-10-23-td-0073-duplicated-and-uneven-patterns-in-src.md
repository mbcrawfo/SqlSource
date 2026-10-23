# TD-0073 Duplicated and Uneven Patterns in `src/` Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve `docs/tech-debt/TD-0073-duplicated-and-uneven-patterns-in-src.md`: one copy of each pattern its fourteen items name, with no change of behaviour except where two copies already disagree.

**Architecture:** Shared code goes where both its users may reach it: the generator's root namespace or `Parsing/` for what the generator and the tool share, `Describing/Describers/` for what both describers share.  Each task removes one item's copies and moves its callers onto the one that is left; a test pins each of the few behaviours that change.

**Tech Stack:** C# (`netstandard2.0` generator, `net8.0` tool), xunit with Shouldly, SQL Server and PostgreSQL in Docker for the container projects.

**Spec:** `docs/tech-debt/TD-0073-duplicated-and-uneven-patterns-in-src.md` (its Problem and Proposed fix), read with `AGENTS.md`, `src/SqlSource/AGENTS.md`, `src/SqlSource.Tool/AGENTS.md`, `tests/AGENTS.md` and `docs/architecture/AGENTS.md`.

## Global Constraints

- Change no behaviour, except where two copies already disagree: items 4, 5, 7 and 8 below.  Each such change gets a test.
- `src/SqlSource` is `netstandard2.0`: no `char.IsAscii*`, no `int.TryParse(ReadOnlySpan<char>)`, no range slices (`[1..]`), no `string.StartsWith(char)`, no collection expression that targets `ImmutableArray<T>`.
- `Parsing/` allocates nothing per character or line; the allocation budgets of `tests/SqlSource.Tests/Parsing/SqlFileParserAllocationTests.cs` must still pass unchanged.
- `Settings/` uses nothing of `Parsing/`, `Generation/` or `Diagnostics/`; a rule it shares goes in the root namespace `SqlSource`.
- `Describing/Describers/` uses nothing of the run's steps: only `Outcome`, `Reporting/`, `Logging/` and the generator's types.  A describer depends on nothing in `Describing/` outside `Describers/` and its own folder.
- A record whose name ends in `Request` or `Answer` crosses the exchange and keeps its exact shape; no capture changes (`tools/record-fixtures.sh` must not be needed).
- A log member is added and never renamed.
- Rename, move or split a name, and update every `AGENTS.md`, `docs/architecture/` page and doc comment that names it, in the same commit.  Do not edit `docs/superpowers/` files other than this plan: they are history.
- Every commit passes `./pre-commit-validation.sh`.  After a task that changes a describer or its tests, also run `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/SqlSource.Tests.Postgres` and `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04 dotnet test --project tests/SqlSource.Tests.SqlServer` before the branch is pushed (once per push is enough, after the last such task).
- Test names are `Method_Scenario_Outcome`, assertions Shouldly's, every test class `sealed`; a test of `src/<Project>/<Folder>/<Type>.cs` is in the mirrored folder as `<Type>Tests.cs`.
- Leave alone, where possible, what the units running beside this one change: `Cli`, `ToolHost`, `SidecarCheck`, `FileOutcomes` (td-0036) and the SQL Server test containers and `tests/SqlSource.Testing` (td-0072).
- Take a new `TD-` id only after syncing with `main`; the next free id is in `docs/tech-debt/README.md`.
- A filtered run is `dotnet test --project <project> --filter-class "*<Class>"` or `--filter-method "*<Method>*"` (Microsoft.Testing.Platform with xunit v3).
- Every commit message ends with the line `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Where the merged code differs from the item

- **Item 2** asks for the shared builders in `Describing/DescribeErrors.cs`.  That file is a step of the run and uses `Planning/`; a describer must not depend on it.  The shared builders go in `Describing/Describers/DescribeFailures.cs` instead.
- **Item 5** says the three readings differ.  `LeadingInteger` and `MajorVersionOf` agree on every input (0 and null both mean "cannot tell", and both decide nothing); SQL Server's reads the text up to the first dot and needs all of it to be digits, so `13beta1` is 13 to the first two and unknown to it.  One rule, the leading digits, is adopted; SQL Server's `ProductVersion` is always dotted digits, so the change reaches no real server, and a test pins it.
- **Item 6** counts nine strips.  `SqlMarkerScope.ReadParam` and `SqlServerErrors.NamedIn` strip a prefix too, but of a marker's text and of a server's message, where no lexeme or reported name exists; they stay.  `NpgsqlCheck` strips `@` or `:`, Npgsql's own prefixes, where the SQL Server pair strips `@`: one helper takes the prefixes it strips.
- **Item 9** names `FileTypesInput.Probe`; the record's member is `Probe` and its parameter `probe`.  Both become `NpgsqlProbe`.
- **Item 14** names "110 lines of the text of a conflict's summary" in `ModelCollector`.  The 110 lines are the XML `<summary>` of a model that several queries share (`SummaryXml` to `AppendSeparator`), and that is what moves.

## Decisions

- **Item 4: write `tableMatch` beside `result` in PostgreSQL's `table-match`.**  Recommended over recording the split: a reader of the log, and phase 10's bundle, then read one member for both engines, and the shared builder writes it; `result` stays, since a member is never renamed.  PostgreSQL's event becomes `query`, `result`, `tableMatch`, `table`, with `tableMatch` equal to `result`.
- **Item 7: a declared type's name may hold a letter or a digit that is not ASCII.**  Verified on `mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04` and `2017-CU31-GDR19-ubuntu-18.04`, through `sys.dm_exec_describe_first_result_set(N'SELECT @p AS c', N'@p <type>', 0)`: both accept `dbo.Größe`, `Größe`, `dbo.名前`, `名前`, `dbo.x٣` and `dbo.Ａbc` unbracketed, and refuse `٣x` (a digit first), `x²`, `x①` and `x·` with `102`.  So the ASCII rule of SQL Server's `DeclaredTypeText` refuses names the server takes, and it joins the other two copies: a letter or a digit by `char.IsLetterOrDigit`, `_`, `@`, `#` or `$`, with a first character a letter by `char.IsLetter`, `_`, `@` or `#`.  What remains apart from the server is recorded as a new tech-debt item (Task 6): the server also takes a combining mark (`xé`), a letter number (`xⅠ`) and a format character (`x‌`) after the first character, which the rule refuses, and refuses a letter newer than its Unicode tables (`ꞔ`, U+A794), which the rule takes and the server then refuses with `102`.
- **Item 8: a schema the server reported is compared with `sys` ordinally.**  Verified on both images: in a database of collation `Latin1_General_CS_AS`, `SYS.int` and `Sys.int` fail with `2715`, `CREATE SCHEMA SYS` makes a schema apart from `sys`, and a type `SYS.money2` is described with `user_type_schema` `SYS`.  The server compares a schema by the database's collation and always reports its own as `sys`, so a reported `SYS` is a user's schema, and `SqlServerTypes`' comparison ignoring case read a user type of it as a built-in one.  A key a user writes is another rule and keeps it: `TypeKeys` reads a bare `sys`, `SYS` or `pg_catalog` ignoring case, as an identifier is folded, and a quoted one exactly.
- **Item 10: the interface gives the probe's findings, not its flags.**  Phase 7 adds `SqlJson` detection and plugin detection to the probes, so `IDriverProbe` yields each diagnostic the probe finds with its argument, and a new flag is a new finding inside the probe with no change to `ReportDriver`.
- **Item 13: PostgreSQL's `DeclaredTypeText.Read` gets the `Outcome` shape and one reason, `NotATypeName`.**  A finer reason, as SQL Server's says what was found, would change `SQLSRC228`'s text; that is recorded in the new tech-debt item of Task 6, not done here.
- **Item 14: `PlanExpression` is split into partial files** (its `Reader` holds state that both the forms and the proofs read), with the tokenizer a class of its own; the other three move their extra job into a class of its own.

## The split

The work is two parts, on this one branch, in this order: Part 1 is Tasks 1 to 7 (the generator, `Parsing/` and what the tool calls of them), Part 2 is Tasks 8 to 14 (the describers) and Task 15.  When the whole branch changes 150 files or fewer against `main` (`git diff --name-only origin/main...HEAD | wc -l`), it is one pull request.  Otherwise Part 1 is pushed and reviewed alone at its last commit, titled `(part 1 of 2)`, and Part 2 follows it, `(part 2 of 2)`, after Part 1 merges.  Every task leaves the solution building and every test passing, so either boundary is a valid head.  When Part 1 goes alone, its last commit rewrites `TD-0073` to describe only the items Part 2 resolves (1, 2, 3, 4, 11, 12, the tool's half of 13 and `PlanExpression` of 14), as `docs/tech-debt/AGENTS.md` asks of a partial fix, and Task 15 deletes it.

## Review Focus

1. A connection that throws while the shared opener reads its answer: the connection is disposed and the exception passes, as today (Task 8).
2. A cancelled `live` call of PostgreSQL: a cancellation passes through `AnswerAsync`, and an `NpgsqlException` thrown after the token is cancelled is still an answer's error, as today; SQL Server's cancellation filter is not copied (Task 9).
3. SQL Server's table match whose first two checks fail: `table` is still not asked, and the shared order still decides the first check that fails (Task 11).
4. The new `tableMatch` member of PostgreSQL's event goes through the session's log like every string, so a secret in it is removed (Task 10).
5. A reported parameter name with no prefix, and an Npgsql name with `:`: both still map to their parameter (Task 5).

---

## Part 1

### Task 1: One reading of a version's major (item 5)

**Files:**
- Create: `src/SqlSource/Versions.cs`
- Modify: `src/SqlSource/Generation/Types/SqlClientProbe.cs`, `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`, `src/SqlSource.Tool/Describing/Postgres/PostgresSession.Builders.cs`, `src/SqlSource.Tool/Describing/SqlServer/SqlServerSession.cs`
- Test: `tests/SqlSource.Tests/VersionsTests.cs` (new), `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs`, `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerSessionTests.cs`

**Interfaces:**
- Produces: `internal static class Versions` in namespace `SqlSource`, with `public static int? LeadingMajor(string? version)`: the leading ASCII digits as an `int`; null for null, no leading digit, or more than an `int` holds.

- [ ] **Step 1: Write the failing tests.**  `VersionsTests.LeadingMajor_Version_IsItsLeadingDigits` as a theory: `"18.4"`→18, `"13.12"`→13, `"9.6.24"`→9, `"18beta1"`→18, `"16.0.1000.6"`→16, `"7.0.0-preview4"`→7, `"6"`→6, `"devel"`→null, `"preview"`→null, `""`→null, `"  14"`→null, `"99999999999.0.0"`→null, `null`→null.  Move the cases of `PostgresSessionTests`' `MajorVersionOf` theory here and delete that theory.  In `SqlServerSessionTests`, `OpenAsync_ServerWithASuffixBelowTheFloor_IsSqlsrc235`: a `ProductVersion` of `"13beta1"` with an edition that is not Azure gives `SQLSRC235` with arguments `[Database, "SQL Server", "13beta1", "14 (SQL Server 2017)"]` (today it opens: the behaviour that changes).
- [ ] **Step 2: Run them and see `Versions` missing and the SQL Server case open.**  `dotnet test --project tests/SqlSource.Tests --filter-class "*VersionsTests"` and `dotnet test --project tests/SqlSource.Tool.Tests --filter-method "*OpenAsync_ServerWithASuffix*"`.
- [ ] **Step 3: Implement `Versions.LeadingMajor`** with a loop over `'0'..'9'`, `Substring` and `int.TryParse(string, NumberStyles.None, CultureInfo.InvariantCulture, out _)` (netstandard2.0).  `SqlClientProbe.IsNewerThanKnown` becomes `Referenced && Versions.LeadingMajor(Version) > NewestKnownMajor` and `LeadingInteger` goes; `MajorVersionOf` goes and `PostgresSession.OpenAsync` reads `Versions.LeadingMajor(server.Version)`; `SqlServerSession.IsBelowTheFloor` keeps its Azure test and reads `Versions.LeadingMajor(version) is { } major && major < MinimumMajorVersion`, and its comment says the leading digits decide, as for PostgreSQL.
- [ ] **Step 4: Run the three test projects' affected classes and see them pass**, then `./pre-commit-validation.sh`.
- [ ] **Step 5: Commit** `Read a version's major by one rule (TD-0073 item 5)`.

### Task 2: One constant per system schema, and one comparison of `sys` (item 8)

**Files:**
- Create: `src/SqlSource/SystemSchemas.cs`
- Modify: `src/SqlSource/Settings/TypeOverrides/TypeKeys.cs`, `src/SqlSource/Generation/Types/PostgresTypeMap.cs`, `src/SqlSource/Generation/Types/SqlServerTypeMap.cs`, `src/SqlSource/Generation/Types/QueryTyper.cs` (the `"sys."` and `"pg_catalog."` prefixes), `src/SqlSource.Tool/Describing/Postgres/PostgresSearchPath.cs`, `src/SqlSource.Tool/Describing/Postgres/Types/PostgresBindNames.cs`, `src/SqlSource.Tool/Describing/Postgres/Types/PostgresTypeNames.cs`, `src/SqlSource.Tool/Describing/SqlServer/SqlServerTypes.cs`
- Test: `tests/SqlSource.Tests/SystemSchemasTests.cs` (new), `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerTypesTests.cs`

**Interfaces:**
- Produces: `internal static class SystemSchemas` in namespace `SqlSource`: `public const string Postgres = "pg_catalog";`, `public const string SqlServer = "sys";` (both `const`: `PostgresBindNames` matches on them in patterns), and `public static bool IsSqlServer(string? schema)`, ordinal, whose doc says why: the server compares a schema by the database's collation and always reports its own as `sys`, and in a case-sensitive database `SYS` is a user's schema.

- [ ] **Step 1: Write the failing tests.**  `SystemSchemasTests.IsSqlServer_Schema_IsTheServersOwnSpellingAlone`: `"sys"`→true; `"SYS"`, `"Sys"`, `"dbo"`, `""`, `null`→false.  `SqlServerTypesTests.Declared_UserTypeOfASchemaSys_IsDeclaredByItsNames`: a type whose user type is `("SYS", "money2", null)` declares as `[SYS].[money2]`; `Spelled_UserTypeOfASchemaSys_NamesTheUserType`: it is spelled `SYS.money2` as any user type is (follow the spelling the existing user-type cases assert).
- [ ] **Step 2: Run them and see `SystemSchemas` missing and the two `SqlServerTypes` cases read the type as built-in.**
- [ ] **Step 3: Implement.**  Every private `SystemSchema`/`Catalog`/`SqlServerSystemSchema` constant goes and its users read `SystemSchemas.Postgres` or `SystemSchemas.SqlServer`; `PostgresBindNames.CatalogPrefix` becomes `SystemSchemas.Postgres + "."`; `QueryTyper`'s prefixes are built from the constants.  `SqlServerTypeMap.IsSystemSchema` and both conditions of `SqlServerTypes` call `SystemSchemas.IsSqlServer`.  `TypeKeys.IsSchema` keeps its rule (bare ignoring case, quoted exactly) and its doc comment says that a written key is read as an identifier and a reported schema is compared with `SystemSchemas.IsSqlServer`.  The SQL texts of `PostgresLive` and `SqlServerLive` keep their literals: they are SQL, not a comparison.
- [ ] **Step 4: Run the tests, then `./pre-commit-validation.sh`.**
- [ ] **Step 5: Commit** `Name each system schema once, and compare a reported sys exactly (TD-0073 item 8)`.

### Task 3: The driver probes behind one interface (item 10)

**Files:**
- Create: `src/SqlSource/Generation/Types/IDriverProbe.cs` (the interface, the `DriverFinding` record struct and the `DriverProbes` helper)
- Modify: `src/SqlSource/Generation/Types/NpgsqlProbe.cs`, `src/SqlSource/Generation/Types/SqlClientProbe.cs`, `src/SqlSource/SqlSourceGenerator.cs`, `src/SqlSource/AGENTS.md` (line 21's list of files that touch symbols gains `IDriverProbe.cs` if `DriverProbes.Has` stays there), `docs/architecture/generator.md` (the sentence on `ReportDriver`, line ~460)
- Test: `tests/SqlSource.Tests/Generator/NpgsqlProbeTests.cs`, `tests/SqlSource.Tests/Generator/SqlClientProbeTests.cs`

**Interfaces:**
- Produces:
  - `internal readonly record struct DriverFinding(DiagnosticDescriptor Descriptor, string Argument);`
  - `internal interface IDriverProbe { IEnumerable<DriverFinding> Findings { get; } }`: the diagnostics of the project's reference to the driver, each once, in the order they are reported (too old, then newer than known).  Its doc says a new flag of a probe is a new finding, as phase 7's `SqlJson` and plugin detection will be.
  - `internal static class DriverProbes { public static bool Has(Compilation compilation, string metadataName); }`, replacing the two private `Has`.
  - `NpgsqlProbe : IDriverProbe` and `SqlClientProbe : IDriverProbe`: `Findings` yields `(SqlDiagnostics.NpgsqlTooOld, VersionText)` when `IsBelowTheFloor` and `(SqlDiagnostics.NpgsqlNewerThanKnown, VersionText)` when `IsNewerThanKnown`, and the SqlClient pair likewise.  `Findings` is a computed property, never a member of the record, so the probes' equality is unchanged.
  - `ReportDriver(SourceProductionContext output, bool usesEntries, IDriverProbe probe)`.

- [ ] **Step 1: Write the failing tests.**  `NpgsqlProbeTests.Findings_ProbeBelowTheFloor_IsSqlsrc247WithTheVersion` and `Findings_ProbeNewerThanKnown_IsSqlsrc248WithTheVersion`, `Findings_ProbeOfAKnownVersion_IsEmpty`, built from `new NpgsqlProbe(...)`; the same three in `SqlClientProbeTests` for `SQLSRC267` and `SQLSRC268`.  The existing driver tests that see the diagnostics reported stay as they are and must pass unchanged.
- [ ] **Step 2: Run and see `Findings` missing.**
- [ ] **Step 3: Implement**, with `ReportDrivers`' two outputs passing `input.Left` as the probe.
- [ ] **Step 4: Run `tests/SqlSource.Tests` (and its `Caching*Tests`), then `./pre-commit-validation.sh`.**
- [ ] **Step 5: Commit** `Report a driver from its probe's findings (TD-0073 item 10)`.

### Task 4: Prefix the PostgreSQL side's names (item 9)

**Files:**
- Rename: `src/SqlSource/Generation/Types/DriverColumn.cs` → `NpgsqlColumn.cs`; `src/SqlSource.Tool/Describing/Postgres/Columns/TableMatcher.cs` → `PostgresTableMatcher.cs`; `tests/SqlSource.Tool.Tests/Describing/Postgres/Columns/TableMatcherTests.cs` → `PostgresTableMatcherTests.cs`
- Modify: every file `git grep -lw` finds for each old name below, outside `docs/superpowers/`: `NpgsqlProbe.cs`, `PostgresTypeMap.cs`, `QueryTyper.cs`, `FileTypesInput.cs`, `TypeKeys.cs`, `TypeOverride.cs`, `TypeOverrideGrammar.cs`, `PostgresSession.cs`, `src/SqlSource/AGENTS.md`, `src/SqlSource.Tool/AGENTS.md`, `docs/architecture/generator.md`, `docs/architecture/postgres-describer.md`, `tests/SqlSource.Tests/Generation/Types/PostgresTypeMapTests.cs`, `tests/SqlSource.Tests/Generator/NpgsqlProbeTests.cs`, `tests/SqlSource.Tests/Settings/TypeOverrides/TypeKeysTests.cs`

**Interfaces:**
- Produces, old → new:
  - `DriverColumn` → `NpgsqlColumn` (its values `Npgsql8`, `Npgsql10`, `None` stay)
  - `OverrideOf` → `PostgresOverrideOf`
  - `FileTypesInput.Probe` → `FileTypesInput.NpgsqlProbe`, and `Gather`'s parameter `probe` → `npgsqlProbe`
  - `TypeKeys.TryNormalize` → `TypeKeys.TryNormalizePostgres`; private `Names` → `PostgresNames`, `Aliases` → `PostgresAliases`, `TryNormalizeParts` → `TryNormalizePostgresParts`; `KnownNames` → `KnownPostgresNames`
  - `TableMatcher` → `PostgresTableMatcher`
  - Test methods: `PostgresTypeMapTests.Keys_OfTheMap_AreTheKnownNamesOfTheSettings` → `Keys_OfTheMap_AreTheKnownPostgresNamesOfTheSettings` (cited in `TypeKeys.cs`, `src/SqlSource/AGENTS.md` and `generator.md`); `TypeKeysTests.TryNormalize_*` → `TryNormalizePostgres_*`; the class `TableMatcherTests` → `PostgresTableMatcherTests`.
  - `TypeOverrideGrammar`'s own private `TryNormalize(key, engine, …)` is another method and keeps its name.

- [ ] **Step 1: Rename** (with the IDE-free tool of your choice: `git mv` for the files, then the identifiers), and update each doc and AGENTS.md line that names an old name.
- [ ] **Step 2: Check nothing is left:** `git grep -nw -e DriverColumn -e OverrideOf -e TableMatcher -e KnownNames -- ':!docs/superpowers' ':!docs/tech-debt/TD-0073*'` prints nothing, and `git grep -n 'TypeKeys.TryNormalize\b'` prints nothing.
- [ ] **Step 3: `./pre-commit-validation.sh`.**
- [ ] **Step 4: Commit** `Prefix the PostgreSQL side's names as SQL Server's are (TD-0073 item 9)`.

### Task 5: One way to strip a parameter's prefix (item 6)

**Files:**
- Create: `src/SqlSource/Parsing/SqlParameterName.cs`
- Modify: `src/SqlSource/Parsing/SqlLexeme.cs`, `src/SqlSource/Parsing/SqlParameterList.cs`, `src/SqlSource/Parsing/SqlDeclaredVariables.cs`, `src/SqlSource.Tool/Describing/SqlServer/ParameterCopies.cs`, `src/SqlSource.Tool/Describing/SqlServer/SqlServerSession.cs` (`Bare` goes), `src/SqlSource.Tool/Describing/Postgres/Statements/ParameterPlaceholders.cs`, `src/SqlSource.Tool/Describing/Postgres/Statements/UncastParameters.cs`, `src/SqlSource.Tool/Describing/Postgres/NpgsqlCheck.cs`
- Test: `tests/SqlSource.Tests/Parsing/SqlParameterNameTests.cs` (new)

**Interfaces:**
- Produces:
  - `internal static class SqlParameterName` in `SqlSource.Parsing`:
    - `public static TextSpan Of(TextSpan parameter)`: the span of a parameter lexeme's name, the lexeme less its one-character prefix (the lexer gives a parameter as the dialect's prefix and at least one name character, so this never fails for a lexeme of that kind).
    - `public static string WithoutPrefix(string name, string prefixes)`: a name as a server or a driver reported it, without its first character when that is one of `prefixes`, and as it is otherwise.
  - `SqlLexeme.ParameterName`: a `TextSpan` property, `SqlParameterName.Of(Span)`, documented as meaningful for a `Parameter` lexeme alone.
  - `SqlServerSession` and `ParameterCopies` call `WithoutPrefix(name, "@")`; `NpgsqlCheck` calls `WithoutPrefix(placed, "@:")`, whose comment says those are Npgsql's prefixes.  `ParameterCopies` keeps its `Prefix` constant where it writes a prefix back.

- [ ] **Step 1: Write the failing tests:** `Of_ParameterSpan_IsTheSpanLessThePrefix` (`new TextSpan(4, 3)` → `new TextSpan(5, 2)`); `WithoutPrefix_NameWithAPrefix_IsTheBareName` (`("@id", "@")`→`"id"`, `(":id", "@:")`→`"id"`, `("@id", "@:")`→`"id"`); `WithoutPrefix_NameWithoutAPrefix_IsTheName` (`("id", "@")`→`"id"`, `(":id", "@")`→`":id"`, `("", "@")`→`""`).
- [ ] **Step 2: Run and see `SqlParameterName` missing.**
- [ ] **Step 3: Implement, and move the nine sites onto it.**  In `Parsing/` the sites keep working on spans and make a string only where they made one (`NameOf` in `SqlParameterList`).  Each site's comment that explained its own strip goes.
- [ ] **Step 4: Run `tests/SqlSource.Tests` (its allocation tests included) and `tests/SqlSource.Tool.Tests`, then `./pre-commit-validation.sh`.**
- [ ] **Step 5: Commit** `Strip a parameter's prefix in one place (TD-0073 item 6)`.

### Task 6: One rule of a T-SQL name's characters (item 7)

**Files:**
- Modify: `src/SqlSource/Parsing/Quoting/QuoteReader.cs`, `src/SqlSource/Parsing/SqlDeclaredVariables.cs`, `src/SqlSource.Tool/Describing/SqlServer/SqlServerErrors.cs`, `src/SqlSource.Tool/Describing/SqlServer/DeclaredTypeText.cs`, `docs/architecture/sqlserver-describer.md` and `docs/manual/sqlserver.md` where they say what a declared type's name may hold (check with `git grep -n -i 'ascii\|bracket' docs/manual/sqlserver.md docs/architecture/sqlserver-describer.md`), `docs/tech-debt/README.md` and a new `docs/tech-debt/TD-<next>-what-the-first-refusal-of-a-declared-type-says.md`
- Test: `tests/SqlSource.Tests/Parsing/Quoting/QuoteReaderTests.cs` (new), `tests/SqlSource.Tool.Tests/Describing/SqlServer/DeclaredTypeTextTests.cs`, `tests/SqlSource.Tests.SqlServer/Describer/ParameterTests.cs`

**Interfaces:**
- Produces: `internal static bool IsNameCharacterOfSqlServer(char value)` on `QuoteReader`, beside `IsNameCharacterOfPostgreSql`: `char.IsLetterOrDigit(value) || value is '_' or '@' or '#' or '$'`, its doc naming what the server was found to take and refuse (the Decisions above).  `SqlDeclaredVariables.IsWordCharacter`, `SqlServerErrors.IsNamePart` and `DeclaredTypeText.IsNext` are replaced by it; `DeclaredTypeText.IsFirst` becomes `char.IsLetter(value) || value is '_' or '#' or '@'`.

- [ ] **Step 1: Write the failing tests.**  `QuoteReaderTests.IsNameCharacterOfSqlServer_Character_IsALetterADigitOrOneOfFour`: true for `'a'`, `'Z'`, `'1'`, `'é'`, `'名'`, `'٣'`, `'_'`, `'@'`, `'#'`, `'$'`; false for `' '`, `'.'`, `'€'`, `'²'`, `'·'`, `'['`.  In `DeclaredTypeTextTests`, add to `Read_TypeName_IsTheText` the rows `"dbo.Größe"`, `"Größe"`, `"dbo.名前"`, `"dbo.x٣"`, `"dbo.Ａbc"`; add to `Read_AnythingElse_IsRefusedWithWhatWasFound` the rows `("٣x", Shape)` and `("x²", Shape)` (keep `"€type"`).  In `tests/SqlSource.Tests.SqlServer/Describer/ParameterTests.cs`, `DescribeAsync_DeclaredTypeWithALetterThatIsNotAscii_IsTheUserType`: create `dbo.Größe FROM int` the way the project's other tests create a scratch object (and drop it in a `finally` if it changes what other tests see), describe `SELECT @p AS v` with `-- param: @p dbo.Größe`, and see the parameter's type `int` with the user type `dbo.Größe`.
- [ ] **Step 2: Run and see the new `DeclaredTypeText` rows refused** (`dotnet test --project tests/SqlSource.Tool.Tests --filter-class "*DeclaredTypeTextTests"`).
- [ ] **Step 3: Implement**, and correct `DeclaredTypeText`'s comment and remarks: a plain identifier is a letter, a digit or one of four, as the server reads one; the remainder is the new tech-debt item.
- [ ] **Step 4: Write the tech-debt item.**  Sync with `main` first and take `Next id`.  **Problem:** two first refusals of a `-- param:` type say or take less than they could: PostgreSQL's `DeclaredTypeText` gives one reason, `it is not written as a type name`, where SQL Server's says what was found; and SQL Server's refuses a name that holds a combining mark, a letter number or a format character after its first character, which the server takes (the cases of the Decisions), so such a name must be bracketed.  **Why:** TD-0073 changed no behaviour beyond its copies.  **Impact:** low.  **Proposed fix:** PostgreSQL's reader gives what was found, as SQL Server's does, changing `SQLSRC228`'s text and its section of `docs/manual/diagnostics.md`; and SQL Server's reader takes Unicode categories `Mn`, `Mc`, `Nl` and `Cf` after the first character, checked against the server.  **Trigger:** a user's report of either.  Add its row to `docs/tech-debt/README.md` and raise `Next id`.  It has no known issue in the manual: a user sees an error that says what to do.
- [ ] **Step 5: Run the SQL Server describer's tests on the default image and on the floor image, then `./pre-commit-validation.sh`.**
- [ ] **Step 6: Commit** `Read a T-SQL name's characters by one rule, as the server does (TD-0073 item 7)`.

### Task 7: The generator's small copies and its three large types (items 13 and 14, generator side)

**Files:**
- Create: `src/SqlSource/Generation/QueryNames.cs`, `src/SqlSource/Generation/Emitting/GeneratedText.cs`, `src/SqlSource/Generation/Types/MappedTypeWarnings.cs`, `src/SqlSource/Settings/TypeOverrides/TypeKeyNames.cs`, `src/SqlSource/Settings/TypeOverrides/TypeKeyText.cs`, `src/SqlSource/Settings/TypeOverrides/UnicodeEscape.cs`, `src/SqlSource/Generation/Models/ModelSummary.cs`
- Modify: `QueryTyper.cs`, `ModelReader.cs`, `TypeEmitter.cs`, `ModelEmitter.cs`, `MethodWriter.cs`, `MappedTypeResolver.cs`, `TypeKeys.cs`, `TypeOverrideMap.cs`, `TypeOverrideGrammar.cs`, `PostgresTypeMap.cs`, `SqlServerTypeMap.cs`, `ModelCollector.cs`, `src/SqlSource/AGENTS.md` (lines 21, 37 and 63), `docs/architecture/generator.md` (every line that names a moved member or links a moved file)
- Test: `tests/SqlSource.Tests/Generation/QueryNamesTests.cs`, `tests/SqlSource.Tests/Settings/TypeOverrides/TypeKeyTextTests.cs`, `tests/SqlSource.Tests/Settings/TypeOverrides/TypeKeyNamesTests.cs`, `tests/SqlSource.Tests/Settings/TypeOverrides/UnicodeEscapeTests.cs` (each new, holding the cases that move out of `TypeKeysTests` for the members that move), `tests/SqlSource.Tests/Generation/Types/PostgresTypeMapTests.cs` and `SqlServerTypeMapTests.cs` (the `Known*Names` they read)

**Interfaces:**
- Produces:
  - `QueryNames.IndexOf(EquatableArray<SqlQuery> queries, string name, int from) → int`, ordinal, -1 when absent; `QueryTyper.IndexOf` and `ModelReader.IndexOfQuery` go.
  - `GeneratedText.Header = "// <auto-generated/>\n#nullable enable\n"` and `GeneratedText.Indent = "    "`; `TypeEmitter`'s header is `GeneratedText.Header` with its pragma line and a blank line after it, byte for byte as today.
  - `MappedTypeWarnings.Add(ISymbol? symbol, List<string> ids) → bool` (today's `AddWarningIds`) and `MappedTypeWarnings.Distinct(List<string> ids) → EquatableArray<string>`, with the constants and helpers they alone use and the remarks paragraph on the ids.  `MappedTypeResolver` keeps every other member.  `src/SqlSource/AGENTS.md`'s list of the files that touch symbols gains `Generation/Types/MappedTypeWarnings.cs`.
  - `TypeKeyNames`: the tables (`Extensions`, `PostgresNames`, `PostgresAliases`, `SqlServerNames`, `SqlServerAliases`, `NamesOf`) as `internal` members that `TypeKeys` reads, and `KnownPostgresNames`, `KnownSqlServerNames` and `IsExtension`, which move from `TypeKeys`.
  - `TypeKeyText`: the grammar of a key's parts, `TrySplit`, `IsQuoted`, `TextOf`, `WritePart`, `TryReadPart`, `IsPlainCharacter`, with `EscapedOpening`, `Quote`, `NeedsEscape`, `MustEscape` and `IsEscaped`.
  - `UnicodeEscape.TryRead(…)`: today's `TryReadEscape` with `TryReadCodePoint` and `HexValue`, its signature as today's.
  - `TypeKeys` keeps the reading of a key for each engine: `TryNormalizePostgres`, `TryNormalizeSqlServer`, `TryNormalizeEach` and their private helpers.
  - `ModelSummary.Write(ModelRole role, List<ModelRequest> requests, StringBuilder text)` (today's `SummaryXml`, with `AddQuery`, `AppendQueries` and `AppendSeparator`) and `ModelSummary.IsSameQuery`, which `ModelCollector.QueryOf` also calls.

- [ ] **Step 1: Write `QueryNamesTests.IndexOf_*`** (found at or after `from`, absent → -1, compared ordinally: `"q"` does not find `"Q"`) and move the tests of the moved `TypeKeys` members into the three new test classes unchanged but for the type they call.
- [ ] **Step 2: Run and see the new types missing.**
- [ ] **Step 3: Move the code**, member by member, unchanged; callers name the new types.  No member's behaviour changes, and the generated text is byte for byte the same (the generator's snapshot and driver tests hold it).
- [ ] **Step 4: Check the sizes:** `wc -l` of `TypeKeys.cs`, `MappedTypeResolver.cs` and `ModelCollector.cs` is each below 550.
- [ ] **Step 5: `./pre-commit-validation.sh`.**
- [ ] **Step 6: Commit** `Share the generator's small copies, and split its three large types (TD-0073 items 13 and 14)`.

## Part 2

### Task 8: One base of the two sessions (items 1 and 12)

**Files:**
- Create: `src/SqlSource.Tool/Describing/Describers/DescriberSession.cs`, `src/SqlSource.Tool/Describing/Describers/SessionOpening.cs`, `src/SqlSource.Tool/Describing/SqlServer/SqlServerSession.Builders.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`, `PostgresSession.Builders.cs`, `PostgresSession.Calls.cs`, `src/SqlSource.Tool/Describing/SqlServer/SqlServerSession.cs`, `SqlServerSession.Calls.cs`, `src/SqlSource.Tool/AGENTS.md` (the `Outcome` of a disposable now names the base's opener), `docs/architecture/postgres-describer.md` (the describer contract) and `docs/architecture/sqlserver-describer.md` (its files table gains `SqlServerSession.Builders.cs`)
- Test: `tests/SqlSource.Tool.Tests/Describing/Describers/DescriberSessionTests.cs` (new)

**Interfaces:**
- Produces:
  - `internal sealed record SessionOpening<TRequest, TAnswer>(string Call, TRequest Request, Func<TRequest, CancellationToken, Task<TAnswer>> Live, Func<TAnswer, DescribeFailure?> ErrorOf, Func<TAnswer, ServerInfo> ServerOf, Func<TAnswer, ServerInfo, bool> IsBelowTheFloor, Func<ServerInfo, DescribeFailure> VersionNotSupported, Func<TAnswer, ServerInfo, IDescribeSession> Session)`: how one engine opens.
  - `internal abstract class DescriberSession(IDescribeExchange exchange, TimeProvider time, ServerInfo server) : IDescribeSession`, in `Describing/Describers/`:
    - `public ServerInfo Server { get; }`; `public abstract Task<Outcome<QueryDescription, DescribeFailure>> DescribeAsync(…)`; `public abstract ValueTask DisposeAsync()`.
    - `protected static Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync<TRequest, TAnswer>(OpenRequest request, IAsyncDisposable connection, SessionOpening<TRequest, TAnswer> opening, CancellationToken cancellationToken)`: asks `opening.Call` through `request.Exchange`; returns `ErrorOf`'s failure, then, from `ServerOf`'s server, `VersionNotSupported` when `IsBelowTheFloor`, else the `Session`, made with the constructor of `Outcome`; disposes `connection` on every path that returns no session, a throw included.
    - `protected static ServerInfo ServerOf(string? version, string driver, string? driverVersion, IEnumerable<ServerSetting> settings)`: throws `InvalidOperationException("The answer to 'open' holds no version and no error.")` for a null version, as both do today.
    - `protected static bool IsBelow(string version, int minimum) => Versions.LeadingMajor(version) is { } major && major < minimum;`
    - `protected Task<TAnswer> AskAsync<TRequest, TAnswer>(string method, TRequest request, Func<TRequest, CancellationToken, Task<TAnswer>> live, Action<TRequest, TAnswer, long> log, CancellationToken cancellationToken)` and `protected Task<(TAnswer Answer, long DurationMs)> TimedAskAsync<…>(…)`, timed by the run's clock as today.
    - `protected static Outcome<QueryDescription, DescribeFailure> Failed(DescribeFailure failure)`.
  - `PostgresSession` and `SqlServerSession` derive from it and keep their names, their `public static OpenAsync` (each now a call of the base's with its `SessionOpening`), their `Connection` and their constants.  `SqlServerSession.AskOpenAsync` goes.  `SqlServerSession`'s builders, the static members that read no state of the session (`RowsOf`, `DescribedOf`, `ColumnsParams`, `IsBelowTheFloor` and the nested `Probes`), move to `SqlServerSession.Builders.cs`, with the comment its sibling's part has.
  - Item 12: every `X.Failure is { } f` followed by `X.Value!` in `SqlServerSession` tests `!X.Succeeded` and reads `X.Value` and `X.Failure` with no `!`; `UnifyAsync`'s switch becomes `unified.Succeeded ? …Of(unified.Value) : unified.Failure switch { … }`.

- [ ] **Step 1: Write the failing tests** of the base, with a fake connection that records its disposal and an exchange that answers from a lambda: `OpenAsync_AnswerWithAnError_IsTheFailureAndDisposesTheConnection`; `OpenAsync_ServerBelowTheFloor_IsVersionNotSupportedAndDisposesTheConnection`; `OpenAsync_ServerAtTheFloor_IsTheSessionAndKeepsTheConnection`; `OpenAsync_ExchangeThatThrows_ThrowsAndDisposesTheConnection`; `ServerOf_NoVersion_Throws`; `IsBelow_Version_ComparesItsLeadingMajor` (`("13.12", 14)`→true, `("14.0", 14)`→false, `("devel", 14)`→false).
- [ ] **Step 2: Run and see `DescriberSession` missing.**
- [ ] **Step 3: Implement**, moving both sessions onto the base; every existing test of either session passes unchanged.
- [ ] **Step 4: `git grep -n 'Value!' src/SqlSource.Tool` prints nothing.**
- [ ] **Step 5: Run `tests/SqlSource.Tool.Tests` and both container projects, then `./pre-commit-validation.sh`.**
- [ ] **Step 6: Commit** `Open and ask through one base of the describers' sessions (TD-0073 items 1 and 12)`.

### Task 9: `PostgresLive` answers through one `AnswerAsync` (item 3)

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs`, `src/SqlSource.Tool/AGENTS.md` (the `live` rule of PostgreSQL names `PostgresLive.AnswerAsync`, as SQL Server's names its own), `docs/architecture/postgres-describer.md` (the connection section, where it says how a `live` function answers)

**Interfaces:**
- Produces: `internal static async Task<TAnswer> AnswerAsync<TAnswer>(Func<Task<TAnswer>> call, Func<NpgsqlException, PostgresServerError> errorOf, Func<PostgresServerError, TAnswer> failed)`: `call`'s answer, or `failed(errorOf(exception))` for an `NpgsqlException`.  It has no cancellation filter and no other catch: today an `NpgsqlException` after a cancellation is an answer's error, and that stays.
- The twelve methods move onto it.  `OpenAsync` passes `ConnectionErrors.AtOpen`, every other `ConnectionErrors.Of`.  `CheckAsync` keeps its `catch (Exception refused) when (IsDriverRefusal(refused))` inside its `call`, and `RefusedAsync` answers through `AnswerAsync` too; both `failed`s are `error => new PostgresCheckAnswer(null, error, null, null, false)`, `TextRefused` `false` as today.

- [ ] **Step 1: Move the methods**, one at a time; the answers each gives are unchanged, so the tests of `tests/SqlSource.Tool.Tests/Describing/Postgres/` and the container tests of `tests/SqlSource.Tests.Postgres/Describer/` are the test.  Add `PostgresLiveTests.AnswerAsync_CallThatThrowsAnNpgsqlException_IsTheFailedAnswer` and `AnswerAsync_CallThatIsCancelled_Throws` (an `OperationCanceledException` passes) in `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresLiveTests.cs`.
- [ ] **Step 2: `git grep -c 'catch (NpgsqlException' src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs` prints 1.**
- [ ] **Step 3: Run `tests/SqlSource.Tool.Tests` and `tests/SqlSource.Tests.Postgres`, then `./pre-commit-validation.sh`.**
- [ ] **Step 4: Commit** `Answer every live call of PostgreSQL through one AnswerAsync (TD-0073 item 3)`.

### Task 10: One builder of the shared event members, and `tableMatch` in PostgreSQL's event (item 4)

**Files:**
- Create: `src/SqlSource.Tool/Describing/Describers/DescriberEvents.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresEvents.cs`, `src/SqlSource.Tool/Describing/SqlServer/SqlServerEvents.cs`, `docs/architecture/sqlserver-describer.md` (its events table, if it lists PostgreSQL's), the remarks of `PostgresEvents` where they list `table-match`'s members
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresEventsColumnTests.cs`, `PostgresEventsRelationTests.cs`, `tests/SqlSource.Tool.Tests/Describing/RedactionTests.cs`

**Interfaces:**
- Produces: `internal static class DescriberEvents`:
  - `public static LogValue TableOf(string? schema, string? table)`: `MapOf(schema, table)`, the shape of every relation the two logs write as `schema` and `table`.
  - `public static LogValue OriginOf(SidecarOrigin? origin)`: `MapOf(schema, table, column)`, or `LogValue.Null`.
  - `public static LogMember[] ColumnHead(string query, SidecarColumn column)`: `query`, `ordinal`, `name`, `type`, `origin`, the members both `column` events start with.
  - `public static LogMember[] TableMatchTail(SidecarTable? table, string tableMatch)`: `tableMatch` and `table`.
  - PostgreSQL's `column` is `[.. ColumnHead, nullable, nullableSource, gated]`; SQL Server's `[.. ColumnHead, noOrigin, nullable, nullableSource, identity, computed]`.  PostgreSQL's `table-match` is `[query, result, .. TableMatchTail]`; SQL Server's `[query, .. TableMatchTail]`.  The relation maps of `walk.node`, `RelationOf`, `PartitionOf` (its `ancestors` after the two) and SQL Server's `table` event use `TableOf` or its two members; `TableOf(PostgresTableRow)`, which writes `name` and not `table`, keeps its members.

- [ ] **Step 1: Change the tests first:** `PostgresEventsColumnTests`' table-match member list becomes `["query", "result", "tableMatch", "table"]` with `Member(match, "tableMatch")` equal to `Member(match, "result")`; the same assertion in `PostgresEventsRelationTests.DescribeAsync_QueryThatIsATable_WritesTheTableInTableMatch`; `RedactionTests` gains, for PostgreSQL's own `table-match` through a session's log, that `tableMatch` is redacted as `result` is.
- [ ] **Step 2: Run and see PostgreSQL's event lack `tableMatch`.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run `tests/SqlSource.Tool.Tests`, then `./pre-commit-validation.sh`.**
- [ ] **Step 5: Commit** `Build the shared members of the describers' events once, and name PostgreSQL's verdict tableMatch too (TD-0073 item 4)`.

### Task 11: One order of the table match's checks (item 11)

**Files:**
- Create: `src/SqlSource.Tool/Describing/Describers/TableMatchChecks.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/Columns/PostgresTableMatcher.cs`, `src/SqlSource.Tool/Describing/SqlServer/SqlServerTableMatcher.cs`, `src/SqlSource.Tool/AGENTS.md` (the two rules of the order name `TableMatchChecks`), `docs/architecture/postgres-describer.md` and `docs/architecture/sqlserver-describer.md` (matching a table)
- Test: `tests/SqlSource.Tool.Tests/Describing/Describers/TableMatchChecksTests.cs` (new)

**Interfaces:**
- Produces: `internal static class TableMatchChecks`:
  - `public static IReadOnlyList<string> Order { get; }`: `NoOrigin`, `SeveralTables`, `NamesDiffer`, `ColumnsDiffer`, `OrderDiffers`, `NullabilityDiffers` of `SidecarValues.TableMatch`, the format's order.
  - `public static string? OfTheOrigins(Func<bool> everyColumnHasAnOrigin, Func<bool> oneTable)`: the first of the two that fails, or null.
  - `public static string OfTheTable(Func<bool> sameNames, Func<bool> sameColumns, Func<bool> sameOrder, Func<bool> sameNullability)`: the first of the four that fails, or `Matched`.
  - Each delegate runs only when every check before it passed, so a check may assume what the ones before it proved.  SQL Server's session still asks `table` only when `OfTheOrigins` is null (through `TableOf`).  Both matchers keep their signatures, their throws and their own predicates, and each builds its answer from the two calls.

- [ ] **Step 1: Write the failing tests:** `Order_IsTheFormatsOrder` (the six constants in that order); `OfTheOrigins_BothFail_IsNoOrigin`; `OfTheOrigins_SecondFails_IsSeveralTables` ; `OfTheOrigins_BothPass_IsNull`; `OfTheTable_EveryCheckFails_IsNamesDiffer`; `OfTheTable_LastFails_IsNullabilityDiffers`; `OfTheTable_EveryCheckPasses_IsMatched`; `OfTheTable_CheckThatFails_RunsNoCheckAfterIt` (a later delegate that throws is never called).  Add to `SqlServerTableMatcherTests.Match_TwoChecksFail_GivesTheFirst` the case it lacks, several tables over names that differ.
- [ ] **Step 2: Run and see `TableMatchChecks` missing.**
- [ ] **Step 3: Implement**, moving both matchers onto it; both matchers' tests pass unchanged.
- [ ] **Step 4: Run `tests/SqlSource.Tool.Tests`, then `./pre-commit-validation.sh`.**
- [ ] **Step 5: Commit** `Hold the table match's checks in one order (TD-0073 item 11)`.

### Task 12: The errors both describers build (item 2)

**Files:**
- Create: `src/SqlSource.Tool/Describing/Describers/DescribeFailures.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs`, `src/SqlSource.Tool/Describing/SqlServer/SqlServerErrors.cs`, `src/SqlSource.Tool/Describing/SqlServer/DeclaredTypeText.cs`
- Test: every test that names a constant that moves (`git grep -n -e 'Errors.NoSuchType\b' -e 'NotATypeName\b' tests`), and `tests/SqlSource.Tool.Tests/Describing/Describers/DescribeFailuresTests.cs` (new)

**Interfaces:**
- Produces: `internal static class DescribeFailures`:
  - `public const string NoSuchType = "the database has no such type";` and `public const string NotATypeName = "it is not written as a type name";`, the two texts the engines share; `PostgresErrors.NoSuchType`, `PostgresErrors.NotATypeName`, `SqlServerErrors.NoSuchType` and `DeclaredTypeText.NotATypeName` go, and their users read these.
  - `public static DescribeFailure Open(string database, EquatableArray<string> serverLines, string help)`: `SQLSRC224`.
  - `public static DescribeFailure VersionNotSupported(string database, string engine, string version, string minimum, string help)`: `SQLSRC235`, its arguments `[database, engine, version, minimum]`.
  - `public static DescribeFailure DeclaredType(DescribeRequest query, string writtenParameter, string? type, string reason, DescribeStep step, EquatableArray<string> serverLines, string help)`: `SQLSRC228`, its arguments `[type ?? "", writtenParameter, query.Name, reason]`.
  - Each engine keeps its own help texts, its `Engine`, its minimum's spelling (`"14"` and `"14 (SQL Server 2017)"`), PostgreSQL's case of `Multiplexing=true` at `open` and its server lines, and calls these.

- [ ] **Step 1: Write `DescribeFailuresTests`** for the three builders' descriptor, arguments, step, lines and help.  Every existing test of either engine's errors passes with only the constants' type changed.
- [ ] **Step 2: Run and see `DescribeFailures` missing.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run `tests/SqlSource.Tool.Tests`, then `./pre-commit-validation.sh`.**
- [ ] **Step 5: Commit** `Build the errors both describers report in one place (TD-0073 item 2)`.

### Task 13: The describers' small copies (item 13, tool side)

**Files:**
- Create: `src/SqlSource.Tool/Describing/Describers/ExceptionChain.cs`, `src/SqlSource.Tool/Describing/Describers/DescriberSql.cs`
- Delete: `src/SqlSource.Tool/Describing/Postgres/Statements/PostgresSql.cs`, `src/SqlSource.Tool/Describing/SqlServer/SqlServerSql.cs`, and their tests `tests/SqlSource.Tool.Tests/Describing/Postgres/Statements/PostgresSqlTests.cs` and `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerSqlTests.cs`, whose cases move
- Modify: both `ConnectionErrors.cs`, `OuterJoinGate.cs`, `StatementShape.cs`, `ParameterPlaceholders.cs`, `ParameterCopies.cs`, `src/SqlSource.Tool/Describing/Postgres/Statements/DeclaredTypeText.cs`, `PostgresErrors.cs` (`TypeRefused` takes the reason), `PostgresSession.cs`, `src/SqlSource.Tool/AGENTS.md` and `docs/architecture/*-describer.md` where they name what moves
- Test: `tests/SqlSource.Tool.Tests/Describing/Describers/ExceptionChainTests.cs`, `DescriberSqlTests.cs` (new), `tests/SqlSource.Tool.Tests/Describing/Postgres/Statements/DeclaredTypeTextTests.cs`, `ParameterPlaceholdersTests.cs`

**Interfaces:**
- Produces:
  - `ExceptionChain.Find<T>(Exception exception) where T : Exception → T?`: the first exception of type `T` in the chain of inner exceptions, the exception itself first; both `InChain<T>` go.
  - `DescriberSql.Lex(string sql, SqlDialectRules rules) → EquatableArray<SqlLexeme>`, throwing `InvalidOperationException("The sample SQL of a query does not lex, and the plan's parser read it.")` as both do today; `PostgresSql` and `SqlServerSql` go.  `UncastParameters` keeps its own `SqlLexer.Lex`, which must not throw.
  - PostgreSQL's `DeclaredTypeText.Read(string text) → Outcome<string, string>`: the type without the spaces around it, or `DescribeFailures.NotATypeName`; `TryRead` goes.  `ParameterPlaceholders.TypeList` fails with `(SqlQueryParameter Parameter, string Reason)`, and `PostgresErrors.TypeRefused(DescribeRequest query, SqlQueryParameter parameter, string reason)` passes the reason, so `SQLSRC228`'s text is unchanged.

- [ ] **Step 1: Write the failing tests:** `ExceptionChainTests.Find_TypeOfAnInnerException_IsThatException`, `Find_TypeOfTheException_IsTheException`, `Find_TypeOfNone_IsNull`; `DescriberSqlTests` with the moved cases of both old test classes, one for each dialect; PostgreSQL's `DeclaredTypeTextTests` asserting `Read(...).Value` and `Read(...).Failure.ShouldBe(DescribeFailures.NotATypeName)`.
- [ ] **Step 2: Run and see the new members missing.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run `tests/SqlSource.Tool.Tests`, then `./pre-commit-validation.sh`.**
- [ ] **Step 5: Commit** `Share the describers' small copies (TD-0073 item 13)`.

### Task 14: Split `PlanExpression` (item 14, tool side)

**Files:**
- Create: `src/SqlSource.Tool/Describing/Postgres/Walk/PlanTokens.cs`, `PlanExpression.Nullability.cs`, `PlanExpression.Proofs.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/Walk/PlanExpression.cs`, `PlanPartitions.cs`, `src/SqlSource.Tool/AGENTS.md` (the rule that lists `PlanExpression`'s files), `docs/architecture/postgres-nullability.md` (the paragraph that links the files and names the tokenizer)

**Interfaces:**
- Produces:
  - `internal static class PlanTokens` with `Read(string) → List<Token>?` and its nested `Kind` and `Token`, today's `PlanExpression.Tokens`, `PlanExpression.Kind` and `PlanExpression.Token`, unchanged; `PlanPartitions` names `PlanTokens`.
  - `PlanExpression.cs` keeps the class's remarks, its tables, its entry points, `MayNameColumn` and `Groups`, and the `Reader`'s state, constructor, span helpers and the grammar of a test that both jobs read (`IsTest` to `Primary`, `Constant`, `IsCountOrRanking`, `IsTypeName`, `IsModifier`, `IsLowerWord`, `Next`).
  - `PlanExpression.Nullability.cs`: the reader of nullability forms, `Form`, `IsVariadicCall`, `AnyArgumentIsNotNull`, `IsNotNull`, `IsCase`, `CaseResultsAreNotNull`.
  - `PlanExpression.Proofs.cs`: the proofs of types, `Proved`, `TypeOid`, `BuiltInType` and `TypesByName`.  `PlanExpression.Forms.cs`, the catalog forms that the proofs read, stays, and its header comment says so.
  - Each part opens with a comment of what it holds, as `.Forms.cs` does; no member changes.

- [ ] **Step 1: Move the members**; `PlanExpressionTests` and the walk's tests pass unchanged.
- [ ] **Step 2: Check `wc -l` of each `PlanExpression*.cs` is below 550.**
- [ ] **Step 3: Run `tests/SqlSource.Tool.Tests` and `tests/SqlSource.Tests.Postgres`, then `./pre-commit-validation.sh`.**
- [ ] **Step 4: Commit** `Split PlanExpression into its tokenizer, its forms and its proofs (TD-0073 item 14)`.

### Task 15: Resolve the item

**Files:**
- Delete: `docs/tech-debt/TD-0073-duplicated-and-uneven-patterns-in-src.md`
- Modify: `docs/tech-debt/README.md` (its row goes)

- [ ] **Step 1: Delete the item and its row; `git grep -n 'TD-0073' -- ':!docs/superpowers'` prints nothing.**
- [ ] **Step 2: Run the two container projects on their floor images (Global Constraints), then `./pre-commit-validation.sh`.**
- [ ] **Step 3: Commit** `Resolve TD-0073`.
