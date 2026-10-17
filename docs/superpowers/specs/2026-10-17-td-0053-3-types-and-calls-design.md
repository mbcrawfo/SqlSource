# TD-0053 PR 3, types, calls and the rest - design

Date: 2026-10-17

Part of [the TD-0053 design](2026-10-17-td-0053-postgresql-describer-patterns-design.md), whose rules for every PR apply here.

This PR fixes what PR 1 and PR 2 leave of [TD-0053](../../tech-debt/TD-0053-duplicated-and-uneven-patterns-in-the-postgresql-describer.md):
- item 6;
- item 7, except `RunSetOperation`;
- item 8, except the `ServerLines` repeat, the `$n` reader and `Invariant`;
- the `Resolve` methods of item 9;
- item 10.

Then it resolves the item.  It starts from `main` after PR 1 and PR 2 have merged ("Order" in the overview).  Read the item as it stands then, which lists only what is left.

Paths are under `src/SqlSource.Tool/Describing/Postgres/` unless they say otherwise.  Line numbers are those of `ae6e66f`, before PR 1 and PR 2; find each place by its name.

## Done

- Every built-in OID the describer names has one named definition, and a container test holds each to `pg_type`.
- The set of types that carry a modifier is defined once, and a test checks that each table handling those types handles every member.
- `PostgresSession.cs` holds the flow of a describe.  Each timed and logged call goes through one helper, and the call names are constants.
- An order that today depends on statement order alone is enforced by a value that one step returns and the next takes.
- The small repeats of item 8 left after PR 1 have one definition each.
- `CaptureJson.Options` cannot be changed.
- The item file, its row in `docs/tech-debt/README.md` and every mention of `TD-0053` outside `docs/superpowers/` are gone.  `docs/tech-debt/AGENTS.md`, "Resolving an item", lists the steps.

## Item 6: type tables

**Named OIDs.**  A new static class, `BuiltInTypes`, holds `const` OIDs for every built-in type the describer names by number:
- `PlanExpression.TypesByName` (`Walk/PlanExpression.cs:55-71`);
- `SetOperationTypes.FamilyOf` (`Walk/SetOperationTypes.cs:29-43`);
- `PostgresTypes.cs:123-124`, which holds `int2vector` 22 and `oidvector` 30.  The item missed these two.

All of today's numbers are right.  They are: bool 16, int8 20, int2 21, int4 23, text 25, float4 700, float8 701, bpchar 1042, varchar 1043, date 1082, timestamp 1114, timestamptz 1184, numeric 1700, plus the two above.  The code and the unit tests (`SetOperationTypesTests`, `PlanExpressionTests:202-210`) use the constants.

**A container test** in `tests/SqlSource.Tests.Postgres/Describer/PlanOutputTests.cs` holds the constants to the server.  It sends every constant of `BuiltInTypes` as an `oid[]` and checks that `pg_type` names each one as expected.  It reaches the constants by reflection or through a list in `BuiltInTypes`; the builder chooses.

`Catalog_CastFamilies_AreBuiltInBothWays` (299-331) checks the cast families today by `regtype` names, which makes it a third copy of the family list.  It changes to take the families from `SetOperationTypes`' data.  The test name stays the same, because `SetOperationTypes.cs:21` names it.

**The types that carry a modifier.**  There are ten: the full set of built-in types with a typmod input.  `BuiltInTypes` defines the set once.
- `PostgresTypeNames.InCatalog` (70-81), `ColumnFacets` (28-35) and `TypeModifiers` (41-45) keep their switches, since each type is handled differently:
  - the header −4 for `varchar` and `bpchar`, but not for `bit` and `varbit`;
  - the precision guard of `numeric`;
  - `interval`'s 65535;
  - each spelling of its own: `character(n)`, `FormatType(n)`, `bit(n)` and `InCatalog`'s.

  A unit theory over the set checks that each of the three switches (`InCatalog` is private, so through the member that calls it, or made `internal`) gives a non-default answer for every member, so a type added to the set and not to a switch fails a test.

**`PairsOf` and `Build`.**  `PostgresTypes` switches on `typtype` three times: in both `Build`s and in `PairsOf`.  `PairsOf` must ask for exactly the pairs `Build` will look up.

