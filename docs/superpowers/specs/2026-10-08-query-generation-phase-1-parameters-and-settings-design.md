# Query generation, phase 1: parameters and settings - design

Date: 2026-10-08

Phase 1 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  The epic decided what this phase delivers; this document adds the detail a plan needs, settles the questions the epic left open, and records three decisions the owner made for this phase.

## Goal

Everything the later phases read from a `.sql` file, from the attribute and from MSBuild is parsed, validated and resolved, before anything is generated from it.

```sql
-- dialect: postgres
-- output: models

-- name: FindUsers -> many
-- summary: Finds users by name.
-- token: {{filter:AND deleted_at IS NULL}}
-- param: @since timestamptz not null
-- param: @page int
-- output-model: UserRow
SELECT id, name FROM users
WHERE name LIKE @pattern AND created_at >= @since {{filter}}
ORDER BY {{orderBy:name}};
```

```csharp
[SqlSourceGenerate(Path = "Queries", Output = GeneratorOutput.Models, Parameters = "no-token-validation")]
internal static partial class Queries;
```

```xml
<PropertyGroup>
    <SqlSourceGeneratorParameters>keep-comments</SqlSourceGeneratorParameters>
</PropertyGroup>
```

Success is:

- A project that uses none of the new markers, properties or attribute properties gets exactly the members it got before, except for the four changes under What changes for a user.
- Every marker, setting, enum and generator parameter of the epic is accepted where the epic allows it and reported where it does not, and none has an effect beyond `keep-comments`, `no-token-validation` and `-- token-ignore:`.
- A query carries its ordered parameter list, its tokens with their resolved defaults, and what its hash is computed from.
- An edit to an MSBuild property, to a file's metadata or to an attribute parses no file again, except a file whose "comments may be wanted" input changes.
- The allocation budgets of `SqlFileParserAllocationTests` and `PathResolverAllocationTests` do not rise.

## Decisions

From the epic, unchanged: the `@name` rule; the parameter list rule; token defaults and their markers; `-- token-ignore:` as a marker; the hash's definition; the `-> shape` suffix; the settings and their precedence; generator parameters at every level, each level setting the list whole; `SqlSourceTokenValidation` and `SQLSRC010` removed; `InternalsVisibleTo` for the tool.

Settled with the owner for this phase.  The first four change the epic, and the outline is updated in the commit that adds this spec:

| Decision | Choice | Why |
|----|----|----|
| Method location | Set by the attribute's `MethodLocation` only.  No marker, no `SqlSourceMethodLocation` property, no metadata | It is a property of the type, as `SqlLocation` is: where a type's members go is decided where the type is declared. |
| `not null` on `-- param:` | Accepted: `-- param: @since timestamptz not null` | It is redundant, since a parameter is non-nullable unless it says `null`, but it is what a column definition says and what a user will write. |
| The `-- token:` marker | Inside a query only.  Not in the preamble | A default is a sample for one query's SQL.  A token of the same name in another query stands in other SQL, and its sample is that query's to give. |
| `keep-comments` outside a marker | The parser always builds the comment-stripped SQL, and builds the SQL with comments as well when it may be wanted | The parameter is the only one that changes what the parser builds, and the levels above the markers resolve after the parse, per type and query.  See Two forms of a query's SQL. |
| How the settings are modelled | One all-nullable record, `SettingsLevel`, filled by each source, and one `Resolve` that takes the first value of each field | The epic asks for one rule and no sibling types. |
| Values from a fixed list | Matched ignoring case, and ignoring hyphens and spaces inside the value: `CodeGen`, `codegen`, `code-gen`; `sealed record`, `SealedRecord` | One rule for the marker, the property and the metadata, whichever spelling the reader of each expects. |
| The parameter list's errors | Parse errors, whatever the query's output resolves to | The output resolves per type and query, after the parse.  A file that is wrong for models is wrong. |
| `SQLSRC010` | Removed, and its id is not used again | As the epic says.  A gap in the ids costs nothing. |
| A file with no `-- name:` marker | It is preamble and query at once: every marker is allowed in it.  It cannot set a shape | The file is one query, and there is no second scope to tell apart. |

## Out of scope

