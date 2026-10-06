# SQL queries from `.sql` files - epic outline

Date: 2026-10-05

This outline coordinates the phases of the epic.  It is kept current until the epic closes: a phase that changes the plan updates it in the same pull request.

## Goal

Let a C# project keep its SQL in `.sql` files and use it through generated members.  A type marked `[SqlQueries]` gains a string constant for each query in the `.sql` files it points at, and a method for each query that contains replacement tokens.

```sql
-- name: GetUser
-- summary: Loads one user by id.
SELECT id, name FROM users WHERE id = @id;

-- name: ListFrom
SELECT id, name FROM {{table}} ORDER BY name;
```

```csharp
[SqlQueries]
public partial class UserRepository
{
    // Generated: Sql.GetUser (a constant) and Sql.ListFrom(string table) (a method).
}
```

## How to read this document

Every phase has been through its own design.  This outline records what was settled while the epic and its phases were designed.  Each statement about a phase is one of three kinds:

- **Decided.**  Agreed with the project owner.  A phase spec may add detail and must not contradict it.  Changing one means asking the owner and updating this outline.
- **Recommended.**  The epic designer's proposal for a question that was raised and not settled.  None remain: each was settled in its phase's design, and is recorded under "Settled in the phase design".
- **Technical notes.**  Facts about Roslyn, MSBuild and this repository that constrain the design.  Verify them against the code before relying on them.

## Phases

| Phase | Status | Spec | Delivers |
|----|----|----|----|
| 1. SQL parser | Done | [sql-parser-design](2026-10-05-sql-parser-design.md) | The text of one `.sql` file becomes named blocks with their directives, summary, cleaned SQL and token segments, or a list of errors.  Pure code with no generator pipeline. |
| 2. Constants | Done | [sql-constants-design](2026-10-05-sql-constants-design.md) | The `[SqlQueries]` attribute and `SqlQueriesMode` enum, `.sql` discovery and `Path` resolution, type-shape checks, constant emission with XML docs, diagnostics located in the `.sql` file, and the MSBuild file in the package. |
| 3. Tokens | Done | [sql-tokens-design](2026-10-06-sql-tokens-design.md) | Method emission with `string.Create`, parameter validation, and the `SqlSourceTokenValidation` MSBuild property. |

Each phase has its own spec, plan and pull request.  Phase 1 changes nothing a user can see.  Phase 2 makes queries without tokens usable, and skips a block that contains tokens.  Phase 3 gives such a block its method.  Nothing was released before phase 3.

The phases are ordered parser first because the parser is the largest piece, has no dependency on Roslyn's pipeline, and can be tested completely by itself.  A thin end-to-end slice was considered and not chosen.

## Decisions for the whole epic

### Consumers

- Generated code targets .NET 8 and later.  A project that targets an older framework gets a diagnostic, not output that fails to compile.  The floor comes from the generated code: `ArgumentException.ThrowIfNullOrWhiteSpace` needs .NET 8, and `string.Create` with a span callback needs .NET Core 2.1.
- The generated code of a type is C# 12, the default language version of a project that targets .NET 8.  This is a documented requirement and is not checked; `docs/tech-debt/TD-0011-language-version-is-not-checked.md` records that.  It was settled in the phase 3 design.
- The host floor is unchanged: Roslyn 4.8.0, the .NET 8 SDK and Visual Studio 2022 17.8.

### The attribute

- The trigger is `SqlQueriesAttribute`, with a `Path` property and a mode of type `SqlQueriesMode`.
- Both types are emitted by the generator as internal source.  The attribute is marked `[Conditional]`, so it is not applied in the consumer's metadata.  The enum cannot be: C# allows `[Conditional]` on attribute classes only.  Both declarations stay in the consumer's assembly as internal types.  The package stays a development dependency with nothing in `lib/`.  Shipping the types in a runtime assembly was rejected: it adds a second project and a runtime reference to manage.
- Two projects that both use SqlSource, where one has `InternalsVisibleTo` the other, get warning CS0436, which is an error under `TreatWarningsAsErrors`.  The clean fix, `AddEmbeddedAttributeDefinition`, needs Roslyn 4.14, above the floor.  This is accepted, and `docs/tech-debt/TD-0006-attribute-conflicts-across-friend-assemblies.md` records it with raising the floor to Roslyn 4.14 as its trigger.
- By default every `.sql` file in the folder of the `.cs` file that carries the attribute belongs to the type.  `Path` points at another folder or at one `.sql` file, relative to that `.cs` file.  For a partial type declared in several files, the file that carries the attribute is the one that counts.
- A `Path` that matches nothing is an error.

