# SQL Parser Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the text of one `.sql` file into the named SQL blocks the generator will emit, or into a list of errors, as pure code with no generator pipeline.

**Architecture:** `SqlLexer` splits the text into lexemes (text, quoted region, line comment, block comment, hint) that cover it without gaps.  `SqlFileParser` groups the lexemes into blocks at `-- name:` markers, using `SqlMarkerReader` to recognise markers, `SqlDirectiveScope` to read `-- SqlSource:` directives, `SqlTextBuilder` to produce each block's cleaned SQL, and `TokenScanner` to split that SQL into literal and `{{token}}` segments.  Every result type is a record whose collections are held in `EquatableArray<T>`, so that phase 2 can cache results in the incremental pipeline.

**Tech Stack:** .NET SDK 10.0.401, C# (`LangVersion` preview) on `netstandard2.0`, Roslyn 4.8.0 (`Microsoft.CodeAnalysis.CSharp`) for `TextSpan` and `SyntaxFacts`, xunit v3 on Microsoft.Testing.Platform, Shouldly, CSharpier.

**Spec:** `docs/superpowers/specs/2026-10-05-sql-parser-design.md`, phase 1 of `docs/superpowers/specs/2026-10-05-sql-queries-epic-design.md`.

## Global Constraints

- Work on the existing branch `sql-parser`, which already holds both specs and this plan.  Commit after each task.  Do not push, and do not merge `main` into the branch.
- `src/SqlSource` targets `netstandard2.0` only and may use no API newer than Roslyn 4.8.0.  Add no package reference.
- Every parser type is `internal` and lives in `src/SqlSource/Parsing/`, namespace `SqlSource.Parsing`.  Tests live in `tests/SqlSource.Tests/Parsing/`, namespace `SqlSource.Tests.Parsing`.
- The parser reads no files.  Comparisons are ordinal; marker keywords and directive names are ordinal ignoring case.
- Warnings are errors, and `AnalysisLevel` is `latest-all` with the Roslynator and Sonar analyzers.  Fix a finding in the code.  A rule that cannot be satisfied is turned off in `.editorconfig` with a comment, never with `NoWarn` or `#pragma`.
- No collection expression may target `ImmutableArray<T>` in `src/SqlSource` (CS9210 on this version of `System.Collections.Immutable`).  Use `ImmutableArray.Create`, a builder or `ToImmutableArray()`.
- Every text file obeys `.editorconfig`: 120 columns (comments and string literals included), 4-space indent, LF line endings, final newline.  CSharpier owns C# layout: run `./format.sh` after writing C#.
- Every commit must pass `./pre-commit-validation.sh`.  Run it as its own command before the commit command; a hook re-runs it and blocks the commit when it fails.  Docker must be running.  Never chain a formatter and a commit in one command.
- Every commit message ends with the line `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Markdown in this repository puts two spaces after a sentence-ending period.
- `README.md` and `CONTRIBUTING.md` do not change: nothing a user or contributor does is different after this phase.
- Out of scope: the attribute, file discovery, code emission, Roslyn diagnostics and their ids, name uniqueness across files, a dialect setting, reformatting SQL, line and column positions.

All file contents below were built, formatted and run in a throwaway copy of the repository before this plan was written; the expected outputs are the observed ones.  Each task was also replayed in order: the build fails as described before the task's source files exist, and passes with the stated test totals after.

## Review Focus

Inputs the spec implies but does not spell out, most likely first.  Each is pinned by a test in the task that owns the code.

1. **Windows line endings.**  A file checked out with `\r\n` must give the same blocks and the same SQL as its `\n` form, with markers still recognised.  Pinned by `Build_AnyLineTerminator_BecomesLineFeed` (Task 5) and `Parse_WindowsLineEndings_GiveTheSameBlocksAsUnixLineEndings` (Task 6).
2. **An empty, whitespace-only or comment-only file.**  It must be an `EmptyBlock` error, not an exception and not an empty constant.  Pinned by `Parse_FileWithoutNameMarkerOrSql_IsAnErrorAtTheStart` (Task 6).
3. **Text that ends where a construct could start.**  A file ending in `-`, `/`, `$`, `E` or `{{` must not read past the end of the text.  Pinned by `Lex_TextEndingWhereAConstructCouldStart_IsText` (Task 2) and `Scan_SqlEndingInsideAToken_IsLiteral` (Task 3).
4. **Non-ASCII SQL and names.**  Accented letters and surrogate pairs in strings, comments, dollar-quote tags, query names and token names must pass through intact.  Pinned by `Lex_NonAsciiText_IsKeptIntact` (Task 2), `Scan_ContextualKeywordOrUnusualIdentifier_IsAToken` (Task 3), `Build_NonAsciiText_IsKeptIntact` (Task 5) and `Parse_UnusualButValidNames_AreAccepted` (Task 6).
5. **Pathological nesting.**  A hundred thousand nested block comments must lex in one pass without a stack overflow.  Pinned by `Lex_DeeplyNestedBlockComments_IsOneComment` (Task 2).

---

## File Structure

| File | Responsibility | Task |
|----|----|----|
| `.editorconfig` (modify) | Turns IDE0130 off for `src/SqlSource/Polyfills/` | 1 |
| `src/SqlSource/SqlSource.csproj` (modify) | `InternalsVisibleTo` for the test assembly | 1 |
| `src/SqlSource/Polyfills/IsExternalInit.cs` | Lets records compile on `netstandard2.0` | 1 |
| `src/SqlSource/EquatableArray.cs` | Immutable array with value equality | 1 |
| `src/SqlSource/Parsing/SqlSegmentKind.cs`, `SqlSegment.cs` | A piece of a block's SQL: literal or token | 1 |
| `src/SqlSource/Parsing/SqlBlock.cs` | One named query | 1 |
| `src/SqlSource/Parsing/SqlParseErrorKind.cs`, `SqlParseError.cs` | A problem and where it is | 1 |
| `src/SqlSource/Parsing/SqlFileParseResult.cs` | Blocks or errors for one file | 1 |
| `src/SqlSource/Parsing/SqlLexemeKind.cs`, `SqlLexeme.cs`, `SqlLexResult.cs` | The lexer's output | 2 |
| `src/SqlSource/Parsing/SqlLexer.cs` | Text to lexemes | 2 |
| `src/SqlSource/Parsing/SqlIdentifier.cs` | The C# identifier rules for names | 3 |
| `src/SqlSource/Parsing/TokenScanResult.cs`, `TokenScanner.cs` | Cleaned SQL to segments | 3 |
| `src/SqlSource/Parsing/SqlMarkerKind.cs`, `SqlMarker.cs`, `SqlMarkerReader.cs` | Recognises `-- name:`, `-- summary:`, `-- SqlSource:` | 4 |
| `src/SqlSource/Parsing/SqlDirectiveScope.cs` | Reads the directives of one scope | 4 |
| `src/SqlSource/Parsing/SqlBlockText.cs`, `SqlTextBuilder.cs` | A block's lexemes to its cleaned SQL, with a map back to file offsets | 5 |
| `src/SqlSource/Parsing/SqlFileParser.cs` | The entry point: blocks, preamble, names, emptiness, errors | 6 |
| `src/SqlSource/AGENTS.md` (modify) | Polyfill and model guidance (Task 1), parser constraints (Task 6) | 1, 6 |
| `tests/SqlSource.Tests/EquatableArrayTests.cs` | | 1 |
| `tests/SqlSource.Tests/Parsing/SqlModelTests.cs` | | 1 |
| `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs` | | 2 |
| `tests/SqlSource.Tests/Parsing/TokenScannerTests.cs` | | 3 |
| `tests/SqlSource.Tests/Parsing/SqlMarkerReaderTests.cs`, `SqlDirectiveScopeTests.cs` | | 4 |
| `tests/SqlSource.Tests/Parsing/SqlTextBuilderTests.cs` | | 5 |
| `tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs` | | 6 |

The spec names three units: `SqlLexer`, `SqlFileParser` and `TokenScanner`.  This plan splits the work `SqlFileParser` would otherwise hold into `SqlMarkerReader`, `SqlDirectiveScope` and `SqlTextBuilder`, so that each file has one job and its own tests.  `EquatableArray<T>` sits in `src/SqlSource/`, not `Parsing/`, because phase 2 uses it for its own pipeline models.

## Commands used throughout

| Purpose | Command |
|----|----|
| Build with every analyzer | `dotnet build SqlSource.slnx` |
| Run one test class | `dotnet test --project tests/SqlSource.Tests --no-build --filter-class "<full class name>"` |
| Run every test | `dotnet test --solution SqlSource.slnx --no-build` |
| Format C# | `./format.sh` |
| Every check | `./pre-commit-validation.sh` |

A passing test run ends with `Test run summary: Passed!` and the counts `total`, `failed`, `succeeded`, `skipped`.  A passing validation ends with a summary in which all seven steps (`format`, `editorconfig-checker`, `shellcheck`, `shfmt`, `actionlint`, `build`, `test`) read `passed`.

---

### Task 1: Foundation - records, value equality and the model

**Files:**
- Modify: `.editorconfig`
- Modify: `src/SqlSource/SqlSource.csproj`
- Modify: `src/SqlSource/AGENTS.md`
- Create: `src/SqlSource/Polyfills/IsExternalInit.cs`
- Create: `src/SqlSource/EquatableArray.cs`
- Create: `src/SqlSource/Parsing/SqlSegmentKind.cs`
- Create: `src/SqlSource/Parsing/SqlSegment.cs`
- Create: `src/SqlSource/Parsing/SqlBlock.cs`
- Create: `src/SqlSource/Parsing/SqlParseErrorKind.cs`
- Create: `src/SqlSource/Parsing/SqlParseError.cs`
- Create: `src/SqlSource/Parsing/SqlFileParseResult.cs`
- Test: `tests/SqlSource.Tests/EquatableArrayTests.cs`
- Test: `tests/SqlSource.Tests/Parsing/SqlModelTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `SqlSource.EquatableArray<T>` where `T : IEquatable<T>`: `new EquatableArray<T>(ImmutableArray<T> items)`, `static EquatableArray<T> Empty`, `int Count`, `T this[int index]`, value `Equals`/`GetHashCode`/`==`/`!=`, `IReadOnlyList<T>`.  A `default` instance behaves as empty.
  - `enum SqlSegmentKind { Literal, Token }` and `record SqlSegment(SqlSegmentKind Kind, string Text)`.
  - `record SqlBlock(string Name, TextSpan NameSpan, string? Summary, bool PreserveComments, bool? TokenValidation, EquatableArray<SqlSegment> Segments)`.
  - `enum SqlParseErrorKind` with the thirteen members listed in the file below.
  - `record SqlParseError(SqlParseErrorKind Kind, TextSpan Span, EquatableArray<string> Arguments)` and `static SqlParseError Create(SqlParseErrorKind kind, TextSpan span, params string[] arguments)`.
  - `record SqlFileParseResult(EquatableArray<SqlBlock> Blocks, EquatableArray<SqlParseError> Errors)`.
  - The test assembly can see the internals of `SqlSource`.

- [ ] **Step 1: Write the failing tests**

Create `tests/SqlSource.Tests/EquatableArrayTests.cs`:

```csharp
using System.Collections.Immutable;
using System.Linq;
using Shouldly;
using Xunit;

namespace SqlSource.Tests;

public class EquatableArrayTests
{
    [Fact]
    public void Equals_SameItemsInSeparateInstances_ReturnsTrue()
    {
        var left = Of("a", "b");
        var right = Of("a", "b");

        left.Equals(right).ShouldBeTrue();
        (left == right).ShouldBeTrue();
        (left != right).ShouldBeFalse();
        left.Equals((object)right).ShouldBeTrue();
        left.Equals("a").ShouldBeFalse();
    }

    [Fact]
    public void Equals_DifferentItems_ReturnsFalse()
    {
        Of("a", "b").Equals(Of("a", "c")).ShouldBeFalse();
        Of("a").Equals(Of("a", "b")).ShouldBeFalse();
        (Of("a") != Of("b")).ShouldBeTrue();
    }

    [Fact]
    public void GetHashCode_SameItemsInSeparateInstances_IsTheSame() =>
        Of("a", "b").GetHashCode().ShouldBe(Of("a", "b").GetHashCode());

    [Fact]
    public void Default_BehavesAsEmpty()
    {
        var array = default(EquatableArray<string>);

        array.Count.ShouldBe(0);
        array.ShouldBeEmpty();
        array.Equals(EquatableArray<string>.Empty).ShouldBeTrue();
        array.GetHashCode().ShouldBe(EquatableArray<string>.Empty.GetHashCode());
    }

    [Fact]
    public void Indexer_ReturnsItemsInOrder()
    {
        var array = Of("a", "b");

        array.Count.ShouldBe(2);
        array[0].ShouldBe("a");
        array[1].ShouldBe("b");
        array.ToArray().ShouldBe(["a", "b"]);
        array.ShouldBe(["a", "b"]);
    }

    private static EquatableArray<string> Of(params string[] items) => new(ImmutableArray.Create(items));
}
```

Create `tests/SqlSource.Tests/Parsing/SqlModelTests.cs`:

```csharp
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class SqlModelTests
{
    [Fact]
    public void SqlParseError_Create_HoldsItsArguments()
    {
        var error = SqlParseError.Create(SqlParseErrorKind.InvalidName, new TextSpan(3, 4), "1abc");

        error.Kind.ShouldBe(SqlParseErrorKind.InvalidName);
        error.Span.ShouldBe(new TextSpan(3, 4));
        error.Arguments.ShouldBe(["1abc"]);
    }

    [Fact]
    public void SqlParseError_SameValuesInSeparateInstances_AreEqual()
    {
        var left = SqlParseError.Create(SqlParseErrorKind.DuplicateName, new TextSpan(1, 2), "A");
        var right = SqlParseError.Create(SqlParseErrorKind.DuplicateName, new TextSpan(1, 2), "A");

        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
        left.ShouldNotBe(SqlParseError.Create(SqlParseErrorKind.DuplicateName, new TextSpan(1, 2), "B"));
    }

    [Fact]
    public void SqlBlock_SameValuesInSeparateInstances_AreEqual()
    {
        static SqlBlock Create(string sql) =>
            new(
                "GetUser",
                new TextSpan(9, 7),
                "Loads a user.",
                PreserveComments: false,
                TokenValidation: null,
                new EquatableArray<SqlSegment>([
                    new SqlSegment(SqlSegmentKind.Literal, sql),
                    new SqlSegment(SqlSegmentKind.Token, "table"),
                ])
            );

        Create("SELECT 1 FROM ").ShouldBe(Create("SELECT 1 FROM "));
        Create("SELECT 1 FROM ").ShouldNotBe(Create("SELECT 2 FROM "));
    }
}
```

- [ ] **Step 2: Run the build to verify it fails**

Run: `dotnet build SqlSource.slnx`
Expected: the build fails with `CS0246` for `EquatableArray<>` and `CS0234` for the namespace `SqlSource.Parsing`.

- [ ] **Step 3: Let the tests see the generator's internals**

In `src/SqlSource/SqlSource.csproj`, add this item group immediately before the item group that holds the two `PackageReference` items:

```xml
    <ItemGroup>
        <InternalsVisibleTo Include="SqlSource.Tests" />
    </ItemGroup>
```

- [ ] **Step 4: Add the `IsExternalInit` polyfill**

A record's positional properties have `init` accessors, which need the type `System.Runtime.CompilerServices.IsExternalInit`.  `netstandard2.0` does not define it.

Create `src/SqlSource/Polyfills/IsExternalInit.cs`:

```csharp
using System.ComponentModel;

namespace System.Runtime.CompilerServices;

/// <summary>
/// Lets the compiler emit <see langword="init" /> accessors, and so records, on <c>netstandard2.0</c>, which does not
/// define this type.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class IsExternalInit;
```

The file's namespace cannot match its folder, which IDE0130 requires.  Append this section to the end of `.editorconfig`, after the `[tests/**.cs]` section, with one blank line before it:

```ini
[src/SqlSource/Polyfills/*.cs]
# IDE0130 (namespace matches folder): a polyfill must be in the namespace of the type it stands in for.
dotnet_diagnostic.IDE0130.severity = none
```

- [ ] **Step 5: Add `EquatableArray<T>`**

`ImmutableArray<T>` compares by reference to its backing array, so a record that holds one never equals another record built from the same text.

Create `src/SqlSource/EquatableArray.cs`:

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace SqlSource;

/// <summary>
/// An immutable array that compares by its items.  A model that holds one has value equality, which the incremental
/// generator pipeline needs in order to cache it.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
internal readonly struct EquatableArray<T>(ImmutableArray<T> items) : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
    where T : IEquatable<T>
{
    public static EquatableArray<T> Empty => new(ImmutableArray<T>.Empty);

    public int Count => Items.Length;

    // A default instance holds a default ImmutableArray, which throws on use.
    private ImmutableArray<T> Items => items.IsDefault ? ImmutableArray<T>.Empty : items;

    public T this[int index] => Items[index];

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);

    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);

    public bool Equals(EquatableArray<T> other) => Items.AsSpan().SequenceEqual(other.Items.AsSpan());

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var item in Items)
        {
            hash = unchecked((hash * 31) + item.GetHashCode());
        }

        return hash;
    }

    public ImmutableArray<T>.Enumerator GetEnumerator() => Items.GetEnumerator();

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)Items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Items).GetEnumerator();
}
```

- [ ] **Step 6: Add the model**

Create `src/SqlSource/Parsing/SqlSegmentKind.cs`:

```csharp
namespace SqlSource.Parsing;

