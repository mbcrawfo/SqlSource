# Docs overhaul, phase 05: move-tool

The outline is [`2026-11-12-docs-overhaul-epic-design.md`](2026-11-12-docs-overhaul-epic-design.md), and the content map is [`2026-11-12-docs-overhaul/content-map.md`](2026-11-12-docs-overhaul/content-map.md).  Both win over this brief.
Done by: an Opus agent.  Needs: phases 0 to 4.  Runs alone.

## Goal

`site/tool.md` becomes five pages of the group "The sqlsource tool" in `site/reference/`, with parts on `reference/msbuild.md` and `guides/report-a-bug.md`, and the three recipes of the tool leave `site/recipes.md` for two guides and one Get started page.  No word changes but for two corrections and the words that the move makes wrong.  The 8 known-issue bullets become 6 limits, one second text of a limit that phase 3 made, and one pointer that goes.  `CliReferenceTests` holds `reference/cli.md` to the tool's own commands, and `EditorRecipeTests` reads the VS Code task from its guide.

## Where things stand

How a phase is done, the ledger, the reviews and publishing are in the outline's [Delivery](2026-11-12-docs-overhaul-epic-design.md#delivery).  This brief adds only what is specific to phase 5.

At the baseline, `28e20e3c`:

- `site/tool.md` has 638 lines, 12,378 words, 27 `<a id>` anchors, 72 links to its own anchors and 8 known-issue bullets (lines 631 to 638).
- `site/recipes.md` has 1,030 lines and 19 recipes.  Lines 125-254 hold the three that move.
- The other pages of `site/` hold 102 links to `tool.md`.  Outside `site/` and `docs/superpowers`, 51 references in 14 files name the page: 9 site URLs in `README.md`, 5 in `src/SqlSource.Tool/README.md`, 1 in `.github/ISSUE_TEMPLATE/bug-report.yml` and 36 in 11 other files.  Nine more name the three recipes.
- `src/SqlSource.Tool/AGENTS.md` is 8,159 bytes, 33 under the 8,192 that `AgentsFileTests` holds.  Phase 4 leaves it at 8,147.

What phases 0 to 4 leave, taken as given:

- `tool.md`, `recipes.md`, `settings.md`, `generator.md` and `concepts.md` are at the root of `site/`.  `tool.md` stands in "The sqlsource tool" of `site/reference/toc.yml` as `../tool.md`, beside `sidecars.md`, and in `SiteStructureTests.PagesNotYetMoved`.
- `site/reference/sidecars.md` exists, with its h1, one sentence that points to `../tool.md#sidecar-files`, `## Limits` and the limit `limit-file-in-two-projects` (`sql-files.md` 200).
- `site/reference/errors.md` and `site/reference/databases/index.md#supported-versions` exist.
- `site/guides/troubleshooting.md` holds one sentence that links the `#known-issues` of the pages not yet moved, `../tool.md#known-issues` among them.
- `tool.md` differs from the baseline: phase 1 put the neutral sentence about `--prerelease` in place of the status sentence of line 20, phase 2 placed two sentences of the `diagnostics.md` preamble in `#sidecar-files`, and phases 3 and 4 retargeted links.  Line numbers in this brief are the baseline's.  In the working tree, find a section by its `<a id>`.

## In scope / Not in scope

In scope:

- The rows of the map's [Map: `site/tool.md`](2026-11-12-docs-overhaul/content-map.md#map-sitetoolmd), the rows 125-254 of [Map: `site/recipes.md`](2026-11-12-docs-overhaul/content-map.md#map-siterecipesmd), and the table "Phase 5" of its section 10.
- The 6 limits that the map's limits table marks "5", and the second text of MP-08.
- Corrections C-01, at `tool.md` 235, and C-03.
- Every link and reference to `tool.md` and to the three recipes: in `site/`, in both readmes, in the issue template and in the rest of the repository.
- `CliReferenceTests`, `EditorRecipeTests`, and the trim of `src/SqlSource.Tool/AGENTS.md`.

Not in scope:

- The other 16 recipes, which stay in `site/recipes.md` until phase 8.
- The rest of `reference/msbuild.md` (phase 7) and of `start/keep-up-to-date.md` (phase 10), and the guide sections that phase 9 writes: `start-the-watch`, `exit-code`, `what-is-safe`.
- The text of `src/SqlSource.Tool/README.md`.  Its site URLs change address here, and phase 11 cuts it.
- The help links of the tool's diagnostics.  `SqlDiagnostics.HelpLinkBase` (`src/SqlSource/Diagnostics/SqlDiagnostics.cs` line 20) names `diagnostics.html#`, which does not move.
- Any change of words in moved text, but for the corrections and for the words that the move makes wrong (step 6).  Phase 18 rewrites the reference pages and phase 12 the guides.

