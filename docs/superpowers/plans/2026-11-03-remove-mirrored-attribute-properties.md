# Remove the Mirrored Attribute Properties Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `[SqlSourceGenerate]` loses `Output`, `Parameters`, `InputModelSuffix`, `OutputModelSuffix`, `ModelNamespace`, `InputModelType`, `OutputModelType` and `CollectionType`.  Those settings come from markers and settings files alone, in the generator and in the tool.

**Architecture:** The attribute stops being a level of these settings.  `QuerySettings.Resolve` drops its attribute argument, the generator's pipeline step that collected files whose attribute asked for `keep-comments` goes, and the tool's `AttributeReader` reads `Source` and the type's name alone.  `SqlLocation`, `MethodLocation` and `NestedClass` stay on the attribute until pull request 2.

**Tech Stack:** C# / Roslyn incremental source generator (netstandard2.0), the `sqlsource` .NET tool, xUnit v3 with Microsoft Testing Platform, Shouldly.

**Spec:** `docs/superpowers/specs/2026-11-03-file-owned-settings-design.md`, section "Pull request 1".

## Global Constraints

- The branch starts from `main` after pull requests A and B of the spec have merged, since the database test projects must no longer claim one file twice with different settings.
- This plan is committed with the work (`docs/superpowers/AGENTS.md`).  Take it from the local branch `claude/file-owned-settings` with `git checkout claude/file-owned-settings -- <path>` if it is not on `main`.
- Breaking change, nothing released: no alias, no `[Obsolete]` property, and no diagnostic for a removed property beyond the compiler's own.
- `SqlLocation`, `MethodLocation`, `NestedClass` and `Source` stay, with their enums `SqlLocation` and `MethodLocation`.
- `AttributeSource.Text` stays C# 7.3: no `#nullable`, no `string?`.
- Every commit passes `./pre-commit-validation.sh`, which always builds the whole solution, examples and database test projects included; the branch passes `./pre-commit-validation.sh --full` before it is pushed (root `AGENTS.md`, "Commits").  So nothing may still write a removed property when the commit that removes it lands: Tasks 1 and 2 move every user off first, while the properties still exist.
- Sidecars are written only by `tools/describe-end-to-end.sh`, and their diff is read before it is committed.
- Docs stay true in the same pull request: user docs describe effects, never internals (root `AGENTS.md` rule 4).
- `VersionPrefix` 0.1.0 and no `v*` tag: no version change.
- Past specs and plans under `docs/superpowers/` are records and are not edited.

## Review Focus

1. **A file that two types claim** now gives both of them its own `-- output:`.  The generator and the tool must agree on which queries need describing: Task 4's parity test pins it.
2. **`keep-comments` written only in a settings file of a parent folder, or only in a header,** must still keep comments once the attribute's comment step is gone: Task 3's caching and comment tests pin it.
3. **User code that still writes `Output = GeneratorOutput.Sql`** gets the compiler's error, and the tool must not crash on it.  It ignores the argument, literal or not: Task 4 pins it.
4. **A settings file's `output` under a file header's `-- output:`**: the header still wins, and nothing in between now wins over it: Task 3 pins it.
5. **An invalid `NestedClass` on the attribute** is still SQLSRC006 after the other cases go: Task 3 keeps its test.

---

### Task 1: Move the generator's unit tests off the removed properties

The tests switch to markers and settings files, which work today, so this task is green on the current code.

**Files:**
- Modify: every file of `tests/SqlSource.Tests/` (its `EndToEnd/` included) and `tests/SqlSource.Testing/` that writes one of the eight properties or their enums.  Find them with both searches; the first alone misses the cast forms and the level rows:
  ```bash
  grep -rnE '(Output|Parameters|InputModelSuffix|OutputModelSuffix|ModelNamespace|InputModelType|OutputModelType|CollectionType) = (GeneratorOutput|GeneratorModelType|GeneratorCollectionType|\\"|")' tests/SqlSource.Tests tests/SqlSource.Testing
  grep -rnE 'GeneratorOutput|GeneratorModelType|GeneratorCollectionType|SettingLevel\.Attribute|LevelSettings' tests/SqlSource.Tests tests/SqlSource.Testing
  ```
  Today the first gives 73 hits in 26 files.  The second adds `LevelSettings.cs` and the `SettingLevel.Attribute` theory rows, the casts such as `(GeneratorOutput)7` in `TypeDiagnosticsTests` and `SettingLevelTests`, and `AttributeConflictTests`.