- Any effect of `output`, `database`, the model settings, the collection type, the method location, the shape, `-- param:`, a token's default, and the generator parameters `sort-input`, `sort-output`, `no-table-models` and `async-method-suffix`.  They are phase 5's and phase 8's.
- "A token without a default under `Models` or `CodeGen`" and "the output needs a dialect that has a describer".  Both need the resolved output and a consumer of it, and are phase 5's.
- Reading `SqlSourceDatabase` in the generator.  The targets trim it and the tool reads it, in phase 2.
- The `SqlSourceImported` property and the project manifest.  They are phase 2's.
- The project-only settings: the date and time options, the type override and the library switches.
- Calling the hash from the pipeline.  This phase delivers the function and its tests.

## What changes for a user

Four things that a project written before this phase can notice:

1. `SqlSourceTokenValidation` is gone.  `<SqlSourceGeneratorParameters>no-token-validation</SqlSourceGeneratorParameters>` says what `false` said.
2. The generator parameters `token-validation` and `token-ignore=name` are gone, and are `SQLSRC109`.  `-- token-ignore: name`, inside a query, replaces the second; nothing replaces the first, since omitting `no-token-validation` says it.
3. A query's `-- generator:` lines replace the preamble's instead of adding to them.  A query that wants what the preamble gives restates it.
4. `{{a:b}}` is the token `a` with the default `b`, where it was literal text.  And a line comment that starts its line with one of the new marker words and a colon is a marker, where it was a comment.

## What a user writes

### Markers

The rule is phase 0's: a line comment that starts its line and reads `-- word: rest`, where the word is one SqlSource knows, is a marker.  Words are matched ignoring case, and every marker line is removed from the generated SQL.

| Marker | Preamble | Query | Value |
|----|----|----|----|
| `name` | | starts one | `Name`, or `Name -> shape` |
| `summary` | no | yes | Text, as today |
| `dialect` | yes | no | As today |
| `generator` | yes | yes | Generator parameters, below |
| `param` | no | yes | `@name [type] [null \| not null]` |
| `token` | no | yes | Exactly one `{{name:default}}` |
| `token-ignore` | no | yes | One token name |
| `database` | yes | yes | A database name |
| `output` | yes | yes | `sql`, `models` or `codegen` |
| `input-model-suffix`, `output-model-suffix` | yes | no | A suffix |
| `model-namespace` | yes | no | A namespace |
| `input-model-type`, `output-model-type` | yes | yes | `record`, `sealed record`, `class` or `sealed class` |
| `input-model`, `output-model` | no | yes | A type name |
| `collection-type` | yes | yes | A member of `GeneratorCollectionType` |

Rules that hold for every marker:

- **Scope.**  A marker in a scope the table does not allow is `SQLSRC116`, and is not applied.  `-- summary:` in the preamble stays `SQLSRC107` and `-- dialect:` outside the header stays `SQLSRC115`.  In a file with no `-- name:` marker every marker is allowed.
- **Place.**  A marker comes before the SQL it describes.  One after the last SQL of its query is `SQLSRC108`, as today, and is not applied.
- **A value that is not valid** is `SQLSRC111`, at the value, or at the whole marker when the value is empty.  The argument is the word, a colon, a space and the trimmed value, as for `-- dialect:` today.
- **Repeats.**  A marker with one value may be written twice in one scope with the same value.  Two different values are `SQLSRC112` at the second, and the first stands.  Values are compared as they are read: `-- output: CodeGen` and `-- output: codegen` are the same.
- **Precedence.**  A query's marker wins over the preamble's.

The values:

- **Shape.**  `-> ` and one of `many`, `one`, `one-optional`, `none` and `rowcount`, after the name.  The name is the text before `->`, trimmed, and is checked as today.  An arrow with nothing after it, or with anything else, is `SQLSRC111` at the text after the name, with the argument `name: <value>`.
- **A database name** is one word of letters, digits, `-`, `_` and `.`.  The rule is narrow on purpose: the tool makes an environment variable's name from it, and widening it later breaks nobody.
- **A suffix** is one or more characters that can follow the first of a C# identifier.
- **A namespace** is one or more identifiers separated by periods, none of them a reserved keyword.
- **A type name**, for `-- input-model:` and `-- output-model:`, is an identifier that is not a reserved keyword, or a namespace, a period and such an identifier.  `-- input-model:` on a query whose parameter list is empty is `SQLSRC119`.
- **A value from a fixed list**, for `output`, the two model types, the collection type and the shape, is matched ignoring case and ignoring hyphens and spaces inside it.