## The work

### 1. Read the pages as they stand

Run `git diff 28e20e3c -- site/tool.md site/recipes.md`.  Each difference is a row of an earlier phase's ledger or text of feature work.  Add each unit of feature work to this phase's ledger and move it with the section it stands under.

### 2. Make the pages

`R/` is `site/reference/`, `G/` is `site/guides/` and `S/` is `site/start/`.  Each block moves whole, in the order shown.  The words column is the map's count.

| Page | Baseline lines, in order | Words |
|----|----|----|
| `R/cli.md`, new | Opening: one new sentence, then the paragraph of line 14.  `tool` (new anchor): the heading of line 1 and lines 3-12.  `install-the-tool` 18-29.  `command-reference` 441-456, with `arguments` 460-472, `options` 476-492, `environment-variables` 496-506, `exit-codes` 510-518, `verbose` 522-536, `logs` 540-558 and `requirements` 562-567.  `summary` 355-369.  Limit 631 | 2,410 |
| `R/describing.md`, new | `describe` (new anchor): a new heading and one new sentence.  `which-queries` 33-50.  `after-a-schema-change` 250-259.  `describe-on-save` 263-313, with `what-your-editor-shows` 317-330.  `check-in-ci` 334-351.  Limits 632, 635, 636, 637 | 2,340 |
| `R/connections.md`, new | `connections` 54-89.  `env-file` 93-127.  Limit 638 | 2,000 |
| `R/sidecars.md`, which exists | Phase 3's sentence that points to `tool.md` goes.  `sidecar-files` 131-168, with phase 2's two sentences.  `sidecars-in-git` 172-187.  `describing-again` 191-206.  All above phase 3's `## Limits`.  Line 634 after phase 3's text, under `limit-file-in-two-projects` | 1,410 |
| `R/msbuild.md`, new; phase 7 fills it | `running-in-the-build` 210-246 | 1,440 |
| `R/bundles.md`, new | Opening: one new sentence.  `bundle-contents` 394-405.  `diagnose` 571-589.  `replay` 593-625 | 1,680 |
| `G/report-a-bug.md`, new | `report-a-bug` 373-390.  `raw-bundle` 409-437 | 860 |
| `G/ci.md`, new | `check-sidecars-in-ci`, `recipes.md` 125-181, cut by two new h3 headings | 260 |
| `G/describe-on-save.md`, new | `recipes.md`: `start-the-watcher-with-your-editor` 200-202; `vs-code` 204-241; `rider` 243-248; `visual-studio` 250-254 | 460 |
| `S/keep-up-to-date.md`, new; phase 10 writes it | `after-a-migration`, `recipes.md` 185-196 | 60 |

That is about 12,900 words.

- **Heading levels.**  The seven headings under `command-reference` and `what-your-editor-shows` stay `###`.  Every other heading of the table is `##`.  So nine headings that are `###` today become `##`: `tool.md` 93, 172, 394, 409, 571 and 593, and `recipes.md` 204, 243 and 250.  The h1 of `tool.md`, "The tool", becomes the `##` heading of `tool`.  The words of a heading do not change, and each `<a id>` line stays above its heading.
- **The h1 of a new page** is new text: its sidebar title in the map's section 1.  `# The sqlsource command`, `# What describe does (watch, check, force)`, `# Connections and the .env file`, `# MSBuild properties and the build`, `# Bug-report bundles (diagnose, replay)`, `# Report a wrong model`, `# Check the sidecars in CI`, `# Describe queries as you save (watch)`, `# Keep your queries up to date`.
- **New anchors** above headings that have none today: `tool`, `describe`, `vs-code`, `rider`, `visual-studio`, and `ci-postgresql` and `ci-sql-server` below.
- **`G/ci.md`.**  `<a id="ci-postgresql"></a>` and a new h3 stand above the paragraph of line 127, and `<a id="ci-sql-server"></a>` and a new h3 above the paragraph of line 155.  Nothing moves: lines 177 to 181 stay last, under the second heading, until phase 12 gives the page its shape.
- **`G/describe-on-save.md`.**  The three editors are `##` sections.  The VS Code task, lines 206-235, is one fenced block that opens with a line that is exactly `` ```jsonc `` and closes with `` ``` ``, at the left margin: never in a tab, a list or a quote.

### 3. The new text

Write it in plain language, by the outline's [language rule](2026-11-12-docs-overhaul-epic-design.md#the-language).

