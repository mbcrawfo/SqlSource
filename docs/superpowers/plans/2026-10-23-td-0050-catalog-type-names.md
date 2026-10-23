# TD-0050 - A catalog type's name does not depend on the search path - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The sidecar `name` of a type of `pg_catalog` is the same whatever the session's `search_path`, so phase 7's library maps can key on `jsonb`, `timestamptz`, `text`.

**Architecture:** `PostgresTypeNames` strips a leading `pg_catalog.` from the server's text of a type whose schema is `pg_catalog`, the rule `PostgresBindNames` already applies to `bindName`.  One helper, `PostgresTypeNames.WithoutCatalog`, serves both.  TD-0050 is resolved.

**Tech Stack:** C# (.NET), xunit v3, Shouldly, Testcontainers PostgreSQL 14.24 and 18.6.

**Spec:** `docs/tech-debt/TD-0050-a-catalog-types-name-depends-on-the-search-path.md` (Problem and Proposed fix).

## Decision: strip the prefix, not a fixed `search_path`

The item offers two fixes.  This plan strips a leading `pg_catalog.` for a row whose `nspname` is `pg_catalog`, because:

- **It is exact.**  `format_type` writes a hidden type of `pg_catalog` as `quote_qualified_identifier`, so the text is `pg_catalog.` and the text it gives when the type is visible.  A visible name never starts with `pg_catalog.`: a type named so is quoted, `"pg_catalog.x"`.  Probed on 14.24 and 18.6 with a domain of every base, range, multirange and pseudo type of `pg_catalog` placed before it: 155 (18) and 157 (14) types came back qualified, every one as `pg_catalog.<visible text>`.
- **It is the rule `bindName` already follows** (`PostgresBindNames.WithoutCatalog`), so `name` and `bindName` come from one rule.
- **It sends nothing new.**  A fixed path needs a transaction and a `SET LOCAL` around `types` and `spellings`, two more statements in every describe, and a pooler in transaction mode (TD-0049) is one more place they could go wrong.  It would also need `pg_temp` named in the path, since an unlisted `pg_temp` is searched before `pg_catalog` for type names.
- **The second fix's advantage does not apply.**  It would make the server's spelling of any name session-independent, but a type of another schema is already the tool's own `schema.name` whatever the path.

## Where the code differs from the item

- The item says `bpchar` is never qualified.  It is: `format_type(oid, -1)` of `bpchar`, of `"bit"` and of `"char"` come back as `pg_catalog.bpchar`, `pg_catalog."bit"` and `pg_catalog."char"` under a hiding type (the server special-cases them only with a modifier, or not at all).  `numeric` is never qualified, so the existing container test of a hidden `numeric` never showed the problem; it guards the modifier only.
- `json` is special-cased on 18 and qualified on 14.
- No capture in the repository holds a qualified name (`grep -rl 'pg_catalog\.'` over `tests/` and `examples/` outside `.cs` finds none), so nothing is re-recorded.  A capture recorded under a hiding type would now replay with the bare name, which is the intended change.
- Error and log texts that name a type by the row's `FormatType` (`PostgresErrors.PseudoType`, `ColumnPseudoType`, `NpgsqlCheck.NameOf`, `PostgresTypes`' throws, the `formatType` member of the events) keep the server's text: they report what the server answered, and none is a contract key.

## Found while building

The container test of Task 2 first failed with `SQLSRC225`, not with the qualified name: the describer's own catalog SQL (`PostgresLive`) cast with `::text`, `::oid` and `::int4`, so a user's domain named `text` before `pg_catalog`, the item's own example when the domain is over something other than text, took the cast.  The functions were already written with `pg_catalog.`.  So this change also:

- writes every cast of the catalog calls with `pg_catalog.` (`::pg_catalog.text`, `::pg_catalog.oid[]`, `::pg_catalog.int4[]`);
- pairs two arrays with `ROWS FROM (pg_catalog.unnest($1), pg_catalog.unnest($2))`, since `unnest(a, b)` is the special form only when unqualified, and `pg_catalog.unnest(a, b)` does not exist;
- records TD-0077: the operators of that SQL are still found by the `search_path`, which a probe on 18.6 confirmed (`shadow.= (oid, oid)` before `pg_catalog` makes `t.oid = 25::pg_catalog.oid` false), with a known issue in `docs/manual/postgresql.md`;
- adds the rule to `src/SqlSource.Tool/AGENTS.md` and the architecture page's "The calls and their records".

