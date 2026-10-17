# Technical Documentation Overhaul Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn `CONTRIBUTING.md` into a short developer readme, move its procedures into readmes beside the code, add architecture docs in `docs/architecture/`, and slim the `AGENTS.md` files to rules that link to those pages.

**Architecture:** This is documentation only, apart from comments that point at moved text.  It ships as two pull requests landing in order.  PR 1 holds the guides and readmes (Tasks 1–6).  PR 2 holds the architecture pages and the slimming of the `AGENTS.md` files (Tasks 7–13).  Content moves, and is not rewritten from scratch.  Every claim that moves into an architecture page is checked against the code first.

**Tech Stack:** GitHub-flavoured Markdown with Mermaid diagrams.  A throwaway Python checker (Appendix A) for links and paths.

**Spec:** `docs/superpowers/specs/2026-10-17-technical-docs-overhaul-design.md`.  Read it before any task.  The anti-goals, "Placeholders", "`AGENTS.md` files" (rule plus link) and "Links that move" are the requirements every task is judged against.

## Global Constraints

- **Out of scope:** `README.md`, `docs/manual/**`, `src/SqlSource.Tool/README.md`.  Never edit them.  `docs/publishing.md`, `docs/superpowers/**` (except this plan and the spec), `docs/tech-debt/**` and `docs/deferred/**` change only where a link of theirs points at text that moved.
- **One home per fact.**  Other documents link to it and never restate it.
- **Never add `.github/README.md`.**  GitHub would show it as the repository's front page.  The workflows' readme is `.github/workflows/README.md`.
- **Rule plus link.**  A rule that stays in an `AGENTS.md` keeps its statement and at most one sentence of reason, then links to the page section that holds the full reasoning.  A rule may keep more only when an agent could not follow it safely without the reason, as with the security invariant "nothing in a `.sql` file can run a statement".
- **Nothing that is a rule is dropped.**  A rule buried in an explanatory paragraph is pulled out and kept.
- **Architecture pages describe today's code.**  Link to source files and never copy them.  Behaviour a user sees is linked to `docs/manual/`, not restated.  Pages hold no status table: "What works today" in `docs/manual/README.md` is the one record of what has been delivered.
- **Placeholder shape, exactly:** a heading, one sentence on what will go there, and the phase with a link to the epic.  Nothing else.

  ```markdown
  ### Reading sidecars

  > **Not built yet.**  How the generator pairs a `.sql` file with its sidecar and reports a stale, missing or orphaned one.  Delivered by phase 5 of the [query generation epic](../superpowers/specs/2026-10-07-query-generation-epic-design.md).
  ```

- **Diagrams are Mermaid** in a ` ```mermaid ` fence.
- **`AGENTS.md` conventions** (root `AGENTS.md`, "Adding guidance for AI agents"):
  - never list child `AGENTS.md` files;
  - any file that names files ends with `> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.`
- **Keep the line `// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.`** at the top of every file in `tests/SqlSource.Tests/Generator/`.  `TestLayoutTests.SharedHeader` asserts it, so `tests/SqlSource.Tests/Generator/AGENTS.md` must keep existing.
- **Voice:** match the existing root `AGENTS.md` and `CONTRIBUTING.md`: plain declarative sentences, two spaces after a full stop.
- **Every commit passes `./pre-commit-validation.sh`** (Docker running, `dotnet tool restore` done).  Run it after the task's last edit, before the commit.  Markdown is excluded from `editorconfig-checker`, but an edited `.cs`, `.yml` or `.targets` comment stays within 120 characters.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.  PR bodies end with `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.
- **Pull requests:**
  - at most 150 files each;
  - there are no `v*` tags, so the version check finds nothing to compare (re-run it anyway: `git fetch --tags origin && git tag --list 'v*' --sort=-v:refname | head -n 1`);
  - before pushing, bring the branch up to date with `main` using the `sync_with_base_branch` host tool;
  - after opening, turn on Auto-fix (`CLAUDE.local.md`);
  - reply to every CodeRabbit finding.

## Review Focus

