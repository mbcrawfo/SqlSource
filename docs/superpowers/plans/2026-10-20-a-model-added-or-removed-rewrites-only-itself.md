# A Model Added or Removed Rewrites Only Itself Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve TD-0060: emit each model from a node that stays with its file and query, so that a model added, removed or renamed writes that model alone.

**Architecture:** Today `ModelOutput` is a `SelectMany` of the one `ModelSet`, which the compiler matches by place in a list sorted by full name.  Instead, each model is emitted from the node of the `.sql` file whose query gave it (the first request of its group), and inside that node from a slot of its own, one for each query and role.  The per-file nodes come from the `ParsedFile` step, before its `Collect()`, whose nodes follow the compiler's list of additional texts: an added text is appended, a removed one goes, and a replaced one keeps its place, so no other file's node moves.  A path the project lists more than once has two such nodes, so its models are emitted from a second, collected output instead, which is rare and keeps every hint name added once.

**Tech Stack:** C# (`netstandard2.0`), Roslyn incremental generators (`Microsoft.CodeAnalysis.CSharp` 4.8.0 floor), xUnit v3, Shouldly.

**Spec:** [`docs/tech-debt/TD-0060-model-added-or-removed-writes-the-models-after-it-again.md`](../../tech-debt/TD-0060-model-added-or-removed-writes-the-models-after-it-again.md) (Problem and Proposed fix), the sub-phase 5.3 models spec (`docs/superpowers/specs/2026-10-18-query-generation-phase-5-3-models-design.md`, "Caching"), and `docs/architecture/generator.md#models`.

### Where the merged code differs from the TD

- **The TD's example would not meet its own test.**  It proposes emitting from `TypeModels` taken apart by a `SelectMany`, one node for each of a type's requests.  Those nodes are matched by their place within the type, and `Run_ModelAdded_EmitsTheModelsAfterItAgain` adds `Archive.sql` to the folder that `OrderQueries` claims, sorted before `Orders.sql` and `Payments.sql`: the type's later requests would still move and be written again.  This plan roots the nodes on the files instead, which an added file does not move, and on a slot for each query and role inside a file, so a query that gains a model moves none of the others either.  What still moves: a query inserted into or removed from a file moves the slots of the queries after it in that file alone.
- **A file listed twice.**  The TD does not mention it.  `ParsedFile` has a node for each listing and `ParsedFiles` keeps one; two nodes emitting one model would add one hint name twice, which throws and costs every type its code (CS8785).  The plan sends such a path's models to a collected output.
- **`src/SqlSource/AGENTS.md` has no line about the Models output** today; it gains one rule (Task 3).

## Global Constraints

- `netstandard2.0`, Roslyn API of 4.8.0 only; no collection expression targets `ImmutableArray<T>` (CS9210).
- Every value between steps is a record or `EquatableArray<T>`, never `ImmutableArray<T>`, and holds no Roslyn object.
- Every `Collect()` has a `Select` after it that gives an `EquatableArray<T>`.
- Compare paths with `SqlPath.Comparer`; find one with `SqlPath.IndexOf`/`SqlPath.Contains`, never a pass over a list.
- `SQLSRC257` and `SQLSRC258` (and the other model problems) are still reported once, from the `Select` of `Models` in `ReportProblems`, unchanged.
- Generated sources are unchanged byte for byte: this is a change to caching only.
- Tests in `tests/SqlSource.Tests/Generator/` also run on Roslyn 4.8.0 (`tests/SqlSource.Tests.RoslynFloor`), C# 12 at most in what they hand the compiler.
- Every commit passes `./pre-commit-validation.sh` (root `AGENTS.md`).  Sentences in comments and docs follow the house style: plain words, two spaces after a period.

## Review Focus

- **A file listed twice, or under two spellings, that has a model**: the model is emitted once and no exception drops the output.  Pinned by `Run_FileListedTwice_EmitsItsModelOnce` (Task 2).
- **The owner of a shared model goes**: `App.Shared.Row` is first in `Orders.sql`; removing `Orders.sql` moves the model to `Users.sql`'s node in one run, with its hint name added once.  Pinned by `Run_FileThatGivesASharedModelRemoved_EmitsItFromTheNextFile` (Task 2).
- **A query that gains a model beside one it has**: the query's other model is not written again.  Pinned by `Run_QueryGainsParameters_EmitsOnlyItsInputModel` (Task 2).
- **Two types claim one file**: each type's model of one query is emitted once, from one slot.  Pinned by `Arrange_TwoTypesClaimOneQuery_PutsBothModelsInItsSlotByClaimingType` (Task 1).
- **Hint names made unique ignoring case**: unchanged, since `ModelCollector.Models` still makes them for the whole set; existing `ModelNameTests` cover it.

