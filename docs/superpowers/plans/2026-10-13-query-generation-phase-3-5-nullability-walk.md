# Query generation, phase 3.5: the nullability walk - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** For a gated query with rows, the PostgreSQL describer asks the server for a generic plan of the prepared statement and walks it, so that a `NOT NULL` column that no outer join null-extends is `false` with `catalog` and one that a join extends is `true` with `outer-join`; registered nowhere.

**Architecture:** A new call of the exchange, `explain`, runs `EXPLAIN (VERBOSE, FORMAT JSON) EXECUTE` under `force_generic_plan` in a transaction that is rolled back.  `PlanNode` reads the text, `PlanInstances` finds each relation instance and whether a join above it null-extends it, `OutputReference` reads an entry of the root's `Output`, and `NullabilityWalk` gives each column its answer and the query its `plan` value.  `PostgresSession` asks after `check`, and makes the table match after the walk.  Everything the walk cannot read keeps the heuristic of sub-phase 3.3.

**Tech Stack:** C# (`LangVersion` `preview`); the tool on `net8.0`, the tests on `net10.0`; Npgsql 10.0.3; System.Text.Json; Testcontainers.PostgreSql; xunit v3 on Microsoft.Testing.Platform with Shouldly; Docker.

**Spec:** `docs/superpowers/specs/2026-10-12-query-generation-phase-3-5-nullability-walk-design.md`.  Read it with this plan.  The epic outline, `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`, is kept current and wins where the two disagree, but for the one bullet Task 9 corrects.

**Branch:** `claude/query-generation-phase-3-5-nullability-walk`, which exists and holds the amended spec.  This file is untracked until Task 1 commits it.

## Where the plan settles what the spec leaves open

Each was checked against PostgreSQL 14 and 18 while the plan was written.  The spec stays as written but where a line here says Task 9 records it.

- **`walk-proved-empty` is `ON o.user_id = u.id AND 1 = 0`, not `AND o.id IS NULL`.**  PostgreSQL 17 and later prove `o.id IS NULL` false from the `NOT NULL` and print a bare `total` from a `Result`; 14 does not, and prints `o.total` under a real scan.  The two give `heuristic` and `outer-join`, and `FixtureCasesTests` compares both servers with one `expected.json`.  `1 = 0` is proved empty on both.  The `IS NULL` shape is a container test of `WalkTests` that expects each version's own answer.
- **`walk-view` holds a gate word of its own.**  `SELECT ... FROM user_orders` has none, since the join is in the view, so it is never walked and stays `view` throughout, as `select-view` shows.  The case joins the view to `billing.invoices` with a `LEFT JOIN`.
- **`walk-using` and `walk-natural` are `FULL` joins.**  The merged column of a `LEFT JOIN ... USING` is the left table's column, with its origin; only a `FULL` join merges with `COALESCE` and has none, which is what the matrix row says.
- **`walk-semi-anti` gates itself with `left(u.name, 1)`.**  `EXISTS` and `NOT EXISTS` are no gate words.
- **`walk-returning` has a `LEFT JOIN` in its `FROM`**, for the same reason.
- **`walk-union`'s columns have no origin**, so both are `null` with `no-origin` and `plan` is `unavailable`; the matrix row's "heuristic" has no column to apply to.
- **The `OLD`, `NEW` and `MERGE` container cases are those of `ColumnTests`**, which sub-phase 3.3 wrote and which this sub-phase changes: they are updated where they are, in Task 5, and `WalkTests` does not repeat them.
- **A query gated by `OLD`, `NEW` or `MERGE` is walked like any other.**  The epic outline says such a query "keeps the heuristic" because "its plan holds no outer join".  A `MERGE` does plan as a `Left`, `Right` or `Full` join under its `ModifyTable`, and `old.id` and `new.id` print under those names, which are no instance's alias.  So the walk gives the source's columns of a `MERGE` their join's answer, the target's `catalog`, and an `OLD` or `NEW` column `heuristic`.  Task 9 corrects the outline's bullet and the remark of `OuterJoinGate`.
- **Steps 1 and 2 of the walk are `PlanInstances`, steps 3 and 4 `NullabilityWalk`**, each with its own test class, where the spec's Testing names `NullabilityWalkTests` for all four.
- **The spec's step 4 is two commits**, Tasks 4 and 5: the walk over a plan a test writes, then the session.  So the pull request has nine commits.
- **The two gated cases that exist are recorded again in Task 5, not with the matrix.**  Once the session asks `explain`, their captures lack the call and `DescriberFixtureTests` fails.
- **"A settings name outside the list throws" is a test of `PlannerSettingsTests`**, with no server, where the spec's Testing puts it in `WalkTests`.
- **The `relation` of `postgres.walk.node` is a map of `schema` and `table`**, as the `table` of `postgres.table-match` is, where the spec says `schema.name`: a name may hold a dot.  Task 9 records it in the outline.
- **`postgres.walk.column` is written for every column of a gated query**, with a null `output` when there was no plan to read, and `postgres.walk` always holds `reason`, null when the plan was walked.
- **`Describing/Postgres/` reaches 25 files and is left flat**, as the spec places the walk there.  Whether the walk becomes a folder is the next maintainability review's to decide.
- **An `Anti` join null-extends its inner side, and a `Right Anti` its outer.**  The spec and the epic said an anti join extends nothing.  The review of Task 3 found, and a probe of 14 and 18 confirmed, that `a LEFT JOIN b ... WHERE b.x IS NULL` plans as an anti join whose `Output` still holds `b.id`, `NULL` in every row.  The spec is amended, the matrix gains `walk-anti`, and Task 9 records it in the outline.
- **A bare name at a root `ModifyTable` is its target's only when no join is under it.**  The review of Task 4 found, on 17 and 18, that `DELETE ... USING a LEFT JOIN b ON false ... RETURNING b.id` prints `b.id` as a bare `id` from a `Result` the join keeps, at a `ModifyTable` root: read as the target's column it was a wrong `false`.  `PlanInstances.RootNamesAnInstance` is false for a `ModifyTable` root with a join among the nodes it followed.  The spec is amended, and Task 5 has a container test of the shape.
- **`schema.sql` is not changed.**  A container test that needs a table of its own makes it, as `TableMatchTests` does.

## Global Constraints

- The describer is registered nowhere: `ToolHost.Create` is unchanged, and the released tool says `SQLSRC216` for `postgres`.
- A column is never `false` when the server can return `NULL` for it.  Anything the walk does not understand keeps the heuristic: `true` with `heuristic` for a table's column, `null` with `view` for a view's, `null` with `no-origin` without an origin.
- The walk adds no diagnostic and never fails a describe.  A failed `explain` is logged and never reported.
- The plan is asked for a gated query with rows alone, after `check` and before `deallocate`.  A query that is not gated asks nothing more than before, so its capture does not change.
- The route on every version: `BEGIN`; `SET LOCAL plan_cache_mode = force_generic_plan`; one `SET LOCAL` for each setting; `EXPLAIN (VERBOSE, FORMAT JSON) EXECUTE <name>(NULL, ...)`, or `EXECUTE <name>` without parameters; `ROLLBACK`.  `EXPLAIN (GENERIC_PLAN)` is not used.
- The settings a request may carry are `enable_hashjoin`, `enable_mergejoin`, `enable_nestloop`, `enable_memoize` and `enable_material`, each `on` or `off`.  The tool itself passes none.
- The join types the walk knows are `Inner`, `Left`, `Right`, `Full`, `Semi`, `Anti`, `Right Semi` and `Right Anti`.  `Left` and `Anti` extend their `Inner` child, `Right` and `Right Anti` their `Outer`, `Full` both, the rest nothing.  An anti join is what the planner makes of `a LEFT JOIN b ... WHERE b.x IS NULL`, and it returns `b`'s columns `NULL`.
- Only children whose `Parent Relationship` is `Outer` or `Inner` are followed.
- The reasons of `unavailable` are `explain-failed`, `unreadable`, `no-output`, `unknown-join-type`, `doubled-alias` and `grouping-sets`.
- The methods of the exchange become `open`, `prepare`, `columns`, `tables`, `types`, `check`, `explain` and `deallocate`.  A record of a call is sealed and holds strings, integers, booleans, nullable ones, lists of them and records of the same kind.
- An event's name and its members are a contract: a member is added and never renamed.  Nothing the describer logs holds the connection's value.
- `README.md`, `CONTRIBUTING.md`, `docs/publishing.md`, `docs/diagnostics.md` and `src/SqlSource.Tool/README.md` are not touched.
- The build treats warnings as errors with `AnalysisLevel` `latest-all`, Roslynator and Sonar.  Fix what it reports in the code; never suppress.  If a check is wrong, stop and tell the user.
- `ImplicitUsings` is off: a file names every namespace it uses.
- Comments say what a thing is for, in the plain declarative style of the files around them, with two spaces after a full stop.  Types under `src/` have XML documentation; test files use `//` comments.
- Test names are `Method_Scenario_Outcome`; assertions are Shouldly's.  A test takes its token from `TestContext.Current.CancellationToken`.
- Every commit passes `./pre-commit-validation.sh`, which needs Docker running and starts a PostgreSQL container.  No hook runs it: run `./format.sh`, then the validation after your last edit, then commit.  An edit made after a passing run needs another run.  Never commit past a failure and never edit the script to make one go away; if a check is wrong, stop and tell the user.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

How a test class is run by itself, here and in every task:

```bash
dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.NullabilityWalkTests"
dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class "SqlSource.Tests.Postgres.Describer.WalkTests"
```

The container tests against the older server:

```bash
SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14 dotnet test --project tests/SqlSource.Tests.Postgres
```

## Review Focus

- **`UPDATE` or `DELETE ... RETURNING` with a gate word in it.**  The `ModifyTable` sits over a scan of its own target with the same alias.  A user expects the returned row's `NOT NULL` columns to be `false`, not a whole query degraded.  Task 3, `Find_RootModifyTableOverAScanOfItsOwnAlias_IsOneInstanceAndUsable`; Task 5, the container's `Describe_UpdateFromReturning_GivesTheTargetTheCatalogsAnswer`.
- **A table aliased `old` or `new`.**  The gate lets the query in for the word, and the alias is a real instance.  A user expects its columns to follow its join, not to be `heuristic` for ever, and never to be taken for PostgreSQL 18's old row.  Task 5, `Describe_TableAliasedOld_IsAnInstanceLikeAnyOther` and `Describe_OldAliasThatIsAlsoATablesAlias_IsRefusedByTheServer`.
- **A partitioned table on the kept side of a join.**  It scans as an `Append` of `Member` children, so its alias is no instance.  A user expects `true` with `heuristic`, never `false`.  Task 5, `Describe_PartitionedTable_KeepsTheHeuristic`.
- **A query of many joins.**  The plan's text nests two levels for each node, and System.Text.Json refuses past 64 by default.  A user with a twelve-table report expects it walked, not `unavailable`.  Task 2, `Read_PlanOfAHundredNestedNodes_IsRead`.
- **A column whose expression starts like a reference**, `"left"((u.name)::text, 1)` or `u.id + 1`.  A user expects it to be an expression, never the column `left` of a root scan.  Task 4, `Parse_Expression_IsNoReference`.

## File structure

Created under `src/SqlSource.Tool/Describing/Postgres/`:

| File | Holds |
|----|----|
| `PlannerSettings.cs` | The closed list of the settings a request may carry, and the `SET LOCAL` of one. |
| `PlanNode.cs` | A node of the plan, and `Read`, which makes the tree of the answer's text. |
| `PlanInstances.cs` | Steps 1 and 2: the relation instances of a plan, which are null-extended, and why a plan is unusable.  `VisitedNode`, `WalkReasons`. |
| `OutputReference.cs` | An entry of `Output` read as `alias.column` or a bare name. |
| `NullabilityWalk.cs` | Steps 3 and 4: each column's answer and the plan's value.  `WalkOutcome`, `WalkedColumn`. |

Modified there: `PostgresCalls.cs` (the records of `explain`), `PostgresConnection.cs` (`BeginAsync`), `PostgresLive.cs` (`ExplainAsync` and its texts), `PostgresSession.cs` (the call, the walk, the match after it, the settings), `PostgresDescriber.cs` (the settings), `PostgresEvents.cs` (four events), `OuterJoinGate.cs` and `ColumnNullability.cs` (remarks).

Created under `tests/SqlSource.Tool.Tests/`: `PostgresPlans.cs`, a helper at the root; under `Describing/Postgres/`, `PlannerSettingsTests.cs`, `PlanNodeTests.cs`, `PlanInstancesTests.cs`, `OutputReferenceTests.cs`, `NullabilityWalkTests.cs`; under `Fixtures/`, `WalkMatrixTests.cs` and the cases of the matrix.  Modified: `PostgresScript.cs`, `Describing/Postgres/PostgresLiveTests.cs`, `PostgresSessionTests.cs`, `PostgresEventsTests.cs`, `PostgresDescriberTests.cs`, `Fixtures/FixtureCase.cs`, `Fixtures/DescriberFixtureTests.cs`.

Created under `tests/SqlSource.Tests.Postgres/Describer/`: `ExplainTests.cs`, `WalkTests.cs`.  Modified: `Sessions.cs`, `ColumnTests.cs`, `FixtureCasesTests.cs`.

Documents: `src/SqlSource.Tool/AGENTS.md`, `tests/AGENTS.md`, `docs/tech-debt/`, the epic outline.

---

### Task 1: The plan and the outline

**Files:**
- Create: `docs/superpowers/plans/2026-10-13-query-generation-phase-3-5-nullability-walk.md` (this file, already written)
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md:75`

**Interfaces:**
- Consumes: nothing.
- Produces: nothing a later task uses.

- [ ] **Step 1: Check the branch**

Run: `git branch --show-current`
Expected: `claude/query-generation-phase-3-5-nullability-walk`

- [ ] **Step 2: Set the outline's row**

In the table of sub-phases, the row that starts `| 3.5 The nullability walk | Not started |` becomes `| 3.5 The nullability walk | In progress |`.  Nothing else of the row changes.

- [ ] **Step 3: Validate and commit**

```bash
./pre-commit-validation.sh
```

```bash
git add docs/superpowers/plans/2026-10-13-query-generation-phase-3-5-nullability-walk.md docs/superpowers/specs/2026-10-07-query-generation-epic-design.md
git commit -m "$(cat <<'EOF'
Plan sub-phase 3.5, the nullability walk

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: `explain` and `PlanNode`

**Files:**
- Create: `src/SqlSource.Tool/Describing/Postgres/PlannerSettings.cs`, `src/SqlSource.Tool/Describing/Postgres/PlanNode.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresCalls.cs`, `PostgresConnection.cs`, `PostgresLive.cs`
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/PlannerSettingsTests.cs`, `PlanNodeTests.cs`, `PostgresLiveTests.cs`; `tests/SqlSource.Tests.Postgres/Describer/ExplainTests.cs`

**Interfaces:**
- Consumes: `PostgresConnection.CommandAsync`, `PostgresConnection.OpenAsync`, `PostgresConnection.ErrorOf`, `PostgresServerError`.
- Produces:
  - `sealed record PostgresExplainRequest(string Statement, int Parameters, IReadOnlyList<string> Settings)`
  - `sealed record PostgresExplainAnswer(string? Plan, PostgresServerError? Error)`
  - `PlannerSettings.None : IReadOnlyList<string>`; `PlannerSettings.Checked(IReadOnlyList<string>) : IReadOnlyList<string>`, which throws `ArgumentException`; `PlannerSettings.SetText(string) : string`
  - `PostgresLive.ExplainAsync(PostgresExplainRequest, CancellationToken) : Task<PostgresExplainAnswer>`; `PostgresLive.ExplainText(PostgresExplainRequest) : string`; `PostgresLive.SetTexts(PostgresExplainRequest) : IReadOnlyList<string>`
  - `sealed record PlanNode(string NodeType, string? JoinType, string? ParentRelationship, IReadOnlyList<string>? Output, string? Alias, string? RelationName, string? Schema, string? SubplanName, string? CteName, bool HasGroupingSets, IReadOnlyList<PlanNode> Plans)` with `static PlanNode? Read(string text)`, null for a text that is not a plan.

- [ ] **Step 1: Write the failing tests of the settings**

Create `tests/SqlSource.Tool.Tests/Describing/Postgres/PlannerSettingsTests.cs`:

```csharp
using System;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The settings of the planner that a request of "explain" may carry: a closed list, so that nothing else reaches a
// SET LOCAL.
public class PlannerSettingsTests
{
    [Theory]
    [InlineData("enable_hashjoin")]
    [InlineData("enable_mergejoin")]
    [InlineData("enable_nestloop")]
    [InlineData("enable_memoize")]
    [InlineData("enable_material")]
    public void SetText_SettingOfTheList_IsItsSetLocal(string name)
    {
        PlannerSettings.SetText(name + "=off").ShouldBe($"SET LOCAL {name} = off");
        PlannerSettings.SetText(name + "=on").ShouldBe($"SET LOCAL {name} = on");
    }

    [Theory]
    [InlineData("work_mem=1")]
    [InlineData("enable_seqscan=off")]
    [InlineData("enable_hashjoin=maybe")]
    [InlineData("enable_hashjoin=off; DROP TABLE users")]
    [InlineData("enable_hashjoin")]
    [InlineData("ENABLE_HASHJOIN=off")]
    [InlineData(" enable_hashjoin=off")]
    [InlineData("")]
    public void SetText_AnythingElse_Throws(string setting) =>
        Should.Throw<ArgumentException>(() => PlannerSettings.SetText(setting));

    [Fact]
    public void Checked_SettingsOfTheList_AreGivenBackInTheirOrder() =>
        PlannerSettings
            .Checked(["enable_nestloop=off", "enable_hashjoin=off"])
            .ShouldBe(["enable_nestloop=off", "enable_hashjoin=off"]);

    [Fact]
    public void Checked_NameOutsideTheList_Throws() =>
        Should.Throw<ArgumentException>(() => PlannerSettings.Checked(["enable_nestloop=off", "work_mem=1"]));

