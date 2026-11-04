# Realistic corpus claimed once Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Each realistic case of both engines is claimed by one class, in `Realistic/Generation/RealisticMethods.cs`, and `RealisticQueries.cs` goes; the Dapper tests get a case's SQL from an in-process generator run at `Models` instead of a compiled constant.

**Architecture:** A new shared helper, `RealisticGeneration` in `tests/SqlSource.Tool.Testing/Realistic/`, runs the generator in process through `GeneratorHarness` on one case's `query.sql`, its committed sidecar, the project's settings file and `Cases/sqlsource.json`, at `Models`.  It reads the constant from the generated C# syntax.  `RealisticRefusalChecks`, which already runs the generator this way, moves onto the same helper.  The models and methods still come from the compiled `CodeGen` claim, which already generates the shared models.  The claims then set `Source` alone, the shape the attribute ends at after pull request 2.

**Tech Stack:** C#, Roslyn `GeneratorDriver` through `tests/SqlSource.Testing/Generator/GeneratorHarness.cs`, xUnit v3 + Shouldly, Dapper, Testcontainers (PostgreSQL, SQL Server).

**Spec:** `docs/superpowers/specs/2026-11-03-file-owned-settings-design.md`, section "Pull request B: the realistic corpus is claimed once".

## Global Constraints

- **No product code.**  Nothing under `src/`, `examples/`, `tools/`, `schemas/` or `docs/manual/` changes.  No `query.sql`, `case.json` or `query.sql.json` of either corpus changes, so `tools/describe-end-to-end.sh` is not run.  `git diff --stat origin/main -- '*/Realistic/Cases/*'` must print nothing.
- **Branch.**  The work is on the branch of its session's worktree, cut from `origin/main`.  It is independent of pull request A, lands on `main` after it and before pull request 1.  Commit this plan and the spec in that branch before the first task (`docs/superpowers/AGENTS.md`: plans travel with their work); pull request A commits the same spec, byte for byte.
- **The corpus keeps one query per case.**
- **Validation.**  Every commit passes `./pre-commit-validation.sh`, run after the last edit.  Before every push, run `./pre-commit-validation.sh --full` on the exact tree, then both container projects on their floor images:
  - `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/DatabaseTests/Postgres`
  - `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04 dotnet test --project tests/DatabaseTests/SqlServer`

  Both projects' realistic tests change, and the script runs only the default images.
- **Before the pull request.**
  - Run `branch-review:review` until it is clean, and again before each later push.
  - Run `git fetch --tags origin`.  There is no `v*` tag today, so `VersionPrefix` stays at `0.1.0`.
  - Bring the branch up to date with `origin/main` before pushing, and turn on auto-fix for the pull request (`CLAUDE.local.md`).
- **Test conventions** (`tests/AGENTS.md`):
  - Names are `Method_Scenario_Outcome` and assertions use Shouldly.
  - Test classes are `sealed`, and library types are `internal`.
  - Repository reads go through `RepositoryRoot.Path`; `RealisticCorpus`'s paths already do.
  - A run states its `OutputKind`: pass `OutputKind.Models` and never `null`.
- **AGENTS.md limits.**  At most 80 words per bullet and 8 KB per file, checked by `tests/SqlSource.Tests/Documentation/AgentsFileTests.cs`.  Keep the maintenance footers.
- **Records stay as written.**  Past specs and plans under `docs/superpowers/` are not edited.

## Decisions (answers to the brief)

- **Where the API lives.**  `tests/SqlSource.Tool.Testing/Realistic/RealisticGeneration.cs`.  `SqlSource.Tool.Testing` already references `tests/SqlSource.Testing` (see its `.csproj`), and `SqlSource.Testing` already gives it `InternalsVisibleTo`.
  - Both container projects already reference `SqlSource.Testing` and `SqlSource.Tool.Testing` (`tests/DatabaseTests/Postgres/Postgres.csproj`, `tests/DatabaseTests/SqlServer/SqlServer.csproj`).
  - **No `.csproj` changes.**
- **How the SQL text is read.**  Parse the generated syntax and do not compile and reflect.
  - `TypeEmitter.Write` (`src/SqlSource/Generation/Emitting/TypeEmitter.cs`) writes each constant as `public const string <Name> = <SymbolDisplay.FormatLiteral(sql)>;`.
  - `LiteralExpressionSyntax.Token.ValueText` gives that string back exactly.
  - Emitting and loading 55 assemblies a run would be slower and would leak loaded assemblies into the test process.