/// <summary>
/// What a <see cref="SqlSegment" /> holds.
/// </summary>
internal enum SqlSegmentKind
{
    /// <summary>SQL text that is emitted as written.</summary>
    Literal,

    /// <summary>A <c>{{name}}</c> token.</summary>
    Token,
}
```

Create `src/SqlSource/Parsing/SqlSegment.cs`:

```csharp
namespace SqlSource.Parsing;

/// <summary>
/// One piece of a block's SQL: literal text, or a token to be replaced.
/// </summary>
/// <param name="Kind">Whether this is literal text or a token.</param>
/// <param name="Text">The literal text, or the token's name without its braces.</param>
internal sealed record SqlSegment(SqlSegmentKind Kind, string Text);
```

Create `src/SqlSource/Parsing/SqlBlock.cs`:

```csharp
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// One named query from a <c>.sql</c> file.
/// </summary>
/// <param name="Name">The query's name, a valid C# identifier.</param>
/// <param name="NameSpan">
/// Where the name is in the file.  An empty span at the start of the file when the name comes from the file name.
/// </param>
/// <param name="Summary">The text of the block's <c>-- summary:</c> markers, or null when it has none.</param>
/// <param name="PreserveComments">Whether comments were kept in the SQL.</param>
/// <param name="TokenValidation">
/// True or false when a validation directive applies to the block, null when none does.
/// </param>
/// <param name="Segments">The SQL, split into literal text and tokens.  Never empty.</param>
internal sealed record SqlBlock(
    string Name,
    TextSpan NameSpan,
    string? Summary,
    bool PreserveComments,
    bool? TokenValidation,
    EquatableArray<SqlSegment> Segments
);
```

Create `src/SqlSource/Parsing/SqlParseErrorKind.cs`:

```csharp
namespace SqlSource.Parsing;

/// <summary>
/// The problems the parser reports.
/// </summary>
internal enum SqlParseErrorKind
{
    UnterminatedQuote,
    UnterminatedBlockComment,
    InvalidName,
    DuplicateName,
    InvalidFileName,
    SqlBeforeFirstName,
    SummaryBeforeFirstName,
    UnknownDirective,
    EmptyDirectiveLine,
    InvalidDirectiveValue,
    ConflictingDirectives,
    EmptyBlock,
    ReservedTokenName,
}
```

Create `src/SqlSource/Parsing/SqlParseError.cs`:

```csharp
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// A problem found in a <c>.sql</c> file.
/// </summary>
/// <param name="Kind">What is wrong.</param>
/// <param name="Span">Where it is, as offsets into the file's text.</param>
/// <param name="Arguments">The text the message quotes, such as the offending name.  Empty for most kinds.</param>
internal sealed record SqlParseError(SqlParseErrorKind Kind, TextSpan Span, EquatableArray<string> Arguments)
{
    public static SqlParseError Create(SqlParseErrorKind kind, TextSpan span, params string[] arguments) =>
        new(kind, span, new EquatableArray<string>(ImmutableArray.Create(arguments)));
}
```

Create `src/SqlSource/Parsing/SqlFileParseResult.cs`:

```csharp
namespace SqlSource.Parsing;

/// <summary>
/// The outcome of parsing one <c>.sql</c> file.
/// </summary>
/// <param name="Blocks">The file's queries.  Empty when <paramref name="Errors" /> is not.</param>
/// <param name="Errors">The problems found, ordered by position.</param>
internal sealed record SqlFileParseResult(EquatableArray<SqlBlock> Blocks, EquatableArray<SqlParseError> Errors);
```

- [ ] **Step 7: Update the agent guidance**

In `src/SqlSource/AGENTS.md`, replace this bullet:

```markdown
- **Language features need runtime support.**  `LangVersion` is `preview`, but `netstandard2.0` lacks the types behind some features (`init` accessors, `required` members, positional records).  Using one needs a polyfill; none is set up yet.
```

with these three:

```markdown
- **Language features need runtime support.**  `LangVersion` is `preview`, but `netstandard2.0` lacks the types behind some features.  `Polyfills/IsExternalInit.cs` supplies the one behind `init` accessors and records.  Any other feature (`required` members, for example) needs its own polyfill in `Polyfills/`, declared in the namespace of the type it stands in for; `.editorconfig` turns IDE0130 off for that folder.
- **No collection expression may target `ImmutableArray<T>`.**  The `System.Collections.Immutable` that Roslyn 4.8.0 brings predates support for it (CS9210).  Use `ImmutableArray.Create`, a builder or `ToImmutableArray()`.
- **Models need value equality.**  A type the generator pipeline will cache is a record, and holds its collections in `EquatableArray<T>`.  `ImmutableArray<T>` compares by reference.
```

- [ ] **Step 8: Format and build**

Run: `./format.sh`
Expected: ends with a line of the form `Formatted <n> files in <t>ms.` and exit code 0.  `git status --short` shows only the files this task names.

Run: `dotnet build SqlSource.slnx`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

- [ ] **Step 9: Run the tests to verify they pass**

Run: `dotnet test --solution SqlSource.slnx --no-build`
Expected: `Test run summary: Passed!` with `total: 9`, `failed: 0`.  The nine are the existing smoke test, five in `EquatableArrayTests` and three in `SqlModelTests`.

- [ ] **Step 10: Validate**

Run: `./pre-commit-validation.sh`
Expected: all seven steps `passed`.

- [ ] **Step 11: Commit**

```bash
git add .editorconfig src/SqlSource tests/SqlSource.Tests
```

```bash
git commit -m "Add the parser model, EquatableArray and the IsExternalInit polyfill" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Lexer

**Files:**
- Create: `src/SqlSource/Parsing/SqlLexemeKind.cs`
- Create: `src/SqlSource/Parsing/SqlLexeme.cs`
- Create: `src/SqlSource/Parsing/SqlLexResult.cs`
- Create: `src/SqlSource/Parsing/SqlLexer.cs`
- Test: `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs`

**Interfaces:**
- Consumes: `EquatableArray<T>`, `SqlParseError.Create`, `SqlParseErrorKind.UnterminatedQuote`, `SqlParseErrorKind.UnterminatedBlockComment` (Task 1).
- Produces:
  - `enum SqlLexemeKind { Text, Quoted, LineComment, BlockComment, Hint }`.
  - `readonly record struct SqlLexeme(SqlLexemeKind Kind, TextSpan Span)` with `TextSpan? GetContentSpan(string text)`: the lexeme's span for `Quoted` and `Hint`, the whitespace-trimmed span for `Text` or null when it is all whitespace, null for comments.
  - `record SqlLexResult(EquatableArray<SqlLexeme> Lexemes, SqlParseError? Error)`.  When `Error` is set, `Lexemes` is empty.
  - `static SqlLexResult SqlLexer.Lex(string text)`.

**Rules the later tasks rely on:**
- The lexemes cover the text without gaps or overlaps, in order.  No lexeme is empty.
- A `LineComment` stops before its line terminator, which belongs to the `Text` lexeme that follows.
- A `Quoted` lexeme starts at its opening quote.  The `E` of `E'...'` stays in the preceding `Text`.
- A `Quoted`, `BlockComment` or `Hint` lexeme always ends with its closing delimiter, never with whitespace.

- [ ] **Step 1: Write the failing tests**

Create `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs`:

```csharp
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class SqlLexerTests
{
    [Fact]
    public void Lex_EmptyText_ReturnsNoLexemes() => Lex(string.Empty).ShouldBeEmpty();

    [Fact]
    public void Lex_PlainSql_ReturnsOneTextLexeme() => Lex("SELECT 1").ShouldBe(["Text:SELECT 1"]);

    [Theory]
    [InlineData("a -- c\nb", "Text:\nb")]
    [InlineData("a -- c\r\nb", "Text:\r\nb")]
    [InlineData("a -- c\rb", "Text:\rb")]
    public void Lex_LineComment_StopsBeforeTheLineTerminator(string text, string rest) =>
        Lex(text).ShouldBe(["Text:a ", "LineComment:-- c", rest]);

    [Fact]
    public void Lex_LineCommentAtEndOfText_RunsToTheEnd() => Lex("a --c").ShouldBe(["Text:a ", "LineComment:--c"]);

    [Theory]
    [InlineData("'it''s' x", "Quoted:'it''s'")]
    [InlineData("\"a\"\"b\" x", "Quoted:\"a\"\"b\"")]
    [InlineData("`a``b` x", "Quoted:`a``b`")]
    [InlineData("'' x", "Quoted:''")]
    public void Lex_QuotedRegion_EndsAtTheUndoubledQuote(string text, string quoted) =>
        Lex(text).ShouldBe([quoted, "Text: x"]);

    [Theory]
    [InlineData("'-- x /* y */' z", "Quoted:'-- x /* y */'")]
    [InlineData("\"-- x /* y */\" z", "Quoted:\"-- x /* y */\"")]
    [InlineData("`-- x` z", "Quoted:`-- x`")]
    [InlineData("'a\n-- b\n' z", "Quoted:'a\n-- b\n'")]
    public void Lex_CommentSyntaxInsideQuotedRegion_IsNotAComment(string text, string quoted) =>
        Lex(text).ShouldBe([quoted, "Text: z"]);

    [Theory]
    [InlineData("E'it\\'s' -- c", "Text:E")]
    [InlineData("e'it\\'s' -- c", "Text:e")]
    public void Lex_EscapeString_TreatsBackslashAsAnEscape(string text, string prefix) =>
        Lex(text).ShouldBe([prefix, "Quoted:'it\\'s'", "Text: ", "LineComment:-- c"]);

    [Fact]
    public void Lex_EscapeStringWithDoubledQuote_EndsAtTheUndoubledQuote() =>
        Lex("E'a''b\\\\' x").ShouldBe(["Text:E", "Quoted:'a''b\\\\'", "Text: x"]);

    [Fact]
    public void Lex_LetterEAtTheEndOfAnIdentifier_IsNotAnEscapeStringPrefix() =>
        Lex("typeE'a\\' -- c").ShouldBe(["Text:typeE", "Quoted:'a\\'", "Text: ", "LineComment:-- c"]);

    [Theory]
    [InlineData("$$ -- x $$ y", "Quoted:$$ -- x $$")]
    [InlineData("$fn$ /* x */ $fn$ y", "Quoted:$fn$ /* x */ $fn$")]
    [InlineData("$a$ $b$ -- c $a$ y", "Quoted:$a$ $b$ -- c $a$")]
    [InlineData("$_t1$ ' $_t1$ y", "Quoted:$_t1$ ' $_t1$")]
    [InlineData("$größe$ -- x $größe$ y", "Quoted:$größe$ -- x $größe$")]
    public void Lex_DollarQuote_EndsAtTheSameTag(string text, string quoted) => Lex(text).ShouldBe([quoted, "Text: y"]);

    [Theory]
    [InlineData("v$session$ -- c", "Text:v$session$ ")]
    [InlineData("$1 -- c", "Text:$1 ")]
    [InlineData("$5.00 -- c", "Text:$5.00 ")]
    [InlineData("$$ -- c", "Text:$$ ")]
    [InlineData("$a$ -- c", "Text:$a$ ")]
    [InlineData("$a$ $b$ -- c", "Text:$a$ $b$ ")]
    public void Lex_DollarThatOpensNoQuote_IsText(string text, string expectedText) =>
        Lex(text).ShouldBe([expectedText, "LineComment:-- c"]);

    [Theory]
    [InlineData("/* a */ x", "BlockComment:/* a */")]
    [InlineData("/**/ x", "BlockComment:/**/")]
    [InlineData("/* a\n b */ x", "BlockComment:/* a\n b */")]
    [InlineData("/* a /* b */ c */ x", "BlockComment:/* a /* b */ c */")]
    [InlineData("/* it's -- \"x */ x", "BlockComment:/* it's -- \"x */")]
    [InlineData("/* /*+ h */ */ x", "BlockComment:/* /*+ h */ */")]
    public void Lex_BlockComment_EndsAtItsMatchingClose(string text, string comment) =>
        Lex(text).ShouldBe([comment, "Text: x"]);

    [Theory]
    [InlineData("/*+ INDEX(t) */ x", "Hint:/*+ INDEX(t) */")]
    [InlineData("/*! STRAIGHT_JOIN */ x", "Hint:/*! STRAIGHT_JOIN */")]
    [InlineData("/*+*/ x", "Hint:/*+*/")]
    public void Lex_BlockCommentStartingWithPlusOrBang_IsAHint(string text, string hint) =>
        Lex(text).ShouldBe([hint, "Text: x"]);

    [Theory]
    [InlineData("-- /* x\ny", "LineComment:-- /* x")]
    [InlineData("-- it's\ny", "LineComment:-- it's")]
    [InlineData("-- $$\ny", "LineComment:-- $$")]
    public void Lex_QuoteOrCommentSyntaxInsideLineComment_IsPartOfTheComment(string text, string comment) =>
        Lex(text).ShouldBe([comment, "Text:\ny"]);

    // The Lex_KnownLimit tests pin where the lexer reads SQL differently from some databases.  Each is deliberate.
    [Theory]
    [InlineData("# x -- y", "Text:# x ", "LineComment:-- y")]
    [InlineData("[a--b]", "Text:[a", "LineComment:--b]")]
    [InlineData("5--3", "Text:5", "LineComment:--3")]
    public void Lex_KnownLimit_FollowsAnsiRules(string text, string first, string second) =>
        Lex(text).ShouldBe([first, second]);

    [Fact]
    public void Lex_KnownLimit_BackslashInPlainStringIsNotAnEscape()
    {
        var result = SqlLexer.Lex("'a\\'b'");

        result.Error.ShouldBe(SqlParseError.Create(SqlParseErrorKind.UnterminatedQuote, new TextSpan(5, 1)));
    }

    [Fact]
    public void Lex_KnownLimit_CommentOpenerInsideCommentMustBeClosed()
    {
        var result = SqlLexer.Lex("/* a /* b */ SELECT 1");

        result.Error.ShouldBe(SqlParseError.Create(SqlParseErrorKind.UnterminatedBlockComment, new TextSpan(0, 2)));
    }

    [Theory]
    [InlineData("'abc", 0)]
    [InlineData("x \"abc", 2)]
    [InlineData("`abc", 0)]
    [InlineData("'abc''", 0)]
    [InlineData("E'abc\\'", 1)]
    [InlineData("E'abc\\", 1)]
    public void Lex_UnterminatedQuote_ReportsTheOpeningQuote(string text, int position)
    {
        var result = SqlLexer.Lex(text);

        result.Error.ShouldBe(SqlParseError.Create(SqlParseErrorKind.UnterminatedQuote, new TextSpan(position, 1)));
        result.Lexemes.Count.ShouldBe(0);
    }

    [Theory]
    [InlineData("SELECT /* x", 7)]
    [InlineData("/*+ x", 0)]
    [InlineData("/*/", 0)]
    [InlineData("/* /* x */", 0)]
    public void Lex_UnterminatedBlockComment_ReportsTheOpener(string text, int position)
    {
        var result = SqlLexer.Lex(text);

        result.Error.ShouldBe(
            SqlParseError.Create(SqlParseErrorKind.UnterminatedBlockComment, new TextSpan(position, 2))
        );
        result.Lexemes.Count.ShouldBe(0);
    }

    [Fact]
    public void Lex_AnyText_CoversItWithoutGaps()
    {
        const string Text = "SELECT 'a', \"b\" /* c */ -- d\r\nFROM $$e$$ /*+ f */ `g`\n";

        var lexemes = SqlLexer.Lex(Text).Lexemes;

        var position = 0;
        foreach (var lexeme in lexemes)
        {
            lexeme.Span.Start.ShouldBe(position);
            lexeme.Span.Length.ShouldBeGreaterThan(0);
            position = lexeme.Span.End;
        }

        position.ShouldBe(Text.Length);
    }

    [Theory]
    [InlineData("-")]
    [InlineData("x -")]
    [InlineData("/")]
    [InlineData("$")]
    [InlineData("a$")]
    [InlineData("$a")]
    [InlineData("E")]
    [InlineData("*")]
    public void Lex_TextEndingWhereAConstructCouldStart_IsText(string text) => Lex(text).ShouldBe(["Text:" + text]);

    [Fact]
    public void Lex_NonAsciiText_IsKeptIntact() =>
        Lex("SELECT 'é😀' -- ñ").ShouldBe(["Text:SELECT ", "Quoted:'é😀'", "Text: ", "LineComment:-- ñ"]);

    [Fact]
    public void Lex_DeeplyNestedBlockComments_IsOneComment()
    {
        const int Depth = 100_000;
        var text = string.Concat(Enumerable.Repeat("/*", Depth)) + string.Concat(Enumerable.Repeat("*/", Depth));

        var result = SqlLexer.Lex(text);

        result.Error.ShouldBeNull();
        result.Lexemes.Count.ShouldBe(1);
        result.Lexemes[0].ShouldBe(new SqlLexeme(SqlLexemeKind.BlockComment, new TextSpan(0, text.Length)));
    }

    [Theory]
    [InlineData("  SELECT 1 \n", 2, 8)]
    [InlineData("'a'", 0, 3)]
    [InlineData("/*+ h */", 0, 8)]
    public void GetContentSpan_LexemeWithContent_ReturnsItWithoutSurroundingWhitespace(
        string text,
        int start,
        int length
    ) => SqlLexer.Lex(text).Lexemes[0].GetContentSpan(text).ShouldBe(new TextSpan(start, length));

    [Theory]
    [InlineData(" \r\n\t ")]
    [InlineData("-- c")]
    [InlineData("/* c */")]
    public void GetContentSpan_WhitespaceOrComment_ReturnsNull(string text) =>
        SqlLexer.Lex(text).Lexemes[0].GetContentSpan(text).ShouldBeNull();

    private static string[] Lex(string text)
    {
        var result = SqlLexer.Lex(text);
        result.Error.ShouldBeNull();
        return
        [
            .. result.Lexemes.Select(lexeme =>
                $"{lexeme.Kind}:{text.Substring(lexeme.Span.Start, lexeme.Span.Length)}"
            ),
        ];
    }
}
```

- [ ] **Step 2: Run the build to verify it fails**

Run: `dotnet build SqlSource.slnx`
Expected: the build fails with `CS0103` for `SqlLexer` and `SqlLexemeKind` and `CS0246` for `SqlLexeme`.

- [ ] **Step 3: Add the lexer's output types**

Create `src/SqlSource/Parsing/SqlLexemeKind.cs`:

```csharp
namespace SqlSource.Parsing;

/// <summary>
/// The kinds of lexeme the <see cref="SqlLexer" /> produces.
/// </summary>
internal enum SqlLexemeKind
{
    /// <summary>Anything that is not one of the other kinds.</summary>
    Text,

    /// <summary>A string literal or a quoted identifier, delimiters included.</summary>
    Quoted,

    /// <summary><c>--</c> to the end of the line, without the line terminator.</summary>
    LineComment,

    /// <summary><c>/* ... */</c>, with any comments nested in it.</summary>
    BlockComment,

