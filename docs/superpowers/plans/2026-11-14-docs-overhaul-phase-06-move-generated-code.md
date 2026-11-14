# Docs overhaul phase 6, move-generated-code: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `site/generator.md` is gone.  Its text, and the sections of `site/settings.md` on models, members, collections, interfaces and exceptions, stand word for word on ten pages of `site/reference/`, with one section each on `reference/type-mapping.md` and `guides/dapper.md` and a limit each on the two database `query-methods.md` pages; the nine known-issue bullets become eight limits and the second text of MP-09; C-02 is made at `settings.md` 953; every link and reference names the new page.

**Architecture:** A verbatim move, done by a script in the scratchpad.  It cuts `site/generator.md` by the baseline's lines (the working tree has the baseline's lines: earlier phases changed only link targets and two words) and `site/settings.md` by `<a id>` (its lines are the baseline's, less 2 before line 430 and plus 2 after line 525), raises or lowers each heading to the level of spec step 2, and re-bases every relative link by resolving it to a repository path and writing it again from the new file.  The same resolver retargets every reference to `site/generator.md` and to the 12 moved anchors of `site/settings.md`, in `site/`, both readmes (site URLs) and every other tracked file (Markdown links and bare `site/...` paths), keyed on page and anchor together.  New text (12 h1, `#generator`, `#attributes` with its table, the sentence of `model-properties.md`, five `## Limits`, eight limit h3, eight troubleshooting entries) and the `position` mends are written by hand.

**Tech Stack:** Markdown, docfx (`tools/build-site.sh`), xUnit v3 (`tests/SqlSource.Repository.Tests`), the epic's checker (`check/check.sh`), Python 3 for the one-off move script.

**Spec:** `docs/superpowers/specs/2026-11-12-docs-overhaul-phase-06-move-generated-code-design.md`, under the outline `docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md` and the map `docs/superpowers/specs/2026-11-12-docs-overhaul/content-map.md`.

## Global Constraints

- Moved text changes no word but C-02 and the `position` mends.  New text is plain language (ASD-STE100 as a guideline).
- No `--prerelease` and no sentence about previews (owner, p01 Q1/Q4).
- A fenced block is byte for byte the same, with its info string; a block inside a bullet keeps the bullet's indent.
- `AGENTS.md`: at most 8,192 bytes and 80 words a rule (`AgentsFileTests`).
- Every commit passes `./pre-commit-validation.sh`; `./format.sh` first.

## Where the tree differs from the spec

- `git diff 28e20e3c -- site/generator.md` holds only retargeted links and two word changes of earlier phases: line 184's "[parameters and tokens]" became "[tokens]" (phase 3), and line 699's "[known issues]" became "[limits]" with its link to `databases/postgresql/query-methods.md#limit-connection-string-repository` (phase 4).  So the spec's g 702 needs no new target; it is re-based alone.  Those units have no baseline text and are named `@site/generator.md:<line>` in the ledger.
- `site/settings.md` differs by phase 1's table, phase 1's paragraph after line 430 and phase 2's sentence after line 525, and retargeted links.  None of them is in a range of phase 6.
- Besides "the generator page", five link texts outside `generator.md` say "known issue(s)" of a limit that this phase makes (`diagnostics.md` SQLSRC208, `reference/describing.md` 26, three on the database pages), and so do the four inside it (g 38, 850, 1097, 1121, 1144) and the two of `README.md`.  Phases 4 and 5 mended such words to "a limit" or "the limit", with `position`; this phase does the same.

## Review Focus

- A link in moved text whose anchor now stands on another page: `UserDocsLinksTests` and docfx fail on it.
- A reference outside `site/` to `site/generator.md`: `InboundSiteReferenceTests`; the done-when greps.
- A limit with no troubleshooting entry: `SiteStructureTests`.
- The two `limit-wrapped-connection`: every link to the query method's limit goes to `reference/query-methods.md`.

---

### Task 1: The move

**Files:**
- Create: `site/reference/{members,models,model-properties,query-methods,return-types,repositories,connection-methods,collection-methods,exceptions,attributes,type-mapping}.md`, `site/guides/dapper.md`
- Modify: `site/settings.md` (cut, C-02 rides with its section), both database `query-methods.md`, `site/guides/troubleshooting.md`, every other `site/` page that links the moved anchors, `site/reference/toc.yml`, `site/guides/toc.yml`, `README.md`, the files of spec step 7's last table, `site/AGENTS.md`
- Delete: `site/generator.md`
- Test: `tests/SqlSource.Repository.Tests/Documentation/SiteStructureTests.cs` (`generator.md` leaves `PagesNotYetMoved`)

- [ ] **Step 1:** Write the move script: page specs as lists of (source, line range, heading level) from spec step 2, the anchor map (45 of `generator.md`, 12 of `settings.md`, the new limits), a link resolver for Markdown links, bare `site/` paths and site URLs, and the `#known-issues` table of spec step 7.  Run it; it writes the twelve pages, adds g 1188 and 1189 to the database pages, cuts `settings.md`, and retargets every reference.
- [ ] **Step 2:** Write the new text of spec step 6 and the limits of step 4 by hand; make C-02; mend the "generator page", "known issues", "above", "below" and "this page" hits with the fewest words.
- [ ] **Step 3:** Sidebars, `site/AGENTS.md`, `SiteStructureTests`; `git rm site/generator.md`.  `dotnet test tests/SqlSource.Repository.Tests` passes; `tools/build-site.sh` exits 0 with no warning; format; validate; commit.

### Task 2: Ledger and worksheets

**Files:**
- Create: `docs/superpowers/specs/2026-11-12-docs-overhaul/ledger/phase-06.tsv`, `merges/MP-01.md` to `MP-05.md`, `MP-07.md`, `MP-12.md`
- Modify: `merges/MP-09.md`, `MP-10.md`

- [ ] **Step 1:** Write the ledger of spec "The ledger", base = the commit the branch started from.
- [ ] **Step 2:** `check.sh move 06` leaves over only the `move-split`, `corrected` and `position` rows; `check.sh coverage` passes.
- [ ] **Step 3:** Write the worksheets; validate; commit.

### Task 3: Done when

- [ ] Run every grep and count of the spec's "Done when"; fix what differs; `./pre-commit-validation.sh`.
