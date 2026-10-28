# AGENTS.md - src/SqlSource/SettingsFiles

The reader of a settings file, `sqlsource.json`.  See [settings files](../../../docs/architecture/generator.md#settings-files).

- **The reader never throws, and bounds nesting by `SidecarTokenizer.MaxDepth`.**  A user writes the file, and an exception in the generator costs every type its generated code.
- **Never hand the walk of `SettingsFileReader` a text the grammar pass has not passed.**  The walk trusts the grammar, as `SidecarCursor` does; `SettingsFileReaderRobustnessTests` holds the two together.
- **It may use `Settings/`, the tokenizer of `Snapshot/Reading/` and `Parsing/SqlDialectName.cs` with the `SqlDialectChoice` it gives, and nothing of `Generation/` or `Diagnostics/`.**  Its errors are data with a `TextSpan`, so that the build and the tool can each report them.
- **Whether a value is valid is `SettingsLevelReader`'s, never the reader's own**, and the dialect's is `SqlDialectName`'s.  `SettingsFileSource` only turns what is rejected into errors at their spans.
- **A new key is a constant of `SettingsFileKeys`, a row of its `All` and of `TypeOf`, a property of `schemas/settings-v1.schema.json`, and, for a new kind of error, a case of `SettingsFileDiagnostics`.**  The schema accepts what the reader accepts and no more.

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
