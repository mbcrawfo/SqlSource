# Docs overhaul, phase 00: guard-rails

[The outline](2026-11-12-docs-overhaul-epic-design.md) and [the content map](2026-11-12-docs-overhaul/content-map.md) are the authority.  This brief adds what is specific to phase 0.  It is also the specification of the epic's instruments, the inventory, the ledger, the checker and the merge worksheet, which every later brief uses by name.

Done by: an Opus agent.  Needs: the outline and the map.  Runs alone.

## Goal

The tests of the user documentation accept a site with folders, nested `toc.yml` files, docfx tabs, front matter and a landing page, while the site is still twelve flat pages.  A new test follows every reference to a page of `site/` from the rest of the repository.  The epic gets its instruments: an inventory of every unit of the user documents at the baseline, a ledger, and a checker.  No page of `site/` changes.

## Where things stand

- **The site.**  `site/` holds twelve pages at its root, `index.md` (three lines of front matter that redirect to `README.html`), a flat `toc.yml` of twelve entries, and `docfx.json`, whose content is `"files": ["*.md", "toc.yml"]`.
- **The tests**, in `tests/SqlSource.Repository.Tests/`: `Documentation/UserDocs.cs`, `UserDocsLinksTests.cs`, `UserDocsReadingTests.cs`, `UserDocsSamplesTests.cs`, `SiteTocTests.cs`, `AgentsFileTests.cs`, and `Diagnostics/DiagnosticsDocumentTests.cs`.  No test reads a reference to `site/` from outside the user documents.
- **The baseline** is commit `28e20e3ce8fc0a7f52abf5cd3ae99b9f8a22bf03`.  `main` can be ahead of it when you start: feature work goes on.
- **The epic folder**, `docs/superpowers/specs/2026-11-12-docs-overhaul/`, holds `content-map.md`, `rewrite-guide.md` and `diagnostics-guide.md`.  It has no inventory, no ledger and no checker.
- **The outline, the map, the guides and the phase briefs** may not be on `main` yet.  If they are not, this pull request commits them, unchanged.
- **Two facts about the repository's checks** that the planning did not see.  `pre-commit-validation.sh` does not know the kind `.tsv` (`is_known_kind`, line 46), so a commit that touches one makes a plain run full.  `.editorconfig` holds every file but `.md` to 120 columns, and `tools/editorconfig-checker.sh` checks every tracked file.

## In scope / Not in scope

In scope:

