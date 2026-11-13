# Docs overhaul, phase 26: close

Phase 26 of the [docs overhaul](2026-11-12-docs-overhaul-epic-design.md), and its last.  Read the outline in full, the [content map](2026-11-12-docs-overhaul/content-map.md) (sections 1 to 3 and 9), [`rewrite-guide.md`](2026-11-12-docs-overhaul/rewrite-guide.md), [`diagnostics-guide.md`](2026-11-12-docs-overhaul/diagnostics-guide.md) and sections 4 to 6 of the [brief of phase 0](2026-11-12-docs-overhaul-phase-00-guard-rails-design.md) before this brief.

- Done by: an Opus agent.
- Needs: every phase, 0 to 24, merged, and phase 25 done.  Phase 25 makes no pull request and leaves no commit when it has nothing to do: the outline then has no section "Resize", and this phase writes it.
- Runs beside: nothing.

## Goal

The epic leaves nothing behind that only the epic needed, and every rule and document that speaks of the site is true.  The site is read once more, whole, as its reader reads it.  The outline is closed.  The pull request is about 200 lines, most of them removed.

## The order of the work

The order matters: the ledger is read before it is removed.

1. Check that phases 0 to 24 are merged: `ledger/phase-00.tsv` to `ledger/phase-24.tsv` are on `main`.  Then look for phase 25: `ledger/phase-25.tsv`, and the section "Resize" of the outline.
2. Start `ledger/phase-26.tsv`, with its `# base` line.  It holds the fixes of this phase.
3. If the outline has no section "Resize", count the pages and write it (below).  Then do the last read, and fix what it finds.
4. Check the deferred work and the debt.
5. Make the last pass over the rules and the other documents.
6. Run the last "check coverage", and keep its output.
7. Remove the files of the epic.
8. Close the outline.
9. Run the reviews, and open the pull request.  The owner publishes the site after the merge.

## The counts, when phase 25 made no pull request

Phase 25 makes no pull request, no commit and no push when no page needs a split or a merge and no fact needs a place.  Then `grep -c '^## Resize' docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md` prints 0, and `ledger/phase-25.tsv` is not there.  Record the counts in this pull request:

- Run the count of the [brief of phase 25](2026-11-12-docs-overhaul-phase-25-resize-design.md), "The counts", on your branch.
- List the facts that the rewrites reported, with the `grep` for `no owner:` of that brief.  Run it before the ledger goes.
- Add the section "Resize" at the end of the outline, as "The record in the outline" of that brief describes it: the date, the table of counts, "no split and no merge", each fact with no owner and where it is, and each page over 3,500 or under 600 words with the reason why it stays, by the rules of that brief.
- **Stop if there is work.**  If a page is over 3,500 words and the rule of that brief gives it a clean cut, or a `no owner:` note names a fact that its owner still lacks and that fits there word for word, phase 25 is not done.  Say so, and do not close the epic: phase 25 runs before this one.

## The last read

Read the site as a junior developer who works SqlSource out alone.  Build it with `tools/build-site.sh` and read the built pages, `dotnet docfx site/docfx.json --serve`, not the Markdown alone.

**The landing page and Get started, front to back:**

- [ ] `index.md`: one paragraph, one sample pair, the table of databases, one link to start, the rows "What do you want to do?", the links for an error.
- [ ] The nine pages in the order of `start/toc.yml`: `install`, `first-query`, `how-it-works`, `write-queries`, `call-queries`, `transactions`, `testing`, `keep-up-to-date`, `errors`.
- [ ] Each page ends with "Next:" and the next page.  The last one leads back to `index.md#find-what-you-need`.
- [ ] No step sends the reader to Reference before the step is done.
- [ ] One running example, and the `repository` output.  `transactions.md` is the first page that names `methods`.
- [ ] Each tab group is read twice, once for PostgreSQL and once for SQL Server.
- [ ] A reader who follows the steps with the files of `examples/PostgresExamples/Repository`, and then of `examples/SqlServerExamples/Repository`, sees what the pages say.

**The sixteen tasks of the landing page**, each followed from its row to its answer.  The rows on the page are the authority; the map gave these:

| # | The reader wants to | The row leads to |
|----|----|----|
| 1 | Run queries in a transaction, or with a timeout | `start/transactions.md` |
| 2 | Pass a list to `IN` | `guides/lists.md` |
| 3 | Stop a property being nullable | `guides/nullability.md` |
| 4 | Mock a repository in a test | `start/testing.md` |
| 5 | Insert many rows in one call | `guides/many-rows.md` |
| 6 | Use an enum or an id type of their own | `guides/custom-types.md` |
| 7 | Read a JSON column | `guides/json.md` |
| 8 | Use a second database | `guides/several-databases.md` |
| 9 | Keep the sidecars right in CI | `guides/ci.md` |
| 10 | Describe as they save | `guides/describe-on-save.md` |
| 11 | Choose a table name at run time | `guides/dynamic-sql.md` |
| 12 | Use Dapper | `guides/dapper.md` |
| 13 | Look up a setting and where to write it | `reference/settings.md#all-settings` |
| 14 | Look up a `-- word:` line | `reference/sql-files.md#marker-index` |
| 15 | Look up a command or an option | `reference/cli.md#command-reference` |
| 16 | Find why it built, but something is wrong | `guides/troubleshooting.md` |

For each row, answer three questions, and write the answers in the pull request:

- Does the page answer the task with no second click?
- Does it hold a whole sample that the reader can copy?
- Does each link that it gives for a rule open the section that owns the rule?

**Three errors**, each from its help link: open `diagnostics.html#sqlsrc241`, `#sqlsrc209` and `#sqlsrc226` in the built site.  Each section stands alone, has its fix, and its `See:` line opens a section of `reference/`.

**The whole site, by search:**

- [ ] The words of the outline that a junior types: "bulk", "mock", "nullable", "timeout", "IN", "enum".  Each is in the title or the first sentence of the page that answers it.
- [ ] No status language.  `StatusLanguageTests` holds five phrases, and the rule is wider.  Read each hit of this search:

```bash
git grep -n -i -E "no effect yet|today|coming|later release|for now|currently|(^|[^[:alnum:]_])(yet|will)([^[:alnum:]_]|$)" -- README.md src/SqlSource.Tool/README.md site
```

- [ ] No internals: the grep of `site/AGENTS.md`, over every page of `site/`.
- [ ] The sidebars: three entries in the top bar; no group over nine entries, or ten where phase 25 made a split; `../diagnostics.md` under Errors.

**What to do with a finding:**

- A wrong link, a wrong "Next:", a typing error: fix it here.
- A false statement: correct it by the source, with a `corrected` row in `ledger/phase-26.tsv`.
- A finding too large for this pull request: write a `docs/tech-debt` item for it, as root rule 2 asks, and name it in the first lines of the pull request.  You cannot ask the owner, so do not leave it unwritten.

## Deferred work and debt

Root rules 1 and 2 ask that deferred work and debt are written down in the pull request that makes them.  This phase checks that the epic left none unwritten.  Read, for each of phases 0 to 25:

- the description of its pull request: "Facts with no owner", each sentence that was left because it could not be checked, each finding of a reviewer that was not fixed;
- the notes of its ledger file, before the file goes;
- each change that it made to the outline: `git log -p -- docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md`;
- the section "Resize" of the outline: each fact that phase 25 could not place.

Then compare with `docs/deferred/README.md` and `docs/tech-debt/README.md`:

- Work that the outline planned and no phase delivered is a `docs/deferred` item, by `docs/deferred/AGENTS.md`.
- A problem that a phase found and did not fix is a `docs/tech-debt` item, by `docs/tech-debt/AGENTS.md`.
- A sentence that no one could check is debt, until you check it now against the source.
- These are not items: a page that stays over 3,500 words with its reason; a rule that the outline leaves to a reviewer on purpose; a call of a brief.

Write each missing item in this pull request.  If a user can see what is missing, the item brings its limit, by the same two files.

## The rules that must be in an `AGENTS.md` by now

Check each rule against "Rules that keep the structure" of the outline, which is the authority, and against the file.  Add what is missing in the fewest words.  A bullet has at most 80 words, `site/AGENTS.md` at most 8 KB and the root file 16 KB: `AgentsFileTests` holds all three.

