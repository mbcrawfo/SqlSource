# Query generation, phase 0: names - design

Date: 2026-10-08

Phase 0 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  The epic decided what is renamed; this document adds the detail a plan needs and settles the one question the epic left open.

## Goal

The names a user writes are the names the rest of the epic builds on, before anything is published under the old ones.

```csharp
[SqlSourceGenerate(Path = "Queries", SqlLocation = SqlLocation.Direct)]
internal static partial class Queries;
```

```sql
-- A licence header.
-- dialect: mysql, no-backslash-escapes
-- generator: keep-comments

-- name: GetPath
-- generator: no-token-validation
SELECT 'C:\temp\' AS path FROM {{table}};
```

Success is:

- A project written with the new names gets exactly the members it got under the old ones.
- No file of the repository, outside `docs/superpowers/`, names `SqlQueries`, `SqlQueriesMode`, `-- SqlSource:` or the `dialect=` directive, and none calls what a `-- generator:` line holds a directive.
- Every diagnostic keeps its id.
- The allocation budgets of `SqlFileParserAllocationTests` and `PathResolverAllocationTests` do not rise.

## Decisions

From the epic, unchanged:

| Decision | Choice |
|----|----|
| The attribute | `SqlQueriesAttribute` becomes `SqlSourceGenerateAttribute`, written `[SqlSourceGenerate]`.  `Path` is unchanged. |
| The enum and its property | `SqlQueriesMode` becomes `SqlLocation`, and so does the attribute's `Mode` property.  `Nested` and `Direct` are unchanged. |
| The generator marker | `-- SqlSource:` becomes `-- generator:`.  What it holds is a generator parameter: `keep-comments`, `token-validation`, `no-token-validation` and `token-ignore=name`. |
| The dialect | The `dialect=` directive becomes a marker of its own, `-- dialect: postgres`, with the placement rule the directive had: before the first `-- name:` line and before any SQL. |
| Diagnostics | Reworded where they name what changed.  No id is added, removed or renumbered. |
| Shape of the work | One pull request, one commit for each rename. |

Settled with the owner for this phase:

| Decision | Choice | Why |
|----|----|----|
| Spaces in the dialect's value | Accepted: `-- dialect: mysql, ansi-quotes` | The value is the rest of the line and goes whole to `SqlDialectName.TryParse`, which trims each part, as it does for the property and the metadata.  The rule that the value is one word existed only because directives were separated by spaces. |
| The old spellings | No compatibility.  `-- SqlSource: keep-comments` is an ordinary comment, and `-- generator: dialect=mysql` is `SQLSRC109` | Nothing has been published.  A marker is a line comment whose word SqlSource knows; nothing else in a comment is read. |
| Where the dialect marker is checked | In the file parser, for the file, not in the scope of generator parameters | A file has one dialect and a dialect is not a generator parameter.  The only place a dialect is valid is one scope already, the preamble or the single query of a file without `-- name:`, so nothing a user sees changes. |
| The package-install project | Gains one file, `Queries/ByMarker.sql`, with a `-- dialect:` marker | The check then proves both new markers in a packed consumer, as the epic asks. |

## Out of scope

- Every other marker, setting, enum and generator parameter of the epic.  They are phase 1's.
- `token-ignore=name` stays a generator parameter; phase 1 makes it a marker.
- `MemberPlacement`, the generator's own form of the enum, keeps its name.  Only its documentation changes.
- `SqlSourceTokenValidation` and `SqlSourceDialect`, the MSBuild names, are unchanged.
- The files under `docs/superpowers/` other than the epic outline.  They record intent at the time and are not rewritten.

## What a user sees

### The attribute

`[SqlSourceGenerate]` replaces `[SqlQueries]`, and `SqlLocation = SqlLocation.Direct` replaces `Mode = SqlQueriesMode.Direct`.  Where the README and `docs/diagnostics.md` call `Nested` and `Direct` a mode, they call it a location.

The generated file of the attribute is `SqlSourceGenerateAttribute.g.cs`.

### Markers

The README states the rule once: a line comment that starts its line and has the form `-- word: rest`, where the word is one SqlSource knows, is a marker.  Nothing else in a comment is read.  Each marker has its own grammar for the rest.  The words are `name`, `summary`, `generator` and `dialect`, matched ignoring case, as today.

Every marker line is removed from the generated SQL, the dialect marker included.

### Generator parameters

A `-- generator:` line holds one or more generator parameters, separated by spaces.  Inside a query it applies to that query; before the first `-- name:` line it applies to every query in the file.  The parameters and their effects are those of the old directives, less `dialect`.

### The dialect marker

`-- dialect: <value>` sets the dialect of the whole file.