- **What the run is fed, so the SQL matches the build.**  This follows the precedent of `RealisticRefusalChecks.cs`:
  - the case's `query.sql` verbatim, so its own header markers such as `-- generator:` apply;
  - the case's committed `query.sql.json`, unchanged;
  - `corpus.CasesSettingsFile` as a settings file in the folder above the query, which carries the `database: realistic` setting and the type override;
  - `corpus.ProjectSettingsFile`, given whole as the harness's project file.  That file names the dialect, and its `generatorParameters`, such as `keep-comments`, reach the run.
- **How the output is set.**  The run passes `OutputKind.Models`, and the project file's own `output` key (`CodeGen`) is removed before it is given as `settings:`.
  - `GeneratorHarness.ProjectSettings` writes `output` first and then lets the entries of `settings` replace keys, so `CodeGen` would otherwise win.
  - The attribute's `Output` is not used, because pull request 1 removes it.
  - `RepositorySettingsFileTests` and `RealisticCorpusTests.SettingsFiles_OfEachCorpus_AreTheProjectsAndTheCasesFolders` already pin that these two files are the whole chain.
- **References.**  `SqlOf` passes none and relies on `GeneratorHarness`'s runtime references.  Those are the test process's trusted platform assemblies, which include Npgsql or Microsoft.Data.SqlClient, as a build references them.  `RealisticRefusalChecks` keeps passing the references it passes today.
- **Diagnostics.**
  - `SqlOf` fails, naming the case and every diagnostic, when the run reports anything other than `SQLSRC240` (`SqlDiagnostics.SidecarToolVersionDiffers`).  `SQLSRC240` is left out because the projects list it in `WarningsNotAsErrors` (`Postgres.csproj`).
  - Compiler errors in the in-process output are not asserted.  The build's compilation of the `CodeGen` claim is the check of the generated code.
- **Caching: none.**
  - `RealisticDapperTests` calls `SqlOf` once per case, before its loop over samples, and nothing else calls it.  That makes one run per case, about 55 per project per test run.
  - Task 2 measures the class's time.  If it grows by more than 30 s, report the figure in the pull request description; do not add a cache in this pull request.
- **What replaces `RealisticClaims.ModelsOutput`: nothing.**
  - `RealisticClaim` becomes `(TypeName, Case)` and `RealisticClaims` reads `corpus.Methods` alone.
  - Task 3 drops `Output = GeneratorOutput.CodeGen` from every claim.  The project's `sqlsource.json` already says `"output": "CodeGen"`, `Cases/sqlsource.json` sets no output, and no case has an `-- output:` marker.  The parser then refuses any attribute argument other than `Source`, naming the class.
  - That `CodeGen` stays in force is pinned by `RealisticModelsTests.MethodOf_Entry_IsTheMethodOfItsCodeGenClaim`, which needs no server, and by `RealisticMethodTests`.
  - With this, pull request 1 does not touch the realistic corpus.

## Review Focus

- **A `VersionPrefix` raise before the sidecars are written again.**  The generator gives `SQLSRC240`, and the build keeps it a warning.  Expected: `SqlOf` still returns the SQL.  Test in Task 1: `ConstantOf_SidecarOfAnotherToolVersion_IsStillTheSql`.
- **A `query.sql` edited without describing it again.**  The entry's hash is stale.  Expected: `SqlOf` fails with a message that names `realistic/<case>` and the diagnostic, and never sends SQL from a stale entry.  Test in Task 1: `ConstantOf_RunWithADiagnostic_ThrowsNamingTheCaseAndTheDiagnostic`.
- **A case whose query gets no constant**, because it has a token and so gets a method, or because its name differs from the entry's.  Expected: a failure that names the case and the query name.  Test in Task 1: `ConstantOf_NameWithoutAConstant_ThrowsNamingTheCaseAndTheName`.
- **The project's own `output: CodeGen` leaking into the run.**  At `CodeGen` the query would have no constant.  Expected: the run's project file says `Models` and keeps the project's other keys.  Test in Task 1: `RunAtModels_Case_GivesTheProjectsSettingsAtModels`.
- **A claim copied from the old file, with `SqlLocation` or `Output`.**  Expected: `RealisticClaims.Parse` throws, naming the class and the argument.  Test in Task 3: `Parse_ClaimWithAnArgumentOtherThanSource_ThrowsNamingTheClassAndTheArgument`.

