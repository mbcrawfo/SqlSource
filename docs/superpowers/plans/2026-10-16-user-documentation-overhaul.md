# User Documentation Overhaul Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the two long reference readmes with a short, friendly root README and a topic-organised user manual in `docs/manual/`, move the diagnostics list into it, and leave AGENTS.md guidance that keeps both current as query generation lands.

**Architecture:** Documentation only, apart from the diagnostics help-link base and two new tests.  Content moves out of today's `README.md` and `src/SqlSource.Tool/README.md` into topic pages, rewritten to describe effects and never internals.  A new test class checks every link and anchor in the user docs and that every `sql` sample in them parses, so the pages stay connected and correct as later phases edit them.

**Tech Stack:** Markdown (GitHub-flavoured), C# 12 / xunit.v3 / Shouldly tests in `tests/SqlSource.Tests`.

**Spec:** `docs/superpowers/specs/2026-10-16-user-documentation-overhaul-design.md`.  Read it before any task: the page table, "How the pages are written", the anti-goals and the sidecar paragraph are the requirements every content task is judged against.

## Global Constraints

- **User facing only.**  No library type names, no "describer", plan walks, `EXPLAIN`, heuristics, provenance fields, capture files; no links into `docs/tech-debt`, `docs/superpowers`, `docs/deferred` or `src/`.  A known issue is described by its effect, in the user's words, with what to do about it.
- **Current functionality, with placeholders.**  A placeholder is the one standard callout below, never a description of the feature as if it worked:

  ```markdown
  > **Coming in a later release.**  <One sentence on what it will do.>  See [what works today](README.md#what-works-today).
  ```

  (From the root README the link is absolute.)
- **Links.**  `README.md` and `src/SqlSource.Tool/README.md` are packed into NuGet packages: every link in them is absolute, `https://github.com/mbcrawfo/SqlSource/blob/main/<path>[#anchor]`.  Pages in `docs/manual/` link to each other relatively.
- **Stable anchors.**  A heading that another page, the README or a diagnostic links to gets an explicit anchor on the line above it: `<a id="configure-a-folder"></a>`.
- **Example first.**  Each reference section opens with an example, then gives the rules.
- **Terms** (defined in `concepts.md`, used everywhere): *query parameter* (`@id`), *generator parameter* (`keep-comments`), *token* (`{{table}}`), *dialect*, *logical database*, *file header* (the part of a `.sql` file before its first `-- name:` line), *sidecar*.
- **Settings live in `settings.md` only.**  Other pages link to a setting's anchor; they do not repeat its values or precedence.
- **Every commit passes `./pre-commit-validation.sh`** (root `AGENTS.md`, Commits).  Run it after the last edit of each task, before the commit.  Docker must be running.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Pull request: at most 150 files; no `v*` tags exist, so the version check has nothing to compare.

## Review Focus

1. **A README link on nuget.org whose anchor was renamed** — the reader lands at the top of a long page.  Pinned by `UserDocsLinksTests` checking absolute links into this repository as well as relative ones (Task 2).
2. **A copied `sql` sample that does not parse** — the reader's first build fails on the docs' own example.  Pinned by `UserDocsSamplesTests` (Task 2).
3. **A placeholder or answer that reads as if an undelivered feature works** (models, query methods, `async-method-suffix`, `ModelNamespace`) — the reader sets it and nothing happens.  Pinned by the "What works today" table and the Task 10 review grep for each undelivered setting name next to a callout.
4. **The "keep SQL out of code files" scenario recommending settings that still make the tool or a build complain** — e.g. leaving `SqlSourceOutput` at its default under `ansi`.  Task 9 verifies the recommendation against the code and tests before writing it.
5. **A help link printed by a build error that lands on no section** — pinned by the existing descriptor tests with the new base URL (Task 1) and the anchor check of Task 2.

---

### Task 1: Move the diagnostics list into the manual