- Modify: `tests/SqlSource.Testing/Generator/GeneratorHarness.cs`.  Its `SampleSource(string? arguments)` comment stops giving `Parameters = "keep-comments"` as its example.  Use `NestedClass = \"Queries\"` or a `Source`.
- Modify: `tests/SqlSource.Testing/Generator/CachingScene.cs`.

**Interfaces:**
- Produces: no test writes one of the eight properties or their enums except those that test the attribute level itself: the `SettingLevel.Attribute` rows, the casts that test SQLSRC006 for a removed property, and their helpers.  Task 3 deletes those.  Each such test gets the comment `// Removed with the attribute level in Task 3.`

- [ ] **Step 1: Rewrite each site.**  A setting for every query of a test's file becomes a header marker in that `.sql` text: `-- output: sql`, `-- generator: keep-comments`, `-- input-model-suffix: Args`, `-- model-namespace: App.Models`, `-- output-model-type: class`, `-- collection-type: list`.  A setting meant for a whole test project becomes the harness's settings file.  The marker spellings are those of the marker index in `docs/manual/sql-files.md`.
  - **`SharedModelTests` two claims of one file:** the claims at lines 63 and 66 (`CodeGen` and `Models`) and at 321/354 (`OutputModelType = Class` against none) test shared models across differing claims.
    - The `CodeGen`/`Models` pair keeps its point (one model, two types) with no `Output` on either claim.
    - The model-type pairs test that two types disagreeing on a model's shape is reported.  Once the attribute level is gone, one file cannot disagree with itself.  Keep the test with two files whose headers set different `-- output-model-type:` and whose queries resolve to one shared model name, if the generator can still reach that conflict.  Otherwise mark it for deletion in Task 3, and say why in the commit message.
- [ ] **Step 2: Run the unit tests.**
  Run: `dotnet test --project tests/SqlSource.Tests`
  Expected: PASS, with the same test count as before, give or take the `SharedModelTests` cases from Step 1.
- [ ] **Step 3: Validate and commit.**
  Run: `./pre-commit-validation.sh`, expected PASS.
  ```bash
  git commit -am "test: write settings as markers, not attribute properties"
  ```

### Task 2: Database tests, examples and tools write markers

Also green on the current code: markers and settings files already work.

**Files:**
- Modify: every remaining site in `tests/DatabaseTests/`, `tests/SqlSource.Tool.Testing/` and `tests/SqlSource.Tool.Tests/`, found by Task 1's two searches run over those folders.  After pull requests A and B, check each claim on the merged code:
  - `ShapeQueries.cs` exists in three projects (Postgres end-to-end, SQL Server end-to-end and `QueryMethodAllocation/SqlServer`), and each sets five removed properties: `OutputModelType = Record`, `OutputModelSuffix = "View"`, `InputModelType = SealedClass`, `ModelNamespace` and `CollectionType = List`.  (Checked on the merged code; this plan first named only the last two.)  The three model settings move to `-- output-model-type: record`, `-- output-model-suffix: View` and `-- input-model-type: sealed-class` in each `Shapes.sql` header.
  - `CollectionType = List` differs by engine.  SQL Server's `Shapes.sql` already sets `-- collection-type: IReadOnlyList` in its header, which wins over the attribute today, so it is dropped there: a second header marker would be SQLSRC112.  PostgreSQL's `Shapes.sql` sets the collection type only on `SearchAccounts`, and its header says the others take the attribute's, so it moves to `-- collection-type: List` in that header, and the header's comment changes.
  - Any other single claim moves its setting to a header marker in its `.sql` file, or to a query marker where the file's other queries must not take it.