### Parameters

A parameter is `@` and a name, found by the lexer under the file's dialect.

- The `@` is followed by a letter, a digit or `_`, and is not preceded by `@`, a letter, a digit or `_`.  The name runs over letters, digits and `_`; a letter is any Unicode letter.
- A `@` inside a string, a quoted identifier, a comment or a hint is not a parameter.
- `@>`, `<@`, `@@`, `@?`, `@@ROWCOUNT` and the `@` of `user@host` are therefore not parameters.  `@x` written for PostgreSQL's absolute value of `x` is one, and so is a T-SQL local variable or a MySQL user variable; `docs/tech-debt/TD-0004` records each.
- Names are compared ignoring case.  A parameter keeps the spelling of its first appearance.
- The prefix is `@` under every dialect.  It is a value of the dialect's rules, so that another prefix is a rule and not a rewrite.

The parameter stays in the SQL as written.  Nothing is generated from the list in this phase.

### The `-- param:` marker

```
-- param: @name [type] [null | not null]
```

- The name is written with the dialect's prefix, and is followed by white space or the end of the line.
- The value ends with `null`, with `not null`, or with neither.  `null` says the parameter is nullable; `not null` says it is not, which is also what saying nothing means.  The three are kept apart: the hash and, later, the sidecar record what was written.
- The type is the text between the name and those words, trimmed, as written.  It is not checked: `decimal(18,2)` and `double precision` need no quoting, and the database resolves the name in phase 2.
- One marker declares one parameter.  A second marker for the same parameter, ignoring case, is fine when it gives the same type and nullability, and `SQLSRC112` when it does not.
- A marker that has no prefix, no name, or a name that runs into other text is `SQLSRC111`.

### The parameter list of a query

1. The parameters of the query's static SQL, in order of first appearance.  Static means outside every token: a parameter inside `{{name:default}}` is part of a sample, not of the SQL.
2. Then the parameters that a `-- param:` marker declares and the static SQL does not hold, in marker order.

Two errors come from the rule:

- `SQLSRC117`: a `-- param:` marker without a type for a parameter that the static SQL does not hold.  Such a parameter reaches the query only through a token, so nothing else can type it.  At the marker.
- `SQLSRC118`: a parameter that appears in the resolved default of one of the query's tokens, and in neither the static SQL nor a marker.  At the parameter, in the SQL or in the `-- token:` marker that holds the default.

A marker that names a parameter with a type, which neither the SQL nor any default holds, is not an error: a fragment passed at run time may use it.

### Tokens

- `{{name:default}}` is the token `name`.  The name is as today, with blanks allowed around it; the default is everything after the first `:` up to the first `}}`, trimmed, and may be empty or span lines.  `{{cast:x::int}}` has the default `x::int`.
- The generated method is unchanged: one `string` parameter for each token name, and the SQL with the argument in the token's place.  A default reaches no generated code.
- A token whose name a `-- token-ignore:` marker of its query lists stays literal text, default included.
- `-- token: {{name:default}}`, inside a query, gives a default by marker, for a token that the query writes several times or whose sample is long.  Its value is exactly one token with a default: text outside the braces, or a token with no colon, is `SQLSRC111`.  A name that is a reserved keyword is `SQLSRC114`.  The default is taken as written, and is lexed alone under the file's dialect to find its parameters; a quote or a block comment that does not close inside it is `SQLSRC111`.
- `-- token-ignore: name` takes one identifier.  Several markers accumulate.

A token's **resolved default** is the one its query gives it, inline or by marker, or none.

- Two defaults for one token that differ are `SQLSRC112` at the second in file order: two inline occurrences, two markers, or one of each.  Occurrences without a default beside one with a default are fine.
- A marker for a token that its query does not hold is not an error.

### Generator parameters

A `-- generator:` line holds one or more of:

| Parameter | Effect in this phase |
|----|----|
| `keep-comments` | Comments and blank lines stay in the SQL |
| `no-token-validation` | The query's method does not check its arguments |
| `sort-input`, `sort-output`, `no-table-models`, `async-method-suffix` | None yet |
| `default` | The empty list.  Alone only |

