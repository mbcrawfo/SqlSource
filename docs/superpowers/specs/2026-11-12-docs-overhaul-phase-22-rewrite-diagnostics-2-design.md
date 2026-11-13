# Docs overhaul, phase 22: rewrite-diagnostics-2

Phase 22 of the [docs overhaul](2026-11-12-docs-overhaul-epic-design.md).  Read the outline, the [content map](2026-11-12-docs-overhaul/content-map.md) (its entry for `diagnostics.md` in section 3, and section 10), [`rewrite-guide.md`](2026-11-12-docs-overhaul/rewrite-guide.md) and [`diagnostics-guide.md`](2026-11-12-docs-overhaul/diagnostics-guide.md) before this brief.  This brief holds only what is special to its range.

- Done by: an Opus agent.
- Needs: phase 11.
- Runs beside: phases 12 to 24.

## Goal

The sections `SQLSRC122` to `SQLSRC225` of `site/diagnostics.md` are in plain language and in the one shape of the diagnostics guide.  Every fact of today's sections is still there.  No line outside the range changes, and no other file of `site/` changes.

## The range

- **Ids:** `SQLSRC122` to `SQLSRC225`.  The range starts at the line `## SQLSRC122` and ends on the line before `## SQLSRC226`.  There is no id from `128` to `199`.
- **Baseline lines:** 1458 to 2186 of `git show 28e20e3c:site/diagnostics.md`.  The units of the inventory in that range are yours.
- **Size:** 32 sections and 11,175 words at the baseline, counted from each heading to the next.  It has the fewest sections and the longest ones.  Phase 2 added a line or two to each section, so count again when you start, with the command of the diagnostics guide.
- **Areas**, by their names on `reference/errors.md#errors-by-area`:

| Area | Ids | Sections |
|----|----|----|
| `.sql` files | 122 to 127, the last 6 of the area | 6 |
| The tool | 200 to 223 | 24 |
| Describing a query | 224 and 225, the first 2 of the area | 2 |

- **Who reports them:** the build alone reports `SQLSRC122` to `125` and `SQLSRC127`.  The build and the tool report `SQLSRC126`.  The tool reports `SQLSRC200` to `225`, and the build reports `SQLSRC209` and `SQLSRC210` too, at the same places.
- **"Database" lines:** `SQLSRC224` and `SQLSRC225` have one.  Both sections cover PostgreSQL and SQL Server.

## Specific to this range

**The longest and hardest sections:**

| Id | Words | What makes it hard |
|----|----|----|
| `SQLSRC225` | 1,576 | The longest section of the page, and it has no `See:` line.  A PostgreSQL half with three `console` blocks, three bullets and four more causes, each in a paragraph of its own.  A SQL Server half with two `console` blocks and eight bullets.  Ten numbers of SQL Server errors, from `Msg 102` to `Msg 11529`, and each is a fact |
| `SQLSRC224` | 1,498 | Two tables of `server:` lines, of 18 and 12 rows.  The first column of each is text that the tool prints |
| `SQLSRC123` | 937 | Four reasons, with different costs: "the first and third" cost the file its members, and "the second and fourth" leave out one method.  Three scenes follow, each with a `console` block.  Four fenced blocks in all |
| `SQLSRC218` | 725 | One error for three kinds of file: a sidecar, a log and a bundle.  Nine bullets in three lists.  Two `See:` links |
| `SQLSRC219` | 713 | Two tables, of 8 and 3 rows.  The exit code 2, and when it is 1.  Four notes with bold openings |
| `SQLSRC213` | 631 | Two paragraphs hold nearly all of it: 160 words on where a connection comes from and which source wins, and 422 words on what the `help:` line says of the `.env` file |
| `SQLSRC122`, `125`, `127` | 327, 325, 324 | Placement and lists.  Each has two or three fenced blocks, and a cost that depends on the output |

**Tables:** `SQLSRC219` has two and `SQLSRC224` has two.  No other section of the range has one.  By the diagnostics guide, no cell of the four tables changes.  Rewrite the sentences around them.

**Several causes, or two databases:**

- `SQLSRC224`, `SQLSRC225`: PostgreSQL, then SQL Server.  Keep that order.
- `SQLSRC123`: four reasons, in the order that the text numbers them.
- `SQLSRC218`: a sidecar, then a log, then a bundle.
- `SQLSRC126`: each wrong use of a list is a bullet with a bold opening already.