- **`R/cli.md`, the opening**: one sentence, or two, that link `describing.md`, `msbuild.md#running-in-the-build`, `bundles.md` and `errors.md#the-tools-errors`.
- **`R/describing.md#describe`**: the heading "What describe does", and one sentence that says what `describe` does.  Its facts are those of `tool.md` 3: it connects to the database, asks for each query's parameters and columns, and writes the answers to the sidecars.
- **`R/bundles.md`, the opening**: one sentence that links `../guides/report-a-bug.md` for the procedure.
- **`R/cli.md#options`, the options of the other commands.**  The table of lines 480-490 has a row for each of the 11 options of `describe`, and the paragraph of line 456 says that the options below are those of `describe`.  So `--help`, `--version` and the four options that `diagnose` alone has stand in the synopsis and in no row.  Under the paragraph of line 492, add one sentence that introduces them and a second table of the same two columns, with six rows: `--help`, `--version`, `--query <file>:<name>`, `--out <path>`, `--no-obfuscate` and `--scrub <bundle>`.  A row holds one sentence on what the option does.  Each row of `diagnose` restates its row of the table under `bundles.md#diagnose` (`tool.md` 582-585) and links that section, and gives no rule of its own.  The rows of `--help` and `--version` restate the paragraph of line 456.  The two options of `replay`, `--verbose` and `--log`, have their rows already.
- The nine h1 headings, the two h3 headings of `G/ci.md`, three `## Limits` headings, six limit headings and six entries of `guides/troubleshooting.md`.

### 4. Place the limits

`R/cli.md`, `R/describing.md` and `R/connections.md` each end with `<a id="limits"></a>` and `## Limits`.  Each bullet goes under it word for word and stays a bullet, under an h3 with its own `<a id>`, as in phases 3 and 4.  The h3's words are new text, from the cell "What the reader sees" of the map's limits table.

| Limit | Bullet | Entry under, in `troubleshooting.md` |
|----|----|----|
| `R/cli.md#limit-path-through-a-symbolic-link` | 631 | `not-described` |
| `R/describing.md#limit-several-target-frameworks` | 632 | `not-described` |
| `R/describing.md#limit-saves-not-seen` | 635 | `watch` |
| `R/describing.md#limit-editors-not-checked` | 636 | `watch` |
| `R/describing.md#limit-folder-changes-not-seen` | 637 | `watch` |
| `R/connections.md#limit-short-password` | 638 | `secrets` |
| `R/sidecars.md#limit-file-in-two-projects` | 634, as the second text of MP-08 | It has its entry from phase 3 |

- **Line 634 loses its last sentence**, "See the known issues of `.sql` files.", which points at the text that now stands directly above it.
- **The pointer of line 633 goes.**  `settings.md` 1058-1068 owns its fact, and phase 7 makes that bullet the limit `R/msbuild.md#limit-file-added-by-a-target`.
- **The heading `## Known issues` and its anchor go.**  `R/msbuild.md`, `R/bundles.md` and the guides get no `## Limits`.

### 5. The two corrections

| Id | Where | Today | Becomes |
|----|----|----|----|
| C-01 | `R/msbuild.md#running-in-the-build`, the bullet of `tool.md` 235 | "only the tool reads that setting, so the tool writes the error and the build lists it" | "only the tool checks that value, so the tool writes the error and the build lists it" |
| C-03 | `diagnostics.md`, the `See:` line of `## SQLSRC294` (baseline 3623) | Two links, to `tool.md#raw-bundle` and `tool.md#known-issues` | One link, to `guides/report-a-bug.md#raw-bundle` |

### 6. Links inside `site/`

Every relative link of moved text is re-based to its new folder.  A link to an anchor of `tool.md` or to one of the three recipes names the new page of that anchor when it is not the link's own page:

| Page | Anchors |
|----|----|
| `reference/cli.md` | `tool`, `install-the-tool`, `command-reference`, `arguments`, `options`, `environment-variables`, `exit-codes`, `verbose`, `logs`, `requirements`, `summary` |
| `reference/describing.md` | `describe`, `which-queries`, `after-a-schema-change`, `describe-on-save`, `what-your-editor-shows`, `check-in-ci` |
| `reference/connections.md` | `connections`, `env-file` |
| `reference/sidecars.md` | `sidecar-files`, `sidecars-in-git`, `describing-again` |
| `reference/msbuild.md` | `running-in-the-build` |
| `reference/bundles.md` | `bundle-contents`, `diagnose`, `replay` |
| `guides/report-a-bug.md` | `report-a-bug`, `raw-bundle` |
| `guides/ci.md` | `check-sidecars-in-ci` |
| `guides/describe-on-save.md` | `start-the-watcher-with-your-editor` |
| `start/keep-up-to-date.md` | `after-a-migration` |

The links that this rule does not settle:

