# Docs overhaul phase 15, rewrite-settings-2: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The three pages of the group "Types", `site/reference/type-mapping.md`, `library-types-and-json.md` and `converters.md`, are in plain language and in the shape of a reference page, and the order in which a C# type is chosen is stated once, on `type-mapping.md#property-types`.

**Architecture:** A rewrite by hand, one page at a time, by section 10 of the rewrite guide: shape, restatements, sentences, reframings.  Every fact stays, or leaves by one of the three ways of the guide's section 8, each with a row of `ledger/phase-15.tsv`.  Facts most at risk are checked against the source files the spec names before the sentence is rewritten.

**Tech Stack:** Markdown, docfx (`tools/build-site.sh`), the epic's checker (`check/check.sh`), `tests/SqlSource.Repository.Tests`.

**Spec:** `docs/superpowers/specs/2026-11-12-docs-overhaul-phase-15-rewrite-settings-2-design.md`, under the outline `docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md`, the map `docs/superpowers/specs/2026-11-12-docs-overhaul/content-map.md` and the rewrite guide `docs/superpowers/specs/2026-11-12-docs-overhaul/rewrite-guide.md`.

## Global Constraints

- Only the three pages and `ledger/phase-15.tsv` change (and this plan).  No `<a id>` added or removed, no link target changed, no fenced block changed or added, no h1 changed.
- The headings "Converters" and "A class in a JSON column" keep their words: eight comments of the examples quote them.
- At most 20 words in a step, 25 in a description; one rule in a bullet; a paragraph of at most six sentences; limiter words kept.
- Outputs listed `repository` first.  No status words.  No internals (`site/AGENTS.md` grep).
- `./format.sh`, then `./pre-commit-validation.sh` before each commit.

## Where the tree differs from the spec

- The map names the h2 of `#property-types` "How a C# type is chosen"; the page has "Property types".  The heading is reworded to the map's, which no file outside `site/` quotes.
- `type-mapping.md` is 3,230 words at the base and `converters.md` 1,934, a little under the map's estimates.
- `QueryTyper.cs` agrees with the five-step order of `#property-types`: marker, column types, type override, a library that is on, the type map.  A column's `-> default` skips the column types alone, which `model-properties.md#column-marker` owns.

## Review Focus

- A rule on quotes and case turned round in "The key": checked against `TypeKeys.cs` and its tests.
- A converter's direction turned round ("from" and "to").
- A limiter word ("only", "alone", "never", "unless") lost in a split sentence: the checker lists four; read for the rest.
- A restatement of the order of type choice that drops a fact `#property-types` lacks: the fact moves to `#property-types`.
- An id that went with the wrong half of a split sentence (SQLSRC250/251/252, SQLSRC279/280, SQLSRC281/282).

---

### Task 1: `type-mapping.md`

**Files:** Modify `site/reference/type-mapping.md`; create `docs/superpowers/specs/2026-11-12-docs-overhaul/ledger/phase-15.tsv` with `# base <sha>`.

- [ ] `#property-types`: heading to the map's words; the 58-word sample sentence becomes a list of four lines; the five-step list kept; the parameter paragraph split; the array and list paragraphs cut to restatements of `databases/postgresql/query-methods.md#array-parameters` and `parameters.md#list-parameters`, keeping any fact the owner lacks.
- [ ] `#setting-type-overrides` in the order of a setting: example, what an entry is (values), default, places with "Every place, merged by type" restating `settings.md#precedence`, then "The key", "The type", "In a settings file", "In a marker", the tool.  "Either wins over the type override" becomes a restatement linking `#property-types`.  "The key" split into one rule a bullet, PostgreSQL and SQL Server told apart.
- [ ] The two limits in three parts.
- [ ] Ledger rows for every unit; `check.sh rewrite 15` lists only accounted lines.

### Task 2: `library-types-and-json.md`

- [ ] `#setting-library-types` in a setting's order; "Below your own choices" becomes a restatement linking `type-mapping.md#property-types`; "What `on` needs" split by database; the 86-word sentence of "Only what you did not choose" a list.
- [ ] `#json-class` and `#json-opt-in` reworded; heading "A class in a JSON column" kept.
- [ ] Ledger rows.

### Task 3: `converters.md`

- [ ] `#converters`: the opening split; "Converters apply only where ..." split into one rule a bullet; "SqlSource's own conversions" cut against `databases/sqlserver/libraries.md#nodatime`; heading "Converters" kept.
- [ ] The two limits in three parts, the Vogen sample unchanged.
- [ ] Ledger rows.

### Task 4: Checks

- [ ] The checks of section 10 of the rewrite guide; the "Done when" greps of the spec; `tools/build-site.sh`; `dotnet test --project tests/SqlSource.Repository.Tests`.
- [ ] Word counts in a `#` line of the ledger.
- [ ] Commit; then the two content reviews, `/branch-review:review`, `./pre-commit-validation.sh --full`.
