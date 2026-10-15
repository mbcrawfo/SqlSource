# TD-0045, the gaps of the check against Npgsql - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close all of TD-0045.
- Every parameter's type in a sidecar carries `bindName`, and `check` binds by it.
- `check` compares where Npgsql placed each parameter with where SqlSource did, as well as the columns.
- An error of `check` that is no disagreement is `SQLSRC225`.
- A type Npgsql cannot write at all stays `SQLSRC232`, and D-0003 records the future conversion mechanism.

**Architecture:** `bindName` is an init-only member of the format's `SidecarType`, written on a parameter's top-level type only. The PostgreSQL describer computes a candidate per parameter (`PostgresBindNames`), and `check` binds by it. `check`'s answer also records Npgsql's rewritten text and the order of its positional parameters, read by reflection from `NpgsqlBatchCommand` (`BatchRewrites`). `NpgsqlCheck` compares that text with the tool's own rewrite, renumbered to Npgsql's order. A new answer member, `TextRefused`, tells Npgsql's refusal of the text apart from a lost connection.

**Tech Stack:** C# (`net8.0` tool, `netstandard2.0` library, .NET 10 tests), Npgsql 10.0.3, xunit v3 with Shouldly, Testcontainers PostgreSQL 14 and 18.

**Spec:** `docs/superpowers/specs/2026-10-15-td-0045-npgsql-check-gaps-design.md`. Read it with this plan: the spec gives the reasons, and the plan gives the names and the tests.

## Global Constraints

- The key is exactly `bindName`, and `formatVersion` stays `1`. It is written right after `name` and only on a parameter's top-level type. It never holds a modifier.
- The method names of the exchange never change. A new member of a record in `PostgresCalls.cs` is nullable, comes last, and defaults to `null`. Its null means what the record meant before it had the member (`src/SqlSource.Tool/AGENTS.md`, the paragraph on the calls).
- `PostgresCheckRequest` gains no member. The tool's own rewrite is passed to `NpgsqlCheck.Read` as an argument, so the requests of old captures still match.
- No fallback for the reflection. A missing member throws `InvalidOperationException` that names the member and Npgsql's assembly version, like `RowDescriptions.MessageOf`.
- The tool never rewrites a user's SQL to make a parameter bindable.
- Every commit passes `./pre-commit-validation.sh` (`AGENTS.md`).
- Test names are `Method_Scenario_Outcome`, with Shouldly assertions, and the tests mirror the source folders (`tests/AGENTS.md`).
- A change to what the describer asks changes the fixtures: record them with `tools/record-fixtures.sh` and read the diff.
- Before the PR, run the version check that `AGENTS.md` requires. When creating the PR, turn on Auto-fix (`CLAUDE.local.md`).

## Review Focus

These are the inputs most likely to reach a user that no task naturally tests. Each line names the task that adds its test.

1. **A built-in type hidden by a user type of the same name on the `search_path`**: the existing test that puts a `numeric` domain first. The parameter's `name` is `pg_catalog.numeric` (TD-0050), and its `bindName` must still bind the built-in, not the domain. *(Task 3)*
2. **A parameter written twice, once as `@id` and once as `:id` in a slice, `tags[1:id]`, with a column `id`.** The columns match, so only the comparison of the texts catches it. *(Task 4)*
3. **A string literal that holds `$1`**, `SELECT '$1' AS a WHERE id = @id`. Both texts hold the same `$1` in the string, and the check must pass. *(Task 4)*
4. **A parameter whose name differs from its other occurrence only in case**, `@ID OR @id`. Npgsql places one positional parameter, and the renumbering matches names ignoring case. *(Task 4)*
5. **A connection lost between `columns` and `check`.** It is `SQLSRC225` at `check parameters` with the lost connection's text, not `SQLSRC231`, and a replay of an old capture whose error has no SQLSTATE stays `SQLSRC231`. *(Task 5)*

---

### Task 1: `bindName` in the sidecar format

