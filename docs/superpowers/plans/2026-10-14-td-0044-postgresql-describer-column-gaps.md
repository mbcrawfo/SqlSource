# TD-0044, small gaps of the PostgreSQL describer's columns - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close all of TD-0044. Each column's and each domain base's `name` holds its whole modifier as the server spells it, with or without an origin. A foreign table's column says `foreign-table`. The lexer reads a `$` or an `E'` after a non-ASCII character as PostgreSQL does. Then delete the item.

**Architecture:** `columns` also records each field's `TypeModifier`, read from Npgsql's internal `NpgsqlDataReader.RowDescription` by reflection, with no fallback. `types` also records `typtypmod`. A new call, `spellings`, asks `format_type(oid, typmod)` for the pairs the built types need. `TypeModifiers.Decode` gives the facets from the number. A capture without the new members spells exactly as today. Two small changes stand apart: one in `ColumnNullability`, and one in two quote readers of `src/SqlSource/Parsing/Quoting/`.

**Tech Stack:** C# (`net8.0` tool, .NET 10 tests), Npgsql 10.0.3, xunit v3 with Shouldly, Testcontainers PostgreSQL 14 and 18.

**Spec:** `docs/superpowers/specs/2026-10-14-td-0044-postgresql-describer-column-gaps-design.md`. Read it with this plan: the spec gives the reasons, and the plan gives the names and the tests.

## Global Constraints

- The new provenance value is exactly `foreign-table`. `formatVersion` stays `1`.
- The new method of the exchange is exactly `spellings`. The method names already recorded never change.
- A new member of a record in `PostgresCalls.cs` is nullable, comes last, and defaults to `null` in the positional list, so that existing constructions compile. Its null means what the record meant before it had the member (`src/SqlSource.Tool/AGENTS.md`, the paragraph on a capture).
- A null `TypeModifier` spells exactly as today: `ColumnFacets.Of` on Npgsql's three numbers, no element facets, and no `spellings` call. A null `Typtypmod` reads as `-1`.
- A parameter's top-level type never gets a modifier. Only a domain's `base`, and what lies under it, does.
- No fallback for the reflection. A missing member throws `InvalidOperationException` that names `NpgsqlDataReader.RowDescription` and Npgsql's assembly version.
- The tail rule throws `InvalidOperationException` when the server's text does not start with `FormatType`.
- Every commit passes `./pre-commit-validation.sh` (`CLAUDE.md`).
- Test names are `Method_Scenario_Outcome`, with Shouldly assertions, and the tests mirror the source folders (`tests/AGENTS.md`).
- Before the PR, run the version check that `CLAUDE.md` requires.

## Review Focus

These are the inputs most likely to reach a user that no task naturally tests. Each line names the task that adds its test.

1. **A domain over a domain over `varchar(254)`, as a parameter and as a column.** Each level's `base` carries the right modifier, and the top-level parameter carries none. *(Task 5)*
2. **An array of a domain, `email_t[]`, as a column.** The row description gives the array the domain's modifier. The element is the domain, its base is `character varying(254)`, and the array's own name stays `public.email_t[]`. *(Task 5)*
3. **A column whose type has a modifier but whose `typmodout` writes nothing**, so `format_type(oid, m)` equals `format_type(oid, -1)`. The tail is empty and nothing throws. *(Task 3)*
4. **An old capture of a column of `varchar(10)[]` or `interval(3)`**, with `TypeModifier` absent. It replays to today's sidecar, `character varying[]` and `interval(3)`, and asks no `spellings`. *(Task 5)*
5. **`format_type` of a built-in type when the user has a type of the same name on the `search_path`**, such as a `public.interval` domain. A `pg_catalog.interval(3)` column is still `interval(3)`. *(Task 6)*

---

### Task 1: The lexer reads a non-ASCII character as part of a name, before `$` and `E'`

**Files:**
- Modify: `src/SqlSource/Parsing/Quoting/QuoteReader.cs`, `DollarQuoteReader.cs`, `EscapeStringReader.cs`
- Test: `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs`, `tests/SqlSource.Tool.Tests/Describing/Postgres/OuterJoinGateTests.cs`
- Check: `README.md`, the table of what `ansi` reads. Update it only if a line says that `ansi` misreads this.

