# Query generation, phase 5.1: sidecars in the generator - design

Date: 2026-10-18

Sub-phase 5.1 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  Phase 5 is seven sub-phases, each with a spec, a plan and a pull request of its own; the epic outline's Phase 5 section lists them.  This one gives `SqlSourceOutput` its effect: from it on, a query whose output is `Models` or `CodeGen` needs a current entry in the sidecar beside its file, and a build says so when it has none.  It emits no model; sub-phase 5.3 does.

It builds on nothing in phase 5.  Sub-phase 5.2 builds on it.

## Intent

The generator has never read a sidecar.  Before any model can be emitted, it must find the sidecar of each file, read it once, keep the result cached as it keeps a parsed `.sql` file, and decide for every query that needs types whether the entry it holds can be trusted.  A stale or missing entry must be an error, so that a wrong type never compiles silently, which is the decision the epic rests on.

The owner's measure of phase 5 is that models are reliable and that the generator stays fast enough that nobody notices it in a build or while editing.  For this sub-phase that means: every state of the epic's table under The sidecar gives the diagnostic it names and nothing else, and an edit to one `.sql` file or one sidecar reads and checks that file alone.

## Goals

1. The package's props include `**/*.sql.json` as `AdditionalFiles`, and its targets nest each sidecar under its `.sql` file in Visual Studio and Rider.
2. The generator pairs each claimed `.sql` file with its sidecar by path, reads the sidecar once with the shared `SidecarReader`, and caches the result.
3. `SqlSourceOutput` takes effect.  A query whose output resolves to `Models` or `CodeGen` for some claiming type needs a current entry; one that resolves to `Sql` for every claiming type does not.  `CodeGen` behaves as `Models` until phase 8.
4. Every row of the epic's sidecar state table, and the version rules of the sidecar format design's section 3, is a diagnostic of the generator.
5. `SQLSRC209` and `SQLSRC210` are reported by the generator as well as by the tool.
6. Caching tests pin what each kind of edit redoes.

## Non-goals

- Emitting a model, mapping a type, or reading `matchesTable`.  Sub-phases 5.2 and 5.3.
- Reading `SqlSourceDatabase`.  The generator never reads the database: an entry is current for the generator when its hash matches.  The tool's skip rule, which also compares the database, is unchanged.
- Any change to the tool, to the sidecar format or to the shared reader's rules.
- Generated code that names a sidecar's content.  Nothing of a sidecar reaches generated source in this sub-phase.

## Anti-goals

- **A model or constant from an entry that is not current.**  The epic's decision: stale snapshots never produce models.  Nothing this sub-phase builds may let a later sub-phase read an entry that failed a check.
- **A crash.**  A sidecar a user can commit, hand-edit or corrupt must never throw in the generator, since an exception costs every type its code (`CS8785`).  The reader already never throws; nothing around it may either.
- **A diagnostic per claiming type.**  The sidecar's checks run once per file, over the union of what every claiming type needs, as the epic decides.  Two types that claim one file give one error for a stale entry, not two.
- **Re-reading every sidecar on every edit.**  An edit to one `.sql` file must not read another file's sidecar again, and an edit to C# that changes no attribute must read no sidecar at all.

## What exists today

As of commit `a3eb3b6`, from a survey of the code:

