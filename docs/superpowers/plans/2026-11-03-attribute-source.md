# `[SqlSourceGenerate]` Source and one-file default Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rename the attribute's `Path` to `Source`, and make a type with no `Source` read `<TypeName>.sql` beside its C# file instead of the whole folder.

**Architecture:** `PathResolver.FindFiles`, which the generator and the `sqlsource` tool already share, takes the type's name and owns the default.  The generator passes `TargetType.Name`; the tool reads the declaration's identifier into `TypeClaim` and passes it.  SQLSRC005 changes meaning to "the default file is missing".

**Tech Stack:** C# Roslyn incremental generator (`src/SqlSource`), the `sqlsource` tool (`src/SqlSource.Tool`), xUnit v3 + Shouldly.

**Spec:** `docs/superpowers/specs/2026-11-03-attribute-source-design.md`

## Global Constraints

- The property is `Source`, the constant `AttributeSource.SourceProperty = "Source"`.  Nothing keeps `Path`: no alias, no obsolete property.
- `Source` unset, `null` or `""` is the default.  A string of white space stays a `Source` that matches nothing, as `Path` did.
- The default is `<Name>.sql` in the folder of the C# file that carries the attribute, where `<Name>` is `Identifier.ValueText` of the type itself: no `@`, no generic arity, a nested type's own name.  Matching ignores case (`SqlPath.Comparer`), and the project's spelling of the path is kept.
- SQLSRC005 message, exactly: `No '{0}.sql' beside this file.  Add it, or set Source.` with `{0}` the type's name.  Title: `Type has no SQL file`.
- SQLSRC004 message: `Source '{0}' matches no .sql file.  It is relative to the folder of this file; a value that ends in .sql is one file, and any other value is a folder.`  Title: `Source matches no SQL file`.
- `src/SqlSource/AnalyzerReleases.Unshipped.md` carries both new titles.
- The settings manual anchor `setting-path` becomes `setting-source`; every link to it changes (the link test in `tests/SqlSource.Tests/Documentation/` catches a miss).
- Past plans and specs under `docs/superpowers/` are not edited.
- Every commit passes `./pre-commit-validation.sh`; before any push, `./pre-commit-validation.sh --full` on the exact tree (root `AGENTS.md`, "Commits").
- No `v*` tag exists, so `VersionPrefix` (`0.1.0`) needs no change; re-check with `git fetch --tags origin` before the PR.

## Review Focus

- **A non-ASCII type name in a C# file that is not UTF-8, with no `Source`.**  The compiler on Windows reads that name in the system's code page, so the tool's `<Name>.sql` would not be the generator's.  Expected: the tool reports SQLSRC (`ToolDiagnostics.FileNotUtf8`) at the identifier and makes no claim, exactly as it does today for a non-ASCII `Path`.  Test in Task 2.
- **An escaped identifier**, `class @event` or `class App`.  Expected: `event.sql` and `App.sql`, the same in generator and tool.  Parity case in Task 2.
- **A partial type whose attribute is in another folder than its other part.**  Expected: the folder of the file with the attribute.  Parity case in Task 2.
- **A type at the project root**, `Top.cs` with `Top.sql` beside it.  Expected: found; an empty folder must not produce `/Top.sql` that misses.  `FindFiles` test in Task 2.
- **A `Source = "."` type and a default type in one folder.**  Expected: both get `UserRepository.sql`, no error, as two types sharing a file do today.  Parity case in Task 2.

---

### Task 1: Rename `Path` to `Source`, behaviour unchanged

A pure rename across code, tests, tools and docs, so Task 2's diff shows only the new behaviour.

