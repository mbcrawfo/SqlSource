# TD-0038, pull request 3: `Parsing/` and the generator - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Item 7 of TD-0038, and the generator's part of items 9, 10 and 11, each get one shape: one `SqlText` for the trim, the line end and the character lookup, a `SqlScopeKind` in place of two booleans, `ReadBlock` as three steps with one record for what `SqlParameterList.Create` reads, and the four small leftovers deleted, inlined or merged.

**Architecture:** Every change is a refactoring of `src/SqlSource` that the existing tests hold.  New units are small and static (`SqlText`, two static formatters of `SidecarJsonBuilder`) or plain data (`SqlScopeKind`, `SqlScannedQuery`), and each gets tests of its own before it is written.  The parse is a hot path, so its time and allocation are measured before the first change and after the last.

**Tech Stack:** C# (`LangVersion` `preview`) on `netstandard2.0` for `src/SqlSource`, with Roslyn 4.8.0; xunit v3 on Microsoft.Testing.Platform with Shouldly.

**Spec:** `docs/superpowers/specs/2026-10-10-td-0038-duplicated-patterns-design.md`, the section "Pull request 3".

**Branch:** `claude/td-0038-parsing-and-generator`, made from `main` in Task 1.  This file is untracked until Task 6 commits it.

## Where the plan settles what the spec leaves open

The user approved the first five on 2026-10-11.

- **One `SqlText.FindLineEnd(text, start, end)`.**  `SqlLexer` searched to the end of the text and `SqlTextBuilder` to a given end; the lexer now passes `text.Length`.
- **`XmlDocWriter.LineTerminators` stays.**  It holds five terminators, the rule for a C# documentation comment, and not the two of SQL.
- **The record of `SqlParameterList.Create` is `SqlScannedQuery`**: the built SQL, its token occurrences, its tokens and its declared variables.  `Create` goes from eight parameters to five.
- **The `EmptyBlock` and `InputModelWithoutParameters` checks stay in `ReadBlock`, between its steps**, so that the order in which a block's errors are added does not change.
- **`SidecarJsonBuilder` gains static `Number(int?)` and `Flag(bool?)` beside `Quote`**, which its own `WriteNumber` and `WriteBoolean` use, and `SidecarComparer`.  `SidecarComparer.Json(string?)` stays: it is the null rule over `Quote`, and no formatting of its own.
- **Step two of `ReadBlock` returns a private `BuiltQuery`** of the `Parser`: the SQL, its segments, tokens and parameters, and where its scan errors start in the list of errors, which the kept form needs.
- **The test of `SqlDialectMarker.Describe` is not moved whole.**  `SqlMarkerReaderTests` already has a theory of `Describe`; the four dialect rows join it.

## Global Constraints

- Nothing a user sees changes: no message, id, generated file or sidecar.  `README.md`, `CONTRIBUTING.md`, `docs/diagnostics.md` and `docs/publishing.md` are not touched.
- `src/SqlSource.Tool` is not touched.  Its tests still run at the end, since the tool parses with this code.
- The existing tests pass unchanged, apart from tests of a member that is renamed or removed, which follow it.
- The budgets of `SqlFileParserAllocationTests` (12 and 24 bytes for a character), `PathResolverAllocationTests` and `SidecarReaderAllocationTests` do not rise.
- `src/SqlSource` targets `netstandard2.0` with Roslyn 4.8.0: no API newer than that, and no collection expression that targets `ImmutableArray<T>`.
- `Parsing/` allocates nothing for a character or a line.  A new helper there takes offsets and returns a span or an offset, never a string.
- The build treats warnings as errors with `AnalysisLevel` `latest-all`.  Fix what it reports in the code, a `using` that nothing needs any more included; never suppress.
- `ImplicitUsings` is off: a file names every namespace it uses.
- Every commit passes `./pre-commit-validation.sh` (it needs Docker running).  Run `./format.sh`, then the validation, then commit in a command of its own.  Never work around the hook.
- An `AGENTS.md` that names a member changes in the commit that renames or removes it.
- Prose in comments and documents follows the repository's style: plain sentences, two spaces after a full stop.
- End each commit message with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

Run one test class with:

```bash
dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj --filter-class "SqlSource.Tests.Parsing.SqlTextTests"
```

Run the generator's tests with `dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj`, and everything with `dotnet test --solution SqlSource.slnx`.

## Review Focus

1. **A block without SQL whose markers would be errors only once a query is built**: `-- param: @a` with no type, or `-- input-model: X`.  It must stay `EmptyBlock` alone, so the check stays before step two.  Test in Task 5.
2. **A file with no `-- name:` marker** takes the markers of a preamble and those of a query, and each of the other two scopes takes its own list only.  Test in Task 2.
3. **A query that keeps its comments, with a token that is an error in the SQL and another in a comment.**  The first is in both forms and must be reported once; the second once.  The kept form finds the shared one from where the scan errors start, which step two now hands to step three.  Test in Task 5.
4. **A line end at the edge of a range**: a terminator just past `end`, an empty range, the end of the text, and U+2028, which does not end a line of SQL.  Tests in Task 1.
5. **A number in a difference of `describe --check` under a culture whose minus sign is not `-`.**  `SidecarComparer` now formats through `SidecarJsonBuilder.Number`, which must stay invariant.  Test in Task 4.

## File Structure

| File | Change |
|----|----|
| `src/SqlSource/Parsing/SqlText.cs` | New: `Trim`, `FindLineEnd`, `CharAt` and the two line terminators |
| `src/SqlSource/Parsing/SqlLexer.cs`, `SqlTextBuilder.cs`, `SqlMarkerReader.cs`, `SqlLexeme.cs`, `Tokens/TokenScanner.cs` | Call `SqlText` and lose their copies |
| `src/SqlSource/Parsing/SqlScopeKind.cs` | New: `Preamble`, `Query`, `WholeFile` |
| `src/SqlSource/Parsing/SqlMarkerScope.cs` | Takes its kind in the constructor; `Read(marker)` |
| `src/SqlSource/Parsing/SqlDialectMarker.cs` | `Describe` goes |
| `src/SqlSource/Settings/SettingValue.cs`, `SettingsLevelReader.cs` | `IsNamespace` and `IsTypeName` become `IsQualifiedName` |
| `src/SqlSource/SqlSourceGenerator.cs` | `ToSortedFiles` and `ToSortedSettings` become `ToSorted<T>` |
| `src/SqlSource/Snapshot/SidecarJsonBuilder.cs`, `SidecarComparer.cs` | Static `Number` and `Flag`; the comparer's two `Json` overloads go |
| `src/SqlSource/Parsing/SqlScannedQuery.cs` | New: what `SqlParameterList.Create` reads of a query |
| `src/SqlSource/Parsing/SqlParameterList.cs` | `Create` takes the record |
| `src/SqlSource/Parsing/SqlFileParser.cs` | `ReadBlock` in three steps; calls `SqlMarkerReader.Describe` |
| `src/SqlSource/AGENTS.md` | `SqlText`, the scope's kind, the one `ToSorted` helper, the steps of `ReadBlock` |
| `tests/SqlSource.Tests/Parsing/SqlTextTests.cs`, `SqlMarkerScopeTests.cs`, `tests/SqlSource.Tests/Snapshot/SidecarJsonBuilderTests.cs` | New |
| `tests/SqlSource.Tests/Parsing/SqlMarkerReaderTests.cs`, `SqlDialectMarkerTests.cs`, `SqlFileParserTests.cs`, `tests/SqlSource.Tests/Settings/SettingValueTests.cs` | Follow the members that move, and gain the tests of the review focus |
| `docs/tech-debt/TD-0038-*.md`, `docs/tech-debt/README.md` | Rewritten to what remains |