**Lists of outputs:** nine lines of the range name an output before `repository`, in `SQLSRC122`, `123`, `125`, `127`, `209` and `210`: "`methods` or `repository`", "`models`, `methods` or `repository`", "`sql` or `models`", "`sql`, `models` or `repository`".  Put `repository` first in each, in the order of the diagnostics guide.  `SQLSRC122` treats the outputs in two groups, `sql` or `models` and then `repository`: change the order inside a list, and keep each output with its own rule.  A `console` block keeps what the build or the tool prints.

**Samples:** 47 fenced blocks: 37 `console`, 2 `csharp`, 1 `jsonc`, 4 `sql`, 2 `sql error` and 1 `sql mssql`.  `SQLSRC203` has none.  Many `console` blocks open with the command, `$ dotnet sqlsource describe`.  This brief lists no block to change.

**`**Fix:**` exists already** in `SQLSRC218`, `219`, `224` and `225`, as a sentence on the line of the label.  Give each the form of the guide.

**`See:` lines:**

- Four sections have none.  Give them these, with the link text of each heading as it stands on its page:

| Id | `See:` |
|----|----|
| `SQLSRC221` | `reference/sidecars.md#sidecar-files` |
| `SQLSRC222` | `reference/cli.md#arguments` |
| `SQLSRC223` | `reference/cli.md#arguments` |
| `SQLSRC225` | `reference/databases/postgresql/index.md#what-describing-does`, then `reference/databases/sqlserver/index.md#what-can-be-described` |

- `SQLSRC208`: the map changed its `See:` line.  It linked the known issues of `generator.md`, and phase 6 pointed it at `reference/attributes.md#limit-source-written-as-text`.  A limit is the right owner here.  Do not point it back at a page.

**Warnings:** none.  Every diagnostic of the range is an error that cannot be configured.  No section says "warning" of itself.

**Sources of the descriptors and the texts:**

- `src/SqlSource/Diagnostics/SqlDiagnostics.cs` holds `SQLSRC122` to `127`.  `src/SqlSource/Diagnostics/ToolDiagnostics.cs` holds `SQLSRC200` to `225`.
- `src/SqlSource.Tool/Reporting/Reporter.cs` and `ToolDiagnostic.cs` print an error of the tool: the first line, then the lines `query:`, `step:`, `server:` and `help:`.
- The first columns of the tables of `SQLSRC224`: `src/SqlSource.Tool/Describing/Postgres/ConnectionErrors.cs` and `src/SqlSource.Tool/Describing/SqlServer/ConnectionErrors.cs`.
- The lines of `SQLSRC225`: `src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs` and `src/SqlSource.Tool/Describing/SqlServer/SqlServerErrors.cs`.
- The messages of `SQLSRC219`: `src/SqlSource.Tool/Describing/SidecarCheck.cs`.  The reason for a log in `SQLSRC218`: `src/SqlSource.Tool/Logging/RunLogFile.cs`.
- Placement, `SQLSRC122` to `125`: `src/SqlSource/Generation/Placement/PlacementChecks.cs` and `tests/SqlSource.Tests/Generator/PlacementDiagnosticsTests.cs`.
- The tool's errors by id: `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`, and the tests under `tests/SqlSource.Tool.Tests/`.
- For any other quoted text, `git grep -F "<the text>" -- src` finds its file.

## Done when

