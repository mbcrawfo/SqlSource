# Docs overhaul, phase 01: status-language

[The outline](2026-11-12-docs-overhaul-epic-design.md) and [the content map](2026-11-12-docs-overhaul/content-map.md) are the authority.  [The phase 0 brief](2026-11-12-docs-overhaul-phase-00-guard-rails-design.md) says how the ledger is written and how the checker is run.  This brief adds what is specific to phase 1.

Done by: an Opus agent.  Needs: phase 0.  Runs alone.

## Goal

The user documents stop speaking of a library in development.  "What works today", the Status column of the settings table and the status sentences go, and so do the rules of three `AGENTS.md` files that produced them.  `StatusLanguageTests` keeps the phrases out.  No fact goes with them: the one pointer that only the table held moves to its section.

## Where things stand

- **Phase 0 is in.**  The tests accept folders, `InboundSiteReferenceTests` follows the references from outside, and the epic folder holds `inventory.tsv`, `ledger/phase-00.tsv` and `check/`.
- **No page has changed.**  `site/` is twelve flat pages, as at the baseline, but for what feature work added.  Run `git diff 28e20e3c -- README.md src/SqlSource.Tool/README.md site` first: the line numbers below are the baseline's, and a line can have moved.
- **The status language** is in six places, the `removed-status` rows of the map: `README.md` 7 and 13, `src/SqlSource.Tool/README.md` 7, `site/README.md` 59-105, `site/settings.md` 36 and 38-58, `site/tool.md` 20.
- **The rules that made it** are in root `AGENTS.md` (lines 10, 12, 18, 22), `site/AGENTS.md` (lines 6, 14, 25-27, 29, 46-55) and `docs/architecture/AGENTS.md` (line 71).
- **Sizes**, with `wc -c`: `site/AGENTS.md` 7,990 of 8,192 bytes; root `AGENTS.md` 14,925 of 16,384; `docs/architecture/AGENTS.md` 7,128 of 8,192.
- **Four strings of a test** name the anchor that goes: `UserDocsReadingTests.cs` 178, 180, 192 and 196.

## In scope / Not in scope

In scope:

- `StatusLanguageTests`, new.
- Every `removed-status` row of the map, the neutral sentence beside `--prerelease`, and the pointer of `site/README.md` 101.
- The lines of the three `AGENTS.md` files, of `UserDocsReadingTests.cs`, of `tests/SqlSource.Repository.Tests/Documentation/AGENTS.md`, of `tests/README.md` and of the outline that this phase makes wrong.

Not in scope:

