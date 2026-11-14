# Docs overhaul phase 5, move-tool: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `site/tool.md` becomes five pages of the group "The sqlsource tool" in `site/reference/`, with parts on `reference/msbuild.md` and `guides/report-a-bug.md`, and the three recipes of the tool leave `site/recipes.md` for `guides/ci.md`, `guides/describe-on-save.md` and `start/keep-up-to-date.md`; the 8 known-issue bullets become 6 limits, one second text of MP-08 and one pointer that goes; `CliReferenceTests` holds `reference/cli.md` to the tool's own commands, options and variables, and `EditorRecipeTests` reads the VS Code task from its guide.

**Architecture:** A verbatim move.  A script in the scratchpad cuts `site/tool.md` and `site/recipes.md` 125-254 by line ranges (the working tree's `tool.md` is the baseline's plus two lines that phase 2 inserted after baseline line 166, so a baseline line `n >= 167` is line `n + 2` today; `recipes.md` has the baseline's lines), re-bases each relative link to the new folder, sends each `#anchor` of `tool.md` to the page that now holds it, and retargets every link and reference to `tool.md` and to the three recipes in `site/`, both readmes, the issue template and the rest of the repository.  New text is written by hand: nine h1, the openings of `cli.md`, `describing.md#describe` and `bundles.md`, the six option rows, two h3 of `guides/ci.md`, three `## Limits`, six limit h3 and six troubleshooting entries.  Every unit gets a row of `ledger/phase-05.tsv`.

**Tech Stack:** Markdown, docfx (`tools/build-site.sh`), xUnit v3 with Shouldly (`tests/SqlSource.Repository.Tests`), System.CommandLine (the tool's `Cli.BuildCommands`), the epic's checker (`check/check.sh`).

**Spec:** `docs/superpowers/specs/2026-11-12-docs-overhaul-phase-05-move-tool-design.md`, under the outline `docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md` and the map `docs/superpowers/specs/2026-11-12-docs-overhaul/content-map.md`.

## Global Constraints

- Moved text changes no word but C-01, C-03, the last sentence of `tool.md` 634, the heading levels of spec step 2 and the `position` mends of step 6.  New text is plain language (ASD-STE100 as a guideline).
- No `--prerelease` and no sentence about previews (owner, p01 Q1/Q4).
- The VS Code task (`recipes.md` 206-235) stays byte-identical, one block at the left margin opened by exactly `` ```jsonc ``.
- `AGENTS.md`: at most 8,192 bytes and 80 words a rule (`AgentsFileTests`); `src/SqlSource.Tool/AGENTS.md` ends at 8,174 bytes or less.
- Every commit passes `./pre-commit-validation.sh`; `./format.sh` first.

## Where the tree differs from the spec

- `git diff 28e20e3c -- site/tool.md site/recipes.md` holds only earlier phases' rows: phase 1's install paragraph and commands (`@site/tool.md:20`, 24, 29 at the base), phase 2's paragraph `@site/tool.md:168`, and phases 3 and 4's retargeted links.  No feature work.  Phase 1 left no "neutral sentence": it removed the status clause (owner), so `install-the-tool` holds no sentence about `--prerelease`.
- Phase 3 already mended the last sentence of `tool.md` 634 to "See [a file in two projects](reference/sidecars.md#limit-file-in-two-projects)."  That sentence goes, as the spec says of its old form.
- `UserDocsReadingTests.Exists_OfTheRepository_NeedsTheCaseOfTheFile` checks `site/tool.md` exists; it takes `site/index.md` (and `site/Index.md` for the false case).
- The spec's line numbers are the baseline's.  In the working tree, baseline line `n >= 167` is line `n + 2`: the spec's 313 ("Editors", linking `#known-issues`) is line 315 today.

## Review Focus

- A link inside moved text to an anchor of `tool.md` that now stands on another page: `UserDocsLinksTests` fails on it, and the script maps every anchor by the spec's table.
- A reference outside `site/` to `site/tool.md`: `InboundSiteReferenceTests` fails on it; the done-when grep lists any left.
- A new option of the tool that no row of `reference/cli.md` names: `CliReferenceTests` fails; checked by taking the `--scrub` and `SQLSOURCE_DEBUG` rows out by hand.
- The VS Code task moved inside a list or with a changed byte: `EditorRecipeTests` reads it from the guide.
- A limit with no troubleshooting entry: `SiteStructureTests` fails.

---

### Task 1: The move

**Files:**
- Create: `site/reference/{cli,describing,connections,msbuild,bundles}.md`, `site/guides/{report-a-bug,ci,describe-on-save}.md`, `site/start/keep-up-to-date.md`
- Modify: `site/reference/sidecars.md`, `site/recipes.md`, `site/guides/troubleshooting.md`, `site/diagnostics.md` (C-03 and links), every other `site/` page that links `tool.md`, the three `toc.yml`, `README.md`, `src/SqlSource.Tool/README.md`, `.github/ISSUE_TEMPLATE/bug-report.yml`, the files of spec step 8, the `AGENTS.md` files of spec step 11
- Delete: `site/tool.md`
- Test: `tests/SqlSource.Repository.Tests/Watching/EditorRecipeTests.cs` (path and two comments), `Documentation/SiteStructureTests.cs` (`tool.md` leaves `PagesNotYetMoved`), `Documentation/UserDocsReadingTests.cs` (the `Exists` fact)

- [ ] **Step 1:** Write the move script in the scratchpad: page specs as lists of baseline ranges (spec step 2 table), headings raised to `##` as step 2 lists, new text inserted by marker, link re-basing by folder, and the anchor map of step 6.  Run it; it writes the nine pages and the new `sidecars.md`, and cuts `recipes.md` 125-254.
- [ ] **Step 2:** Write the new text of spec step 3 and the limits of step 4 by hand; apply C-01 and C-03; retarget the links of step 6's second table; mend the "This page|above|below|tool page" hits with `position`.
- [ ] **Step 3:** Retarget the links of `site/` (spec step 6, `git grep -n -E "\]\((\.\./)*tool\.md" -- site` prints nothing after), the readmes and template (step 7), the references of step 8, the sidebars (step 9), the `AGENTS.md` lines (step 11); `git rm site/tool.md`.
- [ ] **Step 4:** Change the three tests above.  Run `dotnet test tests/SqlSource.Repository.Tests` — all pass.
- [ ] **Step 5:** `tools/build-site.sh` exits 0 with no warning; `./format.sh`; `./pre-commit-validation.sh`; commit.

### Task 2: `CliReferenceTests` and the six option rows

**Files:**
- Create: `tests/SqlSource.Repository.Tests/Documentation/CliReferenceTests.cs`
- Modify: `site/reference/cli.md#options`, `tests/SqlSource.Repository.Tests/Documentation/AGENTS.md`, `tests/README.md` row 13

**Interfaces:**
- Consumes: `SqlSource.Tool.Cli.BuildCommands()` (internal, `Command`), `ConnectionVariables.NamedPrefix`, `RepositoryRoot.Path`.

- [ ] **Step 1: Write the tests.**  `Commands_EachHasASynopsisLine`: each `Subcommands` of the root not `Hidden` has a line of the fenced `text` block under `<a id="command-reference">` that starts `dotnet sqlsource <name>`.  `Options_EachHasARow`: the root's and each command's `Options` not `Hidden` each have a table row between `<a id="options"></a>` and the next heading whose first cell starts with `` `<Name> `` (a code span opening with the name).  `Variables_EachHasARow`: every literal matching `"SQLSOURCE_[A-Z0-9_]+"` in `src/SqlSource.Tool/**/*.cs`, `bin` and `obj` aside, plus `` `SQLSOURCE_CONNECTION_<NAME>` `` from `ConnectionVariables.NamedPrefix`, has a row under `<a id="environment-variables"></a>` whose first cell is the name in code font.  Each finder asserts it found at least one item.  A comment above the class says what fails it.
- [ ] **Step 2:** Run them: `Options_EachHasARow` fails for `--help`, `--version`, `--query`, `--out`, `--no-obfuscate`, `--scrub`.
- [ ] **Step 3:** Under the paragraph of `tool.md` 492 in `cli.md#options`, add one sentence and a table of the six rows of spec step 3: each `diagnose` row restates its row of `bundles.md#diagnose` and links that section; `--help` and `--version` restate the paragraph of line 456.
- [ ] **Step 4:** Run: all pass.  Take the `--scrub` row out by hand: fails; restore.  Take the `SQLSOURCE_DEBUG` row out: fails; restore.
- [ ] **Step 5:** `tests/README.md` row and `Documentation/AGENTS.md` first bullet name the test; format, validate, commit.

### Task 3: Ledger and worksheets

**Files:**
- Create: `docs/superpowers/specs/2026-11-12-docs-overhaul/ledger/phase-05.tsv`, `merges/MP-06.md`, `MP-11.md`, `MP-19.md`
- Modify: `merges/MP-08.md`, `MP-10.md`

- [ ] **Step 1:** Write the ledger rows of spec "The ledger" 1-14, base = the commit the branch started from (`e1892471`).
- [ ] **Step 2:** `check.sh move 05`: list 1 empty; list 2 only the rows 6-9 and 13 of "The ledger"; list 3 only new text.  `check.sh coverage` passes.
- [ ] **Step 3:** Write the worksheets; validate; commit.

### Task 4: Done when

- [ ] Run every grep and count of the spec's "Done when"; fix what differs; `./pre-commit-validation.sh`.