- The value is a dialect name, then any options, separated by commas, with any whitespace around each part: `mysql,ansi-quotes` and `mysql, ansi-quotes` are the same.  The names and options are those of the `SqlSourceDialect` property.
- It goes before the first `-- name:` line and before the first SQL.  Comments may come before it.
- It applies from the line after it.  The first marker in that position that names a dialect is the one that takes effect.
- The marker wins over the metadata, and the metadata over the property, whole: `-- dialect: mysql` in a project that sets `mysql,ansi-quotes` is plain `mysql`.

### Diagnostics

Ids are unchanged.  Titles and messages:

| Id | Title | Message | Argument |
|----|----|----|----|
| `SQLSRC006` | SqlLocation is not valid | `'{0}' is not a value of SqlLocation` | The value |
| `SQLSRC007` | unchanged | `... Rename it, or use SqlLocation.Direct.` | |
| `SQLSRC108` | Marker has no SQL after it | `A '-- summary:', '-- generator:' or '-- dialect:' marker comes before the SQL it describes, and no SQL follows this one in its query` | |
| `SQLSRC109` | Generator parameter is not known | `'{0}' is not a generator parameter` | The parameter as written |
| `SQLSRC110` | Generator parameter is missing | `The '-- generator:' marker has no parameter` | |
| `SQLSRC111` | Marker value is not valid | `'{0}' lacks a value it needs, has one it does not take, or has one that is not valid` | The parameter as written, or `dialect: <value>` |
| `SQLSRC112` | Settings conflict | `'{0}' conflicts with a setting given earlier in the same scope` | The second one: the parameter as written, or `dialect: <value>` |
| `SQLSRC115` | Dialect marker is misplaced | `The '-- dialect:' marker must come before the file's first query and before any SQL` | |

Any other diagnostic whose text names the attribute or the enum is reworded the same way.

For the dialect marker, in this order, as for the directive today:

1. At or after the end of the header: `SQLSRC115`, at the whole marker, whatever its value.
2. A value that is empty or is not a dialect with its options: `SQLSRC111`, at the value, or at the whole marker when the value is empty.
3. A value that differs from an earlier valid marker of the file, in dialect or in options: `SQLSRC112`, at the value.  The same value twice is not a conflict.

A dialect marker after the last SQL of its query is reported twice, as `SQLSRC108` and `SQLSRC115`, as today.

The argument `dialect: <value>` is built from the word `dialect`, a colon, a space and the trimmed value, and is `dialect:` when the value is empty.

`docs/diagnostics.md` follows: the index, each section's text and examples, and for `SQLSRC111` the removal of the sentence that a space after a comma ends the value.

## Code

### Commit 1 - the attribute

- `Generation/AttributeSource.cs`: `HintName` is `SqlSourceGenerateAttribute.g.cs`, `AttributeMetadataName` is `SqlSource.SqlSourceGenerateAttribute`, and the emitted class and the `cref` to it are renamed.
- Every use: the comments of `Generation/` and `Diagnostics/`, the diagnostics' text, the tests, `README.md`, `docs/diagnostics.md`, `docs/tech-debt/TD-0006-attribute-conflicts-across-friend-assemblies.md`, and `tools/package-install/Queries.cs`.
- The test type names `UserQueries`, `OrderQueries` and the like are a project's own names and stay.

### Commit 2 - the location

- `AttributeSource`: the emitted enum is `SqlLocation` and the property is `SqlLocation`; `ModeMetadataName` becomes `LocationMetadataName`, `SqlSource.SqlLocation`, and `ModeProperty` becomes `LocationProperty`, `SqlLocation`.
- A property with the name of its type is legal in C# 7.3, which the attribute file must compile as.  A `cref` is the exception: inside the attribute class, `SqlLocation.Nested` may bind to the property and not the enum.  The emitted documentation writes `global::SqlSource.SqlLocation.Nested`, and the existing test that generated code has no warnings, documentation comments included, is the proof.
- `Diagnostics/AttributeConflictSuppressor.cs` reads the new constant.
- `SqlDiagnostics.InvalidMode` becomes `InvalidSqlLocation`, with the text above; `SQLSRC007`'s message follows.
- `AnalyzerReleases.Unshipped.md`, `README.md`, `docs/diagnostics.md`, `TD-0006`, the tests and `tools/package-install/Queries.cs` follow.

### Commit 3 - the generator marker