---

### Task 1: `RealisticGeneration`, the SQL of a case from an in-process run

`RealisticGeneration` is added, `RealisticRefusalChecks` moves onto it, and a temporary parity test in each engine's project proves that `SqlOf` equals today's compiled `Models` constant for every case.

**Files:**
- Create: `tests/SqlSource.Tool.Testing/Realistic/RealisticGeneration.cs`
- Modify: `tests/SqlSource.Tool.Testing/Realistic/RealisticRefusalChecks.cs`.  Drop its private `CasesSettingsPath`, `QueryPath` and `Source` and its reading of the files, and call `RealisticGeneration`.  Its attribute loses `Output = GeneratorOutput.Models`.  Its class comment says the output is the run's `Models` and not the attribute's.
- Create: `tests/SqlSource.Tool.Tests/Realistic/RealisticGenerationTests.cs`
- Create (temporary, deleted in Task 2): `tests/DatabaseTests/Postgres/Realistic/Generation/RealisticSqlParityTests.cs` and `tests/DatabaseTests/SqlServer/Realistic/Generation/RealisticSqlParityTests.cs`
- Modify: `tests/README.md`
  - The bullet that lists the shared realistic helpers (`RealisticClaims`, `RealisticModels`): add `RealisticGeneration`, "which runs the generator in process on a case's query at `Models`, for the SQL of its run through Dapper and for the refusal of its models".  Find bullets by their text, not by line number.
  - The paragraph that names `GeneratorHarness` and `Sidecars`: they are what "`RealisticGeneration` of its `Realistic/`" runs the generator through.

**Interfaces:**
- Produces, all `internal static` in `SqlSource.Tool.Testing.Realistic.RealisticGeneration`:
  - `const string TypeFile = "App.Realistic.g.cs"`: the generated file of the type that claims the case.
  - `SqlFile QueryOf(RealisticCase found)`: the text of `found.AsFixture.QueryPath` at `/app/Repo/Cases/Case/query.sql`.
  - `SqlFile CommittedSidecarOf(RealisticCase found, SqlFile query)`: `new SqlFile(query.Path + ".json", File.ReadAllText(found.SidecarPath))`.
  - `GeneratorRun RunAtModels(RealisticCorpus corpus, SqlFile query, SqlFile sidecar, IReadOnlyList<MetadataReference> references)`.  It calls `GeneratorHarness.Run(OutputKind.Models, [new SourceFile(GeneratorHarness.SourcePath, Source)], [query, sidecar, cases], references: [.. references], settings: <project file without "output">)`.
    - `Source` is `using SqlSource;\n\nnamespace App { [SqlSourceGenerate(Source = "Cases/Case/query.sql")] internal static partial class Realistic; }`.  Without the `using`, the attribute does not bind and the run claims nothing.
    - `cases` is `GeneratorHarness.SettingsFile("/app/Repo/Cases/sqlsource.json", <CasesSettingsFile text>)`.
    - Parse the project file with `JsonNode.Parse` and `JsonDocumentOptions { CommentHandling = Skip, AllowTrailingCommas = true }`, `Remove("output")`, then write it with `GeneratorHarness.Json`.
  - `string ConstantOf(GeneratorRun run, RealisticCase found, string name)`.
    - It throws `InvalidOperationException` when `run.Result.Diagnostics` holds any id other than `SQLSRC240`.  The message is `"{found} gives diagnostics at Models: " + string.Join("; ", run.Diagnostics)`.
    - Otherwise it parses `run.Sources[TypeFile]`, finds the single `VariableDeclaratorSyntax` named `name` in a `const` `FieldDeclarationSyntax`, and returns its string literal's `Token.ValueText`.
    - When there is none, it throws `"{found} has no constant {name} at Models: a query with a token gets a method, and a name that is not its entry's gets nothing"`.
  - `string SqlOf(RealisticCorpus corpus, RealisticCase found, SidecarEntry entry)`.  It runs `ConstantOf(RunAtModels(corpus, QueryOf(found), CommittedSidecarOf(found, QueryOf(found)), []), found, entry.Name)`.