---

### Task 1: Measure the parse, then one helper for text (item 7)

**Files:**
- Create: `src/SqlSource/Parsing/SqlText.cs`
- Create: `tests/SqlSource.Tests/Parsing/SqlTextTests.cs`
- Modify: `src/SqlSource/Parsing/SqlLexer.cs`, `src/SqlSource/Parsing/SqlTextBuilder.cs`, `src/SqlSource/Parsing/SqlMarkerReader.cs`, `src/SqlSource/Parsing/SqlLexeme.cs`, `src/SqlSource/Parsing/Tokens/TokenScanner.cs`
- Modify: `src/SqlSource/AGENTS.md`
- Modify: this file, the table under "Measurements"

**Interfaces:**
- Produces: `SqlText.Trim(string text, int start, int end) : TextSpan`
- Produces: `SqlText.FindLineEnd(string text, int start, int end) : int`
- Produces: `SqlText.CharAt(string text, int index) : char`

- [ ] **Step 1: Make the branch**

```bash
git switch -c claude/td-0038-parsing-and-generator
```

- [ ] **Step 2: Measure a parse before any change**

`src/SqlSource/AGENTS.md` asks a change to a hot path for its time and allocation, before and after.  Add this method to `tests/SqlSource.Tests/Parsing/SqlFileParserAllocationTests.cs`, with `using System.Diagnostics;` at the top.  It is never committed.

```csharp
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Measure(bool keepsComments)
    {
        const int Parses = 2_000;
        var text = CreateFile(hasDialect: false, keepsComments);
        for (var parse = 0; parse < 200; parse++)
        {
            _ = SqlFileParser.Parse(text, "Queries.sql");
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        for (var parse = 0; parse < Parses; parse++)
        {
            _ = SqlFileParser.Parse(text, "Queries.sql");
        }

        watch.Stop();
        var bytes = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)(Parses * text.Length);
        Assert.Fail(
            $"keepsComments={keepsComments}: {watch.Elapsed.TotalMilliseconds * 1000 / Parses:F1} us for a parse, {bytes:F2} bytes for a character"
        );
    }
```

Run it three times:

```bash
dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj -c Release --filter-method "*SqlFileParserAllocationTests.Measure"
```

Expected: both rows FAIL, each with a message such as `keepsComments=False: 95.3 us for a parse, 11.40 bytes for a character`.

Write the middle of the three times, and the bytes, for each row into the "before" row of the table under "Measurements" at the end of this file.  Then take the method out:

```bash
git checkout -- tests/SqlSource.Tests/Parsing/SqlFileParserAllocationTests.cs
```

- [ ] **Step 3: Write the failing tests**

Create `tests/SqlSource.Tests/Parsing/SqlTextTests.cs`:

```csharp
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class SqlTextTests
{
    [Theory]
    [InlineData("  a b  ", 0, 7, 2, 5)]
    [InlineData("a", 0, 1, 0, 1)]
    [InlineData("\t\r\n x\u00A0", 0, 6, 4, 5)]
    // What is outside the bounds is not looked at.
    [InlineData("xx  a  xx", 2, 7, 4, 5)]
    // Nothing but white space: empty, at the end.
    [InlineData("   ", 0, 3, 3, 3)]
    [InlineData("abc", 1, 1, 1, 1)]
    public void Trim_Bounds_AreMovedInPastTheWhiteSpace(
        string text,
        int start,
        int end,
        int expectedStart,
        int expectedEnd
    ) => SqlText.Trim(text, start, end).ShouldBe(TextSpan.FromBounds(expectedStart, expectedEnd));

    [Theory]
    [InlineData("ab\ncd", 0, 5, 2)]
    [InlineData("ab\r\ncd", 0, 6, 2)]
    [InlineData("ab\rcd", 0, 5, 2)]
    [InlineData("abcd", 0, 4, 4)]
    [InlineData("ab\ncd", 3, 5, 5)]
    [InlineData("ab\ncd\nef", 3, 8, 5)]
    // A terminator at the end of the range is outside it.
    [InlineData("ab\ncd", 0, 2, 2)]
    [InlineData("ab\ncd", 2, 2, 2)]
    [InlineData("ab", 2, 2, 2)]
    // U+2028 ends a line of C#, not a line of SQL.
    [InlineData("ab\u2028cd", 0, 5, 5)]
    public void FindLineEnd_Range_GivesItsFirstTerminatorOrItsEnd(string text, int start, int end, int expected) =>
        SqlText.FindLineEnd(text, start, end).ShouldBe(expected);

    [Theory]
    [InlineData("ab", 0, 'a')]
    [InlineData("ab", 1, 'b')]
    [InlineData("ab", 2, '\0')]
    [InlineData("ab", 9, '\0')]
    [InlineData("", 0, '\0')]
    public void CharAt_Index_GivesTheCharacterOrNulPastTheEnd(string text, int index, char expected) =>
        SqlText.CharAt(text, index).ShouldBe(expected);
}
```

- [ ] **Step 4: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj --filter-class "SqlSource.Tests.Parsing.SqlTextTests"`
Expected: the build fails with CS0103, `The name 'SqlText' does not exist in the current context`.

- [ ] **Step 5: Write `SqlText`**

Create `src/SqlSource/Parsing/SqlText.cs`:

```csharp
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// The small readings of a text that the lexer, the readers of markers and of tokens and the builder of a block's
/// SQL share.  Each takes offsets and allocates nothing.
/// </summary>
internal static class SqlText
{
    // What ends a line of SQL.  A C# documentation comment has more of them, and XmlDocWriter holds its own list.
    private static readonly char[] LineTerminators = ['\r', '\n'];

    /// <summary>
    /// The span from <paramref name="start" /> up to <paramref name="end" /> without the white space at either
    /// side.  When there is nothing else it is empty, at <paramref name="end" />.
    /// </summary>
    public static TextSpan Trim(string text, int start, int end)
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