    /// <summary>A block comment that starts <c>/*+</c> or <c>/*!</c>.  Never stripped.</summary>
    Hint,
}
```

Create `src/SqlSource/Parsing/SqlLexeme.cs`:

```csharp
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// A run of a <c>.sql</c> file's text with one meaning.  The lexemes of a file cover it without gaps.
/// </summary>
/// <param name="Kind">What the text is.</param>
/// <param name="Span">Where it is, as offsets into the file's text.</param>
internal readonly record struct SqlLexeme(SqlLexemeKind Kind, TextSpan Span)
{
    /// <summary>
    /// Returns the part of this lexeme that counts as SQL content, or null when it has none.  Comments and whitespace
    /// are not content.
    /// </summary>
    public TextSpan? GetContentSpan(string text)
    {
        if (Kind is SqlLexemeKind.Quoted or SqlLexemeKind.Hint)
        {
            return Span;
        }

        if (Kind != SqlLexemeKind.Text)
        {
            return null;
        }

        var start = Span.Start;
        var end = Span.End;
        while (start < end && char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        while (end > start && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        return start == end ? null : TextSpan.FromBounds(start, end);
    }
}
```

Create `src/SqlSource/Parsing/SqlLexResult.cs`:

```csharp
namespace SqlSource.Parsing;

/// <summary>
/// The outcome of lexing one <c>.sql</c> file.
/// </summary>
/// <param name="Lexemes">The file's lexemes in order.  Empty when <paramref name="Error" /> is set.</param>
/// <param name="Error">The unterminated quote or comment that stopped the lexer, or null.</param>
internal sealed record SqlLexResult(EquatableArray<SqlLexeme> Lexemes, SqlParseError? Error);
```

- [ ] **Step 4: Add the lexer**

The scanner keeps two positions: where it is reading, and where the pending run of plain text began.  Finding a comment or a quoted region first flushes the pending text as a `Text` lexeme.  Nesting is counted in a loop, not by recursion.

Create `src/SqlSource/Parsing/SqlLexer.cs`:

```csharp
using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// Splits SQL text into quoted regions, comments, hints and plain text.
/// </summary>
/// <remarks>
/// The rules are ANSI SQL plus the quoting forms of PostgreSQL and MySQL that cannot be mistaken for anything else.
/// Where dialects disagree the lexer picks the reading that keeps text, because a comment left in the SQL is harmless
/// and SQL removed from it is a bug.  The dialect-sensitive choices are all in this file: block comments nest, a
/// backslash escapes only inside <c>E'...'</c>, <c>--</c> needs no whitespace after it, and <c>#</c> is not a comment.
/// </remarks>
internal static class SqlLexer
{
    private static readonly char[] LineTerminators = ['\r', '\n'];

    public static SqlLexResult Lex(string text) => new Scanner(text).Run();

    private sealed class Scanner(string text)
    {
        private readonly ImmutableArray<SqlLexeme>.Builder _lexemes = ImmutableArray.CreateBuilder<SqlLexeme>();
        private SqlParseError? _error;
        private int _position;
        private int _textStart;

        public SqlLexResult Run()
        {
            while (_position < text.Length && _error is null)
            {
                Step();
            }

            if (_error is not null)
            {
                return new SqlLexResult(EquatableArray<SqlLexeme>.Empty, _error);
            }

            AddPendingText(text.Length);
            return new SqlLexResult(new EquatableArray<SqlLexeme>(_lexemes.ToImmutable()), null);
        }

        private void Step()
        {
            var start = _position;
            var current = text[start];
            if (current == '-' && CharAt(start + 1) == '-')
            {
                Add(SqlLexemeKind.LineComment, start, FindLineEnd(start));
            }
            else if (current == '/' && CharAt(start + 1) == '*')
            {
                ReadBlockComment(start);
            }
            else if (current is '\'' or '"' or '`')
            {
                ReadQuoted(start);
            }
            else if (current == '$' && TryFindDollarQuoteEnd(start, out var end))
            {
                Add(SqlLexemeKind.Quoted, start, end);
            }
            else
            {
                _position++;
            }
        }

        private static bool IsIdentifierCharacter(char value) => char.IsLetterOrDigit(value) || value is '_' or '$';

        private char CharAt(int index) => index < text.Length ? text[index] : '\0';

        private void Add(SqlLexemeKind kind, int start, int end)
        {
            AddPendingText(start);
            _lexemes.Add(new SqlLexeme(kind, TextSpan.FromBounds(start, end)));
            _textStart = end;
            _position = end;
        }

        private void AddPendingText(int end)
        {
            if (end > _textStart)
            {
                _lexemes.Add(new SqlLexeme(SqlLexemeKind.Text, TextSpan.FromBounds(_textStart, end)));
            }
        }

        private int FindLineEnd(int start)
        {
            var index = text.IndexOfAny(LineTerminators, start);
            return index < 0 ? text.Length : index;
        }

        private void ReadBlockComment(int start)
        {
            var depth = 1;
            var index = start + 2;
            while (index < text.Length && depth > 0)
            {
                if (text[index] == '/' && CharAt(index + 1) == '*')
                {
                    depth++;
                    index += 2;
                }
                else if (text[index] == '*' && CharAt(index + 1) == '/')
                {
                    depth--;
                    index += 2;
                }
                else
                {
                    index++;
                }
            }

            if (depth > 0)
            {
                _error = SqlParseError.Create(SqlParseErrorKind.UnterminatedBlockComment, new TextSpan(start, 2));
                return;
            }

            var kind = CharAt(start + 2) is '+' or '!' ? SqlLexemeKind.Hint : SqlLexemeKind.BlockComment;
            Add(kind, start, index);
        }

        private void ReadQuoted(int start)
        {
            var quote = text[start];
            var backslashEscapes = quote == '\'' && IsEscapeStringPrefix(start);
            var index = start + 1;
            while (index < text.Length)
            {
                var current = text[index];
                if (backslashEscapes && current == '\\')
                {
                    index += 2;
                }
                else if (current != quote)
                {
                    index++;
                }
                else if (CharAt(index + 1) == quote)
                {
                    index += 2;
                }
                else
                {
                    Add(SqlLexemeKind.Quoted, start, index + 1);
                    return;
                }
            }

            _error = SqlParseError.Create(SqlParseErrorKind.UnterminatedQuote, new TextSpan(start, 1));
        }

        // PostgreSQL's E'...' form.  The E stays in the text before the quoted region.
        private bool IsEscapeStringPrefix(int quoteIndex) =>
            quoteIndex > 0
            && (text[quoteIndex - 1] is 'E' or 'e')
            && (quoteIndex < 2 || !IsIdentifierCharacter(text[quoteIndex - 2]));

        // PostgreSQL's $tag$...$tag$ form.  An opener with no closer is plain text: $ has other meanings elsewhere.
        private bool TryFindDollarQuoteEnd(int start, out int end)
        {
            end = 0;
            if (start > 0 && IsIdentifierCharacter(text[start - 1]))
            {
                return false;
            }

            var tagEnd = start + 1;
            if (char.IsLetter(CharAt(tagEnd)) || CharAt(tagEnd) == '_')
            {
                while (char.IsLetterOrDigit(CharAt(tagEnd)) || CharAt(tagEnd) == '_')
                {
                    tagEnd++;
                }
            }

            if (CharAt(tagEnd) != '$')
            {
                return false;
            }

            var delimiter = text.Substring(start, tagEnd - start + 1);
            var close = text.IndexOf(delimiter, tagEnd + 1, StringComparison.Ordinal);
            if (close < 0)
            {
                return false;
            }

            end = close + delimiter.Length;
            return true;
        }
    }
}
```

- [ ] **Step 5: Format and build**

Run: `./format.sh`
Expected: exit code 0.

Run: `dotnet build SqlSource.slnx`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test --project tests/SqlSource.Tests --no-build --filter-class "SqlSource.Tests.Parsing.SqlLexerTests"`
Expected: `Test run summary: Passed!` with `total: 73`, `failed: 0`.

- [ ] **Step 7: Validate**

Run: `./pre-commit-validation.sh`
Expected: all seven steps `passed`.  The test step reports `total: 82`.

- [ ] **Step 8: Commit**

```bash
git add src/SqlSource/Parsing tests/SqlSource.Tests/Parsing
```

```bash
git commit -m "Add the SQL lexer" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Token scanner

**Files:**
- Create: `src/SqlSource/Parsing/SqlIdentifier.cs`
- Create: `src/SqlSource/Parsing/TokenScanResult.cs`
- Create: `src/SqlSource/Parsing/TokenScanner.cs`
- Test: `tests/SqlSource.Tests/Parsing/TokenScannerTests.cs`

**Interfaces:**
- Consumes: `EquatableArray<T>`, `SqlSegment`, `SqlSegmentKind`, `SqlParseError.Create`, `SqlParseErrorKind.ReservedTokenName` (Task 1).
- Produces:
  - `static class SqlIdentifier`: `bool IsValid(string value)` (identifier form; keywords pass), `bool IsReservedKeyword(string value)`, `bool IsUsableName(string value)` (valid and not reserved).
  - `record TokenScanResult(EquatableArray<SqlSegment> Segments, EquatableArray<SqlParseError> Errors)`.  The error spans are offsets into the scanned SQL, not into the file.
  - `static TokenScanResult TokenScanner.Scan(string sql, ISet<string> ignoredNames)`.

- [ ] **Step 1: Write the failing tests**

Create `tests/SqlSource.Tests/Parsing/TokenScannerTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class TokenScannerTests
{
    [Fact]
    public void Scan_EmptySql_ReturnsNoSegments() => Scan(string.Empty).ShouldBeEmpty();

    [Fact]
    public void Scan_SqlWithoutTokens_ReturnsOneLiteral() => Scan("SELECT 1").ShouldBe(["L:SELECT 1"]);

    [Fact]
    public void Scan_TokenInTheMiddle_SplitsTheSql() =>
        Scan("SELECT * FROM {{table}} WHERE x").ShouldBe(["L:SELECT * FROM ", "T:table", "L: WHERE x"]);

    [Theory]
    [InlineData("{{a}}", new[] { "T:a" })]
    [InlineData("{{a}} x {{b}}", new[] { "T:a", "L: x ", "T:b" })]
    [InlineData("{{a}}{{b}}", new[] { "T:a", "T:b" })]
    public void Scan_TokenAtAnEdge_AddsNoEmptyLiteral(string sql, string[] expected) => Scan(sql).ShouldBe(expected);

    [Fact]
    public void Scan_RepeatedToken_RecordsEachOccurrenceInOrder() =>
        Scan("{{a}} {{b}} {{a}}").ShouldBe(["T:a", "L: ", "T:b", "L: ", "T:a"]);

    [Theory]
    [InlineData("{{ a }}")]
    [InlineData("{{\ta\t}}")]
    [InlineData("{{a  }}")]
    [InlineData("{{  a}}")]
    public void Scan_BlanksInsideTheBraces_AreIgnored(string sql) => Scan(sql).ShouldBe(["T:a"]);

    [Theory]
    [InlineData("{{1,2},{3,4}}")]
    [InlineData("{{table name}}")]
    [InlineData("{{}}")]
    [InlineData("{{ }}")]
    [InlineData("{{1abc}}")]
    [InlineData("{{a.b}}")]
    [InlineData("{{a-b}}")]
    [InlineData("{{\na}}")]
    [InlineData("{{a\n}}")]
    [InlineData("{name}")]
    [InlineData("{ {name}}")]
    [InlineData("{{name}")]
    [InlineData("{{name} }")]
    public void Scan_TextThatIsNotExactlyAToken_IsLiteral(string sql) => Scan(sql).ShouldBe(["L:" + sql]);

    [Fact]
    public void Scan_TokenInsideExtraBraces_IsStillAToken() => Scan("{{{name}}}").ShouldBe(["L:{", "T:name", "L:}"]);

    [Fact]
    public void Scan_NamesThatDifferByCase_AreDifferentTokens() =>
        Scan("{{Table}} {{table}}").ShouldBe(["T:Table", "L: ", "T:table"]);

    [Theory]
    [InlineData("{{where}}", "T:where")]
    [InlineData("{{var}}", "T:var")]
    [InlineData("{{_x1}}", "T:_x1")]
    [InlineData("{{größe}}", "T:größe")]
    public void Scan_ContextualKeywordOrUnusualIdentifier_IsAToken(string sql, string expected) =>
        Scan(sql).ShouldBe([expected]);

    [Fact]
    public void Scan_IgnoredToken_StaysLiteralExactlyAsWritten() =>
        Scan("a {{ skip }} b {{keep}}", "skip").ShouldBe(["L:a {{ skip }} b ", "T:keep"]);

    [Fact]
    public void Scan_IgnoredName_IsCaseSensitive() => Scan("{{Skip}}", "skip").ShouldBe(["T:Skip"]);

    [Fact]
    public void Scan_ReservedKeywordToken_IsAnError()
    {
        var result = TokenScanner.Scan("SELECT {{ class }} x", new HashSet<string>());

        result.Errors.ShouldBe([
            SqlParseError.Create(SqlParseErrorKind.ReservedTokenName, new TextSpan(7, 11), "class"),
        ]);
    }

    [Fact]
    public void Scan_ReservedKeywordTokenThatIsIgnored_IsLiteral() =>
        Scan("SELECT {{class}}", "class").ShouldBe(["L:SELECT {{class}}"]);

    [Theory]
    [InlineData("x {{")]
    [InlineData("x {{a")]
    [InlineData("x {{a}")]
    [InlineData("x {{ ")]
    [InlineData("{")]
    public void Scan_SqlEndingInsideAToken_IsLiteral(string sql) => Scan(sql).ShouldBe(["L:" + sql]);

    private static string[] Scan(string sql, params string[] ignoredNames)
    {
        var result = TokenScanner.Scan(sql, new HashSet<string>(ignoredNames));
        result.Errors.ShouldBeEmpty();
        return
        [
            .. result.Segments.Select(segment => (segment.Kind == SqlSegmentKind.Token ? "T:" : "L:") + segment.Text),
        ];
    }
}
```

- [ ] **Step 2: Run the build to verify it fails**

Run: `dotnet build SqlSource.slnx`
Expected: the build fails with `CS0103` for `TokenScanner`.

- [ ] **Step 3: Add the identifier rules**

Create `src/SqlSource/Parsing/SqlIdentifier.cs`:

```csharp
using Microsoft.CodeAnalysis.CSharp;

namespace SqlSource.Parsing;

/// <summary>
/// The C# identifier rules that query names and token names must meet.
/// </summary>
internal static class SqlIdentifier
{
    /// <summary>True when <paramref name="value" /> has the form of an identifier.  Keywords pass.</summary>
    public static bool IsValid(string value) => SyntaxFacts.IsValidIdentifier(value);

    /// <summary>
    /// True for a reserved keyword such as <c>class</c>.  A contextual keyword such as <c>where</c> is not one.
    /// </summary>
    public static bool IsReservedKeyword(string value) => SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None;

    /// <summary>True when <paramref name="value" /> can be the name of a generated member or parameter.</summary>
    public static bool IsUsableName(string value) => IsValid(value) && !IsReservedKeyword(value);
}
```

- [ ] **Step 4: Add the scanner**

A `{{` that is not followed by blanks, an identifier, blanks and `}}` is literal text, which keeps PostgreSQL array literals such as `'{{1,2},{3,4}}'` working.  The ignore check comes before the reserved-keyword check.

Create `src/SqlSource/Parsing/TokenScanResult.cs`:

```csharp
namespace SqlSource.Parsing;

/// <summary>
/// The outcome of scanning a block's SQL for tokens.
/// </summary>
/// <param name="Segments">The SQL as literal text and tokens, in order.</param>
/// <param name="Errors">Problems found.  Their spans are offsets into the scanned SQL, not into the file.</param>
internal sealed record TokenScanResult(EquatableArray<SqlSegment> Segments, EquatableArray<SqlParseError> Errors);
```

Create `src/SqlSource/Parsing/TokenScanner.cs`:

```csharp
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// Splits a block's SQL into literal text and <c>{{name}}</c> tokens.
/// </summary>
internal static class TokenScanner
{
    /// <summary>
    /// Scans <paramref name="sql" />.  A token whose name is in <paramref name="ignoredNames" /> stays literal text.
    /// </summary>
    public static TokenScanResult Scan(string sql, ISet<string> ignoredNames)
    {
        var segments = ImmutableArray.CreateBuilder<SqlSegment>();
        var errors = ImmutableArray.CreateBuilder<SqlParseError>();
        var literalStart = 0;
        var index = 0;
        while (index < sql.Length)
        {
            if (!TryReadToken(sql, index, out var name, out var end))
            {
                index++;
            }
            else if (ignoredNames.Contains(name))
            {
                index = end;
            }
            else if (SqlIdentifier.IsReservedKeyword(name))
            {
                var span = TextSpan.FromBounds(index, end);
                errors.Add(SqlParseError.Create(SqlParseErrorKind.ReservedTokenName, span, name));
                index = end;
            }
            else
            {
                AddLiteral(segments, sql, literalStart, index);
                segments.Add(new SqlSegment(SqlSegmentKind.Token, name));
                index = end;
                literalStart = end;
            }
        }

        AddLiteral(segments, sql, literalStart, sql.Length);
        return new TokenScanResult(
            new EquatableArray<SqlSegment>(segments.ToImmutable()),
            new EquatableArray<SqlParseError>(errors.ToImmutable())
        );
    }

    private static void AddLiteral(ImmutableArray<SqlSegment>.Builder segments, string sql, int start, int end)
    {
        if (end > start)
        {
            segments.Add(new SqlSegment(SqlSegmentKind.Literal, sql.Substring(start, end - start)));
        }
    }

    // A token is "{{", blanks, an identifier, blanks, "}}".  Anything else that starts with "{{" is literal text.
    private static bool TryReadToken(string sql, int start, out string name, out int end)
    {
        name = string.Empty;
        end = 0;
        if (CharAt(sql, start) != '{' || CharAt(sql, start + 1) != '{')
        {
            return false;
        }

        var nameStart = SkipBlanks(sql, start + 2);
        var nameEnd = nameStart;
        while (SyntaxFacts.IsIdentifierPartCharacter(CharAt(sql, nameEnd)))
        {
            nameEnd++;
        }

        var close = SkipBlanks(sql, nameEnd);
        if (CharAt(sql, close) != '}' || CharAt(sql, close + 1) != '}')
        {
            return false;
        }

        name = sql.Substring(nameStart, nameEnd - nameStart);
        end = close + 2;
        return SqlIdentifier.IsValid(name);
    }

    private static int SkipBlanks(string sql, int index)
    {
        while (CharAt(sql, index) is ' ' or '\t')
        {
            index++;
        }

        return index;
    }

    private static char CharAt(string sql, int index) => index < sql.Length ? sql[index] : '\0';
}
```

- [ ] **Step 5: Format and build**

Run: `./format.sh`
Expected: exit code 0.

Run: `dotnet build SqlSource.slnx`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test --project tests/SqlSource.Tests --no-build --filter-class "SqlSource.Tests.Parsing.TokenScannerTests"`
Expected: `Test run summary: Passed!` with `total: 39`, `failed: 0`.

- [ ] **Step 7: Validate**

Run: `./pre-commit-validation.sh`
Expected: all seven steps `passed`.  The test step reports `total: 121`.

- [ ] **Step 8: Commit**

```bash
git add src/SqlSource/Parsing tests/SqlSource.Tests/Parsing
```

```bash
git commit -m "Add the token scanner" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Markers and directives

**Files:**
- Create: `src/SqlSource/Parsing/SqlMarkerKind.cs`
- Create: `src/SqlSource/Parsing/SqlMarker.cs`
- Create: `src/SqlSource/Parsing/SqlMarkerReader.cs`
- Create: `src/SqlSource/Parsing/SqlDirectiveScope.cs`
- Test: `tests/SqlSource.Tests/Parsing/SqlMarkerReaderTests.cs`
- Test: `tests/SqlSource.Tests/Parsing/SqlDirectiveScopeTests.cs`

**Interfaces:**
- Consumes: `SqlLexeme`, `SqlLexemeKind`, `SqlLexer.Lex` (Task 2); `SqlIdentifier.IsValid` (Task 3); `SqlParseError.Create` and the kinds `UnknownDirective`, `EmptyDirectiveLine`, `InvalidDirectiveValue`, `ConflictingDirectives` (Task 1).
- Produces:
  - `enum SqlMarkerKind { Name, Summary, Directives }`.
  - `readonly record struct SqlMarker(SqlMarkerKind Kind, TextSpan Span, TextSpan ValueSpan)`.  `Span` is the whole comment.  `ValueSpan` is the text after the colon without surrounding whitespace, and may be empty.
  - `static SqlMarker? SqlMarkerReader.Read(string text, SqlLexeme lexeme)`.
  - `sealed class SqlDirectiveScope`: `bool PreserveComments`, `bool? TokenValidation`, `HashSet<string> IgnoredTokens`, and `void Read(string text, SqlMarker marker, List<SqlParseError> errors)`, which applies one `-- SqlSource:` marker.  Every error it adds has the directive as written as its one argument, except `EmptyDirectiveLine`, which has none.

- [ ] **Step 1: Write the failing tests**

Create `tests/SqlSource.Tests/Parsing/SqlMarkerReaderTests.cs`:

```csharp
using System;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class SqlMarkerReaderTests
{
    [Theory]
    [InlineData("-- name: GetUser", "Name:GetUser")]
    [InlineData("--name:GetUser", "Name:GetUser")]
    [InlineData("-- NAME: GetUser", "Name:GetUser")]
    [InlineData("--\t Name:\tGetUser  ", "Name:GetUser")]
    [InlineData("-- summary: Loads a user.", "Summary:Loads a user.")]
    [InlineData("-- Summary:Loads: a -- user", "Summary:Loads: a -- user")]
    [InlineData("-- SqlSource: preserve-comments  token-ignore=a", "Directives:preserve-comments  token-ignore=a")]
    [InlineData("-- SQLSOURCE: x", "Directives:x")]
    [InlineData("-- name:", "Name:")]
    [InlineData("-- name:   ", "Name:")]
    public void Read_MarkerComment_ReturnsItsKindAndValue(string text, string expected) =>
        Markers(text).ShouldBe([expected]);

    [Theory]
    [InlineData("  \t-- name: A")]
    [InlineData("SELECT 1\n-- name: A")]
    [InlineData("SELECT 1\r\n-- name: A")]
    [InlineData("SELECT 1\r-- name: A")]
    [InlineData("SELECT 1\n\t  -- name: A\nSELECT 2")]
    [InlineData("/* x */\n-- name: A")]
    [InlineData("'x'\n-- name: A")]
    public void Read_CommentThatStartsItsLine_IsAMarker(string text) => Markers(text).ShouldBe(["Name:A"]);

    [Theory]
    [InlineData("SELECT 1 -- name: A")]
    [InlineData("/* x */ -- name: A")]
    [InlineData("'x' -- name: A")]
    [InlineData("/*+ h */-- name: A")]
    [InlineData("-- name : A")]
    [InlineData("-- names: A")]
    [InlineData("-- rename: A")]
    [InlineData("-- name A")]
    [InlineData("-- -- name: A")]
    [InlineData("- - name: A")]
    [InlineData("--")]
    [InlineData("-- name")]
    [InlineData("-- a comment")]
    [InlineData("/*\n-- name: A\n*/")]
    [InlineData("'\n-- name: A\n'")]
    [InlineData("$$\n-- name: A\n$$")]
    public void Read_AnythingElse_IsNotAMarker(string text) => Markers(text).ShouldBeEmpty();

    [Fact]
    public void Read_Marker_ReportsTheCommentAndTheTrimmedValue()
    {
        const string Text = "SELECT 1\n  -- name:  GetUser  \nSELECT 2";
        var lexeme = SqlLexer.Lex(Text).Lexemes[1];

        var marker = SqlMarkerReader.Read(Text, lexeme);

        _ = marker.ShouldNotBeNull();
        marker.Value.Span.ShouldBe(lexeme.Span);
        marker.Value.ValueSpan.ShouldBe(new TextSpan(Text.IndexOf("GetUser", StringComparison.Ordinal), 7));
    }

    [Fact]
    public void Read_MarkerWithoutAValue_HasAnEmptyValueSpan()
    {
        const string Text = "-- name:  ";

        var marker = SqlMarkerReader.Read(Text, SqlLexer.Lex(Text).Lexemes[0]);

        _ = marker.ShouldNotBeNull();
        marker.Value.ValueSpan.IsEmpty.ShouldBeTrue();
    }

    private static string[] Markers(string text)
    {
        var lexed = SqlLexer.Lex(text);
        lexed.Error.ShouldBeNull();
        return
        [
            .. lexed
                .Lexemes.Select(lexeme => SqlMarkerReader.Read(text, lexeme))
                .OfType<SqlMarker>()
                .Select(marker => $"{marker.Kind}:{text.Substring(marker.ValueSpan.Start, marker.ValueSpan.Length)}"),
        ];
    }
}
```

Create `tests/SqlSource.Tests/Parsing/SqlDirectiveScopeTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class SqlDirectiveScopeTests
{
    [Fact]
    public void NewScope_HasNoDirectives()
    {
        var scope = new SqlDirectiveScope();

        scope.PreserveComments.ShouldBeFalse();
        scope.TokenValidation.ShouldBeNull();
        scope.IgnoredTokens.ShouldBeEmpty();
    }

    [Fact]
    public void Read_PreserveComments_SetsTheFlag()
    {
        var (scope, errors) = Read("-- SqlSource: preserve-comments");

        errors.ShouldBeEmpty();
        scope.PreserveComments.ShouldBeTrue();
        scope.TokenValidation.ShouldBeNull();
    }

    [Theory]
    [InlineData("-- SqlSource: token-validation", true)]
    [InlineData("-- SqlSource: no-token-validation", false)]
    public void Read_ValidationDirective_SetsTokenValidation(string line, bool expected)
    {
        var (scope, errors) = Read(line);

        errors.ShouldBeEmpty();
        scope.TokenValidation.ShouldBe(expected);
    }

    [Fact]
    public void Read_DirectiveNames_AreCaseInsensitive()
    {
        var (scope, errors) = Read("-- SqlSource: PRESERVE-COMMENTS No-Token-Validation Token-Ignore=a");

        errors.ShouldBeEmpty();
        scope.PreserveComments.ShouldBeTrue();
        scope.TokenValidation.ShouldBe(false);
        scope.IgnoredTokens.ShouldBe(["a"]);
    }

    [Fact]
    public void Read_SeveralDirectivesOnOneLine_AppliesEach()
    {
        var (scope, errors) = Read("-- SqlSource: preserve-comments   token-ignore=a\ttoken-ignore=b");

        errors.ShouldBeEmpty();
        scope.PreserveComments.ShouldBeTrue();
        scope.IgnoredTokens.ShouldBe(["a", "b"], ignoreOrder: true);
    }

    [Fact]
    public void Read_SeveralMarkers_Accumulate()
    {
        var (scope, errors) = Read(
            "-- SqlSource: preserve-comments",
            "-- SqlSource: token-validation",
            "-- SqlSource: token-ignore=a"
        );

        errors.ShouldBeEmpty();
        scope.PreserveComments.ShouldBeTrue();
        scope.TokenValidation.ShouldBe(true);
        scope.IgnoredTokens.ShouldBe(["a"]);
    }

    [Fact]
    public void Read_RepeatedDirective_IsAllowed()
    {
        var (scope, errors) = Read(
            "-- SqlSource: preserve-comments preserve-comments token-validation token-ignore=a",
            "-- SqlSource: token-validation token-ignore=a"
        );

        errors.ShouldBeEmpty();
        scope.TokenValidation.ShouldBe(true);
        scope.IgnoredTokens.ShouldBe(["a"]);
    }

    [Fact]
    public void Read_TokenIgnore_KeepsTheNameAsWrittenAndAcceptsAKeyword()
    {
        var (scope, errors) = Read("-- SqlSource: token-ignore=Table token-ignore=class");

        errors.ShouldBeEmpty();
        scope.IgnoredTokens.ShouldBe(["Table", "class"], ignoreOrder: true);
    }

    [Theory]
    [InlineData("preserve-comment")]
    [InlineData("strip-comments")]
    [InlineData("foo=bar")]
    [InlineData("=x")]
    public void Read_UnknownDirective_IsAnError(string directive)
    {
        var line = "-- SqlSource: " + directive;

        var (_, errors) = Read(line);

        errors.ShouldBe([SqlParseError.Create(SqlParseErrorKind.UnknownDirective, SpanOf(line, directive), directive)]);
    }

    [Theory]
    [InlineData("-- SqlSource:")]
    [InlineData("-- SqlSource:   ")]
    public void Read_MarkerWithoutDirectives_IsAnError(string line)
    {
        var (_, errors) = Read(line);

        errors.ShouldBe([SqlParseError.Create(SqlParseErrorKind.EmptyDirectiveLine, new TextSpan(0, line.Length))]);
    }

    [Theory]
    [InlineData("token-ignore")]
    [InlineData("token-ignore=")]
    [InlineData("token-ignore=1x")]
    [InlineData("token-ignore=a=b")]
    [InlineData("token-ignore=a,b")]
    [InlineData("preserve-comments=x")]
    [InlineData("token-validation=true")]
    [InlineData("no-token-validation=")]
    public void Read_MissingOrUnexpectedValue_IsAnError(string directive)
    {
        var line = "-- SqlSource: " + directive;

        var (scope, errors) = Read(line);

        errors.ShouldBe([
            SqlParseError.Create(SqlParseErrorKind.InvalidDirectiveValue, SpanOf(line, directive), directive),
        ]);
        scope.PreserveComments.ShouldBeFalse();
        scope.TokenValidation.ShouldBeNull();
        scope.IgnoredTokens.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("token-validation", "no-token-validation", true)]
    [InlineData("no-token-validation", "token-validation", false)]
    public void Read_BothValidationDirectivesOnOneLine_ReportsTheSecond(string first, string second, bool kept)
    {
        var line = $"-- SqlSource: {first} {second}";

        var (scope, errors) = Read(line);

        errors.ShouldBe([
            SqlParseError.Create(
                SqlParseErrorKind.ConflictingDirectives,
                new TextSpan(line.Length - second.Length, second.Length),
                second
            ),
        ]);
        scope.TokenValidation.ShouldBe(kept);
    }

    [Fact]
    public void Read_BothValidationDirectivesOnSeparateLines_IsAnError()
    {
        var (_, errors) = Read("-- SqlSource: token-validation", "-- SqlSource: no-token-validation");

        errors.Count.ShouldBe(1);
        errors[0].Kind.ShouldBe(SqlParseErrorKind.ConflictingDirectives);
    }

    [Fact]
    public void Read_ErrorInOneDirective_StillAppliesTheOthers()
    {
        var (scope, errors) = Read("-- SqlSource: bogus preserve-comments");

        errors.Count.ShouldBe(1);
        scope.PreserveComments.ShouldBeTrue();
    }

    private static (SqlDirectiveScope Scope, List<SqlParseError> Errors) Read(params string[] lines)
    {
        var scope = new SqlDirectiveScope();
        var errors = new List<SqlParseError>();
        foreach (var line in lines)
        {
            var marker = SqlMarkerReader.Read(line, SqlLexer.Lex(line).Lexemes[0]);
            _ = marker.ShouldNotBeNull();
            scope.Read(line, marker.Value, errors);
        }

        return (scope, errors);
    }

    private static TextSpan SpanOf(string text, string value) =>
        new(text.LastIndexOf(value, StringComparison.Ordinal), value.Length);
}
```

- [ ] **Step 2: Run the build to verify it fails**

Run: `dotnet build SqlSource.slnx`
Expected: the build fails with errors that name the missing types, among them `CS0246` for `SqlDirectiveScope`.

- [ ] **Step 3: Add the marker reader**

A marker is a line comment that is the first thing on its line.  The reader decides that from the text alone: it walks back over blanks from the comment and must reach a line terminator or the start of the text.  That is safe because of the lexer's guarantee that a quoted region or block comment ends with its delimiter, so a line break reached this way is never inside one.

Create `src/SqlSource/Parsing/SqlMarkerKind.cs`:

```csharp
namespace SqlSource.Parsing;

/// <summary>
/// The marker comments the parser gives meaning to.
/// </summary>
internal enum SqlMarkerKind
{
    /// <summary><c>-- name:</c> starts a block.</summary>
    Name,

    /// <summary><c>-- summary:</c> documents a block.</summary>
    Summary,

    /// <summary><c>-- SqlSource:</c> carries directives.</summary>
    Directives,
}
```

Create `src/SqlSource/Parsing/SqlMarker.cs`:

```csharp
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// A marker comment found in a <c>.sql</c> file.
/// </summary>
/// <param name="Kind">Which marker it is.</param>
/// <param name="Span">The whole comment.</param>
/// <param name="ValueSpan">The text after the colon, without surrounding whitespace.  May be empty.</param>
internal readonly record struct SqlMarker(SqlMarkerKind Kind, TextSpan Span, TextSpan ValueSpan);
```

Create `src/SqlSource/Parsing/SqlMarkerReader.cs`:

```csharp
using System;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// Recognises marker comments.
/// </summary>
internal static class SqlMarkerReader
{
    private static readonly (string Keyword, SqlMarkerKind Kind)[] Keywords =
    [
        ("name:", SqlMarkerKind.Name),
        ("summary:", SqlMarkerKind.Summary),
        ("sqlsource:", SqlMarkerKind.Directives),
    ];

    /// <summary>
    /// Returns the marker that <paramref name="lexeme" /> is, or null.  A marker is a line comment that is the first
    /// thing on its line and reads <c>--</c>, optional blanks, a keyword, a colon.
    /// </summary>
    public static SqlMarker? Read(string text, SqlLexeme lexeme)
    {
        if (lexeme.Kind != SqlLexemeKind.LineComment || !StartsLine(text, lexeme.Span.Start))
        {
            return null;
        }

        var end = lexeme.Span.End;
        var keywordStart = lexeme.Span.Start + 2;
        while (keywordStart < end && text[keywordStart] is ' ' or '\t')
        {
            keywordStart++;
        }

        foreach (var (keyword, kind) in Keywords)
        {
            var valueStart = keywordStart + keyword.Length;
            if (
                valueStart <= end
                && string.Compare(text, keywordStart, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase)
                    == 0
            )
            {
                return new SqlMarker(kind, lexeme.Span, Trim(text, valueStart, end));
            }
        }

        return null;
    }

    // Only blanks may come before the comment on its line.  The lexer guarantees that a line break found this way is
    // not inside a quoted region or a block comment: those end with their closing delimiter, never with a blank.
    private static bool StartsLine(string text, int start)
    {
        var index = start - 1;
        while (index >= 0 && text[index] is ' ' or '\t')
        {
            index--;
        }

        return index < 0 || text[index] is '\n' or '\r';
    }

    private static TextSpan Trim(string text, int start, int end)
    {
        while (start < end && char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        while (end > start && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        return TextSpan.FromBounds(start, end);
    }
}
```

- [ ] **Step 4: Add the directive scope**

One scope is one block, or a file's preamble.  A conflict is detected within a scope; how a block's scope combines with the preamble's is the parser's job in Task 6.

Create `src/SqlSource/Parsing/SqlDirectiveScope.cs`:

```csharp
using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// The directives given by the <c>-- SqlSource:</c> markers of one scope: a block, or a file's preamble.
/// </summary>
internal sealed class SqlDirectiveScope
{
    private const string PreserveCommentsName = "preserve-comments";
    private const string TokenValidationName = "token-validation";
    private const string NoTokenValidationName = "no-token-validation";
    private const string TokenIgnoreName = "token-ignore";

    public bool PreserveComments { get; private set; }

    public bool? TokenValidation { get; private set; }

    public HashSet<string> IgnoredTokens { get; } = [];

    /// <summary>
    /// Applies the directives of one <c>-- SqlSource:</c> marker to this scope, adding any problems to
    /// <paramref name="errors" />.
    /// </summary>
    public void Read(string text, SqlMarker marker, List<SqlParseError> errors)
    {
        if (marker.ValueSpan.IsEmpty)
        {
            errors.Add(SqlParseError.Create(SqlParseErrorKind.EmptyDirectiveLine, marker.Span));
            return;
        }

        var start = marker.ValueSpan.Start;
        var end = marker.ValueSpan.End;
        while (start < end)
        {
            var wordEnd = start;
            while (wordEnd < end && !char.IsWhiteSpace(text[wordEnd]))
            {
                wordEnd++;
            }

            Apply(text.Substring(start, wordEnd - start), TextSpan.FromBounds(start, wordEnd), errors);
            start = wordEnd;
            while (start < end && char.IsWhiteSpace(text[start]))
            {
                start++;
            }
        }
    }

    private static bool Is(string name, string directive) =>
        string.Equals(name, directive, StringComparison.OrdinalIgnoreCase);

    private void Apply(string directive, TextSpan span, List<SqlParseError> errors)
    {
        var separator = directive.IndexOf('=');
        var name = separator < 0 ? directive : directive.Substring(0, separator);
        var value = separator < 0 ? null : directive.Substring(separator + 1);
        SqlParseErrorKind? problem;
        if (Is(name, TokenIgnoreName))
        {
            problem = ApplyTokenIgnore(value);
        }
        else if (Is(name, PreserveCommentsName) || Is(name, TokenValidationName) || Is(name, NoTokenValidationName))
        {
            problem = value is null ? ApplyFlag(name) : SqlParseErrorKind.InvalidDirectiveValue;
        }
        else
        {
            problem = SqlParseErrorKind.UnknownDirective;
        }

        if (problem is { } kind)
        {
            errors.Add(SqlParseError.Create(kind, span, directive));
        }
    }

    private SqlParseErrorKind? ApplyTokenIgnore(string? value)
    {
        if (value is null || !SqlIdentifier.IsValid(value))
        {
            return SqlParseErrorKind.InvalidDirectiveValue;
        }

        _ = IgnoredTokens.Add(value);
        return null;
    }

    private SqlParseErrorKind? ApplyFlag(string name)
    {
        if (Is(name, PreserveCommentsName))
        {
            PreserveComments = true;
            return null;
        }

        var validate = Is(name, TokenValidationName);
        if (TokenValidation is { } existing && existing != validate)
        {
            return SqlParseErrorKind.ConflictingDirectives;
        }

        TokenValidation = validate;
        return null;
    }
}
```

- [ ] **Step 5: Format and build**

Run: `./format.sh`
Expected: exit code 0.

Run: `dotnet build SqlSource.slnx`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test --project tests/SqlSource.Tests --no-build --filter-class "SqlSource.Tests.Parsing.SqlMarkerReaderTests"`
Expected: `Test run summary: Passed!` with `total: 35`, `failed: 0`.

Run: `dotnet test --project tests/SqlSource.Tests --no-build --filter-class "SqlSource.Tests.Parsing.SqlDirectiveScopeTests"`
Expected: `Test run summary: Passed!` with `total: 27`, `failed: 0`.

- [ ] **Step 7: Validate**

Run: `./pre-commit-validation.sh`
Expected: all seven steps `passed`.  The test step reports `total: 183`.

- [ ] **Step 8: Commit**

```bash
git add src/SqlSource/Parsing tests/SqlSource.Tests/Parsing
```

```bash
git commit -m "Add marker recognition and directive parsing" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Text builder

**Files:**
- Create: `src/SqlSource/Parsing/SqlBlockText.cs`
- Create: `src/SqlSource/Parsing/SqlTextBuilder.cs`
- Test: `tests/SqlSource.Tests/Parsing/SqlTextBuilderTests.cs`

**Interfaces:**
- Consumes: `EquatableArray<SqlLexeme>`, `SqlLexeme`, `SqlLexemeKind`, `SqlLexer.Lex` (Task 2); `SqlMarkerReader.Read` (Task 4).
- Produces:
  - `record SqlBlockText(string Text, ImmutableArray<int> Offsets)` with `TextSpan ToSourceSpan(TextSpan span)`.  `Offsets[i]` is the file offset of `Text[i]`.  `ToSourceSpan` is valid for a span that lies on one line of `Text`.
  - `static SqlBlockText SqlTextBuilder.Build(string text, EquatableArray<SqlLexeme> lexemes, int start, int end, bool preserveComments)`, which builds the SQL of the lexemes at indexes `start` up to, not including, `end`.

**How it works:**

The builder writes output lines.  Plain text, hints and kept comments are split at line terminators, each ending the current line.  A quoted region is appended to the current line whole, with its line terminators turned into `\n` inside the line.  Clean-up then works on lines: trailing blanks are trimmed, marker lines are dropped, and blank lines are removed (all of them when stripping, only the leading and trailing ones when preserving).  Because a quoted region never ends a line, the clean-up cannot reach inside a string literal.  A parallel list records each output character's offset in the file, which is how Task 6 reports a bad token at its position in the `.sql` file.

- [ ] **Step 1: Write the failing tests**

Create `tests/SqlSource.Tests/Parsing/SqlTextBuilderTests.cs`:

```csharp
using System;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class SqlTextBuilderTests
{
    [Theory]
    [InlineData("SELECT 1 -- c", "SELECT 1")]
    [InlineData("SELECT 1 -- c\nFROM t -- d", "SELECT 1\nFROM t")]
    [InlineData("SELECT 1\n-- c\nFROM t", "SELECT 1\nFROM t")]
    [InlineData("-- c\nSELECT 1\n  -- d", "SELECT 1")]
    public void Build_Stripping_DeletesLineComments(string text, string expected) => Build(text).ShouldBe(expected);

    [Theory]
    [InlineData("SELECT/**/1", "SELECT 1")]
    [InlineData("SELECT a /* x\n y */ FROM t", "SELECT a   FROM t")]
    [InlineData("/* c */\nSELECT 1", "SELECT 1")]
    [InlineData("SELECT 1 /* a /* b */ c */", "SELECT 1")]
    public void Build_Stripping_ReplacesABlockCommentWithOneSpace(string text, string expected) =>
        Build(text).ShouldBe(expected);

    [Theory]
    [InlineData("SELECT 1\n\n  \n\t\nFROM t", "SELECT 1\nFROM t")]
    [InlineData("\n\nSELECT 1\n\n", "SELECT 1")]
    [InlineData("SELECT 1\n/* a */ -- b\nFROM t", "SELECT 1\nFROM t")]
    public void Build_Stripping_RemovesBlankLines(string text, string expected) => Build(text).ShouldBe(expected);

    [Theory]
    [InlineData("-- c\n/* d */")]
    [InlineData("")]
    [InlineData(" \n ")]
    public void Build_NothingButCommentsAndWhitespace_IsEmpty(string text) => Build(text).ShouldBeEmpty();

    [Theory]
    [InlineData("SELECT /*+ H */ 1 /*! M */")]
    [InlineData("SELECT '-- x', \"/* y */\", `-- z` FROM t")]
    [InlineData("SELECT $$ -- x $$")]
    [InlineData("  SELECT 1\n    FROM t")]
    [InlineData("SELECT  1   FROM\tt")]
    public void Build_Stripping_KeepsHintsQuotedRegionsAndSpacing(string text) => Build(text).ShouldBe(text);

    [Theory]
    [InlineData("SELECT 1\r\nFROM t\r\n", "SELECT 1\nFROM t")]
    [InlineData("SELECT 1\rFROM t\r", "SELECT 1\nFROM t")]
    [InlineData("SELECT 'a\r\nb', 'c\rd'", "SELECT 'a\nb', 'c\nd'")]
    [InlineData("SELECT 1\r\n-- summary: x\r\nFROM t", "SELECT 1\nFROM t")]
    public void Build_AnyLineTerminator_BecomesLineFeed(string text, string expected) => Build(text).ShouldBe(expected);

    [Theory]
    [InlineData("SELECT 1   \nFROM t\t", "SELECT 1\nFROM t")]
    [InlineData("SELECT 'a  \n\n  b'  \n", "SELECT 'a  \n\n  b'")]
    [InlineData("/*+ a  \n b */", "/*+ a\n b */")]
    public void Build_TrailingBlanks_AreRemovedUnlessTheLineEndsInsideAQuotedRegion(string text, string expected) =>
        Build(text).ShouldBe(expected);

    [Theory]
    [InlineData(
        "-- summary: x\nSELECT 1\n  -- SqlSource: preserve-comments\nFROM t\n-- name: Next",
        "SELECT 1\nFROM t"
    )]
    [InlineData("SELECT 1\n-- summary: x   ", "SELECT 1")]
    [InlineData("\t-- summary: x\nSELECT 1", "SELECT 1")]
    public void Build_MarkerLines_AreRemoved(string text, string expected)
    {
        Build(text).ShouldBe(expected);
        Build(text, preserveComments: true).ShouldBe(expected);
    }

    [Theory]
    [InlineData("SELECT 1 -- c\n/* b */\nFROM t")]
    [InlineData("-- c\nSELECT 1\n\nFROM t")]
    [InlineData("/* a\n\n b */ SELECT 1")]
    public void Build_Preserving_KeepsCommentsAndInnerBlankLines(string text) =>
        Build(text, preserveComments: true).ShouldBe(text);

    [Theory]
    [InlineData("\n\nSELECT 1\n\nFROM t\n\n", "SELECT 1\n\nFROM t")]
    [InlineData("SELECT 1   \n-- c  ", "SELECT 1\n-- c")]
    [InlineData("/* a\r\n b */\r\nSELECT 1", "/* a\n b */\nSELECT 1")]
    [InlineData("-- summary: x\n-- keep\nSELECT 1\n-- SqlSource: preserve-comments", "-- keep\nSELECT 1")]
    [InlineData("SELECT 1\n-- summary: x\nFROM t", "SELECT 1\nFROM t")]
    public void Build_Preserving_StillCleansLineEndsAndOuterBlankLines(string text, string expected) =>
        Build(text, preserveComments: true).ShouldBe(expected);

    [Fact]
    public void Build_NonAsciiText_IsKeptIntact() => Build("SELECT 'ñ😀' -- é").ShouldBe("SELECT 'ñ😀'");

    [Fact]
    public void Build_LexemeRange_UsesOnlyThatRange()
    {
        const string Text = "A\n-- name: X\nB\n";
        var lexemes = SqlLexer.Lex(Text).Lexemes;

        SqlTextBuilder.Build(Text, lexemes, 0, 1, preserveComments: false).Text.ShouldBe("A");
        SqlTextBuilder.Build(Text, lexemes, 2, 3, preserveComments: false).Text.ShouldBe("B");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ToSourceSpan_SpanInBuiltText_MapsToTheSameTextInTheFile(bool preserveComments)
    {
        const string Text = "-- c\r\n/* x */ SELECT {{a}} -- d\r\n\r\n-- summary: s\r\n  FROM {{b}}";
        var lexemes = SqlLexer.Lex(Text).Lexemes;

        var built = SqlTextBuilder.Build(Text, lexemes, 0, lexemes.Count, preserveComments);

        built.Offsets.Length.ShouldBe(built.Text.Length);
        foreach (var token in new[] { "{{a}}", "{{b}}" })
        {
            var inBuilt = new TextSpan(built.Text.IndexOf(token, StringComparison.Ordinal), token.Length);
            var inFile = new TextSpan(Text.IndexOf(token, StringComparison.Ordinal), token.Length);
            built.ToSourceSpan(inBuilt).ShouldBe(inFile);
        }
    }

    private static string Build(string text, bool preserveComments = false)
    {
        var lexed = SqlLexer.Lex(text);
        lexed.Error.ShouldBeNull();
        return SqlTextBuilder.Build(text, lexed.Lexemes, 0, lexed.Lexemes.Count, preserveComments).Text;
    }
}
```

- [ ] **Step 2: Run the build to verify it fails**

Run: `dotnet build SqlSource.slnx`
Expected: the build fails with `CS0103` for `SqlTextBuilder`.

- [ ] **Step 3: Add the builder**

Create `src/SqlSource/Parsing/SqlBlockText.cs`:

```csharp
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// The SQL of one block after comment handling and whitespace clean-up.
/// </summary>
/// <param name="Text">The SQL.</param>
/// <param name="Offsets">For each character of <paramref name="Text" />, its offset in the file's text.</param>
internal sealed record SqlBlockText(string Text, ImmutableArray<int> Offsets)
{
    /// <summary>
    /// Converts a span of <see cref="Text" /> that lies on one line into the matching span of the file's text.
    /// </summary>
    public TextSpan ToSourceSpan(TextSpan span) => new(Offsets[span.Start], span.Length);
}
```

Create `src/SqlSource/Parsing/SqlTextBuilder.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace SqlSource.Parsing;

/// <summary>
/// Builds the SQL of one block from its lexemes.
/// </summary>
internal static class SqlTextBuilder
{
    /// <summary>
    /// Builds the SQL of the lexemes from <paramref name="start" /> up to <paramref name="end" />.
    /// </summary>
    public static SqlBlockText Build(
        string text,
        EquatableArray<SqlLexeme> lexemes,
        int start,
        int end,
        bool preserveComments
    )
    {
        var writer = new Writer(text, preserveComments);
        for (var index = start; index < end; index++)
        {
            writer.Append(lexemes[index]);
        }

        return writer.Finish();
    }

    private sealed class Line
    {
        public StringBuilder Text { get; } = new();

        public List<int> Offsets { get; } = [];

        public bool IsMarker { get; set; }

        public void Append(char value, int offset)
        {
            _ = Text.Append(value);
            Offsets.Add(offset);
        }

        public void TrimEnd()
        {
            var length = Text.Length;
            while (length > 0 && Text[length - 1] is ' ' or '\t')
            {
                length--;
            }

            Offsets.RemoveRange(length, Text.Length - length);
            Text.Length = length;
        }
    }

    private sealed class Writer(string source, bool preserveComments)
    {
        private readonly List<Line> _lines = [];
        private Line _current = new();

        public void Append(SqlLexeme lexeme)
        {
            if (lexeme.Kind == SqlLexemeKind.Quoted)
            {
                AppendProtected(lexeme);
            }
            else if (SqlMarkerReader.Read(source, lexeme) is not null)
            {
                _current.IsMarker = true;
            }
            else if (preserveComments || lexeme.Kind is SqlLexemeKind.Text or SqlLexemeKind.Hint)
            {
                AppendLines(lexeme);
            }
            else if (lexeme.Kind == SqlLexemeKind.BlockComment)
            {
                // A stripped block comment leaves one space, so that the text on either side of it stays apart.
                // A stripped line comment leaves nothing.
                _current.Append(' ', lexeme.Span.Start);
            }
        }

        public SqlBlockText Finish()
        {
            EndLine();
            var kept = new List<Line>();
            foreach (var line in _lines)
            {
                line.TrimEnd();
                if (!line.IsMarker && (preserveComments || line.Text.Length > 0))
                {
                    kept.Add(line);
                }
            }

            var first = 0;
            var last = kept.Count - 1;
            while (first <= last && kept[first].Text.Length == 0)
            {
                first++;
            }

            while (last >= first && kept[last].Text.Length == 0)
            {
                last--;
            }

            return Join(kept, first, last);
        }

        private static SqlBlockText Join(List<Line> lines, int first, int last)
        {
            var text = new StringBuilder();
            var offsets = ImmutableArray.CreateBuilder<int>();
            for (var index = first; index <= last; index++)
            {
                if (index > first)
                {
                    // The offset of a joining line break is never read: a token cannot span lines.
                    _ = text.Append('\n');
                    offsets.Add(-1);
                }

                _ = text.Append(lines[index].Text);
                offsets.AddRange(lines[index].Offsets);
            }

            return new SqlBlockText(text.ToString(), offsets.ToImmutable());
        }

        // Text, hints and kept comments: a line terminator ends the output line.
        private void AppendLines(SqlLexeme lexeme)
        {
            var index = lexeme.Span.Start;
            var end = lexeme.Span.End;
            while (index < end)
            {
                var terminator = LineTerminatorLength(index, end);
                if (terminator == 0)
                {
                    _current.Append(source[index], index);
                    index++;
                }
                else
                {
                    EndLine();
                    index += terminator;
                }
            }
        }

        // Quoted regions: a line terminator becomes \n and stays inside the output line, so that the clean-up of
        // trailing blanks and blank lines never reaches into a string literal.
        private void AppendProtected(SqlLexeme lexeme)
        {
            var index = lexeme.Span.Start;
            var end = lexeme.Span.End;
            while (index < end)
            {
                var terminator = LineTerminatorLength(index, end);
                _current.Append(terminator == 0 ? source[index] : '\n', index);
                index += Math.Max(terminator, 1);
            }
        }

        private int LineTerminatorLength(int index, int end) =>
            source[index] switch
            {
                '\r' when index + 1 < end && source[index + 1] == '\n' => 2,
                '\r' or '\n' => 1,
                _ => 0,
            };

        private void EndLine()
        {
            _lines.Add(_current);
            _current = new Line();
        }
    }
}
```

- [ ] **Step 4: Format and build**

Run: `./format.sh`
Expected: exit code 0.

Run: `dotnet build SqlSource.slnx`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --project tests/SqlSource.Tests --no-build --filter-class "SqlSource.Tests.Parsing.SqlTextBuilderTests"`
Expected: `Test run summary: Passed!` with `total: 41`, `failed: 0`.

- [ ] **Step 6: Validate**

Run: `./pre-commit-validation.sh`
Expected: all seven steps `passed`.  The test step reports `total: 224`.

- [ ] **Step 7: Commit**

```bash
git add src/SqlSource/Parsing tests/SqlSource.Tests/Parsing
```

```bash
git commit -m "Add the SQL text builder" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: File parser

**Files:**
- Create: `src/SqlSource/Parsing/SqlFileParser.cs`
- Modify: `src/SqlSource/AGENTS.md`
- Modify: `docs/superpowers/specs/2026-10-05-sql-queries-epic-design.md`
- Test: `tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs`

**Interfaces:**
- Consumes: everything Tasks 1 to 5 produce.
- Produces: `static SqlFileParseResult SqlFileParser.Parse(string text, string fileName)`.  `fileName` is the file's name with its extension and without a directory.  This is the entry point phase 2 calls.

**Behaviour the implementation must keep:**
- A lexer error is returned alone, with no blocks.
- Every other error is collected in one pass.  The errors are ordered by the start of their span, and errors that start at the same place keep the order in which they were found.
- A result with any error has no blocks.
- A `-- name:` marker with an unusable name still starts a block, so the lines after it are checked as a block and not reported as SQL before the first name.
- A block's `PreserveComments` is true when its own scope or the preamble's sets it.  Its `TokenValidation` is its own scope's value, or the preamble's when its own is null.  Its ignored token names are the union of both.
- A block is empty when none of its lexemes has content (`SqlLexeme.GetContentSpan`), whether or not comments are preserved.
- A token error from `TokenScanner` has a span in the block's cleaned SQL.  It is converted with `SqlBlockText.ToSourceSpan` before it is reported.

- [ ] **Step 1: Write the failing tests**

Create `tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs`:

```csharp
using System;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class SqlFileParserTests
{
    [Fact]
    public void Parse_FileWithoutNameMarker_IsOneBlockNamedAfterTheFile()
    {
        var block = Blocks("SELECT 1;\n", "GetUser.sql").ShouldHaveSingleItem();

        block.Name.ShouldBe("GetUser");
        block.NameSpan.ShouldBe(new TextSpan(0, 0));
        block.Summary.ShouldBeNull();
        block.PreserveComments.ShouldBeFalse();
        block.TokenValidation.ShouldBeNull();
        block.Segments.ShouldBe([new SqlSegment(SqlSegmentKind.Literal, "SELECT 1;")]);
    }

    [Fact]
    public void Parse_FileWithoutNameMarker_ReadsSummaryAndDirectivesAnywhere()
    {
        const string Text =
            "SELECT 1 -- c\n-- summary: First.\nFROM t\n-- SqlSource: preserve-comments no-token-validation\n"
            + "-- summary: Second.\n";

        var block = Blocks(Text).ShouldHaveSingleItem();

        block.Summary.ShouldBe("First. Second.");
        block.PreserveComments.ShouldBeTrue();
        block.TokenValidation.ShouldBe(false);
        Sql(block).ShouldBe("SELECT 1 -- c\nFROM t");
    }

    [Theory]
    [InlineData("Query.sql", "Query")]
    [InlineData("Query.SQL", "Query")]
    [InlineData("Query", "Query")]
    [InlineData("_q1.sql", "_q1")]
    [InlineData("where.sql", "where")]
    public void Parse_FileWithoutNameMarker_TakesTheNameUpToTheLastDot(string fileName, string expected) =>
        Blocks("SELECT 1", fileName).ShouldHaveSingleItem().Name.ShouldBe(expected);

    [Theory]
    [InlineData("get-user.sql")]
    [InlineData("001_init.sql")]
    [InlineData("class.sql")]
    [InlineData("a.b.sql")]
    [InlineData(".sql")]
    [InlineData("")]
    public void Parse_FileWithoutNameMarkerAndUnusableFileName_IsAnError(string fileName)
    {
        Errors("SELECT 1", fileName)
            .ShouldBe([SqlParseError.Create(SqlParseErrorKind.InvalidFileName, new TextSpan(0, 0), fileName)]);
    }

    [Fact]
    public void Parse_FileWithNameMarkers_IgnoresTheFileName() =>
        Blocks("-- name: A\nSELECT 1", "001-not-a-name.sql").ShouldHaveSingleItem().Name.ShouldBe("A");

    [Fact]
    public void Parse_NameMarkers_SplitTheFileIntoBlocks()
    {
        const string Text = "-- name: GetUser\nSELECT 1;\n\n  -- name: ListUsers\n  SELECT 2;\n";

        var blocks = Blocks(Text);

        blocks.Select(static block => block.Name).ShouldBe(["GetUser", "ListUsers"]);
        blocks.Select(Sql).ShouldBe(["SELECT 1;", "  SELECT 2;"]);
        blocks[0].NameSpan.ShouldBe(SpanOf(Text, "GetUser"));
        blocks[1].NameSpan.ShouldBe(SpanOf(Text, "ListUsers"));
    }

    [Theory]
    [InlineData("-- name: a\nSELECT 1\n-- name: A\nSELECT 2", "a", "A")]
    [InlineData("-- name: where\nSELECT 1\n-- name: Größe\nSELECT 2", "where", "Größe")]
    public void Parse_UnusualButValidNames_AreAccepted(string text, string first, string second) =>
        Blocks(text).Select(static block => block.Name).ShouldBe([first, second]);

    [Fact]
    public void Parse_Preamble_DiscardsItsCommentsEvenWhenPreserving()
    {
        const string Text =
            "-- Copyright\n/* header */\n-- SqlSource: preserve-comments\n\n-- name: A\n-- kept\nSELECT 1\n";

        var block = Blocks(Text).ShouldHaveSingleItem();

        block.PreserveComments.ShouldBeTrue();
        Sql(block).ShouldBe("-- kept\nSELECT 1");
    }

    [Fact]
    public void Parse_PreambleDirectives_ApplyToEveryBlock()
    {
        const string Text =
            "-- SqlSource: no-token-validation token-ignore=x\n"
            + "-- name: A\nSELECT {{x}}\n"
            + "-- name: B\nSELECT {{x}} {{y}}\n";

        var blocks = Blocks(Text);

        blocks[0].TokenValidation.ShouldBe(false);
        blocks[1].TokenValidation.ShouldBe(false);
        blocks[0].Segments.ShouldBe([new SqlSegment(SqlSegmentKind.Literal, "SELECT {{x}}")]);
        blocks[1]
            .Segments.ShouldBe([
                new SqlSegment(SqlSegmentKind.Literal, "SELECT {{x}} "),
                new SqlSegment(SqlSegmentKind.Token, "y"),
            ]);
    }

    [Fact]
    public void Parse_BlockValidationDirective_OverridesThePreamble()
    {
        const string Text =
            "-- SqlSource: no-token-validation\n"
            + "-- name: A\n-- SqlSource: token-validation\nSELECT 1\n"
            + "-- name: B\nSELECT 2\n";

        var blocks = Blocks(Text);

        blocks[0].TokenValidation.ShouldBe(true);
        blocks[1].TokenValidation.ShouldBe(false);
    }

    [Fact]
    public void Parse_BlockTokenIgnore_AddsToThePreamble()
    {
        const string Text =
            "-- SqlSource: token-ignore=x\n-- name: A\n-- SqlSource: token-ignore=y\nSELECT {{x}} {{y}} {{z}}\n";

        Blocks(Text)
            .ShouldHaveSingleItem()
            .Segments.ShouldBe([
                new SqlSegment(SqlSegmentKind.Literal, "SELECT {{x}} {{y}} "),
                new SqlSegment(SqlSegmentKind.Token, "z"),
            ]);
    }

    [Fact]
    public void Parse_BlockDirective_DoesNotLeakIntoTheNextBlock()
    {
        const string Text =
            "-- name: A\n-- SqlSource: preserve-comments token-validation token-ignore=x\nSELECT 1 -- c\n"
            + "-- name: B\nSELECT {{x}} -- c\n";

        var blocks = Blocks(Text);

        blocks[1].PreserveComments.ShouldBeFalse();
        blocks[1].TokenValidation.ShouldBeNull();
        Sql(blocks[1]).ShouldBe("SELECT {{x}}");
        blocks[1].Segments[1].ShouldBe(new SqlSegment(SqlSegmentKind.Token, "x"));
    }

    [Theory]
    [InlineData("SELECT 0;\nSELECT 9;\n-- name: A\nSELECT 1", "SELECT 0;\nSELECT 9;")]
    [InlineData("  'x' y\n/*+ h */\n-- name: A\nSELECT 1", "'x'")]
    [InlineData("-- c\n/*+ h */ z\n-- name: A\nSELECT 1", "/*+ h */")]
    public void Parse_SqlInThePreamble_IsReportedOnceAtItsStart(string text, string content) =>
        Errors(text).ShouldBe([SqlParseError.Create(SqlParseErrorKind.SqlBeforeFirstName, SpanOf(text, content))]);

    [Fact]
    public void Parse_SummaryInThePreamble_IsAnError()
    {
        const string Text = "-- summary: nope\n-- name: A\nSELECT 1";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.SummaryBeforeFirstName, SpanOf(Text, "-- summary: nope")),
            ]);
    }

    [Theory]
    [InlineData("1abc")]
    [InlineData("get-user")]
    [InlineData("class")]
    [InlineData("A B")]
    [InlineData("@class")]
    [InlineData("A -- note")]
    public void Parse_UnusableName_IsAnErrorAtTheName(string name)
    {
        var text = $"-- name: {name}\nSELECT 1";

        Errors(text).ShouldBe([SqlParseError.Create(SqlParseErrorKind.InvalidName, SpanOf(text, name), name)]);
    }

    [Fact]
    public void Parse_NameMarkerWithoutAName_IsAnErrorAtTheMarker()
    {
        const string Text = "-- name:  \nSELECT 1";

        Errors(Text)
            .ShouldBe([SqlParseError.Create(SqlParseErrorKind.InvalidName, SpanOf(Text, "-- name:  "), string.Empty)]);
    }

    [Fact]
    public void Parse_RepeatedName_IsAnErrorAtTheSecondOccurrence()
    {
        const string Text = "-- name: A\nSELECT 1\n-- name: A\nSELECT 2";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.DuplicateName, new TextSpan(Text.LastIndexOf('A'), 1), "A"),
            ]);
    }

    [Theory]
    [InlineData("-- name: A\n-- name: B\nSELECT 1")]
    [InlineData("-- name: A\n-- c\n/* d */\n\n-- name: B\nSELECT 1")]
    [InlineData("-- name: A\n-- SqlSource: preserve-comments\n-- c\n-- name: B\nSELECT 1")]
    [InlineData("-- name: B\nSELECT 1\n-- name: A")]
    [InlineData("-- name: B\nSELECT 1\n-- name: A\n-- summary: s\n")]
    public void Parse_BlockWithoutSql_IsAnErrorAtItsName(string text)
    {
        var errors = Errors(text);

        errors.ShouldBe([SqlParseError.Create(SqlParseErrorKind.EmptyBlock, SpanOf(text, "A"))]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \r\n\t")]
    [InlineData("-- only a comment\n/* and another */")]
    [InlineData("-- SqlSource: preserve-comments\n-- c")]
    public void Parse_FileWithoutNameMarkerOrSql_IsAnErrorAtTheStart(string text) =>
        Errors(text).ShouldBe([SqlParseError.Create(SqlParseErrorKind.EmptyBlock, new TextSpan(0, 0))]);

    [Fact]
    public void Parse_BlockHoldingOnlyAHint_IsNotEmpty() =>
        Sql(Blocks("-- name: A\n/*+ H */").ShouldHaveSingleItem()).ShouldBe("/*+ H */");

    [Fact]
    public void Parse_SummaryMarkers_AreJoinedWithOneSpace()
    {
        const string Text = "-- name: A\n-- summary: One.\nSELECT 1\n-- summary:\n-- summary:   Two.  \n";

        var block = Blocks(Text).ShouldHaveSingleItem();

        block.Summary.ShouldBe("One. Two.");
        Sql(block).ShouldBe("SELECT 1");
    }

    [Fact]
    public void Parse_BlockWithOnlyEmptySummaryMarkers_HasNoSummary() =>
        Blocks("-- name: A\n-- summary:\nSELECT 1").ShouldHaveSingleItem().Summary.ShouldBeNull();

    [Fact]
    public void Parse_Tokens_BecomeSegmentsInOrder()
    {
        const string Text = "-- name: A\nSELECT * FROM {{table}} WHERE {{ col }} = 1 AND {{table}}.x = 2";

        Blocks(Text)
            .ShouldHaveSingleItem()
            .Segments.ShouldBe([
                new SqlSegment(SqlSegmentKind.Literal, "SELECT * FROM "),
                new SqlSegment(SqlSegmentKind.Token, "table"),
                new SqlSegment(SqlSegmentKind.Literal, " WHERE "),
                new SqlSegment(SqlSegmentKind.Token, "col"),
                new SqlSegment(SqlSegmentKind.Literal, " = 1 AND "),
                new SqlSegment(SqlSegmentKind.Token, "table"),
                new SqlSegment(SqlSegmentKind.Literal, ".x = 2"),
            ]);
    }

    [Fact]
    public void Parse_TokenInAStrippedComment_IsNotAToken()
    {
        const string Text = "-- name: A\nSELECT 1 -- {{x}}\n/* {{y}} */\n";

        Blocks(Text).ShouldHaveSingleItem().Segments.ShouldBe([new SqlSegment(SqlSegmentKind.Literal, "SELECT 1")]);
    }

    [Fact]
    public void Parse_TokenInAStringLiteralOrAPreservedComment_IsAToken()
    {
        const string Text = "-- name: A\n-- SqlSource: preserve-comments\nSELECT '{{a}}' -- {{b}}";

        Blocks(Text)
            .ShouldHaveSingleItem()
            .Segments.Where(static segment => segment.Kind == SqlSegmentKind.Token)
            .Select(static segment => segment.Text)
            .ShouldBe(["a", "b"]);
    }

    [Fact]
    public void Parse_ReservedKeywordToken_IsAnErrorAtTheTokenInTheFile()
    {
        const string Text = "-- name: A\r\n/* c */ SELECT 1 -- x\r\n\r\n-- summary: s\r\n  FROM {{ class }}";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.ReservedTokenName, SpanOf(Text, "{{ class }}"), "class"),
            ]);
    }

    [Fact]
    public void Parse_ReservedKeywordTokenThatIsIgnored_IsLiteral()
    {
        const string Text = "-- name: A\n-- SqlSource: token-ignore=class\nSELECT {{class}}";

        Sql(Blocks(Text).ShouldHaveSingleItem()).ShouldBe("SELECT {{class}}");
    }

    [Theory]
    [InlineData("/*\n-- name: A\n*/\nSELECT 1", "SELECT 1")]
    [InlineData("SELECT 1 -- name: A", "SELECT 1")]
    [InlineData("SELECT '\n-- name: A\n'", "SELECT '\n-- name: A\n'")]
    [InlineData("-- name : A\nSELECT 1", "SELECT 1")]
    public void Parse_NameMarkerLookAlike_DoesNotStartABlock(string text, string expectedSql)
    {
        var block = Blocks(text).ShouldHaveSingleItem();

        block.Name.ShouldBe("Query");
        Sql(block).ShouldBe(expectedSql);
    }

    [Theory]
    [InlineData("-- SqlSource: preserve-comment", nameof(SqlParseErrorKind.UnknownDirective), "preserve-comment")]
    [InlineData("-- SqlSource:", nameof(SqlParseErrorKind.EmptyDirectiveLine), "-- SqlSource:")]
    [InlineData("-- SqlSource: token-ignore=", nameof(SqlParseErrorKind.InvalidDirectiveValue), "token-ignore=")]
    [InlineData(
        "-- SqlSource: token-validation no-token-validation",
        nameof(SqlParseErrorKind.ConflictingDirectives),
        "no-token-validation"
    )]
    public void Parse_DirectiveProblem_IsReportedAtItsPlaceInTheFile(string line, string kind, string place)
    {
        var text = "-- name: A\n" + line + "\nSELECT 1";

        var error = Errors(text).ShouldHaveSingleItem();

        error.Kind.ToString().ShouldBe(kind);
        error.Span.ShouldBe(SpanOf(text, place));
    }

    [Theory]
    [InlineData("-- name: 1x\nSELECT 'abc", nameof(SqlParseErrorKind.UnterminatedQuote))]
    [InlineData("-- name: 1x\nSELECT /* abc", nameof(SqlParseErrorKind.UnterminatedBlockComment))]
    public void Parse_LexerError_IsTheOnlyErrorReported(string text, string kind) =>
        Errors(text).ShouldHaveSingleItem().Kind.ToString().ShouldBe(kind);

    [Fact]
    public void Parse_SeveralErrors_AreAllReportedInFileOrderAndNoBlocksAreReturned()
    {
        const string Text =
            "-- name: 1x\nSELECT 1\n"
            + "-- name: B\n-- SqlSource: bogus\n"
            + "-- name: C\nSELECT {{class}}\n"
            + "-- name: C\nSELECT 3\n";

        var errors = Errors(Text);

        errors
            .Select(static error => error.Kind)
            .ShouldBe([
                SqlParseErrorKind.InvalidName,
                SqlParseErrorKind.EmptyBlock,
                SqlParseErrorKind.UnknownDirective,
                SqlParseErrorKind.ReservedTokenName,
                SqlParseErrorKind.DuplicateName,
            ]);
        errors.Select(static error => error.Span.Start).ShouldBeInOrder();
    }

    [Fact]
    public void Parse_MarkerWithUnusableName_StillStartsABlock()
    {
        Errors("-- name: 1x\nSELECT 1\n-- name: B\nSELECT 2")
            .ShouldHaveSingleItem()
            .Kind.ShouldBe(SqlParseErrorKind.InvalidName);
    }

    [Fact]
    public void Parse_WindowsLineEndings_GiveTheSameBlocksAsUnixLineEndings()
    {
        const string Unix =
            "-- SqlSource: no-token-validation\n\n-- name: A\n-- summary: S\nSELECT 1 -- c\nFROM {{t}}\n\n"
            + "-- name: B\nSELECT 'x\ny'\n";
        var windows = Unix.Replace("\n", "\r\n", StringComparison.Ordinal);

        var unixBlocks = Blocks(Unix);
        var windowsBlocks = Blocks(windows);

        windowsBlocks.Select(Sql).ShouldBe(["SELECT 1\nFROM {{t}}", "SELECT 'x\ny'"]);
        windowsBlocks.Select(Sql).ShouldBe(unixBlocks.Select(Sql));
        windowsBlocks.Select(static block => block.Summary).ShouldBe(["S", null]);
        windowsBlocks.Select(static block => block.TokenValidation).ShouldBe([false, false]);
    }

    [Theory]
    [InlineData("-- name: A\n-- summary: s\nSELECT {{a}} -- c\n-- name: B\nSELECT 2\n")]
    [InlineData("-- name: 1x\n-- SqlSource: bogus\nSELECT {{class}}\n")]
    [InlineData("SELECT 'abc")]
    public void Parse_SameTextTwice_GivesEqualResults(string text)
    {
        var first = SqlFileParser.Parse(text, "Query.sql");
        var second = SqlFileParser.Parse(new string(text.ToCharArray()), "Query.sql");

        second.ShouldNotBeSameAs(first);
        second.ShouldBe(first);
        second.GetHashCode().ShouldBe(first.GetHashCode());
    }

    [Fact]
    public void Parse_DifferentText_GivesUnequalResults() =>
        SqlFileParser.Parse("SELECT 1", "Query.sql").ShouldNotBe(SqlFileParser.Parse("SELECT 2", "Query.sql"));

    private static SqlBlock[] Blocks(string text, string fileName = "Query.sql")
    {
        var result = SqlFileParser.Parse(text, fileName);
        result.Errors.ShouldBeEmpty();
        return [.. result.Blocks];
    }

    private static SqlParseError[] Errors(string text, string fileName = "Query.sql")
    {
        var result = SqlFileParser.Parse(text, fileName);
        result.Blocks.ShouldBeEmpty();
        return [.. result.Errors];
    }

    private static string Sql(SqlBlock block) =>
        string.Concat(
            block.Segments.Select(static segment =>
                segment.Kind == SqlSegmentKind.Token ? "{{" + segment.Text + "}}" : segment.Text
            )
        );

    private static TextSpan SpanOf(string text, string value) =>
        new(text.IndexOf(value, StringComparison.Ordinal), value.Length);
}
```

- [ ] **Step 2: Run the build to verify it fails**

Run: `dotnet build SqlSource.slnx`
Expected: the build fails with `CS0103` for `SqlFileParser`.

- [ ] **Step 3: Add the parser**

Create `src/SqlSource/Parsing/SqlFileParser.cs`:

```csharp
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// Turns the text of one <c>.sql</c> file into its named SQL blocks, or into a list of errors.
/// </summary>
/// <remarks>
/// The parser reads no files and compares text ordinally, and its result has value equality.
/// </remarks>
internal static class SqlFileParser
{
    /// <summary>
    /// Parses <paramref name="text" />.  <paramref name="fileName" /> is the file's name with its extension and
    /// without a directory; it names the block of a file that has no <c>-- name:</c> marker.
    /// </summary>
    public static SqlFileParseResult Parse(string text, string fileName)
    {
        var lexed = SqlLexer.Lex(text);
        return lexed.Error is null
            ? new Parser(text, fileName, lexed.Lexemes).Run()
            : new SqlFileParseResult(
                EquatableArray<SqlBlock>.Empty,
                new EquatableArray<SqlParseError>(ImmutableArray.Create(lexed.Error))
            );
    }

