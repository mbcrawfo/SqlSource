# SQL tokens - design

Date: 2026-10-06

Phase 3 of the [SQL queries epic](2026-10-05-sql-queries-epic-design.md).

## Goal

Make queries with tokens usable.  A query that contains a `{{token}}` becomes a static method that takes one `string` for each token and returns the SQL with the tokens replaced.  A developer writes the token in the `.sql` file and calls the method; nothing else changes.  After this phase every query gets a member, and the package can be released.

```sql
-- name: ListFrom
SELECT id, name FROM {{table}} ORDER BY name;
```

```csharp
[SqlQueries]
public partial class UserRepository
{
    public Task<IEnumerable<User>> List() => connection.QueryAsync<User>(Sql.ListFrom("users"));
}
```

Token replacement is string concatenation into SQL.  It is for trusted fragments only.

## Decisions

The epic's decisions about tokens, the method's signature, `string.Create`, validation, its precedence and the generated documentation apply.  The epic recommended seven items for this phase.  The owner accepted five as written and changed two; the rest of this table was settled in this design.

| Decision | Choice | Reason |
|----|----|----|
| `string.Create` overload | `string.Create<TState>(int, TState, SpanAction<char, TState>)` with a `static` lambda.  Accepted as recommended. | It allocates exactly the final string |
| Copying segments | Each literal and each parameter is copied into the span in order.  Accepted as recommended. | Direct, and no formatting is involved |
| Names of generated lambda parameters | `span` and `state`, with a number added until the name is not a token name of that query.  Accepted as recommended. | Any identifier can be a token name |
| Value of `SqlSourceTokenValidation` | `true` or `false`, trimmed and compared ignoring case.  Empty or missing means the default.  Anything else is an error.  Accepted as recommended. | A typo should not silently change behaviour |
| Where the property is declared | A `CompilerVisibleProperty` item in `build/SqlSource.props`.  Accepted as recommended. | The file already exists and every consumer imports it |
| A null argument when validation is off | **Changed.**  Nothing is checked.  The method has no validation code at all, and a null argument fails with a `NullReferenceException` when the method reads its length.  The epic recommended `ArgumentNullException.ThrowIfNull`. | The owner's decision: off means off |
| The language version of a type's file | **Changed.**  C# 12 or later is a documented requirement and is not checked.  The epic's note said the `static` lambda needs C# 9, and phase 2 kept a type's file to C# 8. | The owner's decision.  A project that targets .NET 8 or later has C# 12 or later unless it lowers `LangVersion`.  `docs/tech-debt/TD-0011` records that a lower version is not diagnosed. |
| The nullable context of a type's file | `#nullable enable`, always, as in phase 2.  It does not follow the project's `Nullable` setting. | The compiler ignores that setting in a generated file, so the directive is the only way to annotate one.  A project with nullable on is warned about passing null; a project with it off sees no annotation and gets no warning. |
| Where the project setting enters the pipeline | After a type's queries are selected, as a second input of emission | A change to the property emits each type again and parses nothing again.  Parsing a `.sql` file keeps one input. |
| An invalid property value | `SQLSRC010`, with no location, reported once for the compilation.  Generation continues with validation on. | A generator cannot see where an MSBuild property was set.  Emitting nothing would bury the one real error under a missing-member error for every query.  A type error emits nothing because it is about one type; this is about the project. |
| Reading the state tuple | By position, `Item1` to `ItemN`, never by element name | `Item2`, `Rest` and `ToString` are valid token names and are not valid element names |
| A query with one token | The state is the `string` itself | A tuple of one element has no literal syntax |
| How framework members are written | `global::System.ArgumentException`, and the keyword `string` | `System` and `String` are valid token names, and a parameter of that name would hide the namespace or the type |
| Where the method text is written | A new `MethodWriter`, beside `TypeEmitter` | `TypeEmitter` keeps placement and the checks across a type's files.  The method's text is the larger and the more intricate part. |

## Out of scope

- Parameter types other than `string`, and optional parameters.
- Making a value safe.  Validation checks that a value is present.
- Checking the consumer's language version.  See `TD-0011`.
- Installing the packed package into a project as a test.  `docs/tech-debt/TD-0008-package-is-not-installed-in-a-test.md` stays open; its trigger, the first release, is the next step after this phase and not part of it.
- A change to the parser.  It already produces the segments and the validation flag.

## Structure