    /// <summary>
    /// The offset of the first line terminator from <paramref name="start" /> up to <paramref name="end" />, or
    /// <paramref name="end" /> when there is none.
    /// </summary>
    public static int FindLineEnd(string text, int start, int end)
    {
        var terminator = text.IndexOfAny(LineTerminators, start, end - start);
        return terminator < 0 ? end : terminator;
    }

    /// <summary>
    /// The character at <paramref name="index" />, or <c>'\0'</c> at the end of the text and past it.
    /// </summary>
    public static char CharAt(string text, int index) => index < text.Length ? text[index] : '\0';
}
```

- [ ] **Step 6: Run the tests to see them pass**

Run: `dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj --filter-class "SqlSource.Tests.Parsing.SqlTextTests"`
Expected: PASS, 21 tests.

- [ ] **Step 7: Make the five files call it**

`src/SqlSource/Parsing/SqlMarkerReader.cs`:
- `Trim(text, valueStart, end)` becomes `SqlText.Trim(text, valueStart, end)`.
- Delete the private `Trim`.
- Delete `using Microsoft.CodeAnalysis.Text;`: nothing else in the file names a type of it.

`src/SqlSource/Parsing/Tokens/TokenScanner.cs`:
- `Trim(sql, after + 1, close)` becomes `SqlText.Trim(sql, after + 1, close)`.
- Each of the seven `CharAt(sql, ...)` becomes `SqlText.CharAt(sql, ...)`.
- Delete the private `Trim` and the private `CharAt`.  `SqlText` is in `SqlSource.Parsing`, the parent namespace, so no `using` is added.

`src/SqlSource/Parsing/SqlLexeme.cs`, the end of `GetContentSpan`, from `var start = Span.Start;` to its `return`, becomes:

```csharp
        var content = SqlText.Trim(text, Span.Start, Span.End);
        return content.IsEmpty ? null : content;
```

`src/SqlSource/Parsing/SqlLexer.cs`:
- Delete the field `LineTerminators`.
- Each of the two `FindLineEnd(start)` becomes `SqlText.FindLineEnd(text, start, text.Length)`.
- Each of the ten `CharAt(...)` becomes `SqlText.CharAt(text, ...)`, the argument unchanged.
- Delete the private `CharAt` and the private `FindLineEnd`.

`src/SqlSource/Parsing/SqlTextBuilder.cs`:
- Delete the field `LineTerminators`.
- Each of the two `FindLineEnd(index, end)` in `Writer` becomes `SqlText.FindLineEnd(source, index, end)`.
- Delete `Writer.FindLineEnd`.

- [ ] **Step 8: Check that no copy is left**

```bash
grep -rnE "LineTerminators|private .* (Trim|CharAt|FindLineEnd)\(" src/SqlSource/Parsing
```

Expected: two lines, both in `SqlText.cs`: the field and its one use.

- [ ] **Step 9: Add the rule to `src/SqlSource/AGENTS.md`**

In the section `## Parsing/`, after the bullet that starts `- **Lexemes cover the text without gaps.**`, add:

```markdown
- **The small readings of a text are in `SqlText`**: a trim to a span, the end of a line, and the character at an offset or `'\0'` past the end.  A reader that needs one calls it and does not copy it.  The two line terminators of SQL are there and nowhere else in `Parsing/`; `Generation/Emitting/XmlDocWriter.cs` has the five of a C# documentation comment, which is another rule.
```

- [ ] **Step 10: Run the generator's tests**

Run: `dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj`
Expected: all pass, the allocation tests among them.

- [ ] **Step 11: Format, validate, commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add -A src tests && git commit -m "Read a text's trim, line end and character in one place

SqlMarkerReader, TokenScanner and SqlLexeme each trimmed a span, SqlLexer
and SqlTextBuilder each found a line end, and two files looked up a
character or NUL.  SqlText holds the three, and each allocates nothing.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: A scope knows its kind (item 10)

**Files:**
- Create: `src/SqlSource/Parsing/SqlScopeKind.cs`
- Create: `tests/SqlSource.Tests/Parsing/SqlMarkerScopeTests.cs`
- Modify: `src/SqlSource/Parsing/SqlMarkerScope.cs`, `src/SqlSource/Parsing/SqlFileParser.cs`
- Modify: `src/SqlSource/AGENTS.md`

**Interfaces:**
- Produces: `enum SqlScopeKind { Preamble, Query, WholeFile }`
- Produces: `new SqlMarkerScope(string text, SqlDialectRules rules, SqlScopeKind scopeKind, List<SqlParseError> errors)`
- Produces: `SqlMarkerScope.Read(SqlMarker marker) : void`

- [ ] **Step 1: Write the failing test**

Create `tests/SqlSource.Tests/Parsing/SqlMarkerScopeTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class SqlMarkerScopeTests
{
    // Each row is a marker and whether a preamble and a query take it.  A file with no name marker is one query and
    // its own preamble, and takes what either takes.
    [Theory]
    [InlineData("-- generator: keep-comments", true, true)]
    [InlineData("-- database: main", true, true)]
    [InlineData("-- output: sql", true, true)]
    [InlineData("-- input-model-type: x", true, true)]
    [InlineData("-- output-model-type: x", true, true)]
    [InlineData("-- collection-type: x", true, true)]
    [InlineData("-- input-model-suffix: Args", true, false)]
    [InlineData("-- output-model-suffix: Row", true, false)]
    [InlineData("-- model-namespace: App.Models", true, false)]
    [InlineData("-- token: x", false, true)]
    [InlineData("-- token-ignore: x", false, true)]
    [InlineData("-- param: @a int", false, true)]
    [InlineData("-- input-model: UserArgs", false, true)]
    [InlineData("-- output-model: UserRow", false, true)]
    public void Read_Marker_IsTakenByTheScopesThatAllowIt(string line, bool inPreamble, bool inQuery)
    {
        IsTaken(line, SqlScopeKind.Preamble).ShouldBe(inPreamble);
        IsTaken(line, SqlScopeKind.Query).ShouldBe(inQuery);
        IsTaken(line, SqlScopeKind.WholeFile).ShouldBeTrue();
    }

    // A value that is not valid is another error, and is not what is asked here.
    private static bool IsTaken(string line, SqlScopeKind kind)
    {
        var marker = SqlMarkerReader.Read(line, SqlLexer.Lex(line, SqlDialectRules.Ansi).Lexemes[0]);
        var errors = new List<SqlParseError>();

        new SqlMarkerScope(line, SqlDialectRules.Ansi, kind, errors).Read(marker.ShouldNotBeNull());

        return !errors.Any(static error => error.Kind == SqlParseErrorKind.MarkerNotAllowedHere);
    }
}
```

- [ ] **Step 2: Run it to see it fail**

