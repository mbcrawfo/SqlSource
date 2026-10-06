# SQL constants - design

Date: 2026-10-05

Phase 2 of the [SQL queries epic](2026-10-05-sql-queries-epic-design.md).

## Goal

Make queries without tokens usable.  A type marked `[SqlQueries]` gains a `const string` for each query in the `.sql` files it points at.  A developer installs the package, adds the attribute, and uses the constants; every mistake is reported as an error at the place it was made.

```sql
-- name: GetUser
-- summary: Loads one user by id.
SELECT id, name FROM users WHERE id = @id;
```

```csharp
[SqlQueries]
public partial class UserRepository
{
    public Task<User> Get(int id) => connection.QuerySingleAsync<User>(Sql.GetUser, new { id });
}
```

A query that contains a token gets no member until phase 3.  The package is not released before then.

## Decisions

The epic's decisions about the attribute, the modes, the supported types, the generated documentation and errors apply.  The 15 items its phase 2 section recommended with a concrete answer were accepted as written.  The rest were settled in this design:

| Decision | Choice | Reason |
|----|----|----|
| `SqlQueriesMode` and `[Conditional]` | Only `SqlQueriesAttribute` is conditional.  The enum stays in the consumer's assembly as an internal type. | C# allows `[Conditional]` on attribute classes only (CS1689).  The epic said both types carry it, which cannot compile.  A `bool` property in place of the enum was rejected: it reads worse and rules out a third mode. |
| Pipeline | Resolve paths first, then parse only the files a type claims | A migration script no type points at is never read.  Editing one `.sql` file re-parses that file and re-emits only the types that use it. |
| MSBuild file | `build/SqlSource.props`, with the opt-out property `EnableDefaultSqlSourceItems` | Verified in a scratch project: `$(DefaultItemExcludes)` keeps `bin/` and `obj/` out, and a consumer can opt out by property or by `<AdditionalFiles Remove="..." />`.  A `.targets` file would add the items after the project body and defeat `Remove`. |
| `Path` | Always relative to the folder of the file that carries the attribute.  Null or empty means that folder. | One rule.  A rooted path would not be portable between machines. |
| File-local types | Error | A generated partial declaration cannot join a `file` type.  Not in the epic. |
| A `Mode` that is not a defined value | Error | Every problem is an error.  Not in the epic. |
| Queries with tokens | Skipped without a diagnostic, at a `TODO` comment that names phase 3 | Nothing is released before phase 3, so a diagnostic would be written, documented and tested only to be deleted.  Throwing was rejected: the compiler discards all of a generator's output when it throws. |
| Diagnostic ids | `SQLSRC001` to `SQLSRC009` for usage, `SQLSRC101` to `SQLSRC114` for the `.sql` file | Leaves room in each range |
| Severity | Every descriptor is an error and is tagged `NotConfigurable` | A consumer could otherwise set a severity to `none`, which hides the message while the members are still missing |
| Release tracking | `AnalyzerReleases.Shipped.md` and `AnalyzerReleases.Unshipped.md` | Rule RS2008 requires them, and they make the build fail when an id is added, removed or changed without a record |
| User-facing diagnostic list | `docs/diagnostics.md`, linked from the README and from each descriptor's help link, and kept complete by a test | Ids are what a user sees in a build log and searches for |
| TD-0001 | Resolved: a second test project runs the generator driver tests on Roslyn 4.8.0 | This phase adds all of the generator's symbol analysis, which is what the item's trigger named |
| Output comparison in tests | Shouldly against the whole expected file, written in the test as a raw string | Verify was preferred and rejected after checking: `Verify.SourceGenerators` 2.4.3 and later need Roslyn 4.9, and Verify 33 fails the build until the project declares a sponsorship or an exemption |
| Generated summary | Names the file by its name, not its path | A path would make the output differ between checkouts |

## Out of scope

- Tokens: method emission, parameter validation and the `SqlSourceTokenValidation` property.
- Recursive folder discovery, several attributes on one type, and an accessibility option.
- Checking a query name against the type's existing members, other than `Sql` in `Nested` mode.  The compiler reports those.
- Installing the packed package into a project as a test.  The end-to-end tests import the real MSBuild file, and a script checks where the package puts it.

## Structure

`SqlSourceGenerator` wires the pipeline and holds no logic.  The new code is internal and lives in two folders.