- `SqlPath.IsSqlFile` keys on the `.sql` extension, so a `.sql.json` reaches no step of the generator today.  `SidecarFormat.PathFor(sqlPath)` gives `sqlPath + ".json"`.
- `src/SqlSource/Snapshot/` holds the value-equal model (`Sidecar`, `SidecarEntry`, `SidecarParameter`, `SidecarColumn`, `SidecarType` and its engine records), the reader `Reading/SidecarReader.Read(string)`, which never throws and returns `SidecarReadResult` in one of three states (`Of`, `OfAnotherVersion`, `Malformed`), and `PackageVersion.Prefix`.  The reader keeps two positions: `SidecarEntry.NameSpan`, the key of an entry with its quotes, and `SidecarError.Span`; `LocationInfo.From(path, text, span)` turns either into a position.
- `SqlQueryHash.Compute(dialect, segments, tokens, parameters)` is the one definition of the hash, called only by the tool.  `SqlQuery` carries no hash.
- `QuerySettings.Resolve` resolves `Output` per type and query, with `CodeGen` as the default, in `TypeEmitter`, which never reads it.
- `SqlDescribableDialects` lists the dialects whose database can be asked to describe a query; only the tool reads it.  `SQLSRC209` and `SQLSRC210` are in `ToolDiagnostics` and are reported by `RunPlanner` alone, with messages worded for the tool.
- Phase 4 made every project and test of the repository that runs the generator state `SqlSourceOutput`; the ones that pass `null` on purpose test the default and the precedence of the output, `SettingLevelTests` above all.
- `tests/SqlSource.Tests/Package/BuildFileTests.cs` asserts that the props have exactly one `AdditionalFiles` element, that the one element of `SqlSource.targets` outside a target is the `PropertyGroup` of `SqlSourceManifestDependsOn`, and, in `Targets_MetadataOfEverySqlFile_IsTrimmed`, that the targets hold exactly one `AdditionalFiles` element.  This sub-phase breaks all three assertions and extends them, as `AGENTS.md` requires.
- `SqlDiagnosticsTests` and `ToolDiagnosticsTests` hold every descriptor to the rule of `src/SqlSource/AGENTS.md` that every diagnostic is an error tagged `NotConfigurable`, `ToolDiagnosticsTests.All_Ids_ComeAfterEveryIdOfTheGenerator` requires every id of `SqlDiagnostics` to sort before every id of `ToolDiagnostics`, and `DiagnosticsDocumentTests` expects the manual's sections in the order of `SqlDiagnostics.All` then `ToolDiagnostics.All`.
- A file with parse errors has no queries.
- The highest diagnostic id is `SQLSRC236`.  The epic's later phases take ids from `SQLSRC237`.

## Design

### The build files

`SqlSource.props` gains a second `AdditionalFiles` item, `**/*.sql.json`, under the same `SqlSourceIncludeFiles` condition as `**/*.sql`.

`SqlSource.targets` gains the nesting, as the epic's sidecar section decides: an evaluation-time `ItemGroup`, outside every target, that sets `DependentUpon="%(Filename)"` on every `**/*.sql.json` item, by `Update`, on both the `AdditionalFiles` item and the SDK's `None` item for the file.  It is in the targets file, not the props, so that it also reaches a sidecar that a project lists itself when `SqlSourceIncludeFiles` is off.

**The spike.**  Whether Visual Studio and Rider honour the metadata on these item types is unverified and is the plan's first task: a scratch project with a `.sql` file and its sidecar, opened in both, with the result recorded in the pull request.  If an IDE nests on only one of the two item types, the update is kept on that one.  If neither nests, the item group is dropped, the manual says so, and `docs/tech-debt` records it; the rest of the sub-phase does not depend on the result.

`BuildFileTests` changes in the same commit: the props hold the two `AdditionalFiles` items with their shared condition, and the targets hold the `PropertyGroup` and the nesting `ItemGroup` outside any target, with nothing else.  `SqlSourceWriteManifest` still lists `.sql` files alone, so the project manifest is unchanged.

### Finding and reading a sidecar

A sidecar is an additional file whose path ends in `.sql.json`, ignoring case, as `SqlPath.IsSqlFile` ignores case for `.sql`.  Its `.sql` file is its normalised path with `.json` removed.  A new step reads each sidecar whose `.sql` file is claimed, and only those: an unclaimed file's sidecar is never read, as an unclaimed `.sql` file is never read today.  A sidecar with no `.sql` file beside it is ignored, since the tool deletes such a file and nothing claims it.

Reading gives a record per sidecar that holds, with value equality:

- the path of its `.sql` file;
- the read result: the `Sidecar`, or the format version of another version, or the error;
- each entry's name with the `LocationInfo` of its key, and the error's `LocationInfo`, made with `LocationInfo.From` while the text is in hand, so that no later step needs the text.

A file whose text cannot be had, `AdditionalText.GetText` returning null, is read as malformed at the start of the file.  The reader takes a string without a byte order mark, so one is removed before reading.

Two sidecar paths that differ only by case are treated as `.sql` paths are: the later one is ignored, since `SQLSRC013` already reports its `.sql` file when that is claimed.

### What a file needs

For each claimed file, the generator finds which of its queries need an entry: a query needs one when its output, resolved by `QuerySettings.Resolve`, is `Models` or `CodeGen` for at least one type that claims the file.  This is the union the epic names.  It is resolved per type, as `TypeEmitter` resolves it today, so it can only be known after the per-type steps; the per-file checks therefore run on what every type says it needs, collected and grouped by file.

A file whose types all resolve every query to `Sql` needs nothing; if it has a sidecar, the sidecar is read, and each of its entries is an orphan (below).  Every existing sidecar of a claimed file is read, once.  To keep the common case of a project at `Sql` cheap, the step that reads a sidecar runs only for a sidecar that exists, and the per-file check is a no-op for a file with neither a sidecar nor a query that needs an entry.