- The changes to `UserDocs`, `UserDocsLinksTests`, `UserDocsReadingTests` and `SiteTocTests` in [the table](#1-the-tests-that-change).
- `InboundSiteReferenceTests`, new.
- `site/docfx.json`, and the two lines of `pre-commit-validation.sh` and `.editorconfig` that the epic's files need.
- `inventory.tsv`, `ledger/phase-00.tsv` and `check/` in the epic folder.
- The lines of `tests/SqlSource.Repository.Tests/Documentation/AGENTS.md`, `tests/README.md`, `docs/superpowers/AGENTS.md` and the outline that this phase makes wrong.

Not in scope:

- Any page of `site/`, `site/toc.yml`, `site/AGENTS.md`, root `AGENTS.md`, either readme.
- `DiagnosticsDocumentTests` (phase 2), `StatusLanguageTests` (phase 1), `SiteStructureTests` (phase 3).
- The root of the site as `site/index.md` (phase 3).  The four test strings that name `what-works-today` (phase 1).
- A merge worksheet: the phase that places the first text of a pair starts it.

## The work

### 1. The tests that change

The site must build and every test pass with today's flat pages.  A row says what changes now, and what waits.

| Member | Changes now | Waits |
|----|----|----|
| `UserDocs.SitePages` (lines 36-43) | It lists every `.md` file under `site/` at any depth, as a path from the root with `/`.  It leaves out every `AGENTS.md` and `CLAUDE*.md`, as `docfx.json` does, and `site/index.md` alone, not every file of that name: `reference/databases/index.md` will be a page | Phase 3 takes `site/index.md` out of the exclusion |
| `UserDocs.SiteLinkProblem` (104-127), `SitePagePattern` (394-395) | A page of the site is `<page>.html` or `<folder>/.../<page>.html`.  The message names that form | The root with an anchor stays a problem until phase 3 |
| `UserDocs.Resolve` (146-162) | `reference/cli.html#x` gives `site/reference/cli.md` and `x` | The root gives `site/README.md` until phase 3 |
| `UserDocs.Read` (191-279), front matter | A document whose first line is `---` has front matter up to the next line that is `---`.  `Read` takes no anchor, no link and no unsupported form from those lines, and the lines after them keep their numbers | |
| `UserDocs.Read`, tab headings | A heading whose whole text is one link to `#tab/<id>`, such as `# [PostgreSQL](#tab/postgres)` or `# [Linux](#tab/linux/x64)`, is a docfx tab.  It gives no anchor, is not counted for the suffix of a repeated heading, and its target is not a link | |
| The comments of `UserDocs.cs` at 19-20 and 34-35 | They say that a page is `<path>.md` at any depth | The words on the redirect stay until phase 3 |
| `UserDocsLinksTests.Manual_EntryPage_ListsEveryPage` (125-141), and the constant `Manual` (12) | Both go.  Phase 2 adds a page, and phase 3 removes the entry page | |
| The other eleven methods of `UserDocsLinksTests` | Nothing.  `Follow` and `Resolve` already take a relative link from the folder of its file | |
| `SiteTocTests.Toc_ListsEveryPageOnce_AndNothingElse` | It reads every `toc.yml` under `site/`.  An `href` is read from the folder of its `toc.yml`, so `../diagnostics.md` in `reference/toc.yml` is `site/diagnostics.md`.  An `href` that ends in `/` or names a `toc.yml` is a folder entry: the folder must hold a `toc.yml`, and the entry names no page.  Every other `href` must be a page of `UserDocs.SitePages`, and every page is named by exactly one entry of all the files together | Phase 3 says how the landing page, which is in no sidebar, is excepted |
| `UserDocsReadingTests` | New cases, below.  `sub/tool.html` (line 195) moves from `SiteLinkProblem_NotAPageOfTheSite_IsReported` to `SiteLinkProblem_APageOrAnotherSite_IsNone`, and the comment at 189-190 no longer says that the site has no folders | Lines 178-180, 192 and 196 (phase 1); line 182 and the root cases (phase 3) |
| `UserDocsSamplesTests`, `AgentsFileTests` | Nothing.  A fenced block inside a tab is read as any other, and a nested `AGENTS.md` is found by the walk | |
| `DiagnosticsDocumentTests` | Nothing | All of it is phase 2's |
| `Watching/EditorRecipeTests` | Nothing: it reads `site/recipes.md` by its path | Phase 5 |

New cases of `UserDocsReadingTests`, on lines of their own:

- A tab group: `# Title`, `# [PostgreSQL](#tab/postgres)`, a paragraph, a blank line, `# [SQL Server](#tab/mssql)`, a paragraph, a blank line, `---`.  The anchors are `title` alone, there is no link, and nothing is unsupported.
- Front matter: `---`, `title: Start`, a line that looks like a link, `---`, `# Title`.  The anchors are `title` alone, there is no link, and nothing is unsupported.
- A `---` directly under a paragraph is still reported.  Markdown reads it as a heading, also inside a tab group, so the closing `---` of a tab group needs a blank line above it.
- `Resolve` of `https://mbcrawfo.github.io/SqlSource/reference/cli.html#exit-codes` and of a URL three folders deep.
- `SiteLinkProblem` reports `.../SqlSource/reference/` and `.../SqlSource/reference/cli`, which name no page.

Write `SiteTocTests` so that its check is a function of the text of the `toc.yml` files and the list of pages.  Then one test gives it a nested set of its own (a root file of two folder entries, a folder file that names `../diagnostics.md`, a page in two files, a page in none, a folder entry with no `toc.yml`), and one test gives it the real files.

### 2. `InboundSiteReferenceTests`

A new class in `tests/SqlSource.Repository.Tests/Documentation/`.  It fails on a reference to a page of the site, from outside the user documents, that names no page or no anchor.

**What it scans.**  Every text file of the repository, so not a build output, not `artifacts/site` and not another worktree under `.claude/`.  The test walks the tree from `RepositoryRoot.Path()`, as `AgentsFileTests.Find` does.  It skips the folders of `AgentsFileTests.SkippedFolders`, which moves to one place that both classes read, so that it is kept once.  It also skips `artifacts`, every other folder whose name starts with `.` but `.github`, and the file `CLAUDE.local.md`.  The walk must see only what git tracks: a folder that `.gitignore` leaves out, such as `.superpowers/`, holds briefs of local tools that quote paths of `site/`, and a reference in one fails the test on one machine while it passes in CI.  `.github` is the one folder with such a name whose tracked text the test needs: `.claude` holds four tracked files beside its worktrees, none of them names a page of the site, and it stays skipped.  The test does not ask git for the files: `tests/AGENTS.md` lets a test start a program only through `Programs.RunAsync`, which is in `tests/SqlSource.Tool.Testing`, and `tests/README.md` (line 139) says that this project references nothing of the other test libraries.  A text file is one of a list of extensions in the test: `.md`, `.cs`, `.yml`, `.yaml`, `.json`, `.props`, `.targets`, `.csproj`, `.sh`, `.txt`, `.sql`, `.editorconfig`.  Left out: every path under `docs/superpowers/`, whose files record intent and are not kept current; the user documents, `UserDocs.Files`, whose links `UserDocsLinksTests` follows; and the strings of the table below.

**The forms it reads**, anywhere in a line:

| Form | Where it is today | It must name |
|----|----|----|
| A path `site/<path>.md`, with or without `#<anchor>`.  One pattern finds a relative Markdown link (`../../site/tool.md#exit-codes`), a mention in code font and a mention in a C# comment.  `site/` must start the path or follow `../`: no letter, digit, `_` or `-` stands before it | 274 references in 78 files: 42 `.md`, mostly of `docs/architecture`, `docs/tech-debt`, `docs/deferred` and the examples, and 36 `.cs`, in comments | A file that exists, and an anchor that `UserDocs.Anchors` gives for it |
| A site URL, `https://mbcrawfo.github.io/SqlSource/<path>.html`, with or without `#<anchor>` | 14 files, among them `.github/ISSUE_TEMPLATE/bug-report.yml` 12, five help links of `src/SqlSource/build/SqlSource.targets`, and `SqlDiagnostics.HelpLinkBase` | A page for which `UserDocs.SiteLinkProblem` is null and that `UserDocs.Resolve` finds, with its anchor.  The root alone, and a URL that ends in `#`, name no anchor |

**What it does not read**, on purpose: a page named without `site/` (as `site/AGENTS.md` and line 15 of `src/SqlSource.Tool/AGENTS.md` name some), words such as "the manual's recipe", the folder `site/` alone, a placeholder such as `<page>.html`, and a pattern such as `site/*.md`.

**Would it pass today?**  A search of the tree at the baseline finds every reference good but these, each of which the test must name with its reason, as narrowly as it can:

| File | The string | Why it names nothing |
|----|----|----|
| `tests/SqlSource.Repository.Tests/Documentation/UserDocsReadingTests.cs` | `site/README.md#y`, `site/page.md` (134-137), and the site URLs of 192-200 | Test data of the reading, wrong on purpose.  Leave the whole file out |
| `InboundSiteReferenceTests.cs` itself | Its own test data | The same |
| `tests/SqlSource.Repository.Tests/Validation/TestScopesTests.cs` 30 | `site/x.md` | A made-up path for a pattern test |
| `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs` 66, `tests/SqlSource.Tool.Tests/Expected.cs` 9 | `diagnostics.html#sqlsrc` | The start of a help link, which the code ends with the digits of an id |

That is four exceptions, the four rows, and the later briefs count on these four and no other.

Write the finding of references as a function of text, and test it on strings of its own: each form, each thing it does not read, a path with an anchor that is not there.  A second test asks that the scan of the real tree finds at least the references to `site/diagnostics.md`, so that a scan that finds nothing does not pass.

### 3. The build

| File | Change |
|----|----|
| `site/docfx.json` | `"files": ["*.md", "toc.yml"]` becomes `"files": ["**/*.md", "**/toc.yml"]`.  The `exclude` already has `**/`.  Checked for this brief in a scratch copy with docfx 2.81: today's pages build to the same 14 `.html` files with no warning, and a page with front matter and a tab group in a sub-folder, listed by a nested `toc.yml`, builds too |
| `tools/build-site.sh` | None.  Its `find` already looks through every folder of the output |
| `pre-commit-validation.sh`, the site | None.  `site/*.yml` (line 47) and `site/*` (line 200) match a path in a sub-folder: `*` crosses `/` in a bash pattern |
| `pre-commit-validation.sh`, the epic's files | Line 47 gains the pattern `docs/superpowers/specs/2026-11-12-docs-overhaul/*.tsv`.  The rule of `tools/AGENTS.md` on `is_known_kind` is met: the one check that reads a `.tsv` is editorconfig-checker, which always runs.  Phase 26 takes the pattern out |
| `.editorconfig` | A new section, `[docs/superpowers/specs/2026-11-12-docs-overhaul/**.tsv]` with `max_line_length = off`, and a comment that says whose files they are.  Phase 26 takes it out |
| `tools/test-scopes.txt` | None.  `none site/*` and `none docs/*` cover every new path, and the file says that `*` crosses `/` |
| `.github/workflows/build.yml`, `docs.yml` | None.  Both call `tools/build-site.sh` |

A change to `pre-commit-validation.sh` makes the run full, and a change to `.editorconfig` runs the format check on everything.  Both are expected in this phase.

### 4. The inventory

`inventory.tsv` lists every unit of the fourteen user documents at the baseline: `README.md`, `src/SqlSource.Tool/README.md`, and the twelve pages of `site/` (`README`, `concepts`, `diagnostics`, `dialects`, `generator`, `parameters-and-tokens`, `postgresql`, `recipes`, `settings`, `sql-files`, `sqlserver`, `tool`).  `site/index.md` holds no text and is not in it.

**A unit** is one line that is not blank and is outside a fenced block, or one whole fenced block, from its opening fence to its closing fence.  No paragraph of these documents is wrapped, so a paragraph, a bullet, a table row and a heading are each one line.  Not units: a line that holds only `<a id="..."></a>`, the rule line of a table (`|----|----|`), and front matter.

**The normalised text** of a unit is what the checker compares:

- White space at both ends of the line goes, so that a bullet or a paragraph can change its indent.
- An inline link or image is reduced to its text: `[text](target)` becomes `text`.  So a link's target can change, and a link can be put around words or taken off them.
- An `<a id="..."></a>` inside a line goes.
- A heading loses its `#` marks, so that it can change its level.
- A fenced block is its lines as they are, the fences and the info string included, less the indent of its opening fence.  Nothing inside it is changed.
- Nothing else: case, spaces inside the line and punctuation count.

**The columns**, separated by tabs, after a first line `# baseline 28e20e3ce8fc0a7f52abf5cd3ae99b9f8a22bf03` and a header line:

| Column | Holds |
|----|----|
| `id` | `<file>:<line>`: the path from the root and the unit's first line at the baseline, such as `site/settings.md:153`.  It is stable because the baseline is one commit |
| `last` | The unit's last line: its own line, or the closing fence of a block |
| `kind` | `heading`, `para`, `bullet`, `row` (a header row too), `block` or `quote` |
| `words` | The count of `wc -w` over the unit |
| `hash` | The first 12 hex digits of the SHA-256 of the normalised text, in UTF-8 |
| `phase` | The first phase that gives the unit a disposition, from the table below |
| `start` | The first 60 characters of the first line of the normalised text, for a reader.  For a fenced block that is its opening fence and info string |

**No cell holds a tab or a line break**, in `inventory.tsv` or in a ledger file, so that one row is one line.  A tab inside a cell's text is written as one space.  A cell stops at the first line break of its text: `start` does, and so does the `words` of a ledger row, whose part is then given as `*` with its lines in the note when it runs over more than one line.

**A line range** of the map or of a ledger row, such as `33-58`, is every unit whose first line is from 33 to 58.  A range can start or end on a line that is no unit.

Counted for this brief: about 3,960 units (422 headings, 1,358 paragraphs, 925 bullets, 731 table rows, 522 fenced blocks, 1 quote), and 211 `<a id>` lines, the number the map gives.  A count far from these means another grain.

**The `phase` column** comes from section 10 of the map.  Keep the table as `check/phases.tsv` (file, first line, last line, phase), so that the inventory can be made again.  Check each row against section 10, which wins:

| Document | Lines | Phase |
|----|----|----|
| `README.md` | 7, 13 | 1 |
| `README.md` | 9, 19-29, 132-288 | 8 |
| `README.md` | The rest of 1-130 | 10 |
| `README.md` | 290-305 | 11 |
| `src/SqlSource.Tool/README.md` | 7; the rest | 1; 11 |
| `site/README.md` | 61-105; the rest | 1; 3 |
| `site/concepts.md` | 27-139; the rest | 8; 10 |
| `site/diagnostics.md` | All | 2 |
| `site/sql-files.md`, `site/parameters-and-tokens.md`, `site/dialects.md` | All | 3 |
| `site/postgresql.md`, `site/sqlserver.md` | All | 4 |
| `site/tool.md` | 20; the rest | 1; 5 |
| `site/recipes.md` | 125-254; the rest | 5; 8 |
| `site/generator.md` | All | 6 |
| `site/settings.md` | 36, 38-58 | 1 |
| `site/settings.md` | 257-393, 572-619, 716-818, 914-955 | 6 |
| `site/settings.md` | The rest | 7 |

### 5. The ledger

`ledger/phase-<nn>.tsv` says what phase `<nn>` did with each unit it touched.  One file for each phase, so that phases which run together never edit one file.

- The first line is `# base <sha>`: the commit of `main` that the phase's branch starts from.  After a rebase the agent writes the new one.  The checker reads "old" at this commit.
- Any other line that starts with `#` is a comment.  A rewrite phase writes its word counts in one.
- Then a header line and the rows, six columns separated by tabs.  A cell is never empty: `-` stands for nothing, since a tab at the end of a line fails the check of `.editorconfig`.

| Column | Holds |
|----|----|
| `unit` | A unit of the inventory, `site/tool.md:235`.  A range of them, `site/tool.md:54-127`: the row holds for each.  A part of a unit, `README.md:13/1`.  Text that is not in the inventory, by its place at the phase's base: `@site/errors.md` for a whole file, `@site/tool.md:140` or `@site/tool.md:140-152` for lines.  `-` for a `new` row |
| `disposition` | One of `move`, `move-split`, `merge-pair`, `restated-from`, `removed-status`, `removed-preamble`, `corrected`, `new` |
| `to` | Where the unit stands after the phase: `<file>` or `<file>#<anchor>`, from the root.  `-` when it stands nowhere |
| `arg` | `MP-<nn>` for `merge-pair`.  The owner, `<file>#<anchor>`, for `restated-from`.  `C-<nn>` for a correction of the map.  `fact` for a `move` whose fact stands at `to` in new words.  Else `-` |
| `words` | For a part, the words of the part as the baseline has them, normalised.  For `corrected`, the new words.  `*` for a part that cannot be written as words, such as a cell of each row of a table.  Else `-` |
| `note` | The reason, in a few words.  Required for `removed-status`, `removed-preamble`, `corrected`, a `restated-from` with no `to`, and every row whose unit the checker prints as left over.  It starts with the word `position` on the `move` row of a unit whose words the move mended.  A note that opens `no owner: <file>#<anchor>` marks a fact that a rewrite found on a page whose owner lacks it: phase 25 places it |

How each case is written:

- **`move`**: the unit stands at `to`, word for word.  With `fact` in `arg`, its fact stands at `to` in new words: a phase that writes a new page from old text, as phase 2 does, uses it, and the checker prints the row and does not look for the words.  In a rewrite phase `move` always means that the unit's facts stay on its page, in new words.
- **`move`, with the note `position`**: the unit stands at `to` word for word, but for the fewest words that the move made wrong: "this page", "above", "below", or a page of today named in words, such as "the generator page".  The note starts with `position` and gives the old words and the new, as `position: "the generator page" > "this page"`.  A `merge-pair` or a `restated-from` row with a `to` takes the note in the same way.  The checker looks for the file and the anchor alone, and prints the unit as left over, to be read.  The outline's "How a phase is done" has the rule, and no other change of words takes this note.
- **`move-split`**: one row for the unit, with `to` `-`, then one row for each part, `<unit>/1`, `<unit>/2`, each with its own disposition, its own `to` and its `words`.  A part can have a range as its unit when the same words are cut from each, as the Status cell is from 19 rows.
- **A heading that goes**, where the map says "heading only" (the title of a page that is cut, a group heading, "Known issues"): a `move-split` row with `to` `-`, no part, and a note that says what stands in its place.  The same row serves for the second heading of a merge pair, which a rewrite removes when it merges the pair and lifts the heading's `<a id>` line.  Only a unit of the kind `heading` can be a `move-split` with no part.
- **`merge-pair`**: as `move`, with the pair's id in `arg`.  The texts of a pair are placed whole, so the checker finds them as it finds a `move`.
- **`restated-from`**: with a `to`, the unit moves there whole, and the rewrite of that page cuts it.  With `to` `-`, the unit goes, and `arg` names the section that owns its facts.
- **`removed-status`, `removed-preamble`**: the unit goes.  `to` is `-`.
- **`corrected`**: the old words go, and `words` holds the new ones, which stand at `to`.  `words` is `-` when the false words go and nothing replaces them in this phase; the note then names the phase that states the truth.
- **`new`**: text that has no unit.  `unit` is `-` and `to` says where it is.  One row can stand for several pieces of one kind on one page, with the count in the note: "the Reported by line, 161 sections".
- **Text added after the baseline** by feature work, or by an earlier phase, has no id.  The checker prints such text with its place at the base, and a row names it by `@<file>:<line>`.  A phase that moves a page gives each such unit of the page a row, as the outline's "The ledger" asks.

### 6. The checker

The checker is the folder `check/` of the epic folder.  It is run from the root of the repository, always through one entry point:

```console
$ docs/superpowers/specs/2026-11-12-docs-overhaul/check/check.sh <command> [<nn> | <page>]
```

`check.sh` does the work itself or runs a C# file of the folder with `dotnet run`: your choice.  It uses .NET and the shell alone, and it is not an xUnit test.  It exits with 0 when the command passes, 1 when it finds something, and 2 when it cannot run.  "check coverage", "check move 03" and "check rewrite 14" in the other briefs are the commands `coverage`, `move 03` and `rewrite 14`.

Three sets of text are read the same way, as units with their normalised text: the baseline (`git show 28e20e3c:<file>`), the base (the commit in the ledger's `# base` line) and the working tree.  The user documents at any of them are `README.md`, `src/SqlSource.Tool/README.md` and every `.md` under `site/` but an `AGENTS.md` or a `CLAUDE*.md`.

| Command | What it does | It prints | It exits with 1 when |
|----|----|----|----|
| `inventory` | Writes `inventory.tsv` from the baseline and `check/phases.tsv` | The count of units by document and by kind | A unit has no phase |
| `coverage` | Reads the inventory and every ledger file.  A unit is due when its `phase` is at most the highest number of a ledger file | By document: units, units with a row, units that are due and have none (each listed), units not due.  The count of rows by disposition | A ledger row is not well formed (six cells, a known disposition, a unit or range that the inventory has, the `arg` and the `note` its disposition needs, a `move-split` with its parts, or with none when its unit is a heading).  A unit that is due has no row in any ledger file |
| `move <nn>` | Reads `ledger/phase-<nn>.tsv`.  Old is every unit of the user documents at the base, new is every unit in the working tree.  See below | The four lists below | A list that must be empty is not |
| `rewrite <nn>` | For each user document that differs between the base and the working tree, compares old and new, section by section.  See below | The differences of each section, and the word counts | A fenced block changed or went, or is new in its section, and no row names it.  A section has a difference and no row of the ledger names the section or a unit of it.  A unit of the old page that is not on the new page word for word has no row |
| `origin <page>` | Reads one page in the working tree | Each unit with its line, its count of words, and the id of the baseline unit that has the same normalised text, or `new`.  Then two sums of words: one of the units that have an id, and one of the units marked `new` | Never |
| `selftest` | Runs the cases below in a scratch copy | Pass or fail for each case | A case fails |

**`move <nn>` prints four lists.**

1. **Not at its place.**  For each unit that a row marks `move`, `merge-pair`, or `restated-from` with a `to`: its normalised text must be a unit of the `to` file.  For a part, its `words` must be found inside one unit of the `to` file; the case of the part's first letter and a full stop at its end are not compared.  For `corrected`, the new words must be found the same way.  Where `to` has an anchor, the anchor must be in the file, and the unit must stand between the heading of that anchor and the next heading of the same or a higher level.  For a row with `fact`, or with the note `position`, only the file and the anchor are looked for.  This list must be empty.
2. **Left over.**  Every unit that is in the old set more often than in the new set: text that existed and is gone.  Each is printed with its id, or its `@<file>:<line>`, its text, and the row that names it.  A unit with no row, or with a `move` row that has neither `fact` nor the note `position`, makes the command fail.  The others are the no-loss reviewer's reading list.  A unit with the note `position` is printed with the unit of the `to` file that is nearest to it, so that the reader sees the words that changed.  For a `move-split` unit the checker also prints the words of the unit that no part holds; a part whose `words` is `*` is printed and not checked.
3. **Added.**  Every unit that is in the new set more often than in the old set, by file, less the units that a row of list 1 puts there.  Each file in this list must be the `to` of a `new` row, a `corrected` row, a part, a row with `fact` or a row with the note `position`.  The list is the accuracy reviewer's reading list.
4. **Anchors gone.**  Each `<a id>` of the old set that no file of the new set has.  It fails nothing: the map says which anchors retire.

A true move leaves list 1 empty, list 2 holding only the units whose words it mended, and list 3 holding only those units as they now read and its new headings and sentences.  A unit whose only change is a link's target, a link put around its words, an `<a id>` or a heading's level is in no list.

`move <nn>` can be run again after later phases.  It then also lists what those phases took away or moved again, so compare its output before and after a phase, and do not expect it clean.

**`rewrite <nn>` compares by section.**  A section is an h2 heading with all below it, down to the next h2, and is known by its `<a id>` or, in `diagnostics.md`, by its `## SQLSRCnnn` heading.  An h2 that has no `<a id>` is known by the slug of its heading.  The text before the first h2 is a section too.  A page that the phase removes is compared with nothing.  For each section that differs it prints:

- the code spans, the diagnostic ids (`SQLSRC` and three digits), the numerals outside code, and the rows of each table, as counted sets: each one whose count changed, old count and new count;
- each sentence of the old section that holds "not", "only", "never" or "unless" and has no partner in the new section.  How a sentence finds its partner is yours to write; it must err toward listing;
- each fenced block that changed or went, and each that is new in the section, which the phase's ledger must name;
- the rows of the phase's ledger that name the section, so that a reader sees each difference beside its note;
- the word count of the section and of the page, old and new, and the sum of the phase.

A difference is not a failure.  A difference that no row accounts for is, and whether a note is true is the reviewer's to say.

**Prove the checker**, with `selftest`.  It makes a scratch copy of the tree outside the repository's files, changes it, runs the commands on it with a scratch ledger, and checks what they say.  At least these cases:

1. A section moves to a new file, every link target in it changes, one `<a id>` is renamed and one heading changes level.  `move` passes, and lists 1 and 2 are empty.
2. The same, and one sentence is deleted from a paragraph.  `move` fails and names that unit.
3. The same, and one character changes inside a fenced block.  `move` fails and names the block.
4. A unit is removed.  With a `removed-status` row `move` passes and lists it; without the row it fails.
5. A unit is split in two parts that leave out one clause.  `move` passes and prints the clause.
6. A unit that is due has no row: `coverage` fails and names it.  A row names a unit that is not in the inventory: `coverage` fails.
7. A page is rewritten so that one code span goes, one numeral changes, one sentence loses "not", one fenced block changes and one new fenced block is added.  `rewrite` prints all five, and fails until a row names each block and the section.
8. `inventory`, run a second time, changes no byte of `inventory.tsv`.
9. A paragraph moves to a new file with "above" made "on this page".  With a `move` row whose note starts with `position`, `move` passes and prints the unit as left over.  Without the note it fails.
10. A `move-split` row with no part names a heading: `coverage` passes.  The same row names a paragraph: `coverage` fails.

### 7. The merge worksheet

`merges/MP-<nn>.md`, with two digits, is the worksheet of one merge pair of the map's section 5.  The phase that places the first text of a pair starts the file, each later phase that places a text adds it, and the phase that merges finishes it.  Its form:

| Part | Written by | Holds |
|----|----|----|
| `# MP-<nn>: <the heading the pair lands under>`, then five bullets | The phase that starts it | Lands on (`<page>#<anchor>`, and each `<a id>` kept above the heading); Placed in; Merged in; Source of truth; Where the map says they disagree.  All from the pair's row of the map |
| `## The texts` | Each phase that places one | An h3 for each text: its file and lines at the baseline, where it stands now, and the text word for word in a fenced block |
| `## The facts` | The phase that merges | A table, one row for each fact of either text: the fact, which text has it (first, second, both), and where it stands in the merged text or which section owns it |
| `## Conflicts` | The phase that merges | A table: what each text says, the source file and member or line that settles it, and the ruling.  Or "None found." |
| `## The merged text` | The phase that merges | Where it stands, and the text |

`MP-10` has one h2 for each of the 17 terms, each with these parts one level down.  Five phases merge terms: each writes only under its own.

### 8. Documents that this phase makes wrong

| File | Line | New words |
|----|----|----|
| `tests/SqlSource.Repository.Tests/Documentation/AGENTS.md` | 3 | "- **`UserDocsLinksTests` checks the links and anchors of `README.md`, `src/SqlSource.Tool/README.md` and every page under `site/` but an `AGENTS.md`, `SiteTocTests` that every page is in exactly one `toc.yml`, `InboundSiteReferenceTests` every reference to a page of `site/` from the rest of the repository but `docs/superpowers`, `UserDocsSamplesTests` the SQL in their fenced blocks, and `AgentsFileTests` the limits of every `AGENTS.md`.**" |
| `tests/README.md` | 13, the row of `tests/SqlSource.Repository.Tests` | After "the links, anchors and SQL samples of the user docs": ", the references to a page of `site/` from the rest of the repository" |
| `docs/superpowers/AGENTS.md` | A new bullet under "Intent" | "- `specs/2026-11-12-docs-overhaul/` also holds the instruments of that epic, its inventory, its ledger and a checker in `check/`, which the epic's phase 0 brief specifies and its last phase removes." |
| The outline | The table "Tests" | The row of `InboundSiteReferenceTests` names what the test leaves out.  "Constraints" gains the two facts about `.tsv` files |

Leave `tests/AGENTS.md` alone: its line 48 stays true, and the file is 6 bytes under its limit.  `site/AGENTS.md` does not change: its rule on a new page (line 40) and on the site (line 41) stay true.

## The ledger

`ledger/phase-00.tsv` holds its `# base` line and the header, and no row: this phase touches no unit.  The file exists so that `coverage` has a first file to read and each later phase a form to copy.

- "check coverage" exits with 0.  It prints every unit as not due, and no row.
- "check move 00" exits with 0 and prints four empty lists.
- "check rewrite" does not apply.

## Done when

- [ ] `git diff --name-only <base> -- site` prints `site/docfx.json` alone.
- [ ] `tools/build-site.sh` passes, and `find artifacts/site -name '*.html' | wc -l` prints 14, as before the change.
- [ ] `dotnet test --project tests/SqlSource.Repository.Tests` passes.
- [ ] `git grep -n Manual_EntryPage_ListsEveryPage -- tests` prints nothing.
- [ ] A dry run, not committed: add `site/sub/probe.md` (front matter, a tab group, a link to `../diagnostics.md#sqlsrc001`), `site/sub/toc.yml` that lists it, and an entry `href: sub/` in `site/toc.yml`.  The tests and `tools/build-site.sh` pass.  Then each of these fails one test, by name: the entry of `probe.md` taken out of its `toc.yml` (`SiteTocTests`); a comment `// site/sub/probe.md#nope` in a `.cs` file (`InboundSiteReferenceTests`); `[x](../diagnostics.md#sqlsrc999)` in the page (`UserDocsLinksTests`).  The pull request's description says what each run printed.
- [ ] A second dry run, not committed: write `site/nope.md#x` into a new file `.superpowers/probe.md` and into `CLAUDE.local.md`, both of which git ignores.  `InboundSiteReferenceTests` still passes.  Write the same words into a new file `docs/probe.md`: it fails and names that file.  Then take all three out.
- [ ] `check.sh inventory`, then `git status --porcelain docs/superpowers/specs/2026-11-12-docs-overhaul` prints nothing.  The first line of `inventory.tsv` names the baseline.
- [ ] `check.sh coverage` and `check.sh move 00` exit with 0.  `check.sh selftest` prints a pass for each of its ten cases.
- [ ] `check.sh origin site/settings.md` gives every unit an id, and none is `new`, on a tree where the page equals the baseline.
- [ ] After any run of the checker, `git status --porcelain` shows no file that the run left behind.
- [ ] `wc -c` of each `AGENTS.md` this phase changed is under its limit, and `AgentsFileTests` passes.
- [ ] `./pre-commit-validation.sh --full` passes on the tree as pushed, after a clean `branch-review:review`.

## For the reviewers

No text of a user document changes, so both content reviewers read the instruments.

**The no-loss reviewer** checks that the inventory is the baseline:

- The counts by document and kind against the pages: `git show 28e20e3c:site/settings.md | grep -c '^#'` and the like.
- Thirty units chosen at random: the id's line at the baseline holds the text of `start`, and `last` is the closing fence of a block.
- The first and last unit of each document, a unit after a fenced block inside a list (29 fences are indented), the one quote (`site/parameters-and-tokens.md`), and a header row.
- The `phase` of every range of the table in step 4, against section 10 of the map.
- One sabotage of their own, beyond the ten cases: a paragraph moved with one numeral changed.

**The accuracy reviewer** checks that each test fails for the mistake it names, by making the mistake: a page in two `toc.yml` files, a tab heading that carries an `<a id>` above it and is linked, a reference in `docs/architecture` to an anchor that is not there, a site URL in `SqlSource.targets` with a wrong anchor.  The reviewer also reads the comment of each new or changed test: it says what fails the test.

## Traps

- **`index.md` by name.**  Excluding every file named `index.md` hides the three `index.md` pages of `reference/databases/` later.  Exclude `site/index.md` by its path.
- **A walk of the tree** finds `artifacts/site`, `bin`, `obj` and the worktrees under `.claude/`, each full of references.  So does `.superpowers/sdd/`, where the briefs of a plan's tasks quote the paths of pages, and `CLAUDE.local.md`; git ignores both.  Skip them as "What it scans" says, and prove it: the test's count of files with a reference is 78 at the baseline, and a count far above that means a folder that the walk must skip.
- **A C# file run with `dotnet run`** is under the root `Directory.Build.props` and `Directory.Packages.props`: analyzers at `latest-all`, warnings as errors, lock files.  Prove on the first day that it builds, and that a run leaves no file in the tree.  CSharpier formats every tracked `.cs` file, and shellcheck and shfmt every tracked `.sh` file, the checker's own included.
- **A cell that is empty** leaves a tab at the end of a line, which editorconfig-checker refuses.  Write `-`.
- **The baseline in a shallow clone.**  `git show 28e20e3c:<file>` fails where the commit was not fetched.  The checker then exits with 2 and says to fetch `main`; it does not treat the page as empty.
- **Multisets, not sets.**  `| Id | Title |` stands 29 times in `diagnostics.md`, and a closing bullet can repeat on two pages.  A unit that stood twice and now stands once is left over.
- **A link reduced to its text inside code.**  `` `[a](b.md)` `` in a code span is not a link.  Apply the same rule to old and new, and a wrong reading cancels out; but do not reduce anything inside a fenced block.
- **The test must not be weakened to pass.**  If the search of step 2 finds a string that names nothing and is not in the four rows of the table, it is a broken reference of today: mend the reference in this pull request, and do not add it to the list.

## Left to the agent

- The names of the test methods, and how each test is written.
- Whether the checker is a shell script or a C# file, how its output is laid out, and how `selftest` makes its scratch copy.
- How a sentence of an old section finds its partner in the new one.
- Whether `UserDocs.Read` keeps the tab headings it finds, for the test of phase 3 that reads tab groups.
- Whether the exceptions of `InboundSiteReferenceTests` are whole files or single strings, where the table does not say.

## Calls made in this brief

- **A unit is a line.**  The outline leaves the grain to the builder.  No paragraph is wrapped in these documents, so a line is the paragraph, the bullet and the table row, and an id is a file and a line.
- **A link is reduced to its text**, brackets too.  The outline says "strips link targets".  With the brackets kept, every sentence that gains or loses a link in a move would be left over.
- **A heading is compared without its `#` marks.**  The moves change the level of many headings: 20 in phase 4 alone.
- **The inventory has a `phase` column**, so that "for the phases done so far" is a rule the checker can apply.
- **A ledger row can name a range**, and text that is not in the inventory by `@<file>:<line>` at the phase's base.  The map is written in ranges, and feature work adds text that has no id.
- **`move` in a rewrite phase** means that the unit's facts stay on its page in new words, and `move` with `fact` says the same of a unit whose fact a phase writes new on another page.  The eight dispositions have no word for either, the map uses `move` so for the preamble of `diagnostics.md`, and a ninth disposition would change the outline.
- **A unit whose words a move mended is a `move` row with the note `position`**, and not a `corrected` row.  The outline asks a move to mend such words and to list each with that note.  The checker cannot tell a mended word from a lost one, so it prints the unit and a reviewer reads it.
- **A heading that goes is a `move-split` with no part.**  The map marks such a row "heading only", and the eight dispositions have no word for a unit that goes and states no fact.
- **The checker has one entry point, `check.sh`**, and three commands beyond the three that the briefs name: `inventory`, `origin` and `selftest`.  `origin` answers "which text on this page is old" for a rewrite, which the rewrite guide asks of the ledgers.
- **`rewrite` works by section**, as the diagnostics guide needs for four phases in one page, and fails only for a fenced block or a difference with no row.
- **The worksheet is started by the phase that places the first text**, as the briefs of phases 5 to 8 assume, and not by this phase.
- **`.tsv` becomes a known kind for the epic folder alone, and the epic's `.tsv` files are free of the 120 columns.**  Without the first, every commit of 26 phases runs the database tests.  Without the second, a ledger row with a note fails the lint.
- **`InboundSiteReferenceTests` leaves out the user documents**, which `UserDocsLinksTests` already follows, and walks the tree, since a test of this project may not start `git`.  The walk skips the folders of `AgentsFileTests`, `artifacts`, every other folder whose name starts with `.` but `.github`, and `CLAUDE.local.md`, so that it reads no file that git ignores.
- **The help-link prefix `diagnostics.html#sqlsrc` is a named exception.**  The planning counted two test files that must be left out; the search found these two more.
- **One bullet in `docs/superpowers/AGENTS.md`** says that the epic folder holds code, since that file says the folder holds designs.