- Consumes: `GeneratorHarness`, `SqlFile`, `SourceFile`, `GeneratorRun` (`tests/SqlSource.Testing/Generator/`); `RealisticCorpus.ProjectSettingsFile` and `CasesSettingsFile`; `RealisticModels.EntryOf`.

- [ ] **Step 1: Write the failing unit tests** in `RealisticGenerationTests` (`public sealed class`, namespace `SqlSource.Tool.Tests.Realistic`).  They read the committed corpora and need no server.
  - `SqlOf_PostgresCase_IsItsQueryAsTheBuildWritesIt`: for `RealisticCorpus.Postgres`, case `table-statement`, the result is `"TABLE billing.invoices\nORDER BY issued_on DESC\nLIMIT @limit;"`.
  - `SqlOf_SqlServerCase_IsItsQueryAsTheBuildWritesIt`: for `RealisticCorpus.SqlServer`, case `customers-distinct-from`, the result is the query's lines after its `-- param:` line, joined by `\n` with no trailing newline: `"SELECT\n    c.id,\n    c.name,\n    c.email,\n    c.time_zone,\n    c.closed_at\nFROM dbo.customers AS c\nWHERE c.time_zone IS DISTINCT FROM @time_zone\nORDER BY c.name;"`.
  - `RunAtModels_Case_GivesTheProjectsSettingsAtModels`: for Postgres `table-statement`, parse `run.ProjectSettings`.  `output` is `"Models"`, `dialect` is `"postgres"`, `database` is `"app"`, and `inputModelSuffix` is `"Args"`.
  - `ConstantOf_SidecarOfAnotherToolVersion_IsStillTheSql`: the sidecar is `Sidecars.Of(query, SidecarFormat.Version, "99.0.0", RealisticModels.EntryOf(found))`.  The result equals the first test's text.
  - `ConstantOf_RunWithADiagnostic_ThrowsNamingTheCaseAndTheDiagnostic`: the query is `table-statement`'s text with `LIMIT @limit;` replaced by `LIMIT @limit + 0;`, with its committed sidecar.  The message contains `realistic/table-statement` and `SQLSRC`.
  - `ConstantOf_NameWithoutAConstant_ThrowsNamingTheCaseAndTheName`: a clean run, `name: "Missing"`.  The message contains `realistic/table-statement` and `Missing`.
- [ ] **Step 2: Run them to verify they fail.**  Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Realistic.RealisticGenerationTests"`.  Expected: the build fails because `RealisticGeneration` does not exist.
- [ ] **Step 3: Implement `RealisticGeneration`** per the Interfaces block.  Its class comment says three things:
  - why the run is fed the project's and `Cases/`'s files;
  - why `output` is removed from the project file;
  - why `SQLSRC240` is let through.
- [ ] **Step 4: Run the unit tests.**  Same command.  Expected: PASS.  If the two literals in Step 1 differ from what the generator writes, do not change them yet; Step 6 decides between them.
- [ ] **Step 5: Move `RealisticRefusalChecks` onto it.**  Its body becomes three calls: `QueryOf(found)`, `Sidecars.Of(query, await describe.EntryAsync(found, connection))`, and `RunAtModels(describe.Corpus, query, sidecar, references)`.  The constructor keeps its signature.
- [ ] **Step 6: Write the parity test in each engine's project.**  It is temporary and proves the corpus whole.
  - Each `RealisticSqlParityTests` is a `public sealed class` with no collection, so it needs no container.
  - It has one theory, `SqlOf_CaseWithModels_IsTheConstantOfItsModelsClaim(string name)`, over `RealisticCasesTests.Cases`.
  - The theory calls `RealisticModels.SkipUnlessHasModels(found)`, then checks `RealisticGeneration.SqlOf(RealisticCases.Corpus, found, entry).ShouldBe(RealisticCases.Models.SqlOf(RealisticCases.Corpus, found, entry))`.
  - Its class comment says it goes with `RealisticQueries.cs` in the next commit.
- [ ] **Step 7: Run the parity tests and the refusal tests.**
  - `dotnet test --project tests/DatabaseTests/Postgres -- --filter-class "SqlSource.DatabaseTests.Postgres.Realistic.Generation.RealisticSqlParityTests" --filter-class "SqlSource.DatabaseTests.Postgres.Realistic.Generation.RealisticModelRefusalTests"`
  - The same for `SqlServer`.

  Expected: PASS on every case with models.  The refusal test runs `two-columns-named-id` on PostgreSQL and skips every case on SQL Server.  If a Step 1 literal disagreed, correct it to the compiled constant and run Step 4 again.
