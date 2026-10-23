# TD-0070: a `vector` column below Microsoft.Data.SqlClient 6.1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A project that references a Microsoft.Data.SqlClient older than 6.1 no longer gets a `vector` column typed by `SqlVector<float>`, which that version lacks; the query gets an error that is true for it instead of a compiler error in its model.

**Architecture:** `SqlClientProbe.Column` gains a fourth column, `OlderThan61`, for a project that references the driver without ``Microsoft.Data.SqlTypes.SqlVector`1``.  A row of `SqlServerTypeMap` whose C# type is SqlClient's own records the version that first has it (`"6.1"` for `vector`); under `None` it stays `NeedsDriver` (`SQLSRC269`), under `OlderThan61` it is a new failure kind, `NeedsNewerSqlClient`, whose detail is that version.  `QueryTyper.Diagnose` makes the new kind a new diagnostic, `SQLSRC270`, which names the column, its type, the version it needs and the version the project references.

**Tech Stack:** C# (netstandard2.0 generator, Roslyn 4.8.0 floor), xunit + Shouldly.

**Spec:** `docs/tech-debt/TD-0070-vector-column-below-sqlclient-6-1-does-not-compile.md` (Problem and Proposed fix).

## Where the code differs from the item

- The item names `SqlClientProbe.Column` and suggests returning `None`.  `None` means "no reference", and its failure is `SQLSRC269`, whose text says the project does not reference the driver; so a column of its own, `OlderThan61`, is used instead, and `None` keeps its meaning.
- Since td-0073, `SqlClientProbe` implements `IDriverProbe`, and `SQLSRC267` is one of its `Findings`.  This change adds no finding: `SQLSRC267` still reports the project once; `SQLSRC270` is a per-query type failure, reported from `FileTypes` like `SQLSRC269`.

## Owner decision (pending at the time of writing)

New id `SQLSRC270` (recommended) or `SQLSRC269` reworded, sent to the owner through the implementation manager.  Task 2 is written for `SQLSRC270`; if the owner chooses the rewording, Task 2's descriptor, docs and tests change to that wording and no id is added.  Task 1 does not depend on the answer.

## Global Constraints

- netstandard2.0 and Roslyn 4.8.0 API only in `src/SqlSource`; tests in `tests/SqlSource.Tests/Generator/` compile on Roslyn 4.8.0 too (C# 12 at most).
- The probe and `FileTypesInput` stay value-equal records; no Roslyn object between steps.
- Every diagnostic is an error tagged `NotConfigurable` unless it says why not; a new one touches the descriptor and `All`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, and `docs/manual/diagnostics.md`.  Write the descriptor out in full with a literal id.
- `SQLSRC270` is the next free id (sync with `main` before taking it: another unit may have taken it).
- Manual pages describe effects, never internals; no tech-debt ids in them.
- Phase 7 adds `SqlJson` detection and library-map rows to this probe and map: keep the column switch a flat read of the flags in version order, and the row's minimum version data on the row, so a later flag or row is one line.
- `./pre-commit-validation.sh` passes on the exact tree of every commit.  No describer or describer test changes, so no floor-image run is needed.

## Review Focus

1. A driver merged into an assembly of another name, below 6.1 (`Version` null): `SQLSRC270` says "references Microsoft.Data.SqlClient of an unknown version", as `SQLSRC267` does.  Pinned in Task 2's `QueryTyperTests`.
2. A `vector` parameter, not only a column, below 6.1: the same error, naming "parameter '@v'".  Pinned in Task 2's `QueryTyperTests` theory.
3. A type override of `vector` below 6.1 (`vector=float[]`): the override wins and no error is reported, as for every other row.  Pinned in Task 1's `SqlServerTypeMapTests`.
4. A below-floor project with no `vector` column: every other row still maps as the table says (no regression for EF Core 8/9 users, who are below the floor and get `SQLSRC267` alone).  Pinned by Task 1's row theory, whose new column is the table's.
5. The generated code of a below-floor project with a `vector` column compiles with no compiler error beside `SQLSRC267` and `SQLSRC270`.  Pinned in Task 2's `SqlClientProbeTests` run against `SqlClientReferences.SqlClient60`.

---

### Task 1: The column below 6.1, and the map's failure for it

**Files:**
- Modify: `src/SqlSource/Generation/Types/SqlClientColumn.cs`
- Modify: `src/SqlSource/Generation/Types/SqlClientProbe.cs` (`Column`)
- Modify: `src/SqlSource/Generation/Types/SqlServerTypeMap.cs` (`Row`, `BuildRows`)
- Modify: `src/SqlSource/Generation/Types/TypeMappingKind.cs`, `src/SqlSource/Generation/Types/TypeMapping.cs` (doc of `Detail`)
- Test: `tests/SqlSource.Tests/Generation/Types/SqlServerTypeMapTests.cs`, `tests/SqlSource.Tests/Generator/SqlClientProbeTests.cs`

**Interfaces:**
- Produces: `SqlClientColumn.OlderThan61`; `TypeMappingKind.NeedsNewerSqlClient`, a failure whose `TypeMapping.Detail` is the version the type first appears in (`"6.1"`).

- [ ] **Step 1: Write the failing tests**

In `SqlServerTypeMapTests`, add a column to `Rows`, `underOlder`, after `underNone` (every row but `vector` gives the same cell as `under61`), and assert it in `Map_EveryRowOfTheTable_GivesItsType`:

```csharp
private const string NeedsNewer = "!NeedsNewerSqlClient";
// ...
{ "vector", Vector, Vector, NeedsDriver, NeedsNewer, true },
// ...
ShouldMapTo(SqlServerTypeMap.Map(type, SqlClientColumn.OlderThan61), underOlder, isValueType, name);
```

Add:

```csharp
[Fact]
public void Map_VectorBelow61_NamesTheVersionItNeeds() =>
    SqlServerTypeMap.Map(TestSidecars.SqlServer(name: "vector(3)"), SqlClientColumn.OlderThan61)
        .Detail.ShouldBe("6.1");