    [Fact]
    public void None_IsEmpty() => PlannerSettings.None.ShouldBeEmpty();
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PlannerSettingsTests"`
Expected: the build fails, `PlannerSettings` does not exist.

- [ ] **Step 3: Write `PlannerSettings`**

Create `src/SqlSource.Tool/Describing/Postgres/PlannerSettings.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// The settings of the planner that a request of <c>explain</c> may carry, each as <c>name=value</c>.  The tests
/// force a join method with them; the tool itself passes none.
/// </summary>
/// <remarks>
/// The list is closed and a value is <c>on</c> or <c>off</c>, so the text of a <c>SET LOCAL</c> holds nothing but
/// what is written here.
/// </remarks>
internal static class PlannerSettings
{
    private static readonly string[] Names =
    [
        "enable_hashjoin",
        "enable_mergejoin",
        "enable_nestloop",
        "enable_memoize",
        "enable_material",
    ];

    private static readonly string[] Values = ["on", "off"];

    /// <summary>No setting: what the tool passes.</summary>
    public static IReadOnlyList<string> None { get; } = [];

    /// <summary>The settings as given, when each is of the list.</summary>
    /// <exception cref="ArgumentException">A setting is not a name of the list with <c>on</c> or <c>off</c>.</exception>
    public static IReadOnlyList<string> Checked(IReadOnlyList<string> settings)
    {
        foreach (var setting in settings)
        {
            _ = SetText(setting);
        }

        return [.. settings];
    }

    /// <summary>The statement that sets one setting for the transaction it runs in.</summary>
    /// <exception cref="ArgumentException">The setting is not a name of the list with <c>on</c> or <c>off</c>.</exception>
    public static string SetText(string setting)
    {
        var parts = setting.Split('=');
        return
            parts.Length == 2
            && Names.Contains(parts[0], StringComparer.Ordinal)
            && Values.Contains(parts[1], StringComparer.Ordinal)
            ? $"SET LOCAL {parts[0]} = {parts[1]}"
            : throw new ArgumentException(
                $"'{setting}' is no setting of the planner that an explain may carry.",
                nameof(setting)
            );
    }
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PlannerSettingsTests"`
Expected: PASS.

- [ ] **Step 5: Write the failing tests of the texts**

Add to `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresLiveTests.cs`, inside the class:

```csharp
    [Fact]
    public void ExplainText_StatementWithoutParameters_ExplainsTheExecuteAlone() =>
        PostgresLive
            .ExplainText(new PostgresExplainRequest("sqlsource_1", 0, []))
            .ShouldBe("EXPLAIN (VERBOSE, FORMAT JSON) EXECUTE sqlsource_1");

    [Fact]
    public void ExplainText_StatementWithParameters_HasANullForEach() =>
        PostgresLive
            .ExplainText(new PostgresExplainRequest("sqlsource_7", 2, []))
            .ShouldBe("EXPLAIN (VERBOSE, FORMAT JSON) EXECUTE sqlsource_7(NULL, NULL)");

    // The generic plan first, then each setting in the order of the request.
    [Fact]
    public void SetTexts_RequestWithSettings_ForcesTheGenericPlanThenSetsEach() =>
        PostgresLive
            .SetTexts(new PostgresExplainRequest("sqlsource_1", 0, ["enable_nestloop=off", "enable_hashjoin=off"]))
            .ShouldBe([
                "SET LOCAL plan_cache_mode = force_generic_plan",
                "SET LOCAL enable_nestloop = off",
                "SET LOCAL enable_hashjoin = off",
            ]);

    [Fact]
    public void SetTexts_SettingOutsideTheList_Throws() =>
        Should.Throw<ArgumentException>(() =>
            PostgresLive.SetTexts(new PostgresExplainRequest("sqlsource_1", 0, ["work_mem=1"]))
        );
```

Add `using System;` to the file's usings.

- [ ] **Step 6: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PostgresLiveTests"`
Expected: the build fails, `PostgresExplainRequest` does not exist.

- [ ] **Step 7: Add the records**

Append to `src/SqlSource.Tool/Describing/Postgres/PostgresCalls.cs`:

```csharp
/// <summary>Everything that decides the answer of <c>explain</c>.</summary>
/// <param name="Statement">The prepared statement to plan.</param>
/// <param name="Parameters">The number of parameters of the query's list: a <c>NULL</c> is passed for each.</param>
/// <param name="Settings">
/// Settings of the planner as <c>name=value</c>, each of <see cref="PlannerSettings" />.  The tool passes none; a
/// test forces a join method with them.
/// </param>
internal sealed record PostgresExplainRequest(string Statement, int Parameters, IReadOnlyList<string> Settings);

/// <summary>The generic plan of a statement, or why the server gave none.</summary>
/// <param name="Plan">
/// The text <c>EXPLAIN (VERBOSE, FORMAT JSON)</c> returned, as the server wrote it.
/// </param>
/// <param name="Error">What the server said, or null.  It is logged and never reported.</param>
internal sealed record PostgresExplainAnswer(string? Plan, PostgresServerError? Error);
```

- [ ] **Step 8: Add `BeginAsync` to `PostgresConnection`**

After `CommandAsync` in `src/SqlSource.Tool/Describing/Postgres/PostgresConnection.cs`:

```csharp
    /// <summary>
    /// A transaction on the session's connection.  Its caller rolls it back, and disposes it: disposing one that
    /// was not ended rolls it back too.
    /// </summary>
    public async Task<NpgsqlTransaction> BeginAsync(CancellationToken cancellationToken) =>
        await (await OpenAsync(cancellationToken)).BeginTransactionAsync(cancellationToken);
```

- [ ] **Step 9: Add `explain` to `PostgresLive`**

In `src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs`, replace `ExecuteText` with the two below, and add the rest after `CheckAsync`'s helpers and before `DeallocateAsync`:

```csharp
    /// <summary>
    /// The text <c>columns</c> sends: the statement with a <c>NULL</c> for each parameter.  The server describes an
    /// <c>EXECUTE</c> whatever the number of its arguments, so a statement with a <c>$n</c> of its own is described
    /// too.
    /// </summary>
    public static string ExecuteText(PostgresColumnsRequest asked) => ExecuteText(asked.Statement, asked.Parameters);

    private static string ExecuteText(string statement, int parameters) =>
        parameters == 0
            ? $"EXECUTE {statement}"
            : $"EXECUTE {statement}({string.Join(", ", Enumerable.Repeat("NULL", parameters))})";
```

```csharp
    private const string ForceGenericPlanSql = "SET LOCAL plan_cache_mode = force_generic_plan";

    /// <summary>
    /// Asks <c>explain</c>: the generic plan of the prepared statement, in a transaction that is rolled back.
    /// </summary>
    /// <remarks>
    /// <c>EXPLAIN</c> without <c>ANALYZE</c> plans the statement and does not run it.  Under
    /// <c>force_generic_plan</c> the <c>NULL</c> of a parameter is not folded into the plan, so <c>id = $1</c>
    /// stays a condition.  Disposing a transaction that was not ended rolls it back, so a failure at any step
    /// leaves no setting behind.  The texts hold a name the tool made, settings of a closed list and nothing of a
    /// user's.
    /// </remarks>
    public async Task<PostgresExplainAnswer> ExplainAsync(PostgresExplainRequest asked, CancellationToken token)
    {
        try
        {
            await using var transaction = await connection.BeginAsync(token);
            foreach (var text in SetTexts(asked))
            {
                await using var set = await connection.CommandAsync(text, token);
                _ = await set.ExecuteNonQueryAsync(token);
            }

            await using var command = await connection.CommandAsync(ExplainText(asked), token);
            var plan =
                await command.ExecuteScalarAsync(token) as string
                ?? throw new InvalidOperationException("The server answered 'explain' with no text.");
            await transaction.RollbackAsync(token);
            return new PostgresExplainAnswer(plan, null);
        }
        catch (NpgsqlException exception)
        {
            return new PostgresExplainAnswer(null, PostgresConnection.ErrorOf(exception));
        }
    }

    /// <summary>The text <c>explain</c> sends for the plan.</summary>
    public static string ExplainText(PostgresExplainRequest asked) =>
        "EXPLAIN (VERBOSE, FORMAT JSON) " + ExecuteText(asked.Statement, asked.Parameters);

    /// <summary>What <c>explain</c> sets for its transaction, in order: the generic plan, then each setting.</summary>
    /// <exception cref="ArgumentException">A setting is not of <see cref="PlannerSettings" />.</exception>
    public static IReadOnlyList<string> SetTexts(PostgresExplainRequest asked) =>
        [ForceGenericPlanSql, .. asked.Settings.Select(PlannerSettings.SetText)];
```

- [ ] **Step 10: Run the tests of the texts and of the records**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PostgresLiveTests"`
Expected: PASS.

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Capture.CallRecordTests"`
Expected: PASS, with `PostgresExplainRequest` and `PostgresExplainAnswer` among the records it found.

- [ ] **Step 11: Write the failing tests of `PlanNode`**

Create `tests/SqlSource.Tool.Tests/Describing/Postgres/PlanNodeTests.cs`:

```csharp
using System.Linq;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The plan as EXPLAIN (VERBOSE, FORMAT JSON) writes it, read into the keys the walk uses.
public class PlanNodeTests
{
    // A LEFT JOIN the planner swapped, as PostgreSQL 18 wrote it, cut to a few keys and one that no reader knows.
    private const string Swapped = """
        [
          {
            "Plan": {
              "Node Type": "Hash Join",
              "Parallel Aware": false,
              "Join Type": "Right",
              "Startup Cost": 13.15,
              "Output": ["u.id", "o.total"],
              "Inner Unique": true,
              "Hash Cond": "(o.user_id = u.id)",
              "Plans": [
                {
                  "Node Type": "Seq Scan",
                  "Parent Relationship": "Outer",
                  "Relation Name": "orders",
                  "Schema": "public",
                  "Alias": "o",
                  "Output": ["o.id", "o.user_id", "o.total"]
                },
                {
                  "Node Type": "Hash",
                  "Parent Relationship": "Inner",
                  "Output": ["u.id"],
                  "Plans": [
                    {
                      "Node Type": "CTE Scan",
                      "Parent Relationship": "Outer",
                      "CTE Name": "s",
                      "Alias": "u",
                      "Subplan Name": "CTE s"
                    }
                  ]
                }
              ]
            },
            "Planning Time": 0.1
          }
        ]
        """;

    [Fact]
    public void Read_Plan_ReadsEachKeyOfEveryNode()
    {
        var root = PlanNode.Read(Swapped).ShouldNotBeNull();

        root.NodeType.ShouldBe("Hash Join");
        root.JoinType.ShouldBe("Right");
        root.ParentRelationship.ShouldBeNull();
        root.Output.ShouldBe(["u.id", "o.total"]);
        root.Alias.ShouldBeNull();
        root.HasGroupingSets.ShouldBeFalse();
        root.Plans.Select(static child => child.NodeType).ShouldBe(["Seq Scan", "Hash"]);

        var scan = root.Plans[0];
        scan.ParentRelationship.ShouldBe("Outer");
        scan.Alias.ShouldBe("o");
        scan.RelationName.ShouldBe("orders");
        scan.Schema.ShouldBe("public");
        scan.JoinType.ShouldBeNull();
        scan.Plans.ShouldBeEmpty();

        var cte = root.Plans[1].Plans.ShouldHaveSingleItem();
        cte.NodeType.ShouldBe("CTE Scan");
        cte.CteName.ShouldBe("s");
        cte.SubplanName.ShouldBe("CTE s");
        cte.Output.ShouldBeNull();
    }

    // The key is read as present or absent: what it holds is the planner's own.
    [Theory]
    [InlineData("""[{"Plan": {"Node Type": "Aggregate", "Grouping Sets": [{"Group Keys": [["u.status"], []]}]}}]""")]
    [InlineData("""[{"Plan": {"Node Type": "Aggregate", "Grouping Sets": []}}]""")]
    public void Read_NodeWithGroupingSets_SaysSo(string text) =>
        PlanNode.Read(text).ShouldNotBeNull().HasGroupingSets.ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("""{"Plan": {"Node Type": "Result"}}""")]
    [InlineData("""[{"Plan": {"Node Type": "Result"}}, {"Plan": {"Node Type": "Result"}}]""")]
    [InlineData("""[{"Planning Time": 0.1}]""")]
    [InlineData("""[{"Plan": []}]""")]
    [InlineData("""[{"Plan": {"Output": ["a"]}}]""")]
    [InlineData("""[{"Plan": {"Node Type": 7}}]""")]
    [InlineData("""[{"Plan": {"Node Type": "Result", "Output": "a"}}]""")]
    [InlineData("""[{"Plan": {"Node Type": "Result", "Output": [1]}}]""")]
    [InlineData("""[{"Plan": {"Node Type": "Result", "Alias": 1}}]""")]
    [InlineData("""[{"Plan": {"Node Type": "Result", "Plans": {}}}]""")]
    [InlineData("""[{"Plan": {"Node Type": "Result", "Plans": [{"Alias": "u"}]}}]""")]
    public void Read_TextThatIsNotAnArrayOfOnePlan_IsNull(string text) => PlanNode.Read(text).ShouldBeNull();

    // A chain of nodes, each the one child of the last.
    private static string Nested(int nodes) =>
        "[{\"Plan\": "
        + string.Concat(Enumerable.Repeat("{\"Node Type\": \"Materialize\", \"Plans\": [", nodes - 1))
        + "{\"Node Type\": \"Result\"}"
        + string.Concat(Enumerable.Repeat("]}", nodes - 1))
        + "}]";

    // Each node is two levels of the text, and the reader's own limit is 64: a plan of many joins is read all the
    // same.
    [Fact]
    public void Read_PlanOfAHundredNestedNodes_IsRead()
    {
        var node = PlanNode.Read(Nested(100)).ShouldNotBeNull();

        var depth = 1;
        while (node.Plans.Count > 0)
        {
            node = node.Plans[0];
            depth++;
        }

        depth.ShouldBe(100);
        node.NodeType.ShouldBe("Result");
    }

    // Past the limit the text is no plan the walk can use, and nothing is thrown.
    [Fact]
    public void Read_PlanNestedPastTheLimit_IsNull() => PlanNode.Read(Nested(400)).ShouldBeNull();
}
```

- [ ] **Step 12: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PlanNodeTests"`
Expected: the build fails, `PlanNode` does not exist.

- [ ] **Step 13: Write `PlanNode`**

Create `src/SqlSource.Tool/Describing/Postgres/PlanNode.cs`:

```csharp
using System.Collections.Generic;
using System.Text.Json;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// A node of a plan as <c>EXPLAIN (VERBOSE, FORMAT JSON)</c> writes it: the keys the walk reads, and no other.
/// </summary>
/// <param name="NodeType"><c>Node Type</c>: <c>Hash Join</c>, <c>Seq Scan</c>.</param>
/// <param name="JoinType"><c>Join Type</c>, which a join alone has: <c>Left</c>, <c>Right Anti</c>.</param>
/// <param name="ParentRelationship">
/// <c>Parent Relationship</c>: <c>Outer</c> and <c>Inner</c> for the sides of a join and the one input of any
/// other node; <c>InitPlan</c>, <c>SubPlan</c>, <c>Member</c> and <c>Subquery</c> for another plan.  Null on the
/// root.
/// </param>
/// <param name="Output">
/// <c>Output</c>: the node's target list as text.  On the root it is the result's, in order, with what a sort or a
/// group added after it.
/// </param>
/// <param name="Alias"><c>Alias</c>, as the name is and not as it is printed: <c>Order</c> for <c>"Order"</c>.</param>
/// <param name="RelationName"><c>Relation Name</c>.</param>
/// <param name="Schema"><c>Schema</c>.</param>
/// <param name="SubplanName"><c>Subplan Name</c>.</param>
/// <param name="CteName"><c>CTE Name</c>.</param>
/// <param name="HasGroupingSets">Whether the node has a <c>Grouping Sets</c> key, whatever it holds.</param>
/// <param name="Plans"><c>Plans</c>: the children, empty when the node has none.</param>
internal sealed record PlanNode(
    string NodeType,
    string? JoinType,
    string? ParentRelationship,
    IReadOnlyList<string>? Output,
    string? Alias,
    string? RelationName,
    string? Schema,
    string? SubplanName,
    string? CteName,
    bool HasGroupingSets,
    IReadOnlyList<PlanNode> Plans
)
{
    // Each node is two levels of the text, and the reader's own limit of 64 would refuse a plan of thirty.
    private const int MaxDepth = 512;

    /// <summary>
    /// The root of a plan's text, or null: the text is an array of one object whose <c>Plan</c> is the root, and
    /// anything else is a plan that cannot be used.  A key the walk does not read is ignored.
    /// </summary>
    public static PlanNode? Read(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = MaxDepth });
            var root = document.RootElement;
            return
                root.ValueKind == JsonValueKind.Array
                && root.GetArrayLength() == 1
                && root[0].ValueKind == JsonValueKind.Object
                && root[0].TryGetProperty("Plan", out var plan)
                ? NodeOf(plan)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static PlanNode? NodeOf(JsonElement element)
    {
        if (
            element.ValueKind != JsonValueKind.Object
            || !TryText(element, "Node Type", out var nodeType)
            || nodeType is null
            || !TryText(element, "Join Type", out var joinType)
            || !TryText(element, "Parent Relationship", out var relationship)
            || !TryTexts(element, "Output", out var output)
            || !TryText(element, "Alias", out var alias)
            || !TryText(element, "Relation Name", out var relation)
            || !TryText(element, "Schema", out var schema)
            || !TryText(element, "Subplan Name", out var subplan)
            || !TryText(element, "CTE Name", out var cte)
            || !TryNodes(element, out var plans)
        )
        {
            return null;
        }

        return new PlanNode(
            nodeType,
            joinType,
            relationship,
            output,
            alias,
            relation,
            schema,
            subplan,
            cte,
            element.TryGetProperty("Grouping Sets", out _),
            plans
        );
    }

    // False when the key holds what is no string.  A key that is absent is null.
    private static bool TryText(JsonElement element, string key, out string? text)
    {
        text = null;
        if (!element.TryGetProperty(key, out var value))
        {
            return true;
        }

        text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return text is not null;
    }

    // False when the key holds what is no array of strings.  A key that is absent is null.
    private static bool TryTexts(JsonElement element, string key, out IReadOnlyList<string>? texts)
    {
        texts = null;
        if (!element.TryGetProperty(key, out var value))
        {
            return true;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var read = new List<string>(value.GetArrayLength());
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || item.GetString() is not { } text)
            {
                return false;
            }

            read.Add(text);
        }

        texts = read;
        return true;
    }

    // False when "Plans" holds what is no array of nodes.  A node without the key has no child.
    private static bool TryNodes(JsonElement element, out IReadOnlyList<PlanNode> nodes)
    {
        nodes = [];
        if (!element.TryGetProperty("Plans", out var value))
        {
            return true;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var read = new List<PlanNode>(value.GetArrayLength());
        foreach (var item in value.EnumerateArray())
        {
            if (NodeOf(item) is not { } node)
            {
                return false;
            }

            read.Add(node);
        }

        nodes = read;
        return true;
    }
}
```

- [ ] **Step 14: Run them to see them pass**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PlanNodeTests"`
Expected: PASS.

- [ ] **Step 15: Write the container tests of the live call**

Create `tests/SqlSource.Tests.Postgres/Describer/ExplainTests.cs`:

```csharp
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// "explain" against a real server: the plan is a generic one that PlanNode reads, and the call leaves the session
// as it found it.
[Collection(UsesPostgres.Name)]
public sealed class ExplainTests(PostgresContainer postgres)
{
    private const string Statement = "walk_explain";

    private const string LeftJoin =
        "SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id WHERE u.id = $1";

    private static async Task<string> ShowAsync(PostgresConnection connection, string setting)
    {
        var token = TestContext.Current.CancellationToken;
        await using var command = await connection.CommandAsync($"SHOW {setting}", token);
        return (string)(await command.ExecuteScalarAsync(token)).ShouldNotBeNull();
    }

    private static async Task PrepareAsync(PostgresLive live)
    {
        var prepared = await live.PrepareAsync(
            new PostgresPrepareRequest(Statement, LeftJoin, ["unknown"]),
            TestContext.Current.CancellationToken
        );
        prepared.Error.ShouldBeNull(prepared.Error?.Message);
    }

    // The NULL of the parameter is not folded: the condition on $1 is in the plan, and the join is still there.
    [Fact]
    public async Task ExplainAsync_PreparedStatement_AnswersAGenericPlanThatPlanNodeReads()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new PostgresConnection(postgres.ConnectionString);
        var live = new PostgresLive(connection);
        await PrepareAsync(live);

        var answer = await live.ExplainAsync(new PostgresExplainRequest(Statement, 1, []), token);

        answer.Error.ShouldBeNull(answer.Error?.Message);
        var text = answer.Plan.ShouldNotBeNull();
        text.ShouldContain("$1");
        text.ShouldNotContain("One-Time Filter");
        var root = PlanNode.Read(text).ShouldNotBeNull();
        root.JoinType.ShouldBe("Left");
        root.Output.ShouldBe(["u.id", "o.total"]);
        root.Plans.Select(static child => child.Alias).ShouldBe(["u", "o"]);
    }

    [Theory]
    [InlineData("enable_mergejoin=off", "enable_nestloop=off", "Hash Join")]
    [InlineData("enable_hashjoin=off", "enable_nestloop=off", "Merge Join")]
    [InlineData("enable_hashjoin=off", "enable_mergejoin=off", "Nested Loop")]
    public async Task ExplainAsync_WithTwoMethodsOff_PlansWithTheThirdAndLeavesTheSettingsAsTheyWere(
        string first,
        string second,
        string expected
    )
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new PostgresConnection(postgres.ConnectionString);
        var live = new PostgresLive(connection);
        var prepared = await live.PrepareAsync(
            new PostgresPrepareRequest(
                Statement,
                "SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id WHERE u.name = $1",
                ["unknown"]
            ),
            token
        );
        prepared.Error.ShouldBeNull(prepared.Error?.Message);

        var answer = await live.ExplainAsync(new PostgresExplainRequest(Statement, 1, [first, second]), token);

        PlanNode.Read(answer.Plan.ShouldNotBeNull()).ShouldNotBeNull().NodeType.ShouldBe(expected);
        (await ShowAsync(connection, "plan_cache_mode")).ShouldBe("auto");
        (await ShowAsync(connection, "enable_hashjoin")).ShouldBe("on");
        (await ShowAsync(connection, "enable_mergejoin")).ShouldBe("on");
        (await ShowAsync(connection, "enable_nestloop")).ShouldBe("on");
    }

    // The error is the answer, the transaction is rolled back, and the session goes on.
    [Fact]
    public async Task ExplainAsync_StatementThatWasNotPrepared_AnswersTheErrorAndLeavesNoTransaction()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new PostgresConnection(postgres.ConnectionString);
        var live = new PostgresLive(connection);

        var answer = await live.ExplainAsync(new PostgresExplainRequest("walk_nothing", 0, []), token);

        answer.Plan.ShouldBeNull();
        answer.Error.ShouldNotBeNull().SqlState.ShouldBe("26000");

        // A SHOW in a transaction that an error had ended would be refused: the rollback was made.
        (await ShowAsync(connection, "plan_cache_mode")).ShouldBe("auto");
    }

    // A table of this test alone with one row, made by SQL that can be run again, and left: the container is
    // thrown away, and the fixtures are recorded in one of their own.
    private const string Rows = """
        CREATE TABLE IF NOT EXISTS walk_explain_rows (id integer PRIMARY KEY, label text);
        INSERT INTO walk_explain_rows VALUES (1, 'kept') ON CONFLICT (id) DO UPDATE SET label = 'kept';
        """;

    // An UPDATE is planned and not run.
    [Fact]
    public async Task ExplainAsync_Update_RunsNothing()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new PostgresConnection(postgres.ConnectionString);
        var live = new PostgresLive(connection);
        await using (var made = await connection.CommandAsync(Rows, token))
        {
            _ = await made.ExecuteNonQueryAsync(token);
        }

        var prepared = await live.PrepareAsync(
            new PostgresPrepareRequest(Statement, "UPDATE walk_explain_rows SET label = 'changed' WHERE id = 1", []),
            token
        );
        prepared.Error.ShouldBeNull(prepared.Error?.Message);

        var answer = await live.ExplainAsync(new PostgresExplainRequest(Statement, 0, []), token);

        PlanNode.Read(answer.Plan.ShouldNotBeNull()).ShouldNotBeNull().NodeType.ShouldBe("ModifyTable");
        await using var after = await connection.CommandAsync("SELECT label FROM walk_explain_rows WHERE id = 1", token);
        (await after.ExecuteScalarAsync(token)).ShouldBe("kept");
    }
}
```

Each test has a connection of its own, so the statement's name is free in each.

- [ ] **Step 16: Run them**

Run: `dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class "SqlSource.Tests.Postgres.Describer.ExplainTests"`
Expected: PASS.

Run: `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14 dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class "SqlSource.Tests.Postgres.Describer.ExplainTests"`
Expected: PASS.

- [ ] **Step 17: Format, validate and commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add src/SqlSource.Tool/Describing/Postgres tests/SqlSource.Tool.Tests/Describing/Postgres tests/SqlSource.Tests.Postgres/Describer/ExplainTests.cs
git commit -m "$(cat <<'EOF'
Ask the server for a generic plan, and read it

The call "explain" plans the prepared statement under
force_generic_plan in a transaction that is rolled back, with the
settings of a closed list that the tests force a join method by.
PlanNode reads the keys of the text that the walk uses.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: The relation instances, steps 1 and 2

**Files:**
- Create: `src/SqlSource.Tool/Describing/Postgres/PlanInstances.cs`, `tests/SqlSource.Tool.Tests/PostgresPlans.cs`
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/PlanInstancesTests.cs`

**Interfaces:**
- Consumes: `PlanNode` of Task 2.
- Produces:
  - `static class WalkReasons` with the constants `ExplainFailed = "explain-failed"`, `Unreadable = "unreadable"`, `NoOutput = "no-output"`, `UnknownJoinType = "unknown-join-type"`, `DoubledAlias = "doubled-alias"`, `GroupingSets = "grouping-sets"`
  - `sealed record VisitedNode(int Depth, PlanNode Node, string NullExtends)`, where `NullExtends` is `inner`, `outer`, `both` or `none`
  - `sealed class PlanInstances` with `static PlanInstances Find(PlanNode root)`, `string? Reason`, `IReadOnlyList<VisitedNode> Nodes`, `bool RootNamesAnInstance`, `bool TryFind(string alias, out bool nullExtended)`
  - The test helper `PostgresPlans`: `Scan(string alias, params string[] output)`, `Join(string joinType, PlanNode outer, PlanNode inner)`, `Over(string nodeType, PlanNode child, string relationship = "Outer")`, `Text(PlanNode root)`.

- [ ] **Step 1: Write the helper of the tests**

Create `tests/SqlSource.Tool.Tests/PostgresPlans.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SqlSource.Tool.Describing.Postgres;

namespace SqlSource.Tool.Tests;

// Plans as a server would answer "explain", written by a test: a scan of a table, a join of two nodes, and a node
// over one child.  Text gives the plan as EXPLAIN (VERBOSE, FORMAT JSON) writes it, for a test of the session.
internal static class PostgresPlans
{
    public const string Outer = "Outer";

    public const string Inner = "Inner";

    // A scan of the table of the alias's name in public, with the alias.
    public static PlanNode Scan(string alias, params string[] output) =>
        new("Seq Scan", null, null, output.Length == 0 ? null : output, alias, alias, "public", null, null, false, []);

    // A join whose first child is its outer side and whose second is its inner one.
    public static PlanNode Join(string joinType, PlanNode outer, PlanNode inner) =>
        new(
            "Hash Join",
            joinType,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            [outer with { ParentRelationship = Outer }, inner with { ParentRelationship = Inner }]
        );

    // A node that is no join over one child: a Sort, a Hash, a ModifyTable, a Subquery Scan.
    public static PlanNode Over(string nodeType, PlanNode child, string relationship = Outer) =>
        new(
            nodeType,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            [child with { ParentRelationship = relationship }]
        );

    public static string Text(PlanNode root) =>
        JsonSerializer.Serialize(new[] { new Dictionary<string, object?> { ["Plan"] = MapOf(root) } });

    private static Dictionary<string, object?> MapOf(PlanNode node)
    {
        var map = new Dictionary<string, object?> { ["Node Type"] = node.NodeType };
        Add(map, "Join Type", node.JoinType);
        Add(map, "Parent Relationship", node.ParentRelationship);
        Add(map, "Relation Name", node.RelationName);
        Add(map, "Schema", node.Schema);
        Add(map, "Alias", node.Alias);
        Add(map, "Subplan Name", node.SubplanName);
        Add(map, "CTE Name", node.CteName);
        Add(map, "Output", node.Output);
        if (node.HasGroupingSets)
        {
            map["Grouping Sets"] = System.Array.Empty<object>();
        }

        if (node.Plans.Count > 0)
        {
            map["Plans"] = node.Plans.Select(MapOf).ToArray();
        }

        return map;
    }

