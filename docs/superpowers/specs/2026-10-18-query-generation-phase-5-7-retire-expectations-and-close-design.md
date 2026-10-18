# Query generation, phase 5.7: retire the corpus's expectations and close phase 5 - design

Date: 2026-10-18

Sub-phase 5.7 of the [query generation epic](2026-10-07-query-generation-epic-design.md), the last of phase 5.  It retires the realistic corpus's `expected-pg14.txt` and `expected-pg18.txt`, whose facts the committed sidecars of [sub-phase 5.6](2026-10-18-query-generation-phase-5-6-realistic-corpus-design.md) now hold, finishes the user documentation of phase 5, and closes the phase.  After it the repository is ready for the first release a user can use, which the owner tags.

It builds on 5.6, and on 5.5 for the close.

## Intent

The epic decided that the expectations are retired once the committed sidecars hold every fact they render, and that `--check` on both majors then does their work.  Keeping both is two records of one description, recorded by two scripts, that a change to the describer must update together.  The retirement is its own pull request because it deletes more than a hundred files, which with 5.6's sidecars would pass the limit of 150.

Phase 5 is the first release a user can use.  Each sub-phase documented what it delivered; this one reads the user documentation whole, as a user of the release will, and makes the common path say what is true.

## Goals

1. Every `expected-pg14.txt` and `expected-pg18.txt` under `Realistic/Cases/` is deleted, with `tools/record-realistic.sh` and the test that compares with them.
2. The pull request says which sidecar field took each line of an expectation.
3. Every document that names the expectations or the script is updated.
4. The user documentation of phase 5 is whole: the README's status line and common path, the "What works today" table, and every callout and sentence that says models are still to come.
5. The epic outline's phase 5 is closed: its row Done, and a Settled section that records what the sub-phases settled.

## Non-goals

- A release.  The owner tags it by `docs/publishing.md`'s procedure, in a pull request of its own that sets `VersionPrefix` and moves the diagnostics of `AnalyzerReleases.Unshipped.md` to `AnalyzerReleases.Shipped.md`.
- Any change to the describer, the generator or a case.

## What exists today

As of the merge of 5.6:

- 117 expectation files, of fifty-nine cases: two for each case but `merge-returning`, which has `expected-pg18.txt` alone.  The three refused cases' render the failure: its id, its arguments, its help, and the server's lines, its SQLSTATE, hint and position.
- `RealisticDescriptionTests` compares each description with its expectation, and `tools/record-realistic.sh` writes them under `SQLSOURCE_RECORD_REALISTIC`.  `DescribeAsync_Case_IsRefusedOnlyAsItsCaseSays` checks a refused case's id against its `case.json`'s `refusal`.
- The expectations or the script are named by `AGENTS.md` (rule 5), `.editorconfig`, `tools/AGENTS.md`, `tools/README.md`, `tests/SqlSource.Tests.Postgres/README.md`, `PostgresContainer.cs`, `Recording.cs`, `Realistic/RealisticCase.cs` (`ExpectedPath`, `Files`), `Realistic/RealisticCasesTests.cs`, `Realistic/Describer/RealisticDescribe.cs` (`Expectation`, `Render`, `RefuseMessage`), `RealisticDescriptionTests.cs`, `RealisticNullabilityNoiseTests.cs`, whose failure message points to an expectation, and `docs/tech-debt/TD-0051-columns-that-cannot-be-null-are-answered-nullable.md`.  The builder searches again before the plan, since the list is from a search and code may have moved.
- `--check` compares every case's sidecar on 18 and, but for the cases 5.6 filters out, on 14.

## Design

### Which field took each line

The pull request's description holds this table, as the epic requires:

| Line of an expectation | Sidecar field |
|----|----|
| `kind` | `resultKind` |
| `plan` | `plan` |
| `table-match` | `tableMatch`, with `matchesTable` |
| `parameter @name: type (source)` | `parameters[].name`, `type.name`, `typeSource` |
| `column n name: type \| nullable \| source \| origin` | `columns[].ordinal`, `name`, `type.name`, `nullable`, `nullableSource`, `origin` |
| `failure`, `argument`, `help` and `server` of a refused case | `case.json`'s `refusal`, for the id; the arguments, the help and the server's lines are no longer compared |

`--check` compares what changes a generated type, and ignores the provenance fields and `origin`, so the builder confirms first which of `plan`, `tableMatch`, `typeSource`, `nullableSource` and `origin` it does not compare.  For the corpus those are what the expectations proved and `--check` would not.  The retirement keeps that proof: the corpus's check compares each case's committed entry with the described one whole, provenance included, as `DescribeCommandTests` compares a fixture's entry, rather than by `--check`'s rule.  That check replaces `RealisticDescriptionTests`' comparison and runs on 14 and 18.