- Any other sentence of a page.  A sentence that looks like status and is a fact stays: [the table](#3-what-stays-because-it-is-a-fact).
- Correction C-01.  Its three sentences are false today, and phases 5 and 7 mend them with the text they carry.  This phase only lets the Status cell of `site/settings.md` 41 go with its column.
- The rest of "When a phase lands" in `site/AGENTS.md`, which phase 3 replaces.
- `site/README.md` 3, the sentence of C-06: phase 10.

## The work

### 1. `StatusLanguageTests`, first

Write the test before you change a page, and see it fail.  It is a new class in `tests/SqlSource.Repository.Tests/Documentation/`.

- It reads every line of every file of `UserDocs.Files`, fenced blocks and link targets included, and looks for each phrase without regard to case.
- It reports each hit as `file:line: phrase`.
- A second test gives the search lines of its own: each phrase in two spellings of case is found, and "the rows still to come", "PostgreSQL 14 or later" and "has not been checked" are not.
- Its comment says what fails it, and that a phrase is added only when a real sentence of status got past it.

The phrases, proved against the baseline:

| Phrase | Lines that hold it at the baseline |
|----|----|
| `Coming in a later release` | None.  The rule that orders the callout goes in this phase, and the test keeps the callout out afterwards |
| `works today` | 21: `README.md` 7; `site/README.md` 61; `site/settings.md` 40 to 58 |
| `work today` | 1: `site/settings.md` 36, "Settings that work today" |
| `no effect yet` | None.  The same as the first row |
| `early development` | 2: `README.md` 7; `site/README.md` 63 |

So the test fails on 23 lines before step 2 and on none after it.  Put both runs in the pull request's description.

### 2. What goes

| Where | What goes | What stands there afterwards |
|----|----|----|
| `README.md` 7 | The whole paragraph that starts `**Status:**`, and the blank line under it | Line 5 is followed by the paragraph that starts `**Databases:**` |
| `README.md` 13 | Sentence 1: "Until the first release the packages are previews, so the commands include `--prerelease`." | "The packages are published as previews, so the commands include `--prerelease`."  Sentence 2 stays |
| `src/SqlSource.Tool/README.md` 7 | Sentence 2: "Until the first release the package is a preview, so the command includes `--prerelease`:" | "The packages are published as previews, so the commands include `--prerelease`:" |
| `site/tool.md` 20 | Sentence 2, the same words | The same sentence, with its colon |
| `site/README.md` 59-105 | The line `<a id="what-works-today"></a>`, the heading, the introduction (63), and the table: its header, its rule and its 39 rows (67-105) | The table of "Find what you need" is followed by `## The pages` |
| `site/README.md` 101 | The row "Custom type mapping" goes with its table, but for its pointer | Step 4 |
| `site/settings.md` 36 | The heading `### Settings that work today` | The table stands under the paragraph of line 34, in `## All settings` |
| `site/settings.md` 38-58 | The column "Status": the last cell of the header, of the rule line and of each of the 19 rows | Three columns: Setting, Values, Written as |

The neutral sentence is the same words in all three places: "The packages are published as previews, so the commands include `--prerelease`".  It ends with a full stop in `README.md` and with a colon before the two code blocks.  The flag stays on every command, and the badge stays `nuget/vpre`.

### 3. What stays, because it is a fact

| Where | The words | Why they stay |
|----|----|----|
| `README.md` 9 and 29; `site/sqlserver.md` 28 and 29 | "accepted and not tested", "is not tested with" | A fact about support.  The outline keeps it by name |
| `site/tool.md` 225, 313, 330, 635 and 636; `site/recipes.md` 241, 248 and 252 | "have not been checked", "Checked in Rider 2026.2 on macOS" | The same |
| `site/postgresql.md` 492; `site/settings.md` 386 and 390; `site/sqlserver.md` 483 | "the rows still to come" | It is about the rows of a result.  The test does not hold this phrase, for that reason |
| `site/diagnostics.md` 611 and 1136 | "of earlier previews" | It says which words an earlier preview took, for a user who updates |
| `site/diagnostics.md` 1900 and 2581, in `SQLSRC216` and `SQLSRC243` | "No dialect of this release gets this error ... It is kept for a database ..." | It says that the reader cannot get the error, and why the id exists.  Phases 22 and 23 put it in the present tense |
| `site/diagnostics.md` 313 | "This version of the tool describes the `postgres` and `mssql` dialects" | Phase 2 removes it with the preamble |
| `site/diagnostics.md` 2662 and 3148; `site/postgresql.md` 173; `site/sqlserver.md` 167 | "this version of SqlSource knows ... up to version 10" | A limit of the version the reader has |
| `site/README.md` 3 | "If you have not used SqlSource yet, start with the repository README" | C-06, which phase 10 makes |
| `site/settings.md` 34, 60, and the columns "Values" and "Written as" | All of it | Phase 7 moves them, and makes C-02 at line 34 |
| `site/settings.md` 153; `site/tool.md` 235 | "Only the tool reads this setting" | False today (C-01).  Phases 7 and 5 mend them |

**The Status cell of the Database row**, `site/settings.md` 41, says "Works today, for the tool alone".  The second half is a fact and not status, and the fact is false: the build reads the setting too (`MethodPlanner.DatabaseOf`, in `src/SqlSource/Generation/Methods/MethodPlanner.cs`).  So the cell goes with its column, nothing of it is carried, and its ledger row is `corrected` with `C-01`.  Do not write the true statement here: line 153 of the page still says the old thing, and phase 7 mends both in one place.

### 4. The pointer of `site/README.md` 101

The row's third cell ends "; for a PostgreSQL type that cannot be sent as a parameter, see [SQLSRC232](diagnostics.md#sqlsrc232)".  Nothing else leads from type overrides to `SQLSRC232`: `site/settings.md` never names it.

- Place it in `site/settings.md`, in the section `setting-type-overrides` (397-467), as a paragraph of its own after the paragraph that starts "To give one column, or one parameter".
- Its words: "For a PostgreSQL type that cannot be sent as a parameter, see [SQLSRC232](diagnostics.md#sqlsrc232)."  Nothing is added to them.
- Phase 7 moves it on with its section.

### 5. The test strings

`UserDocsReadingTests.cs` uses the anchor as data, not as a link.  In lines 178 and 180 (`Resolve_SiteUrl_IsThePageOfSite`) and in lines 192 and 196 (`SiteLinkProblem_NotAPageOfTheSite_IsReported`), `what-works-today` becomes `find-what-you-need`, the heading of `site/README.md` 5.  The tests still pass: `Resolve` does not ask whether the anchor is there.  Phase 3 changes these lines again, with the root.

### 6. The rules

Each line is replaced by the words given, or goes.  Measure each file again when you are done.

| File | Line | Becomes |
|----|----|----|
| Root `AGENTS.md` | 10 | "     - They state what SqlSource does, as fact.  No page says that a feature is coming, works "today" or is in development.  A sentence that says something is not tested, or was checked on one platform, is a fact about support and stays." |
| Root `AGENTS.md` | 12 | The words "its status, " go from the list of what `README.md` holds |
| Root `AGENTS.md` | 18 | The bullet goes: "A feature still to come is this callout, word for word ..." |
| Root `AGENTS.md` | 22 | The clause `, and its "What works today" table is the one record of what is delivered` goes.  The sentence ends "routes a reader by need." |
| `site/AGENTS.md` | 6 | "- **This folder**: the entry page, `README.md`, holds "Find what you need" and "The pages", which says what each page holds." |
| `site/AGENTS.md` | 14 | The first sentence goes: "The same holds for what is coming."  The bullet starts "A type map is" |
| `site/AGENTS.md` | 25-27 | One bullet in place of three: "- **State what SqlSource does, as fact.**  No page says that a feature is coming, works "today" or is in development; `StatusLanguageTests` holds the phrases.  A sentence that says something is not tested, or was checked on one platform, is a fact about support and stays." |
| `site/AGENTS.md` | 29 | The second sentence goes.  The bullet is "- A setting's row under `#all-settings` names every place it can be written." |
| `site/AGENTS.md` | 35 | The bullet gains a sentence: "The sentence beside it is "The packages are published as previews, so the commands include `--prerelease`", and says nothing more." |
| `site/AGENTS.md` | 46 | The words ", its Status cells in `settings.md` included" go |
| `site/AGENTS.md` | 48-54 | The bullets "Callouts" and "Sentences that say it is missing or still to come" go, with the fenced `git grep` between them and the blank lines |
| `site/AGENTS.md` | 55 | The words "the introduction's status line, " go |
| `docs/architecture/AGENTS.md` | 71 | "- A page holds no status table.  What SqlSource does is stated in `site/`, as fact." |

Root line 19, on `--prerelease` and `nuget/vpre`, stays as it is.  So does step 3 of `docs/publishing.md` (line 28), which already takes "the sentences that explain them" out at the first stable release.  `docs/architecture/AGENTS.md` 65, "Never describe the future as present", is a rule for the architecture pages and stays.

Measured on these words: `site/AGENTS.md` comes to about 6,870 bytes, and root `AGENTS.md` to about 14,650.  The longest new bullet has 46 words.

### 7. The other files

| File | Change |
|----|----|
| `tests/SqlSource.Repository.Tests/Documentation/AGENTS.md` 3 | The list of tests gains "`StatusLanguageTests` that no user document holds a phrase of status," |
| `tests/README.md` 13 | The row of `tests/SqlSource.Repository.Tests` gains ", the phrases of status that the user docs must not hold" |
| The outline, the table "Tests" | The row of `StatusLanguageTests` lists five phrases: "work today" is the fifth |

No reference from outside the user documents names `what-works-today` after step 6.  `git grep -n what-works-today -- . ':!docs/superpowers'` must print nothing, and `InboundSiteReferenceTests` fails if root `AGENTS.md` 18 is left.

### 8. Search again

The map's rows came from a search for known phrases.  Search the user documents once more before you finish, and read each hit:

```console
$ git grep -n -i -E "today|coming|arrive|later release|first release|this release|not yet|for now|currently|planned|future|soon|(^|[^[:alnum:]_])(yet|will)([^[:alnum:]_]|$)" -- README.md src/SqlSource.Tool/README.md site ':!site/AGENTS.md'
```

At the baseline it finds, beyond the map's rows, only the lines of the table in step 3 and uses that are not about SqlSource's state, such as "a maintainer will say how to send it", "as soon as it is written", "a null arrives" and "a folder that does not exist yet".  A hit that is neither is new since the baseline: take it out if it is status, and give it a row of the ledger.

## The ledger

`ledger/phase-01.tsv` holds these rows.  A unit is named by its file and its line at the baseline.

| Unit | Disposition | To | Note |
|----|----|----|----|
| `README.md:7` | `removed-status` | None | What it says is generated is stated in `site/generator.md` 1-32, and describing in `site/tool.md` 1-14 |
| `README.md:13` | `move-split` | | Part 1, sentence 1: `removed-status`.  Part 2, sentence 2: `move`, to `README.md` |
| `src/SqlSource.Tool/README.md:7` | `move-split` | | Part 1, sentence 1: `move`, stays.  Part 2, sentence 2: `removed-status` |
| `site/tool.md:20` | `move-split` | | The same two parts |
| None, three rows | `new` | `README.md`; `src/SqlSource.Tool/README.md`; `site/tool.md#install-the-tool` | The neutral sentence |
| `site/README.md:61`, `:63`, `:65-100`, `:102-105` | `removed-status` | None | Each row's facts are stated in the section that its third cell links.  The anchor `what-works-today` retires |
| `site/README.md:101` | `move-split` | | Part 1, the row but its pointer: `removed-status`, words `*`.  Part 2, the pointer: `move`, to `site/settings.md#setting-type-overrides` |
| `site/settings.md:36` | `removed-status` | None | The heading |
| `site/settings.md:38-58` | `move-split` | | Part 1, the cells Setting, Values and Written as: `move`, to `site/settings.md#all-settings`, words `*` |
| `site/settings.md:38-40/2`, `:42-58/2` | `removed-status` | None | The Status cell |
| `site/settings.md:41/2` | `corrected`, `C-01` | None | "for the tool alone" is a fact, and false.  Nothing replaces it here; phase 7 states the truth at line 153 |

The checker must show:

- **"check coverage"** passes for phases 0 and 1.  The units that are due are `README.md` 7 and 13, the tool readme's 7, `site/README.md` 61-105, `site/tool.md` 20, and `site/settings.md` 36 and 38-58.
- **"check move 01"**, list 1, is empty: the pointer's words are found under `setting-type-overrides`, and each sentence that stays is found in its file.
- **List 2, left over**, holds 67 units, each with its row and none without: 2 of `README.md`, 1 of the tool's readme, 1 of `site/tool.md`, 42 of `site/README.md` (the heading, the introduction, the header row and 39 rows), and 21 of `site/settings.md` (the heading, the header row and 19 rows).  A unit beyond these is a change you did not mean.
- **List 3, added**, holds 24 units: one changed paragraph in each of `README.md`, the tool's readme and `site/tool.md`, and in `site/settings.md` the 20 rows without their last cell and the one new paragraph.
- **List 4** holds one anchor, `what-works-today`.
- **"check rewrite"** does not apply.

## Done when

- [ ] `StatusLanguageTests` failed on 23 lines before step 2 and passes now.  Both runs are in the pull request's description.
- [ ] `git grep -n -i -E "works? today|early development|no effect yet|coming in a later release" -- README.md src/SqlSource.Tool/README.md site ':!site/AGENTS.md'` prints nothing.
- [ ] `git grep -n -i "until the first release" -- README.md src/SqlSource.Tool/README.md site` prints nothing, and `git grep -c "The packages are published as previews, so the commands include" -- README.md src/SqlSource.Tool/README.md site/tool.md` prints 1 for each of the three files.
- [ ] `git grep -n -- "--prerelease" README.md src/SqlSource.Tool/README.md site/tool.md` prints as many command lines as at the baseline.
- [ ] `git grep -n what-works-today -- . ':!docs/superpowers'` prints nothing.
- [ ] `git grep -n SQLSRC232 -- site/settings.md` prints one line.
- [ ] `git diff --stat <base> -- site` names `site/README.md`, `site/settings.md`, `site/tool.md` and `site/AGENTS.md`, and no other file of `site/`.
- [ ] In `git diff --word-diff <base> -- site/settings.md`, each row of the table differs from the old one only by its last cell: the words removed are "Works today", and in the Database row "Works today, for the tool alone".
- [ ] `wc -c site/AGENTS.md AGENTS.md docs/architecture/AGENTS.md` is under 8,192, 16,384 and 8,192, and `AgentsFileTests` passes.
- [ ] "check coverage" and "check move 01" show what [The ledger](#the-ledger) says.
- [ ] `tools/build-site.sh` passes, and so does every test of `tests/SqlSource.Repository.Tests`.
- [ ] The content reviews, a clean `branch-review:review` and `./pre-commit-validation.sh --full` on the last commit, as the outline's "Reviews" says.

## For the reviewers

**The no-loss reviewer** reads the 39 rows of the table, at `git show 28e20e3c:site/README.md`, lines 67 to 105.  For each row, every fact of its first cell is stated in the section that its third cell links.  The map says so, and this review confirms it.  The rows that say most, and where to look:

| Row (line) | The fact to find | In |
|----|----|----|
| Collection methods (94) | "1,000 items to a round trip", "in a transaction of its own", `CollectionItemException` | `site/generator.md` `#collection-methods`, near line 900 |
| Nullability by `!`, `?` or `-- column:` (90) | The marker "leaves the column's name alone for Dapper" | `site/generator.md` `#column-marker` |
| Array parameters (85) | "accept any `IEnumerable<T>`" | `site/postgresql.md` `#array-parameters` |
| Repositories (92) | The four connection sources, "found by its name or type or marked `[SqlSourceConnection]`", the interface | `site/generator.md` `#repository-methods`, `#repository-connection` |
| Synchronous methods (95) | "in place of its asynchronous ones, or beside them" | `site/generator.md` `#synchronous-methods` |
| Short names (103) | "wherever SqlSource takes a C# type" | `site/settings.md` `#setting-type-overrides`, near line 456 |
| Build integration (104) | "when a connection is configured" | `site/tool.md` `#running-in-the-build` |
| Bundles (105) | `--no-obfuscate`, `--scrub`, `replay` | `site/tool.md` `#diagnose`, `#replay` |

The reviewer also reads `README.md` 7 against `README.md` 5 and 9 and `site/generator.md` 1-32.  Line 7 is where the README says "which SqlSource calls describing the query".  Check that the README still reads without it: line 5 uses "described" and line 9 "describing".  If a reader of the README alone now meets the word with no meaning, say so; the fix is a clause in line 9, as a `new` row.

**The accuracy reviewer** checks three things:

- The neutral sentence says only what is true of both packages: `docs/publishing.md` and the version suffix in `Directory.Build.props`.
- The pointer leads to the section of `SQLSRC232`, and that section is about a PostgreSQL type that cannot be sent as a parameter.
- Each new `AGENTS.md` rule is true of the tree as this phase leaves it, and no rule of the three files still names the table, the callout or a Status cell.

## Traps

- **A `|` inside a cell.**  No row of the table of `site/settings.md` has one at the baseline.  Check again before you cut the last cell with a script: a cell with code that holds `|` would be cut in the wrong place.
- **The rule line of the table.**  `|----|----|----|----|` must lose a cell too, or the table has four columns in its rule and three in its rows.
- **"Works today" in the Database row is not the whole cell.**  Take the whole cell out, "Works today, for the tool alone", not the two words.
- **`site/AGENTS.md` names what this phase removes in more lines than the map lists.**  The map names line 27.  Lines 6, 14, 25, 26, 29, 46, 48, 49 and 55 speak of the table, the callout or a status too, and `docs/architecture/AGENTS.md` 71 names the table.
- **The link test is not the status test.**  A link to `#what-works-today` that is left fails `UserDocsLinksTests`, and docfx.  A phrase that is left fails `StatusLanguageTests`.  Run both.
- **Do not reword while you are there.**  `README.md` 9 says "follow the five steps below" and stays word for word.  A reworded sentence shows in list 2 of "check move 01".
- **Phase 2 and phase 3 edit `site/AGENTS.md` next.**  Change only the lines of the table, so that their briefs find the file they expect.

## Left to the agent

- The names of the test's methods, and how it reads the lines.
- Whether the cells are cut by hand or by a script.  Either way the word diff of the last check is the proof.
- The exact place of the pointer inside its section, if feature work changed the paragraphs around it.

## Calls made in this brief

- **A fifth phrase, "work today".**  The outline lists four.  None of them finds the heading "Settings that work today", and "works today" does not hold "work today".
- **The neutral sentence is the same words in three places**, also where one package and one command stand, as the map's section 7 says of the tool's readme.  Only its last mark differs.
- **The pointer is a paragraph of its own**, with a capital letter and a full stop.  The map says where it goes and not how.
- **The Status cell of the Database row is a `corrected` row with no new words.**  The map marks it `corrected` and gives its truth to phase 7.  Stating the truth here would put two statements that disagree on one page until phase 7.
- **`SQLSRC216` and `SQLSRC243` stay as they are.**  "No dialect of this release gets this error" is a fact that the reader needs, and the diagnostics guide gives both sections a shape of their own.  Their words are phases 22 and 23's.
- **"of earlier previews" stays** in `SQLSRC019` and `SQLSRC109`.  It is history that helps a user who updates, not a statement that the library is unfinished.
- **`docs/architecture/AGENTS.md` 71 changes here**, not in phase 3.  The map lists it among the references to retarget, but its rule is false from the commit that removes the table.
- **The parts of "When a phase lands" that order status language go now**, and the rest of that section waits for phase 3, which replaces it.
- **The test reads fenced blocks and link targets too.**  A phrase of status in a sample or in a link's text is still on the page.