    private sealed class Parser(string text, string fileName, EquatableArray<SqlLexeme> lexemes)
    {
        private static readonly TextSpan FileStart = new(0, 0);

        private readonly List<SqlBlock> _blocks = [];
        private readonly List<SqlParseError> _errors = [];
        private readonly HashSet<string> _names = [];

        public SqlFileParseResult Run()
        {
            var nameMarkers = FindNameMarkers();
            if (nameMarkers.Count == 0)
            {
                ReadUnnamedFile();
            }
            else
            {
                ReadNamedBlocks(nameMarkers);
            }

            return _errors.Count > 0
                ? new SqlFileParseResult(
                    EquatableArray<SqlBlock>.Empty,
                    new EquatableArray<SqlParseError>(
                        _errors.OrderBy(static error => error.Span.Start).ToImmutableArray()
                    )
                )
                : new SqlFileParseResult(
                    new EquatableArray<SqlBlock>(_blocks.ToImmutableArray()),
                    EquatableArray<SqlParseError>.Empty
                );
        }

        private List<(int Index, SqlMarker Marker)> FindNameMarkers()
        {
            var markers = new List<(int Index, SqlMarker Marker)>();
            for (var index = 0; index < lexemes.Count; index++)
            {
                if (SqlMarkerReader.Read(text, lexemes[index]) is { Kind: SqlMarkerKind.Name } marker)
                {
                    markers.Add((index, marker));
                }
            }

            return markers;
        }