**Files:**
- Modify: `src/SqlSource/Snapshot/SidecarType.cs`, `SidecarKeys.cs`, `SidecarWriter.cs`, `SidecarComparer.cs`, `Reading/SidecarTypeReader.cs`, `Reading/SidecarReader.cs` (the two calls at lines 484 and 553)
- Modify: `schemas/sidecar-v1.schema.json`, `docs/superpowers/specs/2026-10-07-sidecar-format-design.md` (sections 1.5 is unchanged; 2.1, 2.2, 2.3, 2.4, 3, the example of 4.1, and 5)
- Test: `tests/SqlSource.Tests/Snapshot/SidecarWriterTests.cs`, `SidecarComparerTests.cs`, `SidecarRoundTripTests.cs`, `SidecarSchemaTests.cs`, `Reading/` (the type reader's tests), `SidecarFaker.cs` if the round trip draws from it

**Interfaces:**
- Produces: `SidecarType.BindName : string?`, a `public string? BindName { get; init; }` on the abstract record. It is not a positional member, so no constructor changes. It takes part in record equality.
- Produces: `SidecarKeys.BindName = "bindName"`.
- Produces: `SidecarTypeReader.Read(SidecarCursor cursor, string engine, bool orNull, bool isParameter)`. When `isParameter` is false, `bindName` is an unknown key and ignored (reader rule 1). Nested reads (`element`, `base`, `subtype`) always pass false.

- [ ] **Step 1: Write the failing tests.**
  - `Write_ParameterTypeWithABindName_WritesItAfterTheName`: a PostgreSQL parameter of `public.status` with `BindName = "public.status"` writes `"name"`, then `"bindName"`, then `"kind"`.
  - `Write_TypeWithoutABindName_WritesNoKey`: nothing for a column's type or for null.
  - `Read_BindNameOnAParameterType_IsRead`, for `postgres`, `mssql` and an unknown engine.
  - `Read_BindNameOnAColumnOrANestedType_IsIgnored`: a column's type and an `element` holding `bindName` read with `BindName` null, and no error.
  - `Compare_BindNamesDiffer_IsADifferenceOfTheParameter`: the comparer reports it as it reports a different `name`, under the key `bindName`.
  - The schema test: a sidecar with `bindName` on a parameter's type validates, and one with `"bindName": 3` does not.
  - The round trip keeps `BindName`.
- [ ] **Step 2: Run them and see them fail.** `dotnet test --project tests/SqlSource.Tests -- --filter-class "*Sidecar*"`
- [ ] **Step 3: Make the change.**
  - Add the member, the key and the writer line. Writing it right after `name` holds for every engine.
  - Add the reader's flag. `SidecarReader` passes `isParameter: true` for a parameter's `type` and `false` for a column's.
  - Add one comparison in the comparer, for every engine.
  - In the schema, add `"bindName": { "type": "string", "minLength": 1 }` to each engine's type definition, optional.
  - In the format spec:
    - Section 2.1's recommendation gains the shared convention: the spec's table under "The convention, for every engine".
    - 2.2 gets a row saying it is always written on a parameter's type, with the rules of the spec's PostgreSQL table in one sentence.
    - 2.3 says phase 6 decides, with the expected `TypeName` and `UdtTypeName`.
    - 2.4 says each future engine states when it writes the key.
    - Section 3 lists the key as an additive change made in this version.
    - The example of 4.1 gains `bindName` on each parameter's type.
    - Section 5 matches the schema file.
- [ ] **Step 4: Run the library's tests.** Everything should pass.
- [ ] **Step 5: Commit** with the message "Add bindName to the sidecar's type of a parameter".

### Task 2: The candidate name of a PostgreSQL parameter

**Files:**
- Create: `src/SqlSource.Tool/Describing/Postgres/PostgresBindNames.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresTypeNames.cs` (the remark on reserved words only)
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresBindNamesTests.cs` (new), with rows from `PostgresRows.cs`

**Interfaces:**
- Produces: `PostgresBindNames.Of(long oid, IReadOnlyDictionary<long, PostgresTypeRow> rows) : string?`. It returns the name `check` binds the parameter by, or null when no name can say the type: a schema or a name that holds a `.`.
- Produces: `PostgresBindNames.NoName : string`, the text that `SQLSRC232`'s server line gives for a null candidate. Its wording is the builder's, and it says that a schema or a type name that holds a `.` cannot be named to Npgsql.

The rules, in order:
1. An array: the element's candidate and `[]`, or null when the element's is null.
2. A domain whose base is a domain: the candidate of the first base, going down, that is no domain.
3. `pg_catalog`, plain `bit`: `bit`.
4. `pg_catalog`, `"char"` (`typname` `char`): `"char"`'s `FormatType` for now. Task 3 replaces it if the container finds a spelling Npgsql binds.
5. `pg_catalog`, anything else: `FormatType` without a leading `pg_catalog.`, so that it never depends on the session (TD-0050).
6. Any other schema: `nspname + "." + typname`, unquoted, or null if either holds a `.`.

- [ ] **Step 1: Write `Of_EachKindOfType_IsTheNameNpgsqlBindsBy` as a theory, with these rows (type → candidate):**
  - `int4` → `integer`
  - `varchar` → `character varying`
  - `timestamptz` → `timestamp with time zone`
  - `bit` (`FormatType` `"bit"`) → `bit`
  - `varbit` → `bit varying`
  - `numeric`, with `FormatType` `pg_catalog.numeric` (hidden) → `numeric`
  - `_text` → `text[]`
  - `public.user_status` → `public.user_status`
  - `"My Schema"."Status"` → `My Schema.Status`
  - its array → `My Schema.Status[]`
  - `user.status` → `user.status`
  - `public.email_t` (a domain over `varchar`) → `public.email_t`
  - `public.check_d2` (a domain over `email_t`) → `character varying`
  - `public.check_pair` (composite) → `public.check_pair`
- [ ] **Step 2: Write `Of_SchemaOrNameWithADot_IsNull`**, for a schema `a.b` and for a type `x.y`, and for an array of each.
- [ ] **Step 3: Run them and see them fail.**
- [ ] **Step 4: Implement `PostgresBindNames`.** Its `<remarks>` give the rules and the reason for each, in the style of `PostgresTypeNames`. In `PostgresTypeNames`, change the remark "the name is the one that Npgsql binds…" to the true reason a reserved word stays bare: `name` is unchanged since phase 3, and `bindName` is what is bound. Keep the container test name it cites.
- [ ] **Step 5: Run them and see them pass.**
- [ ] **Step 6: Commit** with the message "Compute the name a PostgreSQL parameter is bound by".

### Task 3: The describer records `bindName`, and `check` binds by it

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs` (the parameters built near line 585, `CheckAsync`, `CheckParameterOf`), `PostgresErrors.cs` (`CannotBind`, `BindHelp`, remove `BindQuotedHelp`), `PostgresCalls.cs` (the doc of `PostgresCheckParameter.DataTypeName`)
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs`, `PostgresErrorsTests.cs`, `tests/SqlSource.Tests.Postgres/Describer/NpgsqlCheckTests.cs`, `ParameterTests.cs`, the fixtures (`tools/record-fixtures.sh`)

**Interfaces:**
- Consumes: `PostgresBindNames.Of`, `PostgresBindNames.NoName`, `SidecarType.BindName`.
- Produces: each `DescribedParameter.Type` is built `with { BindName = candidate }`. `PostgresCheckParameter.DataTypeName` is that candidate. `SQLSRC232`'s first argument is still the type's `name`.
- Produces: `PostgresErrors.Unnamed(DescribeRequest query, int parameter, string typeName) : DescribeFailure`. It is `SQLSRC232` at `check parameters` with `NoName` as its one server line and `BindHelp`, and is returned before `check` is asked, for the first parameter whose candidate is null.

- [ ] **Step 1: Write the failing unit tests.**
  - `DescribeAsync_ParameterOfAQuotedType_HasItsBindNameAndChecksByIt`, with a script whose `check` request has `DataTypeName` `My Schema.Status`. Assert the parameter's `Type.BindName`, and that columns' types have none.
  - `DescribeAsync_ParameterTypeWithADotInItsName_IsSqlsrc232AndAsksNoCheck`, using `Unnamed`.
  - In `PostgresErrorsTests`, the `SQLSRC232` test expects the arguments `[name, written, query]` and `BindHelp`. Delete the quoted-help case.
- [ ] **Step 2: Run them and see them fail.**
- [ ] **Step 3: Make the change in the session and in `PostgresErrors`.**
  - Rewrite `BindHelp` for the remaining kinds: Npgsql cannot send a value of this type; a `text` parameter with a cast in the SQL works; a later version may let a project declare a conversion.
  - `CannotBind` reads the type's `name` from `query`'s described parameter, not from `asked`.
- [ ] **Step 4: Run the tool's tests.** A replay test whose `check` request now differs throws `CaptureMissException`. Correct each script's request to the candidate. Only types whose candidate differs from `name` should be affected.
- [ ] **Step 5: Rewrite the container tests in `NpgsqlCheckTests`.**
  - `Describe_ParameterOfEachKindOfType_IsBoundByItsSpelledName` becomes `…_IsBoundByItsBindName`. Add `"My Schema"."Status"`, `"My Schema"."Status"[]`, `bit(3)` declared (`bindName` `bit`) and `check_d2` (`bindName` `character varying`), each asserting the described `bindName`.
  - `Describe_ParameterNpgsqlCannotBind_IsSqlsrc232NamingIt` keeps the rows for `check_pair`, a table's row type (add one, `users`), and `regclass` (add `WHERE c.oid = @t::regclass`). Drop the `bool quoted` argument.
  - `"char"` and the long array: try the spellings the spec lists (`char`, `pg_catalog.char`, the `DataTypeName` Npgsql reports for `NpgsqlDbType.InternalChar`; the unqualified `name[]`). If one binds and does not depend on the `search_path`, put it in rule 4 of Task 2, or in a rule for the array, with its unit row. Then move the row to the bound theory. Otherwise keep the row in the `SQLSRC232` theory and write in the test's comment what was tried.
  - `Describe_DeclaredOnlyParameterNpgsqlCannotBind_IsSqlsrc232NamingIt` uses `check_pair` instead of `check_d2`, which now binds.
  - Review Focus 1: in the test of the hidden built-in `numeric` (`ParameterTests` or `ColumnTests`, the one added by commit `93cd3f8`), assert the parameter's `bindName` is `numeric`, and that the describe passes on PostgreSQL 14 and 18.
- [ ] **Step 6: Re-record the fixtures** with `tools/record-fixtures.sh`, and read the diff: only `bindName` lines on parameters, and the `check` requests of the types that changed.
- [ ] **Step 7: Run `./pre-commit-validation.sh`.** It should pass.
- [ ] **Step 8: Commit** with the message "Record bindName for each PostgreSQL parameter and bind by it in check".

### Task 4: `check` compares where the parameters are

**Files:**
- Create: `src/SqlSource.Tool/Describing/Postgres/BatchRewrites.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresCalls.cs`, `PostgresLive.cs` (`CheckAsync`), `NpgsqlCheck.cs`, `PostgresSession.cs` (`CheckAsync`), `PostgresEvents.cs` if the `check` event writes the answer's members
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/BatchRewritesTests.cs` (new), `CheckVerdictTests.cs`, `PostgresSessionTests.cs`, `PostgresEventsTests.cs`, `tests/SqlSource.Tests.Postgres/Describer/NpgsqlCheckTests.cs`

**Interfaces:**
- Produces: `PostgresCheckSent(string Text, IReadOnlyList<string> Parameters)`, a record in `PostgresCalls.cs`. It holds Npgsql's `FinalCommandText`, and the `ParameterName` of each of its `PositionalParameters`, in order.
- Produces: on `PostgresCheckAnswer`, a last member `PostgresCheckSent? Sent = null`. It is set with `Columns`, and null in a capture recorded before it.
- Produces: `BatchRewrites.Resolve() : void` and `BatchRewrites.Of(NpgsqlBatchCommand command) : PostgresCheckSent`. They read the internal `FinalCommandText` and `PositionalParameters` once per process, in the pattern of `RowDescriptions`. Either member null after a describe throws.
- Produces: `NpgsqlCheck.Read(PostgresCheckAnswer answer, IReadOnlyList<PostgresColumnRow> described, IReadOnlyDictionary<long, PostgresTypeRow> rows, string sql, EquatableArray<SqlQueryParameter> parameters) : CheckVerdict`. `sql` and `parameters` are the request's, for the tool's own rewrite.

The comparison, when `Sent` is not null, before the columns:

```text
order  = for each name of Sent.Parameters, the query's parameter of that name (ignoring case);
         then the query's parameters that Sent does not name, in their order
ours   = ParameterPlaceholders.Rewrite(sql, order)      // $k now means Sent.Parameters[k-1]
theirs = Sent.Text
a name of Sent.Parameters that the query's list lacks   -> differ ("Npgsql placed @x, which SqlSource does not read as a parameter")
ours and theirs, each with trailing white space trimmed, differ -> differ at the first character that differs,
         quoting a few characters of each from there
```

- [ ] **Step 1: Write the failing unit tests.**
  - `BatchRewritesTests.Resolve_PinnedNpgsql_FindsTheMembers`, which needs no server.
  - In `CheckVerdictTests`:
    - `Read_SameTextsAndColumns_IsSame`.
    - `Read_NpgsqlPlacedAParameterSqlSourceDidNot_IsDiffer`: `SELECT tags[1:$1] …` against the tool's `tags[1:id]`.
    - `Read_NpgsqlNamesAParameterTheQueryLacks_IsDiffer`.
    - `Read_NpgsqlNumbersInAnotherOrder_IsSame`.
    - `Read_LiteralDollarOneInAString_IsSame` (Review Focus 3).
    - `Read_NamesDifferInCaseOnly_IsSame` (Review Focus 4).
    - `Read_AnswerWithoutSent_ComparesColumnsAlone`.
- [ ] **Step 2: Run them and see them fail.**
- [ ] **Step 3: Implement `BatchRewrites`, the record, the member and the comparison.**
  - `PostgresLive.CheckAsync` sets `Sent` from `batch.BatchCommands[0]` after the reader opens.
  - `PostgresSession.CheckAsync` passes `request.Sql` and `request.Parameters` to `Read`.
  - The capture holds no more of the user's SQL than its request does, so no redaction changes.
  - Update the `<remarks>` of `NpgsqlCheck`: it compares the places of the parameters and the columns. What it still cannot see is a `$n` the SQL writes itself, which `SQLSRC234` refuses before.
- [ ] **Step 4: Write the container tests.**
  - `Describe_ColonParameterNpgsqlReadsInASlice_IsSqlsrc231` (Review Focus 2): `SELECT tags[1:id] AS t FROM users WHERE id = @id`, where `users.id` is the integer column the slice reads. The arguments say where the two texts part.
  - Add `SELECT '$1' AS a, id FROM users WHERE id = @id` to `Describe_QueryNpgsqlReadsAsTheLexerDoes_PassesTheCheck`.
  - `CheckAsync_QueryWithParameters_AnswersNpgsqlsText`: the live answer's `Sent` holds `$1` where `@id` was, and the parameter list `["id"]`.
- [ ] **Step 5: Re-record the fixtures**, since every `check` answer gains `sent`. Read the diff: only `sent`.
- [ ] **Step 6: Run `./pre-commit-validation.sh`.** It should pass.
- [ ] **Step 7: Commit** with the message "Compare where Npgsql and SqlSource place each parameter".

### Task 5: What an error of `check` is reported as, and the help of `SQLSRC231`

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresCalls.cs`, `PostgresLive.cs` (`CheckAsync`, `RefusedAsync`), `PostgresErrors.cs` (`Check`, `CheckHelp`, a new `ArrayOfDomainHelp`)
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresErrorsTests.cs`, `PostgresSessionTests.cs`, `tests/SqlSource.Tests.Postgres/Describer/NpgsqlCheckTests.cs`

**Interfaces:**
- Produces: on `PostgresCheckAnswer`, a last member `bool? TextRefused = null`, after `Sent`. It is true when `RefusedAsync` answers Npgsql's refusal of the text, and false for every other `Error`. Null, in an old capture, reads as true.
- Produces: `PostgresErrors.ArrayOfDomainHelp : string`. It says such a parameter is sent as an array of the domain's base, and to cast the column, `@p::varchar[]`.

`PostgresErrors.Check`, after the `DriverError` branch:

| `Error` | Reported as |
|----|----|
| SQLSTATE starting `42` | `SQLSRC231`, as today |
| no SQLSTATE, `TextRefused` true or null | `SQLSRC231`, as today |
| anything else | `Rejected(error, query, DescribeStep.CheckParameters, ServerLines(error with { Position = null }, 0))`, `SQLSRC225` |
| none (a difference) | `SQLSRC231`; its help is `ArrayOfDomainHelp` when the difference is a column's type and a parameter's `Type` is a PostgreSQL array whose `Element` is a domain, else `CheckHelp` |

The `SQLSRC231` help can only be one line (`DescribeFailure.Help` is one string). So for an array of a domain, `ArrayOfDomainHelp` replaces `CheckHelp` instead of being a second line: the `@` help is wrong for that case. `NpgsqlCheck` tells `PostgresErrors` that the difference is a type through a new `CheckVerdict` member, `bool TypeDiffers`, set where it compares OIDs.

`CheckHelp` gains, at its end: that a table, a column or a type changed by another session during the run shows the same way, and to run `describe` again.

- [ ] **Step 1: Write the failing unit tests.**
  - `Check_ServerErrorOfClass42_IsSqlsrc231`.
  - `Check_ServerErrorOutsideClass42_IsSqlsrc225AtCheckParameters`, with `57014` (query canceled).
  - `Check_LostConnection_IsSqlsrc225WithItsText`: no SQLSTATE, `TextRefused` false, message `PostgresConnection.LostConnection` (Review Focus 5).
  - `Check_TextRefusedByNpgsql_IsSqlsrc231`.
  - `Check_ErrorWithoutSqlStateInAnOldCapture_IsSqlsrc231`: `TextRefused` null (Review Focus 5).
  - `Check_TypeDiffersAndAParameterIsAnArrayOfADomain_HelpsWithTheCast`.
  - `Check_TypeDiffersWithoutSuchAParameter_HasTheCheckHelp`.
- [ ] **Step 2: Run them and see them fail.**
- [ ] **Step 3: Implement it.** `PostgresLive` sets `TextRefused` on each `Error` it answers from `check`.
- [ ] **Step 4: Update the container tests.**
  - `Describe_ArrayOfADomainReturnedAsAColumn_IsSqlsrc231NamingBothTypes` asserts `ArrayOfDomainHelp`. Remove its comment's reference to docs/tech-debt.
  - `CheckAsync_TextNpgsqlWouldSplit_AnswersAnErrorAndRunsNothing` asserts `TextRefused` true when it ran with a parameter.
- [ ] **Step 5: Re-record the fixtures, read the diff, and run `./pre-commit-validation.sh`.**
- [ ] **Step 6: Commit** with the message "Report an error of check that is no disagreement as SQLSRC225".

### Task 6: Docs, D-0003, and closing TD-0045

**Files:**
- Modify: `docs/diagnostics.md` (`SQLSRC225`, `SQLSRC231`, `SQLSRC232`), `src/SqlSource.Tool/AGENTS.md` (the paragraphs on `check`, the calls, error ids and type names), `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (phase 8's note on `DataTypeName`), `docs/tech-debt/README.md`, `docs/tech-debt/TD-0049-describe-behind-a-pooler-can-reach-another-backend.md` (only if its text no longer holds)
- Create: `docs/deferred/D-0003-custom-type-conversion.md`; modify `docs/deferred/README.md`
- Delete: `docs/tech-debt/TD-0045-the-check-against-npgsql-is-best-effort.md`

- [ ] **Step 1: Update `docs/diagnostics.md`.**
  - `SQLSRC231`: the check compares where each parameter is, as well as the columns. Add the slice example. Say that a schema changed during the run shows the same way. Point the array-of-a-domain prose at the new help.
  - `SQLSRC232`: list only what is still refused. Say plainly that it is a known limit, link D-0003, and give the `text` workaround. Update the example's type and message to a composite type.
  - `SQLSRC225`: one paragraph for the step `check parameters` (a lost connection, or a server error that is no syntax or name error). Make the console examples match the code's messages.
- [ ] **Step 2: Update `src/SqlSource.Tool/AGENTS.md`.**
  - Line 39: `check` binds by `bindName`, compares texts and columns, and the list of `SQLSRC232` kinds shrinks. Remove the reference to TD-0045.
  - Line 43: `sent` and `textRefused` in the calls, and `BatchRewrites` beside `RowDescriptions`.
  - Line 44: what an error of `check` becomes.
  - Line 45: `bindName` beside `name`.
  - Keep the paragraphs' style.
- [ ] **Step 3: Update the epic.** Phase 8's technical note binds by the parameter's type's `bindName`.
- [ ] **Step 4: Read TD-0049.** It says a pooler shows as `SQLSRC231`. Correct only what Task 4 or 5 changed, such as a missing statement that answers no columns.
- [ ] **Step 5: Write D-0003** with the content of the spec's "D-0003" section, in the sections `docs/deferred/AGENTS.md` names. Add its row, and set `Next id` to `D-0004`.
- [ ] **Step 6: Delete TD-0045 and its row.** Then run `grep -rn "TD-0045" src tests docs schemas README.md AGENTS.md`. The only hits outside `bin/` and `obj/` should be in `docs/superpowers/` (history).
- [ ] **Step 7: Run `./pre-commit-validation.sh`.** It should pass.
- [ ] **Step 8: Commit** with the message "Close TD-0045 and record D-0003, custom type conversion".
- [ ] **Step 9: Run the version check** (`AGENTS.md`), then open the PR with Auto-fix on.