---

### Task 1: Arrange the models by file, query and role

**Files:**
- Create: `src/SqlSource/Generation/Models/ModelPlace.cs`
- Create: `src/SqlSource/Generation/Models/ModelsByFile.cs`
- Modify: `src/SqlSource/Generation/Models/ModelSet.cs`
- Modify: `src/SqlSource/Generation/Models/ModelCollector.cs` (`Resolve`, `Models`)
- Modify: `src/SqlSource/Generation/Paths/SqlPath.cs` (add `FindRepeated`)
- Test: `tests/SqlSource.Tests/Generation/Models/ModelsByFileTests.cs` (create)
- Test: `tests/SqlSource.Tests/Generation/Paths/SqlPathTests.cs`

**Interfaces:**
- Produces:
  - `internal sealed record ModelPlace(string NormalizedPath, int QueryIndex, ModelRole Role, string ClaimingType)` — where a model is emitted from: the first request's `ModelSource.NormalizedPath`, `QueryIndex`, the request's `Role` and `ModelSource.ClaimingType`.
  - `ModelSet(EquatableArray<Model> Models, EquatableArray<ModelPlace> Places, EquatableArray<DiagnosticInfo> Diagnostics)` — `Places[i]` is the place of `Models[i]`; same count.
  - `internal sealed record FileModelSlots(string NormalizedPath, EquatableArray<EquatableArray<Model>> Slots)`.
  - `internal sealed record ModelsByFile(EquatableArray<FileModelSlots> Files, EquatableArray<Model> OfRepeatedPaths)` with
    - `public static ModelsByFile Arrange(ModelSet set, EquatableArray<string> repeatedPaths)`
    - `public static EquatableArray<EquatableArray<Model>> SlotsOf(string normalizedPath, ModelsByFile byFile)`
  - `SqlPath.FindRepeated(ImmutableArray<SqlFilePath> files, EquatableArray<string> sortedPaths) -> EquatableArray<string>`: the paths of `sortedPaths`, spelled as there, that more than one of `files` has under `SqlPath.Comparer`, in the order of `sortedPaths`; `Empty` without allocating when there is none (`PathCollision.Find` has the same shape: a search for each file).

- [ ] **Step 1: Write the failing tests**

`ModelsByFileTests` (build `ModelSet`s by hand with a small `Model` helper; `TestModels.cs` beside it may already have one):
- `Arrange_ModelsOfTwoFiles_GivesEachFileItsSlotsInPathOrder`: models placed at `b/B.sql` query 0 Output, `a/A.sql` query 1 Input → `Files` is `[a/A.sql, b/B.sql]`; `A.sql`'s `Slots` has 4 entries, slot `1 * 2 + 0 = 2` holds the model and slots 0, 1, 3 are empty; `B.sql`'s `Slots` has 2 entries, slot 1 holds its model.
- `Arrange_TwoTypesClaimOneQuery_PutsBothModelsInItsSlotByClaimingType`: two models at `a/A.sql` query 0 Output with claiming types `App.Z` and `App.A` → slot 1 is `[model of App.A, model of App.Z]`.
- `Arrange_RepeatedPath_LeavesItsModelsToTheCollectedOutput`: models at `a/A.sql` and `b/B.sql`, `repeatedPaths = ["A/a.sql"]` (other case) → `Files` is `[b/B.sql]`, `OfRepeatedPaths` is A's model.
- `SlotsOf_PathWithoutModels_IsEmpty` and `SlotsOf_PathIgnoringCase_FindsItsSlots`.
- `Arrange_EqualSets_AreEqual`: two `Arrange` calls over equal inputs give equal values (value equality of the nested arrays).

`SqlPathTests`:
- `FindRepeated_PathListedOnce_IsEmpty`, `FindRepeated_PathListedTwiceOrUnderTwoSpellings_IsThatPathOnce` (files `a/A.sql`, `a/a.sql`, `a/A.sql`, `b/B.sql` with sorted `[a/A.sql, b/B.sql]` → `["a/A.sql"]`).

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test --project tests/SqlSource.Tests -- --filter-class "SqlSource.Tests.Generation.Models.ModelsByFileTests"`
Expected: build fails (types not defined).

- [ ] **Step 3: Implement**

- `ModelCollector.Models` fills `Places` beside `Models` from each emitted group's first request; `Resolve` returns `Places` empty for no groups.
- `ModelsByFile.Arrange`: order the indices of the set by `Places` (path by `SqlPath.Comparer`, then query index, then role, then claiming type ordinally); a model whose path `SqlPath.Contains(repeatedPaths, path)` goes to `OfRepeatedPaths` (in the set's order), the others into one `FileModelSlots` for each path, whose `Slots` has `2 * (largest query index + 1)` entries, the model at `QueryIndex * 2 + (int)Role`.  An empty set gives one shared empty value.
- `SlotsOf`: `SqlPath.IndexOf(byFile.Files, path, f => f.NormalizedPath)`, the file's `Slots` or `Empty`.
- Doc comments on each record, in the style of `ModelSet.cs`: say what the slots are for (a slot stays with its query and role, so that a model added beside another moves nothing).

- [ ] **Step 4: Run them to verify they pass**

Run the same command, then `-- --filter-class "SqlSource.Tests.Generation.Paths.SqlPathTests"`.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/SqlSource/Generation tests/SqlSource.Tests/Generation
git commit -m "Arrange the models by the file, query and role they come from"
```

