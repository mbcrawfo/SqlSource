# Briefs for the review sweeps

Each sweep is a read-only subagent.  Every brief starts with: the repository root, that nothing may be modified, and that `.claude/worktrees/`, `bin/` and `obj/` are ignored.  The sweep reads the AGENTS.md files that cover its area first and reports nothing they call deliberate; it reads them for that and does not check them.  It reads each file with the Read tool, one file per call, and never prints several files through `cat` in one shell command: shell output past about 30 KB is cut to a preview, and the rest is never seen.  Each report gives `path:line`, the evidence, and the proposed text or action.  It ends with the list of files read in full, and a separate list of files not read in full: a file read only in part, or not read because the sweep ran out of room.

## Folder structure

Give the folder sizes (files and lines, from `find` and `wc`).  For each folder over about twenty files: `ls` it, read the first thirty lines of each file for the type and its dependencies, group by cohesion, and propose a sub-folder only for a group of four or more files with a crisp name and few references to the rest of the folder.  For each proposal: the files, the count of `using` lines the move implies (a whole-word grep over `src` and `tests`; a file that stays in the parent folder counts, since a parent namespace does not see a child), the AGENTS.md lines that name the files, and a move-or-leave recommendation, ranked by value for risk.  State the constraints: namespaces follow folders; `tests/SqlSource.Tests/Generator/` is compiled whole by `tests/SqlSource.Tests.RoslynFloor`; the examples and fixtures are bound by their `.csproj`.  Ask whether any non-C# file moves with a group.

Then name each folder with a non-obvious constraint that no AGENTS.md covers: a path that is a public URL, a file a `.csproj` names, a layout a test enforces.  For each: the evidence, with `path:line`, and the rules an AGENTS.md there would hold.

## Comments, one sweep per slice

Read every `.cs`, `.props`, `.targets` and `.csproj` file of the slice in full.  Report comments that are incorrect (the code does otherwise; a name that does not exist; a `<param>` that disagrees with the signature), outdated (future work now done or abandoned; a tech-debt id with no file in `docs/tech-debt/`), needlessly verbose (restates the code, repeats a sibling comment on the same member), or mismatched (a summary copied from a sibling type).  Say that invariants and contracts are documented on purpose and stay.  Comments inside string literals that are test input, SQL or C# given to the generator, are not findings.  Propose the corrected text, or "delete".  Verify before reporting.

A test whose comment says it checks what its assertions do not is reported as a tech-debt candidate, with the gap stated.  The proposed action is a corrected comment, never a change to the test's data, cases or name.

**Slices.**  A slice is about 12k lines of `.cs`, `.props`, `.targets` and `.csproj` files.  Slices follow folders: a folder under the size is one slice, or is joined with its siblings up to the size.  A folder over the size is split by its sub-folders, and a folder with none is split by its sorted file list into parts under the size.  `tests/SqlSource.Tests/Snapshot/Examples/` and `tests/SqlSource.Tool.Tests/Fixtures/Projects/` are test data and are in no slice.  Each slice's brief names its folders and gives its exact file list, which the coverage check compares against.  Measure with this command from the repository root; it prints lines, files and folder for every folder to depth four:

```bash
find src tests -type f \( -name '*.cs' -o -name '*.props' -o -name '*.targets' -o -name '*.csproj' \) \
  -not -path '*/bin/*' -not -path '*/obj/*' -print0 | xargs -0 wc -l \
  | awk '$2!="total"{n=split($2,p,"/"); for(d=2; d<=4 && d<n; d++){k=p[1]; for(i=2;i<=d;i++) k=k"/"p[i]; L[k]+=$1; F[k]++}}
         END{for(k in L) printf "%7d %4d %s\n", L[k], F[k], k}' | sort -k3
```

## Patterns in `src/`

An item already in `docs/tech-debt/README.md` is not a new finding, though a report may say that its scope is wrong.  Look for duplicated logic in two places, dead members, types over about four hundred lines or methods over about eighty with several responsibilities, conventions that differ across siblings for one concept, parallel lists maintained by hand with no test pinning them, parameter lists of seven or more, boolean cascades, order dependencies nothing enforces, mutable statics.  For each: `path:line`, the call sites, a one-line fix, and every location and count it claims as a list a checker can confirm one by one: "about 25 times" is the 25 `path:line` entries.  Eight to twenty real findings, ranked; say which folders came up clean.

## Patterns in `tests/`

Read the test projects' `.csproj` files first.  An item already in `docs/tech-debt/README.md` is not a new finding.  Look for the same arrange code in several classes, fakes of one interface written twice, copy-pasted bodies that should be theories or theories whose rows change the arrangement, inconsistent naming or assertion style, classes over about six hundred lines that mix subjects, magic strings repeated across classes, helpers duplicated between the two test projects, and what the compile-three-times arrangement blocks from being shared.  Report separately, as fragility rather than duplication, tests that depend on the process, the working directory, test order, sleeps, the real clock or programs on the path with no skip guard.  Same output shape as the `src/` sweep, every location and count listed.

## The checker

A checker is a fresh subagent, given one sweep's report and the files that sweep owns; it is never the agent that wrote the report.  It re-reads each flagged spot before acting, then:

- for a comment finding that holds, applies the fix, reflowing the whole comment paragraph to 120 columns rather than editing a single line;
- for a pattern or fragility finding, confirms each listed location and count and returns the item with the confirmed list, editing nothing;
- for a test-comment gap, corrects the comment and returns the gap as a tech-debt item;
- drops a finding that does not hold, or the part of it that does not, and returns it with its reason.

It edits only its sweep's files, and does not build, test, format or touch git.  It reports the files changed, the findings applied, the findings dropped with their reasons, and the items returned for tech debt.