**Files:**
- Move: `docs/diagnostics.md` → `docs/manual/diagnostics.md` (`git mv`)
- Modify: `src/SqlSource/Diagnostics/SqlDiagnostics.cs` (`HelpLinkBase`, the remarks comment), `src/SqlSource/Diagnostics/ToolDiagnostics.cs` (comments), `src/SqlSource.Tool/Reporting/ToolDiagnostic.cs` (comment)
- Modify: `tests/SqlSource.Tests/Diagnostics/SqlDiagnosticsTests.cs`, `ToolDiagnosticsTests.cs`, `DiagnosticsDocumentTests.cs`, `tests/SqlSource.Tests/SqlSource.Tests.csproj`, `tests/SqlSource.Tool.Tests/Expected.cs`
- Modify: every other live reference that `git grep -n "docs/diagnostics.md"` finds outside `docs/superpowers/` (AGENTS.md files, `README.md`, `src/SqlSource.Tool/README.md`, `docs/deferred/D-0002…`, `D-0003…`, `docs/tech-debt/TD-0006…`, `TD-0049…`).  `docs/superpowers/` specs and plans keep the old path, except `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`.

**Interfaces:**
- Produces: `SqlDiagnostics.HelpLinkBase == "https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/diagnostics.md#"`; the test project copies the page to `docs/manual/diagnostics.md` in its output folder.

- [ ] **Step 1: Change the expected URLs in the tests**

In `SqlDiagnosticsTests.All_HelpLinks_PointAtTheDescriptorsSectionOfTheDiagnosticsDocument`, the matching test in `ToolDiagnosticsTests`, and `Expected.See`, replace `docs/diagnostics.md#sqlsrc` with `docs/manual/diagnostics.md#sqlsrc`.

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test --project tests/SqlSource.Tests` and `dotnet test --project tests/SqlSource.Tool.Tests`
Expected: FAIL in the two help-link tests and every tool test that uses `Expected.See`.

- [ ] **Step 3: Move the page and change the base**

`git mv docs/diagnostics.md docs/manual/diagnostics.md`.  Set `HelpLinkBase` as in Interfaces.  In `SqlSource.Tests.csproj` change the linked item to `Include="../../docs/manual/diagnostics.md" Link="docs/manual/diagnostics.md"`, and in `DiagnosticsDocumentTests` read `Path.Combine(AppContext.BaseDirectory, "docs", "manual", "diagnostics.md")` and update its header comment.  Update the comments in the three source files.  Fix the page's own links that the move breaks (relative links up to the repository now need `../../`).

- [ ] **Step 4: Group the page's index by area**

Split the index table into four tables under bold labels, in id order: **Project and attribute** (`SQLSRC001`–`SQLSRC014`), **`.sql` files** (`SQLSRC101`–`SQLSRC119`), **The tool** (`SQLSRC200`–`SQLSRC223`), **PostgreSQL** (`SQLSRC224`–`SQLSRC236`).  Each table keeps the `| Id | Title |` header.  Do not use a `## ` heading for a group: `DiagnosticsDocumentTests` reads every `## ` heading as a diagnostic's section, and its table test expects the `| [SQLSRC` rows in descriptor order across the whole page.

- [ ] **Step 5: Update the remaining references**

Every hit of `git grep -n "docs/diagnostics.md"` outside `docs/superpowers/` (and inside the epic outline) names the new path.  In `README.md` and the tool readme, which later tasks rewrite, a search-and-replace is enough.

- [ ] **Step 6: Run the tests and see them pass**

Run: `dotnet test --project tests/SqlSource.Tests` and `dotnet test --project tests/SqlSource.Tool.Tests`
Expected: PASS.  `git grep -n "docs/diagnostics.md" -- ':!docs/superpowers'` prints only the epic outline's historical mentions, if any, and nothing else.

- [ ] **Step 7: Validate and commit**

Run `./pre-commit-validation.sh`; expected: every step `passed`.

```bash
git add -A docs src tests README.md AGENTS.md
git commit -m "Move the diagnostics list into docs/manual"
```

---

### Task 2: Tests that keep the user docs linked and their samples valid

**Files:**
- Create: `tests/SqlSource.Tests/Documentation/UserDocs.cs` (helper: the set of user-doc files and their parsed links, headings and fenced blocks)
- Create: `tests/SqlSource.Tests/Documentation/UserDocsLinksTests.cs`
- Create: `tests/SqlSource.Tests/Documentation/UserDocsSamplesTests.cs`
- Create: `docs/manual/README.md` (a stub with the title and one line; Task 8 writes it), so the test set has its entry page

