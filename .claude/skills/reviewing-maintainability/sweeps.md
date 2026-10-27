# Briefs for the review sweeps

Each sweep is a read-only subagent.  Every brief starts with: the repository root, that nothing may be modified, and that `bin/`, `obj/` and any `.claude/worktrees/` folder below the root are ignored.  The sweep reads the AGENTS.md files that cover its area first and reports nothing they call deliberate; it reads them for that and does not check them.  It reads a file with the Read tool, never several files through `cat` in one shell command: shell output past about 30 KB is cut to a preview, and the rest is never seen.  A long file read in several calls whose ranges cover it all counts as read in full.  Each report gives `path:line`, the evidence, and the proposed text or action.  A comment sweep ends with the list of files read in full, and a separate list of files not read in full: a file read only in part, or not read because the sweep ran out of room.  The structure and pattern sweeps read selectively, and end with the folders they covered.

## Folder structure

Give the folder sizes (files and lines, from `find` and `wc`).  For each folder over about twenty of those files: `ls` it, read the first thirty lines of each file for the type and its dependencies, group by cohesion, and propose a sub-folder only for a group of four or more files with a crisp name and few references to the rest of the folder.  For each proposal: the files, the count of `using` lines the move implies (a whole-word grep over `src` and `tests`; a file that stays in the parent folder counts, since a parent namespace does not see a child), the AGENTS.md lines that name the files, and a move-or-leave recommendation, ranked by value for risk.  State the constraints: namespaces follow folders; `tests/SqlSource.Tests/Generator/` is compiled whole by `tests/SqlSource.Tests.RoslynFloor`; the examples and fixtures are bound by their `.csproj`.  Ask whether any non-C# file moves with a group.

Then name each folder with a non-obvious constraint that no AGENTS.md covers: a path that is a public URL, a file a `.csproj` names, a layout a test enforces.  For each: the evidence, with `path:line`, and the rules an AGENTS.md there would hold.

## Comments, one sweep per slice

Read every `.cs`, `.props`, `.targets` and `.csproj` file of the slice in full.  Report comments that are incorrect (the code does otherwise; a name that does not exist; a `<param>` that disagrees with the signature), outdated (future work now done or abandoned; a tech-debt id with no file in `docs/tech-debt/`), needlessly verbose (restates the code, repeats a sibling comment on the same member), or mismatched (a summary copied from a sibling type).  Say that invariants and contracts are documented on purpose and stay.  Comments inside string literals that are test input, SQL or C# given to the generator, are not findings.  A comment's "the tool will", "phase 5" or "not yet" is checked against the phase table of the epic outline, `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`, before it is called stale: several are still true.  A tech-debt id is checked against `ls docs/tech-debt`.  A bare `// Review focus N.` or "this sub-phase" names no plan the reader can follow, and is a finding.  Propose the corrected text, or "delete".  Verify before reporting.

A test whose comment says it checks what its assertions do not is reported as a tech-debt candidate, with the gap stated.  The proposed action is a corrected comment, never a change to the test's data, cases or name.

**Slices.**  A slice holds up to about 12k lines of `.cs`, `.props`, `.targets` and `.csproj` files, and never more than 13k.  Slices follow folders.  A folder of up to 13k lines is one slice, or is joined with its siblings, the folders of the same parent taken in sorted order, while the slice stays at or under 12k.  A folder over 13k is split by its sub-folders, with its own files counted as the first sibling, and a folder with no sub-folders is split by its files, taken in path order, into parts of up to 12k.  `tests/SqlSource.Tests/Snapshot/Examples/` and `tests/SqlSource.Tool.Tests/Fixtures/Projects/` are test data and are in no slice.  Each slice's brief names its folders and gives its exact file list, which the coverage check compares against; the sweep's scope is the listed files and nothing else, even where a listed folder holds test data.  Measure with this command from the repository root.  It leaves out the test data and prints the lines, files and path of every folder at every depth, `src` and `tests` included, and a row marked `(own files)` for the files directly in a folder that also has sub-folders, in tree order, each folder followed by its own files and then its sub-folders:

