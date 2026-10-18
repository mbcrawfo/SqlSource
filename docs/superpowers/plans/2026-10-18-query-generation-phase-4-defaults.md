# Query generation phase 4 - defaults Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every project and every test in the repository that runs the generator states its `SqlSourceOutput`, so that phase 5's default, `CodeGen` under `ansi`, breaks nothing that exists.

**Architecture:** The output becomes a required `OutputKind?` parameter of `TestOptionsProvider` and of every entry point of `GeneratorHarness`, so a call that forgets it does not compile; `null` means "unset" and is kept for the tests of the default.  The two projects built with the generator set the project property `SqlSourceOutput` to `Sql`.  No generator, tool or package code changes.

**Tech Stack:** C#, Roslyn incremental generator test harness (Roslyn 4.8.0 floor, C# 12), xUnit v3, Shouldly, MSBuild.

**Spec:** `docs/superpowers/specs/2026-10-18-query-generation-phase-4-defaults-design.md`

## Global Constraints

- No change to `src/`, to the user documentation, or to any assertion, expected file or generated output.  The only edits are settings, the harness's parameter, the call sites, one guidance rule and the outline's row.
- The harness must not supply `Sql` on a test's behalf: no default value for the output parameter anywhere, helpers of a test class included.
- The output is never spelled in a test's C# or SQL text (no `Output =` on the attribute, no `-- output:` marker) and never as per-file metadata.
- Files under `tests/SqlSource.Testing/Generator/` and `tests/SqlSource.Tests/Generator/` are compiled on Roslyn 4.8.0 too: Roslyn 4.8.0 API only, C# 12 at most.
- `Microsoft.CodeAnalysis.OutputKind` exists.  In a file that imports `Microsoft.CodeAnalysis` and names the generator's enum, add `using OutputKind = SqlSource.Settings.OutputKind;`; in other files, `using SqlSource.Settings;`.  `GeneratorHarness`'s own `CSharpCompilationOptions` then spells `Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary`.
- Comments name no phase.
- Every commit passes `./pre-commit-validation.sh`, run after the last edit (root `AGENTS.md`, Commits).

## Review Focus

