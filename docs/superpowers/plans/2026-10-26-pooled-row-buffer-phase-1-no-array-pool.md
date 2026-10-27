# Pooled row buffer, phase 1: the `no-array-pool` parameter, implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the generator parameter `no-array-pool`, read at every level, and carry its resolved value to the method writer on `QueryMethod`, with no change to any generated code.

**Architecture:** The word joins `GeneratorParameterList`'s names and the `GeneratorParameters` flags, so every reader of a list (the `-- generator:` marker, the attribute, the metadata and the property) accepts it with no change of its own.  `QuerySettings` exposes it as `UseArrayPool`, and `MethodPlanner` copies that onto `QueryMethod`.  Nothing reads the flag yet; phases 2 and 3 do.

**Tech Stack:** C# source generator (`netstandard2.0`, Roslyn 4.8.0 floor), xUnit, Shouldly.

**Spec:** `docs/superpowers/specs/2026-10-26-pooled-row-buffer-design.md`, section Delivery, Phase 1, and the `no-array-pool` decisions.

## Global Constraints

- The generated code does not change in this phase: every method, under the parameter or not, is written as today.
- Words are matched ordinally ignoring case (`src/SqlSource/AGENTS.md`, `Parsing/`).
- A test in `tests/SqlSource.Tests/Generator/` also compiles on Roslyn 4.8.0, starts with the `// Shared:` line, hands the compiler C# 12 at most, and its file name ends in `Tests.cs` (that folder's `AGENTS.md`).
- User documentation describes effects, never internals; the parameter's effect is the manual's callout, word for word: `> **Coming in a later release.**  <One sentence on what it will do.>  See [what works today](README.md#what-works-today).`
- A new generator parameter is named in the Values cell of the Generator parameters row and the parameter table of `docs/manual/settings.md`, the `SQLSRC109` section of `docs/manual/diagnostics.md`, and the list in `tests/SqlSource.Tests/Diagnostics/DiagnosticsDocumentTests.cs` (`src/SqlSource/AGENTS.md`, `Settings/`).
- Every commit passes `./format.sh` then `./pre-commit-validation.sh`.

## Where the code differs from the spec

- The spec names `SQLSRC014` for a bad word on the attribute.  The code reports the attribute's as `SQLSRC006` and the property's and metadata's as `SQLSRC014` (`GeneratorParameterLevelTests`); the tests follow the code.
- The spec asks for the callout "word for word as the root `AGENTS.md` gives it".  The root gives the packed readmes' form, with an absolute URL; `docs/manual/AGENTS.md` gives the same words with the relative link `README.md#what-works-today` for a page of the manual, and the link test requires relative links there.  The manual's form is used.  A Markdown table cell cannot hold a blockquote, so the parameter table's row holds the callout's words without the leading `>`.
- `docs/manual/AGENTS.md` requires the "What works today" table to record a setting that is read and changes no generated code as "Accepted; no effect yet", and the Status column of `settings.md` to say which part works.  The spec's Phase 1 list does not name those two places; this phase adds them, and phase 2 flips them.

## Review Focus

1. `no-array-pool` written in upper or mixed case at each level: accepted, same flag.  Covered by Task 1's list and marker rows.
2. `default no-array-pool` in a property or on the attribute: the whole value is invalid and gives no list.  Covered by Task 1's list row.
3. A file-level `-- generator: no-array-pool` with a query-level list that omits it: the query uses the pool.  Covered by Task 1's level theory.
4. A `many` method of each collection type writes the same text with and without the parameter.  Covered by Task 1's unchanged-text theory over every `GeneratorCollectionType`.
5. A misspelt `no-arraypool` beside `no-array-pool` in a property: the bad word is `SQLSRC014` and the good one still applies.  Covered by Task 1.

---

### Task 1: The parameter, its flag and their tests

**Files:**
- Modify: `src/SqlSource/Settings/GeneratorParameters.cs`, `src/SqlSource/Settings/GeneratorParameterList.cs`, `src/SqlSource/Settings/QuerySettings.cs`, `src/SqlSource/Generation/Methods/QueryMethod.cs`, `src/SqlSource/Generation/Methods/MethodPlanner.cs`
- Modify tests: `tests/SqlSource.Tests/Settings/GeneratorParameterListTests.cs`, `tests/SqlSource.Tests/Parsing/Markers/SqlGeneratorParameterScopeTests.cs`, `tests/SqlSource.Tests/Settings/QuerySettingsTests.cs`, `tests/SqlSource.Tests/Generation/Emitting/QueryMethodWriterTests.cs` (the positional `GetUser`)
- Create test: `tests/SqlSource.Tests/Generator/ArrayPoolParameterTests.cs`

**Interfaces:**
- Produces: `GeneratorParameters.NoArrayPool = 64`; `"no-array-pool"` in `GeneratorParameterList.Names`, after `async-method-suffix`; `bool QuerySettings.UseArrayPool => (Parameters & GeneratorParameters.NoArrayPool) == 0`; `bool QueryMethod.UseArrayPool`, the positional parameter right after `ValidateTokens`, documented as whether a `Many` method may read its rows into buffers from the shared array pool, false under `no-array-pool`.

