# TD-0044, small gaps of the PostgreSQL describer's columns - design

Date: 2026-10-14

Resolves [TD-0044](../../tech-debt/TD-0044-small-gaps-of-the-postgresql-describers-columns.md).  The item lists six gaps of the PostgreSQL describer's record of a column, and one more that its own proposed fix left open.  This document says what each becomes.  The numbers 1 to 6 below are the item's bullets, in order; "the no-origin gap" is the paragraph of its Impact that begins "One gap outlives the fix below".

## Goal

Close the whole item now, while few sidecars exist.  Each of the fixes changes what a sidecar holds, so each costs every user with such a column one failure of `describe --check` and one `describe`; the fewer sidecars there are, the less that costs.  Nothing is left behind as a new tech-debt item.

Success is:

- A column's `name` is spelled as `format_type(oid, typmod)` spells the type with its modifier, for every column the protocol gives a modifier, with an origin or without one: `geometry(Point,4326)` as `public.geometry(Point,4326)`, `character varying(10)[]`, `interval year to month`, `interval day to second(3)`, and `'1 day'::interval day to second(3)` as an expression.  An array's `element` and a domain's `base` carry the facets their modifier sets.
- A parameter of a domain over a type with a modifier has a `base` with that modifier, as a column of the domain has.
- A column of a foreign table has `nullableSource: "foreign-table"`.
- The query `SELECT o.total AS §$x$ FROM users u LEFT JOIN orders o ON o.user_id = u.id --$x$` is gated.
- `docs/tech-debt/TD-0044-*.md` and its row in `docs/tech-debt/README.md` are gone, and `grep -r "TD-0044" src tests docs` finds nothing outside `bin/`, `obj/` and the dated plans and specs of `docs/superpowers/`, which are history.
- `./pre-commit-validation.sh` passes, including the PostgreSQL container tests on 14 and 18.

Non-goals:

- A typed `modifier` member on the PostgreSQL type, or any other new member of the sidecar's type object.  The item's own analysis keeps the modifier in `name` until a reader needs it as data.
- What name generated code binds a parameter by.  That is [TD-0045](../../tech-debt/TD-0045-the-check-against-npgsql-is-best-effort.md)'s, for phase 8.
- Any other construct that the lexer reads differently from the server: [TD-0004](../../tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md)'s.
- Planning a query that the gate does not catch.

Anti-goals, which would make this a failure even if every test passes:

- A parameter's own `name` that gains a modifier.  `check` binds a parameter by that name (`PostgresCheckParameter.DataTypeName`), and Npgsql binds `timestamp(3) with time zone` as `timestamp without time zone`, the wrong type and no error (TD-0045's last bullet).  A parameter's top-level type never has a modifier: only a domain's `base` gains one, and `check` does not bind by it.
- A `name` that depends on the session's `search_path` for a type outside `pg_catalog`.
- A capture recorded before this change whose columns or types replay to a different spelling or facets, or that throws `CaptureMissException` because of `spellings`, `TypeModifier` or `Typtypmod`.  Captures are a contract, and from phase 10 a user's bug bundle is one.  The anti-goal covers the modifier work alone (my call, after the builder check): what the describer concludes from answers it already had may change where this fix means it to, so an old capture of a foreign table replays to `foreign-table`, and an old capture of a query that the lexer now reads differently (gap 6) is of a different query, whose hash changes.
- A silent fallback.  If Npgsql's internal member that holds the row description is not there, the describe fails as a bug of the tool; it never quietly spells a name the old way, which would change sidecars without a word and fail every `describe --check`.

## Decisions