- Each of the 32 sections has the one shape.  Each ends with a `See:` line, and each but `SQLSRC216` has its fix: the two counts of the diagnostics guide, of headings and of `See:` lines over the range, both print 32.
- The check of the diagnostics guide, with `## SQLSRC122` and `## SQLSRC226`, prints nothing: no line outside the range changed.
- `git diff --stat` against the merge base names `site/diagnostics.md`, the phase's ledger file, and the outline if the plan changed.  It names no other file.
- "check rewrite 22" exits with 0 on the last commit.  Every section of the range has a row in `ledger/phase-22.tsv`, and each difference that the command prints has a true note.  "check coverage" still passes.
- The four new `See:` lines are `new` rows of the ledger.
- The four tables have their rows: 8 and 3 in `SQLSRC219`, 18 and 12 in `SQLSRC224`.
- The tests that read this page pass: `DiagnosticsDocumentTests`, `UserDocsSamplesTests`, `UserDocsLinksTests`, `StatusLanguageTests`, `SiteStructureTests` and `InboundSiteReferenceTests`.  `dotnet test --project tests/SqlSource.Repository.Tests` runs those of that project, and `./pre-commit-validation.sh --full` runs them all.
- `tools/build-site.sh` gives no warning.
- The grep of `site/AGENTS.md` for internals, run over the range, has no hit that is not text of a message or a title.
- The word count of the range, before and after, is in the pull request's description and in a `#` comment line of `ledger/phase-22.tsv`.
- The search for the order of the outputs, run over the range as the diagnostics guide says, prints only text that the build or the tool prints.
- The description of the pull request holds what section 10 of the rewrite guide asks.  "Facts with no owner" is written as its section 8 says, in the pull request and in `ledger/phase-22.tsv`.
- The reviews of the outline are done, in their order: content review, `branch-review:review`, `./pre-commit-validation.sh --full`.

## For the reviewers

**The no-loss reviewer** reads each section old against new, with the checker's output for that section beside it.

- Start with `SQLSRC225` and `SQLSRC224`: together they are a quarter of the range.  Compare the `Msg` numbers and the SQLSTATE codes as sets.
- Compare the four tables cell for cell.
- In `SQLSRC123`, check that each reason still has its own cost, and that the text still says which marker the error points at: `-- visibility:`, else `-- member-location:`, else the name.
- In `SQLSRC213`, check the order of the sources of a connection: the command line, then the variable of the name, then `SQLSOURCE_CONNECTION`, and the environment before the `.env` file.
- Read `SQLSRC209`, `210`, `216` and `221` for "not" and "only".
- Run the check for lines outside the range yourself.

**The accuracy reviewer** checks the reworded text against the source.

- Each `server:` line and each reason against the source files named above.
- The exit codes of `SQLSRC219` against `reference/cli.md#exit-codes` and the tests of the tool.
- The four new `See:` lines: the anchor exists, and the section says what the error is about.
- Each sentence on PostgreSQL and on SQL Server on its own: a sentence true of one database must not now read as true of both.
- The "Database" lines of `SQLSRC224` and `225` against the text under them.

## Traps

- **The title of `SQLSRC222` is "Directory cannot be read".**  It is the descriptor's title and it stays.  The text says "folder".
- **The title of `SQLSRC216` says "describer".**  The grep for internals hits it.  A title is text that the user sees, so it stays, and the text below it does not use the word.
- **`SQLSRC216` has no cause that a user can meet.**  It says that no dialect gets this error.  Do not invent a fix: by the diagnostics guide it has no "Fix" part.  Keep what it says, as phase 1 left it, in short sentences.
- **Do not merge the PostgreSQL half and the SQL Server half of `SQLSRC225`** into one list of causes.  The lines that the reader sees differ: a SQLSTATE on one, `Msg` numbers on the other.
- **Do not put a table of `SQLSRC224` into a list or a tab.**  A table stands at the left margin, after the sentence that names it.
- **`SQLSRC209` and `SQLSRC210` are the tool's ids that the build reports too.**  Their text speaks of both.  Keep both.
- **The `help:` line of `SQLSRC213` is quoted in three forms.**  Each is a code span, and each stays.
- **`SQLSRC122` is the first section of the range and `SQLSRC225` the last.**  The line `## SQLSRC226` and all below it belong to phase 23.
- **A `console` block that opens with `$ dotnet sqlsource` is one block.**  Do not cut the command from the output.

## Calls made in this brief

- **The four new `See:` lines**, as the table above gives them.  The map says that the rewrite gives each of the seven sections a `See:` line and names no target.  `SQLSRC238` and `239` link `sidecar-files` for the same subject as `SQLSRC221`, and `SQLSRC201` to `203` link `arguments`.
- **`SQLSRC225` links two different anchors**, one for each database.  On PostgreSQL the section that says how the tool asks the server is `what-describing-does`.  On SQL Server the table of `what-can-be-described` names this error three times.
- **The range does not move after the recount.**  The lines of phase 2 are new text, which no reviewer reads old against new, so they do not count against the 12,000 words of the outline.
- **This phase does not edit `site/AGENTS.md`.**  Four phases would collide on it.  Phase 26 lands the rule on the shape of a diagnostic section there, after the four have merged.
