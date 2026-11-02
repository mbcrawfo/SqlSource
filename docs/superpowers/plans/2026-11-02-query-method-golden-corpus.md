# Query-method golden corpus Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A permanent, checked-in golden-output corpus that runs the whole generator over `.sql` files and sidecars, records every generated file and every diagnostic of each run, and proves that PRs 2 to 6 of the query-method emission refactor leave the output byte-identical.

**Architecture:** One xunit theory in `tests/SqlSource.Tests/Generator/` (so RoslynFloor runs it too) over the corpus's cases.  A case is an engine (a row of `CorpusEngine`) and a scenario; the shared scenarios are written once against the engine row, so a new engine is new rows.  Each case runs `GeneratorHarness.Run` at `CodeGen`, renders the run to text with `GoldenFiles.Render`, and compares it with `tests/SqlSource.Tests/Generator/Golden/<engine>/<scenario>.txt`; only `SQLSOURCE_UPDATE_GOLDEN=1` writes a golden file.

**Tech Stack:** C#, xunit v3 (4.0.1) with Microsoft.Testing.Platform, Shouldly, Roslyn 4.14 floor, the existing harness in `tests/SqlSource.Testing/Generator/`.

**Spec:** `docs/superpowers/specs/2026-10-30-query-method-emission-refactor-design.md`, section "PR 1: the golden-output corpus".

## Where the code differs from the spec

- **`SidecarFaker` is not used.**  It is a Bogus-based random faker in `tests/SqlSource.Tests/Snapshot/`, for the sidecar round trip; RoslynFloor cannot reference Bogus, and random sidecars cannot give stable golden files.  The corpus builds its entries with the deterministic helpers the method tests use: `ModelRuns.Entry`, `MethodRuns.SqlServerEntry`, `Sidecars.Of` and `TestSidecars`.
- **The run goes through `GeneratorHarness.Run`, not `MethodRuns.Run`**, because cases need several sources, several `.sql` files and the `SqlSourcePublicExceptions` property, which `MethodRuns.Run` does not take.  It uses `MethodRuns`' and `ModelRuns`' entry builders and sources.
- **There is no `UseArrayPool` or `ValidateTokens` setting key.**  Both are generator parameters: `no-array-pool` and `no-token-validation`, given as `-- generator: <word>` on a query or as `"generatorParameters"` in settings.

## Global Constraints