- [ ] **Step 8: Validate and commit.**  Run `./pre-commit-validation.sh`, then:
```bash
git add docs/superpowers/plans/2026-11-03-realistic-corpus-single-claim.md tests/SqlSource.Tool.Testing/Realistic tests/SqlSource.Tool.Tests/Realistic/RealisticGenerationTests.cs tests/DatabaseTests/*/Realistic/Generation/RealisticSqlParityTests.cs tests/README.md
git commit -m "test(realistic): read a case's SQL from an in-process run at Models"
```

### Task 2: One claim per case; `RealisticQueries.cs` goes

**Files:**
- Delete: `tests/DatabaseTests/Postgres/Realistic/Generation/RealisticQueries.cs` and `tests/DatabaseTests/SqlServer/Realistic/Generation/RealisticQueries.cs`.
- Delete: both `RealisticSqlParityTests.cs` files from Task 1.
- Modify: both `Realistic/Generation/RealisticDapperTests.cs`.  `var sql = RealisticGeneration.SqlOf(RealisticCases.Corpus, found, entry);` replaces the `RealisticCases.Models.SqlOf` call.  The class comment says the SQL is what the generator writes for the case at `Models`, from an in-process run.
- Modify: `tests/SqlSource.Tool.Testing/Realistic/RealisticModels.cs`.  Delete `SqlOf`; the class comment names `RealisticMethods.cs` alone.
- Modify: `tests/SqlSource.Tool.Testing/Realistic/RealisticCorpus.cs`.  Delete `Queries`.  `Methods` becomes "the classes that claim each case with models, once"; the class comment becomes "the file of the classes that claim them".
- Modify: `tests/SqlSource.Tool.Testing/Realistic/RealisticClaims.cs`.
  - `AllOf` reads `corpus.Methods` alone; delete `ModelsOutput`.
  - `Of(RealisticCorpus, RealisticCase, string output)` keeps its signature until Task 3.
  - `ClaimedBy` names `corpus.Methods` alone.
  - Update the comments.
- Modify: `tests/SqlSource.Tool.Testing/Realistic/RealisticClaimChecks.cs`.
  - `ShouldBeTheCasesWithModelsOnceAtEachOutput` becomes `ShouldBeTheCasesWithModelsOnce(RealisticCorpus corpus, string name)`.  The count of the case's claims is `found.HasModels ? 1 : 0`, and every claim is at `CodeGenOutput`.
  - `ShouldBeItsClassAtEachOutput` becomes `ShouldBeItsClass(RealisticCorpus corpus, string name, string methodType)`.
  - Delete `ShouldAllBeAtModels`.
- Modify: both `Realistic/Generation/RealisticClaimsTests.cs`.
  - `All_Claims_AreTheCasesWithModelsOnce(string name)`
  - `Of_CaseWithModels_IsItsClass()`, with `"cte-chain", "CteChainMethod"` on PostgreSQL and `"category-tree-recursive", "CategoryTreeRecursiveMethod"` on SQL Server.
  - Keep `Methods_EveryClaim_IsAtCodeGen` and `All_Claims_NameOnlyCasesThatExist`; delete `Queries_EveryClaim_IsAtModels`.
  - Rewrite the class comment.
- Modify: `tests/SqlSource.Tool.Tests/Realistic/RealisticCorpusTests.cs`.  `Files_OfEachCorpus_AreInTheRepository` drops the `corpus.Queries` line.
- Modify: `tests/SqlSource.Tool.Tests/Realistic/RealisticClaimsTests.cs`.  The header names `RealisticMethods.cs` alone.
- Modify: both `Realistic/Generation/RealisticMethods.cs` header comments.  Each case is claimed here once, the models it gives are those the Dapper and method tests read, and the Dapper tests' SQL comes from `RealisticGeneration`.  Drop "RealisticQueries.cs claims it at Models".
- Modify: both `Realistic/RealisticCases.cs`.  The comment on `Models` names `Realistic/Generation/RealisticMethods.cs` alone.
- Modify: `tests/DatabaseTests/Postgres/Realistic/AGENTS.md` and `tests/DatabaseTests/SqlServer/Realistic/AGENTS.md`.  The bullet naming `RealisticQueries.cs` becomes one bullet, at most 80 words:
  - `RealisticMethods.cs` holds the one claim of each case with models, which `RealisticClaimsTests` checks.
  - `RealisticDapperTests` takes the case's SQL from `RealisticGeneration.SqlOf`, an in-process run at `Models`, never from a second claim, because a file gives every type that claims it one output.
