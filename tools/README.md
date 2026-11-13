# Tools

This folder holds the scripts that `pre-commit-validation.sh` and `.github/workflows/build.yml` run, the consumer project that one of them installs the package into, the file that says which database tests a change can skip, a script that builds the manual's site, a script that pulls the test images, two recording scripts and the script and program of mutation testing and of the test review, none of which is a check.  [`CONTRIBUTING.md`](../CONTRIBUTING.md) says when to run them; [`AGENTS.md`](AGENTS.md) holds the traps of changing them.

## Linters

Each linter script runs its tool from a pinned Docker image, so they need Docker:

| Script | Checks |
|----|----|
| `tools/editorconfig-checker.sh` | Every file against `.editorconfig` |
| `tools/shellcheck.sh` | Shell scripts, with ShellCheck |
| `tools/shfmt.sh --check` | Shell script formatting; without `--check` it rewrites the files |
| `tools/actionlint.sh` | GitHub Actions workflows |

## The site

`tools/build-site.sh` builds the manual, `site/`, into `artifacts/site` with docfx, the local tool of `dotnet-tools.json`, so it needs `dotnet tool restore` first.  Any docfx warning fails it: a link to a page or an anchor that is not there.  It also fails when the output holds a page built from an `AGENTS.md` or a `CLAUDE*.md`, which `site/docfx.json` excludes.  The `Site` job of `.github/workflows/build.yml` runs it on every pull request, and `.github/workflows/docs.yml` before it publishes the site.

## Package checks

The three package checks look at what `dotnet pack SqlSource.slnx --output artifacts/packages` wrote.  Each takes that folder as its argument.

### `check-package.sh`

```bash
tools/check-package.sh artifacts/packages
```

This checks what the two packages hold.  `SqlSource`: the generator under `analyzers/`, `build/SqlSource.props`, `build/SqlSource.targets`, `build/SqlSource.AfterSdk.targets` and `buildMultiTargeting/SqlSource.targets`, the readme, and nothing under `lib/`.  `SqlSource.Tool`: the tool under `tools/net8.0/any/` with the generator's assembly, Roslyn, the library that reads a solution, Npgsql with `Npgsql.NetTopologySuite`, NetTopologySuite and `NetTopologySuite.IO.PostGis`, which its PostgreSQL describer registers to bind a PostGIS parameter, and Microsoft.Data.SqlClient beside it, SqlClient's copies for Unix and Windows and its native SNI for Windows under `runtimes/`, `DotnetToolSettings.xml`, the readme, which is `src/SqlSource.Tool/README.md`, and no documentation file of an assembly.  It fails when the two have different versions.  Without an argument it packs both into a temporary folder first.

### `check-package-install.sh`

```bash
tools/check-package-install.sh artifacts/packages
```

This installs the package the way a consumer does, and runs the tool's package through the build.  It copies the project in `tools/package-install` to a temporary folder outside the repository, adds the package to it with `dotnet add package` from a feed that holds nothing else, builds and runs it, and compares what it prints with `tools/package-install/expected-output.txt`.

The project's files are these.  `Consumer.csproj` and `Directory.Build.targets` are its MSBuild files, `sqlsource.json` is its settings file, `Queries.cs` has the type that the generator fills from the `.sql` files of `Queries/`, and `Program.cs` prints one line for each thing it checks.  The project uses a constant, a method with tokens, the generator parameters and the dialect, each in the settings file and in a file header with the markers `-- generator:` and `-- dialect:`, and a dialect with an option, so it fails when the generator, either MSBuild file or the settings file does not reach a consumer.  The settings file sets `"root": true`, since the copy is outside the repository, `output` to `Sql`, so its queries need no sidecar, and `memberLocation` to `class`, so that their members are on the type `Queries`, which `Program.cs` reads.

Its `Directory.Build.targets` has a target that adds a `.sql` file while the build runs, whose header names its dialect.  The SDK imports that file after the targets of a package, so the project also fails when a file that a target adds does not reach the generator.

