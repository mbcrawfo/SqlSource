# Technical documentation overhaul — design

## Intent

Today a developer who wants to understand SqlSource has two places to look, and neither serves them.  `CONTRIBUTING.md` (177 lines, 18 KB) answers "how do I build this?" with paragraphs of detail about every script, fixture and workflow.  How the generator and the tool work is written down nowhere except the folder `AGENTS.md` files, where it is mixed in with the rules: `src/SqlSource.Tool/AGENTS.md` is 87 KB and `src/SqlSource/AGENTS.md` 32 KB, and by volume more than half of each is explanation of how the code works rather than a rule for editing it.  Anyone else has to read the code.

This change gives each kind of developer documentation one home:

- **`CONTRIBUTING.md` is the developer readme**: a brief getting-started guide that introduces the project and sends the reader to the architecture docs and the readmes for detail.
- **`docs/architecture/`** explains how the components work and how they hand work to each other, so understanding the pipeline no longer requires reading the code.
- **Readmes beside the code that has procedures** (the tests, the tools, the workflows) hold those procedures.
- **`AGENTS.md` files hold rules and traps**, and link to the architecture page that explains the code they guard.
- **The root `AGENTS.md` rules** make each of these documents part of the change that makes it wrong, so they stay current as query generation is built out.

## Goals

1. `CONTRIBUTING.md` is about half its current length (roughly 80–100 lines) and reads as a getting-started guide.
2. The architecture of the generator, its MSBuild files and manifest, the tool, and the PostgreSQL describer is documented in `docs/architecture/`, at the level of components, steps and hand-offs.
3. Every procedure now in `CONTRIBUTING.md` survives, in the readme of the area it belongs to.
4. Explanation in the `AGENTS.md` files moves to the architecture pages; each rule stays behind, with a link where the reader needs the explanation to apply it.
5. Parts of query generation not yet built have placeholder sections in the architecture pages, and the rules require the work that delivers each part to fill its placeholder.
6. `docs/architecture/` has its own `AGENTS.md` for navigating and maintaining the pages.

## Non-goals