- Modify: the claims of SQL Server's `Shapes.sql` from two projects, SQL Server end-to-end and `QueryMethodAllocation` (which lists it in its `.csproj`), that differ only by `ModelNamespace`.  The namespace moves to a `-- model-namespace:` header marker only if both projects can take one namespace (a namespace in another assembly compiles, and `QueryMethodAllocation/SqlServer/AllocationTests.cs`'s `using` follows).  Otherwise, drop it from the project whose tests do not depend on it, and update that project's `using`s.  PostgreSQL's `Shapes.sql` has one claim, so its namespace moves to its header.
- Modify: comments that say a setting comes from the attribute, in `.sql` files and settings files as well as in C#: `tests/DatabaseTests/Postgres/EndToEnd/Queries/Shapes.sql`, `tests/DatabaseTests/SqlServer/EndToEnd/Queries/Shapes.sql` (`SearchAccountsUnpooled`'s summary), `examples/*/Models/Orders.sql` and the other `.sql` files under `examples/*/Models/`, `examples/*/Sql/CountQueries.sql`, and `tests/DatabaseTests/SqlServer/sqlsource.json`'s collection-type comment.  Find them with `grep -rniE 'attribute' --include='*.sql' --include='sqlsource.json' tests examples tools`.  A changed summary may change a sidecar: Step 2 re-describes.
- Keep: `tests/SqlSource.Tool.Tests/Realistic/RealisticClaimsTests.cs`'s row `"Output = GeneratorOutput.CodeGen"`.  It is source text that `RealisticClaims.Parse` must refuse, not a compiled use.
- Modify: `examples/PostgresExamples/Models/StoreQueries.cs` and `examples/SqlServerExamples/Models/StoreQueries.cs`.  Drop `Output`, and add `Models/sqlsource.json` in each project with `{ "output": "models" }` and a comment saying that it gives the folder's files models.  The `$schema` line follows the project's root settings file.
- Modify: `examples/*/Sql/CountQueries.cs`.  Drop `Output`, add `-- output: sql` to `CountQueries.sql`'s header, and update the class comment.
- Modify: `examples/AGENTS.md` rule 4 ("whose classes' `[SqlSourceGenerate]` attribute sets the folder's output") and both `examples/*/README.md`'s folder paragraph ("Each class's `[SqlSourceGenerate]` attribute sets its folder's output").  The output now comes from `Sql/`'s file header and `Models/`'s settings file.
- Modify: `tools/package-install/`.  `Users.sql` is claimed twice: by `Queries` (`Source = "Queries"`) and by `KeptQueries` (with `Parameters = "keep-comments"`).  A header marker in `Users.sql` would reach both claims and replace the project's `["sort-input", "no-token-validation"]` list.  So:
  - Delete the `KeptQueries` claim from `Queries.cs`.
  - Delete its line, `comments kept by the attribute`, from `Program.cs` and `expected-output.txt`, and fix `Program.cs`'s comment above the `Kept.sql` line, which names `KeptQueries`.
  - `Kept.sql`'s header, `-- generator: no-token-validation keep-comments`, already shows `keep-comments` in a file header.
  - `tools/README.md`: the sentence on the project's files drops "and `Parameters` on the attribute", and "`Queries.cs` has the two types" becomes "the type".

**Interfaces:**
- Produces: after this task, the only writers of a removed property are the attribute-level tests marked in Task 1.

- [ ] **Step 1: Rewrite each site**, keeping each test's behavior the same.
- [ ] **Step 2: Re-describe and read the diff.**
  Run: `tools/describe-end-to-end.sh` (Docker running).
  Expected: no sidecar change, or a change you can explain by a moved marker.  Commit only a diff that you have read.
- [ ] **Step 3: Run the database tests.**
  Run: `dotnet test --project tests/DatabaseTests/Postgres` and `dotnet test --project tests/DatabaseTests/SqlServer`
  Expected: PASS.
- [ ] **Step 4: Validate and commit.**
  Run: `./pre-commit-validation.sh`, expected PASS, with its `package-install` step run and passing.  If the summary says it skipped `package-install`, run `./pre-commit-validation.sh --full`.
  ```bash
  git commit -am "test, examples: settings as markers and settings files"
  ```

### Task 3: Remove the properties from the generator