- The lines of one scope add up to that scope's list.  A query that has a list uses it whole; a query that has none uses the preamble's.
- `default` beside another parameter in one scope is `SQLSRC112`, at whichever comes second.
- No parameter takes a value: `keep-comments=1` is `SQLSRC111`.  Anything else is `SQLSRC109`, the old `token-validation` and `token-ignore=name` included.
- A marker with no parameter is `SQLSRC110`, as today.
- Outside a marker, `default` beside another word makes the whole value not valid: it is reported whole and the level gives no list.

The same list is the value of the `SqlSourceGeneratorParameters` property, of the metadata of that name, and of the attribute's `Parameters`.  The list in effect for a query, for a type that claims its file, is the one from the first level that gives one: the query's markers, the preamble's, the attribute, the metadata, the property; and the empty list when none does.

### The attribute

```csharp
internal sealed class SqlSourceGenerateAttribute : Attribute
{
    public string Path { get; set; }
    public SqlLocation SqlLocation { get; set; }
    public GeneratorOutput Output { get; set; }
    public string InputModelSuffix { get; set; }
    public string OutputModelSuffix { get; set; }
    public string ModelNamespace { get; set; }
    public GeneratorModelType InputModelType { get; set; }
    public GeneratorModelType OutputModelType { get; set; }
    public MethodLocation MethodLocation { get; set; }
    public GeneratorCollectionType CollectionType { get; set; }
    public string Parameters { get; set; }
}
```

| Enum | Members, in order from zero |
|----|----|
| `GeneratorOutput` | `Sql`, `Models`, `CodeGen` |
| `GeneratorModelType` | `Record`, `SealedRecord`, `Class`, `SealedClass` |
| `GeneratorCollectionType` | `IEnumerable`, `ICollection`, `IReadOnlyCollection`, `IList`, `IReadOnlyList`, `Array`, `List`, `ImmutableArray`, `ImmutableList`, `IImmutableList` |
| `MethodLocation` | `ExtensionClass`, `Public`, `Internal`, `Private` |

- A property that is not written is not set: the generator reads the named arguments, so no enum needs its default at zero, and each property's documentation says what the default is.
- A string that is empty or white space is not set, as `Path` is today.
- A value that is not valid is `SQLSRC006`, at the attribute: a number that is no member of its enum, a suffix or a namespace that the marker would reject, and each word of `Parameters` that is not a generator parameter.  The value is then not set, and the other words of a list still apply.

### MSBuild

| Property | Metadata of an `AdditionalFiles` item | The generator reads it |
|----|----|----|
| `SqlSourceDialect` | yes | yes, as today |
| `SqlSourceDatabase` | yes | no |
| `SqlSourceOutput` | yes | yes |
| `SqlSourceInputModelSuffix`, `SqlSourceOutputModelSuffix` | yes | yes |
| `SqlSourceModelNamespace` | yes | yes |
| `SqlSourceInputModelType`, `SqlSourceOutputModelType` | yes | yes |
| `SqlSourceCollectionType` | yes | yes |
| `SqlSourceGeneratorParameters` | yes | yes |

- Each value is what the marker of the same setting takes.  An empty value is not set.
- A value that is not valid is `SQLSRC014`, with no position, once for each distinct setting and value, and for the metadata only when a type claims the file.  The value is then not set; in a list of generator parameters the other words still apply.
- The metadata wins over the property, whole.

### Diagnostics

| Id | Change | Title | Message | Argument |
|----|----|----|----|----|
| `SQLSRC006` | Generalised | Attribute value is not valid | `'{0}' is not a valid value of {1}` | The value; the attribute's property |
| `SQLSRC010` | Removed | | | |
| `SQLSRC014` | New | MSBuild setting is not valid | `'{0}' is not a valid value of {1}` | The value; the MSBuild name |
| `SQLSRC108` | Reworded | Marker has no SQL after it | `A marker comes before the SQL it describes, and no SQL follows this one in its query` | |
| `SQLSRC111` | Wider use | unchanged | unchanged | The generator parameter as written, or `word: value` |
| `SQLSRC112` | Wider use | unchanged | unchanged | The second one, as for `SQLSRC111` |
| `SQLSRC116` | New | Marker is not allowed here | `The '-- {0}:' marker is allowed only {1}` | The word; `inside a query` or `before the file's first query` |
| `SQLSRC117` | New | Parameter has no type | `'{0}' is not in the SQL of its query, so its '-- param:' marker must give its type` | The parameter, with its prefix |
| `SQLSRC118` | New | Parameter is not declared | `'{0}' appears only in the default of a token.  Declare it with a '-- param:' marker that gives its type` | The parameter, with its prefix |
| `SQLSRC119` | New | Query has no parameters | `The query has no parameters, so '-- input-model:' names nothing` | |

