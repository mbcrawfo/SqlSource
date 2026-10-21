# Claimed-File Read and Sidecar Checks Keyed by File Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve TD-0019: a change to the set of claimed files reads only the files whose answer changed, and a claimed file added, removed or renamed checks, types and reads the models of no other file.

**Architecture:** Whether a file is claimed becomes a value of its own for each file, a `FileClaim` compared by value, between `FileParseInput` and the read, so a change to `ClaimedPaths` gives an equal claim, and no read, for every file whose answer is the same.  The checks of the sidecars, and the per-file steps after them (`FileTypesInput`, `TypedFile`, `FileTypes`, `FileModels`), are rooted on each file's `ParsedFile` node, as `RegisterModelSources` roots the models since #97: those nodes follow the compiler's list of additional texts, so a file added, removed or renamed moves no other.  A path in `RepeatedPaths` has a node for each listing, and two nodes would report its problems twice, so such a path is checked from the collected `ParsedFiles` instead, through a second, identical chain of steps.

**Tech Stack:** C# (`netstandard2.0`), Roslyn incremental generators (`Microsoft.CodeAnalysis.CSharp` 4.8.0 floor), xUnit v3, Shouldly.

**Spec:** [`docs/tech-debt/TD-0019-change-to-claimed-files-reads-every-claimed-file-again.md`](../../tech-debt/TD-0019-change-to-claimed-files-reads-every-claimed-file-again.md) (Problem and Proposed fix), with [`TD-0069`](../../tech-debt/TD-0069-change-to-any-model-runs-every-files-model-node-again.md) to fix only if this makes it cheap, and `docs/architecture/generator.md` (the pipeline, Sidecars, Types, the fan-out by file).

### Where the merged code differs from the TD

- **The types and the models' properties move with the checks.**  The TD names the checks alone, but `FileTypesInput`, `TypedFile`, `FileTypes` and `FileModels` are a node for each `SidecarCheck`, so today they move with it and run again for every moved file (generator.md's Types section says so).  Rooting the checks on `ParsedFile` roots them too.  Since a values provider cannot be joined to another in Roslyn 4.8.0 without a `Collect()`, which would match by place again, the chain from `SidecarCheckInput` to `FileModels` runs twice: once on the `ParsedFile` nodes of the paths listed once, and once on the repeated paths taken from `ParsedFiles`.  A private `FileNodes<T>` record struct in `SqlSourceGenerator` holds the two providers and applies a step to both, and `AllFileModels` collects both.
- **The claim is a pair, not a boolean alone.**  The TD's first option.  `FileClaim(FileParseInput Input, bool IsClaimed)` keeps the input beside the answer so the read takes it from the claim, and so a test can key the step by path.
- **TD-0069 is left open.**  Its cost is `FileModelSlots`, a node for each file combined with the collected `ModelsByFile`; this change does not touch that path, and its proposed fix starts with a profiler measurement and keeps a change only if every `CachingModelTests` case holds.  The pull request says so.
- **The tool's planning (`src/SqlSource.Tool/Planning/`) is unaffected**: it calls `FileParseInput.Resolve` and `SqlFileReader.Read`, neither of which changes.  `SqlPath` does not change (path-above-root, TD-0068, works there in parallel).

## Global Constraints

- `netstandard2.0`, Roslyn API of 4.8.0 only; no collection expression targets `ImmutableArray<T>` (CS9210).
- Every value between steps is a record or `EquatableArray<T>`, holds no Roslyn object except the `AdditionalText` of `FileParseInput` (so `FileClaim` may hold one, through its input), compared by reference.
- Every `Collect()` has a `Select` after it that gives an `EquatableArray<T>`.
- Compare paths with `SqlPath.Comparer`; find one with `SqlPath.IndexOf`/`SqlPath.Contains`, never a pass over a list.
- A file's problems (sidecar checks, types, models' names) are reported exactly once, whether the project lists its path once, twice, or under two spellings.
- Generated sources and diagnostics are unchanged: this is a change to caching only.
- No `TrackingNames` step changes meaning without the caching tests that name it changing with it.  `ParsedFile` stays the step after the read's `WhereNotNull`, so a file read again with an equal value is still `Cached` there.
- Tests in `tests/SqlSource.Tests/Generator/` also run on Roslyn 4.8.0 (`tests/SqlSource.Tests.RoslynFloor`); each file there starts with the `// Shared:` line.
- Every commit passes `./pre-commit-validation.sh` (root `AGENTS.md`).  Comments and docs: plain words, two spaces after a period.

