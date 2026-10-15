# TD-0045, the gaps of the check against Npgsql - design

Date: 2026-10-15

Resolves [TD-0045](../../tech-debt/TD-0045-the-check-against-npgsql-is-best-effort.md).  The item lists seven gaps of the PostgreSQL describer's check of its lexer against Npgsql, and of the name a parameter is bound by.  This document says what each becomes.  The numbers 1 to 7 below are the item's bullets, in order.

## Goal

Close the whole item now.  Gap 5 changes what a sidecar holds, and it is cheapest while few sidecars exist, as it was for TD-0044.  Nothing is left behind as a new tech-debt item; what this fix leaves undone on purpose is planned work, recorded as a new deferred item, D-0003.

Success is:

- Every parameter's type in a PostgreSQL sidecar carries `bindName`, the name Npgsql binds it by, and `check` binds by it.
- A query whose parameter is of a type with a quoted name or schema, of plain `bit`, or of a domain over a domain is described, where today it is `SQLSRC232`.  So is `"char"` and an array of a type whose name is 63 characters long, if Npgsql takes some name for them (see Gap 5).
- A query in which Npgsql places a parameter anywhere SqlSource does not, or the other way round, and whose columns are the same, fails with `SQLSRC231`.  That includes a second occurrence: `@id` and, later, `arr[1:id]` where `id` is a column.
- An error of `check` that is no disagreement, a lost connection or a server error outside class `42`, is reported as what it is and not as `SQLSRC231`.  A table dropped between two calls gives a class-`42` error and stays `SQLSRC231`, whose help says that a schema changed during the run shows the same way (see Gap 4).
- `SQLSRC231` says, in its help, when a parameter that is an array of a domain is the likely cause.
- `docs/deferred/D-0003-*.md` exists and is in `docs/deferred/README.md`.
- `docs/tech-debt/TD-0045-*.md` and its row in `docs/tech-debt/README.md` are gone, and `grep -r "TD-0045" src tests docs schemas` finds nothing outside `bin/`, `obj/` and the dated plans and specs of `docs/superpowers/`, which are history.
- `./pre-commit-validation.sh` passes, including the PostgreSQL container tests on 14 and 18.

Non-goals:

- Sending a value of a type that Npgsql cannot write at all: a composite type and its array, a table's row type and its array, `regclass`, `aclitem`, `txid_snapshot`, `pg_node_tree` and the other types of the system catalogs that Npgsql does not load.  These stay `SQLSRC232`.  Making them work is D-0003's.
- Changing `name`.  It stays the type as the server spells it, quotes and all.  `bindName` sits beside it.
- Generated code.  Phase 8 binds by `bindName`; this fix records the name and proves it with `check`, and changes the epic's note on phase 8 to say so.
- The SQL Server describer.  It does not exist yet.  This fix states the rule `bindName` follows for every engine, and phase 6 applies it.
- Any other construct that the lexer reads differently from the server: [TD-0004](../../tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md)'s.

Anti-goals, which would make this a failure even if every test passes:

- A `bindName` that is PostgreSQL's alone in shape.  It is a key of the format's shared convention, like `name`, with a meaning every engine can fill in or leave out.
- A `bindName` that holds a modifier, or that is written on a column's type or on an `element`, `base` or `subtype`.  Gap 7 is closed by there being no such name to bind by.
- Rewriting the user's SQL so that a parameter can be bound.  Generated code sends the SQL as written; a type that can be bound only with a cast in the SQL is not bound, and stays `SQLSRC232`.
- A `bindName` that `check` did not prove.  Every name that reaches a sidecar is one Npgsql took in this run.
- An old capture, recorded before this change, that throws `CaptureMissException` or replays to a different verdict for a query that was described.  Old captures of a query that was `SQLSRC232` may change, which is the point of the fix.

## Decisions