`SqlParseErrorKind` gains `MarkerNotAllowedHere`, `MissingParameterType`, `UndeclaredParameter` and `InputModelWithoutParameters` at its end, in that order, since the ids follow it.  `SqlDiagnostics.InvalidSqlLocation` becomes `InvalidAttributeValue` and `InvalidTokenValidation` goes.  `docs/diagnostics.md` and `AnalyzerReleases.Unshipped.md` follow in the commit that changes each.

## Code

### A new folder, `Settings/`

Plain data and the readers of values, with no file access, no symbols and no pipeline types.  `Parsing/` and `Generation/` both use it, and the tool will.

- The generator's own forms of the emitted enums, as `MemberPlacement` is of `SqlLocation`: `OutputKind`, `ModelKind`, `CollectionKind` and `MethodPlacement`, each with the numbers of the enum it stands for, and `ResultShape`.  A test compares each with the text `AttributeSource` emits.
- `GeneratorParameters`, a flags enum of the six switches, and `GeneratorParameterList`, the one place that knows their names: it reads a list from a span and names each word it does not know.  The marker, the property, the metadata and the attribute all read through it.
- `SettingValue`: the readers of a value from a fixed list, of a suffix, a namespace, a type name and a database name.  Each reads a span and allocates nothing unless it returns a string.
- `SettingsLevel`, a record whose every member is nullable: `Output`, `Database`, `InputModelSuffix`, `OutputModelSuffix`, `ModelNamespace`, `InputModelType`, `OutputModelType`, `CollectionType` and `Parameters`.  A level sets the members its source has and leaves the rest null.  `SettingsLevel.Over(other)` gives a level with this one's values where it has them and the other's elsewhere, which is how the parser puts a query's markers over the preamble's.
- `QuerySettings`, a `readonly record struct` with no nullable member but `ModelNamespace`, and `QuerySettings.Resolve(markers, attribute, metadata, property)`: the first level that has a member gives it, and the default otherwise.  The defaults: `CodeGen`, `Params`, `Dto`, the type's namespace, `SealedRecord` twice, `Array`, the empty list.

### `Parsing/`

- **`SqlLexer`**: `SqlLexemeKind.Parameter`, the prefix and the name.  `SqlDialectRules.ParameterPrefix` is `@` in every rule set and is one of the starter characters, so plain text is still skipped in one search.  A `@` that is not a parameter is plain text, as any other character that starts nothing.  A parameter ends a string that was waiting for a continuation, as plain text does.  `SqlLexeme.GetContentSpan` counts a parameter as content.
- **`SqlTextBuilder`** copies a parameter as text, and `SqlBlockText` gains the offsets in the built SQL where each parameter starts and ends.
- **`TokenScanner`** reads the default, and its result gains each token's extent in the scanned SQL and its default.  The reader of one token is shared with the `-- token:` marker.
- **`SqlMarkerReader`** knows the new words.  A word that is the start of another, `token:` and `token-ignore:`, is no problem: the colon is part of what is matched.
- **`SqlGeneratorParameterScope`** holds a scope's `GeneratorParameters?` and reads through `GeneratorParameterList`.  `TokenValidation`, `IgnoredTokens` and `KeepComments` go.
- **`SqlMarkerScope`**, new: what the markers of one scope give, the preamble or one query.  It holds a `SettingsLevel` being built, and for a query its token defaults, declarations, ignored tokens, model names and summary.  `SqlFileParser.ReadPreamble` and `ReadBlock` hand it each marker with whether the marker is allowed there; it reports `SQLSRC111`, `SQLSRC112` and `SQLSRC116`.  This is what keeps `SqlFileParser` from growing a branch for each word.
- **`SqlFileParser`** takes a second input, whether comments may be wanted, and its result gains the file's dialect as it is after the header.  `ReadBlock` builds the stripped SQL, scans it, sorts each parameter into static or in-a-default by the tokens' extents, applies the parameter list rule, and builds the kept SQL when it is wanted.  Errors with one kind, one span and the same arguments are reported once.
- **`SqlBlock`** becomes:

  | Member | Holds |
  |----|----|
  | `Name`, `NameSpan`, `Summary` | As today |
  | `Shape` | `ResultShape?`, null when the name marker gives none |
  | `Segments` | The comment-stripped SQL, as literal text and tokens.  Never empty |
  | `KeptSegments` | The SQL with comments, or null when it was not built |
  | `Tokens` | `SqlToken(Name, Default)` for each token of `Segments`, once, in order of first appearance.  `Default` is the resolved default, null when there is none |
  | `Parameters` | `SqlQueryParameter(Name, Type, Nullable, IsDeclared)` in the order of the rule.  `Type` is null when no marker gives one; `Nullable` is `bool?`, null when the marker says neither |
  | `Markers` | The `SettingsLevel` of the query's markers over the preamble's |
  | `InputModelName`, `OutputModelName` | The query's own, or null |

  `KeepComments` and `TokenValidation` go.