1. **A rule silently dropped while slimming an `AGENTS.md`.**  Each slimming task writes a disposition list (Appendix B), and its reviewer checks every lead of the old file against it.
2. **The same paragraph in two homes**, typically a procedure copied into both `CONTRIBUTING.md` and a readme, or an explanation left in an `AGENTS.md` and also added to a page.  Each task greps for a distinctive phrase of what it moved: `git grep -n "<phrase>"` must return one file.
3. **A pointer that still resolves but no longer reaches its content**, such as a comment saying "see `CONTRIBUTING.md`" for text that moved.  Task 13 runs the repository-wide grep from the spec.
4. **A moved explanation that the code contradicts.**  Each page task checks every type, file and member name it writes against the code (step "Check names"), and reads the code behind each step it narrates.
5. **A placeholder that carries design from a spec.**  The Task 7 to 11 reviewers reject any placeholder longer than the shape above.

---

## PR 1 — guides and readmes

Work on the current branch, `claude/technical-docs-overhaul-9c41dc`, which already holds the spec and this plan.

### Task 0: Set up the checker

**Files:**
- Create: `artifacts/doc-check.py` (ignored by `.gitignore`, never committed)

- [ ] **Step 1:** If `artifacts/doc-check.py` does not exist, write it from Appendix A.
- [ ] **Step 2:** Run `python3 -I artifacts/doc-check.py CONTRIBUTING.md AGENTS.md`.  Expected: one line, `CONTRIBUTING.md:<n>: path missing: tools/net8.0/any/`.  That path is inside a package, a known false positive, and is acceptable wherever it appears.

### Task 1: The PostgreSQL tests' readme

**Files:**
- Create: `tests/SqlSource.Tests.Postgres/README.md`
- Modify: the comments in `tests/SqlSource.Tests.Postgres/PostgresContainer.cs:20`, `tests/SqlSource.Tests.Postgres/PostgresContainerTests.cs:35` and `.github/workflows/build.yml:98`, which say "see CONTRIBUTING.md" and now say `tests/SqlSource.Tests.Postgres/README.md`
- Modify: root `AGENTS.md` rule 5, which now says "See the image section of `tests/SqlSource.Tests.Postgres/README.md`" in place of "See the image paragraph under the PostgreSQL tests in `CONTRIBUTING.md`"

**Produces:** these headings, which later tasks link to by GitHub slug: `## Running`, `## The image`, `## The describer's fixtures`, `## The realistic corpus`.