    private static void Add(Dictionary<string, object?> map, string key, object? value)
    {
        if (value is not null)
        {
            map[key] = value;
        }
    }
}
```

- [ ] **Step 2: Write the failing tests**

Create `tests/SqlSource.Tool.Tests/Describing/Postgres/PlanInstancesTests.cs`:

```csharp
using System.Linq;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;
using static SqlSource.Tool.Tests.PostgresPlans;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// Steps 1 and 2 of the walk, over plans a test writes: which nodes name a relation instance, which of them a join
// above null-extends, and what makes a plan unusable.
public class PlanInstancesTests
{
    private static bool ExtendedIn(PlanInstances instances, string alias)
    {
        instances.TryFind(alias, out var extended).ShouldBeTrue(alias);
        return extended;
    }

    // "a" is the outer side and "b" the inner one.
    [Theory]
    [InlineData("Inner", false, false)]
    [InlineData("Left", false, true)]
    [InlineData("Right", true, false)]
    [InlineData("Full", true, true)]
    [InlineData("Semi", false, false)]
    [InlineData("Anti", false, true)]
    [InlineData("Right Semi", false, false)]
    [InlineData("Right Anti", true, false)]
    public void Find_JoinOfTwoScans_ExtendsTheSidesItsTypeSays(string joinType, bool outer, bool inner)
    {
        var instances = PlanInstances.Find(Join(joinType, Scan("a"), Scan("b")));

        instances.Reason.ShouldBeNull();
        ExtendedIn(instances, "a").ShouldBe(outer);
        ExtendedIn(instances, "b").ShouldBe(inner);
    }

    // A LEFT JOIN the planner swapped is a Right join with the sides swapped: the rule gives the same answer.
    [Fact]
    public void Find_SwappedLeftJoin_ExtendsTheOuterChild()
    {
        var instances = PlanInstances.Find(Join("Right", Scan("o"), Over("Hash", Scan("u"), Inner)));

        ExtendedIn(instances, "o").ShouldBeTrue();
        ExtendedIn(instances, "u").ShouldBeFalse();
    }

    // a LEFT JOIN b LEFT JOIN c: a join on the path is enough, however far above.
    [Fact]
    public void Find_ChainOfTwoLeftJoins_ExtendsEverythingUnderAnInnerSide()
    {
        var plan = Join("Left", Join("Left", Scan("a"), Scan("b")), Scan("c"));

        var instances = PlanInstances.Find(plan);

        ExtendedIn(instances, "a").ShouldBeFalse();
        ExtendedIn(instances, "b").ShouldBeTrue();
        ExtendedIn(instances, "c").ShouldBeTrue();
    }

    // a LEFT JOIN (b JOIN c): the inner join extends nothing, and the one above it both of its sides.
    [Fact]
    public void Find_InnerJoinUnderTheInnerSideOfALeftJoin_ExtendsBothOfItsScans()
    {
        var plan = Join("Left", Scan("a"), Join("Inner", Scan("b"), Scan("c")));

        var instances = PlanInstances.Find(plan);

        ExtendedIn(instances, "a").ShouldBeFalse();
        ExtendedIn(instances, "b").ShouldBeTrue();
        ExtendedIn(instances, "c").ShouldBeTrue();
    }

    [Theory]
    [InlineData("Seq Scan")]
    [InlineData("Index Scan")]
    [InlineData("Index Only Scan")]
    [InlineData("Bitmap Heap Scan")]
    [InlineData("Tid Scan")]
    [InlineData("Tid Range Scan")]
    [InlineData("Sample Scan")]
    public void Find_ScanOfARelation_NamesAnInstance(string nodeType)
    {
        var instances = PlanInstances.Find(Scan("u") with { NodeType = nodeType });

        ExtendedIn(instances, "u").ShouldBeFalse();
        instances.RootNamesAnInstance.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Subquery Scan")]
    [InlineData("CTE Scan")]
    [InlineData("Function Scan")]
    [InlineData("Values Scan")]
    [InlineData("WorkTable Scan")]
    [InlineData("Foreign Scan")]
    [InlineData("Result")]
    public void Find_NodeThatIsNoScanOfARelation_NamesNone(string nodeType)
    {
        var instances = PlanInstances.Find(Scan("s") with { NodeType = nodeType });

        instances.TryFind("s", out _).ShouldBeFalse();
        instances.RootNamesAnInstance.ShouldBeFalse();
        instances.Reason.ShouldBeNull();
    }

    // The plan of a CTE hangs under whichever node first needs it: its scans are no instances of this plan's joins.
    [Theory]
    [InlineData("InitPlan")]
    [InlineData("SubPlan")]
    [InlineData("Member")]
    [InlineData("Subquery")]
    public void Find_ChildThatIsAnotherPlan_IsNotFollowed(string relationship)
    {
        var plan = Over("CTE Scan", Join("Bogus", Scan("u"), Scan("o")), relationship);

        var instances = PlanInstances.Find(plan);

        instances.TryFind("u", out _).ShouldBeFalse();
        instances.TryFind("o", out _).ShouldBeFalse();
        instances.Reason.ShouldBeNull();
        instances.Nodes.Count.ShouldBe(1);
    }

    [Fact]
    public void Find_JoinTypeTheWalkDoesNotKnow_IsUnusable() =>
        PlanInstances.Find(Join("Sideways", Scan("a"), Scan("b"))).Reason.ShouldBe(WalkReasons.UnknownJoinType);

    [Fact]
    public void Find_AliasFoundTwice_IsUnusable() =>
        PlanInstances.Find(Join("Inner", Scan("a"), Scan("a"))).Reason.ShouldBe(WalkReasons.DoubledAlias);

    [Fact]
    public void Find_NodeWithGroupingSets_IsUnusable()
    {
        var plan = Over("Aggregate", Scan("u")) with { HasGroupingSets = true };

        PlanInstances.Find(plan).Reason.ShouldBe(WalkReasons.GroupingSets);
    }

    [Fact]
    public void Find_MixedAggregate_IsUnusable() =>
        PlanInstances.Find(Over("MixedAggregate", Scan("u"))).Reason.ShouldBe(WalkReasons.GroupingSets);

    // The grouping sets of a subquery's own plan are found too, when the planner flattened the scan away.
    [Fact]
    public void Find_GroupingSetsUnderAJoin_IsUnusable()
    {
        var grouped = Over("Aggregate", Scan("o")) with { HasGroupingSets = true };

        PlanInstances.Find(Join("Left", Scan("u"), grouped)).Reason.ShouldBe(WalkReasons.GroupingSets);
    }

    // An UPDATE or a DELETE scans its target under the ModifyTable with the ModifyTable's own alias: one instance,
    // which no join extends, since the row returned is the one the statement acted on.
    [Fact]
    public void Find_RootModifyTableOverAScanOfItsOwnAlias_IsOneInstanceAndUsable()
    {
        var plan = Over("ModifyTable", Join("Full", Scan("t"), Scan("s"))) with { Alias = "t" };

        var instances = PlanInstances.Find(plan);

        instances.Reason.ShouldBeNull();
        instances.RootNamesAnInstance.ShouldBeTrue();
        ExtendedIn(instances, "t").ShouldBeFalse();
        ExtendedIn(instances, "s").ShouldBeTrue();
    }

    // An INSERT has a Result under it and no scan of its target.
    [Fact]
    public void Find_RootModifyTableWithoutAScanOfItsTarget_NamesItsTarget()
    {
        var plan = Over("ModifyTable", Scan("x") with { NodeType = "Result", Alias = null }) with { Alias = "orders" };

        ExtendedIn(PlanInstances.Find(plan), "orders").ShouldBeFalse();
    }

    [Fact]
    public void Find_TargetsAliasScannedTwiceUnderTheModifyTable_IsUnusable()
    {
        var plan = Over("ModifyTable", Join("Inner", Scan("t"), Scan("t"))) with { Alias = "t" };

        PlanInstances.Find(plan).Reason.ShouldBe(WalkReasons.DoubledAlias);
    }

    // Only the root's ModifyTable is a target: one further down is no scan of a relation.
    [Fact]
    public void Find_ModifyTableThatIsNotTheRoot_NamesNoInstance()
    {
        var plan = Over("Result", Over("ModifyTable", Scan("t")) with { Alias = "t" });

        var instances = PlanInstances.Find(plan);

        instances.Reason.ShouldBeNull();
        ExtendedIn(instances, "t").ShouldBeFalse();
        instances.RootNamesAnInstance.ShouldBeFalse();
    }

    // What the log says of each node, in the order of the visit.
    [Fact]
    public void Find_Plan_KeepsEachNodeItVisitedWithItsDepthAndWhatItExtends()
    {
        var plan = Over("Sort", Join("Left", Scan("u"), Join("Full", Scan("o"), Scan("i"))));

        var nodes = PlanInstances.Find(plan).Nodes;

        nodes
            .Select(static visited => $"{visited.Depth} {visited.Node.NodeType} {visited.Node.Alias} {visited.NullExtends}")
            .ShouldBe([
                "0 Sort  none",
                "1 Hash Join  inner",
                "2 Seq Scan u none",
                "2 Hash Join  both",
                "3 Seq Scan o none",
                "3 Seq Scan i none",
            ]);
    }

    [Fact]
    public void Find_RightJoin_SaysItExtendsItsOuterSide() =>
        PlanInstances.Find(Join("Right", Scan("a"), Scan("b"))).Nodes[0].NullExtends.ShouldBe("outer");
}
```

- [ ] **Step 3: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PlanInstancesTests"`
Expected: the build fails, `PlanInstances` does not exist.

- [ ] **Step 4: Write `PlanInstances`**

Create `src/SqlSource.Tool/Describing/Postgres/PlanInstances.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>Why a walk had no plan it could use, as the <c>reason</c> of <c>postgres.walk</c> says it.</summary>
internal static class WalkReasons
{
    public const string ExplainFailed = "explain-failed";
    public const string Unreadable = "unreadable";
    public const string NoOutput = "no-output";
    public const string UnknownJoinType = "unknown-join-type";
    public const string DoubledAlias = "doubled-alias";
    public const string GroupingSets = "grouping-sets";
}

/// <summary>A node the walk visited, as the log says it.</summary>
/// <param name="Depth">How far under the root, which is at zero.</param>
/// <param name="Node">The node.</param>
/// <param name="NullExtends">
/// Which side the node null-extends: <c>inner</c>, <c>outer</c>, <c>both</c>, or <c>none</c> for a node that is no
/// join or a join that extends nothing.
/// </param>
internal sealed record VisitedNode(int Depth, PlanNode Node, string NullExtends);

/// <summary>
/// The relation instances of a plan, each by its alias, and whether a join on the path from the root to it
/// null-extends its side: the first two steps of the walk.
/// </summary>
/// <remarks>
/// <para>
/// Only a child whose <c>Parent Relationship</c> is <c>Outer</c> or <c>Inner</c> is followed.  An <c>InitPlan</c>,
/// a <c>SubPlan</c>, a <c>Member</c> and a <c>Subquery</c> are other plans: a CTE's plan hangs under whichever node
/// first needs it, and its scans are no instances of this plan's joins.
/// </para>
/// <para>
/// A <c>ModifyTable</c> at the root names its target, which a <c>RETURNING</c> list refers to by that alias.  An
/// <c>UPDATE</c> and a <c>DELETE</c> scan the target under it with the same alias: that scan is the target itself
/// and no second instance, and no join above it extends the target, since the row returned is the one the
/// statement acted on.
/// </para>
/// </remarks>
internal sealed class PlanInstances
{
    private const string Outer = "Outer";
    private const string Inner = "Inner";
    private const string Both = "both";
    private const string None = "none";
    private const string ModifyTable = "ModifyTable";
    private const string MixedAggregate = "MixedAggregate";

    private static readonly HashSet<string> RelationScans = new(StringComparer.Ordinal)
    {
        "Seq Scan",
        "Index Scan",
        "Index Only Scan",
        "Bitmap Heap Scan",
        "Tid Scan",
        "Tid Range Scan",
        "Sample Scan",
    };

    private readonly Dictionary<string, bool> _extended = new(StringComparer.Ordinal);
    private readonly List<VisitedNode> _nodes = [];
    private readonly string? _target;
    private bool _targetScanSeen;

    private PlanInstances(PlanNode root)
    {
        _target = root.NodeType == ModifyTable ? root.Alias : null;
        RootNamesAnInstance = NamesAnInstance(root, isRoot: true);
        Visit(root, 0, extended: false);
    }

    /// <summary>
    /// Why the plan cannot be used, or null: a join type the walk does not know, an alias found twice, or a
    /// grouping set, which puts <c>NULL</c> into a grouping column with no join.
    /// </summary>
    public string? Reason { get; private set; }

    /// <summary>Every node that was visited, in the order of the visit.</summary>
    public IReadOnlyList<VisitedNode> Nodes => _nodes;

    /// <summary>
    /// Whether the root is itself a relation instance, which is when a bare name in its <c>Output</c> is a column
    /// of that relation.
    /// </summary>
    public bool RootNamesAnInstance { get; }

    /// <summary>The instances of a plan.</summary>
    public static PlanInstances Find(PlanNode root) => new(root);

    /// <summary>Whether the alias names an instance, and whether a join null-extends it.</summary>
    public bool TryFind(string alias, out bool nullExtended) => _extended.TryGetValue(alias, out nullExtended);

    private static bool NamesAnInstance(PlanNode node, bool isRoot) =>
        node.Alias is not null
        && (RelationScans.Contains(node.NodeType) || (isRoot && node.NodeType == ModifyTable));

    // Which side a join extends, "none" for a node that is no join, and null for a join type that is not known.
    private static string? ExtendsOf(PlanNode node) =>
        node.JoinType switch
        {
            null => None,
            "Left" or "Anti" => "inner",
            "Right" or "Right Anti" => "outer",
            "Full" => Both,
            "Inner" or "Semi" or "Right Semi" => None,
            _ => null,
        };

    private void Visit(PlanNode node, int depth, bool extended)
    {
        var extends = ExtendsOf(node);
        _nodes.Add(new VisitedNode(depth, node, extends ?? None));
        if (extends is null)
        {
            Reason ??= WalkReasons.UnknownJoinType;
        }

        if (node.HasGroupingSets || node.NodeType == MixedAggregate)
        {
            Reason ??= WalkReasons.GroupingSets;
        }

        if (node.Alias is { } alias && NamesAnInstance(node, depth == 0))
        {
            Add(alias, depth, extended);
        }

        foreach (var child in node.Plans)
        {
            if (child.ParentRelationship is Outer or Inner)
            {
                var side = child.ParentRelationship == Inner ? "inner" : "outer";
                Visit(child, depth + 1, extended || extends == Both || extends == side);
            }
        }
    }

    private void Add(string alias, int depth, bool extended)
    {
        // The first scan of the target's alias under the ModifyTable is the target, which the root already named.
        if (depth > 0 && !_targetScanSeen && string.Equals(alias, _target, StringComparison.Ordinal))
        {
            _targetScanSeen = true;
            return;
        }

        if (!_extended.TryAdd(alias, extended))
        {
            Reason ??= WalkReasons.DoubledAlias;
        }
    }
}
```

- [ ] **Step 5: Run them to see them pass**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PlanInstancesTests"`
Expected: PASS.

- [ ] **Step 6: Format, validate and commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add src/SqlSource.Tool/Describing/Postgres/PlanInstances.cs tests/SqlSource.Tool.Tests/PostgresPlans.cs tests/SqlSource.Tool.Tests/Describing/Postgres/PlanInstancesTests.cs
git commit -m "$(cat <<'EOF'
Find the relation instances of a plan and which a join null-extends

The first two steps of the walk: the nodes reached through Outer and
Inner children that scan a relation, the joins above each, and what
makes a plan unusable.  A ModifyTable at the root names its target, and
the scan of the target under it is that same instance.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: The references and the walk, steps 3 and 4

**Files:**
- Create: `src/SqlSource.Tool/Describing/Postgres/OutputReference.cs`, `src/SqlSource.Tool/Describing/Postgres/NullabilityWalk.cs`
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/OutputReferenceTests.cs`, `NullabilityWalkTests.cs`

**Interfaces:**
- Consumes: `PlanNode`; `PlanInstances.Find`, `.Reason`, `.Nodes`, `.RootNamesAnInstance`, `.TryFind`; `WalkReasons`; `VisitedNode`; `ColumnNullability.Of(ColumnOrigin? origin, bool gated) : (bool? Nullable, string Source)`; `ColumnOrigin`; `SidecarColumn`; `SidecarValues.Plan`, `SidecarValues.NullableSource`.
- Produces:
  - `sealed record OutputReference(string? Alias, string Column)` with `static OutputReference? Parse(string entry)`
  - `sealed record WalkedColumn(SidecarColumn Column, string? Output, OutputReference? Reference, bool Mapped, bool NullExtended)`
  - `sealed record WalkOutcome(string Plan, string? Reason, IReadOnlyList<VisitedNode> Nodes, IReadOnlyList<WalkedColumn> Columns)`
  - `NullabilityWalk.Run(PlanNode root, IReadOnlyList<SidecarColumn> columns, IReadOnlyList<ColumnOrigin?> origins) : WalkOutcome`
  - `NullabilityWalk.Unavailable(string reason, IReadOnlyList<SidecarColumn> columns) : WalkOutcome`

- [ ] **Step 1: Write the failing tests of a reference**

Create `tests/SqlSource.Tool.Tests/Describing/Postgres/OutputReferenceTests.cs`:

```csharp
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// An entry of a plan's Output read as a plain reference to a column, as PostgreSQL prints one: a name it need not
// quote is lower-case letters, digits and underscores, and any other is in double quotes with "" for a quote.
public class OutputReferenceTests
{
    [Theory]
    [InlineData("u.id", "u", "id")]
    [InlineData("users.id", "users", "id")]
    [InlineData("u_1.id", "u_1", "id")]
    [InlineData("\"Order\".total", "Order", "total")]
    [InlineData("o.\"Total Due\"", "o", "Total Due")]
    [InlineData("\"a\"\"b\".c", "a\"b", "c")]
    [InlineData("\"a.b\".c", "a.b", "c")]
    [InlineData("\"left\".\"right\"", "left", "right")]
    public void Parse_QualifiedName_IsTheAliasAndTheColumn(string entry, string alias, string column) =>
        OutputReference.Parse(entry).ShouldBe(new OutputReference(alias, column));

    [Theory]
    [InlineData("id", "id")]
    [InlineData("_x1", "_x1")]
    [InlineData("\"Total\"", "Total")]
    public void Parse_BareName_IsTheColumnWithNoAlias(string entry, string column) =>
        OutputReference.Parse(entry).ShouldBe(new OutputReference(null, column));