        private void ReadUnnamedFile()
        {
            var lastDot = fileName.LastIndexOf('.');
            var name = lastDot < 0 ? fileName : fileName.Substring(0, lastDot);
            if (!SqlIdentifier.IsUsableName(name))
            {
                AddError(SqlParseErrorKind.InvalidFileName, FileStart, fileName);
            }

            ReadBlock(name, FileStart, new SqlDirectiveScope(), 0, lexemes.Count);
        }

        private void ReadNamedBlocks(List<(int Index, SqlMarker Marker)> nameMarkers)
        {
            var preamble = ReadPreamble(nameMarkers[0].Index);
            for (var position = 0; position < nameMarkers.Count; position++)
            {
                var (index, marker) = nameMarkers[position];
                var end = position + 1 < nameMarkers.Count ? nameMarkers[position + 1].Index : lexemes.Count;
                var name = text.Substring(marker.ValueSpan.Start, marker.ValueSpan.Length);
                var nameSpan = name.Length == 0 ? marker.Span : marker.ValueSpan;
                if (!SqlIdentifier.IsUsableName(name))
                {
                    AddError(SqlParseErrorKind.InvalidName, nameSpan, name);
                }
                else if (!_names.Add(name))
                {
                    AddError(SqlParseErrorKind.DuplicateName, nameSpan, name);
                }

                // A marker with an unusable name still starts a block, so that what follows is checked as a block.
                ReadBlock(name, nameSpan, preamble, index + 1, end);
            }
        }