**Files:**
- Modify: `src/SqlSource/Generation/AttributeSource.cs` (constant and attribute text: property `Source`, same doc comment for now)
- Modify: `src/SqlSource/Generation/TargetType.cs` (parameter `Path` → `Source`, its `<param>`)
- Modify: `src/SqlSource/Generation/TargetTypeReader.cs` (`ReadPath` → `ReadSource`, and the comment "Path is no setting")
- Modify: `src/SqlSource/Generation/Paths/PathResolver.cs` (parameter `path` → `source`, docs)
- Modify: `src/SqlSource/Diagnostics/SqlDiagnostics.cs` (SQLSRC004 title and message per Global Constraints; SQLSRC005 message keeps its meaning but ends `or set Source.`)
- Modify: `src/SqlSource/AnalyzerReleases.Unshipped.md` (SQLSRC004 title)
- Modify: `src/SqlSource.Tool/Planning/AttributeReader.cs`, `TypeClaim.cs`, `ListedFiles.cs` (`Path` → `Source`, `TryReadPath` → `TryReadSource`, comments)
- Modify: every test, fixture and tool source with `Path =` on the attribute: `tests/**/*.cs` (generator, tool, `DatabaseTests`, `SqlSource.Tests/EndToEnd`), `tools/package-install/Queries.cs`, `tests/SqlSource.Testing/Generator/TestModels.cs` (`Type(string? path` → `source`)
- Modify: `docs/manual/settings.md` (section and anchor `setting-source`), `generator.md`, `tool.md`, `recipes.md`, `diagnostics.md` (SQLSRC004 heading/title, 005 and 208 text, every `Path`), `README.md` (links), `docs/architecture/generator.md`, `tool.md`, `README.md`, `src/SqlSource/AGENTS.md`, `src/SqlSource.Tool/Planning/AGENTS.md`, `docs/tech-debt/TD-0031-*.md`, `TD-0097-*.md`, `tests/DatabaseTests/Postgres/README.md`, `tests/DatabaseTests/SqlServer/README.md`

**Interfaces:**
- Produces: `AttributeSource.SourceProperty`; `TargetType.Source` (`string?`); `TypeClaim(string SourcePath, string? Source, OutputKind? Output, LinePosition Position)`; `PathResolver.FindFiles(string sourceFilePath, string? source, EquatableArray<string> sqlPaths)`; `TestModels.Type(string? source = null, …)`.

- [ ] **Step 1: Rename in `src/` and the generator's attribute text.**  Find every use with `grep -rn "PathProperty\|TargetType.*Path\|claim.Path\|\.Path\b" src --include='*.cs'` and skip the unrelated `file.Path` of `AdditionalText`.
- [ ] **Step 2: Rename in tests, fixtures and `tools/`.**  `grep -rln 'SqlSourceGenerate(Path\|Path = ' tests tools --include='*.cs'`; in C# source held in strings too (`"[SqlSourceGenerate(Path = \"…\")]"`), and in expected-message strings (`TypeDiagnosticsTests` lines asserting SQLSRC004/005/208 text, `AttributeReaderTests.PathPrefix`).  Assertions on `claim.Path` become `claim.Source`.
- [ ] **Step 3: Build and run the unit tests.**

Run: `dotnet build && dotnet test --project tests/SqlSource.Tests && dotnet test --project tests/SqlSource.Tool.Tests`
Expected: PASS.  `grep -rn "Path = " tests src tools --include='*.cs' | grep SqlSourceGenerate` prints nothing.

- [ ] **Step 4: Rename in the docs listed above.**  `grep -rn '`Path`\|Path =\|setting-path' README.md src/SqlSource.Tool/README.md docs/manual docs/architecture docs/tech-debt tests/DatabaseTests/*/README.md src/SqlSource/AGENTS.md src/SqlSource.Tool/Planning/AGENTS.md examples/*/README.md` prints nothing afterwards.  Descriptions of the folder default stay as they are; Task 2 rewrites them.
- [ ] **Step 5: Validate and commit.**

Run: `./pre-commit-validation.sh`
Expected: passes (database test projects run, since their attributes changed).

```bash
git add -A && git commit -m "refactor(generator): rename the attribute's Path to Source"
```

---

### Task 2: A type with no `Source` reads `<Name>.sql`

