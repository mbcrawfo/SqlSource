# AGENTS.md - site

Rules for the user documentation, this folder and the two packed readmes, beside root rule 4.

- **`README.md`**, beyond root rule 4: the databases it works with, getting started in five steps, "Only want to generate models?", "Just want to keep your SQL out of code files?", a FAQ of five to ten items, "NodaTime and other libraries", "Custom type mapping" and "Where to next".  A FAQ answer is a few sentences that link to a recipe where one exists.
- **This folder**: the entry page, `README.md`, holds "Find what you need" and "The pages", which says what each page holds.
- `sql-files.md` opens with the marker index; `parameters-and-tokens.md` holds `DECLARE` and token validation; `tool.md` gives the workflow, then the command reference; a database page covers describing it, its types and models, and its query methods.

## Write effects, never internals

- Never name the describer, a plan or a plan walk, `EXPLAIN`, a heuristic, a provenance value, a capture file, a type of the library, or a field of the sidecar (`tool.md` links `schemas/sidecar-v1.schema.json` once, for the curious).
- Never link into the project's notes or source, and never cite an item's id.
- Say what makes a column possibly null, not what the sidecar records, and what the user's database experiences.
- A type map is the database type and the C# type a user gets, with the driver version where that changes it; not how the driver is detected or what is set on a `DbParameter`.  A generated method is its signature and what it returns and throws; not how it reads a row.
- Before a commit, grep the pages you changed and rewrite every hit but text the user sees anyway: a diagnostic's own title (`SQLSRC216`'s) or a SQL command a user writes (`EXPLAIN` in `SQLSRC233`'s list).

```console
$ grep -n -i -E "describer|EXPLAIN|heuristic|nullableSource|provenance|capture|tech-debt|superpowers|TD-0|D-0" <pages>
```

## Conventions

- **Example first.**  A reference section opens with an example, then the rules.
- **Terms.**  Use the terms of `concepts.md#glossary`, *inside a query*, and *folder*, never "directory" in prose.  Never say "parameter" or "database" alone where it is ambiguous: say *query parameter* (`@id`), *generator parameter* or *logical database*.
- **State what SqlSource does, as fact.**  No page says that a feature is coming, works "today" or is in development; `StatusLanguageTests` holds the phrases.  A sentence that says something is not tested, or was checked on one platform, is a fact about support and stays.
- **Settings are documented in `settings.md` only**, each in a section anchored `setting-<name>`; other pages link to it and never repeat a setting's values or precedence.
- A setting's row under `#all-settings` names every place it can be written.
- A new setting or marker gets its row and its section, and a marker its row in the marker index of `sql-files.md`.  A new generator parameter goes in each place that the comment on the list in `DiagnosticsDocumentTests` names.
- **Recipes hold examples, not rules.**  Each is a sample and a link to the rules, so it cannot drift from them.
- **Anchors.**  A heading of this folder linked from anywhere gets an `<a id>` anchor on the line above, kept when the heading is reworded; "Known issues" gets `<a id="known-issues"></a>`.
- **Links.**  Pages of this folder link to each other relatively, and to any other file of the repository by `https://github.com/mbcrawfo/SqlSource/blob/main/<path>`: only this folder is published.
- **SQL samples** are marked by the info strings `UserDocsSamplesTests` reads; never weaken the test.  Put no fence inside a blockquote.
- **No `--prerelease`.**  Install and update commands are written for a stable release, with no `--prerelease` and no sentence about previews.
- **Known issues.**  A page that documents behaviour ends with "Known issues": what a user can plausibly hit from the open items of `docs/tech-debt` and `docs/deferred`, by its effect in the user's words and what to do.  Leave out what a user cannot see, such as a test gap, duplicated code or a cost with no visible effect.
- Add one in the PR that opens its item; remove it in the PR that closes it, found by its effect, as it names no id.  A page's first one brings the section and a link from the entry page's "Find a problem that is not an error" row.
- **`diagnostics.md`.**  A new diagnostic gets a `## SQLSRCnnn` section whose first bold line is its descriptor's title, then its "Reported by" line, a "Database" line when it is an error of describing a query against its database, and, where a manual section documents the rule, a closing `See: [section](page.md#anchor)` line.  Its id joins its area's row in `errors.md#errors-by-area`.
- Only the title and one paragraph stand before `## SQLSRC001`.  Never give a heading another anchor: every error's help link is the lower-case id's slug.
- **A new page** gets an entry in `toc.yml`, the site's sidebar, in the order of "The pages", and is listed under "The pages" of the entry page, with a "Find what you need" row for each need it answers.
- **The site.**  docfx publishes every page of this folder but `AGENTS.md`, and `index.md` only redirects the site's root to `README.html`.  `tools/build-site.sh` builds it and fails on any docfx warning.
- **Tests.**  The tests in `tests/SqlSource.Repository.Tests/Documentation/` and `Diagnostics/DiagnosticsDocumentTests.cs` say in comments what fails them.  None checks whether a page is true today and free of internals.

## When a phase lands

Every phase of query generation (`docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`), and every later feature, does in its PR what root rule 4 and this file ask, and:

- **Recipes** go in `recipes.md` where a task is new.  **The README** changes where the common path does: getting started, a scenario, a FAQ answer, the supported environments.

The pages each phase touches, besides those:

| Phase | Pages |
|----|----|
| 9. Build integration | `tool.md`: running in the build, the `.env` file and what to keep out of git, watch mode and what each editor shows, the new options in the command reference.  `recipes.md`: starting the watcher with each editor.  `settings.md`: what turns it on.  README: step 5 |
| 10. Bug reports | `tool.md`: making a bundle, what it holds before you share it, the new commands in the command reference |

A database that becomes describable gets its own page, a row in the support table of `dialects.md`, and the README's supported environments.  A new dialect gets its rows in `dialects.md`'s tables and its name in `SQLSRC019`'s section of `diagnostics.md`, where `dialect` lists the names; `settings.md`'s Dialect row changes only for a dialect with options.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