    [Theory]
    [InlineData("")]
    [InlineData("$1")]
    [InlineData("1")]
    [InlineData("NULL::numeric")]
    [InlineData("(SubPlan 1)")]
    [InlineData("COALESCE(u.id, p.id)")]
    [InlineData("count(*)")]
    [InlineData("count(*) OVER (?)")]
    [InlineData("u.id + 1")]
    [InlineData("(u.id)")]
    [InlineData("u.id::text")]
    [InlineData("u.*")]
    [InlineData("\"left\"((u.name)::text, 1)")]
    [InlineData("a.b.c")]
    [InlineData("u.")]
    [InlineData(".id")]
    [InlineData("u..id")]
    [InlineData("u.1x")]
    [InlineData("U.id")]
    [InlineData("u.Id")]
    [InlineData("\"open")]
    [InlineData("\"\".id")]
    [InlineData("'u.id'")]
    [InlineData("u.id ")]
    [InlineData(" u.id")]
    public void Parse_Expression_IsNoReference(string entry) => OutputReference.Parse(entry).ShouldBeNull();
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.OutputReferenceTests"`
Expected: the build fails, `OutputReference` does not exist.

- [ ] **Step 3: Write `OutputReference`**

Create `src/SqlSource.Tool/Describing/Postgres/OutputReference.cs`:

```csharp
using System.Text;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// An entry of a plan's <c>Output</c> that is a plain reference to a column: <c>alias.column</c>, which names the
/// relation instance, or a bare name, which a node prints for a column of its own relation.
/// </summary>
/// <remarks>
/// PostgreSQL prints a name without quotes only when it is lower-case letters, digits and underscores and does not
/// start with a digit; any other is in double quotes, with <c>""</c> for a quote.  Whatever else an entry holds, an
/// operator, a parenthesis, a cast, a space, makes it an expression, and an expression is no reference: the walk
/// knows nothing of what it can return.
/// </remarks>
/// <param name="Alias">The alias as the name is, without its quotes, or null for a bare name.</param>
/// <param name="Column">The column as the name is, without its quotes.</param>
internal sealed record OutputReference(string? Alias, string Column)
{
    /// <summary>The reference an entry is, or null for an expression.</summary>
    public static OutputReference? Parse(string entry)
    {
        var at = 0;
        if (Name(entry, ref at) is not { } first)
        {
            return null;
        }

        if (at == entry.Length)
        {
            return new OutputReference(null, first);
        }

        if (entry[at] != '.')
        {
            return null;
        }

        at++;
        return Name(entry, ref at) is { } second && at == entry.Length ? new OutputReference(first, second) : null;
    }

    // The name that starts at the index, which is moved past it, or null.
    private static string? Name(string entry, ref int at)
    {
        if (at >= entry.Length)
        {
            return null;
        }

        return entry[at] == '"' ? Quoted(entry, ref at) : Bare(entry, ref at);
    }

    private static string? Bare(string entry, ref int at)
    {
        var start = at;
        if (!IsLowerOrUnderscore(entry[at]))
        {
            return null;
        }

        while (at < entry.Length && (IsLowerOrUnderscore(entry[at]) || char.IsAsciiDigit(entry[at])))
        {
            at++;
        }

        return entry[start..at];
    }

    private static string? Quoted(string entry, ref int at)
    {
        var name = new StringBuilder();
        var index = at + 1;
        while (index < entry.Length)
        {
            if (entry[index] != '"')
            {
                _ = name.Append(entry[index]);
                index++;
            }
            else if (index + 1 < entry.Length && entry[index + 1] == '"')
            {
                _ = name.Append('"');
                index += 2;
            }
            else
            {
                at = index + 1;
                return name.Length > 0 ? name.ToString() : null;
            }
        }

        return null;
    }

    private static bool IsLowerOrUnderscore(char character) => char.IsAsciiLetterLower(character) || character == '_';
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.OutputReferenceTests"`
Expected: PASS.

- [ ] **Step 5: Write the failing tests of the walk**

Create `tests/SqlSource.Tool.Tests/Describing/Postgres/NullabilityWalkTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Postgres;
using Xunit;
using static SqlSource.Tool.Tests.PostgresPlans;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// Steps 3 and 4 of the walk, over plans a test writes: each row of the spec's table of a column, and the plan's
// value with its reason.
public class NullabilityWalkTests
{
    // A table with a NOT NULL column and a nullable one, and a view of the same two.
    private static readonly PostgresTableRow Table = PostgresTables.TableOf(
        16700,
        "t",
        PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true),
        PostgresTables.Attribute(2, "opt", PostgresRows.Int4.Oid)
    );

    private static readonly PostgresTableRow View = Table with { Oid = 16701, Name = "v", Relkind = "v" };

    private static readonly IReadOnlyDictionary<long, PostgresTableRow> Tables = PostgresTables.Of(Table, View);

    // A column of the table, of the view, or of an expression when the table is null.
    private sealed record Asked(PostgresTableRow? Of, int Attnum);

    private static readonly Asked NotNull = new(Table, 1);
    private static readonly Asked Optional = new(Table, 2);
    private static readonly Asked OfView = new(View, 1);
    private static readonly Asked Expression = new(null, 0);

    // The columns as the session builds them for a gated query, with the heuristic's answer, and their origins.
    private static (SidecarColumn[] Columns, ColumnOrigin?[] Origins) Built(params Asked[] asked)
    {
        var columns = new SidecarColumn[asked.Length];
        var origins = new ColumnOrigin?[asked.Length];
        for (var index = 0; index < asked.Length; index++)
        {
            var row = asked[index].Of is { } table
                ? PostgresTables.ColumnOf(index, $"c{index}", 23, table, asked[index].Attnum)
                : new PostgresColumnRow(index, $"c{index}", 23, 0, 0, null, null, null);
            origins[index] = ColumnOrigin.Find(row, Tables);
            columns[index] = PostgresColumns.Build(row, origins[index], PostgresRows.Of(PostgresRows.Int4), gated: true);
        }

        return (columns, origins);
    }

    private static WalkOutcome Run(PlanNode root, params Asked[] asked)
    {
        var (columns, origins) = Built(asked);
        return NullabilityWalk.Run(root, columns, origins);
    }

    private static (bool? Nullable, string? Source)[] AnswersOf(WalkOutcome outcome) =>
        [.. outcome.Columns.Select(static walked => (walked.Column.Nullable, walked.Column.NullableSource))];

    // "a" is the side a LEFT JOIN keeps and "b" the side it null-extends.
    private static PlanNode LeftJoin(params string[] output) => Join("Left", Scan("a"), Scan("b")) with { Output = output };

    [Fact]
    public void Run_ColumnsOfATableThatNothingExtends_TakeTheCatalogsAnswer()
    {
        var outcome = Run(LeftJoin("a.id", "a.opt"), NotNull, Optional);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Walked);
        outcome.Reason.ShouldBeNull();
        AnswersOf(outcome).ShouldBe([(false, "catalog"), (true, "catalog")]);
    }

    [Fact]
    public void Run_ColumnsOfATableThatAJoinExtends_AreNullableByTheOuterJoin() =>
        AnswersOf(Run(LeftJoin("b.id", "b.opt"), NotNull, Optional))
            .ShouldBe([(true, "outer-join"), (true, "outer-join")]);

    [Theory]
    [InlineData("a.id + 1")]
    [InlineData("(a.id)")]
    [InlineData("COALESCE(a.id, b.id)")]
    [InlineData("$1")]
    [InlineData("old.id")]
    [InlineData("id")]
    public void Run_ColumnOfATableWhoseEntryCannotBeMapped_KeepsTheHeuristic(string entry) =>
        AnswersOf(Run(LeftJoin(entry, entry), NotNull, Optional))
            .ShouldBe([(true, "heuristic"), (true, "heuristic")]);

    [Fact]
    public void Run_ColumnOfAView_SaysViewUnlessAJoinExtendsIt() =>
        AnswersOf(Run(LeftJoin("a.id", "b.id", "a.id + 1"), OfView, OfView, OfView))
            .ShouldBe([(null, "view"), (true, "outer-join"), (null, "view")]);

    // Nothing of the plan can make it non-nullable, so it is not looked up, whatever its entry is.
    [Fact]
    public void Run_ColumnWithoutAnOrigin_IsUnknownAndIsNotLookedUp()
    {
        var outcome = Run(LeftJoin("b.id", "count(*)"), Expression, Expression);

        AnswersOf(outcome).ShouldBe([(null, "no-origin"), (null, "no-origin")]);
        outcome.Columns.ShouldAllBe(static walked => !walked.Mapped && walked.Reference == null);
        outcome.Columns[0].Output.ShouldBe("b.id");
    }

    // The rule of a NOT NULL is written once, in ColumnNullability: one that is NOT VALID is nullable here too.
    [Fact]
    public void Run_NotNullThatIsNotValidated_IsNullableByTheCatalog()
    {
        var table = PostgresTables.TableOf(
            16702,
            "n",
            PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true, notNullValidated: false)
        );
        var row = PostgresTables.ColumnOf(0, "id", 23, table, 1);
        var origin = ColumnOrigin.Find(row, PostgresTables.Of(table));
        var column = PostgresColumns.Build(row, origin, PostgresRows.Of(PostgresRows.Int4), gated: true);

        var outcome = NullabilityWalk.Run(Scan("n", "id"), [column], [origin]);

        AnswersOf(outcome).ShouldBe([(true, "catalog")]);
    }

    [Theory]
    [InlineData("Subquery Scan")]
    [InlineData("CTE Scan")]
    [InlineData("Function Scan")]
    [InlineData("Values Scan")]
    [InlineData("WorkTable Scan")]
    public void Run_AliasOfANodeThatIsNoScanOfARelation_IsUnmappable(string nodeType)
    {
        var plan = Join("Inner", Scan("a"), Scan("s") with { NodeType = nodeType }) with { Output = ["s.id"] };

        var outcome = Run(plan, NotNull);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(outcome).ShouldBe([(true, "heuristic")]);
    }

    // A CTE's own plan is another plan: its alias is reached from here through the CTE Scan alone.
    [Fact]
    public void Run_AliasUnderAnInitPlan_IsNotFound()
    {
        var plan = Over("CTE Scan", Scan("u"), "InitPlan") with { Alias = "s", Output = ["u.id"] };

        AnswersOf(Run(plan, NotNull)).ShouldBe([(true, "heuristic")]);
    }

    // A single-relation query prints bare names.
    [Fact]
    public void Run_BareNameAtARootThatScansARelation_IsThatRelationsColumn() =>
        AnswersOf(Run(Scan("users", "id", "opt"), NotNull, Optional)).ShouldBe([(false, "catalog"), (true, "catalog")]);

    // So does a Result that replaced a scan the planner proved empty, beside a scan that remains: there the name
    // says nothing of which relation.
    [Fact]
    public void Run_BareNameUnderAJoin_IsUnmappable() =>
        AnswersOf(Run(LeftJoin("a.id", "opt"), NotNull, Optional)).ShouldBe([(false, "catalog"), (true, "heuristic")]);

    // A DELETE of one table prints bare names, and its ModifyTable is its target.
    [Fact]
    public void Run_BareNameAtARootModifyTable_IsTheTargetsColumn()
    {
        var plan = Over("ModifyTable", Scan("orders")) with { Alias = "orders", Output = ["id", "opt"] };

        AnswersOf(Run(plan, NotNull, Optional)).ShouldBe([(false, "catalog"), (true, "catalog")]);
    }

    // A MERGE that inserts what the target lacks and deletes what the source lacks: the row returned is the
    // target's own, and the source's columns are NULL for a row that was deleted.
    [Fact]
    public void Run_TargetUnderAFullJoin_IsNotExtendedAndTheSourceIs()
    {
        var plan = Over("ModifyTable", Join("Full", Scan("t"), Scan("s"))) with { Alias = "t", Output = ["t.id", "s.id"] };

        AnswersOf(Run(plan, NotNull, NotNull)).ShouldBe([(false, "catalog"), (true, "outer-join")]);
    }

    [Fact]
    public void Run_QuotedAlias_NamesTheInstanceOfThatName()
    {
        var plan = Join("Left", Scan("u"), Scan("Order")) with { Output = ["u.id", "\"Order\".id"] };

        AnswersOf(Run(plan, NotNull, NotNull)).ShouldBe([(false, "catalog"), (true, "outer-join")]);
    }

    // PostgreSQL prints the target list first and what a sort or a group added after it.
    [Fact]
    public void Run_OutputWithMoreEntriesThanColumns_UsesTheFirst()
    {
        var plan = Over("Sort", LeftJoin()) with { Output = ["a.id", "b.id", "b.opt"] };

        var outcome = Run(plan, NotNull, NotNull);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(outcome).ShouldBe([(false, "catalog"), (true, "outer-join")]);
    }

    public static TheoryData<string, string> Unusable =>
        new()
        {
            { "no-output", Text(Over("Append", Scan("a"), "Member")) },
            { "no-output", Text(LeftJoin("a.id")) },
            { "unknown-join-type", Text(Join("Sideways", Scan("a"), Scan("b")) with { Output = ["a.id", "a.opt"] }) },
            { "doubled-alias", Text(Join("Inner", Scan("a"), Scan("a")) with { Output = ["a.id", "a.opt"] }) },
            {
                "grouping-sets",
                Text(Over("Aggregate", Scan("a")) with { HasGroupingSets = true, Output = ["a.id", "a.opt"] })
            },
            { "grouping-sets", Text(Over("MixedAggregate", Scan("a")) with { Output = ["a.id", "a.opt"] }) },
        };

    // The first is a UNION's Append, which has no Output; the second has fewer entries than the result has columns.
    [Theory]
    [MemberData(nameof(Unusable))]
    public void Run_PlanThatCannotBeUsed_IsUnavailableWithItsReasonAndEveryColumnKeepsTheHeuristic(
        string reason,
        string plan
    )
    {
        var outcome = Run(PlanNode.Read(plan).ShouldNotBeNull(), NotNull, OfView);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Unavailable);
        outcome.Reason.ShouldBe(reason);
        AnswersOf(outcome).ShouldBe([(true, "heuristic"), (null, "view")]);
        outcome.Nodes.ShouldNotBeEmpty();
        outcome.Columns.ShouldAllBe(static walked => !walked.Mapped && !walked.NullExtended);
    }

    [Theory]
    [InlineData("explain-failed")]
    [InlineData("unreadable")]
    public void Unavailable_ReasonWithoutAPlan_LeavesEveryColumnAsItWasBuilt(string reason)
    {
        var (columns, _) = Built(NotNull, OfView, Expression);

        var outcome = NullabilityWalk.Unavailable(reason, columns);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Unavailable);
        outcome.Reason.ShouldBe(reason);
        outcome.Nodes.ShouldBeEmpty();
        outcome.Columns.Select(static walked => walked.Column).ShouldBe(columns);
        outcome.Columns.ShouldAllBe(static walked => walked.Output == null && !walked.Mapped);
        AnswersOf(outcome).ShouldBe([(true, "heuristic"), (null, "view"), (null, "no-origin")]);
    }

    // What the log says of a column.
    [Fact]
    public void Run_Column_KeepsItsEntryItsReferenceAndWhatWasFound()
    {
        var outcome = Run(LeftJoin("a.id", "b.id", "a.id + 1"), NotNull, NotNull, NotNull);

        outcome
            .Columns.Select(static walked => (walked.Output, walked.Reference, walked.Mapped, walked.NullExtended))
            .ShouldBe([
                ("a.id", new OutputReference("a", "id"), true, false),
                ("b.id", new OutputReference("b", "id"), true, true),
                ("a.id + 1", null, false, false),
            ]);
        outcome.Nodes.Select(static visited => visited.Node.NodeType).ShouldBe(["Hash Join", "Seq Scan", "Seq Scan"]);
    }

    // Nothing but the two members of a column changes.
    [Fact]
    public void Run_Column_ChangesItsNullabilityAndNothingElse()
    {
        var (columns, origins) = Built(NotNull);

        var walked = NullabilityWalk.Run(LeftJoin("a.id"), columns, origins).Columns.ShouldHaveSingleItem().Column;

        walked.ShouldBe(columns[0] with { Nullable = false, NullableSource = "catalog" });
    }
}
```

- [ ] **Step 6: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.NullabilityWalkTests"`
Expected: the build fails, `NullabilityWalk` does not exist.

- [ ] **Step 7: Write `NullabilityWalk`**

Create `src/SqlSource.Tool/Describing/Postgres/NullabilityWalk.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using SqlSource.Snapshot;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>A column after the walk, with what the walk read of it, as the log says it.</summary>
/// <param name="Column">The column as it will be recorded.</param>
/// <param name="Output">The column's entry of the root's <c>Output</c>, or null when there was none to read.</param>
/// <param name="Reference">The entry as a reference, or null for an expression and for a column not looked up.</param>
/// <param name="Mapped">Whether the reference named a relation instance of the plan.</param>
/// <param name="NullExtended">Whether a join null-extends that instance.</param>
internal sealed record WalkedColumn(
    SidecarColumn Column,
    string? Output,
    OutputReference? Reference,
    bool Mapped,
    bool NullExtended
);

/// <summary>What a walk gave: the plan's value, each column, and what the log says of how.</summary>
/// <param name="Plan"><c>walked</c>, or <c>unavailable</c> when the walk had no plan it could use.</param>
/// <param name="Reason">Why it is <c>unavailable</c>, one of <see cref="WalkReasons" />, or null.</param>
/// <param name="Nodes">The nodes that were visited, none when there was no plan to read.</param>
/// <param name="Columns">Every column of the result, in order.</param>
internal sealed record WalkOutcome(
    string Plan,
    string? Reason,
    IReadOnlyList<VisitedNode> Nodes,
    IReadOnlyList<WalkedColumn> Columns
);

/// <summary>
/// The third layer of nullability: a column of a gated query is not nullable when its origin is <c>NOT NULL</c>
/// and the plan proves that no outer join null-extends its relation instance.
/// </summary>
/// <remarks>
/// <para>
/// The root's <c>Output</c> is the result's target list in order, and a plain reference in it prints as
/// <c>alias.column</c>, which names the relation instance and so tells the two sides of a self-join apart.  The
/// planner's join strength reduction comes for free: a <c>LEFT JOIN</c> that a <c>WHERE</c> makes inner is an
/// <c>Inner</c> join in the plan.
/// </para>
/// <para>
/// Whatever the walk does not understand keeps the heuristic and is never a wrong <c>false</c>.  An entry that is
/// no plain reference, or that names what is no relation instance, a <c>Subquery Scan</c> or a <c>CTE Scan</c>,
/// degrades its column alone; a plan that cannot be used degrades every column.  A reference through a
/// <c>Subquery Scan</c> is not matched to the subquery's own plan by name: a column renamed to another's name
/// would be read as the other.
/// </para>
/// </remarks>
internal static class NullabilityWalk
{
    /// <summary>Walks a plan that was read.</summary>
    /// <param name="root">The root of the plan.</param>
    /// <param name="columns">The columns as they were built for a gated query, with the heuristic's answer.</param>
    /// <param name="origins">Where each column comes from, or null, in the order of the columns.</param>
    public static WalkOutcome Run(
        PlanNode root,
        IReadOnlyList<SidecarColumn> columns,
        IReadOnlyList<ColumnOrigin?> origins
    )
    {
        var instances = PlanInstances.Find(root);

        // PostgreSQL prints the target list first and what a sort or a group added after it, so the entry of a
        // column is the one at its ordinal.  A UNION's Append has no Output at all.
        if (root.Output is not { } entries || entries.Count < columns.Count)
        {
            return Unusable(WalkReasons.NoOutput, root, instances, columns);
        }

        if (instances.Reason is { } reason)
        {
            return Unusable(reason, root, instances, columns);
        }

        var walked = new List<WalkedColumn>(columns.Count);
        for (var index = 0; index < columns.Count; index++)
        {
            walked.Add(ColumnOf(columns[index], origins[index], entries[index], instances));
        }

        return new WalkOutcome(SidecarValues.Plan.Walked, null, instances.Nodes, walked);
    }

    /// <summary>
    /// The outcome of a walk that had no plan to read: the server gave none, or its text is no plan.  Every column
    /// keeps what it was built with.
    /// </summary>
    /// <param name="reason">One of <see cref="WalkReasons" />.</param>
    /// <param name="columns">The columns as they were built.</param>
    public static WalkOutcome Unavailable(string reason, IReadOnlyList<SidecarColumn> columns) =>
        new(SidecarValues.Plan.Unavailable, reason, [], [.. columns.Select(static column => Kept(column, null))]);

    // A plan that was read and cannot be used: every column keeps what it was built with, and the log is given
    // the nodes and the entries all the same.
    private static WalkOutcome Unusable(
        string reason,
        PlanNode root,
        PlanInstances instances,
        IReadOnlyList<SidecarColumn> columns
    ) =>
        new(
            SidecarValues.Plan.Unavailable,
            reason,
            instances.Nodes,
            [.. columns.Select((column, index) => Kept(column, EntryAt(root, index)))]
        );

    private static WalkedColumn Kept(SidecarColumn column, string? entry) => new(column, entry, null, false, false);

    private static string? EntryAt(PlanNode root, int index) =>
        root.Output is { } output && index < output.Count ? output[index] : null;

    private static WalkedColumn ColumnOf(SidecarColumn column, ColumnOrigin? origin, string entry, PlanInstances instances)
    {
        // Nothing of the plan can make a column without an origin non-nullable: it is not looked up.
        if (origin is null)
        {
            return Kept(column, entry);
        }

        var reference = OutputReference.Parse(entry);
        var extended = false;
        var mapped = reference switch
        {
            null => false,
            { Alias: { } alias } => instances.TryFind(alias, out extended),

            // A bare name is a column of the root's own relation, which has no join above it.
            _ => instances.RootNamesAnInstance,
        };

        (bool? Nullable, string Source) answer = (mapped, extended) switch
        {
            (true, true) => (true, SidecarValues.NullableSource.OuterJoin),

            // The catalog's answer: NOT NULL for a table, and unknown with "view" for a view.
            (true, false) => ColumnNullability.Of(origin, gated: false),
            _ => ColumnNullability.Of(origin, gated: true),
        };

        return new WalkedColumn(
            column with
            {
                Nullable = answer.Nullable,
                NullableSource = answer.Source,
            },
            entry,
            reference,
            mapped,
            mapped && extended
        );
    }
}
```

The reason of a plan with no usable `Output` is `no-output` whatever else is wrong with it; otherwise it is what `PlanInstances` found.

- [ ] **Step 8: Run them to see them pass**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.NullabilityWalkTests"`
Expected: PASS.

- [ ] **Step 9: Format, validate and commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add src/SqlSource.Tool/Describing/Postgres/OutputReference.cs src/SqlSource.Tool/Describing/Postgres/NullabilityWalk.cs tests/SqlSource.Tool.Tests/Describing/Postgres/OutputReferenceTests.cs tests/SqlSource.Tool.Tests/Describing/Postgres/NullabilityWalkTests.cs
git commit -m "$(cat <<'EOF'
Walk a plan to each column's nullability

An entry of the root's Output that is a plain reference names a
relation instance: its column takes the catalog's answer when no join
extends the instance and is nullable by the outer join when one does.
An entry that cannot be mapped keeps the heuristic for its column
alone, and a plan that cannot be used for every column.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: The session asks, walks, and matches after the walk

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`, `ColumnNullability.cs:9-14`
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs`, `PostgresEventsTests.cs`, `tests/SqlSource.Tool.Tests/Fixtures/DescriberFixtureTests.cs`
- Modify: `tests/SqlSource.Tests.Postgres/Describer/ColumnTests.cs:414-530`
- Create: `tests/SqlSource.Tests.Postgres/Describer/WalkTests.cs`
- Record: `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/select-left-join/` and `match-left-join-nullability/`

**Interfaces:**
- Consumes: `PostgresExplainRequest`, `PostgresExplainAnswer`, `PostgresLive.ExplainAsync`, `PlannerSettings.None`, `PlanNode.Read`, `NullabilityWalk.Run`, `NullabilityWalk.Unavailable`, `WalkOutcome`, `WalkReasons`, `PostgresPlans`.
- Produces: `PostgresSession.DescribeAsync` asks `explain` for a gated query with rows, after `check`, and gives `plan` `walked` or `unavailable`.  The private `WalkAsync(DescribeRequest, string statement, IReadOnlyList<SidecarColumn>, ColumnOrigin?[], CancellationToken) : Task<WalkOutcome>`, which Task 6 gives the settings and Task 7 the events.

- [ ] **Step 1: Write the failing tests of the session**

In `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs`:

Add `using static SqlSource.Tool.Tests.PostgresPlans;` after the usings.

Add beside `Checks`:

```csharp
    // "explain" for the statement, with no setting, answering what the test gives.
    private static PostgresScript Explains(
        PostgresScript script,
        int number,
        int parameters,
        PostgresExplainAnswer answer
    ) => script.Answer("explain", new PostgresExplainRequest($"sqlsource_{number}", parameters, []), answer);

    private static PostgresExplainAnswer PlanOf(PlanNode root) => new(Text(root), null);
```

Give `DescribeUsersIdAsync` and `DescribeLabelsAsync` a last parameter `PostgresExplainAnswer? explain = null`, and in each, between the `Checks` line and the `Frees` line:

```csharp
        if (explain is not null)
        {
            _ = Explains(script, 1, 0, explain);
        }
```

Replace `DescribeAsync_GatedQuery_MakesEveryOriginColumnNullableAndSaysThePlanIsUnavailable` and its comment with:

```csharp
    private const string GatedUsersId =
        "SELECT u.id, count(*) OVER () AS n FROM users u LEFT JOIN orders o ON o.user_id = u.id";

    private static (bool? Nullable, string? Source)[] AnswersOf(QueryDescription description) =>
        [.. description.Columns.ShouldNotBeNull().Select(static column => (column.Nullable, column.NullableSource))];

    // users is the side the join keeps: its NOT NULL column stays so, by the catalog.
    [Fact]
    public async Task DescribeAsync_GatedQueryWhoseJoinKeepsTheColumnsSide_TakesTheCatalogsAnswerAndIsWalked()
    {
        var plan = Join("Left", Scan("u"), Scan("o")) with { Output = ["u.id", "count(*) OVER (?)"] };

        var description = await DescribeUsersIdAsync(GatedUsersId, PlanOf(plan));

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(description).ShouldBe([(false, "catalog"), (null, "no-origin")]);
    }

    // The same query planned with users as the side the join null-extends.
    [Fact]
    public async Task DescribeAsync_GatedQueryWhoseJoinExtendsTheColumnsSide_IsNullableByTheOuterJoin()
    {
        var plan = Join("Right", Scan("u"), Scan("o")) with { Output = ["u.id", "count(*) OVER (?)"] };

        var description = await DescribeUsersIdAsync(GatedUsersId, PlanOf(plan));

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(description).ShouldBe([(true, "outer-join"), (null, "no-origin")]);
    }

    public static TheoryData<string?, string?> PlansThatCannotBeUsed =>
        new()
        {
            // The server gave no plan: the error is logged and never reported.
            { null, "canceling statement due to statement timeout" },
            // The text is no plan.
            { "not json", null },
            { "[]", null },
            // An answer that holds neither.
            { null, null },
            // A plan that is read and cannot be used.
            { Text(Join("Sideways", Scan("u"), Scan("o")) with { Output = ["u.id", "n"] }), null },
        };

    [Theory]
    [MemberData(nameof(PlansThatCannotBeUsed))]
    public async Task DescribeAsync_GatedQueryWithoutAPlanTheWalkCanUse_IsDescribedByTheHeuristicAndUnavailable(
        string? plan,
        string? error
    )
    {
        var answer = new PostgresExplainAnswer(
            plan,
            error is null ? null : new PostgresServerError("57014", error, null, null, null)
        );

        var description = await DescribeUsersIdAsync(GatedUsersId, answer);

        description.Plan.ShouldBe(SidecarValues.Plan.Unavailable);
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.NoOrigin);
        AnswersOf(description).ShouldBe([(true, "heuristic"), (null, "no-origin")]);
    }

    // A check that fails ends the describe before the plan is asked for: the script holds no "explain".
    [Fact]
    public async Task DescribeAsync_GatedQueryWhoseCheckDiffers_IsTheFailureAndAsksNoPlan()
    {
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = PreparesOnly(script, 1, GatedUsersId, [], new([], null));
        _ = Columns(script, 1, 0, PostgresTables.ColumnOf(0, "id", 23, users, 1), Expression(1, "n", 20));
        _ = Tables(script, [users.Oid], users);
        _ = Resolves(script, [20, 23], PostgresRows.Int8, PostgresRows.Int4);
        _ = Checks(script, GatedUsersId, [], Expression(0, "id", 23));
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Request(GatedUsersId), TestContext.Current.CancellationToken);

        described.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC231");
        exchange.Unused.ShouldBeEmpty();
    }

    // The plan is asked with a NULL for each parameter of the query's list, as "columns" is.
    [Fact]
    public async Task DescribeAsync_GatedQueryWithAParameter_AsksThePlanWithTheNumberOfItsParameters()
    {
        var sql = "SELECT u.id FROM users u LEFT JOIN orders o ON o.user_id = u.id WHERE u.id = @id";
        var users = PostgresTables.Users;
        var id = PostgresTables.ColumnOf(0, "id", 23, users, 1);
        var script = new PostgresScript().Open();
        _ = PreparesOnly(
            script,
            1,
            "SELECT u.id FROM users u LEFT JOIN orders o ON o.user_id = u.id WHERE u.id = $1",
            ["unknown"],
            new([23], null)
        );
        _ = Columns(script, 1, 1, id);
        _ = Tables(script, [users.Oid], users);
        _ = Resolves(script, [23], PostgresRows.Int4);
        _ = Checks(script, sql, [("id", "integer")], Expression(0, "id", 23));
        _ = Explains(script, 1, 1, PlanOf(Join("Left", Scan("u"), Scan("o")) with { Output = ["u.id"] }));
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request(sql, Inferred("id")),
            TestContext.Current.CancellationToken
        );

        exchange.Unused.ShouldBeEmpty();
        AnswersOf(described.Value.ShouldNotBeNull()).ShouldBe([(false, "catalog")]);
    }
```

Change the comment of `DescribeAsync_GatedStatementWithoutRows_HasNoPlan` to `// A statement without rows has no provenance and asks for no plan, gate word or not.`

Replace `DescribeAsync_GatedQueryOverATablesList_MatchesNothingAndSaysNullabilityDiffers` and its comment with:

```csharp
    // The match is made after the walk and reads the nullability it set: labels is the side the join extends, so
    // its NOT NULL column is nullable and the result is not the table's.
    [Fact]
    public async Task DescribeAsync_GatedQueryThatExtendsATablesList_MatchesNothingAndSaysNullabilityDiffers()
    {
        var plan = Join("Left", Scan("o"), Scan("l")) with { Output = ["l.id", "l.label"] };

        var description = await DescribeLabelsAsync(
            "SELECT l.* FROM orders o LEFT JOIN labels l ON l.id = o.id",
            explain: PlanOf(plan)
        );

        description.MatchesTable.ShouldBeNull();
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.NullabilityDiffers);
        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
    }

    // The other way round, labels keeps every row as it is, and the result is the table's.
    [Fact]
    public async Task DescribeAsync_GatedQueryThatKeepsATablesList_MatchesTheTable()
    {
        var plan = Join("Left", Scan("l"), Scan("o")) with { Output = ["l.id", "l.label"] };

        var description = await DescribeLabelsAsync(
            "SELECT l.* FROM labels l LEFT JOIN orders o ON o.id = l.id",
            explain: PlanOf(plan)
        );

        description.MatchesTable.ShouldBe(new SidecarTable("public", "labels"));
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.Matched);
        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
    }

    // Without a plan the heuristic stands, and the match reads that.
    [Fact]
    public async Task DescribeAsync_GatedQueryOverATablesListWithoutAPlan_SaysNullabilityDiffers()
    {
        var description = await DescribeLabelsAsync(
            "SELECT l.* FROM labels l LEFT JOIN orders o ON o.id = l.id",
            explain: new PostgresExplainAnswer(null, new PostgresServerError(null, "lost", null, null, null))
        );

        description.TableMatch.ShouldBe(SidecarValues.TableMatch.NullabilityDiffers);
        description.Plan.ShouldBe(SidecarValues.Plan.Unavailable);
    }
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PostgresSessionTests"`
Expected: the new tests fail with a `CaptureMissException` unused call or `unavailable` where `walked` is expected: the session asks no `explain`.

- [ ] **Step 3: Change the session**

In `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`:

Replace the comment above `DescribePreparedAsync` with:

```csharp
    // What is asked of a statement that was prepared, in this order: its columns, the tables they name, and every
    // type that the parameters, the columns and their origins have, once; Npgsql's own reading of the SQL; and, for
    // a gated query with rows, its generic plan.  The table match is made last and asks nothing: it reads the
    // nullability the walk set.
```

Replace the end of `DescribePreparedAsync`, from the comment `// A statement without rows has nothing a join could null-extend.` to the method's closing brace, with:

```csharp
        // A statement without rows has nothing a join could null-extend.
        var gated = columns.Count > 0 && OuterJoinGate.IsGated(request.Sql);
        var built = BuildColumns(request, columns, origins, rows.Value, gated);
        if (!built.Succeeded)
        {
            return Failed(built.Failure);
        }

        if (await CheckAsync(request, parameters.Value, columns, rows.Value, cancellationToken) is { } differs)
        {
            return Failed(differs);
        }

        // After the check, and for a gated query alone: one that is not gated pays nothing.
        var walked = gated ? await WalkAsync(request, statement, built.Value, origins, cancellationToken) : null;
        IReadOnlyList<SidecarColumn> recorded = walked is null
            ? built.Value
            : [.. walked.Columns.Select(static column => column.Column)];

        // After everything that sets a column's nullability: the last check of the match reads it.
        var match = MatchOf(request, recorded, origins);
        return Described(parameters.Value, recorded, walked?.Plan ?? SidecarValues.Plan.NotNeeded, match);
    }

    // Asks for the generic plan of the statement and walks it.  A plan the server will not give is no fault of the
    // query: the error is logged and never reported, and the columns keep the heuristic, as they do when the text
    // is no plan.
    private async Task<WalkOutcome> WalkAsync(
        DescribeRequest request,
        string statement,
        IReadOnlyList<SidecarColumn> columns,
        ColumnOrigin?[] origins,
        CancellationToken cancellationToken
    )
    {
        var asked = new PostgresExplainRequest(statement, request.Parameters.Count, PlannerSettings.None);
        var answer = await _exchange.AskAsync("explain", asked, _live.ExplainAsync, cancellationToken);
        if (answer.Error is not null)
        {
            return NullabilityWalk.Unavailable(WalkReasons.ExplainFailed, columns);
        }

        return answer.Plan is { } text && PlanNode.Read(text) is { } root
            ? NullabilityWalk.Run(root, columns, origins)
            : NullabilityWalk.Unavailable(WalkReasons.Unreadable, columns);
    }
```

Replace `Described` and its comment with:

```csharp
    // A statement without rows has its parameters and nothing else: no columns, no table and no provenance.  One
    // with rows says what became of its plan, "not-needed" when it was not gated, and which table its result is,
    // or the first check of the match that failed.
    private static Outcome<QueryDescription, DescribeFailure> Described(
        IReadOnlyList<DescribedParameter> parameters,
        IReadOnlyList<SidecarColumn> columns,
        string plan,
        (SidecarTable? Table, string Result)? match
    )
    {
        var typed = new EquatableArray<DescribedParameter>([.. parameters]);
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

In the class remarks of `PostgresSession`, after the sentence that ends `which gives its columns.`, add: `For a query that the outer-join gate lets in, an <c>EXPLAIN</c> of that <c>EXECUTE</c> under <c>force_generic_plan</c> gives the plan that <see cref="NullabilityWalk" /> walks.`

In `src/SqlSource.Tool/Describing/Postgres/ColumnNullability.cs`, replace the remark's sentence `Until the walk exists, a gated query makes every column of a table nullable.` with `A gated query makes every column of a table nullable here, and <see cref="NullabilityWalk" /> then answers for each column whose relation instance the plan names.`

- [ ] **Step 4: Run the session's tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PostgresSessionTests"`
Expected: PASS.

- [ ] **Step 5: Bring the tests of the events to the new order**

The match is now made after `check`.  In `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresEventsTests.cs`:

- `Describe_QueryWithColumns_WritesColumnsTablesAColumnForEachAndCheck`: the list of names becomes `"prepare", "columns", "tables", "types", "column", "column", "check", "table-match", "deallocate"`.
- `Describe_QueryThatIsATable_WritesTheTableInTableMatch`: the list becomes `["prepare", "columns", "tables", "types", "column", "check", "table-match", "deallocate"]`, and `log.Events[5]` becomes `log.Events[6]`.
- `Describe_CheckThatDiffers_StillWritesTheTableMatchBeforeIt` is renamed `Describe_CheckThatDiffers_WritesNoTableMatch`, its comment becomes `// A check that fails ends the describe before the match is made.`, and its list becomes `"prepare", "columns", "tables", "types", "column", "column", "check", "deallocate"`.

In `src/SqlSource.Tool/Describing/Postgres/PostgresEvents.cs`, the class remarks' last sentence becomes:

```csharp
/// <c>open</c> has no event of its own: the run's <c>database.open</c> records what the session knows of its
/// server.  A describe writes <c>prepare</c>, <c>columns</c>, <c>tables</c>, <c>types</c>, one <c>column</c> for
/// each column, <c>check</c>, <c>table-match</c> and <c>deallocate</c>, in that order, each only when its call was
/// made, and <c>table-match</c> only for a statement with rows whose check agreed.
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PostgresEventsTests"`
Expected: PASS.

- [ ] **Step 6: Bring the container tests of the gate to the walk**

In `tests/SqlSource.Tests.Postgres/Describer/ColumnTests.cs`, each test below keeps its SQL.  What it expects changes, and its name with it:

`Describe_LeftJoin_IsGatedAndEveryOriginColumnIsNullableByTheHeuristic` becomes `Describe_LeftJoin_IsWalkedAndOnlyTheExtendedSideIsNullableByTheJoin`, with:

```csharp
        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
```

```csharp
        NullabilityOf(columns)
            .ShouldBe([(false, "catalog"), (true, "catalog"), (true, "outer-join"), (null, "no-origin")]);
```

`Describe_GroupingSetsOverANotNullColumn_IsGated` is unchanged: a plan with a grouping set is `unavailable`, and its test now proves the walk refuses it.  Its comment gains the sentence `The walk refuses a plan with a grouping set, so the heuristic stands.`

`Describe_ReturningOldAndNew_IsGated` becomes `Describe_ReturningOldAndNew_KeepsTheHeuristic`, its comment's last clause `with no outer join in the plan` becomes `and the plan prints them as old.id and new.id, which name no relation instance`, and:

```csharp
        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
```

Its `NullabilityOf(columns)` line is unchanged: `[(true, "heuristic"), (true, "heuristic")]`.

`Describe_MergeReturning_IsGated` becomes `Describe_MergeReturning_ExtendsTheSourceAndNotTheTarget`, its comment gains `The MERGE plans as a join that extends the source, and the row returned is the target's own.`, and everything after the call of `ShouldDescribeAsync` becomes:

```csharp
        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        var columns = description.Columns.ShouldNotBeNull();
        columns[0].Origin.ShouldBe(Origin("users", "id"));
        NullabilityOf(columns).ShouldBe([(true, "outer-join"), (false, "catalog")]);
```

`Describe_FunctionNamedAsAGateWord_IsGated` becomes `Describe_FunctionNamedAsAGateWord_IsWalkedAndFindsNothing`, with:

```csharp
        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        NullabilityOf(description.Columns.ShouldNotBeNull()).ShouldBe([(false, "catalog"), (null, "no-origin")]);
```

In `Describe_ColumnsOfAViewAndOfAMaterializedView_SayView`, the comment's `A view's column says "view" in a gated query too.` becomes `In a gated query a view's column says "view" where no join extends it.`, and the last line becomes:

```csharp
        NullabilityOf(gated).ShouldBe([(null, "view"), (true, "outer-join")]);
```

- [ ] **Step 7: Write the container tests of the walk**

Create `tests/SqlSource.Tests.Postgres/Describer/WalkTests.cs`:

```csharp
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// The nullability walk against a real server: what only a planner can show, and what a describe leaves behind.
// The matrix of join shapes is the fixture cases named walk-*, which FixtureCasesTests describes here.
[Collection(UsesPostgres.Name)]
public sealed class WalkTests(PostgresContainer postgres)
{
    // A partitioned table of these tests alone, made by SQL that can be run again, and left: the container is
    // thrown away, and the fixtures are recorded in one of their own.
    private const string Partitioned = """
        CREATE TABLE IF NOT EXISTS walk_parts (id integer NOT NULL, kind integer NOT NULL) PARTITION BY LIST (kind);
        CREATE TABLE IF NOT EXISTS walk_parts_1 PARTITION OF walk_parts FOR VALUES IN (1);
        CREATE TABLE IF NOT EXISTS walk_parts_2 PARTITION OF walk_parts FOR VALUES IN (2);
        """;

    private async Task<QueryDescription> ShouldDescribeAsync(string sql)
    {
        var described = await Sessions.DescribeAsync(postgres.ConnectionString, Requests.Of(sql));

        described.Failure.ShouldBeNull(described.Failure?.Descriptor.Id);
        return described.Value.ShouldNotBeNull();
    }

    private async Task<(bool? Nullable, string? Source)[]> WalkedAsync(string sql)
    {
        var description = await ShouldDescribeAsync(sql);

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        return
        [
            .. description.Columns.ShouldNotBeNull().Select(static column => (column.Nullable, column.NullableSource)),
        ];
    }

    // One value from the server, on a connection of the test's own.
    private async Task<object?> ScalarAsync(string sql)
    {
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var command = source.CreateCommand(sql);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }

    private async Task<int> ServerMajorAsync() =>
        int.Parse((string)(await ScalarAsync("SHOW server_version_num"))!, CultureInfo.InvariantCulture) / 10000;

    // The query of the spec's Goal, and its table.
    [Fact]
    public async Task Describe_LeftJoin_KeepsTheOuterSideNotNullAndExtendsTheInnerOne() =>
        (
            await WalkedAsync(
                "SELECT u.id, u.name, o.id AS order_id, o.total "
                    + "FROM users u LEFT JOIN orders o ON o.user_id = u.id WHERE u.id = @id"
            )
        ).ShouldBe([(false, "catalog"), (false, "catalog"), (true, "outer-join"), (true, "outer-join")]);

    // With a condition on the nullable side the planner makes the join inner, which the text does not show.
    [Fact]
    public async Task Describe_LeftJoinThatAWhereMakesInner_IsNotNullThroughout() =>
        (
            await WalkedAsync(
                "SELECT u.id, u.name, o.id AS order_id, o.total "
                    + "FROM users u LEFT JOIN orders o ON o.user_id = u.id WHERE u.id = @id AND o.total > @min"
            )
        ).ShouldBe([(false, "catalog"), (false, "catalog"), (false, "catalog"), (false, "catalog")]);

    // The tool leaves the database as it found it: no statement, and the settings as "open" read them.
    [Fact]
    public async Task Describe_GatedQuery_LeavesNoStatementAndThePlanCacheModeAsOpenReadIt()
    {
        var token = TestContext.Current.CancellationToken;
        var opened = await Sessions.OpenAsync(postgres.ConnectionString);
        await using var session = opened.Value.ShouldBeOfType<PostgresSession>();
        var read = session.Server.Settings.Single(static setting => setting.Name == "plan_cache_mode").Value;

        var described = await session.DescribeAsync(
            Requests.Of("SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id"),
            token
        );

        described.Value.ShouldNotBeNull().Plan.ShouldBe(SidecarValues.Plan.Walked);
        (await Sessions.PreparedStatementsAsync(session)).ShouldBe(0);
        await using var show = await session.Connection.CommandAsync("SHOW plan_cache_mode", token);
        (await show.ExecuteScalarAsync(token)).ShouldBe(read);
    }

    // The ModifyTable sits over a scan of its own target with the same alias: one instance, not a doubled alias.
    [Fact]
    public async Task Describe_UpdateFromReturning_GivesTheTargetTheCatalogsAnswer() =>
        (
            await WalkedAsync(
                "UPDATE users u SET name = @name FROM orders o LEFT JOIN billing.invoices i ON i.order_id = o.id "
                    + "WHERE o.user_id = u.id RETURNING u.id, o.total, i.amount"
            )
        ).ShouldBe([(false, "catalog"), (false, "catalog"), (true, "outer-join")]);

    // A DELETE of one table prints bare names, which are its target's.  The function's name is what gates it.
    [Fact]
    public async Task Describe_DeleteReturningBareNames_GivesTheTargetTheCatalogsAnswer() =>
        (await WalkedAsync("DELETE FROM orders WHERE left(note, 1) = @initial RETURNING id, note")).ShouldBe([
            (false, "catalog"),
            (true, "catalog"),
        ]);

    // The gate lets the query in for the word, and here the word is a table's alias: an instance like any other,
    // on every version.
    [Fact]
    public async Task Describe_TableAliasedOld_IsAnInstanceLikeAnyOther() =>
        (
            await WalkedAsync(
                "UPDATE users u SET name = @name FROM orders old WHERE old.user_id = u.id RETURNING old.total, u.id"
            )
        ).ShouldBe([(false, "catalog"), (false, "catalog")]);

    // PostgreSQL 18's old row cannot be given the name of a table of the statement, so an alias in the plan is
    // never both: the walk can take an alias it finds for the relation instance.
    [Fact]
    public async Task Describe_OldAliasThatIsAlsoATablesAlias_IsRefusedByTheServer()
    {
        Assert.SkipWhen(await ServerMajorAsync() < 18, "PostgreSQL before 18 has no old and new in RETURNING.");

        var described = await Sessions.DescribeAsync(
            postgres.ConnectionString,
            Requests.Of(
                "UPDATE users u SET name = @name FROM orders o WHERE o.user_id = u.id "
                    + "RETURNING WITH (OLD AS o) o.id"
            )
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure
            .ServerLines.Any(static line => line.Contains("specified more than once", StringComparison.Ordinal))
            .ShouldBeTrue();
    }

    // PostgreSQL 17 proves the join's condition false from the NOT NULL and scans nothing: the plan prints a bare
    // "total" from a Result, which says nothing of a relation.  An older server scans orders, and the walk maps it.
    // Either way the column is nullable.
    [Fact]
    public async Task Describe_JoinConditionTheNewerPlannerProvesFalse_IsNullableOnEveryVersion()
    {
        var proved = await ServerMajorAsync() >= 17;

        var columns = await WalkedAsync(
            "SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id AND o.id IS NULL"
        );

        columns.ShouldBe([(false, "catalog"), (true, proved ? "heuristic" : "outer-join")]);
    }

    // A relation the planner proved empty under an outer join is a Result, and a RETURNING list prints its column
    // as a bare name at the ModifyTable: not the target's, and nullable on every version.
    [Fact]
    public async Task Describe_ReturningOfARelationProvedEmpty_IsNullable()
    {
        var columns = await WalkedAsync(
            "DELETE FROM orders t USING users a LEFT JOIN billing.invoices b ON false "
                + "WHERE t.user_id = a.id RETURNING b.id, t.id"
        );

        columns[0].Nullable.ShouldBe(true);
        columns[1].ShouldBe((false, "catalog"));
    }

    // A partitioned table scans as an Append of its partitions, whose aliases are not the table's: its columns
    // keep the heuristic and are never false.
    [Fact]
    public async Task Describe_PartitionedTable_KeepsTheHeuristic()
    {
        await using (var source = NpgsqlDataSource.Create(postgres.ConnectionString))
        await using (var create = source.CreateCommand(Partitioned))
        {
            _ = await create.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        (await WalkedAsync("SELECT p.id, u.name FROM walk_parts p LEFT JOIN users u ON u.id = p.id")).ShouldBe([
            (true, "heuristic"),
            (true, "outer-join"),
        ]);
    }
}
```

- [ ] **Step 8: Record the two gated cases that exist**

Add to the theory `Replay_CaptureWithACallRemoved_ThrowsACaptureMiss` of `tests/SqlSource.Tool.Tests/Fixtures/DescriberFixtureTests.cs`, after the line of `check`:

```csharp
    [InlineData("explain", "select-left-join")]
```

Run: `tools/record-fixtures.sh`
Expected: `Recording 23 cases of postgres against postgres:18 ... done`

Run: `git status --short tests/SqlSource.Tool.Tests/Fixtures/Describers`
Expected: four files modified, the `capture.json` and the `expected.json` of `select-left-join` and of `match-left-join-nullability`.  When the default image has moved to a new patch release, the `open` answer and `serverVersion` of every case change too, which is the server's and is committed as it is.  Any other change is a bug to find before going on.

Read `git diff tests/SqlSource.Tool.Tests/Fixtures/Describers`.  What it must show:

- In each of the two `capture.json`, one new call `explain` between `check` and `deallocate`, whose request is `"statement": "sqlsource_1"`, the number of parameters and `"settings": []`, and whose answer holds a `plan` and `"error": null`.
- `select-left-join/expected.json`: `"plan": "walked"`; `id` `false` with `catalog`; `email` `true` with `catalog`; `total` `true` with `outer-join`; `n` unchanged.
- `match-left-join-nullability/expected.json`: `"plan": "walked"`; every column `true` with `outer-join`; `tableMatch` still `nullability-differs`.

- [ ] **Step 9: Run everything the change touches, on both servers**

Run: `dotnet test --project tests/SqlSource.Tool.Tests`
Expected: PASS.

Run: `dotnet test --project tests/SqlSource.Tests.Postgres`
Expected: PASS.

Run: `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14 dotnet test --project tests/SqlSource.Tests.Postgres`
Expected: PASS, with the tests of 17 and of 18 skipped.

- [ ] **Step 10: Format, validate and commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add src/SqlSource.Tool/Describing/Postgres tests/SqlSource.Tool.Tests tests/SqlSource.Tests.Postgres
git commit -m "$(cat <<'EOF'
Walk the plan of a gated query, and match its table after the walk

A gated query with rows is asked for its generic plan after the check,
and each column takes the walk's answer; a plan the server will not
give, or that cannot be used, leaves the heuristic.  The table match
moves after the check and the walk, whose nullability it reads, so a
query whose check fails writes no table-match event.  The two gated
cases that exist are recorded again.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: The settings for the tests, and `explain-settings.txt`

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresDescriber.cs`, `PostgresSession.cs`
- Modify: `tests/SqlSource.Tool.Tests/PostgresScript.cs`, `Fixtures/FixtureCase.cs`, `Fixtures/DescriberFixtureTests.cs:46-51`, `tests/SqlSource.Tests.Postgres/Describer/FixtureCasesTests.cs:49-67`
- Create: `tests/SqlSource.Tool.Tests/Fixtures/FixtureCaseTests.cs`
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresDescriberTests.cs`, `PostgresSessionTests.cs`

**Interfaces:**
- Consumes: `PlannerSettings.Checked`, `PlannerSettings.None`, `PostgresExplainRequest`, the session's `WalkAsync` of Task 5.
- Produces:
  - `PostgresDescriber()` and `PostgresDescriber(IReadOnlyList<string> explainSettings)`, which throws `ArgumentException` for a setting outside the list
  - `PostgresSession.OpenAsync(OpenRequest request, IRunLog log, IReadOnlyList<string> explainSettings, CancellationToken cancellationToken)`
  - `FixtureCase.ExplainSettingsFile = "explain-settings.txt"` and `FixtureCase.ExplainSettings : IReadOnlyList<string>`, empty for a case without the file
  - `PostgresScript.OpenAsync(IDescribeExchange exchange, IRunLog? log = null, string connection = Connection, IReadOnlyList<string>? explainSettings = null)` and `PostgresScript.SessionAsync(IDescribeExchange exchange, IRunLog? log = null, IReadOnlyList<string>? explainSettings = null)`

- [ ] **Step 1: Write the failing tests**

Add to `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresDescriberTests.cs`:

```csharp
    // The list is closed: nothing else reaches a SET LOCAL, and a describer that would send it is never made.
    [Theory]
    [InlineData("work_mem=1")]
    [InlineData("enable_hashjoin=off; DROP TABLE users")]
    public void Constructor_SettingOutsideTheList_Throws(string setting) =>
        Should.Throw<ArgumentException>(() => new PostgresDescriber(["enable_nestloop=off", setting]));
```

Add to `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs`:

```csharp
    // The settings of the describer are in the request, so a capture recorded with them is replayed with them.
    [Fact]
    public async Task DescribeAsync_DescriberWithSettings_AsksThePlanWithThem()
    {
        var users = PostgresTables.Users;
        var plan = Join("Left", Scan("u"), Scan("o")) with { Output = ["u.id", "count(*) OVER (?)"] };
        var script = new PostgresScript().Open();
        _ = PreparesOnly(script, 1, GatedUsersId, [], new([], null));
        _ = Columns(script, 1, 0, PostgresTables.ColumnOf(0, "id", 23, users, 1), Expression(1, "n", 20));
        _ = Tables(script, [users.Oid], users);
        _ = Resolves(script, [20, 23], PostgresRows.Int8, PostgresRows.Int4);
        _ = Checks(script, GatedUsersId, [], Expression(0, "id", 23), Expression(1, "n", 20));
        _ = script.Answer(
            "explain",
            new PostgresExplainRequest("sqlsource_1", 0, ["enable_hashjoin=off", "enable_mergejoin=off"]),
            PlanOf(plan)
        );
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(
            exchange,
            explainSettings: ["enable_hashjoin=off", "enable_mergejoin=off"]
        );

        var described = await session.DescribeAsync(Request(GatedUsersId), TestContext.Current.CancellationToken);

        exchange.Unused.ShouldBeEmpty();
        described.Value.ShouldNotBeNull().Plan.ShouldBe(SidecarValues.Plan.Walked);
    }
```

Create `tests/SqlSource.Tool.Tests/Fixtures/FixtureCaseTests.cs`:

```csharp
using System;
using Shouldly;
using Xunit;

namespace SqlSource.Tool.Tests.Fixtures;

// What a case reads of its folder beside its three texts.
public sealed class FixtureCaseTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private FixtureCase Case() => new("postgres", "one", _folder.CreateFolder("postgres/one"));

    [Fact]
    public void ExplainSettings_CaseWithoutTheFile_HasNone() => Case().ExplainSettings.ShouldBeEmpty();

    // One name=value on a line; an empty line and the spaces around a line are not settings.
    [Fact]
    public void ExplainSettings_CaseWithTheFile_HasEachLine()
    {
        _ = _folder.WriteFile(
            $"postgres/one/{FixtureCase.ExplainSettingsFile}",
            "enable_mergejoin=off\n  enable_nestloop=off  \n\n"
        );

        Case().ExplainSettings.ShouldBe(["enable_mergejoin=off", "enable_nestloop=off"]);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Fixtures.FixtureCaseTests"`
Expected: the build fails: `PostgresDescriber` has no such constructor, and `FixtureCase` no `ExplainSettings`.

- [ ] **Step 3: Give the describer and the session the settings**

`src/SqlSource.Tool/Describing/Postgres/PostgresDescriber.cs` becomes:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// Asks a PostgreSQL to describe queries, through Npgsql.  It is registered nowhere until the describer is whole:
/// the released tool has none for <c>postgres</c>.
/// </summary>
internal sealed class PostgresDescriber : IQueryDescriber
{
    private readonly IReadOnlyList<string> _explainSettings;

