# AGENTS.md - src/SqlSource/Settings

Plain data and the readers of values.  See [settings](../../../docs/architecture/generator.md#settings).

- **No file access, no symbols, no pipeline types, and nothing from `Parsing/`, `Generation/` or `Diagnostics/`.**  A rule shared with another folder goes in the root namespace, as `SqlIdentifier`, `DatabaseName` and `SystemSchemas` do.
- **`QuerySettings.Resolve` is the one rule that puts the levels together**, with `ResolveTypeOverrides`, `ResolveColumnTypes`, `ResolveLibraries` and `ResolveDatabase` for the four settings the attribute has no level for.
- **A setting comes whole from the most specific level that has it, except the type override, the column types and the library types, which merge per key.**
- **A new setting** is a nullable member of `SettingsLevel`, a line in `Over`, a member of `QuerySettings` with its default, and a `case` in `SqlMarkerScope.Read` when a marker gives it.  When another source gives it, it is also a member of `SettingId`, a line in `SettingsLevelReader.Read`, the one reader of the attribute and of the settings files, and a property in the `AttributeSettings` of `TargetTypeReader`.
- **An `ISettingSource` only finds a raw value**, as text, as the elements of an array (the generator parameters, which the reader asks for before their text) or as the members of an object (the type override, the column types and the library types, which the reader reads from members alone); records one that is not valid, and an element or member that is not valid or repeats a key, by its index; and says its `SettingPlace`.  See [reading a level](../../../docs/architecture/generator.md#reading-a-level).
- **A reader that finds nothing returns `SettingsLevel.None`**, never a new empty level.
- **A value from a fixed list is read by `SettingValue.TryReadChoice`**, from the names of an enum's members, so renaming a member changes what a user may write, and with it the list and the pattern of that choice in `schemas/settings-v1.schema.json`, which `SettingsFileSchemaTests` checks against the enum.
- **`OutputKind`, `MemberPlacement`, `ModelKind`, `CollectionKind` and `MethodPlacement` keep the numbers of the attribute's enums.**  `tests/SqlSource.Tests/Generation/AttributeSourceTests.cs` compares each pair.

## Documenting a setting

- **A setting a user can write is documented in `docs/manual/settings.md` alone**: a row of its table with every place it can be written and its status, and a section anchored `setting-<name>`.  A new marker also gets a row in the marker index of `docs/manual/sql-files.md`.
- **A new generator parameter gets its name in each of these places.**  In `settings.md`, the Values cell of the Generator parameters row and the table under `#setting-generator-parameters`.  The section of `SQLSRC109` in `docs/manual/diagnostics.md`, and the list of parameters in `tests/SqlSource.Tests/Diagnostics/DiagnosticsDocumentTests.cs`, which checks the section against that list.  The list and the patterns of `schemas/settings-v1.schema.json`, which `SettingsFileSchemaTests` checks against `GeneratorParameterList.Names`.
- **A setting accepted before the change that gives it an effect says so in its status**, and that change flips it.

## The type override

See [the type override](../../../docs/architecture/generator.md#the-type-override).

- **`TypeOverrideMap.Over` merges it**, in `SettingsLevel.Over` and in `QuerySettings.ResolveTypeOverrides`, and keeps an entry of `default` so that it hides the key below.
- **`TypeOverrideMap.Find` weighs an entry of `default` by its place** against the other entries that match a type, and gives null when it wins, so never drop one before `Find`.
- **The type override is not part of `QuerySettings.Resolve`**, which runs for each query and each claiming type: compute the type override once for each distinct level of markers.
- **Every array of entries holds each key once**, in the order of `TypeOverrideMap`: by the key's text ignoring case, then ordinally.  A key's text is its parts' text without quotes and with escapes decoded, compared ordinally.
- **Keep a key a string in its written form, quotes included, and compare keys only through `TypeOverrideMap`.**
- **`TypeOverrideGrammar` is its one reader**, of an object's members and of a marker alike; the attribute has no level for it.
- **A level holds two arrays, one for each `DatabaseEngine`**: `TypeOverrides`, keyed by `TypeKeys.TryNormalizePostgres`, and `SqlServerTypeOverrides`, keyed by `TypeKeys.TryNormalizeSqlServer`.  An entry is absent from the array of an engine that does not read its key.
- **An object of a settings file fills both arrays; a marker fills only its file's engine's**, by `SqlDescribableDialects.EngineOf`, and a key that engine does not read is `SQLSRC111`.
- **Code that carries a level's entries carries both arrays**, as `SettingsLevel.Over`, `MappedTypeNames` and `FileTypesInput.TypeSettingsOf` do.

## The library types

- **The library types are `Settings/Libraries/`: one `LibrarySetting` for each `SqlLibrary`, merged by `LibrarySettings.Over`, which keeps an entry of `LibraryState.Default` so that it hides the farther levels.**  Never drop one before `LibrarySettings.Find`.
- **The library types are not part of `QuerySettings.Resolve`**: `ResolveLibraries` merges them, and its caller does so once for each distinct level of markers, as for the type override.
- **A new library is a member of `SqlLibrary`, whose lower-cased name is what a user writes**, and with it the names in `schemas/settings-v1.schema.json`, which `SettingsFileSchemaTests` checks against the enum.
- **A level holds no position in a settings file.**  The reader tells the source which member gives each entry through `ISettingSource.LibraryEntry`, and a settings file keeps it apart, so an edit that only moves a member changes no level and types nothing again.  Only a marker's entry carries its `Marker` span, since an edit of the marker parses its file again.  See [the library types](../../../docs/architecture/generator.md#the-library-types).

## The column types

See [the column types](../../../docs/architecture/generator.md#the-column-types).

- **The column types follow the type override's rules, in `Settings/ColumnTypes/`, with one array for both engines.**
- **`ColumnTypeMap` merges and weighs them as `TypeOverrideMap` does the type override's**, so never drop a `default` before `ColumnTypeMap.Find`, and compare keys only through it.
- **The two maps share their sorted list and the rule of its order, `Settings/SortedKeyedList.cs`, and their ranking of a place, `SettingPlaces.Specificity`**, so a change to either changes both settings.
- **`ColumnTypeGrammar` is their one reader**, and reads every type with `TypeOverrideGrammar`'s `TryReadType`, so a change to that changes both settings.

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
