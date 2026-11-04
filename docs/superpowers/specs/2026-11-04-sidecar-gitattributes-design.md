# Sidecars in git: design

A small change, to head off future problems with sidecars in version control.  Nothing has gone wrong yet.

## Intent

Tell users how to set up git for `.sql.json` sidecars, and correct the advice for a sidecar with a merge conflict.  Users don't read sidecars until they're chasing a problem, so pull request diffs should keep sidecars out of the way.

## Goals

- Recommend `*.sql.json linguist-generated=true` in `.gitattributes`, which collapses sidecar diffs on GitHub.
- Say why sidecars get no merge rule.  Each entry carries the hash of its query, so a text merge that git completes cleanly is correct, and the build catches an entry left behind (SQLSRC244).  A lock-file rule (`-merge` or `binary`) would turn every change made on two branches at once into a conflict, and would leave one side's file readable with no markers, so the other side's re-describe after a schema change would be lost silently.
- Correct the conflict advice.  "Take either side of the conflict or none" is wrong today: a side you take is readable, so `describe` skips its current entries, and a schema change that only the other side's `--force` picked up is lost.  The new advice is to leave the conflict markers in and run `dotnet sqlsource describe`, which can't read the file and so describes every query in it again.  If a side has already been taken, run `dotnet sqlsource describe --force <the .sql file>`.

## Non-goals

- No `eol` rule for users.  `--check` compares content, and the query hash ignores line endings.
- The tool does not write or check `.gitattributes`.

## Approaches

1. **Manual only:** an "In git" part of the sidecar section of `docs/manual/tool.md`, the corrected advice in SQLSRC237, and the attribute in this repository's `.gitattributes`.
2. **Manual plus a README line:** the same, plus a sentence where `README.md` says to commit the sidecars.
3. **The tool writes it:** `describe` adds the line to `.gitattributes`, or warns when it is missing.  Turned down: it touches a file the user owns, and there is no setup command to put it in.

**Chosen: 1 and 2 together**, so the README's common path mentions it and the manual holds the detail.

## The changes

- `docs/manual/tool.md`, the sidecar section: a short part on git with the `.gitattributes` snippet, why there is no merge rule, and what to do after a conflict.  The existing sentence "A sidecar with a merge conflict is no exception…" points to it.
- `docs/manual/diagnostics.md`, SQLSRC237: replace "take either side of the conflict or none" with the corrected advice, and link to the new part.
- `README.md`: one sentence beside "Commit the sidecars with the project", with the `.gitattributes` line and an absolute link to the new manual part, without HTML or `<a id>` (packed readme rules).
- `.gitattributes` of this repository: add `linguist-generated=true` to the existing `*.sql.json` line.

## Left to the builder

- The heading and anchor of the new manual part, and the exact wording, in the manual's style.
- Whether `src/SqlSource.Tool/README.md` needs the line too.  It should only if it already covers committing sidecars.