The script then asks the project what the `sqlsource` tool asks one.  `SqlSourceImported` must be `true`, which shows that NuGet imported the package's props.  The target `SqlSourceWriteManifest`, run by name, must write a manifest that starts with its version, lists a `.sql` file, holds a `Settings` line with the full path of the copy's `sqlsource.json`, and does not list `Queries/AddedByATarget.sql`, the file that `Directory.Build.targets` adds from a hook of the build.  A target that is run by name does not run that hook, which is [TD-0025](../docs/tech-debt/TD-0025-manifest-misses-a-file-that-a-build-hook-adds.md); the build of the consumer compiled the file, and `expected-output.txt` has its query.

It then deletes a `.sql` file the project uses and builds again, and fails unless that build reports the missing member: an incremental build must notice that a file is gone.

Then it writes a web project, of `Microsoft.NET.Sdk.Web`, that references the package and has a `.sql` file, its sidecar and a settings file, `sqlsource.json`, publishes it, and fails when any of them is in its `bin` folder or in what `dotnet publish` wrote.  The Web SDK copies a `.json` file to both unless the package says otherwise, and the consumer, of the plain SDK, copies none.  The project claims no file, so the sidecar can be empty, and the settings file sets nothing.  Two builds use a stand-in for the `sqlsource` tool, a shell script that logs its arguments, so the script clears `CI`, `TF_BUILD`, `TEAMCITY_VERSION`, `JENKINS_URL`, `GITHUB_ACTIONS`, `SQLSOURCE_LOG` and `SQLSOURCE_CONNECTION*` first, since CI sets the first of them and that turns the online build off.  The first rebuilds the consumer, which has no connection and does not set `SqlSourceOnline`, and fails when the tool ran or the build warned.  The script sets `DOTNET_CLI_UI_LANGUAGE` to `en` for every run of `dotnet`, since it reads MSBuild's own lines, which are in the language of the user's interface.

Then it writes a library of two target frameworks, `netstandard2.1;net10.0`, which share the SDK's reference assemblies so that the restore still needs no other feed, and builds it with `SqlSourceOnline=true`.  NuGet imports `buildMultiTargeting/SqlSource.targets` only for a package reference, so this is the one test of that file.  The script fails unless the stand-in ran once, with the manifest of `netstandard2.1`, the first framework, and the log of the build shows the outer build's `SqlSourceRunOnlineBeforeDispatch` finished before `DispatchToInnerBuilds` started, and both frameworks produced a DLL.  Without that file in the package an inner build still runs the tool once, so the order in the log is what fails.

Last, it runs the packed tool through the build, with no database.  The folder must hold `SqlSource.Tool.<version>.nupkg` of the generator's version beside it, and the script fails naming the file when it does not.  It writes a library of one query, `GetUser`, whose `Users.sql` and `Users.sql.json` it copies from `examples/PostgresExamples/Models/`, since `tools/describe-end-to-end.sh` keeps that entry current for the tool of `VersionPrefix`, with a settings file of the dialect `postgres` and `TreatWarningsAsErrors`.  It installs the tool into it with `dotnet new tool-manifest` and `dotnet tool install` from the same feed, under a `DOTNET_CLI_HOME` of its own, since dotnet caches where the local tool of a version is and would otherwise run a tool of the same version installed before.  Then, each failing with its own line:

- `dotnet sqlsource --version` prints the package's version.
- With a `.env` that holds no key the tool reads, which would start the tool under `auto`, a build with `SqlSourceOnline` `false` passes, and its detailed log holds no `describe --manifest`, so the tool did not run.
- With the same `.env` and `SqlSourceOnline` unset, so `auto`, the build passes and its detailed log holds the tool's `sqlsource: summary:` line, since a current entry needs no connection.
- The script changes the SQL of `GetUser`, so that its entry is stale.  `dotnet sqlsource describe` must exit with 1 and report `SQLSRC213` at `Users.sql`, and the build under `auto` must fail, show the tool's line `sqlsource: offline: no connection for postgres; the build uses the committed sidecars` and report the generator's `SQLSRC244` at `Queries/Users.sql`.
- Without the `.env`, a build with `SqlSourceOnline` `true` must fail with the tool's `SQLSRC213` at `Users.sql`, on one line that ends with its `see:` link, and no `MSB3073` of Exec.
- `dotnet sqlsource replay` of `tests/SqlSource.Tool.Tests/Fixtures/Bundles/postgres/select-left-join`, a committed folder of a bug-report bundle, exits with 0 and prints `sqlsource: reproduced: the pipeline gives this answer`.  A replay needs no database, project or MSBuild, so this shows that the packed tool carries what a replay needs.

