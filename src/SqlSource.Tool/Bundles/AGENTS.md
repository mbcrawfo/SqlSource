# AGENTS.md - src/SqlSource.Tool/Bundles

How a bundle, `replay` and the renaming work: [`bundles.md`](../../../docs/architecture/bundles.md).

## The bundle and `replay`

- **`BundleReplay.cs` is the one replay of a bundle**, whose steps `ReplayCommand` and `Renaming/RenamingProof.cs` share.  A second way to replay one would let the proof pass a bundle that `replay` does not reproduce.
- **A question the capture does not answer is `ReplayOutcome.Missed`, never an exception that reaches `Cli`**: it is a bundle of another version, `SQLSRC290`, and no bug.
- **A bundle reads no log and holds none.**  `replay`'s lines are built from the describer's events by `ReplayLines.cs`, so no describer writes a line for `replay`.
- **Every error of reading or replaying a bundle is built by `BundleErrors.cs`**, at the bundle's path, so the command and the tests share its texts.
- **A line of `explain-settings.txt` is checked against `PlannerSettings` before a describer is made**, in `BundleDescribers.cs`, so a bad line is `SQLSRC288` and never the describer's exception.

## The renaming

- **`Renaming/IEngineRenaming.cs`'s shape is fixed by its contract in [`bundles.md`](../../../docs/architecture/bundles.md#the-engines-contract)**, which PostgreSQL's part and SQL Server's are built against.  It changes only by the agreement of both engines' parts, and its contract changes with it.
- **Everything an engine decides is behind the switch of `Renaming/EngineRenamings.cs`.**  The neutral part asks the engine's part and never names an engine.
- **A renaming refuses what it cannot rename safely, and never guesses or blanks a value**: it returns a `RenamingRefusal` at the place, with the token found.
- **A member with no `FieldRule` is a refusal**, at its place.  A member the pipeline reads gets a rule, and any other `FieldRule.Drop`.
- **Every replacement comes from the run's `NameMapping`**, through `IRenamingScope`; an engine's part writes no name of its own making.
- **The token check trusts nothing the renaming kept** but the mapping's replacements, the spellings it kept from an engine's row and the engine's words, so a fix that makes it pass by trusting the renamer, or a spelling kept through `IRenamingScope.Keep`, defeats it.
- **The run's `RenamingKey` is never written, logged or printed**, and its `ToString` names only its type.

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