| Gap | Choice | Whose |
|----|----|----|
| All | Close all six and the no-origin gap; leave no new tech-debt item. | **Yours** |
| 1, 2, 3, no-origin | The modifier of every column comes from the row description, read through Npgsql's internal `NpgsqlDataReader.RowDescription` by reflection.  This replaces the item's Step A and Step B for columns: the catalog's `atttypmod` is not read. | **Yours** |
| Reflection | No fallback.  A missing member throws, and a test pins it, so that a bump of Npgsql fails in CI. | **Yours**, on my recommendation |
| 1, 2, 3 | The server spells each `(type, modifier)` pair with `format_type`, in one new call.  The tool keeps `schema.name` for a type outside `pg_catalog` and appends the modifier's tail. | Mine |
| Facets | `length`, `precision` and `scale` are decoded by the tool from the modifier, no longer from Npgsql's three numbers. | Mine |
| 5 | `types` reads `pg_type.typtypmod`, and a domain's `base` is built with it. | Mine, the item's own fix |
| 4 | A new provenance value, `foreign-table`.  Additive: `formatVersion` stays 1. | Mine, the item's own fix |
| 6 | The lexer: a `$` after any non-ASCII character opens no dollar quote, and a tag may hold one.  The same rule for the prefix of `E'...'`. | Mine for the dollar quote; **yours** for `E'...'`, after the builder check |
| Old captures | A column whose recorded modifier is null spells exactly as today, from Npgsql's three numbers. | Mine |
| Shape of the work | One spec, one plan, one pull request. | Mine |

## Gaps 1, 2, 3 and the no-origin gap: the modifier of a column

### Reading it

`PostgresLive.ColumnsAsync` describes through `ExecuteReaderAsync(CommandBehavior.SchemaOnly)`.  The reader holds the row description in the internal member `NpgsqlDataReader.RowDescription` (a field in 10.0.3, of the internal type `RowDescriptionMessage`); each of its fields is a public `Npgsql.BackendMessages.FieldDescription`, whose public `TypeModifier` is the `atttypmod` the server sent, `-1` for none.  Read the member by reflection, once per process (a static that resolves the `FieldInfo`), and take `TypeModifier` of the field at each column's ordinal.  Whether the indexer of `RowDescriptionMessage` is public or the fields are reached another way is the builder's to find; the item verified the field and the property for 10.0.3 and 11.0.0-preview.1.

If the member, its type or the property is not found, or the value is null after a describe that gave columns, throw `InvalidOperationException` naming the member and the version of Npgsql.  The run reports it as a bug of the tool, `SQLSRC200`, as it reports any exception of a describer.  A unit test in `tests/SqlSource.Tool.Tests` resolves the member without a server, so that a bump of Npgsql in `Directory.Packages.props` fails before any container runs; a container test asserts the modifier of a known column.  A comment at the reflection says why it is there, and that `Directory.Packages.props` pins the tool's own Npgsql.

`PostgresColumnRow` gains `int? TypeModifier`, after the members it has.  The three numbers of Npgsql are still read and recorded: they cost nothing, and a capture that holds both is easier to read when a spelling is questioned.

The row description gives the modifier of the type it reports: for a column of a domain, the base type with the domain's modifier; for an array, the array's type with the element's modifier.  `format_type` reads both that way.

### Spelling it: a new call

The modifier's text belongs to the type's `typmodout` function, which only the server has for a type of an extension.  So the server spells it.  A new method of the exchange, `spellings`, asked after `types`, when there is at least one pair to spell:

- Request `PostgresSpellingsRequest(IReadOnlyList<PostgresTypmodPair> Pairs)`, each pair `(long Oid, int Modifier)`, sorted by OID then modifier, each once.
- Answer `PostgresSpellingsAnswer(IReadOnlyList<PostgresSpellingRow>? Rows, PostgresServerError? Error)`, each row `(long Oid, int Modifier, string FormatType)`.
- SQL, roughly: `SELECT p.oid, p.typmod, pg_catalog.format_type(p.oid, p.typmod) FROM unnest($1::oid[], $2::int4[]) AS p(oid, typmod) ORDER BY 1, 2`, with the two arrays as parameters as `types` passes its OIDs.

The pairs are every `(oid, modifier)` with a modifier other than `-1` that the built types will need: a non-array base type with the column's modifier; for an array, its element with the array's modifier; for a domain, its base with the modifier the rule of domains below gives.  An array is never itself spelled by the server: its name stays `PostgresTypeNames.ArrayOf(element.Name)`, so one rule spells an array of a built-in type and of an extension's type alike.  A replay of an old capture holds no modifiers, finds no pair, and does not ask `spellings`.

An error of `spellings` is handled as an error of `types` is today; the builder copies what `PostgresSession` does there.

### From the server's text to `name`

