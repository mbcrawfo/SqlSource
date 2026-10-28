# Pre-commit validation skips: design

## Intent

`./pre-commit-validation.sh` takes about 4m25s on a warm tree with nothing to rebuild, and it runs before every commit.  Most of that time goes to checks the change cannot affect.  A pre-commit run should skip the steps and test projects that the uncommitted change cannot affect, and say what it skipped.  A push still runs everything.

Measured on a warm tree with no changes (Apple silicon, SQL Server under emulation):

| Step | Time |
|----|----|
| format (C#) | 65s: style 19s, analyzers 34s, CSharpier 2s |
| editorconfig-checker, shellcheck, shfmt, actionlint | 5s |
| build | 8s |
| test (projects in parallel) | 168s: SqlServer 166s, SqlClient61 95s, Postgres 62s, Tests 26s, Tool.Tests 23s, RoslynFloor 19s, Npgsql8 11s |
| package, package-install, tool-install | 19s |

Run alone, SqlServer takes 122s.  Of that, 55s is its 26 `EndToEnd.AllocationTests`, which run serialized after every other test, on a second container.

## Goals

- A plain run checks only what the uncommitted change (staged, unstaged and untracked) could break.
- `--full` runs everything, as the script does today.  Every push uses it.
- A path that no rule recognizes leads to a full run, so a new folder or file is never skipped by accident.
- The table that decides which database tests run is checked by tests.  A test that starts reading a file the table does not account for fails, instead of being skipped silently.
- The summary names every skipped step or project and why, and a partial run ends with a reminder to run `--full` before pushing.

## Non-goals

- No change to CI, which keeps running everything.
- No caching of earlier passing runs.
- No skipping of the build.  It takes 8s when nothing changed, and the unit tests need it.
- No checked table for the format, linter and package rules.  They follow from what each tool reads, which is not code the tests change.

## Anti-goals

- A plain run that passes on a change it skipped tests for, when those tests would have failed.  The rules err toward running.
- A clean tree that reports a pass without checking anything.
- A hand-kept table that nothing checks.

## Approaches

- **A. Path rules per step (chosen).**  Each changed path is sorted into a kind, each step lists the kinds it needs, and a path of no known kind means a full run.  This gets nearly all of the savings measured above for about 100 lines of bash.
- **B. Affected projects from the project references.**  This is more precise for `src/` changes, but tests read files outside the reference graph (describer fixtures, docs, linked build files), so it still needs hand-kept exceptions.  Most `src/` commits touch both shipped projects anyway.
- **C. Coarse modes: docs-only, tests-only or everything.**  This is the simplest, but it misses the format and allocation-test savings.

The owner chose A, then chose to have the test table checked by tests in the same change rather than record it as tech debt.

## Decided

### The change set and `--full`

- The change set is `git diff --name-only --no-renames HEAD` plus `git ls-files --others --exclude-standard`.  A clean tree means a full run, since that run is almost always a check before a push.
- `--full` ignores the change set.  The push rule in `AGENTS.md` (Commits, rule 2) requires it.

### Paths of no known kind

The step rules below are positive triggers, so on their own a file of a kind nobody planned for would skip almost everything.  The script therefore keeps a list of known kinds, and a changed path outside it makes the whole run full.  The known kinds are:
- by extension: `.cs`, `.csproj`, `.props`, `.targets`, `.slnx`, `.md`, `.sql`, `.json` (fixtures, sidecars, schemas and settings), `.sh`, `.txt`, snapshot files (`*.verified.*`), and `.yml` under `.github/workflows/` or `examples/*/docker-compose.yml`;
- by name: `.editorconfig`, `.gitignore`.

Anything else, such as `LICENSE`, `.gitattributes` or a new extension, means a full run.  These are rare, so the cost is small.  The builder confirms the list against `git ls-files` and adds any kind already in the repository whose readers are clear.

### Format

- It is skipped when no `.cs` file changed.
- Otherwise `dotnet format style` and `dotnet format analyzers` take `--include` with the changed `.cs` files that still exist, and CSharpier checks those files.  `--include` took the two `dotnet format` checks from about 55s to about 15s for two files.
- A change to `format.sh`, or to any file that the formatters read as configuration, runs format on everything.  These are `.editorconfig`, any `*.globalconfig`, any `*.props`, `*.targets` or `*.csproj` file (`Directory.Packages.props` pins the analyzers), `global.json`, `dotnet-tools.json` (it pins CSharpier) and any CSharpier configuration or ignore file.

### Linters

- editorconfig-checker always runs (3s).
- shellcheck and shfmt run only when a `*.sh` file or `.editorconfig` changed.  shfmt takes its layout from `.editorconfig`.
- actionlint runs only when a file under `.github/workflows/` or `tools/actionlint.sh` changed.

### Tests

- The script writes a temporary solution filter (`.slnf`) naming the projects to run, so the chosen projects still run in parallel.  `dotnet test --solution <file>.slnf` was checked and works.
- `SqlSource.Tests`, `SqlSource.Tests.RoslynFloor` and `SqlSource.Tool.Tests` always run.  They take 26s at most, in parallel, and `SqlSource.Tests` reads the docs, the workflows and the schemas.
- The PostgreSQL pair (`Tests.Postgres`, `Tests.Postgres.Npgsql8`) and the SQL Server pair (`Tests.SqlServer`, `Tests.SqlServer.SqlClient61`) are decided separately, by the test scopes file below.
- **Allocation tests.**  `--filter-not-class '*.EndToEnd.AllocationTests'` drops the `AllocationTests` of both engines.  It was checked and drops exactly 26 SQL Server and 17 PostgreSQL tests.  It is added unless the change set holds a path that can change what those tests measure:
  - a path under `src/SqlSource/` other than a `*.md` file;
  - a path under `tests/SqlSource.Testing/`;
  - a path under a container project's `EndToEnd/` folder: its queries, sidecars, seeds and the query classes the tests call;
  - a root `Directory.*.props` file, which holds the compile settings and the driver versions, or a container project's own `.csproj`, which can override them;
  - a file whose name contains `Allocation`.

  This keeps the saving for the common case, describer or fixture work, and errs toward running for everything that reaches the generated code, the drivers or the measured queries.

### The test scopes file

`tools/test-scopes.txt` holds the table.  The script and the tests both read it.

- **Format.**
  - Each line is `<scope> <pattern>`.  Blank lines and lines starting with `#` are ignored.
  - The scope is `none` (neither pair), `postgres`, `sqlserver` or `both`.
  - The pattern is a path from the repository root, with `/` as separator.  `*` matches any run of characters, `/` included, and every other character is literal.  The patterns are kept this small so that bash's `[[ $path == $pattern ]]` and the C# matcher agree without a test between them.
- **Matching.**  The first line whose pattern matches a path decides that path's scope.  A path that no line matches is `both`.  The pairs that run are the union over the change set, and a change to `tools/test-scopes.txt` itself runs both pairs.
- **Starting content**, in this order, so that the exceptions come before the broad lines:

  | Scope | Patterns |
  |----|----|
  | postgres | `src/SqlSource.Tool/Describing/Postgres/*`, `tests/SqlSource.Tests.Postgres/*`, `tests/SqlSource.Tests.Postgres.Npgsql8/*`, `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/*`, `tests/SqlSource.Tool.Tests/Fixtures/Projects/Single/*` |
  | sqlserver | `src/SqlSource.Tool/Describing/SqlServer/*`, `tests/SqlSource.Tests.SqlServer/*`, `tests/SqlSource.Tests.SqlServer.SqlClient61/*`, `tests/SqlSource.Tool.Tests/Fixtures/Describers/mssql/*`, `tests/SqlSource.Tool.Tests/Fixtures/Projects/SqlServer/*` |
  | none | `docs/*`, `examples/*`, `tools/*`, `.github/*`, `tests/SqlSource.Tests/*`, `tests/SqlSource.Tests.RoslynFloor/*`, `tests/SqlSource.Tool.Tests/*`, `*.md` |
  | both | every path not matched above, including the rest of `src/`, `tests/SqlSource.Testing/`, `tests/SqlSource.Tool.Testing/`, `schemas/`, and the settings and build files of `tests/` and of the root |

  Because the first match wins, a `.md` file in a container project counts for its engine, and the two `Fixtures/Projects` folders count for theirs rather than `none`.

### The invariant the checks hold

A container project may read a repository file outside its own folder only when the file's scope runs that project: its own engine or `both`.  That covers two kinds of reads, each with its own check:

- **Build-time inputs.**  A test in `SqlSource.Tests`, which runs on every change, reads the `.csproj` of each of the four container projects.
  - It takes every `Include` that leaves the project's folder (`ProjectReference` included), expands its glob against the files on disk, and classifies each file with the scopes file.
  - It fails, naming the file and the project, when one falls in `none` or in the other engine.
  - This is the check that would have caught the `Fixtures/Projects/Single` and `Fixtures/Projects/SqlServer` links, which the first draft of this design marked as safe to skip.
- **Run-time reads.**  `RepositoryRoot.Folder` (in `tests/SqlSource.Testing/RepositoryRoot.cs`) is replaced by `RepositoryRoot.Path(params string[] parts)`, which returns the path and checks it first.
  - The running project is the entry assembly's name, which under Microsoft.Testing.Platform is the test project.
  - For the four container projects, it throws when the path, or for a folder any file under it, falls in a scope that does not run the project.  Every other project passes unchecked, since it always runs.
  - The roughly 17 call sites in 12 files move to it, and paths built by `Path.Combine` on the root move inside the call.  For example, `RepositoryPaths.DescriberFixtures` names the engine's own subfolder, not the shared `Describers` folder.
  - A test that adds a new read fails the first time it runs, and it runs in that change, because the change touches its own project.
- **The root as a working folder.**  The end-to-end `CheckTests` start the tool in the repository root and read no file there themselves.  They get the root from `RepositoryRoot.WorkingFolder`, which is not a `string`, so it cannot be passed to `Path.Combine` by accident; `CliRun.Real` takes it.  What the tool reads from there is the settings and build files of `tests/` and the root, which are `both`.
- **The file itself.**  A test in `SqlSource.Tests` checks that every line parses, and that every pattern matches at least one file in the repository, so a renamed folder cannot leave a dead line behind.

### Package checks

- They run when the change set holds a path under `src/`, a root build file (`Directory.*.props`, `global.json`, `*.slnx`, `dotnet-tools.json`), `README.md`, `tools/package-install/**` or a `tools/check-*.sh` script.  Otherwise all three are skipped.
- In every run, the packages are packed once into a temporary folder that is passed to all three checks, as CI does.

### Docs in the same PR

- `AGENTS.md`, Commits: rule 1 runs the plain script, and rule 2 requires `--full` before every push.
- `CONTRIBUTING.md`, Checks: the skips and `--full`.
- `tools/README.md`: the scopes file, its format and how to change it.
- `tests/README.md`: reading the repository through `RepositoryRoot.Path`, and what to do when the check refuses a path.
- `tests/AGENTS.md`: a test reads the repository only through `RepositoryRoot.Path`, and a new read outside the project that the check refuses goes in `tools/test-scopes.txt`.
- Every other file that names the script and states what a run covers: `tools/AGENTS.md`, both container test readmes and `.github/AGENTS.md`.
- No tech-debt entry: the table is checked.

## My calls

- **The engine split** (a describer change of one engine skips the other engine's pair): it was not discussed, and it saves 166s of wall time on a PostgreSQL-only change.
- **`--plan`**: it prints what would run and why, and runs nothing.  It is used to verify the rules on sample changes and is cheap to keep.
- **A clean tree means a full run**, as above.
- **The scopes file**: the name `tools/test-scopes.txt`, a line format that bash reads without `jq`, which is not a required tool, and the small pattern language.
- **`RepositoryRoot.WorkingFolder`** as a type of its own, rather than a source-scanning test, to keep the root out of `Path.Combine`.
- **The list of known kinds**, with a full run for anything outside it, so that the positive step triggers never skip a kind of file nobody planned for.
- **The allocation trigger** widened to the end-to-end folders and the root `Directory.*.props` files, and the filter applied to both engines.

## Left to the builder

- Shape the script's rules as bash `case` patterns in one function per decision, near the top of the script, apart from the scopes file.
- Confirm that the entry assembly is the test project under Microsoft.Testing.Platform, on all three operating systems the nightly run uses.  If it is not, find the running project another way, such as an assembly attribute set by `tests/SqlSource.Testing`.
- Normalize separators to `/` before matching, since the nightly run runs the unit tests on Windows.
- Verify with `--plan` on one sample change for each row of the scopes table, then with a real plain run and a `--full` run.  Then show that each check refuses: link a `none` file into a container project, and read a `none` file from a container test.