| Gap | Choice | Whose |
|----|----|----|
| All | Close all seven; leave no new tech-debt item. | **Yours** |
| 5, unbindable | A type Npgsql cannot write at all stays `SQLSRC232`, documented as a known limit.  A future mechanism for custom type conversion is recorded as D-0003, covering parameters, models and generated query code. | **Yours** |
| 5, 7 | A new key, `bindName`, on a parameter's top-level type, which `check` binds by and phase 8 will. | **Yours**, on my recommendation |
| 5, 7 | `bindName` is a shared convention of the format, not a key of PostgreSQL's type object alone. | **Yours** |
| Key name | `bindName`. | Mine |
| 1 | Compare every place Npgsql put a parameter with every place SqlSource did, by comparing Npgsql's rewritten text with the tool's `$n` text.  Read by reflection, with no fallback and a test that a bump of Npgsql fails. | Mine, as TD-0044 did for the row description; occurrences and not names, after the builder check |
| 2 | No code of its own: gap 1's comparison of occurrences catches a `:id` that Npgsql reads and the server accepts, and the server refuses the rest. | Mine, after the builder check |
| 4, class 42 | A class-`42` error stays `SQLSRC231`, a dropped table included; no extra call to tell the two apart.  Npgsql's own refusal of the text stays `SQLSRC231`. | Mine, after the builder check |
| 5, hidden built-in | `bindName` of a type of `pg_catalog` does not depend on the session, even where `name` does (TD-0050). | Mine, after the builder check |
| 5, a dot in a name | A type whose schema or name holds a `.` is not bound: `SQLSRC232`.  The unquoted form cannot say where the schema ends. | Mine, after the builder check |
| 3 | A second help line on `SQLSRC231`. | Mine, the item's own fix |
| 4 | An error of `check` that is no syntax or name error is reported as other steps report it. | Mine, the item's own fix |
| 6 | No code.  The round trips happen only after `check` has already failed. | Mine |
| Old captures | A recorded `check` answer without Npgsql's parameters compares columns alone, as today. | Mine |
| Shape of the work | One spec, one plan, one pull request. | Mine |

## Gap 5 and gap 7: `bindName`

### The convention, for every engine

Section 2 of the [sidecar format](2026-10-07-sidecar-format-design.md) gives every engine's type object a required `name`.  It gains a second shared key:

| Key | Type | Written | Meaning |
|----|----|----|----|
| `bindName` | string | on a parameter's top-level type, when the engine's driver binds a parameter by a type name | The name by which the engine's .NET driver binds a parameter of this type, as that driver takes it.  It never holds a modifier.  It is never written on a column's type, nor on a type nested in another (`element`, `base`, `subtype`). |

Each engine's section says when it is written and what it holds.  An engine whose driver binds every type by an enum, and never by name, writes none.

- **PostgreSQL**: always written on a parameter's type.  The `NpgsqlParameter.DataTypeName` that `check` bound the parameter by.
- **SQL Server**, for phase 6 to settle: the expected shape is the `SqlParameter.TypeName` of a table type, or the `UdtTypeName` of a CLR type, and no `bindName` for a type that `SqlDbType` names.  Section 2.3 says that phase 6 decides.

The key is additive under section 3: `formatVersion` stays 1, an older reader ignores it, and the JSON Schema gains it on each engine's type object as an optional string.  `--check` compares a type as a whole, `bindName` included, since it changes what generated code does.  Every existing PostgreSQL sidecar that has a parameter fails `describe --check` once and needs one `describe`.  The new `toolVersion` makes `describe` re-describe every query anyway.

In the model, the builder chooses whether `BindName` is a member of `SidecarType` or of each engine's record.  The reader reads the key where it is written and nowhere else.  The writer writes it after `name`.  Key order otherwise follows section 2.2's table.

### What PostgreSQL's `bindName` is

The tool computes a candidate for each parameter's type from the catalog rows it already holds.  `check` binds by it, and only a candidate Npgsql takes is recorded.