```bash
find src tests -type f \( -name '*.cs' -o -name '*.props' -o -name '*.targets' -o -name '*.csproj' \) \
  -not -path '*/bin/*' -not -path '*/obj/*' \
  -not -path 'tests/SqlSource.Tests/Snapshot/Examples/*' -not -path 'tests/SqlSource.Tool.Tests/Fixtures/Projects/*' \
  -print0 | xargs -0 wc -l \
  | awk '{l=$1; sub(/^ *[0-9]+ /, ""); if ($0 == "total") next; n=split($0, p, "/"); k=p[1]
         for (i=1; i<n; i++) { if (i>1) k=k"/"p[i]; L[k]+=l; F[k]++ } OL[k]+=l; OF[k]++ }
         END { for (k in L) { s=k; gsub("/", " ", s); printf "%s\t%7d %4d %s\n", s, L[k], F[k], k }
               for (k in OL) if (OF[k] < F[k]) { s=k; gsub("/", " ", s); printf "%s \t%7d %4d %s (own files)\n", s, OL[k], OF[k], k } }' \
  | LC_ALL=C sort | cut -f2-
```

A slice's file list comes from the same filters, rooted at each of its folders, with `-maxdepth 1` for a part that is a folder's own files:

```bash
find <folder> -type f \( -name '*.cs' -o -name '*.props' -o -name '*.targets' -o -name '*.csproj' \) \
  -not -path '*/bin/*' -not -path '*/obj/*' \
  -not -path 'tests/SqlSource.Tests/Snapshot/Examples/*' -not -path 'tests/SqlSource.Tool.Tests/Fixtures/Projects/*' | LC_ALL=C sort
```

## Patterns in `src/`

An item already in `docs/tech-debt/README.md` is not a new finding, though a report may say that its scope is wrong.  Look for duplicated logic in two places, dead members, types over about four hundred lines or methods over about eighty with several responsibilities, conventions that differ across siblings for one concept, parallel lists maintained by hand with no test pinning them, parameter lists of seven or more, boolean cascades, order dependencies nothing enforces, mutable statics.  For each: `path:line`, the call sites, a one-line fix, and every location and count it claims as a list a checker can confirm one by one: "about 25 times" is the 25 `path:line` entries.  Eight to twenty real findings, ranked; say which folders came up clean.

## Patterns in `tests/`

Read the test projects' `.csproj` files first.  An item already in `docs/tech-debt/README.md` is not a new finding.  Look for the same arrange code in several classes, fakes of one interface written twice, copy-pasted bodies that should be theories or theories whose rows change the arrangement, inconsistent naming or assertion style, classes over about six hundred lines that mix subjects, magic strings repeated across classes, helpers duplicated between the two test projects, and what `tests/SqlSource.Tests.RoslynFloor`, which compiles the files of `tests/SqlSource.Tests/Generator/` again through a `<Compile Include>`, blocks from being shared.  Report separately, as fragility rather than duplication, tests that depend on the process, the working directory, test order, sleeps, the real clock or programs on the path with no skip guard.  Same output shape as the `src/` sweep, every location and count listed.

## The checker

A checker is a fresh subagent, given the reports of one slice, a re-sweep's included, and the files of that slice; it is never an agent that wrote one of them.  A pattern checker is given one pattern report and owns no files.  It re-reads each flagged spot before acting, then:

- for a comment finding that holds, applies the fix, reflowing the whole comment paragraph to 120 columns rather than editing a single line;
- for a pattern or fragility finding, confirms each listed location and count and returns the item with the confirmed list, editing nothing;
- for a test-comment gap, corrects the comment and returns the gap as a tech-debt item;
- drops a finding that does not hold, or the part of it that does not, and returns it with its reason; a finding applied in part counts as applied, with its dropped part listed;
- merges a finding that two reports of the slice both give, and counts it once.

It edits only its sweep's files, and does not build, test, format or touch git.  It reports the files changed, the findings applied, the findings dropped with their reasons, and the items returned for tech debt.