A file with parse errors has no queries, so its sidecar would read as all orphans on every keystroke that leaves a typo.  For such a file only the file-level checks run, malformed and the two format versions; no query-level check and no orphan check runs until the file parses.

### The checks

Run once per file with a sidecar or a query that needs an entry.  In this order, the first that applies deciding for the file or the query:

| State | Diagnostic | Where | Effect |
|----|----|----|----|
| The file's dialect has a describer and this version of the generator has no type map for it: `mssql` until phase 6 | `SQLSRC243`, error, naming the engine | Once for the file, at its first query that needs an entry, as `SQLSRC209` | No query of the file gets another error of this table: no `sqlsource describe` can give it an entry, since the tool says `SQLSRC216` for it |
| The sidecar is malformed | `SQLSRC237`, error, with the reader's error | At the reader's error position in the sidecar | No entry of the file is usable.  Each query that needs one gets no other error. |
| The sidecar's `formatVersion` is higher than the generator's | `SQLSRC238`, error: update the `SqlSource` package | At the start of the sidecar | As above |
| It is lower | `SQLSRC239`, error: run `sqlsource describe` | At the start of the sidecar | As above |
| The `toolVersion`'s prefix differs from `PackageVersion.Prefix`, the format matching | `SQLSRC240`, warning | At the start of the sidecar | The file is read and its entries are used |
| A query needs an entry, and the sidecar does not exist or has none of its name | `SQLSRC241`, error: run `sqlsource describe` | At the query's name | No entry for the query |
| The entry's `engine` is not the canonical name of the file's dialect | `SQLSRC242`, error, naming both | At the query's name | No entry for the query |
| The entry's `hash` is not the query's | `SQLSRC244`, error: the snapshot is stale, run `sqlsource describe` | At the query's name | No entry for the query |
| An entry names no query of the file that needs one | `SQLSRC245`, warning: an orphan entry | At the entry's key in the sidecar | None |
| None of the above | | | The entry is usable |

The orphan check covers an entry of a query that is gone and one of a query that every claiming type now resolves to `Sql`, as the epic's table says.  It does not run for a malformed sidecar or one of another format version.

`SQLSRC242` is at the query, where the sidecar format's reader rule 4 says an unknown engine is an error at the sidecar: the generator checks per query.  The epic outline's sidecar section says so; the format design is not edited, since a spec is not rewritten, and the epic records where the two differ.

**The hash.**  Computed with `SqlQueryHash.Compute` from the query's comment-stripped segments, tokens and declared parameters, under the file's dialect, as the tool computes it, and only for a query that needs an entry, so that a project at `Sql` never pays for it.  It is per file and per query, never per type: it depends on nothing a type sets.  `TD-0032`, the hash of a file that is not UTF-8, names this as its trigger; the sub-phase re-reads it and updates it to say what the generator now does with such a file.

**The version comparison** uses `VersionPrefix` alone, a suffix stripped, as the epic decides, so a sidecar written by `0.1.0-dev` matches a generator built as `0.1.0-pr-42`.

**What the checks give.**  Besides the diagnostics, the checks give, for each claimed file, the usable entries keyed by query name.  Sub-phase 5.3 reads them; until then, nothing does, but a test reads them, so that the anti-goal above is proven before anything depends on it.

### The output and the dialect

`SQLSRC209`, the output that needs a dialect with a describer, becomes the generator's too: for a file that has a query that needs an entry under a dialect that `SqlDescribableDialects` does not list, one error for the file, at the file's first query that needs an entry, which is where the tool reports it.  The epic left open whether the generator gains the position of the marker that set the output; it does not, so that the generator and the tool report one place, which `tests/SqlSource.Tool.Tests/Planning/GeneratorParityTests.cs` can compare.  The file's queries get no `SQLSRC241`: the fix is one setting.  The constants and methods of the file are generated as before.

`SQLSRC210`, the token without a default, becomes the generator's too, at the query, for a query that needs an entry; that query is then not checked against its sidecar.

Both descriptors stay in `ToolDiagnostics`, which the generator assembly holds and so the generator can report, and their messages are reworded to read right from either: the condition and the fix, not "is described".  `ToolDiagnostics`' remark that the generator reports none of its ids is corrected.  `docs/manual/diagnostics.md` follows.  A query that `SQLSRC209` or `SQLSRC210` stops is still given its constant or method.