### Task 2: Emit each model from its file's node

**Files:**
- Modify: `src/SqlSource/SqlSourceGenerator.cs` (`Initialize`, `ClaimFiles`, `ParseFiles`, `RegisterModelSources`, the record structs at the end)
- Modify: `src/SqlSource/Generation/TrackingNames.cs` (add `RepeatedPaths`, `ModelsByFile`, `FileModelSlots`)
- Modify: `tests/SqlSource.Tests/Generator/CachingModelTests.cs`
- Modify: `tests/SqlSource.Testing/Generator/StepReasons.cs` (the comment of `ModelsRemoved`)

**Interfaces:**
- Consumes: Task 1's `ModelsByFile.Arrange`, `ModelsByFile.SlotsOf`, `SqlPath.FindRepeated`.
- Produces: tracking names `RepeatedPaths`, `ModelsByFile`, `FileModelSlots`; `ModelOutput` keeps its name and its `(string HintName, string Source)` value, from two nodes.

- [ ] **Step 1: Change and add the tests in `CachingModelTests`**

- Rename `Run_ModelAdded_EmitsTheModelsAfterItAgain` to `Run_ModelAdded_EmitsOnlyThatModel`: expect `GetArchiveDto` `New` and the other three `Cached`.  Drop the TD-0060 comment; say instead that the new file has a node of its own, which moves no other file's.
- Rename `Run_ModelRemoved_EmitsTheModelsAfterItAgain` to `Run_ModelRemoved_EmitsNoOtherModel`: expect `GetOrderDto` and `GetUserDto` `Cached`, and `ModelsRemoved(result)` to be `["App.Orders.GetPaymentDto-Model.g.cs"]`.
- `Run_ModelRenamedInPlace_EmitsOnlyThatModel`: replace `Users` with `"-- name: GetUser\n-- output-model: App.Users.UserRow\nSELECT 1;\n"` (a marker is a comment, so the sidecar stays current) → `ModelReasons` is `{UserRow: Modified, GetOrderDto: Cached, GetPaymentDto: Cached}`, `ModelsRemoved` is empty, and the run's trees have `App.Users.UserRow-Model.g.cs` and no `GetUserDto`.
- `Run_QueryGainsParameters_EmitsOnlyItsInputModel`: first run with `Users` as `"-- name: GetUser\nSELECT 1 WHERE 1 = @id;\n"` and its sidecar of one column (int4) and no parameter, then replace the sidecar with one whose entry also has a parameter `id` of int4 (`TestSidecars.Entry(parameters: ...)`) → `GetUserParams` (the input suffix; check `QuerySettings` for its default) `New`, `GetUserDto` `Cached`, the other two `Cached`.
- `Run_FileThatGivesASharedModelRemoved_EmitsItFromTheNextFile`: as `Run_SharedModelEdited_EmitsOnlyThatModelAgain`'s first run, then remove `orders` and its sidecar → the run's trees have exactly one `App.Shared.Row-Model.g.cs`, whose summary names `GetUser` alone, and `run.Results.Single().Exception` is null.
- `Run_FileListedTwice_EmitsItsModelOnce`: first run of models with `Orders` listed twice (a second `InMemoryAdditionalText` with the same path and text) → no exception, exactly one `GetOrderDto` tree, no diagnostic.  Then remove one of the two listings → still one `GetOrderDto` tree and no exception.
- Keep every other test as it is; `Run_CSharpEdit_EmitsNoModelAgain` and the two that assert `ModelOutput` is all `Cached` must still pass with outputs from both nodes.

- [ ] **Step 2: Run them to verify the new expectations fail**

Run: `dotnet test --project tests/SqlSource.Tests -- --filter-class "SqlSource.Tests.Generator.CachingModelTests"`
Expected: the renamed add/remove tests and the query-gains test FAIL (models after the change are `Modified`); `Run_FileListedTwice_EmitsItsModelOnce` and the shared-model removal may pass already.

- [ ] **Step 3: Rewire the pipeline**