| Unit | Folder | Responsibility | Depends on |
|----|----|----|----|
| `AttributeSource` | `Generation/` | The text of the generated attribute and enum, and their metadata names | - |
| `TargetTypeReader` | `Generation/` | A `GeneratorAttributeSyntaxContext` to a `TargetType` | Roslyn symbols and syntax |
| `SqlPath` | `Generation/` | Normalising a path, and taking its folder and file name.  String work only. | - |
| `PathResolver` | `Generation/` | A `TargetType` and the list of `.sql` paths to a `TypeFiles` | `SqlPath` |
| `SqlFileReader` | `Generation/` | An `AdditionalText` to a `ParsedSqlFile` | `SqlFileParser` |
| `TypeEmitter` | `Generation/` | A `TypeFiles` and its parsed files to a `TypeOutput`: the source text and the type's diagnostics | `XmlDocWriter` |
| `XmlDocWriter` | `Generation/` | A summary and SQL to documentation comment lines | - |
| `SqlDiagnostics` | `Diagnostics/` | Every `DiagnosticDescriptor`, and the mapping from `SqlParseErrorKind` | - |
| `DiagnosticInfo`, `LocationInfo` | `Diagnostics/` | A diagnostic as value-equal data, turned into a `Diagnostic` in an output step | `SqlDiagnostics` |

### Model

Every type is a record, and holds its collections in `EquatableArray<T>`.  None holds an `ISymbol`, a `SyntaxNode`, a `Compilation`, a `Location` or a `Diagnostic`.

| Type | Members |
|----|----|
| `TargetType` | `Namespace`, empty for the global namespace; `Types`, the containing types from the outermost to the type itself; `Mode`; `Path`, null when not set; `FilePath`, the path of the file that carries the attribute; `AttributeLocation`; `Diagnostics`, the problems found in the declaration |
| `TypeDeclaration` | `Keyword` (`class`, `struct`, `record`, `record struct`, `interface`); `Name`, the identifier as written, so a keyword name keeps its `@`; `TypeParameters`, as written without attributes, so variance is kept |
| `TypeFiles` | `Type`; `Files`, the normalised paths of its `.sql` files in order; `Diagnostics`, the type's diagnostics so far |
| `ParsedSqlFile` | `Path`, as the compiler gave it; `NormalizedPath`; `FileName`; `Queries`; `Errors` |
| `SqlQuery` | `Name`; `NameLocation`; `Summary`, null when the block has none; `Sql` |
| `TypeOutput` | `HintName`; `Source`, null when the type gets no file; `Diagnostics` |
| `DiagnosticInfo` | The descriptor's id, a `LocationInfo`, and the message arguments |
| `LocationInfo` | A file path, a `TextSpan` and a `LinePositionSpan` |

## The generated attribute

Emitted by `RegisterPostInitializationOutput` as `SqlQueriesAttribute.g.cs`:

```csharp
// <auto-generated/>
#nullable enable

namespace SqlSource
{
    /// <summary>
    /// Where the members generated for a type marked with <see cref="SqlQueriesAttribute" /> go.
    /// </summary>
    internal enum SqlQueriesMode
    {
        /// <summary>
        /// In a private static class named <c>Sql</c>, nested in the type.
        /// </summary>
        Nested = 0,

        /// <summary>
        /// On the type itself.
        /// </summary>
        Direct = 1,
    }

    /// <summary>
    /// Generates a member for each query in the <c>.sql</c> files of the type.
    /// </summary>
    [global::System.AttributeUsage(
        global::System.AttributeTargets.Class | global::System.AttributeTargets.Struct,
        AllowMultiple = false,
        Inherited = false
    )]
    [global::System.Diagnostics.Conditional("SQLSOURCE_ATTRIBUTES")]
    internal sealed class SqlQueriesAttribute : global::System.Attribute
    {
        /// <summary>
        /// A folder or one <c>.sql</c> file, relative to the folder of the file that carries the attribute.
        /// The default is that folder.
        /// </summary>
        public string? Path { get; set; }

        /// <summary>
        /// Where the generated members go.  The default is <see cref="SqlQueriesMode.Nested" />.
        /// </summary>
        public SqlQueriesMode Mode { get; set; }
    }
}
```

The attribute leaves no trace on the consumer's types unless the consumer defines `SQLSOURCE_ATTRIBUTES`.  The two type declarations themselves stay in the consumer's assembly as internal types.