`PairsOf` cannot be derived by running `Build`.
- `Build` throws for pseudo and unknown types (`PostgresTypes.cs:66`) and for missing enum labels, where `PairsOf` answers `[]`.
- `PairsOf` also runs before the pseudo-type refusals, SQLSRC226 and SQLSRC229 (`PostgresSession.cs:284`, and 665 and 706).
- Deriving it would change which describes fail.

So the walk over `typtype` in the type `Build` and in `PairsOf` becomes one method.  Each caller passes what it does at a base type, and what to do where `Build` throws.

The facets `Build` (81-95) passes facets differently: a domain passes its facets down, and an array's element gets none.  It keeps its own switch, with a remark saying why it cannot share the walk.

**One "no row for type" lookup.**  `ColumnTypes.RowOf` (`ColumnTypes.cs:45-48`) is the same as `PostgresTypes.Row` (204-207), and is replaced by it.

`PostgresColumns.FacetsOf` (58-63) adds "of the column 'x'" to the message.  It also uses `PostgresTypes.Row`, or one overload that takes that detail; the builder chooses.  These lookups are guards against bugs that `MissingType` makes unreachable, and no test asserts their messages.

## Item 7: `PostgresSession`

**One helper for a call.**  Each call is timed, asked and logged.  That is written out in the six wrappers (`PostgresSession.cs:808-871`: prepare, columns, tables, types, spellings and deallocate), and inline for explain (377), partitions (437), relations (472) and check (749).

Their log events differ:
- Prepare, Columns, Explain, Partitions, Relations and Check take the query's name; the others do not.
- Check logs the verdict of `NpgsqlCheck.Read`, which is worked out between the ask and the log.

The shape suggested is `AskAsync<TRequest, TAnswer>(string method, TRequest request, Func<TRequest, CancellationToken, Task<TAnswer>> live, Action<TRequest, TAnswer, long> log, CancellationToken)`, with lambdas that capture the query.

Check either stays inline, with a remark, or uses a form of the helper that returns the answer and the time without logging.  The builder chooses, but every other call goes through the helper.

`open` (97) is neither timed nor logged, and stays that way.

**Call names as constants.**  One static class holds the names of the calls, with today's values.
- The helper and the exchanges use them.
- So does `PostgresEvents`, which writes the same names again as the names of events (lines 39, 56, 74, 109, 126, 148, 217, 238, 259, 328).

A remark on the class says that a value must never change, since every capture records it (`src/SqlSource.Tool/AGENTS.md`).

**The flow apart from the parts.**
- `PostgresSession.cs` keeps the flow: opening, `DescribeAsync`, `DescribePreparedAsync`, `WalkAsync` and the `…OfAsync` steps.
- The call helper and the wrappers move to a partial file, `PostgresSession.Calls.cs`.
- The static builders that read no state of the session move to a second partial file, or to classes of their own; the builder chooses. These are `RecordedColumns`, `ArgumentCount`, `MissingType`, `ParameterOids`, `TypeParameters`, `CheckParameterOf`, `Described` and `ServerOf`.
- The aim is a main file under about 450 lines.
- `src/SqlSource.Tool/AGENTS.md` is updated if it names what moved.

**Order enforced by values.**
- `ColumnOrigin.MissingFrom` must run before `ColumnOrigin.Find` (`PostgresSession.cs:250-262`).
  - A new `ColumnOrigin.FindAll(columns, tables)` returns either every column's origin, or the message for the first column that has none, in column order, as today's `FirstOrDefault` finds it.
  - `MissingFrom`'s condition is exactly the one under which `Find` throws, so the answers are the same.
  - `Find` stays, since the tests call it directly about 12 times.
- `MissingType` (505) must run before `PostgresTypes.Build`, and before what `SpellingsOfAsync` (284) reads: `PairsOf`, `ColumnTypes.OidOf` and `IsPseudo`.
  - `MissingType` returns a value that holds the rows it checked. On success that is the dictionary wrapped in a small record; on failure it is the message.
  - The session's own methods that lead to those readers (`SpellingsOfAsync`, `TypeParameters`, `BuildColumns` and the like) take that value instead of the bare dictionary, and unwrap it where they call the static helpers.
  - The static helpers (`PostgresTypes.Build`, `PairsOf`, `IsPseudo`, `ColumnTypes.OidOf`, `PostgresColumns.Build`, `PostgresBindNames.Of`) keep taking the dictionary, so their unit tests do not change.
  - The builder chooses the type, but inside `PostgresSession` an unchecked dictionary must no longer reach a helper.