**Files:**
- Modify: `src/SqlSource/Generation/AttributeSource.cs`.  Remove `OutputMetadataName`, `ModelTypeMetadataName`, `CollectionTypeMetadataName`, `ParametersProperty`, `OutputProperty`, `InputModelSuffixProperty`, `OutputModelSuffixProperty`, `ModelNamespaceProperty`, `InputModelTypeProperty`, `OutputModelTypeProperty` and `CollectionTypeProperty`.  From `Text`, remove the enums `GeneratorOutput`, `GeneratorModelType` and `GeneratorCollectionType` and the eight properties.  The `NestedClass` doc comment stays.
- Modify: `src/SqlSource/Generation/TargetTypeReader.cs`.  In `AttributeSettings.PropertyOf`, every `SettingId` but `NestedClass` maps to `null`.  Its comment now says the attribute gives only the nested class.
- Modify: `src/SqlSource/Settings/QuerySettings.cs`.  `Resolve(SettingsLevel markers, SettingsLevel file)`; its doc drops "then the attribute".
- Modify: the callers of `Resolve`:
  - `src/SqlSource/Generation/UsableQuerySequence.cs`, which drops `_attribute`;
  - `src/SqlSource/Generation/UsableQueries.cs:48,79`;
  - `src/SqlSource/Generation/Sidecars/SidecarNeeds.cs:24-32`;
  - the tool's `src/SqlSource.Tool/Planning/RunPlanner.cs:308`, which calls `QuerySettings.Resolve(query.Markers, claimant.Level)` here so that the tool still compiles.  Task 4 removes the claims' levels and the inner loop.
- Modify: `src/SqlSource/SqlSourceGenerator.cs`.  Remove the `commentPaths` step (lines ~200-208) and its parameter of `ParseFiles`, and `CommentPaths` from the claims record (~line 1290).
- Modify: `src/SqlSource/Generation/FileParseInput.cs`.  Drop the `commentPaths` parameter, so a file keeps its comments by `chain.Level.KeepsComments` or by its own markers.
- Modify: `src/SqlSource/Generation/TrackingNames.cs`.  Remove `CommentPaths`.
- Modify: the doc comments of `src/SqlSource/Settings/OutputKind.cs`, `ModelKind.cs` and `CollectionKind.cs`, which call themselves the generator's form of the attribute's enums.
- Modify: `src/SqlSource/Settings/AGENTS.md`.  The enum-pair rule names `MemberPlacement` and `MethodPlacement` alone.  The "new setting" rule drops "a property in the `AttributeSettings` of `TargetTypeReader`" except for the nested class.
- Modify: `src/SqlSource/AGENTS.md`, if it names the removed members (`grep -n 'Output\|commentPaths\|CommentPaths' src/SqlSource/AGENTS.md`).
- Test:
  - `tests/SqlSource.Tests/Generation/AttributeSourceTests.cs`, whose theory rows at lines 29-31 go.
  - `tests/SqlSource.Tests/Settings/QuerySettingsTests.cs`.
  - `tests/SqlSource.Tests/Generator/CachingParseTests.cs:98` and `tests/SqlSource.Tests/Generator/CachingOptionsTests.cs:150-185`.
  - The tests marked in Task 1, with `LevelSettings.cs`.
  - The golden files under `tests/SqlSource.Tests/Generator/Golden/`, each of which holds the generated attribute source, `GeneratorOutput` and the eight properties included.  Rewrite them with `SQLSOURCE_UPDATE_GOLDEN=1` (`tests/README.md`, "The golden corpus of query methods"), never by hand, and check that the diff is the attribute source alone; the PR description says why they changed.

**Interfaces:**
- Consumes: Tasks 1 and 2, after which only the marked tests write a removed property.
- Produces: `QuerySettings.Resolve(SettingsLevel markers, SettingsLevel file)`, which the tool calls in Task 4.  `TargetType.Settings` still exists and carries only `NestedClass`.

- [ ] **Step 1: Write the failing tests.**  In `AttributeSourceTests`:
  ```csharp
  [Theory]
  [InlineData("GeneratorOutput")]
  [InlineData("GeneratorModelType")]
  [InlineData("GeneratorCollectionType")]
  [InlineData("public string Parameters")]
  [InlineData("public GeneratorOutput Output")]
  [InlineData("public string ModelNamespace")]
  public void Text_RemovedSettings_AreNotDeclared(string declaration) =>
      AttributeSource.Text.ShouldNotContain(declaration);
  ```
  In `tests/SqlSource.Tests/Generator/SettingLevelTests.cs`:
  - `TwoTypes_ClaimingOneFile_BothTakeItsHeaderOutput`: a header `-- output: sql`, two claims with no arguments, and both types get the constant and neither gets models.
  - `HeaderOutput_OverSettingsFileOutput_Wins`: a settings file with `models` and a header with `sql`, giving a constant and no models.
  In `TypeDiagnosticsTests`, keep or add `NestedClass_NotAnIdentifier_IsSqlsrc006`.
