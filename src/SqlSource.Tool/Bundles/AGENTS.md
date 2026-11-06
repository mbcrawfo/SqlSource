# AGENTS.md - src/SqlSource.Tool/Bundles

How a bundle, `replay` and the renaming work: [`bundles.md`](../../../docs/architecture/bundles.md).

## The bundle and `replay`

- **`BundleReplay.cs` is the one replay of a bundle**, whose steps `ReplayCommand` and `Renaming/RenamingProof.cs` share.  A second way to replay one would let the proof pass a bundle that `replay` does not reproduce.
- **A question the capture does not answer is `ReplayOutcome.Missed`, never an exception that reaches `Cli`**: it is a bundle of another version, `SQLSRC290`, and no bug.
- **A bundle reads no log and holds none.**  `replay`'s lines are built from the describer's events by `ReplayLines.cs`, so no describer writes a line for `replay`.
- **Every error of reading or replaying a bundle is built by `BundleErrors.cs`**, at the bundle's path, so the command and the tests share its texts.
- **Every text `replay` prints from a bundle goes through `BundleText.cs`**, an error's through `BundleErrors.cs`, unless a JSON writer wrote it: a bundle may come from anyone, and a control character in it would reach the terminal ([why](../../../docs/architecture/bundles.md#what-it-prints)).
- **A line of `explain-settings.txt` is checked against `PlannerSettings` before a describer is made**, in `BundleDescribers.cs`, so a bad line is `SQLSRC288` and never the describer's exception.

## The raw bundle

- **`BundleQueryFile.cs` writes only the markers that `Renaming/SqlRenamer.QueryFile.cs` renames, and carries every setting that changes what `BundleReplay.Plan` gives**: a marker the renamer does not hold refuses every bundle, and a setting dropped makes `query.sql` plan another query than the one described ([why](../../../docs/architecture/bundles.md#the-raw-bundle-of-a-described-query)).

## `diagnose`

- **`DiagnoseRun.cs` writes no sidecar and deletes none, and describes its query whatever its hash**: it plans and describes through `DescribeCommand`, `RunDecisions`, `DescribeRun.ConnectionsOf` and `Describing/DatabaseRuns.cs`, and never reaches `SidecarStore`.  A copy of a step would let its connection or secret handling drift from `describe`'s.
- **Its capture is given to `DatabaseRuns` as a `RunCapture`, of the one queried database**, which writes it through `CaptureJson.Write` with the secret; a second session for it is a fault of the tool.
- **A bundle it writes is renamed by `Renaming/BundleRenamer.cs` alone, raw only under `--no-obfuscate`, and a refusal writes nothing**: its errors are built by `BundleErrors.CannotBeMadeSafe`, each with the private route, and an `--out` that cannot be written is `SQLSRC218`.

## The renaming

- **`Renaming/IEngineRenaming.cs`'s shape is fixed by its contract in [`bundles.md`](../../../docs/architecture/bundles.md#the-engines-contract)**, which PostgreSQL's part and SQL Server's are built against.  It changes only by the agreement of both engines' parts, and its contract changes with it.
- **Everything an engine decides is behind the switch of `Renaming/EngineRenamings.cs`.**  The neutral part asks the engine's part and never names an engine.
- **A renaming refuses what it cannot rename safely, and never guesses or blanks a value**: it returns a `RenamingRefusal` at the place, with the token found.
- **A member with no `FieldRule` is a refusal**, at its place.  A member the pipeline reads gets a rule, and any other `FieldRule.Drop`.
- **Every replacement comes from the run's `NameMapping`**, through `IRenamingScope`; an engine's part writes no name of its own making.
- **The token check trusts nothing the renaming kept** but the mapping's replacements, the spellings it kept from an engine's row and the engine's words, so a fix that makes it pass by trusting the renamer, or a spelling kept through `IRenamingScope.Keep`, defeats it.
- **The run's `RenamingKey` is never written, logged or printed**, and its `ToString` names only its type.

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