**Interfaces:**
- Produces: `QuoteReader.IsNameCharacterOfPostgreSql(char value) : bool` (protected static). It is `IsIdentifierCharacter(value) || value > '\u007F'`. `IsIdentifierCharacter` is unchanged, since `QuoteOperatorReader` (Oracle) keeps it.

- [ ] **Step 1: Write the failing tests.** Add them to `SqlLexerTests`, under `postgresql`, `cockroachdb`, `mysql` and `ansi` wherever the reader serves the dialect:
  - `Lex_DollarAfterANonAsciiCharacter_IsText`: `SELECT 1 AS §$x$ FROM t --$x$` has no `Quoted` lexeme, and its last lexeme is a comment.
  - `Lex_DollarTagWithANonAsciiCharacter_IsAQuote`: `SELECT $§$a$§$` holds one `Quoted` lexeme, `$§$a$§$`.
  - `Lex_EscapePrefixAfterANonAsciiCharacter_IsAPlainString`, for `postgresql`, `cockroachdb` and `ansi`: in `SELECT §E'\'' AS a, 'b'`, the first string ends at the second `'`. Assert the `Quoted` lexemes' texts.
- [ ] **Step 2: Add `IsGated_AliasThatEndsLikeADollarQuote_IsGated` to `OuterJoinGateTests`**, using the spec's query `SELECT o.total AS §$x$ FROM users u LEFT JOIN orders o ON o.user_id = u.id --$x$`. It should be `true`.
- [ ] **Step 3: Run them and see them fail.** Run `dotnet test --project tests/SqlSource.Tests -- --filter-method "*NonAscii*"` and the gate test.
- [ ] **Step 4: Add the helper.** In `DollarQuoteReader`, use it for the character before the `$` and for each character of the tag (`char.IsLetter(c) || c > '\u007F' || c == '_'` for the first, and the same with digits after). In `EscapeStringReader.HasPrefix`, use it for the character before the prefix. Update each reader's `<remarks>` in one sentence.
- [ ] **Step 5: Run the whole lexer suite and the gate tests.** Everything should pass, the `Lex_KnownLimit_*` tests included.
- [ ] **Step 6: Commit** with the message "Read a non-ASCII character before a dollar quote or an escape prefix as part of a name".

### Task 2: `foreign-table`

**Files:**
- Modify: `src/SqlSource/Snapshot/SidecarValues.cs` (`NullableSource.ForeignTable`), `src/SqlSource.Tool/Describing/Postgres/ColumnNullability.cs`, `schemas/sidecar-v1.schema.json` (the `nullableSource` enum), `docs/superpowers/specs/2026-10-07-sidecar-format-design.md` (line 85 and the enum near line 1135)
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/ColumnNullabilityTests.cs`, `NullabilityWalkTests.cs`, `tests/SqlSource.Tests.Postgres/Describer/ColumnTests.cs`

**Interfaces:**
- Produces: `SidecarValues.NullableSource.ForeignTable = "foreign-table"`.

- [ ] **Step 1: Write the failing tests.**
  - `Of_OriginIsAForeignTable_IsUnknownFromTheForeignTable`: relkind `f`, gated and not gated, gives `(null, "foreign-table")`. Relkind `v` and `m` keep `view`.
  - In `NullabilityWalkTests`, a foreign table's column on no null-extending side is `foreign-table`, and on one is `outer-join`.
  - A container test, `Describe_ColumnOfAForeignTable_IsUnknownFromTheForeignTable`, next to the foreign-partition test. Reuse its `postgres_fdw` loopback setup.
- [ ] **Step 2: Run them and see them fail.**
- [ ] **Step 3: Make the change.** Split relkind `f` out of the `view` arm of `ColumnNullability.Of`, and fix the comment there. In the format spec, add `"foreign-table"`, meaning the origin is a foreign table whose constraints PostgreSQL does not enforce, and change the precedence sentence to "Inside the walk `outer-join` beats `view` and `foreign-table`, which beat `catalog`." Add the value to the schema's enum.
- [ ] **Step 4: Run the tool's tests, the schema test and the PostgreSQL container tests.** Everything should pass.
- [ ] **Step 5: Commit** with the message "Say foreign-table for a column of a foreign table".

### Task 3: Decoding a modifier, and the tail rule

**Files:**
- Create: `src/SqlSource.Tool/Describing/Postgres/TypeModifiers.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresTypeNames.cs`
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/TypeModifiersTests.cs` (new), `PostgresTypeNamesTests.cs`

