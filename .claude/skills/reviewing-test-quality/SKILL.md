---
name: reviewing-test-quality
description: A review of whether each test of this repository earns its place.  It measures the tests, has agents read every test beside the measurements and ends in a cleanup epic.  Only a person starts it, with /reviewing-test-quality and an optional list of test folders.
disable-model-invocation: true
---

# Reviewing test quality

## Overview

A review of every test on four questions: **value** (does it assert what its name says), **brittleness** (does it break when the code changes and the behaviour does not), **redundancy** (do other tests make the same check) and **cost** (is a database or process test worth its time).  It is evidence first: the tests are measured mechanically, then one agent reads each slice of them in full with the measurements beside it, and a fresh agent tries to refute every finding that would remove, merge or move a test.  It changes no test.  It ends in a cleanup epic: an outline, one spec for each small phase and a findings appendix, with one item of tech debt for each source project's unchecked code.

Comments, folder structure and duplicated patterns are the review of `.claude/skills/reviewing-maintainability/`.

**A number is never a verdict.**  Stryker's runner for these tests is a preview, a mutation run sees only its own scope, and mutation cannot see a diagnostic's wording, an allocation budget, a wait or an engine's behaviour.  A test is never removed on a mutation result alone.

## Before starting

Every command below is run from the repository root, in `bash`, with these three set:

```bash
R="$PWD/artifacts/test-review/$(date +%F)"   # the run folder; a second run of one day ends in -2
D=tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll
S=.claude/skills/reviewing-test-quality
```

1. **The run.**  The argument is a list of test folders; with none, the run is every folder of [projects.txt](projects.txt), the one marked `not-run` too.  `R` is under `artifacts/`, which git ignores; everything measured and every agent's file goes there.
2. **The previous review** is the newest `docs/superpowers/specs/*-test-cleanup-findings.md`, if there is one.  Every agent is given it: what it settled is not raised again.
3. **The scopes and projects.**  A scope of [scopes.txt](scopes.txt) is needed when one of its `folders` holds, or is held by, a folder of the run.  A project of `projects.txt` is needed when it holds a folder of the run, or is one.  Docker runs for the projects under `tests/DatabaseTests/`.
4. **Cut the slices**, which takes seconds and is the count of the run's reviewers:

   ```bash
   dotnet build tools/MutationReport --nologo --verbosity quiet
   dotnet $D slices . "$R/slices" --exclude-from $S/excluded.txt <the folders of the run>
   ```

   `$R/slices/index.txt` has a line for each slice.  `slices` refuses a folder that holds anything, so a second cut names a new run folder.