- A type of `pg_catalog`: the server's text as it is.  `format_type(bpchar, 14)` is `character(10)`, `format_type(bit, 5)` is `bit(5)`, `format_type(interval, …)` is `interval year to month`.  The tool's own facet spelling in `PostgresTypeNames.InCatalog` stays for a modifier of `-1` and for the replay of an old capture.
- A type of any other schema: the tool's `schema.name`, then the tail.  The tail is what `format_type(oid, typmod)` holds after `format_type(oid, -1)`, which `types` already holds as `FormatType`.  If the server's text does not start with that prefix, throw `InvalidOperationException`: the rule of the tail was verified for PostGIS and for a type made over `varcharin`, and a type that breaks it is a bug to see, not a name to guess.  `bit`, `character` and the time types, whose spelling `format_type` builds itself and for which the rule is wrong, are all of `pg_catalog`.

### Facets

`PostgresFacets` is decoded from the modifier by the tool, for the built-in types that carry one, by internal name (a new decoder beside or replacing `ColumnFacets.Of`; the builder chooses):

- `varchar`, `bpchar`: `length` is the modifier less 4.
- `bit`, `varbit`: `length` is the modifier.
- `time`, `timetz`, `timestamp`, `timestamptz`: `precision` is the modifier.
- `numeric`: with `m` the modifier less 4, `precision` is `(m >> 16) & 0xFFFF` and `scale` is the low 11 bits of `m`, read as signed (`((m & 0x7FF) ^ 1024) - 1024`).
- `interval`: `precision` is `m & 0xFFFF`, and `65535` is none.  The fields are in the high bits and land only in `name`.
- Any other type, and a modifier of `-1`: no facets.

This decoding replaces Npgsql's three numbers for every capture that holds a modifier, so the three quirks that `ColumnFacets`' remarks list stop applying there.  An element of an array gets the facets of the array's modifier, read by the element's internal name: that is gap 2.

### Old captures

A column whose `TypeModifier` is null was recorded before this change.  It is spelled exactly as today: `ColumnFacets.Of` on Npgsql's three numbers, the array with no element facets, no `spellings`.  A unit test replays a capture without the member and asserts today's sidecar.

## Gap 5: a domain's modifier for a parameter

`types` reads `t.typtypmod` into `int? Typtypmod` on `PostgresTypeRow`, after its members; null in an old capture means `-1`.

`PostgresTypes.Build` takes a modifier where it took facets today, and builds a domain's `base` with the domain's `typtypmod` when that is not `-1`, else with the modifier the domain was built with.  For a column of a domain the second is the row description's modifier, which is the domain's; for a parameter it is `-1`, so the domain's own counts.  A domain over a domain over `varchar(254)` is built the same way at each level.  A parameter's top-level type is never given a modifier (see the anti-goals).

The pairs a parameter's domain bases need go into the same `spellings` request as the columns'.

## Gap 4: a foreign table

`SidecarValues.NullableSource` gains `ForeignTable = "foreign-table"`.  `ColumnNullability.Of` answers `(null, ForeignTable)` for an origin of relkind `f`, and keeps `view` for `v` and `m`.  The builder checks `NullabilityWalk` for any other place that turns relkind `f` into `view`.

The format design's section on `nullableSource` (`docs/superpowers/specs/2026-10-07-sidecar-format-design.md`, line 85 and the enum at line 1135) and `schemas/sidecar-v1.schema.json` gain the value.  It is additive under the format's own test (section on versioning): a reader ignores an unknown provenance value, and `--check` does not compare `nullableSource`.  `formatVersion` stays `1`.  `NullabilityWalk` calls `ColumnNullability.Of` for any origin it finds, so the walk answers `foreign-table` too, or `outer-join` when a join extends the column: the sentence on precedence becomes "Inside the walk `outer-join` beats `view` and `foreign-table`, which beat `catalog`."

## Gap 6: the gate and the lexer

The gate is right to read the lexer's lexemes; the lexer is wrong about `§$x$`.  PostgreSQL and MySQL read any character outside ASCII as an identifier character, and a `$` inside an identifier opens nothing.  `DollarQuoteReader` already refuses to open a quote after an identifier character, by `QuoteReader.IsIdentifierCharacter`, which is `char.IsLetterOrDigit` or `_` or `$`, so `§`, `€` and the like are not counted.

