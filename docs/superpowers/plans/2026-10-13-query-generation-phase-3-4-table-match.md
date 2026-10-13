# Query generation, phase 3.4: the table match - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The PostgreSQL describer says whether a query's result is exactly one relation's column list, in `matchesTable` and `tableMatch`, so that phase 5 can name a model after the table and share it; registered nowhere.

**Architecture:** `TableMatcher` is a static class of `Describing/Postgres/` that asks the server nothing: it is given the columns as they will be recorded and the `ColumnOrigin` of each, which already holds the relation and its whole attribute list from the answer of `tables`, and runs the six checks in the format's order.  `PostgresSession` calls it once for a statement with rows, after the columns are built and before `check`, and writes one `postgres.table-match` event.  It is tested over columns a test makes, through the session over a replay, and against a real server.

**Tech Stack:** C# (`LangVersion` `preview`); the tool on `net8.0`, the tests on `net10.0`; Npgsql 10.0.3; Testcontainers.PostgreSql; xunit v3 on Microsoft.Testing.Platform with Shouldly; Docker.

**Spec:** `docs/superpowers/specs/2026-10-12-query-generation-phase-3-4-table-match-design.md`.  Read it with this plan.  The epic outline, `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`, is kept current and wins where the two disagree.

**Branch:** `claude/query-generation-phase-3-4-table-match`, made from `main` in Task 1.  This file is untracked until Task 1 commits it.

## Where the plan settles what the spec leaves open

The spec was written before sub-phase 3.3 was reviewed, and sub-phase 3.5 is not in.  These are the readings the plan takes; the spec stays as written, and Task 5 records the first in the epic outline.

- **Check 6 asks `ColumnNullability`, not bare `!notNull`.**  The review of 3.3 made a `NOT NULL` that PostgreSQL 18 holds as `NOT VALID` nullable (`PostgresAttributeRow.NotNullValidated`), and made a materialized view answer as a view does.  What a relation's column "has" is therefore `ColumnNullability.Of(origin, gated: false).Nullable`: what the describer records for the column in a query that is not gated.  The rule of a `NOT NULL` stays in one place, a `SELECT *` from a table with a `NOT VALID` column matches, and a materialized view may match as a view may.
- **`TableMatcher.Match` takes the columns and their origins, not the `tables` dictionary.**  The spec says `Match(columns, tables)` over columns that carry a table OID and an attribute number.  The session already holds a `ColumnOrigin?` for each column, with its `PostgresTableRow`, whose `Columns` is every attribute that is not dropped, and its `PostgresAttributeRow`.  That is everything checks 2, 4, 5 and 6 read.
- **The event is written after the `column` events and before `check`.**  The match is made where the spec says, after the columns are built; a `check` that then fails leaves the event in the log, as it leaves the `column` events.
- **No call is added, so no `capture.json` changes.**  A re-recording changes `expected.json` alone.  A diff in a `capture.json` is a bug to find, with one exception: when the default image has moved to a new patch release, the `open` answer and `serverVersion` change in every case, which is the server's and is committed as it is.
- **The six existing cases whose `expected.json` changes are recorded in Task 2, not Task 4.**  The spec's order of work puts the re-recording in step 4, and also says each step leaves `./pre-commit-validation.sh` passing: once `DescribeAsync` fills the match, `DescriberFixtureTests` fails on the old files.  So Task 2 records, and Task 4 adds the eleven new cases.  What Task 2 must see: `select-other-schema` and `select-view` `matched`; `select-plain` `several-tables`; `select-suffix-names` `names-differ`; `select-declared-domain` and `insert-returning` `columns-differ`; `select-expressions` and `select-left-join` unchanged at `no-origin`.
- **`schema.sql` is not changed.**  `match-two-tables` selects `users` and `orders` whole; two columns named `id` are recorded as they are.  The container test of a dropped column makes a table of its own, as `ColumnTests` does.
- **What the spec leaves to sub-phase 3.5 stays there**: the view over the outer join, and `nullability-differs` under the walk.  `match-left-join-nullability` is recorded under the heuristic.
- **`docs/tech-debt/TD-0044` is not touched.**  Its trigger names this sub-phase's recording; none of its steps is in this spec.

## Global Constraints

- The describer is registered nowhere: `ToolHost.Create` is unchanged, and the released tool says `SQLSRC216` for `postgres`.
- The values of `tableMatch` are those of `SidecarValues.TableMatch`, in the format's fixed order: `matched`, `no-origin`, `several-tables`, `names-differ`, `columns-differ`, `order-differs`, `nullability-differs`.  The first check that fails decides.  The order is not to be changed.
- The match is provenance: it adds no diagnostic, never fails a describe, and asks the server nothing.  The methods of the exchange stay `open`, `prepare`, `columns`, `tables`, `types`, `check` and `deallocate`, and no record of `PostgresCalls.cs` changes.
- A statement without rows has no `matchesTable`, no `tableMatch` and no `table-match` event.
- A view, a materialized view, a foreign table and a partitioned table may match, as a table may.
- Names are compared ordinally, with the name as the server returned it.
- An event's name and its members are a contract: `table-match` with `query`, `result`, `table`, in that order.  Nothing the describer logs holds the connection's value.
- `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/schema.sql` is not changed.
- `README.md`, `CONTRIBUTING.md`, `docs/publishing.md`, `docs/diagnostics.md` and `src/SqlSource.Tool/README.md` are not touched.  Nothing is expected for `docs/tech-debt` and `docs/deferred`; if the work finds a gap, it is recorded there in the same pull request, as `AGENTS.md` requires.
- The build treats warnings as errors with `AnalysisLevel` `latest-all`, Roslynator and Sonar.  Fix what it reports in the code; never suppress.  If a check is wrong, stop and tell the user.
- `ImplicitUsings` is off: a file names every namespace it uses.
- Comments say what a thing is for, in the plain declarative style of the files around them, with two spaces after a full stop.  Types under `src/` have XML documentation; test files use `//` comments.
- Test names are `Method_Scenario_Outcome`; assertions are Shouldly's.  A test takes its token from `TestContext.Current.CancellationToken`.
- Every commit passes `./pre-commit-validation.sh`, which needs Docker running and starts a PostgreSQL container.  Run `./format.sh`, then the validation, then commit in a command of its own.  Never work around the hook.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

How a test class is run by itself, here and in every task:

```bash
dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.TableMatcherTests"
dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class "SqlSource.Tests.Postgres.Describer.TableMatchTests"
```

## Review Focus

- **`INSERT ... RETURNING *`.**  A `RETURNING` list keeps the table's origins, and "insert and give me the row back" is as common as `SELECT *`.  A user expects the table's model, shared with the selects.  Task 2, the container's `Describe_InsertReturningStar_MatchesTheTable`.
- **A table selected whole twice, `SELECT a.*, b.* FROM users a JOIN users b ...`.**  One relation, every name the table's, every attribute present.  A user expects no table model, since the result has twice the columns: `columns-differ`.  Task 2, `Match_TableSelectedWholeTwice_IsColumnsDiffer`.
- **A `NOT NULL` that is `NOT VALID`, on PostgreSQL 18.**  The describer records the column as nullable.  A user expects `SELECT *` from that table to match it all the same, not `nullability-differs` for ever.  Task 2, `Match_NotNullThatIsNotValidated_MatchesAsTheNullableColumnItIs`.
- **A partitioned table and a materialized view.**  `relkind` `p` and `m`, which the spec does not name.  A user expects a partitioned table to match as a table and a materialized view as a view.  Task 2, `Match_PartitionedTable_IsTheTable` and `Match_ViewMaterializedViewOrForeignTableWithEveryColumnUnknown_IsTheRelation`.
- **A table with a dropped column in the middle.**  Its attribute numbers have a gap, and its list is what is left.  A user expects `SELECT *` to match.  Task 2, `Match_EveryColumnOfATableInItsOrder_IsTheTable`, whose table has the gap, and the container's `Describe_StarOfATableWithADroppedColumn_MatchesTheTable`.