| Unit | Folder | Change | Responsibility |
|----|----|----|----|
| `SqlQuery` | `Generation/` | Changed | Carries a block's segments and its validation directive in place of one SQL string |
| `SqlFileReader` | `Generation/` | Changed | Copies every block; the skip and its `TODO` are removed |
| `TokenValidationSetting` | `Generation/` | New | The project's `SqlSourceTokenValidation` property, read into a value-equal record |
| `MethodWriter` | `Generation/` | New | Writes one method: its parameters, validation, length and copy steps |
| `XmlDocWriter` | `Generation/` | Changed | Gains the `<param>` line |
| `TypeEmitter` | `Generation/` | Changed | Writes a constant or calls `MethodWriter`, by whether the query has a token |
| `TrackingNames` | `Generation/` | Changed | Names the new step |
| `SqlDiagnostics` | `Diagnostics/` | Changed | `SQLSRC010` |
| `SqlSourceGenerator` | - | Changed | Wires the new step and its diagnostic |
| `build/SqlSource.props` | - | Changed | Makes the property visible to the compiler |

### Model

```csharp
internal sealed record SqlQuery(
    string Name,
    LocationInfo NameLocation,
    string? Summary,
    EquatableArray<SqlSegment> Segments,
    bool? TokenValidation
);

internal sealed record TokenValidationSetting(bool Validate, string? InvalidValue);
```

- `Segments` is the parser's value, unchanged.  It is never empty, a literal segment is never empty, and two literal segments are never adjacent.  A query with no token segment has exactly one literal segment and is a constant.
- `TokenValidation` is the parser's value: true or false when a directive of the block or of the file's preamble applies, null when neither does.
- `TokenValidationSetting.Validate` is what the property asks for, and true when the value is missing, empty or invalid.  `InvalidValue` is the value as written when it is not valid, and null otherwise.
- Both records are value-equal, as everything the pipeline caches must be.

## Pipeline

One step is added and one is changed.

```text
AnalyzerConfigOptionsProvider ──> TokenValidationSetting ──┬──> SQLSRC010 (output)
                                                           │
... ──> TypeQueries ───────────────────────────────────────┴──> TypeOutput ──> source and diagnostics (output)
```

- **`TokenValidationSetting`:** `context.AnalyzerConfigOptionsProvider.Select(...)` reads `build_property.SqlSourceTokenValidation` from `GlobalOptions` and returns the record.  The provider changes whenever any option of the project does; the record compares equal unless this property changed, so the steps after it stay cached.
- **`TypeOutput`:** the `TypeQueries` step is combined with the setting, and `TypeEmitter.Emit(queries, setting.Validate)` runs on the pair.  `TypeQueries` itself does not depend on the setting.
- **The diagnostic:** a `RegisterSourceOutput` on the setting reports `SQLSRC010` when `InvalidValue` is not null.  It creates the `Diagnostic` there with `Location.None`.  `DiagnosticInfo` is not used, because it always carries a position in a file.  The diagnostic does not depend on any type: a project with an invalid value and no attributed type still gets it.
- **Parsing is untouched.**  `SqlFileReader.Read` has the same inputs as before.

### Reading the property

| Value, after trimming white space | `Validate` | `InvalidValue` |
|----|----|----|
| Missing, or empty | true | null |
| `true`, in any case | true | null |
| `false`, in any case | false | null |
| Anything else | true | The value as written, untrimmed |

### Which queries validate

A method validates when `query.TokenValidation ?? setting.Validate` is true.  The parser has already put a block's own directive above the preamble's, so this one expression gives the epic's whole precedence: block directive, preamble directive, property, default.

## Generated code

A query with at least one token segment is a method.  A query with none is a constant, as in phase 2.  Both sit in the same place and in the same order: the `Sql` class in `Nested` mode, the type in `Direct` mode, by file and then by position in the file.

For this query in `Users.sql`:

```sql
-- name: ListFrom
SELECT id FROM {{table}} WHERE {{filter}} ORDER BY {{table}}.id;
```

the member is:

```csharp
/// <summary>
/// The <c>ListFrom</c> query from <c>Users.sql</c>.
/// </summary>
/// <remarks>
/// <code>
/// SELECT id FROM {{table}} WHERE {{filter}} ORDER BY {{table}}.id;
/// </code>
/// </remarks>
/// <param name="table">The text that replaces <c>{{table}}</c>.</param>
/// <param name="filter">The text that replaces <c>{{filter}}</c>.</param>
public static string ListFrom(string table, string filter)
{
    global::System.ArgumentException.ThrowIfNullOrWhiteSpace(table);
    global::System.ArgumentException.ThrowIfNullOrWhiteSpace(filter);
    return string.Create(
        36 + table.Length * 2 + filter.Length,
        (table, filter),
        static (span, state) =>
        {
            "SELECT id FROM ".CopyTo(span);
            span = span.Slice(15);
            state.Item1.CopyTo(span);
            span = span.Slice(state.Item1.Length);
            " WHERE ".CopyTo(span);
            span = span.Slice(7);
            state.Item2.CopyTo(span);
            span = span.Slice(state.Item2.Length);
            " ORDER BY ".CopyTo(span);
            span = span.Slice(10);
            state.Item1.CopyTo(span);
            span = span.Slice(state.Item1.Length);
            ".id;".CopyTo(span);
        }
    );
}
```

