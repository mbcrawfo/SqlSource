# Settings files, layer 5: every project moves to settings files - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** No project of the repository sets a `SqlSource*` setting as an MSBuild property or as item metadata: each setting is in a `sqlsource.json` of the folder it applies to, or in a header marker of the one file it applies to, and every build, test and sidecar gives what it gave before.

**Architecture:** Data, not code.  A project's properties become a `sqlsource.json` beside its `.sql` files; metadata on a folder's glob becomes that folder's `sqlsource.json`; metadata on one file becomes a marker in that file's header.  The repository's root gets `{"root": true}`.  A test of an MSBuild-only behaviour that loses its project goes, with what covers the same ground named.  The layer lands as two pull requests: part 1, the projects on disk and the tests that read them; part 2, the tests that build their projects in memory (the generator harness, the tool's `TestProject`).

**Tech Stack:** MSBuild project files, JSON with comments (`sqlsource.json`), header markers of `.sql` files, xUnit v3 and Shouldly, bash (`tools/`).

**Spec:** [`docs/superpowers/specs/2026-10-25-settings-files-design.md`](../specs/2026-10-25-settings-files-design.md), "The layers" item 5, "The chain of a folder", "Which place wins".  Read the root `AGENTS.md`, `examples/AGENTS.md`, `tests/AGENTS.md`, `tools/AGENTS.md`, `tests/README.md` and the READMEs of `tests/SqlSource.Tests.Postgres/` and `tests/SqlSource.Tests.SqlServer/` before any task.

## Where the merged code differs from the spec, and the builder's calls

- **A linked `.sql` file takes its own folder's chain** (spec, "The chain of a folder").  So `tests/SqlSource.Tests.Postgres.Npgsql8` and `tests/SqlSource.Tests.SqlServer.SqlClient61`, which link `AllTypes.sql` (and `NewTypes.sql`) from the end-to-end projects, get those projects' settings, not their own: the input suffix `Args` (their code says `InsertEveryTypeParams`, `InsertAllTypesParams` and `WriteAllTypesParams` today), `record` input models, `IReadOnlyList` rows and, for SqlClient 6.1, the output `CodeGen`.  The two projects keep no settings file (their own folder holds no `.sql` file), take the `Args` names, and SqlClient 6.1's two models-only claims say `Output = GeneratorOutput.Models` on their attribute.
- **A copied fixture reaches no file of the repository.**  `FixtureProjects.Copy` copies a fixture into a folder under the temporary directory, so `ErrorPaths`, `Single` and the `SqlServer` fixture each get their own `sqlsource.json`, with `"root": true` so that a stray file in the temporary directory cannot reach them.
- **One file's metadata becomes a header marker** (spec): `Notes.sql` in both end-to-end projects (`-- column: accounts.id -> AccountId`), `Collections/ByMetadata.sql` of SQL Server (`-- collection-type: ImmutableArray`), `Queries/Users.sql` of the `Single` fixture (`-- database: billing`), and the files of `tests/SqlSource.Tests/EndToEnd/` and `tools/package-install/`.  Every marker but `-- dialect:` is a comment to the hash (`SqlQueryHash`), and no file with a sidecar gets a `-- dialect:`, so no sidecar changes.  `Collections/ByMetadata.sql` and its query `ListAccountsByMetadata` keep their names: renaming the query would rewrite its sidecar, which is written by `tools/describe-end-to-end.sh` alone.  Files without sidecars whose names say where their setting came from are renamed: `ByMetadata.sql` to `ByHeader.sql` and `ByProperty.sql` to `ByProject.sql`, in `tests/SqlSource.Tests/EndToEnd/Dialects/` and `tools/package-install/Queries/`.
- **The realistic corpus's glob is a folder**, `Realistic/Cases/**/*.sql`: its settings go in `Realistic/Cases/sqlsource.json`.  `RealisticCaseReader.NamesIn` lists the folders of `Cases/`, and `RealisticCasesTests` checks the files of each case's folder, so a file in `Cases/` itself is neither.
- **Repository settings files carry comments** where the project file had them, and the examples' files carry `$schema` and `"root": true`, as the spec's sample does (spec, "Left to the builder": "whether the examples' files carry it").  They indent by 2, as `.editorconfig` says of `*.json`.
- **The repository's root file is pinned by a test** (`RepositorySettingsFileTests`), so that nothing adds a setting there unseen.
- **Scope added to part 2:** the tool's tests whose in-memory manifests carry `Property.SqlSource*` or `File.SqlSource*` lines only to configure a plan (`Plans.Postgres` and about 25 other sites).  The spec lists the generator's tests; the tool's are the same case, and moving them now leaves layer 6 only the tests of the MSBuild path itself.