### Where the diagnostics are reported

The checks' diagnostics are reported from the per-file step's output, once per file, not with a type's output, so that a file claimed by two types gives each error once.  The located diagnostics are in the `.sql` file or in the sidecar, both additional files, whose positions the compiler accepts.

The descriptors of this sub-phase are in `SqlDiagnostics`, the generator's, since the generator reports them, and their ids sort after `ToolDiagnostics`' ones.  So `All_Ids_ComeAfterEveryIdOfTheGenerator` gives way to a test that every id of the two classes is unique, and `DiagnosticsDocumentTests` expects the manual's sections in the order of the ids of both classes together, which is the order the manual's index already keeps.  `AnalyzerReleases.Unshipped.md` gains each.

**The two warnings.**  `SQLSRC240` and `SQLSRC245` are warnings, as the epic decides, where the rule of `src/SqlSource/AGENTS.md` is that every diagnostic is an error tagged `NotConfigurable`.  The owner settled it: the rule is not a dictate, and a diagnostic is not configurable when turning it off would give a poor experience, an error a build must not pass, and is an ordinary warning when that makes sense.  These two make sense as warnings: a sidecar of another tool version is still readable, and an orphan entry generates nothing wrong; an error for either would break every build after a version bump, or after a query is deleted, until the tool runs.  So both are warnings without `NotConfigurable`, which a project can list in `WarningsNotAsErrors` or `NoWarn`, and `TreatWarningsAsErrors` makes errors.  The end-to-end and example projects list `SQLSRC240` in `WarningsNotAsErrors`, as the epic decides, so that the pull request that raises `VersionPrefix` warns until it runs `tools/describe-end-to-end.sh`.

In this sub-phase's pull request, the rule of `src/SqlSource/AGENTS.md` becomes: every diagnostic is an error tagged `NotConfigurable` unless its descriptor says why it is a warning or information a project may configure; `SqlDiagnosticsTests` and `ToolDiagnosticsTests` hold every descriptor to that, with the configurable ones listed by id; and the manual's first line, that nothing can be turned off or made a warning, says which can.

### The pipeline

The builder designs the steps, within these constraints, and documents them in `docs/architecture/generator.md`'s pipeline section and diagram, replacing its "Reading sidecars" placeholder:

- Every value between steps has value equality, as today.  No `AdditionalText`, `SourceText` or Roslyn object flows past the step that reads a sidecar.
- The sidecars are an input of their own, filtered by extension, so that a change to a sidecar re-runs nothing that a `.sql` file feeds.
- A `.sql` edit re-checks that file alone.  A sidecar edit re-checks its file alone.  An edit to an attribute that changes no output re-checks nothing.  A project property that changes no output, a model suffix for instance, re-checks nothing.
- The output of each type and query reaches the per-file check through a collected, sorted value, as `ClaimedPaths` does, and back to one node per file, so that one type's change does not re-check every file whose needs are the same.
- Each new step has a name in `TrackingNames`.

## Testing

| Where | Cases |
|----|----|
| Generator tests, a new `SidecarTests` in `tests/SqlSource.Tests/Generator/` | Each row of the table of checks: its id, its severity, its position, and that a query given an error has no usable entry.  A file with parse errors: no orphan and no query-level error.  An `mssql` file at `Models`: one `SQLSRC243` and no `SQLSRC241`.  A sidecar on a file no type claims is not read.  A sidecar without its `.sql` file is ignored.  A file at `Sql` with no sidecar gets nothing.  A query at `Models` and another at `Sql` in one file: only the first needs an entry, and an entry for the second is an orphan.  Two types claiming one file, one at `Sql` and one at `Models`: the second's needs decide, the error is reported once, and an entry is not an orphan.  The version prefix with and without a suffix.  A byte order mark.  A sidecar that differs only by case from another.  `SQLSRC209` once per file at its first query that needs an entry, and none of `SQLSRC241` for the file.  `SQLSRC210` at the query |
| `SidecarTests`, the anti-goal | The usable entries of a run, read from the step's output, hold no entry that failed a check |
| `CachingTests` | A `.sql` edit re-reads no sidecar and re-checks only its file.  A sidecar edit re-checks only its file and re-parses no `.sql` file.  A C# edit that leaves every attribute alone reads no sidecar and checks nothing.  A model suffix property re-checks nothing |
| The harness | `tests/SqlSource.Testing/Generator/` gains a way to hand the generator a sidecar beside a `.sql` file, from the model through `SidecarWriter` or as raw text for the malformed cases, and the hash of a query computed as the generator computes it, so that a test writes a current entry without restating the algorithm |
| `SettingLevelTests` | The tests that pass `null` for the output now see its effect: they assert the diagnostics the default gives, not that nothing changes |
| `GeneratorParityTests` | The generator and the tool report `SQLSRC209` and `SQLSRC210` at the same places for the same inputs |
| `BuildFileTests` | The two `AdditionalFiles` items of the props, and the nesting `ItemGroup` of the targets beside the `PropertyGroup`, as above |
| `SqlDiagnosticsTests`, `ToolDiagnosticsTests`, `DiagnosticsDocumentTests` | The ids of both classes unique, and the manual in their order together |
| `tests/SqlSource.Tests/EndToEnd/` and `tools/package-install` | Unchanged: both are at `Sql`.  The package-install check proves that a project at `Sql` with no sidecar still builds from the packed package |