### Modes

| Mode | Generated shape |
|----|----|
| `Nested` (default) | `private static class Sql` nested in the type, with public members |
| `Direct` | Public members on the type itself |

- The modes are named for where the members go, not for their accessibility.  The original names, private and public, were dropped because the modes change placement.
- There is no accessibility option.  An `Accessibility` property and internal members in `Direct` mode were both considered and rejected for now.
- A query without tokens is a `const string`.  A query with tokens is a static method that returns `string`.

### Supported types

Partial classes, structs, record classes and record structs, including static, generic and nested ones.  The type and each containing type must be `partial`, because the generator can only add members through another partial declaration.  A type that is not gets the generator's own error, not the compiler's.

### SQL files

The [parser spec](2026-10-05-sql-parser-design.md) holds the full rules.  In outline:

- Three markers are recognised, each a line comment that starts its line, matched case-insensitively and always removed from the SQL: `-- name:`, `-- summary:` and `-- SqlSource:`.
- `-- name:` starts a named block that runs to the next name marker or the end of the file.  A file with no name marker is one block named after the file.
- Before the first name marker, comments and `-- SqlSource:` directives are allowed and the directives apply to every block.  SQL there is an error.
- A `-- summary:` or `-- SqlSource:` marker comes before the SQL it describes.  One at the end of a block, with no SQL after it before the next name marker or the end of the file, is the error `MarkerAtEndOfBlock`.  This holds for every block, including the last one in a file and the single block of a file with no name marker.  The rule was added after the parser spec was written, so that a marker written above a name marker cannot silently apply to the block before it.
- Directives:

| Directive | Effect |
|----|----|
| `preserve-comments` | Comments are kept in the SQL |
| `no-token-validation` | The generated method does not validate its parameters |
| `token-validation` | The generated method validates its parameters even when validation is off for the project |
| `token-ignore=name` | `{{name}}` stays in the SQL as literal text |

- Comments are stripped by one conservative lexer with no dialect setting.  Its aim is to keep text where dialects disagree and to report an error where input is unbalanced: a comment left in is harmless, SQL removed is a bug.  It does not always meet that aim.  It misreads some constructs that are specific to one dialect, and a misread either reports an error for valid SQL or strips SQL as if it were a comment.  `docs/tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md` lists the constructs.  `preserve-comments` prevents the stripping and does not prevent the error.
- The dialect-sensitive choices sit in one place in the lexer so that a later dialect setting can become flags; no such setting is built now.
- A hint (`/*+ ... */` or `/*! ... */`) is never stripped, and is copied exactly as written apart from line endings, like a quoted region.  It can hold executable SQL.  This was settled by the review of phase 1, and replaces the parser spec's treatment of a hint as plain text.
- Line endings in generated SQL are always `\n`, so the constants do not differ between checkouts.

### Tokens

- The form is `{{name}}`, where the name is a valid C# identifier; spaces inside the braces are ignored.
- Each distinct name becomes one `string` parameter.  Parameters are ordered by first appearance.
- Tokens are replaced anywhere in the SQL, including inside string literals.  Where a token makes sense is left to the developer.
- Token replacement is string concatenation into SQL.  It is an escape hatch for the places a query parameter cannot go, and it does not make a value safe.  The README says that tokens are for trusted fragments only.

### Generated documentation

Every generated member has XML documentation, so a consumer that treats CS1591 as an error is not broken:

- a `<summary>` taken from the block's `-- summary:` lines, or generated text that names the query and its file;
- the SQL in `<remarks>`, inside `<code>`;
- a `<param>` for each token parameter.

### Errors

Every problem is an error, never a warning: an unknown directive, a duplicate or invalid name, an empty block, an unterminated string or comment, a `Path` that matches nothing.  A `.sql` file with any error produces no members.

## What phase 1 hands over

`SqlFileParser.Parse(text, fileName)` returns a `SqlFileParseResult`.  The later phases consume it and do not parse SQL themselves.