### Two forms of a query's SQL

`Segments` is always built.  `KeptSegments` is built when:

- the query's own list of generator parameters has `keep-comments`; or
- no marker gives the query a list, and the file's input says comments may be wanted.

A query whose markers give a list without `keep-comments` never has the kept form: a marker is the most specific level, so nothing can ask for it.

The emitter takes `KeptSegments` when the resolved list has `keep-comments`, and `Segments` otherwise.  The member is built from the form that is emitted: a token that stands only inside a comment is a parameter of the method in the kept form and not in the other, as it is today.

`Tokens`, `Parameters` and the hash come from `Segments`.  The token scan of the kept form adds only an error the stripped form's scan did not report at the same place.

### The hash

`SqlQueryHash.Compute(SqlDialect dialect, EquatableArray<SqlSegment> segments, EquatableArray<SqlToken> tokens, EquatableArray<SqlQueryParameter> parameters)` in `Parsing/`: SHA-256, lower-case hex, of the UTF-8 bytes of

```
<engine> LF <sql> LF <declaration> LF <declaration> LF ...
```

- `engine` is `SqlDialectName.Canonical(dialect)`, the first name the dialect has in `SqlDialectName`: `postgres`, `mssql`.
- `sql` is `segments` in order: a literal as it is, and a token as `{{name:default}}` with its resolved default, `{{name:}}` for an empty one, and `{{name}}` when it has none.  `segments` is the stripped form, so `keep-comments` changes nothing, and its line endings are `\n` already.
- A declaration is one parameter that a `-- param:` marker declares, in marker order: the name without its prefix as the marker writes it, then a space and the type when there is one, then ` null` or ` not null` when the marker says so.  A query with no declarations ends after the line feed that follows the SQL.

It is a pure function, and nothing in the pipeline calls it in this phase, so a parse pays nothing for it.  Phase 2 and phase 5 call it, and decide where its result is kept.  `SHA256` is used through `System.Security.Cryptography`, which `netstandard2.0` has; the plan confirms first that the analyzer rules allow it.

### `Generation/` and the pipeline

- **`AttributeSource`**: the four enums and the nine properties, in C# 7.3.  `AttributeSource.GeneratedTypes` lists the metadata names of the six generated types, and `AttributeConflictSuppressor` suppresses for exactly those.
- **`TargetTypeReader`** reads the attribute into `TargetType.Settings`, a `SettingsLevel`, and `TargetType.MethodPlacement`, beside `Placement`.
- **`ProjectSettings`**: the properties, read once from the global options into a `SettingsLevel` and a list of the values that are not valid.  It replaces `TokenValidationSetting`.
- **`FileSettings`**: one file's metadata, read the same way, in a step that is not an input of the parse.  The files that have any are collected, sorted by path, and `SelectFiles` finds a type's by `SqlPath.IndexOf`.  `TypeQueries` carries them beside the type's files.
- **`FileDialect` becomes `FileParseInput`**: the file, its dialect, its invalid dialect, and `CommentsWanted`.  That is true when the list of the file's metadata has `keep-comments`, or the metadata gives no list and the property's does, or the file is in `CommentPaths`.
- **`CommentPaths`**, a step beside `ClaimedPaths`: the files of the types whose `Parameters` has `keep-comments`.  Almost always empty, so it almost never changes.
- **`SqlFileReader`** passes `CommentsWanted` to the parser and copies the block's new members to `SqlQuery`.  `ParsedSqlFile` gains the file's dialect.
- **`TypeEmitter.Emit(TypeQueries, ProjectSettings)`** resolves `QuerySettings` for each query and reads two things from it: `keep-comments` picks the form, and `no-token-validation` decides whether the method checks its arguments.
- **Reporting.**  `SQLSRC014` for the properties comes from an output step over `ProjectSettings`, and for the metadata from one over the collected `FileSettings` and `ClaimedPaths`, each value once in ordinal order.
- **`TrackingNames`** follows: `FileParseInput` for `FileDialect`, `ProjectSettings` for `TokenValidation`, and `FileSettings`, `FilesSettings` and `CommentPaths` new.