- [ ] **Step 1: Write the failing tests**
  - `GeneratorParameterListTests.Parse_ValidList_IsItsParameters`: rows `("no-array-pool", 64)` and `("No-Array-Pool keep-comments", 65)`.  `Parse_DefaultBesideAnotherWord_IsInvalidWhole`: row `("default no-array-pool", "default no-array-pool")`.
  - `SqlGeneratorParameterScopeTests.Read_KnownParameters_SetTheList`: rows `("-- generator: no-array-pool", 64)`, `("-- generator: NO-ARRAY-POOL", 64)`.  `Read_UnknownGeneratorParameter_IsAnError`: row `"no-arraypool"`.  `Read_DefaultBesideAnotherParameter_ConflictsAtTheSecond`: row `(new[] { "-- generator: default no-array-pool" }, "no-array-pool")`.
  - `QuerySettingsTests`: `UseArrayPool` is true for the default settings and false when the resolved list holds `NoArrayPool`.
  - `ArrayPoolParameterTests` (Generator folder, CodeGen on postgres via `GeneratorHarness.Run` with `NpgsqlReferences.Npgsql10`, the file and `Sidecars.Of` its entries, as `MethodRuns` does):
    - `Run_NoArrayPool_ComesWholeFromTheMostSpecificLevelThatGivesAList`: theory over (property, metadata, attribute, preamble, query, expected `UseArrayPool`), with `On = "no-array-pool"`, `Other = "sort-input"`, `Default = "default"`, rows mirroring `GeneratorParameterLevelTests.Run_GeneratorParameters_ComeWholeFromTheMostSpecificLevelThatGivesAList`, including `(null, null, null, On, Other) → true` (Review Focus 3).  Asserts `run.ShouldBeClean()` and `StepOutputs.Single<TypeMethods>(run, TrackingNames.TypeMethods).Methods` has a single method whose `UseArrayPool` is the expected value.
    - `Run_NoArrayPool_LeavesTheGeneratedTextAsItIs`: theory over each `GeneratorCollectionType` member name; the methods file under `Parameters = "no-array-pool"` equals the one without, and both runs are clean.
    - `Run_PropertyWithAMisspeltWordBesideNoArrayPool_IsAnErrorAndNoArrayPoolApplies`: property `"no-arraypool no-array-pool"` gives exactly `SQLSRC014 (1,1)-(1,1): 'no-arraypool' is not a valid value of SqlSourceGeneratorParameters`, and the method's `UseArrayPool` is false.
    - `Run_AttributeWithAMisspeltWordBesideNoArrayPool_IsAnErrorAtTheAttribute`: `Parameters = "no-array-pool nope"` gives one `SQLSRC006` naming `'nope'` and `Parameters`.

- [ ] **Step 2: Run them and see them fail**

  Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~GeneratorParameterListTests|FullyQualifiedName~SqlGeneratorParameterScopeTests|FullyQualifiedName~QuerySettingsTests|FullyQualifiedName~ArrayPoolParameterTests"`
  Expected: build errors for `NoArrayPool` and `UseArrayPool`.

- [ ] **Step 3: Implement the Interfaces block**, passing `settings.UseArrayPool` in `MethodPlanner`'s `new QueryMethod(...)` and `true` in `QueryMethodWriterTests.GetUser`.

- [ ] **Step 4: Run the same filter, then the RoslynFloor project**

  Run: the command of Step 2, then `dotnet test tests/SqlSource.Tests.RoslynFloor --filter FullyQualifiedName~ArrayPoolParameterTests`.  Expected: PASS.

- [ ] **Step 5: `./format.sh`, `./pre-commit-validation.sh`, commit.**  The validation fails on `DiagnosticsDocumentTests` only if Task 2's list is ahead of its doc; Task 1 does not touch either, so it passes.

### Task 2: Documentation

**Files:**
- Modify: `docs/manual/settings.md`, `docs/manual/diagnostics.md`, `docs/manual/README.md`, `docs/architecture/generator.md`, `tests/SqlSource.Tests/Diagnostics/DiagnosticsDocumentTests.cs`

- [ ] **Step 1: Add `"no-array-pool"` to the list in `Document_UnknownGeneratorParameterSection_ListsEveryGeneratorParameter`**, after `"async-method-suffix"`; run `dotnet test tests/SqlSource.Tests --filter FullyQualifiedName~DiagnosticsDocumentTests` and see it fail.
- [ ] **Step 2: `diagnostics.md`**, `SQLSRC109`: the list of parameters gains `no-array-pool` before `default`.  Run the filter again: PASS.
- [ ] **Step 3: `settings.md`**
  - Generator parameters row: `no-array-pool` in the Values cell after `async-method-suffix`; Status `Works today, except no-array-pool: [accepted; no effect yet](README.md#what-works-today)` (with the word in code).
  - Parameter table: a row after `async-method-suffix`: `` `no-array-pool` `` | `**Coming in a later release.**  A query method that returns many rows will read them into buffers borrowed from .NET's shared array pool, and no-array-pool will turn that off.  See [what works today](README.md#what-works-today).` (word in code).
- [ ] **Step 4: `docs/manual/README.md`**, "What works today": a row `The generator parameter no-array-pool` | `Accepted; no effect yet` | `[Generator parameters](settings.md#setting-generator-parameters)`, after the query methods' settings row, and beneath the table one sentence on what "Accepted; no effect yet" means: SqlSource reads and checks the setting, and it changes no generated code yet.
- [ ] **Step 5: `docs/architecture/generator.md`**: in "The settings have two stages", the method plan reads `no-array-pool` as `QuerySettings.UseArrayPool`; in the method plan's step 4, the `QueryMethod` also carries whether its rows may go through the shared array pool, false under `no-array-pool`, which the writer does not read yet.
- [ ] **Step 6: The manual's internals grep** (`docs/manual/AGENTS.md`) over the changed pages finds nothing new; `./format.sh`, `./pre-commit-validation.sh`, commit.