The container test also hides `oid` and `int4` and reads a column of `public.samples`, so that `tables` and `spellings` run under the hiding path.

## Global Constraints

- Every commit passes `./pre-commit-validation.sh` on the exact tree.  After the describer change, also run `tests/SqlSource.Tests.Postgres` with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`.
- Test names have three parts (`Method_Condition_Result`); container tests are `DescribeAsync_...`.
- An object a test makes that changes what other tests see is dropped by the test in a `finally`.
- Resolving the item: delete its file, its `README.md` row, every reference to `TD-0050` in code and current docs (plans and specs under `docs/superpowers/` are history and stay), and its known issue in `docs/manual/postgresql.md`.

## Review Focus

1. A hidden type whose visible text is quoted, `"char"`, `"bit"`: the name keeps its quotes and loses only the schema (unit and container tests in Tasks 1 and 2).
2. A hidden type's array: `text[]` with an `element` of `text`, since the array name is built from the element (container test, Task 2).
3. A hidden type with a modifier, `bit(8)` under a hiding `"bit"`: the tool's own spelling, never qualified (unit test, Task 1).
4. A type of another schema whose text starts with `pg_catalog.`: untouched, since the strip runs only for `nspname = pg_catalog` (unit test, Task 1).
5. The floor image: `json` is qualified on 14 and not on 18; the same container test must pass on both (Task 2's run on 14.24).

---

### Task 1: Strip the schema from a catalog type's text

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Postgres/Types/PostgresTypeNames.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/Types/PostgresBindNames.cs` (use the shared helper; drop its private `CatalogPrefix` and `WithoutCatalog`)
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/Types/PostgresTypeNamesTests.cs`

**Interfaces:**
- Produces: `internal static string PostgresTypeNames.WithoutCatalog(string formatted)`: `formatted` without a leading `pg_catalog.` (ordinal), else as it is.

- [ ] **Step 1: Write the failing tests** in `PostgresTypeNamesTests`:

```csharp
// format_type writes a type of the catalog with its schema when a type of the same name stands before pg_catalog
// on the session's search_path.  The name is the one it has in any other session.
[Theory]
[InlineData("text", "pg_catalog.text", null, "text")]
[InlineData("jsonb", "pg_catalog.jsonb", null, "jsonb")]
[InlineData("bpchar", "pg_catalog.bpchar", null, "bpchar")]
[InlineData("char", "pg_catalog.\"char\"", null, "\"char\"")]
[InlineData("bit", "pg_catalog.\"bit\"", null, "\"bit\"")]
[InlineData("bit", "pg_catalog.\"bit\"", 8, "bit(8)")]
public void Spell_TypeOfTheCatalogHiddenOnTheSearchPath_IsItsNameInAnySession(
    string typname, string formatType, int? length, string expected) => ...ShouldBe(expected);

[Fact]
public void WithModifier_TypeOfTheCatalogQualifiedByTheServer_IsWithoutTheSchema() =>
    PostgresTypeNames.WithModifier(PostgresRows.BaseOf(1, "tagged", "pg_catalog.tagged", "U"), "pg_catalog.tagged(7)")
        .ShouldBe("tagged(7)");

// Only a type of pg_catalog loses the prefix: another schema's type is its own schema and name.
[Fact]
public void Spell_TypeOfAnotherSchemaNamedLikeTheCatalog_KeepsItsSchema() =>
    PostgresTypeNames.Spell(PostgresRows.BaseOf(1, "pg_catalog.text", "\"pg_catalog.text\"", "U", "public"), PostgresFacets.None)
        .ShouldBe("public.\"pg_catalog.text\"");

[Theory]
[InlineData("pg_catalog.text", "text")]
[InlineData("pg_catalog.\"char\"", "\"char\"")]
[InlineData("text", "text")]
[InlineData("\"pg_catalog.x\"", "\"pg_catalog.x\"")]
public void WithoutCatalog_Text_HasNoLeadingCatalogSchema(string formatted, string expected) =>
    PostgresTypeNames.WithoutCatalog(formatted).ShouldBe(expected);