## Pipeline

Each step carries value-equal data, and has a tracking name so that a test can assert it was cached.

| # | Step | Result | Runs again when |
|----|----|----|----|
| 0 | Post-initialization | The attribute source | Never |
| 1 | `ForAttributeWithMetadataName("SqlSource.SqlQueriesAttribute")` | A `TargetType` for each attributed declaration | The declaration or its file changes |
| 2 | `AdditionalTextsProvider`, filtered to `.sql`, collected | The normalised paths: de-duplicated ignoring case, sorted ordinally | A `.sql` file is added or removed |
| 3 | `CompilationProvider` | Whether `System.ArgumentException` has a member named `ThrowIfNullOrWhiteSpace` | The compilation changes; the result rarely does |
| 4 | 1 with 2 and 3 | A `TypeFiles` for each type | A type, the path list or the flag changes.  Not when a `.sql` file is edited. |
| 5 | The files of every `TypeFiles`, collected | The set of claimed paths | 4 changes |
| 6 | Each `.sql` `AdditionalText` with 5, where its path is claimed | A `ParsedSqlFile` | That file's text changes, or the claimed set does |
| 7 | 6, collected | The parsed files, one for each path | 6 changes |
| 8 | Output from 7 | Each file's errors, reported once | 7 changes |
| 9 | Each `TypeFiles` with 7 | The type with its own parsed files | 4 or 7 changes; the result is equal unless one of the type's files changed |
| 10 | 9 | A `TypeOutput` | One of the type's files or the type changes |
| 11 | Output from 10 | `AddSource` and the type's diagnostics | 10's result changes |

- Step 2 exists so that step 4 does not depend on the text of any file.
- A project that lists a `.sql` file twice gives two `AdditionalText` values with one path.  Steps 2 and 7 keep the first.
- Step 8 reports from the collected value, not from each file, so that a file listed twice is reported once.
- A `.sql` file that no type claims is never read.  Its errors are never reported.
- A file whose text cannot be read is parsed as empty text.

### Reading the attributed type

The predicate of step 1 accepts class, struct and record declarations.  The transform reads:

- **Namespace:** the symbol's containing namespace as a display string.
- **Types:** the declaration and each type declaration that contains it, from syntax.  For each: the keyword, the identifier as written, and the type parameter list without attributes.
- **`Path` and `Mode`:** the named arguments of the first matching attribute.  A `Path` that is not a string constant is read as null; the compiler has reported it.
- **Locations:** the attribute's, and each type identifier's, as path, span and line span.

An attribute repeated on two partial declarations of one type is the compiler's error CS0579.  The transform returns a `TargetType` only for the declaration that carries the type's first matching attribute, so that the type gets one generated file.

### Checks on the type

Each check that fails adds a diagnostic.  A type with any of them gets no generated file.

| Id | Check | Location |
|----|----|----|
| `SQLSRC001` | The type and each containing type have the `partial` modifier.  One diagnostic for each type that lacks it. | That type's identifier |
| `SQLSRC002` | Neither the type nor a containing type has the `file` modifier | That type's identifier |
| `SQLSRC003` | The floor flag of step 3 is set | The attribute |
| `SQLSRC004` | An explicit `Path` matches at least one `.sql` file | The attribute |
| `SQLSRC005` | With no `Path`, the folder holds at least one `.sql` file | The attribute |
| `SQLSRC006` | `Mode` is `Nested` or `Direct` | The attribute |
| `SQLSRC007` | In `Nested` mode, the type has no member named `Sql` | The attribute |

Step 1 finds `SQLSRC001`, `SQLSRC002`, `SQLSRC006` and `SQLSRC007`, and step 4 the rest.

### Resolving `Path`