## Global Constraints

- Every commit passes `./pre-commit-validation.sh`; run `./format.sh` first.  It needs Docker running: the end-to-end, realistic and container tests are part of `dotnet test`.
- Sidecars (`*.sql.json`) do not change.  After each task, `git status --short -- '*.sql.json'` prints nothing.  A sidecar the build or `CheckTests` finds stale means a setting did not arrive: find which, never re-describe.
- A settings file holds only the keys of the spec's table, camelCase, with the values the MSBuild text held (`"sealed class"`, `"IReadOnlyList"`, `"timestamptz": "System.DateTimeOffset"`).  `generatorParameters` is an array of words.
- `SqlSourceIncludeFiles`, `SqlSourceImported`, `SqlSourceManifestFile` and `SqlSourceBuildFolders` are not settings and stay where they are.
- AGENTS.md files are updated in the commit that renames, moves or removes what they name.  The tests' and tools' READMEs are updated where a task makes them wrong; the manual, `README.md` and `docs/architecture/` are layer 7's.
- No test of the MSBuild path is weakened to pass: one that loses its project is deleted, and the deletion is listed in `5-projects-notes.md` (shared notes folder) and in the pull request with what covers the same ground.

## Review Focus

1. **A linked `.sql` file outside its project** (Npgsql 8, SqlClient 6.1) gets the settings of the folder it lives in.  Covered by building and running both projects in Task 2 and Task 3.
2. **A fixture copied to the temporary directory** reads only its own settings files: the database `billing` of `Single`, `fixtures` of the `SqlServer` fixture and `errors` of `ErrorPaths` reach the describer.  Covered by `ToolProcessTests`, `DescribeCommandTests` and `ErrorPathTests` in Tasks 2, 3 and 5.
3. **A nearer folder file over the project's file for one key, the farther file's other keys kept** (billing: `timestamptz`/`date` overridden while the project's other overrides and the input suffix stay).  Covered by `ModelShapeTests` and the billing tests in Tasks 2 and 3.
4. **A settings file a sidecar's provenance depends on** (`database`): `CheckTests` runs `--check` over both real projects, realistic cases included, and finds every sidecar current.  Tasks 2 and 3.
5. **A header marker added above a file's first query** moves no position a test asserts.  `Users.sql` of `Single` and the files of `tests/SqlSource.Tests/EndToEnd/` are checked in Tasks 4 and 5.

---

## Part 1: the projects

### Task 1: The repository's root file and the examples

**Files:**
- Create: `sqlsource.json` (`{"root": true}` and nothing else, with a comment saying why: no file above the checkout, the main checkout's own when this is a worktree under `.claude/worktrees/`, reaches a project of the repository).
- Create: `tests/SqlSource.Tests/RepositorySettingsFileTests.cs`
- Create: `examples/PostgresExamples/sqlsource.json`, `examples/SqlServerExamples/sqlsource.json`
- Modify: `examples/PostgresExamples/PostgresExamples.csproj`, `examples/SqlServerExamples/SqlServerExamples.csproj` (remove `SqlSourceDialect`, `SqlSourceTypeOverrides`, `SqlSourceColumnTypes` and their comments; the opening comment says the settings are in `sqlsource.json` beside the project file)
- Modify where they name the properties: `examples/*/README.md`, `examples/AGENTS.md` (rule 3: "sets no `database`"), `tools/AGENTS.md` (the `describe-end-to-end.sh` rule's "neither project sets `SqlSourceDatabase`")

**Interfaces:**
- Produces: the root file every later task's projects reach last.

- [ ] **Step 1: Write the failing test** `RepositorySettingsFileTests.RootSettingsFile_OfTheRepository_StopsEveryChainAndSetsNothing`: read `Path.Combine(RepositoryRoot.Folder, "sqlsource.json")` (`RepositoryRoot` of `tests/SqlSource.Testing`) with `SettingsFileReader.Read(text, SettingPlace.OfSettingsFile(path, 0))`; assert `Root == true`, `Dialect == null`, `Level == SettingsLevel.None`, `Errors` empty.  The class is `public sealed`, at the project's root as `TestLayoutTests` is.
- [ ] **Step 2: Run it**: `dotnet test --project tests/SqlSource.Tests -- --filter-class '*RepositorySettingsFileTests'`; expect FAIL (file not found).
- [ ] **Step 3: Write the root file and the examples' files.**  Postgres: `$schema` (`https://raw.githubusercontent.com/mbcrawfo/SqlSource/main/schemas/settings-v1.schema.json`), `"root": true`, `"dialect": "postgres"`, `"typeOverrides": {"timestamptz": "System.DateTimeOffset"}`, `"columnTypes": {"order_lines.order_id": "OrderId"}`, each with the comment the project file had.  SQL Server: the same with `"dialect": "mssql"` and `"typeOverrides": {"date": "System.DateOnly"}`.
- [ ] **Step 4: Run the test and build the examples**: the test passes; `dotnet build examples/PostgresExamples` and `dotnet build examples/SqlServerExamples` succeed with no warning but `SQLSRC240`; `git status --short -- '*.sql.json'` prints nothing.  A model whose `timestamptz`, `date` or `order_id` property changed type fails the examples' own code, which is the check that the files are read.
- [ ] **Step 5: Format, validate, commit** ("Move the examples' settings into sqlsource.json, and stop every chain at the repository's root").

### Task 2: PostgreSQL's end-to-end project, its error paths and Npgsql 8

**Files:**
- Create: `tests/SqlSource.Tests.Postgres/sqlsource.json`: `dialect` `postgres`, `database` `app`, `output` `CodeGen`, `inputModelSuffix` `Args`, `inputModelType` `record`, `collectionType` `IReadOnlyList`, `typeOverrides` `{"timestamptz": "System.DateTimeOffset"}`, with the project file's comments.
- Create: `tests/SqlSource.Tests.Postgres/EndToEnd/Queries/Billing/sqlsource.json`: `outputModelType` `sealed class`, `outputModelSuffix` `Row`, `typeOverrides` `{"timestamptz": "System.DateTime"}`, `collectionType` `IList`.
- Create: `tests/SqlSource.Tests.Postgres/Realistic/Cases/sqlsource.json`: `database` `realistic`, `typeOverrides` `{"timestamptz": "default"}`.
- Create: `tests/SqlSource.Tests.Postgres/EndToEnd/ErrorPaths/sqlsource.json`: `"root": true`, `dialect` `postgres`, `database` `errors`.
- Modify: `tests/SqlSource.Tests.Postgres/EndToEnd/Queries/Notes.sql` (header: `-- column: accounts.id -> AccountId` beside the `notes.account_id` marker; the opening comment says both come from the header)
- Modify: `tests/SqlSource.Tests.Postgres/SqlSource.Tests.Postgres.csproj` (the setting properties and the three `AdditionalFiles Update` items go), `tests/SqlSource.Tests.Postgres/EndToEnd/ErrorPaths/App.csproj` (its two properties go)
- Modify: `tests/SqlSource.Tests.Postgres.Npgsql8/SqlSource.Tests.Postgres.Npgsql8.csproj` (both properties go; a comment says the linked file takes the settings of its own folder), `tests/SqlSource.Tests.Postgres.Npgsql8/AllTypesQueryTests.cs` (`InsertEveryTypeParams` → `InsertEveryTypeArgs`, `InsertAllTypesParams` → `InsertAllTypesArgs`, and whatever else the compiler names)
- Modify: `tests/SqlSource.Tests.Postgres/README.md` (the corpus paragraph's "the project's metadata on `Realistic/Cases/**/*.sql`", and the rows of `Accounts.sql`, `Billing/Invoices.sql` and `Notes.sql` in the table of the end-to-end project), `tests/AGENTS.md` where it names what changed.

- [ ] **Step 1: Make the change** as listed.  `Notes.sql` keeps its queries byte for byte; only its header grows.
- [ ] **Step 2: Build**: `dotnet build tests/SqlSource.Tests.Postgres` and `dotnet build tests/SqlSource.Tests.Postgres.Npgsql8` succeed; `git status --short -- '*.sql.json'` prints nothing.
- [ ] **Step 3: Run the tests** (Docker running): `dotnet test --project tests/SqlSource.Tests.Postgres` and `dotnet test --project tests/SqlSource.Tests.Postgres.Npgsql8` pass, `CheckTests`, `ModelShapeTests`, `ErrorPathTests`, the billing and notes tests and the realistic tests among them.  A failure of `CheckTests` names the setting that did not arrive.  Run `tests/SqlSource.Tests.Postgres` again on the floor image, `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24` (tests/AGENTS.md: a change there runs on both images).
- [ ] **Step 4: Format, validate, commit** ("Move the PostgreSQL end-to-end project's settings into sqlsource.json and markers").

### Task 3: SQL Server's end-to-end project, its error paths and SqlClient 6.1

**Files:**
- Create: `tests/SqlSource.Tests.SqlServer/sqlsource.json`: `dialect` `mssql`, `database` `app`, `output` `CodeGen`, `inputModelSuffix` `Args`, `inputModelType` `record`, `typeOverrides` `{"date": "System.DateOnly"}`, `collectionType` `IReadOnlyList`, with the comments.
- Create: `tests/SqlSource.Tests.SqlServer/EndToEnd/Queries/Billing/sqlsource.json` (`sealed class`, `Row`, `{"date": "default"}`), `tests/SqlSource.Tests.SqlServer/Realistic/Cases/sqlsource.json` (`realistic`, `{"date": "default"}`), `tests/SqlSource.Tests.SqlServer/EndToEnd/ErrorPaths/sqlsource.json` (`"root": true`, `mssql`, `errors`).
- Modify: `tests/SqlSource.Tests.SqlServer/EndToEnd/Queries/Notes.sql` (header `-- column: accounts.id -> AccountId`), `tests/SqlSource.Tests.SqlServer/EndToEnd/Queries/Collections/ByMetadata.sql` (header `-- collection-type: ImmutableArray`; its comment and its query's `summary` say the header gives it), the test of `ListAccountsByMetadata` in `EndToEnd/MethodCollectionTests.cs` (its name says the header, not the metadata) and its claim's comment in `EndToEnd/CollectionQueries.cs`.
- Modify: `tests/SqlSource.Tests.SqlServer/SqlSource.Tests.SqlServer.csproj`, `tests/SqlSource.Tests.SqlServer/EndToEnd/ErrorPaths/App.csproj`, `tests/SqlSource.Tests.SqlServer.SqlClient61/SqlSource.Tests.SqlServer.SqlClient61.csproj` (as in Task 2).
- Modify: `tests/SqlSource.Tests.SqlServer.SqlClient61/AllTypesQueries.cs` (`Output = GeneratorOutput.Models` on `AllTypesQueries` and `NewTypesQueries`), its tests (`WriteAllTypesParams` → `WriteAllTypesArgs` and the like), `tests/SqlSource.Tests.SqlServer/README.md` (the corpus paragraph and the end-to-end table), `tests/AGENTS.md` where it names what changed.

The `summary` of a query is a comment to the hash, but check the sidecar does not change after the edit; if it does, leave the summary as it was.

- [ ] **Step 1: Make the change.**
- [ ] **Step 2: Build both projects**; no sidecar changes.
- [ ] **Step 3: Run** `dotnet test --project tests/SqlSource.Tests.SqlServer` and `dotnet test --project tests/SqlSource.Tests.SqlServer.SqlClient61`: pass.  Run `tests/SqlSource.Tests.SqlServer` again on the floor image, `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`.
- [ ] **Step 4: Format, validate, commit** ("Move the SQL Server end-to-end project's settings into sqlsource.json and markers").

### Task 4: `tests/SqlSource.Tests`' own project

**Files:**
- Create: `tests/SqlSource.Tests/EndToEnd/sqlsource.json`: `dialect` `postgres`, `output` `Sql`, `generatorParameters` `["sort-input", "no-token-validation"]`, with the comments (token validation off, so a query without a list of its own does not check its arguments; output `Sql`, since no query here has a sidecar).
- Rename: `EndToEnd/Dialects/ByMetadata.sql` → `ByHeader.sql` (header `-- dialect: mysql`), `EndToEnd/Dialects/ByProperty.sql` → `ByProject.sql`; header `-- dialect: mysql, no-backslash-escapes` in `ByOption.sql`, `-- generator: no-token-validation keep-comments` in `EndToEnd/Parameters/Kept.sql` (its comment says the header).  Each file's leading comment says where its setting comes from.
- Modify: `tests/SqlSource.Tests/SqlSource.Tests.csproj` (the setting properties, their comment and the three `AdditionalFiles Update` items go), `tests/SqlSource.Tests/EndToEnd/EndToEndTests.cs` (the members `DialectQueries.ByHeader` and `ByProject`; comments that say "metadata" or "written over several lines" say the header or the settings file), `tests/SqlSource.Tests/Package/BuildFileTests.cs` (the comment of `Targets_EveryPropertyOfThePackage_IsTrimmed` no longer cites the projects).
- Delete: `EndToEndTests.ProjectWithMetadata_FileTheBuildWritesForTheCompiler_NamesOnlyTheFilesWithMetadata` (it reads `build_metadata` from `compiler.editorconfig`, which no file has now).  The settings of the moved files are still shown reaching the generator by the tests of `ByHeader`, `ByOption` and `Kept`; the targets' writing of metadata is layer 6's to remove, and `BuildFileTests` keeps the XML until then.
- Modify: `tests/AGENTS.md` (the rules "Multi-line values in `tests/SqlSource.Tests/SqlSource.Tests.csproj` are the test" and "The metadata of a file of `tests/SqlSource.Tests/EndToEnd/` is set by its exact path" go), `tests/README.md` where it says the same.

- [ ] **Step 1: Make the change.**
- [ ] **Step 2: Run** `dotnet test --project tests/SqlSource.Tests -- --filter-namespace '*EndToEnd'` and `--filter-class '*BuildFileTests'`: pass, with every constant as before (a marker above the first query moves no query text).
- [ ] **Step 3: Format, validate, commit** ("Move tests/SqlSource.Tests' own settings into sqlsource.json and markers").

### Task 5: The tool's fixture projects

**Files** (all under `tests/SqlSource.Tool.Tests/Fixtures/Projects/` unless named):
- `Single/sqlsource.json` (`"root": true`, `"dialect": "postgres"`), header `-- database: billing` in `Single/Queries/Users.sql`; `Single/App.csproj` keeps no setting and its comment no longer speaks of lines of their own.
- `SqlServer/sqlsource.json` (`"root": true`, `mssql`, `fixtures`); `SqlServer/App.csproj` loses both properties.
- `BuildHookFile/`, `LateFile/`, `Multi/`, `OddPaths/`: the `SqlSourceDialect` property goes (nothing reads it); the metadata of `Generated/Late.sql` in both `Directory.Build.targets` goes, with the comment that it is trimmed.
- `InSolution/Shared.props`: sets `<DefineConstants>$(DefineConstants);FROM_SOLUTION</DefineConstants>` in place of the dialect.
- Delete `SeveralLines/` and `FixtureProjectTests.Evaluate_ProjectWhoseValuesAreWrittenOverSeveralLines_GivesEachOnOneLine`.  A JSON value has no line to trim; `BuildFileTests.Targets_EveryPropertyOfThePackage_IsTrimmed` pins the trims until layer 6 removes them.
- `tests/SqlSource.Tool.Tests/Projects/FixtureProjectTests.cs`:
  - `Evaluate_ProjectWithOneFramework_GivesItsManifest`: the properties are `SqlSourceBuildFolders` alone (of the setting properties, none has a value), the file has no metadata, and `SettingsFiles` is `[<copy>/sqlsource.json]`.
  - `Evaluate_ProjectOfASolution_EvaluatesAsInABuildOfTheSolution`: `DefineConstants` contains `FROM_SOLUTION`.
  - The `Directory.Build.rsp` test (about line 243): pass a property that is not a setting (`-p:LangVersion=12.0`, asserting `LangVersion`), since a global property is still how a build sets one.
- `tests/SqlSource.Tool.Tests/Projects/ManifestTargetTests.cs`:
  - `WriteManifest_ProjectWithOneFramework_HoldsWhatTheCompilerIsGiven`: no `PropertyLines`/`FileLines` values but empty ones, and the line `Settings=<copy>/sqlsource.json` between the `File=` line and the `Compile=` line.
  - `WriteManifest_FileAddedByATargetThatHooksTheTrim_IsListedWithTrimmedMetadata` becomes `..._IsListed`: the `File=` line of `Late.sql` with no metadata.
- `tests/README.md` (the fixture projects' list), `tests/SqlSource.Tests.Postgres/README.md` and `tests/SqlSource.Tests.SqlServer/README.md` where they describe `Single` or the `SqlServer` fixture.

- [ ] **Step 1: Update the tests** to the expectations above, and run them to see them fail against the fixtures as they are: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class '*FixtureProjectTests' --filter-class '*ManifestTargetTests'`.
- [ ] **Step 2: Change the fixtures** as listed.
- [ ] **Step 3: Run** the two classes again, then `SettingsFileSearchTests` and `TerminationTests`: pass.  Then, Docker running, `ToolProcessTests` of `tests/SqlSource.Tests.Postgres` (`billing (postgres): 1 described`) and `DescribeCommandTests` and `ToolProcessTests` of `tests/SqlSource.Tests.SqlServer` (`fixtures (mssql)`): pass.
- [ ] **Step 4: Format, validate, commit** ("Move the tool's fixture projects to settings files and markers").

### Task 6: The installed package and the installed tool

**Files:**
- Create: `tools/package-install/sqlsource.json`: `"root": true`, `dialect` `postgres`, `output` `Sql`, `generatorParameters` `["sort-input", "no-token-validation"]`.
- Modify: `tools/package-install/Consumer.csproj` (the properties, the three items and their comments go), `tools/package-install/Directory.Build.targets` (the property and the metadata of `AddedByATarget.sql` go; the item a target adds stays, as TD-0025's gap), header markers: `-- dialect: mysql` in `Queries/ByMetadata.sql` renamed `Queries/ByHeader.sql`, `-- dialect: mysql, no-backslash-escapes` in `Queries/ByOption.sql`, `-- generator: no-token-validation keep-comments` in `Queries/Kept.sql`, `-- dialect: mssql` in `Queries/AddedByATarget.sql`; rename `Queries/ByProperty.sql` → `Queries/ByProject.sql`.
- Modify: `tools/package-install/Program.cs` and `expected-output.txt` together: the labels "comments kept by the item", "dialect of the item", "dialect of the item, with an option on the next line" and "dialect of an item that a target adds" say the file header ("comments kept by the file header", "dialect of the file header", "dialect of the file header, with an option", "dialect of a file that a target adds"); the SQL of each line is unchanged.
- Modify: `tools/check-package-install.sh`: the checks of `File.SqlSourceDialect=mysql, no-backslash-escapes` and `Property.SqlSourceGeneratorParameters=sort-input no-token-validation` go; a check that the manifest holds `Settings=<consumer>/sqlsource.json` takes their place; the web stage's `Web.csproj` loses its `SqlSourceDialect`.
- Modify: `tools/check-tool-install.sh`: the heredoc project loses its dialect and database, and the script writes `App/sqlsource.json` with `"root": true`, `"dialect": "mssql"`, `"database": "orders"`.
- Modify: `tools/README.md` (`check-package-install.sh`: no values over several lines; the manifest checks), `tools/AGENTS.md` (the rule "Its multi-line values are deliberate" goes).

- [ ] **Step 1: Make the change.**
- [ ] **Step 2: Run** `tools/check-package.sh`, `tools/check-package-install.sh` and `tools/check-tool-install.sh` (they pack first if they need to; see `tools/README.md`): each passes.  Then `tools/shellcheck.sh` and `tools/shfmt.sh --check`.
- [ ] **Step 3: Format, validate, commit** ("Move the installed package's consumer and the tool's install check to settings files").

### Task 7: No setting left, and the notes

- [ ] **Step 1: Search**: `git grep -nE '<SqlSource(Dialect|Database|Output|GeneratorParameters|InputModelSuffix|OutputModelSuffix|ModelNamespace|InputModelType|OutputModelType|CollectionType|TypeOverrides|ColumnTypes|NestedClass)>|SqlSource(Dialect|Database|Output|GeneratorParameters|InputModelSuffix|OutputModelSuffix|ModelNamespace|InputModelType|OutputModelType|CollectionType|TypeOverrides|ColumnTypes|NestedClass)="' -- '*.csproj' '*.props' '*.targets' tools/ ':!src/'` prints nothing.
- [ ] **Step 2:** Write `5-projects-notes.md` in the shared notes folder: the MSBuild-only tests left for layer 6 (from part 1 and the inventory of part 2), the tests this layer deleted with what covers them, and the settings that could not move (none expected).

## Part 2: the tests that build their projects in memory

Detailed in this plan before its build starts, once part 1 has merged.  The decisions so far:

- **The generator harness writes a settings file in place of the properties.**  `GeneratorHarness.Run`'s `output`, `dialect`, `generatorParameters` and `properties` (and `ModelRuns`, `MethodRuns`, `CachingScene`, `CreateReference`, `BuildAsync`) become one project settings file at a path above the test's own files; a `SqlFile`'s `Dialect` and `Metadata` become a settings file in the file's folder.  `null` output still writes no `output`.  `TestOptionsProvider` keeps its properties and metadata only for the tests of the MSBuild path, which layer 6 deletes with it.
- **Levels:** `SettingLevel.Property` becomes a farther settings file and `SettingLevel.Metadata` a nearer one; the order of the places holds (`SettingPlaces.Specificity`), so the level tests keep their expectations, except the place a diagnostic names ("the typeOverrides of the settings file <path>").
- **The traps the inventory found:** a harness file beats a test's own property or metadata, so every test of a file moves in one commit; a test's own `"root": true` cuts the harness file off; a test's own file at the harness's path; caching tests that count settings-file steps; `GeneratorParityTests`, whose tool side reads real files.
- **The tool:** `TestProject` gets project settings rendered into one `sqlsource.json` merged with what a test adds by `AddSettings`, and `Plans.Postgres` uses it.
- **MSBuild-only tests left for layer 6:** `Generation/MSBuild/`, `TestOptionsProviderTests`, the `SQLSRC011`/`SQLSRC014` tests, the caching tests that swap `AnalyzerConfigOptions`, the manifest format tests of `Property.`/`File.` lines, and the tests of trimming and of a value cut at `;`.