- [ ] **Step 2: Run them, expecting failure.**
  Run: `dotnet test --project tests/SqlSource.Tests -- --filter-class "SqlSource.Tests.Generation.AttributeSourceTests"`
  Expected: FAIL on `Text_RemovedSettings_AreNotDeclared`.  The two `SettingLevelTests` cases may already pass, since they pin behavior that must survive.
- [ ] **Step 3: Remove the code** listed under Files.  Delete the tests marked in Task 1 and the attribute rows of `QuerySettingsTests`.  Rewrite the `CommentPaths` assertions of `CachingOptionsTests` to assert that a `generatorParameters` edit in the settings file re-parses the file.
- [ ] **Step 4: Run all unit tests.**
  Run: `dotnet test --project tests/SqlSource.Tests`
  Expected: PASS.
- [ ] **Step 5: Validate and commit.**
  Run: `./pre-commit-validation.sh`, expected PASS.
  ```bash
  git commit -am "feat!: the attribute no longer sets output, parameters or model settings"
  ```

### Task 4: The tool reads `Source` alone

**Files:**
- Modify: `src/SqlSource.Tool/Planning/AttributeReader.cs`.  Remove `OutputType`, the `Output` branch (lines ~139-148) and `TryReadOutput`.  The class comment says it reads `Source` and the type's name.  An unknown named argument, `Output` included, literal or not, is skipped without error.
- Modify: `src/SqlSource.Tool/Planning/TypeClaim.cs`.  The record becomes `TypeClaim(string SourcePath, string TypeName, string? Source, LinePosition Position)`.
- Modify: `src/SqlSource.Tool/Planning/RunPlanner.cs`:
  - `ClaimsByFile` builds no level.
  - `FileClaims.Levels` and its use at line ~114 go.
  - `GreatestOutput` loops over `work.Claimants` alone, with `QuerySettings.Resolve(query.Markers, claimant.Level).Output`.
- Modify: `src/SqlSource.Tool/Planning/AGENTS.md`.  The `AttributeReader` rule says "reads syntax: one argument and the type's name", and SQLSRC208 applies to `Source` alone.
- Modify: `docs/tech-debt/TD-0031-attribute-reader-of-the-tool-is-syntax-only.md`.  The misreadings of `Output` go; keep those of `Source`.
- Test:
  - `tests/SqlSource.Tool.Tests/Planning/AttributeReaderTests.cs`, `RunPlannerTests.cs` and `GeneratorParityTests.cs`.
  - `tests/SqlSource.Tool.Tests/Planning/RunPlannerErrorTests.cs:297-319`: its case of two claims with two outputs cannot be written any more.  Rewrite it as two projects whose settings files give one file different outputs, if the planner's rule of the greatest output across projects still needs a test; otherwise delete it, and say why in the commit message.

**Interfaces:**
- Consumes: `QuerySettings.Resolve(SettingsLevel markers, SettingsLevel file)` from Task 3.
- Produces: `TypeClaim` without `Output`.

- [ ] **Step 1: Write the failing tests.**
  - `AttributeReaderTests.Read_OutputArgument_IsIgnoredWithoutError` is a theory over `Output = GeneratorOutput.Sql` and `Output = (GeneratorOutput)1`.  Each gives one claim of `a.sql`, beside `Source = "a.sql"`, and no SQLSRC208.
  - `GeneratorParityTests.TwoClaims_OfOneFile_DescribeWhatItsHeaderSays`: a header `-- output: sql` gives no queries to describe in the tool, matching the generator.
  - Every existing test that set `Output` on an attribute moves it to a header marker or the settings file, as in Task 1.
- [ ] **Step 2: Run them, expecting failure.**
  Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Planning.AttributeReaderTests"`
  Expected: FAIL on the `(GeneratorOutput)1` row, for which the reader today reports SQLSRC208 and drops the claim.  The literal row passes today and must keep passing.
- [ ] **Step 3: Implement** the changes listed under Files.
- [ ] **Step 4: Run the tool's tests.**
  Run: `dotnet test --project tests/SqlSource.Tool.Tests`
  Expected: PASS.
- [ ] **Step 5: Validate and commit.**
  Run: `./pre-commit-validation.sh`, expected PASS.
  ```bash
  git commit -am "feat(tool)!: read only Source from the attribute"
  ```

