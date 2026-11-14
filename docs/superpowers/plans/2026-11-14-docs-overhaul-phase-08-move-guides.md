# Docs overhaul phase 8, move-guides: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `site/recipes.md` is gone and its recipes stand word for word on twelve new guides, two existing guides and two new `start/` pages; the 17 terms of `site/concepts.md` stand in their reference sections and the page is parked at `site/start/concepts.md`; the README's scenarios, FAQ, libraries and type-mapping sections stand on the site with one pointer under each README heading; the root of `site/` holds `index.md` and `diagnostics.md` alone.

**Architecture:** A verbatim move, done by a Python script in the scratchpad.  It builds each page from pieces cut by `<a id>` from the working `site/recipes.md`, by working line from `README.md` (baseline line minus 2), and by term from `site/concepts.md`, and re-bases every link by resolving it to a repository path and anchor, applying the anchor map (recipe anchors to their guides, term anchors to section 6.3 of the map, the README's own headings to spec step 1's table), and writing it again relative to the destination.  The same resolver retargets every other reference to `recipes.md`, `concepts.md` and `recipes.html`.  New text (titles, new headings, the 14 pointers, the sentence of `D/other.md`), C-07, C-08 and the `position` mends are written by hand.

**Tech Stack:** Markdown, docfx (`tools/build-site.sh`), xUnit v3 (`tests/SqlSource.Repository.Tests`), the epic's checker (`check/check.sh`), Python 3 for the one-off script.

**Spec:** `docs/superpowers/specs/2026-11-12-docs-overhaul-phase-08-move-guides-design.md`, under the outline `docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md` and the map `docs/superpowers/specs/2026-11-12-docs-overhaul/content-map.md`.

## Global Constraints

- Moved text changes no word but C-07, C-08 and the `position` mends.  New text is plain language (ASD-STE100 as a guideline) and states facts: no status words, no `--prerelease`.
- A fenced block is byte for byte the same, with its info string; a block inside a bullet keeps the bullet's indent.  `recipes.md` 595 keeps its bare `sql` block.
- A link to a file outside `site/` stays `https://github.com/mbcrawfo/SqlSource/blob/main/<path>`; every other link on the site is relative.
- `AGENTS.md`: at most 8,192 bytes and 80 words a rule (`AgentsFileTests`).
- Every commit passes `./pre-commit-validation.sh`; `./format.sh` first.

## Where the tree differs from the spec

- `README.md` lines are the baseline's less 2 from line 9 on (phase 1 took out line 7 and its blank).  `site/recipes.md` is 897 lines; pieces are cut by `<a id>`, not by line.
- The guides groups "Projects" and "Other ways to use your SQL" hold one page each, by the owner's interim exception (p06 Q1, p07 Q1) written into `site/guides/AGENTS.md`; this phase fills both groups and drops the exception clause.
- `G/troubleshooting.md#at-run-time` holds limit entries already; the new h3 `invalidcastexception` goes at the end of that h2.
- `R/generator-parameters.md` has no `## Limits`, so `three-kinds-of-parameter` is the last h2 of the page.
- `D/index.md#supported-versions`: the README's sentences stand first, directly under the heading, since MP-23's row lists `README.md` first.

## Review Focus

- A link re-based to a dead anchor or page: `UserDocsLinksTests`, docfx.
- A sample block altered or a `sql` info string lost: `UserDocsSamplesTests`, `check.sh move 08`.
- A reference outside `site/` still naming `site/recipes.md` or root `site/concepts.md`: `InboundSiteReferenceTests` and the done-when greps.
- A unit placed twice, or under the wrong heading: `check.sh move 08`, and the no-loss reviewer.
- A twin's comment naming an anchor that does not hold its sample: the accuracy reviewer.

---

### Task 1: The move

**Files:**
- Create: `site/guides/{several-databases,many-rows,sync-and-streaming,repository-connection,naming-and-placement,custom-types,json,nodatime-and-spatial,dynamic-sql,lists,several-projects,other-databases}.md`, `site/start/{transactions,testing}.md`
- Move: `site/concepts.md` to `site/start/concepts.md`
- Modify: `site/guides/{dapper,folders-and-files,report-a-bug,troubleshooting}.md`; the twelve reference pages of spec step 3; `R/cli.md`, `R/attributes.md`, `R/msbuild.md`, `D/index.md`, `D/other.md`; `site/index.md`, `site/diagnostics.md`; both `toc.yml`; `README.md`; `docs/architecture/README.md`
- Delete: `site/recipes.md`

- [ ] **Step 1:** Write the move script: the page specs of spec step 1 (heading, anchor, pieces in order), the README pieces of step 2, the term placements of step 3, the anchor map, and the resolver for Markdown links, bare `site/` paths and site URLs.  Run it.
- [ ] **Step 2:** By hand: page titles, new headings, C-07 (both copies), C-08 (both copies), the four closing sentences of step 3 dropped on the reference copies, the sentence of `D/other.md`, the README pointers of step 5, the README links of step 6, the `position` mends.
- [ ] **Step 3:** Sidebars; `git mv` and `git rm`.  `dotnet test tests/SqlSource.Repository.Tests` passes; `tools/build-site.sh` has no warning.

### Task 2: Twins, tests and `AGENTS.md`

**Files:**
- Modify: the files of spec step 7; `tests/SqlSource.Repository.Tests/Documentation/SiteStructureTests.cs`; the `AGENTS.md` files of spec step 8

- [ ] **Step 1:** The words of spec step 7, inside each file's line width.
- [ ] **Step 2:** `SiteStructureTests`: remove `PagesNotYetMoved`; the root allows `index.md` and `diagnostics.md` alone.
- [ ] **Step 3:** The `AGENTS.md` words of spec step 8, and drop the one-page-group exception of `site/guides/AGENTS.md`; check sizes.
- [ ] **Step 4:** Repository tests pass; format; validate; commit Tasks 1 and 2.

### Task 3: Ledger and worksheets

**Files:**
- Create: `docs/superpowers/specs/2026-11-12-docs-overhaul/ledger/phase-08.tsv`; `merges/MP-{13,14,15,16,17,18,20,21,22}.md`
- Modify: `merges/MP-{10,11,12,19,23}.md`

- [ ] **Step 1:** Write the ledger of spec "The ledger", base = `4f244433`.
- [ ] **Step 2:** `check.sh move 08` leaves over only what spec "The ledger" lists; `check.sh coverage` passes.
- [ ] **Step 3:** Write the worksheets; validate; commit.

### Task 4: Done when

- [ ] Run every grep and count of the spec's "Done when"; fix what differs; `./pre-commit-validation.sh`.
