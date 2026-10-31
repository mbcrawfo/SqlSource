# SqlSource - C# SQL Query Source Generator

## Core rules

1. **Document deferred work.**  Any work that is not implemented as planned must be documented in `docs/deferred`, in the same PR that defers it.
2. **Document tech debt.**  Any problem identified and not resolved, or any intentional choice that is known to be sub-optimal, must be documented in `docs/tech-debt`, in the same PR that discovers or introduces it.
3. **Deferred minor feedback is not allowed.**  When a review gives valid but minor feedback you should not skip it.  If the fix is large enough that you hesitate to add it, ask for user input about making the change or recording it as tech debt.
4. **Keep the docs current, each in its own place.**  A change that makes one of these documents wrong updates it in the same PR.
   - **User documentation describes effects, never internals.**  `README.md`, `src/SqlSource.Tool/README.md` and `docs/manual/` say what a user writes, what they get and what they see: the effect on their queries, parameters, generated code and build, never how SqlSource does it.
     - They describe what works today; a feature still to come is a "Coming in a later release" callout, and the change that delivers it replaces its callouts with documentation and flips its status.
     - A change to behaviour a user can see updates the manual page that documents it, and `README.md` too when it changes the common path or an FAQ answer.
   - `README.md` is for the average user, who should never need to read further: what SqlSource does and why, its status, installation and the supported environments, getting started, the other ways to use it, and a short FAQ.  It links to the manual for every option and never repeats the manual's tables.
     - Nothing about developing or releasing the project belongs there; link to `CONTRIBUTING.md`.
     - It is packed as the `SqlSource` package's readme on nuget.org, and `src/SqlSource.Tool/README.md`, the tool's short page, as `SqlSource.Tool`'s, so every link in both is an absolute URL.
   - **The two packed readmes have rules of their own.**
     - nuget.org shows them with HTML turned off, so they hold no raw HTML and no `<a id>` line: their headings are linked by GitHub's slug, and renaming one means updating every link to it, which the link test in `tests/SqlSource.Tests/Documentation/` catches.
     - A feature still to come is this callout, word for word: `> **Coming in a later release.**  <One sentence on what it will do.>  See [what works today](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/README.md#what-works-today).`
     - Install and update commands carry `--prerelease`, and the NuGet badge is `nuget/vpre`, until the first stable release takes them out (`docs/publishing.md`).
     - A `sql` sample's info string names its dialect (`sql postgres`), or says `fragment` or `error`, since a test parses it.
     - Never name the describer, a plan or `EXPLAIN`, a field of the sidecar, or a tech-debt or deferred id.
   - `docs/manual/` is for the user with a specific need: one part of a project configured differently, a complex query, a database's limits, a build error to look up.  Each topic has one page; the entry page, `docs/manual/README.md`, routes a reader by need, and its "What works today" table is the one record of what is delivered.
   - `docs/manual/diagnostics.md` is the manual's list of diagnostics: what each `SQLSRC` error means and how to fix it.  A change that adds, removes or changes a diagnostic updates it.  A test in `tests/SqlSource.Tests/Diagnostics/` fails when its sections and the descriptors disagree; the explanations are yours to keep true.
   - `CONTRIBUTING.md` is the developer readme.  It holds the required tools, one sentence per project, and the commands to build, test, check, pack and run, with detail in the documents it links to.  A change to a required tool, or to how the project is built, tested, checked or packed, updates it.  So does adding or removing a project.
   - `docs/architecture/` explains how the components work and hand work to each other, as the code is today, for a developer who needs to understand one before changing it.  A change that makes a page wrong updates it.  Delivering a phase of query generation replaces that phase's placeholder with a description of what was built.  A new component, such as another describer, gets its page or section in the change that adds it.
   - `tests/README.md`, `tests/DatabaseTests/README.md`, `tests/DatabaseTests/Postgres/README.md`, `tests/DatabaseTests/SqlServer/README.md`, `tools/README.md` and `.github/workflows/README.md` each hold the procedures of their area.  A change to a project, script, workflow or procedure they describe updates them in the same PR.
   - **`examples/` shows what a user writes and gets.**  A change to what a user writes, gets or sees updates the example projects that show it, in the same PR, and a phase of query generation that delivers a feature adds an example of it to each example project the feature applies to.  The examples build with the solution, and their sidecars are written by `tools/describe-end-to-end.sh`.
   - `docs/publishing.md` covers the version scheme, the release procedure and the configuration outside the repository that releasing depends on.  A change to any of those updates it.