        private SqlDirectiveScope ReadPreamble(int end)
        {
            var scope = new SqlDirectiveScope();
            var sqlReported = false;
            for (var index = 0; index < end; index++)
            {
                var lexeme = lexemes[index];
                var marker = SqlMarkerReader.Read(text, lexeme);
                if (marker is { Kind: SqlMarkerKind.Directives })
                {
                    scope.Read(text, marker.Value, _errors);
                }
                else if (marker is { Kind: SqlMarkerKind.Summary })
                {
                    AddError(SqlParseErrorKind.SummaryBeforeFirstName, marker.Value.Span);
                }
                else if (!sqlReported && lexeme.GetContentSpan(text) is { } content)
                {
                    AddError(SqlParseErrorKind.SqlBeforeFirstName, content);
                    sqlReported = true;
                }
            }

            return scope;
        }

        private void ReadBlock(string name, TextSpan nameSpan, SqlDirectiveScope inherited, int start, int end)
        {
            var scope = new SqlDirectiveScope();
            var summary = new List<string>();
            var hasContent = false;
            for (var index = start; index < end; index++)
            {
                var lexeme = lexemes[index];
                var marker = SqlMarkerReader.Read(text, lexeme);
                if (marker is { Kind: SqlMarkerKind.Directives })
                {
                    scope.Read(text, marker.Value, _errors);
                }
                else if (marker is { Kind: SqlMarkerKind.Summary, ValueSpan.IsEmpty: false })
                {
                    summary.Add(text.Substring(marker.Value.ValueSpan.Start, marker.Value.ValueSpan.Length));
                }
                else
                {
                    hasContent |= lexeme.GetContentSpan(text) is not null;
                }
            }

            if (!hasContent)
            {
                AddError(SqlParseErrorKind.EmptyBlock, nameSpan);
                return;
            }

            var preserveComments = inherited.PreserveComments || scope.PreserveComments;
            var sql = SqlTextBuilder.Build(text, lexemes, start, end, preserveComments);
            HashSet<string> ignoredTokens = [.. inherited.IgnoredTokens, .. scope.IgnoredTokens];
            var scanned = TokenScanner.Scan(sql.Text, ignoredTokens);
            foreach (var error in scanned.Errors)
            {
                _errors.Add(error with { Span = sql.ToSourceSpan(error.Span) });
            }

            _blocks.Add(
                new SqlBlock(
                    name,
                    nameSpan,
                    summary.Count == 0 ? null : string.Join(" ", summary),
                    preserveComments,
                    scope.TokenValidation ?? inherited.TokenValidation,
                    scanned.Segments
                )
            );
        }