    /// <summary>The describer as the tool makes it: the planner chooses its own join methods.</summary>
    public PostgresDescriber()
        : this(PlannerSettings.None) { }

    /// <summary>
    /// A describer whose every <c>explain</c> carries settings of the planner: for a test that forces a join
    /// method, and for the replay of a capture that was recorded so.
    /// </summary>
    /// <param name="explainSettings">Settings as <c>name=value</c>, each of <see cref="PlannerSettings" />.</param>
    /// <exception cref="ArgumentException">A setting is not of the list.</exception>
    public PostgresDescriber(IReadOnlyList<string> explainSettings) =>
        _explainSettings = PlannerSettings.Checked(explainSettings);

    public SqlDialect Dialect => SqlDialect.PostgreSql;

    public Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
        OpenRequest request,
        IRunLog log,
        CancellationToken cancellationToken
    ) => PostgresSession.OpenAsync(request, log, _explainSettings, cancellationToken);
}
```

In `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`:

- Add the field `private readonly IReadOnlyList<string> _explainSettings;`.
- The private constructor takes `IReadOnlyList<string> explainSettings` after `ServerInfo server` and sets the field.
- `OpenAsync` takes `IReadOnlyList<string> explainSettings` between `log` and `cancellationToken`, with the documentation `/// <param name="explainSettings">The settings of the planner that every <c>explain</c> carries: none, but in a test.</param>`, and passes it to the constructor: `new PostgresSession(request, log, connection, live, ServerOf(answer), explainSettings)`.
- In `WalkAsync`, `PlannerSettings.None` becomes `_explainSettings`.

- [ ] **Step 4: Give the helpers of the tests the settings**

In `tests/SqlSource.Tool.Tests/PostgresScript.cs`, add `using System.Collections.Generic;` and replace the last two methods with:

```csharp
    public static Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
        IDescribeExchange exchange,
        IRunLog? log = null,
        string connection = Connection,
        IReadOnlyList<string>? explainSettings = null
    ) =>
        new PostgresDescriber(explainSettings ?? [])
            .OpenAsync(
                new OpenRequest(Database, connection, exchange, TimeProvider.System),
                log ?? NullRunLog.Instance,
                TestContext.Current.CancellationToken
            );

    // The session of a script whose "open" answered a server.
    public static async Task<IDescribeSession> SessionAsync(
        IDescribeExchange exchange,
        IRunLog? log = null,
        IReadOnlyList<string>? explainSettings = null
    ) => (await OpenAsync(exchange, log, explainSettings: explainSettings)).Value.ShouldNotBeNull();
```

In `tests/SqlSource.Tool.Tests/Fixtures/FixtureCase.cs`, add `using System.Collections.Generic;` and `using System.Linq;`, extend the comment above the record with the sentence `An optional explain-settings.txt holds the settings of the planner the case was recorded with.`, and add inside the record, after `ExpectedFile`:

```csharp
    // The settings of the planner that the case's "explain" carries, one name=value on a line: a case whose name
    // ends in a join method turns the other two off.  Most cases have no such file.
    public const string ExplainSettingsFile = "explain-settings.txt";
```

and after `Expected`:

```csharp
    public IReadOnlyList<string> ExplainSettings =>
        File.Exists(Path.Combine(Folder, ExplainSettingsFile))
            ?
            [
                .. File.ReadAllLines(Path.Combine(Folder, ExplainSettingsFile))
                    .Select(static line => line.Trim())
                    .Where(static line => line.Length > 0),
            ]
            : [];
```

In `tests/SqlSource.Tool.Tests/Fixtures/DescriberFixtureTests.cs`, `"postgres" => new PostgresDescriber(),` becomes `"postgres" => new PostgresDescriber(fixture.ExplainSettings),`.

In `tests/SqlSource.Tests.Postgres/Describer/FixtureCasesTests.cs`, both `static _ => new PostgresDescriber(),` become `static found => new PostgresDescriber(found.ExplainSettings),`.

- [ ] **Step 5: Run the tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests`
Expected: PASS.

Run: `dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class "SqlSource.Tests.Postgres.Describer.FixtureCasesTests"`
Expected: PASS.

- [ ] **Step 6: Format, validate and commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add src/SqlSource.Tool/Describing/Postgres tests/SqlSource.Tool.Tests tests/SqlSource.Tests.Postgres
git commit -m "$(cat <<'EOF'
Let a test give the describer settings of the planner

A PostgresDescriber is made with settings of a closed list, which its
every explain carries.  A fixture case reads them from an
explain-settings.txt beside its query, so that the harness describes
and replays the case with the settings it was recorded with.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 7: The events

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresEvents.cs`, `PostgresSession.cs`
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresEventsTests.cs`

**Interfaces:**
- Consumes: `PostgresExplainRequest`, `PostgresExplainAnswer`, `PostgresLive.ExplainText`, `WalkOutcome`, `WalkedColumn`, `VisitedNode`, `LogValue`, `LogMember`, `IRunLog`.
- Produces: `PostgresEvents.Explain(IRunLog log, string query, PostgresExplainRequest request, PostgresExplainAnswer answer, long durationMs)` and `PostgresEvents.Walk(IRunLog log, string query, WalkOutcome outcome)`.  The names as the log of a session writes them: `postgres.explain`, `postgres.walk.node`, `postgres.walk.column`, `postgres.walk`.

- [ ] **Step 1: Write the failing tests**

Add to `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresEventsTests.cs`, with `using static SqlSource.Tool.Tests.PostgresPlans;` among the usings:

```csharp
    private static readonly DescribeRequest Joined = new(
        "ListUserTotals",
        "SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id",
        new EquatableArray<SqlQueryParameter>([])
    );