5. **Pin every test database to its version.**  A PostgreSQL image the tests run names a major and a minor, `postgres:18.6` and never `postgres:18`.
   - A SQL Server image names its update, `<year>-CU<n>` with `-GDR<n>` when the build has one, then `-ubuntu-<version>`, and never `-latest`, a bare year or an on-demand hotfix, and `SqlServerContainer.ImageBuilds` in `tests/DatabaseTests/TestHarness/SqlServerContainer.cs` holds the build its server reports.
   - Moving the default image of either engine records again, in the same PR, what was recorded on it.
   - For PostgreSQL that is `DefaultImages.Postgres` in `tests/DatabaseTests/TestHarness/DefaultImages.cs`, whose tag is also in `examples/PostgresExamples/docker-compose.yml`, the describer's fixtures, written by `tools/record-fixtures.sh postgres`, and the sidecars of the end-to-end project, of the realistic cases and of the examples, written by `tools/describe-end-to-end.sh`.
   - For SQL Server it is `DefaultImages.SqlServer` in the same file, whose tag is also in `examples/SqlServerExamples/docker-compose.yml`, with its row of `ImageBuilds`, the describer's fixtures, written by `tools/record-fixtures.sh mssql`, and the sidecars of the end-to-end project, of the realistic cases and of the examples, written by `tools/describe-end-to-end.sh`.
   - Nothing is recorded on a floor image, and moving one changes the places that name it and runs the tests on it: `postgres:14.24` is in `.github/workflows/build.yml`, `CONTRIBUTING.md` and `tests/DatabaseTests/Postgres/README.md`, and `mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04` in `.github/workflows/build.yml`, `CONTRIBUTING.md`, `tests/DatabaseTests/SqlServer/README.md` and its row of `ImageBuilds`.
   - The same holds for the images of the nightly run, each supported major between the floor and the default: `postgres:15.19`, `postgres:16.15` and `postgres:17.11` are in `.github/workflows/nightly.yml` and `tests/DatabaseTests/Postgres/README.md`, and `mcr.microsoft.com/mssql/server:2019-CU32-GDR12-ubuntu-20.04` and `mcr.microsoft.com/mssql/server:2022-CU27-GDR1-ubuntu-22.04` in `.github/workflows/nightly.yml`, `tests/DatabaseTests/SqlServer/README.md` and their rows of `ImageBuilds`.
   - The PostGIS image of the NetTopologySuite tests is built by `tests/DatabaseTests/TestHarness/Postgis/Dockerfile` from the run's pinned `postgres` image with PostGIS `3.6.4+dfsg-2.pgdg13+1`, the version named in that file's default and in `PostgisContainer.Package` of `tests/DatabaseTests/TestHarness/PostgisContainer.cs`, which a test keeps equal.  Moving it runs the PostGIS tests on each image CI and the nightly run on, and nothing is recorded on it but the sidecars of `tests/DatabaseTests/Postgres/EndToEnd/NetTopologySuite/`, written by `tools/describe-end-to-end.sh`.
   - See the image sections of `tests/DatabaseTests/Postgres/README.md` and `tests/DatabaseTests/SqlServer/README.md`.

## Commits

1. **Validate before you commit.**  Every commit must pass `./pre-commit-validation.sh`, and no hook runs it for you: run it after your last edit and fix what it reports before you commit.
   - It checks only what the uncommitted change can affect and names what it skipped.
   - A run only covers the tree it ran on; an edit made after a passing run, however small, needs another run.
