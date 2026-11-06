# TD-0100 — Each sidecar entry records its tool's version — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Each sidecar entry records the version of the tool that wrote it, so that `describe` describes again an entry an older tool wrote and the build warns of it, even when a git merge put it under a newer tool's file.

**Architecture:** `SidecarEntry` gains `ToolVersion`, written as the entry's `toolVersion` between `hash` and `database`, so that a merge of a tool update with a changed query or database conflicts in git instead of merging silently.  The tool's test of a current entry compares the entry's version (`SidecarEntry.IsCurrentFor`) in place of the file's (`Sidecar.IsWrittenBy`, `SidecarOnDisk.Usable`, both removed); the generator's SQLSRC240 check looks at every entry's version too.  The file-level `toolVersion` stays, as the version of the last write.

**Tech Stack:** C# / .NET, Roslyn incremental generator, xUnit + Shouldly, JSON Schema (`schemas/`).

**Spec:** `docs/tech-debt/TD-0100-a-merge-can-keep-an-older-tools-entry.md`

## Decisions

- **Q3 (owner): the format version stays 1, changed in place** — "c, library is not released, breaking change is fine".  `SidecarFormat.Version` stays 1 and `SchemaId` names `schemas/sidecar-v1.schema.json`, which is edited in place with the entry's `toolVersion` required.  The reader refuses an entry without it (`SidecarErrorKind.MissingKey`) or with `null`, so a sidecar an earlier build of the tool wrote is malformed (SQLSRC237) to the build, and `describe` writes it again in full.  `docs/architecture/sidecar-format.md`, `schemas/AGENTS.md` and `docs/publishing.md` state the exception: until the first stable release the sidecar format may change in place, as the settings schema may.
- **Q2 (owner): where SQLSRC240 points** — "agree": one warning per sidecar, its message unchanged.  At the file's start, naming the file's `toolVersion`, when the file's prefix differs; otherwise at the key of the first entry whose own prefix differs (`SidecarFile.EntryLocations[i]`), naming that entry's `toolVersion`.
- **Key order of an entry** becomes `hash`, `toolVersion`, `database`, `engine`, `serverVersion`, … .  Git conflicts on changes to adjacent lines and merges changes a line apart cleanly (checked on git 2.x): a tool update changes every entry's `toolVersion`, and the two things besides it that make an entry not current, `hash` and `database`, are its neighbours.  Key order is not significant to a reader, so moving `database` before `engine` is free.
- **What stays outside git's reach** is an entry re-described with its hash and database unchanged, which only `describe --force` does, after a schema change.  The manual's "Sidecars in git" already tells the reader to run `describe --force` after merging a schema change, so no item remains; the tool-update half of that advice, and the known issue, go.
- **Fixtures keep `fixture`.**  `FixtureReplay` writes the entry's `toolVersion` as `fixture` too (`entry with { ToolVersion = FixtureReplay.ToolVersion }`), so that a new version of the tool touches no fixture.
- **Where the code differs from the spec:** the spec names `Sidecar.IsWrittenBy` and `SidecarOnDisk.Usable` as comments to update; since the entry carries its version, both lose their only use and are removed, and their callers read `SidecarOnDisk.Sidecar` (only a file of this format reads as a `Sidecar`).

## Global Constraints

- Every commit passes `./pre-commit-validation.sh`; `./format.sh` runs first.  Root `AGENTS.md`, "Commits".
- A sidecar is never edited or merged by hand: the committed ones are written by `tools/describe-end-to-end.sh` (end-to-end, realistic, examples), the describer fixtures by `tools/record-fixtures.sh postgres` and `tools/record-fixtures.sh mssql`.
- The schema is strict and says what this tool writes; the reader is lenient.  A new key gets a `description` in the schema (`schemas/AGENTS.md`).
- `SidecarComparer` compares only what `--check` compares; the entry's `toolVersion` is not compared (versions differ between a developer and CI by design).
- User docs (`docs/manual/`, `README.md`) describe effects, never internals, and never name a tech-debt id.
- `src/SqlSource.Tool/AGENTS.md` and `tests/AGENTS.md` are within a few bytes of 8 KB: add nothing there.

## Review Focus

1. A sidecar written by this tool whose one entry carries an older `toolVersion` (the merge): `describe` describes that query alone and rewrites the file; the others are skipped.  Task 2 tests it.
2. A tool update merged with a changed query: the writer puts `toolVersion` on the line after `hash` and before `database`.  Task 1 tests the order.
3. A version with a suffix (`0.1.0-dev`) on an entry: the build compares the prefix (`PackageVersion.PrefixOf`), the tool compares exactly with `PackageVersion.Prefix`.  Tasks 2 and 3 test both.
4. A file whose own version and an entry's both differ: one SQLSRC240, at the file's start.  Task 3 tests it.
5. `describe --check` on a sidecar whose entries carry another tool's version and are otherwise equal: no difference.  Task 1 tests the comparer.

---

### Task 1: The format — model, writer, reader, schema, examples