What an edit costs:

| Edit | Parses | Runs `TypeEmitter` for |
|----|----|----|
| A property, with no change to whether its list has `keep-comments` | Nothing | Every type |
| A file's metadata, the same | Nothing | The types that claim the file |
| An attribute, the same | Nothing | That type |
| `keep-comments` added or removed at one of those levels | The files it covers that have a query without a list of its own | The types that claim them |

### `build/`

- **`SqlSource.props`** lists a `CompilerVisibleProperty` for each property the generator reads, and a `CompilerVisibleItemMetadata` for each metadata it reads, on one item type.  `SqlSourceDialectFile` becomes `SqlSourceSettingsFile`: the `AdditionalFiles` items that have any of that metadata, so that the file the compiler reads still has a section only for those.
- **`SqlSource.targets`**: `SqlSourceTrimProperties` trims every property of the table, `SqlSourceDatabase` included.  `SqlSourceTrimDialectOfFiles` becomes `SqlSourceTrimMetadataOfFiles` and trims every metadata of the table, batching on their combination so that it is one target; if MSBuild does not allow a condition where that needs one, the plan falls back to one target for each metadata.  `SqlSourceCollectDialectFiles` becomes `SqlSourceCollectSettingsFiles`.
- `DialectSetting.MetadataName`, `tests/SqlSource.Tests/Package/BuildFileTests.cs`, `TD-0016` and `src/SqlSource/AGENTS.md` follow the renames.
- **`SqlSource.csproj`** gains `InternalsVisibleTo` for `SqlSource.Tool`.

## Order of work

One pull request, one commit for each step.  Each leaves `./pre-commit-validation.sh` passing, and each brings its tests, its diagnostics' four places, and the parts of `README.md`, `docs/diagnostics.md` and `src/SqlSource/AGENTS.md` it makes wrong.

1. This spec, and the epic outline: the row links here and says In progress, and the four owner decisions are recorded.  The one line of the sidecar format design that reserves `nullable: false`.
2. The `Parameter` lexeme, the prefix in the rules, and the static parameter list on `SqlBlock` and `SqlQuery`.
3. Token defaults: `{{name:default}}`, the `-- token:` marker, `Tokens`, and static against in-a-default parameters; `SQLSRC116`, which this is the first marker to need.
4. `-- token-ignore:`, and `token-ignore=` dropped.
5. `-- param:`, the parameter list rule, `SQLSRC117` and `SQLSRC118`.
6. The hash.
7. The `-> shape` suffix.
8. `Settings/` with `GeneratorParameters`, and the markers' new vocabulary: `default`, the four new switches, `token-validation` dropped, a query's list replacing the preamble's.  `SqlSourceTokenValidation` still decides for a query without a list.
9. `SettingsLevel`, `QuerySettings` and both stages, with the generator parameters as their one member: the property, the metadata and the attribute's `Parameters`; the two forms; `SqlSourceTokenValidation` and `SQLSRC010` removed; `SQLSRC006` generalised and `SQLSRC014`; the targets generalised and renamed.
10. `-- database:` and `-- output:`, `SqlSourceDatabase` and `SqlSourceOutput`, `GeneratorOutput` and the attribute's `Output`.
11. The model settings and the collection type at every level, the attribute's `MethodLocation`, their enums, `-- input-model:` and `-- output-model:` with `SQLSRC119`, and the suppressor's list.
12. `InternalsVisibleTo`, and the epic's row set to Done.