## File structure

Created under `src/SqlSource.Tool/Describing/Postgres/`:

| File | Holds |
|----|----|
| `TableMatcher.cs` | The six checks, in the format's order, over the columns and their origins. |

Modified there: `PostgresSession.cs` (the call, and `Described`), `PostgresEvents.cs` (`TableMatch`).

Created under `tests/SqlSource.Tool.Tests/Describing/Postgres/`: `TableMatcherTests.cs`.  Modified there: `PostgresSessionTests.cs`, `PostgresEventsTests.cs`.

Created under `tests/SqlSource.Tests.Postgres/Describer/`: `TableMatchTests.cs`.

Under `tests/SqlSource.Tool.Tests/Fixtures/`: six `expected.json` recorded again, eleven new cases under `Describers/postgres/`, and `DescriberFixtureTests.cs`.

Documents: `src/SqlSource.Tool/AGENTS.md` and the epic outline.

---

### Task 1: The branch, the plan and the outline

**Files:**
- Create: `docs/superpowers/plans/2026-10-13-query-generation-phase-3-4-table-match.md` (this file, already written)
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`

**Interfaces:**
- Consumes: nothing.
- Produces: the branch every later task commits to.

- [ ] **Step 1: Make the branch**

```bash
git switch -c claude/query-generation-phase-3-4-table-match main
```

- [ ] **Step 2: Set the row of 3.4 to In progress**

In `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`, in the table of sub-phases, the row that begins `| 3.4 The table match | Not started |` becomes `| 3.4 The table match | In progress |`.  Nothing else of the row changes.

- [ ] **Step 3: Validate**

Run: `./pre-commit-validation.sh`
Expected: every step `passed`.

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers/plans/2026-10-13-query-generation-phase-3-4-table-match.md docs/superpowers/specs/2026-10-07-query-generation-epic-design.md
```