**Interfaces:**
- Produces: `TypeModifiers.Decode(string typname, int modifier) : PostgresFacets`. A modifier of `-1`, or a type outside the spec's list, gives `PostgresFacets.None`.
- Produces: `PostgresTypeNames.WithModifier(PostgresTypeRow row, string formatted) : string`. For `pg_catalog` it returns `formatted`. Otherwise it returns `Spell(row, PostgresFacets.None) + formatted[row.FormatType.Length..]`, and throws `InvalidOperationException` naming both texts when `formatted` does not start with `row.FormatType` (ordinal).

- [ ] **Step 1: Write `Decode_ModifierAsTheCatalogHoldsIt_IsItsFacets` as a theory with these rows** (typname, modifier, length, precision, scale):
  - `varchar`, 14 → 10, null, null
  - `bpchar`, 6 → 2, null, null
  - `bit`, 5 → 5, null, null
  - `varbit`, 8 → 8, null, null
  - `timestamptz`, 3 → null, 3, null
  - `time`, 0 → null, 0, null
  - `numeric`, 393222 → null, 6, 2
  - `numeric`, 329730 → null, 5, -2
  - `interval`, 2147418115 → null, 3, null
  - `interval`, 458751 → null, null, null (year to month)
  - `interval`, 470286339 → null, 3, null (day to second(3))
  - `varchar`, -1 → none
  - `int4`, 7 → none

  Before trusting these numbers, confirm each against a container with `SELECT atttypmod FROM pg_attribute …` for a table of those types, and fix a row that differs. The spec's bit layout is the authority.
- [ ] **Step 2: Write `WithModifier_*` tests.**
  - `pg_catalog`: `character varying(10)` is returned as is.
  - `public.geometry`, with `FormatType` `geometry` and formatted `geometry(Point,4326)`, gives `public.geometry(Point,4326)`.
  - A schema that needs quotes, `"My Schema".tagged(7)`.
  - An empty tail (Review Focus 3): formatted equal to `FormatType` gives `public.tagged`.
  - A formatted text with another prefix throws.
- [ ] **Step 3: Run them, see them fail, implement both functions, and run them again until they pass.** `ColumnFacets` stays as it is, for old captures.
- [ ] **Step 4: Commit** with the message "Decode a type modifier and spell its tail".

### Task 4: The records, the live calls and the reflection

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresCalls.cs`, `PostgresLive.cs`, `PostgresEvents.cs`
- Create: `src/SqlSource.Tool/Describing/Postgres/RowDescriptions.cs`
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/RowDescriptionsTests.cs` (new), `PostgresEventsTests.cs`; `tests/SqlSource.Tool.Tests/Describing/Capture/CallRecordTests.cs` covers the records with no change

**Interfaces:**
- Produces: on `PostgresColumnRow`, a last member `int? TypeModifier = null`, documented as "the modifier the row description gave, `-1` for none; null in a capture recorded before it was read".
- Produces: on `PostgresTypeRow`, a last member `int? Typtypmod = null`.
- Produces: `PostgresTypmodPair(long Oid, int Modifier)`, `PostgresSpellingsRequest(IReadOnlyList<PostgresTypmodPair> Pairs)`, `PostgresSpellingRow(long Oid, int Modifier, string FormatType)`, and `PostgresSpellingsAnswer(IReadOnlyList<PostgresSpellingRow>? Rows, PostgresServerError? Error)`.
- Produces: `PostgresLive.SpellingsAsync(PostgresSpellingsRequest asked, CancellationToken token) : Task<PostgresSpellingsAnswer>`.
- Produces: `RowDescriptions.ModifiersOf(NpgsqlDataReader reader) : IReadOnlyList<int>`, and `RowDescriptions.Resolve() : void`. `Resolve` finds every member or throws, and runs once, from a static initializer.
- Produces: `PostgresEvents.Spellings(IRunLog log, PostgresSpellingsRequest request, PostgresSpellingsAnswer answer, long durationMs)`, which writes the event `spellings` with `pairs`, then `rows` or `error`, then the duration, in the shape of `Types`.

