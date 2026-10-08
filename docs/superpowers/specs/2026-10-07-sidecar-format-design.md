# Sidecar format - design

Date: 2026-10-07

The format of `<file>.sql.json`, the file that `sqlsource describe` writes beside each `.sql` file and the generator reads.  It is part of the [query generation epic](2026-10-07-query-generation-epic-design.md), whose section "The sidecar" holds the decisions this document details.  The file is committed to source control and read by two separately versioned packages, so it is a public contract and is designed as one.

## Decisions

Settled with the owner after the format was proposed.  The open questions in section 6 record the alternatives.

| Decision | Choice |
|----|----|
| What `nullable` records | The description: what the server and the tool's inference established.  The generator applies the `!` and `?` suffix and the policy that unknown means nullable.  The file is a faithful record and the policy can change without a database. |
| `$schema` | Written in every file, so editors validate it.  Readers ignore it. |
| A partial run | A file with a query whose database has no connection in the run is not written; mixing old and new entries under one `toolVersion` is not allowed. |
| `ordinal` | Kept, though redundant with array position, so a reordered select list and a hand edit are visible. |
| SQL Server facets | Written verbatim for every type; the alternative needs a type table in the tool. |
| PostgreSQL type names | Qualified outside `pg_catalog`, the form the override uses. |
| Range and multirange | Both carry `subtype`. |
| `source_database` on SQL Server | Not stored; adding `database` to `origin` is additive. |
| The JSON Schema | Shipped in the repository under `schemas/`, one file per format version, by phase 2. |

## 0. Prior art

**SQLx** (`.sqlx/query-<sha256>.json`: `db_name`, `query`, `describe{columns[{ordinal,name,type_info}], parameters, nullable[]}`, `hash`).  Good: a tri-state `nullable`, pretty-printed output with a trailing newline, a `db_name` check before deserialising, `prepare --check` for CI.  Bad, and avoided here: **no format version**, so compatibility rests on the query text matching; `type_info` is serde's enum encoding, sometimes a string (`"Text"`) and sometimes an object (`{"Custom": {"name": ..., "kind": {"Enum": [...]}}}`), which is hostile to a hand-written parser and to a schema; `nullable` is a parallel array beside `columns`; `parameters` is `{"Left": [...]}`, a Rust `Either` leaking into a file people read; the query is duplicated into every file; and one file per hash means a one-character edit shows in review as a deleted file and an added one.

**sqlc** (`plugin/codegen.proto`): columns and parameters share one `Column` message of accreted flags (`is_array`, `array_dims`, `is_named_param`, `is_func_call`, `is_sqlc_slice`, `unsigned`); `type` is a bare `Identifier` with no facets and no structure for arrays, domains or enums; `not_null` defaults to `false`, so unknown and nullable are one value.  The lesson worth taking is how protobuf evolves: fields are only added, never renamed or reinterpreted, and a reader ignores what it does not know.  That rule is the backbone of section 3.

## 1. The format

A sidecar is one UTF-8 JSON object, no BOM, `\n` line endings, two-space indent, one key or array element per line, a trailing newline.  Keys are written in the order the tables below list them and never sorted.  Only objects, arrays, strings, integers (signed 32-bit), booleans and `null` appear.

### 1.1 Top level

| Key | Type | Written | Meaning |
|----|----|----|----|
| `$schema` | string | always | URL of the JSON Schema for this `formatVersion`.  Readers ignore it; editors use it. |
| `formatVersion` | integer | always | The compatibility contract.  `1` for this proposal. |
| `toolVersion` | string | always | The package version of the tool that wrote the file (`VersionPrefix` plus any suffix, no build metadata), so that a tool and a generator that are out of step are detected. |
| `queries` | object | always | One entry per query that needs types, keyed by query name, in the `.sql` file's order. |

An object rather than an array with a `name` field: the generator looks entries up by name, names are unique within a file, and a diff reads `"GetUser": {` with no indirection.  JSON gives no ordering guarantee for object keys, so the writer's order is part of the format and a reviewer can rely on it; a reader must not.

A file whose `queries` would be empty is deleted by the tool, never written empty.

### 1.2 A query entry

| Key | Type | Written | Meaning |
|----|----|----|----|
| `hash` | string | always | Lowercase hex SHA-256, 64 characters, of `engine + "\n" + SQL` exactly as phase 1 defines it: the comment-stripped emitted SQL with `\n` endings, each token rendered as `{{name:default}}` with its resolved default, followed by the `-- param:` declarations in effect.  Algorithm and input are fixed by `formatVersion`; no prefix. |
| `engine` | string | always | The canonical name of the file's dialect, options stripped: `postgres`, `mssql`, `cockroachdb`.  It selects the shape of every `type` object in the entry (section 2). |
| `database` | string | always | The logical database name the query was described against, resolved by the epic's directive/metadata/property rule.  Informational to the generator. |
| `serverVersion` | string | always | Informational.  PostgreSQL: `server_version` (`"16.4"`).  SQL Server: `SERVERPROPERTY('ProductVersion')` (`"16.0.4135.4"`).  A short, deterministic value rather than the `@@VERSION` banner, so a diff shows a server change in one word. |
| `resultKind` | `"rows"` or `"none"` | always | Whether the statement produces a result set. |
| `parameters` | array | always | In parameter order; empty when the query has none. |
| `columns` | array | iff `resultKind` is `"rows"` | In result order.  Absent, not empty, when there is no result set. |