## Review Focus

- **A path listed twice with a stale sidecar**: `SQLSRC244` is reported once, from the repeated chain, and still once after one listing goes and the path moves to a `ParsedFile` node.  Pinned by `Run_FileListedTwice_ChecksItOnce` (Task 2).
- **A path listed under two spellings**: `SQLSRC013` and the file's check are each reported once.  Pinned by the `[InlineData]` spelling case of `Run_FileListedTwice_ChecksItOnce` (Task 2).
- **A removed file**: its node goes as `Removed` and nothing after it reports its problems; no other file's node moves.  Pinned by `Run_ClaimedSqlFileRemoved_MovesNoOtherFilesCheck` (Task 2).
- **A file added to a claimed folder**: read alone; every other file's claim is `Unchanged` and its `ParsedFile` `Cached`.  Pinned by `Run_SqlFileAddedToAClaimedFolder_ReadsOnlyThatFile` (Task 1).
- **The models of a repeated path** still have their properties: `AllFileModels` holds each path once from either chain.  Pinned by the existing `Run_FileListedTwice_EmitsItsModelOnce` and `Run_FileListedAgain_EmitsItsModelOnce` (Task 2 runs them).

---

### Task 1: The claim of each file is a value of its own

**Files:**
- Create: `src/SqlSource/Generation/FileClaim.cs`
- Modify: `src/SqlSource/Generation/TrackingNames.cs` (add `FileClaim` after `FileParseInput`)
- Modify: `src/SqlSource/SqlSourceGenerator.cs` (`ParseFiles`)
- Modify: `tests/SqlSource.Testing/Generator/StepReasons.cs` (add `Claims`, reword the comment of `ParseInputs`)
- Test: `tests/SqlSource.Tests/Generator/CachingParseTests.cs`

**Interfaces:**
- Produces:
  - `internal sealed record FileClaim(FileParseInput Input, bool IsClaimed)` with `public static FileClaim Of(FileParseInput input, EquatableArray<string> claimedPaths)`: claimed when `input.NormalizedPath` is not null and `SqlPath.Contains(claimedPaths, path)`.  Doc comment: the answer is compared by value, so a change to the claimed paths reads only a file whose answer or input changed.
  - `TrackingNames.FileClaim`.
  - `StepReasons.Claims(GeneratorDriverRunResult result) -> Dictionary<string, IncrementalStepRunReason>`: `Reasons<FileClaim>(result, TrackingNames.FileClaim, claim => claim.Input.File.Path)`.

- [ ] **Step 1: Write the failing test** in `CachingParseTests`:

```csharp
// A file added to a folder a type claims changes the claimed paths.  Every file's claim is worked out again, and only
// the added file's is new, so it alone is read.
[Fact]
public void Run_SqlFileAddedToAClaimedFolder_ReadsOnlyThatFile()
{
    var compilation = GeneratorHarness.CreateCompilation(Sources);
    var driver = _scene.FirstRun(OutputKind.Sql, compilation);
    var count = new InMemoryAdditionalText("/app/Orders/Count.sql", "SELECT 3;\n");

    var result = Run(driver.AddAdditionalTexts([count]), compilation);

    AllReasons(result, TrackingNames.ClaimedPaths).ShouldBe([IncrementalStepRunReason.Modified]);
    Claims(result).ShouldBe(
        new Dictionary<string, IncrementalStepRunReason>
        {
            [_scene.Users.Path] = IncrementalStepRunReason.Unchanged,
            [_scene.Orders.Path] = IncrementalStepRunReason.Unchanged,
            [count.Path] = IncrementalStepRunReason.New,
        },
        ignoreOrder: true
    );
    Reasons<ParsedSqlFile>(result, TrackingNames.ParsedFile, file => file.FileName).ShouldBe(
        new Dictionary<string, IncrementalStepRunReason>
        {
            ["Users.sql"] = IncrementalStepRunReason.Cached,
            ["Orders.sql"] = IncrementalStepRunReason.Cached,
            ["Count.sql"] = IncrementalStepRunReason.New,
        },
        ignoreOrder: true
    );
}
```