- [ ] **Step 1: Write the failing tests.**
  - `Resolve_NpgsqlOfTheTool_FindsTheRowDescription`: `Should.NotThrow(RowDescriptions.Resolve)`.
  - A `PostgresEventsTests` case for `spellings`, in the shape of the `types` case.
- [ ] **Step 2: Run them and see them fail.**
- [ ] **Step 3: Implement `RowDescriptions`.** Reflect over `NpgsqlDataReader`'s non-public instance field `RowDescription`, and over its type's `Count` and indexer (public or not). Read each item as `Npgsql.BackendMessages.FieldDescription` and take `TypeModifier`. A comment says why the member is internal, that `Directory.Packages.props` pins the tool's Npgsql, and that this test fails on a bump that moves it.
- [ ] **Step 4: Wire the live calls.**
  - `ColumnsAsync` sets `TypeModifier` from `RowDescriptions.ModifiersOf(reader)[ordinal]`.
  - `TypesSql` adds `t.typtypmod` as the last column, and the reader sets `Typtypmod`.
  - `SpellingsAsync` runs `SELECT p.oid, p.typmod, pg_catalog.format_type(p.oid, p.typmod) FROM unnest($1::oid[], $2::int4[]) AS p (oid, typmod) ORDER BY 1, 2`, binding the two arrays as `TablesAsync` binds its `oid[]`, and returns an `NpgsqlException` as an `Error`, as the other calls do.
  - Add `Typtypmod` to the event row of `types` and `typeModifier` to the event column of `columns`, so the log shows what the capture holds.
- [ ] **Step 5: Run the tool's unit tests.** Everything should pass, `CallRecordTests` and `ReplayingExchangeTests` included.
- [ ] **Step 6: Commit** with the message "Read each column's type modifier, each type's typtypmod, and the server's spelling of a modifier".

### Task 5: Building types with their modifier, and the session's `spellings`