Not stored, deliberately: the SQL text (the `.sql` file is beside the sidecar and the hash proves equality; SQLx's copy doubles every diff), a timestamp (it changes on every run), the C# types (the generator's map evolves without a database), and anything about tokens beyond what the hash covers.  Names and defaults are in the SQL, the sample is reconstructible from it, and the method's `string` parameters come from the lexer.  An entry for a token query is indistinguishable from one for a plain query, which is the point: the describer saw a plain query.

### 1.3 A parameter

| Key | Type | Written | Meaning |
|----|----|----|----|
| `name` | string | always | The bare name, prefix removed, with the case of its first appearance.  The prefix is a dialect rule, not data. |
| `ordinal` | integer | always | Zero-based position; must equal the array index. |
| `type` | object or `null` | always | The engine's type object (section 2).  `null` when neither the server nor a declaration gave a type, which the two engines in scope never produce but a future one may. |
| `nullable` | boolean or `null` | always | `true` when a `-- param:` marker says `null`.  Otherwise `null`: no engine reports parameter nullability.  `false` is reserved for a future marker that says "not null". |
| `typeSource` | `"inferred"` or `"declared"` | when `type` is not `null` | Whether the server inferred the type from the query or resolved a type the user declared with `-- param:`.  Explains a `varchar(8000)` in review. |

### 1.4 A column

| Key | Type | Written | Meaning |
|----|----|----|----|
| `ordinal` | integer | always | Zero-based, as `DbDataReader.GetFieldValue(int)` takes it; must equal the array index. |
| `name` | string | always | Exactly as the server returned it, override suffix included: `"deleted_at?"`.  The sidecar records the description; the generator strips the suffix and applies the override. |
| `type` | object | always | The engine's type object (section 2).  Never `null`: a column whose type the server cannot name is a tool error, not an entry. |
| `nullable` | boolean or `null` | always | What the server and the tool's inference established; `null` is unknown.  The policy that makes unknown nullable stays out of the file so a reader can tell "the server said nullable" from "nobody knows". |
| `origin` | object or `null` | always | `{ "schema", "table", "column" }` of the base column, or `null` for an expression, aggregate, cast, set operation or `USING` column.  `schema` may be `null` for an engine without schemas; `table` and `column` are strings. |
| `identity` | boolean or `null` | always | Identity column (`attidentity`, `is_identity_column`).  `null` when the engine cannot say. |
| `computed` | boolean or `null` | always | Generated or computed column (`attgenerated`, `is_computed_column`).  `null` when the engine cannot say. |

### 1.5 Reader rules

1. **Unknown keys are ignored**, at every level, including inside type objects.  This is what makes section 3's additive changes free.
2. **A missing key that the tables mark "always" is read as `null`**, and `null` means unknown or not applicable.  A reader therefore never distinguishes absent from `null`; the writer always writes the core keys so that diffs are uniform, and writes facets and engine-specific keys only when they have a value.
3. **A key of the wrong JSON type**, a non-integer `formatVersion`, a duplicate query key, an `ordinal` that is not its index, `columns` present when `resultKind` is `"none"`, or a type object missing a key its engine requires, is a malformed file: one error at the file, no models from its queries, and the rest of the project is unaffected.
4. **Unknown enumeration values** are handled per field: an unknown `engine` is an error at the sidecar naming the engine, an unknown `kind` inside a type is the "unsupported type" diagnostic at the column, an unknown `typeSource` reads as `"inferred"`, an unknown `resultKind` is malformed.
5. **Key order is not significant to a reader.**
6. **Strings are compared ordinally**, except parameter names, which the generator already compares ignoring case.
7. Everything a reader needs about a query is inside its entry; only the two versions come from the top level.

## 2. Engine-specific types

### 2.1 The choice

Two shapes were weighed.

**A. One object per engine, chosen by the entry's `engine`.**  `type` is a flat object whose keys are whatever that engine's describer can say.  Nothing in the object says which engine it belongs to; the entry does.

**B. A common core with an engine extension.**  `type` holds engine-neutral keys (a display name, precision, scale, length, maybe `isArray`) and one nested object, `"postgres": { ... }` or `"mssql": { ... }`, with the rest.

B looks like the forward-compatible one, but the core is where the trouble lives.  Almost nothing is common to all engines: PostgreSQL's `length` is characters from a type modifier, SQL Server's `max_length` is bytes with `-1` for `max`, MySQL has `unsigned`, SQLite has a declared-type string and nothing else.  A core either shrinks to a display name, which needs no object of its own, or grows into sqlc's bag of flags, each meaningful to one engine.  The extra level costs the hand parser a nesting and the reader a line per type, and the generator never reads the core, since each type map is per engine anyway.

**Recommendation: A, with one shared convention.**  Every engine's type object has a required `name`: the type as that engine spells it, facets included, deterministic, so that a reader that knows nothing about the engine, the tool's own `--check` diff, a documentation generator, a reviewer, can show it.  Everything else in the object is the engine's own vocabulary.  A new engine adds a new `engine` value and a new object shape under section 3's additive rule; the old generator rejects the engine by name and is otherwise untouched.  The JSON Schema selects the shape with `if`/`then` on `engine`.

### 2.2 PostgreSQL (`postgres`, `cockroachdb`)