### Task 5: Documentation

**Files and what changes:**
- `docs/manual/settings.md`:
  - The "Settings that work today" rows drop "on the attribute" from Output, Generator parameters, the model suffixes, namespace and type, and Collection type.
  - "Which place wins" is redrawn with five rows: query, file header, nearest settings file, farther settings files, default.
  - The `Output` section drops the attribute example and the SQLSRC208 paragraph on writing `Output` literally.
  - The generator-parameters list drops step 3.  The other sections that show or name the attribute for a removed setting change too: today lines ~206, ~232, ~249 and ~268.
  - "A value that is wrong" keeps the attribute row for the three that remain.
  - "The attribute belongs to one type" becomes: two types that claim one file take the same settings from it.
  - "Configure one folder differently" ends with "To change a setting for the files of one type, use the type's attribute" (line ~816).  That becomes: use the file's header markers.
- `docs/manual/generator.md`:
  - The attribute's property table lists `Source`, `SqlLocation`, `NestedClass` and `MethodLocation`.
  - "Which files belong to a type": the sentence on each type's attribute applying to its own members changes to the shared settings.
  - The examples at lines ~45 and ~95-105 use markers.
  - The known issue "A query whose output is `sql` gets none" (line ~242) drops its case of two types with different outputs.
- `docs/manual/sql-files.md`: the known issue "A `.sql` file that two projects list" (line ~197) says each project's types give the file their own output.  With the output in the file and its settings files, two projects give it one output, unless their settings files differ.  Rewrite it to that.
- `docs/tech-debt/TD-0033-sql-file-that-two-projects-list-has-one-sidecar.md` and its row in `docs/tech-debt/README.md`: same correction.  Resolve the entry if no case is left; otherwise narrow it to the case of two projects' settings files.
- `docs/tech-debt/TD-0022-query-that-keeps-its-comments-is-built-and-scanned-twice.md`: its "Why it exists" names the attribute's `keep-comments`, which Task 3 removes.
- `docs/manual/diagnostics.md`:
  - SQLSRC006 lists only `SqlLocation`, `MethodLocation` and `NestedClass`, with an example that uses one of them.
  - SQLSRC208 names `Source` alone.
- `docs/manual/concepts.md`, `docs/manual/recipes.md` and `README.md` wherever they show a removed property.  The README FAQ and getting started section, if affected.
- `docs/architecture/generator.md`: the attribute level, the `commentPaths` step and `Resolve`'s signature.
- `docs/architecture/tool.md`: the plan reads `Source` alone.
- `docs/tech-debt/TD-0097-...`, if its text names a removed property as current.

- [ ] **Step 1: Edit each document.**  Then search, leaving out `docs/superpowers/`:
  ```bash
  grep -rnE 'GeneratorOutput|GeneratorModelType|GeneratorCollectionType|(Output|Parameters|CollectionType|ModelNamespace|InputModelType|OutputModelType|InputModelSuffix|OutputModelSuffix) = ' README.md docs src/SqlSource.Tool/README.md examples tools tests --include='*.md' | grep -v 'docs/superpowers/'
  grep -rniE "on the attribute|the attribute's|attribute level|its attribute|their attribute|each type's attribute|attribute of the type" README.md docs examples tools tests --include='*.md' | grep -v 'docs/superpowers/'
  ```
  Expected: no hit from the first.  Every hit from the second is about `Source`, `SqlLocation`, `NestedClass` or `MethodLocation`.
- [ ] **Step 2: Run the documentation tests.**
  Run: `dotnet test --project tests/SqlSource.Tests -- --filter-namespace "SqlSource.Tests.Documentation"` and `-- --filter-namespace "SqlSource.Tests.Diagnostics"`
  Expected: PASS (the link, sample and diagnostics-section checks).
- [ ] **Step 3: Validate and commit.**
  Run: `./pre-commit-validation.sh`, expected PASS.
  ```bash
  git commit -am "docs: settings mirrored on the attribute are now file settings"
  ```

### Task 6: Branch check

- [ ] **Step 1:** Run `./pre-commit-validation.sh --full`.  Expected: PASS.
- [ ] **Step 2:** Run the `branch-review:review` skill, address its findings, and repeat until it comes back clean (root `AGENTS.md`, "Pull requests").
