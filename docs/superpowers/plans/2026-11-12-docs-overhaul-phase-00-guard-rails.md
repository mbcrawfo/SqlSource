# Docs overhaul phase 0, guard-rails: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The documentation tests accept a site with folders, nested `toc.yml` files, docfx tabs and front matter; a new test follows every reference to a page of `site/` from the rest of the repository; the epic gets its inventory, ledger and checker.  No page of `site/` changes.

**Architecture:** The tests change in place in `tests/SqlSource.Repository.Tests/Documentation/`.  The checker is one C# file run as a .NET 10 file-based app (`dotnet run check.cs`) behind `check.sh`, in `docs/superpowers/specs/2026-11-12-docs-overhaul/check/`.

**Tech Stack:** C# (xUnit v3, Shouldly), .NET 10 file-based apps, bash, docfx 2.81.

**Spec:** `docs/superpowers/specs/2026-11-12-docs-overhaul-phase-00-guard-rails-design.md` (the brief).  The outline `docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md` and section 10 of `docs/superpowers/specs/2026-11-12-docs-overhaul/content-map.md` are its authorities.  Read the brief's section for your task before you start; this plan names the decisions the brief leaves open and does not repeat the rest.

## Global Constraints

- No file under `site/` changes but `site/docfx.json`.  `site/toc.yml`, `site/AGENTS.md`, root `AGENTS.md`, `README.md`, `src/SqlSource.Tool/README.md` do not change.
- The baseline is commit `28e20e3ce8fc0a7f52abf5cd3ae99b9f8a22bf03`.  The user documents on `main` equal it today.
- Every `.cs` file is formatted by CSharpier, every `.sh` by shfmt and checked by shellcheck, every tracked file by editorconfig-checker (120 columns, but `.md`, and the epic's `.tsv` files once Task 4 lands).  Run `./format.sh` before validating.
- The checker builds under the root `Directory.Build.props`: analyzers at `latest-all`, warnings as errors.  Its first lines are `#:property RestorePackagesWithLockFile=false` and `#:property PublishAot=false` (the first keeps a lock file out of the tree, the second keeps the ILCompiler audit warning off).  A run leaves no file in the tree.
- A test of `tests/SqlSource.Repository.Tests` starts no program, and references nothing of the other test libraries (`tests/AGENTS.md`, `tests/README.md`).
- Tests that cover a change: `zsh -lc 'dotnet test --project tests/SqlSource.Repository.Tests'`.  Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.  Run `./pre-commit-validation.sh` before each commit.

## Review Focus

1. **A file git ignores on one machine.**  `.superpowers/`, `CLAUDE.local.md`, `artifacts/site` and `TestResults/` all quote site paths.  The walk of `InboundSiteReferenceTests` must not read them: Task 3 tests the walk on a temporary tree.
2. **A site URL followed by punctuation**, as in `(see https://…/tool.html#exit-codes).` or `…/diagnostics.html#sqlsrc001,`: the trailing `.`, `,`, `)` is not part of the reference.  Task 3 tests it.
3. **A fenced block indented inside a list**, whose closing fence has another indent: its `last` and its normalised text.  Task 5 tests it.
4. **A unit that stands twice** (`| Id | Title |` stands 29 times in `diagnostics.md`): one copy removed is left over.  Task 6 adds a selftest case for it.
5. **A link whose text holds brackets or code**, such as ``[`[x]` value](a.md)``: the normalised text keeps the text whole.  Task 5 tests it.

---

### Task 1: `UserDocs` reads folders, front matter and tabs

**Files:**
- Modify: `tests/SqlSource.Repository.Tests/Documentation/UserDocs.cs`
- Modify: `tests/SqlSource.Repository.Tests/Documentation/UserDocsReadingTests.cs`
- Modify: `tests/SqlSource.Repository.Tests/Documentation/UserDocsLinksTests.cs`
- Modify: `site/docfx.json`

**Interfaces:**
- Produces: `UserDocs.SitePages` lists `site/**/*.md` but `AGENTS.md`, `CLAUDE*.md` and the path `site/index.md`.  `UserDocs.SiteLinkProblem` and `UserDocs.Resolve` accept `<folder>/.../<page>.html`.  `UserDocs.Document` gains `IReadOnlyList<(int Line, string Id)> TabList`: each docfx tab heading, with its id after `#tab/` (`linux/x64` for `#tab/linux/x64`).

- [ ] **Step 1: Write the failing tests in `UserDocsReadingTests`**, from the brief's step 1 list:
  - `Anchors_TabGroup_GiveNoAnchorAndNoLink`: `Read(["# Title", "# [PostgreSQL](#tab/postgres)", "Text.", "", "# [SQL Server](#tab/mssql)", "Text.", "", "---"])` gives `AnchorSet == ["title"]`, empty `LinkList`, empty `Unsupported`, and `TabList == [(2, "postgres"), (5, "mssql")]`.
  - `Anchors_TabHeadingWithAPathId_IsATab`: `# [Linux](#tab/linux/x64)` gives no anchor and `TabList == [(1, "linux/x64")]`.
  - `Anchors_TabHeading_IsNotCountedForASuffix`: `["# [Same](#tab/a)", "# Same"]` gives `["same"]`.
  - `FrontMatter_IsNotRead`: `["---", "title: Start", "[a](b.md)", "---", "# Title", "[c](d.md)"]` gives `["title"]`, `LinkList == [(6, "d.md")]`, empty `Unsupported`.
  - `FrontMatter_WithNoClosingLine_IsText`: `["---", "[a](b.md)"]` gives the link `(2, "b.md")`.
  - `Unsupported_RuleUnderAParagraphOfATab_IsReported`: `["# [PostgreSQL](#tab/postgres)", "Text.", "---"]` gives unsupported lines `[3]`.
  - `Resolve_SiteUrl_IsThePageOfSite` gains `https://mbcrawfo.github.io/SqlSource/reference/cli.html#exit-codes` → `("site/reference/cli.md", "exit-codes")` and `https://mbcrawfo.github.io/SqlSource/reference/databases/postgresql/types.html` → `("site/reference/databases/postgresql/types.md", "")`.
  - `SiteLinkProblem_NotAPageOfTheSite_IsReported` gains `https://mbcrawfo.github.io/SqlSource/reference/` and `https://mbcrawfo.github.io/SqlSource/reference/cli`; `sub/tool.html` moves to `SiteLinkProblem_APageOrAnotherSite_IsNone`; the comment above the first theory drops "the site has no folders" and keeps the redirect.  Do not touch the `what-works-today` strings or line 182's root case (phases 1 and 3).

- [ ] **Step 2: Run `dotnet test --project tests/SqlSource.Repository.Tests`.**  Expected: the new cases fail (`TabList` does not compile until step 3: add it first as an empty list if you want the run to reach the assertions).

- [ ] **Step 3: Change `UserDocs.cs`.**
  - `SitePages`: enumerate with `SearchOption.AllDirectories`, a path from the root with `/`; leave out a file named `AGENTS.md` or matching `CLAUDE*.md`, and the one path `site/index.md`.
  - `SitePagePattern`: `^((?:[^/]+/)*[^/]+)\.html$`.  `SiteLinkProblem`'s message names both forms: `use {SiteUrl}<page>.html or {SiteUrl}<folder>/<page>.html: the site has no other kind of page`.
  - `Read`: when `lines[0] == "---"` and a later line is exactly `---`, lines up to and including that line give nothing.  A heading whose whole text matches `^\[[^\]]*\]\(#tab/([^)\s]+)\)$` is a tab: added to `TabList`, no anchor, not counted in `counts`, and its line gives no link and no unsupported form.
  - The comments at lines 19-20 and 34-35 say a page is `<path>.md` at any depth and `<path>.html` on the site; keep the words on the redirect.
- [ ] **Step 4: In `UserDocsLinksTests`, delete `Manual_EntryPage_ListsEveryPage` and the constant `Manual`.**
- [ ] **Step 5: In `site/docfx.json`, `"files": ["*.md", "toc.yml"]` becomes `"files": ["**/*.md", "**/toc.yml"]`.**
- [ ] **Step 6: Run the project's tests and `tools/build-site.sh`.**  Expected: pass; `find artifacts/site -name '*.html' | wc -l` prints 14.
- [ ] **Step 7: Commit** (`test: read site pages in folders, docfx tabs and front matter`).

### Task 2: `SiteTocTests` reads nested `toc.yml` files

**Files:**
- Modify: `tests/SqlSource.Repository.Tests/Documentation/SiteTocTests.cs`

**Interfaces:**
- Consumes: `UserDocs.SitePages`, `UserDocs.Site`.
- Produces: `internal static IReadOnlyList<string> SiteTocTests.Problems(IReadOnlyDictionary<string, string> tocs, IReadOnlyList<string> pages)`: `tocs` maps a repository path with `/` (`site/reference/toc.yml`) to its text; `pages` are paths as `SitePages` gives them.  Each problem is one string that names the file and the href or page.

- [ ] **Step 1: Write `Problems_OfANestedSet_AreFound`.**  `tocs`: `site/toc.yml` with `href: a/` and `href: b/toc.yml` and `href: c/`; `site/a/toc.yml` with `href: ../diagnostics.md`, `href: page.md` and `href: twice.md`; `site/b/toc.yml` with `href: ../a/twice.md` and `href: gone.md`.  `pages`: `site/diagnostics.md`, `site/a/page.md`, `site/a/twice.md`, `site/a/none.md`.  Expected, in any order, exactly four problems: `c/` names a folder with no `toc.yml`; `gone.md` of `site/b/toc.yml` names no page; `site/a/twice.md` is named by two entries; `site/a/none.md` is in no `toc.yml`.  Assert with `ShouldContain` on a distinctive part of each, and the count 4.
- [ ] **Step 2: Rewrite `Toc_ListsEveryPageOnce_AndNothingElse`** to read every `toc.yml` under `site/` at any depth and assert `Problems(...)` is empty.  Its comment says what fails it.
- [ ] **Step 3: Implement `Problems`.**  An href is read from the folder of its `toc.yml` and normalised (`..` resolved).  One that ends in `/` or whose last segment is `toc.yml` is a folder entry: the folder's `toc.yml` must be a key of `tocs`, and it names no page.  Every other href must be in `pages`.  Every page is named exactly once across all files.  Keep the `HrefPattern` regex.
- [ ] **Step 4: Run the project's tests.**  Expected: pass.
- [ ] **Step 5: Commit** (`test: check every toc.yml of the site, nested ones too`).

### Task 3: `InboundSiteReferenceTests`

**Files:**
- Create: `tests/SqlSource.Repository.Tests/Documentation/RepositoryFolders.cs`
- Create: `tests/SqlSource.Repository.Tests/Documentation/InboundSiteReferenceTests.cs`
- Modify: `tests/SqlSource.Repository.Tests/Documentation/AgentsFileTests.cs` (its `SkippedFolders` moves out)
- Modify: `tests/SqlSource.Repository.Tests/Documentation/AGENTS.md` line 3, `tests/README.md` line 13: the brief's step 8 words.
- Modify: any file outside the user documents whose reference names nothing and is not one of the four exceptions (the brief's trap: mend it, never add an exception).

**Interfaces:**
- Consumes: `UserDocs.Files`, `UserDocs.Anchors(string)`, `UserDocs.SiteLinkProblem`, `UserDocs.Resolve`, `UserDocs.Exists`.
- Produces: `internal static class RepositoryFolders { internal static IReadOnlySet<string> Skipped }`, the folder names of today's `AgentsFileTests.SkippedFolders`, which `AgentsFileTests.Find` now reads.  In the test class: `internal static IEnumerable<(int Line, string Reference)> References(string text)`, `internal static string? Problem(string reference)` (null when the reference names a page and, if any, an anchor that exist), and `internal static IEnumerable<string> Walk(string root)` (repository paths with `/` of the text files it scans, before the exceptions).

- [ ] **Step 1: Write the tests of the text functions.**
  - `References_EachForm_IsFound` (theory: line → reference): `` `site/tool.md` `` → `site/tool.md`; `[x](../../site/tool.md#exit-codes)` → `site/tool.md#exit-codes`; `// see site/sub/probe.md#nope` → `site/sub/probe.md#nope`; `See https://mbcrawfo.github.io/SqlSource/tool.html#exit-codes.` → the URL without the `.`; `(https://mbcrawfo.github.io/SqlSource/reference/cli.html)` → the URL without `)`; `"https://mbcrawfo.github.io/SqlSource/diagnostics.html#"` → the URL with its `#`.
  - `References_OtherWords_AreNotRead` (theory, empty result): `the manual's recipe`, `` the folder `site/` ``, `site/<page>.md`, `https://mbcrawfo.github.io/SqlSource/<page>.html` reads as the root alone and is no problem (assert in `Problem`), `` `site/*.md` ``, `tests/site/x.md`, `` `tool.md` ``, `mysite/tool.md`.
  - `Problem_OfAReference` (theory: reference → null or not): `site/diagnostics.md#sqlsrc001` null; `site/diagnostics.md#sqlsrc999` not; `site/nope.md` not; `site/diagnostics.md#` null; `https://mbcrawfo.github.io/SqlSource/` null; `https://mbcrawfo.github.io/SqlSource/diagnostics.html#` null; `https://mbcrawfo.github.io/SqlSource/diagnostics.html#sqlsrc999` not; `https://mbcrawfo.github.io/SqlSource/reference/` not.
  - `Walk_SkipsWhatGitIgnores`: in a temporary folder (deleted in `finally`), files `a.md`, `.github/b.yml`, `docs/c.cs`, `.superpowers/d.md`, `.claude/e.md`, `artifacts/site/f.md`, `src/bin/g.cs`, `TestResults/h.txt`, `CLAUDE.local.md`, `docs/i.png`.  `Walk` gives exactly `a.md`, `.github/b.yml`, `docs/c.cs`.
- [ ] **Step 2: Run them.**  Expected: fail (no class).
- [ ] **Step 3: Implement.**
  - The path form: `site/` then a path of letters, digits, `_`, `-`, `.`, `/` ending in `.md`, then an optional `#` and anchor of letters, digits, `_`, `-`.  No letter, digit, `_`, `-`, `.` or `/` stands before `site/`, unless it follows `../`.  It names `site/<path>.md`, which must exist, and the anchor, when not empty, must be in `UserDocs.Anchors`.
  - The URL form: `https?://mbcrawfo.github.io/SqlSource` (ignore case) and every following character up to a space, `` ` ``, `"`, `'`, `<`, `>`, `)`, `]`; trailing `.`, `,`, `;`, `:` are dropped.  `SiteLinkProblem` must be null, `Resolve` must give a file that exists, and its anchor, when not empty, must be in `UserDocs.Anchors`.
  - `Walk`: from `root`, skip the folders of `RepositoryFolders.Skipped`, `artifacts`, every folder whose name starts with `.` but `.github`, and the file `CLAUDE.local.md`; keep the extensions `.md .cs .yml .yaml .json .props .targets .csproj .sh .txt .sql` and the file name `.editorconfig`.
  - The fact `[Fact] References_OfTheRepository_NameAPageAndAnchor`: walk `RepositoryRoot.Path()`, leave out every path under `docs/superpowers/`, every path of `UserDocs.Files`, and the two whole files `tests/SqlSource.Repository.Tests/Documentation/UserDocsReadingTests.cs` and `InboundSiteReferenceTests.cs`; then leave out the single-string exceptions, each a (file, reference) pair: `tests/SqlSource.Repository.Tests/Validation/TestScopesTests.cs` with `site/x.md`; `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs` and `tests/SqlSource.Tool.Tests/Expected.cs` with `https://mbcrawfo.github.io/SqlSource/diagnostics.html#sqlsrc`.  Each problem prints `file:line: reference`.  The class comment names each exception and its reason, as the brief's table does.
  - `[Fact] References_OfTheRepository_AreFound`: the scan finds at least one path-form and one URL-form reference to `site/diagnostics.md`, and every single-string exception is still found, so that none outlives its string.
- [ ] **Step 4: Run the project's tests.**  If the fact finds a problem that is not an exception, mend the reference in its file (and say so in the hand-back).  Report the count of files with a reference (about 78 at the baseline, and more since; far above means a folder that the walk must skip).
- [ ] **Step 5: Update the two documents of the brief's step 8** (`Documentation/AGENTS.md` line 3, `tests/README.md` line 13) with the brief's words.  Check `wc -c` of the `AGENTS.md` and that `AgentsFileTests` passes.
- [ ] **Step 6: Commit** (`test: follow every reference to a page of the site from the rest of the repository`).

### Task 4: The checker's core: units, `inventory`, `coverage`, `origin`

**Files:**
- Create: `docs/superpowers/specs/2026-11-12-docs-overhaul/check/check.sh` (executable)
- Create: `docs/superpowers/specs/2026-11-12-docs-overhaul/check/check.cs`
- Create: `docs/superpowers/specs/2026-11-12-docs-overhaul/check/phases.tsv`
- Create: `docs/superpowers/specs/2026-11-12-docs-overhaul/inventory.tsv` (written by `check.sh inventory`)
- Create: `docs/superpowers/specs/2026-11-12-docs-overhaul/ledger/phase-00.tsv`
- Modify: `pre-commit-validation.sh` line 47; `.editorconfig` (new section after `[schemas/*.json]`)
- Modify: `docs/superpowers/AGENTS.md` (the brief's step 8 bullet); the outline, `docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md`: the Tests row of `InboundSiteReferenceTests` names what the test leaves out (`docs/superpowers`, the user documents, and four named exceptions of test data), and "Constraints" gains the two facts on `.tsv` files of the brief's "Where things stand".

**Interfaces:**
- Produces, in `check.cs`, for Task 5 to use: `record Unit(string File, int Line, int Last, string Kind, int Words, string Text)` (`Text` normalised; `Hash` and `Start` computed from it); `static List<Unit> Units(string file, string[] lines)`; `static string Normalise(...)` per the brief's step 4; `static Docs Read(string root, string? commit)` (null commit: the working tree) giving every user document's units; `static Ledger LoadLedger(string root, int phase)` with rows `record Row(string Unit, string Disposition, string To, string Arg, string Words, string Note)` and the `# base` sha; `static int Run(string root, string[] args, TextWriter output)` returning the exit code, so that `selftest` calls commands in process on a scratch root.
- `check.sh <command> [<nn> | <page>]` from the root runs `dotnet run --file <dir>/check.cs -- <root> <command> ...` with the root from `git rev-parse --show-toplevel`, and exits with the program's code (2 when `dotnet` or git is missing).

- [ ] **Step 1: Write `check.sh` and a `check.cs` whose `selftest` makes the scratch copy and runs no case yet.**  The scratch copy: `git clone --shared --no-checkout <root> <temp>`, `git checkout HEAD -- README.md src/SqlSource.Tool/README.md site docs/superpowers/specs/2026-11-12-docs-overhaul`, then the working tree's files of those paths copied over and committed, so the scratch HEAD is the tree under test.  The folder goes in `finally`.  Prove: `check.sh selftest` exits 0, and `git status --porcelain --ignored docs` shows only the new files of this task.
- [ ] **Step 2: Add selftest cases 6, 8 and 10 of the brief, and a case of your own for the units**: a document of a heading, a paragraph with a link whose text holds a code span with brackets, a bullet, an `<a id>` line, a table with its rule line, a quote, and a fenced block indented by two spaces inside a list whose closing fence has four; assert ids, `last`, kinds, words and normalised texts.  Run: they fail.
- [ ] **Step 3: Implement units and normalisation** (the brief's step 4: fences read as `UserDocs.Read` reads them; kinds `heading`, `para`, `bullet` for `-`, `*`, `+` and `1.`/`1)` items, `row`, `block`, `quote`; an inline link or image `[text](target)`/`![text](target)` becomes `text`, but not inside a code span; `words` is `wc -w` of the unit's raw lines; `hash` the first 12 lower-case hex digits of SHA-256 of the normalised text in UTF-8, a block's lines joined with `\n`; a tab in a cell becomes a space).
- [ ] **Step 4: Implement `inventory`.**  `phases.tsv`: a `#` comment line saying the rule, a header `file	first	last	phase`, then the brief's step 4 table as ranges; for "the rest" rows give the whole range (`README.md 1 130 10`), since the narrowest range that holds a unit's first line wins.  Verify each row against section 10 of the map.  `inventory.tsv`: `# baseline 28e20e3ce8fc0a7f52abf5cd3ae99b9f8a22bf03`, the header `id	last	kind	words	hash	phase	start`, rows in the order `README.md`, `src/SqlSource.Tool/README.md`, then the `site/` pages in ordinal order, each by line.  It prints counts by document and by kind and exits 1 when a unit has no phase.  It reads the baseline with `git show`, and exits 2 with "fetch main" when the commit is missing.
- [ ] **Step 5: Run `check.sh inventory`.**  Expected: about 3,960 units: 422 headings, 1,358 paragraphs, 925 bullets, 731 rows, 522 blocks, 1 quote; and 211 `<a id>` lines skipped (print that count).  A count far from these is another grain: find out why before going on.  Run it twice: `git status --porcelain` shows no change after the second run.
- [ ] **Step 6: Implement the ledger and `coverage`**, with the row checks of the brief's step 5 and its `coverage` row: six cells, none empty; a known disposition; a unit, range (holding at least one unit), part, `@` place or `-` that the inventory has; `merge-pair` needs `MP-<nn>`; `restated-from` a `<file>#<anchor>` owner; `corrected` `C-<nn>` or `-`; `move` `fact` or `-`; a note on `removed-status`, `removed-preamble`, `corrected` and a `restated-from` with `to` `-`; `new` has unit `-` and a `to`; a `move-split` has parts `<unit>/1`… unless its unit is a heading, and then has none and a note.  Write `ledger/phase-00.tsv`: `# base <sha of origin/main this branch starts from>`, a comment, the header `unit	disposition	to	arg	words	note`, no row.
- [ ] **Step 7: Implement `origin <page>`.**  Each unit of the page in the working tree: line, words, the baseline id with the same normalised text (every id when several have it) or `new`; then the two sums.  `check.sh origin site/settings.md` gives no `new`.
- [ ] **Step 8: Change `pre-commit-validation.sh` line 47** to add `docs/superpowers/specs/2026-11-12-docs-overhaul/*.tsv`, and add to `.editorconfig` `[docs/superpowers/specs/2026-11-12-docs-overhaul/**.tsv]` with `max_line_length = off` and a comment that says the files are the docs overhaul epic's inventory and ledger, which its last phase removes.  Update `docs/superpowers/AGENTS.md` and the outline as the Files list says.
- [ ] **Step 9: Run `check.sh selftest`, `check.sh coverage`, `check.sh move 00` is not there yet: skip it.**  Expected: the cases pass; `coverage` exits 0 and prints every unit not due.
- [ ] **Step 10: Commit** (`docs: add the inventory, ledger and checker of the docs overhaul`).

### Task 5: The checker's `move` and `rewrite`

**Files:**
- Modify: `docs/superpowers/specs/2026-11-12-docs-overhaul/check/check.cs`

**Interfaces:**
- Consumes: Task 4's `Unit`, `Read`, `LoadLedger`, `Run`, the selftest's scratch copy.

- [ ] **Step 1: Add selftest cases 1-5, 7 and 9 of the brief, and an eleventh**: one of the 29 `| Id | Title |` rows of `site/diagnostics.md` is removed; `move` with no row fails and names it.  Each case writes a scratch ledger `ledger/phase-<nn>.tsv` whose `# base` is the scratch HEAD, edits the scratch working tree, and asserts the exit code and lines of the output.  Run: they fail.
- [ ] **Step 2: Implement `move <nn>`** as the brief's step 6 says, with these decisions:
  - A unit of the base gets its baseline id by its hash: the ids of every baseline unit with the same normalised text; none, and it is named `@<file>:<line>` at the base.  A row names it by any of those.
  - Old and new are multisets of normalised text over all user documents; list 2 is what old holds more often than new, list 3 the reverse, by file, less what list 1 placed.
  - A left-over unit fails the command when no row names it, when its row is a `move` with neither `fact` nor a note that starts with `position`, or when its row's note is `-`.
  - A file of list 3 fails the command unless it is the `to` file of a `new`, `corrected`, part, `fact` or `position` row.
  - The section of an anchor: from the heading the anchor names (a slug, or the heading below an `<a id>` line) to the next heading of the same or a higher level.
  - A part's or a correction's words are found inside one unit, the case of their first letter and a final full stop not compared.  The words of a `move-split` unit that no part holds are printed.
- [ ] **Step 3: Implement `rewrite <nn>`** as the brief's step 6 says, with these decisions:
  - Sections are found in the base and the working tree.  A section is known by its first `<a id>` above the h2, else the slug of the h2, and the text before the first h2 as `(top)`; a section differs when its multiset of normalised units differs.
  - Numerals: runs of digits, with inner `.` or `,`, outside code spans and blocks.  Code spans by their text.  Table rows by their normalised text.
  - A sentence of the old section that holds "not", "only", "never" or "unless" as a word has a partner when a sentence of the new section holds each of those words it holds, and at least half of the words of either sentence are in the other.  Otherwise it is listed.
  - A changed or gone block is named by a row naming its unit; a new block by a `new` or `corrected` row whose `to` is `<file>#<section>`.  A section is named by a row naming one of its old units, or whose `to` is `<file>#<section>`.
- [ ] **Step 4: Run `check.sh selftest`, then `check.sh move 00` and `check.sh coverage` on the real tree.**  Expected: every case passes; `move 00` exits 0 with four empty lists; `coverage` exits 0.  `git status --porcelain` is unchanged by the runs.
- [ ] **Step 5: Commit** (`docs: check a move and a rewrite of the docs overhaul`).

### Task 6 (controller): the brief's "Done when"

- [ ] The two dry runs of the brief, not committed; record what each run printed for the PR description.
- [ ] `git diff --name-only origin/main -- site` prints `site/docfx.json` alone; `git grep -n Manual_EntryPage_ListsEveryPage -- tests` prints nothing.
- [ ] The content reviewers, then `branch-review:review`, then `./pre-commit-validation.sh --full`.