- **User documentation is out of scope**: `README.md`, `docs/manual/` and `src/SqlSource.Tool/README.md` (the tool's packed nuget.org readme) are not edited.  This includes the link in `README.md` to `CONTRIBUTING.md`, which stays valid.
- No readme for `src/SqlSource` or `src/SqlSource.Tool`.  The architecture pages replace a developer readme for those projects.
- `docs/publishing.md`, `docs/superpowers/` (specs and plans), `docs/tech-debt/` and `docs/deferred/` keep their content.  Links into them are fine; their own text changes only where a link of theirs breaks.
- No code changes, apart from comments that point at a document whose content moves (see "Links that move").
- No new tests of the developer documentation.  The documentation tests in `tests/SqlSource.Tests/Documentation/` cover user documents only, and stay that way.

## Anti-goals

The change fails, even if every file is written, if:

- **The same explanation is in two places.**  Each fact has one home.  Other documents link to it and do not restate it.
- **An architecture page restates the folder tree or the code.**  A page that lists type names without saying what they do, or copies method bodies, is not architecture.  Pages link to source files rather than quoting them (a short snippet that shows a shape is fine).
- **A procedure is lost.**  Re-recording fixtures, moving to a new image minor, adding a fixture project, adding a realistic case, the package-install consumer and the nightly workflow's manual run are each findable from `CONTRIBUTING.md` in one link.
- **A rule is lost.**  Slimming an `AGENTS.md` drops explanation, never a rule.  A rule that was embedded in an explanatory paragraph is extracted and stays.
- **An architecture page describes the future as present, or invents it.**  Not-yet-built parts are placeholders only (see "Placeholders").
- **Moved explanation is wrong.**  The `AGENTS.md` text was written over many phases and may have drifted.  Each claim that moves into an architecture page is checked against the code first, and corrected if the code disagrees.

## Constraints

- Root `AGENTS.md`, "Adding guidance for AI agents":
  - AGENTS.md files stay brief and direct.  They are under about 250 lines, except at a project root.
  - They give actionable guidance, not descriptions of what a folder does.
  - They never list child `AGENTS.md` files.
  - Any that names files carries the maintenance footer.
- Every commit passes `./pre-commit-validation.sh`.  Markdown is excluded from `editorconfig-checker` (`.editorconfig-checker.json`), but a code comment edited in `.cs` or `.yml` stays within 120 characters.
- A pull request changes 150 files or fewer.  This change is a few dozen.
- `VersionPrefix` is checked against the highest `v*` tag before the PR opens, per root `AGENTS.md`.
- **GitHub gives `.github/README.md` precedence over the root `README.md`** as the repository's front page.  So no readme is added at `.github/README.md`.  The workflows' readme goes at `.github/workflows/README.md`.

## The documents

### `CONTRIBUTING.md` — the developer readme

Reader: a developer who has just cloned the repository.  Contents, in order:

1. One paragraph: what the repository holds, and where to read more (architecture docs, `tests/README.md`, `docs/publishing.md`).
2. **Required tools.**  The current table stays; it is already short.  Add `dotnet tool restore`.
3. **Projects.**  One sentence per project.
   - The two shipped projects: the generator, and the `sqlsource` tool.
   - The four test projects and two test libraries.
   - Each sentence links to the architecture page or `tests/README.md` for more.
4. **Build, test and run.**  The commands, each with at most one line of context:
   - build and test the solution;
   - run the PostgreSQL tests on 14 with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`;
   - run the tool from source;
   - collect coverage.

   Docker is needed for the PostgreSQL tests.  Recording fixtures and the realistic corpus get one line each, with a link to the Postgres test readme.
5. **Pack.**  `dotnet pack`, and the three package checks named with one line each, linking to `tools/README.md`.
6. **Checks.**  `./pre-commit-validation.sh`, what it runs in one sentence, `format.sh`, and the locked-restore note (`dotnet restore` after changing a package).  The linter table reduces to a sentence or moves to `tools/README.md`.
7. **Continuous integration.**  A short paragraph:
   - CI runs the same checks on Linux, plus the PostgreSQL tests on 14;
   - `ci` is the required check;
   - nightly runs on Windows and macOS;
   - the one command to run it on a branch.

   Link to `.github/workflows/README.md` for the rest.

The `postgres:14.24` tag stays in `CONTRIBUTING.md`, in the command of item 4.  Root `AGENTS.md` rule 5 lists `CONTRIBUTING.md` among the places that carry that tag.

### `docs/architecture/`

The reader is a developer, human or agent, who needs to understand how a component works before changing it.  Pages explain responsibilities, the order of steps, the data handed between components and the key design decisions with their reasons.  They name the entry types and files and link to them.

Pages, as decided; the names are the builder's to choose:

| Page | Covers |
|----|----|
| `README.md` | The overview, described below |
| Generator | The incremental pipeline of `SqlSourceGenerator`, with its inputs, steps, records and outputs.  Then the folder responsibilities: Generation, Paths, MSBuild, Emitting, Parsing, Tokens, Quoting, Settings, Diagnostics, Snapshot.  Then settings precedence and the two stages of settings, the two forms of a query's SQL, how the dialect reaches the parser, and how diagnostics travel as data. |
| MSBuild files and the manifest | What `build/SqlSource.props` and `build/SqlSource.targets` do: visible properties and metadata, `SqlSourceSettingsFile`, trimming, and tracking additional files.  Then the `SqlSourceWriteManifest` target and the manifest format, which is the contract between the package and the tool, with its known limits linked to their TD items. |
| Tool | The command-line lifecycle (`Cli`, `UsageCheck`, `ToolHost`, exit codes), then a `describe` run: find the unit, evaluate projects, plan, decide, connect, describe, then write or check.  Also how the tool reuses the generator's code, the folder responsibilities and their dependencies, logging and reporting. |
| PostgreSQL describer | The engine-neutral describer contract and the exchange seam, with capture and replay.  Then the session, the order of calls in one describe, how parameters and columns are built, the nullability rules and the plan walk, how a table is matched, error mapping, and why nothing in a `.sql` file can run a statement. |

The **overview** (`README.md`) covers:

- the components and the two packages;
- one diagram of the flow: `.sql` files and the attribute go to the generator and to `sqlsource describe`, the tool asks the database, and the sidecar goes back to the generator;
- where shared code lives, which is the generator assembly, referenced by the tool;
- a short glossary of the project's terms (sidecar, describer, manifest, claim, hint name, and so on);
- a reading guide to the other pages.

The builder may split a page that grows large.  The PostgreSQL describer is the likely one, since its explanation is about 35 KB today.  The builder may also add a sidecar page if the sidecar does not fit cleanly in the generator and tool pages.  It must not restate `docs/superpowers/specs/2026-10-07-sidecar-format-design.md`, which is the format's record.

**Diagrams** are Mermaid in the markdown.  GitHub renders it, and it diffs as text.  At least the overview has one.  Others are added where a flow is easier to see than read.  *(My call.)*

**The manual.**  Where a page touches behaviour the manual documents, such as exit codes, settings precedence or which columns can be null, the page links to the manual for what the user sees and explains only how the code produces it.  *(My call.)*

**Drift.**  When an explanation moved from an `AGENTS.md` turns out to be wrong, the page describes what the code does.  When the code itself is the problem, root rule 2 applies, and it is recorded in `docs/tech-debt`.

**Status.**  Architecture pages describe the code as it is.  They do not keep a status table.  The record of what is delivered is `docs/manual/README.md`, "What works today".

#### Placeholders

A part of query generation that is not built gets a section in the page it will extend:

- a heading;
- one sentence saying what will go there;
- the epic phase that delivers it, linking to `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`.

The section holds nothing else: no design, no types.  The agent that implements the part writes the section, from the code it wrote.

The placeholders, from the epic's phase table:

| Placeholder | Page | Phase |
|----|----|----|
| Reading sidecars: pairing a `.sql` file with its sidecar, staleness, and the missing/stale/orphan diagnostics | Generator (and the overview's diagram, drawn as planned) | 5 |
| The type map and model emission | Generator | 5 (library maps extend it in 7) |
| Execution methods | Generator | 8 |
| `DependentUpon` nesting and build integration (online mode) | MSBuild files and the manifest | 5, 9 |
| The SQL Server describer | Tool (or a describer page of its own when built) | 6 |
| Build integration options of the tool (`--manifest`, `--online`, `--watch`) | Tool | 9 |
| Bug-report commands (`diagnose`, `replay`) | Tool | 10 |

Phase 4 (defaults) changes only the repository's own tests and gets no placeholder.

#### `docs/architecture/AGENTS.md`

Navigation and maintenance, under 250 lines, with the maintenance footer:

- Which page covers which code (`src/` folder → page), so an agent finds the page for the code it is changing.
- **Pages describe what the code does today.**  When a change makes a page wrong, the same PR updates it.  When a change renames, moves or removes a file or type a page names, the same commit updates the page.  This is the coupling rule that root `AGENTS.md` already applies to `AGENTS.md` files.
- **Placeholders.**  What one looks like.  The work that delivers a phase replaces its placeholder with a description of what was built, written from the code.  A placeholder is never filled with design from a spec.
- **What belongs here and what does not.**
  - Rules for editing code go in the folder's `AGENTS.md`.
  - Behaviour a user sees goes in `docs/manual/`.
  - Decisions and their history go in `docs/superpowers/`.
  - Delivery status goes in "What works today".
- Link to source; never copy it.  No line-by-line walkthroughs.
- Mermaid for diagrams.

### Readmes beside the code

| File | Holds |
|----|----|
| `tests/README.md` | What each test project and test library is for, which Roslyn version it runs on, and how they reference each other (a diagram is welcome).  Also what `tests/SqlSource.Tests` proves as a consumer (`EndToEnd/` and the imported MSBuild files), and the fixture projects under `tests/SqlSource.Tool.Tests/Fixtures/Projects/` with how to add one.  It links to the Postgres readme for that project's procedures. |
| `tests/SqlSource.Tests.Postgres/README.md` | Docker, the default image and the image variable, and why every image is pinned to a minor, with the procedure for moving to a new minor.  Then the describer fixtures (layout, `tools/record-fixtures.sh`, adding a case) and the realistic corpus (layout, `tools/record-realistic.sh` on both majors, adding a case, the truth, the seed rule, `exempt`, the noise budget). |
| `tools/README.md` | Each script in `tools/`: the linters, the three package checks with the full detail now in `CONTRIBUTING.md` (what `check-package-install.sh` proves and the `package-install/` consumer), and the two recording scripts, which link to the Postgres readme. |
| `.github/workflows/README.md` | The workflow table, the artifacts, `coverage-comment.yml` running from `main`, the `ci` required check, nightly's scope, and the schedule being turned off after 60 days. |

The builder may fold `tests/SqlSource.Tests.Postgres/README.md` into `tests/README.md` if the result stays readable.  The procedures must stay in one findable place either way.

### `AGENTS.md` files

Every `AGENTS.md` in the repository is reviewed.  Each piece of explanation is either moved to an architecture page or readme, or deleted if a page already says it.  What stays is rules, traps and pointers.  Most of the work is in the two `src` files.  Each folder's file gains a one-line pointer near the top to the architecture page that explains its code.  Individual rules link to a page section where the explanation is needed to apply them.

The files to review:

- `src/SqlSource/AGENTS.md` and `src/SqlSource.Tool/AGENTS.md`;
- `tests/AGENTS.md`, `tests/SqlSource.Tests/Generator/AGENTS.md`, `tools/AGENTS.md`, `.github/AGENTS.md`, `docs/superpowers/AGENTS.md`.

The recon's classification is a starting point; the builder verifies each item.

- **`src/SqlSource.Tool/AGENTS.md`** (87 KB).  Most of the PostgreSQL describer bullets go to the describer page, and so do the `Describing/` step descriptions and the logging description.  Each leaves its rule behind.  Examples of rules that stay:
  - never reorder the `TableMatcher` checks;
  - never merge the `PlanVocabulary` node sets;
  - deallocate in a `finally`;
  - nothing in a `.sql` file runs a statement, with its canary test;
  - re-record after changing a call.
- **`src/SqlSource/AGENTS.md`** (32 KB).  These go to the generator and MSBuild pages:
  - the pipeline introduction;
  - the dialect flow;
  - the two stages of settings;
  - trimming;
  - the manifest's format description and limits;
  - the mechanics of parameters, marker scope and the dialect marker;
  - the sidecar reader's internals.

  The rules stay.
- **`tests/AGENTS.md` and `tools/AGENTS.md`** say today that `CONTRIBUTING.md` explains each project and check.  That pointer moves to `tests/README.md` and `tools/README.md`.  Descriptive bullets go to those readmes.
- **The other three** need light trimming only, of what the recon flagged as explanation.

**Rule plus link** (decided).  A rule keeps its statement and at most a sentence of reason.  The full reasoning moves to the architecture page, and the rule links to the section that holds it.  Line counts say little here, since each bullet is one physical line.  The measure is size instead: `src/SqlSource.Tool/AGENTS.md` goes from 87 KB to roughly 20–30 KB, and `src/SqlSource/AGENTS.md` shrinks in proportion.  A rule may keep more than one sentence only when an agent could not follow it safely without the reason in front of them.  A security invariant such as "nothing in a `.sql` file can run a statement", with its canary test, is the example.  The other files keep their current shape.

`docs/manual/AGENTS.md`, `docs/tech-debt/AGENTS.md` and `docs/deferred/AGENTS.md` are not slimmed.  The first serves user documentation, which is out of scope.  The other two are procedures, which is what an `AGENTS.md` is for.

### Root `AGENTS.md` rules

Rule 4, "Keep the docs current", gains or changes these entries.  Each says what the document is for and which change must update it in the same PR.

- **`CONTRIBUTING.md`**, rewritten: the developer readme.  It holds the required tools, one sentence per project, and the commands to build, test, check, pack and run; detail lives in the documents it links to.  A change to a required tool or to how the project is built, tested, checked or packed updates it.  So does adding or removing a project.
- **`docs/architecture/`**, new.  It is how the components work and hand work to each other, described as the code is today.  A change that makes a page wrong updates it.  Delivering a phase of query generation replaces that phase's placeholder with a description of what was built.  A new component, such as another describer, gets its page or section in the change that adds it.
- **The readmes beside the code**, new: `tests/README.md`, the Postgres test readme, `tools/README.md` and `.github/workflows/README.md`.  Each holds the procedures for its area.  A change to a project, script, workflow or procedure they describe updates them.
- **`AGENTS.md` files hold rules, not explanation**, a new line or a change to "Adding guidance for AI agents".  An explanation of how code works goes in `docs/architecture/`, and the `AGENTS.md` links to it.

"Adding guidance for AI agents", item 3 ("do not describe what the folder does"), gets one pointer to `docs/architecture/` as the place where descriptions go.

Rule 5's reference to "the image paragraph under the PostgreSQL tests in `CONTRIBUTING.md`" points to the Postgres test readme instead.  `CONTRIBUTING.md` stays in its list of files that carry the `postgres:14.24` tag.

### The maintainability skill

`.claude/skills/reviewing-maintainability/` keeps current documents accurate.

- Its AGENTS.md accuracy sweep (`sweeps.md`) also covers `docs/architecture/` and the new readmes: names that no longer exist, claims the code contradicts, and placeholders for phases that have shipped.
- Step 3 of "Executing the moves" adds `docs/architecture/` and the readmes to the documents whose paths are rewritten after a move.

*(My call: without this, nothing periodically checks the new pages.)*

### Links that move

Text that points at `CONTRIBUTING.md` for detail that moves is updated to point at the detail's new home:

- the comments in `tests/SqlSource.Tests.Postgres/PostgresContainer.cs:20` and `PostgresContainerTests.cs:35`, and in `.github/workflows/build.yml:98` (all about image pinning), which go to the Postgres test readme;
- `tests/AGENTS.md:3` and `tools/AGENTS.md:3`;
- root `AGENTS.md` rules 4 and 5;
- `docs/publishing.md:3` still points at `CONTRIBUTING.md` for building and testing, which stays true, so it is unchanged.

Pointers into `AGENTS.md` text that moves to an architecture page are retargeted to that page, because otherwise they still resolve to a file but no longer reach the content.  Retargeting counts as a broken link for the purposes of the non-goals.  *(My call.)*  Known cases:

- the comment in `src/SqlSource/build/SqlSource.targets:166`, which points to the manifest contract;
- the comment in `src/SqlSource.Tool/Describing/Postgres/PostgresCallNames.cs:9`, which points to the call and capture contract;
- `docs/tech-debt/TD-0021` and `TD-0022`, which link to the "two stages" and "two forms" bullets;
- `docs/tech-debt/TD-0052`, which cites the reason `AGENTS.md` gives for not using `EXPLAIN (GENERIC_PLAN)`.

Before PR 2 closes, the builder runs `git grep -n AGENTS.md` over `src`, `tests`, `tools`, `.github` and `docs` (excluding `docs/superpowers/`) to find any others.

After the change, every relative link and every backtick path in the edited files resolves.

## Delivery

Two pull requests, landing in order.  Both are well under 150 files.  *(My call: splitting keeps each review to one kind of change.)*

1. **Guides and readmes.**  Rewrite `CONTRIBUTING.md`, without links to `docs/architecture/`, which PR 2 adds. and add `tests/README.md`, the Postgres test readme, `tools/README.md` and `.github/workflows/README.md`.  Update the pointers in `tests/AGENTS.md` and `tools/AGENTS.md` and trim their descriptive bullets.  Update the code comments, and root `AGENTS.md` rule 5 plus the `CONTRIBUTING.md` and readme entries of rule 4.
2. **Architecture.**  Add `docs/architecture/` with its pages, placeholders and `AGENTS.md`, and add the links to it from `CONTRIBUTING.md`.  Retarget the pointers listed under "Links that move".  Slim the `src` `AGENTS.md` files and the remaining three, add the architecture entries to root `AGENTS.md`, and update the maintainability skill.

If the builder finds the two depend on each other more than this suggests, one PR is acceptable.

## Left to the builder

- Page file names and the order of sections within each page.
- Where a diagram helps beyond the overview.
- Whether the PostgreSQL describer page splits, and whether a sidecar page is needed.
- Whether the Postgres test readme is its own file or a section of `tests/README.md`.
- The exact wording of the rules and entries above, in the voice of the existing root `AGENTS.md`.
- Which explanation in an `AGENTS.md` is already said by a page and can simply be deleted.