- In `DollarQuoteReader`, a character above U+007F before the `$` counts as an identifier character, and a tag may hold one: `$§$ … $§$` is a dollar quote, as it is on the server.  Leave `QuoteReader.IsIdentifierCharacter` as it is for the readers of other dialects, Oracle's `q'...'` among them, whose servers do not read every non-ASCII character as part of a name.
- The same rule for the prefix of `EscapeStringReader` where it serves PostgreSQL and CockroachDB: `§E'\''` is the identifier `§e` and a plain string on the server, and an escape string to the tool today, which moves where the string ends.  This is the same misreading one reader over, and it can hide an outer-join word from the gate the same way.

This changes how the generator reads such a file under `postgresql`, `cockroachdb` and `mysql`, toward what the server reads.  `ansi` shares the same reader instances and changes with them (my call, after the builder check): it already reads dollar quotes as PostgreSQL does, and a second instance or a setting to keep its old misreading buys nothing.  `README.md`'s table of what `ansi` reads is checked for a line to update.  Tests in `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs` for `§$x$ … $x$` (no quote), `$§$ … $§$` (a quote), `€$1` and the `E` prefix; and an `OuterJoinGate` test with the item's query.

## Fixtures and tests

`tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/schema.sql` gains what the cases need, without an extension:

- A base type with a modifier of its own: input and output `LANGUAGE internal` over `varcharin` and `varcharout`, `TYPMOD_IN = varchartypmodin`, `TYPMOD_OUT = varchartypmodout`, spelled `public.tagged(7)` for a column of `tagged(7)`.
- Columns of `interval year to month`, `interval day to second(3)`, `varchar(10)[]`, `tagged(7)` and `tagged(7)[]`.
- A domain over `numeric(10,2)` or a time type with a precision, so that more than a `varchar` length is decoded through a parameter.

New fixture cases at least for: each of those columns read from a table; an expression without an origin, `'1 day'::interval day to second(3)` and a `CASE` over a `varchar(10)`; a parameter of `email_t`.  A foreign table needs `postgres_fdw`, so it stays a container test in `tests/SqlSource.Tests.Postgres/Describer/ColumnTests.cs`, beside the foreign partition one there.

A change to `schema.sql` moves the OIDs that the captures hold, so every case is recorded again with `tools/record-fixtures.sh`, and the diff is read: the expected sidecars change only where a type has a modifier.

Unit tests of the decoder against the numbers the item verified on the catalog, of the tail rule and of its refusal, of the rule of domains, and of the replay of an old capture.

## Documentation

- `src/SqlSource.Tool/AGENTS.md`: the paragraph on a type's `name` (it names TD-0044) is rewritten for the new rule: the server's spelling of a modifier, the tail for a type outside `pg_catalog`, the facets decoded by the tool, a parameter's top-level type with none, and a domain's `base` with the domain's.  The paragraph on the calls lists `spellings`, and says a column of `columns` holds its modifier as the row description gave it, read through Npgsql's internal member, beside Npgsql's three numbers.
- `ColumnFacets`, `PostgresFacets`, `PostgresTypeNames` and `PostgresTypes`: their remarks say what is now true.  `PostgresFacets`' "A parameter has none" changes.
- `docs/tech-debt/TD-0045-the-check-against-npgsql-is-best-effort.md`: its last bullet, its proposed fix and its trigger speak of "the fix of TD-0044" in the future.  Rewrite them in the present, without the link: the `name` of a column, an element and a base holds the whole modifier now.  Its trigger for the seventh becomes phase 8 alone.
- `docs/tech-debt/README.md`: remove TD-0044's row.  Delete the item's file.

## What is left to the builder

- How `RowDescriptionMessage` is walked, and whether the reflection lives in `PostgresLive` or a small class of its own.
- Whether the decoder replaces `ColumnFacets.Of` or sits beside it for the replay of old captures.
- Names of the new records and members, within the rule of a record in `PostgresCalls.cs` and its test in `CallRecordTests.cs`.
- The order of tasks, and how the fixture cases are named.

## Not verified

From the item, still open and to be watched in the tests: an extension other than PostGIS; that a column with an origin has its attribute's modifier under every plan (the row description is now the source, which makes this moot for spelling); and what `format_type` writes for a type of `pg_catalog` that another type hides on the `search_path`.  For the last, a container test that creates `public.interval` as a domain and asserts that a column of `pg_catalog.interval(3)` is still spelled `interval(3)`.