- Modify: `tests/DatabaseTests/Postgres/README.md`.
  - Line 54, the shared-parts list: "the claims of `RealisticMethods.cs`, the run of a case's query at `Models` in process (`RealisticGeneration`)".
  - Line 60, the claim paragraph: each case is claimed once, by a class of its own in `RealisticMethods.cs` at `CodeGen`, which gives the project its models and its method.  Drop "the two classes are in one namespace".
  - Line 62, the `RealisticClaimsTests` bullet: "claimed exactly once", "a class at an output other than `CodeGen`".
  - The `RealisticDapperTests` bullet adds that its SQL is what the generator writes for the case at `Models`, from an in-process run over the case's query, its committed sidecar and the settings files of the project and of `Cases/`.
  - The `RealisticModelRefusalTests` bullet adds "through `RealisticGeneration`".
  - Line 84, "To add a case": "Add its class to `Realistic/Generation/RealisticMethods.cs`".
- Modify: `tests/DatabaseTests/SqlServer/README.md`.  Make the same edits at lines 51, 74, 76, the Dapper bullet and line 100.
- Modify: `tests/README.md`.
  - The bullet that lists the shared realistic helpers: `RealisticClaims` reads the claims of a corpus's `RealisticMethods.cs`, and `RealisticModels` finds "a case's models and its method".
  - The line that names `RealisticQueries.cs`: "in `Realistic/Generation/RealisticMethods.cs` of the second and the fourth".
- Modify: `docs/architecture/sqlserver-describer.md:387`.
  - "is a `Models` query of the end-to-end project" becomes "is a query of the end-to-end project, claimed once at `CodeGen`".
  - "read through Dapper over the seed" gains ", with the SQL the generator writes for it at `Models` in a run in process".
  - "and each case is claimed a second time at `CodeGen` and run through" becomes "and each case is run through".

**Interfaces:**
- Consumes: `RealisticGeneration.SqlOf` (Task 1).
- Produces: `RealisticClaimChecks.ShouldBeTheCasesWithModelsOnce(RealisticCorpus, string)`; `RealisticClaimChecks.ShouldBeItsClass(RealisticCorpus, string name, string methodType)`; `RealisticClaims.ClaimedBy(RealisticCorpus, RealisticCase, string output, int count)`, unchanged until Task 3.

- [ ] **Step 1: Time the Dapper tests before the change.**  Run: `dotnet test --project tests/DatabaseTests/Postgres -- --filter-class "SqlSource.DatabaseTests.Postgres.Realistic.Generation.RealisticDapperTests"`, and the same for `SqlServer`.  Note each run's duration.
- [ ] **Step 2: Switch both `RealisticDapperTests` to `RealisticGeneration.SqlOf`.**  Delete `RealisticModels.SqlOf`, both `RealisticQueries.cs`, both parity tests, `RealisticCorpus.Queries` and `RealisticClaims.ModelsOutput`.
- [ ] **Step 3: Update the claim checks and tests** per Files.
- [ ] **Step 4: Update every comment, README, AGENTS.md and architecture line** in Files.  Then search: `grep -rn -e RealisticQueries -e ModelsOutput -e "AtEachOutput" -e "claimed twice" -e "second time" tests docs/architecture`.  Expected: no hit about the realistic corpus outside `docs/superpowers/`.  Hits that are about something else stay: `Run_ModelsOutput_GivesNoMethod`, the `Fixtures/` bullet of `tests/README.md`, the comments of the allocation tests, `docs/architecture/generator.md`'s use of "second time", and `tests/DatabaseTests/SqlServer/README.md`'s `EndToEnd/` paragraph, which belongs to pull request A.  Read each hit before deciding.
- [ ] **Step 5: Run the affected tests.**
  - `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-namespace "SqlSource.Tool.Tests.Realistic"`
  - For each engine, with `--filter-namespace "SqlSource.DatabaseTests.<Engine>.Realistic.Generation"`.

  Expected: PASS, with the same skips as on `main`.  Compare the Dapper classes' time with Step 1, and follow the caching rule in Decisions.