| Key | Type | Written | Meaning |
|----|----|----|----|
| `name` | string | always | The spelled type with facets: `"numeric(18,2)"`, `"character varying(100)"`, `"timestamp with time zone"`, `"text[]"`.  A type outside `pg_catalog` is schema-qualified, `"public.user_status"`, whatever the search path was, so the value does not depend on the session. |
| `kind` | `"base"`, `"array"`, `"domain"`, `"enum"`, `"range"`, `"multirange"`, `"composite"` | always | The `pg_type.typtype` family, with arrays split out. |
| `schema` | string | always | `pg_namespace.nspname`. |
| `internalName` | string | always | `pg_type.typname`: `"int4"`, `"varchar"`, `"timestamptz"`, `"_text"`, `"user_status"`.  The stable catalog identity and the key of the generator's map and of the user's override (`timestamptz`, `public.user_status`). |
| `length` | integer | when the modifier sets it | `varchar(100)`, `char(2)`, `bit(8)`. |
| `precision` | integer | when the modifier sets it | `numeric(18,2)`, `timestamp(3)`, `time(0)`, `interval(6)`. |
| `scale` | integer | when the modifier sets it | `numeric(18,2)`. |
| `element` | type object | `kind` is `array` | The element type. |
| `base` | type object | `kind` is `domain` | The domain's base type, with the domain's own modifier applied to it. |
| `labels` | array of strings | `kind` is `enum` | In `enumsortorder`. |
| `subtype` | type object | `kind` is `range` or `multirange` | The range's element type.  A multirange carries the same subtype as its range. |

A composite carries no fields in this epic; `fields` is the obvious additive key when it is supported.  OIDs are never stored.

### 2.3 SQL Server (`mssql`)

| Key | Type | Written | Meaning |
|----|----|----|----|
| `name` | string | always | `system_type_name` verbatim: `"nvarchar(50)"`, `"decimal(18,2)"`, `"varchar(max)"`, `"datetime2(7)"`, `"int"`.  `rowversion` is reported, and stored, as `"timestamp"`, and `json` as `"nvarchar(max)"`, because that is what the server says. |
| `maxLength` | integer | always | `max_length` verbatim: bytes, `-1` for `max`. |
| `precision` | integer | always | `precision` verbatim. |
| `scale` | integer | always | `scale` verbatim. |
| `userType` | object | alias or CLR type | `{ "schema", "name", "assemblyQualifiedName"? }`: `user_type_schema`, `user_type_name`, and for a CLR type `assembly_qualified_type_name`. |

The three facets are written verbatim for every column, even `int`'s `10, 0`, because the alternative is a table in the tool of which types own which facet, duplicating the generator's map.

### 2.4 Future engines

MySQL: `{ "name": "int unsigned", "unsigned": true }`, parameters with `type: null` unless declared.  SQLite: `{ "name": "INTEGER" }` from the declared type, or `null`; `nullable: null`.  DuckDB: its own `kind` for `LIST`, `STRUCT`, `MAP`.  Oracle: `origin: null`.  Each is a new shape with the `name` convention, and each is additive.

## 3. Compatibility

The test for every change: **can a reader of the previous format, following section 1.5, still generate correct code from the new file?**  If yes, the change is additive and `formatVersion` stays.  If no, it bumps.

**Additive, no bump:**

- A new key anywhere: on the top level, an entry, a parameter, a column, an origin, or a type object.  Old readers ignore it.
- A new `engine` value with its own type shape.  Old readers reject the engine by name.
- A new `kind` inside an engine's type object, with any keys it needs.  Old readers report an unsupported type at the column.
- A new `typeSource` value.
- A key moving between "written when it has a value" and "always written", since readers treat absent and `null` alike.
- A new key inside `origin`, a composite gaining `fields`, a different spelling of the informational `serverVersion`.

**A bump:**

- Removing, renaming or re-typing any key, or changing its meaning: zero-based ordinals becoming one-based, `maxLength` becoming characters, PostgreSQL `name` losing its qualification rule, `nullable` gaining a fourth state.
- Any change to the hash algorithm or its input.  The hash is the correctness contract; an old generator would call every entry stale, a new one would accept a stale file.
- A new `resultKind` value.
- A new required key, or `columns` becoming required when `resultKind` is `"none"`.
- Changing `queries` from an object keyed by name to anything else.
- Changing the shape of an existing engine's type object in any of the above ways.

A bump is a whole-file matter: a file is one `formatVersion`, never mixed entries.  The tool writes only the current version and rewrites a lower-version file completely on `describe`.

**What the generator does with a version:**

| It sees | It does |
|----|----|
| `formatVersion` equal to its own | Reads the file. |
| Higher | One error at the sidecar: the file was written by a newer tool; update the `SqlSource` package.  No models from the file. |
| Lower | One error at the sidecar: run `sqlsource describe`.  No models from the file.  Reading several versions is possible later, since the version is read first; this proposal does not promise it. |
| Missing or not an integer | Malformed file. |
| `toolVersion` different from its own, `formatVersion` equal | One warning at the sidecar; the file is read.  The packages share a version, so a difference means the tool manifest and the package reference are out of step, not that the file is unreadable. |

**What the tool does:** it skips a query whose `hash` matches and whose file has its own `formatVersion` and `toolVersion`; any other state re-describes.  Since both versions are per file, updating the tool re-describes every query of every file on the next `describe`, which is the epic's intent without `--force`.

