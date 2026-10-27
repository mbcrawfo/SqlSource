# Reviewing maintainability: code focus Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the `reviewing-maintainability` skill into a review of the code only, with size-based comment slices, a check that every file was read in full, and a fresh checker that verifies and applies each slice's findings.

**Architecture:** The skill is two Markdown files, `.claude/skills/reviewing-maintainability/SKILL.md` (the procedure) and `sweeps.md` (the subagent briefs).  The briefs are rewritten first, then the procedure is rewritten to refer to them.  The root `AGENTS.md` section that names the skill follows.  A dry run by a fresh subagent checks that the skill can be followed.

**Tech Stack:** Markdown; `bash`, `find`, `wc`, `awk`; `./pre-commit-validation.sh`.

**Spec:** `docs/superpowers/specs/2026-10-27-reviewing-maintainability-code-focus-design.md`

## Global Constraints

- The sweeps are folder structure, comments (sliced by size), patterns in `src/` and patterns in `tests/`.  No sweep checks AGENTS.md files, `docs/architecture/` or the readmes against the code.
- A comment slice holds about 12k lines, split along folder lines.
- Every sweep reads its files with the Read tool and ends with the list of files it read in full.
- Every finding is checked by an agent other than the one that found it; that checker applies the findings that hold, on files no other agent edits.
- A test whose assertions do not check what its comment says gets its comment corrected and a `TD-` item; its data, cases and name are not changed.
- Docs are edited only to follow the code: old paths after a move, and names the review renamed.
- The table row "a folder whose traps no AGENTS.md covers" stays.
- Write in the house style of the existing skill files: plain sentences, two spaces after a full stop, no line limit in Markdown.
- `./pre-commit-validation.sh` passes before each commit (root `AGENTS.md`, "Commits").

## Review Focus

1. **A single folder over the slice size.**  `tests/SqlSource.Tests/Generator/` is about 21k lines with no sub-folders, and `src/SqlSource.Tool/Describing/Postgres/` is about 12k.  A reader expects the rule to say how to split such a folder (by sub-folder, else by a sorted file list in halves), not to leave one oversized slice.  Pinned in Task 1, Step 3; checked in Task 3.
2. **Two agents editing one file.**  A move rewrites `using` lines across many files, and a docs path rewrite can touch a file a checker edits.  A reader expects the order to rule this out: checkers own disjoint slices, and moves and doc rewrites start only after every checker has finished.  Pinned in Task 2, Step 2; checked in Task 3.
3. **A finding that is partly true or wrong.**  A reader expects the checker to fix what holds, drop the rest, and report each dropped finding with its reason, so the lead's summary can count them.  Pinned in Task 1, Step 4.
4. **Files that are test data.**  `tests/SqlSource.Tests/Snapshot/Examples/` and `tests/SqlSource.Tool.Tests/Fixtures/Projects/` hold inputs, not code under review.  A reader expects the slice's file list, and so the coverage comparison, to leave them out by name, so they are not reported as missed.  Pinned in Task 1, Step 3; checked in Task 3.
5. **A sweep that runs out of room.**  The PR #159 docs sweep stopped at the subagent limit.  A reader expects a sweep that cannot finish to say which files it did not read, and the lead to send another agent for them, the same as a missed file.  Pinned in Task 1, Step 2 and Task 2, Step 2.

---

### Task 1: Rewrite the sweep briefs

**Files:**
- Modify: `.claude/skills/reviewing-maintainability/sweeps.md` (whole file, 23 lines)

**Interfaces:**
- Produces: the section headings `## Folder structure`, `## Comments, one sweep per slice`, `## Patterns in \`src/\``, `## Patterns in \`tests/\`` and `## The checker`, which Task 2 refers to by name; and the measuring command of Step 3, which Task 2 quotes.

- [ ] **Step 1: Remove the `## AGENTS.md accuracy` section**

Delete the whole section.  Its last request, naming folders with traps no AGENTS.md covers, moves to Step 2's structure brief; nothing else of it survives.

- [ ] **Step 2: Rewrite the opening paragraph and the folder-structure brief**

The opening paragraph keeps: repository root, nothing modified, `.claude/worktrees/`, `bin/` and `obj/` ignored, reports give `path:line`, evidence and the proposed text or action.  It adds:
- read the AGENTS.md files that cover the sweep's area first, and report nothing they call deliberate; they are read, not checked;
- read each file with the Read tool, one file per call, never several files through `cat` in one shell command, whose output past about 30 KB is cut to a preview;
- end with the list of files read in full; a file only partly read, or not read because the sweep ran out of room, goes in a separate list of files not read.

`## Folder structure` keeps its current text and adds: name each folder with a non-obvious constraint that no AGENTS.md covers (a path that is a public URL, a file a `.csproj` names, a layout a test enforces), with the evidence and the rules a new AGENTS.md there would hold.