**Interfaces:**
- Consumes: `SqlSource.Testing.RepositoryRoot.Folder`; `SqlFileParser.Parse(string text, string fileName, SqlDialectChoice dialect = default, bool commentsWanted = false)`; `SqlDialectName.TryParse(string? value, out SqlDialectChoice choice)`.
- Produces: `internal static class UserDocs` with
  - `IReadOnlyList<string> Files` — repository-relative paths, `/`-separated: `README.md`, `src/SqlSource.Tool/README.md`, and every `docs/manual/*.md` except `AGENTS.md`.
  - `ISet<string> Anchors(string file)` — the GitHub anchors of the file: each heading's slug (lower case; drop every character that is not a letter, digit, space, `-` or `_`; spaces to `-`; a repeated slug gets `-1`, `-2`, …) plus every `<a id="…">` / `<a name="…">`.
  - `IEnumerable<(string File, int Line, string Target)> Links()` — every inline `[text](target)` link outside fenced blocks and inline code.
  - `IEnumerable<(string File, int Line, string Info, string Text)> FencedBlocks()`.
  - `const string RepositoryUrl = "https://github.com/mbcrawfo/SqlSource/blob/main/"`.

- [ ] **Step 1: Write `UserDocsLinksTests`**

Each test reads `UserDocs`; a failure message names the file, line and target.

```csharp
[Fact] public void Slug_FollowsGitHub() // pins the slug rule with literal cases
{
    UserDocs.Slug("Projects that share internals").ShouldBe("projects-that-share-internals");
    UserDocs.Slug("The `Parameters` property").ShouldBe("the-parameters-property");
    UserDocs.Slug("What a dialect changes?").ShouldBe("what-a-dialect-changes");
}
[Fact] public void RelativeLinks_ReachAFileAndAnchorThatExist()
[Fact] public void RepositoryUrls_ReachAFileAndAnchorThatExist()   // links starting with RepositoryUrl, mapped to the file under RepositoryRoot.Folder
[Fact] public void PackedReadmes_HaveOnlyAbsoluteLinks()           // README.md and src/SqlSource.Tool/README.md: every target starts with "https://"
[Fact] public void UserDocs_LinkToNoInternalDocument()            // no target, relative or under RepositoryUrl, into docs/tech-debt/, docs/superpowers/, docs/deferred/ or src/ (src/SqlSource.Tool/README.md excepted)
[Fact] public void Manual_EntryPage_ListsEveryPage()               // docs/manual/README.md links to every other docs/manual/*.md except AGENTS.md
```

An anchor-only target (`#x`) is checked against its own file.  A link to a directory or a file other than `.md` (for example `schemas/sidecar-v1.schema.json`, `LICENSE`) is checked for existence only.  `Slug` is `internal static string Slug(string heading)` on `UserDocs`.

- [ ] **Step 2: Write `UserDocsSamplesTests`**

```csharp
[Fact] public void SqlSamples_Parse()
```

For every fenced block whose info string's first word is `sql`: the second word, if any, is a dialect name read by `SqlDialectName.TryParse` (the block is parsed with it), or `fragment` or `error` (the block is skipped).  No second word means `ansi`.  An unknown second word fails the test.  Parse with `SqlFileParser.Parse(text, "Example.sql", dialect)` and assert `Errors` is empty, naming the file and line on failure.  GitHub highlights by the first word only, so `sql postgres` still renders as SQL.

- [ ] **Step 3: Run them and see what fails**

Run: `dotnet test --project tests/SqlSource.Tests`
Expected: `Manual_EntryPage_ListsEveryPage` fails until the stub links `diagnostics.md` (add that link to the stub); `SqlSamples_Parse` and the link tests fail on whatever today's `README.md` and tool readme get wrong (for example a marker-only block, or a relative link).  Fix those by marking a block `sql fragment` or `sql <dialect>`, not by changing the tests.  The README and tool readme are rewritten later; only make them pass here.

- [ ] **Step 4: Run the tests and see them pass**

Run: `dotnet test --project tests/SqlSource.Tests`
Expected: PASS.

- [ ] **Step 5: Validate and commit**

Run `./pre-commit-validation.sh`; expected: every step `passed`.  Commit: `Check the links and SQL samples of the user docs`.

---

### Tasks 3–9: the content