- [ ] **Step 1: Write the readme.**  Move the content of `CONTRIBUTING.md` lines 41 and 53–75 (the `SqlSource.Tests.Postgres` table row's body; Docker and the image; pinning to a minor; the fixtures; the realistic corpus) into the four sections.  Open with two sentences on what the project proves, which are the four things in the table row.  Keep both recording commands and the PG14 command.  Do not edit `CONTRIBUTING.md` yet; Task 5 removes the text from there.
- [ ] **Step 2: Check:** `python3 -I artifacts/doc-check.py tests/SqlSource.Tests.Postgres/README.md AGENTS.md`.  Expected: clean, apart from the Task 0 false positive.
- [ ] **Step 3: Validate:** `./pre-commit-validation.sh`.  Expected: every step `passed`.
- [ ] **Step 4: Commit** the four edited files and the readme: `git commit -m "Move the PostgreSQL tests' procedures into their own readme"`.

### Task 2: `tests/README.md`

**Files:**
- Create: `tests/README.md`
- Modify: `tests/AGENTS.md`, `tests/SqlSource.Tool.Testing/SqlSource.Tool.Testing.csproj:4` (its comment says "the ones that tests/AGENTS.md lists", which retargets to `tests/README.md` if that list moves)

**Consumes:** the Task 1 headings.

**Produces:** the headings `## The projects`, `## How they reference each other` and `## Fixture projects`.

- [ ] **Step 1: Write the readme.**
  - `## The projects` covers each of the four test projects and two libraries: what it tests or holds, its Roslyn version, and whether it needs Docker.  The source is `CONTRIBUTING.md` lines 29–45, rewritten as one short paragraph or table row each.
  - `## How they reference each other` is a Mermaid graph of the project references, read from each `tests/*/*.csproj` `ProjectReference`.  It includes RoslynFloor compiling `tests/SqlSource.Tests/Generator/`.
  - Then the consumer view of `tests/SqlSource.Tests` (line 47).
  - `## Fixture projects` takes line 49 and how to add a fixture project.
  - Link to `SqlSource.Tests.Postgres/README.md` for that project's procedures.
- [ ] **Step 2: Slim `tests/AGENTS.md`.**
  - Line 3 says `tests/README.md` (not `CONTRIBUTING.md`) explains each project.
  - Move what each bullet describes about layout into the readme, unless it is already there.
  - Keep every rule, and write the Appendix B disposition list for the file.
  - Keep the footer.
- [ ] **Step 3: Check:** `python3 -I artifacts/doc-check.py tests/README.md tests/AGENTS.md`.  Expected: clean.  Then `git grep -n "never built or restored" -- CONTRIBUTING.md tests tools .github`, which must not show the same paragraph in two files once Task 5 is done.  Note the phrase for Task 5.
- [ ] **Step 4: Validate and commit:** `./pre-commit-validation.sh`, then `git commit -m "Add a readme of the test projects and how they fit together"`.

### Task 3: `tools/README.md`

**Files:**
- Create: `tools/README.md`
- Modify: `tools/AGENTS.md`

**Produces:** the headings `## Linters`, `## Package checks` and `## Recording scripts`.

- [ ] **Step 1: Write the readme.**
  - `## Linters`: the linter table from `CONTRIBUTING.md` lines 147–153.
  - `## Package checks`: the full paragraphs of `tools/check-package.sh`, `check-package-install.sh` (including the `package-install/` consumer and "a change to what the package gives a consumer adds a line to `Program.cs` and the expected output") and `check-tool-install.sh`, from lines 102–122.
  - `## Recording scripts`: one line each for `record-fixtures.sh` and `record-realistic.sh`, linking to `../tests/SqlSource.Tests.Postgres/README.md#the-describers-fixtures` and `#the-realistic-corpus`.
- [ ] **Step 2: Slim `tools/AGENTS.md`.**
  - Line 3 points to `tools/README.md`.
  - The `record-*` and `check-tool-install` bullets keep only their rules; the recon names one, that renaming the tests or the environment variables means changing the scripts.
  - Write the disposition list.
- [ ] **Step 3: Check:** `python3 -I artifacts/doc-check.py tools/README.md tools/AGENTS.md`.  Expected: clean.
- [ ] **Step 4: Validate and commit:** `git commit -m "Add a readme of the scripts in tools"`.

### Task 4: `.github/workflows/README.md`

**Files:**
- Create: `.github/workflows/README.md`
- Modify: `.github/AGENTS.md` (its opening workflow map becomes a pointer to the readme)

- [ ] **Step 1: Write the readme** from `CONTRIBUTING.md` lines 157–177: the workflow table, the artifacts, `coverage-comment.yml` running as it is on `main`, the `ci` required check, nightly's scope with the `gh workflow run nightly.yml --ref <branch>` command, and the schedule being turned off after 60 days.
- [ ] **Step 2: Trim `.github/AGENTS.md`.**  Its first paragraph becomes one line pointing at `workflows/README.md`.  Keep every bullet's rule.  The "why" of the trusted-publishing names may shrink to one sentence.  Write the disposition list.
- [ ] **Step 3: Check:** `python3 -I artifacts/doc-check.py .github/workflows/README.md .github/AGENTS.md`, and `test ! -e .github/README.md`.  Expected: clean, exit 0.
- [ ] **Step 4: Validate and commit:** `git commit -m "Add a readme of the GitHub workflows"`.

### Task 5: Rewrite `CONTRIBUTING.md`

**Files:**
- Modify: `CONTRIBUTING.md` (rewritten)
- Modify: root `AGENTS.md` rule 4 (the `CONTRIBUTING.md` bullet, and a new bullet for the readmes) and "Commits" item 4 (its "see `CONTRIBUTING.md`" stays, since the tools section remains there)

**Consumes:** the headings from Tasks 1–4.

- [ ] **Step 1: Rewrite** to the spec's seven sections (spec, "`CONTRIBUTING.md` — the developer readme"), in that order, linking to the readmes of Tasks 1–4.  Do not link to `docs/architecture/` yet; Task 12 adds those links.  Keep the `postgres:14.24` command.  Target 80–100 lines.
- [ ] **Step 2: Edit root `AGENTS.md` rule 4.**
  - The `CONTRIBUTING.md` bullet becomes: the developer readme, holding the required tools, one sentence per project, and the commands to build, test, check, pack and run, with detail in the documents it links to.  A change to a required tool, or to how the project is built, tested, checked or packed, updates it.  So does adding or removing a project.
  - Add a bullet for `tests/README.md`, `tests/SqlSource.Tests.Postgres/README.md`, `tools/README.md` and `.github/workflows/README.md`: each holds the procedures of its area, and a change to a project, script, workflow or procedure they describe updates them in the same PR.
- [ ] **Step 3: Check:**
  - `wc -l CONTRIBUTING.md` gives 80 to 100.
  - `python3 -I artifacts/doc-check.py CONTRIBUTING.md AGENTS.md tests/README.md tools/README.md .github/workflows/README.md tests/SqlSource.Tests.Postgres/README.md` is clean.
  - For each phrase below, `git grep -n "<phrase>" -- CONTRIBUTING.md tests tools .github` returns one file:
    - `"never built or restored"` (Task 2);
    - `"a feed that holds nothing else"`;
    - `"turns the schedule of a public repository off"`;
    - `"exempt"`.
- [ ] **Step 4: Check procedures.**  Each of these is reachable from `CONTRIBUTING.md` in one link:
  - re-record fixtures;
  - move to a new image minor;
  - add a fixture project;
  - add a realistic case;
  - the package-install consumer;
  - the nightly manual run.

  List the link for each in the commit message body.
- [ ] **Step 5: Validate and commit:** `git commit -m "Make CONTRIBUTING.md a short guide to getting started"`.

### Task 6: Open PR 1

- [ ] **Step 1:** Run the version check from Global Constraints, then call `sync_with_base_branch`.  Resolve any conflicts, then run `./pre-commit-validation.sh` on the final tree.  Expected: all `passed`.
- [ ] **Step 2:** Push, and open a PR titled "Make CONTRIBUTING.md a getting-started guide and move its detail into readmes".  The body links the spec and says the architecture docs follow in a second PR.  Bind it with the `ccd_pr` tools and turn on Auto-fix.
- [ ] **Step 3:** Wait for review and merge.  Reply to every CodeRabbit finding.  Start PR 2 only after PR 1 is merged.

---

## PR 2 — architecture

Branch `claude/technical-docs-architecture` from `main` once PR 1 has merged.  Copy `artifacts/doc-check.py` if the worktree is new.

The page file names below are this plan's choices.  Every later task uses them.

| File | Page |
|----|----|
| `docs/architecture/README.md` | Overview |
| `docs/architecture/generator.md` | Generator |
| `docs/architecture/msbuild-and-manifest.md` | MSBuild files and the manifest |
| `docs/architecture/tool.md` | Tool |
| `docs/architecture/postgres-describer.md` | PostgreSQL describer (Task 11 may split it into `postgres-describer.md` and `postgres-nullability.md` when it passes about 40 KB) |

### Task 7: Overview and the folder's `AGENTS.md`

**Files:**
- Create: `docs/architecture/README.md`, `docs/architecture/AGENTS.md`

**Produces:**
- The overview's section headings: `## Components`, `## How a query flows`, `## Shared code`, `## Glossary` and `## Reading guide`.
- The glossary entries, which every page uses as written: sidecar, describer, manifest, claim, run unit, hint name, settings level, dialect, capture, plan walk.

- [ ] **Step 1: Write the overview.**
  - `## Components`: the two packages and what each assembly is.
  - `## How a query flows`: a Mermaid flowchart.  The `.sql` files and `[SqlSourceGenerate]` go to the generator, which produces constants and token methods.  The tool, through the manifest, gets the project and describes against the database to produce a `.sql.json` sidecar.  A dashed edge from sidecar to generator is labelled "planned (phase 5)".
  - `## Shared code`: the generator assembly is referenced by the tool, with `InternalsVisibleTo`.
  - `## Glossary`.
  - `## Reading guide`: an empty list for now.  Tasks 8–11 each add their page's line when the page exists, so no commit links to a missing file.
- [ ] **Step 2: Write `docs/architecture/AGENTS.md`**, per the spec's section on it.
  - A table mapping each `src/` folder to its page and section.
  - The rules:
    - pages describe today's code and are updated in the same PR;
    - a rename updates a page in the same commit;
    - the placeholder shape (copied from Global Constraints) and when to fill one;
    - what does not belong here, and where it goes instead;
    - link to source, never copy it;
    - Mermaid.
  - The footer.
- [ ] **Step 3: Check names:** for every backticked type name in both files, `git grep -lw <Name> -- src` returns at least one file.
- [ ] **Step 4: Validate and commit:** `git commit -m "Add the architecture overview"`.

### Task 8: Generator page, and slim the generator's `AGENTS.md`

**Files:**
- Create: `docs/architecture/generator.md`
- Modify:
  - `src/SqlSource/AGENTS.md` (all sections except "The project manifest");
  - `docs/tech-debt/TD-0021-comments-and-token-defaults.md:17` and `docs/tech-debt/TD-0022-query-that-keeps-its-comments-is-built-and-scanned-twice.md:18`, retargeted to `docs/architecture/generator.md#<section>`.

**Produces:** the headings `## The pipeline`, `## Settings`, `## Parsing`, `## Emitting`, `## Diagnostics`, `## Snapshot` and `## Not built yet`.  The last holds the placeholders: Reading sidecars (5), Type map and models (5, extended in 7), Execution methods (8).

- [ ] **Step 1: Write the page.**
  - Add the page's line to the overview's `## Reading guide`.
  - Sources: the (b) items of the generator recon list in Appendix C, and `src/SqlSource/SqlSourceGenerator.cs`, read in full.
  - `## The pipeline` has a Mermaid flowchart of the steps, named as in `Generation/TrackingNames.cs`, and says which steps feed which outputs.
  - Read the code behind each step before describing it.
- [ ] **Step 2: Slim `src/SqlSource/AGENTS.md`** to rules plus links (Global Constraints), leaving "The project manifest" for Task 9.  Add a line under the title: "How the generator works: [`docs/architecture/generator.md`](../../docs/architecture/generator.md)."  Write the Appendix B disposition list.
- [ ] **Step 3: Retarget TD-0021 and TD-0022.**
- [ ] **Step 4: Check:**
  - `python3 -I artifacts/doc-check.py docs/architecture/generator.md src/SqlSource/AGENTS.md docs/tech-debt/TD-0021-*.md docs/tech-debt/TD-0022-*.md` is clean.
  - Check names, as in Task 7.
  - `git grep -n "has two stages" -- src docs/architecture` returns one file.
- [ ] **Step 5: Validate and commit:** `git commit -m "Add the generator's architecture page and keep only its rules in AGENTS.md"`.

### Task 9: MSBuild files and the manifest

**Files:**
- Create: `docs/architecture/msbuild-and-manifest.md`
- Modify:
  - `src/SqlSource/AGENTS.md` ("The project manifest" section, and the trimming and `SqlSourceSettingsFile` parts of the dialect bullet, if Task 8 left them);
  - the comment at `src/SqlSource/build/SqlSource.targets:166`, which now says `docs/architecture/msbuild-and-manifest.md`.

**Produces:** the headings `## What the props and targets do`, `## Trimming`, `## The manifest`, `## Known limits` and `## Not built yet`.  `## Not built yet` holds `DependentUpon` nesting (5) and Build integration (9).

- [ ] **Step 1: Write the page**, and add its line to the overview's `## Reading guide`.  Work from `src/SqlSource/build/SqlSource.props`, `src/SqlSource/build/SqlSource.targets` and `src/SqlSource.Tool/Projects/ManifestReader.cs`, all read in full.
  - `## The manifest` gives the format's lines in order and says it is the contract with the tool.
  - `## Known limits` links TD-0025, TD-0026 and TD-0027.
- [ ] **Step 2: Slim the manifest section** to its rules (hooks nothing, the format is a contract, new setting means two lines, escape, one item per file), each with a link.  Retarget the `.targets` comment.  Write the disposition list.
- [ ] **Step 3: Check:**
  - `wc -c src/SqlSource/AGENTS.md` gives 15000 or less, now that Tasks 8 and 9 have both slimmed it.
  - Run doc-check on the page and on `src/SqlSource/AGENTS.md`, then check names.  Expected: clean.
- [ ] **Step 4: Validate and commit:** `git commit -m "Add the architecture page of the MSBuild files and the manifest"`.

### Task 10: Tool page

**Files:**
- Create: `docs/architecture/tool.md`
- Modify: `src/SqlSource.Tool/AGENTS.md`, every bullet outside the PostgreSQL describer's (recon lines L3–L27 and L49–L65)

**Produces:** the headings `## The command line`, `## A describe run`, `## Folders`, `## Logging and reporting` and `## Not built yet`.  `## Not built yet` holds the SQL Server describer (6), Build integration options (9) and Bug-report commands (10).

- [ ] **Step 1: Write the page**, and add its line to the overview's `## Reading guide`.  Work from `Cli.cs`, `DescribeCommand.cs`, `ToolHost.cs` and `Describing/DescribeRun.cs`, all read in full, and the (b) items of Appendix C's tool list.
  - `## A describe run` has a Mermaid sequence or flowchart: find the unit, evaluate, plan, decide, connect, describe, then write or check.
  - Exit codes link to `docs/manual/` for what the user sees.
  - `## Folders` gives each folder's responsibility and its dependencies on the generator.
- [ ] **Step 2: Slim** those bullets of `src/SqlSource.Tool/AGENTS.md`.  Add the "How the tool works" pointer line.  The rule in `src/SqlSource.Tool/Outcome.cs:10` stays in the file; check it.  Write the disposition list.
- [ ] **Step 3: Check:** run doc-check and check names.  Expected: clean.
- [ ] **Step 4: Validate and commit:** `git commit -m "Add the tool's architecture page and keep only its rules in AGENTS.md"`.

### Task 11: PostgreSQL describer page

**Files:**
- Create: `docs/architecture/postgres-describer.md`, and `docs/architecture/postgres-nullability.md` if the page splits
- Modify:
  - `src/SqlSource.Tool/AGENTS.md`, the describer bullets (recon L21–L48: the contract, the exchange, captures, fixtures, the call order, columns, nullability, table match, explain, the walk, set operations, the planner settings, check, prepared statements, security, the connection, calls, errors, spellings);
  - the comment at `src/SqlSource.Tool/Describing/Postgres/PostgresCallNames.cs:9`;
  - `docs/tech-debt/TD-0052-explain-plans-every-partition.md`, where it cites the `AGENTS.md` reason against `EXPLAIN (GENERIC_PLAN)`.

**Produces:** the headings `## The describer contract`, `## Capture and replay`, `## One describe, step by step`, `## Building parameters and columns`, `## Nullability`, `## The plan walk`, `## Matching a table`, `## Errors` and `## Why a query cannot run a statement`.

- [ ] **Step 1: Write the page**, and add its line, or two lines if it splits, to the overview's `## Reading guide`.  Work from `PostgresSession.cs` (all three parts), `NullabilityWalk.cs`, `PlanInstances`, `TableMatcher` and `PostgresErrors`, all read, and the (b) items of Appendix C.
  - `## One describe, step by step` gives the twelve steps in the order `DescribePreparedAsync` runs them.
  - `## Nullability` links `docs/manual/` for what the user sees.
- [ ] **Step 2: Slim** the describer bullets.  The rules that stay include:
  - never reorder the `TableMatcher` checks;
  - never merge the node-type sets in `PlanVocabulary`;
  - pin a form in `PlanOutputTests` before reading it;
  - deallocate in a `finally`;
  - do not change the 14/18 matrix to fit a recording;
  - never rewrite the user's SQL;
  - the security invariant with its canary test, which may keep its reasons;
  - the connection value goes nowhere else;
  - calls are added and never renamed;
  - re-record with `tools/record-fixtures.sh`.

  Write the disposition list.
- [ ] **Step 3: Retarget** `PostgresCallNames.cs:9` and TD-0052.
- [ ] **Step 4: Check:**
  - `wc -c src/SqlSource.Tool/AGENTS.md` gives 30000 or less.
  - Run doc-check over `docs/architecture/*.md`, `src/SqlSource.Tool/AGENTS.md` and TD-0052.  Expected: clean.
  - Check names.
- [ ] **Step 5: Validate and commit:** `git commit -m "Add the PostgreSQL describer's architecture page and keep only its rules in AGENTS.md"`.

### Task 12: Rules, remaining trims and the skill

**Files:**
- Modify:
  - root `AGENTS.md`;
  - `CONTRIBUTING.md`;
  - `tests/SqlSource.Tests/Generator/AGENTS.md`;
  - `docs/superpowers/AGENTS.md`;
  - `.claude/skills/reviewing-maintainability/SKILL.md` (step 3 of "Executing the moves");
  - `.claude/skills/reviewing-maintainability/sweeps.md` (the AGENTS.md accuracy brief).

- [ ] **Step 1: Root `AGENTS.md`.**
  - Rule 4 gains a `docs/architecture/` bullet.  The pages say how the components work and hand work to each other, as the code is today.  A change that makes a page wrong updates it in the same PR.  Delivering a phase of query generation replaces its placeholder with a description of what was built.  A new component gets its page or section in the change that adds it.
  - "Adding guidance for AI agents" item 3 gains: descriptions of how code works go in `docs/architecture/`, and the `AGENTS.md` links to them.
  - Add an item: an `AGENTS.md` holds rules; each keeps at most a sentence of reason and links to the page that explains it.
- [ ] **Step 2: `CONTRIBUTING.md`.**  The intro paragraph links `docs/architecture/README.md`.  Each `src` project's sentence links its page.
- [ ] **Step 3: Light trims.**  In `tests/SqlSource.Tests/Generator/AGENTS.md`, cut the justification sentences and keep the header rule.  In `docs/superpowers/AGENTS.md`, cut the folder table's prose if a sentence covers it.  Write a disposition list for each.
- [ ] **Step 4: The skill.**
  - `sweeps.md`'s AGENTS.md accuracy brief also covers `docs/architecture/` and the five readmes: names that no longer exist, claims the code contradicts, and placeholders for phases that have shipped (check against the epic's phase table).
  - `SKILL.md` step 3 adds `docs/architecture/` and the readmes to the documents rewritten after a move.