```

- [ ] **Step 2: Run them and see the hidden cases fail**

Run: `dotnet test tests/SqlSource.Tool.Tests -- --filter-class "*PostgresTypeNamesTests"` (or the project's equivalent filter).  Expected: the `pg_catalog.`-prefixed cases fail with the qualified text; `WithoutCatalog` does not compile until Step 3.

- [ ] **Step 3: Implement.**  Move `CatalogPrefix` and `WithoutCatalog` from `PostgresBindNames` to `PostgresTypeNames` (internal).  `InCatalog` reads `WithoutCatalog(row.FormatType)` once and uses it wherever it read `row.FormatType`.  `WithModifier` returns `WithoutCatalog(formatted)` for a type of `pg_catalog`.  `PostgresBindNames` calls `PostgresTypeNames.WithoutCatalog`.  Rewrite the remarks of `PostgresTypeNames` (a type of `pg_catalog` is the server's text without the schema the server writes when the type is hidden, so the name never depends on the session) and rule 5 of `PostgresBindNames` (same rule as `name`), with no `TD-0050`.

- [ ] **Step 4: Run** `PostgresTypeNamesTests`, `PostgresBindNamesTests`, `BuiltInTypesTests`.  Expected: PASS.

- [ ] **Step 5: Commit** (after `./pre-commit-validation.sh`, folded with Task 2 if run together).

### Task 2: Container case on 14 and 18, and resolve TD-0050

**Files:**
- Test: `tests/SqlSource.Tests.Postgres/Describer/ColumnTests.cs` (beside `DescribeAsync_BuiltInTypeHiddenOnTheSearchPath_IsStillTheCatalogsSpellingAndBindsByTheBareName`)
- Delete: `docs/tech-debt/TD-0050-a-catalog-types-name-depends-on-the-search-path.md`
- Modify: `docs/tech-debt/README.md` (remove the row), `docs/manual/postgresql.md` (remove the known issue "A type of your own named like a built-in one changes the names of built-in types"), `docs/architecture/postgres-describer.md` ("Type names": the no-modifier bullet says the `pg_catalog.` the server writes for a hidden type is dropped; remove the TD-0050 paragraph; the `bindName` paragraph says it follows the same rule as `name`)

- [ ] **Step 1: Write the container test**

`DescribeAsync_BuiltInTypesHiddenOnTheSearchPath_AreNamedAsInAnySession`: on its own data source, `CREATE SCHEMA td0050_shadow` and in it `CREATE DOMAIN ... AS integer` named `text`, `jsonb`, `json`, `uuid`, `bpchar`, `"char"`, `"bit"`; in a `finally`, `DROP SCHEMA IF EXISTS td0050_shadow CASCADE`.  Describe with `{ConnectionString};Search Path=td0050_shadow,pg_catalog`:

```sql
SELECT NULL::pg_catalog.text AS t, NULL::pg_catalog.jsonb AS j, NULL::pg_catalog.json AS js,
       NULL::pg_catalog.uuid AS u, NULL::pg_catalog.bpchar AS b, NULL::pg_catalog."char" AS c,
       NULL::pg_catalog."bit" AS bt, NULL::pg_catalog.text[] AS a, @p AS p
```

with parameter `("p", "pg_catalog.text")`.  Assert:

```csharp
TypeNamesOf(columns).ShouldBe(["text", "jsonb", "json", "uuid", "bpchar", "\"char\"", "\"bit\"", "text[]"]);
columns[^1].Type.Element.ShouldNotBeNull().Name.ShouldBe("text");
description.Parameters.ShouldHaveSingleItem().Type.ShouldNotBeNull()
    .ShouldSatisfyAllConditions(type => type.Name.ShouldBe("text"), type => type.BindName.ShouldBe("text"));
```

(The test class is `ColumnTests`; a parameter's `name` is asserted here too since it is the same rule.)

- [ ] **Step 2: Run it on 18.6** with the fix stashed in a WIP commit reverted, or before Task 1 lands, to see it fail with `pg_catalog.text`; then with the fix, see it pass.

- [ ] **Step 3: Resolve the item** as the Files list says, then `grep -rn "TD-0050" . --exclude-dir=.git` finds only `docs/superpowers/`.

- [ ] **Step 4: Validate.**  `./pre-commit-validation.sh` passes; `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test tests/SqlSource.Tests.Postgres` passes.

- [ ] **Step 5: Commit** `Name a catalog type the same whatever the search path (TD-0050)`.