- `SqlMarkerReader`: the keyword `sqlsource:` becomes `generator:`.
- `SqlMarkerKind.Directives` becomes `GeneratorParameters`.
- `SqlDirectiveScope` becomes `SqlGeneratorParameterScope`, and its test file follows.
- `SqlParseErrorKind`, in the same order, since the ids follow it: `UnknownDirective` becomes `UnknownGeneratorParameter`, `EmptyDirectiveLine` becomes `EmptyGeneratorLine`, `InvalidDirectiveValue` becomes `InvalidMarkerValue`, `ConflictingDirectives` becomes `ConflictingSettings`.  The descriptors in `SqlDiagnostics` take the same names and the text above, less the mention of `-- dialect:` in `SQLSRC108`.
- Every comment, test name and document that says directive for a generator parameter says generator parameter.  The `.editorconfig` settings and the C# `#nullable` and `using` directives are not that word and stay.
- For this one commit the dialect is the generator parameter `dialect=name`, and the documents say so.

### Commit 4 - the dialect marker

- `SqlMarkerReader` gains the keyword `dialect:` and `SqlMarkerKind` gains `Dialect`.
- `SqlPreambleDialect` becomes `SqlDialectMarker`, the one place that knows what a valid dialect marker is:
  - `Apply(SqlLexer lexer, string text)`, as today, switching the lexer at the first marker of kind `Dialect` whose value parses, and returning where the header ends.
  - `TryRead(string text, SqlMarker marker, out SqlDialectChoice dialect)`, which parses the marker's value span with `SqlDialectName.TryParse` over a span.  Nothing is allocated.
- `SqlGeneratorParameterScope` loses `Dialect`, `TryFindDialect`, `ReportMisplacedDialects`, `ApplyDialect` and its `headerEnd` parameter.  `dialect` is then an unknown generator parameter with no code of its own.
- `SqlFileParser`'s `Parser` holds the file's dialect so far and reads each `Dialect` marker by the three rules above, in the preamble, in a query, and after a query's last SQL.  The last case needs no rule of its own: such a marker is past the header by definition.
- `SQLSRC108` names `-- dialect:`; `SQLSRC111`, `SQLSRC112` and `SQLSRC115` take the dialect marker's spans and arguments.
- `tests/SqlSource.Tests/EndToEnd/Dialects/ByDirective.sql` becomes `ByMarker.sql`, so `DialectQueries.ByDirective` becomes `DialectQueries.ByMarker`; `SqlPreambleDialectTests` becomes `SqlDialectMarkerTests`.
- `src/SqlSource/AGENTS.md`, `README.md`, `docs/diagnostics.md` and `docs/tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md` follow.

### The epic outline

In the commit that adds this spec: the phase's row links here and says In progress, and the phase's Decided list records that the dialect's value accepts spaces.  The last commit of the pull request sets the row to Done.

## Testing

The existing tests, under the new names and markers.  Each commit leaves `./pre-commit-validation.sh` passing.

New cases:

| Where | Case |
|----|----|
| `SqlMarkerReaderTests` | `-- generator:` and `-- dialect:` are markers in any case; `-- SqlSource:` is not a marker |
| `SqlTextBuilderTests` | A `-- dialect:` line is removed; a `-- SqlSource:` line is a comment, removed by default and kept under `keep-comments` |
| `SqlDialectMarkerTests` | `-- dialect: mysql, no-backslash-escapes` switches the lexer with the option; a marker with an invalid value is passed over and a later valid one switches |
| `SqlFileParserTests` | The three rules, each with its span and argument; an empty value; the same value twice; a marker after the last SQL gives `SQLSRC108` and `SQLSRC115`; `-- generator: dialect=mysql` is an unknown parameter |
| `Generator/DialectTests` | A file with `-- dialect: mysql, ansi-quotes` is read with the option, on both Roslyn versions |
| `Generator/GeneratedSourceTests` | The attribute file has no warnings, which covers the `cref` to `SqlLocation.Nested` |
| `DiagnosticsDocumentTests` | Whatever it pins of the old marker text is updated |
| `tools/package-install` | `Queries.cs` uses `[SqlSourceGenerate(Path = "Queries", SqlLocation = SqlLocation.Direct)]`; `Users.sql` uses `-- generator: token-validation`; `ByMarker.sql` sets a dialect by marker in a project whose property is `postgres`, and `Program.cs` and `expected-output.txt` gain its line |

A parse of the allocation test's input is measured before and after commit 4, and the pull request states the numbers.

## Documentation

- `README.md`: the attribute and enum throughout; the marker rule; a Generator parameters section in place of Directives; the dialect marker under Dialects, without the caveat about a space after the comma.  Its links stay absolute.
- `docs/diagnostics.md`: as under Diagnostics.
- `src/SqlSource/AGENTS.md`: the types and members it names, in the commit that renames each.
- `CONTRIBUTING.md` and `docs/publishing.md`: nothing in them changes.
- `docs/deferred` and `docs/tech-debt`: nothing is deferred and no debt is expected.  `TD-0004` and `TD-0006` are reworded for the names only.

## Version

The repository has no `v*` tag, so `VersionPrefix` stays `0.1.0`.