Every check reads a line that only the real tool writes.  Without an argument the script packs both packages first.

### `check-tool-install.sh`

```bash
tools/check-tool-install.sh artifacts/packages
```

This installs the tool the way a user does: `dotnet tool install` into a temporary folder outside the repository, from a feed that holds nothing else.  It then runs `sqlsource --version` and compares it with the package's version, runs `sqlsource --help`, and runs `sqlsource describe` in an empty folder, which must exit with 1 and report `SQLSRC201`.  Then it writes a project of one `mssql` query beside SqlSource's MSBuild files, copied from `src/SqlSource/build/`, with a settings file that sets `"root": true`, the dialect `mssql` and the database `orders`, never restored, and describes it with a connection to the port 1 of the machine, which refuses: the run must exit with 1 and report `SQLSRC224` with the text that SqlClient could not reach the server, and no `SQLSRC200`.  SqlClient is loaded only to connect, and the copy outside `runtimes/` is a stub that throws on any use, which the tool reports as `SQLSRC224` with another text, so this shows that the copy for the platform loads.  A tool's package carries everything the tool loads, so this is what shows that all of it is there.

Last, it runs `sqlsource --version` again with `DOTNET_ROLL_FORWARD` set to `LatestMajor`, which starts the tool on the newest runtime the machine has.  The tool targets .NET 8 and rolls forward on a machine that has only a later one, and this proves it wherever the script runs.  Without an argument the script packs first.

## Formatting

`format.sh` is at the root of the repository, and `pre-commit-validation.sh` runs it as `format.sh --check`.

```bash
./format.sh [--check] [style|analyzers|csharpier ...] [-- file ...]
```

Without `--check` it rewrites files in place.  The part names choose which of `dotnet format style`, `dotnet format analyzers` and CSharpier run, always in that order, and all three run when none is given.  Files after `--`, as paths from the repository root, limit all three parts to those files: the two `dotnet format` parts take them as `--include`, and CSharpier checks them in place of the tree.  That is how a pre-commit run checks only the `.cs` files that changed.  `--` with no file after it is a usage error.

## Test scopes

`tools/test-scopes.txt` says which database test projects a change to a path can affect.  `pre-commit-validation.sh` reads it to skip the PostgreSQL pair of test projects (`tests/DatabaseTests/Postgres` and `tests/DatabaseTests/Postgres.Npgsql8`) or the SQL Server pair (`tests/DatabaseTests/SqlServer` and `tests/DatabaseTests/SqlServer.SqlClient61`) when nothing in the uncommitted change reaches them, and the tests read it to check that a skip is safe.  `--full` ignores it, and so does CI.

A line is `<scope> <pattern>`:

- The scope is `none` (neither pair), `postgres`, `sqlserver` or `both`.
- The pattern is a path from the repository root, with `/` as the separator.  `*` matches any run of characters, `/` included, and every other character is literal.  A pattern with `?`, `[`, `]`, `\`, `(`, `)`, `!`, `+` or `@` in it is refused, since bash would read them as glob characters, as an escape or as an extended glob operator.
- Blank lines and lines that start with `#` are ignored.
- The first line whose pattern matches a path decides that path's scope, so an exception goes before the broad line it carves out of.  A path that no line matches is `both`.
- The pairs that run are the union over every changed path.  A change to the file itself runs both.
- A `project <postgres|sqlserver> <path>` line names a container project's `.csproj` and the engine that runs it.  The four container projects are the only ones listed, and every other test project always runs.