**The schema** ships one file per format version, `sidecar-v1.schema.json`, and is strict (`additionalProperties: false`) although the reader is lenient: the schema says what this tool version writes, the reader rules say what any version tolerates.  An additive change updates the schema in the same PR without renaming it.

## 4. Examples

### 4.1 `Users.sql` (PostgreSQL)

```sql
-- SqlSource: dialect=postgres database=app

-- name: GetUser
-- summary: Loads one user by id.
SELECT id, name, email, created_at, deleted_at
FROM users
WHERE id = @id;

-- name: CreateUser
INSERT INTO users (name, email, status)
VALUES (@name, @email, @status)
RETURNING id, created_at;

-- name: DeleteUser
DELETE FROM users WHERE id = @id;

-- name: ListUsers
-- summary: Lists users by status, newest first unless told otherwise.
-- param: @limit int
SELECT id, name, tags, status, array_length(tags, 1) AS tag_count, lower(email) AS "email_lower!"
FROM users
WHERE status = ANY(@statuses) AND deleted_at IS NULL
ORDER BY {{orderBy:created_at DESC}}
LIMIT @limit;
```

`Users.sql.json`:

```json
{
  "$schema": "https://raw.githubusercontent.com/mbcrawfo/SqlSource/main/schemas/sidecar-v1.schema.json",
  "formatVersion": 1,
  "toolVersion": "0.4.0",
  "queries": {
    "GetUser": {
      "hash": "5a1d8c0e9b7f3a2c4d6e8f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c",
      "engine": "postgres",
      "database": "app",
      "serverVersion": "16.4",
      "resultKind": "rows",
      "parameters": [
        {
          "name": "id",
          "ordinal": 0,
          "type": {
            "name": "integer",
            "kind": "base",
            "schema": "pg_catalog",
            "internalName": "int4"
          },
          "nullable": null,
          "typeSource": "inferred"
        }
      ],
      "columns": [
        {
          "ordinal": 0,
          "name": "id",
          "type": {
            "name": "integer",
            "kind": "base",
            "schema": "pg_catalog",
            "internalName": "int4"
          },
          "nullable": false,
          "origin": {
            "schema": "public",
            "table": "users",
            "column": "id"
          },
          "identity": true,
          "computed": false
        },
        {
          "ordinal": 1,
          "name": "name",
          "type": {
            "name": "character varying(100)",
            "kind": "base",
            "schema": "pg_catalog",
            "internalName": "varchar",
            "length": 100
          },
          "nullable": false,
          "origin": {
            "schema": "public",
            "table": "users",
            "column": "name"
          },
          "identity": false,
          "computed": false
        },
        {
          "ordinal": 2,
          "name": "email",
          "type": {
            "name": "public.email",
            "kind": "domain",
            "schema": "public",
            "internalName": "email",
            "base": {
              "name": "character varying(254)",
              "kind": "base",
              "schema": "pg_catalog",
              "internalName": "varchar",
              "length": 254
            }
          },
          "nullable": false,
          "origin": {
            "schema": "public",
            "table": "users",
            "column": "email"
          },
          "identity": false,
          "computed": false
        },
        {
          "ordinal": 3,
          "name": "created_at",
          "type": {
            "name": "timestamp with time zone",
            "kind": "base",
            "schema": "pg_catalog",
            "internalName": "timestamptz"
          },
          "nullable": false,
          "origin": {
            "schema": "public",
            "table": "users",
            "column": "created_at"
          },
          "identity": false,
          "computed": false
        },
        {
          "ordinal": 4,
          "name": "deleted_at",
          "type": {
            "name": "timestamp with time zone",
            "kind": "base",
            "schema": "pg_catalog",
            "internalName": "timestamptz"
          },
          "nullable": true,
          "origin": {
            "schema": "public",
            "table": "users",
            "column": "deleted_at"
          },
          "identity": false,
          "computed": false
        }
      ]
    },
    "CreateUser": {
      "hash": "9c3b2a1f0e8d7c6b5a4f3e2d1c0b9a8f7e6d5c4b3a2f1e0d9c8b7a6f5e4d3c2b",
      "engine": "postgres",
      "database": "app",
      "serverVersion": "16.4",
      "resultKind": "rows",
      "parameters": [
        {
          "name": "name",
          "ordinal": 0,
          "type": {
            "name": "character varying(100)",
            "kind": "base",
            "schema": "pg_catalog",
            "internalName": "varchar",
            "length": 100
          },
          "nullable": null,
          "typeSource": "inferred"
        },
        {
          "name": "email",
          "ordinal": 1,
          "type": {
            "name": "public.email",
            "kind": "domain",
            "schema": "public",
            "internalName": "email",
            "base": {
              "name": "character varying(254)",
              "kind": "base",
              "schema": "pg_catalog",
              "internalName": "varchar",
              "length": 254
            }
          },
          "nullable": null,
          "typeSource": "inferred"
        },
        {
          "name": "status",
          "ordinal": 2,
          "type": {
            "name": "public.user_status",
            "kind": "enum",
            "schema": "public",
            "internalName": "user_status",
            "labels": [
              "active",
              "suspended",
              "deleted"
            ]
          },
          "nullable": null,
          "typeSource": "inferred"
        }
      ],
      "columns": [
        {
          "ordinal": 0,
          "name": "id",
          "type": {
            "name": "integer",
            "kind": "base",
            "schema": "pg_catalog",
            "internalName": "int4"
          },
          "nullable": false,
          "origin": {
            "schema": "public",
            "table": "users",
            "column": "id"
          },
          "identity": true,
          "computed": false
        },
        {
          "ordinal": 1,
          "name": "created_at",
          "type": {
            "name": "timestamp with time zone",
            "kind": "base",
            "schema": "pg_catalog",
            "internalName": "timestamptz"
          },
          "nullable": false,
          "origin": {
            "schema": "public",
            "table": "users",
            "column": "created_at"
          },
          "identity": false,
          "computed": false
        }
      ]
    },
    "DeleteUser": {
      "hash": "1e2d3c4b5a6f7e8d9c0b1a2f3e4d5c6b7a8f9e0d1c2b3a4f5e6d7c8b9a0f1e2d",
      "engine": "postgres",
      "database": "app",
      "serverVersion": "16.4",
      "resultKind": "none",
      "parameters": [
        {
          "name": "id",
          "ordinal": 0,
          "type": {
            "name": "integer",
            "kind": "base",
            "schema": "pg_catalog",
            "internalName": "int4"
          },
          "nullable": null,
          "typeSource": "inferred"
        }
      ]
    },
    "ListUsers": {
      "hash": "7f6e5d4c3b2a1f0e9d8c7b6a5f4e3d2c1b0a9f8e7d6c5b4a3f2e1d0c9b8a7f6e",
      "engine": "postgres",
      "database": "app",
      "serverVersion": "16.4",
      "resultKind": "rows",
      "parameters": [
        {
          "name": "statuses",
          "ordinal": 0,
          "type": {
            "name": "public.user_status[]",
            "kind": "array",
            "schema": "public",
            "internalName": "_user_status",
            "element": {
              "name": "public.user_status",
              "kind": "enum",
              "schema": "public",
              "internalName": "user_status",
              "labels": [
                "active",
                "suspended",
                "deleted"
              ]
            }
          },
          "nullable": null,
          "typeSource": "inferred"
        },
        {
          "name": "limit",
          "ordinal": 1,
          "type": {
            "name": "integer",
            "kind": "base",
            "schema": "pg_catalog",
            "internalName": "int4"
          },
          "nullable": null,
          "typeSource": "declared"
        }
      ],
      "columns": [
        {
          "ordinal": 0,
          "name": "id",
          "type": {
            "name": "integer",
            "kind": "base",
            "schema": "pg_catalog",
            "internalName": "int4"
          },
          "nullable": false,
          "origin": {
            "schema": "public",
            "table": "users",
            "column": "id"
          },
          "identity": true,
          "computed": false
        },
        {
          "ordinal": 1,
          "name": "name",
          "type": {
            "name": "character varying(100)",
            "kind": "base",
            "schema": "pg_catalog",
            "internalName": "varchar",
            "length": 100
          },
          "nullable": false,
          "origin": {
            "schema": "public",
            "table": "users",
            "column": "name"
          },
          "identity": false,
          "computed": false
        },
        {
          "ordinal": 2,
          "name": "tags",
          "type": {
            "name": "text[]",
            "kind": "array",
            "schema": "pg_catalog",
            "internalName": "_text",
            "element": {
              "name": "text",
              "kind": "base",
              "schema": "pg_catalog",
              "internalName": "text"
            }
          },
          "nullable": true,
          "origin": {
            "schema": "public",
            "table": "users",
            "column": "tags"
          },
          "identity": false,
          "computed": false
        },
        {
          "ordinal": 3,
          "name": "status",
          "type": {
            "name": "public.user_status",
            "kind": "enum",
            "schema": "public",
            "internalName": "user_status",
            "labels": [
              "active",
              "suspended",
              "deleted"
            ]
          },
          "nullable": false,
          "origin": {
            "schema": "public",
            "table": "users",
            "column": "status"
          },
          "identity": false,
          "computed": false
        },
        {
          "ordinal": 4,
          "name": "tag_count",
          "type": {
            "name": "integer",
            "kind": "base",
            "schema": "pg_catalog",
            "internalName": "int4"
          },
          "nullable": null,
          "origin": null,
          "identity": null,
          "computed": null
        },
        {
          "ordinal": 5,
          "name": "email_lower!",
          "type": {
            "name": "text",
            "kind": "base",
            "schema": "pg_catalog",
            "internalName": "text"
          },
          "nullable": null,
          "origin": null,
          "identity": null,
          "computed": null
        }
      ]
    }
  }
}
```

