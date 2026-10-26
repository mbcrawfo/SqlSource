# Settings files, layer 1: the reader - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Read a `sqlsource.json` file (JSON with comments and trailing commas) into its `root`, its dialect and a `SettingsLevel`, with every problem reported at its position, plus the diagnostics, the schema and their tests.  Nothing calls the reader yet.

**Architecture:** `SidecarTokenizer` gains a lenient mode (comments, trailing commas) that sidecars never use.  `ISettingSource` and `SettingsLevelReader` learn the array form of the generator parameters and the object form of the type override and the column types, so validity stays in `Settings/`.  A new folder `src/SqlSource/SettingsFiles/` holds the file reader: a grammar pass, then a read of the members into a struct `ISettingSource`; the dialect is read beside it with `SqlDialectName`.  Errors are data with a `TextSpan`; `Diagnostics/SettingsFileDiagnostics.cs` turns one into a `DiagnosticInfo` with file, line and column.

**Tech Stack:** C# (`netstandard2.0` analyzer, `LangVersion` preview), Roslyn 4.8.0 text types, xunit v3, Shouldly, JsonSchema.Net in the tests.

**Spec:** `docs/superpowers/specs/2026-10-25-settings-files-design.md`, "The settings file" and "The layers", item 1.  Owner decisions and shared-file rules: `/Users/michael/.claude/plugins/data/implementation-manager-mbcrawfo-skills/mbcrawfo-SqlSource/settings-files/notes.md`.

## Where the code differs from the spec, and the calls this plan makes