    private static readonly PostgresColumnRow UserId = PostgresTables.ColumnOf(0, "id", 23, PostgresTables.Users, 1);

    private static readonly PostgresColumnRow Total = PostgresTables.ColumnOf(
        1,
        "total",
        1700,
        PostgresTables.Orders,
        3
    );

    // The plan of Joined: users is the side the join keeps.
    private static readonly string JoinedPlan = Text(
        Join("Left", Scan("u"), Scan("o")) with
        {
            Output = ["u.id", "o.total"],
        }
    );

    // The whole script of Joined, a gated query, with the answer of "explain" the test gives.
    private static PostgresScript JoinedScript(PostgresExplainAnswer explain) =>
        new PostgresScript()
            .Open()
            .Answer(
                "prepare",
                new PostgresPrepareRequest("sqlsource_1", Joined.Sql, []),
                new PostgresPrepareAnswer([], null)
            )
            .Answer(
                "columns",
                new PostgresColumnsRequest("sqlsource_1", 0),
                new PostgresColumnsAnswer([UserId, Total], null)
            )
            .Answer(
                "tables",
                new PostgresTablesRequest([PostgresTables.Users.Oid, PostgresTables.Orders.Oid]),
                new PostgresTablesAnswer([PostgresTables.Users, PostgresTables.Orders], null)
            )
            .Answer(
                "types",
                new PostgresTypesRequest([23, 1700]),
                new PostgresTypesAnswer([PostgresRows.Int4, PostgresRows.Numeric], null)
            )
            .Answer("check", new PostgresCheckRequest(Joined.Sql, []), Sees(UserId, Total))
            .Answer("explain", new PostgresExplainRequest("sqlsource_1", 0, []), explain)
            .Answer("deallocate", Deallocate, new PostgresDeallocateAnswer(null));

    [Fact]
    public async Task Describe_GatedQuery_WritesExplainANodeForEachAColumnForEachAndWalkBeforeTheMatch()
    {
        var log = new RecordingLog();

        await DescribeAsync(JoinedScript(new PostgresExplainAnswer(JoinedPlan, null)), log, Joined);

        log.Events.Select(written => written.Name)
            .ShouldBe([
                "prepare",
                "columns",
                "tables",
                "types",
                "column",
                "column",
                "check",
                "explain",
                "walk.node",
                "walk.node",
                "walk.node",
                "walk.column",
                "walk.column",
                "walk",
                "table-match",
                "deallocate",
            ]);

        var explain = log.Events[7].Members;
        NamesOf(explain).ShouldBe(["query", "statement", "settings", "sql", "plan", "durationMs"]);
        Member(explain, "query").ShouldBe((LogValue)"ListUserTotals");
        Member(explain, "statement").ShouldBe((LogValue)"sqlsource_1");
        Member(explain, "settings").ShouldBe(LogValue.ListOfTexts([]));
        Member(explain, "sql").ShouldBe((LogValue)"EXPLAIN (VERBOSE, FORMAT JSON) EXECUTE sqlsource_1");
        Member(explain, "plan").ShouldBe((LogValue)JoinedPlan);
        _ = Member(explain, "durationMs").ShouldBeOfType<LogValue.Number>();

        var join = log.Events[8].Members;
        NamesOf(join)
            .ShouldBe(["depth", "nodeType", "joinType", "parentRelationship", "alias", "relation", "nullExtends"]);
        Member(join, "depth").ShouldBe((LogValue)0L);
        Member(join, "nodeType").ShouldBe((LogValue)"Hash Join");
        Member(join, "joinType").ShouldBe((LogValue)"Left");
        Member(join, "parentRelationship").ShouldBe(LogValue.Null);
        Member(join, "alias").ShouldBe(LogValue.Null);
        Member(join, "relation").ShouldBe(LogValue.Null);
        Member(join, "nullExtends").ShouldBe((LogValue)"inner");

        var orders = log.Events[10].Members;
        Member(orders, "depth").ShouldBe((LogValue)1L);
        Member(orders, "nodeType").ShouldBe((LogValue)"Seq Scan");
        Member(orders, "joinType").ShouldBe(LogValue.Null);
        Member(orders, "parentRelationship").ShouldBe((LogValue)"Inner");
        Member(orders, "alias").ShouldBe((LogValue)"o");
        Member(orders, "relation").ShouldBe(LogValue.MapOf(new("schema", "public"), new("table", "o")));
        Member(orders, "nullExtends").ShouldBe((LogValue)"none");

        var total = log.Events[12].Members;
        NamesOf(total)
            .ShouldBe([
                "ordinal",
                "name",
                "output",
                "alias",
                "column",
                "mapped",
                "nullExtended",
                "nullable",
                "nullableSource",
            ]);
        Member(total, "ordinal").ShouldBe((LogValue)1L);
        Member(total, "name").ShouldBe((LogValue)"total");
        Member(total, "output").ShouldBe((LogValue)"o.total");
        Member(total, "alias").ShouldBe((LogValue)"o");
        Member(total, "column").ShouldBe((LogValue)"total");
        Member(total, "mapped").ShouldBe((LogValue)true);
        Member(total, "nullExtended").ShouldBe((LogValue)true);
        Member(total, "nullable").ShouldBe((LogValue)true);
        Member(total, "nullableSource").ShouldBe((LogValue)"outer-join");

        var walk = log.Events[13].Members;
        NamesOf(walk).ShouldBe(["query", "plan", "reason"]);
        Member(walk, "query").ShouldBe((LogValue)"ListUserTotals");
        Member(walk, "plan").ShouldBe((LogValue)"walked");
        Member(walk, "reason").ShouldBe(LogValue.Null);
    }

    // The column event of sub-phase 3.3 is written when the column is built, with the heuristic's answer: the
    // walk's own event holds what the sidecar records.
    [Fact]
    public async Task Describe_GatedQuery_KeepsTheHeuristicInTheColumnEventAndTheAnswerInTheWalks()
    {
        var log = new RecordingLog();

        await DescribeAsync(JoinedScript(new PostgresExplainAnswer(JoinedPlan, null)), log, Joined);

        var built = log.Events.First(written => written.Name == "column").Members;
        Member(built, "nullableSource").ShouldBe((LogValue)"heuristic");
        Member(built, "gated").ShouldBe((LogValue)true);
        var walked = log.Events.First(written => written.Name == "walk.column").Members;
        Member(walked, "nullable").ShouldBe((LogValue)false);
        Member(walked, "nullableSource").ShouldBe((LogValue)"catalog");
    }

    // The log is where a failed EXPLAIN is told from a bug of the walk.
    [Fact]
    public async Task Describe_ExplainThatFails_WritesItsErrorAndTheReasonAndNoNode()
    {
        var log = new RecordingLog();
        var refused = new PostgresServerError("57014", "canceling statement due to statement timeout", null, null, null);

        await DescribeAsync(JoinedScript(new PostgresExplainAnswer(null, refused)), log, Joined);

        log.Events.Select(written => written.Name)
            .Skip(7)
            .ShouldBe(["explain", "walk.column", "walk.column", "walk", "table-match", "deallocate"]);
        var explain = log.Events[7].Members;
        NamesOf(explain).ShouldBe(["query", "statement", "settings", "sql", "error", "durationMs"]);
        Member(explain, "error")
            .ShouldBe(
                LogValue.MapOf(
                    new("sqlState", "57014"),
                    new("message", "canceling statement due to statement timeout"),
                    new("detail", LogValue.Null),
                    new("hint", LogValue.Null),
                    new("position", LogValue.Null)
                )
            );
        var column = log.Events[8].Members;
        Member(column, "output").ShouldBe(LogValue.Null);
        Member(column, "mapped").ShouldBe((LogValue)false);
        Member(column, "nullableSource").ShouldBe((LogValue)"heuristic");
        var walk = log.Events.Single(written => written.Name == "walk").Members;
        Member(walk, "plan").ShouldBe((LogValue)"unavailable");
        Member(walk, "reason").ShouldBe((LogValue)"explain-failed");
    }

    [Theory]
    [InlineData("not json", "unreadable")]
    [InlineData("""[{"Plan": {"Node Type": "Append"}}]""", "no-output")]
    public async Task Describe_PlanThatCannotBeUsed_WritesItsReason(string plan, string reason)
    {
        var log = new RecordingLog();

        await DescribeAsync(JoinedScript(new PostgresExplainAnswer(plan, null)), log, Joined);

        var walk = log.Events.Single(written => written.Name == "walk").Members;
        Member(walk, "plan").ShouldBe((LogValue)"unavailable");
        Member(walk, "reason").ShouldBe((LogValue)reason);
    }