2. **Never push a branch that has not passed.**  This is the rule that matters most.
   - Before every push, run `./pre-commit-validation.sh --full` on the tree exactly as it will be pushed and see it pass; a plain run skips checks.
   - CI runs the same checks, and each engine's describer tests again on that engine's oldest supported image, which the script does not: after a change to a describer or to its tests, also run that describer's project on its floor image, `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24` for `tests/DatabaseTests/Postgres` and `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04` for `tests/DatabaseTests/SqlServer`.
   - A failure in CI is one you could have found locally, and it costs a full CI run, a fix, another push and another wait.
   - Do not push on the strength of an earlier run, of a run on another commit, or of the belief that a change is too small to break anything.
3. **Never skip a check.**  Do not commit or push past a failure, and do not edit the script to make one go away.  If a check is wrong, tell the user.
4. **It needs the full toolchain.**  The script fails when Docker is not running or when the local tools have not been restored.  Fix the environment; see `CONTRIBUTING.md`.

## Pull requests

1. **Check the version before you open a pull request.**  Run `git fetch --tags origin`, then compare `VersionPrefix` in `Directory.Build.props` with the highest release tag, `git tag --list 'v*' --sort=-v:refname | head -n 1`.  If `VersionPrefix` is not greater than that tag, ask the user what the new version should be and set it in the same pull request.  When the repository has no `v*` tags there is nothing to compare.
2. **Run the branch-review skill when the work is complete, and again before every later push.**
   - When the work on a branch is complete, run `branch-review:review` on your own, without waiting to be asked, address its findings and run it again until it comes back clean, and only then offer to open a pull request.
   - After that, run it again the same way before every push, on the branch exactly as it will be pushed.
   - A review covers only the commits it saw: a commit made after a clean review needs another one.

## Reviewing maintainability

A periodic review of the code's folder growth, comments and duplicated patterns follows the skill in `.claude/skills/reviewing-maintainability/`, which says what is fixed in the review's pull request and what is recorded in `docs/tech-debt`.

## AGENTS.md guidelines

1. Always use AGENTS.md files for AI guidance.
2. Add folder-level AGENTS.md files to give AI agents non-obvious context to work safely with the code in that folder.  Add them where there is complex code with patterns or structure that may not be clear, or where additional context is beneficial when working with the code.
3. Be brief and direct.  Do not describe what the folder "does" - focus on actionable guidance that helps navigate the code and avoid pitfalls.  Descriptions of how code works go in `docs/architecture/`, and the AGENTS.md links to them.
4. An AGENTS.md holds rules, not explanation.  Each rule keeps its statement and at most one sentence of reason, and a rule about how code works links to the page in `docs/architecture/` that explains it.  A rule keeps more only when an agent could not follow it safely without the reason, as with the rule that nothing a `.sql` file holds can make the describer run a statement.
5. Do not repeat source code in an AGENTS.md file - link to the actual source files instead.  Brief code snippets that demonstrate a common pattern or pitfall are acceptable.
6. Keep lists of files and folders in sync.  Once an AGENTS.md names a specific file, folder, type, function, etc. it is coupled to that name.  When you rename, move, split, or delete those things, update the AGENTS.md **in the same commit**.  Treat it as part of the change, not a follow-up.  Any AGENTS.md that lists files or folders must end with a one-line maintenance footer reminding future editors of this coupling.
7. DO NOT list child AGENTS.md files from a parent AGENTS.md or instruct agents to read them - AGENTS.md files are loaded automatically by the tooling.
8. Keep only what's true, non-obvious, brief, and at the deepest level it applies to.
9. Project choices between real alternatives are never obvious; keep them.
10. New nested AGENTS.md files: allowed.
11. File-specific quirks: comment in that file.
12. Push-down: aggressive.
13. One rule per bullet, of at most 80 words, and a file of at most 8 KB, or 16 KB for this root file; `tests/SqlSource.Tests/Documentation/AgentsFileTests.cs` checks both.  Detail an agent needs only for some tasks, such as how a describer reads a database, goes in `docs/architecture/`, which an agent reads when its task needs it.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
