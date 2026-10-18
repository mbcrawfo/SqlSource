# Query generation phase 4 - defaults

Date: 2026-10-18

Phase 4 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  The phase is not split into sub-phases: this spec lands in a pull request of its own, then one plan and one pull request implement it.

## Intent

Phase 5 gives `SqlSourceOutput` its effect.  From then on the default, `CodeGen`, means a query needs a sidecar entry, and `CodeGen` or `Models` under a dialect with no describer, `ansi` among them, is an error.  Every project in this repository that runs the generator today relies on that default without saying so, because the setting has no effect yet.  Phase 4 makes each of them say `Sql`, so that phase 5's diff is the feature and not the migration, and so that a test that breaks in phase 5 breaks because it asked for models, not because it was silent.

Nothing a user sees changes.  The output still has no effect in the generator, so every generated file, diagnostic and compiled result is what it was.

## Goals

1. Every project in the repository that the generator runs on at build time sets `SqlSourceOutput` to `Sql`.
2. Every test that runs the generator states its output, through a parameter of the harness that the compiler requires.  A test that means the default says so with `null`.
3. The suites pass with no change in behaviour.

## Non-goals

- Any change to the generator, the tool, the package's props and targets, or the user documentation.
- Any change to dialects.  The default dialect `ansi` is valid with `Sql`, so no project or test needs a dialect it does not have.  The outline's row for this phase said "and dialects"; the epic's own scope for the phase says a dialect protects nothing, and this spec follows the scope.
- Rewriting the tests that check the default and the precedence of the output, `SettingLevelTests` above all.  They pass `null` and keep checking that the output changes nothing.  Phase 5 changes what they assert, since that is the phase that gives the setting an effect.
- New tests of models or sidecars.  They arrive in phases 5 and 6 with their committed sidecars.

## Anti-goals

- **A hidden default.**  The harness must not supply `Sql` on a test's behalf.  The owner chose this explicitly: a default in the harness hides the real default from every test, and phase 5's tests would have to remember to override it.
- **Spelling the output in the test's C# or SQL text.**  `Output = GeneratorOutput.Sql` on the attribute or `-- output: sql` in the file changes text that tests assert on: generated source, positions and hashes.  The output is a property of the project, as `SqlSourceOutput` is.
- **Per-file metadata.**  `tests/SqlSource.Tests/EndToEnd/EndToEndTests.cs` asserts the exact list of per-file metadata in the generated editorconfig.  A project property leaves that list as it is.

## What exists today

Facts the design rests on, as of commit `2da9a1e`:

- `src/SqlSource/Settings/QuerySettings.cs` resolves the output, marker over attribute over metadata over property, with `OutputKind.CodeGen` as the default.  The generator resolves it in `Generation/Emitting/TypeEmitter.cs` and never reads it.
- The tool already gives the output its effect: `src/SqlSource.Tool/Planning/RunPlanner.cs` skips `Sql` queries, and reports `SQLSRC209` for a query under a dialect with no describer and `SQLSRC210` for a token with no default.  The tool and its tests need nothing from this phase.
- Two projects run the generator at build time:
  - `tests/SqlSource.Tests` imports the props and targets by path and references the generator as an analyzer.  Its ten `.sql` files under `EndToEnd/` include queries with tokens that have no default.  It sets `SqlSourceDialect` to `postgres` and `SqlSourceGeneratorParameters` as project properties, and per-file metadata on three files.
  - `tools/package-install/Consumer.csproj`, built from the packed package by `tools/check-package-install.sh`, which `pre-commit-validation.sh` and CI run.  It sets the dialect to `postgres`, and its `Directory.Build.targets` adds `Queries/AddedByATarget.sql` in a `BeforeBuild` target.
- `tests/SqlSource.Tests.RoslynFloor` has no `.sql` files.  It compiles `tests/SqlSource.Tests/Generator/**/*.cs` a second time on Roslyn 4.8.0, so it is covered by the change to those tests and to the harness.
- No other `.sql` file reaches the generator.  The fixture projects of `tests/SqlSource.Tool.Tests` are only evaluated by MSBuild, and the `.sql` files of `tests/SqlSource.Tests.Postgres` are read from disk by the describer.
- Every generator test reaches the generator through `tests/SqlSource.Testing/Generator/`:
  - `GeneratorHarness.Run` (two overloads), `BuildAsync`, `CreateReference` and `CreateDriver`.  `BuildAsync` and `CreateReference` call `CreateDriver` with no options, and `CreateDriver` falls back to `new TestOptionsProvider(null)`.
  - `TestOptionsProvider`, which `CachingTests` also builds directly, eleven times, for `WithUpdatedAnalyzerConfigOptions`.
  - The call sites are in the nine test files of `tests/SqlSource.Tests/Generator/`, about 170 calls, and in `tests/SqlSource.Tool.Tests/Planning/GeneratorParityTests.cs`.