- [ ] **Step 6: Check that the corpus is untouched.**  Run `git status --short -- '*/Realistic/Cases/*'`.  Expected: nothing.
- [ ] **Step 7: Validate and commit.**  Run `./pre-commit-validation.sh`, then:
```bash
git add -A tests docs/architecture/sqlserver-describer.md
git commit -m "test(realistic): claim each case once, at CodeGen, and drop RealisticQueries.cs"
```

### Task 3: The claims set `Source` alone

A reviewer may reject this task without Task 2.  It removes the last attribute settings from the corpus, so that pull request 1 does not touch it.

**Files:**
- Modify: both `Realistic/Generation/RealisticMethods.cs`.
  - Every `[SqlSourceGenerate(Source = "../Cases/<case>/query.sql", Output = GeneratorOutput.CodeGen)]` becomes `[SqlSourceGenerate(Source = "../Cases/<case>/query.sql")]`.  That is 52 single-line claims and 3 multi-line ones on PostgreSQL (lines 53, 80 and 155), and 53 on SQL Server.
  - Run `./format.sh csharpier -- <file>`, which joins the multi-line three.
  - The header comment says the output, `CodeGen`, is the project's `sqlsource.json`, and that a claim sets `Source` alone.
- Modify: `tests/SqlSource.Tool.Testing/Realistic/RealisticClaims.cs`.
  - The record is `RealisticClaim(string TypeName, string Case)`.
  - Delete `CodeGenOutput` and `OutputOf`.
  - `Of(RealisticCorpus corpus, RealisticCase found)` and `ClaimedBy(RealisticCorpus corpus, RealisticCase found, int count)` lose their output.
  - `Parse` throws `InvalidOperationException` for any attribute argument whose name is not `AttributeSource.SourceProperty`: `"The class {X} sets {Arg}, and a claim of the corpus sets Source alone: its output and every other setting are its settings files'."`.  This check runs before the `Source` check.
- Modify: `tests/SqlSource.Tool.Testing/Realistic/RealisticClaimChecks.cs`.  Delete `ShouldAllBeAtCodeGen`; `ShouldBeTheCasesWithModelsOnce` counts claims alone.
- Modify: `tests/SqlSource.Tool.Testing/Realistic/RealisticModels.cs`.  `MethodOf` calls `RealisticClaims.Of(corpus, found)`.
- Modify: both `Realistic/Generation/RealisticClaimsTests.cs`.  Delete `Methods_EveryClaim_IsAtCodeGen`.  The class comment notes that `RealisticModelsTests.MethodOf_Entry_IsTheMethodOfItsCodeGenClaim` pins the output.
- Modify: `tests/SqlSource.Tool.Tests/Realistic/RealisticClaimsTests.cs`.  Its tests are listed in Step 1.
- Modify: both `Realistic/AGENTS.md`.  The Task 2 bullet adds "with `Source` alone, its output `CodeGen` being the project's settings file's".  Both READMEs' claim paragraph adds the same, and their `RealisticClaimsTests` bullet changes "a class at an output other than `CodeGen`" to "a claim that sets more than `Source`".

**Interfaces:**
- Produces: `RealisticClaim(string TypeName, string Case)`; `RealisticClaims.Of(RealisticCorpus, RealisticCase)`; `RealisticClaims.ClaimedBy(RealisticCorpus, RealisticCase, int)`.

- [ ] **Step 1: Rewrite the parser tests first** in `tests/SqlSource.Tool.Tests/Realistic/RealisticClaimsTests.cs`.
  - `Parse_ClassWithPathAttribute_IsItsClaim`: `[SqlSourceGenerate(Source = "../Cases/a-b/query.sql")] internal static partial class ABMethod;` parses to `[new RealisticClaim("ABMethod", "a-b")]`.
  - `Parse_QualifiedAttributeName_IsItsClaim` is kept with the new record.
  - `Parse_ClaimWithAnArgumentOtherThanSource_ThrowsNamingTheClassAndTheArgument` is a theory over `Output = GeneratorOutput.CodeGen` and `SqlLocation = SqlLocation.Direct`.  The message contains `XMethod` and `Output`, or `SqlLocation`.
  - `Parse_AttributeWithoutSource_ThrowsNamingTheClass` takes the input `[SqlSourceGenerate] internal static partial class XMethod;`.  The message contains `XMethod` and `no path`.
  - Keep `Parse_ClassWithAnotherAttribute_IsNoClaim` and `Parse_PathOutsideCases_Throws`.
  - Delete `Parse_ClaimAtCodeGen_HasThatOutput`, `Parse_QualifiedOutput_IsItsMemberName` and `Parse_OutputThatIsNotAMemberAccess_ThrowsNamingTheClass`.
