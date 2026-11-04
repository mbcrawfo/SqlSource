# Test Variants in Their Files Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** No `.sql` file in `tests/DatabaseTests` (outside `Realistic/`) is claimed twice with different settings that pull request 1 removes from the attribute: each such variant becomes a query of its own in the same file, with the query-level marker that makes it a variant, and the tests call it by its new name.

**Architecture:** Test-only refactor, no product code.  One commit adds every variant query to its file and rewrites the sidecars in one run of `tools/describe-end-to-end.sh`.  The build stays green with the old claims still in place, so later commits switch each area's tests to the copies and delete the extra claims without touching a sidecar.

**Tech Stack:** C# 14 / .NET 10, xunit v3 on Microsoft.Testing.Platform, Shouldly, Dapper, Npgsql 10 and 8, Microsoft.Data.SqlClient 7 and 6.1, Testcontainers, Docker.

**Spec:** `docs/superpowers/specs/2026-11-03-file-owned-settings-design.md`, section "Test refactoring first", "Pull request A: variants move into the files".

## Global Constraints

- No file under `src/`, `docs/manual/`, `README.md`, `examples/` or any `Realistic/` folder changes.  The realistic corpus is pull request B.
- "Each of the two refactors below uses only what works today, changes no product code, and lands on `main` before pull request 1" (spec).
- Every sidecar is written by `tools/describe-end-to-end.sh` alone, never by hand (`tests/DatabaseTests/*/EndToEnd/AGENTS.md`).  It needs Docker, runs both engines and the examples every time, and takes no arguments.
- Images are untouched: `DefaultImages.Postgres`, `DefaultImages.SqlServer`, the floor images `postgres:14.24` and `mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04` (root `AGENTS.md` rule 5).
- Every commit passes `./pre-commit-validation.sh`, run after the last edit.  Before the push, `./pre-commit-validation.sh --full` on the exact tree, plus `tests/DatabaseTests/Postgres` and `tests/DatabaseTests/SqlServer` on their floor images (`tests/DatabaseTests/AGENTS.md`: "A change here is run on both of the engine's images before it is pushed").
- Docs are kept true in the same PR: each README and AGENTS.md that names a removed class or describes "a second claim" changes in the task that removes it.  AGENTS.md bullets stay at most 80 words (`AgentsFileTests`).
- This plan and the spec are committed with the work (`docs/superpowers/AGENTS.md`).
- Work on a branch cut from `origin/main`, for example `claude/test-variants-in-files`.  Main has moved past this worktree's base (#219 edited `Postgres/README.md` and `SqlServer/README.md`), so cite rows by file name, not by line.

### Decisions

**Names of the copies.**  A copy is named `<Original><Variant>` after the conventions already in the corpus:

| Variant | Suffix | Precedent |
|----|----|----|
| `no-array-pool` | `Unpooled` | `SearchAccountsUnpooled` in `SqlServer/EndToEnd/Queries/Shapes.sql`; the hand-written `*UnpooledByHandAsync` of the allocation tests |
| `-- output: models`, for Dapper | `ForDapper` | none; the old claim classes are `*DapperQueries` |
| `-- collection-type: X` | `As<X>` | `NumbersAs<X>` in both `RowBuffers.sql` |
| `async-method-suffix` | `WithSuffix`, so the method is `<Original>WithSuffixAsync` | none |

**What a copy holds.**  The original's SQL unchanged and every marker of the original but `-- name:` and `-- summary:`, so the copy is described and typed as the original is.  On top of that:
- A summary: `-- summary: <Original> again, <reason>.` (for example "ListAllTypes again at the output models, whose constant the Dapper tests run.").
- The variant's marker.  A copy's `-- generator:` list is the original's list plus the variant's word, since a list replaces the one below it whole (`docs/manual/settings.md`, "Which place wins").
- `-- input-model:` and `-- output-model:` naming the original's models, unless the model is a table model (`SELECT *`, `RETURNING *`, `OUTPUT INSERTED.*`), which the copy shares by itself.  So the tests keep their model types (`docs/manual/generator.md#shared-models`).
- It sits directly after its original.

**Where the Dapper copies' constants go.**  A `models` query's constant goes to the private nested class `Sql` unless the claim has `SqlLocation = SqlLocation.Direct` (`docs/manual/settings.md#setting-sql-location`).  The tests are outside the type, so the single claim needs `Direct`.  The Postgres claims `AllTypesQueries`, `NoteQueries` and `Postgres.Npgsql8/AllTypesQueries`, and SQL Server's `NoteQueries`, have it already.  SQL Server's `AllTypesQueries`, `NewTypesQueries` and `WriteQueries` gain it.  `SqlLocation` stays on the attribute until pull request 2, which turns it into `-- member-location: class`.