5. **Say the cost to the owner.**  One reviewer and one verifier for each slice, up to three mirror agents and a writer: everything is 59 slices on 2026-11-12.  The scopes run one after another, each about a second for each mutant it tests, the count above it in `scopes.txt`, and about five minutes besides; [tools/README.md](../../../tools/README.md#how-long-a-run-takes) has the measured runs.  The test projects take what `./pre-commit-validation.sh --full` takes for its tests, and the two `SyncAsync` projects besides.  A run of more than ten slices, or of more than an hour of measurements, **waits for the owner's yes**.  If the measurements will not fit in a night, ask the owner which scopes to leave out; a scope left out is named in the epic.
6. **Start `$R/run-notes.md`**, which the writer is given: the date, the folders of the run, the slices, and then, as the run goes, each scope's time and counts from `measure.log` and `evidence/gaps.md`, each scope left out or failed, each test project that failed, each coverage gap by its path, and each correction to a list of the skill.

**Agents.**  These instructions are the opt-in that the `Workflow` tool asks for.  Without that tool, dispatch agents in batches of about ten.  Every agent is general-purpose, on the lead's model.  [briefs.md](briefs.md) has the briefs: an agent's prompt is the path of `$R/brief.md`, the text of its own section of `briefs.md`, pasted, and the paths that section says it is given.

## Stage 1: measure

No agents.  One command, in the background, and wait for it to end; `;` and not `&&`, so that a scope that fails does not stop the tests:

```bash
$S/measure.sh "$R" mutation <the scopes needed>; $S/measure.sh "$R" tests <the projects needed, but the one marked not-run>
```

With no name after `mutation` or `tests`, every scope or every project that is run is meant.  A run that needs no scope leaves the first half out.

- Stryker builds in the working tree, so **nothing else that builds runs in the checkout until the command has ended**: no `dotnet run`, no test, no `pre-commit-validation.sh`.
- `tests/DatabaseTests/QueryMethodAllocation` is sliced and reviewed by reading and is never run: its tests skip under coverage.
- `$R/measure.log` has a line for each start and end, with the seconds and the exit code.  A scope that failed has a file `failed` in its folder: run it once more by name.  One that fails twice has its folder removed from `$R/mutation/`, its tests are judged by reading, and `run-notes.md` names it.  A fault of Stryker that [tools/README.md](../../../tools/README.md#limits) does not describe is put to the owner.
- A test project with failing tests still gives its times: note it.  One that does not build gives none, and its slices say `Not run`: fix the cause or ask the owner, since a cost finding needs the times.

## Stage 2: condense

```bash
dotnet $D condense . "$R" --broad-from $S/broad-tests.txt
```

It writes `$R/evidence/<slice>.md` for each slice, `gaps.md` and `database-only-lines.md`, and refuses a scope that failed or whose run tested a static mutant.  Read one evidence file whole before any agent is sent: a section that is wrong there is wrong in all of them.  [broad-tests.txt](broad-tests.txt) is data that a run may correct: a test that checks a whole output at once and is not in it is added, the evidence is condensed again, and `run-notes.md` says so.

## Stages 3 and 4: review and verify

1. Write `$R/brief.md` once: the repository root, `R` in full, the previous appendix's path or that there is none, and then "Every agent" and "The shared brief" of `briefs.md`, whole.
2. One reviewer for each slice, all at once, with "The reviewer" and its slice's name.
3. When a reviewer returns, compare what it read with its slice:

   ```bash
   comm -23 <(sort "$R/slices/<slice>.txt") <(sort "$R/findings/<slice>.read.txt")
   ```

   The files this prints go to one more reviewer, with "The reviewer" and those files as its list, who writes the `-rest` files that section names.  A file that one does not read in full either is a coverage gap, noted by its path.
4. As soon as a slice's files are all read, one fresh verifier for it, with "The verifier".  A slice with no findings needs none.

## Stage 5: measure again, where asked

After every verifier has ended, since this builds.  For each scope with findings whose verdict is **needs measurement**, the candidates are the tests of those findings, and the scope is run once more without them:

```bash
tools/mutation-test.sh <project> --output "$R/remeasure/<scope>" \
    --tests '(<the tests line of the scope>)&FullyQualifiedName!=<Namespace.Class.Method>&FullyQualifiedName!=...' \
    <the mutate line of the scope, each glob quoted>
tools/mutation-test.sh compare "$R/mutation/<scope>/reports/mutation-report.json" \
    "$R/remeasure/<scope>/reports/mutation-report.json"
```

A candidate is left out by `!=` and its full name, which the summary line of `kills` gives: `!~` would also leave out every test whose name starts with it.

- Nothing `LOST` confirms every candidate.
- For each mutant `LOST`, run `dotnet $D kills` in the scope's first report for each candidate: a candidate that killed it, or had it as a timeout under it, is refuted.  The others are confirmed.
- `NOT COMPARABLE` is read by the lead: a mutant that was killed and now times out is a doubt, and the candidates that reached it stay unsettled.

Add a line to each such finding's entry in its verdict file: `- measured: confirmed | refuted | unsettled`, with `compare`'s line for it or `nothing lost`.  The writer takes **measured: confirmed** as confirmed and the other two as they are.

## Stage 6: compare the mirrors

One agent for each pair of "The mirror agent" whose sides are all in the run, with that section, and then one verifier for each pair's findings, which writes `verdicts/<pair>.md`.

## Stage 7: write the epic

One writer, with "The writer".  Then check what it wrote, yourself:

- every confirmed finding is in exactly one phase, and every other finding is in the appendix with its reason;
- no two phases touch one file, and each phase keeps to one folder and, where it could, one kind of action;
- every mutation guard is its scope's command of `scopes.txt`, with two output folders and the `compare` of them;
- every test named as cover exists, and no cover is a broad test or a test of the other engine;
- the counts of the outline, the specs and the appendix agree.

## Finishing

The pull request holds the outline, the phase specs, the appendix and the tech-debt items, and no other change but a correction to a list of the skill, which its description names.  `./pre-commit-validation.sh` before the commit and `./pre-commit-validation.sh --full` before the push; `branch-review:review` until clean; `VersionPrefix` against the highest `v*` tag.  Offer the pull request to the owner and open it when they agree.  Its description is `run-notes.md` in short: the run's folders, the slices, each scope's counts and time, the findings by action and by verdict, and each coverage gap and failed scope exactly, by file, with no rounding.

## Common mistakes

- Believing a kill that the test's inputs do not explain.  It is a doubt about the tool: the verdict is **needs measurement**.
- Taking a `Timeout` for a kill.  A report names no test for it, and at a short allowance a surviving mutant is reported as one.
- Taking a survivor for a fault of a test whose name promises nothing about it.  It is a gap that belongs to no test, counted in the appendix.
- Taking a broad test, or a test of the other engine, as cover.
- Taking two tests that cover each other as two tests to delete.
- Proposing to delete a database test, a test of `tests/SqlSource.Repository.Tests` or a test of a test library without a cover that a verifier read.
- Reporting what an `AGENTS.md` says is deliberate: the second compile of `Generator/`, the driver floors, the engine twins, the allocation budgets, the golden corpus.
- Running anything that builds during a mutation run, or `dotnet run` from two agents at once: agents run the built program, `dotnet $D`.
- Giving the repository root as `.` to an agent: an agent is given paths in full.
- Letting the agent that found a finding verify it.
- Printing several files with one `cat`: past about 30 KB the output is cut to a preview, and the reviewer then reports a file it did not read.
- In `zsh`, a glob without its quotes fails with "no matches found", and an unquoted variable is not split into words: run these commands through `bash`.