Run: `dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj --filter-class "SqlSource.Tests.Parsing.SqlMarkerScopeTests"`
Expected: the build fails with CS0103, `The name 'SqlScopeKind' does not exist in the current context`.

- [ ] **Step 3: Write `SqlScopeKind`**

Create `src/SqlSource/Parsing/SqlScopeKind.cs`:

```csharp
namespace SqlSource.Parsing;

/// <summary>
/// What a <see cref="SqlMarkerScope" /> is the scope of.  It decides which markers the scope takes, and a scope
/// never changes what it is.
/// </summary>
internal enum SqlScopeKind
{
    /// <summary>What comes before the first <c>-- name:</c> marker of a file that has one.</summary>
    Preamble,

    /// <summary>One query of a file that has a <c>-- name:</c> marker.</summary>
    Query,

    /// <summary>
    /// A file with no <c>-- name:</c> marker, which is one query and its own preamble: it takes what either takes.
    /// </summary>
    WholeFile,
}
```

- [ ] **Step 4: Give the scope its kind**

In `src/SqlSource/Parsing/SqlMarkerScope.cs`, the class declaration becomes:

```csharp
internal sealed class SqlMarkerScope(
    string text,
    SqlDialectRules rules,
    SqlScopeKind scopeKind,
    List<SqlParseError> errors
)
```

`Read`, from its documentation comment to its first `if`, becomes:

```csharp
    /// <summary>
    /// Reads one marker.  The scope takes it or reports it by what it is the scope of.
    /// </summary>
    public void Read(SqlMarker marker)
    {
        Level = null!; // Built again at the next read of Level.
        if (!IsAllowed(marker.Kind))
```

The body of that `if` and the rest of `Read` do not change.  Add above `IsAllowedInQuery`:

```csharp
    private bool IsAllowed(SqlMarkerKind kind) =>
        scopeKind switch
        {
            SqlScopeKind.Preamble => IsAllowedInPreamble(kind),
            SqlScopeKind.Query => IsAllowedInQuery(kind),
            _ => IsAllowedInQuery(kind) || IsAllowedInPreamble(kind),
        };
```

In `src/SqlSource/Parsing/SqlFileParser.cs`:

`ReadPreamble` makes its scope with `new SqlMarkerScope(text, rules, SqlScopeKind.Preamble, _errors)`, and its `scope.Read(marker, inQuery: false, inPreamble: true);` becomes `scope.Read(marker);`.

In `ReadBlock`, the branch for a marker of the scope becomes:

```csharp
                else if (marker.Kind != SqlMarkerKind.Name)
                {
                    (
                        scope ??= new SqlMarkerScope(
                            text,
                            rules,
                            preamble is null ? SqlScopeKind.WholeFile : SqlScopeKind.Query,
                            _errors
                        )
                    ).Read(marker);
                }
```

- [ ] **Step 5: Extend the bullet of `src/SqlSource/AGENTS.md`**

In `## Parsing/`, the bullet that starts `- **`SqlMarkerScope` is the one place that knows a marker's scope and value.**` gains, after its second sentence (`...to the scope of the preamble or of the query.`):

```markdown
A scope is made with its `SqlScopeKind`: `Preamble`, `Query`, or `WholeFile` for a file with no `-- name:` marker, which takes what either of the others takes.
```

- [ ] **Step 6: Run the tests**

Run: `dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj --filter-class "SqlSource.Tests.Parsing.SqlMarkerScopeTests"`
Expected: PASS, 14 tests.

Run: `dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj`
Expected: all pass.  `Parse_ModelMarkerInTheWrongScope_IsNotAllowedThere` holds the message of each scope and is not changed.

- [ ] **Step 7: Format, validate, commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add -A src tests && git commit -m "Make a marker scope with its kind, in place of two booleans at each read

Read took inQuery and inPreamble, and both true was a third state.  A scope
is now a Preamble, a Query or a WholeFile from its constructor on.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: The leftovers of the parser and the settings (item 11)

**Files:**
- Modify: `src/SqlSource/Parsing/SqlDialectMarker.cs`, `src/SqlSource/Parsing/SqlFileParser.cs`
- Modify: `src/SqlSource/Settings/SettingValue.cs`, `src/SqlSource/Settings/SettingsLevelReader.cs`, `src/SqlSource/Parsing/SqlMarkerScope.cs`
- Modify: `tests/SqlSource.Tests/Parsing/SqlMarkerReaderTests.cs`, `tests/SqlSource.Tests/Parsing/SqlDialectMarkerTests.cs`, `tests/SqlSource.Tests/Settings/SettingValueTests.cs`

**Interfaces:**
- Produces: `SettingValue.IsQualifiedName(ReadOnlySpan<char> value) : bool`
- Removes: `SettingValue.IsNamespace`, `SettingValue.IsTypeName`, `SqlDialectMarker.Describe`

- [ ] **Step 1: Move the tests to the members that stay**

In `tests/SqlSource.Tests/Parsing/SqlDialectMarkerTests.cs`, delete the theory `Describe_Marker_GivesTheWordAndTheTrimmedValue` with its four rows.

In `tests/SqlSource.Tests/Parsing/SqlMarkerReaderTests.cs`, the theory `Describe_Marker_IsItsWordAndItsValue` gains the four rows, after its three:

```csharp
    [InlineData("-- dialect: mysql", "dialect: mysql")]
    [InlineData("-- DIALECT:   MySql, ansi-quotes  ", "dialect: MySql, ansi-quotes")]
    [InlineData("-- dialect:", "dialect:")]
    [InlineData("-- dialect:   ", "dialect:")]
```

In `tests/SqlSource.Tests/Settings/SettingValueTests.cs`, the two theories `IsNamespace_IdentifiersJoinedByPeriods_IsANamespace` and `IsTypeName_AnIdentifierOrAFullName_IsATypeName` become one:

```csharp
    // The rule of a namespace and of a type's name, which is one identifier or a full name.
    [Theory]
    [InlineData("App", true)]
    [InlineData("App.Data.Models", true)]
    [InlineData("UserRow", true)]
    [InlineData("App.Models.UserRow", true)]
    [InlineData("", false)]
    [InlineData("App.", false)]
    [InlineData(".App", false)]
    [InlineData("App..Data", false)]
    [InlineData("App.class", false)]
    [InlineData("class", false)]
    [InlineData("App.1st", false)]
    [InlineData("global::App", false)]
    [InlineData("User Row", false)]
    [InlineData("UserRow<T>", false)]
    public void IsQualifiedName_IdentifiersJoinedByPeriods_IsAName(string value, bool expected) =>
        SettingValue.IsQualifiedName(value.AsSpan()).ShouldBe(expected);
```

- [ ] **Step 2: Run them to see the build fail**

Run: `dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj --filter-class "SqlSource.Tests.Settings.SettingValueTests"`
Expected: the build fails with CS0117, `'SettingValue' does not contain a definition for 'IsQualifiedName'`.