- `TestOptionsProvider` is also built by reader unit tests that never run the generator, `tests/SqlSource.Tests/Generation/MSBuild/MSBuildSettingsTests.cs` (eleven) and `DialectSettingTests.cs` (five).  Several assert an exactly empty settings level.

## Design

### The two projects

`tests/SqlSource.Tests/SqlSource.Tests.csproj` and `tools/package-install/Consumer.csproj` each set the project property `SqlSourceOutput` to `Sql`, beside the `SqlSourceDialect` they already set, with a short comment that every query of the project is generated as SQL alone and that a query with models would need a sidecar.  The comment names no phase.

A project property covers every `.sql` file of the project, including `AddedByATarget.sql`, which a target adds after evaluation, and adds nothing to the per-file metadata that `EndToEndTests` checks.  `tools/package-install/expected-output.txt` does not change, since the output has no effect on what the consumer prints.  The manifest already has a `Property.SqlSourceOutput=` line, empty today; it now holds `Sql`, and no check of `check-package-install.sh` reads it.

### The harness

The output becomes a required parameter, of type `OutputKind?`, of every entry point of the harness that runs the generator or builds what it reads:

- `TestOptionsProvider`'s constructor takes it as its first parameter.  A value sets the project property `SqlSourceOutput`; `null` leaves it unset, as today.  An entry for `SqlSourceOutput` in `properties` is copied over it, as `properties` already overrides the dialect, so a test of a malformed or oddly spelled value passes `null` and its own string in `properties`.
- `GeneratorHarness.Run`, both overloads, `BuildAsync` and `CreateReference` take it and pass it to the `TestOptionsProvider` they build.
- `CreateDriver` loses its fallback: its `TestOptionsProvider` is required, so no run can reach the generator without having stated its output.

Required means no default value: a call that leaves the output out does not compile.  In the overload of `Run` that has optional parameters it goes before them.

`TestOptionsProvider` writes the value's spelling itself, `Sql`, `Models` or `CodeGen`, from a switch over the members, rather than from `ToString()`.  The provider spells out every key so that a change to the generator's spelling fails a test, and the value follows the same rule.

### The call sites

- Every test in `tests/SqlSource.Tests/Generator/` passes `OutputKind.Sql`, except the ones that check the default or the precedence of the output, which pass `null`:
  - every test of `SettingLevelTests`, whose plain run and set run must differ by the setting alone;
  - any other test the builder finds whose subject is the output itself.
- `CachingTests` passes the output to every `TestOptionsProvider` it builds, the first run's and each update's alike, so that an update of the options does not drop it.  Its helper `FirstRun`, and any other helper of a test class that runs the generator, takes the output as a required parameter from the test rather than supplying it, so that each test says what it runs with.
- `GeneratorParityTests` passes `null`.  It compares the generator with the tool over the same inputs, and the tool's side sets no project property.
- The reader unit tests pass `null`.  They test how levels are read, not what the generator does, and their empty-level assertions stay as they are.

### Guidance

`tests/SqlSource.Tests/Generator/AGENTS.md` gains a rule: every run of the generator states its output through the harness; `OutputKind.Sql` is the norm; `null` is for a test of the default or of the output's precedence, and says so in the test.

`src/SqlSource/AGENTS.md`'s paragraph on the generator tests names the harness files and needs no change.  No user document changes.

### The epic outline

In the spec's pull request, the phase-4 row of the outline links this spec with the status `Specified`, and its "Delivers" text drops "and dialects".  The implementation's pull request sets the status to `Done`.

## Testing

1. `./pre-commit-validation.sh` passes, which runs both test projects, the Roslyn-floor project and the package-install check.
2. The only edits are settings, the harness's parameter and the call sites: no assertion, expected file or generated output changes.  The diff shows it.
3. A throwaway check that the change protects phase 5, run locally and not committed.  Make the generator report an error for any query whose output resolves to `Models` or `CodeGen`, then run `tests/SqlSource.Tests`, `tests/SqlSource.Tests.RoslynFloor`, `tests/SqlSource.Tool.Tests` and `tools/check-package-install.sh`.  The only failures must be the tests that pass `null` on purpose; any other failure is a call site, a project or a file that still relies on the default.  Revert the change to the generator and record in the pull request's description that the check ran and which tests failed under it.

## Left to the builder

- The order of the parameters beyond what is said above, and the wording of the csproj comments and of the AGENTS.md rule.
- The pull request rules of the root `AGENTS.md` apply as usual, among them the version check before opening it.