- `ClaimFiles`: add `RepeatedPaths` = `listedPaths.Combine(sqlPaths).Select(SqlPath.FindRepeated)`, tracked as `TrackingNames.RepeatedPaths`, returned in `ClaimedFiles`.  Comment: almost always none, so it almost never changes.
- `ParseFiles`: return the per-file `ParsedFile` provider (after `WhereNotNull`, before `Collect`) beside the sorted `ParsedFiles`, in a new private record struct; update the callers.
- `RegisterModelSources(context, models, parsedFile, repeatedPaths)`:
  - `ModelsByFile` = `models.Combine(repeatedPaths).Select(ModelsByFile.Arrange)`.
  - `FileModelSlots` = `parsedFile.Select(file => file.NormalizedPath).Combine(modelsByFile).Select(ModelsByFile.SlotsOf)`; then `SelectMany` to the slots, `SelectMany` to the models, `Select` to `(model.HintName, Source: ModelEmitter.Emit(model))` tracked as `ModelOutput`, and a `RegisterSourceOutput` that adds it.
  - The repeated paths' models: `modelsByFile.SelectMany(byFile => byFile.OfRepeatedPaths)`, the same `Select` tracked as `ModelOutput`, and its own `RegisterSourceOutput`.
  - Comments say why: a file's node stays with its additional text, a slot with its query and role, and a path listed twice is left to the collected output so that one hint name is added once.

- [ ] **Step 4: Run the generator tests**

Run: `dotnet test --project tests/SqlSource.Tests -- --filter-namespace "SqlSource.Tests.Generator"` and then `dotnet test --project tests/SqlSource.Tests.RoslynFloor`.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/SqlSource tests
git commit -m "Emit each model from the node of its file, so that a model added or removed writes itself alone"
```

### Task 3: Documents, tech debt and the measurement

**Files:**
- Modify: `docs/architecture/generator.md` (the pipeline graph, "What each step gives", the count of source outputs, the Models section's flowchart and "The fan-out, and its cost", the caching tests paragraph)
- Modify: `src/SqlSource/AGENTS.md` (one rule in "The pipeline")
- Modify: `docs/tech-debt/TD-0019-change-to-claimed-files-reads-every-claimed-file-again.md` (Proposed fix)
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (the 5.3 sentence on TD-0060)
- Delete: `docs/tech-debt/TD-0060-model-added-or-removed-writes-the-models-after-it-again.md` and its row in `docs/tech-debt/README.md`

- [ ] **Step 1: Update the documents**

- generator.md: graph edges `ParsedFile --> FileModelSlots`, `Models --> ModelsByFile`, `RepeatedPaths --> ModelsByFile`, `ModelsByFile --> FileModelSlots`, `FileModelSlots -->|"each slot"| ModelOutput`, `ModelsByFile -->|"a path listed twice"| ModelOutput`, `SqlPaths --> RepeatedPaths`; entries for the three new steps; eleven source outputs; rewrite "The fan-out, and its cost" to say what moves now (a query inserted or removed moves the slots after it in its file; a change to a setting that renames every model writes every model) and that a path listed twice is emitted from the collected output, matched by place as before; update the caching tests paragraph to the new test names.
- AGENTS.md, "The pipeline": **A model is emitted from the node of its file's `ParsedFile`, in the slot of its query and role, never from a `SelectMany` of the sorted `Models`**: the compiler matches the nodes of a `SelectMany` by place, and a path listed twice is emitted from `ModelsByFile.OfRepeatedPaths` so that its hint name is added once.  Link the fan-out section.
- TD-0019: in the Proposed fix, say that the models output now roots on the `ParsedFile` nodes with `RepeatedPaths` deciding which path a node may report (see `RegisterModelSources`), and that the checks can follow it with little more work: give each `ParsedFile` node its check, and check a repeated path's file from the collected list, which replaces the "null for the item that is not the one `ParsedFiles` kept" idea.
- Epic outline: "A model added or removed writes again the models sorted after it, accepted as `TD-0060`" → say it was accepted, then resolved after 5.7 by emitting each model from its file's node.
- Delete TD-0060's file and row; `grep -rn "TD-0060" .` leaves only the historical plans and specs of `docs/superpowers/` other than the epic.

- [ ] **Step 2: Measure**

Run the temporary benchmark (500 files of one model each, one type) in `Release`, before and after, as `src/SqlSource/AGENTS.md` asks: time and allocations per run for a model added sorted first, removed from the middle, renamed in place, and an edit of one model's column.  Keep the numbers for the pull request; do not commit the benchmark.

- [ ] **Step 3: Validate and commit**

Run: `./pre-commit-validation.sh`
Expected: every step `passed`.

```bash
git add docs src/SqlSource/AGENTS.md
git commit -m "Document the models output by file and resolve TD-0060"
```