- [ ] **Step 3: Merge the two rules of `SettingValue`**

In `src/SqlSource/Settings/SettingValue.cs`, `IsNamespace` is renamed and documented as the rule of both, and `IsTypeName` is deleted with its documentation comment:

```csharp
    /// <summary>
    /// Whether <paramref name="value" /> is identifiers that are not reserved keywords, joined by periods.  It is
    /// the rule of a namespace and of the name of a type, which is one identifier or a full name: nothing in the
    /// text tells the two apart.
    /// </summary>
    public static bool IsQualifiedName(ReadOnlySpan<char> value)
```

The body does not change.

The four callers follow:
- `src/SqlSource/Settings/SettingsLevelReader.cs`: `SettingValue.IsNamespace` becomes `SettingValue.IsQualifiedName`.
- `src/SqlSource/Parsing/SqlMarkerScope.cs`: the one `SettingValue.IsNamespace(Value(marker))` and the two `SettingValue.IsTypeName(Value(marker))` become `SettingValue.IsQualifiedName(Value(marker))`.

- [ ] **Step 4: Take out the forwarder of `SqlDialectMarker`**

In `src/SqlSource/Parsing/SqlFileParser.cs`, the two `SqlDialectMarker.Describe(text, marker)` of `ReadDialect` become `SqlMarkerReader.Describe(text, marker)`.

In `src/SqlSource/Parsing/SqlDialectMarker.cs`, delete `Describe` with its documentation comment.

- [ ] **Step 5: Check that nothing names what went**

```bash
grep -rnE "IsNamespace|IsTypeName|SqlDialectMarker\.Describe" src tests --include='*.cs' --include='*.md'
```

Expected: no output.

- [ ] **Step 6: Run the tests**

Run: `dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj`
Expected: all pass.

- [ ] **Step 7: Format, validate, commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add -A src tests && git commit -m "Name the rule of a namespace and a type's name once, and describe a marker in one place

SettingValue.IsTypeName forwarded to IsNamespace, and
SqlDialectMarker.Describe to SqlMarkerReader.Describe.  The first pair is
IsQualifiedName; the second forwarder is gone.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: The leftovers of the generator and the snapshot (item 11)

**Files:**
- Create: `tests/SqlSource.Tests/Snapshot/SidecarJsonBuilderTests.cs`
- Modify: `src/SqlSource/Snapshot/SidecarJsonBuilder.cs`, `src/SqlSource/Snapshot/SidecarComparer.cs`
- Modify: `src/SqlSource/SqlSourceGenerator.cs`
- Modify: `src/SqlSource/AGENTS.md`

**Interfaces:**
- Produces: `SidecarJsonBuilder.Number(int? value) : string`
- Produces: `SidecarJsonBuilder.Flag(bool? value) : string`

- [ ] **Step 1: Write the failing tests**

Create `tests/SqlSource.Tests/Snapshot/SidecarJsonBuilderTests.cs`:

```csharp
using System.Globalization;
using Shouldly;
using SqlSource.Snapshot;
using Xunit;

namespace SqlSource.Tests.Snapshot;

public class SidecarJsonBuilderTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(-1, "-1")]
    [InlineData(2147483647, "2147483647")]
    [InlineData(null, "null")]
    public void Number_Value_IsWhatTheWriterWrites(int? value, string expected)
    {
        var json = new SidecarJsonBuilder();
        json.BeginArray(null);
        json.WriteNumber(null, value);
        json.EndArray();

        SidecarJsonBuilder.Number(value).ShouldBe(expected);
        json.ToText().ShouldBe("[\n  " + expected + "\n]\n");
    }

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    [InlineData(null, "null")]
    public void Flag_Value_IsWhatTheWriterWrites(bool? value, string expected)
    {
        var json = new SidecarJsonBuilder();
        json.BeginArray(null);
        json.WriteBoolean(null, value);
        json.EndArray();

        SidecarJsonBuilder.Flag(value).ShouldBe(expected);
        json.ToText().ShouldBe("[\n  " + expected + "\n]\n");
    }

    // In Swedish the minus sign of a number is U+2212.  A difference of --check reads the same on every machine.
    [Fact]
    public void Number_UnderAnotherCulture_IsWrittenTheSameWay()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("sv-SE");

            SidecarJsonBuilder.Number(-1).ShouldBe("-1");
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj --filter-class "SqlSource.Tests.Snapshot.SidecarJsonBuilderTests"`
Expected: the build fails with CS0117, `'SidecarJsonBuilder' does not contain a definition for 'Number'`.

- [ ] **Step 3: Give `SidecarJsonBuilder` the two formatters**

In `src/SqlSource/Snapshot/SidecarJsonBuilder.cs`, after `Quote`:

```csharp
    /// <summary>A number as the writer writes one, or <c>null</c>.</summary>
    public static string Number(int? value) =>
        value is { } number ? number.ToString(CultureInfo.InvariantCulture) : Null;

    /// <summary>A boolean as the writer writes one, or <c>null</c>.</summary>
    public static string Flag(bool? value) =>
        value switch
        {
            true => "true",
            false => "false",
            null => Null,
        };
```

`WriteNumber` and `WriteBoolean` become:

```csharp
    public void WriteNumber(string? key, int? value)
    {
        Item(key);
        _ = _text.Append(Number(value));
    }

    public void WriteBoolean(string? key, bool? value)
    {
        Item(key);
        _ = _text.Append(Flag(value));
    }
```

- [ ] **Step 4: Make `SidecarComparer` format through them**

In `src/SqlSource/Snapshot/SidecarComparer.cs`, `Number`, `Flag` and `Length` become:

```csharp
    private static SidecarDifference? Number(string path, int? committed, int? described) =>
        committed == described
            ? null
            : new SidecarDifference(path, SidecarJsonBuilder.Number(committed), SidecarJsonBuilder.Number(described));

    private static SidecarDifference? Flag(string path, bool? committed, bool? described) =>
        committed == described
            ? null
            : new SidecarDifference(path, SidecarJsonBuilder.Flag(committed), SidecarJsonBuilder.Flag(described));
```

```csharp
    private static SidecarDifference Length(string array, int committed, int described) =>
        new(array + ".length", SidecarJsonBuilder.Number(committed), SidecarJsonBuilder.Number(described));
```

Delete `Json(int? value)` and `Json(bool? value)`.  `Json(string? value)` stays, and so do the constant `Null` and `using System.Globalization;`, which `Element` and the labels still use.

- [ ] **Step 5: Run the snapshot's tests**

Run: `dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj --filter-class "SqlSource.Tests.Snapshot.SidecarJsonBuilderTests"`
Expected: PASS, 8 tests.

Run: `dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj --filter-class "SqlSource.Tests.Snapshot.SidecarComparerTests"`
Expected: PASS, unchanged.

