# Test cleanup phase 56: tests/SqlSource.Tool.Tests/Describing/Postgres, mixed actions

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/Describing/Postgres`
- Findings: 4 (1 fold, 3 strengthen)
- Kind: mixed: its files hold findings of several kinds, or a finding ties them to such a file
- Changed lines: about 31
- Guard: mutation (tool-describing-postgres), with covers and run time for 3 findings

## Files

- `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/NullabilityWalkTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests.Describing.Postgres.Walk-1-08

- test: `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/NullabilityWalkTests.cs` | `NullabilityWalkTests.Run_ColumnOfATableWhoseEntryCannotBeMapped_KeepsTheHeuristic` | rows: `"(a.id)"` (line 31), `"COALESCE(a.id, b.id)"` (line 32), `"$1"` (line 33), `"id"` (line 35)
- action: fold
- evidence: measured (6 rows, kills 34, alone 0, rows needed 2; cover `NullabilityWalkUnusablePlanTests.Run_Column_KeepsItsEntryItsReferenceAndWhatWasFound`, `NullabilityWalkNameTests.Run_AliasAtAModifyTableThatIsNotTheTargetsWhereOldOrNewMayBeNamed_KeepsTheHeuristic`) and read (in `ColumnOf`, src/SqlSource.Tool/Describing/Postgres/Walk/NullabilityWalk.cs:269-297, an entry is read only through `OutputReference.Parse`.  `(a.id)`, `COALESCE(...)` and `$1` parse to null and take the same path as the kept row `a.id + 1`, and the parser's verdict on each is `OutputReferenceTests.Parse_Expression_IsNoReference`'s rows `(u.id)`, `COALESCE(u.id, p.id)` and `$1` (OutputReferenceTests.cs:32, 36, 40).  The bare `id` under a join is `NullabilityWalkNameTests.Run_BareNameUnderAJoin_IsUnmappable`, which asserts the heuristic for the bare `opt` of the same `LeftJoin` (NullabilityWalkNameTests.cs:80-81).  The rows `a.id + 1` and `old.id`, an alias of no instance, stay.  No comment or document makes the rows a record); blind: no
- cover: `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/OutputReferenceTests.cs` | `OutputReferenceTests.Parse_Expression_IsNoReference`; `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/NullabilityWalkNameTests.cs` | `NullabilityWalkNameTests.Run_BareNameUnderAJoin_IsUnmappable`
- proposal: Drop the four named rows and keep `a.id + 1` and `old.id`.  The parser's tests keep which texts are no reference, and this theory keeps the walk's answer for an entry that is none.
- verdict: confirmed
- verifier's reason: The theory is NullabilityWalkTests.cs:29-37, with no comment above it.
  - **Parse-null rows:** `(a.id)`, `COALESCE(a.id, b.id)` and `$1` parse to null. They then take the same path as the kept `a.id + 1`: `OutputFits` skips a null reference (NullabilityWalk.cs:250), and `ColumnOf` resolves `Unknown`, then the heuristic (:278-297).
  - **The parser's verdict:** it is held by `OutputReferenceTests.Parse_Expression_IsNoReference` rows `$1` (:32), `COALESCE(u.id, p.id)` (:36) and `(u.id)` (:40). The finding pairs the texts with those lines in another order, but the set is right.
  - **The `id` row:** the bare `id` under the `LeftJoin` resolves through `PlanInstances.BareAlias`, which is null under a join. `NullabilityWalkNameTests.Run_BareNameUnderAJoin_IsUnmappable` (NullabilityWalkNameTests.cs:79-81) asserts the heuristic for the bare `opt` of the same `LeftJoin`. A wrong mapping to `a` would give `NullableCatalog` there and to `b` `NullableOuterJoin`, so it guards the same path.
  - **Rows kept:** `old.id`, an alias of no instance, keeps the `TryFind` miss, which is a path of its own.
  - **Measurement:** the evidence gives 0 timeouts and "rows needed 2". A filter cannot measure dropping rows, so this rests on reading, and the cleanup phase's own guard proves it.
  - **Records:** the documents name neither the test nor the class.

### F-SqlSource.Tool.Tests.Describing.Postgres.Walk-1-01