`SqlMarkerScope` arrives with the first step that needs it, step 3, and each later step adds its words.

## Testing

| Where | Cases |
|----|----|
| `SqlLexerTests` | The `@name` rule under every dialect: in a string, a quoted identifier, a line comment, a block comment and a hint; `@>`, `<@`, `@@`, `@?`, `@@ROWCOUNT`, `a@b`, `@1`, `@_a`, a non-ASCII name; at the start and at the end of the text; between two parts of a continued string.  Lexemes still cover the text |
| `SqlDialectRulesTests` | The prefix is a cell of the table, for every dialect and option set |
| `TokenScannerTests` | A default, an empty one, one with `:` and one over two lines; `{{a:` with no close is literal; an ignored name keeps its default as text |
| `SqlMarkerReaderTests` | Each new word, in any case; a word that only starts like one is not a marker |
| `SqlFileParserTests` | For each marker: its value, each scope, the same value twice, two values, after the last SQL, with the span and argument of each error.  The parameter list in each order the rule gives; `SQLSRC117`; `SQLSRC118` in the SQL and in a marker; `null`, `not null` and neither.  A token's default inline and by marker, and each conflict.  A query's list replacing the preamble's; `default`.  The file with no `-- name:`.  The kept form built and not built, for each of its conditions |
| `SqlQueryHashTests` | A known input gives a known hash.  The same for `\r\n` and `\n`, with and without `keep-comments`, and with a comment edited.  Different for a default, an empty default against none, a declaration, a type, `null` against `not null` against neither, a token's name, a parameter's case, and the engine |
| `Settings/` tests | Each reader's valid and invalid values, the list rule included.  `Resolve` for each member from each level, and its default.  The own enums against the emitted text |
| `Generator/` tests, on both Roslyn versions | `keep-comments` and `no-token-validation` from the property, the metadata, the attribute, the preamble and the query, each winning over the one below it, and `default` undoing each.  Two types that claim one file with different `Parameters` get different SQL.  `SQLSRC006` for each attribute property and `SQLSRC014` for each property and metadata, once for a value.  The attribute file compiles as C# 7.3 without warnings, and the suppressor covers each generated type and nothing else |
| `CachingTests` | The table under What an edit costs, row by row |
| `SqlFileParserAllocationTests` | The file loses its `-- generator: token-ignore=raw` line, which is no longer valid in a preamble.  The budget stays, and the pull request states the bytes for each character before and after.  A parse that asks for the kept form is not what the budget measures |
| `BuildFileTests` | The properties and metadata the props list, the renamed targets, and that every name starts with `SqlSource` |
| `EndToEnd` and `tools/package-install` | `SqlSourceGeneratorParameters` in place of `SqlSourceTokenValidation`, written over several lines so that the trim is proven; a file with `keep-comments` by metadata; a type with `Parameters`; a query with a parameter, a default, a `-- param:` and a shape, which builds and gives the SQL it gave without them |

If a parse of the allocation test's file comes out over its budget, the work stops and the owner decides: the budget is not raised to make this pass.

## Documentation

- `README.md`: the marker table and its rules; parameters, briefly, since nothing is generated from them yet; tokens with defaults, `-- token:` and `-- token-ignore:`, in place of the two bullets on braces and on `token-ignore=`; generator parameters with their levels and the replace rule; validation's three switches rewritten for the five levels; the attribute's properties and the note on friend assemblies for six types; under MSBuild, `SqlSourceGeneratorParameters` in place of Token validation.  One table, Settings for models and methods, lists what is accepted and has no effect yet, and says so.  Its links stay absolute.
- `docs/diagnostics.md`: as under Diagnostics.
- `src/SqlSource/AGENTS.md`: `Settings/`; the two stages and what may reach the parse; the two forms; the parameter lexeme; the renamed types, steps and targets.
- `CONTRIBUTING.md` and `docs/publishing.md`: nothing in them changes.
- `docs/tech-debt`: `TD-0004` gains what the parameter rule misreads; `TD-0006` names six types; `TD-0016` names the renamed target.  A new entry if the combined trim target has to become one target for each metadata.
- `docs/deferred`: nothing is deferred.
- The epic outline: in step 1 and step 12, as above.

## Version

The repository has no `v*` tag, so `VersionPrefix` stays `0.1.0`.