    // A plan holds a user's names, and the connection's value is taken out of it as out of any string.
    [Fact]
    public async Task Describe_ThroughTheLogOfASession_RemovesTheSecretFromThePlanAndFromANode()
    {
        var log = new RecordingLog();

        await DescribeAsync(
            JoinedScript(new PostgresExplainAnswer(JoinedPlan, null)),
            new SessionRunLog(log, "Seq Scan", "postgres."),
            Joined
        );

        var plan = Member(log.Events.Single(written => written.Name == "postgres.explain").Members, "plan");
        plan.ShouldBeOfType<LogValue.Text>().Value.ShouldNotContain("Seq Scan");
        var scan = log.Events.Last(written => written.Name == "postgres.walk.node").Members;
        Member(scan, "nodeType").ShouldBe((LogValue)"***");
    }
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PostgresEventsTests"`
Expected: the new tests fail: the log holds no `explain` and no `walk` event.

- [ ] **Step 3: Write the events**

In `src/SqlSource.Tool/Describing/Postgres/PostgresEvents.cs`, the class remarks' last sentence becomes:

```csharp
/// <c>open</c> has no event of its own: the run's <c>database.open</c> records what the session knows of its
/// server.  A describe writes <c>prepare</c>, <c>columns</c>, <c>tables</c>, <c>types</c>, one <c>column</c> for
/// each column, <c>check</c>, <c>table-match</c> and <c>deallocate</c>, in that order, each only when its call was
/// made, and <c>table-match</c> only for a statement with rows whose check agreed.  A gated query with rows
/// writes, between <c>check</c> and <c>table-match</c>, <c>explain</c>, one <c>walk.node</c> for each node the
/// walk visited, one <c>walk.column</c> for each column, and <c>walk</c>.
```

Add after `TableMatch`:

```csharp
    /// <summary>
    /// <c>postgres.explain</c>: the <c>EXPLAIN</c> as it was sent, with the settings it ran under, and the plan's
    /// text or the error.  The log's writer cuts a string at 1 MiB.
    /// </summary>
    public static void Explain(
        IRunLog log,
        string query,
        PostgresExplainRequest request,
        PostgresExplainAnswer answer,
        long durationMs
    ) =>
        log.Write(
            "explain",
            new("query", query),
            new("statement", request.Statement),
            new("settings", LogValue.ListOfTexts(request.Settings)),
            new("sql", PostgresLive.ExplainText(request)),
            answer.Error is { } error ? new LogMember(Error, ErrorOf(error)) : new LogMember("plan", answer.Plan),
            new(Duration, durationMs)
        );

    /// <summary>
    /// The events of a walk, in this order: <c>postgres.walk.node</c> for each node that was visited,
    /// <c>postgres.walk.column</c> for each column, and <c>postgres.walk</c> with the plan's value and, when it is
    /// <c>unavailable</c>, the reason.  It is the one place that tells a failed <c>EXPLAIN</c> from a plan the
    /// walk could not use.
    /// </summary>
    /// <remarks>
    /// The relation of a node is its schema and its name apart, as the table of <c>table-match</c> is: a name may
    /// hold a dot.
    /// </remarks>
    public static void Walk(IRunLog log, string query, WalkOutcome outcome)
    {
        foreach (var visited in outcome.Nodes)
        {
            var node = visited.Node;
            log.Write(
                "walk.node",
                new("depth", visited.Depth),
                new("nodeType", node.NodeType),
                new("joinType", node.JoinType),
                new("parentRelationship", node.ParentRelationship),
                new("alias", node.Alias),
                new(
                    "relation",
                    node.RelationName is { } relation
                        ? LogValue.MapOf(new("schema", node.Schema), new("table", relation))
                        : LogValue.Null
                ),
                new("nullExtends", visited.NullExtends)
            );
        }

        foreach (var walked in outcome.Columns)
        {
            var column = walked.Column;
            log.Write(
                "walk.column",
                new("ordinal", column.Ordinal),
                new("name", column.Name),
                new("output", walked.Output),
                new("alias", walked.Reference?.Alias),
                new("column", walked.Reference?.Column),
                new("mapped", walked.Mapped),
                new("nullExtended", walked.NullExtended),
                new("nullable", column.Nullable is { } nullable ? (LogValue)nullable : LogValue.Null),
                new("nullableSource", column.NullableSource)
            );
        }

        log.Write("walk", new("query", query), new("plan", outcome.Plan), new("reason", outcome.Reason));
    }
```

- [ ] **Step 4: Write them from the session**

In `WalkAsync` of `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`, the body becomes:

```csharp
        var asked = new PostgresExplainRequest(statement, request.Parameters.Count, _explainSettings);
        var started = _time.GetTimestamp();
        var answer = await _exchange.AskAsync("explain", asked, _live.ExplainAsync, cancellationToken);
        PostgresEvents.Explain(_log, request.Name, asked, answer, ElapsedSince(started));

        var outcome = answer switch
        {
            { Error: not null } => NullabilityWalk.Unavailable(WalkReasons.ExplainFailed, columns),
            { Plan: { } text } when PlanNode.Read(text) is { } root => NullabilityWalk.Run(root, columns, origins),
            _ => NullabilityWalk.Unavailable(WalkReasons.Unreadable, columns),
        };
        PostgresEvents.Walk(_log, request.Name, outcome);
        return outcome;
```

- [ ] **Step 5: Run them to see them pass**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PostgresEventsTests"`
Expected: PASS.

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PostgresSessionTests"`
Expected: PASS.

- [ ] **Step 6: Format, validate and commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add src/SqlSource.Tool/Describing/Postgres/PostgresEvents.cs src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresEventsTests.cs
git commit -m "$(cat <<'EOF'
Log the plan and what the walk made of it

postgres.explain holds the EXPLAIN as sent and its plan or its error;
postgres.walk.node each node that was visited; postgres.walk.column
each column with its entry and what was found; postgres.walk the
plan's value and why it is unavailable.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 8: The matrix

**Files:**
- Create: 37 folders under `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/`, each with a `query.sql`, a recorded `capture.json` and `expected.json`, and eight of them with an `explain-settings.txt`
- Create: `tests/SqlSource.Tool.Tests/Fixtures/WalkMatrixTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Fixtures/DescriberFixtureTests.cs:68-98`

**Interfaces:**
- Consumes: `FixtureCases.Of`, `FixtureCase.Expected`, `FixtureCase.Capture`, `FixtureCase.ExplainSettings`, `tools/record-fixtures.sh`.
- Produces: the cases named `walk-*`.

- [ ] **Step 1: Write the test that holds the matrix**

It says, for each case, the plan's value and each column's answer, so that a recording is checked against what the spec expects and not against itself.

Create `tests/SqlSource.Tool.Tests/Fixtures/WalkMatrixTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Shouldly;
using SqlSource.Tool.Describing.Capture;
using Xunit;

namespace SqlSource.Tool.Tests.Fixtures;

// The matrix of the nullability walk: what the expected.json of each walk-* case must say, written here by hand
// from the spec.  A recording that says otherwise is a bug of the walk or of the case, never a reason to change
// this table without knowing which.  DescriberFixtureTests replays the same cases, and tests/SqlSource.Tests.Postgres
// describes them on PostgreSQL 14 and 18.
public class WalkMatrixTests
{
    // A NOT NULL column that nothing extends.
    private const string Kept = "false catalog";

    // A column that an outer join null-extends.
    private const string Joined = "true outer-join";

    // A column of a table whose entry of the plan cannot be mapped, or whose plan cannot be used.
    private const string Heuristic = "true heuristic";

    // A column of a view that nothing extends.
    private const string OfView = "null view";

    // An expression.
    private const string NoOrigin = "null no-origin";

    private const string Walked = "walked";
    private const string Unavailable = "unavailable";

    private static readonly IReadOnlyList<FixtureCase> Found = FixtureCases.Of("postgres");

    private static readonly Dictionary<string, (string Plan, string[] Columns)> Rows = new(StringComparer.Ordinal)
    {
        ["walk-left"] = (Walked, [Kept, Kept, Joined, Joined]),
        ["walk-left-hash"] = (Walked, [Kept, Kept, Joined, Joined]),
        ["walk-left-merge"] = (Walked, [Kept, Kept, Joined, Joined]),
        ["walk-left-nestloop"] = (Walked, [Kept, Kept, Joined, Joined]),
        ["walk-right"] = (Walked, [Kept, Kept, Joined, Joined]),
        ["walk-right-hash"] = (Walked, [Kept, Kept, Joined, Joined]),
        ["walk-right-merge"] = (Walked, [Kept, Kept, Joined, Joined]),
        ["walk-right-nestloop"] = (Walked, [Kept, Kept, Joined, Joined]),
        ["walk-full"] = (Walked, [Joined, Joined]),
        ["walk-full-hash"] = (Walked, [Joined, Joined]),
        ["walk-full-merge"] = (Walked, [Joined, Joined]),
        ["walk-nested"] = (Walked, [Kept, Joined, Joined]),
        ["walk-chain"] = (Walked, [Kept, Joined, Joined]),
        ["walk-self-join"] = (Walked, [Kept, Kept, Joined, Joined]),
        ["walk-reduced-by-where"] = (Walked, [Kept, Kept]),
        ["walk-reduced-by-join"] = (Walked, [Kept, Kept, Kept]),
        ["walk-removed"] = (Walked, [Kept, Kept]),
        ["walk-subquery-flattened"] = (Walked, [Kept, Joined]),
        ["walk-subquery-kept"] = (Walked, [Heuristic, Heuristic, NoOrigin]),
        ["walk-cte-inlined"] = (Walked, [Kept, Joined]),
        ["walk-cte-materialized"] = (Walked, [Heuristic, Heuristic]),
        ["walk-lateral"] = (Walked, [Kept, Joined]),
        ["walk-view"] = (Walked, [OfView, OfView, Joined, Joined, Joined]),
        ["walk-view-nullable-side"] = (Walked, [Kept, Joined]),
        ["walk-using"] = (Walked, [NoOrigin, Joined, Joined]),
        ["walk-natural"] = (Walked, [NoOrigin, NoOrigin, Joined, Joined]),
        ["walk-aggregate"] = (Walked, [Kept, Joined, NoOrigin]),
        ["walk-union"] = (Unavailable, [NoOrigin, NoOrigin]),
        ["walk-returning"] = (Walked, [Kept, Kept, Joined]),
        ["walk-proved-empty"] = (Walked, [Kept, Heuristic]),
        ["walk-quoted-alias"] = (Walked, [Kept, Joined]),
        ["walk-semi-anti"] = (Walked, [Kept, NoOrigin]),
        ["walk-anti"] = (Walked, [Kept, Joined, Joined]),
        ["walk-rollup"] = (Unavailable, [Heuristic, NoOrigin]),
        ["walk-cube"] = (Unavailable, [Heuristic, Heuristic, NoOrigin]),
        ["walk-grouping-sets"] = (Unavailable, [Heuristic, NoOrigin]),
        ["walk-order-by-unselected"] = (Walked, [Kept, Joined]),
    };

    public static TheoryData<string> Names => [.. Rows.Keys];

    private static FixtureCase CaseOf(string name) => Found.Single(found => found.Name == name);

    // The one call of "explain" a case's capture holds.
    private static CaptureCall ExplainOf(string name) =>
        CaptureJson
            .Read(CaseOf(name).Capture)
            .Value.ShouldNotBeNull()
            .Calls.Single(static call => call.Method == "explain");

    // The plan the server answered, as it wrote it.
    private static string PlanOf(string name) =>
        ExplainOf(name).Answer.GetProperty("plan").GetString().ShouldNotBeNull();

    [Theory]
    [MemberData(nameof(Names))]
    public void Expected_CaseOfTheMatrix_SaysThePlansValueAndEachColumnsAnswer(string name)
    {
        var (plan, columns) = Rows[name];
        using var sidecar = JsonDocument.Parse(CaseOf(name).Expected);
        var entry = sidecar.RootElement.GetProperty("queries").EnumerateObject().Single().Value;

        entry.GetProperty("plan").GetString().ShouldBe(plan);
        entry
            .GetProperty("columns")
            .EnumerateArray()
            .Select(static column =>
                $"{column.GetProperty("nullable").GetRawText()} {column.GetProperty("nullableSource").GetString()}"
            )
            .ShouldBe(columns);
    }

    // A recording that added or lost a case of the matrix does not pass silently.
    [Fact]
    public void Matrix_AndTheCasesNamedWalk_AreTheSame() =>
        Found
            .Select(static found => found.Name)
            .Where(static name => name.StartsWith("walk-", StringComparison.Ordinal))
            .ShouldBe(Rows.Keys, ignoreOrder: true);

    // A case named for a join method was planned with it: its request holds the settings that turned the other
    // two off, and the root of its plan is the join.
    [Theory]
    [InlineData("walk-left-hash", "Hash Join")]
    [InlineData("walk-left-merge", "Merge Join")]
    [InlineData("walk-left-nestloop", "Nested Loop")]
    [InlineData("walk-right-hash", "Hash Join")]
    [InlineData("walk-right-merge", "Merge Join")]
    [InlineData("walk-right-nestloop", "Nested Loop")]
    [InlineData("walk-full-hash", "Hash Join")]
    [InlineData("walk-full-merge", "Merge Join")]
    public void Capture_CaseNamedForAJoinMethod_WasPlannedWithIt(string name, string node)
    {
        var fixture = CaseOf(name);

        fixture.ExplainSettings.Count.ShouldBe(2);
        ExplainOf(name)
            .Request.GetProperty("settings")
            .EnumerateArray()
            .Select(static setting => setting.GetString())
            .ShouldBe(fixture.ExplainSettings);
        PlanOf(name).ShouldContain($"\"Node Type\": \"{node}\"");
    }

    // The shapes whose plan is what the case is for.
    [Theory]
    [InlineData("walk-subquery-kept", "\"Node Type\": \"Subquery Scan\"")]
    [InlineData("walk-cte-materialized", "\"Node Type\": \"CTE Scan\"")]
    [InlineData("walk-union", "\"Node Type\": \"Append\"")]
    [InlineData("walk-returning", "\"Node Type\": \"ModifyTable\"")]
    [InlineData("walk-proved-empty", "\"Node Type\": \"Result\"")]
    [InlineData("walk-rollup", "\"Grouping Sets\"")]
    [InlineData("walk-cube", "\"Grouping Sets\"")]
    [InlineData("walk-grouping-sets", "\"Grouping Sets\"")]
    [InlineData("walk-full", "\"Join Type\": \"Full\"")]
    [InlineData("walk-anti", "Anti\"")]
    public void Capture_CaseOfAShape_HoldsTheShape(string name, string holds) => PlanOf(name).ShouldContain(holds);

    [Theory]
    [InlineData("walk-subquery-flattened", "Subquery Scan")]
    [InlineData("walk-cte-inlined", "CTE Scan")]
    [InlineData("walk-reduced-by-where", "\"Join Type\": \"Left\"")]
    [InlineData("walk-reduced-by-where", "\"Join Type\": \"Right\"")]
    [InlineData("walk-reduced-by-join", "\"Join Type\": \"Left\"")]
    [InlineData("walk-reduced-by-join", "\"Join Type\": \"Right\"")]
    [InlineData("walk-removed", "Join Type")]
    public void Capture_CaseThePlannerSimplified_HoldsNoTraceOfWhatItRemoved(string name, string lacks) =>
        PlanOf(name).ShouldNotContain(lacks);
}
```

- [ ] **Step 2: Run it to see it fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Fixtures.WalkMatrixTests"`
Expected: FAIL, no case named `walk-left`.

- [ ] **Step 3: Write the queries**

Each case is a folder under `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/` with a `query.sql` of two or more lines, the marker and the query, ended by `;` and a line break.  The SQL of each:

| Case | `-- name:` | SQL |
|----|----|----|
| `walk-left` | `WalkLeft` | `SELECT u.id, u.name, o.id AS order_id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id WHERE u.id = @id` |
| `walk-left-hash`, `-merge`, `-nestloop` | `WalkLeftByName` | `SELECT u.id, u.name, o.id AS order_id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id WHERE u.name = @name` |
| `walk-right`, `-hash`, `-merge`, `-nestloop` | `WalkRight` | `SELECT u.id, u.name, o.id AS order_id, o.total FROM orders o RIGHT JOIN users u ON o.user_id = u.id WHERE u.name = @name` |
| `walk-full`, `-hash`, `-merge` | `WalkFull` | `SELECT u.id, o.total FROM users u FULL JOIN orders o ON o.user_id = u.id` |
| `walk-nested` | `WalkNested` | `SELECT u.id, o.total, i.amount FROM users u LEFT JOIN (orders o JOIN billing.invoices i ON i.order_id = o.id) ON o.user_id = u.id` |
| `walk-chain` | `WalkChain` | `SELECT u.id, o.total, i.amount FROM users u LEFT JOIN orders o ON o.user_id = u.id LEFT JOIN billing.invoices i ON i.order_id = o.id` |
| `walk-self-join` | `WalkSelfJoin` | `SELECT a.id, a.name, b.id AS other_id, b.name AS other_name FROM users a LEFT JOIN users b ON b.name = a.name AND b.id <> a.id` |
| `walk-reduced-by-where` | `WalkReducedByWhere` | `SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id WHERE o.total > @min` |
| `walk-reduced-by-join` | `WalkReducedByJoin` | `SELECT u.id, o.total, i.amount FROM users u LEFT JOIN orders o ON o.user_id = u.id JOIN billing.invoices i ON i.order_id = o.id` |
| `walk-removed` | `WalkRemoved` | `SELECT o.id, o.total FROM orders o LEFT JOIN users u ON u.id = o.user_id WHERE o.total > @min` |
| `walk-subquery-flattened` | `WalkSubqueryFlattened` | `SELECT s.id, s.total FROM (SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id) s WHERE s.id = @id` |
| `walk-subquery-kept` | `WalkSubqueryKept` | `SELECT s.id, s.total, s.n FROM (SELECT u.id, o.total, row_number() OVER (ORDER BY u.id) AS n FROM users u LEFT JOIN orders o ON o.user_id = u.id) s WHERE s.n > @n` |
| `walk-cte-inlined` | `WalkCteInlined` | `WITH s AS (SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id) SELECT s.id, s.total FROM s WHERE s.id = @id` |
| `walk-cte-materialized` | `WalkCteMaterialized` | `WITH s AS MATERIALIZED (SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id) SELECT s.id, s.total FROM s` |
| `walk-lateral` | `WalkLateral` | `SELECT u.id, l.total FROM users u LEFT JOIN LATERAL (SELECT o.total FROM orders o WHERE o.user_id = u.id) l ON true` |
| `walk-view` | `WalkView` | `SELECT v.user_id, v.name, v.order_id, v.total, i.amount FROM user_orders v LEFT JOIN billing.invoices i ON i.order_id = v.order_id` |
| `walk-view-nullable-side` | `WalkViewNullableSide` | `SELECT o.id, a.name FROM orders o LEFT JOIN active_users a ON a.id = o.user_id` |
| `walk-using` | `WalkUsing` | `SELECT user_id, o.total, b.during FROM orders o FULL JOIN bookings b USING (user_id)` |
| `walk-natural` | `WalkNatural` | `SELECT id, user_id, o.total, b.during FROM orders o NATURAL FULL JOIN bookings b` |
| `walk-aggregate` | `WalkAggregate` | `SELECT u.id, o.user_id, count(*) AS n FROM users u LEFT JOIN orders o ON o.user_id = u.id GROUP BY u.id, o.user_id` |
| `walk-union` | `WalkUnion` | `SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id UNION ALL SELECT u.id, o.total FROM users u RIGHT JOIN orders o ON o.user_id = u.id` |
| `walk-returning` | `WalkReturning` | `UPDATE users u SET name = @name FROM orders o LEFT JOIN billing.invoices i ON i.order_id = o.id WHERE o.user_id = u.id RETURNING u.id, o.total, i.amount` |
| `walk-proved-empty` | `WalkProvedEmpty` | `SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id AND 1 = 0` |
| `walk-quoted-alias` | `WalkQuotedAlias` | `SELECT u.id, "Order".total FROM users u LEFT JOIN orders "Order" ON "Order".user_id = u.id` |
| `walk-semi-anti` | `WalkSemiAnti` | `SELECT u.id, left(u.name, 1) AS initial FROM users u WHERE EXISTS (SELECT 1 FROM orders o WHERE o.user_id = u.id) AND NOT EXISTS (SELECT 1 FROM bookings b WHERE b.user_id = u.id)` |
| `walk-anti` | `WalkAnti` | `SELECT u.id, o.id AS order_id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id WHERE o.user_id IS NULL` |
| `walk-rollup` | `WalkRollup` | `SELECT u.status, count(*) AS n FROM users u GROUP BY ROLLUP (u.status)` |
| `walk-cube` | `WalkCube` | `SELECT u.status, u.name, count(*) AS n FROM users u GROUP BY CUBE (u.status, u.name)` |
| `walk-grouping-sets` | `WalkGroupingSets` | `SELECT u.status, count(*) AS n FROM users u GROUP BY GROUPING SETS ((u.status), ())` |
| `walk-order-by-unselected` | `WalkOrderByUnselected` | `SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id ORDER BY o.placed_at` |

The script below writes them all, with the settings of the eight cases named for a join method.  Save it in the scratch directory, not in the repository, and run it from the repository's root:

```bash
#!/usr/bin/env bash
set -euo pipefail
cases='tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres'

# query <case> <name> <sql>
query() {
    mkdir -p "$cases/$1"
    printf -- '-- name: %s\n%s;\n' "$2" "$3" >"$cases/$1/query.sql"
}

# settings <case> <setting> <setting>
settings() {
    printf '%s\n%s\n' "$2" "$3" >"$cases/$1/explain-settings.txt"
}

left='SELECT u.id, u.name, o.id AS order_id, o.total
FROM users u LEFT JOIN orders o ON o.user_id = u.id
WHERE u.name = @name'
right='SELECT u.id, u.name, o.id AS order_id, o.total
FROM orders o RIGHT JOIN users u ON o.user_id = u.id
WHERE u.name = @name'
full='SELECT u.id, o.total
FROM users u FULL JOIN orders o ON o.user_id = u.id'

query walk-left WalkLeft 'SELECT u.id, u.name, o.id AS order_id, o.total
FROM users u LEFT JOIN orders o ON o.user_id = u.id
WHERE u.id = @id'
query walk-right WalkRight "$right"
query walk-full WalkFull "$full"
for method in hash merge nestloop; do
    query "walk-left-$method" WalkLeftByName "$left"
    query "walk-right-$method" WalkRight "$right"
done
for method in hash merge; do
    query "walk-full-$method" WalkFull "$full"
done
for shape in left right full; do
    settings "walk-$shape-hash" enable_mergejoin=off enable_nestloop=off
    settings "walk-$shape-merge" enable_hashjoin=off enable_nestloop=off
done
for shape in left right; do
    settings "walk-$shape-nestloop" enable_hashjoin=off enable_mergejoin=off
done

query walk-nested WalkNested 'SELECT u.id, o.total, i.amount
FROM users u
LEFT JOIN (orders o JOIN billing.invoices i ON i.order_id = o.id) ON o.user_id = u.id'
query walk-chain WalkChain 'SELECT u.id, o.total, i.amount
FROM users u
LEFT JOIN orders o ON o.user_id = u.id
LEFT JOIN billing.invoices i ON i.order_id = o.id'
query walk-self-join WalkSelfJoin 'SELECT a.id, a.name, b.id AS other_id, b.name AS other_name
FROM users a LEFT JOIN users b ON b.name = a.name AND b.id <> a.id'
query walk-reduced-by-where WalkReducedByWhere 'SELECT u.id, o.total
FROM users u LEFT JOIN orders o ON o.user_id = u.id
WHERE o.total > @min'
query walk-reduced-by-join WalkReducedByJoin 'SELECT u.id, o.total, i.amount
FROM users u
LEFT JOIN orders o ON o.user_id = u.id
JOIN billing.invoices i ON i.order_id = o.id'
query walk-removed WalkRemoved 'SELECT o.id, o.total
FROM orders o LEFT JOIN users u ON u.id = o.user_id
WHERE o.total > @min'
query walk-subquery-flattened WalkSubqueryFlattened 'SELECT s.id, s.total
FROM (SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id) s
WHERE s.id = @id'
query walk-subquery-kept WalkSubqueryKept 'SELECT s.id, s.total, s.n
FROM (
    SELECT u.id, o.total, row_number() OVER (ORDER BY u.id) AS n
    FROM users u LEFT JOIN orders o ON o.user_id = u.id
) s
WHERE s.n > @n'
query walk-cte-inlined WalkCteInlined 'WITH s AS (SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id)
SELECT s.id, s.total FROM s WHERE s.id = @id'
query walk-cte-materialized WalkCteMaterialized 'WITH s AS MATERIALIZED (SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id)
SELECT s.id, s.total FROM s'
query walk-lateral WalkLateral 'SELECT u.id, l.total
FROM users u
LEFT JOIN LATERAL (SELECT o.total FROM orders o WHERE o.user_id = u.id) l ON true'
query walk-view WalkView 'SELECT v.user_id, v.name, v.order_id, v.total, i.amount
FROM user_orders v LEFT JOIN billing.invoices i ON i.order_id = v.order_id'
query walk-view-nullable-side WalkViewNullableSide 'SELECT o.id, a.name
FROM orders o LEFT JOIN active_users a ON a.id = o.user_id'
query walk-using WalkUsing 'SELECT user_id, o.total, b.during
FROM orders o FULL JOIN bookings b USING (user_id)'
query walk-natural WalkNatural 'SELECT id, user_id, o.total, b.during
FROM orders o NATURAL FULL JOIN bookings b'
query walk-aggregate WalkAggregate 'SELECT u.id, o.user_id, count(*) AS n
FROM users u LEFT JOIN orders o ON o.user_id = u.id
GROUP BY u.id, o.user_id'
query walk-union WalkUnion 'SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id
UNION ALL
SELECT u.id, o.total FROM users u RIGHT JOIN orders o ON o.user_id = u.id'
query walk-returning WalkReturning 'UPDATE users u SET name = @name
FROM orders o LEFT JOIN billing.invoices i ON i.order_id = o.id
WHERE o.user_id = u.id
RETURNING u.id, o.total, i.amount'
query walk-proved-empty WalkProvedEmpty 'SELECT u.id, o.total
FROM users u LEFT JOIN orders o ON o.user_id = u.id AND 1 = 0'
query walk-quoted-alias WalkQuotedAlias 'SELECT u.id, "Order".total
FROM users u LEFT JOIN orders "Order" ON "Order".user_id = u.id'
query walk-semi-anti WalkSemiAnti 'SELECT u.id, left(u.name, 1) AS initial
FROM users u
WHERE EXISTS (SELECT 1 FROM orders o WHERE o.user_id = u.id)
  AND NOT EXISTS (SELECT 1 FROM bookings b WHERE b.user_id = u.id)'
query walk-anti WalkAnti 'SELECT u.id, o.id AS order_id, o.total
FROM users u LEFT JOIN orders o ON o.user_id = u.id
WHERE o.user_id IS NULL'
query walk-rollup WalkRollup 'SELECT u.status, count(*) AS n
FROM users u
GROUP BY ROLLUP (u.status)'
query walk-cube WalkCube 'SELECT u.status, u.name, count(*) AS n
FROM users u
GROUP BY CUBE (u.status, u.name)'
query walk-grouping-sets WalkGroupingSets 'SELECT u.status, count(*) AS n
FROM users u
GROUP BY GROUPING SETS ((u.status), ())'
query walk-order-by-unselected WalkOrderByUnselected 'SELECT u.id, o.total
FROM users u LEFT JOIN orders o ON o.user_id = u.id
ORDER BY o.placed_at'
```

Run: `find tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres -name query.sql -path '*walk-*' | wc -l`
Expected: `37`

Run: `find tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres -name explain-settings.txt | wc -l`
Expected: `8`

A `FULL JOIN` cannot be a nested loop, so there is no `walk-full-nestloop`: the two `-nestloop` cases, the three `-hash` and the three `-merge` are the eight.

- [ ] **Step 4: Record**

Run: `tools/record-fixtures.sh`
Expected: `Recording 60 cases of postgres against postgres:18 ... done`

Run: `git status --short tests/SqlSource.Tool.Tests/Fixtures/Describers | grep -v 'walk-'`
Expected: nothing.  A case that existed before this task does not change.

- [ ] **Step 5: Check the recording against the matrix**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Fixtures.WalkMatrixTests"`
Expected: PASS.

When a row fails, do not change the table to what was recorded.  Read the case's `capture.json`: the answer of `explain` holds the plan, and the root's `Output` and the `Join Type` of each join say what the walk was given.  A row that is wrong because the planner gave another shape than the case is for is fixed in the case's SQL; a row that is wrong because the walk misread the plan is a bug of Tasks 3 to 5, fixed there with a test of its own.  If neither, stop and tell the user.

- [ ] **Step 6: Name the cases in `Cases_OfPostgres_HoldEveryCase`**

In `tests/SqlSource.Tool.Tests/Fixtures/DescriberFixtureTests.cs`, add to the array of `Cases_OfPostgres_HoldEveryCase`, after `"postgres/update-inferred",`:

```csharp
                "postgres/walk-aggregate",
                "postgres/walk-anti",
                "postgres/walk-chain",
                "postgres/walk-cte-inlined",
                "postgres/walk-cte-materialized",
                "postgres/walk-cube",
                "postgres/walk-full",
                "postgres/walk-full-hash",
                "postgres/walk-full-merge",
                "postgres/walk-grouping-sets",
                "postgres/walk-lateral",
                "postgres/walk-left",
                "postgres/walk-left-hash",
                "postgres/walk-left-merge",
                "postgres/walk-left-nestloop",
                "postgres/walk-natural",
                "postgres/walk-nested",
                "postgres/walk-order-by-unselected",
                "postgres/walk-proved-empty",
                "postgres/walk-quoted-alias",
                "postgres/walk-reduced-by-join",
                "postgres/walk-reduced-by-where",
                "postgres/walk-removed",
                "postgres/walk-returning",
                "postgres/walk-right",
                "postgres/walk-right-hash",
                "postgres/walk-right-merge",
                "postgres/walk-right-nestloop",
                "postgres/walk-rollup",
                "postgres/walk-self-join",
                "postgres/walk-semi-anti",
                "postgres/walk-subquery-flattened",
                "postgres/walk-subquery-kept",
                "postgres/walk-union",
                "postgres/walk-using",
                "postgres/walk-view",
                "postgres/walk-view-nullable-side",
```

- [ ] **Step 7: Replay without Docker, and describe on both servers**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Fixtures.DescriberFixtureTests"`
Expected: PASS, 60 cases replayed.

Run: `dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class "SqlSource.Tests.Postgres.Describer.FixtureCasesTests"`
Expected: PASS.

Run: `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14 dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class "SqlSource.Tests.Postgres.Describer.FixtureCasesTests"`
Expected: PASS.  A case that fails on 14 alone gives another answer there: it cannot be a fixture as it stands.  Find what the older planner does with it, as the plan did for `walk-proved-empty`, and either change its SQL to a shape both servers plan alike or move it to `WalkTests` with each version's answer.  Tell the user which case and why.

- [ ] **Step 8: Format, validate and commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add tests/SqlSource.Tool.Tests/Fixtures
git commit -m "$(cat <<'EOF'
Record the matrix of the nullability walk

Thirty-seven cases: each join type under each method the planner can
choose for it, the shapes the planner flattens, reduces or removes, and
the shapes the walk refuses.  WalkMatrixTests says what each must give,
so that a recording is checked against the spec and not against itself.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 9: The documents, and the outline's row

**Files:**
- Modify: `src/SqlSource.Tool/AGENTS.md:27-36`, `tests/AGENTS.md`, `docs/tech-debt/README.md`, `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`, `src/SqlSource.Tool/Describing/Postgres/OuterJoinGate.cs:11-22`
- Create: `docs/tech-debt/TD-0046-the-walk-leaves-some-columns-to-the-heuristic.md`

**Interfaces:**
- Consumes: everything above.
- Produces: nothing a later task uses.

- [ ] **Step 1: `src/SqlSource.Tool/AGENTS.md`**

In the bullet that starts `**The fixtures of a describer are in`, after `and an optional `issue.md`.` add: `A case of the PostgreSQL describer may hold an `explain-settings.txt`, one `name=value` on a line, which `FixtureCase.ExplainSettings` reads and both harnesses give the describer: a case whose name ends in `-hash`, `-merge` or `-nestloop` turns the other two join methods off.`

In the bullet that starts `**`Describing/Postgres/` is the PostgreSQL describer, and it is registered nowhere.**`:

- `until the describer is whole: a description lacks the walk.` becomes `until sub-phase 3.6 registers it.`
- `the columns built; the table match, which asks nothing; `check`; and `deallocate` in the `finally`.` becomes `the columns built; `check`; for a gated query with rows, `explain`, whose plan `NullabilityWalk` walks; the table match, which asks nothing; and `deallocate` in the `finally`.`

In the bullet that starts `**A column is `nullable` `false` only when`:

- The bold sentence becomes `**A column is `nullable` `false` only when the catalog says `NOT NULL`, its origin is a table, and either the query is not gated or the walk proves that no outer join null-extends its relation instance** (`ColumnNullability`, `NullabilityWalk`).`
- `which come back `NULL` while the row description keeps the table's origin, with no outer join in the plan.` becomes `which come back `NULL` while the row description keeps the table's origin.`
- Its last sentence becomes `` `plan` is `not-needed` for a query with rows that is not gated, `walked` for a gated one whose plan the walk used, and `unavailable` for one it could not: the `reason` of `postgres.walk` says why. ``

In the bullet that starts `**The table match is `TableMatcher``:

- `after the columns are built, and sub-phase 3.5's walk goes before it:` becomes `after `check` and after the walk:`
- `while one over a view, a materialized view, a foreign table or a table whose columns are all nullable still matches, until sub-phase 3.5's walk answers for it;` becomes `when the walk finds that a join extends the table or cannot answer, while one whose table the join keeps matches;`
- `a statement with rows whose columns were all built writes one, between its `column` events and `check`.` becomes `a statement with rows whose check agreed writes one, after `check` and the events of the walk.`

In the bullet that starts `**The calls and their records are a contract with the captures.**`, the list of methods becomes `` `open`, `prepare`, `columns`, `tables`, `types`, `check`, `explain` and `deallocate` ``.

In the bullet that starts `**The connection is made inside a `live` function and nowhere else.**`, after `and makes every command and every batch;` add `` and the transaction of `explain`, through `PostgresConnection.BeginAsync`; ``.

Add these bullets after the bullet of the table match:

```markdown
- **The plan of a gated query is a generic one, through the prepared statement** (`PostgresLive.ExplainAsync`).  `explain` runs `BEGIN`, `SET LOCAL plan_cache_mode = force_generic_plan`, a `SET LOCAL` for each setting of the request, `EXPLAIN (VERBOSE, FORMAT JSON) EXECUTE <name>(NULL, ...)` and `ROLLBACK`, on every version.  `EXPLAIN (GENERIC_PLAN)` of PostgreSQL 16 is not used: through Npgsql, with the parameters bound as nulls, 18.4 folded `u.id = $1` into a `Result` with `One-Time Filter: false`.  `EXPLAIN` without `ANALYZE` plans and does not run, and disposing the transaction rolls it back when a step failed, so the session is left as it was found.  An `explain` that fails is logged and never reported: `plan` is `unavailable` and the heuristic stands.  It is asked after `check` and for a gated query with rows alone, so a query that is not gated has the capture it had.
- **The walk follows a join's sides and nothing else** (`PlanInstances`).  A child whose `Parent Relationship` is `Outer` or `Inner` is a side of a join or the one input of another node; an `InitPlan`, a `SubPlan`, a `Member` and a `Subquery` are other plans, since a CTE's plan hangs under whichever node first needs it, and their scans are no instances of this plan's joins.  A relation instance is an `Alias` on a scan of a relation, the seven node types of `PlanInstances.RelationScans`, and it is null-extended when a join on the path from the root is `Left` or `Anti` with the instance under its `Inner` child, `Right` or `Right Anti` under its `Outer`, or `Full`.  An anti join is not only a `NOT EXISTS`: the planner makes one of `a LEFT JOIN b ... WHERE b.x IS NULL`, keeps `b`'s columns in its `Output` and returns them `NULL`, as `Anti` on 14 and `Right Anti` on 18.  A `LEFT JOIN` the planner swapped is a `Right` join with the sides swapped, and one it reduced is `Inner`: the rule needs neither told apart.  A join type outside the eight of `PlanInstances.ExtendsOf`, an alias found twice, a `Grouping Sets` key or a `MixedAggregate` makes the plan unusable, and so does a root without an `Output` for every column, a `UNION`'s `Append` for one.
- **A `ModifyTable` at the root is its target, and the scan of the target under it is that same instance.**  An `UPDATE` and a `DELETE` scan their target under the `ModifyTable` with the `ModifyTable`'s own alias, on 14 and 18: read as a second instance it would be a doubled alias, and every `RETURNING` of one would be unusable.  The target is never null-extended, whatever join it is under, since the row a `RETURNING` list gives is the one the statement acted on; the source of a `MERGE` is extended as its join says.  PostgreSQL 18 prints the old and the new row as `old.id` and `new.id`, or under the names `WITH (OLD AS o, NEW AS n)` gives them, which the server refuses when a table of the statement has the name: so an alias the walk finds in the plan is a relation instance, and an `OLD` or `NEW` column is unmappable and keeps the heuristic.
- **An entry of `Output` is a reference or it is nothing** (`OutputReference`).  `alias.column`, each part a name PostgreSQL prints bare, lower-case letters, digits and underscores, or in double quotes with `""` for a quote; or one bare name.  Anything else is an expression and its column keeps the heuristic: `"left"((u.name)::text, 1)` starts as a quoted name does and is none.  A bare name resolves only when the root is itself a relation instance, a scan, or a `ModifyTable` with no join under it: a `Result` that replaced a scan the planner proved empty prints bare names too, beside a scan that remains, and under a `ModifyTable` too, in its `RETURNING` list.  A reference to a `Subquery Scan` or a `CTE Scan` is unmappable and is not matched to the subquery's own plan by name: that plan's `Output` is positional expressions, the subquery's column names are nowhere in it, and a column renamed to another's name would be read as the other.  A subquery the planner flattens has no such scan and is walked exactly.  `docs/tech-debt/TD-0046-the-walk-leaves-some-columns-to-the-heuristic.md` records what mapping one would take.
- **The walk degrades a column alone, and the plan whole** (`NullabilityWalk`).  An entry that cannot be mapped leaves its own column to the heuristic and the plan `walked`; a plan that cannot be used leaves every column to it and is `unavailable`.  A mapped column that nothing extends takes `ColumnNullability.Of` for a query that is not gated, so the rule of a `NOT NULL` is written once and a view's column stays `null` with `view`; one that a join extends is `true` with `outer-join`, a view's too.  A column without an origin is not looked up.  The walk never answers `false` on its own: when in doubt it is the heuristic.
- **The settings of the planner are for the tests, and the list is closed** (`PlannerSettings`).  `PostgresDescriber` takes them at construction and every `explain` of its sessions carries them; `ToolHost.Create` makes none, and when sub-phase 3.6 registers the describer it is with the constructor that takes none.  A name outside `enable_hashjoin`, `enable_mergejoin`, `enable_nestloop`, `enable_memoize` and `enable_material`, or a value that is not `on` or `off`, throws at construction and again where the text of the `SET LOCAL` is made, so nothing else reaches one.  The settings are in the request, so a capture recorded with them is replayed with them.
- **Every fixture case gives one answer on PostgreSQL 14 and on 18.**  `FixtureCasesTests` describes each case on both and compares with the one `expected.json`.  A shape the two plan differently, `ON ... AND o.id IS NULL`, which 17 and later prove false from the `NOT NULL`, is a test of `tests/SqlSource.Tests.Postgres/Describer/WalkTests.cs` with each version's answer, and not a fixture.  `tests/SqlSource.Tool.Tests/Fixtures/WalkMatrixTests.cs` holds what each `walk-*` case must give, by hand: a recording that disagrees with it is a bug to find, and the table is not changed to fit one.
```

In the bullet that starts `**`tests/SqlSource.Tool.Tests/PostgresScript.cs` answers the session with no server.**`, add at its end: `` `tests/SqlSource.Tool.Tests/PostgresPlans.cs` writes a plan as a server would answer `explain`, a scan, a join of two nodes and a node over one, for the tests of the walk and of the session. ``

Run: `wc -l src/SqlSource.Tool/AGENTS.md`
The file is past the 250 lines its own rule gives a folder's file; it was before this task.  Do not cut it here.

- [ ] **Step 2: `tests/AGENTS.md`**

In the bullet `**Tests mirror the source folders.**`, `` `PostgresScript.cs` and `PostgresRows.cs` are among the helpers there `` becomes `` `PostgresScript.cs`, `PostgresRows.cs`, `PostgresTables.cs` and `PostgresPlans.cs` are among the helpers there ``.

- [ ] **Step 3: The remark of the gate**

In `src/SqlSource.Tool/Describing/Postgres/OuterJoinGate.cs`, the remark's sentence `Each keeps the table's origin in the row description, and none has an outer join in its plan.` becomes:

```csharp
/// Each keeps the table's origin in the row description.  The walk answers for them as for any gated query: a
/// <c>MERGE</c> plans as a join that null-extends its source, and the old and the new row print under names that
/// are no relation instance's, so their columns keep the heuristic.
```

Reflow the lines of the remark to the file's width.

- [ ] **Step 4: The tech debt**

In `docs/tech-debt/README.md`, `Next id: `TD-0046`` becomes `Next id: `TD-0047``, and the table gains, after the row of TD-0045:

```markdown
| [TD-0046](TD-0046-the-walk-leaves-some-columns-to-the-heuristic.md) | Open | 2026-10-13 | Low | The nullability walk leaves a column to the heuristic when it comes through a subquery or a CTE the planner kept, from a partitioned or inherited table, or from a foreign table, and asks for a plan that it has no use for |
```

Create `docs/tech-debt/TD-0046-the-walk-leaves-some-columns-to-the-heuristic.md`:

```markdown
# TD-0046 - The walk leaves some columns to the heuristic

## Problem

The nullability walk of the PostgreSQL describer ([`NullabilityWalk`](../../src/SqlSource.Tool/Describing/Postgres/NullabilityWalk.cs), [`PlanInstances`](../../src/SqlSource.Tool/Describing/Postgres/PlanInstances.cs)) gives a column of a gated query the catalog's answer only when the column's entry of the plan's `Output` names a relation instance.  Four kinds of column never do, and stay `true` with `heuristic` where the server can never return `NULL` for them:

- **A column through a `Subquery Scan` or a `CTE Scan`.**  A subquery with a window function, a `LIMIT` the planner keeps apart, a `MATERIALIZED` CTE or one that is referenced twice is planned by itself, and the outer plan prints `s.total`, where `s` is the scan and no relation.  The fixture cases `walk-subquery-kept` and `walk-cte-materialized` show it.
- **A column of a partitioned table or of an inheritance parent.**  It scans as an `Append` of `Member` children, whose aliases are `p_1` and `p_2` while the `Output` above prints `p.id`.  The planner prunes at run time, so a generic plan keeps the `Append` unless the table has one partition.
- **A column of a foreign table**, whose `Foreign Scan` is no scan of the seven kinds: it stays `null` with `view` where a join extends it, and is not `outer-join`.
- **A column of an extension's `Custom Scan`**, Citus and TimescaleDB for two.
- **A bare name in the `RETURNING` list of an `UPDATE` or a `DELETE` with any join under its `ModifyTable`**, a `WHERE id IN (...)` that plans as a semi join for one.  A bare name there may be a column of a relation the planner proved empty under an outer join, so the walk takes it for the target's only with no join under the `ModifyTable`.  Only a join that null-extends can hide such a relation, and the rule could narrow to those.

Two more things are not whole:

- **A plan is asked for when it cannot change anything.**  The gate reads words, so `left(name, 3)` gates a query with no join, and a gated query none of whose columns has an origin is asked for its plan all the same.  Each costs one `EXPLAIN` in a transaction.
- **`postgres.column` holds the heuristic's answer for a gated query.**  The event is written when the column is built, before the plan is asked for; the answer the sidecar records is in `postgres.walk.column`.

## Why it exists

The epic planned to map a subquery scan's outputs to the child plan's by position.  The spike found that the scan's `Output` lists the subquery's columns by name, and the child plan's `Output` is positional expressions, `o.total`: the names of the subquery's columns are nowhere in the plan.  Matching by name is unsafe, since `SELECT o.note AS total, o.total AS note` would be read crossed, and a `false` that is wrong is the one thing the walk must never give.  A partitioned table was left because an `Append` is also what a `UNION` plans as, and telling the two apart needs the `Relation Name` of every member and the catalog's `pg_inherits`.  The plan is asked for every gated query because the spec says so, and the cost is one round trip.

## Impact

Low.  A column that is `true` with `heuristic` is a nullable property in a generated model where a non-nullable one would do: never wrong, sometimes a `!` or a check the user did not need.  A project whose tables are all partitioned gets nothing from the walk for the columns of the table an outer join keeps.

## Proposed fix

For a subquery scan: the child plan of a `Subquery Scan` is its one `Subquery` child, and of a `CTE Scan` the `InitPlan` whose `Subplan Name` is `CTE <CTE Name>`, both of which `PlanNode` already reads.  The scan's `Output` and the child's are in the same order when the scan projects nothing away, which a test must establish for each shape before any code trusts it; where it holds, the `n`th entry of the scan maps to the `n`th of the child, which is walked in turn with the joins above the scan added to its path.  Where the order cannot be proved the column stays unknown.  For a partitioned table: an `Append` whose every `Member` is a scan of a relation that `pg_inherits` says is a child of one parent is an instance of that parent under the parent's alias, the members' alias with its `_n` cut.  That needs one more catalog call.  For a foreign table: add `Foreign Scan` to `PlanInstances.RelationScans`, with a container test against `postgres_fdw`.  For the plan nobody needs: skip `explain` when no column has an origin in a table or a view, and record `plan` as `not-needed`.  The event needs none: a member is never changed once a reader has it.

## Trigger

A user's report of a column they expected to be non-nullable, with a bundle of phase 10 that shows a `Subquery Scan` or an `Append`; phase 5, when the models make the cost of a nullable property visible; a project on a partitioned schema.
```

- [ ] **Step 5: The epic outline**

In `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`:

The row `| 3.5 The nullability walk | In progress |` becomes `| 3.5 The nullability walk | Done |`.

In Settled, the bullet `**The gate has three words more than sub-phase 3.3's spec lists**` ends `Sub-phase 3.5's walk cannot answer a query gated by one of them, since its plan holds no outer join, so such a query keeps the heuristic.`  That sentence becomes: `Sub-phase 3.5's walk answers a query gated by one of them as any other, and a bullet below says how.`

In the bullet `**The gate has ten words.**`, nothing changes.

In the bullet `**Bare column names**`, `a bare name resolves only when the root is itself a scan of a relation.` becomes `a bare name resolves only when the root is itself a scan of a relation, or a `ModifyTable` with no join under it, whose bare names are its target's: a `RETURNING` of a relation proved empty under an outer join prints a bare name too.`

Add at the end of Settled:

```markdown
- **A `ModifyTable` sits over a scan of its own target**, with the same alias, on 14 and 18.  Sub-phase 3.5's spec was amended before its plan: the root's `ModifyTable` is the target instance with an empty path, the scan under it with that alias is the same instance and no doubled alias, and the target is never null-extended, since the row a `RETURNING` list gives is the one the statement acted on.  Any other alias found twice still makes the plan unusable.
- **A query gated by `OLD`, `NEW` or `MERGE` is walked.**  A `MERGE` with `WHEN NOT MATCHED BY SOURCE` plans as a `Left` join that null-extends its source, with `WHEN NOT MATCHED BY TARGET` as a `Right` one that extends the target's scan, with both as a `Full` one: the source's columns of a `RETURNING` list take the join's answer and the target's the catalog's.  PostgreSQL 18 prints the old and the new row as `old.id` and `new.id`, or under the names `WITH (OLD AS o, NEW AS n)` gives them, and refuses such a name when a table of the statement has it: so an alias found in the plan is a relation instance, and a column of the old or the new row is unmappable and keeps the heuristic.
- **An `Anti` join null-extends its inner side, and a `Right Anti` its outer.**  This outline and sub-phase 3.5's spec said an anti join extends nothing, which holds for a `NOT EXISTS`.  The planner also makes an anti join of `a LEFT JOIN b ... WHERE b.x IS NULL`, and its `Output` still holds `b.id`, `NULL` in every row: a `NOT NULL` column would have been `false` there.  The review of the walk found it, the spec was amended, and the fixture `walk-anti` holds it on 14, where the join is `Anti`, and on 18, where it is `Right Anti`.  A semi join shows nothing of its hidden side and extends nothing.
- **The table match is made after `check` and the walk**, where sub-phase 3.4 made it before `check`: its last check reads the nullability the walk set.  It asks nothing, so no capture changed.  A query whose `check` fails writes no `postgres.table-match` event.
- **A fixture case gives one answer on 14 and on 18**, since each is described on both against one `expected.json`.  The matrix's `ON ... AND o.id IS NULL` does not: 17 and later prove it false from the `NOT NULL` and print a bare name from a `Result`, `heuristic`, where 14 scans the table, `outer-join`.  The fixture `walk-proved-empty` is `AND 1 = 0`, which both prove, and the `IS NULL` shape is a container test.  Of the other rows of the matrix: a view over an outer join is walked only when the query holds a gate word of its own, so `walk-view` joins the view; the merged column of a `USING` or a `NATURAL` join has no origin only when the join is `FULL`; and a `UNION`'s columns have no origin, so `walk-union` is `unavailable` with nothing for the heuristic to answer.
- **The `relation` of `postgres.walk.node` is a map of `schema` and `table`**, as the `table` of `postgres.table-match` is, where sub-phase 3.5's spec says `schema.name`.  `postgres.walk.column` is written for every column of a gated query, and `postgres.walk` always holds `reason`, null when the plan was walked.  `postgres.column` holds the heuristic's answer for a gated query.
- **`src/SqlSource.Tool/README.md` says nothing of `plan`**, so the widened `unavailable` is recorded here and in `src/SqlSource.Tool/AGENTS.md` alone.
```

Read the rest of the outline for a sentence this sub-phase made wrong: search it for `walk`, `GENERIC_PLAN`, `by position`, `unavailable` and `Anti`, and correct what you find where it stands.

- [ ] **Step 6: The version**

Run: `git fetch --tags origin && git tag --list 'v*' --sort=-v:refname | head -n 1`
Expected: nothing.  The repository has no `v*` tag, so there is nothing to compare `VersionPrefix` with.  If a tag is listed and `VersionPrefix` in `Directory.Build.props` is not greater, stop and ask the user for the new version.

- [ ] **Step 7: Check that no document names what is gone**

Run: `grep -rn "sub-phase 3.5's walk\|until the walk exists\|lacks the walk" src tests docs/tech-debt docs/deferred`
Expected: nothing.

Run: `grep -rn "Until the walk\|the walk does not exist" src tests`
Expected: nothing.

Each line either command finds is a comment or a document this sub-phase made wrong: correct it where it stands.  `docs/tech-debt/TD-0044` may speak of the walk as to come; bring it to what is.

- [ ] **Step 8: Validate and commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add src/SqlSource.Tool/AGENTS.md tests/AGENTS.md src/SqlSource.Tool/Describing/Postgres/OuterJoinGate.cs docs/tech-debt docs/superpowers/specs/2026-10-07-query-generation-epic-design.md
git commit -m "$(cat <<'EOF'
Document the nullability walk, and close sub-phase 3.5

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

Add to that commit any other file Step 7 changed.

- [ ] **Step 9: The pull request**

The pull request is opened by the finishing skill, not by this plan.  When it is: its description ends with the line the session gives for one, the user's instruction is to turn Auto-fix on for it, and every CodeRabbit finding is answered in its thread, as `AGENTS.md` requires.
