# TD-0080 item 11: split the classes that mix subjects, implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Each of the three classes that item 11 names keeps one subject, and the tests of the others move, unchanged, into classes of their own.

**Architecture:** Members move whole, with their comments and attributes, so that no test changes.  `PlanOutputTests` keeps its `Explain_*` tests and the six `Catalog_*` pins become `CatalogPinTests` beside it, on the same collection and fixture.  `SettingsFileTests` keeps which level of the settings wins, and the tests of the folders the build gives in `SqlSourceBuildFolders` become `SettingsFileBuildFoldersTests`, with the two helpers and two constants only they use.  `ConverterMethodTests` keeps the converters' methods and diagnostics; the obsolete and experimental tests become `ConverterObsoleteTests` and the array tests `ConverterArrayTests`.  The three converter classes share the members they all use through a static helper, `ConverterCases` of `tests/SqlSource.Testing/Generator/`, since `tests/AGENTS.md` asks split classes to share through a static helper and no base class, and `tests/SqlSource.Tests/Generator/AGENTS.md` puts every helper of that folder in the library that RoslynFloor compiles against.

**Tech Stack:** .NET 10 SDK, xunit v3, Shouldly, Roslyn 4.8.0 for the files of `Generator/`.

**Spec:** `docs/tech-debt/TD-0080-duplicated-arrangements-and-uneven-conventions-in-tests.md`, item 11 of "Problem" and of "Proposed fix".

## Global Constraints

- No test checks anything different, and the counts of every project stay the same.  The one change of an arrangement: the moved settings-file tests give their `.sql` files `SqlTexts.GetUser` in place of `SettingsFileTests`' backtick `Query`, which only the dialect tests need; every moved test still passes.
- Each new file of `Generator/` starts with the shared line and ends in `Tests.cs`; `TestLayoutTests` checks it.
- The source comments that name a moved test by its class (`BuiltInTypes`, `TotalFunctions`, `SetOperationTypes`, `CatalogOverloads`) and `docs/architecture/postgres-nullability.md` name `CatalogPinTests`.
- `tests/DatabaseTests/Postgres` passes on its floor image too: `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`.
- Every commit passes `./pre-commit-validation.sh`; the push passes `--full`.  Only item 11 of TD-0080 is struck, and its row in `docs/tech-debt/README.md` describes only what remains.

## Review Focus

- A member that both halves use stays reachable from both: the build of `tests/SqlSource.Tests` and of RoslynFloor proves it.
- A test that moved loses no attribute (`[Theory]`, `[InlineData]`, `[SuppressMessage]`): the counts of the projects prove it.

---

### Task 1: Item 11

**Files:**
- Create: `tests/DatabaseTests/Postgres/Describer/CatalogPinTests.cs`, `tests/SqlSource.Tests/Generator/SettingsFileBuildFoldersTests.cs`, `tests/SqlSource.Tests/Generator/ConverterObsoleteTests.cs`, `tests/SqlSource.Tests/Generator/ConverterArrayTests.cs`, `tests/SqlSource.Testing/Generator/ConverterCases.cs`
- Modify: the three split classes, the four source files above, `docs/architecture/postgres-nullability.md`, `tests/README.md`, `tests/SqlSource.Tests/Generator/AGENTS.md`, TD-0080 and its row.

- [ ] **Step 1:** Move the members as above, by name, with a script that takes each member from its leading comment to its end.
- [ ] **Step 2:** Build `tests/SqlSource.Tests`, `tests/SqlSource.Tests.RoslynFloor` and `tests/DatabaseTests/Postgres`; run the first two and see the counts unchanged.
- [ ] **Step 3:** Update the comments and documents; strike item 11.
- [ ] **Step 4:** `./format.sh`, `./pre-commit-validation.sh`, commit; before the push, run `tests/DatabaseTests/Postgres` on `postgres:14.24`.
