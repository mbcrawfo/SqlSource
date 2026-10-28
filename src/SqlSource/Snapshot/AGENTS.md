# AGENTS.md - src/SqlSource/Snapshot

See [snapshot](../../../docs/architecture/generator.md#snapshot).

- **The format is a contract** with every committed sidecar, and `schemas/sidecar-v1.schema.json` defines it.  A change to what `SidecarWriter` gives changes the schema and the examples in `tests/SqlSource.Tests/Snapshot/Examples/` in one pull request.  What needs a new `formatVersion` is not yet written down anywhere current (TD-0084).
- **The reader never throws.**  Never hand a `SidecarCursor` a text the grammar pass has not passed, and keep a file the tool wrote tokenized twice and no more.  `Read_MistakeInAnArrayBeforeTheEngine_IsFound` in `tests/SqlSource.Tests/Snapshot/Reading/SidecarReaderTests.cs` and `Read_ReorderedFileCutShortOrDamagedAnywhere_ReturnsOneOfItsThreeStates` in `SidecarReaderRobustnessTests.cs` beside it hold the path no file of the tool takes.  See [reading a sidecar](../../../docs/architecture/generator.md#reading-a-sidecar).
- **The sidecar is strict JSON**: no reader of a sidecar constructs a `SidecarTokenizer` with `lenient: true`, which `SettingsFiles/` alone uses.
- **The reader is lenient and the schema is strict.**  A new key is added to the model, to its object's list of keys and to the `switch` beside it; a key of a parameter's type to each engine's list in `SidecarTypeReader`, and a key of an entry to `SidecarReader.EntryMembers`, which the `switch` of `ReadMembers` sets.
- **Every name of a key is in `SidecarKeys`, and every value the tool writes in `SidecarValues`.**  A test compares each list of `SidecarValues` with the schema's.
- **The writer goes by the condition, not by the value**, as the remarks of `SidecarWriter` say.  It checks nothing and never throws.
- **`Snapshot/` uses `EquatableArray<T>` and `TextSpan` and nothing of `Parsing/`, `Generation/`, `Settings/` or `Diagnostics/`.**  A test holds `SidecarValues.Engine` and `Parsing/SqlDialectName.cs` together.
- **`SidecarComparer` compares what `describe --check` compares, and no more**: the keys its summary lists.  A new key is not compared, unless it is a key of a type object.

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