1. A test that updates the driver's options (`CachingTests`, `WithUpdatedAnalyzerConfigOptions`) and builds the new `TestOptionsProvider` without the output would silently fall back to the default on the second run.  Every provider a test builds states it (Task 1).
2. A test that passes an output and also a `SqlSourceOutput` entry in `properties` must get the `properties` value, as `properties` already overrides the dialect (Task 1 test).
3. `tools/package-install/Queries/AddedByATarget.sql` is added by a `BeforeBuild` target, after evaluation; only a project property, not item metadata, reaches it (Task 2, proven by Task 3's check).
4. The Roslyn-floor project compiles the same harness and tests on Roslyn 4.8.0, where an alias or API that the current Roslyn accepts may not compile (Task 1, `dotnet build` of the solution).
5. `GeneratorParityTests` in `tests/SqlSource.Tool.Tests` uses the harness from another project and passes the parse options positionally today; the reordered `CreateDriver` must not take them as the provider (Task 1).

---

### Task 1: The harness requires the output, and every test states it

**Files:**
- Modify: `tests/SqlSource.Testing/Generator/TestOptionsProvider.cs`
- Modify: `tests/SqlSource.Testing/Generator/GeneratorHarness.cs`
- Create: `tests/SqlSource.Tests/Generation/MSBuild/TestOptionsProviderTests.cs`
- Modify: every file under `tests/SqlSource.Tests/Generator/` (nine test files)
- Modify: `tests/SqlSource.Tests/Generation/MSBuild/MSBuildSettingsTests.cs`, `tests/SqlSource.Tests/Generation/MSBuild/DialectSettingTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Planning/GeneratorParityTests.cs`
- Modify: `tests/SqlSource.Tests/Generator/AGENTS.md`

**Interfaces:**
- Produces (exact signatures; the rest of each parameter list is as today):
  - `TestOptionsProvider(OutputKind? output, string? generatorParameters, string? dialect = null, IReadOnlyDictionary<string, string>? fileDialects = null, IReadOnlyDictionary<string, string?>? properties = null, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? fileMetadata = null)`
  - `GeneratorHarness.Run(OutputKind? output, string source, params SqlFile[] sqlFiles)`
  - `GeneratorHarness.Run(SourceFile[] sources, SqlFile[] sqlFiles, OutputKind? output, bool supportedFramework = true, LanguageVersion languageVersion = LanguageVersion.CSharp12, MetadataReference[]? references = null, string? generatorParameters = null, string? dialect = null, IReadOnlyDictionary<string, string?>? properties = null)`
  - `GeneratorHarness.BuildAsync(OutputKind? output, string source, SqlFile[] sqlFiles, MetadataReference[] references, bool warningsAsErrors = false, bool packageAnalyzers = true)`
  - `GeneratorHarness.CreateReference(OutputKind? output, string assemblyName, string source)`
  - `GeneratorHarness.CreateDriver(IEnumerable<AdditionalText> sqlFiles, TestOptionsProvider options, CSharpParseOptions? parseOptions = null)`: the options are required and come before the parse options; the fallback `new TestOptionsProvider(null)` is removed.

- [ ] **Step 1: Write the failing tests** in `tests/SqlSource.Tests/Generation/MSBuild/TestOptionsProviderTests.cs`, class `TestOptionsProviderTests`, namespace `SqlSource.Tests.Generation.MSBuild`.  The key is spelled out, `"build_property.SqlSourceOutput"`, as the provider's own comment requires.

```csharp
[Theory]
[InlineData(OutputKind.Sql, "Sql")]
[InlineData(OutputKind.Models, "Models")]
[InlineData(OutputKind.CodeGen, "CodeGen")]
public void GlobalOptions_Output_IsTheProjectPropertySpelledAsAUserWritesIt(OutputKind output, string spelled)
{
    new TestOptionsProvider(output, null).GlobalOptions
        .TryGetValue("build_property.SqlSourceOutput", out var value).ShouldBeTrue();
    value.ShouldBe(spelled);
}

[Fact]
public void GlobalOptions_NoOutput_LeavesThePropertyUnset() =>
    new TestOptionsProvider(null, null).GlobalOptions
        .TryGetValue("build_property.SqlSourceOutput", out _).ShouldBeFalse();

[Fact]
public void GlobalOptions_OutputAndAPropertyOfTheSameName_IsTheProperty()
{
    var options = new TestOptionsProvider(
        OutputKind.Sql, null, properties: new Dictionary<string, string?> { ["SqlSourceOutput"] = "models" });
    options.GlobalOptions.TryGetValue("build_property.SqlSourceOutput", out var value).ShouldBeTrue();
    value.ShouldBe("models");
}

[Theory]
[InlineData(OutputKind.Sql)]
[InlineData(OutputKind.Models)]
[InlineData(OutputKind.CodeGen)]
public void ReadProject_OutputOfTheProvider_IsReadAsThatOutput(OutputKind output) =>
    ProjectSettings.Read(new TestOptionsProvider(output, null).GlobalOptions).Level.Output.ShouldBe(output);
```

- [ ] **Step 2: Run the build to verify it fails**

Run: `dotnet build tests/SqlSource.Tests`
Expected: FAIL with CS1503 or CS1739 at the new tests (the provider's first parameter is `string?` today).

- [ ] **Step 3: Implement the parameter in `TestOptionsProvider` and `GeneratorHarness`**

`CreateGlobal` writes `PropertyPrefix + "SqlSourceOutput"` from a `switch` over the three members, `"Sql"`, `"Models"`, `"CodeGen"`, and `null` for `null`, before the `properties` loop so that `properties` wins.  The comment at the head of the provider says that a null output is a project that does not set it.  The harness's entry points pass their `output` to the provider they build; `BuildAsync` and `CreateReference` now build one (`new TestOptionsProvider(output, null)`) where they called `CreateDriver` with none.  Update the comment above `CreateDriver`, which says "Without options the project sets none of the package's properties".

- [ ] **Step 4: Update every call site**

Each call states its output.  By file:

| File | Output |
|----|----|
| `AttributeConflictTests`, `DialectTests`, `FileDiagnosticsTests`, `GeneratedSourceTests`, `GeneratorParameterLevelTests`, `TokenValidationTests`, `TypeDiagnosticsTests` | `OutputKind.Sql` on every run |
| `CachingTests` | `OutputKind.Sql` on every `TestOptionsProvider` it builds, the first run's and each `WithUpdatedAnalyzerConfigOptions` update alike |
| `SettingLevelTests` | `null` on every run, plain and set, with a comment on the class that its runs leave the output unset because its subject is the output's levels |
| `MSBuildSettingsTests`, `DialectSettingTests` | `null`: they read levels and never run the generator; their empty-level assertions stay |
| `GeneratorParityTests` | `CreateDriver(files, new TestOptionsProvider(null, null), options)`: `null`, since it compares the generator with the tool over the same inputs and the tool's side sets no property; the parse options move to the third argument |

A helper of a test class that runs the generator takes the output as a required parameter, first, from the test that calls it, and passes it on:
- `DialectTests.Run(OutputKind? output, string? property, params SqlFile[] files)`
- `TokenValidationTests.Run(OutputKind? output, string? property, SqlFile file)`
- `GeneratorParameterLevelTests.Run(OutputKind? output, string? property, string? attribute)`
- `CachingTests.FirstRun(OutputKind? output, Compilation compilation)`, and `FirstRun(Compilation compilation, InMemoryAdditionalText users, TestOptionsProvider options)` with the options required.

A test whose subject turns out to be the output itself, other than those above, passes `null` and says why in a comment; list any such test in the pull request's description.

- [ ] **Step 5: Add the guidance rule** to `tests/SqlSource.Tests/Generator/AGENTS.md`, as a bullet after "Only test classes live here": every run of the generator states its output through the harness, whose output parameter has no default; `OutputKind.Sql` is the norm; `null` leaves `SqlSourceOutput` unset and is for a test of the default or of the output's levels, which says so in a comment.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet build SqlSource.slnx && dotnet test --solution SqlSource.slnx --no-build`
Expected: build succeeds (the Roslyn-floor project included), every test passes, and the test count is the old count plus the eight new cases.

- [ ] **Step 7: Validate and commit**

Run: `./pre-commit-validation.sh`
Expected: every step `passed`.

```bash
git add tests/SqlSource.Testing tests/SqlSource.Tests tests/SqlSource.Tool.Tests
git commit -m "Make every generator test state its output through the harness"
```

---

### Task 2: The two projects built with the generator set the output

**Files:**
- Modify: `tests/SqlSource.Tests/SqlSource.Tests.csproj` (the `PropertyGroup` that sets `SqlSourceDialect`, lines 18-24)
- Modify: `tools/package-install/Consumer.csproj` (the `PropertyGroup` that sets `SqlSourceDialect`, lines 21-23)

**Interfaces:**
- Consumes: nothing from Task 1.
- Produces: the project property `SqlSourceOutput` = `Sql` in both projects, which Task 3's check relies on.

- [ ] **Step 1: Add the property** `<SqlSourceOutput>Sql</SqlSourceOutput>` beside `SqlSourceDialect` in each project, with a comment that every query of the project is generated as SQL alone, and that a query with models would need a sidecar.  A project property, never metadata on an `AdditionalFiles` item.

- [ ] **Step 2: Run the affected checks to verify nothing changed**

Run: `dotnet test --project tests/SqlSource.Tests && tools/check-package-install.sh`
Expected: all tests pass, `EndToEndTests` among them with its list of per-file metadata unchanged, and the package-install check passes with `tools/package-install/expected-output.txt` unchanged.

- [ ] **Step 3: Validate and commit**

Run: `./pre-commit-validation.sh`
Expected: every step `passed`.

```bash
git add tests/SqlSource.Tests/SqlSource.Tests.csproj tools/package-install/Consumer.csproj
git commit -m "Set SqlSourceOutput to Sql in the projects that the generator builds"
```

---

### Task 3: Prove the change protects phase 5, and close the phase

**Files:**
- Temporarily modify, then revert: `src/SqlSource/Generation/Emitting/TypeEmitter.cs` (after the call to `QuerySettings.Resolve`, about line 155)
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (the phase-4 row of the phase table, line 77)

**Interfaces:**
- Consumes: Tasks 1 and 2.

- [ ] **Step 1: Make the default fail loudly, locally only.**  After `QuerySettings.Resolve` in `TypeEmitter`, throw `new InvalidOperationException("output is not Sql")` when the resolved `Output` is not `OutputKind.Sql`.  Do not commit this.

- [ ] **Step 2: Run everything that runs the generator**

Run: `dotnet build SqlSource.slnx; dotnet test --solution SqlSource.slnx --no-build; tools/check-package-install.sh`
Expected: the solution builds, so the `EndToEnd` project's own build produced its constants; the package-install check passes; the only failing tests are those of `SettingLevelTests` whose output resolves to `Models` or `CodeGen` and, if it reaches emission, `GeneratorParityTests`.  Any other failure is a call site, project or file that still relies on the default: fix it in Task 1's or Task 2's manner and run again.

- [ ] **Step 3: Revert the generator**

Run: `git checkout -- src/SqlSource/Generation/Emitting/TypeEmitter.cs && git status --short src`
Expected: no output.  Note for the pull request's description that the check ran and which tests failed under it.

- [ ] **Step 4: Mark the phase done** in the outline's phase table: the phase-4 row's status `Specified` becomes `Done`; the spec link and the "Delivers" text stay.

- [ ] **Step 5: Validate and commit**

Run: `./pre-commit-validation.sh`
Expected: every step `passed`.

```bash
git add docs/superpowers/specs/2026-10-07-query-generation-epic-design.md
git commit -m "Close phase 4 of query generation in the epic outline"
```
