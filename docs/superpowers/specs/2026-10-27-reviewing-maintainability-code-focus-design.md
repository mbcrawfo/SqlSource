# Reviewing maintainability: a review of the code

## Intent

The run of 2026-10-27 (PR #159) did what the skill asks, in 32 minutes, but an evaluation of it found four weaknesses.  The AGENTS.md sweep was the costliest, and it still left about 360 KB of `docs/architecture/` unread.  Two comment slices printed several files in one command, saw only the 2 KB preview of the output, and still listed those files as read.  For six slices the agent that found a comment applied it, and the 35 pattern findings became tech-debt items after about two greps of spot checks.  The skill names one comment sweep for all of `tests/` (120k lines), so the run had to work out its own slices.

The owner is building a separate skill that cleans up the guidance in the AGENTS.md files, and docs are specialised enough to have a skill of their own.  So this skill becomes a review of the code: comments, structure and duplicated patterns in `src/` and `tests/`.

## Goals

- The sweeps are folder structure, comments (sliced by size), patterns in `src/` and patterns in `tests/`.  The AGENTS.md accuracy sweep is removed.
- The structure sweep also names folders whose traps no AGENTS.md covers, and the review adds one for each.  Researching missing guidance stays here: the owner's other skill cleans up existing guidance and does not look for gaps.
- Comment slices are sized by a rule, not a fixed list: about 12k lines each, split along folder lines, measured at the start of the run with a command the skill gives.
- Each sweep reads its files with the Read tool, so long output cannot be cut off unseen, and ends with the list of files it read in full.  The lead compares that list with the slice's files and sends another agent for any file that was missed.
- Every finding is checked by an agent other than the one that found it.  When a sweep reports, a fresh checker gets its findings, re-reads each spot, drops what does not hold and applies the rest, each checker on files no other agent edits.  For a pattern finding the checker confirms every location and count before the lead writes the `TD-` item.
- A test whose assertions do not check what its comment says gets its comment corrected, and the gap goes into a `TD-` item.  The review does not change test data, add test cases or rename tests to close the gap.

## Non-goals

- Checking AGENTS.md files, `docs/architecture/`, or the readmes (`tests/README.md`, `tests/SqlSource.Tests.Postgres/README.md`, `tests/SqlSource.Tests.SqlServer/README.md`, `tools/README.md`, `.github/workflows/README.md`) against the code.  Also out: placeholders for shipped phases, and the size or wording of existing AGENTS.md files.
- Refactoring patterns, which are still recorded and not fixed.

## Anti-goals

- A finding applied on the word of the agent that found it.
- A coverage list that names a file the sweep did not read in full.

## Constraints

- Docs are edited only to follow the code.  A move rewrites old paths in every document listed in step 3 of "Executing the moves", and a type or member renamed by the review gets its name rewritten where documents mention it.  Root `AGENTS.md` rule 6 requires both in the same commit.
- Sweeps still read the AGENTS.md files that cover their area, to learn what the repository does on purpose.  They read them; they do not check them.
- Root `AGENTS.md` rule 3: minor findings are fixed or recorded, never skipped.

## Approaches

Who checks and applies a finding:

1. The lead checks every spot and applies every edit.  This is independent and simple, but with about 150 findings it fills the lead's context and runs one finding at a time.
2. **Chosen:** a fresh checker per slice re-reads the flagged spots and applies the findings that hold, in parallel and on disjoint files.  This is independent and fast.  It costs one agent per slice, which reads only the flagged spots.
3. The finder applies its own findings, as happened in PR #159.  This is the cheapest, but nothing checks a finding twice.  Rejected.

## Decided

- The AGENTS.md accuracy sweep and its brief are removed.  So are the table rows for a wrong name or claim in a document and for a placeholder of a shipped phase, and the "Taking 'phase 5' as stale" mistake insofar as it concerns docs.  The epic outline check stays for comments.
- The row "a folder whose traps no AGENTS.md covers" stays.  Its finding moves to the folder-structure brief.
- Test-comment gaps go to tech debt, not into test changes.
- The skill's `description` drops "an AGENTS.md is suspected of being stale".  The root `AGENTS.md` section "Reviewing maintainability" drops "AGENTS.md accuracy" from its list.

## Left to the builder (my calls, to check)

- The slice size of about 12k lines, and the exact measuring command.
- Whether the checker is briefed through a shared file, as the lead's `comment-brief.md` did in PR #159, or inline.
- How the pattern checker's results reach the lead before the `TD-` items are written: the checker reports confirmed items and the lead writes the items.
- Smaller lessons of the run, worded into "Common mistakes" or the finishing steps:
  - In `zsh` an unquoted glob in `grep --include=*.cs` fails with "no matches found"; quote it.
  - A rewritten comment is reflowed as a whole paragraph rather than edited line by line, which is where the over-long lines came from.
  - The final report states any coverage gap exactly.