Also add `TrackingNames.FileClaim` to the list of steps in `Run_MethodBodyEdited_TakesEveryStepFromThePreviousRun`, after `FileParseInput`.

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test --project tests/SqlSource.Tests -- --filter-class "SqlSource.Tests.Generator.CachingParseTests"`
Expected: build fails (`FileClaim`, `Claims` not defined).

- [ ] **Step 3: Implement**

`ParseFiles`: `fileInputs.Combine(claimedPaths).Select(FileClaim.Of)` tracked as `FileClaim`, then `.Select((claim, ct) => claim.IsClaimed ? SqlFileReader.Read(claim.Input, ct) : null).WhereNotNull()` tracked as `ParsedFile`, as today.  Comment: the claim is a value of its own, so a change to the claimed paths reads only a file whose answer changed.  `ParseInputs`'s comment: that a file was not read is shown by its claim, which is neither `Modified` nor `New` (`Claims`), or by its input and `ClaimedPaths`, neither of which is.

- [ ] **Step 4: Run the generator tests**

Run: `dotnet test --project tests/SqlSource.Tests -- --filter-namespace "SqlSource.Tests.Generator"` and `dotnet test --project tests/SqlSource.Tests.RoslynFloor`.
Expected: PASS.  A test whose expectation names `ParsedFile` or `FileParseInput` must still hold unchanged; if one fails, the step changed meaning: fix the code, not the test.

- [ ] **Step 5: Validate and commit**

```bash
./pre-commit-validation.sh
git add src/SqlSource tests
git commit -m "Make whether a file is claimed a value of its own, so a change to the claimed paths reads only the files whose answer changed"
```

### Task 2: Check, type and read the models of each file from its own node

**Files:**
- Modify: `src/SqlSource/SqlSourceGenerator.cs` (`Initialize`, `CheckSidecars`, `TypeFiles`, `CollectModels`, `ReportProblems`, the record structs; a helper `OfRepeatedPaths` at the end)
- Test: `tests/SqlSource.Tests/Generator/CachingCheckTests.cs`
- Possibly: `tests/SqlSource.Tests/Generator/CachingTypeTests.cs`, `CachingModelTests.cs`, if an expectation pinned a moved node (none known; see Step 4)

**Interfaces:**
- Consumes: `ParsedFiles.EachFile` and `ParsedFiles.Sorted` (exist), `ClaimedFiles.RepeatedPaths` (exists), `SidecarCheckInput.Gather`, `SidecarChecker.Check`, `FileTypesInput.Gather`, `ModelReader.Read` (unchanged).
- Produces (all private to `SqlSourceGenerator`):
  - `private readonly record struct FileNodes<T>(IncrementalValuesProvider<T> EachFile, IncrementalValuesProvider<T> OfRepeatedPaths)` with `public FileNodes<TResult> Then<TResult>(Func<IncrementalValuesProvider<T>, IncrementalValuesProvider<TResult>> step) => new(step(EachFile), step(OfRepeatedPaths));`
  - `CheckedSidecars.Checks`, `TypedFiles.Files`, `TypedFiles.FileTypes` and `CollectedModels.FileModels` become `FileNodes<...>` of their current element types.
  - `private static IEnumerable<ParsedSqlFile> OfRepeatedPaths(EquatableArray<ParsedSqlFile> files, EquatableArray<string> repeatedPaths)`: for each repeated path, the file `SqlPath.IndexOf` finds, if any (a repeated path no type claims has none).  No pass over `files`.
  - Tracking names are unchanged: each named step of both chains keeps its name, as the two `ModelOutput`s share theirs.

- [ ] **Step 1: Change and add the tests in `CachingCheckTests`**

- Rename `Run_ClaimedSqlFileAdded_ChecksTheFilesSortedAfterIt` to `Run_ClaimedSqlFileAdded_ChecksOnlyThatFile`; drop the TD-0019 comment and say the added file has a node of its own, which moves no other file's.  Expect `Checks(result)` = `{Orders: Cached, Payments: New, Users: Cached}` and no diagnostic.
- `Run_ClaimedSqlFileRemoved_MovesNoOtherFilesCheck`: `FirstRunWithSidecars`, then run with `_scene.Payments` and `CurrentSidecar(_scene.Payments, "GetPayment")` added, then remove `_scene.Orders` and `_scene.OrdersSidecar` (Orders sorts first) → `Checks(result)` = `{"app/Orders/Orders.sql": Removed, "app/Orders/Payments.sql": Cached, "app/Users/Users.sql": Cached}`; `AllReasons(result, TrackingNames.FileTypesInput)` has no `Modified` or `New`; no diagnostic.
- `Run_FileListedTwice_ChecksItOnce` as a `[Theory]` with `[InlineData("/app/Users/Users.sql")]` and `[InlineData("/app/Users/users.sql")]`: driver over `Users`, a second listing at the given spelling with `Users`'s text, `Orders`, `OrdersSidecar`, and a stale sidecar for `Users` (`Sidecars.Of(ToSqlFile(_scene.Users), Sidecars.Entry("GetUser", "stale"))`), options `Models`/`postgres`.  First run: no exception; diagnostics `SQLSRC244` once, plus `SQLSRC013` once for the second spelling; `Checks(result)` has one key under `app/Users/` (a duplicate key would throw in `ToDictionary`).  Then remove `_scene.Users` (the first listing): the same diagnostics, still once each.

- [ ] **Step 2: Run them to verify the new expectations fail**

Run: `dotnet test --project tests/SqlSource.Tests -- --filter-class "SqlSource.Tests.Generator.CachingCheckTests"`
Expected: the added and removed cases FAIL (moved files `Modified`/`New`); the listed-twice case may pass already.

- [ ] **Step 3: Rewire the pipeline**

- `Initialize`: pass `parsedFiles` (the struct) and `claims.RepeatedPaths` to `CheckSidecars`.
- `CheckSidecars`: the files to check are `FileNodes<ParsedSqlFile>`: `EachFile` = `parsedFiles.EachFile.Combine(repeatedPaths).Select(file, or null when SqlPath.Contains(repeated, file.NormalizedPath)).WhereNotNull()`; `OfRepeatedPaths` = `parsedFiles.Sorted.Combine(repeatedPaths).SelectMany(OfRepeatedPaths)`.  `Checks` = `files.Then(...)` of today's `Combine(fileNeeds.Combine(sidecars))`, `Gather` tracked `SidecarCheckInput`, `Check` tracked `SidecarCheck`.  Comments say why: the `ParsedFile` nodes follow the additional texts, so a file added, removed or renamed moves no other file's check; a path listed twice has two nodes, which would report its problems twice, so it is checked from the collected files, matched by place, a cost a project that lists no path twice never pays.
- `TypeFiles` and `CollectModels`: wrap today's per-file chains in `Then`.  `AllFileModels` = `fileModels.EachFile.Collect().Combine(fileModels.OfRepeatedPaths.Collect()).Select(ToSorted(left.AddRange(right), path))`, tracked as today.
- `ReportProblems`: register `EachFile` then `OfRepeatedPaths` for checks, then for types, then for models, keeping today's order of kinds.

- [ ] **Step 4: Run the generator tests**

Run: `dotnet test --project tests/SqlSource.Tests -- --filter-namespace "SqlSource.Tests.Generator"` and `dotnet test --project tests/SqlSource.Tests.RoslynFloor`.
Expected: PASS.  If a case of `CachingTypeTests` or `CachingModelTests` expected a moved file's `FileTypesInput`, `TypedFile` or `FileModels` to run again, change it to `Cached` with a comment that the file's node did not move; any other failure is a bug.

- [ ] **Step 5: Validate and commit**

```bash
./pre-commit-validation.sh
git add src/SqlSource tests
git commit -m "Check, type and read the models of each file from its own node, so a claimed file added or removed moves no other"
```

### Task 3: Documents, tech debt and the measurement

**Files:**
- Modify: `docs/architecture/generator.md` (the pipeline graph and "What each step gives", the count of source outputs, the Paths paragraph on unclaimed files, Sidecars' last paragraph, Types' caching paragraph, the fan-out by file, the caching tests paragraph)
- Modify: `src/SqlSource/AGENTS.md` ("The pipeline")
- Delete: `docs/tech-debt/TD-0019-change-to-claimed-files-reads-every-claimed-file-again.md` and its row in `docs/tech-debt/README.md`

- [ ] **Step 1: Update the documents**

- generator.md graph: `FileParseInput --> FileClaim`, `ClaimedPaths --> FileClaim`, `FileClaim --> ParsedFile` (replacing the two edges into `ParsedFile`); `ParsedFile -->|"each file listed once"| SidecarCheckInput`, `ParsedFiles -->|"a path listed twice"| SidecarCheckInput`, `RepeatedPaths --> SidecarCheckInput` (replacing the `ParsedFiles -->|"each file"|` edge); `SidecarCheck --> FileTypesInput` without "each file".  Step list: a `FileClaim` entry; `ParsedFile` read from the claim.  Fourteen source outputs: the checks, the types and the models' names each from two outputs.
- Sidecars, last paragraph, and Types, caching paragraph: a claimed file added, removed or renamed checks and types that file alone; drop both TD-0019 links.
- The fan-out by file: say that the checks, the types and the models' properties are rooted on the `ParsedFile` nodes too, through two chains, and a repeated path goes through the one over `ParsedFiles`.
- Caching tests: `Run_ClaimedSqlFileAdded_ChecksOnlyThatFile`, `Run_ClaimedSqlFileRemoved_MovesNoOtherFilesCheck`, `Run_FileListedTwice_ChecksItOnce`, `Run_SqlFileAddedToAClaimedFolder_ReadsOnlyThatFile`.
- AGENTS.md: the rule "A `.sql` file that no type claims is never read" gains: whether one is claimed is its `FileClaim`, so a change to `ClaimedPaths` reads only a file whose answer changed.  The models rule becomes: a file's check, types, models' properties and models run from its `ParsedFile` node, never from a `SelectMany` of `ParsedFiles` or `Models`, and a path in `RepeatedPaths` from the collected list or `ModelsByFile.OfRepeatedPaths`; link the fan-out.
- Delete TD-0019's file and row; `grep -rn "TD-0019" docs src tests` leaves only `docs/superpowers/`.

- [ ] **Step 2: Measure**

Run the temporary benchmark (not committed: 500 files of one model each in one claimed folder, each with a typed sidecar, `Release`, tracking off, median of 40 runs after 10 warm-ups) before and after: a file added sorted first, the same file removed, a file removed from the middle, added back, and an edit of one file's text.  Keep the numbers for the pull request.

- [ ] **Step 3: Validate and commit**

```bash
./pre-commit-validation.sh
git add docs src/SqlSource/AGENTS.md
git commit -m "Document the claim and the checks by file and resolve TD-0019"
```
