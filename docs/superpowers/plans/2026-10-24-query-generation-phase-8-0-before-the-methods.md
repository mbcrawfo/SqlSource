# Query generation 8.0: before the methods - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve `TD-0012` and record, in a spike document, what Npgsql 8 and 10 and SqlClient 6.1 and 7 do with the calls phase 8's generated methods will make, on each engine's floor and default image; amend the later sub-phase specs where a finding changes a detail.

**Architecture:** Three commits.  The first is test-only: two token queries run in `tests/SqlSource.Tests/EndToEnd/`, and the awkward-name theory copied to a class outside `tests/SqlSource.Tests/Generator/` at `LanguageVersion.Latest`.  The second is a document written from throwaway console probes kept in the session's scratchpad, never committed.  The third edits specs.

**Tech Stack:** .NET 10 SDK 10.0.401, xunit, Shouldly, Roslyn 5.9.0 (the `VersionOverride` of `tests/SqlSource.Tests`), Npgsql 10.0.3 and 8.0.9, Microsoft.Data.SqlClient 7.1.1 and 6.1.7, Docker 29.4.

**Spec:** [`docs/superpowers/specs/2026-10-24-query-generation-phase-8-0-before-the-methods-design.md`](../specs/2026-10-24-query-generation-phase-8-0-before-the-methods-design.md).  The epic's phase 8 section and the specs of 8.1 to 8.6 are the documents the third task amends.

## Global Constraints

- Images, exactly: `postgres:18.6` and `postgres:14.24`; `mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04` and `mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`.  All four are pulled locally.
- Drivers, exactly: Npgsql `10.0.3` and `8.0.9`; Microsoft.Data.SqlClient `7.1.1` and `6.1.7`.  These are the versions of `Directory.Packages.props` and of the floor projects' `VersionOverride`s.
- Nothing of the spike's code is committed.  It lives in the session's scratchpad directory, outside the repository, so that the repository's `Directory.Packages.props` and `Directory.Build.props` do not apply to it.
- Every commit passes `./pre-commit-validation.sh` run after the last edit (root `AGENTS.md`, Commits).  No describer or describer test changes, so the floor images need no extra run.
- Test names are `Method_Scenario_Outcome`, assertions are Shouldly's, every test class is `sealed` (`tests/AGENTS.md`).
- A file of `tests/SqlSource.Tests/Generator/` hands the compiler C# 12 at most, so the new `Latest` test is not there (`tests/SqlSource.Tests/Generator/AGENTS.md`).
- Specs are not rewritten after the fact except the epic outline; the spec of 8.0 allows the amendment of 8.1 to 8.6 in this PR because none is built.  A finding that contradicts a **Decided** item of the outline is not amended: it goes to the owner through the implementation manager.

## Where merged code differs from the spec

Nothing found.  The spec assumes commit `0fb40716`; `main` is `db6125f6`, which adds only the spec itself.  `TD-0012`, the files it names, and the driver versions are as the spec says.

## Review Focus

1. **A token whose name is a C# 14 contextual keyword** (`field`, `extension`) used as a generated parameter name — expected to compile with no warning at `Latest`; Task 1's theory pins it.
2. **The eighth token**, read from the nested tuple's `Rest` — expected to land in the right place and the repeated first token to be replaced at both occurrences; Task 1's `Eight` assertion pins it with a repeated `{{a}}`.
3. **A spike answer that differs between the floor and the default** (driver or server) — the document must say which does what, not one answer; Task 3's per-question table has a column per combination.
4. **A probe that passes by reading nothing** (a `NULL` test whose column was not null, a count test on an empty table) — each probe prints the value or exception type and message it observed; Task 2's output format requires it.
5. **A finding that contradicts a Decided item** (the not-null read with no `IsDBNull`, the connection rule) — expected to stop at a question to the owner, not an edit; Task 4 checks each finding against the Decided list before amending.

---

### Task 1: TD-0012 resolved, outline In progress

**Files:**
- Modify: `tests/SqlSource.Tests/EndToEnd/Tokens/Search.sql` (two queries appended)
- Modify: `tests/SqlSource.Tests/EndToEnd/TokenQueries.cs` (two wrappers)
- Modify: `tests/SqlSource.Tests/EndToEnd/EndToEndTests.cs` (two facts after `NestedLocation_QueryWithSeveralTokens_ReplacesEachOccurrence`)
- Modify: `tests/SqlSource.Tests/Generation/Emitting/MethodWriterTests.cs` (one theory)
- Modify: `tests/SqlSource.Tests/Generator/AGENTS.md` (one sentence)
- Delete: `docs/tech-debt/TD-0012-token-method-shapes-are-compiled-but-not-run.md`
- Modify: `docs/tech-debt/README.md` (row removed)
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (row 8.0: `In progress`)
- Add: this plan

