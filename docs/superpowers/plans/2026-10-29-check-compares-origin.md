# describe --check compares origin Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `describe --check` reports a sidecar entry out of date when a column's `origin` differs, so that it fails whenever a fresh `describe` would change the generated code.

**Architecture:** `SidecarComparer.FindDifference` compares each column's `origin` after its `nullable`: presence first, then `schema`, `table` and `column`, ordinally.  The difference is an ordinary path of the default arm of `SidecarCheck.FoundOf`, so it is `SQLSRC219` with `columns[i].origin.<part> is "<committed>", and the database says "<described>"` and the `describe --force` help line.  No new diagnostic and no new reason.

**Tech Stack:** C# (.NET), xUnit v3, Shouldly, Testcontainers (PostgreSQL, SQL Server).

**Spec:** `docs/tech-debt/TD-0091-check-does-not-compare-a-columns-origin.md`, and the unit brief: "make `describe --check` report a sidecar entry stale when a column's `origin` differs".

## Global Constraints

- Compare `origin` in `FindDifference` after `nullable`, with a test in `SidecarComparerTests` for each part (spec).
- What `--check` prints follows the existing differences' pattern: the default arm of `SidecarCheck.FoundOf`, `ForceHelp`.
- Ordinal comparison of each part: `ColumnTypeMap.Find` matches a quoted key part exactly, so a change of case alone can change the generated type.
- Resolve TD-0091 per `docs/tech-debt/AGENTS.md`: delete the item file, its row in `docs/tech-debt/README.md`, every `TD-0091` reference, and the known issue in `docs/manual/tool.md`.
- No sidecar format change: `origin` is already written and read; the schema's `origin` description stays true.

## Where merged code differs from the spec