| Link at | New target |
|----|----|
| `tool.md` 89, 246, 552, to `#known-issues` | `connections.md#limit-short-password` |
| `tool.md` 238 | `describing.md#limit-several-target-frameworks` |
| `tool.md` 313 | `#limit-saves-not-seen`, same page |
| `tool.md` 48, to `generator.md#known-issues` | Stays, re-based: phase 6 makes its limit |
| `tool.md` 567, two links to each database's `#connecting` | Both take `databases/index.md#supported-versions`.  The words stay |
| `tool.md` 327, 346, to the recipes | `../guides/describe-on-save.md#start-the-watcher-with-your-editor`; `../guides/ci.md#check-sidecars-in-ci` |
| `recipes.md` 8, 9, 10, the list's links to the three recipes | `guides/ci.md#check-sidecars-in-ci`; `start/keep-up-to-date.md#after-a-migration`; `guides/describe-on-save.md#start-the-watcher-with-your-editor` |
| `generator.md` 3, to `tool.md` | `reference/describing.md#describe` |
| `concepts.md` 133, to `tool.md` | `reference/cli.md#tool`, until phase 8 takes the link out |
| `index.md`, the links that came from `site/README.md` | By the table; a link with no anchor names `reference/cli.md` |
| `guides/troubleshooting.md`, the sentence that links the known issues of the pages not yet moved | Its link to `../tool.md#known-issues` goes |

**Words that the move makes wrong.**  "This page has two parts" (line 14), each "above" or "below" whose referent left its page, and each link whose words name "the tool page", on a page of this phase or on one that an earlier phase moved, no longer say what is true.  Find them with `grep -n -i -E "This page|above|below|tool page" site/reference/*.md site/guides/*.md site/start/*.md`.  Change the fewest words that make each phrase true, and give the unit's row the note `position`.  Change no other word.

**Interim links this phase retargets:**

| Link | Left by | Parked at | Becomes |
|----|----|----|----|
| `tool.md` 567, two links | Phase 4 | Each database's `index.md#connecting` | `databases/index.md#supported-versions` |
| The sentence of `reference/sidecars.md` that points | Phase 3 | `../tool.md#sidecar-files` | It goes |
| One link of the one sentence of `guides/troubleshooting.md` | Phase 3 | `../tool.md#known-issues` | It goes |

**Interim links this phase leaves:**

| Link | Now on | Parked at | The map's target | Phase |
|----|----|----|----|----|
| `tool.md` 48 | `reference/describing.md` | `../generator.md#known-issues` | `R/attributes.md#limit-source-written-as-text` | 6 |
| `docs/tech-debt/TD-0025-...md` 25 | The same file | `site/settings.md#known-issues` | `site/reference/msbuild.md#limit-file-added-by-a-target` | 7 |
| `src/SqlSource.Tool/AGENTS.md` 15, "The logical database setting is in" | The same file | `site/settings.md` | `site/reference/settings.md` | 7 |
| `concepts.md` 133 | `concepts.md`, at the root | `reference/cli.md#tool` | None: the closing sentence goes | 8 |
| The links of `index.md` to a section of `tool.md`, or to the page | `index.md` | By the table; `reference/cli.md` for the page | None: the landing page is written new | 10 |

`git grep -n -E "\]\((\.\./)*tool\.md" -- site` lists the rest: at the baseline `diagnostics.md` 49, `recipes.md` 11, `settings.md` 6, `concepts.md` 4, `postgresql.md` 3, `generator.md` 2, `sqlserver.md` 2, `parameters-and-tokens.md` 1, and the 24 of `site/README.md`.

### 7. The two readmes and the issue template

The URLs change address only, by the table of step 6, in the form `https://mbcrawfo.github.io/SqlSource/<folder>/<page>.html#<anchor>`.  No word changes.

| File | URLs (baseline lines) |
|----|----|
| `README.md` | 9 to `tool.html` (126, 128, 130, 236, 299, 300) and 2 to the recipes (128, 300).  `tool.html` with no anchor becomes `reference/cli.html` |
| `src/SqlSource.Tool/README.md` | 5 (36, 38, 47, 48, 50): `reference/describing.html#describe-on-save`, `reference/msbuild.html#running-in-the-build`, `guides/report-a-bug.html#report-a-bug`, `reference/bundles.html#replay`, `reference/cli.html` |
| `.github/ISSUE_TEMPLATE/bug-report.yml` 12 | `https://mbcrawfo.github.io/SqlSource/guides/report-a-bug.html#report-a-bug` |

### 8. References outside `site/`

`git grep -n -E "site/tool\.md|site/recipes\.md" -- . ':!site' ':!docs/superpowers'` lists them.  Each reference to `tool.md` takes the page of its anchor.  A reference to `recipes.md` changes here only when it names one of the three recipes.  The ones that need more than the table:

| Where (baseline line) | Change |
|----|----|
| `docs/architecture/tool.md` 3; `examples/PostgresExamples/README.md` 66; `examples/SqlServerExamples/README.md` 71 | `site/reference/cli.md` |
| `docs/architecture/tool.md` 187, `#the-summary` | `site/reference/cli.md#summary`: the `<a id>`, not the heading's slug |
| `docs/architecture/tool.md` 283, "under its known issues" | A link to `site/reference/describing.md#limit-folder-changes-not-seen` |
| `docs/tech-debt/TD-0034-...md` 25 | `site/reference/connections.md#limit-short-password` |
| `docs/tech-debt/TD-0025-...md` 25 | The words "and those of `site/tool.md` point there" go.  Its link to `site/settings.md#known-issues` stays until phase 7 |
| `docs/tech-debt/TD-0097-...md` 56, 259; both example READMEs (76, 81) | `site/guides/describe-on-save.md#start-the-watcher-with-your-editor` |
| `tests/README.md` 13 | "the VS Code recipe of `site/recipes.md`" becomes "the VS Code task of `site/guides/describe-on-save.md`", and the list gains "the commands, options and variables of `site/reference/cli.md`" |

### 9. The sidebars

| File | Change |
|----|----|
| `site/reference/toc.yml`, "The sqlsource tool" | `../tool.md` goes.  The group is `cli.md` "The sqlsource command", `describing.md` "What describe does (watch, check, force)", `connections.md` "Connections and the .env file", `sidecars.md` "Sidecar files", `bundles.md` "Bug-report bundles (diagnose, replay)", in that order |
| `site/reference/toc.yml`, "Settings" | Gains `msbuild.md` "MSBuild properties and the build", last |
| `site/guides/toc.yml`, "Workflow" | Gains `describe-on-save.md` "Describe queries as you save (watch)", `ci.md` "Check the sidecars in CI" and `report-a-bug.md` "Report a wrong model", before `troubleshooting.md`.  The entry of `../recipes.md` stays |
| `site/start/toc.yml` | Gains `keep-up-to-date.md` "Keep your queries up to date" |

### 10. Tests

**`EditorRecipeTests`** (`tests/SqlSource.Repository.Tests/Watching/EditorRecipeTests.cs`).  Its `VsCodeTask()` reads `site/recipes.md` (line 140) and takes the one block between a line `` ```jsonc `` and a line `` ``` `` that holds `"beginsPattern"`.  After the move it finds none, and `ShouldHaveSingleItem` fails every test of the class.

- Line 140 reads `RepositoryRoot.Path("site", "guides", "describe-on-save.md")`.
- The comment of line 16 names `site/guides/describe-on-save.md#vs-code`, and the comment of line 137 says "of the guide".  `InboundSiteReferenceTests` fails on the old one.
- Nothing else changes: the task's JSON, the two markers and the patterns are as they were.

**`CliReferenceTests`**, new, in `tests/SqlSource.Repository.Tests/Documentation/`.  The project references the tool and sees its internals (`src/SqlSource.Tool/SqlSource.Tool.csproj` line 35).

| It reads | From | It expects, in `site/reference/cli.md` |
|----|----|----|
| Each command that is not hidden | `Cli.BuildCommands()` (`src/SqlSource.Tool/Cli.cs` line 409), the `Subcommands` of the root: `describe`, `replay`, `diagnose` | A line of the synopsis block that starts `dotnet sqlsource <command>`.  The synopsis is the fenced `text` block under `command-reference` |
| Each option that is not hidden, of the root and of each command | The `Options` of each `Command`, less those whose `Hidden` is true.  The root has `--help` and `--version`.  `describe` has 11, `diagnose` 9 and `replay` 2 | A row of a table under `options`, between `<a id="options"></a>` and the next heading, whose first cell starts with the option's `Name` in code font |
| Each `SQLSOURCE_` variable | Every string literal `"SQLSOURCE_[A-Z0-9_]+"` of the `.cs` files of `src/SqlSource.Tool`, `bin` and `obj` aside.  Today: `SharedOptions.LogVariable`, `Cli.DebugVariable`, `ConnectionVariables.Unnamed` | A row of the table under `environment-variables` whose first cell is the name in code font |
| The named connection variable | `ConnectionVariables.NamedPrefix`, `SQLSOURCE_CONNECTION_` | The row whose first cell is `` `SQLSOURCE_CONNECTION_<NAME>` `` |