**Signature**

- `public static string`, named after the query.
- One `string` parameter for each distinct token name, compared ordinally, in the order of first appearance.  The parameter has the token's name exactly as the parser gives it.  The parser has rejected reserved keywords, so no name needs an `@`.

**Validation**

- When the method validates, its body starts with one `global::System.ArgumentException.ThrowIfNullOrWhiteSpace(name);` for each parameter, in parameter order.  The parameter's name reaches the exception through the method's caller-expression default, which the C# 12 requirement makes safe to rely on.
- When it does not, those lines are absent and the method is otherwise identical.

**Length**

- The first argument of `string.Create` is the total length of the literal segments, written as one number, then `+ name.Length` for each parameter, in parameter order, with `* n` when the token appears `n` times and `n` is above one.
- When the query has no literal segment the number is left out.
- The arithmetic is plain.  A total above the largest `int` fails inside `string.Create` or the first copy, as any string that large does.

**State**

- With one parameter the state is the parameter, and the lambda reads it as `state`.
- With two or more the state is a tuple of the parameters in parameter order, and the lambda reads them as `state.Item1` to `state.ItemN`.  Above seven the compiler nests the tuple and the same member names still work.
- The lambda is `static`: it captures nothing, so it is allocated once for the program and not once for each call.

**Copy steps**

- One step for each segment, in order.  A literal is `"text".CopyTo(span);`, with the text written by `SymbolDisplay.FormatLiteral`, and is followed by `span = span.Slice(length);` with the length as a number.  A token is `value.CopyTo(span);` followed by `span = span.Slice(value.Length);`, where `value` is `state` or `state.ItemN`.
- The step for the last segment has no `Slice` after it.
- `string.CopyTo(Span<char>)` exists from .NET 6, below the .NET 8 floor.

**Names**

- The lambda's parameters are `span` and `state`.  When a token of the query has one of those names, a number is added, starting at 1, until the name is not a token name: `span1`, `state1`.  The two names are chosen independently and cannot collide with each other.
- Nothing else in the method is an identifier that a token name can hide: `string` is a keyword, the exception type is qualified with `global::`, and `CopyTo`, `Slice`, `Length` and `ItemN` are member accesses.

**Documentation comment**

- `<summary>` is as for a constant.
- `<remarks>` holds the SQL inside `<code>`, with each token written as `{{name}}`, whatever spacing the file had inside the braces.
- One `<param name="name">The text that replaces <c>{{name}}</c>.</param>` for each parameter, in parameter order, after `<remarks>`.
- Escaping and line splitting are as for a constant.

**Layout**

- The method's lines are indented with the member's indent, and each nested level adds four spaces, as the example shows.  Line endings are `\n`.
- The file header is unchanged: `// <auto-generated/>`, `#nullable enable` and the CS0108 pragma.  CS0108 also covers a method that has the name of an inherited member.

## Diagnostics

One diagnostic is added.  It is an error tagged `NotConfigurable`, like the others.

| Id | Title | Message | Location |
|----|----|----|----|
| `SQLSRC010` | SqlSourceTokenValidation is not valid | `The MSBuild property SqlSourceTokenValidation is '{0}'.  It must be 'true' or 'false'.` | None |

It is recorded in the four places `src/SqlSource/AGENTS.md` lists that apply to a usage diagnostic: the descriptor and `SqlDiagnostics.All`, `AnalyzerReleases.Unshipped.md`, and `docs/diagnostics.md`.  The section in `docs/diagnostics.md` says that the diagnostic has no file position, names the property, and shows the two valid values.

No other new diagnostic is needed:

- A query name that is used twice, or that is the name of the containing type, is already reported by name, whether the member is a constant or a method.
- A token name can be the query's own name, the name of the containing type or `Sql`.  A parameter may have any of those names.

## Package

`build/SqlSource.props` gains, outside the item group that `EnableDefaultSqlSourceItems` conditions:

```xml
<ItemGroup>
    <CompilerVisibleProperty Include="SqlSourceTokenValidation" />
</ItemGroup>
```