```bash
git commit -m "Plan sub-phase 3.4, the table match

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: `TableMatcher`, and the session filling `matchesTable` and `tableMatch`

**Files:**
- Create: `src/SqlSource.Tool/Describing/Postgres/TableMatcher.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs` (`DescribePreparedAsync`, `Described`)
- Create: `tests/SqlSource.Tool.Tests/Describing/Postgres/TableMatcherTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs`
- Create: `tests/SqlSource.Tests.Postgres/Describer/TableMatchTests.cs`
- Modify (by recording): six `expected.json` under `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/`

**Interfaces:**
- Consumes: `ColumnOrigin(PostgresTableRow Table, PostgresAttributeRow Attribute)`; `ColumnNullability.Of(ColumnOrigin? origin, bool gated)` returning `(bool? Nullable, string Source)`; `SidecarColumn`, `SidecarTable(string? Schema, string? Table)`, `SidecarValues.TableMatch`; the tests' `PostgresTables.TableOf`, `PostgresTables.Attribute`, `PostgresTables.ColumnOf`, `PostgresRows`, `TestSidecars.Postgres()`.
- Produces: `TableMatcher.Match(IReadOnlyList<SidecarColumn> columns, IReadOnlyList<ColumnOrigin?> origins)` returning `(SidecarTable? Table, string Result)`; in `PostgresSession`, the private static `MatchOf(IReadOnlyList<SidecarColumn> columns, ColumnOrigin?[] origins)` returning `(SidecarTable? Table, string Result)?`, which Task 3 changes.

- [ ] **Step 1: Write the failing tests of the matcher**

Create `tests/SqlSource.Tool.Tests/Describing/Postgres/TableMatcherTests.cs`:

```csharp
using System;
using System.Linq;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tests.Snapshot;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The six checks of the table match, in the format's order: the first that fails is the answer.
public class TableMatcherTests
{
    // Attribute 3 was dropped: the answer of "tables" leaves a dropped attribute out, so the numbers have a gap.
    private static readonly PostgresTableRow Items = PostgresTables.TableOf(
        7,
        "items",
        PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true),
        PostgresTables.Attribute(2, "name", PostgresRows.Text.Oid, notNull: true),
        PostgresTables.Attribute(4, "note", PostgresRows.Text.Oid)
    );

    private static readonly PostgresTableRow Other = PostgresTables.TableOf(
        8,
        "other",
        PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true)
    );

    // A column of an expression.
    private static readonly Cell Expression = new(
        new SidecarColumn(
            0,
            "n",
            TestSidecars.Postgres(),
            null,
            SidecarValues.NullableSource.NoOrigin,
            null,
            null,
            null
        ),
        null
    );

    // A column as the describer builds one in a query that is not gated: the attribute's name, and what the
    // catalog says of its nullability.  The match reads no ordinal and no type.
    private static Cell From(PostgresTableRow table, int attnum)
    {
        var origin = new ColumnOrigin(table, table.Columns.Single(attribute => attribute.Attnum == attnum));
        var (nullable, source) = ColumnNullability.Of(origin, gated: false);
        return new Cell(
            new SidecarColumn(
                0,
                origin.Attribute.Name,
                TestSidecars.Postgres(),
                nullable,
                source,
                new SidecarOrigin(table.Schema, table.Name, origin.Attribute.Name),
                false,
                false
            ),
            origin
        );
    }

    // The relation's list in its order: what SELECT * gives.
    private static Cell[] Whole(PostgresTableRow table) =>
        [.. table.Columns.Select(attribute => From(table, attribute.Attnum))];

    private static (SidecarTable? Table, string Result) Match(params Cell[] cells) =>
        TableMatcher.Match(
            [.. cells.Select(static cell => cell.Column)],
            [.. cells.Select(static cell => cell.Origin)]
        );

    [Fact]
    public void Match_EveryColumnOfATableInItsOrder_IsTheTable() =>
        Match(Whole(Items)).ShouldBe((new SidecarTable("public", "items"), SidecarValues.TableMatch.Matched));

    [Fact]
    public void Match_TableOfOneColumn_IsTheTable() =>
        Match(Whole(Other)).ShouldBe((new SidecarTable("public", "other"), SidecarValues.TableMatch.Matched));

    [Fact]
    public void Match_RelationOfAnotherSchema_SaysItsSchema() =>
        Match(Whole(Items with { Schema = "billing" }))
            .ShouldBe((new SidecarTable("billing", "items"), SidecarValues.TableMatch.Matched));

    [Fact]
    public void Match_ColumnWithoutAnOrigin_IsNoOrigin() =>
        Match([.. Whole(Items), Expression]).ShouldBe((null, SidecarValues.TableMatch.NoOrigin));

    [Fact]
    public void Match_ColumnsOfTwoTables_IsSeveralTables() =>
        Match([.. Whole(Items), .. Whole(Other)]).ShouldBe((null, SidecarValues.TableMatch.SeveralTables));

    // An alias, a name that differs in case alone, and an override suffix: the name is compared ordinally, as the
    // server returned it.
    [Theory]
    [InlineData("label")]
    [InlineData("Name")]
    [InlineData("name!")]
    public void Match_ColumnUnderAnotherName_IsNamesDiffer(string name) =>
        Match(From(Items, 1), From(Items, 2).Named(name), From(Items, 4))
            .ShouldBe((null, SidecarValues.TableMatch.NamesDiffer));

    [Fact]
    public void Match_ColumnLeftOut_IsColumnsDiffer() =>
        Match(From(Items, 1), From(Items, 2)).ShouldBe((null, SidecarValues.TableMatch.ColumnsDiffer));

    [Fact]
    public void Match_ColumnTwiceBesideTheWholeList_IsColumnsDiffer() =>
        Match(From(Items, 1), From(Items, 1), From(Items, 2), From(Items, 4))
            .ShouldBe((null, SidecarValues.TableMatch.ColumnsDiffer));

    // As many columns as the table has, and not its list: the lists are compared, not their lengths.
    [Fact]
    public void Match_ColumnTwiceInThePlaceOfAnother_IsColumnsDiffer() =>
        Match(From(Items, 1), From(Items, 1), From(Items, 4))
            .ShouldBe((null, SidecarValues.TableMatch.ColumnsDiffer));

    // SELECT a.*, b.* of a table joined to itself: one relation, every name its own, every attribute there.
    [Fact]
    public void Match_TableSelectedWholeTwice_IsColumnsDiffer() =>
        Match([.. Whole(Items), .. Whole(Items)]).ShouldBe((null, SidecarValues.TableMatch.ColumnsDiffer));

    [Fact]
    public void Match_TwoColumnsSwapped_IsOrderDiffers() =>
        Match(From(Items, 2), From(Items, 1), From(Items, 4))
            .ShouldBe((null, SidecarValues.TableMatch.OrderDiffers));

    // What the heuristic of a gated query does to a NOT NULL column.
    [Fact]
    public void Match_NotNullColumnRecordedNullable_IsNullabilityDiffers() =>
        Match(From(Items, 1).WithNullable(true), From(Items, 2), From(Items, 4))
            .ShouldBe((null, SidecarValues.TableMatch.NullabilityDiffers));

    // A gated query over a table of nullable columns: each is nullable by the heuristic, as the catalog says too.
    [Fact]
    public void Match_NullableColumnsRecordedNullable_IsTheTable()
    {
        var notes = PostgresTables.TableOf(
            9,
            "notes",
            PostgresTables.Attribute(1, "a", PostgresRows.Text.Oid),
            PostgresTables.Attribute(2, "b", PostgresRows.Text.Oid)
        );

        Match([.. Whole(notes).Select(static cell => cell.WithNullable(true))])
            .ShouldBe((new SidecarTable("public", "notes"), SidecarValues.TableMatch.Matched));
    }

    // PostgreSQL records no NOT NULL for a column of a view, and does not enforce a foreign table's: every column
    // is unknown, in the result and in the relation.
    [Theory]
    [InlineData("v")]
    [InlineData("m")]
    [InlineData("f")]
    public void Match_ViewMaterializedViewOrForeignTableWithEveryColumnUnknown_IsTheRelation(string relkind) =>
        Match(Whole(Items with { Relkind = relkind }))
            .ShouldBe((new SidecarTable("public", "items"), SidecarValues.TableMatch.Matched));

    // What the walk of sub-phase 3.5 does to a view's column on the nullable side of a join.
    [Fact]
    public void Match_ColumnOfAViewRecordedNullable_IsNullabilityDiffers()
    {
        var view = Items with { Relkind = "v" };

        Match(From(view, 1).WithNullable(true), From(view, 2), From(view, 4))
            .ShouldBe((null, SidecarValues.TableMatch.NullabilityDiffers));
    }

    [Fact]
    public void Match_PartitionedTable_IsTheTable() =>
        Match(Whole(Items with { Relkind = "p" }))
            .ShouldBe((new SidecarTable("public", "items"), SidecarValues.TableMatch.Matched));

    // PostgreSQL 18 sets attnotnull for a NOT NULL that is NOT VALID, and the describer records the column as
    // nullable.  The table's column is nullable by the same rule, so SELECT * still matches.
    [Fact]
    public void Match_NotNullThatIsNotValidated_MatchesAsTheNullableColumnItIs()
    {
        var loose = PostgresTables.TableOf(
            10,
            "loose",
            PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true, notNullValidated: false)
        );

        Match(Whole(loose)).ShouldBe((new SidecarTable("public", "loose"), SidecarValues.TableMatch.Matched));
        Match(From(loose, 1).WithNullable(false)).ShouldBe((null, SidecarValues.TableMatch.NullabilityDiffers));
    }

    // Each case fails two checks, and the answer is the one that comes first in the format's order.
    [Fact]
    public void Match_TwoChecksThatFail_GivesTheFirst()
    {
        var alias = From(Items, 2).Named("label");
        var loosened = From(Items, 1).WithNullable(true);

        Match(Expression, From(Other, 1), From(Items, 1)).Result.ShouldBe(SidecarValues.TableMatch.NoOrigin);
        Match(From(Other, 1), alias).Result.ShouldBe(SidecarValues.TableMatch.SeveralTables);
        Match(From(Items, 1), alias).Result.ShouldBe(SidecarValues.TableMatch.NamesDiffer);
        Match(From(Items, 4), From(Items, 1)).Result.ShouldBe(SidecarValues.TableMatch.ColumnsDiffer);
        Match(From(Items, 2), loosened, From(Items, 4)).Result.ShouldBe(SidecarValues.TableMatch.OrderDiffers);
    }

    // A statement without rows has no match, and the session never asks for one.
    [Fact]
    public void Match_NoColumn_Throws() =>
        Should.Throw<InvalidOperationException>(() => TableMatcher.Match([], []));

    [Fact]
    public void Match_OriginsOfAnotherCount_Throws() =>
        Should.Throw<InvalidOperationException>(() => TableMatcher.Match([From(Items, 1).Column], []));

    // A column and where it comes from, as the session holds the two side by side.
    private sealed record Cell(SidecarColumn Column, ColumnOrigin? Origin)
    {
        public Cell Named(string name) => this with { Column = Column with { Name = name } };

        public Cell WithNullable(bool? nullable) => this with { Column = Column with { Nullable = nullable } };
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.TableMatcherTests"`
Expected: the build fails with `CS0103: The name 'TableMatcher' does not exist in the current context`.

- [ ] **Step 3: Write `TableMatcher`**

Create `src/SqlSource.Tool/Describing/Postgres/TableMatcher.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.Snapshot;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// Whether a query's result is exactly one relation's column list, so that the generator can name the model after
/// the relation and share it between every query that matches it.
/// </summary>
/// <remarks>
/// The checks are the values of the sidecar format's <c>tableMatch</c>, in the format's order, and the first that
/// fails is the answer: the order is the format's and is not to be changed.  A view, a materialized view and a
/// foreign table may match as a table may.  The last check reads the nullability each column was given, so the
/// match is made after everything that sets one.  It asks the server nothing: an origin holds its relation with
/// every attribute that is not dropped, as <c>tables</c> answered it.
/// </remarks>
internal static class TableMatcher
{
    /// <summary>
    /// The relation whose column list the columns are, or null, and <c>matched</c> or the first check that failed.
    /// </summary>
    /// <param name="columns">The columns of a statement with rows, in order, as they will be recorded.</param>
    /// <param name="origins">Where each column comes from, or null, in the same order.</param>
    /// <exception cref="InvalidOperationException">
    /// There is no column, or not one origin for each: a bug of the describer.
    /// </exception>
    public static (SidecarTable? Table, string Result) Match(
        IReadOnlyList<SidecarColumn> columns,
        IReadOnlyList<ColumnOrigin?> origins
    )
    {
        if (columns.Count == 0 || columns.Count != origins.Count)
        {
            throw new InvalidOperationException(
                $"The table match was asked for {columns.Count} columns with {origins.Count} origins."
            );
        }

        ColumnOrigin[] found = [.. origins.OfType<ColumnOrigin>()];
        if (found.Length != columns.Count)
        {
            return (null, SidecarValues.TableMatch.NoOrigin);
        }

        var table = found[0].Table;
        if (found.Any(origin => origin.Table.Oid != table.Oid))
        {
            return (null, SidecarValues.TableMatch.SeveralTables);
        }

        // The name as the server returned it: an alias and an override suffix are each another name.
        if (
            !columns
                .Select(static column => column.Name)
                .SequenceEqual(found.Select(static origin => origin.Attribute.Name), StringComparer.Ordinal)
        )
        {
            return (null, SidecarValues.TableMatch.NamesDiffer);
        }

        // As lists and not as sets: a column that is left out and a column that is there twice both differ.
        int[] numbers = [.. found.Select(static origin => origin.Attribute.Attnum)];
        if (!numbers.Order().SequenceEqual(table.Columns.Select(static attribute => attribute.Attnum).Order()))
        {
            return (null, SidecarValues.TableMatch.ColumnsDiffer);
        }

        if (!numbers.SequenceEqual(numbers.Order()))
        {
            return (null, SidecarValues.TableMatch.OrderDiffers);
        }

        // What the relation's column has is what a query that is not gated records for it: the rule of a NOT NULL,
        // of one that is not validated and of a view is ColumnNullability's, and is not written twice.
        return columns
            .Select(static column => column.Nullable)
            .SequenceEqual(found.Select(static origin => ColumnNullability.Of(origin, gated: false).Nullable))
            ? (new SidecarTable(table.Schema, table.Name), SidecarValues.TableMatch.Matched)
            : (null, SidecarValues.TableMatch.NullabilityDiffers);
    }
}
```

- [ ] **Step 4: Run the matcher's tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.TableMatcherTests"`
Expected: PASS, 24 tests.  If an analyzer rejects the code, fix the code as the analyzer asks and keep the six checks in their order.

- [ ] **Step 5: Write the failing tests of the session**

In `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs`, after `DescribeAsync_GatedStatementWithoutRows_HasNoPlan`, add:

```csharp
    // A table of two columns, whole in the answer of "tables".
    private static readonly PostgresTableRow Labels = PostgresTables.TableOf(
        16500,
        "labels",
        PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true),
        PostgresTables.Attribute(2, "label", PostgresRows.Text.Oid)
    );

    // One script for a query whose columns are the two of labels in their order, the second under the name given.
    private static async Task<QueryDescription> DescribeLabelsAsync(string sql, string second = "label")
    {
        var script = new PostgresScript().Open();
        _ = PreparesOnly(script, 1, sql, [], new([], null));
        _ = Columns(
            script,
            1,
            0,
            PostgresTables.ColumnOf(0, "id", 23, Labels, 1),
            PostgresTables.ColumnOf(1, second, 25, Labels, 2)
        );
        _ = Tables(script, [Labels.Oid], Labels);
        _ = Resolves(script, [23, 25], PostgresRows.Int4, PostgresRows.Text);
        _ = Checks(script, sql, [], Expression(0, "id", 23), Expression(1, second, 25));
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Request(sql), TestContext.Current.CancellationToken);

        exchange.Unused.ShouldBeEmpty();
        return described.Value.ShouldNotBeNull();
    }

    // The match asks nothing: the script holds the calls of any query with columns, and no other.
    [Fact]
    public async Task DescribeAsync_EveryColumnOfATableInItsOrder_SaysTheTable()
    {
        var description = await DescribeLabelsAsync("SELECT * FROM labels");

        description.MatchesTable.ShouldBe(new SidecarTable("public", "labels"));
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.Matched);
    }

    // The name is the one the server returned.
    [Fact]
    public async Task DescribeAsync_ColumnUnderAnotherName_MatchesNothingAndSaysNamesDiffer()
    {
        var description = await DescribeLabelsAsync("SELECT id, label AS \"label!\" FROM labels", "label!");

        description.MatchesTable.ShouldBeNull();
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.NamesDiffer);
    }

    // The match is made after the columns are built: it reads the nullability the gate gave them.
    [Fact]
    public async Task DescribeAsync_GatedQueryOverATablesList_MatchesNothingAndSaysNullabilityDiffers()
    {
        var description = await DescribeLabelsAsync("SELECT l.* FROM orders o LEFT JOIN labels l ON l.id = o.id");

        description.MatchesTable.ShouldBeNull();
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.NullabilityDiffers);
        description.Plan.ShouldBe(SidecarValues.Plan.Unavailable);
    }
```

In the same file, in `DescribeAsync_StatementWithoutColumns_IsNoneWithNoProvenance`, after the line `description.Columns.ShouldBeNull();`, add:

```csharp
        description.MatchesTable.ShouldBeNull();
```

- [ ] **Step 6: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PostgresSessionTests"`
Expected: the three new tests FAIL, each on its match: the session still answers `null` and `no-origin`.  Every other test passes.

- [ ] **Step 7: Call the matcher from the session**

In `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`, the end of `DescribePreparedAsync`, which is now

```csharp
        if (!built.Succeeded)
        {
            return Failed(built.Failure);
        }

        return await CheckAsync(request, parameters.Value, columns, rows.Value, cancellationToken) is { } differs
            ? Failed(differs)
            : Described(parameters.Value, built.Value, gated);
    }
```

becomes

```csharp
        if (!built.Succeeded)
        {
            return Failed(built.Failure);
        }

        // After the columns, and after everything that sets a column's nullability: the last check of the match
        // reads it.
        var match = MatchOf(built.Value, origins);

        return await CheckAsync(request, parameters.Value, columns, rows.Value, cancellationToken) is { } differs
            ? Failed(differs)
            : Described(parameters.Value, built.Value, gated, match);
    }

    // A statement without rows has no match.
    private static (SidecarTable? Table, string Result)? MatchOf(
        IReadOnlyList<SidecarColumn> columns,
        ColumnOrigin?[] origins
    ) => columns.Count == 0 ? null : TableMatcher.Match(columns, origins);
```

The comment above `DescribePreparedAsync`, `// What is asked of a statement that was prepared, in this order: its columns, the tables they name, and every` / `// type that the parameters, the columns and their origins have, once, and Npgsql's own reading of the SQL.`, gains a sentence at its end: `The table match is made between the columns and the check, and asks nothing.`  Wrap the comment at 120 columns.

Replace the whole of `Described`, with its comment, by:

```csharp
    // A statement without rows has its parameters and nothing else: no columns, no table and no provenance.  One
    // with rows says whether it needed a plan, "unavailable" for a gated query, until the walk exists, and which
    // table its result is, or the first check of the match that failed.
    private static Outcome<QueryDescription, DescribeFailure> Described(
        IReadOnlyList<DescribedParameter> parameters,
        IReadOnlyList<SidecarColumn> columns,
        bool gated,
        (SidecarTable? Table, string Result)? match
    )
    {
        var typed = new EquatableArray<DescribedParameter>([.. parameters]);
        var plan = gated ? SidecarValues.Plan.Unavailable : SidecarValues.Plan.NotNeeded;
        return Outcome<QueryDescription, DescribeFailure>.Of(
            match is { } found
                ? new QueryDescription(
                    SidecarResultKind.Rows,
                    typed,
                    new EquatableArray<SidecarColumn>([.. columns]),
                    found.Table,
                    plan,
                    found.Result
                )
                : new QueryDescription(SidecarResultKind.None, typed, null, null, null, null)
        );
    }
```

- [ ] **Step 8: Run the session's tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PostgresSessionTests"`
Expected: PASS.

- [ ] **Step 9: Write the container's tests**

Create `tests/SqlSource.Tests.Postgres/Describer/TableMatchTests.cs`:

```csharp
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// The table match against a real server: what only a server can show, the origins of a row description for
// SELECT *, for a view, for a RETURNING list and for a table that lost a column.
[Collection(UsesPostgres.Name)]
public sealed class TableMatchTests(PostgresContainer postgres)
{
    // A table of these tests alone, whose second attribute is dropped.  It is made by SQL that can be run again,
    // and left: the container is thrown away, and the fixtures are recorded in one of their own.
    private const string DroppedColumnTable = """
        CREATE TABLE IF NOT EXISTS match_dropped (id integer PRIMARY KEY, gone text, kept text);
        ALTER TABLE match_dropped DROP COLUMN IF EXISTS gone;
        """;

    private async Task<QueryDescription> ShouldDescribeAsync(string sql)
    {
        var described = await Sessions.DescribeAsync(postgres.ConnectionString, Requests.Of(sql));

        described.Failure.ShouldBeNull(described.Failure?.Descriptor.Id);
        return described.Value.ShouldNotBeNull();
    }

    private async Task ShouldMatchAsync(string sql, string table, string schema = "public")
    {
        var description = await ShouldDescribeAsync(sql);

        description.TableMatch.ShouldBe(SidecarValues.TableMatch.Matched);
        description.MatchesTable.ShouldBe(new SidecarTable(schema, table));
    }

    // The first two queries of the spec's Goal.
    [Theory]
    [InlineData("SELECT * FROM users WHERE id = @id")]
    [InlineData("SELECT id, name, email, status, tags, balance, created_at, deleted_at, name_upper FROM users")]
    public Task Describe_WholeTableByStarOrByItsListInOrder_MatchesTheTable(string sql) =>
        ShouldMatchAsync(sql, "users");

    [Fact]
    public Task Describe_StarOfAView_MatchesTheView() => ShouldMatchAsync("SELECT * FROM active_users", "active_users");

    [Fact]
    public Task Describe_StarOfATableInAnotherSchema_SaysItsSchema() =>
        ShouldMatchAsync("SELECT * FROM billing.invoices", "invoices", "billing");

    // A RETURNING list keeps the table's origins, so the row an INSERT gives back is the table's.
    [Fact]
    public Task Describe_InsertReturningStar_MatchesTheTable() =>
        ShouldMatchAsync("INSERT INTO orders (user_id, total) VALUES (@user_id, @total) RETURNING *", "orders");

    // The dropped attribute keeps its number and is not in the answer of "tables": the table's list is what is left.
    [Fact]
    public async Task Describe_StarOfATableWithADroppedColumn_MatchesTheTable()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using (var create = source.CreateCommand(DroppedColumnTable))
        {
            _ = await create.ExecuteNonQueryAsync(token);
        }

        // The premise: the table holds an attribute that is dropped.
        await using (
            var dropped = source.CreateCommand(
                "SELECT count(*) FROM pg_catalog.pg_attribute "
                    + "WHERE attrelid = 'match_dropped'::regclass AND attnum > 0 AND attisdropped"
            )
        )
        {
            (await dropped.ExecuteScalarAsync(token)).ShouldBe(1L);
        }

        await ShouldMatchAsync("SELECT * FROM match_dropped", "match_dropped");
    }

    // Each query differs from users in one way, the third of the spec's Goal in two, and says the first check that
    // failed.
    [Theory]
    [InlineData("SELECT *, upper(name) AS shouted FROM users", "no-origin")]
    [InlineData("SELECT u.*, o.* FROM users AS u JOIN orders AS o ON o.user_id = u.id", "several-tables")]
    [InlineData(
        "SELECT id, name AS user_name, email, status, tags, balance, created_at, deleted_at, name_upper FROM users",
        "names-differ"
    )]
    [InlineData(
        "SELECT id, name AS \"name!\", email, status, tags, balance, created_at, deleted_at, name_upper FROM users",
        "names-differ"
    )]
    [InlineData("SELECT id, name AS user_name FROM users", "names-differ")]
    [InlineData(
        "SELECT id, name, email, status, tags, balance, created_at, deleted_at FROM users",
        "columns-differ"
    )]
    [InlineData(
        "SELECT id, id, name, email, status, tags, balance, created_at, deleted_at, name_upper FROM users",
        "columns-differ"
    )]
    [InlineData("SELECT a.*, b.* FROM users AS a JOIN users AS b ON b.id = a.id", "columns-differ")]
    [InlineData(
        "SELECT name, id, email, status, tags, balance, created_at, deleted_at, name_upper FROM users",
        "order-differs"
    )]
    [InlineData("SELECT u.* FROM orders AS o LEFT JOIN users AS u ON u.id = o.user_id", "nullability-differs")]
    public async Task Describe_ResultThatIsNotATable_MatchesNothingAndSaysTheFirstCheckThatFailed(
        string sql,
        string expected
    )
    {
        var description = await ShouldDescribeAsync(sql);

        description.MatchesTable.ShouldBeNull();
        description.TableMatch.ShouldBe(expected);
    }
}
```

- [ ] **Step 10: Run them**

Run: `dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class "SqlSource.Tests.Postgres.Describer.TableMatchTests"`
Expected: PASS, 16 tests.  A case that gives another value is a finding about the server's row description: stop and tell the user, and do not change the expected value to fit.

Run: `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14 dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class "SqlSource.Tests.Postgres.Describer.TableMatchTests"`
Expected: PASS: PostgreSQL 14 answers as 18 does.

- [ ] **Step 11: Record the cases that are there, and read the diff**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Fixtures.DescriberFixtureTests"`
Expected: FAIL for six cases, each on its `tableMatch`: the old `expected.json` says `no-origin`.

Run: `tools/record-fixtures.sh`
Expected: `Recording 12 cases of postgres against postgres:18 ... done`.

Run: `git status --short tests/SqlSource.Tool.Tests/Fixtures`
Expected: exactly these six files modified, and no `capture.json`:

```text
 M tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/insert-returning/expected.json
 M tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/select-declared-domain/expected.json
 M tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/select-other-schema/expected.json
 M tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/select-plain/expected.json
 M tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/select-suffix-names/expected.json
 M tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/select-view/expected.json
```

Run: `git diff --unified=0 tests/SqlSource.Tool.Tests/Fixtures | grep '^[+-] '`
Expected: only the lines of `matchesTable`, with its object where it has one, and of `tableMatch`, with these new values:

| Case | `tableMatch` | `matchesTable` |
|----|----|----|
| `insert-returning` | `columns-differ` | `null` |
| `select-declared-domain` | `columns-differ` | `null` |
| `select-other-schema` | `matched` | `{ "schema": "billing", "table": "invoices" }` |
| `select-plain` | `several-tables` | `null` |
| `select-suffix-names` | `names-differ` | `null` |
| `select-view` | `matched` | `{ "schema": "public", "table": "active_users" }` |

If every case shows a new `serverVersion` and every `capture.json` a new `open` answer, the default image has moved to a new patch release: that is the server's, keep it and say so in the commit's body.  Any other line, in an `expected.json` or a `capture.json`, is a bug of this task: find it before going on.

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Fixtures.DescriberFixtureTests"`
Expected: PASS.

- [ ] **Step 12: Format, validate and commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`.
Expected: every step `passed`.

```bash
git add src/SqlSource.Tool/Describing/Postgres/TableMatcher.cs src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs tests/SqlSource.Tool.Tests/Describing/Postgres/TableMatcherTests.cs tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs tests/SqlSource.Tests.Postgres/Describer/TableMatchTests.cs tests/SqlSource.Tool.Tests/Fixtures
```

```bash
git commit -m "Say which table a query's result is, or the first check that failed

TableMatcher runs the six checks of the format's tableMatch in the format's
order over the columns and their origins, and asks the server nothing: an
origin holds its relation with every attribute that is not dropped.  The
last check asks ColumnNullability what a query that is not gated records
for the column, so a NOT NULL that is not validated and a materialized view
answer as they do for a column.  The session makes the match after the
columns are built.  Six cases with rows are recorded again: their captures
are the same, and each expected.json has its match.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: The event

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresEvents.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs` (`DescribePreparedAsync`, `MatchOf`)
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresEventsTests.cs`

**Interfaces:**
- Consumes: `TableMatcher.Match`; `PostgresSession.MatchOf` as Task 2 left it; `IRunLog.Write(string name, params LogMember[] members)`; `LogValue`'s implicit conversion from `string?` and `LogValue.Null`; the tests' `RecordingLog`, `SessionRunLog(IRunLog inner, string secret, string? prefix)`, `PostgresScript`.
- Produces: `PostgresEvents.TableMatch(IRunLog log, string query, SidecarTable? table, string result)`, which writes `table-match` with `query`, `result`, `table`.

- [ ] **Step 1: Write the failing tests**

In `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresEventsTests.cs`, in `Describe_QueryWithColumns_WritesColumnsTablesAColumnForEachAndCheck`:

The assertion of the names becomes

```csharp
        log.Events.Select(written => written.Name)
            .ShouldBe([
                "prepare",
                "columns",
                "tables",
                "types",
                "column",
                "column",
                "table-match",
                "check",
                "deallocate",
            ]);
```

and its end, from `var check = log.Events[6].Members;`, becomes

```csharp
        var match = log.Events[6].Members;
        NamesOf(match).ShouldBe(["query", "result", "table"]);
        Member(match, "query").ShouldBe((LogValue)"ListUsers");
        Member(match, "result").ShouldBe((LogValue)"no-origin");
        Member(match, "table").ShouldBe(LogValue.Null);

        var check = log.Events[7].Members;
        NamesOf(check).ShouldBe(["query", "parameters", "result", "differs", "durationMs"]);
        Member(check, "result").ShouldBe((LogValue)"same");
        Member(check, "differs").ShouldBe(LogValue.Null);
    }
```

After `Describe_ThroughTheLogOfASession_RemovesTheSecretFromAColumnsNameAndItsOrigin`, add:

```csharp
    // A table of one column, whole in the answer of "tables", and the query that is the table.
    private static readonly PostgresTableRow Ids = PostgresTables.TableOf(
        16500,
        "users",
        PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true)
    );

    private static readonly DescribeRequest Whole = new(
        "ListIds",
        "SELECT * FROM users",
        EquatableArray<SqlQueryParameter>.Empty
    );

    private static PostgresScript WholeScript()
    {
        var id = PostgresTables.ColumnOf(0, "id", 23, Ids, 1);
        return new PostgresScript()
            .Open()
            .Answer("prepare", new PostgresPrepareRequest("sqlsource_1", Whole.Sql, []), new PostgresPrepareAnswer([], null))
            .Answer("columns", new PostgresColumnsRequest("sqlsource_1", 0), new PostgresColumnsAnswer([id], null))
            .Answer("tables", new PostgresTablesRequest([Ids.Oid]), new PostgresTablesAnswer([Ids], null))
            .Answer("types", new PostgresTypesRequest([23]), new PostgresTypesAnswer([PostgresRows.Int4], null))
            .Answer("check", new PostgresCheckRequest(Whole.Sql, []), Sees(id))
            .Answer("deallocate", Deallocate, new PostgresDeallocateAnswer(null));
    }

    [Fact]
    public async Task Describe_QueryThatIsATable_WritesTheTableInTableMatch()
    {
        var log = new RecordingLog();

        await DescribeAsync(WholeScript(), log, Whole);

        log.Events.Select(written => written.Name)
            .ShouldBe(["prepare", "columns", "tables", "types", "column", "table-match", "check", "deallocate"]);
        var match = log.Events[5].Members;
        Member(match, "query").ShouldBe((LogValue)"ListIds");
        Member(match, "result").ShouldBe((LogValue)"matched");
        Member(match, "table").ShouldBe((LogValue)"public.users");
    }

    // The match is in the log when the check that follows it fails.
    [Fact]
    public async Task Describe_CheckThatDiffers_StillWritesTheTableMatchBeforeIt()
    {
        var log = new RecordingLog();
        var script = ListingScript().Answer("check", new PostgresCheckRequest(Listing.Sql, []), Sees(IdColumn));

        await DescribeAsync(script, log, Listing);

        log.Events.Select(written => written.Name)
            .ShouldBe(["prepare", "columns", "tables", "types", "column", "column", "table-match", "check", "deallocate"]);
    }

    // A table's name is a user's text, and the connection's value is taken out of it as out of any string.
    [Fact]
    public async Task Describe_ThroughTheLogOfASession_RemovesTheSecretFromTheTableOfATableMatch()
    {
        var log = new RecordingLog();

        await DescribeAsync(WholeScript(), new SessionRunLog(log, "users", "postgres."), Whole);

        var match = log.Events.Single(written => written.Name == "postgres.table-match").Members;
        Member(match, "table").ShouldBe((LogValue)"public.***");
    }
```

`./format.sh` wraps the long lines of `WholeScript` and of the second test.  A statement without rows writes no `table-match`: `Describe_QueryWithAParameter_WritesPrepareTypesAndDeallocateInOrder`, which is there, holds the whole list of such a describe and is not changed.

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PostgresEventsTests"`
Expected: the changed test and the three new ones FAIL, each for a list of names without `table-match`, or for no event of that name.

- [ ] **Step 3: Write the event**

In `src/SqlSource.Tool/Describing/Postgres/PostgresEvents.cs`, the last sentence of the class's remarks, which is

```csharp
/// event of its own: the run's <c>database.open</c> records what the session knows of its server.  A describe
/// writes <c>prepare</c>, <c>columns</c>, <c>tables</c>, <c>types</c>, one <c>column</c> for each column,
/// <c>check</c> and <c>deallocate</c>, in that order, each only when its call was made.
```

becomes

```csharp
/// event of its own: the run's <c>database.open</c> records what the session knows of its server.  A describe
/// writes <c>prepare</c>, <c>columns</c>, <c>tables</c>, <c>types</c>, one <c>column</c> for each column,
/// <c>table-match</c>, <c>check</c> and <c>deallocate</c>, in that order, each only when its call was made, and
/// <c>table-match</c> only for a statement with rows whose columns were all built.
```

After the method `Column`, add:

```csharp
    /// <summary>
    /// <c>postgres.table-match</c>: <c>matched</c> with the relation whose column list the result is, as
    /// <c>schema.name</c>, or the first check of the match that failed with no table.
    /// </summary>
    public static void TableMatch(IRunLog log, string query, SidecarTable? table, string result) =>
        log.Write(
            "table-match",
            new("query", query),
            new("result", result),
            new("table", table is null ? LogValue.Null : (LogValue)$"{table.Schema}.{table.Name}")
        );
```

- [ ] **Step 4: Write it from the session**

In `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`, in `DescribePreparedAsync`, the line `var match = MatchOf(built.Value, origins);` becomes `var match = MatchOf(request, built.Value, origins);`, and `MatchOf`, with its comment, becomes:

```csharp
    // A statement without rows has no match, and no event of one.
    private (SidecarTable? Table, string Result)? MatchOf(
        DescribeRequest request,
        IReadOnlyList<SidecarColumn> columns,
        ColumnOrigin?[] origins
    )
    {
        if (columns.Count == 0)
        {
            return null;
        }

        var match = TableMatcher.Match(columns, origins);
        PostgresEvents.TableMatch(_log, request.Name, match.Table, match.Result);
        return match;
    }
```

- [ ] **Step 5: Run the tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PostgresEventsTests"`
Expected: PASS.

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PostgresSessionTests"`
Expected: PASS.

- [ ] **Step 6: Format, validate and commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`.
Expected: every step `passed`.

```bash
git add src/SqlSource.Tool/Describing/Postgres/PostgresEvents.cs src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresEventsTests.cs
```

```bash
git commit -m "Log the table match of a describe

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: The fixtures

**Files:**
- Create: `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/<case>/query.sql` for the eleven cases below, and the `capture.json` and `expected.json` that the recording writes beside each
- Modify: `tests/SqlSource.Tool.Tests/Fixtures/DescriberFixtureTests.cs` (`Cases_OfPostgres_HoldEveryCase`)

**Interfaces:**
- Consumes: the describer as Tasks 2 and 3 left it; `tools/record-fixtures.sh`; `FixtureCasesTests` and `DescriberFixtureTests`.
- Produces: twenty-three cases under `postgres/`, which sub-phase 3.5 records again.

- [ ] **Step 1: Write the eleven queries**

Each file ends with one line break and holds exactly one query.

`match-star/query.sql`:

```sql
-- name: GetUser
SELECT * FROM users WHERE id = @id;
```

`match-explicit-list/query.sql`:

```sql
-- name: ListUsers
SELECT id, name, email, status, tags, balance, created_at, deleted_at, name_upper FROM users;
```

`match-view-star/query.sql`:

```sql
-- name: ListActiveUsersWhole
SELECT * FROM active_users;
```

`match-alias/query.sql`:

```sql
-- name: ListUsersRenamed
SELECT id, name AS user_name, email, status, tags, balance, created_at, deleted_at, name_upper FROM users;
```

`match-suffix/query.sql`:

```sql
-- name: ListUsersWithASuffix
SELECT id, name AS "name!", email, status, tags, balance, created_at, deleted_at, name_upper FROM users;
```

`match-missing-column/query.sql`:

```sql
-- name: ListUsersWithoutTheLast
SELECT id, name, email, status, tags, balance, created_at, deleted_at FROM users;
```

`match-twice/query.sql`:

```sql
-- name: ListUsersWithIdTwice
SELECT id, id, name, email, status, tags, balance, created_at, deleted_at, name_upper FROM users;
```

`match-reordered/query.sql`:

```sql
-- name: ListUsersNameFirst
SELECT name, id, email, status, tags, balance, created_at, deleted_at, name_upper FROM users;
```

`match-expression/query.sql`:

```sql
-- name: ListUsersShouted
SELECT *, upper(name) AS shouted FROM users;
```

`match-two-tables/query.sql`:

```sql
-- name: ListUsersWithOrders
SELECT u.*, o.* FROM users AS u JOIN orders AS o ON o.user_id = u.id;
```

`match-left-join-nullability/query.sql`:

```sql
-- name: ListOrderUsers
SELECT u.* FROM orders AS o LEFT JOIN users AS u ON u.id = o.user_id;
```

- [ ] **Step 2: Name the cases in the test that holds them**

In `tests/SqlSource.Tool.Tests/Fixtures/DescriberFixtureTests.cs`, in `Cases_OfPostgres_HoldEveryCase`, the array gains the eleven names, between `"postgres/insert-returning",` and `"postgres/select-declared-domain",`, in the order of their names:

```csharp
                "postgres/match-alias",
                "postgres/match-explicit-list",
                "postgres/match-expression",
                "postgres/match-left-join-nullability",
                "postgres/match-missing-column",
                "postgres/match-reordered",
                "postgres/match-star",
                "postgres/match-suffix",
                "postgres/match-twice",
                "postgres/match-two-tables",
                "postgres/match-view-star",
```

- [ ] **Step 3: Record**

Run: `tools/record-fixtures.sh`
Expected: `Recording 23 cases of postgres against postgres:18 ... done`.

Run: `git status --short tests/SqlSource.Tool.Tests/Fixtures`
Expected: the eleven new folders untracked and `DescriberFixtureTests.cs` modified; no file of the twelve older cases changed.  A change there is a bug: Task 2 recorded them with this describer.

- [ ] **Step 4: Read what was recorded**

Run: `grep -H -A5 '"matchesTable"' tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/match-*/expected.json | grep -E 'matchesTable|"schema"|"table"|tableMatch|"plan"'`
Expected, for each case:

| Case | `matchesTable` | `plan` | `tableMatch` |
|----|----|----|----|
| `match-alias` | `null` | `not-needed` | `names-differ` |
| `match-explicit-list` | `public`, `users` | `not-needed` | `matched` |
| `match-expression` | `null` | `not-needed` | `no-origin` |
| `match-left-join-nullability` | `null` | `unavailable` | `nullability-differs` |
| `match-missing-column` | `null` | `not-needed` | `columns-differ` |
| `match-reordered` | `null` | `not-needed` | `order-differs` |
| `match-star` | `public`, `users` | `not-needed` | `matched` |
| `match-suffix` | `null` | `not-needed` | `names-differ` |
| `match-twice` | `null` | `not-needed` | `columns-differ` |
| `match-two-tables` | `null` | `not-needed` | `several-tables` |
| `match-view-star` | `public`, `active_users` | `not-needed` | `matched` |

A case that recorded another value is a finding: stop and tell the user, and do not change the query to fit.

Run: `grep -c '"method": "tables"' tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/match-star/capture.json`
Expected: `1`: the match asked nothing more.

- [ ] **Step 5: Replay and describe live**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Fixtures.DescriberFixtureTests"`
Expected: PASS, with a case of the theory for each of the twenty-three.

Run: `dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class "SqlSource.Tests.Postgres.Describer.FixtureCasesTests"`
Expected: PASS on the default image.

Run: `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14 dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class "SqlSource.Tests.Postgres.Describer.FixtureCasesTests"`
Expected: PASS: PostgreSQL 14 describes each case as 18 does, but for the version.  A case that differs there is a finding: stop and tell the user.

- [ ] **Step 6: Format, validate and commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`.
Expected: every step `passed`.

```bash
git add tests/SqlSource.Tool.Tests/Fixtures
```

```bash
git commit -m "Record the fixtures of the table match

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: The documents, the outline, and the pull request

**Files:**
- Modify: `src/SqlSource.Tool/AGENTS.md`
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`

**Interfaces:**
- Consumes: everything Tasks 2 to 4 built, by name.
- Produces: the pull request.

- [ ] **Step 1: Mend the describer's bullets of `src/SqlSource.Tool/AGENTS.md`**

In the bullet that begins `**`Describing/Postgres/` is the PostgreSQL describer, and it is registered nowhere.**`:

- `until the describer is whole: a description lacks `matchesTable`, which is always `null` with `tableMatch` `no-origin`, and the walk.` becomes `until the describer is whole: a description lacks the walk.`
- `the parameters typed; the columns built; `check`; and `deallocate` in the `finally`.` becomes `the parameters typed; the columns built; the table match, which asks nothing; `check`; and `deallocate` in the `finally`.`

After the bullet that begins `**A column is `nullable` `false` only when the catalog says `NOT NULL`**`, add this bullet:

```markdown
- **The table match is `TableMatcher`, and it runs after everything that sets a column's nullability.**  `PostgresSession.MatchOf` calls it once for a statement with rows, after the columns are built, and sub-phase 3.5's walk goes before it: its last check reads the `nullable` each column was given.  It is given the columns and the `ColumnOrigin` of each, which holds the relation and every attribute of it that is not dropped, as `tables` answered, and it asks the server nothing.  The checks are the values of the format's `tableMatch` in the format's order, `no-origin`, `several-tables`, `names-differ`, `columns-differ`, `order-differs`, `nullability-differs`, and the first that fails is the answer: do not reorder them, since a sidecar says which failed first.  A view, a materialized view and a foreign table may match, as a table may.  What a relation's column has, for the last check, is `ColumnNullability.Of` for a query that is not gated, so the rule of a `NOT NULL` is written once: a gated query over a table with a `NOT NULL` column is `nullability-differs`, and a `SELECT *` from a table whose `NOT NULL` is `NOT VALID` matches.  A name is compared ordinally, as the server returned it, so an override suffix, `AS "name!"`, is `names-differ`.  The lists are compared, not the sets: a column twice is `columns-differ`.  An `INSERT ... RETURNING *` matches its table.  A statement without rows has no match and no `table-match` event; a statement with rows writes one, between its `column` events and `check`.
```

Run: `grep -c 'always `null`' src/SqlSource.Tool/AGENTS.md`
Expected: `0`.

- [ ] **Step 2: Close the row of the outline, and record the reading**

In `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`:

The row that begins `| 3.4 The table match | In progress |` becomes `| 3.4 The table match | Done |`.  Nothing else of the row changes.

After the bullet that begins `- **The gate has three words more than sub-phase 3.3's spec lists**`, add:

```markdown
- **The table match takes a column's nullability from the rule of the columns.**  Sub-phase 3.4's spec says `!notNull` for a table and `null` for a view or a foreign table.  The review of 3.3 had since made a `NOT NULL` that is `NOT VALID` nullable and a materialized view answer as a view, so the match's last check asks what a query that is not gated records for the column, and the rule is written once.  The match needs no call of its own: a column's origin holds its relation with every attribute that is not dropped.  An `INSERT ... RETURNING *` matches its table, since a `RETURNING` list keeps the table's origins, and a table joined to itself and selected whole twice is `columns-differ`.
```

- [ ] **Step 3: Check the other documents**

Run: `grep -rn 'no-origin' README.md CONTRIBUTING.md docs/publishing.md docs/diagnostics.md src/SqlSource.Tool/README.md`
Expected: no line that says the describer's `tableMatch` is always `no-origin`.  The spec expects no change to any of the five.

Run: `git status --short docs/tech-debt docs/deferred`
Expected: nothing, unless a task found a gap and recorded it.

- [ ] **Step 4: Format, validate and commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`.
Expected: every step `passed`.

```bash
git add src/SqlSource.Tool/AGENTS.md docs/superpowers/specs/2026-10-07-query-generation-epic-design.md
```

```bash
git commit -m "Document the table match, and close sub-phase 3.4

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 5: Check the version**

Run: `git fetch --tags origin`, then `git tag --list 'v*' --sort=-v:refname | head -n 1`.
Expected: nothing: the repository has no `v*` tag, and there is nothing to compare `VersionPrefix` with.  If a tag is there and `VersionPrefix` in `Directory.Build.props` is not greater, ask the user for the new version and set it in this pull request.

- [ ] **Step 6: Open the pull request, with autofix on**

```bash
git push -u origin claude/query-generation-phase-3-4-table-match
```

```bash
gh pr create --base main --title "Query generation 3.4: the PostgreSQL describer's table match" --body "$(cat <<'EOF'
Sub-phase 3.4 of the query generation epic: the describer says whether a query's result is exactly one relation's column list.

- `TableMatcher` runs the six checks of the format's `tableMatch` in the format's order, over the columns and their origins, and asks the server nothing.
- `PostgresSession` fills `matchesTable` and `tableMatch` for every statement with rows, and writes `postgres.table-match`.
- Six fixture cases have their match in `expected.json`, with no capture changed, and eleven `match-*` cases are new.
- The describer is still registered nowhere.

Where the plan departs from the spec's wording, which the epic outline now records: the last check asks `ColumnNullability` what a query that is not gated records for a column, so a `NOT NULL` that is `NOT VALID` and a materialized view answer as they do for a column.

Spec: `docs/superpowers/specs/2026-10-12-query-generation-phase-3-4-table-match-design.md`
Plan: `docs/superpowers/plans/2026-10-13-query-generation-phase-3-4-table-match.md`

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

Then turn autofix on for the pull request, which the user's instructions require for every pull request, and read its CI.  Reply to every CodeRabbit finding in its thread, as `AGENTS.md` requires.