// An entry of the type override wins below 6.1 as it does everywhere.
[Fact]
public void Map_VectorBelow61WithAnEntry_IsTheEntry() =>
    SqlServerTypeMap.Map(TestSidecars.SqlServer(name: "vector(3)"), SqlClientColumn.OlderThan61, Overrides(("vector", "Floats")))
        .Kind.ShouldBe(TypeMappingKind.Mapped);
```

(Use the file's existing `Overrides` helper and its existing assertions on an overridden mapping.)

In `SqlClientProbeTests.Read_EachDriver_GivesItsFlags`, the `"6.0"` case's column becomes `SqlClientColumn.OlderThan61`.  Add:

```csharp
[Theory]
[InlineData(false, false, false, SqlClientColumn.None)]
[InlineData(true, false, false, SqlClientColumn.OlderThan61)]
[InlineData(true, true, false, SqlClientColumn.SqlClient61)]
[InlineData(true, true, true, SqlClientColumn.SqlClient7)]
public void Column_Flags_IsTheNewestVersionTheProjectHas(bool referenced, bool atLeast61, bool atLeast7, SqlClientColumn expected) =>
    new SqlClientProbe(referenced, atLeast61, atLeast7, null).Column.ShouldBe(expected);
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~SqlServerTypeMapTests|FullyQualifiedName~SqlClientProbeTests"`
Expected: build errors for `OlderThan61` and `NeedsNewerSqlClient`.

- [ ] **Step 3: Implement**