### 4.2 `Orders.sql` (SQL Server)

```sql
-- SqlSource: dialect=mssql database=sales

-- name: GetOrder
SELECT Id, CustomerId, Total, Notes, RowVersion
FROM dbo.Orders
WHERE Id = @id;

-- name: CreateOrder
INSERT INTO dbo.Orders (CustomerId, Total, Notes)
OUTPUT inserted.Id, inserted.RowVersion
VALUES (@customerId, @total, @notes);

-- name: ArchiveOrder
UPDATE dbo.Orders SET Archived = 1 WHERE Id = @id;

-- name: SearchOrders
-- param: @top int
SELECT TOP (@top) Id, Total, TotalWithTax, ISNULL(Notes, '') AS NotesOrEmpty
FROM dbo.Orders
WHERE CustomerName LIKE @pattern
ORDER BY {{orderBy:Id DESC}};
```

`Orders.sql.json`:

```json
{
  "$schema": "https://raw.githubusercontent.com/mbcrawfo/SqlSource/main/schemas/sidecar-v1.schema.json",
  "formatVersion": 1,
  "toolVersion": "0.4.0",
  "queries": {
    "GetOrder": {
      "hash": "2b4d6f8a0c2e4a6c8e0a2c4e6a8c0e2a4c6e8a0c2e4a6c8e0a2c4e6a8c0e2a4c",
      "engine": "mssql",
      "database": "sales",
      "serverVersion": "16.0.4135.4",
      "resultKind": "rows",
      "parameters": [
        {
          "name": "id",
          "ordinal": 0,
          "type": {
            "name": "int",
            "maxLength": 4,
            "precision": 10,
            "scale": 0
          },
          "nullable": null,
          "typeSource": "inferred"
        }
      ],
      "columns": [
        {
          "ordinal": 0,
          "name": "Id",
          "type": {
            "name": "int",
            "maxLength": 4,
            "precision": 10,
            "scale": 0
          },
          "nullable": false,
          "origin": {
            "schema": "dbo",
            "table": "Orders",
            "column": "Id"
          },
          "identity": true,
          "computed": false
        },
        {
          "ordinal": 1,
          "name": "CustomerId",
          "type": {
            "name": "int",
            "maxLength": 4,
            "precision": 10,
            "scale": 0,
            "userType": {
              "schema": "dbo",
              "name": "CustomerId"
            }
          },
          "nullable": false,
          "origin": {
            "schema": "dbo",
            "table": "Orders",
            "column": "CustomerId"
          },
          "identity": false,
          "computed": false
        },
        {
          "ordinal": 2,
          "name": "Total",
          "type": {
            "name": "decimal(18,2)",
            "maxLength": 9,
            "precision": 18,
            "scale": 2
          },
          "nullable": false,
          "origin": {
            "schema": "dbo",
            "table": "Orders",
            "column": "Total"
          },
          "identity": false,
          "computed": false
        },
        {
          "ordinal": 3,
          "name": "Notes",
          "type": {
            "name": "nvarchar(max)",
            "maxLength": -1,
            "precision": 0,
            "scale": 0
          },
          "nullable": true,
          "origin": {
            "schema": "dbo",
            "table": "Orders",
            "column": "Notes"
          },
          "identity": false,
          "computed": false
        },
        {
          "ordinal": 4,
          "name": "RowVersion",
          "type": {
            "name": "timestamp",
            "maxLength": 8,
            "precision": 0,
            "scale": 0
          },
          "nullable": false,
          "origin": {
            "schema": "dbo",
            "table": "Orders",
            "column": "RowVersion"
          },
          "identity": false,
          "computed": false
        }
      ]
    },
    "CreateOrder": {
      "hash": "8e0a2c4e6a8c0e2a4c6e8a0c2e4a6c8e0a2c4e6a8c0e2a4c6e8a0c2e4a6c8e0a",
      "engine": "mssql",
      "database": "sales",
      "serverVersion": "16.0.4135.4",
      "resultKind": "rows",
      "parameters": [
        {
          "name": "customerId",
          "ordinal": 0,
          "type": {
            "name": "int",
            "maxLength": 4,
            "precision": 10,
            "scale": 0,
            "userType": {
              "schema": "dbo",
              "name": "CustomerId"
            }
          },
          "nullable": null,
          "typeSource": "inferred"
        },
        {
          "name": "total",
          "ordinal": 1,
          "type": {
            "name": "decimal(18,2)",
            "maxLength": 9,
            "precision": 18,
            "scale": 2
          },
          "nullable": null,
          "typeSource": "inferred"
        },
        {
          "name": "notes",
          "ordinal": 2,
          "type": {
            "name": "nvarchar(max)",
            "maxLength": -1,
            "precision": 0,
            "scale": 0
          },
          "nullable": null,
          "typeSource": "inferred"
        }
      ],
      "columns": [
        {
          "ordinal": 0,
          "name": "Id",
          "type": {
            "name": "int",
            "maxLength": 4,
            "precision": 10,
            "scale": 0
          },
          "nullable": false,
          "origin": {
            "schema": "dbo",
            "table": "Orders",
            "column": "Id"
          },
          "identity": true,
          "computed": false
        },
        {
          "ordinal": 1,
          "name": "RowVersion",
          "type": {
            "name": "timestamp",
            "maxLength": 8,
            "precision": 0,
            "scale": 0
          },
          "nullable": false,
          "origin": {
            "schema": "dbo",
            "table": "Orders",
            "column": "RowVersion"
          },
          "identity": false,
          "computed": false
        }
      ]
    },
    "ArchiveOrder": {
      "hash": "4c6e8a0c2e4a6c8e0a2c4e6a8c0e2a4c6e8a0c2e4a6c8e0a2c4e6a8c0e2a4c6e",
      "engine": "mssql",
      "database": "sales",
      "serverVersion": "16.0.4135.4",
      "resultKind": "none",
      "parameters": [
        {
          "name": "id",
          "ordinal": 0,
          "type": {
            "name": "int",
            "maxLength": 4,
            "precision": 10,
            "scale": 0
          },
          "nullable": null,
          "typeSource": "inferred"
        }
      ]
    },
    "SearchOrders": {
      "hash": "0a2c4e6a8c0e2a4c6e8a0c2e4a6c8e0a2c4e6a8c0e2a4c6e8a0c2e4a6c8e0a2c",
      "engine": "mssql",
      "database": "sales",
      "serverVersion": "16.0.4135.4",
      "resultKind": "rows",
      "parameters": [
        {
          "name": "top",
          "ordinal": 0,
          "type": {
            "name": "int",
            "maxLength": 4,
            "precision": 10,
            "scale": 0
          },
          "nullable": null,
          "typeSource": "declared"
        },
        {
          "name": "pattern",
          "ordinal": 1,
          "type": {
            "name": "nvarchar(200)",
            "maxLength": 400,
            "precision": 0,
            "scale": 0
          },
          "nullable": null,
          "typeSource": "inferred"
        }
      ],
      "columns": [
        {
          "ordinal": 0,
          "name": "Id",
          "type": {
            "name": "int",
            "maxLength": 4,
            "precision": 10,
            "scale": 0
          },
          "nullable": false,
          "origin": {
            "schema": "dbo",
            "table": "Orders",
            "column": "Id"
          },
          "identity": true,
          "computed": false
        },
        {
          "ordinal": 1,
          "name": "Total",
          "type": {
            "name": "decimal(18,2)",
            "maxLength": 9,
            "precision": 18,
            "scale": 2
          },
          "nullable": false,
          "origin": {
            "schema": "dbo",
            "table": "Orders",
            "column": "Total"
          },
          "identity": false,
          "computed": false
        },
        {
          "ordinal": 2,
          "name": "TotalWithTax",
          "type": {
            "name": "decimal(19,4)",
            "maxLength": 9,
            "precision": 19,
            "scale": 4
          },
          "nullable": true,
          "origin": {
            "schema": "dbo",
            "table": "Orders",
            "column": "TotalWithTax"
          },
          "identity": false,
          "computed": true
        },
        {
          "ordinal": 3,
          "name": "NotesOrEmpty",
          "type": {
            "name": "nvarchar(max)",
            "maxLength": -1,
            "precision": 0,
            "scale": 0
          },
          "nullable": false,
          "origin": null,
          "identity": null,
          "computed": null
        }
      ]
    }
  }
}
```