`SqlPath.Normalize` turns `\` into `/`, drops empty and `.` segments, and resolves each `..` against the segment before it.  A `..` with nothing before it makes the path match nothing.  Normalised paths are compared ordinally ignoring case.

1. The base folder is the folder of `TargetType.FilePath`.
2. With no `Path`, the target is the base folder.  Otherwise the target is the base folder joined with `Path`, normalised.  `Path` is never treated as rooted: a leading separator is an empty segment.
3. If `Path` ends in `.sql`, ignoring case, the type's file is the one whose normalised path equals the target.
4. Otherwise the type's files are those whose folder equals the target.  Subfolders are not searched.
5. The files are ordered by normalised path, ordinally.

## Parsing a claimed file

`SqlFileReader` calls `SqlFileParser.Parse` with the file's text and its file name, then builds a `ParsedSqlFile`:

- Each `SqlParseError` becomes a `DiagnosticInfo` with the descriptor for its kind, its arguments, and its span converted to a line span through the file's `SourceText`.
- If there are no errors, each block without a token segment becomes a `SqlQuery`: its SQL is the text of its one literal segment, and its `NameLocation` is its `NameSpan` as a location.
- A block with a token segment is skipped.  The skip is one statement under a `TODO` comment that names phase 3, which replaces it with method emission.

A file with errors has no queries.

## Checks across a type's files

`TypeEmitter` walks the type's files in order, and the queries of each in file order.

| Id | Check | Location |
|----|----|----|
| `SQLSRC008` | A query's name was already taken by an earlier file of this type | The later query's name |
| `SQLSRC009` | A query is named like the type its member goes in: `Sql` in `Nested` mode, the type's own name in `Direct` mode | The query's name |

A file with either problem contributes no members to that type.  The type's other files still do, and the same file can still contribute to another type.  A file with parser errors contributes nothing to any type.

## Generated code

One file for each type, when the type passed its checks.  A type whose files all failed still gets its file, with an empty `Sql` class in `Nested` mode, so that a reference to `Sql` reports the missing member and not a missing type.

**Hint name:** the namespace, then each type's name followed by `-` and its arity when it is generic, joined with `.`, then `.g.cs`.  `My.App.Outer.UserRepository<T>` gives `My.App.Outer.UserRepository-1.g.cs`.

**Layout,** for `Nested` mode:

```csharp
// <auto-generated/>
#nullable enable

namespace My.App
{
    partial class Outer
    {
        partial class UserRepository<T>
        {
            private static class Sql
            {
                /// <summary>
                /// Loads one user by id.
                /// </summary>
                /// <remarks>
                /// <code>
                /// SELECT id, name
                /// FROM users
                /// WHERE id = @id;
                /// </code>
                /// </remarks>
                public const string GetUser = "SELECT id, name\nFROM users\nWHERE id = @id;";

                /// <summary>
                /// The <c>ListUsers</c> query from <c>Users.sql</c>.
                /// </summary>
                /// <remarks>
                /// <code>
                /// SELECT id, name FROM users;
                /// </code>
                /// </remarks>
                public const string ListUsers = "SELECT id, name FROM users;";
            }
        }
    }
}
```

- In `Direct` mode the constants are members of the type and there is no `Sql` class.
- A type in the global namespace has no namespace block.
- Each type is declared as `partial`, its keyword, its name and its type parameters.  Accessibility, `static` and other modifiers are not repeated.
- Members are in the order of the type's files, then of the queries in each file, with one blank line between members.
- The value is written with `SymbolDisplay.FormatLiteral`, quoted: a regular escaped literal.
- Indentation is four spaces and line endings are `\n`.  The file ends with a line ending.

**Documentation comments:**

- The `<summary>` is the block's summary.  Without one it is `The <c>{name}</c> query from <c>{file name}</c>.`
- The `<remarks>` holds the SQL inside `<code>`.
- Text is escaped for XML: `&`, `<` and `>`.
- Text is split into comment lines at every character C# treats as a line terminator: `\n`, `\r`, U+0085, U+2028 and U+2029.  A quoted region can hold one, and it would otherwise end the comment.
- An empty line is written as `///` with no trailing space.

## Diagnostics

Every descriptor has category `SqlSource`, severity error, is enabled by default, and carries the custom tag `NotConfigurable`.  Its help link is `https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#` followed by the id in lower case.

### Usage

| Id | Title | Message |
|----|----|----|
| `SQLSRC001` | Type must be partial | '{0}' must be declared partial so that SqlSource can add members to it |
| `SQLSRC002` | Type is file-local | '{0}' is a file-local type, and SqlSource cannot add members to it |
| `SQLSRC003` | Target framework is not supported | SqlSource generates code for .NET 8 and later, and this project targets an older framework |
| `SQLSRC004` | Path matches no SQL file | Path '{0}' matches no .sql file.  It is relative to the folder of this file; a value that ends in .sql is one file, and any other value is a folder. |
| `SQLSRC005` | Folder has no SQL file | The folder of this file has no .sql file.  Add one, or set Path. |
| `SQLSRC006` | Mode is not valid | '{0}' is not a value of SqlQueriesMode |
| `SQLSRC007` | Type has a member named Sql | '{0}' already has a member named 'Sql'.  Rename it, or use SqlQueriesMode.Direct. |
| `SQLSRC008` | Query name is used in two files | The query name '{0}' is already used in '{1}', which also belongs to '{2}' |
| `SQLSRC009` | Query is named like its containing type | A query of '{0}' cannot be named '{1}', because its member would have the name of the type that contains it |