- Today's text does not pass.  The 11 options of `describe` each have a row of the table under `options`, `--manifest` and `--online` among them, and five of the 9 options of `diagnose` and both of `replay` share those rows.  `--help`, `--version`, `--query`, `--out`, `--no-obfuscate` and `--scrub` have none: the six rows of step 3 are theirs.  See the test fail before you write them.
- **An option is hidden by `Hidden = true`** on its `Option`, a property of System.CommandLine's `Symbol`.  The remarks of `SharedOptions` (`src/SqlSource.Tool/SharedOptions.cs` lines 20-21) say when: an option that is read before it has an effect.  No option is hidden today, so the filter excludes nothing yet.  Write it all the same.
- The test fails for a command, an option or a variable that the tool gains and the page lacks.  It does not check arguments, exit codes or the words of a row.
- A comment on the test says what fails it.  A finder that finds no command and no variable must fail too.

**`SiteStructureTests`**: `tool.md` leaves `PagesNotYetMoved`.  `recipes.md` stays in it.

`UserDocsLinksTests`, `SiteTocTests` and `InboundSiteReferenceTests` need no change.  They fail until steps 6 to 9 are complete.

### 11. `AGENTS.md` files

**`src/SqlSource.Tool/AGENTS.md`**.  Lines 15 and 30 name `tool.md`, and the new paths are longer than the file's room.  Three lines change, and the file ends at 8,174 bytes when phases 1 to 4 changed nothing else in it:

- Line 14, which loses its last sentence, "Keep it that short.": `` - **`README.md` here is the package's short page on nuget.org**: what the tool is for, how to install it, its commonest commands, and links to the manual and the repository's `README.md`. ``
- Line 15: `` - **The tool is documented in `site/reference/`**: `cli.md`, where a new command, option, environment variable or exit code goes, `describing.md`, `connections.md`, `sidecars.md`, `bundles.md` and `databases/`.  The logical database setting is in `site/settings.md`. ``
- Line 30, its first sentence: `` - **The exit code is `0`, `1` or `2`**: the manual's [exit codes](../../site/reference/cli.md#exit-codes). ``  The rest of the bullet stays.

**`src/SqlSource.Tool/Watching/AGENTS.md`** line 5, its second sentence: `` The manual's [describe on save](../../../site/reference/describing.md#describe-on-save) quotes them, the VS Code task of `site/guides/describe-on-save.md` reads them in its problem matcher, and `EditorRecipeTests` of `tests/SqlSource.Repository.Tests/Watching/` holds the two together. ``

**`site/AGENTS.md`**:

- The row of the table reads `| Command, option, environment variable, exit code | reference/cli.md | CliReferenceTests |`, with the page and the test in code font.
- The bullet "Pages not yet moved" loses `tool.md`.  Its clause on a recipe stays, since `recipes.md` stays.
- Under "Write effects, never internals", "(`tool.md` links `schemas/sidecar-v1.schema.json` once, for the curious)" names `reference/sidecars.md`.
- Run `grep -n -E "tool\.md|recipes\.md" site/AGENTS.md site/*/AGENTS.md` and rewrite each other hit to the page that now holds the thing.

**`tests/SqlSource.Repository.Tests/Documentation/AGENTS.md`**: its first bullet names `CliReferenceTests` and what it holds.

Check each file against `AgentsFileTests`: 8,192 bytes and 80 words a rule.

### 12. Remove `tool.md`, and what is left of `recipes.md`

`git rm site/tool.md`, once the checker finds every moved unit.  `site/recipes.md` stays at the root with:

- lines 1-23: the title, the opening paragraph and the list of 19 links, three of which now leave the page;
- 16 recipes: lines 27-121 (three) and 258-1030 (thirteen).

## The ledger

`docs/superpowers/specs/2026-11-12-docs-overhaul/ledger/phase-05.tsv` holds a row for each unit of `tool.md` and of `recipes.md` 125-254.  Beyond the `move` rows:

1. `move`, with the note `level`: `tool.md` 1, the h1, whose words become the heading of `R/cli.md#tool`.
2. `merge-pair MP-10`: `tool.md` 3-8 and 133, the openings that phase 18 merges with the terms "Tool" and "Sidecar".
3. `restated-from`, with a location: `tool.md` 10-12, word for word under `R/cli.md#tool`.
4. `move`, navigation: `tool.md` 14, in the opening of `R/cli.md`.
5. `merge-pair MP-11`: `tool.md` 18-29.  `merge-pair MP-06`: 210-246.  `merge-pair MP-19`: 373-390.
6. `corrected`: C-01, inside the bullet of line 235, and C-03.
7. `move-split`: `tool.md` 634.  Its text is `merge-pair MP-08`, and its last sentence is `restated-from R/sidecars.md#limit-file-in-two-projects` and goes.
8. `restated-from`, with no location: `tool.md` 633.
9. `move-split`: the heading "Known issues", `tool.md` 629, which goes with its anchor.
10. `move`, with a note: the ten headings that change level.
11. A note on `tool.md` 567: both links take the table of supported versions.
12. `new`: the text of step 3, the six rows of options among it, and the changed lines of the `AGENTS.md` files.  A note on the two sentences of phase 3 that go: the one on `R/sidecars.md` that points to `tool.md`, and the link of `troubleshooting.md`.
13. `move`, with the note `position` and the old and the new words: each unit whose words step 6 mended.
14. A row by `@site/tool.md:<line>` for each unit that is not in the inventory: phase 1's sentence beside `--prerelease`, and phase 2's sentences in `#sidecar-files`.