## 5. JSON Schema, draft 2020-12

Proposed path: `schemas/sidecar-v1.schema.json`, referenced by `$schema` in every sidecar.

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "https://raw.githubusercontent.com/mbcrawfo/SqlSource/main/schemas/sidecar-v1.schema.json",
  "title": "SqlSource sidecar, format version 1",
  "description": "Written by `sqlsource describe` beside a .sql file and read by the SqlSource source generator. This schema is strict and describes what the current tool writes; the generator's reader ignores keys it does not know.",
  "type": "object",
  "required": ["formatVersion", "toolVersion", "queries"],
  "additionalProperties": false,
  "properties": {
    "$schema": { "type": "string", "format": "uri" },
    "formatVersion": { "const": 1 },
    "toolVersion": {
      "type": "string",
      "pattern": "^[0-9]+\\.[0-9]+\\.[0-9]+(-[0-9A-Za-z.-]+)?$"
    },
    "queries": {
      "type": "object",
      "propertyNames": { "pattern": "^[A-Za-z_][A-Za-z0-9_]*$" },
      "additionalProperties": { "$ref": "#/$defs/query" }
    }
  },
  "$defs": {
    "hash": { "type": "string", "pattern": "^[0-9a-f]{64}$" },
    "triState": { "type": ["boolean", "null"] },
    "ordinal": { "type": "integer", "minimum": 0 },
    "query": {
      "type": "object",
      "required": ["hash", "engine", "database", "serverVersion", "resultKind", "parameters"],
      "additionalProperties": false,
      "properties": {
        "hash": { "$ref": "#/$defs/hash" },
        "engine": { "type": "string", "enum": ["postgres", "cockroachdb", "mssql"] },
        "database": { "type": "string", "minLength": 1 },
        "serverVersion": { "type": "string" },
        "resultKind": { "type": "string", "enum": ["rows", "none"] },
        "parameters": { "type": "array", "items": { "$ref": "#/$defs/parameter" } },
        "columns": { "type": "array", "minItems": 1, "items": { "$ref": "#/$defs/column" } }
      },
      "allOf": [
        {
          "if": { "properties": { "resultKind": { "const": "rows" } } },
          "then": { "required": ["columns"] },
          "else": { "not": { "required": ["columns"] } }
        },
        {
          "if": { "properties": { "engine": { "enum": ["postgres", "cockroachdb"] } } },
          "then": {
            "properties": {
              "parameters": { "items": { "properties": { "type": { "anyOf": [{ "type": "null" }, { "$ref": "#/$defs/postgresType" }] } } } },
              "columns": { "items": { "properties": { "type": { "$ref": "#/$defs/postgresType" } } } }
            }
          }
        },
        {
          "if": { "properties": { "engine": { "const": "mssql" } } },
          "then": {
            "properties": {
              "parameters": { "items": { "properties": { "type": { "anyOf": [{ "type": "null" }, { "$ref": "#/$defs/mssqlType" }] } } } },
              "columns": { "items": { "properties": { "type": { "$ref": "#/$defs/mssqlType" } } } }
            }
          }
        }
      ]
    },
    "parameter": {
      "type": "object",
      "required": ["name", "ordinal", "type", "nullable"],
      "additionalProperties": false,
      "properties": {
        "name": { "type": "string", "pattern": "^[A-Za-z0-9_]+$" },
        "ordinal": { "$ref": "#/$defs/ordinal" },
        "type": { "type": ["object", "null"] },
        "nullable": { "$ref": "#/$defs/triState" },
        "typeSource": { "type": "string", "enum": ["inferred", "declared"] }
      },
      "if": { "properties": { "type": { "type": "object" } } },
      "then": { "required": ["typeSource"] }
    },
    "column": {
      "type": "object",
      "required": ["ordinal", "name", "type", "nullable", "origin", "identity", "computed"],
      "additionalProperties": false,
      "properties": {
        "ordinal": { "$ref": "#/$defs/ordinal" },
        "name": { "type": "string" },
        "type": { "type": "object" },
        "nullable": { "$ref": "#/$defs/triState" },
        "origin": { "anyOf": [{ "type": "null" }, { "$ref": "#/$defs/origin" }] },
        "identity": { "$ref": "#/$defs/triState" },
        "computed": { "$ref": "#/$defs/triState" }
      }
    },
    "origin": {
      "type": "object",
      "required": ["schema", "table", "column"],
      "additionalProperties": false,
      "properties": {
        "schema": { "type": ["string", "null"] },
        "table": { "type": "string", "minLength": 1 },
        "column": { "type": "string", "minLength": 1 }
      }
    },
    "postgresType": {
      "type": "object",
      "required": ["name", "kind", "schema", "internalName"],
      "additionalProperties": false,
      "properties": {
        "name": { "type": "string", "minLength": 1 },
        "kind": { "type": "string", "enum": ["base", "array", "domain", "enum", "range", "multirange", "composite"] },
        "schema": { "type": "string", "minLength": 1 },
        "internalName": { "type": "string", "minLength": 1 },
        "length": { "type": "integer", "minimum": 0 },
        "precision": { "type": "integer", "minimum": 0 },
        "scale": { "type": "integer", "minimum": 0 },
        "element": { "$ref": "#/$defs/postgresType" },
        "base": { "$ref": "#/$defs/postgresType" },
        "labels": { "type": "array", "items": { "type": "string" } },
        "subtype": { "$ref": "#/$defs/postgresType" }
      },
      "allOf": [
        { "if": { "properties": { "kind": { "const": "array" } } }, "then": { "required": ["element"] }, "else": { "not": { "required": ["element"] } } },
        { "if": { "properties": { "kind": { "const": "domain" } } }, "then": { "required": ["base"] }, "else": { "not": { "required": ["base"] } } },
        { "if": { "properties": { "kind": { "const": "enum" } } }, "then": { "required": ["labels"] }, "else": { "not": { "required": ["labels"] } } },
        { "if": { "properties": { "kind": { "enum": ["range", "multirange"] } } }, "then": { "required": ["subtype"] }, "else": { "not": { "required": ["subtype"] } } }
      ]
    },
    "mssqlType": {
      "type": "object",
      "required": ["name", "maxLength", "precision", "scale"],
      "additionalProperties": false,
      "properties": {
        "name": { "type": "string", "minLength": 1 },
        "maxLength": { "type": "integer", "minimum": -1 },
        "precision": { "type": "integer", "minimum": 0 },
        "scale": { "type": "integer", "minimum": 0 },
        "userType": {
          "type": "object",
          "required": ["schema", "name"],
          "additionalProperties": false,
          "properties": {
            "schema": { "type": "string", "minLength": 1 },
            "name": { "type": "string", "minLength": 1 },
            "assemblyQualifiedName": { "type": "string", "minLength": 1 }
          }
        }
      }
    }
  }
}
```

## 6. Open questions and trade-offs

1. **Description or decision for `nullable`.**  This proposal stores what the server and the inference said and leaves the `!`/`?` suffix in `name` for the generator to apply, so that the file is a faithful record and the policy can change without a database.  The epic's phase 2 text reads "then the override, then the policy" as the describer's steps, which would bake both into the file.  Phase 2 should pick one; the format is the same either way, but the meaning of `nullable: null` is not.
2. **`$schema` in every file.**  It gives editor validation for free and is ignored by readers, but it puts a URL into every committed file and ties it to the repository's layout.  If the owner would rather not, drop the key from the writer; nothing else changes.
3. **Per-file `toolVersion` and a partial run.**  When one database of a file has no connection, the tool either keeps that database's old entries under the new file-level `toolVersion`, or refuses to write the file.  The format cannot express a mixed file and should not; the tool's rule is phase 2's to settle.  Refusing is the safer default.
4. **`ordinal` is redundant** with array position.  It is kept because a reordered select list diffs as moved numbers and a hand edit that drops an element is caught.  Dropping it later would be a bump.
5. **SQL Server facets written verbatim** put `"maxLength": 4, "precision": 10, "scale": 0` on every `int`.  If that proves noisy in review, omitting the three when `name` has no parenthesis is the one mechanical rule that needs no type table, and it is additive.
6. **PostgreSQL `name` qualification.**  Qualifying every type outside `pg_catalog` makes `name` session-independent at the cost of `public.user_status` where a user would write `user_status`.  The override key uses the same qualified form, which argues for it.
7. **Range and multirange `subtype`.**  Both carry the element type.  If phase 3 maps a multirange to `NpgsqlRange<T>[]` it has what it needs; if it wants the range type's own name, that is an additive key.
8. **CockroachDB** shares the PostgreSQL shape; whether the generator accepts it for `Models` is the epic's question, not the format's.
9. **Cross-database origins on SQL Server.**  `source_database` is dropped; adding `database` to `origin` is additive when someone needs it.