- **The reader lives in `src/SqlSource/SettingsFiles/`, beside `Settings/`.**  `Settings/` may use nothing of `Parsing/` (`src/SqlSource/AGENTS.md`), and the dialect is read with `Parsing/SqlDialectName.cs`.  The spec allows "a folder beside it".
- **The tokenizer extends `SidecarTokenizer` with a lenient mode** (a constructor flag, default off), rather than a second tokenizer.  Sidecars never pass it, so they stay strict JSON.
- **The reader takes the `SettingPlace` its type override and column type entries carry** as a parameter.  How a folder file's depth ranks (spec, which place wins) is layer 3's design; `SettingPlace` is unchanged here.
- **Errors are data with a `TextSpan`, not diagnostics.**  `Settings/` and `SettingsFiles/` may not use `Diagnostics/`; layer 3 and 4 turn them into diagnostics with `SettingsFileDiagnostics.Create(error, path, sourceText)`, which gives line and column.
- **An element of `generatorParameters`, or a member of `typeOverrides` or `columnTypes`, of the wrong JSON type is an error for that element**, and the others apply, as an unknown word does.  A top-level value of the wrong type sets nothing.
- **`null` is the wrong JSON type** for every key; an empty or white-space string is how a value is blanked.  An empty or white-space element of `generatorParameters` is skipped.  A member of `typeOverrides` or `columnTypes` whose type is empty is not valid.
- **Keys and types of a member are trimmed**, as a marker's are around its `=`.
- **A key given twice at the top level: the first applies.**  Two members of one key in `typeOverrides` or `columnTypes`: the first applies, and the second is an error.  "One key" is the map's identity (`TypeOverrideMap.IndexOf`, `ColumnTypeMap.IndexOf`); two members with one JSON text are one key by it, or both not valid.
- **`$schema` must be a string** (the schema says so, and the schema accepts no more than the reader).
- **`default` beside another word of `generatorParameters`** makes the list not set, as today; each `default` element and each unknown word is reported on its own.
- **Diagnostic ids `SQLSRC015` to `SQLSRC020`.**  A misnamed `sqlsource.json` and an unreachable `nestedClass` are not this layer's to report (no caller yet); the notes for layers 3 and 4 say so.
- **The schema writes the choices and the dialect as patterns** that match what `SettingValue.TryReadChoice`, `GeneratorParameterList` and `SqlDialectName.TryParse` accept, with an `enum` beside each for editors' completion.  The texts of `database`, the suffixes, the namespace and the nested class are plain strings: their rules use Unicode categories and C# keywords, which a schema's regex cannot be relied on to hold (the spec's exception).

## Global Constraints

- The analyzer targets `netstandard2.0`, ships no dependencies, may not use `System.Text.Json`; no collection expression may target `ImmutableArray<T>`.
- Nothing a user writes may make the reader throw; nesting is bounded by `SidecarTokenizer.MaxDepth` (64).
- A cached result is a record with value equality and holds collections in `EquatableArray<T>`.
- Every commit passes `./pre-commit-validation.sh` (AGENTS.md, Commits).  Run `./format.sh` first.  The script needs Docker and takes many minutes: run it in the background and wait for it.
- Test names are `Method_Scenario_Outcome`, assertions Shouldly's.
- Each new descriptor: written in full with a literal id, in `SqlDiagnostics.All`, a row of `AnalyzerReleases.Unshipped.md`, a section and a table row of `docs/manual/diagnostics.md`.  Error, `NotConfigurable`.
- An `AGENTS.md` that names a file is updated in the commit that adds, renames or removes it.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. A file cut short or damaged anywhere, or nested 1000 deep: the reader returns, never throws (Task 3, robustness theory).
2. A sidecar with a comment or a trailing comma: still malformed (Task 1).
3. A key in other case, `"Dialect"`: unknown, not read (Task 3).
4. A type override written by an alias and by its name, `"integer"` and `"int4"`: one key, the second reported (Task 2).
5. Two files' errors in the order of their positions, whatever order the settings are read in (Task 3).

---

### Task 1: Lenient mode of the tokenizer

**Files:**
- Modify: `src/SqlSource/Snapshot/Reading/SidecarTokenizer.cs`
- Test: `tests/SqlSource.Tests/Snapshot/Reading/SidecarTokenizerTests.cs`, `tests/SqlSource.Tests/Snapshot/Reading/SidecarReaderTests.cs`
- Modify: `src/SqlSource/AGENTS.md` (`## Snapshot/`: one bullet, "The sidecar is strict JSON: no reader of a sidecar constructs a `SidecarTokenizer` with `lenient: true`, which `SettingsFiles/` alone uses.")

**Interfaces:**
- Produces: `new SidecarTokenizer(string text, int position = 0, bool lenient = false)`.  In lenient mode `Next()` skips `//` comments (to the next `\n` or `\r` or the end) and `/* */` comments as white space, and `TrySkipValue` accepts one comma before the `}` or `]` that closes a non-empty object or array.  A `/*` with no `*/` is an `Invalid` token with an empty span at the end of the text; a `/` that starts no comment is `Invalid` at the `/`.  `{,}` and `[,]` stay invalid.  Strict mode is unchanged.

- [ ] **Step 1: Write the failing tests** in `SidecarTokenizerTests`:
  - `Next_CommentsInLenientMode_AreSkippedAsWhiteSpace`: `"// a\n/* b */ 1 /* c\n d */ // e"` with `lenient: true` gives only `Number:1`.
  - `Next_CommentInStrictMode_IsInvalidAtTheSlash`: `"/ 1"` and `"// a\n1"` strict give `Invalid` at offset 0.
  - `Next_BlockCommentNotClosed_IsInvalidAtTheEnd`: lenient `"1 /* a"` gives `Number`, then `Invalid` with `new TextSpan(6, 0)`.
  - `Next_SlashThatStartsNoComment_IsInvalidAtTheSlash`: lenient `"/x"` gives `Invalid` with `new TextSpan(0, 1)`.
  - `TrySkipValue_TrailingCommaInLenientMode_IsAccepted`: theory over `{"a":1,}`, `[1,]`, `{"a":[1,2,],}` → true.
  - `TrySkipValue_TrailingCommaInStrictMode_IsTheClosingBracket`: `[1,]` strict → false, error span at the `]`.
  - `TrySkipValue_CommaAloneInLenientMode_IsInvalid`: `{,}` and `[,]` lenient → false.
  - In `SidecarReaderTests`: `Read_FileWithACommentOrATrailingComma_IsInvalidJson`: the Users example with `// c\n` inserted after the first `{`, and with a `,` inserted before the last `}`, each gives `SidecarErrorKind.InvalidJson`.
- [ ] **Step 2: Run them and see them fail**: `dotnet test --project tests/SqlSource.Tests --filter-class "*SidecarTokenizerTests"` (or the solution's usual filter syntax; see `tests/README.md`).  Expected: compile error on the `lenient` argument.
- [ ] **Step 3: Implement** the flag as a primary-constructor parameter; the comment skip lives in the white-space loop of `Next()`, the trailing comma in `TrySkipObject` / `TrySkipArray` after a comma.  Update the type's summary: strict JSON for sidecars, lenient for settings files.
- [ ] **Step 4: Run the tests and see them pass**, and `SidecarReaderAllocationTests` unchanged.
- [ ] **Step 5: Format, validate, commit**: `./format.sh`, `./pre-commit-validation.sh` passes, commit `Read comments and trailing commas in a lenient mode of SidecarTokenizer`.

### Task 2: The array and object forms of a setting

**Files:**
- Modify: `src/SqlSource/Settings/ISettingSource.cs`, `src/SqlSource/Settings/SettingsLevelReader.cs`, `src/SqlSource/Settings/GeneratorParameterList.cs`, `src/SqlSource/Settings/TypeOverrides/TypeOverrideGrammar.cs`, `src/SqlSource/Settings/ColumnTypes/ColumnTypeGrammar.cs`
- Modify (stubs only): `src/SqlSource/Generation/MSBuild/MSBuildSettings.cs` (`Source`), `src/SqlSource/Generation/TargetTypeReader.cs` (the attribute's source)
- Test: `tests/SqlSource.Tests/Settings/SettingsLevelReaderTests.cs` (its `FakeSource` gains the members), `tests/SqlSource.Tests/Settings/GeneratorParameterListTests.cs`, `tests/SqlSource.Tests/Settings/TypeOverrides/TypeOverrideGrammarTests.cs`, `tests/SqlSource.Tests/Settings/ColumnTypes/ColumnTypeGrammarTests.cs` (create where absent, beside the existing tests of the folder)
- Modify: `src/SqlSource/AGENTS.md` (`## Settings/`: the `ISettingSource` sentence names the array and object forms)

**Interfaces:**
- Produces, on `ISettingSource`:
  - `IReadOnlyList<string>? Elements(SettingId setting)`: the elements of a setting written as a JSON array of strings, as a settings file writes the generator parameters; null when the source writes it as text or not at all.
  - `IReadOnlyList<KeyValuePair<string, string>>? Members(SettingId setting)`: each member's key and value as written, for the type override and the column types written as an object; null likewise.
  - `void InvalidPart(SettingId setting, int index)`: the element or member at `index` is not valid; the others apply.
  - `void RepeatedKey(SettingId setting, int index, int first)`: the member at `index` has the key of the member at `first`, by the map's identity; it is left out.
  - The MSBuild and attribute sources return null and do nothing for these.
- Produces, in `Settings/`:
  - `GeneratorParameterList.ReadElements(IReadOnlyList<string> elements, List<int> invalid) -> GeneratorParameters?`: each element trimmed; a blank element skipped; no word at all (`[]`, `[""]`) is `GeneratorParameters.None`, the empty list; `default` alone or repeated is `None`; an unknown word's index is added and the others apply; only unknown words give null; `default` beside another word gives null, with the index of each `default` element and each unknown word added, in index order.
  - `TypeOverrideGrammar.ReadMembers(IReadOnlyList<KeyValuePair<string, string>> members, SettingPlace place, List<int> invalid, List<(int Index, int First)> repeated) -> TypeOverrideLists`: key and type trimmed; type `default` ignoring case is null, else `ClrTypeName.TryParse`; key by `TypeKeys.TryNormalizeEach`; a member neither engine reads, or whose type is not valid or empty, is invalid.  A member is repeated when either engine's list already has its key (`TypeOverrideMap.IndexOf`); it then goes in neither.  Lists in the order of `TypeOverrideMap`.
  - `ColumnTypeGrammar.ReadMembers(IReadOnlyList<KeyValuePair<string, string>> members, SettingPlace place, List<int> invalid, List<(int Index, int First)> repeated) -> EquatableArray<ColumnType>`: the same, keys by `ColumnTypeKey.TryNormalize`, identity by `ColumnTypeMap.IndexOf`.
  - `SettingsLevelReader.Read` asks `Elements(Parameters)` before `Text(Parameters)`, and `Members(TypeOverrides)` / `Members(ColumnTypes)` before `Text`, and reports through `InvalidPart` and `RepeatedKey`.  A form with no valid entry sets nothing, as the text form does; the generator parameters' `None` from `[]` is set.

- [ ] **Step 1: Write the failing tests.**
  - `GeneratorParameterListTests.ReadElements_...`: `[]` → `None`; `["", "  "]` → `None`; `["DEFAULT"]` → `None`; `[" keep-comments ", "sort-input"]` → `KeepComments | SortInput`, no invalid; `["keep-comments", "bogus"]` → `KeepComments`, invalid `[1]`; `["bogus"]` → null, `[0]`; `["keep-comments sort-input"]` → null, `[0]`; `["default", "keep-comments"]` → null, `[0]`; `["bogus", "default", "default"]` → null, `[0, 1, 2]`.
  - `TypeOverrideGrammarTests.ReadMembers_...`: `{"int4": "long", "public.\"Email\"": "MyApp.Email", "timestamptz": "default"}` gives PostgreSQL entries `int4→long`, `public."Email"→global::MyApp.Email`, `timestamptz→null`, each at the given place; `{"nvarchar": "string"}` is in the SQL Server list only; `{"int4": "int?"}` → invalid `[0]`; `{"[dbo].[Email]": "X"}` → invalid `[0]`; `{"int4": ""}` → invalid `[0]`; `{"integer": "long", "int4": "int"}` → one entry `int4→long`, repeated `[(1, 0)]`; `{" int4 ": " long "}` → `int4→long`.
  - `ColumnTypeGrammarTests.ReadMembers_...`: `{"users.id": "UserId", "public.posts.created_by": "UserId"}` → two entries; `{"id": "X"}` → invalid; `{"users.id": "A", "\"users\".id": "B"}` → repeated `[(1, 0)]`; `default` → null type.
  - `SettingsLevelReaderTests`: `Read_ParametersAsElements_...` (elements win over text; `[]` sets `None`; an unknown word goes to `InvalidPart` with its index); `Read_TypeOverridesAsMembers_...` and `Read_ColumnTypesAsMembers_...` (entries set; `InvalidPart` and `RepeatedKey` called with the indexes; `{}` sets nothing).  Extend the existing `Asked` order test, if it records every call, with the new calls.
- [ ] **Step 2: Run them and see them fail** (compile errors on the new members).
- [ ] **Step 3: Implement** the interface members, the three grammar methods and the reader's branches; stubs in the two existing sources.  Update `ISettingSource`'s summary: a source finds a raw value as text, elements or members.
- [ ] **Step 4: Run the tests and see them pass**, the existing settings and generator tests too.
- [ ] **Step 5: Format, validate, commit**: `Read the array form of the generator parameters and the object form of the type override and the column types`.

### Task 3: The settings file reader

**Files:**
- Create in `src/SqlSource/SettingsFiles/`: `SettingsFile.cs`, `SettingsFileReader.cs`, `SettingsFileKeys.cs`, `SettingsFileValueType.cs`, `SettingsFileError.cs`, `SettingsFileErrorKind.cs`, `SettingsFileSource.cs` (the `ISettingSource`, `internal` to the reader's use), and whatever small record holds a raw value with its spans.
- Test: create `tests/SqlSource.Tests/SettingsFiles/SettingsFileReaderTests.cs` and `tests/SqlSource.Tests/SettingsFiles/SettingsFileReaderRobustnessTests.cs`.
- Modify: `src/SqlSource/AGENTS.md`, a new `## SettingsFiles/` section after `## Settings/` (rules below).

**Interfaces:**
- Consumes: Task 1's lenient tokenizer (`SidecarTokenizer`, `SidecarToken`, `GetString`), Task 2's `ISettingSource` members, `SettingsLevelReader.Read`, `SqlDialectName.TryParse`.
- Produces (namespace `SqlSource.SettingsFiles`):
  - `internal sealed record SettingsFile(bool? Root, SqlDialectChoice? Dialect, SettingsLevel Level, EquatableArray<SettingsFileError> Errors)` with `public const string FileName = "sqlsource.json";`.  `Root`: null when the text is not one JSON object (its `root` is unknown, so a chain goes on past it), otherwise true only for `"root": true`.  `Dialect`: null when not set, blank or not valid.  `Level`: `SettingsLevel.None` when nothing is set.  `Errors`: sorted by `Span.Start`, a stable sort.
  - `internal static class SettingsFileReader { public static SettingsFile Read(string text, SettingPlace place); }`
  - `internal enum SettingsFileValueType { String, Boolean, ArrayOfStrings, ObjectOfStrings }`
  - `internal static class SettingsFileKeys`: a `const string` for each key of the spec's table (`Schema = "$schema"`, `Root`, `Dialect`, `Database`, `Output`, `GeneratorParameters`, `InputModelSuffix`, `OutputModelSuffix`, `ModelNamespace`, `InputModelType`, `OutputModelType`, `CollectionType`, `TypeOverrides`, `ColumnTypes`, `NestedClass`), `ImmutableArray<string> All` in the table's order, `SettingsFileValueType TypeOf(string key)` (throws `ArgumentOutOfRangeException` for a key not in `All`: callers pass only known keys), `bool TryGetSetting(string key, out SettingId setting)` and `string KeyOf(SettingId setting)`.
  - `internal enum SettingsFileErrorKind { InvalidJson, NotAnObject, UnknownKey, DuplicateKey, WrongType, InvalidValue, RepeatedKey }`, each member documenting its span and arguments as below.
  - `internal sealed record SettingsFileError(SettingsFileErrorKind Kind, TextSpan Span, string? Setting, string? Argument, string? Other)` with `public bool IsAboutTheFile => Kind is not (SettingsFileErrorKind.InvalidValue or SettingsFileErrorKind.RepeatedKey);` (both the build and the tool report those; the others only the side that reads `Setting`).

| Kind | Span | Setting | Argument | Other |
|----|----|----|----|----|
| `InvalidJson` | the character or token that breaks the grammar, or the bracket that opens level 65; empty at the end of a text cut short | null | null | null |
| `NotAnObject` | the whole top-level value | null | null | null |
| `UnknownKey` | the key, quotes included | null | the key | null |
| `DuplicateKey` | the second key, quotes included | null | the key | null |
| `WrongType` | the whole value, or the element or member's value | the top-level key | null | null |
| `InvalidValue` | the string, quotes included; for a member, from its key's opening quote to its value's closing quote | the top-level key | the value trimmed; for a member, its key trimmed | null; for a member, its value trimmed |
| `RepeatedKey` | the repeated member's key, quotes included | the top-level key | that key trimmed | the first member's key trimmed |

Rules the reader holds (each one a test below):
- Pass one: a lenient tokenizer runs `TrySkipValue` on the first token at depth 0, then needs `End`.  A failure is the file's one error, `InvalidJson`; a top level that is not `{` is `NotAnObject`.  Either way `Root` and `Dialect` are null and `Level` is `None`.
- Pass two walks the object's members.  A key seen before (ordinal) is `DuplicateKey`, its value skipped; else a key not in `SettingsFileKeys.All` is `UnknownKey`, skipped.  `$schema` must be a string; `root` `true` or `false`; the other keys by `TypeOf`, a wrong top-level type (`null` included) being `WrongType` and setting nothing.  An element or member value that is not a string is `WrongType` and is left out.
- `dialect`: blank sets nothing; otherwise `SqlDialectName.TryParse` of the trimmed text, or `InvalidValue`.
- Every `SettingId` setting goes through `SettingsLevelReader.Read` with the source.  `Choice<T>`: blank is null; the trimmed text by `SettingValue.TryReadChoice`, or `Invalid(setting, trimmed)`.  The source turns `Invalid`, `InvalidPart` and `RepeatedKey` into errors with the spans of the table.

`AGENTS.md` section `## SettingsFiles/` (rules, one line of reason each): the reader never throws and bounds nesting by `SidecarTokenizer.MaxDepth`, and never hands pass two a text pass one has not passed; it may use `Settings/`, `Snapshot/Reading/`'s tokenizer and `Parsing/SqlDialectName.cs`, and nothing of `Generation/` or `Diagnostics/`; validity of a value is `SettingsLevelReader`'s, never the reader's own; a new key is a constant of `SettingsFileKeys`, a row of `All` and `TypeOf`, a property of `schemas/settings-v1.schema.json`, and, for a new kind of error, a case of `SettingsFileDiagnostics`.  Maintenance footer stays.

- [ ] **Step 1: Write the failing tests** in `SettingsFileReaderTests` (place `SettingPlace.Metadata` throughout; one `[Fact]` or `[Theory]` per rule):
  - `Read_DesignsExample_SetsEverySettingWithoutErrors`: the spec's jsonc example (comments, trailing commas, `$schema`) gives `Root` true, dialect `postgres`, parameters `KeepComments | AsyncMethodSuffix`, the two type overrides and the two column types, no errors.
  - `Read_EveryStringSetting_IsSetFromItsKey`: a file with every string and choice key gives the level's members (`output: "Code-Gen"` → `OutputKind.CodeGen`, `inputModelType: "sealed record"`, `collectionType: "ireadonlylist"`, `database: "orders"`, suffixes, namespace, nested class); `dialect: "mysql, ansi_quotes"` → MySql with `AnsiQuotes`.
  - `Read_RootFalseOrAbsent_IsFalse`.
  - `Read_TextThatIsNotJson_IsInvalidJsonAndGivesNothing`: theory over `""`, `"{"`, `"{} {}"`, `"{\"a\" 1}"`, `"{ /* open"`, 65 nested arrays as the value of `"x"`; `Root` null, `Level` `None`, one `InvalidJson` error with its span.
  - `Read_TopLevelThatIsNotAnObject_IsNotAnObject`: `"[]"`, `"1"`, `"\"a\""`, `"null"`.
  - `Read_KeyNotInTheTable_IsUnknown`: `{"Dialect": "postgres", "bogus": [1, {}]}` gives two `UnknownKey` errors at the keys, no dialect.
  - `Read_KeyGivenTwice_IsDuplicateAndTheFirstApplies`: `{"output": "sql", "output": "models", "zzz": 1, "zzz": 2}` → output `Sql`, `DuplicateKey` at the second `"output"`, `UnknownKey` at the first `"zzz"` and `DuplicateKey` at the second.
  - `Read_ValueOfTheWrongType_IsWrongTypeAndSetsNothing`: theory of (key, JSON value): `dialect 1`, `output null`, `root "yes"`, `$schema 1`, `generatorParameters "keep-comments"`, `typeOverrides []`, `columnTypes "a.b=X"`; error span is the whole value, setting unset.
  - `Read_ElementOrMemberOfTheWrongType_IsWrongTypeAndTheOthersApply`: `{"generatorParameters": ["keep-comments", 1]}` → `KeepComments`, `WrongType` at `1`; `{"typeOverrides": {"int4": "long", "text": {}}}` → `int4` entry, `WrongType` at `{}`.
  - `Read_EmptyOrWhiteSpaceString_SetsNothingAndIsNoError`: every string key with `""` and `" \t"`.
  - `Read_ValueThatIsNotValid_IsInvalidValueAndSetsNothing`: `output "bogus"`, `modelNamespace "a..b"`, `nestedClass "class"`, `database "a b"`, `dialect "sqlserver2"`, `dialect "ansi,ansi-quotes"`; the error's `Argument` is the value trimmed, the span the string; setting null.
  - `Read_GeneratorParameters_FollowTheElementRules`: `[]` → `None`; `["keep-coments", "sort-input"]` → `SortInput` and `InvalidValue` at `"keep-coments"`; `["default", "sort-input"]` → unset, `InvalidValue` at `"default"`.
  - `Read_TypeOverrideMembers_...`: a not-valid member is `InvalidValue` with `Argument` key and `Other` type, span from key to value; `{"integer": "long", "int4": "int"}` → `RepeatedKey` at `"int4"` with `Other` `integer`; the entries carry `place`.
  - `Read_ColumnTypeMembers_...`: the same for column types.
  - `Read_Errors_AreInTheOrderOfTheirPositions`: `{"nestedClass": "class", "output": "bogus", "bogus": 1}` gives errors at increasing `Span.Start` though the level reads `output` first.
  - `Read_FileThatSetsNothing_GivesTheSharedEmptyLevel`: `{}` and `{"$schema": "x", "root": true}` give `ReferenceEquals(level, SettingsLevel.None)`.
  - `Read_TwoReadsOfOneText_AreEqual` (value equality, for caching).
  - In `SettingsFileReaderRobustnessTests`: `Read_DesignsExampleCutShortAnywhere_ReturnsWithAnError` (every prefix shorter than the trimmed text: no exception, an `InvalidJson` error, `Root` null), `Read_DesignsExampleWithACharacterReplacedAnywhere_Returns` (each offset replaced by each of `{ } [ ] " , : / * \\ 1 a` and a NUL: no exception; `Root` is null exactly when an `InvalidJson` or `NotAnObject` error is present), `Read_DeepNesting_IsInvalidJsonWithoutOverflow` (10 000 `[` as a value).
- [ ] **Step 2: Run them and see them fail.**
- [ ] **Step 3: Implement** the types above.  The source is a `struct` holding the raw values by `SettingId` and a reference to the reader's error list.
- [ ] **Step 4: Run the tests and see them pass.**
- [ ] **Step 5: Format, validate, commit**: `Read a sqlsource.json file into its root, dialect and settings level`.

### Task 4: The diagnostics

**Files:**
- Modify: `src/SqlSource/Diagnostics/SqlDiagnostics.cs` (six descriptors after `InvalidSettingValue`, and `All`), `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/manual/diagnostics.md`
- Create: `src/SqlSource/Diagnostics/SettingsFileDiagnostics.cs`
- Test: `tests/SqlSource.Tests/Diagnostics/SettingsFileDiagnosticsTests.cs`; `SqlDiagnosticsTests` and `DiagnosticsDocumentTests` must keep passing.
- Modify: `src/SqlSource/AGENTS.md` (`## Diagnostics`: one bullet naming `SettingsFileDiagnostics.Create` as the one place a `SettingsFileError` becomes a diagnostic.)

**Interfaces:**
- Consumes: `SettingsFileError`, `SettingsFileErrorKind`, `SettingsFileKeys`, `TypeOverrideGrammar.BracketHelp`, `SqlDialectName.Accepted`, `SidecarTokenizer.MaxDepth`.
- Produces: `internal static class SettingsFileDiagnostics { public static DiagnosticInfo Create(SettingsFileError error, string path, SourceText text); }`, location `LocationInfo.From(path, text, error.Span)`.

| Id | Field | Title | Message | Kinds |
|----|----|----|----|----|
| SQLSRC015 | `SettingsFileNotAnObject` | Settings file is not a JSON object | `This settings file is not one JSON object: {0}` | `InvalidJson`: `the text is not JSON, or nests deeper than 64 levels`; `NotAnObject`: `its top level is not an object` (64 from `MaxDepth`) |
| SQLSRC016 | `SettingsFileUnknownKey` | Settings file key is not known | `'{0}' is not a key of a settings file` | `UnknownKey` |
| SQLSRC017 | `SettingsFileDuplicateKey` | Settings file key is written twice | `'{0}' is written twice in this settings file, and the first applies` | `DuplicateKey` |
| SQLSRC018 | `SettingsFileWrongType` | Settings file value has the wrong type | `'{0}' takes {1}` | `WrongType`; `{1}` by `TypeOf(Setting)`: `a string`, `true or false`, `an array of strings`, `an object whose values are strings` |
| SQLSRC019 | `SettingsFileInvalidValue` | Settings file value is not valid | `'{0}' is not a valid value of {1}{2}` | `InvalidValue`; `{0}` the value, or `key: type` for a member; `{2}`: for `dialect`, `.  dialect takes ` + `SqlDialectName.Accepted`; for a `default` element of `generatorParameters`, `.  default is the empty list and stands alone: write [] for it`; for a member of `typeOverrides`, `BracketHelp(key)`; otherwise empty |
| SQLSRC020 | `SettingsFileRepeatedKey` | Settings file entry repeats a key | `'{0}' is the same key as '{1}' in {2}, and the first applies` | `RepeatedKey`; `{2}` the setting |

A kind that is no member gives `SettingsFileNotAnObject` with a reason naming its number, as `SidecarFileChecks.ReasonOf` does.

`docs/manual/diagnostics.md`: a `**Settings files**` table of the six between the project table and the `.sql` table; six sections between `SQLSRC014` and `SQLSRC101`, each its bold title, what is wrong, a `jsonc` sample, how to fix it, and what applies meanwhile (the file gives nothing / the setting is not set / the first applies); `SQLSRC019` lists what each key takes, in the words of `SQLSRC014`'s section; and the page's opening paragraph adds that an error in a settings file is reported at its line and column there.  Effects only: no type names.  Each section ends `See: [Settings](settings.md)`.

- [ ] **Step 1: Write the failing tests** in `SettingsFileDiagnosticsTests`: `Create_EachKind_GivesItsDescriptor` (theory over the seven kinds); `Create_Location_IsTheLineAndColumnOfTheSpan` (a three-line text with the error on line 2, `LineSpan.Start` `(1, 4)`); `Create_InvalidDialect_EndsWithTheAcceptedNames`; `Create_DefaultBesideAnotherParameter_SaysItStandsAlone`; `Create_TypeOverrideKeyInBrackets_GivesTheBracketHelp` (`[dbo].[Email]` → message contains `dbo.Email`); `Create_InvalidMember_QuotesKeyAndType` (`'int4: int?' is not a valid value of typeOverrides`); `Create_WrongType_NamesTheTypeTheKeyTakes` (theory over the four types); `Create_InvalidJson_NamesTheDepthLimit` (message contains `64`).
- [ ] **Step 2: Run them and see them fail.**
- [ ] **Step 3: Implement** the descriptors, the release rows, the mapper and the document.
- [ ] **Step 4: Run the tests and see them pass**, `SqlDiagnosticsTests`, `DiagnosticsDocumentTests` and the documentation tests too.
- [ ] **Step 5: Format, validate, commit**: `Add the diagnostics of a settings file, SQLSRC015 to SQLSRC020`.

### Task 5: The schema and its agreement test

**Files:**
- Create: `schemas/settings-v1.schema.json`
- Modify: `tests/SqlSource.Tests/SqlSource.Tests.csproj` (link the schema to `schemas/settings-v1.schema.json` in the output folder, as the sidecar's is)
- Modify: `src/SqlSource/Parsing/SqlDialectName.cs`: `Names` and `Options` become `internal`, and a new `internal static bool TakesOptions(SqlDialect dialect)` replaces the private `OptionsOf` test (or `OptionsOf` becomes internal), so the test generates the dialect's pattern.  No behaviour change.
- Test: create `tests/SqlSource.Tests/SettingsFiles/SettingsFileSchemaTests.cs`

**Interfaces:**
- Consumes: `SettingsFileKeys`, `SettingsFileReader`, `SettingValue.TryReadChoice`, `GeneratorParameterList.TryFind` / `IsDefault`, `SqlDialectName`.

Schema shape: draft 2020-12, `$id` `https://raw.githubusercontent.com/mbcrawfo/SqlSource/main/schemas/settings-v1.schema.json`, a title and a description saying it is for editors and that keys, types and the choices are checked; `"type": "object"`, `"additionalProperties": false`, a property for each key of `SettingsFileKeys.All` with a `description` of the setting:
- `$schema`, `database`, `inputModelSuffix`, `outputModelSuffix`, `modelNamespace`, `nestedClass`: `{"type": "string"}`.
- `root`: `{"type": "boolean"}`.
- `output`, `inputModelType`, `outputModelType`, `collectionType`, and `dialect`: `{"type": "string", "anyOf": [{"enum": [...]}, {"pattern": P}]}`.
- `generatorParameters`: `{"type": "array", "items": {"type": "string", "anyOf": [{"enum": [...]}, {"pattern": P}]}}`.
- `typeOverrides`, `columnTypes`: `{"type": "object", "additionalProperties": {"type": "string"}}`.

The patterns, which the test generates and compares to the schema's text exactly (`C(name)` writes each letter `x` as `[xX]` and any other character as itself; `|` joins alternatives in the order of the source list):
- A choice of enum `T`: `^\s*(?:[- \t]*(?:` + the names of `T`'s members in declaration order, each written letter by letter as `[xX][- \t]*`, joined by `|` + `))?\s*$`.
- `generatorParameters`' element: `^\s*(?:` + `C` of each parameter name, then `C("default")` + `)?\s*$`.
- `dialect`: `^\s*(?:(?:` + `C` of each name of a dialect that takes no option + `)|(?:` + `C` of each name of a dialect that takes options + `)(?:\s*,\s*(?:` + `C` of each option name + `))*)?\s*$`.

The enums are spellings for completion: the documented names (`"sql"`, `"models"`, `"codegen"`; `"record"`, `"sealed record"`, `"class"`, `"sealed class"`; the ten collection names; the six parameters and `"default"`; the eight dialect names of `SqlDialectName.Accepted`).

- [ ] **Step 1: Write the failing tests**:
  - `Schema_Properties_AreTheKeysOfTheReader` (set equality with `SettingsFileKeys.All`), `Schema_TypeOfEachKey_IsTheReaders` (via `TypeOf`).
  - `Schema_EnumOfAChoice_NamesEveryMemberOnce` (theory over the four choice keys and their enums: each value read by `TryReadChoice`, distinct, covering every member); `Schema_EnumOfGeneratorParameters_NamesEveryParameterAndDefault`; `Schema_EnumOfDialect_NamesEveryDialect`.
  - `Schema_PatternOfAChoice_IsGeneratedFromItsEnum`, `..._OfGeneratorParameters_...`, `..._OfDialect_IsGeneratedFromTheNamesAndOptions` (exact string equality).
  - `Schema_Id_IsTheUrlOfTheSchemasFolder`.
  - `Schema_DesignsExample_IsValid` (parse with `JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }`).
  - `Schema_SpellingTheReaderAccepts_IsValid`: theory of (key, JSON value) — `output " Code-Gen "`, `inputModelType "SEALED record"`, `collectionType "i-read-only-list"`, `output ""`, `dialect " MySQL , ANSI_QUOTES "`, `dialect "tsql"`, `dialect "  "`, `generatorParameters ["  Keep-Comments ", ""]` — the schema accepts it and the reader reports no error.
  - `Schema_FileTheReaderRejects_IsNotValid`: theory — `{"bogus": 1}`, `{"Dialect": "ansi"}`, `{"root": "yes"}`, `{"output": "bogus"}`, `{"output": "-"}`, `{"dialect": "ansi,ansi-quotes"}`, `{"dialect": "mysql,"}`, `{"generatorParameters": ["nope"]}`, `{"generatorParameters": "keep-comments"}`, `{"typeOverrides": {"int4": 5}}`, `{"columnTypes": []}`, `{"output": null}` — the schema rejects it and the reader reports an error.
- [ ] **Step 2: Run them and see them fail.**
- [ ] **Step 3: Write the schema** and the visibility change.
- [ ] **Step 4: Run the tests and see them pass.**
- [ ] **Step 5: Format, validate, commit**: `Add schemas/settings-v1.schema.json and a test that holds it to the reader`.

### Task 6: Architecture documentation

**Files:**
- Modify: `docs/architecture/generator.md`, `## Settings`: a `### Settings files` subsection after `### The column types`; and in `### Reading a level`, one bullet for the settings file's source.
- Modify (only if a test or a list there names the touched files): `tests/README.md`.

Content of the subsection, as the code is today: `SettingsFileReader.Read` (link) reads one `sqlsource.json`, JSON with comments and trailing commas through `SidecarTokenizer`'s lenient mode, in two passes so that it never throws and nests at most `MaxDepth`; what `SettingsFile` holds and why `Root` can be unknown; that values go through `SettingsLevelReader` with the array and object forms, the dialect through `SqlDialectName`; that errors are `SettingsFileError`s with spans, which `SettingsFileDiagnostics.Create` turns into `SQLSRC015` to `SQLSRC020` with line and column, `IsAboutTheFile` saying which both the build and the tool report; that the schema in `schemas/` is held to the reader by `SettingsFileSchemaTests`; and that the generator and the tool do not read settings files yet.

- [ ] **Step 1: Write the section and the bullet.**
- [ ] **Step 2: Format, validate, commit**: `Describe the settings file reader in generator.md`.
