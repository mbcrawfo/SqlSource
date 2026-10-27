---
name: reviewing-maintainability
description: Use when asked for a maintainability review, cleanup, tidy-up or housekeeping pass over the code of this repository, or when a folder has grown past about twenty files, or comments mention sub-phases and later phases that have since shipped.
---

# Reviewing maintainability

## Overview

A review of the code in `src/` and `tests/`: its comments, its folder structure and its duplicated patterns.  It reads every file, so it is done as parallel read-only sweeps by subagents, each with one question, and every finding is checked and applied by a fresh agent that did not find it.  Comments and structure are fixed in the review's own pull request; a pattern that needs a refactor is recorded in `docs/tech-debt` and not fixed.  The AGENTS.md files, `docs/architecture/` and the readmes are not reviewed here: the review edits them only to follow code it moves or renames.

**Reading is the test.**  A grep for `phase`, `later` or `TODO` finds the comments that announce their age and misses the ones that are simply wrong.  Each sweep reads its files in full.

## The sweeps

The briefs are in [sweeps.md](sweeps.md).  Each brief says what the repository does on purpose, so the sweep does not report it.

1. Measure the folders with the command in "Comments, one sweep per slice" and cut the comment slices by its rule.
2. Dispatch at once, each a read-only subagent with its brief: folder structure, one comment sweep per slice, patterns in `src/`, patterns in `tests/`.  Write the brief that every comment slice shares once, to a file in the scratchpad, and give each slice's prompt that file, its folders and its file list.
3. When a sweep reports, compare its list of files read in full with its file list.  A file missing from it, read only in part, or in its list of files not read goes to a new read-only sweep with the same brief before the slice is checked.
4. Then dispatch a checker with the brief in "The checker", on that slice's files only.  The two pattern reports get a checker too, which edits nothing.  The structure report is checked by the lead: a move by the whole-word count of "Weighing a move", a missing AGENTS.md by reading the evidence the sweep gives.
5. Folder moves, the rewrites of old paths and names in documents, and the `TD-` items start only after every checker has finished, so no two agents edit one file.

## What to do with each kind of finding

| Finding | Action |
|----|----|
| A cohesive group of four or more files in a large folder | Move now, if the cost below allows |
| A folder whose traps no AGENTS.md covers | Add one, under 250 lines, traps only, with the maintenance footer |
| A comment that is wrong, stale, or restates its own code | Fix or delete now |
| A test whose assertions do not check what its comment says | Correct the comment now; record the gap in the area's grouped `TD-` item; do not change the test |
| Duplicated logic, uneven conventions, over-long methods, dead members | One grouped `TD-` item per area (`src/`, tests), numbered, with the file links; do not refactor |
| A test that depends on the process, the clock or the repository | Its own `TD-` item: it is fragility, not duplication |

A `TD-` item lists only what a checker confirmed, with the confirmed locations and counts.  `docs/tech-debt/AGENTS.md` has the item format.  Date the review in the item's Problem section.

## Weighing a move

A move changes a namespace: `dotnet_style_namespace_match_folder` is on, and the build treats IDE0130 as an error everywhere but `Polyfills/`.  A child namespace sees its parent without a `using`; the parent, and every other consumer, needs one.  So the cost of a move is the number of files outside the new folder that reference its types, the files that stay in the parent folder included.  Count it with a whole-word grep over `src` and `tests` before deciding, and:

- Move when the group is a concept with a name, references little of what stays, and costs under about twenty `using` lines.
- Leave a group that a dozen folders reference (`SqlDialect`), a group that cuts across a sequence an AGENTS.md narrates as one (the steps of `Describing/`), and `tests/SqlSource.Tests/Generator/`, which `tests/SqlSource.Tests.RoslynFloor` compiles whole and `TestLayoutTests` keeps to `*Tests.cs` files.
- `tests/SqlSource.Tests/Snapshot/Examples/`, `tests/SqlSource.Tool.Tests/Fixtures/Projects/` and `AnalyzerReleases.*.md` stay where their `.csproj` names them.
- A test moves with its type, into the mirrored folder.

## Executing the moves

1. `git mv` each file and rewrite its `namespace` line.  For every type that moved, add `using <new namespace>;` to each file outside the folder that mentions the type by whole word; a word in a comment adds a `using` the compiler will reject, which the next step removes.
2. Build the solution in a loop.  IDE0005 is an error, so each pass reports the unnecessary `using` lines; delete them by line number, highest first, and build again until clean.  The build stops at the first failing project, so expect several passes.
3. Rewrite the old paths in every document that is kept current: the AGENTS.md files, `CONTRIBUTING.md`, `docs/architecture/`, the readmes (`tests/README.md`, `tests/SqlSource.Tests.Postgres/README.md`, `tests/SqlSource.Tests.SqlServer/README.md`, `tools/README.md` and `.github/workflows/README.md`), `docs/tech-debt/*.md` and `docs/deferred/*.md`, and comments in `src`, `tests`, `tools` and `.github`.  Rewrite there too the name of a type, member or test that the review renamed.  Then check every relative link and every backtick path in those files resolves.  This is the only edit the review makes to those documents.  Leave `docs/superpowers/` alone: plans and specs are records of their date.
4. Run `./format.sh` to sort the `using` lines, then the full test suite.

## Finishing

`./pre-commit-validation.sh` before each commit, and again on the final tree before the push; nothing runs it for you.  Lines over 120 characters fail `editorconfig-checker`, and a rewritten comment is where they appear.  Check `VersionPrefix` against the highest `v*` tag before opening the pull request.  The final report and the pull request state each coverage gap exactly, by file, with no rounding of the count.

## Common mistakes

- Reporting what an AGENTS.md says is deliberate: the full `new DiagnosticDescriptor(...)` per diagnostic, the linear path searches, the parallel engine-name lists, the allocation-free readers.
- Taking a comment's "the tool will", "phase 5" or "not yet" as stale without checking the epic outline, `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`: several are still true.
- Fixing a duplicated helper while passing: it belongs in the tech-debt item, and the pull request stays a review.
- Proposing a sub-folder from file names alone.  Read the first thirty lines of each file and count the references first.
- Leaving a bare `// Review focus N.` or "this sub-phase" in a test: the plan it refers to is not named, so the reader cannot follow it.
- Letting the agent that found a finding apply it.  A fresh checker re-reads the spot first.
- Printing several files with one `cat`: past about 30 KB the output is cut to a preview, and the sweep then reports a file it did not read.
- Changing a test's data, cases or name to make its comment true.  Correct the comment and record the gap.
- In `zsh`, an unquoted glob such as `grep --include=*.cs` fails with "no matches found"; quote it.  `zsh` also does not split an unquoted variable into words, so run a loop over a file list through `bash`.
