# TD-0084 sidecar contract documentation: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve TD-0084: the sidecar's layout and key order, its reader rules, its compatibility rules and the meaning of each value are written where they are kept current, and every page, remark, comment and `AGENTS.md` rule that pointed at the stale format design points there instead.

**Architecture:** Documentation and comments only; no behaviour changes.  The contract is split in two, each half where the code it describes is already edited:

- **The schema holds the shape and the meaning.**  Each key of `schemas/sidecar-v1.schema.json` gets a `description`: what its value means beyond its JSON type, the meaning of each enumerated value, and the inputs of `hash`, `toolVersion` and `serverVersion`.  Each object lists its properties in the order the writer writes them.  Why: `docs/architecture/AGENTS.md` makes the schema the definition of the format and forbids a page to restate it; a key's meaning then sits beside its shape, so the change that adds or alters a key edits both in one place, which `src/SqlSource/Snapshot/AGENTS.md` already requires of every format change; and an editor shows a description on hover to anyone who opens a sidecar, which is what `$schema` is written for.
- **A new page, `docs/architecture/sidecar-format.md`, holds the rules**, which a schema cannot say: the text layout and where key order is fixed, what is written when, the reader rules, the compatibility rules (which change keeps `formatVersion` and which raises it), what the generator and the tool do with each version, and what `describe --check` compares.  They span the reader and writer of `src/SqlSource/Snapshot/`, the generator's checks and the tool's run, so they need a page of their own rather than a section of `generator.md`, which is already 197 KB.

**Tech Stack:** Markdown, JSON Schema draft 2020-12, C# XML doc comments.

**Spec:** `docs/tech-debt/TD-0084-the-sidecar-contract-is-documented-only-in-a-spec.md`; content from sections 1, 1.5 and 3 of `docs/superpowers/specs/2026-10-07-sidecar-format-design.md`, checked against the code.

## Where the code differs from the format design

The page and the schema document the code.  Each difference goes in the PR description.  None looks like a bug: each is a refinement the code made later, so none is a question for the manager.