**Interfaces:**
- Consumes: `GeneratorHarness.Run(OutputKind?, SourceFile[], SqlFile[], bool supportedFramework = true, LanguageVersion languageVersion = LanguageVersion.CSharp12, ...)` and `GeneratorHarness.SourcePath`, `GeneratorHarness.UsersPath`, `GeneratorRun.ShouldBeClean()` of `tests/SqlSource.Testing/Generator/`.
- Produces: nothing later tasks use.

- [ ] **Step 1: The queries.**  Append to `Search.sql`, after `Checked`, a token-only query and one of eight tokens, the first repeated so the replacement of a repeated token through the nested tuple is seen:

```sql
-- name: Whole
{{sql}}

-- name: Eight
SELECT {{a}}{{b}}, {{c}}, {{d}}, {{e}}, {{f}}, {{g}}, {{h}}, {{a}};
```

  And to `TokenQueries`: `public static string Whole(string sql) => Sql.Whole(sql);` and `public static string Eight(string a, string b, string c, string d, string e, string f, string g, string h) => Sql.Eight(a, b, c, d, e, f, g, h);`.

- [ ] **Step 2: The end-to-end facts.**

```csharp
[Fact]
public void NestedLocation_QueryThatIsOnlyAToken_ReturnsTheArgument() =>
    TokenQueries.Whole("SELECT 1;").ShouldBe("SELECT 1;");

// Eight tokens are more than one tuple holds: the eighth is read from the nested tuple's Rest.
[Fact]
public void NestedLocation_QueryWithEightTokens_ReplacesEachInItsPlace() =>
    TokenQueries
        .Eight("1", "2", "3", "4", "5", "6", "7", "8")
        .ShouldBe("SELECT 12, 3, 4, 5, 6, 7, 8, 1;");
```

  If the generated text differs only by a trailing `\n` or the query's whitespace, match what `MethodWriter` keeps for a query's text (check the `Checked` fact's form: no trailing newline).

- [ ] **Step 3: The awkward names at `Latest`.**  In `MethodWriterTests`, a theory `Run_TokenWithAnAwkwardName_CompilesOnTheNewestCSharp(string name)` with the same rows as `Run_TokenWithAnAwkwardName_Compiles` of `tests/SqlSource.Tests/Generator/GeneratedSourceMethodTests.cs` plus `[InlineData("extension")]`, the same source and SQL as that test, run through the overload with `languageVersion: LanguageVersion.Latest`, sources `[new SourceFile(GeneratorHarness.SourcePath, source)]`, and `run.ShouldBeClean()`.  A comment above says why it is not in `Generator/`: that folder also compiles on Roslyn 4.8.0, which knows C# 12 at most, and a consumer on .NET 10 compiles generated code as C# 14, where `field` and `extension` are contextual keywords.  The original theory stays where it is, as the Roslyn floor's check.

- [ ] **Step 4: Run.**  `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~EndToEndTests|FullyQualifiedName~MethodWriterTests"`.  Expected: all pass, the new theory with 24 rows.

- [ ] **Step 5: The docs.**  Delete the item file; remove its row from `docs/tech-debt/README.md` (leave `Next id` alone); `grep -rn "TD-0012" --exclude-dir=bin --exclude-dir=obj .` finds only the specs and plans, which are not rewritten.  Row 8.0 of the outline's Phases table says `In progress`.  In `tests/SqlSource.Tests/Generator/AGENTS.md`, after "hands the compiler C# 12 at most", the rule: a test that needs a newer C# goes in the folder of the type it tests, outside this one, as `MethodWriterTests`'s awkward names at `LanguageVersion.Latest`.

- [ ] **Step 6: Validate and commit.**  `./pre-commit-validation.sh` passes, then:

```bash
git add -A
git commit -m "Resolve TD-0012: run token-only and eight-token methods, compile awkward names at Latest"
```

### Task 2: The spike's probes (scratch, two engines in parallel)

Not committed.  Each engine's probes are one console project per driver version in `<scratchpad>/spike/<engine>-<driver>/`, sharing one probe source file by a `Compile Include` link, run once against each of the engine's two containers.  Each run writes `<scratchpad>/spike/results/<engine>-<driver>-<server>.txt`.

**Output format (both engines):** one line per probe, `Q<n>.<case> | <what was called> | <observed>`, where `<observed>` is the returned value (with its runtime type) or `throws <ExceptionType>: <message>`.  A probe that sets up data prints what it set up, so a `NULL` probe shows the column held `NULL`.  The header line names the driver's assembly informational version and the server's reported version (`SHOW server_version` / `SELECT @@VERSION`).

**Containers:** started with `docker run -d --rm`, a fixed host port each (`postgres:18.6` on 55418, `postgres:14.24` on 55414, the 2025 image on 51425, the 2017 image on 51417), stopped at the end.  SQL Server with `ACCEPT_EULA=Y` and an `MSSQL_SA_PASSWORD` of the agent's choosing; connection strings with `TrustServerCertificate=True`.  The connections go through `DbConnection`, and commands through `DbConnection.CreateCommand()` and `DbCommand.CreateParameter()`, as generated code will, unless a question names the provider's own type.

**PostgreSQL** (Npgsql 10.0.3 and 8.0.9 × 18.6 and 14.24), the spec's questions:

1. `GetFieldValue<T>` on a `NULL` with no `IsDBNull`, for `int`, `long`, `bool`, `decimal`, `double`, `string`, `byte[]`, `int[]`, `string[]`, `Guid`, `DateTime` (`timestamptz` and `timestamp`), `DateOnly`, `TimeOnly`, `DateTimeOffset`, a C# enum after `MapEnum<T>()` on the data source, a range (`NpgsqlRange<int>`), `JsonDocument`/`string` of `jsonb`.
2. `GetFieldValue<int?>`, `GetFieldValue<Guid?>`, `GetFieldValue<DateTime?>` on a `NULL` and on a value.
3. `ARRAY[1,NULL,3]` read as `int?[]` and as `int[]`; `ARRAY['a',NULL]` as `string[]`; under the default `Array Nullability Mode`.
4. `@name` in the text with `ParameterName` `"name"`, `"@name"`, `"NAME"`, `"@NAME"`, through `CreateCommand` on a plain connection and on one with `Transaction` set; and the same text when the command has no parameters at all (does Npgsql rewrite, and what error).
5. A declared parameter whose placeholder is not in the text, beside one that is: accepted, and is it sent (`pg_stat_activity` or a server error says).
6. `ExecuteNonQueryAsync` for `SELECT` of 3 rows, `INSERT ... RETURNING` of 2 rows, `UPDATE` of 0 rows, a multi-statement text `UPDATE ...; UPDATE ...;`.
7. Binding by value alone, no `DbType`, through `CreateParameter`, each C# type the PostgreSQL base map gives (`src/SqlSource/Generation/Types/PostgresTypeMap.cs` gives the full list), in an `INSERT` into a column of its type and in `WHERE col = @p`: accepted, or the server's error.
8. `DataTypeName` on `(NpgsqlParameter)command.CreateParameter()` and on `NpgsqlParameter<T>` for `integer[]`, an enum as a `string` (`DataTypeName` the enum's name), `jsonb` as a `string`, `int4range`, a domain over `integer`; and whether `new NpgsqlParameter<int> { TypedValue = 1, DataTypeName = "integer" }` boxes (measure `GC.GetAllocatedBytesForCurrentThread()` around setting and executing, against `Value = 1`).
10. `ExecuteReaderAsync(CommandBehavior.Default)`, then `ReadAsync`, then the synchronous `GetFieldValue<T>` of each column including a 1 MB `text` and `bytea`: works, and whether a column read out of order works.  `SingleResult` and `SingleRow`: with `SingleRow` on a query of two rows, does a second `ReadAsync` return `false`?  (It must not be used if so: `one` reads past the first row.)
11. `OpenAsync` on an open connection, `CloseAsync` on a closed one, `CloseAsync` on a connection with an open transaction (is the transaction rolled back, and the connection usable after reopening); a token cancelled during `ReadAsync` of a `pg_sleep` query: the exception type, and the connection's `State` after a `finally` that closes it.

**SQL Server** (SqlClient 7.1.1 and 6.1.7 × 2025 and 2017), the spec's questions; Q8 is PostgreSQL's alone:

1. As PostgreSQL's, for `int`, `long`, `bool`, `decimal`, `double`, `string`, `byte[]`, `Guid`, `DateTime` (`datetime2`), `DateOnly`, `TimeOnly`, `DateTimeOffset`, `TimeSpan`, a C# enum from an `int` column.
2. As PostgreSQL's.
3. Not applicable (no arrays); say so.
4. `@name` in the text with `ParameterName` `"name"`, `"@name"`, `"NAME"`, `"@NAME"`; on a command with and without the transaction set when the connection has one (SqlClient's own error).
5. A declared parameter the text does not use: accepted.
6. As PostgreSQL's, with `OUTPUT inserted.id` for `RETURNING`, plus `SET NOCOUNT ON; UPDATE ...` and a batch `UPDATE ...; UPDATE ...;` of 2 and 3 rows, and `MERGE` of 2 rows.
7. As PostgreSQL's for each C# type of the SQL Server base map (`src/SqlSource/Generation/Types/SqlServerTypeMap.cs`), and what the server receives (`sys.dm_exec_describe_first_result_set` is not needed; `SQL_VARIANT_PROPERTY(@p, 'BaseType')` and `'MaxLength'`, `'Precision'`, `'Scale'` on the bound value say what was sent).
9. `SqlParameter` with `SqlDbType.NVarChar`: `Size` 50 sends `nvarchar(50)` (characters), `Size` -1 sends `nvarchar(max)`; `SqlDbType.Decimal` with `Precision` 12 `Scale` 4 for `1.23456m` (rounded to 4) and without them (inferred from the value); `SqlDbType.DateTime2` with `Scale` 7 and a `DateTime` with ticks, round-trip exact; `SqlDbTypeExtensions.Vector` on 6.1.7 and 7.1.1 on 2025 (the type of the value it takes, `SqlVector<float>`, and how the dimension is given: `Size`, or the value's length) and on 2017 (the server's error); `GetFieldValue<MyEnum>` from an `int` column and from a `tinyint`.
10. As PostgreSQL's, with `varbinary(max)` and `nvarchar(max)` of 1 MB.
11. As PostgreSQL's, with `WAITFOR DELAY '00:00:10'` for the cancellation.

- [ ] **Step 1: PostgreSQL probes** (subagent A): build, run the four combinations, write the four results files, stop the containers.
- [ ] **Step 2: SQL Server probes** (subagent B, in parallel with A): the same for SQL Server.
- [ ] **Step 3: Check the output.**  Every question has a line on every combination, and no line's observation is empty or "skipped" without a reason.

### Task 3: The spike document

**Files:**
- Create: `docs/superpowers/specs/2026-10-24-query-generation-phase-8-spike.md`

Modelled on [phase 6's spike](../specs/2026-10-20-query-generation-phase-6-sql-server-spike.md): date, what it is an appendix to, what ran (the four images by tag and reported version, the four drivers by version, the host: Apple silicon, Docker), that the code was not kept and how it was shaped.  Then one section per question, numbered as the spec numbers them, each with the call summarised in one or two lines of code or prose and the answer stated plainly; where the four (or eight) combinations agree, one sentence says so; where they differ, a table with one column per driver and server.  A closing section, "What the later sub-phases take from this", lists each finding that settles one of the specs' "8.0's spike settles" points, naming the spec and the setting.

- [ ] **Step 1: Write it** from the results files.
- [ ] **Step 2: Validate and commit.**  `./pre-commit-validation.sh` passes, then `git add docs/superpowers/specs/2026-10-24-query-generation-phase-8-spike.md && git commit -m "Record the phase 8 driver spike"`.

### Task 4: The amendments, and the row Done

**Files:**
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (row 8.0 `Done`; Technical notes or Testing where a finding changes them)
- Modify, where a finding changes a detail: the specs of 8.1 (`ParameterName` with or without `@`; the reader's `CommandBehavior`; `GetFieldValue<T?>`; the synchronous read after `ReadAsync`), 8.2 (a `NULL` element of a value-type array), 8.3 (`NpgsqlParameter<T>` or the created parameter), 8.4 (`vector`'s dimension; facets), 8.5 and 8.6 if any finding touches them.

- [ ] **Step 1: Sort the findings.**  For each finding, against the outline's two Decided lists of phase 8: does it contradict one?  The candidates are the read of a not-null column with no `IsDBNull` (Q1: every kind must throw) and the connection rule (Q11).  A contradiction stops here: the PR holds the document, and the question goes to the implementation manager with a recommendation; nothing of that decision is amended until the owner answers.
- [ ] **Step 2: Amend** each spec where a finding changes a detail it gives or settles a point it leaves to 8.0's spike, replacing "as 8.0's spike settles" with the answer and a link to the document's section.  The epic outline's Technical notes and Testing say "the spike of 8.0 verifies" for two points; each becomes what it found.
- [ ] **Step 3: Row 8.0 `Done`.**
- [ ] **Step 4: Validate and commit.**  `./pre-commit-validation.sh` passes, then `git add -A && git commit -m "Amend phase 8's specs by the spike's findings; 8.0 done"`.