**Files:**
- Modify: `src/SqlSource/Generation/Paths/PathResolver.cs`
- Modify: `src/SqlSource/Diagnostics/SqlDiagnostics.cs` (`FolderHasNoSqlFile` → `TypeHasNoSqlFile`, title and message per Global Constraints), `src/SqlSource/AnalyzerReleases.Unshipped.md`
- Modify: `src/SqlSource/Generation/AttributeSource.cs` (XML doc of `Source`: "A folder or one `.sql` file, relative to the folder of the file that carries the attribute.  The default is the file named for the type, `UserRepository.sql` for `UserRepository`, in that folder; `\".\"` is every `.sql` file of that folder."  The class summary, "for each query in the `.sql` files of the type", stays.)
- Modify: `src/SqlSource.Tool/Planning/TypeClaim.cs`, `AttributeReader.cs` (remarks too), `ListedFiles.cs`
- Modify: `tests/SqlSource.Testing/Generator/GeneratorHarness.cs` (`SampleSource` writes `Source = "."` before the other arguments; its comment says so)
- Modify: every other test, fixture and script that relied on the folder default — find them with `grep -rlnE '\[SqlSource(\.SqlSource)?Generate(Attribute)?(\]|\((Output|SqlLocation|Method|Nested|Collection|Parameters|InputModel|OutputModel|ModelNamespace))' tests tools --include='*.cs' --include='*.sh'` plus C# held in strings; each gets `Source = "."` unless the test is about the default.  Known ones: `tests/SqlSource.Tests/EndToEnd/UserQueries.cs` (comment: "The folder, Source = \".\", nested location"), `tests/SqlSource.Tool.Tests/Fixtures/Projects/SqlServer/Queries/Queries.cs`, `GeneratorParityTests` case "no path" (rename the case "the folder"), `AttributeReaderTests` theory of names (asserts `Source` null, unchanged).
- Modify: `tools/check-tool-install.sh` (rename `Orders.sql` to `Queries.sql`, so the installed tool exercises the default)
- Create: `tests/SqlSource.Tests/EndToEnd/Named/NamedQueries.cs` and `Named/NamedQueries.sql` (bare `[SqlSourceGenerate(SqlLocation = SqlLocation.Direct)]` on `internal static partial class NamedQueries`, one query `GetNamed`); a subfolder so `UserQueries`' `Source = "."` does not take it.
- Test: `tests/SqlSource.Tests/Generation/Paths/PathResolverTests.cs`, `tests/SqlSource.Tests/Generator/TypeDiagnosticsTests.cs`, `tests/SqlSource.Tests/EndToEnd/EndToEndTests.cs`, `tests/SqlSource.Tool.Tests/Planning/AttributeReaderTests.cs`, `tests/SqlSource.Tool.Tests/Planning/GeneratorParityTests.cs`
- Docs: `README.md` getting started (step 1 saves `Queries/UserRepository.sql`; step 2's sentence: "The type gets the `.sql` file named for it, `UserRepository.sql`, in the folder of its C# file."  The "More:" link says "for a folder, another file, or methods on the type itself"), `docs/manual/settings.md` (the `Source` section: the default, `"."`, and the SQLSRC004/005 line), `docs/manual/generator.md` (line ~81 rules, "A `.sql` file that no type claims"), `docs/manual/recipes.md`, `docs/manual/tool.md`, `docs/manual/diagnostics.md` (SQLSRC005 section rewritten: what it means, an `error` sample, the fix "add `UserRepository.sql` beside the file, or set `Source`, `\".\"` for every file of the folder"; the SQLSRC686-area line "point the type's `Source` somewhere else"; the FileNotUtf8 section also covers a type name), `docs/architecture/generator.md` and `tool.md` (how `FindFiles` builds the default, and that the tool reads the type's identifier), `src/SqlSource/AGENTS.md` (rule: resolving depends only on the list of paths and the type's name), `src/SqlSource.Tool/Planning/AGENTS.md` (reads `Source`, `Output` and the type's identifier), `docs/tech-debt/TD-0031-*.md` (what the reader reads).

**Interfaces:**
- Consumes: Task 1's names.
- Produces: `PathResolver.FindFiles(string sourceFilePath, string? source, string typeName, EquatableArray<string> sqlPaths)`; `SqlDiagnostics.TypeHasNoSqlFile` (SQLSRC005, one argument, the type's name); `TypeClaim(string SourcePath, string TypeName, string? Source, OutputKind? Output, LinePosition Position)`.

- [ ] **Step 1: Write the failing resolver tests** in `PathResolverTests`.  `SqlPaths` gains `"app/Repo/UserRepository.sql"` and `"app/Repo/userrepository2.SQL"`; `TestModels.Type()`'s type is `UserRepository` at `/app/Repo/UserRepository.cs`.

```csharp
[Fact]
public void Resolve_NoSource_TakesTheFileNamedForTheType() =>
    PathResolver.Resolve(TestModels.Type(), SqlPaths, true, null).Files.ShouldBe(["app/Repo/UserRepository.sql"]);

[Fact]
public void Resolve_SourceDot_TakesTheFilesOfTheFolder() =>  // the old Resolve_NoPath_… expectations, plus UserRepository.sql
    PathResolver.Resolve(TestModels.Type("."), SqlPaths, true, null).Files.ShouldBe([
        "app/Repo/Count.sql", "app/Repo/UserRepository.sql", "app/Repo/userrepository2.SQL", GeneratorHarness.UsersNormalizedPath,
    ]);

[Theory]
[InlineData("/app/Empty/UserRepository.cs")]
[InlineData("UserRepository.cs")]
[InlineData("")]
public void Resolve_NoSourceAndNoFileNamedForTheType_IsAnErrorAtTheAttribute(string filePath) =>
    PathResolver.Resolve(TestModels.Type(filePath: filePath), SqlPaths, true, null).Diagnostics.ShouldBe([
        DiagnosticInfo.Create(SqlDiagnostics.TypeHasNoSqlFile, TestModels.AttributeLocation, "UserRepository"),
    ]);

[Theory]
[InlineData("/app/Repo/A.cs", "UserRepository", "app/Repo/UserRepository.sql")]
[InlineData("/app/Repo/A.cs", "USERREPOSITORY2", "app/Repo/userrepository2.SQL")]
[InlineData("/app/Root.cs", "Root", "app/Root.sql")]
[InlineData("/app/Repo/A.cs", "Orders", "")]
[InlineData("/app/Repo/A.cs", "Admin", "")]   // a folder of that name is not the file
public void FindFiles_NoSource_GivesTheFileNamedForTheType(string sourceFile, string name, string expected) =>
    string.Join('|', PathResolver.FindFiles(sourceFile, null, name, SqlPaths)).ShouldBe(expected);

[Fact]
public void FindFiles_NoSourceAtTheProjectRoot_FindsTheFileThere() =>
    PathResolver.FindFiles("/Top.cs", null, "Top", TestSidecars.Of("Top.sql")).ShouldBe(["Top.sql"]);
```

Existing tests: `FindFiles_PathOfAnAttribute_GivesTheFilesItNames` passes `"UserRepository"` and its `null` row expects `app/Repo/UserRepository.sql`; `Resolve_SourceFileWithWindowsPath_MatchesTheSameFolder` uses `"."`.  The old `Resolve_NoPath_…` and `…NoSqlFileInTheFolder…` tests are replaced by the ones above.

- [ ] **Step 2: Run them to see them fail.**

Run: `dotnet test --project tests/SqlSource.Tests -- --filter-class "*PathResolverTests"`
Expected: FAIL (compile error: no `FindFiles` overload with four arguments, no `TypeHasNoSqlFile`).

- [ ] **Step 3: Implement.**  `FindFiles` takes `typeName`; when `source` is null it looks up `SqlPath.Combine(folder, typeName + ".sql")` the way it looks up a named file today (`SqlPath.IndexOf`, project's spelling kept), and `"."` falls through the folder branch unchanged.  Check that `Combine` with an empty folder normalises to `Top.sql`; if not, use the name alone for an empty folder.  `Resolve` passes `type.Name` and reports `TypeHasNoSqlFile` with `type.Name` when `type.Source` is null.  Rename the descriptor and update `AnalyzerReleases.Unshipped.md`.  `ListedFiles.ClaimedBy` passes `claim.TypeName`.

- [ ] **Step 4: Write the failing tool tests** in `AttributeReaderTests`.

```csharp
[Theory]
[InlineData("internal static partial class Queries;", "Queries")]
[InlineData("internal partial class Repo<T>;", "Repo")]
[InlineData("internal partial class @event;", "event")]
[InlineData("internal partial class \\u0041pp;", "App")]
[InlineData("internal partial record struct Point;", "Point")]
public void Read_Type_GivesItsName(string declaration, string expected) =>
    Read($"using SqlSource;\n\n[SqlSourceGenerate]\n{declaration}\n").Claims.ShouldHaveSingleItem().TypeName.ShouldBe(expected);

[Fact]
public void Read_NestedType_GivesItsOwnName() =>
    Read("using SqlSource;\n\ninternal partial class Outer\n{\n    [SqlSourceGenerate]\n    private partial class Inner;\n}\n")
        .Claims.ShouldHaveSingleItem().TypeName.ShouldBe("Inner");

[Fact]
public void Read_NonAsciiNameWithNoSourceInAFileThatIsNotUtf8_IsAnErrorAndNoClaim()
{
    var claims = ReadLatin1("using SqlSource;\n\n[SqlSourceGenerate]\ninternal partial class Café;\n");
    claims.Claims.ShouldBeEmpty();
    // ToolDiagnostics.FileNotUtf8 at the identifier: line 3, column 23 (zero-based).
    claims.Errors.ShouldHaveSingleItem().Descriptor.ShouldBe(ToolDiagnostics.FileNotUtf8);
}

[Fact]
public void Read_NonAsciiNameWithASourceInAFileThatIsNotUtf8_IsAClaim() =>
    ReadLatin1("using SqlSource;\n\n[SqlSourceGenerate(Source = \".\")]\ninternal partial class Café;\n").Claims.ShouldHaveSingleItem();
```

Match the existing tests' way of asserting a diagnostic's descriptor and position (see the current non-UTF-8 `Path` test in the same file).

- [ ] **Step 5: Write the failing parity cases** in `GeneratorParityTests.Cases`, each with the count of claims and the file list, as the existing cases do:
  - `"the default, each type its own file"`: `Repo/Types.cs` with `UserRepository`, `OrderRepository` and `Repo<T>`; files `Repo/UserRepository.sql`, `Repo/orderrepository.SQL`, `Repo/Repo.sql`, `Repo/Other.sql`.
  - `"the default, nested and escaped"`: `Outer` with nested `[SqlSourceGenerate] Inner`, and `@event`; files `Inner.sql`, `Outer.sql`, `event.sql`.
  - `"the default, the attribute in one part of a partial type"`: `Repo/UserRepository.cs` (no attribute) and `Repo/Queries/UserRepository.Queries.cs` (attribute); files `Repo/UserRepository.sql`, `Repo/Queries/UserRepository.sql`.  Both sides claim `Repo/Queries/UserRepository.sql` alone.
  - `"the folder and the default in one folder"`: `[SqlSourceGenerate(Source = ".")] All` and `[SqlSourceGenerate] UserRepository` in `Repo/Types.cs`; files `Repo/UserRepository.sql`, `Repo/Orders.sql`.
  - `"the default with no file"`: `[SqlSourceGenerate] Missing`; files `Repo/Other.sql`.  Both claim nothing.

- [ ] **Step 6: Run them to see them fail.**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "*AttributeReaderTests" --filter-class "*GeneratorParityTests"`
Expected: FAIL (`TypeClaim` has no `TypeName`).

- [ ] **Step 7: Implement in the tool.**  `ReadAttribute` takes the `TypeDeclarationSyntax`, reads `Identifier.ValueText` into `TypeClaim.TypeName`, and, when no `Source` was read and `compilerFallsBack` and `Identifier.Text` has a character above `\u007F`, adds `FileNotUtf8` at `StartOf` the identifier token and makes no claim.  Update the class remarks.

- [ ] **Step 8: Move the folder-default tests and fixtures to `Source = "."`** (files listed above), rename `tools/check-tool-install.sh`'s `Orders.sql`, and add `EndToEnd/Named/` with an assertion in `EndToEndTests`: `NamedQueries.GetNamed.ShouldBe(<its SQL>)`.  `TypeDiagnosticsTests`' SQLSRC005 test asserts the new message: `"SQLSRC005 /app/Repo/Sample.cs(3,2)-(3,19): No 'Sample.sql' beside this file.  Add it, or set Source."` — that test's source is the bare attribute, not `SampleSource`.

- [ ] **Step 9: Run the unit tests.**

Run: `dotnet test --project tests/SqlSource.Tests && dotnet test --project tests/SqlSource.Tool.Tests && dotnet test --project tests/SqlSource.Tests.RoslynFloor`
Expected: PASS.

- [ ] **Step 10: Update the docs listed above.**  Afterwards, `grep -rn "every .sql file in the folder\|folder of its C# file\|folder of the source file" README.md docs/manual docs/architecture` shows only text that describes `Source = "."`.

- [ ] **Step 11: Validate and commit.**

Run: `./pre-commit-validation.sh`
Expected: passes, including the diagnostics-docs test and the documentation link test.

```bash
git add -A && git commit -m "feat(generator): a type with no Source reads the .sql file named for it"
```

---

### Task 3: Examples, one file per class

Both `examples/PostgresExamples` and `examples/SqlServerExamples`, mirrored.

**Files:**
- `CodeGen/`: replace `StoreQueries.cs` with `UserQueries.cs`, `OrderLineQueries.cs`, `DeliveryQueries.cs`, each `[SqlSourceGenerate] public sealed partial class XQueries(DbConnection connection)` holding the wrapper methods of its file's queries, with the existing comments moved beside them.  `git mv` `Users.sql` → `UserQueries.sql`, `OrderLines.sql` → `OrderLineQueries.sql`, `Deliveries.sql` → `DeliveryQueries.sql`, and their `.sql.json` sidecars.  Postgres: `CreateDataSource` and its comment move to `CodeGen/StoreDataSource.cs`, `public static class StoreDataSource`.  Per-file headers (`-- type-override:`) move with their file.
- `Models/`: keep its one class, `StoreQueries`, with `[SqlSourceGenerate(Source = ".", Output = GeneratorOutput.Models)]`, and a comment saying `Source = "."` gives it every `.sql` file of its folder.
- `Sql/`: `git mv Counts.sql CountQueries.sql`.
- `README.md` of each example project: every class and file name, the `StoreQueries` usage sample (`new UserQueries(connection)` etc., `StoreDataSource.CreateDataSource`), the generated-files paragraph (`UserQueriesExtensions`, …), and `DapperSetup.cs`'s comment that names `StoreQueries.CreateDataSource`.
- `examples/AGENTS.md` if it names any renamed file.

**Interfaces:**
- Consumes: Task 2's behaviour.

- [ ] **Step 1: Restructure both projects as above.**
- [ ] **Step 2: Build them.**

Run: `dotnet build examples/PostgresExamples && dotnet build examples/SqlServerExamples`
Expected: builds with no SQLSRC errors.  A sidecar moved by `git mv` still matches its file, since sidecars are keyed by their own file name.

- [ ] **Step 3: Rewrite the sidecars.**

Run: `tools/describe-end-to-end.sh` (Docker running)
Expected: completes; `git status examples` shows only the expected renames and sidecar updates.

- [ ] **Step 4: Validate and commit.**

Run: `./pre-commit-validation.sh`
Expected: passes.

```bash
git add -A && git commit -m "docs(examples): one .sql file per class, and Source = \".\" for a folder"
```

---

### Task 4: Close the branch

- [ ] **Step 1: Run the branch review** (`branch-review:review`), fix every finding, and repeat until clean (root `AGENTS.md`, "Pull requests").
- [ ] **Step 2: Full validation** on the exact tree to be pushed.

Run: `./pre-commit-validation.sh --full`
Expected: passes.  No describer changed, so no floor-image run is needed.

- [ ] **Step 3: Offer the pull request**, following `CLAUDE.local.md`: bring the branch up to date with `main` before pushing, and turn on auto-fix.