**Files:**
- Modify: `src/SqlSource/Snapshot/SidecarEntry.cs`, `Sidecar.cs`, `SidecarFormat.cs`, `SidecarWriter.cs`, `Reading/SidecarReader.cs`, `SidecarComparer.cs` (its summary only, if it lists what is not compared)
- Modify: `schemas/sidecar-v1.schema.json` in place (the entry's `toolVersion` required with its `description`, same pattern as the top `toolVersion`)
- Modify: `tests/SqlSource.Tests/Snapshot/Examples/Orders.sql.json`, `Users.sql.json` (the writer's output; `toolVersion` of each entry `0.4.0` as the file's)
- Modify: `tests/SqlSource.Testing/Snapshot/TestSidecars.cs`, `tests/SqlSource.Tests/Snapshot/SidecarFaker.cs`, every other `new SidecarEntry(` in tests
- Test: `tests/SqlSource.Tests/Snapshot/SidecarWriterTests.cs`, `Reading/SidecarReaderTests.cs`, `SidecarSchemaTests.cs`, `SidecarComparerTests.cs`

**Interfaces:**
- Produces: `SidecarEntry(string Name, TextSpan NameSpan, string Hash, string ToolVersion, string Engine, string? Database, …)` — `ToolVersion` right after `Hash`.
- Produces: `bool SidecarEntry.IsCurrentFor(string hash, string database, string toolVersion)` — hash ordinal, database by `DatabaseName.Comparer`, tool version ordinal.
- Produces: `TestSidecars.Entry(…, string? toolVersion = null)` defaulting to `PackageVersion.Prefix`.
- Removes: `Sidecar.IsWrittenBy`.

- [ ] **Step 1: Failing tests**
  - `Write_Entry_PutsToolVersionBetweenHashAndDatabase`: the lines of an entry start `"hash"`, `"toolVersion"`, `"database"`, `"engine"`, `"serverVersion"`.
  - `Read_EntryWithoutToolVersion_IsMissingKey` (`SidecarErrorKind.MissingKey`, argument `toolVersion`) and `Read_EntryToolVersionNull_IsWrongType`.
  - `Read_EntryToolVersion_IsKept`: an entry's `toolVersion` other than the file's is read as written.
  - `IsCurrentFor_OtherToolVersion_IsFalse`, `IsCurrentFor_SameHashDatabaseAndVersion_IsTrue`.
  - `FindDifference_EntriesDifferOnlyInToolVersion_IsNull`.
  - The examples validate against `sidecar-v1.schema.json` as changed.
- [ ] **Step 2:** `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~Snapshot"` — the new tests FAIL.
- [ ] **Step 3: Implement.**  The reader adds `SidecarKeys.ToolVersion` to `EntryKeys`, a `case` of its own in `ReadMembers` (`orNull: false`), and requires it beside `hash`.  The writer writes it after `hash`, then `database`, then `engine`.  Regenerate the two examples by writing their model (`SidecarWriterTests` holds them to the writer).
- [ ] **Step 4:** the filter of Step 2 and `dotnet build` of the solution — PASS (the tool's callers of `IsWrittenBy` are Task 2's: make them compile by passing `PackageVersion.Prefix`, nothing more).
- [ ] **Step 5: Commit** `feat(snapshot): each sidecar entry records its tool's version, (TD-0100)`.

### Task 2: The tool keeps only entries of its own version

**Files:**
- Modify: `src/SqlSource.Tool/Describing/SidecarOnDisk.cs` (remove `Usable`; summary of `Sidecar` says a run keeps an entry only when it is current), `RunDecisions.cs` (`onDisk?.Sidecar?.Find(…)` and `entry.IsCurrentFor(hash, database, PackageVersion.Prefix)`), `EntryBuilder.cs` (sets `ToolVersion = PackageVersion.Prefix`)
- Modify: `tests/SqlSource.Tool.Testing/Fixtures/FixtureReplay.cs` (the entry's version `fixture`), `tests/SqlSource.Tool.Testing/SidecarFiles.cs` (entries of a helper's `toolVersion`)
- Test: `tests/SqlSource.Tool.Tests/Describing/RunDecisionsTests.cs`, `tests/SqlSource.Tool.Tests/Commands/` (the describe command's tests that hold a sidecar of another tool)

**Interfaces:**
- Consumes: `SidecarEntry.IsCurrentFor(string, string, string)`, `SidecarEntry.ToolVersion`.

- [ ] **Step 1: Failing tests**
  - `Decide_EntryOfAnotherToolInAFileOfThisTool_IsToDescribe` (`RunDecisionsTests`).
  - `Decide_EntryOfThisToolInAFileOfAnotherTool_IsSkipped`: the file's `toolVersion` no longer decides.
  - `Decide_EntryVersionWithSuffix_IsToDescribe`: `PackageVersion.Prefix + "-dev"` is another version to the tool.
  - A describe-command test: a sidecar of this tool with two current entries, one of them `toolVersion` `0.0.1`; the run describes that query alone and the file it writes has both entries with this tool's version.
- [ ] **Step 2:** `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~RunDecisions|FullyQualifiedName~Commands"` — new tests FAIL.
- [ ] **Step 3: Implement** as the Files list says.
- [ ] **Step 4:** `dotnet test tests/SqlSource.Tool.Tests` — PASS except the fixture comparisons, which Task 4 records.
- [ ] **Step 5: Commit** `fix(tool): describe describes again an entry another tool wrote, whatever its file says (TD-0100)`.

### Task 3: The build warns of every entry of another tool

**Files:**
- Modify: `src/SqlSource/Generation/Sidecars/SidecarFileChecks.cs`
- Modify: `docs/manual/diagnostics.md` (SQLSRC240: an entry another version wrote, after a merge, is warned at its query's key)
- Test: `tests/SqlSource.Tests/Generation/Sidecars/SidecarFileChecksTests.cs`

**Interfaces:**
- Consumes: `SidecarFile.EntryLocations`, `SidecarEntry.ToolVersion`.

- [ ] **Step 1: Failing tests**
  - `Read_OneEntryOfAnotherTool_WarnsSqlsrc240AtItsKey` with the entry's version as the first argument.
  - `Read_FileAndEntryOfAnotherTool_WarnsOnceAtTheStart`.
  - `Read_EntryVersionWithSuffix_DoesNotWarn` (`PackageVersion.Prefix + "-dev"`).
  - The sidecar is still returned and used in each case.
- [ ] **Step 2:** `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~SidecarFileChecks"` — FAIL.
- [ ] **Step 3: Implement**: after the file's own check, when it did not warn, the first entry whose `PackageVersion.PrefixOf(entry.ToolVersion)` is not `PackageVersion.Prefix` warns at `file.EntryLocations[index]`.
- [ ] **Step 4:** `dotnet test tests/SqlSource.Tests` — PASS.
- [ ] **Step 5: Commit** `fix(generator): SQLSRC240 looks at every entry's tool version (TD-0100)`.

### Task 4: Every sidecar written again

**Files:** every `*.sql.json` under `tests/DatabaseTests/` and `examples/`; `tests/SqlSource.Tool.Tests/Fixtures/Describers/**/expected.json`; `tests/SqlSource.Tool.Tests/Fixtures/Bundles/**/{expected.json,bundle.json}`; `tests/SqlSource.Tool.Tests/Fixtures/Projects/Online/Queries/Users.sql.json`; tests holding sidecar text inline (`git grep -n '"formatVersion"' -- tests`).

- [ ] **Step 1:** `tools/record-fixtures.sh postgres` and `tools/record-fixtures.sh mssql`; read the diff: only an entry `toolVersion` of `fixture` and the order of `database` and `engine` change (a capture that changes is a finding to report, not to commit).
- [ ] **Step 2:** The bundle fixtures: each case's `expected.json` is its describer case's, as `docs/architecture/bundles.md` says.  The `Online` project's sidecar has no entry and does not change.
- [ ] **Step 3:** `tools/describe-end-to-end.sh` (long: run in the background); read the diff: each entry's `toolVersion` and the order of `database` and `engine`, and nothing else.
- [ ] **Step 4:** `./pre-commit-validation.sh --full` — PASS.
- [ ] **Step 5: Commit** `test: every sidecar and fixture written again (TD-0100)`.

### Task 5: The docs, and TD-0100 resolved

**Files:**
- `docs/architecture/sidecar-format.md`: Compatibility — the pre-release exception (the format changes in place until the first stable release; `docs/publishing.md` takes it out); "What is written" — the entry's `toolVersion` and why it stands between `hash` and `database`; reader rule 4 (the entry's `toolVersion` required) and rule 8 (only `formatVersion` and the file's `toolVersion` come from the top, and the tool reads an entry's version from the entry); Versions — the tool's bullets (current entry by its own version; the merge sentence becomes: a merge that keeps an older tool's entry gets it described again, and a tool update merged with a changed query conflicts), the generator's table row for SQLSRC240; what `--check` compares (the entry's `toolVersion` not).
- `docs/architecture/tool.md` (line 143); `docs/publishing.md` (step 3 also takes out the sidecar's pre-release rule).
- `src/SqlSource.Tool/Describing/AGENTS.md` (the rule of line 8: `SidecarEntry.IsCurrentFor` alone, no TD id), `schemas/AGENTS.md` (the sidecar schema changes in place until the first stable release).
- `docs/manual/tool.md`: "Sidecars in git" — after a merge with a tool update, `describe` describes again what the older tool wrote, and the build warns until it does; `--force` stays for a schema change; "Describing again" — the version bullet per entry; the known issue removed.
- `docs/tech-debt/`: delete the item, remove its row, `git grep TD-0100` finds nothing outside `docs/superpowers/`.

- [ ] **Step 1:** Edit as listed.
- [ ] **Step 2:** `./format.sh`, then `./pre-commit-validation.sh` — PASS (the documentation and AGENTS tests of `tests/SqlSource.Repository.Tests` included).
- [ ] **Step 3: Commit** `docs: an entry's tool version, and TD-0100 resolved`.