| Phase 1 output | Used by |
|----|----|
| `SqlBlock.Name`, `NameSpan` | Phase 2: the member name, and the location for diagnostics about the block |
| `SqlBlock.Summary` | Phase 2: the `<summary>`; null means generate one |
| `SqlBlock.Segments` | Phase 2: one literal segment is a constant, and a block with any token segment is skipped.  Phase 3: the method body and its parameters. |
| `SqlBlock.TokenValidation` | Phase 3: true or false when a directive applies, null when the project setting decides |
| `SqlBlock.PreserveComments` | Nothing.  The parser has already applied it. |
| `SqlParseError` kind, span and arguments | Phase 2: mapped to a Roslyn diagnostic located in the `.sql` file.  Each member of `SqlParseErrorKind` documents the one argument it carries, or that it carries none. |

The results are value-equal, so they can be cached in the incremental pipeline.  Spans are offsets; phase 2 converts them to lines and columns.

## Phase 2 - constants

### Scope

1. Emit `SqlQueriesAttribute` and `SqlQueriesMode`.
2. Find the `.sql` files of each attributed type.
3. Check the type's shape.
4. Emit a constant for each block without tokens, with XML documentation.
5. Report parser errors and generator errors as Roslyn diagnostics.
6. Ship the MSBuild file that registers `.sql` files with the compiler.
7. Make the feature usable: README, package contents, end-to-end tests.

### Decided