- [ ] **Step 5: Check:** run doc-check over every file edited in this task.  Expected: clean.
- [ ] **Step 6: Validate and commit:** `git commit -m "Make the architecture docs part of the rules that keep the docs current"`.

### Task 13: Final sweep and PR 2

- [ ] **Step 1: Stale pointers.**  Run `git grep -n -e "AGENTS.md" -e "CONTRIBUTING.md" -- src tests tools .github docs .claude ':!docs/superpowers' ':!docs/manual' ':!README.md' ':!src/SqlSource.Tool/README.md'`.  For each hit, open the target and confirm that the text it cites is still there.  Retarget any that are not.
- [ ] **Step 2: Whole-tree doc-check:** `python3 -I artifacts/doc-check.py $(git ls-files '*.md' | grep -v -e '^docs/superpowers/' -e '^docs/manual/' -e '^README.md$' -e '^src/SqlSource.Tool/README.md$')`.  Expected: only the Task 0 false positive.
- [ ] **Step 3: Sizes:** `wc -c src/SqlSource/AGENTS.md src/SqlSource.Tool/AGENTS.md`.  Record both in the PR body, with the before sizes (32076 and 87247 bytes).
- [ ] **Step 4:** Version check, then `sync_with_base_branch`, then `./pre-commit-validation.sh`.  Expected: all `passed`.
- [ ] **Step 5:** Push, and open a PR titled "Add architecture docs and keep only rules in the AGENTS.md files".  The body attaches every disposition list as a collapsed `<details>` block.  Bind it and turn on Auto-fix.  Reply to every CodeRabbit finding.

