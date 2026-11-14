# Test cleanup phase 30: tests/SqlSource.Tool.Tests/Describing/Postgres, folds

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/Describing/Postgres`
- Findings: 2 (2 fold)
- Kind: one kind, fold
- Changed lines: about 9
- Guard: mutation (tool-describing-postgres)

## Files

- `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionPlanTests.cs`
- `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/NullabilityWalkNameTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests.Describing.Postgres-2-06

- test: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionPlanTests.cs` | `PostgresSessionPlanTests.DescribeAsync_GatedQueryWithoutAPlanTheWalkCanUse_IsDescribedByTheHeuristicAndUnavailable` | rows: `"empty-list"` (line 211)
- action: fold
- evidence: measured (5 rows; kills 198, alone 0, timeouts under it 9; rows needed 2) and read: the rows `"text-that-is-no-json"` (:210) and `"empty-list"` (:211) both take the one branch of src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs:336-355 where `PlanNode.Read` gives null, and the test asserts the same three things of each (:237-239); which texts `PlanNode.Read` refuses is proved row by row by `PlanNodeTests.Read_TextThatIsNotAnArrayOfOnePlan_IsNull`, whose rows hold both `"not json"` and `"[]"` (tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/PlanNodeTests.cs:191-194).  The timeouts under the theory are not from this row alone, and the other rows stay.  The grep of the documents names neither the test nor its class; the comment above the two rows says only "The text is no plan."; blind: no
- cover: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionPlanTests.cs` | `PostgresSessionPlanTests.DescribeAsync_GatedQueryWithoutAPlanTheWalkCanUse_IsDescribedByTheHeuristicAndUnavailable` (row `"text-that-is-no-json"`, line 210); `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/PlanNodeTests.cs` | `PlanNodeTests.Read_TextThatIsNotAnArrayOfOnePlan_IsNull`
- proposal: Drop the row `"empty-list"` from `PlansThatCannotBeUsedRows`: the session's handling of a text that is no plan is held by the row `"text-that-is-no-json"`, and the reader's refusal of `[]` by PlanNodeTests.
- verdict: confirmed
- verifier's reason: The rows `"text-that-is-no-json"` and `"empty-list"` (tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionPlanTests.cs:210-211) both have no error and no relations, so they run the same script through `DescribeUsersIdAsync` and the same assertions (:229-239).  Both texts make `PlanNode.Read` return null: "not json" through the `JsonException` catch, and "[]" through `GetArrayLength() == 1` (src/SqlSource.Tool/Describing/Postgres/Walk/PlanNode.cs:140-157).  Both then take the `else` arm, `Unavailable(WalkReasons.Unreadable)` (src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs:336-355).  What the "[]" row alone reaches is the array checks of PlanNode.cs:147-148.  `kills` for `PlanNodeTests.Read_TextThatIsNotAnArrayOfOnePlan_IsNull`, whose rows hold "not json" and "[]" (tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/PlanNodeTests.cs:192, :194), kills 147:17 (three mutants) and 148:20, with no timeouts under it.  The theory's nine timeouts are in PostgresIdentifiers, PostgresSearchPath and StatementShape, which every row runs, and in PlanExpression and PlanTokens, which only the row `"plan-the-walk-cannot-use"` reaches, so none is the "[]" row's alone.  The rows share a name, so a filter cannot measure the fold: this is confirmed by reading, and the guard of the cleanup phase proves it.  The grep of the documents names neither the test nor its class, and the comment above the two rows (:209) says only "The text is no plan.".

### F-SqlSource.Tool.Tests.Describing.Postgres.Walk-1-09

- test: `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/NullabilityWalkNameTests.cs` | `NullabilityWalkNameTests.Run_AliasOfANodeThatIsNoScanOfARelation_IsUnmappable` | rows: `"Values Scan"` (line 51), `"WorkTable Scan"` (line 52)
- action: fold
- evidence: measured (5 rows, kills 35, alone 0, rows needed 1; cover `Run_BareNameUnderAJoin_IsUnmappable`, `NullabilityWalkSubqueryTests.Run_ColumnThroughASubqueryScan_IsAnsweredByItsOrigin`) and read (a `Function Scan`, a `Values Scan` and a `WorkTable Scan` name no instance, so the walk's `TryFind` misses each of them the same way.  Which node types name none is `PlanInstancesTests.Find_NodeThatIsNoScanOfARelation_NamesNone`, whose rows are those three and `Result` (PlanInstancesTests.cs:146-157).  `Subquery Scan` and `CTE Scan` are another path, instances of a plan with no child to resolve in, and stay, as does `Function Scan` for the path of no instance.  No comment or document makes the rows a record); blind: no
- cover: `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/PlanInstancesTests.cs` | `PlanInstancesTests.Find_NodeThatIsNoScanOfARelation_NamesNone`
- proposal: Drop the `Values Scan` and `WorkTable Scan` rows.  The walk keeps one row for an alias of no instance and two for a plan's scan with nothing to resolve in, and `PlanInstancesTests` keeps each node type's classification.
- verdict: confirmed
- verifier's reason: The theory is NullabilityWalkNameTests.cs:47-61, with no comment above it.
  - **No code names the dropped node types:** `PlanInstances` names a relation only for `RelationScans` (PlanInstances.cs:147, 227, 255), and an instance of a plan only for `Subquery Scan` or `CTE Scan` (:339). No source under src/SqlSource.Tool/Describing/Postgres names `Values Scan`, `WorkTable Scan` or `Function Scan` (grep), so all three miss `TryFind` alike.
  - **The node types' classification:** `PlanInstancesTests.Find_NodeThatIsNoScanOfARelation_NamesNone` (PlanInstancesTests.cs:145-157) has the rows `Function Scan`, `Values Scan`, `WorkTable Scan` and `Result`, and asserts no instance, no bare alias and no reason.
  - **Rows kept:** `Function Scan` keeps the walk's no-instance path, and `Subquery Scan` and `CTE Scan` keep the path of a plan instance with no child.
  - **Measurement:** the evidence gives 0 timeouts and "rows needed 1". This rests on reading, and the phase's own guard proves it.
  - **Records:** the documents name neither the test nor `NullabilityWalkNameTests`.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-describing-postgres`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-30/tool-describing-postgres/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Describing.Postgres.' 'Describing/Postgres/**/*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-30/tool-describing-postgres/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Describing.Postgres.' 'Describing/Postgres/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-30/tool-describing-postgres/before/reports/mutation-report.json artifacts/test-cleanup/phase-30/tool-describing-postgres/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-30/tool-describing-postgres/before/reports/mutation-report.json PostgresSessionPlanTests.DescribeAsync_GatedQueryWithoutAPlanTheWalkCanUse_IsDescribedByTheHeuristicAndUnavailable
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-30/tool-describing-postgres/before/reports/mutation-report.json NullabilityWalkNameTests.Run_AliasOfANodeThatIsNoScanOfARelation_IsUnmappable
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-describing-postgres`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-30/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-30/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-30/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-30/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-30/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-30/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