In `SQLSRC008`, `{1}` is the first file's name and `{2}` the type's name.  In `SQLSRC009`, `{0}` is the attributed type's name.

### SQL files

One for each `SqlParseErrorKind`, in the order of the enum.  The location is the error's span in the `.sql` file.

| Id | Kind | Title | Message |
|----|----|----|----|
| `SQLSRC101` | `UnterminatedQuote` | Quote is not closed | A quoted string or identifier is not closed |
| `SQLSRC102` | `UnterminatedBlockComment` | Comment is not closed | A block comment or hint is not closed |
| `SQLSRC103` | `InvalidName` | Query name is not valid | '{0}' is not a valid query name.  A name is a C# identifier that is not a reserved keyword. |
| `SQLSRC104` | `DuplicateName` | Query name is used twice | The query name '{0}' is used more than once in this file |
| `SQLSRC105` | `InvalidFileName` | File name is not a valid query name | The file has no '-- name:' marker, and its name '{0}' does not give a valid query name.  Add a marker or rename the file. |
| `SQLSRC106` | `SqlBeforeFirstName` | SQL before the first name | The SQL before the first '-- name:' marker belongs to no query |
| `SQLSRC107` | `SummaryBeforeFirstName` | Summary before the first name | The '-- summary:' marker before the first '-- name:' marker belongs to no query |
| `SQLSRC108` | `MarkerAtEndOfBlock` | Marker has no SQL after it | A '-- summary:' or '-- SqlSource:' marker comes before the SQL it describes, and no SQL follows this one in its query |
| `SQLSRC109` | `UnknownDirective` | Directive is not known | '{0}' is not a SqlSource directive |
| `SQLSRC110` | `EmptyDirectiveLine` | Directive is missing | The '-- SqlSource:' marker has no directive |
| `SQLSRC111` | `InvalidDirectiveValue` | Directive value is not valid | The directive '{0}' lacks a value it needs, or has one it does not take |
| `SQLSRC112` | `ConflictingDirectives` | Directives conflict | '{0}' conflicts with the other token validation directive in the same scope |
| `SQLSRC113` | `EmptyBlock` | Query has no SQL | The query has no SQL |
| `SQLSRC114` | `ReservedTokenName` | Token name is a keyword | The token name '{0}' is a reserved C# keyword |

### Release tracking

`AnalyzerReleases.Shipped.md` and `AnalyzerReleases.Unshipped.md` sit next to `SqlSource.csproj` and are `AdditionalFiles` of it.  The shipped file holds its header only.  The unshipped file lists all 23 ids as new rules.  Neither is packed.

On each release the unshipped entries move to the shipped file under a `## Release` heading for that version, in the commit that is tagged.  `docs/publishing.md` gains that step.

### `docs/diagnostics.md`

The list a user reads.  It opens with a table of every id and title, then has one section for each id, headed with the id alone so that the anchor is stable: what the error means, an example that causes it, and how to fix it.

It is kept complete three ways:

- A test reads the file and fails when a descriptor has no section, a section has no descriptor, or a section does not contain its descriptor's title.
- The root `AGENTS.md` names it under "Keep the docs current": a change that adds, removes or changes a diagnostic updates it.
- Each descriptor's help link points at its section.

## Package

`src/SqlSource/build/SqlSource.props`, packed to `build/`:

```xml
<Project>
    <ItemGroup Condition="'$(EnableDefaultSqlSourceItems)' != 'false'">
        <AdditionalFiles Include="**/*.sql" Exclude="$(DefaultItemExcludes);$(DefaultExcludesInProjectFolder)" />
    </ItemGroup>
</Project>
```

A consumer who sets `EnableDefaultSqlSourceItems` to `false` lists the `.sql` files as `AdditionalFiles` by hand.