- test: `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/NullabilityWalkTests.cs` | `NullabilityWalkTests.Run_UngatedQueryWhoseReferenceNamesAnotherColumn_IsUnavailableAndKeepsTheCatalogsAnswer` | rows: all
- action: strengthen
- evidence: read (the name promises `unavailable`, but the test asserts only `Reason` and the answer, NullabilityWalkTests.cs:113-119.  In the source the ungated `!fits` return of `Unusable` at src/SqlSource.Tool/Describing/Postgres/Walk/NullabilityWalk.cs:159-162 is what makes the plan `unavailable`; without it the final return at NullabilityWalk.cs:189-194 still gives `Reason = output-differs` with `Plan = walked`, and the ungated column with an origin is `Kept` at NullabilityWalk.cs:184, so it is still `NotNullCatalog`.  The test would pass with that return removed.  No other test asserts `output-differs` with `unavailable` (grep of tests/ finds `OutputDiffers` only in NullabilityWalkTests.cs:97, 108, 117).  Measured: kills 56, alone 0, cover `Run_ReferenceThatNamesAnotherColumnUnderALeftJoin_KeepsTheJoinsAnswer`, a gated test that cannot see this either); blind: yes (NullabilityWalk.cs:160-162 does not compile under mutation)
- cover: none
- proposal: Add `outcome.Plan.ShouldBe(SidecarValues.Plan.Unavailable);`, the half of the name the test does not assert.
- verdict: confirmed
- verifier's reason: The test, NullabilityWalkTests.cs:112-119, asserts only `Reason == OutputDiffers` and `[NotNullCatalog]`. In src/SqlSource.Tool/Describing/Postgres/Walk/NullabilityWalk.cs:158-162, removing the ungated `Unusable` return lets the run reach the loop. There `expressions` is null because `fits` is false, the column with an origin falls to the `Kept` arm at :184, which keeps the catalog's answer it was built with (PostgresWalkColumns.cs:103-107 builds with `gated: false`), and the return at :189-194 still gives `OutputDiffers` with `Plan = walked`. The test would pass, though its name promises `unavailable`. `OutputDiffers` and `output-differs` appear in tests/ only at NullabilityWalkTests.cs:97, 108 and 117, so nothing else asserts `unavailable` for this case. Lines 160-162 are blind in the evidence. Adding the `Plan` assertion fixes what the name promises.
- survivors to kill: none named by place; the missing assertion is the proposal's

### F-SqlSource.Tool.Tests.Describing.Postgres.Walk-1-02

- test: `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/NullabilityWalkTests.cs` | `NullabilityWalkTests.Run_UnmappedReferenceAndExpression_PassTheCheck` | rows: all
- action: strengthen
- evidence: read (the test is gated, through `Run`, and asserts only `Plan == walked`, NullabilityWalkTests.cs:132-133.  A gated plan that fails `OutputFits` is still `walked`, with `Reason = output-differs` (src/SqlSource.Tool/Describing/Postgres/Walk/NullabilityWalk.cs:158-194, and NullabilityWalkTests.cs:96-97, which asserts exactly that).  If `OutputFits` (NullabilityWalk.cs:244-264) counted the unmapped `x.other` against the origin, the check would fail and the test would still pass, and the columns with an origin are walked either way, so the answers would not show it.  Measured: kills 24, alone 0, cover `Run_ColumnOfAForeignTable_SaysForeignTableUnlessAJoinExtendsIt`, which does not assert the reason either); blind: yes (NullabilityWalk.cs:244-264 does not compile under mutation)
- cover: none
- proposal: Assert `outcome.Reason.ShouldBeNull()`, which is what passing the check means for a gated query; the `Plan` assertion can stay or go.
- verdict: confirmed
- verifier's reason: NullabilityWalkTests.cs:131-133 runs gated, through `Run` (PostgresWalkColumns.cs:80-91), and asserts only `Plan == walked`. In NullabilityWalk.cs:158-194, a gated run is `walked` whatever `OutputFits` returns, and only `Reason` changes to `OutputDiffers`, which :96-97 shows. Both columns have an origin (`NotNull`), so they go through `ColumnOf` and the expression reader is never used: their answers do not depend on the check. If the check at :247-257 counted `x.other`, an alias of no instance, against the origin `id`, the test would still pass. Lines 244-264 are blind. `Reason.ShouldBeNull()` is what "pass the check" means.
- survivors to kill: none named by place; the missing assertion is the proposal's

### F-SqlSource.Tool.Tests.Describing.Postgres.Walk-1-03