### A refused case

Keeps its `refusal` in `case.json`, which the existing test checks on both majors.  Its arguments, its help and the server's lines were in its expectation alone, and are no longer compared: the first two are the descriptor's text with the case's names, which the diagnostics' own tests cover, and the server's text is PostgreSQL's.

### A case an earlier major describes otherwise

Sub-phase 5.6 gave such a case `checkedFrom`, and left it out of `--check` on an earlier major, while its expectation for that major still proved the describer there.  None exists today.  After the retirement such a case is described on the earlier major by no committed record: its `checkedFrom`, with the `why` that 5.6 requires, says so, and the case's description on the earlier major is not checked.  `docs/tech-debt` records this as a limit when the first such case appears, not before.

### What stays

The truth, the oracle, the noise budget and its `Budgets`, `RealisticNullabilitySafetyTests` and `RealisticNullabilityNoiseTests`, as the epic decides; the noise test's failure message, which points to an expectation, points to the sidecar instead, and nothing else of it changes.

### The documents of the retirement

`AGENTS.md`'s rule 5 names what a new minor of an image records again: the describer's fixtures and the sidecars of `tools/describe-end-to-end.sh`, and no longer the expectations.  The same in the image section of `tests/SqlSource.Tests.Postgres/README.md`, and its realistic corpus section rewritten without the expectations and the script.  `tools/README.md` and `tools/AGENTS.md` drop the script.  `.editorconfig`'s section for the expectation files is removed.  `CONTRIBUTING.md`, if it names the script.  `TD-0051` re-read and updated where it names an expectation.

### The user documentation

By `docs/manual/AGENTS.md`'s "When a phase lands", read whole for phase 5:

1. The "What works today" table: every row of phase 5 says what is true, and no row says models are coming.
2. Every callout of phase 5 replaced, found by the manual's `git grep` for "Coming in a later release" and its finder of sentences that say a thing is still to come.  A callout that also covers a later phase is narrowed to it.
3. The README, as its phase map row names: the introduction's paragraph on what SqlSource generates, the status line, "steps 1 to 4 ... give you a constant", step 4, step 5, "Only want to generate models?", the FAQ on the models' namespace, "Custom type mapping", and the line to the examples of 5.5.  It is packed, so its links stay absolute.
4. `concepts.md`, `tool.md`, `src/SqlSource.Tool/README.md`, `generator.md`, `postgresql.md`, `settings.md` and `sql-files.md`, each read for what an earlier sub-phase left saying "later".
5. `docs/tech-debt` and `docs/deferred`, each item that names phase 5 re-read: its trigger has come, or it is closed.

`docs/architecture/` is read for any "Not built yet" placeholder of phase 5 that is left.

### Closing the phase

The epic outline's phase 5 row is Done.  Its Phase 5 section gains, as phases 2 and 3 have, the table of sub-phases with their specs, and a Settled section: what each sub-phase settled that the outline had recommended or left open, and each finding that changed what it said, recorded where it stood.  Its Decided and Recommended items that the sub-phases settled are moved there.

## Testing

`./pre-commit-validation.sh` passes, and CI on 18.6, 14.24 and Npgsql 8.  The corpus's whole-entry check fails on a hand-edited provenance field of a committed sidecar, which a throwaway edit proves before the pull request, recorded in its description.  The documentation's own tests, of links and of the diagnostics' sections, pass.

## Order of work

One pull request, one commit for each step:

1. The epic's 5.7 row says In progress.  The whole-entry check of the corpus, beside the comparison with the expectations.
2. The expectations, the script and the comparison deleted, and the documents of the retirement.
3. The user documentation.
4. The close of the phase in the epic outline.

About 140 files, 117 of them deleted.  The plan counts them before it starts; if they pass 150, the user documentation and the close go first, as a pull request of their own, and the retirement after.

## Left to the builder

- The form of the whole-entry check.
- The wording of the README's status line and of the Settled section.

## Calls made in this spec

Not decided with the owner, and open to the review of this spec:

- The corpus's check compares the whole entry, provenance included, so that the retirement loses nothing the expectations proved.
- A refused case keeps its id and loses the comparison of its arguments, help and server lines.
- A case an earlier major describes otherwise is not checked on that major after the retirement.
- The user documentation is finished in this sub-phase, with the retirement, rather than in a pull request of its own, unless the count of files says otherwise.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.  When this spec was written the repository had no `v*` tag.  The release that follows the close is the owner's.