| The type | Its `bindName` |
|----|----|
| Of `pg_catalog`, no array | The type's SQL spelling without a modifier: `integer`, `character varying`, `timestamp with time zone`.  This is `name` in the usual case.  Where a user type of the same name hides the built-in on the session's `search_path`, `name` is `pg_catalog.text` (TD-0050); `bindName` is still the spelling the session-independent rule gives, which the builder settles by trying `text` and `pg_catalog.text` against Npgsql, choosing one that binds the built-in in either session.  TD-0050 stays open: it is about `name`. |
| Plain `bit` | `bit`.  `name` stays `"bit"`, which is the server's spelling, since plain `bit` in SQL means `bit(1)`. |
| Of any other schema | `schema.name` with neither part quoted: `My Schema.Status`, which Npgsql takes.  For a name that needs no quotes this equals `name`.  If the schema or the name holds a `.`, the unquoted form is ambiguous, and Npgsql could bind another type that exists: no candidate, and the parameter is `SQLSRC232`. |
| A domain over a domain | The `bindName` of its innermost base that is no domain.  Npgsql sends a domain as its base type anyway; it refuses only the nesting. |
| An array | The element's `bindName` and `[]`. |
| `"char"` | A name Npgsql takes for it, if one exists: the builder tries `"char"`'s other spellings (`char`, `pg_catalog.char`, Npgsql's own `DataTypeName` for `NpgsqlDbType.InternalChar`).  If none binds a `"char"`, the type stays `SQLSRC232`. |
| An array of a type whose name is 63 characters long | A name Npgsql takes, if one exists; the builder tries the unqualified `name[]`.  If none does, or the one that does depends on the session's `search_path`, it stays `SQLSRC232`. |
| Anything else | The same rules.  A composite type, a table's row type and the catalog types Npgsql does not load get a candidate that Npgsql refuses, and stay `SQLSRC232`. |

Rules that hold for every row:

- The candidate never depends on the session's `search_path`, like `name`.
- The candidate never holds a modifier.  A parameter's type has none: the protocol gives a parameter an OID and no modifier.
- The tool never guesses a cast.  A type that binds only if the SQL casts it is not bound.

What the item calls the round trip per parameter (gap 6) stays.  It is how `SQLSRC232` names the parameter Npgsql refused.

### `check`

`PostgresCheckParameter.DataTypeName` becomes the candidate `bindName`, not the type's `name`.  `SQLSRC232` names the type by its `name`, as the SQL would spell it, and says in its server line what Npgsql said.  Its help no longer needs the quoted-name variant (`BindQuotedHelp`): a quoted name is now bound.  The remaining help says the type cannot be sent by Npgsql, that a future version may let a project declare a conversion, and that a `text` parameter with a cast in the SQL works today.

### Docs

- `docs/diagnostics.md`, `SQLSRC232`: the list of refused types shrinks to the ones still refused, and the text says plainly that this is a known limit and not a fault of the query.
- `docs/superpowers/specs/2026-10-07-sidecar-format-design.md`, sections 2.1, 2.2, 2.3, 3 and 5 (the schema), and the example of section 4.1: `bindName` on each parameter's type.
- `schemas/sidecar-v1.schema.json`: the key.
- The epic design, phase 8's technical note "bound by setting `NpgsqlParameter.DataTypeName` to the type's name from the sidecar", becomes "to the type's `bindName`".  The note on phase 3's parameters that `DataTypeName` rejects `int` and flattens a domain stays.
- `PostgresTypeNames`'s remark that a reserved word is not quoted because "the name is the one that Npgsql binds" is out of date once `bindName` exists.  Whether `name` keeps the reserved word bare stays as it is (no change to `name`); the remark gives the true reason or is cut.

## Gap 1: compare the parameters

After the check's describe, read the text Npgsql sent, its rewrite of the SQL with `$n` in place of each parameter it found, and compare it with the text the tool prepared, which `ParameterPlaceholders` made the same way from the lexer's parameters.  The two agree when every place one put a parameter the other did too, and the parameter there is the same.  Comparing distinct names would miss a second occurrence that only Npgsql reads: `@id` and, later, `arr[1:id]` where `id` is a column, which Npgsql rewrites to `arr[1:$1]` when the command has a parameter `id`, which the server accepts, and which gives the same columns.  A count of the server's `ParameterDescription` misses it too, and is no fallback.