- `SqlClientColumn`: `SqlClient61`'s doc drops "or an older one"; add `OlderThan61` ("A Microsoft.Data.SqlClient older than 6.1, which is `SQLSRC267`.  A type whose C# type it lacks, such as `vector`'s, has none.").
- `SqlClientProbe.Column`: one switch over the flags newest first: `AtLeast7` → `SqlClient7`, `AtLeast61` → `SqlClient61`, `Referenced` → `OlderThan61`, else `None`.  Update the `AtLeast61` param doc to say it also chooses the column.
- `SqlServerTypeMap.Row`: replace `bool IsOfSqlClient` with `string? SqlClientSince` (the version of Microsoft.Data.SqlClient that first has the C# type, or null when the type is not SqlClient's own).  `Row.OfSqlClient(text, isValueType, since)`; the `vector` row passes `"6.1"`.  `Under(column)`: a row with `SqlClientSince` is `NeedsDriver` under `None` and `TypeMapping.Failed(TypeMappingKind.NeedsNewerSqlClient, Key, SqlClientSince)` under `OlderThan61`.
- `TypeMappingKind.NeedsNewerSqlClient`: "The project references a Microsoft.Data.SqlClient older than the first version that has the type's C# type, which is `TypeMapping.Detail`: `vector` below 6.1."  Add it to the list in `TypeMapping.Detail`'s doc.
- Until Task 2, `QueryTyper.Diagnose` sends the new kind to its default branch; Task 2 gives it its case.

- [ ] **Step 4: Run the tests and see them pass**

Same command; expected PASS.  Also `dotnet test tests/SqlSource.Tests.RoslynFloor --filter "FullyQualifiedName~SqlClientProbeTests"`.

- [ ] **Step 5: Validate and commit**

`./pre-commit-validation.sh`, then commit "Give a Microsoft.Data.SqlClient below 6.1 a column of its own (TD-0070)".

### Task 2: `SQLSRC270`, its docs, and the item resolved

**Files:**
- Modify: `src/SqlSource/Diagnostics/SqlDiagnostics.cs` (descriptor after `TypeNeedsSqlClient`, and `All`)
- Modify: `src/SqlSource/AnalyzerReleases.Unshipped.md`
- Modify: `src/SqlSource/Generation/Types/QueryTyper.cs` (`Typing`, `Type`, `TypeOf`, `Diagnose`)
- Modify: `src/SqlSource/AGENTS.md` (the taken range: `SQLSRC200` to `SQLSRC270`, later phases from `SQLSRC271`)
- Modify: `docs/manual/diagnostics.md`, `docs/manual/sqlserver.md`, `docs/manual/generator.md` (line listing the type errors)
- Modify: `docs/architecture/generator.md` (the flowchart's `outTypes`, "The SqlClient probe", "The SQL Server map", step 3 of "The types of a query", and the bullet "A problem of a query's C# types")
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (the 6.4 bullet: a `vector` column below 6.1 is `SQLSRC270`)
- Delete: `docs/tech-debt/TD-0070-vector-column-below-sqlclient-6-1-does-not-compile.md`; remove its row from `docs/tech-debt/README.md`
- Test: `tests/SqlSource.Tests/Diagnostics/SqlDiagnosticsTests.cs`, `tests/SqlSource.Tests/Generation/Types/QueryTyperTests.cs`, `tests/SqlSource.Tests/Generator/SqlClientProbeTests.cs`

**Interfaces:**
- Consumes: `SqlClientColumn.OlderThan61`, `TypeMappingKind.NeedsNewerSqlClient` with `Detail` the needed version (Task 1); `SqlClientProbe.VersionText`.
- Produces: `SqlDiagnostics.TypeNeedsNewerSqlClient` (`SQLSRC270`).

- [ ] **Step 1: Write the failing tests**

`SqlDiagnosticsTests`:

```csharp
[Fact]
public void TypeNeedsNewerSqlClient_Message_HoldsBothVersions() =>
    Format(SqlDiagnostics.TypeNeedsNewerSqlClient, "column 'embedding'", "vector(3)", "6.1", "6.0.5")
        .ShouldBe(
            "The column 'embedding' has the type 'vector(3)', whose C# type needs Microsoft.Data.SqlClient 6.1 or "
                + "later, and this project references Microsoft.Data.SqlClient 6.0.5.  Update the package."
        );
```

`QueryTyperTests` (add `SqlClient60 = new(true, false, false, "6.0.5")` and `MergedSqlClient60 = new(true, false, false, null)` beside the file's probes):

```csharp
[Theory]
[InlineData(true, "column 'c0'")]
[InlineData(false, "parameter '@v'")]   // match the subject text the file's other parameter cases use
public void Type_VectorBelowSqlClient61_IsSqlsrc270(bool asColumn, string subject)
// entry with one vector(3) column, or one vector(3) parameter; InputOf(..., SqlClient60)
// Queries empty; single diagnostic: Descriptor TypeNeedsNewerSqlClient, Arguments [subject, "vector(3)", "6.1", "6.0.5"]

[Fact]
public void Type_VectorBelowSqlClient61OfAnUnknownVersion_SaysTheVersionIsUnknown()
// MergedSqlClient60: Arguments [..., "6.1", "of an unknown version"]
```

`SqlClientProbeTests` (a driver run, so it compiles the generated model):

```csharp
// A vector column below 6.1 has no C# type, so its model is not generated and the project compiles beside its
// two errors: the project's, and the query's.
[Fact]
public void Run_VectorColumnBelowSqlClient61_IsSqlsrc270AndCompiles()
// sidecar entry of GetUser under mssql with one column "embedding" of TestSidecars.SqlServer("vector(3)", 20),
// built as SqlServerModelTests.Run builds its entry (TestSidecars.Entry + Sidecars.Of), at OutputKind.Models,
// references [SqlClientReferences.SqlClient60]
// run.Diagnostics contains the SQLSRC267 line and exactly one SQLSRC270 line:
//   "SQLSRC270 (1,10)-(1,17): The column 'embedding' has the type 'vector(3)', whose C# type needs
//    Microsoft.Data.SqlClient 6.1 or later, and this project references Microsoft.Data.SqlClient 6.0.5.  Update the package."
//   (take the span the harness prints for the query's name)
// run.CompilationErrors.ShouldBeEmpty(); run.GeneratedCodeWarnings.ShouldBeEmpty();
// the same entry on SqlClientReferences.SqlClient61 has no SQLSRC270 and no compilation error
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~SqlDiagnosticsTests|FullyQualifiedName~QueryTyperTests|FullyQualifiedName~SqlClientProbeTests"`
Expected: build error for `TypeNeedsNewerSqlClient`.

- [ ] **Step 3: Implement the diagnostic**

- Descriptor, in full, after `TypeNeedsSqlClient`, with a comment naming its arguments ({0} the column or parameter, {1} its type as the database spells it, {2} the version that first has its C# type, {3} the version the project references, as `SqlClientProbe.VersionText` gives it):
  - id `"SQLSRC270"`, title `"Type needs a newer Microsoft.Data.SqlClient"`, message `"The {0} has the type '{1}', whose C# type needs Microsoft.Data.SqlClient {2} or later, and this project references Microsoft.Data.SqlClient {3}.  Update the package."`, error, `NotConfigurable`, help `HelpLinkBase + "sqlsrc270"`.  Add it last to `All`.
- `AnalyzerReleases.Unshipped.md`: `SQLSRC270 | SqlSource | Error | Type needs a newer Microsoft.Data.SqlClient` after `SQLSRC269`.
- `QueryTyper`: `Typing` gains `string SqlClientVersion` (filled from `input.SqlClientProbe.VersionText`); `Diagnose` takes it and gets the case `TypeMappingKind.NeedsNewerSqlClient => DiagnosticInfo.Create(SqlDiagnostics.TypeNeedsNewerSqlClient, at, subject, typeName, failure.Detail ?? "", sqlClientVersion)`.  Update the comment above `Diagnose` (NeedsNewerSqlClient is SQL Server's alone).

- [ ] **Step 4: Run the tests and see them pass**

Same command, then `dotnet test tests/SqlSource.Tests.RoslynFloor --filter "FullyQualifiedName~SqlClientProbeTests"`.  Expected PASS.  `DiagnosticsDocumentTests` fails until Step 5.

- [ ] **Step 5: Docs, and the item resolved**

- `docs/manual/diagnostics.md`: a row `| [SQLSRC270](#sqlsrc270) | Type needs a newer Microsoft.Data.SqlClient |` after `SQLSRC269` in the last table; the paragraph on `SQLSRC269` covers `SQLSRC270` too ("`SQLSRC269` and `SQLSRC270` are reported ..."); a section `## SQLSRC270` after `SQLSRC269`'s: what it means (the project references a Microsoft.Data.SqlClient older than 6.1, and a column or parameter of an `mssql` query is `vector`, whose C# type `Microsoft.Data.SqlTypes.SqlVector<float>` first appears in 6.1), the console sample with the message above at `/work/App/Queries/Documents.sql(1,10)`, the fix (update the package to 6.1 or later, which also clears SQLSRC267; EF Core 8 and 9 bring an older one), that it is at the query's name once for the query and the query has no models while its constant or method is still generated, and `See: [Which version of Microsoft.Data.SqlClient](sqlserver.md#which-sqlclient)`.  `SQLSRC267`'s section: a sentence that a `vector` column or parameter also gets SQLSRC270 at its query.
- `docs/manual/sqlserver.md`: the "older than 6.1" bullet of "Which version" adds that every type but `vector` gets the table's type, and `vector` is SQLSRC270 until the update; the `vector` row of the table reads "`Microsoft.Data.SqlTypes.SqlVector<float>`.  Without Microsoft.Data.SqlClient, SQLSRC269; with one older than 6.1, SQLSRC270"; remove the known issue "A `vector` column in a project that references a Microsoft.Data.SqlClient older than 6.1".
- `docs/manual/generator.md`: add `[SQLSRC270](diagnostics.md#sqlsrc270)` to the list of errors that leave a query without models.
- `docs/architecture/generator.md`: `OlderThan61` in the SqlClient probe's paragraph (chosen by `Referenced` without `AtLeast61`); in the SQL Server map's, a row whose type is SqlClient's own records the version that first has it, and is `NeedsDriver` under `None` and `NeedsNewerSqlClient` under `OlderThan61`; `SQLSRC270` beside `SQLSRC269` in the flowchart, step 3 (`NeedsNewerSqlClient` is `SQLSRC270`, which names both versions) and the bullet of a query's C# types.
- `src/SqlSource/AGENTS.md`: `SQLSRC200` to `SQLSRC270` are taken; a later phase takes ids from `SQLSRC271`.
- Epic 6.4 bullet: "...`SQLSRC269` without the driver, and `SQLSRC270` for a driver older than 6.1, ...".
- Delete the item file, remove its row from `docs/tech-debt/README.md`, and `git grep -n "TD-0070"` finds nothing outside `docs/superpowers/plans/`.
- Run the manual's internals grep over the changed manual pages (`docs/manual/AGENTS.md`).

- [ ] **Step 6: Validate and commit**

`./pre-commit-validation.sh`, then commit "Report a vector column below Microsoft.Data.SqlClient 6.1 as SQLSRC270 (TD-0070)".