Everything under [Decisions for the whole epic](#decisions-for-the-whole-epic) that concerns the attribute, the modes, the supported types, the generated documentation and errors.  In addition:

- The nested class is named `Sql`.
- A block that contains tokens gets no member and no diagnostic until phase 3.  The package is not released before phase 3, so a diagnostic would only be written to be deleted.  The skip is marked with a `TODO` comment for phase 3 to replace.
- Diagnostic ids and descriptors are created in this phase, one for each `SqlParseErrorKind` and one for each generator error.
- A parser error is reported at its position in the `.sql` file, not at the attribute.
- The dialect limits go into the README in this phase.  `docs/tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md` is the current list; the parser spec's list is incomplete.  `preserve-comments` is a workaround only where a limit strips SQL.  Where a limit reports an unterminated quote or comment, the SQL has to be rewritten.

### Settled in the phase design

The owner accepted the epic's recommendations as written, and settled the two it left open.  All of these are now decided.

| Question | Decision | Reason |
|----|----|----|
| Shape of the attribute | `Path` and `Mode` are named properties with no constructor arguments.  `AttributeUsage` is `Class \| Struct`, not inherited.  Namespace `SqlSource`. | Records are covered by the class and struct targets.  The compiler then rejects interfaces and enums by itself. |
| Several attributes on one type | Not allowed (`AllowMultiple = false`) | One attribute, one path is simple to explain.  Allowing more later breaks nobody. |
| Is folder discovery recursive? | No | A subfolder usually belongs to another type.  A `Recursive` option can be added later without breaking anyone. |
| Is `Path` a file or a folder? | A value ending in `.sql`, compared case-insensitively, is a file.  Anything else is a folder. | The generator may not touch the file system, so it cannot ask. |
| Path comparison | Accept `/` and `\` in `Path`, and compare paths case-insensitively | A project then builds the same on every operating system |
| The default folder holds no `.sql` file | Error | The attribute does nothing, which is never intended |
| A `.sql` file no type points at | Ignored, including its parser errors | The package registers every `.sql` file in the project.  A migration script elsewhere must not break the build. |
| A stray file in a type's folder whose name is not an identifier | Keep the parser's `InvalidFileName` error | It follows from the rules.  The developer adds a `-- name:` marker, moves the file or sets `Path`. |
| A `.sql` file shared by two types | Parse it once and report its errors once | Avoids duplicate diagnostics |
| A query name used in two files of one type | Generator error at the second block | The compiler's duplicate-member error would point at generated code |
| A query named like its enclosing type (`Sql` in `Nested` mode, the type's own name in `Direct` mode) | Generator error | Otherwise the compiler reports CS0542 in generated code |
| An existing member with a query's name, or an existing member named `Sql` in `Nested` mode | Generator error for the `Sql` case.  Leave other member collisions to the compiler. | The `Sql` case is common enough to explain.  Full collision checking is a lot of code for a rare mistake. |
| Where the .NET 8 floor is checked | In this phase, for every attributed type | The supported set then does not depend on whether a query has tokens |
| How the floor is detected | By looking for `ArgumentException.ThrowIfNullOrWhiteSpace` in the compilation | It tests the capability the generated code needs, and needs no MSBuild property |
| Diagnostic id prefix | `SQLSRC` followed by three digits, category `SqlSource` | Short and unlikely to collide |
| `docs/tech-debt/TD-0001` | Resolved in this phase: `tests/SqlSource.Tests.RoslynFloor` runs the generator driver tests on Roslyn 4.8.0 | Its trigger, "the generator gains its first real analysis logic", fires here |
| Output comparison in tests | Shouldly against the whole expected file | `Verify.SourceGenerators` 2.4.3 and later need Roslyn 4.9, and Verify 33 fails the build until the project declares a sponsorship or an exemption |
| `Path` rooted or relative | Always relative to the folder of the file that carries the attribute.  Null or empty means that folder. | One rule, and a rooted path is not portable |
| A file-local type | Generator error | A generated partial declaration cannot join it |
| A `Mode` that is not a defined value | Generator error | Every problem is an error |
| Can a consumer change a diagnostic's severity? | No.  Every descriptor is tagged `NotConfigurable`. | Turning one off would hide the message while the members are still missing |
| Diagnostic id ranges | `SQLSRC001` to `SQLSRC099` for usage, `SQLSRC101` and up for the `.sql` file | Each range has room to grow |
| A list of diagnostics for users | `docs/diagnostics.md`, linked from the README and from each descriptor, and kept complete by a test | Ids are what a user sees in a build log |
| The MSBuild file | `build/SqlSource.props`, with the opt-out property `EnableDefaultSqlSourceItems` | Verified: `bin/` and `obj/` are excluded, and a consumer can opt out by property or with `Remove` |

### Technical notes

**Finding files**

- A generator sees only C# sources and `AdditionalFiles`.  The `.sql` files reach it through `context.AdditionalTextsProvider`, filtered by extension.
- The package must add them: an `AdditionalFiles` item that includes `**/*.sql`, in an MSBuild file under `build/` named after the package id.  It is `build/SqlSource.props`.  A `.props` file is imported before the project body, so a consumer can remove items in the project body; a `.targets` file would add them afterwards.  Its `Exclude` uses `$(DefaultItemExcludes)`, which keeps `bin/` and `obj/` out because items are evaluated after every property.
- `SqlSource.csproj` packs only the README and the analyzer assembly today.  The `build/` file is a new pack item.
- `tests/SqlSource.Tests` uses the generator through a `ProjectReference`, so it never sees the package's MSBuild file.  An end-to-end test there must import that file or declare the item itself.  Importing the real file exercises what ships.
- The generator may not read the file system (analyzer rule RS1035).  Resolving `Path` is string work: take the directory of `AttributeData.ApplicationSyntaxReference.SyntaxTree.FilePath`, combine it with `Path`, normalise, and compare with each `AdditionalText.Path`.

**The attribute**

- Emit it with `RegisterPostInitializationOutput`, and find its uses with `SyntaxProvider.ForAttributeWithMetadataName`, which exists in Roslyn 4.8.
- A `[Conditional]` attribute is still visible to the generator.  It is dropped only when the consumer's assembly is emitted.

**Emission**

- One generated file per attributed type.  The hint name must be unique and may not contain characters such as `<`, `>` or a path separator, so a generic or nested type needs a sanitised name.
- The generated partial declaration repeats the namespace, each containing type and the type's own keyword (`class`, `struct`, `record`, `record struct`) and type parameters.  It does not need to repeat accessibility, `static` or other modifiers.
- Write string values with `SymbolDisplay.FormatLiteral`.  The output is a regular escaped literal, which compiles under any `LangVersion` the consumer sets.  Raw string literals do not.
- XML-escape the summary and the SQL in the documentation comment.
- Start each type's file with `// <auto-generated/>` and `#nullable enable`.  The attribute's file has no `#nullable enable` and no nullable annotation: it is added to every project that references the package, including one on an older framework and C# 7.3, which must get the framework diagnostic and not a compiler error in generated code.
- A compiler warning inside generated code cannot be fixed by the consumer.  The attribute's file turns off CS0436, which a project that sees another project's internals would otherwise get for the two generated types.  A type's file turns off CS0108, so that a query can have the name of an inherited member, such as `GetType`.
- The compiler compares the names of generated files ignoring case and throws on a clash, which drops the output of every type.  A name that would clash gets a hash of its exact spelling.
- Order members deterministically, for example by file path and then by position in the file.

**The incremental pipeline**

- Parse each `.sql` file in its own pipeline step, so that editing one file re-parses only that file.
- Keep `ISymbol`, `SyntaxNode`, `Compilation`, `Location` and `Diagnostic` out of the values that flow between steps.  They defeat caching.  Carry value-equal data and build the `Location` and `Diagnostic` in the output step.
- A location in a `.sql` file is `Location.Create(path, textSpan, lineSpan)`.  The line span comes from the file's `SourceText`.

**Diagnostics**

- The analyzer release-tracking files, `AnalyzerReleases.Shipped.md` and `AnalyzerReleases.Unshipped.md`, are a choice and not a requirement: RS2008, the rule that asks for them, is not reported for a generator project.  Once they are `AdditionalFiles` of the project, rules RS2000 to RS2007 fail the build when a descriptor and the files disagree.
- All descriptors are errors.

**Repository rules that bite in this phase**

- `src/SqlSource/AGENTS.md`: `netstandard2.0` only, no API newer than Roslyn 4.8.0, Roslyn references stay private.
- The README is the package readme on nuget.org.  Its links must be absolute URLs, and its "Status" section becomes wrong in this phase.
- `VersionPrefix` is checked against the release tags before the pull request is opened.

### Testing

- **Generator driver tests:** in-memory `AdditionalText` inputs and source text, asserting on generated trees and diagnostics, as `SqlSourceGeneratorTests` does today.  Cover each mode, each supported kind of type, generic and nested types, each generator error, and a parser error surfacing with its `.sql` location.
- **End-to-end tests:** real `.sql` files and attributed types inside `tests/SqlSource.Tests`, with assertions on the constants at run time.
- **Caching:** a driver test with step tracking turned on that edits one input and asserts that unrelated steps are reused.
- **Package:** the packed `.nupkg` contains the `build/` file.

### Documentation

- `README.md`: status, installation, the attribute, both modes, the `.sql` file format, directives, the dialect limits.
- `src/SqlSource/AGENTS.md`: the pipeline rules above.
- `CONTRIBUTING.md`: only if building, testing or packing changes.
- `docs/tech-debt`: the CS0436 item, and TD-0001 resolved or updated.

## Phase 3 - tokens

### Scope

1. Emit a static method for each block that has tokens.
2. Validate the method's parameters, under the control of the directives and a project setting.
3. Replace phase 2's skip of a block that has tokens.

### Decided

- **Signature.**  The method has the query's name and returns `string`.  It takes one `string` parameter for each distinct token name, named after the token and ordered by first appearance.  It sits where a constant would: in the `Sql` class in `Nested` mode, on the type in `Direct` mode.
- **Building the string.**  The method uses `string.Create` to build the result efficiently.  The intent is one allocation of the final string, with no intermediate strings.
- **Validation.**  Each parameter is checked with `ArgumentException.ThrowIfNullOrWhiteSpace` by default.
- **Turning validation off for a project.**  `<SqlSourceTokenValidation>false</SqlSourceTokenValidation>` in the project file.
- **Precedence**, highest first:
  1. the block's own `token-validation` or `no-token-validation` directive;
  2. the same directive in the file's preamble;
  3. the `SqlSourceTokenValidation` property;
  4. the default, which is to validate.

  The parser resolves the first two into `SqlBlock.TokenValidation`.  Phase 3 applies the property only when that value is null.
- **Why validation can be turned off.**  An empty fragment can be legitimate, for example an optional clause.
- **Safety.**  Validation checks that a value is present, not that it is safe.  The README states that tokens are for trusted fragments only.
- **Documentation.**  The method gets the same `<summary>` and `<remarks>` as a constant, plus a `<param>` for each parameter.
- **Adding a token changes the member.**  A query that gains its first token changes from a constant to a method, which breaks its callers at compile time.  This is expected.

### Settled in the phase design

The owner accepted five of the epic's seven recommendations as written and changed two.  The rest were settled in the [phase spec](2026-10-06-sql-tokens-design.md).  All of these are now decided.

| Question | Decision | Reason |
|----|----|----|
| Which `string.Create` overload | `string.Create<TState>(int length, TState state, SpanAction<char, TState> action)`.  The length is the sum of the literal lengths plus the length of each parameter times its number of occurrences.  The callback is a `static` lambda. | It allocates exactly the final string.  The interpolated-string overload builds in a pooled buffer first. |
| The state | A tuple of the parameters, read by position (`Item1`).  A query with one token passes the parameter itself. | `Item2`, `Rest` and `ToString` are valid token names and are not valid element names.  A tuple of one element cannot be written. |
| Copying segments | Copy each literal and each parameter into the span in order, moving the span past what was copied | Direct, and no formatting is involved |
| Names of the lambda's parameters | `span` and `state`, with a number added until the name is not a token name of that query | Any identifier can be a token name, so no fixed name is safe |
| How framework members are written | `global::System.ArgumentException`, and the keyword `string` | `System` and `String` are valid token names |
| A null argument when validation is off | **Changed from the recommendation.**  Nothing is checked.  The method has no validation code, and a null argument fails with a `NullReferenceException`.  The epic recommended `ArgumentNullException.ThrowIfNull`. | The owner's decision: off means off |
| The language version of a type's file | **Changed from phase 2.**  C# 12 or later is a documented requirement and is not checked.  Phase 2 kept a type's file to C# 8. | The owner's decision.  A project that targets .NET 8 or later has C# 12 unless it lowers `LangVersion`. |
| Value of `SqlSourceTokenValidation` | `true` and `false`, trimmed and compared ignoring case.  An empty or missing value means the default.  Any other value is the error `SQLSRC010`. | A typo should not silently change behaviour |
| An invalid value | `SQLSRC010` has no location and is reported once for the compilation, whether or not a type carries the attribute.  Generation continues with validation on. | A generator cannot see where a property was set.  Emitting nothing would bury the one real error under an error for every use of a query. |
| Where the property is declared | A `CompilerVisibleProperty` item in `build/SqlSource.props`, outside the condition on `EnableDefaultSqlSourceItems` | The file already exists and is imported by every consumer.  A project that lists its own `.sql` files still needs the property. |
| Where the setting enters the pipeline | After a type's queries are selected, as a second input of emission | A change to the property emits each type again and parses nothing again |
| The nullable context of a type's file | `#nullable enable`, always.  It does not follow the project's `Nullable` setting. | The compiler ignores that setting in a generated file |
| README | A tokens section: the syntax, the generated method, validation and its three switches, `token-ignore`, and the trusted-fragments warning | The warning is a decided requirement |

### Technical notes

- An MSBuild property reaches a generator only when it is listed as a `CompilerVisibleProperty`.  It is then read from `AnalyzerConfigOptionsProvider.GlobalOptions` under the key `build_property.SqlSourceTokenValidation`.
- A driver test supplies the property through a test implementation of `AnalyzerConfigOptionsProvider`.  The test project, which uses a `ProjectReference`, gets it only if it imports the package's MSBuild file.
- For up to four parts `string.Concat` also makes a single allocation.  `string.Create` is the stated requirement and gives one code path for any number of parts.
- A `static` lambda needs C# 9, the caller-expression default that gives a validation exception its parameter name needs C# 10, and a tuple state needs `System.ValueTuple`.  All are present for any .NET 8 consumer on its default language version, C# 12.
- Token names are case-sensitive, so `{{Table}}` and `{{table}}` are two parameters.  Reserved keywords never arrive as token names; the parser rejects them.

### Testing

- **Generator driver tests:** one token, several tokens, a repeated token, both modes, validation on and off through each of the three switches and their precedence, and an invalid property value.
- **End-to-end tests:** call the generated methods in `tests/SqlSource.Tests` and assert on the returned string and on the exceptions.
- **Replacement:** phase 2's test that a query with tokens gets no member is replaced by the method tests.
- **The property, end to end:** `tests/SqlSource.Tests` sets `SqlSourceTokenValidation` to `false`, so a query there without a directive shows the property reaching the generator through the MSBuild file that ships, and a query under `token-validation` shows the directive beating it.

### Documentation

- `README.md`: the tokens section described above.
- `docs/diagnostics.md` and the analyzer release-tracking file gain each diagnostic this phase adds.  New usage ids continue from `SQLSRC010`.

## Out of scope for the epic

- A dialect setting, and dialect-specific lexers.
- An accessibility option on the attribute.
- Parameter types other than `string`.
- Consumers that target a framework older than .NET 8.
- Recursive folder discovery and several attributes on one type.
