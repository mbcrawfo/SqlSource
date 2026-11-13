# Docs overhaul, phase 01: status-language Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The user documents stop speaking of a library in development, and `StatusLanguageTests` keeps the phrases out.

**Architecture:** One new xUnit class reads every line of `UserDocs.Files`.  The status text goes from five pages, the rules that ordered it go from four `AGENTS.md` files, and `ledger/phase-01.tsv` records each unit.

**Tech Stack:** C# / xUnit / Shouldly in `tests/SqlSource.Repository.Tests`; Markdown; the epic's checker (`check.sh`).

**Spec:** `docs/superpowers/specs/2026-11-12-docs-overhaul-phase-01-status-language-design.md`.  Its tables hold every exact word; this plan names the step of the spec instead of repeating them.

## Global Constraints

- The pages are unchanged since the baseline `28e20e3c` (`git diff --stat 28e20e3c -- README.md src/SqlSource.Tool/README.md site` names only `site/docfx.json`), so the spec's line numbers hold.
- Change only the words the spec names.  "Do not reword while you are there."
- `site/AGENTS.md` under 8,192 bytes, root `AGENTS.md` under 16,384; every bullet at most 80 words.
- The neutral sentence, word for word: "The packages are published as previews, so the commands include `--prerelease`", ending `.` in `README.md` and `:` in the tool's readme and `site/tool.md`.

## Review Focus

- A `|` inside a cell of the settings table would cut the wrong cell: checked, none at the base.
- The rule line `|----|----|----|----|` must lose a cell with the rows.
- The Database row's whole cell "Works today, for the tool alone" goes, not two words.
- No reference outside `docs/superpowers` names `what-works-today` afterwards.
- `--prerelease` command lines keep their count.

---

### Task 1: `StatusLanguageTests`, failing first

**Files:**
- Create: `tests/SqlSource.Repository.Tests/Documentation/StatusLanguageTests.cs`

**Interfaces:**
- Consumes: `UserDocs.Files`, `RepositoryRoot.Path(string)`.
- Produces: `internal static IEnumerable<string> Hits(string file, IEnumerable<string> lines)`, each hit `file:line: phrase`.

- [ ] **Step 1:** Write the class.  Phrases: `Coming in a later release`, `works today`, `work today`, `no effect yet`, `early development`, matched with `StringComparison.OrdinalIgnoreCase` on every line, fenced blocks and link targets included.
  - `[Fact] UserDocs_HoldNoPhraseOfStatus`: `Hits` over every file of `UserDocs.Files` `.ShouldBeEmpty()`.
  - `[Theory] Hits_FindEachPhrase_InAnyCase`: each phrase, as given and upper-cased, gives one hit naming it.
  - `[Theory] Hits_PassFacts`: "the rows still to come", "PostgreSQL 14 or later", "has not been checked" give none.
  - Its comment says what fails it, and that a phrase is added only when a real sentence of status got past it.
- [ ] **Step 2:** Run `dotnet test tests/SqlSource.Repository.Tests --filter StatusLanguageTests`.  Expected: the fact fails on 23 lines (`README.md` 7 twice, `site/README.md` 61 and 63, `site/settings.md` 36 and 40-58); the theories pass.  Keep the output for the PR description.
- [ ] **Step 3:** Commit the test with the page changes of Task 2 (a commit must pass validation).

### Task 2: The pages, the pointer and the test strings

**Files:** `README.md`, `src/SqlSource.Tool/README.md`, `site/tool.md`, `site/README.md`, `site/settings.md`, `tests/SqlSource.Repository.Tests/Documentation/UserDocsReadingTests.cs`.

- [ ] **Step 1:** Spec step 2, row by row.  Cut the Status column of `site/settings.md` 38-58 with a script that drops the last `| ... ` cell of each line; check `git diff --word-diff` shows only "Status", the rule cell, "Works today" and "Works today, for the tool alone" removed.
- [ ] **Step 2:** Spec step 4: the SQLSRC232 paragraph after the paragraph "To give one column, or one parameter" (now line 432) of `site/settings.md`.
- [ ] **Step 3:** Spec step 5: `what-works-today` becomes `find-what-you-need` in the four strings of `UserDocsReadingTests.cs`.
- [ ] **Step 4:** Run `dotnet test tests/SqlSource.Repository.Tests`.  Expected: all pass, `StatusLanguageTests` included (keep its output).

### Task 3: The rules and the other files

**Files:** `AGENTS.md`, `site/AGENTS.md`, `docs/architecture/AGENTS.md`, `src/SqlSource/Settings/AGENTS.md`, `tests/SqlSource.Repository.Tests/Documentation/AGENTS.md`, `tests/README.md`, the epic outline.

- [ ] **Step 1:** Spec step 6, line by line, and spec step 7.
- [ ] **Step 2:** `wc -c` the four `AGENTS.md` files; `dotnet test tests/SqlSource.Repository.Tests --filter "AgentsFileTests|InboundSiteReferenceTests"` passes.

### Task 4: Ledger, search and checks

**Files:** Create `docs/superpowers/specs/2026-11-12-docs-overhaul/ledger/phase-01.tsv`.

- [ ] **Step 1:** Write the ledger of the spec's "The ledger", with `# base` the merge-base with `origin/main`, in the form of `phase-00.tsv`.
- [ ] **Step 2:** `check.sh coverage` passes; `check.sh move 01` shows list 1 empty, 67 left over each with a row, 24 added, one anchor gone.
- [ ] **Step 3:** Spec step 8's search; each hit is a fact, or is taken out with a ledger row.
- [ ] **Step 4:** Every "Done when" command of the spec; `tools/build-site.sh` passes.
- [ ] **Step 5:** `./format.sh`, `./pre-commit-validation.sh`, commit.

### Task 5: Reviews

- [ ] The no-loss and accuracy reviewers (Opus subagents) with the kit of the spec's "For the reviewers", then `/branch-review:review` to clean, then `./pre-commit-validation.sh --full`.  A fix reruns the later steps.