To change it, edit the file and run the tests; `tests/SqlSource.Repository.Tests/Validation/` checks:

- Every line parses and every pattern matches at least one file of the repository, so a renamed folder cannot leave a dead line behind (`TestScopesFileTests`).
- Every file that a container project includes from outside its own folder, a linked file, a referenced project or an import, has a scope that runs that project (`ContainerProjectInputsTests`).  The check reads the project's `.csproj`, every `Directory.Build.props` and `Directory.Build.targets` from its folder up to the root, and every project under `tests/` that it references, directly or not, with the `Directory.Build` files above that project, and every file under `tests/` that an import of any of those names, `tests/Fixtures.props` among them.  A project that links a file whose scope is `none` or the other engine's fails there.
- In a container project, `RepositoryRoot.Path` throws when the path, or a file below it, has a scope that would let a change to it skip that project (`RepositoryRootTests`).  A read that bypasses `Path` is not caught, which is why [`tests/AGENTS.md`](../tests/AGENTS.md) forbids it.  [`tests/README.md`](../tests/README.md#reading-the-repository) says what to do when `Path` throws.

`pre-commit-validation.sh --plan <path> ...` prints what a run would do for those paths and runs nothing, which shows the effect of a line.

## Pulling the test images

`tools/pull-test-images.sh [image ...]` is a step of the test legs on PostgreSQL and SQL Server of `build.yml` and of the `allocation-postgres`, `allocation-sqlserver`, `sync-async-postgres` and `sync-async-sqlserver` jobs of `nightly.yml`, and no check, and `pre-commit-validation.sh` does not run it.  It runs `docker pull` once for the default image of each engine, which it reads from the lines of `tests/DatabaseTests/TestHarness/DefaultImages.cs` that declare `Postgres` and `SqlServer`, and once for each image given as an argument, and prints `<image>: <n> s` for each.  Those legs run it with no argument, after their build; each floor job pulls its own floor image with `docker pull`.  A person can run it to have every image before a first run of the tests, which on Apple silicon saves minutes of the first test.

## Building the PostGIS image

`tools/build-postgis-image.sh [--name | --id | --unchanged <id>] [postgres-image]` is a step of each CI job that runs a PostGIS test, the test leg on PostgreSQL and the `test-postgres-floor` job of `build.yml` and the `test-postgres` and `sync-async-postgres` jobs of `nightly.yml`, and no check, and `pre-commit-validation.sh` does not run it.  It builds `tests/DatabaseTests/TestHarness/Postgis/Dockerfile` on the postgres image given, or on the default image, read from the line of `tests/DatabaseTests/TestHarness/DefaultImages.cs` that declares `Postgres`, with the package of the line of `PostgisContainer.cs` that declares `Package`, under the name that `PostgisContainer` looks for: `sqlsource-postgis:<postgres tag>-<PostGIS version>-<hash>`, where the hash is the first twelve hex digits of the SHA-256 of the Dockerfile without its carriage returns.  It always builds, through Docker's cache, with `--progress=plain`, so that a failed build fails its step with Docker's whole output; the tests then find the image and build nothing.  `--name` prints the name and builds nothing, and `PostgisImageScriptTests` of `tests/DatabaseTests/Postgres` checks it against the harness's.  `--id` prints the image's ID, which each of those jobs keeps after its build step, and `--unchanged <id>`, which the job runs after its tests, fails when the image's ID is no longer that one, as when the tests built the image again over it, which they do only if `PostgisContainer` no longer builds only a missing image; a name of the tests' own that differs from the script's leaves the script's image as it was, and is `PostgisImageScriptTests`' to catch; `PostgisImageCheckTests` tests both.  `tools/describe-end-to-end.sh` builds the image through it.

## Recording scripts

None of these is a check, and a person runs them.  `pre-commit-validation.sh` and `build.yml` run none of them.  Each reads the default image of the engine it records from the line of `tests/DatabaseTests/TestHarness/DefaultImages.cs` that declares it, and fails when it finds none: both read `Postgres` and `SqlServer`.

- `tools/record-fixtures.sh [postgres|mssql]` records the cases of one describer's fixtures, or of both when it is given no engine; any other argument is a usage error.  It records on the default image of the engine, or on the image of `SQLSOURCE_TESTS_POSTGRES_IMAGE` or `SQLSOURCE_TESTS_SQLSERVER_IMAGE` when the variable is set.  The procedures are [the PostgreSQL describer's fixtures](../tests/DatabaseTests/Postgres/README.md#the-describers-fixtures) and [the SQL Server describer's](../tests/DatabaseTests/SqlServer/README.md#the-describers-fixtures).
- `tools/describe-end-to-end.sh` writes every sidecar of both end-to-end projects, of both engines' realistic cases and of both example projects, `examples/PostgresExamples` and `examples/SqlServerExamples`, from nothing, on the default image of each engine alone; [PostgreSQL's end-to-end project](../tests/DatabaseTests/Postgres/README.md#writing-the-sidecars) and [SQL Server's](../tests/DatabaseTests/SqlServer/README.md#writing-the-sidecars) have the procedure.  It runs no test, since a project does not build until its sidecars are current, and has a part for each engine, which starts a container of its own with `docker run` and removes it.  PostgreSQL's applies `Realistic/schema.sql` and `Realistic/seed.sql` of `tests/DatabaseTests/Postgres` to a database `realistic`, and the schemas and seeds of `app` and `billing`, with `psql`.  It also builds the PostGIS image of the tests with [`tools/build-postgis-image.sh`](#building-the-postgis-image) on the default image, keeps it, and starts a second container of it for the schema and seed of `gis`, which the end-to-end project's `EndToEnd/NetTopologySuite/` names, and for the examples' schema, which creates the extension `postgis`, on a database `examples`.  SQL Server's starts its image with `--platform linux/amd64`, which an arm64 host runs under emulation, and applies `Realistic/schema.sql` and `Realistic/seed.sql` of `tests/DatabaseTests/SqlServer` to a database `realistic`, then the schemas and seeds of `app` and `billing` of its `EndToEnd/`, with `new-types.sql` after the seed of `app`, for the queries of `EndToEnd/Queries/`, `EndToEnd/NodaTime/Queries/` and `EndToEnd/NetTopologySuite/Queries/`, and the examples' schema to a database `examples`, with the image's `sqlcmd`, given `-I` so that `QUOTED_IDENTIFIER` is on, as SqlClient has it.  Each part then runs the tool from its source with `describe --force`.

## Mutation testing

`tools/mutation-test.sh` runs a mutation test of one of the two source projects with [Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/), the local tool `dotnet-stryker`: Stryker changes the project's code one small place at a time, each change a mutant, and runs the tests that reach it, so that a mutant no test fails on shows what the tests do not check.  It is no check: a person runs it on a folder of a project, which takes minutes, and neither `pre-commit-validation.sh` nor `build.yml` runs it.

```bash
tools/mutation-test.sh generator --tests 'FullyQualifiedName~SqlSource.Tests.Parsing.' 'Parsing/**/*.cs'
tools/mutation-test.sh tool --output artifacts/mutation/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.DotEnv.' 'DotEnv/*.cs'
tools/mutation-test.sh compare artifacts/mutation/before/reports/mutation-report.json artifacts/mutation/after/reports/mutation-report.json
```

`generator` is `src/SqlSource`, tested by `tests/SqlSource.Tests`, and `tool` is `src/SqlSource.Tool`, tested by `tests/SqlSource.Tool.Tests`.  Each glob names source files from the project's folder and is quoted, so that the shell does not expand it; with none every file is tested.  A folder's glob starts at the folder, `Parsing/**/*.cs`, and never with `**/`: Stryker matches a glob against a file's full path as well, so in a checkout under a folder named `Projects`, `**/Projects/**/*.cs` is every file of the project.  `--tests` is Stryker's `--test-case-filter`, which limits the tests that run: a run is given the folder of the source and the tests of that folder, [since a whole project does not fit in a night](#how-long-a-run-takes).  `--output` is the folder of the run, `artifacts/mutation/<generator|tool>` unless it is given.  The script runs from any folder, exits with Stryker's code, and ends by printing the path of the report, `reports/mutation-report.json` in the output folder, beside `mutation-report.html`, the same report to read in a browser.

Stryker builds and runs in the working tree, so two runs cannot share a checkout: a run holds the folder `artifacts/mutation/.running` as its lock and a second one is refused.  Nothing else that builds, `pre-commit-validation.sh` included, runs in the checkout during a run.

### The two runs of a run

A run of the script is two runs of Stryker.  The first tests nothing: it finds the static mutants, those of a static initialiser or constructor, and writes it to `static/` of the output folder.  The second leaves them out, by the span of each in its file, which `static/exclusions.txt` lists.

This is not an optimisation.  Stryker's runner for the Microsoft Testing Platform tests each mutant in a process that it goes on using for the mutants after it, and a static mutant changes state that the process keeps: the tests that notice that state then fail under the mutants that follow, and Stryker reports those mutants killed, by those tests.  On `Parsing/SqlDialectName.cs`, two mutants that it reported killed pass every test when the change is made by hand, and two identical runs gave 48 and 46 kills.  With the static mutants left out the runs agree with each other, with a run of one worker and with the changes made by hand.  A run whose report still shows a static mutant tested fails, and its report is not to be used.

### How long a run takes

A run is given a folder of the source and the tests of that folder.  Measured on 2026-11-12 on an Apple M3 Max of 14 cores and 36 GB, with Stryker's seven workers, each time the two runs together:

| Run | Tests | Mutants tested | Killed | Survived | Timeout | Time |
|----|----|----|----|----|----|----|
| `generator`, `Parsing/**/*.cs`, the tests of `SqlSource.Tests.Parsing` | 2,284 | 2,260 | 1,968 | 209 | 83 | 38 min |
| `tool`, `Planning/**/*.cs`, the tests of `SqlSource.Tool.Tests.Planning` | 204 | 144 | 122 | 21 | 1 | 3 min |
| `generator`, `Parsing/SqlDialectName.cs`, the tests of `Parsing` and `Generator` | 4,368 | 35 | 30 | 3 | 2 | 18 min |
| `generator`, `Generation/Paths/**/*.cs`, the tests of `SqlSource.Tests.Generation.Paths` | 166 | 149 | 132 | 15 | 2 | 3 min |
| The same files, with the driver tests of `SqlSource.Tests.Generator` added | 2,250 | 149 | 132 | 11 | 6 | 34 min |
| `generator`, `*.cs`, its root files, with the driver tests and the root files' own | 2,213 | 235 | 208 | 13 | 14 | 67 min |
| `tool`, `Projects/**/*.cs`, the tests of `SqlSource.Tool.Tests.Projects` | 316 | 228 | 214 | 12 | 2 | 19 min |

The last four rows are of 2026-11-13.  The tests of `Projects`, some of which run MSBuild, cost about five seconds for each mutant.  The driver tests, which run the whole generator, cost about 17 seconds for each mutant where a folder's own tests cost about one: on `Generation/Paths` they took eleven times as long, killed one of the fifteen mutants that its own tests leave alive, and had three more reported as timeouts, [which may not be kills](#limits).  So a folder is run with its own tests, and the driver tests with the root files alone.

A whole project does not fit in a night, so it is not run.  The generator's one whole run, with every test and Stryker's default time allowance, took 11 hours 41 minutes for 10,782 mutants and reported no survivor, [since it reported each as a timeout](#limits); its report was 738 MB, which `compare` reads in two seconds.  Before a single mutant is tested, a run of every generator test takes eleven minutes to build, run the tests once and capture what each reaches, and the script does that twice.

The generator has 19,868 mutants, of which 4,572 do not compile and 1,687 are static; the tool has 15,207, of which 2,759 do not compile and 1,583 are static.

### What a report holds

`reports/mutation-report.json` is in the [mutation-testing report schema](https://github.com/stryker-mutator/mutation-testing-elements/tree/master/packages/report-schema).  For each source file it holds the mutants: where each is, what it changes, its status, `coveredBy`, the tests that reach it, and `killedBy`, the tests that fail on it.  `testFiles` gives each test's file, full name and line by the id those lists use.  A mutant is `Killed` or `Timeout` when a test notices it, `Survived` when the tests that reach it all pass, `NoCoverage` when none reaches it, `CompileError` when it does not compile, and `Ignored` when the run left it out.

### Comparing two reports

`tools/mutation-test.sh compare <before.json> <after.json>` is the guard of a change to the tests: it names every mutant that the first report has as `Killed` or `Timeout` and the second does not, and exits 0 only when there is none.  It prints one line for each, `LOST` for a mutant that now survives or that no test reaches, and `NOT COMPARABLE` for one the second run left out, could not compile or does not hold, then the counts; it exits 1 when a mutant is lost or not comparable and 2 when a report cannot be read.  A mutant that was killed and now times out is not comparable, since [a timeout is not always a kill](#limits); one that timed out in both runs is kept.  The two runs have the same globs and no change to `src/` between them, since a mutant is matched by its file's path from the project folder, its place, what it changes and what it changes it to.  Reports of two checkouts compare.

`tools/MutationReport` is the program behind it, a console project of the solution that is never packed, whose tests are in `tests/SqlSource.Repository.Tests/MutationReport/`.  Its `static <report.json>` prints the exclusions of a report's static mutants and exits 1 when the report's run tested one, which is how the script finds them and checks its second run.

### The test review's commands

`tools/MutationReport` has three more commands, which the skill in `.claude/skills/reviewing-test-quality/` runs to prepare what its reviewers read.  None of them runs a test or a mutation run: they read what a run of the skill measured into its run folder, `artifacts/test-review/<run>/`.

```bash
dotnet run --project tools/MutationReport -- slices . artifacts/test-review/<run>/slices --exclude-from <file> tests/SqlSource.Tests
dotnet run --project tools/MutationReport -- condense . artifacts/test-review/<run> --broad-from <file>
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills <report.json> SqlLexerTests.Read_Quote_Ends
```

`slices <repository root> <output folder> [--limit <lines>] [--exclude-from <file>] <folder> ...` cuts the `.cs` files of the folders into slices of at most 5,000 lines, or the limit given, so that every file is in exactly one slice and none is split.  It leaves out `bin/`, `obj/` and the paths the exclusion file lists, one to a line with `#` starting a comment, and cuts each folder by itself, and each test project in it by itself, so no slice holds files of two folders or of two projects.  A slice follows folders: a folder that fits is one block, a larger one is its own files and then its sub-folders, each cut the same way, and neighbouring blocks of one folder are joined while they fit.  The files of one partial class stay together.  It writes `<name>.txt` for each slice, its files one to a line, and `index.txt`, a line `<name> <lines> <files>` for each, and refuses an output folder that holds anything, since a second cut would leave the first one's slices beside its own.  A slice is named for the deepest folder that holds all its files, as `SqlSource.Tests.Parsing-2`.

`condense <repository root> <run folder> [--broad-from <file>]` writes `evidence/<slice>.md` for each slice of the run folder, from what the folder holds:

| Path in the run folder | Holds |
|----|----|
| `slices/` | The slices, as `slices` writes them |
| `mutation/<scope>/reports/mutation-report.json` | The report of each mutation scope, and a file `failed` beside `reports/` when the scope's run failed |
| `tests/<project>/results.xunit.xml` | A test project's tests with their times, as `--report-xunit-xml` writes them; `<project>` is its folder from `tests/` |
| `tests/<project>/coverage.cobertura.xml` | The same run's line coverage |

An evidence file lists the slice's files, the mutation scopes that ran its tests, and each test method with its rows, its time, the mutants it kills, how many of those no other test kills, and the smallest set found of other tests that kill all it kills.  It then lists the mutants that survive under the slice's tests, the mutants whose tests ran out of time, the lines of the source its tests reach that no test covers or that no run can test, and each class's share of its project's time.  The rows of a theory are one method, since a report tells them apart only by id.  A broad test, one whose full name starts with a line of the file `--broad-from` names, is never another test's cover and never the other killer that stops a kill being a test's alone.  A slice that no scope ran says so, and does not say that its tests kill nothing.

It also writes `evidence/gaps.md`, the mutants of each source file by what became of them, and `evidence/database-only-lines.md`, the source lines that only the projects under `tests/DatabaseTests/` reach.  It exits 2, naming the scope, for a scope that failed, a report that cannot be read or one whose run tested a static mutant, and when two scopes tested one mutant, whose kills would be counted twice.

`kills <report.json> <test name>` prints each mutant that a test method kills in one scope's report, with the other methods that kill it, and the mutants under it whose tests ran out of time (`TIMEOUT`) or that no test fails on (`SURVIVED`).  The name is a method's full name, or its end, as `SqlLexerTests.Read_Quote_Ends`.  It exits 1 when the report holds no such test.

An agent runs the built program, the third form above, and never `dotnet run`: two builds of one project at once fail, and nothing builds in a checkout during a mutation run.

### Limits

- **Stryker's runner for the Microsoft Testing Platform is a preview**, and its other runner cannot run these projects.  The static mutants are the one fault found in it, on one file checked by hand; any state that a process keeps from one mutant to the next would do the same, so a result that decides something is read by a person, and a scope whose two runs disagree is not to be trusted.  Stryker has an open request for a process for each mutant, [stryker-net#3742](https://github.com/stryker-mutator/stryker-net/issues/3742).
- **Two runs of one scope do not agree to the last mutant.**  Of the 2,275 mutants of `Parsing/`, three runs on one tree differed in five to seven that one run reports `Killed` and another `Timeout`, and in two that one run reports `Killed`, by one test alone, and two report `Survived`: both read memory that an `ArrayPool` hands back, so what the test sees depends on what the process did before.  A kill by one test alone of a mutant that leaves memory unfilled, or reads past what was filled, may not repeat.
- **The static mutants are never tested.**  They are mostly the texts of diagnostics and the tables of names.
- **A method with a mutant that does not compile loses all its mutants.**  Stryker removes them together, and the code's `out` and pattern variables make it common.
- **The rows of a theory share one name.**  Each row has its own id, so a report says how many rows kill a mutant and not which.
- **Only the tests of the run test its mutants.**  A mutant that only a test outside `--tests` fails on is reported as surviving, and so is a mutant of the generator that only a test of the tool or of a database project fails on, since each source project has one test project.
- **Stryker makes every mutant of the project on every run**, whatever the globs, and each run of the script pays for the build, the first run of the tests and the capture of what each test reaches twice.
- **A timeout is not always a kill.**  Stryker gives a mutant's tests the time they took unmutated and an allowance, `additional-timeout`, and reports a mutant whose tests outlast it as `Timeout`, which it counts as detected.  At its default of five seconds, every surviving mutant of a run with slow tests in scope was reported as a timeout, and tests that timed out while Stryker captured what each test reaches were then run against every mutant.  Both `stryker-config.json` files set the allowance to one minute: no test then times out in the capture, and a folder with its own tests gives what it gives at ten minutes, but for one mutant of 2,260, in a sixth of the time, since each mutant that truly hangs holds a worker for the whole allowance.  With the generator's driver tests in scope, two of five known survivors were still reported as timeouts at one minute and none at ten, so a run with slow tests in scope raises the allowance in the file for that run, and a `Timeout` in a report is read before it is believed.

### Where the configuration is

Stryker refuses the repository root: it finds `SqlSource.slnx` there, takes the run as a run of the solution and then rejects a named test project.  So each source project has its `stryker-config.json` beside it, where Stryker looks, with the runner, its one test project and the reports to write, and a bare `dotnet stryker` in that folder works, though it tests the static mutants and writes to `StrykerOutput/` there.  Stryker finds the project it mutates among the test project's own references, which is why `tests/SqlSource.Tool.Tests` references `src/SqlSource.Tool` itself.