- [ ] **Step 6: Make the two sort helpers of the generator one**

In `src/SqlSource/SqlSourceGenerator.cs`, `ToSortedFiles` and `ToSortedSettings`, each with its comment, become:

```csharp
    // One element for each path, the first the project lists, in the order of SqlPath.Comparer.
    private static EquatableArray<T> ToSorted<T>(ImmutableArray<T> items, Func<T, string> pathOf)
        where T : IEquatable<T> =>
        new(
            items
                .GroupBy(pathOf, SqlPath.Comparer)
                .Select(static group => group.First())
                .OrderBy(pathOf, SqlPath.Comparer)
                .ToImmutableArray()
        );
```

The two callers become:

```csharp
            .Select(static (files, _) => ToSorted(files, static file => file.NormalizedPath))
```

```csharp
            .Select(static (settings, _) => ToSorted(settings, static file => file.NormalizedPath))
```

In `src/SqlSource/AGENTS.md`, in the bullet `**A collected value needs a `Select` after it.**`, `or the `ToSorted` helpers at its end` becomes `or the `ToSorted` helper at its end`.

- [ ] **Step 7: Run the generator's tests**

Run: `dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj`
Expected: all pass.  `CachingTests` holds that the two steps still give equal values for equal input.

- [ ] **Step 8: Format, validate, commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add -A src tests && git commit -m "Format a sidecar's number and boolean in one place, and sort collected values with one helper

SidecarComparer wrote a number and a boolean as SidecarJsonBuilder does, in
code of its own, and SqlSourceGenerator had one sort in two element types.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: `ReadBlock` in three steps, and one record for `SqlParameterList.Create` (item 9)

**Files:**
- Create: `src/SqlSource/Parsing/SqlScannedQuery.cs`
- Modify: `src/SqlSource/Parsing/SqlParameterList.cs`, `src/SqlSource/Parsing/SqlFileParser.cs`
- Modify: `tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs`
- Modify: `src/SqlSource/AGENTS.md`
- Modify: this file, the table under "Measurements"

**Interfaces:**
- Consumes: `new SqlMarkerScope(text, rules, SqlScopeKind scopeKind, errors)` and `SqlMarkerScope.Read(SqlMarker)` from Task 2; `SqlMarkerReader.Describe` in `ReadDialect` from Task 3.
- Produces: `readonly record struct SqlScannedQuery(SqlBlockText Sql, EquatableArray<SqlTokenOccurrence> Occurrences, EquatableArray<SqlToken> Tokens, SqlDeclaredVariables? Variables)`
- Produces: `SqlParameterList.Create(SqlDialectRules rules, SqlScannedQuery query, IReadOnlyList<SqlTokenDefault> markerDefaults, IReadOnlyList<SqlParameterDeclaration> declarations, List<SqlParseError> errors) : EquatableArray<SqlQueryParameter>`

- [ ] **Step 1: Pin what the split must keep**

These tests pass before the change: they say what the parser does today, and the split must not change it.

In `tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs`, the theory `Parse_BlockWithoutSql_IsAnErrorAtItsName` gains two rows, after its five:

```csharp
    // A marker whose error comes from the query's parameters: no query is built for a block without SQL.
    [InlineData("-- name: A\n-- param: @a\n-- name: B\nSELECT 1")]
    [InlineData("-- name: A\n-- input-model: FindArgs\n-- name: B\nSELECT 1")]
```

Add after `Parse_ReservedKeywordTokenAroundAStrippedComment_IsReportedOverTheWholeToken`:

```csharp
    // The first token is in both forms of the SQL and the second in the kept form alone.
    [Fact]
    public void Parse_ReservedKeywordTokensUnderKeepComments_AreEachReportedOnce()
    {
        const string Text = "-- name: A\n-- generator: keep-comments\nSELECT {{class}} -- {{ class }}\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.ReservedTokenName, SpanOf(Text, "{{class}}"), "class"),
                SqlParseError.Create(SqlParseErrorKind.ReservedTokenName, SpanOf(Text, "{{ class }}"), "class"),
            ]);
    }
```

Run: `dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj --filter-class "SqlSource.Tests.Parsing.SqlFileParserTests"`
Expected: PASS.  If a new test fails, it is wrong about what the parser does today: stop and report it, and do not change the parser to make it pass.

- [ ] **Step 2: Write `SqlScannedQuery`**

Create `src/SqlSource/Parsing/SqlScannedQuery.cs`:

```csharp
using SqlSource.Parsing.Tokens;

namespace SqlSource.Parsing;

/// <summary>
/// The SQL of one query as built, with what was read from it.  Each is found from the one before, and
/// <see cref="SqlParameterList" /> reads them together.
/// </summary>
/// <param name="Sql">The SQL without comments, with where each parameter is in it.</param>
/// <param name="Occurrences">Where each token is in the SQL, in order.</param>
/// <param name="Tokens">The query's tokens, each name once.</param>
/// <param name="Variables">
/// The names the SQL declares as local variables, or null when the dialect has none or the SQL has no parameter.
/// </param>
internal readonly record struct SqlScannedQuery(
    SqlBlockText Sql,
    EquatableArray<SqlTokenOccurrence> Occurrences,
    EquatableArray<SqlToken> Tokens,
    SqlDeclaredVariables? Variables
);
```

- [ ] **Step 3: Make `SqlParameterList.Create` take it**

In `src/SqlSource/Parsing/SqlParameterList.cs`, the signature of `Create` and its first line become:

```csharp
    public static EquatableArray<SqlQueryParameter> Create(
        SqlDialectRules rules,
        SqlScannedQuery query,
        IReadOnlyList<SqlTokenDefault> markerDefaults,
        IReadOnlyList<SqlParameterDeclaration> declarations,
        List<SqlParseError> errors
    )
    {
        var (sql, occurrences, tokens, variables) = query;
        if (sql.Parameters.Length == 0 && declarations.Count == 0 && markerDefaults.Count == 0)
```

The rest of the body, and every private method, does not change.

- [ ] **Step 4: Split `ReadBlock`**

In `src/SqlSource/Parsing/SqlFileParser.cs`, `ReadBlock` with the comment above it is replaced by the following.  `BuildKept`, `ReadDialect`, `FindLastContent` and `AddError` do not change.