- **This PR changes no product code.**  Nothing under `src/` changes but the stale `LocalNames` line of `src/SqlSource/Generation/Emitting/AGENTS.md`.
- Golden files are written only when the environment variable `SQLSOURCE_UPDATE_GOLDEN` is `1`; a run without it never writes, creates or deletes one.
- Every file in `tests/SqlSource.Tests/Generator/` starts with `// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.`, ends in `Tests.cs`, uses only Roslyn 4.14.0 API and the packages RoslynFloor references (no Bogus, no JsonSchema.Net), and hands the compiler `LanguageVersion.CSharp12` (never `Latest`).
- Helpers go in `tests/SqlSource.Testing/Generator/`, `internal`, within Roslyn 4.14.0; test classes are `public sealed`; names are `Method_Scenario_Outcome`; assertions are Shouldly's.
- Every test run of the generator passes an `OutputKind` (`OutputKind.CodeGen` here), named through `using OutputKind = SqlSource.Settings.OutputKind;`.
- A test reads and writes the repository only through `RepositoryRoot.Path`.
- Golden files use `\n` line endings in every checkout (`.gitattributes`), and the reader also turns `\r\n` into `\n` before comparing.
- Before each commit: `./format.sh -- <changed .cs files>` then `./pre-commit-validation.sh` must pass (root `AGENTS.md`).
- Every golden file recorded is read by the person who records it, who confirms it shows what its case is for (the spec's anti-goal: no golden file regenerated without anyone reading it).

## Review Focus

- **A Windows checkout:** CRLF golden files must still compare equal → `.gitattributes` `eol=lf` and the reader's `\r\n` → `\n`; tested in Task 1 by `Check_GoldenWithCrlf_Passes`.
- **Paths of compiler diagnostics on Windows use `\`** → `Render` writes `/` for every path separator in the three diagnostic lists; tested by `Render_DiagnosticWithBackslashPath_WritesSlashes`.
- **A golden file with no case (a renamed or removed case)** → the leftover test fails, and the opt-in deletes it; tested by `GoldenFolder_EveryFile_IsACase`.
- **A missing golden file** without the opt-in → the test fails, naming the variable, and writes nothing; tested by `Check_MissingGoldenWithoutOptIn_FailsAndWritesNothing`.
- **A generated file that does not end in `\n`** must not render the same as one that does → `Render` marks it with `\ No newline at end of file`; tested by `Render_FileWithoutFinalNewline_MarksIt`.

---

## File structure

- Create `tests/SqlSource.Testing/Generator/GoldenFiles.cs`: rendering a run to text, checking it against its golden file, updating under the opt-in, listing leftovers.
- Create `tests/SqlSource.Testing/Generator/CorpusEngine.cs`: one row per engine (PostgreSQL, SQL Server): name, dialect, driver references, entry builder, type vocabulary, the scenarios only it has.
- Create `tests/SqlSource.Testing/Generator/QueryMethodCorpus.cs`: the shared scenarios and the list of cases (`engine × shared scenarios` plus each engine's own).
- Create `tests/SqlSource.Testing/Generator/CorpusScenarios*.cs` as needed when `QueryMethodCorpus.cs` passes ~600 lines: split by theme (`CorpusLibraryScenarios.cs`, `CorpusBindingScenarios.cs`), each a `static partial class QueryMethodCorpus` part or a static class the engine rows call.
- Create `tests/SqlSource.Tests/Generator/QueryMethodCorpusTests.cs`: the theory and the leftover test.
- Create `tests/SqlSource.Tests/Generator/GoldenFilesTests.cs`: unit tests of `GoldenFiles` that run no generator.
- Create `tests/SqlSource.Tests/Generator/Golden/<engine>/<scenario>.txt`: the golden files.
- Modify `.gitattributes`, `.editorconfig`, `tests/README.md`, `tests/SqlSource.Tests/Generator/AGENTS.md`, `src/SqlSource/Generation/Emitting/AGENTS.md`, `CONTRIBUTING.md`.

---

### Task 1: Golden mechanism, engine rows and the `shapes` scenario

**Files:**
- Create: `tests/SqlSource.Testing/Generator/GoldenFiles.cs`, `CorpusEngine.cs`, `QueryMethodCorpus.cs`
- Create: `tests/SqlSource.Tests/Generator/GoldenFilesTests.cs`, `QueryMethodCorpusTests.cs`
- Create: `tests/SqlSource.Tests/Generator/Golden/postgres/shapes.txt`, `Golden/sqlserver/shapes.txt`
- Modify: `.gitattributes`, `.editorconfig`, `tests/README.md`, `tests/SqlSource.Tests/Generator/AGENTS.md`, `src/SqlSource/Generation/Emitting/AGENTS.md`, `CONTRIBUTING.md`

**Interfaces:**
- Produces:
  - `internal static class GoldenFiles`
    - `public const string UpdateVariable = "SQLSOURCE_UPDATE_GOLDEN";`
    - `public static bool IsUpdating { get; }`: the variable is `1`.
    - `public static string Folder { get; }` = `RepositoryRoot.Path("tests", "SqlSource.Tests", "Generator", "Golden")`.
    - `public static string Render(string caseName, GeneratorRun run)`
    - `public static void Check(string caseName, string actual)`: compares with `<Folder>/<caseName>.txt`, or writes it under the opt-in.
    - `internal static void Check(string path, string actual, bool updating)`: the same on any path, for the unit tests.
    - `public static IReadOnlyList<string> Leftovers(IEnumerable<string> caseNames)`: the case names of the `.txt` files below `Folder` that no case has, `/`-separated, sorted ordinally.
  - `internal sealed record CorpusEngine(...)` with at least:
    - `string Name` (`"postgres"`, `"sqlserver"`), `string Dialect` (`"postgres"`, `"mssql"`), `MetadataReference[] Driver` (`[NpgsqlReferences.Npgsql10]`, `[SqlClientReferences.SqlClient7]`);
    - `SidecarType Int`, `SidecarType Text` (`ModelRuns.Int4` / `ModelRuns.Text`; `TestSidecars.SqlServer()` / `TestSidecars.SqlServer("nvarchar(100)", 200, 0)`), and `string IntOverrideKey` (`"int4"` / `"int"`) for `-- type-override:`;
    - `SidecarEntry Entry(SqlFile file, string name, SidecarParameter[] parameters, SidecarColumn[]? columns)` (delegates to `ModelRuns.Entry` / `MethodRuns.SqlServerEntry`);
    - `SidecarParameter Parameter(string name, SidecarType type, bool? nullable = null)` and `SidecarColumn Column(string name, SidecarType type, bool nullable = false)`;
    - `GeneratorRun Run(SourceFile[] sources, SqlFile[] files, MetadataReference[]? references = null, string? settings = null, IReadOnlyDictionary<string, string?>? properties = null)`: `GeneratorHarness.Run(OutputKind.CodeGen, sources, files, references: references ?? Driver, dialect: Dialect, settings: settings, properties: properties)`, at `LanguageVersion.CSharp12`;
    - `public static readonly CorpusEngine Postgres, SqlServer;` and `public static IReadOnlyList<CorpusEngine> All` (in that order).
    - `IReadOnlyList<CorpusScenario> OwnScenarios`: the scenarios only this engine has (empty in this task).
  - `internal sealed record CorpusScenario(string Name, Func<CorpusEngine, GeneratorRun> Run);`
  - `internal static class QueryMethodCorpus`
    - `public static IReadOnlyList<CorpusScenario> Shared`: the scenarios every engine runs (`shapes` in this task).
    - `public static IReadOnlyList<(string Name, CorpusEngine Engine, CorpusScenario Scenario)> Cases`: for each engine of `CorpusEngine.All`, `Shared` then `OwnScenarios`, named `<engine>/<scenario>`; names are unique (a duplicate throws).
    - `public static GeneratorRun RunCase(string name)`.

**The golden format** (exact; `Render` writes exactly this):

```
// The golden output of the query-method corpus case <caseName>.  Written by SQLSOURCE_UPDATE_GOLDEN=1: see "The golden corpus of query methods" in tests/README.md.

==== Generator diagnostics
<each of run.Diagnostics on its own line, in order, or "(none)">

==== Compilation errors
<each of run.CompilationErrors, or "(none)">

==== Warnings in generated code
<each of run.GeneratedCodeWarnings, or "(none)">

==== File: <hint name>
<the file's text verbatim>
```

- Files sorted by hint name, `StringComparer.Ordinal`; each `==== File:` section is preceded by one blank line.
- A file's text is written as it is; when it does not end in `\n`, the line `\ No newline at end of file` follows it.
- In the three diagnostic lists, every `\` is written as `/`.

- [ ] **Step 1: Write the failing unit tests in `GoldenFilesTests.cs`.**  They run no generator: they call the overload `internal static string Render(string caseName, IReadOnlyDictionary<string, string> sources, IReadOnlyList<string> diagnostics, IReadOnlyList<string> errors, IReadOnlyList<string> warnings)`, which the public `Render` calls with the run's lists.
  - `Render_RunWithFilesAndDiagnostics_WritesTheSectionsInOrder`: sources `{"b.g.cs": "B\n", "a.g.cs": "A\n"}`, diagnostics `["SQLSRC275 /app/Repo/Users.sql(1,10)-(1,14): m"]`, no errors or warnings → exactly the header line, blank, `==== Generator diagnostics`, the diagnostic, blank, `==== Compilation errors`, `(none)`, blank, `==== Warnings in generated code`, `(none)`, blank, `==== File: a.g.cs`, `A`, blank, `==== File: b.g.cs`, `B`, ending in `\n`.
  - `Render_FileWithoutFinalNewline_MarksIt`: `{"a.g.cs": "A"}` → the section is `==== File: a.g.cs\nA\n\\ No newline at end of file\n`.
  - `Render_DiagnosticWithBackslashPath_WritesSlashes`: error `CS0001 SqlSource\\Gen\\a.g.cs(1,1)-(1,2): x` → written `CS0001 SqlSource/Gen/a.g.cs(1,1)-(1,2): x`.
  - `Check_SameText_Passes`, `Check_GoldenWithCrlf_Passes` (file holds `x\r\ny\r\n`, actual `x\ny\n`).
  - `Check_DifferentText_FailsNamingTheFirstDifferentLine`: golden `a\nb\nc\n`, actual `a\nB\nc\n` → throws `ShouldAssertException` whose message contains `line 2`, `b`, `B` and `SQLSOURCE_UPDATE_GOLDEN`; the file is unchanged.
  - `Check_MissingGoldenWithoutOptIn_FailsAndWritesNothing`: message contains the path and `SQLSOURCE_UPDATE_GOLDEN`; the file still does not exist.
  - `Check_UpdatingWithDifferentText_WritesTheGolden`, `Check_UpdatingWithMissingFolder_CreatesIt`.
  - The `Check` tests use a temporary folder (`Directory.CreateTempSubdirectory`), deleted in a `finally`, through the `internal Check(path, actual, updating)` overload; they never touch `Golden/`.

- [ ] **Step 2: Run them to see them fail** — `dotnet test --project tests/SqlSource.Tests -- --filter-class "SqlSource.Tests.Generator.GoldenFilesTests"` → compile failure (no `GoldenFiles`).

- [ ] **Step 3: Implement `GoldenFiles`.**  `Check` reads with `File.ReadAllText` and `.Replace("\r\n", "\n")`; the mismatch message names the case or path, the first different line's number with both lines, and says: run the test with `SQLSOURCE_UPDATE_GOLDEN=1`, read the diff, and say in the PR why it changed.  Under the opt-in, write (UTF-8 without BOM, `\n`) only when the text differs, creating the folder.  `Leftovers` enumerates `*.txt` under `Folder` recursively (none when it does not exist).

- [ ] **Step 4: Run the unit tests** — same command → PASS.

- [ ] **Step 5: Write `QueryMethodCorpusTests.cs`:**
  - `public static TheoryData<string> CaseNames` (or `IEnumerable<TheoryDataRow<string>>`) from `QueryMethodCorpus.Cases`, each row carrying the trait `engine` = the engine's name (`new TheoryDataRow<string>(name).WithTrait("engine", engine.Name)`), so that a run can be filtered by engine.
  - `Run_Case_MatchesItsGoldenFile(string name)`: `GoldenFiles.Check(name, GoldenFiles.Render(name, QueryMethodCorpus.RunCase(name)))`.
  - `GoldenFolder_EveryFile_IsACase`: `Leftovers(case names)` is empty; under `GoldenFiles.IsUpdating` it deletes each leftover first (and then asserts empty).
- [ ] **Step 6: Write `CorpusEngine` and the `shapes` scenario.**  `shapes` runs `MethodRuns.Source` with one file `/app/Repo/Users.sql` (`GeneratorHarness.UsersPath`) holding, in this order:
  - `Touch -> none` (`UPDATE users SET seen = 1;`, no parameters, `columns: null`);
  - `Rename -> none` with `@id` (Int, not null) and `@name` (Text, nullable);
  - `CountUsers -> rowcount` with `@id`;
  - `GetUser -> one` with `@id`, columns `id` (Int), `name` (Text, nullable);
  - `FindUser -> one-optional` with `@id`, same columns;
  - `FirstUser -> one`, no parameters, same columns;
  - `ListUsers` (no arrow: many, default collection) with `@id`, same columns;
  - `ListAll -> many`, no parameters, same columns.
  The SQL is the same for both engines (`@name` parameters parse under both dialects).
- [ ] **Step 7: Record the two golden files** — `SQLSOURCE_UPDATE_GOLDEN=1 dotnet test --project tests/SqlSource.Tests -- --filter-class "SqlSource.Tests.Generator.QueryMethodCorpusTests"`, then without the variable → PASS.  Read both files: no diagnostics, no errors, no warnings; `App.Sample-Methods.g.cs` holds the eight methods; the SQL Server one holds `transaction.Connection is null` refusals, `catch (global::System.Exception exception)` around reads and `NextResultAsync` drains; the PostgreSQL one holds none of them.
- [ ] **Step 8: Run RoslynFloor** — `dotnet test --project tests/SqlSource.Tests.RoslynFloor -- --filter-class "SqlSource.Tests.Generator.QueryMethodCorpusTests"` → PASS against the same golden files.  If the output differs on Roslyn 4.14, stop and report it.
- [ ] **Step 9: Configuration and documents.**
  - `.gitattributes`: `# The golden corpus is compared byte for byte with what the generator writes, at LF` then `tests/SqlSource.Tests/Generator/Golden/** text eol=lf`.
  - `.editorconfig`: a section `[tests/SqlSource.Tests/Generator/Golden/**]` with `max_line_length = off` and a comment that the files are generated code compared byte for byte.  Run `./pre-commit-validation.sh`; if editorconfig-checker reports anything else in the golden files (trailing whitespace, indentation), turn off only that check for the folder, with the reason, and report it.
  - `tests/README.md`: a new section `## The golden corpus of query methods` after `## Reading the repository`: what it is for (the byte identity of the query-method refactor's PRs, and of any later change to what the generator writes), where the files are, what a file holds, how cases are named (`<engine>/<scenario>`, the engine a row of `CorpusEngine`, a new engine being new rows), how to run it (`dotnet test --project tests/SqlSource.Tests -- --filter-class "SqlSource.Tests.Generator.QueryMethodCorpusTests"`, and `--filter-trait "engine=postgres"`), how to update it (`SQLSOURCE_UPDATE_GOLDEN=1` on `tests/SqlSource.Tests` alone, never on the solution, since two projects would write the same files at once; then run without it; read every changed file's diff; a PR that changes a golden file says why in its description), and that a removed case's file is deleted by the same opt-in.  Also add `GoldenFiles`, `CorpusEngine` and `QueryMethodCorpus` to the description of `tests/SqlSource.Testing` in the table of "The projects", in the style of its other entries.
  - `tests/SqlSource.Tests/Generator/AGENTS.md`: add `GoldenFiles`, `CorpusEngine` and `QueryMethodCorpus` to the harness list; change "Only test classes live here." to say that besides the test classes the folder holds only `Golden/`, the corpus's golden files; and add a rule: **Golden files under `Golden/` are written only by `SQLSOURCE_UPDATE_GOLDEN=1`, never by hand; read each one's diff before committing it, and a PR that changes one says why** ([procedure](../../README.md#the-golden-corpus-of-query-methods)).  Keep each bullet under 80 words.
  - `src/SqlSource/Generation/Emitting/AGENTS.md`: `LocalNames` is in `LocalNames.cs`, held by `QueryMethodLocals` of `QueryMethodLocals.cs`.
  - `CONTRIBUTING.md`: after the coverage paragraph, one sentence and link: a change to what the generator writes updates the golden corpus, as [`tests/README.md`](tests/README.md#the-golden-corpus-of-query-methods) says.
- [ ] **Step 10: Validate and commit** — `./format.sh -- <the new .cs files>`, `./pre-commit-validation.sh` → passes; commit `test(corpus): add the golden-output corpus of query methods with its shapes`.

### Task 2: The shared scenarios

**Files:**
- Modify: `tests/SqlSource.Testing/Generator/QueryMethodCorpus.cs` (and a split file if it passes ~600 lines)
- Create: `Golden/<engine>/<scenario>.txt` for each scenario below, both engines

**Interfaces:**
- Consumes: `CorpusEngine`, `CorpusScenario`, `QueryMethodCorpus.Shared` from Task 1.
- Produces: the scenario names below, appended to `Shared` in this order.

Each scenario runs on both engines through `CorpusEngine.Run`, with the engine's `Int`, `Text` and `IntOverrideKey`.  Where an engine needs something else (a reference, a type), the scenario reads it from a new `CorpusEngine` member, named for what it gives.

- [ ] **Step 1: `collections`** — one many query per `CollectionKind` (`Array`, `IEnumerable`, `ICollection`, `IReadOnlyCollection`, `IList`, `IReadOnlyList`, `List`, `ImmutableArray`, `ImmutableList`, `IImmutableList`, `IAsyncEnumerable`), by `-- collection-type: <name>`, each with `@id` and columns `id`, `name` (nullable); plus a second `IAsyncEnumerable` query with no parameters.  The row buffer is on (the default).
- [ ] **Step 2: `collections-no-pool`** — the same file under settings `{"generatorParameters": ["no-array-pool"]}`.
- [ ] **Step 3: `tokens`** — queries with tokens, each with `@id`:
  - a many query with `-- token: {{table:users}}`; a one-optional, a rowcount and a none query with the same; an `IAsyncEnumerable` query with the same;
  - a one query and an `IAsyncEnumerable` query with a token named `await`;
  - a many query with two tokens (`{{schema:app}}` and `{{table:users}}`) and a query whose token has no `-- token:` marker;
  - an `IAsyncEnumerable` query and a one query with `-- generator: no-token-validation`;
  - one `IAsyncEnumerable` query and one one query whose tokens are named after the method's locals: `sql`, `command`, `reader`, `row`, `rows`, `wasClosed`, `exception`, `atRow`, `ReadRows`, `value`, `span`, `state`, so the generated locals take numbered names.
- [ ] **Step 4: `placement`** — several types, each claiming its own `.sql` file by `[SqlSourceGenerate(Path = "<file>.sql")]` with one `-> one` query and one `IAsyncEnumerable` query:
  - `public partial class App.Sample` (default: the public extension class);
  - `internal partial class App.InternalSample` (default: an internal extension class);
  - `App.PublicSample`, `App.InternalPlacement`, `App.PrivatePlacement` with `MethodLocation = MethodLocation.Public`, `.Internal`, `.Private`;
  - `App.Outer.Inner`, nested, with `MethodLocation.Public` (two levels of `partial`);
  - `GlobalSample` in no namespace (default placement).
- [ ] **Step 5: `converters`** — `-- type-override: <IntOverrideKey>=App.UserId` (a `readonly record struct UserId(int Value)` with `[SqlSourceConverter]` both ways, as `MethodRuns.KindsSource` does for long) and a text type overridden to `App.Email` (a class with converters, as `ConverterCases.AppSource`): parameters of each, nullable and not; columns of each, nullable and not; in a many, a one, a one-optional and an `IAsyncEnumerable` query.  Add `App.LegacyId` with `[System.Obsolete]` converters used by one column, so the methods' file has its `#pragma warning disable`.
- [ ] **Step 6: `diagnostics`** — the problems a method can meet, one file each where they would interfere:
  - SQLSRC275: `-- token: {{transaction:users}}` (as `MethodDiagnosticsTests`);
  - SQLSRC272: an `UPDATE` declared `-> one` with `columns: null`;
  - SQLSRC274: a query named `Open`, and a query named `SampleExtensions`;
  - two types sharing an extension class with one method name (as `MethodConflictTests.cs:54-80`), so that one method is dropped;
  - a user type named `SampleExtensions` in the source, so the extension class is blocked (find the exact setup in `AttributeConflictTests` or `MethodConflictTests`; if no such run exists, report it);
  - and one query that has no problem, so the file of methods is still written beside them.
- [ ] **Step 7: `public-exceptions`** — the `shapes` file under `properties: { ["SqlSourcePublicExceptions"] = "true" }` (`PublicExceptions.PropertyName`).
- [ ] **Step 8: Record and read** — record with the opt-in, run without it (both test projects); read each new golden file and confirm what its case is for: the numbered locals in `tokens`, `@await`, `ThrowIfNullOrWhiteSpace` present and absent; the seven type declarations in `placement`; the converter calls and `is { } value` patterns in `converters` with the pragma; every expected `SQLSRC` id in `diagnostics`; `public sealed class` in `SqlSource-Exceptions.g.cs` of `public-exceptions`.  Every scenario but `diagnostics` has no diagnostic, no error and no warning.
- [ ] **Step 9: Validate and commit** — format, `./pre-commit-validation.sh`; commit `test(corpus): add the shared scenarios`.

### Task 3: Library scenarios and the PostgreSQL driver

**Files:**
- Modify or create: the corpus files of Task 2; `CorpusEngine.cs`
- Create: the golden files below

**Interfaces:**
- Consumes: Task 1 and 2.
- Produces: shared scenarios `nodatime` and `json`; PostgreSQL's own scenarios `driver-npgsql8` and `driver-npgsql10`.

- [ ] **Step 1: `nodatime`** (shared; each engine gives its references and types through `CorpusEngine`):
  - PostgreSQL: references `[Npgsql10, LibraryReferences.NodaTime("3.3.7"), LibraryReferences.NpgsqlNodaTime("10.0.3")]`; a column and a parameter of `timestamptz` (`ModelRuns.Catalog("timestamptz") with { Name = "timestamp with time zone" }`) and of `date`, nullable and not, in a many, a one and an `IAsyncEnumerable` query.
  - SQL Server: references `[SqlClient7, LibraryReferences.NodaTime("3.3.7")]`, settings `{"libraryTypes": {"nodaTime": "on"}}`; columns and parameters of `date` `("date",3,10)`, `datetime2` `("datetime2",8,27,7)`, `time` `("time",5,16,7)` and `datetimeoffset` `("datetimeoffset",10,34,7)`, nullable and not, in the same three shapes.  The golden holds `SqlSource-NodaTimeConversions.g.cs`.
- [ ] **Step 2: `json`** (shared): `App.OrderMetadata` (class), `App.Tag` (record) and `App.Point` (struct), as `JsonSerializerMethodTests.cs:34-50`.
  - Both engines: a not-null column and a nullable column read whole as `App.OrderMetadata` (`-- column: meta -> App.OrderMetadata`), a parameter written whole (`-- param: @meta -> App.OrderMetadata`), a column of `System.Text.Json.Nodes.JsonObject`, not null, and a string column opted in by `-- column: notes -> json App.Tag`; in a many, a one and an `IAsyncEnumerable` query.
  - PostgreSQL also: a `jsonb[]` column and parameter by element as `App.Tag[]`, the column not null, so it throws on a JSON null element, and a nullable `App.Point?[]`.
  - Types: `Jsonb = ModelRuns.Catalog("jsonb") with { BindName = "jsonb" }`, `ModelRuns.ArrayOf(Jsonb, "_jsonb") with { BindName = "jsonb[]" }`; SQL Server `("json", -1, 0)` and `("nvarchar(max)", -1, 0)`.
- [ ] **Step 3: `driver-npgsql8` and `driver-npgsql10`** (PostgreSQL's own): one file under references `[NpgsqlReferences.Npgsql8]` and `[NpgsqlReferences.Npgsql10]`: `-- type-override: jsonb=System.Text.Json.Nodes.JsonNode` with a jsonb column and parameter, and `date` and `time` columns and parameters, nullable and not.  The 8 golden holds `SqlSource-JsonConversions.g.cs` and `DateTime`/`TimeSpan`; the 10 golden holds neither.
- [ ] **Step 4: Record, read, validate and commit** as in Task 2 (`test(corpus): add the library and driver scenarios`).

### Task 4: Each engine's own scenarios

**Files:**
- Modify or create: the corpus files; `CorpusEngine.cs` (`OwnScenarios` of each row)
- Create: the golden files below

**Interfaces:**
- Consumes: Tasks 1 to 3.
- Produces: PostgreSQL's `binding` and `by-element`; SQL Server's `binding`, `vector` and `spatial`.

- [ ] **Step 1: `postgres/binding`** — one `-> none` query per parameter type, each parameter nullable and not where the type allows: every row of `MethodRuns.ParameterKinds`; a parameter whose type has no `BindName`; a domain over a domain (`work_email` over `public.email`, `BindName = "text"`); an array of it (`BindName = "text[]"`); a type whose schema holds a dot and one whose kind is unknown (`kind: "shell"`), each given a CLR type by a `-- type-override:` if the type map refuses it.  Where a full run cannot reach a branch of `PostgresParameterBinder.DataTypeNameOf`, report which and why instead of forcing it.
- [ ] **Step 2: `postgres/by-element`** — `ConverterCases.AppSourceOf("class")` and `("struct")` setups as `ConverterArrayTests`: a `public.email[]` column read by element (reference element: `null!`), a nullable one, an `int8[]` read by element into `App.UserId[]` (value element: the "holds a null element" throw) and into `App.UserId?[]` (nullable element: `null`); the same arrays as parameters, nullable and not, including a not-null value-type element array (the direct call, no `is { } item`); in a many, a one, a one-optional and an `IAsyncEnumerable` query.
- [ ] **Step 3: `sqlserver/binding`** — one `-> none` query per parameter type, nullable and not, with the types of this table (`TestSidecars.SqlServer(name, maxLength, precision, scale, userType)`): `("char(10)",10,0)`, `("varchar(50)",50,0)`, `("nvarchar(max)",-1,0)`, `("nchar(5)",10,0)`, `("binary(4)",4,0)`, `("varbinary(max)",-1,0)`, `("decimal(10,2)",9,10,2)`, `("numeric(5,0)",5,5,0)`, `("datetime2(3)",7,23,3)`, `("json",-1,0)`, `("vector(3)",20,0)`, `int`, `("sql_variant",8016,0)`, an alias `("nvarchar(320)",640,0,0, new SqlServerUserType("dbo","Email",null))`, `("decimal(300,4)",17,300,4)` (bound by value), a `hierarchyid` CLR type with `-- type-override: hierarchyid=Microsoft.SqlServer.Types.SqlHierarchyId` and a stub of that type in the source, and a user CLR type `("Money",-1,0,0, new SqlServerUserType("dbo","Money","App.Types.Money, App.Types"))` with `-- type-override: dbo.Money=App.Types.Money` and a stub.  Add a `varchar(50)` parameter converted through `App.Email`, so the over-long check tests the converted value.  Where a full run refuses a type, report it rather than dropping it silently.
- [ ] **Step 4: `sqlserver/vector`** — references `[SqlClient7]`:
  - a not-null `vector(3)` column and a nullable one in a many, a one, a one-optional and an `IAsyncEnumerable` query;
  - a `vector(3)` parameter, nullable and not;
  - a column converted through `App.Embedding` (`-- type-override: vector=App.Embedding`, as `ConverterMethodTests.cs:618-668`), not null;
  - the name collision: a query with a token `value`, a parameter converted through `App.UserId` (`-- type-override: int=App.UserId`) and a not-null `vector(3)` column at index 1, so the locals are `value1` and `value11`; once as a one query and once as `IAsyncEnumerable`.
- [ ] **Step 5: `sqlserver/spatial`** — as `SpatialConversionTests`: references `[SqlClient7, LibraryReferences.NetTopologySuite("2.6.0"), LibraryReferences.SqlServerBytes("2.1.0")]`, settings `{"libraryTypes": {"netTopologySuite": "on"}}`, `-- type-override: geometry=NetTopologySuite.Geometries.Point`; a nullable `geography` and a not-null `geometry` (`SpatialConversionTests.SqlServerClr`), as columns and parameters, in a many, a one and an `IAsyncEnumerable` query.
- [ ] **Step 6: Record, read, validate and commit** as in Task 2 (`test(corpus): add each engine's own scenarios`).

### Task 5: Coverage audit

**Files:**
- Modify: the corpus files, only to add rows for branches no case reaches.

- [ ] **Step 1: Measure each engine apart.**  For `E` in `postgres` and `sqlserver`: `dotnet test --project tests/SqlSource.Tests --results-directory artifacts/corpus-$E -- --filter-class "SqlSource.Tests.Generator.QueryMethodCorpusTests" --filter-trait "engine=$E" --coverage --coverage-output-format cobertura`.
- [ ] **Step 2: List what each run misses** in `src/SqlSource/Generation/Emitting/QueryMethodWriter.cs`, `RowReaderWriter.cs`, `ParameterBinder.cs`, `PostgresParameterBinder.cs`, `SqlServerParameterBinder.cs`, `MethodEmitter.cs` and `QueryMethodLocals.cs`: lines with no hit and branches not taken, from the Cobertura XML, with a scratch script in the session's scratchpad (not committed).
- [ ] **Step 3: Close the gaps.**  Every line and branch must be hit by the union of both runs, and every one that does not depend on the engine must be hit by each run; the binders count for their own engine only.  Add rows to the scenarios for each gap.  What is left is a line no input reaches (an `or _` arm of an exhaustive switch, a guard for a sidecar edited by hand): list it with its reason.
- [ ] **Step 4: Record, read, validate and commit** (`test(corpus): reach every branch of the query-method writers`), and hand back the list of what is left uncovered with reasons, per engine.