Npgsql 10.0.3 holds the rewritten text in the internal `NpgsqlBatchCommand.FinalCommandText`.  The two texts may number the parameters differently, the tool's by the lexer's order and Npgsql's by the order of the command's parameters, and Npgsql may change the text in other ways (a trailing `;`).  The builder normalises both so that only the places and identities of the parameters are compared: for example, by mapping each `$n` of Npgsql's text through the parameter list `check` sent, and each of the tool's through the lexer's list, to names.  A parameter that only a `-- param:` marker declares is in neither text.

The member is read by reflection, resolved once per process.  If it is missing, or its value is null after a describe that succeeded, throw `InvalidOperationException` naming the member and the version of Npgsql: a bug of the tool, `SQLSRC200`.  A unit test resolves the member without a server, so a bump of Npgsql in `Directory.Packages.props` fails before any container runs.  This is the pattern TD-0044 used for `NpgsqlDataReader.RowDescription`; reuse its helper if one fits.

`PostgresCheckAnswer` gains Npgsql's text as a new member after the ones it has.  `NpgsqlCheck.Read` compares it before the columns, and a difference is `differ`, with text that says where they first part, such as "at offset 41, Npgsql placed @id and SqlSource nothing".  A recorded answer that lacks the member, an old capture, compares columns alone.  The new member holds no more of the user's SQL than the request already does, so nothing about redaction changes.

## Gap 3: an array of a domain

When `NpgsqlCheck` finds that a column's type differs, and a parameter of the query is an array whose element is a domain, `SQLSRC231`'s help says that such a parameter is sent by Npgsql as an array of the domain's base type, and a cast of the column, `@p::varchar[]`, is the fix.  A failure has one help line, and the help about a `@` is wrong for this case, so this help replaces it.  The prose that `docs/diagnostics.md` already has stays, and points at the line.

## Gap 4: an error of `check` that is no disagreement

`PostgresErrors.Check` reports the error of `check` as `SQLSRC231` only when it is what a disagreement looks like:

- an error of the server whose SQLSTATE is of class `42`, syntax error or access rule violation (`42601` syntax, `42P01` undefined table, `42703` undefined column, `42883` undefined function and so on, since a misread `@` gives any of them);
- Npgsql's own refusal of the text, which `RefusedAsync` answers when no single parameter is refused, such as a `;` followed by more text (the case of `CheckAsync_TextNpgsqlWouldSplit`).  It has no SQLSTATE, so the answer must mark it apart from a lost connection; the builder adds a member or a kind to the record rather than matching a message.

Any other error of the server, and any other error without a SQLSTATE (a lost connection, a timeout), is reported as `SQLSRC225` at the step `check parameters`, through `Rejected`, as the other steps report theirs.  The builder checks whether a lost connection has a reporting of its own in the session and uses it if it applies.

A table, column or type dropped by another session between `columns` and `check` gives a class-`42` error and stays `SQLSRC231`: telling it apart would take another call, and it is rare.  `SQLSRC231`'s help says that a schema changed during the run shows the same way, and to run `describe` again.

[TD-0049](../../tech-debt/TD-0049-describe-behind-a-pooler-can-reach-another-backend.md) says a pooler can show as `SQLSRC231`.  If this changes which error it shows as, the builder rewrites its text to match, and the `SQLSRC231` prose about poolers in `docs/diagnostics.md` with it.

## Gaps 2 and 6: no code

- Gap 2: a query that writes both `@id` and `:id` for one parameter is prepared by SqlSource with `:id` left as text.  Where that is no valid SQL the server refuses it, `SQLSRC225`.  Where it is, `arr[1:id]` with a column `id`, gap 1's comparison of the two texts sees that Npgsql placed a parameter there and SqlSource did not, `SQLSRC231`.
- Gap 6: the extra round trips happen only after Npgsql has refused, and they are how `SQLSRC232` names the parameter.

Both are removed with the item.

## D-0003: custom type conversion

A new deferred item, `D-0003-custom-type-conversion.md`, with the sections of `docs/deferred/AGENTS.md`:

- **Planned**: the [epic design](2026-10-07-query-generation-epic-design.md) plans a project-level type override (phase 5), map rows that convert a value with a read and a write expression (phase 7), and binding by the sidecar's type name (phase 8).  It says a type the driver cannot read is a diagnostic that names the type and the override.  It plans nothing for a type the driver cannot *write*, nor a conversion a user declares for one.
- **Delivered instead**: `SQLSRC232` for a parameter whose type Npgsql cannot bind, linking `PostgresErrors` and the diagnostic.  The workaround is a `text` parameter with a cast in the SQL.
- **Why deferred**: a general mechanism is a design of its own across three parts of the epic, larger than closing TD-0045.
- **Remaining work**: a way for a project to declare a conversion for a database type, applied consistently in three places:
  - **parameters**: what C# type a parameter of that database type takes, and what the driver is sent, such as text, with a type the driver can bind, possibly with a server-side cast or the driver's own mapping (`MapComposite`);
  - **models**: what C# type a column of that type maps to;
  - **generated query code**: how a value is bound and read.
  It must work per engine, and say what the tool's `check` binds by when a conversion applies.  The types it would cover today on PostgreSQL are the ones still refused by `SQLSRC232`, listed in `docs/diagnostics.md`.
- **Trigger**: phases 5, 7 and 8, whichever designs the override first; or a user's report of `SQLSRC232` for a composite type.

Its row: status `Open`, added `2026-10-15`, planned in the epic design, description "A conversion a project declares for a database type, for parameters, models and generated query code, so that a type the driver cannot write can be used".  `Next id` becomes `D-0004`.

## Fixtures and tests

- Unit tests in `tests/SqlSource.Tool.Tests` for the candidate `bindName` of each row of the table above, for `NpgsqlCheck` comparing parameters (and an answer without them), for gap 3's help line, and for gap 4's choice between `SQLSRC231` and `SQLSRC225`.
- A unit test that resolves the reflected member of gap 1 without a server.
- Container tests, on 14 and 18, that a quoted schema and type, plain `bit`, and a domain over a domain are described with the expected `bindName`; that a composite type, a table's row type and `regclass` are still `SQLSRC232`; whatever the builder found for `"char"` and the 63-character array; and a query that Npgsql reads with other parameters and the same columns fails with `SQLSRC231`.  Rewrite the existing `SQLSRC232` tests of the types that now bind.
- Sidecar reader, writer, comparer and schema tests for `bindName`, including that it is not read on a column's type.
- Captures and fixtures that change are re-recorded with the existing scripts.  An old capture of a described query replays unchanged (the anti-goal).

## Documentation

- `docs/diagnostics.md`: `SQLSRC231` (gaps 1, 3, 4), `SQLSRC232` (gap 5) and `SQLSRC225` (gap 4, a new paragraph for the step `check parameters`).
- The sidecar format design, its schema and the epic, as under Gap 5.
- `src/SqlSource.Tool/AGENTS.md` references TD-0045: rewrite that passage to state the rule as it now stands.
- The `NpgsqlCheck` remarks: the check is no longer called best effort for parameters.  What it still cannot see, a `$n` the SQL writes itself, is `SQLSRC234` before `check` runs; the builder confirms and words the remark to match.
- `docs/deferred`: D-0003 and its row.
- `docs/tech-debt`: TD-0045 removed.  The TD-0044 spec's mention of TD-0045 is history and stays.

## What is left to the builder

- The normalisation of the two texts of gap 1, and its reflection.
- Whether `BindName` lives on `SidecarType` or on each engine's record.
- `"char"`'s and the 63-character array's names, by trying them against Npgsql; each stays `SQLSRC232` if none binds.
- The exact wording of every message and help line.
- How a lost connection during `check` is reported, if the session already has a way.

## Not verified

- That Npgsql 10.0.3 takes `My Schema.Status`.  The item says it does; the container test proves it.
- That `FinalCommandText` is set after a `SchemaOnly` describe of a batch, and holds the whole rewrite.
- That Npgsql rewrites `:id` in `arr[1:id]` when the command has a parameter `id`.  Npgsql's documentation says it takes `:name`; the container test proves this case.
- That binding a domain over a domain by its innermost base gives the server the same parameter type as `PREPARE` with the declared domain.  If it does not, the type differs in the comparison of columns, and the builder reports back before going further.