**Only the queries a test runs get a copy.**  Dapper runs `ListAllTypes` and `InsertAllTypes` (both engines), Postgres `ListNoteKinds`, SQL Server `ListNewTypes`, `AddNote` and `ListMarkedNames`.  SQL Server's `RenameAccount` is only asserted as a constant by `ModelShapeTests`, so that assertion moves to `AddNoteForDapper` and `RenameAccount` gets no copy.  The allocation tests run `GetAccountName`, `ListAccounts` and `ListClosedBefore` unpooled.

**The 17 copies, 17 new sidecar entries** (6 PostgreSQL, 11 SQL Server; each is a new describe, and `NewTypes.sql` runs on 2025 alone):

| File (under `tests/DatabaseTests/`) | Original → copy | Markers beyond the original's |
|----|----|----|
| `Postgres/EndToEnd/Queries/Accounts.sql` | `GetAccountName` → `GetAccountNameUnpooled` | `-- generator: no-array-pool`, `-- input-model: GetAccountNameArgs`, `-- output-model: AccountName` (the original's own) |
| same | `ListAccounts` → `ListAccountsUnpooled` | `-- generator: no-array-pool` (table model `AccountsDto`) |
| same | `ListClosedBefore` → `ListClosedBeforeUnpooled` | `-- generator: no-array-pool`, `-- input-model: ListClosedBeforeArgs`, `-- output-model: ListClosedBeforeDto` |
| `Postgres/EndToEnd/Queries/AllTypes.sql` | `ListAllTypes` → `ListAllTypesForDapper` | `-- output: models` (table model `AllTypesDto`) |
| same | `InsertAllTypes` → `InsertAllTypesForDapper` | `-- output: models`, `-- input-model: InsertAllTypesArgs` |
| `Postgres/EndToEnd/Queries/Notes.sql` | `ListNoteKinds` → `ListNoteKindsForDapper` | `-- output: models`, `-- output-model: ListNoteKindsDto` |
| `SqlServer/EndToEnd/Queries/AllTypes.sql` | `ListAllTypes` → `ListAllTypesForDapper` | `-- output: models` |
| same | `InsertAllTypes` → `InsertAllTypesForDapper` | `-- output: models`, `-- input-model: InsertAllTypesArgs` |
| `SqlServer/EndToEnd/Queries/NewTypes.sql` | `ListNewTypes` → `ListNewTypesForDapper` | `-- output: models` (table model `NewTypesDto`) |
| `SqlServer/EndToEnd/Queries/Writes.sql` | `AddNote` → `AddNoteForDapper` | `-- output: models`, `-- input-model: AddNoteArgs`, `-- output-model: AddNoteDto` |
| `SqlServer/EndToEnd/Queries/Notes.sql` | `ListMarkedNames` → `ListMarkedNamesForDapper` | `-- output: models`, `-- output-model: ListMarkedNamesDto` |
| `SqlServer/EndToEnd/Queries/Collections/Claimed.sql` | `ListCollectedAccounts` → `ListCollectedAccountsAsIEnumerable`, `…AsICollection`, `…AsIReadOnlyCollection`, `…AsList`, `…AsImmutableList` | `-- collection-type: IEnumerable` (and `ICollection`, `IReadOnlyCollection`, `List`, `ImmutableList`); `-- output-model: CollectedAccount` is the original's |
| `SqlServer/EndToEnd/Queries/Locations.sql` | `ListAccountNames` → `ListAccountNamesWithSuffix` | `-- generator: async-method-suffix`, `-- output-model: ListAccountNamesDto` |

**Not in this PR**, with the reason:
- *Double claims that differ only by `MethodLocation` or `SqlLocation`.*  These are `Postgres/EndToEnd/LocationQueries.cs` (2 claims of `Locations.sql`), `SqlServer/EndToEnd/LocationQueries.cs` (`LocationQueries`, `PublicLocationQueries`, `InternalLocationQueries`, `PrivateLocationQueries`) and `Postgres/EndToEnd/Reporting/AccountReports.cs` (`SqlLocation.Direct`, against `AccountQueries`' default, on an all-`codegen` file).  No query-level marker for them exists today, and both properties survive pull request 1.  Pull request 2 folds them into `-- visibility:` / `-- member-location:` copies, as the spec's section "Test claims that differ only by placement" says.  Record nothing about them in `docs/deferred`.
- *`Shapes.sql` claimed by `SqlServer/EndToEnd/ShapeQueries.cs` and `QueryMethodAllocation/SqlServer/ShapeQueries.cs`.*  The claims are in two projects, the same but for `ModelNamespace`, which pull request 1 removes along with the claim's other properties ("Every test that sets one of them").  It is not a variant.
- *Same-settings claims across projects* (`Json`, `NodaTime`, `NetTopologySuite`, `AllTypes` in `Postgres.Npgsql8`, `Streaming`, `RowBuffers`).  They are the same as the project they link from and give the same result under the new design.

## Review Focus

1. **A copy drifts from its original** (one marker missing, such as the `-- type-override: timestamptz = default` of Postgres `ListAllTypes` or `-- param: @closedBefore timestamptz null`).  Dapper or the allocation test would then run SQL other than the method's, or the shared model would split (SQLSRC257).  Expected: each copy's committed entry has its original's hash.  Pinned by `VariantCopyTests` in both engines (Task 1).
2. **The sidecar run changes more than the 17 entries**, through image drift or a nondeterministic answer on the realistic corpus or the examples.  Expected: only the nine touched `.sql.json` files change, each by its new entries.  Pinned by the diff check in Task 1, Step 5.
3. **The new entries differ on a floor major.**  `CheckTests` runs on SQL Server 2017 and PostgreSQL 14 too, and SQL Server's AGENTS.md says no entry may differ between majors.  Expected: the copies are their originals' SQL, so they check alike.  Pinned by the floor-image runs in Task 6.
4. **The allocation tests run only nightly** (`QueryMethodAllocation`), so a broken switch to the `Unpooled` copies would show only after merge.  Expected: they build with the solution and pass locally.  Pinned by Task 2, Step 4.
5. **A stale name survives** in a comment, README or AGENTS.md (`UnpooledAccountQueries`, `AllTypesSql`, `NoteSql`, `*DapperQueries`, `AllTypesMethodQueries`, `NewTypesMethodQueries`, the five `*CollectionQueries` variants, `SuffixLocationQueries`, `UnpooledCopyTests`, "second claim").  Expected: none outside `docs/superpowers/` and `Realistic/`.  Pinned by the grep in Task 6, Step 1.

---

### Task 1: Add the copies to the files, rewrite the sidecars, pin each copy to its original

**Files:**
- Modify: the nine `.sql` files in the copies table.  Also update the header comments that name claims: Postgres `AllTypes.sql` lines 1-2 (drop `AllTypesSql`, name the `ForDapper` copies), SQL Server `Collections/Claimed.sql` ("claimed by a class of CollectionQueries.cs for each" becomes one claim and a copy per collection type, each by its query's marker), SQL Server `Locations.sql` ("and of the suffix", which is now its own query).
- Modify (written by the script): the nine matching `.sql.json` sidecars.
- Modify: `tests/DatabaseTests/Postgres/RepositoryPaths.cs`.  Add `public static string EndToEndQueries { get; } = Path.Combine(EndToEnd, "Queries");`, as SQL Server's `RepositoryPaths` has.
- Create: `tests/DatabaseTests/Postgres/EndToEnd/VariantCopyTests.cs`
- Rename and modify: `tests/DatabaseTests/SqlServer/EndToEnd/UnpooledCopyTests.cs` → `VariantCopyTests.cs`
- Modify: `tests/DatabaseTests/Postgres/EndToEnd/ModelShapeTests.cs`, theory `Build_CodeGenOutput_GivesNoConstant`: remove `[InlineData(typeof(AllTypesQueries))]`, since `AllTypesQueries` (with `SqlLocation.Direct`) now holds the `ForDapper` constants.
- Modify: `tests/DatabaseTests/SqlServer/README.md`: line 7's list of tests that need no server, and "The tests" bullet for `UnpooledCopyTests`.  `tests/DatabaseTests/Postgres/README.md`: line 7's list and a bullet for `VariantCopyTests`.
- Modify: `tests/DatabaseTests/QueryMethodAllocation/SqlServer/AllocationTests.cs` header comment ("UnpooledCopyTests holds its SQL" becomes `VariantCopyTests`).

**Interfaces:**
- Produces: the 17 query names of the copies table, as generated members.  Codegen copies are extension methods in the claim's `<Type>Extensions`, or static methods on the type for a `MethodLocation.Public` claim.  `ForDapper` copies are `const string`s on a `Direct` claim, or in its private `Sql` class otherwise.
- Produces: `RepositoryPaths.EndToEndQueries` (Postgres).

- [ ] **Step 1: Write the failing tests.**  Both classes need no server and are in no collection, like `UnpooledCopyTests` today:

```csharp
public sealed class VariantCopyTests
{
    [Theory]
    [InlineData("Accounts.sql", "GetAccountName", "GetAccountNameUnpooled")]
    // ... one row per (file, original, copy) of this engine in the copies table;
    // SQL Server also keeps ("Shapes.sql", "SearchAccounts", "SearchAccountsUnpooled").
    public void Copy_CommittedEntry_HasTheHashOfItsOriginal(string file, string original, string copy)
    {
        var queries = /* JsonNode of Path.Combine(RepositoryPaths.EndToEndQueries, file + ".json"), ["queries"] */;
        queries[copy]!["hash"]!.GetValue<string>().ShouldBe(queries[original]!["hash"]!.GetValue<string>());
    }
}
```

The `Claimed.sql` rows pass `Path.Combine("Collections", "Claimed.sql")` as `file`.  The class comment carries over the old one's reason: a query's hash is of its SQL alone, and the project does not build while an entry's hash is stale.

- [ ] **Step 2: Run the tests and see them fail.**  The build still passes; each new row fails on a missing entry.
  Run: `dotnet test --project tests/DatabaseTests/Postgres -- --filter-class "SqlSource.DatabaseTests.Postgres.EndToEnd.VariantCopyTests"` and the same for `tests/DatabaseTests/SqlServer` with `SqlSource.DatabaseTests.SqlServer.EndToEnd.VariantCopyTests`.
  Expected: FAIL on every new row (Shapes' row passes).

- [ ] **Step 3: Add the 17 copies** as the copies table and "What a copy holds" say, and update the three header comments above.  Leave every existing query's text, markers and summary as it is.

- [ ] **Step 4: Rewrite the sidecars.**  With Docker running: `tools/describe-end-to-end.sh`.  Expected: it ends without error, and SQL Server runs under emulation on Apple silicon, which is slow.

- [ ] **Step 5: Read the diff.**  Run `git status --short -- '*.sql.json'`.  Expected: exactly the nine sidecars of the copies table.  In each, `git diff` shows only added entries named after the copies, and `toolVersion` and `serverVersion` do not change.  Anything else, a realistic case or an example, is drift: stop and report it, never commit it.

- [ ] **Step 6: Run the tests and see them pass.**  Use the same two commands as Step 2, then `dotnet test --project tests/DatabaseTests/Postgres -- --filter-class "SqlSource.DatabaseTests.Postgres.EndToEnd.ModelShapeTests"`.
  Expected: PASS.  The old claims still compile beside the copies; every class that claims a touched file gains the copies too.

- [ ] **Step 7: Validate and commit.**  Run `./pre-commit-validation.sh` and expect it to pass.
```bash
git add tests/DatabaseTests
git commit -m "test: each test variant of a query is a copy in its file"
```

### Task 2: PostgreSQL's unpooled accounts by their copies

**Files:**
- Modify: `tests/DatabaseTests/QueryMethodAllocation/Postgres/AllocationTests.cs` (the three `*_UnderNoArrayPool_AllocatesNoMoreThanByHand` tests and the header comment)
- Modify: `tests/DatabaseTests/QueryMethodAllocation/Postgres/AccountQueries.cs`.  Delete `UnpooledAccountQueries` and its comment; the comment of `AccountQueries` says its `Unpooled` copies are the ones measured under `no-array-pool`.
- Modify: `tests/DatabaseTests/Postgres/EndToEnd/AccountQueries.cs`.  Delete `UnpooledAccountQueries` and its comment, which presents a second claim as something the project keeps on purpose.
- Modify: `tests/DatabaseTests/QueryMethodAllocation/README.md` (the `Postgres/AllocationTests.cs` bullet: "its method of `UnpooledAccountQueries`" becomes "the method of its copy `<Query>Unpooled` in `Accounts.sql`, under `no-array-pool` by the copy's own marker")
- Modify: `tests/DatabaseTests/Postgres/README.md`.  In the `Accounts.sql` row, the claims are `AccountQueries` and `Reporting.AccountReports`.  Covers: "`no-array-pool` by a query's marker, on the copies `GetAccountNameUnpooled`, `ListAccountsUnpooled` and `ListClosedBeforeUnpooled` that `tests/DatabaseTests/QueryMethodAllocation` measures, into their originals' models".

**Interfaces:**
- Consumes: `GetAccountNameUnpooled`, `ListAccountsUnpooled`, `ListClosedBeforeUnpooled` (Task 1), extension methods of `AccountQueriesExtensions` with the originals' signatures.

- [ ] **Step 1: Switch the three tests to the copies.**  Each `UnpooledAccountQueries.X(connection, …)` becomes `connection.XUnpooled(…)`, with the same arguments and assertions.  Test names stay.  The header comment's "its method of UnpooledAccountQueries, the same query under no-array-pool" becomes "the method of its copy XUnpooled, the same query under no-array-pool by its own marker".
- [ ] **Step 2: Delete both `UnpooledAccountQueries` claims** and fix the comments above.
- [ ] **Step 3: Build.**  Run `dotnet build SqlSource.slnx` and expect success; `QueryMethodAllocation` builds with the solution.
- [ ] **Step 4: Run the Postgres allocation tests**, which only the nightly runs otherwise.
  Run: `dotnet test --project tests/DatabaseTests/QueryMethodAllocation -p:RunQueryMethodAllocation=true --filter-namespace SqlSource.DatabaseTests.QueryMethodAllocation.Postgres`
  Expected: all 18 pass.  An extension call is the same static call as before, so the margins hold.
- [ ] **Step 5: Update the two READMEs**, then run `./pre-commit-validation.sh` and expect it to pass.
- [ ] **Step 6: Commit**: `git commit -m "test: measure the unpooled accounts through their copies in Accounts.sql"`

### Task 3: PostgreSQL's Dapper tests by the `ForDapper` copies

**Files:**
- Delete: `tests/DatabaseTests/Postgres/EndToEnd/AllTypesSql.cs`, `tests/DatabaseTests/Postgres/EndToEnd/NoteSql.cs`, `tests/DatabaseTests/Postgres.Npgsql8/AllTypesSql.cs`
- Modify: `tests/DatabaseTests/Postgres/EndToEnd/AllTypesQueryTests.cs` (calls and the header comment at line 17), `DapperRecipeTests.cs`, `ModelShapeTests.cs`; `tests/DatabaseTests/Postgres.Npgsql8/AllTypesQueryTests.cs`
- Modify: the comments of `Postgres/EndToEnd/AllTypesQueries.cs`, `Postgres/EndToEnd/NoteQueries.cs` and `Postgres.Npgsql8/AllTypesQueries.cs`.  Each now also holds its file's `ForDapper` constants.
- Modify: `tests/DatabaseTests/Postgres/EndToEnd/AGENTS.md`, `tests/DatabaseTests/Postgres/README.md` (rows `AllTypes.sql` and `Notes.sql`, the `DapperRecipeTests` bullet, the `AllTypesQueryTests` paragraph), `tests/DatabaseTests/README.md` (the Npgsql 8 paragraph: "by the constant of `AllTypesSql`, a second claim of the file at the output `models`")

**Interfaces:**
- Consumes: `AllTypesQueries.ListAllTypesForDapper`, `AllTypesQueries.InsertAllTypesForDapper`, `NoteQueries.ListNoteKindsForDapper` (Task 1), `const string`, in both Postgres projects for `AllTypesQueries`.

- [ ] **Step 1: Switch every caller.**  `AllTypesSql.ListAllTypes` becomes `AllTypesQueries.ListAllTypesForDapper`, `AllTypesSql.InsertAllTypes` becomes `AllTypesQueries.InsertAllTypesForDapper` and `NoteSql.ListNoteKinds` becomes `NoteQueries.ListNoteKindsForDapper`.  The model types stay (`AllTypesDto`, `InsertAllTypesArgs`, `ListNoteKindsDto`).
- [ ] **Step 2: Rename the constant test in `ModelShapeTests`.**  `Build_AllTypesSqlWithADirectLocation_HasTheConstantsOnTheType` becomes `Build_ModelsCopiesWithADirectLocation_HaveTheConstantsOnTheType`, with the same three assertions on `AllTypesQueries.ListAllTypesForDapper` and `AllTypesQueries.InsertAllTypesForDapper`.
- [ ] **Step 3: Delete the three claim files.**  Then run `dotnet test --project tests/DatabaseTests/Postgres -- --filter-class "SqlSource.DatabaseTests.Postgres.EndToEnd.AllTypesQueryTests"`, the same for `DapperRecipeTests` and `ModelShapeTests`, and `dotnet test --project tests/DatabaseTests/Postgres.Npgsql8`.  Expected: PASS.
- [ ] **Step 4: Update the docs.**  In `Postgres/EndToEnd/AGENTS.md`, replace the bullet "A Dapper test runs the constants of a second claim…" with:
  - **A Dapper test runs the constant of a copy of its query in the same file, `<Query>ForDapper`, marked `-- output: models` and naming the original's models by `-- input-model:` and `-- output-model:` unless they are a table's**, on the file's one claim with `SqlLocation.Direct`, since a `codegen` query has a method and no constant.  `VariantCopyTests` holds each copy's SQL to its original's.
  - **A variant of a query, under another generator parameter, collection type or output, is a copy of it in its file, `<Query><Variant>`, never a second claim of the file**, since a file will give every claim the same settings.

  In the README rows, "and `AllTypesSql` at the output `models`, whose constants the Dapper tests run" becomes "whose copies `ListAllTypesForDapper` and `InsertAllTypesForDapper`, at the output `models`, the Dapper tests run by their constants".  `Notes.sql` follows the same pattern with `ListNoteKindsForDapper`.
- [ ] **Step 5: Validate and commit.**  Run `./pre-commit-validation.sh` and expect it to pass, then `git commit -m "test: Dapper runs the ForDapper copies of PostgreSQL's queries"`.

### Task 4: SQL Server's Dapper tests and the SqlClient 6.1 project by the `ForDapper` copies

**Files:**
- Delete: `tests/DatabaseTests/SqlServer/EndToEnd/DapperQueries.cs`
- Modify: `tests/DatabaseTests/SqlServer/EndToEnd/AllTypesQueries.cs`, `NewTypesQueries.cs` and `WriteQueries.cs`.  Add `SqlLocation = SqlLocation.Direct` and a comment that it puts the `ForDapper` constants on the type.
- Modify: `tests/DatabaseTests/SqlServer/EndToEnd/AllTypesQueryTests.cs` (calls and the comment at line 15), `NewTypesQueryTests.cs` (call and the comment at line 11), `DapperRecipeTests.cs` (calls and the comment at line 24), `ModelShapeTests.cs`
- Modify: `tests/DatabaseTests/SqlServer.SqlClient61/AllTypesQueries.cs`.  Keep two claims, `AllTypesQueries` of `AllTypes.sql` and `NewTypesQueries` of `NewTypes.sql`, each `[SqlSourceGenerate(Source = "../SqlServer/EndToEnd/Queries/<File>.sql", SqlLocation = SqlLocation.Direct)]` with no `Output`, which leaves the linked folder's `codegen`.  Delete `AllTypesMethodQueries` and `NewTypesMethodQueries` and rewrite the comments.
- Modify: `tests/DatabaseTests/SqlServer.SqlClient61/AllTypesQueryTests.cs` (two calls), `MethodAllTypesTests.cs` (the comment at line 14).  The methods are now in `AllTypesQueriesExtensions` and `NewTypesQueriesExtensions`; the tests call them as `connection.X(...)`, so they don't change.
- Modify: `tests/DatabaseTests/SqlServer/EndToEnd/AGENTS.md`, `tests/DatabaseTests/SqlServer.SqlClient61/AGENTS.md`, `tests/DatabaseTests/SqlServer/README.md` (rows `AllTypes.sql`, `NewTypes.sql`, `Notes.sql`, `Writes.sql`, and the paragraph after the table, "A file whose SQL a Dapper test runs is claimed a second time…"), `tests/DatabaseTests/README.md` (the SqlClient 6.1 paragraph: "claims each file twice: at the output `models`, for Dapper, and at `codegen`…")

**Interfaces:**
- Consumes: `AllTypesQueries.ListAllTypesForDapper`, `AllTypesQueries.InsertAllTypesForDapper`, `NewTypesQueries.ListNewTypesForDapper`, `WriteQueries.AddNoteForDapper`, `NoteQueries.ListMarkedNamesForDapper` (Task 1), each a `const string` once its claim is `Direct`.

- [ ] **Step 1: Make the three claims `Direct`.**  Then switch every caller: `AllTypesDapperQueries.X` becomes `AllTypesQueries.XForDapper`, `NewTypesDapperQueries.ListNewTypes` becomes `NewTypesQueries.ListNewTypesForDapper`, `WriteDapperQueries.AddNote` becomes `WriteQueries.AddNoteForDapper` and `NoteDapperQueries.ListMarkedNames` becomes `NoteQueries.ListMarkedNamesForDapper`.  In SqlClient 6.1, `AllTypesQueries.ListAllTypes` becomes `AllTypesQueries.ListAllTypesForDapper` and `NewTypesQueries.ListNewTypes` becomes `NewTypesQueries.ListNewTypesForDapper`.  The model types stay.
- [ ] **Step 2: Rename the constant test in `ModelShapeTests`.**  `Build_ModelsClaimWithADirectLocation_HasTheConstantsOnTheType` becomes `Build_ModelsCopiesWithADirectLocation_HaveTheConstantsOnTheType`.  Its comment reads "A query at the output models, on a claim with a direct location, has its constant on the type…".  The assertions:

```csharp
AllTypesQueries.ListAllTypesForDapper.ShouldBe("SELECT *\nFROM dbo.all_types\nORDER BY id;");
WriteQueries.AddNoteForDapper.ShouldBe(
    "INSERT INTO dbo.notes (account_id, kind, body, written_at)\nOUTPUT INSERTED.id, INSERTED.kind\n"
        + "VALUES (@accountId, @kind, @body, @writtenAt);"
);
```

- [ ] **Step 3: Delete `DapperQueries.cs`** and the two `*MethodQueries` claims of SqlClient 6.1.  Then run `dotnet test --project tests/DatabaseTests/SqlServer -- --filter-class` for each of `SqlSource.DatabaseTests.SqlServer.EndToEnd.AllTypesQueryTests`, `NewTypesQueryTests`, `DapperRecipeTests` and `ModelShapeTests`, and `dotnet test --project tests/DatabaseTests/SqlServer.SqlClient61`.  Expected: PASS.
- [ ] **Step 4: Update the docs.**  In `SqlServer/EndToEnd/AGENTS.md`, the bullet "A query runs through its generated method…" keeps its first sentence.  Its second becomes the two bullets of Task 3, Step 4, with `EndToEnd/` paths.  In `SqlServer.SqlClient61/AGENTS.md`, "Its two claims at `models` name that output on their attribute, since the end-to-end project's is `CodeGen`" becomes "**It claims each linked file once, with `SqlLocation.Direct`**, so that Dapper reads by the constants of the file's `ForDapper` copies, and the methods of the file's `codegen` queries are in `<Type>Extensions`."  In the README rows, `and AllTypesDapperQueries at models` (and the like) becomes "and Dapper by its `<Query>ForDapper` copy at `models`".
- [ ] **Step 5: Validate and commit.**  Run `./pre-commit-validation.sh` and expect it to pass, then `git commit -m "test: Dapper runs the ForDapper copies of SQL Server's queries"`.

### Task 5: SQL Server's collection types and method suffix by their copies

**Files:**
- Modify: `tests/DatabaseTests/SqlServer/EndToEnd/CollectionQueries.cs`.  `Claimed.sql` keeps one claim, `CollectionQueries` with `MethodLocation.Public`.  Delete `EnumerableCollectionQueries`, `CollectionInterfaceCollectionQueries`, `ReadOnlyCollectionQueries`, `ListCollectionQueries` and `ImmutableListCollectionQueries`, and rewrite the top comment: there is no attribute level any more, and the methods stay on the type.
- Modify: `tests/DatabaseTests/SqlServer/EndToEnd/MethodCollectionTests.cs` (five tests and the header comment, "set by the project's settings file, the attribute, the file header and a query's marker")
- Modify: `tests/DatabaseTests/SqlServer/EndToEnd/LocationQueries.cs`.  Delete `SuffixLocationQueries`; the top comment drops "and the suffix of the generator parameter async-method-suffix".
- Modify: `tests/DatabaseTests/SqlServer/EndToEnd/MethodLocationTests.cs` (the suffix test and the header comment at line 10)
- Modify: `tests/DatabaseTests/SqlServer/README.md` (rows `Collections/…` and `Locations.sql`)

**Interfaces:**
- Consumes: `CollectionQueries.ListCollectedAccountsAs{IEnumerable,ICollection,IReadOnlyCollection,List,ImmutableList}` and `PublicLocationQueries.ListAccountNamesWithSuffixAsync` (Task 1), static methods taking `(DbConnection connection, …, CancellationToken cancellationToken)`.

- [ ] **Step 1: Switch the five collection tests.**  Each test's name becomes `<Copy>_<Type>ByTheQuerysMarker_<its old tail>`, for example `ListCollectedAccountsAsIEnumerable_IEnumerableByTheQuerysMarker_HoldsAnArray`.  Each calls `CollectionQueries.<Copy>` and passes `typeof(CollectionQueries)` and `nameof(CollectionQueries.<Copy>)` to `ShouldHold`, whose type arguments stay.
- [ ] **Step 2: Switch the suffix test.**  It becomes `ListAccountNamesWithSuffixAsync_AsyncMethodSuffixByTheQuerysMarker_EndsTheNameInAsync`, which:

```csharp
var accounts = await PublicLocationQueries.ListAccountNamesWithSuffixAsync(connection, cancellationToken: ...);
accounts.ShouldBe(Accounts);
typeof(PublicLocationQueries).GetMethod("ListAccountNamesWithSuffix", Statics).ShouldBeNull();
```

- [ ] **Step 3: Delete the six claims** (five collection, one suffix).  Then run `dotnet test --project tests/DatabaseTests/SqlServer -- --filter-class "SqlSource.DatabaseTests.SqlServer.EndToEnd.MethodCollectionTests"` and the same for `MethodLocationTests`.  Expected: PASS.
- [ ] **Step 4: Update the README rows.**  `Collections/…` is "by the project's settings file, the file header of each of two files and a query's marker, the last on the copies of `ListCollectedAccounts` in `Claimed.sql`, claimed by `CollectionQueries`".  `Locations.sql` is "Each `MethodLocation` but by the attribute alone, a private method called by the type's own code, and `async-method-suffix` by the marker of the copy `ListAccountNamesWithSuffix`".
- [ ] **Step 5: Validate and commit.**  Run `./pre-commit-validation.sh` and expect it to pass, then `git commit -m "test: SQL Server's collection and suffix variants are copies in their files"`.

### Task 6: Sweep, full validation, floor images, review

**Files:** whatever the sweep finds; no new ones.

- [ ] **Step 1: Sweep for stale names.**

```bash
grep -rnE 'UnpooledAccountQueries|AllTypesSql\b|NoteSql\b|(AllTypes|NewTypes|Write|Note)DapperQueries|DapperQueries\.cs|AllTypesMethodQueries|NewTypesMethodQueries|(Enumerable|CollectionInterface|ReadOnly|List|ImmutableList)CollectionQueries|SuffixLocationQueries|UnpooledCopyTests|second claim|claimed (a second time|twice)|claims each file twice' \
  --include='*.cs' --include='*.md' --include='*.sql' --include='*.csproj' --include='*.txt' --include='*.sh' --include='*.yml' . \
  | grep -v -e '/bin/' -e '/obj/' -e 'docs/superpowers/' -e '/Realistic/'
```

Expected: only the matches that stay, which are:
- the realistic corpus's "claimed twice" in the realistic sections of `tests/DatabaseTests/Postgres/README.md` and `tests/DatabaseTests/SqlServer/README.md`, and its "claimed a second time" in `docs/architecture/sqlserver-describer.md`, unless pull request B has merged and removed them;
- the summary of `SearchAccountsUnpooled` in `SqlServer/EndToEnd/Queries/Shapes.sql` ("so a second claim could not give it one"), which is still true while `Parameters` exists.

Fix any other match.

- [ ] **Step 2: Check the version.**  Run `git fetch --tags origin`, then compare `VersionPrefix` in `Directory.Build.props` (0.1.0) with `git tag --list 'v*' --sort=-v:refname | head -n 1`.  Expected: no tag, so nothing to compare.  Otherwise ask the user (root `AGENTS.md`, "Pull requests" 1).
- [ ] **Step 3: Full validation.**  Run `./pre-commit-validation.sh --full` and expect it to pass.
- [ ] **Step 4: Run the floor images.**
  - `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/DatabaseTests/Postgres`
  - `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04 dotnet test --project tests/DatabaseTests/SqlServer`

  Expected: PASS, `CheckTests` included.
- [ ] **Step 5: Run `branch-review:review`**, fix what it finds and repeat until it is clean.  Then offer the pull request.  When opening it, bring the branch up to date with `main` first and turn on autofix (`CLAUDE.local.md`).

---

## Self-review

- **Spec coverage.**  `Accounts.sql` under `no-array-pool` is Tasks 1 and 2; `Collections/Claimed.sql` is Tasks 1 and 5; the Dapper `Models` claims of `AllTypes.sql`, `Notes.sql`, `NewTypes.sql` and `Writes.sql` are Tasks 1, 3 and 4.  "Any other double claim…, found by the builder" found SqlClient 6.1's `*MethodQueries` (Task 4), `SuffixLocationQueries` (Task 5) and Npgsql 8's `AllTypesSql` (Task 3).  The `MethodLocation`/`SqlLocation`-only claims and `Shapes.sql` are listed under "Not in this PR" with their reasons.  The sidecars are rewritten by the script (Task 1), and the tests call the variants by their new names (Tasks 2-5).
- **Type consistency.**  The copy names in the table are the only names Tasks 2-5 use.  `VariantCopyTests` and `Copy_CommittedEntry_HasTheHashOfItsOriginal` are named alike in both engines.
- **Proportion.**  The plan holds names, markers, file lists and test names; the only code is the new test's shape and two exact assertions.

## Settled questions

1. **The `MethodLocation`/`SqlLocation` double claims** (`Locations.sql` in both engines, `AccountReports`): the spec's pull request 2 now names them, so nothing goes in `docs/deferred`.
2. **Where the spec and this plan land:** in this pull request, the first to implement them (`docs/superpowers/AGENTS.md`).  Take both from the local branch `claude/file-owned-settings` with `git checkout claude/file-owned-settings -- <path>` and commit them with the work.