**Files:**
- Create: `src/SqlSource.Tool/Describing/Postgres/TypeSpellings.cs`
- Modify: `PostgresTypes.cs`, `PostgresColumns.cs`, `PostgresSession.cs` (all in `src/SqlSource.Tool/Describing/Postgres/`)
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresTypesTests.cs`, `PostgresColumnsTests.cs`, `PostgresSessionTests.cs`

**Interfaces:**
- Consumes: `TypeModifiers.Decode` and `PostgresTypeNames.WithModifier` (Task 3), and the records and `SpellingsAsync` (Task 4).
- Produces: `TypeSpellings`, a sealed class over `IReadOnlyDictionary<PostgresTypmodPair, string>`, with `static TypeSpellings None`, `static TypeSpellings From(IReadOnlyList<PostgresSpellingRow> rows)`, and `(string Name, PostgresFacets Facets) Of(PostgresTypeRow row, int modifier)`. A modifier of `-1` gives `(PostgresTypeNames.Spell(row, None), None)`. Any other gives `(WithModifier(row, spelled), Decode(row.Typname, modifier))`, and throws `InvalidOperationException` when the pair is missing.
- Produces: `PostgresTypes.Build(long oid, IReadOnlyDictionary<long, PostgresTypeRow> rows, int modifier, TypeSpellings spellings) : PostgresType`. The existing `Build(oid, rows, PostgresFacets)` stays, for a column with a null `TypeModifier`.
- Produces: `PostgresTypes.PairsOf(long oid, IReadOnlyDictionary<long, PostgresTypeRow> rows, int modifier) : IEnumerable<PostgresTypmodPair>`. These are the pairs that `Build` with the same arguments will look up, and no others.
- Produces: `PostgresColumns.Build(PostgresColumnRow column, ColumnOrigin? origin, IReadOnlyDictionary<long, PostgresTypeRow> rows, TypeSpellings spellings, bool gated)`.

The rules both `Build` and `PairsOf` follow:
- A non-array base type: one pair `(oid, modifier)` when the modifier is not `-1`.
- An array: its element with the array's modifier, and no pair of its own.
- A domain: its base with `row.Typtypmod` when that is neither null nor `-1`, else with the domain's own modifier.
- Enum, range, multirange and composite: their subtypes with `-1`, as today they get `PostgresFacets.None`.

- [ ] **Step 1: Write the failing `PostgresTypesTests`.** Use `PostgresRows`, adding rows for `varchar`, `_varchar`, `interval`, `numeric`, a `tagged` of schema `public` and its array, and a domain over a domain:
  - `Build_ArrayWithAModifier_GivesItsElementTheFacetsAndTheName`: `_varchar` with 14 is `character varying(10)[]`, and its element has `Length` 10.
  - `Build_TypeOfAnotherSchemaWithAModifier_IsTheSchemaNameAndTheTail`: `public.tagged(7)`, and its array `public.tagged(7)[]`.
  - `Build_IntervalWithFields_IsTheServersSpelling`: `interval year to month`, with no precision.
  - `Build_ParameterOfADomain_GivesItsBaseTheDomainsModifier`: `email_t` with `Typtypmod` 258 and modifier `-1` has `Base.Name` `character varying(254)` and `Base.Length` 254, while the domain's own `Name` is `public.email_t`.
  - `Build_DomainOverADomain_GivesEachBaseItsModifier` (Review Focus 1).
  - `Build_ArrayOfADomain_GivesTheDomainsBaseTheModifier` (Review Focus 2).
  - `PairsOf_EachCaseAbove_IsExactlyWhatBuildLooksUp`: build with a `TypeSpellings` holding only `PairsOf`'s pairs, and see that nothing throws.
  - `Build_PairNotSpelled_Throws`.
- [ ] **Step 2: Write the failing `PostgresSessionTests`.**
  - `DescribeAsync_ColumnsWithModifiers_AsksSpellingsOnceAfterTypes`: the script holds `spellings` with sorted, distinct pairs from a parameter's domain base and from two columns.
  - `DescribeAsync_NoModifierToSpell_AsksNoSpellings`.
  - `DescribeAsync_OldCaptureWithoutModifiers_IsDescribedAsBefore`: columns with `TypeModifier` null and Npgsql's numbers for `varchar(10)[]` and `interval(3)`, with `spellings` not in the script, give `character varying[]` and `interval(3)` (Review Focus 4).
  - `DescribeAsync_SpellingsRefused_IsSqlsrc225AtResolveType`.
- [ ] **Step 3: Run them and see them fail.**
- [ ] **Step 4: Implement the types side.** Write `TypeSpellings`, the new `Build`, and `PairsOf`. In `PostgresColumns.Build`, a non-null `TypeModifier` uses the new `Build` with `ColumnTypes.OidOf(...)` and that modifier. A null one keeps today's path through `FacetsOf`.
- [ ] **Step 5: Implement the session side.** After the `MissingType` check, gather the pairs:
  - `PairsOf(oid, rows, -1)` for each parameter;
  - `PairsOf(ColumnTypes.OidOf(...), rows, column.TypeModifier)` for each column whose `TypeModifier` is non-null.

  Sort them and keep each once. If there are any, ask `spellings` through a private `SpellingsAsync` shaped like `TypesAsync`, with `PostgresEvents.Spellings` and the method name `"spellings"`. An `Error` fails with `PostgresErrors.Types(error, request)`. `TypeParameters` uses the new `Build` with `-1`. Update the comment "A parameter has no facets" and the remarks of `PostgresTypes`, `PostgresFacets` and `ColumnFacets` to what is now true.
- [ ] **Step 6: Run the tool's unit tests.** Everything should pass. The fixture replays still pass at this point, because their captures have no modifiers.
- [ ] **Step 7: Commit** with the message "Spell a column's and a domain base's type with its modifier".

### Task 6: Fixtures and container tests

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/schema.sql`, and every `capture.json` and `expected.json` under that folder, through the recorder
- Create: cases `select-modifiers`, `select-modifiers-expression` and `select-domain-parameter` (each with `query.sql`, `capture.json` and `expected.json`)
- Modify: `tests/SqlSource.Tests.Postgres/Describer/ColumnTests.cs`, `ParameterTests.cs`

