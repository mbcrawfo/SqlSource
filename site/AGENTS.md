# AGENTS.md - site

Rules for the user documentation, this folder and the two packed readmes, beside root rule 4.

## Where a new thing goes

| New thing | Goes in | What fails if it does not |
|----|----|----|
| Setting, as a settings-file key or a marker | A `setting-<name>` section on the reference page of what it changes; a row in the matrix of `reference/settings.md` | `SettingsReferenceTests` |
| Marker | A row in the marker index of `reference/sql-files.md` | `SettingsReferenceTests` |
| Generator parameter | `reference/generator-parameters.md`; the section of `SQLSRC109` | `SettingsReferenceTests`, `DiagnosticsDocumentTests` |
| MSBuild property | A section on the reference page of what it changes; a row in `reference/msbuild.md#properties` and in the matrix; its name in the list of `SettingsReferenceTests` | `SettingsReferenceTests`, once listed; a reviewer otherwise |
| Attribute, or a property of one | `reference/attributes.md` | A reviewer |
| Command, option, environment variable, exit code | `reference/cli.md` | `CliReferenceTests`, for a command, an option and a `SQLSOURCE_` variable; a reviewer, for an exit code and another variable |
| Diagnostic | A `## SQLSRCnnn` section of `diagnostics.md`, in id order | `DiagnosticsDocumentTests` |
| Dialect | Its rows in `reference/dialects.md` and `reference/databases/index.md`; the section of `SQLSRC019` | A reviewer |
| Database that can be described | A folder of the five pages under `reference/databases/`; a row in `reference/databases/index.md`; a tab in every tab group that names a database; the README's list; an example project | `SiteStructureTests`, for the folder and the tabs; a reviewer, for the rest |
| Feature that meets the line for Get started | A part of one `start/` page; its rules in Reference | A reviewer |
| Any other feature | A reference section; a guide when it is a task; an example, as root rule 4 says | `SiteTocTests`, for a new page |
| Limit | `## Limits` of the page that owns it, with its own anchor; a row in `guides/troubleshooting.md` | `SiteStructureTests` |

## Structure

- **Three folders, one job each.**  `start/` is the path of a newcomer, read in order; `guides/` holds one task on each page; `reference/` is look-up, one page for each thing.  The root holds `index.md`, the landing page and the site's root, and `diagnostics.md`.
- **A fact has one home.**  A rule, a value, a default or a limit is stated in one reference section; every other page restates it in a sentence or two and links there.
- **A new page** gets an entry in the `toc.yml` of its folder, under its group.  Aim at 600 to 3,500 words for a page and at most nine entries for a group; a reviewer holds both.
- **The two packed readmes.**  `README.md` holds the outline of root rule 4, and neither readme states anything that a page of this folder does not own.
- **The site.**  `tools/build-site.sh` builds it and fails on any docfx warning.  The tests are in `tests/SqlSource.Repository.Tests/Documentation/` and `Diagnostics/`; none checks whether a page is true and free of internals.

## Write effects, never internals

- Never name the describer, a plan or a plan walk, `EXPLAIN`, a heuristic, a provenance value, a capture file, a type of the library, or a field of the sidecar (`reference/sidecars.md` links the sidecar's schema once).
- Never link into the project's notes or source, and never cite an item's id.
- Say what makes a column possibly null, not what the sidecar records, and what the user's database experiences.
- A type map is the database type and the C# type a user gets, with the driver version where that changes it; not how the driver is detected or what is set on a `DbParameter`.  A generated method is its signature and what it returns and throws; not how it reads a row.
- Before a commit, grep the pages you changed and rewrite every hit but text the user sees anyway, such as a diagnostic's own title or a SQL command a user writes (`EXPLAIN`).

```console
$ grep -n -i -E "describer|EXPLAIN|heuristic|nullableSource|provenance|capture|tech-debt|superpowers|TD-0|D-0" <pages>
```

## Writing

- **State what SqlSource does, as fact.**  No page says that a feature is coming, works "today" or is in development; `StatusLanguageTests` holds the phrases.  A sentence that says something is not tested, or was checked on one platform, is a fact about support and stays.
- **Plain language**, by ASD-STE100 as a guideline, for text you write or change; a reviewer holds it.  One instruction in a sentence.  At most 20 words in a step and 25 in a description.  A paragraph has one topic and at most six sentences.
- Active voice and present tense, not "will".  A condition comes first: "If the column can be null, ...".  "Must" for what is required and "can" for what is allowed.  A list for conditions and alternatives.
- **Terms.**  One term for one thing.  Say *query parameter*, *generator parameter* or *logical database* where "parameter" or "database" alone is unclear, *inside a query*, and *folder*, never "directory" in prose.  A term is defined once, in the reference section that documents the thing.
- **The outputs are listed with the default first**: `repository`, `methods`, `models`, `sql`, in a part of the list too.  Text that the tool prints keeps its order.  A reader sets the dialect to their database; no text says "choose a dialect".
- **One word for each of these**: a type *claims* a `.sql` file; a column *can be null* or is *never null*; a sidecar entry is *current* or *out of date*; the tool *describes* a query; `sqlsource.json` is a *settings file*.
- **Tabs.**  A docfx tab holds a sample or a few sentences, never a heading or an `<a id>`: an anchor in a tab that is not selected is a dead link target.  A long difference between databases is two anchored sections.  A tab group that names one described database names them all.
- **Anchors.**  A heading of this folder linked from anywhere gets an `<a id>` anchor on the line above, kept when the heading is reworded.
- **Links.**  Pages of this folder link to each other relatively, and to any other file of the repository by `https://github.com/mbcrawfo/SqlSource/blob/main/<path>`: only this folder is published.
- **SQL samples** are marked by the info strings `UserDocsSamplesTests` reads; never weaken the test.  Put no fence inside a blockquote.
- **No `--prerelease`.**  Install and update commands are written for a stable release, with no `--prerelease` and no sentence about previews.

## Limits and diagnostics

- **Limits.**  A page that owns behaviour ends with `## Limits`, under `<a id="limits"></a>`: what a user can plausibly hit from the open items of `docs/tech-debt` and `docs/deferred`.  Each is an h3 under its own anchor, `limit-<name>`: what the user sees, why, and what to do.  Leave out what a user cannot see, such as a test gap or duplicated code.
- Add a limit in the PR that opens its item, with its row in `guides/troubleshooting.md`.  Remove both in the PR that closes it, found by its effect, as a limit names no id.
- **`diagnostics.md` stays at the root, and a `## SQLSRCnnn` heading never changes and never gets another anchor**: every error's help link is `diagnostics.html#sqlsrcnnn`, compiled into the packages.  Nothing but the title and one paragraph stands before `## SQLSRC001`.
- A new diagnostic gets a section in one shape: its descriptor's title in bold, its "Reported by" line, a "Database" line where a database gives the error, what happened, why, `**Fix:**` with its steps or a `Fix:` under each cause, and a closing `See:` line whose first link is a section of `reference/`.  It has no heading and stands alone, with its own lists of values.  After a bulleted fix, a note is a paragraph that opens in bold.
- A new diagnostic also gets its place on `reference/errors.md`: its area under `#errors-by-area`, its row of `#who-reports`, `#severity` when it is a warning or information, and `#where-an-error-is-reported` when it has no file and line.  No test checks the last three.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