        private void AddError(SqlParseErrorKind kind, TextSpan span, params string[] arguments) =>
            _errors.Add(SqlParseError.Create(kind, span, arguments));
    }
}
```

- [ ] **Step 4: Format and build**

Run: `./format.sh`
Expected: exit code 0.

Run: `dotnet build SqlSource.slnx`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --project tests/SqlSource.Tests --no-build --filter-class "SqlSource.Tests.Parsing.SqlFileParserTests"`
Expected: `Test run summary: Passed!` with `total: 68`, `failed: 0`.

- [ ] **Step 6: Document the parser's constraints**

In `src/SqlSource/AGENTS.md`, insert this section immediately before the final `> Maintenance:` line, keeping one blank line between the section and that line:

```markdown
## `Parsing/`

`SqlFileParser.Parse` turns the text of one `.sql` file into named SQL blocks, or into errors.

- **Pure.**  No file access and no generator pipeline types.  Comparisons are ordinal; marker keywords and directive names are ordinal ignoring case.
- **Every span is an offset into the file's text**, including for a problem found in a block's cleaned SQL.  `SqlBlockText.ToSourceSpan` maps such a span back to the file.
- **Lexemes cover the text without gaps.**  `SqlMarkerReader` and `SqlTextBuilder` depend on that, and on a quoted region or block comment ending with its closing delimiter.
- **Dialect-sensitive choices live in `SqlLexer` only.**  The `Lex_KnownLimit_*` tests pin where it reads SQL differently from some databases.  Changing one changes the SQL users get.
- **A result with errors has no blocks.**  The generator must never emit from a partly valid file.
```

- [ ] **Step 7: Mark the phase done in the epic outline**

The outline is kept current until the epic closes.  In `docs/superpowers/specs/2026-10-05-sql-queries-epic-design.md`, in the table under `## Phases`, change the status cell of the row that starts `| 1. SQL parser |` from `Designed` to `Done`.  Change nothing else in the row.

- [ ] **Step 8: Verify the whole phase**

Run: `./pre-commit-validation.sh`
Expected: all seven steps `passed`.  The test step reports `total: 292`, `failed: 0`.

Run: `dotnet test --project tests/SqlSource.Tests --no-build --filter-class "SqlSource.Tests.SqlSourceGeneratorTests"`
Expected: `Test run summary: Passed!` with `total: 1`.  The generator still produces no output and no diagnostics: nothing in this phase is wired into it.

Run: `git status --short`
Expected: only `src/SqlSource/Parsing/SqlFileParser.cs`, `src/SqlSource/AGENTS.md`, `tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs` and the epic outline are listed.  `README.md`, `CONTRIBUTING.md` and both `packages.lock.json` files are unchanged across the whole branch.

- [ ] **Step 9: Commit**

```bash
git add src/SqlSource tests/SqlSource.Tests/Parsing docs/superpowers/specs/2026-10-05-sql-queries-epic-design.md
```

```bash
git commit -m "Add the SQL file parser" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## After the last task

- Nothing was deferred and no tech debt was introduced by this plan as written.  If implementation departs from it, record the departure in `docs/deferred` or `docs/tech-debt` in the same pull request, as the root `AGENTS.md` requires.
- Before a pull request is opened, the root `AGENTS.md` requires the version check: `git fetch --tags origin`, then compare `VersionPrefix` in `Directory.Build.props` with the highest `v*` tag.  The repository has no `v*` tags at the time of writing, so there is nothing to compare.