- [ ] **Step 1: Extend `schema.sql`.**
  - Add the type `tagged`: `CREATE TYPE tagged;`, then the `LANGUAGE internal` input and output functions over `varcharin` and `varcharout`, then `CREATE TYPE tagged (INPUT = …, OUTPUT = …, TYPMOD_IN = varchartypmodin, TYPMOD_OUT = varchartypmodout, LIKE = varchar)`. If `LANGUAGE internal` is refused on the image, stop and report it: the spec says it was verified.
  - Add a table `modifiers` with `ym interval year to month`, `ds interval day to second(3)`, `names varchar(10)[]`, `tag tagged(7)`, `tags tagged(7)[]` and `amount money_t`, where `money_t` is a new domain over `numeric(10,2)`.
- [ ] **Step 2: Write the three new cases.**
  - `select-modifiers`: every column of `modifiers`.
  - `select-modifiers-expression`: `'1 day'::interval day to second(3)`, and a `CASE` over `names[1]`.
  - `select-domain-parameter`: `WHERE email = @email`, where the parameter is inferred as `email_t`.
- [ ] **Step 3: Change the container tests.** In `ColumnTests`, turn the theory that pins what TD-0044 records (the rows near line 84) into the full spellings: `interval year to month`, `interval day to second(3)`, `character varying(10)[]` and `numeric(6,2)[]`, with the element facets asserted. Add the rest:
  - `Describe_ExpressionOfAnIntervalWithFields_KeepsTheFields`.
  - `Describe_ColumnOfATypeWithItsOwnModifier_IsTheSchemaNameAndTheTail`, for `tagged(7)`.
  - `Describe_BuiltInTypeHiddenOnTheSearchPath_IsStillTheCatalogsSpelling`: create a domain `public.interval` over `text`, and expect `interval(3)` for a `pg_catalog.interval(3)` column (Review Focus 5). Drop the domain in a `finally`.
  - In `ParameterTests`, `Describe_ParameterOfADomain_HasItsBaseWithTheDomainsLength`.
- [ ] **Step 4: Record the fixtures with `tools/record-fixtures.sh`.** Read the diff. The `expected.json` files should change only where a type has a modifier, plus a renumbered OID in a capture. Investigate any other change before you go on.
- [ ] **Step 5: Run `./pre-commit-validation.sh`, with the container tests on 14 and 18.** Everything should pass.
- [ ] **Step 6: Commit** with the message "Record the fixtures with the modifiers of columns and of a domain's parameter".

### Task 7: Documentation, and close TD-0044

**Files:**
- Modify: `src/SqlSource.Tool/AGENTS.md`, `docs/tech-debt/TD-0045-the-check-against-npgsql-is-best-effort.md`, `docs/tech-debt/README.md`
- Delete: `docs/tech-debt/TD-0044-small-gaps-of-the-postgresql-describers-columns.md`

- [ ] **Step 1: Rewrite `src/SqlSource.Tool/AGENTS.md`.**
  - The paragraph on a type's `name` gives the new rule:
    - a modifier spelled by the server, `format_type(oid, typmod)`;
    - the tool's `schema.name` and the tail outside `pg_catalog`;
    - an array's name, which is its element's and `[]`;
    - facets decoded by `TypeModifiers`;
    - a parameter's top-level type with no modifier, and a domain's `base` with the domain's `typtypmod`;
    - a capture without modifiers, which spells as before.
  - The paragraph on the calls adds `spellings` to the list of methods, and says that a column holds its `typeModifier`, read through Npgsql's internal `RowDescription` and pinned by `RowDescriptionsTests`, beside Npgsql's three numbers.
- [ ] **Step 2: Rewrite TD-0045.** In its last bullet, its proposed fix and its trigger, say in the present that the `name` of a column, of an `element` and of a `base` holds the whole modifier, and drop every mention of TD-0044. The trigger of the seventh becomes phase 8 alone.
- [ ] **Step 3: Delete the item.** Remove TD-0044's row from `docs/tech-debt/README.md`, delete its file, and run `grep -rn "TD-0044" src tests docs README.md --exclude-dir=bin --exclude-dir=obj`. Expected: hits only under `docs/superpowers/plans/` and `docs/superpowers/specs/`.
- [ ] **Step 4: Run `./pre-commit-validation.sh`.** It should pass.
- [ ] **Step 5: Commit** with the message "Close TD-0044".