- `src/SqlSource/Snapshot/AGENTS.md`'s rule ("the keys its summary lists") stays true once the summary lists `origin`; it needs no edit.
- `src/SqlSource.Tool/README.md` does not say what `--check` compares; no edit.
- `docs/manual/diagnostics.md` also lists what is compared (SQLSRC219's "What is compared" bullet, and the table row of a difference the database reports): both are updated, which the spec did not name.

## Review Focus

- A column with an origin on one side and none on the other (an expression replaced by a table column): `columns[i].origin is an object, and the database says null` (Task 1 test).
- A difference of case alone in a part (`Users` vs `users`): reported, since a quoted column-type key matches exactly (Task 1 test).
- A committed sidecar on a server of another version, floor or nightly image, whose origins differ from the default image's: the end-to-end `CheckTests` would now fail.  Run both engines' `CheckTests` on the floor and nightly images (Task 3, step 5).
- A table moved to another schema the `search_path` still finds (PostgreSQL), a synonym re-pointed at another table (SQL Server): `--check` exits 2 with the origin path (Task 3 tests).
- Two differences in one entry, nullable and origin of one column, origin of column 0 and name of column 1: the first in the order is reported (Task 1 test).

---

### Task 1: Compare origin in `SidecarComparer`

**Files:**
- Modify: `src/SqlSource/Snapshot/SidecarComparer.cs` (summary; `Columns`; new `Origin`)
- Test: `tests/SqlSource.Tests/Snapshot/SidecarComparerTests.cs`
- Test: `tests/SqlSource.Tool.Tests/Commands/CheckTests.cs`

**Interfaces:**
- Produces: difference paths `columns[i].origin` (presence, `an object` / `null`), `columns[i].origin.schema`, `.table`, `.column`, values quoted as JSON.

- [ ] **Step 1: Write the failing tests** in `SidecarComparerTests` (column 0 of `Sample()` has origin `("public", "users", "id")`):
  - `FindDifference_Origin_IsComparedAsAnObjectAndThenByItsKeys`:
    - `Origin = null` → `("columns[0].origin", "an object", "null")`
    - `Origin = new SidecarOrigin("sales", "users", "id")` → `("columns[0].origin.schema", "\"public\"", "\"sales\"")`
    - `Origin = new SidecarOrigin(null, "users", "id")` → `("columns[0].origin.schema", "\"public\"", "null")`
    - `Origin = new SidecarOrigin("public", "Users", "id")` → `("columns[0].origin.table", "\"users\"", "\"Users\"")`
    - `Origin = new SidecarOrigin("public", "users", "user_id")` → `("columns[0].origin.column", "\"id\"", "\"user_id\"")`
    - column 1 (no origin) given `new SidecarOrigin("public", "users", "name")` → `("columns[1].origin", "null", "an object")`
  - Remove the `Origin = null` line from `FindDifference_MemberThatCheckDoesNotCompare_IsNull`, and `origin` from its comment.
  - In `FindDifference_TwoDifferences_IsTheFirstInTheOrder`: column 0 with `Nullable = null` and `Origin = null` → `columns[0].nullable`; column 0 `Origin = null` and column 1 `Name = "x"` → `columns[0].origin`.
- [ ] **Step 2: Write the failing tool test** `Check_ColumnNowReadFromAnotherTable_NamesTheOriginAndTheTwoValues` in `tests/SqlSource.Tool.Tests/Commands/CheckTests.cs`: describe first with a description of rows whose column has an origin (`FakeDescriber.TypedRows` or one like it), rewrite the committed entry's column 0 origin with `RewriteAsync`, run `--check`; exit code 2, error is exactly `Difference(<origin>, <query>, "columns[0].origin.table is \"<committed>\", and the database says \"users\"", ForceHelp)`, and the sidecar untouched.
- [ ] **Step 3: Run them to see them fail**: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~SidecarComparerTests"` and `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~Commands.CheckTests"`.
- [ ] **Step 4: Implement** `private static SidecarDifference? Origin(SidecarOrigin? committed, SidecarOrigin? described)` as `Table` is written (`Presence` with `AnObject`/`Null`, then `Under(SidecarKeys.Origin, Text(Schema) ?? Text(Table) ?? Text(Column))`), called after `Flag(SidecarKeys.Nullable, …)` in `Columns`.  The summary lists `origin` among the compared keys, after `nullable` and for columns only, and drops it and TD-0091 from what is not compared.
- [ ] **Step 5: Run the tests to see them pass**, same commands.
- [ ] **Step 6: Commit** `feat(check): compare a column's origin`.

### Task 2: Docs and resolving TD-0091

**Files:**
- Modify: `docs/architecture/sidecar-format.md` (`## What --check compares`)
- Modify: `docs/manual/tool.md` (check-in-ci bullet "It compares what changes generated code"; remove the known issue "`--check` misses a column that now comes from another table or schema.")
- Modify: `docs/manual/diagnostics.md` (SQLSRC219: the table row of `columns[2].nullable …`; the "What is compared" bullet)
- Delete: `docs/tech-debt/TD-0091-check-does-not-compare-a-columns-origin.md`; Modify: `docs/tech-debt/README.md` (its row only)

- [ ] **Step 1:** `sidecar-format.md`: the list becomes "each parameter's and column's `name`, `ordinal`, `type` and `nullable`, and each column's `origin`, in that order", with a sentence that `origin` is compared since `columnTypes` keys match it, part by part and exactly; drop `origin` and TD-0091 from the not-compared list.
- [ ] **Step 2:** `tool.md` bullet: "the queries, their parameters and columns, their types, whether they can be null, and the table and column each column is read from"; delete the known issue.
- [ ] **Step 3:** `diagnostics.md`: the row's meaning names a column now read from another table too; the bullet adds "and the table column each column is read from".
- [ ] **Step 4:** Delete the item file and its row; `grep -rn "TD-0091" . --exclude-dir=.git` finds nothing outside `docs/superpowers/`.
- [ ] **Step 5:** `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~Documentation"` passes (links, AGENTS sizes).
- [ ] **Step 6: Commit** `docs(tech-debt): resolve TD-0091`.

### Task 3: Both engines' describers reach `--check` with the new origin

**Files:**
- Test: `tests/DatabaseTests/Postgres/Describer/DescribeCommandTests.cs`
- Test: `tests/DatabaseTests/SqlServer/Describer/DescribeCommandTests.cs`

- [ ] **Step 1: PostgreSQL** `Check_TableMovedToAnotherSchemaOnTheSearchPath_DiffersInTheOrigin`, after `Check_ColumnAddedAfterDescribe_DiffersUntilDescribedWithForce`: create schemas `command_old` and `command_new` and `command_old.command_moved (id integer PRIMARY KEY)`; connect with `NpgsqlConnectionStringBuilder { SearchPath = "command_old,command_new" }` through `DescribeWithAsync`; a query `SELECT id FROM command_moved`; describe (exit 0); `ALTER TABLE command_old.command_moved SET SCHEMA command_new`; `--check` exits 2 and its error holds `columns[0].origin.schema is "command_old", and the database says "command_new"`; `finally` drops both schemas `CASCADE`.
- [ ] **Step 2: SQL Server** `Check_SynonymPointedAtAnotherTable_DiffersInTheOrigin`: tables `dbo.command_first (id int NOT NULL PRIMARY KEY)` and `dbo.command_second` of the same shape, `CREATE SYNONYM dbo.command_alias FOR dbo.command_first`; add a `.sql` file of the copied fixture project with `-- name: GetAliased` / `SELECT id FROM command_alias;`; describe (exit 0); re-point the synonym to `dbo.command_second`; `--check` exits 2 and its error holds `columns[0].origin.table is "command_first", and the database says "command_second"`; `finally` drops the synonym and both tables.  (`sqlserver-describer.md#origins`: a synonym's column has the synonym's table as its source.)
- [ ] **Step 3:** Run each describer project's `DescribeCommandTests` on its default image: `dotnet test tests/DatabaseTests/Postgres --filter "FullyQualifiedName~Describer.DescribeCommandTests"`, the same for `tests/DatabaseTests/SqlServer`.
- [ ] **Step 4: Commit** `test(check): a moved table's origin reaches --check on both engines`.
- [ ] **Step 5:** Run the end-to-end `CheckTests` and `DescribeCommandTests` of each engine on its floor image (`SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`, `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`) and the nightly images (`postgres:15.19`, `16.15`, `17.11`; `2019-CU32-GDR12-ubuntu-20.04`, `2022-CU27-GDR1-ubuntu-22.04`): committed sidecars must still match where origin is now compared.  A failure is an owner question, not a test change.