---

## Appendix A — `artifacts/doc-check.py`

```python
"""Checks developer markdown: every relative link and anchor resolves, and every backtick path exists.

Usage: python3 -I artifacts/doc-check.py <file.md> [...]   (run from the repository root)
Exit 1 and one line per problem when anything is broken.
"""

import os
import re
import sys

LINK = re.compile(r"\[[^\]]*\]\(([^)\s]+)\)")
TICK = re.compile(r"`([^`\s]+)`")
HEADING = re.compile(r"^(#{1,6})\s+(.*)$")
FENCE = re.compile(r"^\s*(```|~~~)")
PATHLIKE = re.compile(r"^[\w.\-]+(/[\w.\-]*)+$|^[\w.\-]+\.(md|cs|csproj|props|targets|sh|yml|json|sql|slnx)$")
ROOTS = {"src", "tests", "tools", "docs", ".github", ".claude", "schemas"}
PLACEHOLDER = re.compile(r"[<>*{}$]|\.\.\.|^-")


def slug(text):
    text = re.sub(r"`|\*\*|\*|_(?=\w)|(?<=\w)_", "", text.strip().lower())
    text = re.sub(r"\[([^\]]*)\]\([^)]*\)", r"\1", text)
    text = re.sub(r"[^\w\- ]", "", text)
    return text.replace(" ", "-")


def anchors(path):
    found, seen, fenced = set(), {}, False
    with open(path, encoding="utf-8") as f:
        for line in f:
            if FENCE.match(line):
                fenced = not fenced
            if fenced:
                continue
            for a in re.findall(r'<a id="([^"]+)"', line):
                found.add(a)
            m = HEADING.match(line)
            if m:
                s = slug(m.group(2))
                n = seen.get(s, 0)
                seen[s] = n + 1
                found.add(s if n == 0 else f"{s}-{n}")
    return found


def check(path):
    problems, base, fenced = [], os.path.dirname(path), False
    with open(path, encoding="utf-8") as f:
        lines = f.readlines()
    for number, line in enumerate(lines, 1):
        if FENCE.match(line):
            fenced = not fenced
            continue
        if fenced:
            continue
        for target in LINK.findall(line):
            if re.match(r"^[a-z]+:", target):
                continue
            file, _, anchor = target.partition("#")
            resolved = os.path.normpath(os.path.join(base, file)) if file else path
            if not os.path.exists(resolved):
                problems.append(f"{path}:{number}: link target missing: {target}")
            elif anchor and resolved.endswith(".md") and anchor not in anchors(resolved):
                problems.append(f"{path}:{number}: anchor missing: {target}")
        for token in TICK.findall(LINK.sub("", line)):
            token = token.rstrip(".,;:")
            token = re.sub(r":\d+(-\d+)?$", "", token)
            if PLACEHOLDER.search(token) or not PATHLIKE.match(token) or token.startswith("http"):
                continue
            # Only paths from the repository root are checked; a path relative to a folder the text names is not.
            if token.split("/")[0] not in ROOTS or token in ROOTS:
                continue
            if not os.path.exists(token):
                problems.append(f"{path}:{number}: path missing: {token}")
    return problems


if __name__ == "__main__":
    all_problems = [p for file in sys.argv[1:] for p in check(file)]
    print("\n".join(all_problems) if all_problems else f"doc-check: {len(sys.argv) - 1} file(s) clean")
    sys.exit(1 if all_problems else 0)
```

## Appendix B — disposition list

Every task that slims an `AGENTS.md` writes one, in `artifacts/disposition-<file>.md`, before it commits.  The task's reviewer checks it against `git show HEAD~:<file>`.  Task 13 attaches them all to the PR.  There is one row per bullet or paragraph of the old file, identified by its bold lead or its first words:

| Old lead | Kept rule (new wording, or "unchanged") | Explanation went to | Deleted because |
|----|----|----|----|

Every row fills "Kept rule" or says "no rule".  "Explanation went to" holds a page and its section anchor, or is blank when there was none.  "Deleted because" is filled only for text that a page already says, and names that page.

## Appendix C — the recon's classification

The recon is a starting point, and each item is verified against the code.  It classified each bullet of the two `src` `AGENTS.md` files as (a), a rule to keep, or (b), an explanation to move.  It is summarised in the spec's "`AGENTS.md` files" section.  The strongest (b) candidates are:

- **Generator:** the pipeline introduction, how the dialect reaches the parse, the two stages of settings, trimming, the manifest's format and limits, parameters as lexemes, `SqlMarkerScope`, finding the dialect marker, the two forms of SQL, and the sidecar reader's two passes.
- **Tool:** the test layout introduction, `AttributeReader`, which databases a run needs, the `Describing/` steps, the describer contract, the exchange's three shapes, `FakeDescriber`, the fixture layout, the Postgres call order, building columns, the nullability rule, `TableMatcher`, explain and the generic plan, the walk's instances, `ModifyTable`, `OutputReference`, `PlanExpression`, set operations, how the walk degrades, `check` against Npgsql, prepared statements as session state, error ids, type spelling, `PostgresScript`, writing a sidecar whole, filters, the logging description, and exit codes.