The throwaway check that phase 4 ran is the proof of the migration: every test that fails under this sub-phase and is not one of those that pass `null` on purpose is a bug of phase 4.

## Documentation

By the rules of `AGENTS.md` and `docs/manual/AGENTS.md`, for what this sub-phase delivers and nothing more:

- `docs/manual/diagnostics.md`: a section for each of `SQLSRC237` to `SQLSRC245`, in a new area for the generator's sidecar checks after the PostgreSQL table, since the index is in id order, and `SQLSRC209` and `SQLSRC210` reworded where they stand, saying that a build reports them too.
- `docs/manual/settings.md`: the Output row says the setting now takes effect for whether a query needs a sidecar entry, and that models arrive in a later release.  The "What works today" row for `Output` likewise.
- `docs/manual/tool.md` and `concepts.md`: the sentences that say nothing in a build reads a sidecar, and the instruction to commit the sidecars with the project, now true.
- The README: step 5's "Nothing in a build reads the sidecars yet" becomes what a build now reports.  The other phase 5 rows of the README wait for 5.3 and 5.7.
- The VS Code nesting line, `explorer.fileNesting.patterns`, goes on the manual page that the phase map names for sidecars, and the IDE result of the spike beside it.
- `docs/architecture/generator.md`: the pipeline, the Snapshot section's "No step of the generator reads a sidecar yet", and the "Reading sidecars" placeholder replaced.  `docs/architecture/msbuild-and-manifest.md` for the new item and the nesting group.
- `docs/tech-debt`: `TD-0032` updated.  `TD-0025`, `TD-0026`, `TD-0031` and `TD-0033` each say that from phase 5 their case is a missing or stale entry at build time; each is re-read and updated where it says "from phase 5" or "will".
- The epic outline: the 5.1 row's status, and its sidecar section on where an engine with no type map is reported.

## Order of work

One pull request, one commit for each step, each leaving `./pre-commit-validation.sh` passing:

1. The spike, recorded in the pull request.  The epic's 5.1 row says In progress.
2. The build files and `BuildFileTests`.
3. The harness's sidecars.
4. Reading sidecars, with caching tests.
5. The output taking effect and the checks, with their diagnostics and tests.
6. `SQLSRC209` and `SQLSRC210` in the generator, with the parity tests.
7. The documents, and the epic's row set to Done.

## Left to the builder

- The names and the shape of the steps and records, within the pipeline's constraints above.
- The messages of the new diagnostics, by the house style of `SqlDiagnostics`: the problem, then the fix.
- Whether `SQLSRC237` names the reader's error kind in its message or only its position.

## Calls made in this spec

Not decided with the owner, and open to the review of this spec:

- The generator reports `SQLSRC209` at the file's first query that needs an entry, as the tool does, rather than gaining the position of the output marker.
- `SQLSRC242`, an engine that is not the file's, is reported at the query, not at the sidecar.
- `SQLSRC243` is reported once for a file from its dialect, before any entry is looked for, since the tool cannot give such a file an entry until phase 6.
- A file with parse errors gets the file-level checks alone.
- `SQLSRC209` and `SQLSRC210` stay in `ToolDiagnostics`, and the diagnostics' tests order both classes by id together.
- A query stopped by `SQLSRC209` or `SQLSRC210` is not checked against its sidecar.
- The order of the checks, and that a file-level failure suppresses every query-level error of the file.

Settled with the owner after the review: `SQLSRC240` and `SQLSRC245` are configurable warnings, and the rule of `src/SqlSource/AGENTS.md` changes to allow them.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.  When this spec was written the repository had no `v*` tag, and nothing is published until phase 5 closes.