1. **Duplicate keys.**  The design makes a duplicate query name malformed.  The reader also refuses any key it reads written twice in one object (`SidecarErrorKind.DuplicateKey`); an unknown key written twice is skipped like any unknown key.
2. **A key a reader cannot do without, given as `null`.**  The design reads an absent "always" key as `null` and lists the keys whose absence is malformed.  The reader also refuses `null` for those keys (wrong type), except a parameter's `type` and the `nullable` of a parameter or column.  A PostgreSQL `element`, `base`, `labels` or `subtype` that its kind needs, given as `null`, is missing.
3. **Required keys of a type object.**  The design's rule 2 names `name` alone and rule 3 "a key the schema requires for its engine".  The reader requires `name`, `kind`, `schema` and `internalName` of a PostgreSQL type, plus the nested key its kind needs; `name`, `maxLength`, `precision` and `scale` of a SQL Server type, none of them `null`; and `name` of a type under an engine it does not know.
4. **An unknown `engine`.**  The design reports one error at the sidecar naming the engine.  The reader reads each type of such an entry with `name` (and a parameter's `bindName`) alone, and the generator reports `SQLSRC242` at each query whose entry it is, since its engine is not the file's dialect, naming both.
5. **An unknown PostgreSQL `kind`.**  The design reports an unsupported type at the column.  The generator reports `SQLSRC246` once for the query, at its name, with the help to run `describe` (`TypeMappingKind.NotWrittenByTheTool`).
6. **A file of another `formatVersion`.**  The design does not say how much of it is read.  The reader reads its version alone, and judges nothing else of it but a second `formatVersion`.
7. **A file of a newer `formatVersion` and the tool.**  The design says the tool rewrites a lower version.  The tool also leaves a newer one as it is: a run that writes reports `SQLSRC221` for it, and never deletes it.
8. **Deleting a sidecar that would be empty.**  The design deletes it always.  The tool deletes it only on a run of the whole run unit with no earlier error, never a newer format's file, and leaves one it cannot open with `SQLSRC218`.
9. **`hash` input.**  The design's input is refined by `SqlQueryHash`: `{{name:}}` for an empty default and `{{name}}` for none, a line feed after the SQL, then a line for each parameter a `-- param:` marker declares with a type or a nullability.
10. **`toolVersion`.**  The design says `VersionPrefix`.  The tool writes the first three parts of its assembly's version, which the build sets from `VersionPrefix`; the generator compares a sidecar's up to its first `-` or `+`.

## Global Constraints

- Docs and code comments only: no behaviour, no test assertion and no accepted value of the schema changes.
- `docs/architecture/AGENTS.md`: the schema defines the format; a page links to it and never restates it; a new page gets its row in that file's table and its line in the reading guide of `README.md`; write in the repository's voice, two spaces after a full stop.
- An `AGENTS.md` rule is at most 80 words, and a file at most 8 KB (`AgentsFileTests`).
- Resolving follows `docs/tech-debt/AGENTS.md`: delete the item file, its row, and every `TD-0084` reference outside plan and spec history.  The item has no known issue in `docs/manual/`.
- The spec in `docs/superpowers/specs/` is history and stays as it is.

## Review Focus

- `SidecarValues` tests compare the schema's `enum` lists with the code's: the descriptions must leave every `enum` as an `enum`, in its order.
- A link in another page to a removed anchor of the spec (`#12-a-query-entry`, `#14-a-column`, `#15-reader-rules`, `#23-sql-server-mssql`) must land on a heading of the new page or on the schema, never on the spec.
- A sentence that says a rule must name the code that holds it, so that a reviewer can check it, and the page must not restate a key's type or meaning that the schema gives.

---

### Task 1: Describe every key in the schema

**Files:**
- Modify: `schemas/sidecar-v1.schema.json`
- Test: `tests/SqlSource.Tests/Snapshot/SidecarSchemaTests.cs` (unchanged; it must still pass)

- [x] **Step 1:** Give every property a `description`: the root object (unchanged, plus the key order and the page), `_WARNING`, `$schema`, `formatVersion`, `toolVersion`, `queries`, each key of `query`, `parameter`, `column`, `tableRef`, `origin`, `postgresType` and `mssqlType` and its `userType`.  Meanings from sections 1.1 to 1.4 and 2.2 to 2.3 of the design, corrected by the differences above.  The PostgreSQL `bindName` rules stay in `docs/architecture/postgres-describer.md`; its description says what the value is and that rule list is the describer's.  The SQL Server `bindName` says why it is never written.
- [x] **Step 2:** Order each object's `properties` as `SidecarWriter` writes them: the query's `matchesTable`, `plan`, `tableMatch` before `parameters`, `columns`; `postgresType`'s `bindName` after `name`.
- [x] **Step 3:** Run `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~Snapshot"`; expected: all pass.

### Task 2: Write the page of rules

**Files:**
- Create: `docs/architecture/sidecar-format.md`
- Modify: `docs/architecture/README.md` (glossary "Sidecar"; reading guide line)
- Modify: `docs/architecture/AGENTS.md` (the `src/SqlSource/Snapshot` row; "What belongs elsewhere")

- [x] **Step 1:** Write the page with these sections: the schema and this page (who holds what); `## The text` (layout, escapes, key order fixed by the examples and `SidecarWriterTests`); `## What is written` (always, conditional, facets, `bindName`, absent vs `null`, empty file deleted, entries in the file's order); `## Reader rules`; `## Compatibility` (the test; additive; a bump; a new key stays out of the schema's `required`, by `schemas/AGENTS.md`); `## Versions` (generator table: `SQLSRC237` to `SQLSRC240`; tool: current entry, rewriting, newer format `SQLSRC221`); `## What --check compares`.  Each rule names the type that holds it.
- [x] **Step 2:** Glossary "Sidecar" links the schema and the page; reading guide gets a line for the page; `AGENTS.md` table row for `src/SqlSource/Snapshot` adds `sidecar-format.md`, and "What belongs elsewhere" says the schema defines the shape and meaning and `sidecar-format.md` the rules.

### Task 3: Point everything at the new home and resolve TD-0084

**Files:**
- Modify: `docs/architecture/generator.md` (`## Snapshot`, `### Reading a sidecar`)
- Modify: `docs/architecture/postgres-describer.md:232`, `docs/architecture/postgres-nullability.md:3,60`, `docs/architecture/sqlserver-describer.md:164,185,212`
- Modify: remarks of `src/SqlSource/Snapshot/Sidecar.cs`, `SidecarComparer.cs`, `SidecarJsonBuilder.cs`, `SidecarValues.cs`, `Reading/SidecarReader.cs`; comment of `tests/SqlSource.Tests/Snapshot/SidecarWriterTests.cs:70`
- Modify: `src/SqlSource/Snapshot/AGENTS.md` (first rule), `schemas/AGENTS.md` (sidecar rule)
- Delete: `docs/tech-debt/TD-0084-the-sidecar-contract-is-documented-only-in-a-spec.md`; Modify: `docs/tech-debt/README.md` (row)

- [x] **Step 1:** Replace every link to `2026-10-07-sidecar-format-design.md` in `docs/architecture/` with the page's section or the schema; replace "format design" in the remarks and the test comment with the page or the schema.
- [x] **Step 2:** `src/SqlSource/Snapshot/AGENTS.md`'s first rule names the page for what needs a new `formatVersion`; `schemas/AGENTS.md`'s sidecar rule says the descriptions hold each key's meaning and change with it.
- [x] **Step 3:** Delete the item and its row; `grep -rn "TD-0084\|sidecar-format-design" . --exclude-dir=.git` finds only `docs/superpowers/`.
- [x] **Step 4:** Run `dotnet csharpier format .`, then `./pre-commit-validation.sh`; expected: pass.  Commit.