| Rule | Must be in |
|----|----|
| The table "Where a new thing goes", twelve rows.  Each page and each test that a row names exists | `site/AGENTS.md`, at its top |
| A fact has one home | `site/AGENTS.md` |
| State what SqlSource does; no status | `site/AGENTS.md`, and root rule 4 |
| The language rule, the seven lines of the outline | `site/AGENTS.md` |
| The terms: *query parameter*, *generator parameter*, *logical database*, *folder*; one term for one thing | `site/AGENTS.md` |
| No heading or anchor inside a tab.  A tab group that names one described database names all | `site/AGENTS.md` |
| Write effects, never internals, with its grep.  Anchors, links, SQL info strings, `--prerelease` | `site/AGENTS.md` |
| A limit: `## Limits`, an h3 with the anchor `limit-<name>`, a row in `guides/troubleshooting.md` | `site/AGENTS.md`, `docs/deferred/AGENTS.md`, `docs/tech-debt/AGENTS.md` |
| `diagnostics.md` stays at the root.  A `## SQLSRCnnn` heading never changes and never gets another anchor.  Only the title and one paragraph stand before `## SQLSRC001` | `site/AGENTS.md` |
| The line for Get started; read in order, with "Next:"; one running example and `repository`; no `## Limits` and no setting's definition | `site/start/AGENTS.md` |
| One task in the fixed shape; a setting in use, never its values; a title in the reader's words; no group of one page | `site/guides/AGENTS.md` |
| One section defines each written name; example first; rules, not how-to | `site/reference/AGENTS.md` |
| The same file names in every database folder; only what is true of the database | `site/reference/databases/AGENTS.md` |
| Three folders and two root pages; the readmes link `<folder>/<page>.html#<anchor>`; the README is brief and holds no FAQ | Root `AGENTS.md`, rule 4 |

Two rules are new with the rewrites of `diagnostics.md`.  This phase lands them: phases 21 to 24 do not edit `site/AGENTS.md`, since four phases wrote the page at one time.  Phase 3 wrote a bullet "A new diagnostic gets a section whose first bold line is its descriptor's title ...", which ends with the area's row on `reference/errors.md`.  Replace it with these two, or bring them into line if a later phase changed them:

```markdown
- A new diagnostic gets a section in one shape: its descriptor's title in bold, its "Reported by" line, a "Database" line where a database gives the error, what happened, why, `**Fix:**` with its steps, and a closing `See:` line whose first link is a section of `reference/`.  It holds no heading, and it stands alone: it keeps its own lists of values.
- A new diagnostic also gets its place on `reference/errors.md`: its area under `#errors-by-area`, `#who-reports` when the tool reports it, and `#severity` when it is a warning or information.
```

Two more rules come from the rewrite guide, and the outline does not list them for a file.  Thirteen rewrites applied them, and one new page that does not know them undoes that.  Add each as one bullet of `site/AGENTS.md`, under "Writing", if the file has room.  If it has none, leave them out, and say so in the closing note.

```markdown
- **The outputs are listed with the default first**: `repository`, `methods`, `models`, `sql`, in a part of the list too.  Text that the tool prints keeps its order.  A reader sets the dialect to their database; no text says "choose a dialect".
- **One word for each of these**: a type *claims* a `.sql` file; a column *can be null* or is *never null*; a sidecar entry is *current* or *out of date*; the tool *describes* a query; `sqlsource.json` is a *settings file*.
```

**The room in `site/AGENTS.md`.**  The limit is 8,192 bytes.  These sizes were measured with `wc -c` on the words of the briefs, each change applied in its order to the file that the brief of phase 3 gives:

| After phase | Bytes | What changed |
|----|----|----|
| 3 | 7,563 | The file, whole: the 7,493 bytes that its brief measures, and the 70 of the last sentence of its bullet on a new diagnostic |
| 4 | 7,604 | The row of a database; two names less in "Pages not yet moved" |
| 5 | 7,600 | The row of a command; `reference/sidecars.md`; one name less |
| 6 | 7,585 | The row of an attribute; one name less |
| 7 | 7,569 | Four rows in the outline's words; one name less; the clause on "Known issues" |
| 8 | 7,233 | "Pages not yet moved" goes; the Terms bullet; the README bullet |
| 9 | 7,233 | Nothing |
| 10 | 7,220 | The row of a Get started feature |
| 11 | 7,047 | The README bullet |
| 26, the two bullets on a diagnostic | 7,260 | They replace one bullet of 332 bytes |
| 26, the bullet on the outputs | 7,505 | 245 bytes; 42 words |
| 26, the bullet on the words | 7,729 | 224 bytes; 41 words |

So all four bullets fit, with 463 bytes to spare, and the longest has 63 words.  The file as you find it can differ: phase 25 changes a row when it splits a page, and feature work can add a rule.  Run `wc -c site/AGENTS.md` before and after each edit.  If the four do not fit, land the two on a diagnostic first, then the outputs, then the words.

## The last pass over the other documents

Find every `AGENTS.md` that speaks of the site by search, not from a list:

```bash
git grep -l -E "site/|mbcrawfo\.github\.io/SqlSource|the manual|diagnostics\.md" -- '*AGENTS.md'
```

At the baseline that gave 17 files, and phase 3 added four under `site/`.  For each file: every page, anchor and test that it names exists; what it says of the site's shape is true; it holds no rule of the old site; it ends with its maintenance footer; it is under its limit.  `InboundSiteReferenceTests` proves that a path exists.  It cannot prove that a rule names the right page, so read each one.

| File | Check |
|----|----|
| `CONTRIBUTING.md` | The paragraph on `site/`: the landing page, the errors page and the three folders.  The command that serves the site.  The list of what `./pre-commit-validation.sh` runs |
| `docs/publishing.md` | Under "Site", take out the paragraph that starts "One exception holds until the documentation overhaul closes".  The paragraph before it, on a run by hand, is true again as it stands.  The step on `--prerelease` still names the places that have the flag |
| `tests/README.md` | The row of `tests/SqlSource.Repository.Tests` says what that project checks now: the references to the site from outside it, the site's structure, the status phrases, the supported versions, the settings and the command reference, the readmes.  It names no `site/recipes.md` |
| `tools/README.md` | "The site": still true of `tools/build-site.sh` |
| `.github/workflows/README.md` | The rows of `build.yml` and `docs.yml`: still true |
| `tests/SqlSource.Repository.Tests/Documentation/AGENTS.md` | It names each test class of the outline's table of tests that is in the folder, and what fails it |
| `docs/architecture/**` | Links and names alone: the outline's non-goals allow no more.  Each pointer to "the manual" or to a user page opens the section it means |
| `examples/AGENTS.md`, the two example READMEs, `.github/ISSUE_TEMPLATE/bug-report.yml` | Each pointer into the site |
| `README.md`, `src/SqlSource.Tool/README.md` | Read each once as nuget.org shows it.  `ReadmeTests` holds the size and the commands |

Then search the repository for the words of the old site, and read each hit:

```bash
git grep -n -i -E "known issues|what works today|recipes\.md|concepts\.md|glossary|status column|entry page|site/README\.md" -- . ':!docs/superpowers' ':!site'
```

## What goes and what stays

Before anything goes, run `docs/superpowers/specs/2026-11-12-docs-overhaul/check/check.sh coverage`.  It must exit with 0, with no unit that is due and has no row, and no unit that is not due.  Paste its whole output into the pull request: after this phase nothing can produce it again.

**These go:**

- `inventory.tsv`, the folder `ledger/`, the folder `merges/` and the folder `check/`, all in `docs/superpowers/specs/2026-11-12-docs-overhaul/`.  No brief makes another file in that folder: phase 0 makes `inventory.tsv`, `ledger/` and `check/`, with `check/phases.tsv` in it, and the move phases make `merges/`.  If `ls` shows a fifth thing beside the three files that stay, read it before you decide.
- The bullet of `docs/superpowers/AGENTS.md` that phase 0 added under "Intent", which says that the folder also holds the instruments of the epic.
- The pattern `docs/superpowers/specs/2026-11-12-docs-overhaul/*.tsv` in `pre-commit-validation.sh`, which phase 0 added.
- The section `[docs/superpowers/specs/2026-11-12-docs-overhaul/**.tsv]` of `.editorconfig`, with its comment.
- `PagesNotYetMoved` of `SiteStructureTests`, if it is still there, with the code that lets a page stand at the root by that list, and any bullet that names it.  The root of `site/` holds `index.md` and `diagnostics.md`, and the test says so with no list.
- Any other reference to the removed files: `git grep -n "docs-overhaul" -- . ':!docs/superpowers/specs'` must name the epic's outline at most.

**These stay**, as the record of the plan: `content-map.md`, `rewrite-guide.md`, `diagnostics-guide.md`, the outline and the 27 briefs.  Do not edit them to fit what was delivered, and do not take out their mentions of the files that are gone: `docs/superpowers/AGENTS.md` says that a spec records the intent.  The outline alone gets its closing note.

## The outline, closed

`docs/superpowers/AGENTS.md` keeps an epic outline current until its epic closes.  "Closed" is a dated note directly under the title of the outline.  Nothing else changes in it, but for the section "Resize" when phase 25 made no pull request.  The note holds:

- the date, and "Closed.  From here on this outline is a record and is not kept current.";
- the last commit that holds the inventory, the ledger, the merge worksheets and the checker, for a reader who wants them;
- **what was delivered differently from the plan**, each in one line: the count of pages, with each split and merge of phase 25; each page that stays outside 600 to 3,500 words; each phase that took two pull requests, or none; each call of a brief that changed a rule of the outline; the count of corrections beyond C-01 to C-06 of the map; each item of `docs/deferred` and `docs/tech-debt` that the epic opened, by its id; each rule of this brief that found no room in an `AGENTS.md`;
- that the owner publishes the site by hand after this pull request merges.

## The last publish

The site is published by hand once more at the close.  The owner starts it, after the merge: `gh workflow run docs.yml --ref main`.  Do not start it.  Say in the first lines of the pull request that it is due.

## Done when

- The checklist of the last read is filled in, in the pull request, with the answers for the sixteen rows.
- "check coverage" exited with 0 before the removal, and its output is in the pull request.
- `ls docs/superpowers/specs/2026-11-12-docs-overhaul` prints `content-map.md`, `diagnostics-guide.md` and `rewrite-guide.md`, and nothing else: the four things of the epic folder are gone.
- `git grep -n "docs-overhaul" -- pre-commit-validation.sh .editorconfig docs/superpowers/AGENTS.md` and `git grep -n "PagesNotYetMoved" -- . ':!docs/superpowers'` print nothing.  `git grep -n "documentation overhaul closes" -- docs/publishing.md` prints nothing: the exception is out.
- `grep -c '^## Resize' docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md` prints 1, whichever phase wrote the section.
- Every rule of the table above is in its file.  `grep -c -F "A new diagnostic" site/AGENTS.md` prints 2, and `wc -c site/AGENTS.md` prints less than 8,192.  `AgentsFileTests` passes.
- Every deferred piece of work and every debt of the epic has its item.
- The outline has its closing note: `sed -n 3p docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md` prints its first line, with the date and "Closed.".
- `tools/build-site.sh` gives no warning, and every test passes.  No test of the outline's table was weakened or removed.
- The reviews of the outline are done, in their order: content review, `branch-review:review`, `./pre-commit-validation.sh --full`.  The change to `pre-commit-validation.sh` and to `.editorconfig` makes a plain run full too.

## For the reviewers

**The no-loss reviewer:**

- Read the output of "check coverage": the units of each document, the rows by disposition, and no unit without a row.  Take twenty rows marked `removed-status`, `removed-preamble` or `restated-from` with no place, and check the note of each.
- Check that the pull request removes the four things and no other file of `docs/superpowers`.
- Check that each "Facts with no owner" of phases 12 to 24 has a place, or a line in "Resize".

**The accuracy reviewer:**

- Check each changed line of an `AGENTS.md` against the repository: the page, the anchor and the test exist, and the rule is true.
- Walk four of the sixteen rows and three pages of Get started alone, for both databases, and compare with the writer's answers.
- Check the closing note against the merged pull requests of the epic.
- Check each new item of `docs/deferred` and `docs/tech-debt` against the two `AGENTS.md` files that give their form.

## Traps

- **Do not remove the ledger before you read it.**  Its notes are one source for the debt.
- **Do not edit the content map or a brief to match the site.**  They are the plan.
- **Do not start the publish.**
- **`docs/architecture/` gets links and names, no new prose.**
- **`src/SqlSource.Tool/AGENTS.md` has almost no room**, and `site/AGENTS.md` little.  Count the bytes after each edit.
- **A removed folder can still be named** by a script, by `.editorconfig`, or by a rule.  Search before the push.
- **The quoted words of two tests**, "Give the method the transaction" and "A synchronous method needs a data source without multiplexing", must still stand on their pages: `tests/DatabaseTests/SqlServer.SyncAsync/KnownDifferences.cs` and `tests/DatabaseTests/Postgres.SyncAsync/KnownDifferences.cs` quote them, and no test compares.
- **A late pull request.**  If a rewrite merged after you branched, rebase, and run the last read on its pages.

## Calls made in this brief

- **`merges/` goes with the ledger.**  The outline names the inventory, the ledger and the checker as what the last phase removes, and the content map and the two guides as what stays.  A worksheet is a working paper of one rewrite, and the closing note names the commit that holds it.
- **The counts are recorded here when phase 25 made no pull request.**  The outline says so.  This phase counts again and does not wait for a report: a count is a command, and the reasons for a page that stays are the rules of the brief of phase 25.
- **The words of the two bullets from the rewrite guide** are given here, and measured, so that the question of room is settled before the phase starts.
- **The three files that stay, and the briefs, are not edited.**  The pointer to what was removed is in the closing note of the outline.
- **The shape of a diagnostic section becomes a rule here**, with its place on `reference/errors.md`.  The outline's list of rules has neither, and no earlier phase could write them.
- **The reframings and the terms of the rewrite guide become rules if `site/AGENTS.md` has room.**  The outline does not ask for them.
- **"Closed" is a dated note under the title.**  The table of phases gets no column of status.
- **This phase has a ledger file of its own**, for the fixes of the last read, and removes it with the others.
- **A finding too large to fix here becomes a `docs/tech-debt` item**, named at the top of the pull request.  Root rule 3 says to ask the owner, and this agent cannot.