- test: `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/NullabilityWalkTests.cs` | `NullabilityWalkTests.Run_ViewsColumnPrintedUnderTheBaseTablesName_IsWalked` | rows: all
- action: strengthen
- evidence: read (the comment, NullabilityWalkTests.cs:121, and docs/architecture/postgres-nullability.md:96 say a view's column printed under the base table's column passes `OutputFits`, which compares only a table's origin, through `ColumnNullability.IsTable` at src/SqlSource.Tool/Describing/Postgres/Walk/NullabilityWalk.cs:249.  The origin `OfView` is the view's attribute 1, `id` (tests/SqlSource.Tool.Tests/PostgresWalkColumns.cs:25, 47), and the entry is `t.opt`, so without that guard the check fails.  The query is gated, so the plan is still `walked` and the column, walked through its origin, is still `UnknownView`.  The test asserts only those two, NullabilityWalkTests.cs:127-128, and passes either way.  Measured: kills 31, alone 0, cover `Run_ColumnOfAView_SaysViewUnlessAJoinExtendsIt`, whose entries name `id` and never reach the guard); blind: yes (NullabilityWalk.cs:244-264 does not compile under mutation)
- cover: none
- proposal: Assert `outcome.Reason.ShouldBeNull()`, so that the test fails when the view's column is compared as a table's.
- verdict: confirmed
- verifier's reason: `OfView` is `View` attribute 1, `id` (PostgresWalkColumns.cs:25, 47), and the entry is `t.opt`, a relation instance of the scan `t`. Without the `ColumnNullability.IsTable(origin.Table)` guard at NullabilityWalk.cs:249, `opt` is not `id` and `OutputFits` is false. The run is gated, so it is still `walked` (:159, :189-191). `ColumnOf` finds `t` preserved and answers `ColumnNullability.Of(view origin, gated: false)`, which is `(null, View)` (ColumnNullability.cs:43), so the answer is still `UnknownView`. The test asserts only those two things (NullabilityWalkTests.cs:127-128), so it passes either way. The comment at :121 and docs/architecture/postgres-nullability.md:96 make the check the behaviour under test. Lines 244-264 are blind.
- survivors to kill: none named by place; the missing assertion is the proposal's

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-describing-postgres`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-56/tool-describing-postgres/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Describing.Postgres.' 'Describing/Postgres/**/*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-56/tool-describing-postgres/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Describing.Postgres.' 'Describing/Postgres/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-56/tool-describing-postgres/before/reports/mutation-report.json artifacts/test-cleanup/phase-56/tool-describing-postgres/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-56/tool-describing-postgres/before/reports/mutation-report.json NullabilityWalkTests.Run_UngatedQueryWhoseReferenceNamesAnotherColumn_IsUnavailableAndKeepsTheCatalogsAnswer
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-56/tool-describing-postgres/before/reports/mutation-report.json NullabilityWalkTests.Run_UnmappedReferenceAndExpression_PassTheCheck
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-56/tool-describing-postgres/before/reports/mutation-report.json NullabilityWalkTests.Run_ViewsColumnPrintedUnderTheBaseTablesName_IsWalked
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-56/tool-describing-postgres/before/reports/mutation-report.json NullabilityWalkTests.Run_ColumnOfATableWhoseEntryCannotBeMapped_KeepsTheHeuristic
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tool.Tests.Describing.Postgres.Walk-1-01: no survivor is named by place; the test fails when the assertion it adds is removed, checked once by hand
- F-SqlSource.Tool.Tests.Describing.Postgres.Walk-1-02: no survivor is named by place; the test fails when the assertion it adds is removed, checked once by hand
- F-SqlSource.Tool.Tests.Describing.Postgres.Walk-1-03: no survivor is named by place; the test fails when the assertion it adds is removed, checked once by hand

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-describing-postgres`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-56/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-56/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-56/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-56/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-56/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-56/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

### Covers and run time

Mutation cannot guard these findings, whose test is blind, reaches no tested mutant, or has its subject outside the scope: F-SqlSource.Tool.Tests.Describing.Postgres.Walk-1-01, F-SqlSource.Tool.Tests.Describing.Postgres.Walk-1-02, F-SqlSource.Tool.Tests.Describing.Postgres.Walk-1-03.  For them, and for the database tests of the phase if it has any, the pull request lists each removed test or row beside its named cover, and the test project's run time before and after the change, on one machine.

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tool.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
