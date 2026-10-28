# AGENTS.md - schemas

- **Never rename or move a file here.**  Its path on `main` is its `$id` and the URL that users' files name: every committed sidecar's `$schema` is `SidecarFormat.SchemaId` (`src/SqlSource/Snapshot/SidecarFormat.cs`), and `docs/manual/settings.md` tells users to put the settings schema's URL in their `sqlsource.json`.
- **A change that would make a valid file invalid, or change what a key means, is a new `-vN` file**, never an edit of the old one, since files already written name the old URL.
- **`sidecar-v1.schema.json` is the sidecar's contract, and changes only with the sidecar format**: the reader and writer in `src/SqlSource/Snapshot/` and the examples of `tests/SqlSource.Tests/Snapshot/Examples/`, which `SidecarSchemaTests` validates against it.
- **`settings-v1.schema.json` accepts what `SettingsFileReader` accepts and no more**, which `SettingsFileSchemaTests` checks; a key added to `SettingsFileKeys` is added here in the same change, with the manual's page.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