```csharp
        // preamble is null for a file with no name marker: the file is one query and its own preamble.  The three
        // steps and the two checks between them stay in this order: an error is added where it is found, and two
        // errors at one place are reported in the order they were added.
        private void ReadBlock(
            string name,
            TextSpan nameSpan,
            ResultShape? shape,
            SqlMarkerScope? preamble,
            int start,
            int end
        )
        {
            var lastContent = FindLastContent(start, end);
            var (scope, summary) = ReadBlockMarkers(
                preamble is null ? SqlScopeKind.WholeFile : SqlScopeKind.Query,
                start,
                end,
                lastContent
            );
            if (lastContent < 0)
            {
                AddError(SqlParseErrorKind.EmptyBlock, nameSpan);
                return;
            }

            var query = BuildQuery(scope, start, end);
            if (scope?.InputModel is { } inputModel && query.Parameters.Count == 0)
            {
                AddError(SqlParseErrorKind.InputModelWithoutParameters, inputModel.Marker.Span);
            }

            var (markers, kept) = ResolveSettings(scope, preamble, query, start, end);
            _blocks.Add(
                new SqlBlock(
                    name,
                    nameSpan,
                    summary,
                    shape,
                    query.Segments,
                    kept,
                    query.Tokens,
                    query.Parameters,
                    markers,
                    scope?.InputModel?.Name,
                    scope?.OutputModel
                )
            );
        }

        // Step one: the block's markers.  The scope is the query's, or null when no marker was handed to one, as
        // for most queries; the summary is its lines joined, or null.
        private (SqlMarkerScope? Scope, string? Summary) ReadBlockMarkers(
            SqlScopeKind kind,
            int start,
            int end,
            int lastContent
        )
        {
            SqlMarkerScope? scope = null;
            var summary = new List<string>();
            for (var index = start; index < end; index++)
            {
                if (SqlMarkerReader.Read(text, lexemes[index]) is not { } marker)
                {
                    continue;
                }

                if (lastContent >= 0 && index > lastContent)
                {
                    // A marker comes before the SQL it describes.  One after the block's last SQL would be taken by a
                    // reader to belong to the next block, so it is rejected and not applied.  A dialect marker there
                    // is past the header by definition, and is reported as misplaced too: it belongs at the top of
                    // the file, not above the next SQL.
                    AddError(SqlParseErrorKind.MarkerAtEndOfBlock, marker.Span);
                    if (marker.Kind == SqlMarkerKind.Dialect)
                    {
                        ReadDialect(marker);
                    }
                }
                else if (marker.Kind == SqlMarkerKind.Dialect)
                {
                    ReadDialect(marker);
                }
                else if (marker.Kind == SqlMarkerKind.Summary)
                {
                    if (!marker.ValueSpan.IsEmpty)
                    {
                        summary.Add(text.Substring(marker.ValueSpan.Start, marker.ValueSpan.Length));
                    }
                }
                else if (marker.Kind != SqlMarkerKind.Name)
                {
                    (scope ??= new SqlMarkerScope(text, rules, kind, _errors)).Read(marker);
                }
            }

            return (scope, summary.Count == 0 ? null : string.Join(" ", summary));
        }

        // Step two: the query.  The stripped SQL is what the query is: its tokens, its parameters and its hash come
        // from it.
        private BuiltQuery BuildQuery(SqlMarkerScope? scope, int start, int end)
        {
            var sql = SqlTextBuilder.Build(text, lexemes, start, end, keepComments: false);
            var scanned = TokenScanner.Scan(sql.Text, scope?.IgnoredTokens ?? SqlMarkerScope.NoNames);
            var firstScanError = _errors.Count;
            foreach (var error in scanned.Errors)
            {
                _errors.Add(error with { Span = sql.ToSourceSpan(error.Span) });
            }

            // A query without markers has no scope, and so no defaults and no declarations.
            var tokenDefaults = scope?.TokenDefaults ?? [];
            var tokens = SqlTokenList.Create(text, sql, scanned.Occurrences, tokenDefaults, _errors);

            // A variable is written in the SQL where it is declared, so SQL without parameters declares none.
            var variables =
                sql.Parameters.Length == 0 ? null : SqlDeclaredVariables.Find(text, lexemes, start, end, rules);
            var parameters = SqlParameterList.Create(
                rules,
                new SqlScannedQuery(sql, scanned.Occurrences, tokens, variables),
                tokenDefaults,
                scope?.Declarations ?? [],
                _errors
            );

            return new BuiltQuery(sql, scanned.Segments, tokens, parameters, firstScanError);
        }

        // Step three: what the parser can resolve of the settings.  A query that has a list uses it whole, so it
        // alone says whether the comments can be wanted.  A query without one may get the parameter from a level the
        // parser does not see.
        private (SettingsLevel Markers, EquatableArray<SqlSegment>? Kept) ResolveSettings(
            SqlMarkerScope? scope,
            SqlMarkerScope? preamble,
            BuiltQuery query,
            int start,
            int end
        )
        {
            var markers = (scope?.Level ?? SettingsLevel.None).Over(preamble?.Level ?? SettingsLevel.None);
            var keepsComments = markers.Parameters is { } list
                ? (list & GeneratorParameters.KeepComments) != 0
                : commentsWanted;

            return (
                markers,
                keepsComments
                    ? BuildKept(
                        start,
                        end,
                        query.Sql,
                        scope?.IgnoredTokens ?? SqlMarkerScope.NoNames,
                        query.FirstScanError
                    )
                    : null
            );
        }
```

Add at the end of the `Parser` class, after `AddError`:

```csharp
        // What step two gives.  FirstScanError is where the errors of the SQL's scan start in _errors: the kept form
        // is scanned too, and looks from there for an error it shares.
        private readonly record struct BuiltQuery(
            SqlBlockText Sql,
            EquatableArray<SqlSegment> Segments,
            EquatableArray<SqlToken> Tokens,
            EquatableArray<SqlQueryParameter> Parameters,
            int FirstScanError
        );
```

- [ ] **Step 5: Say in `src/SqlSource/AGENTS.md` why the order is fixed**

In `## Parsing/`, after the bullet that starts `- **`SqlMarkerScope` is the one place that knows a marker's scope and value.**`, add:

```markdown
- **`SqlFileParser.Parser.ReadBlock` is three steps in a fixed order**: the block's markers, the query, the settings, with the `EmptyBlock` check after the first and the `InputModelWithoutParameters` check after the second.  An error is added where it is found and the result sorts them by position with a stable sort, so moving a step changes which of two errors at one place comes first.  A block without SQL builds no query, so a marker whose error comes from the parameters is not reported for it.
```

- [ ] **Step 6: Run the parser's tests**

Run: `dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj`
Expected: all pass, with `SqlFileParserAllocationTests` inside its two budgets, which are not changed.

- [ ] **Step 7: Measure a parse after the last change**

Add this method to `tests/SqlSource.Tests/Parsing/SqlFileParserAllocationTests.cs`, with `using System.Diagnostics;` at the top.  It is the method of Task 1, and it is never committed.