A project that lists its `.sql` files itself still needs the property to reach the generator, which is why the item is not under the condition.  The pack items of `SqlSource.csproj` do not change.

## Testing

Tests use xunit v3 and Shouldly.  Each behaviour is written as a failing test before its code.

- **Unit tests** (`tests/SqlSource.Tests/Generation/`):
  - `MethodWriter`: one token; several tokens; a repeated token; two adjacent tokens; a token first, last and alone; eight tokens; tokens named `span`, `state`, `span1`, `Item2` and `System`; a literal that needs escaping in the string and in the XML; a multi-line query; validation on and off.
  - `TokenValidationSetting`: each row of the table under [Reading the property](#reading-the-property), with mixed case and surrounding white space.
  - `SqlFileReader`: a block with tokens becomes a query with its segments and its validation flag.  This replaces the test that it is skipped.
  - `XmlDocWriter`: the `<param>` line.
- **Driver tests** (`tests/SqlSource.Tests/Generator/`, run on both Roslyn versions): `GeneratorHarness` gains a test `AnalyzerConfigOptionsProvider` and an argument for the property's value.  Covered: a whole generated file with a constant and a method, in each mode; a method on a generic struct; the precedence of validation as a matrix of block directive, preamble directive, property and default; an invalid property value, which gives `SQLSRC010` with no location and still gives the file, with validation on; no compilation error and no warning in generated code for every case above, including the odd token names.  This replaces the test that a query with tokens gets no member.
- **Caching** (in `Generator/`): changing the property's value runs `TypeOutput` again and leaves every `ParsedFile` cached; a new options provider with the same value leaves `TypeOutput` cached.
- **End-to-end** (`tests/SqlSource.Tests/EndToEnd/`): `SqlSource.Tests.csproj` sets `<SqlSourceTokenValidation>false</SqlSourceTokenValidation>`.
  - A query with no directive returns the expected string, accepts an empty string, and throws `NullReferenceException` for null.  That the method does not validate proves the property reaches the generator through the MSBuild file that ships.
  - A query under `-- SqlSource: token-validation` throws `ArgumentException` for an empty and for a blank string and `ArgumentNullException` for null, each naming the parameter.  This proves that a directive beats the property, and runs the validation code.
  - A query with a repeated token and several tokens returns the expected string, in `Nested` and in `Direct` mode.
  - One call allocates no more than the returned string: after a first call, which creates the cached delegate, the bytes a call allocates on the calling thread equal those of `new string('x', length)` for the same length.
  - This replaces the test that a query with tokens gets no member.
- **Documentation:** the existing `docs/diagnostics.md` test covers `SQLSRC010`.
- **Package:** `tools/check-package.sh`, unchanged.

## Documentation

- `README.md`:
  - the opening line and the status, which no longer say that tokens are missing;
  - a tokens section: the syntax, the generated method with an example, validation with its three switches and their precedence, what a null argument does with validation off, `token-ignore`, and the warning that tokens are for trusted fragments only;
  - the directives table, where `token-validation` and `no-token-validation` are described and no longer reserved;
  - the MSBuild section, which gains `SqlSourceTokenValidation`;
  - supported environments, which gains C# 12 or later.

  Links stay absolute.
- `docs/diagnostics.md`: `SQLSRC010`.
- `src/SqlSource/AGENTS.md`: the bullet about the skipped query is replaced by the rules for `MethodWriter` (the state is read by position, generated names avoid token names, framework members are qualified); "a type's file needs C# 8" becomes C# 12; the setting joins the pipeline rules.
- `docs/tech-debt/TD-0011`: a project whose `LangVersion` is below 12 gets a compiler error inside generated code and no diagnostic from the generator.  Proposed fix: read the language version from the compilation's parse options and report a usage error.  Trigger: a user report.
- The epic outline: the phase table marks phase 3 done and links this spec; the phase's recommended table becomes a settled table that records the two changes; the technical note about the `static` lambda's language version records the C# 12 requirement.
- `CONTRIBUTING.md` and `docs/publishing.md`: no change.  Nothing about building, testing, packing or releasing changes.

## Verification

- `./pre-commit-validation.sh` exits zero with every step passed.
- Both test projects pass.
- The pull request states the time and allocation of a generator run over a representative input that has token queries, before and after, measured with a `Stopwatch` loop on a `Release` build, as `src/SqlSource/AGENTS.md` requires for a hot path.
- `VersionPrefix` is compared with the release tags before the pull request is opened.  It is `0.1.0`, and the repository has no `v*` tag today.