- [ ] **Step 3: Write `## Comments, one sweep per slice`**

Replace `## Comments, one sweep per project`.  Keep its list of what to report and that invariants and contracts stay.  Change the test case: a test whose comment says it checks what its assertions do not is reported as a tech-debt candidate with the gap stated, and the proposed action is a corrected comment, never a change to the test's data, cases or name.

Add the slicing rule:
- A slice is about 12k lines of `.cs`, `.props`, `.targets` and `.csproj` files.
- Slices follow folders: a folder under the size is one slice, or is joined with its siblings up to the size.  A folder over the size is split by its sub-folders, and a folder with none is split by its sorted file list into parts under the size.
- `tests/SqlSource.Tests/Snapshot/Examples/` and `tests/SqlSource.Tool.Tests/Fixtures/Projects/` are test data and are in no slice.
- Each slice's brief names its folders and gives its exact file list, which the coverage check compares against.

The measuring command, verbatim, printing lines, files and folder for every folder to depth four:

```bash
find src tests -type f \( -name '*.cs' -o -name '*.props' -o -name '*.targets' -o -name '*.csproj' \) \
  -not -path '*/bin/*' -not -path '*/obj/*' -print0 | xargs -0 wc -l \
  | awk '$2!="total"{n=split($2,p,"/"); for(d=2; d<=4 && d<n; d++){k=p[1]; for(i=2;i<=d;i++) k=k"/"p[i]; L[k]+=$1; F[k]++}}
         END{for(k in L) printf "%7d %4d %s\n", L[k], F[k], k}' | sort -k3
```

- [ ] **Step 4: Update the two pattern briefs and write `## The checker`**

`## Patterns in \`src/\`` and `## Patterns in \`tests/\`` keep their lists.  Each finding now also gives every location and every count it claims as a list a checker can confirm one by one; "about 25 times" becomes the 25 `path:line` entries.  The tests brief keeps fragility (process, clock, repository) as findings of their own.

`## The checker` is new.  A checker is a fresh subagent, given one sweep's report and the files that sweep owns.  It:
- re-reads each flagged spot before acting;
- for a comment finding that holds, applies the fix, reflowing the whole comment paragraph to 120 columns rather than editing a single line;
- for a pattern or fragility finding, confirms each listed location and count and returns the item with the confirmed list, editing nothing;
- for a test-comment gap, corrects the comment and returns the gap as a tech-debt item;
- drops a finding that does not hold, and returns it with its reason;
- edits only its sweep's files, and does not build, test, format or touch git;
- reports files changed, findings applied, findings dropped with reasons, and items returned for tech debt.

- [ ] **Step 5: Verify**

Run: `grep -n 'AGENTS.md accuracy\|docs/architecture\|Not built yet\|README' .claude/skills/reviewing-maintainability/sweeps.md`
Expected: no line asks a sweep to check a document; any match only names a file to read or to leave alone.

Run: `grep -n '^## ' .claude/skills/reviewing-maintainability/sweeps.md`
Expected: exactly `Folder structure`, `Comments, one sweep per slice`, `Patterns in \`src/\``, `Patterns in \`tests/\``, `The checker`, after the `#` title.

Run the measuring command of Step 3 from the repository root.
Expected: one line per folder, including `src/SqlSource.Tool/Describing/Postgres` near 12k lines and `tests/SqlSource.Tests/Generator` near 21k.

---

### Task 2: Rewrite the procedure, and the root `AGENTS.md` section

**Files:**
- Modify: `.claude/skills/reviewing-maintainability/SKILL.md` (whole file, 60 lines)
- Modify: `AGENTS.md:37-39` (section `## Reviewing maintainability`)

**Interfaces:**
- Consumes: the section headings and the measuring command of Task 1.

- [ ] **Step 1: Rewrite the frontmatter `description` and `## Overview`**

`description`: "Use when asked for a maintainability review, cleanup, tidy-up or housekeeping pass over the code of this repository, or when a folder has grown past about twenty files, or comments mention sub-phases and later phases that have since shipped."  The AGENTS.md trigger is gone.

`## Overview`: a review of the code in `src/` and `tests/` (comments, structure, duplicated patterns), done as parallel read-only sweeps, each finding checked and applied by a fresh checker.  It says in one sentence that AGENTS.md files, `docs/architecture/` and the readmes are not reviewed here, and are edited only to follow code the review moves or renames.  Keep "Reading is the test."

- [ ] **Step 2: Rewrite `## The sweeps` as the run's order**