```csharp
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Measure(bool keepsComments)
    {
        const int Parses = 2_000;
        var text = CreateFile(hasDialect: false, keepsComments);
        for (var parse = 0; parse < 200; parse++)
        {
            _ = SqlFileParser.Parse(text, "Queries.sql");
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        for (var parse = 0; parse < Parses; parse++)
        {
            _ = SqlFileParser.Parse(text, "Queries.sql");
        }

        watch.Stop();
        var bytes = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)(Parses * text.Length);
        Assert.Fail(
            $"keepsComments={keepsComments}: {watch.Elapsed.TotalMilliseconds * 1000 / Parses:F1} us for a parse, {bytes:F2} bytes for a character"
        );
    }
```

Run it three times:

```bash
dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj -c Release --filter-method "*SqlFileParserAllocationTests.Measure"
```

Write the middle of the three times, and the bytes, for each row into the "after" row of the table under "Measurements".  Then take the method out:

```bash
git checkout -- tests/SqlSource.Tests/Parsing/SqlFileParserAllocationTests.cs
```

The bytes must not be above the "before" row.  The time may differ by the noise between the three runs.  If the bytes rose, or the time rose by more than that noise, stop and find the cause before going on: a struct that is boxed, a `List` that was not there, or a lookup that no longer inlines.

- [ ] **Step 8: Format, validate, commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add -A src tests && git commit -m "Read a block in three steps, and give SqlParameterList.Create one record

ReadBlock did eleven things in about 115 lines.  It is now the block's
markers, the query and the settings, with the two checks between them where
they were, so the order of a block's errors is the same.  Create took eight
parameters, four of which were the built SQL and what was read from it.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Rewrite TD-0038, open the pull request

**Files:**
- Modify: `docs/tech-debt/TD-0038-duplicated-and-uneven-patterns-in-the-generator-and-the-tool.md`
- Modify: `docs/tech-debt/README.md` (the row of TD-0038)
- Add: `docs/superpowers/plans/2026-10-11-td-0038-pr3-parsing-and-generator.md` (this file)

- [ ] **Step 1: Run everything**

Run: `dotnet test --solution SqlSource.slnx`
Expected: all pass, the tool's tests among them: the tool parses with this code.

- [ ] **Step 2: Rewrite TD-0038 to what remains**

In the item's file, keeping the number of the item that stays:

- Under **Problem**, the second paragraph becomes: `Items 1 to 8, 10 and 11 of the review are resolved, and so is the generator's part of 9; the number of what remains is kept, written out so that a renderer does not count it again, since the [design](../superpowers/specs/2026-10-10-td-0038-duplicated-patterns-design.md) names it.`
- Delete the items **7**, **10** and **11**.
- Item **9** becomes: `- **9. Methods that do several things.**  `Cli.RunAsync` (about 130 lines in a nested `try`), `RunPlanner.ReadProject` and `RunPlanner.PlanFile` (about 100 lines each), `SidecarReader.ReadEntry`, `ProjectEvaluator.EvaluateOneAsync`.  `DatabaseRuns.DescribeAllAsync` takes eight parameters, several of which always travel together.`  Before writing the counts, read each method and correct a count that is no longer true.
- Under **Why it exists**, delete the last sentence, the one about item 7.
- Under **Impact**, the text becomes: `Low.  Nothing here is wrong today.  Each method makes a change to one of its responsibilities read, and risk, the others.`
- Under **Proposed fix**, the text becomes: `As the [design](../superpowers/specs/2026-10-10-td-0038-duplicated-patterns-design.md) says, in its pull request 4, held by the existing tests: split each by the responsibilities named, `SidecarReader.ReadEntry` within its allocation budget, with one record for what belongs to a session in `DatabaseRuns.DescribeAllAsync`.`
- Under **Trigger**: `Before the next phase that changes one of the methods of 9.`

In `docs/tech-debt/README.md`, the description of the row becomes:

> Methods of the tool and of the sidecar reader that do several things: `Cli.RunAsync`, `RunPlanner.ReadProject` and `PlanFile`, `SidecarReader.ReadEntry`, `ProjectEvaluator.EvaluateOneAsync`, and the eight parameters of `DatabaseRuns.DescribeAllAsync`

The status stays `Open` and the impact `Low`.  The file keeps its name: the id's links stay true.

- [ ] **Step 3: Validate and commit the documents**

```bash
./pre-commit-validation.sh
```

```bash
git add -A docs && git commit -m "Rewrite TD-0038 to what remains after Parsing/ and the generator, and add the plan

What remains is the long methods of the tool and of the sidecar reader, the
design's pull request 4.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 4: Check the version**

```bash
git fetch --tags origin
```

```bash
git tag --list 'v*' --sort=-v:refname | head -n 1
```

Expected: no output, as on 2026-10-11: the repository has no release tag, so there is nothing to compare.  If a tag is printed, compare it with `VersionPrefix` in `Directory.Build.props`, and ask the user for the new version if `VersionPrefix` is not greater.

- [ ] **Step 5: Ask the user, then push and open the pull request**

Pushing and opening a pull request are seen outside this machine: ask the user first.  Then:

```bash
git push -u origin claude/td-0038-parsing-and-generator
```

Open the pull request against `main` with `gh pr create`.  Its description says: what TD-0038 is and that this is the third of four pull requests of the design; the items resolved, one line each (7, and the generator's part of 9, 10 and 11); that nothing a user sees changes; the time and the allocation of a parse before and after, from the table under "Measurements", with how they were measured (a `Stopwatch` loop over a `Release` build, 3,000 parses to warm up and 5,000 timed, the middle of three runs); the points the plan settled beyond the design, from the top of this file; and that TD-0038 is rewritten, not deleted.  End it with `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.

Then follow the repository's rules for a pull request: reply in its thread to every CodeRabbit finding, with what changed or with why not.

---

## Measurements

A parse of the file of `SqlFileParserAllocationTests` (50 queries), `Release`, the middle of three runs.  Task 1 fills the first row and Task 5 the second.

Measured with 3,000 parses to warm up and 5,000 timed, not the 200 and 2,000 of the method above: with 200 the first row was still being compiled while it was timed, and gave 122 to 192 us for the plain parse.

| | Plain: time for a parse | Plain: bytes for a character | Keeps comments: time for a parse | Keeps comments: bytes for a character |
|----|----|----|----|----|
| before | 71.4 us | 11.31 | 123.8 us | 20.25 |
| after | 71.9 us | 11.31 | 125.6 us | 20.26 |

In exact bytes a parse of this file allocates 8 more than before, in both forms: 220,136 became 220,144 for the plain file of 19,456 characters, and 394,648 became 394,656 with comments.  It is the `SqlScopeKind` field, which every marker scope now has.  A parse of this file makes one scope, the preamble's, since its queries have no marker of their own; a file pays 8 bytes for its preamble's scope and 8 for each query that has such a marker, and nothing for a line or a character.  No budget sees the scope of a query: `docs/tech-debt/TD-0042-allocations-of-a-parse-that-no-budget-sees.md`.  The times differ by less than the three runs differ among themselves.