Open or extend the worksheets of the epic folder with each text this phase places and its new address: `merges/MP-06.md` (`tool.md` 210-246, which phase 7 adds to), `MP-08.md` (634), `MP-10.md` (3-8 and 133), `MP-11.md` (18-29) and `MP-19.md` (373-390).  The merged text is the rewrite's.

## Done when

- [ ] `test ! -e site/tool.md`, and `grep -c "^## " site/recipes.md` prints 16.
- [ ] "check move 05" finds every unit that the ledger marks moved.  What it leaves over is rows 6 to 9 and 13 of "The ledger".  The checker compares a heading without its `#` marks, so rows 1 and 10 are in no list.  Nothing else is left over.
- [ ] "check coverage" passes, and `ls docs/superpowers/specs/2026-11-12-docs-overhaul/merges/` lists `MP-06.md`, `MP-08.md`, `MP-10.md`, `MP-11.md` and `MP-19.md`.
- [ ] `git grep -n -E "\]\((\.\./)*tool\.md" -- site` prints nothing.
- [ ] `git grep -n -E "site/tool\.md|SqlSource/tool\.html" -- . ':!docs/superpowers'` prints nothing but test data in the files that phase 0's `InboundSiteReferenceTests` leaves out whole, `UserDocsReadingTests.cs` and `InboundSiteReferenceTests.cs`.
- [ ] `git grep -n -E "recipes\.(md|html)#(check-sidecars-in-ci|after-a-migration|start-the-watcher-with-your-editor)" -- . ':!docs/superpowers'` prints nothing.
- [ ] `git grep -l 'id="known-issues"' -- site` prints `site/generator.md` and `site/settings.md` alone.
- [ ] `git grep -c '<a id="limit-' -- site/reference/cli.md site/reference/describing.md site/reference/connections.md site/reference/sidecars.md` prints 1, 4, 1 and 1.
- [ ] `grep -c '^```jsonc$' site/guides/describe-on-save.md` prints 1, and `grep -c "#tab/" site/guides/describe-on-save.md` prints 0.
- [ ] `wc -c src/SqlSource.Tool/AGENTS.md` prints 8192 or less: 8174 by this brief's words.
- [ ] `EditorRecipeTests` passes.  `CliReferenceTests` failed before the six rows of step 3 were written, passes now, and fails when the row of `SQLSOURCE_DEBUG` or the row of `--scrub` is taken out of `reference/cli.md` by hand.
- [ ] `tools/build-site.sh` exits 0, with no docfx warning.
- [ ] The content reviews, `branch-review:review` and `./pre-commit-validation.sh --full` are clean on the last commit.
- [ ] The pull request tells the owner to publish the site by hand after the merge: both readmes and the issue template then name pages that the published site lacks.

## For the reviewers

The no-loss reviewer, beyond the general kit:

- **The VS Code task**, `recipes.md` 206-235: byte-identical.
- **`tool.md` 235**: one clause changed, and no other word of the bullet.
- **`tool.md` 634**: whole but for its last sentence, and after phase 3's text, not in place of it.
- **Text that is not in the baseline page**: phase 1's sentence beside `--prerelease` in `install-the-tool`, and phase 2's two sentences in `sidecar-files`.  Each is a row of that phase's ledger and must be on its new page.
- **`tool.md` 10-12**, which the map marks restated: here it moves whole.
- **The three `Rules:` lines** of the recipes (181, 196, 254), each last in its section.
- **The 16 rows of the options and variables tables** (480-490, 500-504), row for row.

The accuracy reviewer checks the new and corrected text against these sources:

- **C-01** against `src/SqlSource/Generation/Methods/MethodPlanner.cs` lines 319-320: `DatabaseOf` calls `QuerySettings.ResolveDatabase`, so the build reads the setting.
- **C-03** against the eight bullets of `tool.md` 631-638: none is about a bundle.
- **The sentence of `R/describing.md#describe`** against `src/SqlSource.Tool/DescribeCommand.cs` and `tool.md` 3.
- **The opening of `R/cli.md`**: each of its four links names a section that holds what the sentence says.
- **`CliReferenceTests`** against `Cli.BuildCommands`, `DescribeCommand.Create`, `DiagnoseCommand.Create` and `ReplayCommand.Create`: the counts 11, 9 and 2, and that the test would fail for a new option.
- **The six new rows of options** against `DiagnoseCommand.Create` (lines 71 to 102), `Cli.BuildCommands` for `--version`, and the rows of `reference/bundles.md#diagnose`: a new row says no more than its owner.
- **Each unit with the note `position`**: the fewest words changed, and the phrase is now true.
- **Each limit's heading and each entry of `troubleshooting.md`** against its bullet.
- **The three `AGENTS.md` lines**: each page they name exists and holds what the line says.

## Traps

- **`recipes.md` stays.**  Its list links the three recipes by anchors that are gone: retarget those three links, or the build fails.  The twins of the Dapper recipes, in `tests/DatabaseTests/*/EndToEnd/` and `examples/`, name `site/recipes.md` and stay true until phase 8.
- **Two things are named "describe on save".**  `reference/describing.md#describe-on-save` holds the rules, and `guides/describe-on-save.md` holds the editor recipes.  A link to `tool.md#describe-on-save` takes the first.
- **`docs/architecture/tool.md` 187 links the heading's slug**, `#the-summary`.  It resolves on the new page too, and breaks when phase 18 rewords the heading.
- **The task must stay where `EditorRecipeTests` finds it.**  An indented fence, a fence with a second word after `jsonc`, or a second `jsonc` block that holds `"beginsPattern"` fails it.
- **`UserDocsReadingTests` holds strings such as `tool.html#exit-codes`.**  They test the form of a URL.  If one of them is resolved to a file after phase 0, give it `reference/cli.html`.
- **The sample of `tool.md` 218 holds a `diagnostics.html` URL** inside a `console` block.  It does not change.
- **`--manifest` and `--online` are for the build's use and are documented.**  Do not hide them, and do not take their rows out.
- **`reference/msbuild.md` joins the Settings group, but the row "MSBuild property" of `site/AGENTS.md` keeps naming `settings.md`**: the property sections move in phase 7.
- **`start/keep-up-to-date.md` holds one unit and has no "Next:" line.**  The map plans it so until phase 10.  Do not write the rest of the page.
- **The issue template is a folded YAML scalar.**  Keep its indentation, and run the linters.
- **Measure `src/SqlSource.Tool/AGENTS.md` after the edit.**  Its room is 18 bytes by this brief's words.  If an earlier phase changed another line, cut words of lines 14 and 15, never a rule about the code.

## Left to the agent

- The words of the new sentences, of the six limit headings, of the two h3 headings of `G/ci.md` and of the six entries of `troubleshooting.md`.
- The names of the test methods of `CliReferenceTests`, and whether the methods of `EditorRecipeTests` keep "Recipe" in their names.
- The heading of `troubleshooting.md` for an entry, where an earlier phase put a like symptom elsewhere: follow it, and keep the six headings.
- Whether the links are rewritten by a script or by hand.

## Calls made in this brief

- **"A row" of `CliReferenceTests` is a row**: a synopsis line for a command, and a row of a table for an option and for a variable, as the outline's table of tests says.  So this phase writes six rows that today's page lacks, as new text that the map does not list.  Each restates its owner in one sentence and links it, so `reference/bundles.md#diagnose` stays the owner of the options of `diagnose`.
- **The variables are found by their string literals**, so that a new one is caught.  `DOTNET_USE_POLLING_FILE_WATCHER` has a row and is not tested, since the outline names the `SQLSOURCE_` variables.
- **The old h1, "The tool", is the heading of `R/cli.md#tool`.**  The map words that heading "The `sqlsource` command", which is the page's title.  A heading may be reworded and its anchor may not, so phase 18 can change it.
- **`tool.md` 14 moves whole**, though the map calls it navigation: its second sentence states what `diagnose` and `replay` do.
- **The last sentence of `tool.md` 634 goes.**  The map says its link goes, and the words without the link point at nothing.
- **Both links of `tool.md` 567 take one target**, since the map sends them to the table and a move changes no word.
- **`G/ci.md` keeps today's order**, so three paragraphs for both databases stand under the second heading until phase 12.
- **`concepts.md` 133 and the links of `index.md` get a target now**, since their page is gone before phases 8 and 10 take them out.
- **`src/SqlSource.Tool/AGENTS.md` names the five pages**, as the map says, and pays for them with "Keep it that short." and the words "as the manual's ... say".  Neither is a rule that the bold text of its bullet does not state.
- **Words that the move makes wrong are mended**, in the fewest words, each with the note `position`, as the outline rules and as in phase 4.  Phases 12 and 18 still rewrite the sentences.