Each content task has the same shape.  Its Files block says what it writes; its page contents are the spec's table row for that page, plus the notes below.  Its steps are:

1. **Write the page(s)** to the spec, the Global Constraints and the notes.  Source material is today's `README.md` and `src/SqlSource.Tool/README.md` (read them at the commit before this plan's work started, `git show b69bd85:README.md`) and the Markdown files the notes name.
2. **Check every statement against the code or tests** that own it (`src/SqlSource/`, `src/SqlSource.Tool/`, their tests).  A statement you cannot confirm is removed, not hedged.
3. **Run** `dotnet test --project tests/SqlSource.Tests --filter-class "SqlSource.Tests.Documentation.*"` (or the whole project if the filter is not accepted).  Expected: PASS.
4. **Internals check:** `grep -n -i -E "describer|EXPLAIN|heuristic|nullableSource|provenance|capture|tech-debt|superpowers|TD-0|D-0" <the task's pages>` prints nothing.
5. **Validate and commit** with `./pre-commit-validation.sh` and a message naming the page(s).

A page links to a page a later task writes: create that page as a stub (title and one line) in the earlier task so the link test passes, and the later task fills it.

### Task 3: `concepts.md` and `sql-files.md`

**Files:** Create `docs/manual/concepts.md`, `docs/manual/sql-files.md`; stubs as needed for `settings.md`, `parameters-and-tokens.md`, `dialects.md`.

- `concepts.md`: one paragraph and one small diagram (a Mermaid `flowchart LR`, which GitHub renders) of `.sql` file → generator → constants and methods; `.sql` file → tool + database → sidecar → generator (models, later).  Then the glossary of the terms in Global Constraints.
- `sql-files.md` opens with the **marker index**: one row per marker of today's README Markers table, with columns Marker, Purpose, File header, Inside a query, and a link to its home: a section of this page (`name`, `summary`), `parameters-and-tokens.md` (`param`, `token`, `token-ignore`), `dialects.md` (`dialect`), or the setting's anchor in `settings.md` (`generator`, `database`, `output`, the model and collection markers).  Then: queries and names, result shapes (accepted, no effect yet: callout), summaries, the file header, a file with no `-- name:`, what reaches the generated SQL, and the misplaced-marker rules with their diagnostic links.  Known issues: a `.sql` file that is not UTF-8 (TD-0032) and one listed by two projects (TD-0033), in user terms.

### Task 4: `settings.md`

**Files:** Create/fill `docs/manual/settings.md`.

- One table, a row per setting: Output, Database, Dialect, Generator parameters, Input/Output model suffix, Model namespace, Input/Output model type, one query's model name, Collection type, `SqlLocation`, `MethodLocation`, Path.  Columns: Values, MSBuild property, Item metadata, Attribute, File header, In a query, Status.  Status is "Works today" or "Accepted; no effect yet" (link to the status table).  Each row's name is a link to a section with an explicit anchor (`<a id="setting-output"></a>`, `setting-database`, `setting-dialect`, `setting-generator-parameters`, `setting-model-suffix`, `setting-model-namespace`, `setting-model-type`, `setting-model-name`, `setting-collection-type`, `setting-sql-location`, `setting-method-location`, `setting-path`).
- `<a id="precedence"></a>` The rule once: in a query, then the file header, then the attribute, then item metadata, then the property.  Exceptions stated plainly: a generator-parameter list and a dialect value replace the one below whole; the dialect has no attribute or in-query level; `SqlLocation` and `MethodLocation` exist only on the attribute.
- Generator parameters: the table of today's parameters, with status, and "a list replaces; it never adds".
- `<a id="configure-a-folder"></a>` Worked example: one folder with another dialect and `keep-comments`, as item metadata.
- Which files are included (`SqlSourceIncludeFiles`, `AdditionalFiles Remove`/`Include`), and MSBuild values (white space, `;` and `#`).
- Known issues: a file added by a late build target loses its item metadata (TD-0016).

### Task 5: `parameters-and-tokens.md` and `dialects.md`

**Files:** Create/fill `docs/manual/parameters-and-tokens.md`, `docs/manual/dialects.md`.