- [ ] **Step 2: Run them to verify they fail.**  Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Realistic.RealisticClaimsTests"`.  Expected: the build fails because `RealisticClaim` still has three parameters.
- [ ] **Step 3: Implement the `RealisticClaims` and `RealisticClaimChecks` changes**, and update `RealisticModels.MethodOf`.
- [ ] **Step 4: Rewrite both `RealisticMethods.cs` and format them.**
- [ ] **Step 5: Run the tests.**
  - The Tool.Tests command from Step 2.
  - For each engine, `--filter-namespace "SqlSource.DatabaseTests.<Engine>.Realistic.Generation"`.

  Expected: PASS.  `RealisticMethodTests` and `RealisticModelsTests.MethodOf_*` prove the output is still `CodeGen`.
- [ ] **Step 6: Update the AGENTS.md and README lines**, then validate and commit.  Run `./pre-commit-validation.sh`, then:
```bash
git add -A tests
git commit -m "test(realistic): a claim sets Source alone; the output is the project's"
```

### Task 4: Verify and open the pull request

- [ ] **Step 0: Record the tech debt.**  Add `docs/tech-debt/TD-<next id>-sql-server-refusal-references-never-run.md`, in the format of the other entries, and its row in `docs/tech-debt/README.md`.  It says that `RealisticRefusalChecks` passes `SqlClientReferences.SqlClient7` beside the runtime's SqlClient, and that the pairing has never run because every SQL Server case skips the refusal test (root `AGENTS.md` rule 2).  Commit it after `./pre-commit-validation.sh`.
- [ ] **Step 1: Run the full validation.**  Run `./pre-commit-validation.sh --full`.  Expected: every step passes.
- [ ] **Step 2: Run the floor images**, with both commands in Global Constraints.  Expected: PASS.  Cases with a `minimumMajor` above the floor skip.
- [ ] **Step 3: Run `branch-review:review`** and address its findings until it comes back clean.
- [ ] **Step 4: Check the version and bring the branch up to date.**  Run `git fetch --tags origin` and confirm there is no `v*` tag.  Rebase onto `origin/main`.  If anything changed, run Steps 1–3 again.
- [ ] **Step 5: Push, open the pull request and turn on auto-fix.**  The description says:
  - no product code changed and no sidecar was written again;
  - the Dapper class's timing before and after;
  - the claims now set `Source` alone.

## Self-review

- **Spec coverage.**
  - One claim per case at `CodeGen`: Tasks 2 and 3.
  - `RealisticQueries.cs` goes: Task 2.
  - Dapper SQL from an in-process run on the case's file at `models` through the generator harness: Tasks 1 and 2.
  - Models from the compiled `CodeGen` claim: Task 2, unchanged `RealisticModels.OutputModelOf` and `InputModelOf`.
  - One query per case: Global Constraints and Task 2 Step 6.
  - `RealisticClaimsTests`, the realistic AGENTS.md files and `tests/DatabaseTests/*/README.md`: Tasks 2 and 3.
  - Also updated: `tests/README.md` and `docs/architecture/sqlserver-describer.md`, which name the claims.
- **Types.**
  - Task 1 adds `RealisticGeneration.SqlOf(RealisticCorpus, RealisticCase, SidecarEntry)`, and Task 2 uses it.
  - Task 2 uses `RealisticClaims.Of` with three parameters, and Task 3 moves it to two.  `MethodOf` is its only other caller and is updated in Task 3.
- **Out of scope.**  `RealisticRefusalChecks` passes `SqlClientReferences.SqlClient7` beside the runtime's SqlClient.  That pairing has never run, because every SQL Server case skips it.  This plan does not fix it, but root `AGENTS.md` rule 2 requires recording it: Task 4, Step 0.