1. Measure with the command in `sweeps.md` and cut the comment slices by its rule.
2. Dispatch at once, each read-only with its brief: folder structure, one comment sweep per slice, patterns in `src/`, patterns in `tests/`.  A brief shared by every comment slice is written once to a file in the scratchpad, and each slice's prompt names that file, its folders and its file list.
3. When a sweep reports, compare its read list with its file list.  Any file missing, partly read, or in its not-read list goes to a new read-only sweep with the same brief, before the slice is checked.
4. Then dispatch a checker (the `## The checker` brief) for that slice, on the slice's files only.  The two pattern reports get a checker too, which edits nothing.  The structure report is checked by the lead: a move by the whole-word count of "Weighing a move", a missing AGENTS.md by reading the evidence the sweep gives.
5. Folder moves, doc path rewrites and the `TD-` items start only after every checker has finished, so no two agents edit one file.

- [ ] **Step 3: Rewrite the table of `## What to do with each kind of finding`**

Remove the rows "A name in an AGENTS.md, an architecture page or a readme that does not exist, or a claim the code contradicts" and "A placeholder for a phase that has shipped".  Keep the rows for a cohesive group, a folder whose traps no AGENTS.md covers, a wrong or stale comment, duplicated logic, and a fragile test.  Add a row: "A test whose assertions do not check what its comment says | Correct the comment now; record the gap in the area's grouped `TD-` item; do not change the test".  Add under the table: a `TD-` item lists only what the checker confirmed, with its confirmed locations and counts.

- [ ] **Step 4: Update `## Executing the moves` and `## Finishing`**

`## Executing the moves` keeps its steps.  Step 3 keeps its list of documents to rewrite, and adds: also rewrite the name of a type, member or test that the review renamed.  It adds that this is the only edit the review makes to those documents.

`## Finishing` keeps validation, the 120-character note and the `VersionPrefix` check.  It adds that the final report and the pull request state each coverage gap exactly, by file, with no rounding of the count.

- [ ] **Step 5: Update `## Common mistakes`**

Keep the deliberate-patterns, "phase 5" (now about comments only), fixing a duplicated helper, proposing a sub-folder from file names, and bare "Review focus N" items.  Add:
- Letting the agent that found a finding apply it.
- Printing several files with one `cat`: past about 30 KB the output is cut to a preview, and the sweep reports a file it did not read.
- An unquoted glob in `zsh`, as in `grep --include=*.cs`, fails with "no matches found"; quote it.
- Changing a test's data, cases or name to make its comment true.

Move the existing `zsh` word-splitting sentence from `## Executing the moves` next to the new glob item.

- [ ] **Step 6: Update the root `AGENTS.md` section**

Replace "A periodic review of folder growth, AGENTS.md accuracy, comments and duplicated patterns follows" with "A periodic review of the code's folder growth, comments and duplicated patterns follows".  The rest of the sentence stays.

- [ ] **Step 7: Verify**

Run: `grep -n -i 'agents.md accuracy\|stale\|placeholder\|architecture page' .claude/skills/reviewing-maintainability/SKILL.md AGENTS.md`
Expected: no match asks the review to check a document; "stale" appears only about comments.

Run: `grep -n 'sweeps.md\|The checker\|one sweep per slice' .claude/skills/reviewing-maintainability/SKILL.md`
Expected: each heading named in Task 1's Interfaces is referred to by its exact name.

- [ ] **Step 8: Validate and commit Tasks 1 and 2**

Run: `./pre-commit-validation.sh`
Expected: the summary lists every check as `passed`.

```bash
git add .claude/skills/reviewing-maintainability/SKILL.md .claude/skills/reviewing-maintainability/sweeps.md AGENTS.md
git commit -m "Focus the maintainability review on the code, and check every finding with a fresh agent"
```

---

### Task 3: Dry-run the skill

**Files:**
- None changed, unless the dry run finds a gap; then the file it names.

- [ ] **Step 1: Dispatch a fresh read-only subagent to plan a run**

Give it the repository root and ask it to read only `.claude/skills/reviewing-maintainability/SKILL.md` and `sweeps.md`, then, without dispatching anything or editing anything, produce: the slices it would cut, each with its folders, file count and line count; the order of dispatch, checks and moves; and every point where the skill left it unsure what to do.

- [ ] **Step 2: Check its answer**

Expected:
- every slice is at or under about 12k lines, with `tests/SqlSource.Tests/Generator/` split by file list and `src/SqlSource.Tool/Describing/Postgres/` alone or split by sub-folder;
- `Snapshot/Examples/` and `Fixtures/Projects/` are in no slice;
- the slices' files add up to the measuring command's totals for `src` and `tests`, less the test data;
- no checker is dispatched before its slice's coverage is complete, and no move before every checker has finished;
- no sweep checks a document.

Fix each "unsure" point that the spec answers, in the file it names.  Bring any the spec does not answer to the user.

- [ ] **Step 3: Validate and commit any fixes**

Run: `./pre-commit-validation.sh`
Expected: every check `passed`.

```bash
git add .claude/skills/reviewing-maintainability/
git commit -m "Settle what the dry run of the maintainability review found unclear"
```