- `parameters-and-tokens.md`: query parameters and `-- param:`, the `mssql` `DECLARE` rule, MySQL user variables and PostgreSQL's `@x`; tokens, defaults, `-- token:`, `-- token-ignore:`, validation and `no-token-validation`.  The injection warning is a blockquote with **Tokens are for trusted text only.** in bold, near the top of the tokens section.
- `dialects.md`: choosing (link to `settings.md#setting-dialect` for where), the dialect table, options, what each dialect changes, still read differently, and `<a id="database-support"></a>` the **database support table**: Database, Dialect, Described today, Page.  PostgreSQL: yes, `postgresql.md`; SQL Server: coming, `sqlserver.md`; the others: no.

### Task 6: `generator.md`

**Files:** Create/fill `docs/manual/generator.md`.

- The attribute and its properties (link each to `settings.md`), which files belong to a type, locations (`Nested`, `Direct`), constants and token methods, documentation shown in IntelliSense, projects that share internals (the CS0436 explanation, without naming the suppressor's internals beyond its id `SQLSRC901`).  Placeholder callouts for models and generated query methods.  Known issues: the attribute is read without compiling, so `Path` and `Output` must be literals for the tool (SQLSRC208); attribute conflicts between projects that share internals (TD-0006) if a user can still hit one.

### Task 7: `tool.md`, the tool readme, `postgresql.md`, `sqlserver.md`

**Files:** Create/fill `docs/manual/tool.md`, `docs/manual/postgresql.md`, `docs/manual/sqlserver.md`; rewrite `src/SqlSource.Tool/README.md`.

- `tool.md`, part one, workflow: install as a local tool (`dotnet new tool-manifest`, `dotnet tool install SqlSource.Tool`), which queries get described (output `models` or `codegen`, link to `settings.md#setting-output`), logical databases and connections (the three sources and their order), `<a id="sidecar-files"></a>` sidecar files exactly as the spec's sidecar paragraph says (what, why committed, maintenance, never edit by hand, one link to `schemas/sidecar-v1.schema.json`), describing again, `<a id="after-a-schema-change"></a>` `--force`, `<a id="check-in-ci"></a>` `--check`, the summary lines.  Part two, `<a id="command-reference"></a>` command reference: arguments, `--project`, `--database`, a `.sql` path, `--connection`, `--force`, `--check`, `--verbose`, `--log`, environment variables (`SQLSOURCE_CONNECTION`, `SQLSOURCE_CONNECTION_<NAME>`, `SQLSOURCE_LOG`), exit codes (read them from `src/SqlSource.Tool`), what a log holds before you share it, requirements (.NET SDK, restored projects, matching package versions).  Known issues: symbolic-link project paths (TD-0030), multi-targeted projects (TD-0026), files added by a build hook (TD-0025), in user terms.
- Tool readme: one paragraph, install, the three commands most run (`describe`, `describe --force`, `describe --check`), absolute links to `docs/manual/tool.md` and the root README.  No other content.
- `postgresql.md`: as the spec row says.  Nullability in plain terms from today's tool readme section on PostgreSQL, rewritten as "SqlSource treats a column as possibly null when …" bullets.  Known issues from TD-0047, TD-0049, TD-0050, TD-0051, TD-0052 where a user can see them, each with what to do.  Placeholders: type map, models, generated query methods.
- `sqlserver.md`: the stub the spec describes, linking to `dialects.md` and `parameters-and-tokens.md` for `DECLARE`.

### Task 8: `recipes.md`, the manual's entry page, and the diagnostics cross-links

**Files:** Create/fill `docs/manual/recipes.md`, `docs/manual/README.md`; modify `docs/manual/diagnostics.md`.

- `recipes.md`: the eight recipes the spec lists, each with `<a id="…">`, a sample, and a link to the reference.  No rules beyond what the sample shows.
- `README.md` (manual entry): one paragraph on who the manual is for; "Find what you need" table (need → page/anchor); `<a id="what-works-today"></a>` **What works today** table: Feature, Status (Available / Coming), Where documented — rows: queries as constants, token methods, dialects, settings validation, describing PostgreSQL, describing SQL Server, models, generated query methods, library maps (NodaTime etc.), custom type mapping, build integration, bug-report bundles; then the page list.
- `diagnostics.md`: where a diagnostic enforces a rule a manual section documents, its section ends with a line `See: [<section>](<page>#<anchor>)`.  Do not change any `## ` heading or the first bold line of a section.

### Task 9: the root README

**Files:** Rewrite `README.md`.

- Follow the spec's "The root README" section in order.  Badges: `[![CI](https://github.com/mbcrawfo/SqlSource/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/mbcrawfo/SqlSource/actions/workflows/ci.yml?query=branch%3Amain)` and `[![NuGet](https://img.shields.io/nuget/v/SqlSource.svg)](https://www.nuget.org/packages/SqlSource/)`.
- Supported environments: take the target-framework and language-version floors from SQLSRC003 and SQLSRC012 and `src/SqlSource/AGENTS.md` (Roslyn 4.8: .NET 8 SDK, Visual Studio 2022 17.8).
- Before writing "Just want to keep your SQL out of code files?", confirm in the generator's and tool's settings code and tests what a project that never runs the tool should set (expected: `SqlSourceOutput` `sql`), and that it then needs no dialect beyond the default and gets no error from either.
- FAQ answers about `async-method-suffix` and `ModelNamespace` say the setting is accepted today and takes effect when query methods / models arrive, with the callout's link.
- NodaTime/libraries and custom type mapping: callout sections.
- Every link absolute.  Target length: under about 200 lines.

---

### Task 10: AGENTS.md guidance and the epic outline

**Files:** Modify `AGENTS.md`, `src/SqlSource/AGENTS.md`, `src/SqlSource.Tool/AGENTS.md`, `tests/AGENTS.md`, `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`; create `docs/manual/AGENTS.md`.

- [ ] **Step 1: Root `AGENTS.md` rule 4** — rewrite as the spec's AGENTS.md section says, one short paragraph per document role, naming `README.md`, `docs/manual/`, `docs/manual/diagnostics.md`, `CONTRIBUTING.md`, `docs/publishing.md`, and pointing to `docs/manual/AGENTS.md` for how to write manual pages (that is a pointer to a rule, not to a child AGENTS.md listing).
- [ ] **Step 2: `docs/manual/AGENTS.md`** — under 250 lines: audiences; the no-internals rule with three before/after examples taken from this work (e.g. a plan-walk sentence → "a column from the outer side of a `LEFT JOIN` is possibly null"); the Global Constraints of this plan as rules (callout text, anchors, example first, settings only in `settings.md`, recipes hold no rules, terms); the two tests and what each enforces; the phase-to-page map from the spec; "every phase flips its rows of the What works today table and its Status cells, and adds or removes its known issues as tech-debt and deferred items open and close".  End with the maintenance footer line used by the root `AGENTS.md`.
- [ ] **Step 3: Other AGENTS.md files** — `tests/AGENTS.md` names `docs/manual/diagnostics.md` and the two `Documentation/` test classes; `src/SqlSource/AGENTS.md` the new path; `src/SqlSource.Tool/AGENTS.md`'s readme bullet as the spec says.
- [ ] **Step 4: Epic outline** — its Documentation section and phases 7 and 9's scope items name the manual pages from the spec's phase map instead of README sections.
- [ ] **Step 5: Validate and commit** — `./pre-commit-validation.sh`, then commit `Keep the README and the manual current through AGENTS.md`.

### Task 11: Whole-docs review and pull request

- [ ] **Step 1: Read the README as a new user**, then follow each "Where to next" row into the manual; note any need that takes more than one hop past the entry page, and fix it.
- [ ] **Step 2: Undelivered-feature check** — `grep -n -E "async-method-suffix|ModelNamespace|model-namespace|collection-type|MethodLocation|-> (many|one)" README.md docs/manual/*.md`; each hit outside `settings.md`'s table is next to a callout or says the setting has no effect yet.
- [ ] **Step 3: Internals check over everything** — the Task 3–9 grep over `README.md src/SqlSource.Tool/README.md docs/manual/*.md` (excluding `docs/manual/AGENTS.md`) prints nothing.
- [ ] **Step 4: Validate** — `./pre-commit-validation.sh`, every step `passed`.
- [ ] **Step 5: Open the pull request** — bring the branch up to date with `main` first (the `sync_with_base_branch` tool in this worktree), validate again if it merged anything, push, open the PR against `main` with the spec and plan linked, bind it, and turn on Auto-fix.  Reply to every CodeRabbit finding as the root `AGENTS.md` requires.