**`WalkAsync`'s parameters.**  `WalkAsync` (365) takes seven parameters, and is cut to four or fewer by grouping the values that travel together into a record.  `RunSetOperation`'s parameters are PR 2's.

**`VersionOf`** (132) moves to `PostgresLive`, the only production caller (`PostgresLive.cs:199`).  `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresDescriberTests.cs:93` follows it.

## Item 8: the rest

- **"The answer to 'prepare' holds no types and no error."** is thrown at `PostgresSession.cs:359` and 547.
  - The throw at 359 cannot be reached, because 547 runs first. It is removed: the count of the prepare's own types found at 547 (`resolved.Count`, every `$n` the server read) is passed along.  Not `parameterOids.Count`, which counts only the request's parameters and is smaller when the server read a `$n` the tool's lexer did not; `ArgumentCount` would then be wrong for `EXPLAIN EXECUTE`.
- **`LogValue.Of(bool?)`** is added beside `LogValue.Of(long?)` (`src/SqlSource.Tool/Logging/LogValue.cs:25`).
  - The six conversions in `PostgresEvents.cs` (184, 312, 373-375, 386) use it.
  - The logged text stays the same; `PostgresEvents`' tests pin it.
- **In `PostgresLive`:**
  - **The driver's refusal.**  The three catches are written out in `CheckAsync` (572-583) and in `RefusalOfAsync` (628-639).
    - They become one `IsDriverRefusal(Exception)` used as a `when` filter.
    - `NpgsqlException` is still caught first (568).
  - **The `oid[]` parameter** is built the same way at 298 and 452.
    - One `OidArray` helper serves both.
    - The one at 496 is built from pairs and has no name, so it uses the helper only if it fits.
  - **The schema-and-name parameters** are built the same way at 321-334 and 398-411. One `AddRelations` helper serves both.
  - **One style of parameter: positional.**  That is PostgreSQL's own form, Npgsql sends it unrewritten, and most of the SQL already uses it.
    - `TablesSql` and `TypesSql` change from `@oids` to `$1`.
    - `ParameterTypesSql` (36-38, 231-232) changes from `@name`/`@statement` to `$1`/`$2`.
    - Their parameters lose their names.
    - The user's SQL in `check` keeps its named parameters, because `BatchRewrites` reads Npgsql's rewrite of them (`FinalCommandText`, `PositionalParameters`).
    - The SQL text changes, but nothing records it (overview, rule 2).
  - **`OpenSql`** is built from `SettingNames`. Today the two are matched by index (30-34, 201).

## Item 9: the `Resolve` methods

`RowDescriptions.Resolve` (`RowDescriptions.cs:48`) and `BatchRewrites.Resolve` (`BatchRewrites.cs:48`) stay.  `RowDescriptionsTests.cs:14` and `BatchRewritesTests.cs:15` use them to pin the reflection against the pinned Npgsql.  Each gets a remark that says so.

## Item 10: `CaptureJson.Options`

`Capture/CaptureJson.cs:44-50` calls `MakeReadOnly(populateMissingResolver: true)` in its initializer.

The tool targets net8.0. There, a bare `MakeReadOnly()` throws when no `TypeInfoResolver` is set.

The written output does not change, and every user is read-only: `ReplayingExchange.cs:65`, `CaptureJsonTests.cs:169, 186` and `CallRecordTests.cs:63-68`.

A test checks that the options are read-only.

## Resolving TD-0053

Follow `docs/tech-debt/AGENTS.md`, "Resolving an item":
1. Delete the item file.
2. Remove its row from `docs/tech-debt/README.md`.
3. Run `grep -rn "TD-0053" .` and remove every mention outside `docs/superpowers/`. Those are history, like the mention in the TD-0054 design.

The item has no known issue in `docs/manual/`.

## Tests

New tests:
- the `BuiltInTypes` container test;
- the theory over the types that carry a modifier;
- the read-only options;
- `ColumnOrigin.FindAll`: every origin found, and the first one missing;
- `LogValue.Of(bool?)`, for true, false and null.

The rest is covered by the existing tests, which must pass unchanged, apart from tests that name a member this PR moves.

## Left to the builder

These are the builder's to decide:
- the names of the new types and members;
- the shape of the call helper, and whether check uses it;
- where the static builders go;
- the type that carries the checked rows;
- what `WalkAsync` groups;
- the commits.