`tools/check-package.sh` checks a packed `.nupkg`: it holds `build/SqlSource.props`, `analyzers/dotnet/cs/SqlSource.dll` and `README.md`, and nothing under `lib/`.  `pre-commit-validation.sh` packs and runs it, and `workflows/build.yml` runs it after its pack step, so that the two stay mirrors of each other.

## Testing

Tests use xunit v3 and Shouldly.  Each behaviour is written as a failing test before its code.

- **Unit tests** (`tests/SqlSource.Tests/Generation/`): `SqlPath` normalisation, including both separators, `.` and `..`, a `..` past the start, and case; `PathResolver` for the default folder, a folder path, a file path, a subfolder that is not searched, and each "matches nothing" case; `XmlDocWriter` for escaping, each line terminator and an empty line.
- **Driver tests** (`tests/SqlSource.Tests/Generator/`): one helper builds a compilation from C# 12 source and in-memory `AdditionalText` values, runs the generator, and returns the generated sources and diagnostics.  A test compares a whole generated file with its expected text.  The helper also asserts that the compilation with the generated trees has no errors, except in tests of a diagnostic.  Covered: the attribute source; each mode; class, struct, record class and record struct; static, generic and nested types; the global namespace; a keyword as a type name; a generated summary and a written one; multi-line SQL; text that needs escaping in the literal and in the XML; member order across files; each of `SQLSRC001` to `SQLSRC009` with its location; a query with tokens, which gets no member and no diagnostic while the other queries of its file do; a parser error with its line and column in the `.sql` file; one test that every `SqlParseErrorKind` has a descriptor; a file shared by two types reported once; a file listed twice; an unclaimed file with errors; an attribute on two partial declarations.
- **Caching** (in `Generator/`): with step tracking on, editing one `.sql` file leaves the other file's parse and the unrelated type's output cached; editing a method body leaves every step cached; adding a `.sql` file to another folder leaves every type's output cached.
- **End-to-end** (`tests/SqlSource.Tests/EndToEnd/`): real `.sql` files and attributed types, compiled by the build with the generator loaded.  `SqlSource.Tests.csproj` imports the real `SqlSource.props`.  The tests assert the constants at run time, for: the default folder in `Nested` mode; `Direct` mode; `Path` to a file and to a folder; a generic type; a nested type; a record struct.
- **Documentation:** the `docs/diagnostics.md` test above.
- **Roslyn floor** (`tests/SqlSource.Tests.RoslynFloor/`): a test project that references Roslyn 4.8.0, has its own lock file, and compiles the files of `tests/SqlSource.Tests/Generator/` by link.  It is in `SqlSource.slnx`, so `dotnet test` runs it, and `SqlSource.csproj` gives it `InternalsVisibleTo`.  The driver tests therefore use only Roslyn API that 4.8.0 has, and only C# 12 in their inputs.
- **Package:** `tools/check-package.sh`.

## Documentation

- `README.md`: the status, which says that queries with tokens are not generated yet; installation; the attribute, `Path` and both modes; the `.sql` file format and the directives that apply without tokens; the dialect limits from `docs/tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md`, with what `preserve-comments` can and cannot work around; opting out of the default `.sql` items; a link to `docs/diagnostics.md`.  Links stay absolute.
- `docs/diagnostics.md`: new, as described above.
- `AGENTS.md`: `docs/diagnostics.md` joins the documents under "Keep the docs current".
- `src/SqlSource/AGENTS.md`: the pipeline rules, the two new folders, where a new diagnostic is recorded, and the floor test project in place of the TD-0001 reference.
- `CONTRIBUTING.md`: the floor test project, the package check, and a coverage command that works with two test projects.
- `docs/publishing.md`: the release-tracking step.
- `docs/tech-debt`: TD-0001 is deleted with its row and every reference to it.  TD-0006 records the CS0436 warning between two projects that share internals, with raising the floor to Roslyn 4.14 as its trigger.
- The epic outline: the phase table, the correction about the enum, and the recommended items recorded as decided.

## Verification

- `./pre-commit-validation.sh` exits zero with every step passed, including the package check.
- Both test projects pass.
- The pull request states the time and allocation of a generator run over a representative input, measured with a `Stopwatch` loop on a `Release` build, as `src/SqlSource/AGENTS.md` requires for a hot path.
- `VersionPrefix` is compared with the release tags before the pull request is opened.  The repository has no `v*` tag today.
