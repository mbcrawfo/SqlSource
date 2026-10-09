# Query generation, phase 1: parameters and settings - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Parse, validate and resolve everything the later phases read from a `.sql` file, the attribute and MSBuild, with no effect yet beyond `keep-comments`, `no-token-validation` and `-- token-ignore:`.

**Architecture:** The lexer gains a `Parameter` lexeme.  One class, `SqlMarkerScope`, reads the markers of a preamble or a query, and the parser puts the result on `SqlBlock`: tokens with defaults, the parameter list, the shape, and a `SettingsLevel` of marker values.  A new folder, `Settings/`, holds the level record, the resolver and the value readers.  The pipeline reads a level from the properties, from each file's metadata and from the attribute, and resolves per type and query in `TypeEmitter`.  The parser always builds the comment-stripped SQL and builds the SQL with comments beside it when a per-file input says it may be wanted.

**Tech Stack:** C# source generator on `netstandard2.0` (Roslyn 4.8.0 floor), xunit v3 on Microsoft.Testing.Platform, Shouldly, CSharpier, MSBuild props and targets.

**Spec:** [`docs/superpowers/specs/2026-10-08-query-generation-phase-1-parameters-and-settings-design.md`](../specs/2026-10-08-query-generation-phase-1-parameters-and-settings-design.md).  Read it first; this plan argues from it.

## Global Constraints

- Branch: `claude/query-generation-phase-1-parameters-and-settings`.  One commit per task, in task order.
- Every commit passes `./pre-commit-validation.sh`.  Run `./format.sh`, then the validation as its own command, fix what it reports, then commit in a separate command.  A hook runs the validation again before any Bash command that contains the words of the commit command; never work around it.
- `src/SqlSource` targets `netstandard2.0` only and may use no Roslyn API newer than 4.8.0.  The attribute file, `AttributeSource.Text`, must compile as C# 7.3: no `#nullable`, no `string?`.
- No collection expression may target `ImmutableArray<T>`.  A model the pipeline caches is a record that holds collections in `EquatableArray<T>`.
- The allocation budgets in `tests/SqlSource.Tests/Parsing/SqlFileParserAllocationTests.cs` and `tests/SqlSource.Tests/Generation/PathResolverAllocationTests.cs` are never raised.  If a parse of the test's file goes over its budget, stop and ask the owner.
- A diagnostic that is added, removed or changed touches four places: the descriptor and `SqlDiagnostics.All` in `src/SqlSource/Diagnostics/SqlDiagnostics.cs`; `src/SqlSource/AnalyzerReleases.Unshipped.md`; the index row and section of `docs/diagnostics.md`; and `SqlDiagnostics.ForParseError` for a parser error.  A new `SqlParseErrorKind` goes at the end of the enum.  Write each `new DiagnosticDescriptor(...)` out in full with a literal id.
- Every MSBuild property of the package starts with `SqlSource`.
- Tests in `tests/SqlSource.Tests/Generator/` are compiled a second time against Roslyn 4.8.0: they use only API that version has and hand the compiler C# 12 at most.
- Files under `docs/superpowers/` are not rewritten, except the epic outline where a task says so.
- `README.md` links are absolute URLs.  Prose uses two spaces after a full stop, as the repository does.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Each task keeps `src/SqlSource/AGENTS.md` true for the types, steps and files it renames or adds.

Commands used throughout:

```bash
dotnet build SqlSource.slnx
```

```bash
dotnet test --solution SqlSource.slnx
```

One class, when a task says so (if the filter option is rejected, run the whole solution instead):

```bash
dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.Parsing.SqlFileParserTests
```

## Review Focus

Inputs the spec implies and does not list a test for.  Each has a test in the task named.

1. An ordinary comment that starts its line with a new marker word, `-- output: the rows we need` or `-- token: see below`, is now a marker; a reader expects one clear error at the value, not silence and not a crash.  Tasks 2 and 10.
2. `-- param:` as people type it: `NULL` in capitals, `not  null` with two spaces, a tab after the name, Windows line endings, and a type that merely ends in the letters of the word, `mynull`.  Task 4.
3. A token whose name is a reserved keyword and which has a default, `{{default:x}}`, must be `SQLSRC114`, as `{{default}}` is.  Task 2.
4. The name marker with a shape written tight or twice: `-- name: A->one`, `-- name: A -> one -> many`, `-- name: -> one`.  Task 6.
5. Two types claim one file and one of them asks for `keep-comments`; a token stands only inside a comment.  One type gets a method and the other a constant, and neither gets the other's SQL.  Task 9.

---

### Task 1: The `Parameter` lexeme and the static parameter list

**Files:**
- Modify: `src/SqlSource/Parsing/SqlLexemeKind.cs`, `SqlLexeme.cs`, `SqlDialectRules.cs`, `SqlLexer.cs`, `SqlTextBuilder.cs`, `SqlBlockText.cs`, `SqlBlock.cs`, `SqlFileParser.cs`
- Create: `src/SqlSource/Parsing/SqlQueryParameter.cs`, `src/SqlSource/Parsing/SqlParameterList.cs`
- Modify: `src/SqlSource/Generation/SqlQuery.cs`, `SqlFileReader.cs`
- Modify: `src/SqlSource/AGENTS.md`, `docs/tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md`
- Test: `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs`, `SqlDialectRulesTests.cs`, `SqlTextBuilderTests.cs`, `SqlFileParserTests.cs`, `SqlModelTests.cs`; `tests/SqlSource.Tests/Generation/SqlFileReaderTests.cs`, `TypeEmitterTests.cs`

**Interfaces:**
- Produces: `SqlLexemeKind.Parameter`; `SqlDialectRules.ParameterPrefix` (`char`); `SqlLexer.IsParameterNameCharacter(char)` (`internal static bool`); `SqlBlockText.Parameters` (`TextSpan[]`, offsets into `SqlBlockText.Text`, prefix included); `SqlQueryParameter(string Name, string? Type, bool? Nullable, bool IsDeclared)`; `SqlParameterList.Create(SqlBlockText sql)` returning `EquatableArray<SqlQueryParameter>`; `SqlBlock.Parameters` and `SqlQuery.Parameters`, the last member of each.

- [ ] **Step 1: Write the failing lexer tests**

Add to `SqlLexerTests`:

```csharp
[Theory]
[InlineData("WHERE id = @id", new[] { "Text:WHERE id = ", "Parameter:@id" })]
[InlineData("@a + @b", new[] { "Parameter:@a", "Text: + ", "Parameter:@b" })]
[InlineData("(@1)", new[] { "Text:(", "Parameter:@1", "Text:)" })]
[InlineData("@_a;", new[] { "Parameter:@_a", "Text:;" })]
[InlineData("@größe;", new[] { "Parameter:@größe", "Text:;" })]
[InlineData("'x'@p", new[] { "Quoted:'x'", "Parameter:@p" })]
public void Lex_Parameter_IsThePrefixAndAName(string text, string[] expected) => Lex(text).ShouldBe(expected);

[Theory]
[InlineData("a @> b")]
[InlineData("a <@ b")]
[InlineData("a @@ b")]
[InlineData("a @? b")]
[InlineData("SELECT @@ROWCOUNT")]
[InlineData("user@host")]
[InlineData("x_@y")]
[InlineData("1@y")]
[InlineData("@ x")]
[InlineData("@")]
public void Lex_AtSignThatStartsNoParameter_IsText(string text) => Lex(text).ShouldBe(["Text:" + text]);

[Theory]
[InlineData("'@a'", "Quoted:'@a'")]
[InlineData("\"@a\"", "Quoted:\"@a\"")]
[InlineData("-- @a", "LineComment:-- @a")]
[InlineData("/* @a */", "BlockComment:/* @a */")]
[InlineData("/*+ @a */", "Hint:/*+ @a */")]
public void Lex_AtSignInsideAQuoteACommentOrAHint_IsNotAParameter(string text, string expected) =>
    Lex(text).ShouldBe([expected]);

[Theory]
[InlineData("Ansi")]
[InlineData("SqlServer")]
[InlineData("PostgreSql")]
[InlineData("CockroachDb")]
[InlineData("MySql")]
[InlineData("MySql+AnsiQuotes+NoBackslashEscapes")]
[InlineData("MariaDb")]
[InlineData("Sqlite")]
[InlineData("Oracle")]
public void Lex_Parameter_IsFoundUnderEveryDialect(string dialect) =>
    Lex("x = @p", Rules(dialect)).ShouldBe(["Text:x = ", "Parameter:@p"]);

// Without the parameter the second part would continue the E string, and its backslash would escape the quote.
[Fact]
public void Lex_ParameterBetweenTwoStrings_EndsTheContinuation() =>
    Lex("E'a'\n@p\n'b\\' x", SqlDialectRules.PostgreSql)
        .ShouldBe(["Text:E", "Quoted:'a'", "Text:\n", "Parameter:@p", "Text:\n", "Quoted:'b\\'", "Text: x"]);

[Fact]
public void Lex_TextWithParameters_IsCoveredWithoutGaps()
{
    const string Text = "SELECT @a,@b FROM t WHERE x@y = '@z' -- @c\n";

    var lexemes = SqlLexer.Lex(Text, SqlDialectRules.Ansi).Lexemes;

    string.Concat(lexemes.Select(lexeme => Text.Substring(lexeme.Span.Start, lexeme.Span.Length))).ShouldBe(Text);
}
```

Add to `SqlDialectRulesTests`:

```csharp
[Fact]
public void ParameterPrefix_IsTheAtSignInEveryDialectAndOptionSet()
{
    foreach (var dialect in Enum.GetValues<SqlDialect>())
    {
        foreach (var options in new[] { SqlDialectOptions.None, SqlDialectOptions.AnsiQuotes, SqlDialectOptions.NoBackslashEscapes })
        {
            var rules = SqlDialectRules.For(new SqlDialectChoice(dialect, options));

            rules.ParameterPrefix.ShouldBe('@');
            rules.FindStarter("abc @p", 0).ShouldBe(4);
        }
    }
}
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet build SqlSource.slnx`
Expected: FAIL, `SqlDialectRules` has no `ParameterPrefix`.

- [ ] **Step 3: Add the lexeme kind and the prefix**

`SqlLexemeKind.cs`, a member at the end:

```csharp
    /// <summary>A parameter: the dialect's prefix and a name, as in <c>@id</c>.  SQL content, copied as written.</summary>
    Parameter,
```

`SqlLexeme.GetContentSpan`: the first test becomes `Kind is SqlLexemeKind.Quoted or SqlLexemeKind.Hint or SqlLexemeKind.Parameter`.

`SqlDialectRules.cs`: the constructor takes the prefix, which joins the starters.

```csharp
    // The prefix of a parameter in every dialect SqlSource reads today.  It is a value of the rules so that an engine
    // with another prefix, Oracle's ":name", is a rule and not a rewrite.
    private const char AtSign = '@';

    private SqlDialectRules(params (char Opener, QuoteReader Reader)[] readers)
        : this(AtSign, readers) { }

    private SqlDialectRules(char parameterPrefix, (char Opener, QuoteReader Reader)[] readers)
    {
        ParameterPrefix = parameterPrefix;
        _starters = new char[CommentStarters.Length + 1 + readers.Length];
        CommentStarters.CopyTo(0, _starters, 0, CommentStarters.Length);
        _starters[CommentStarters.Length] = parameterPrefix;
        for (var index = 0; index < readers.Length; index++)
        {
            var (opener, reader) = readers[index];
            _readers[opener] = reader;
            _starters[CommentStarters.Length + 1 + index] = opener;
        }
    }

    /// <summary>The character that starts a parameter, as in <c>@id</c>.</summary>
    public char ParameterPrefix { get; }
```

Reword the comment on `_starters` to "a comment in any dialect, a parameter, or a quoted region in this one".

- [ ] **Step 4: Read a parameter in the lexer**

`SqlLexer.Step`, a branch before `else if (Rules.ReaderFor(current) is { } reader)`:

```csharp
        else if (current == Rules.ParameterPrefix && TryFindParameterEnd(start, out var parameterEnd))
        {
            Add(SqlLexemeKind.Parameter, start, parameterEnd);
            PassGap(allowed: false);
        }
```

And the two helpers:

```csharp
    /// <summary>Whether <paramref name="value" /> can be part of a parameter's name: a letter, a digit or <c>_</c>.</summary>
    internal static bool IsParameterNameCharacter(char value) => char.IsLetterOrDigit(value) || value == '_';

    // A parameter is the prefix and a name.  The prefix follows neither another prefix nor a character of a name:
    // "@@rowcount" is a function, and the "@" of "user@host" stands inside a word.
    private bool TryFindParameterEnd(int start, out int end)
    {
        end = start + 1;
        if (start > 0 && (text[start - 1] == Rules.ParameterPrefix || IsParameterNameCharacter(text[start - 1])))
        {
            return false;
        }

        while (end < text.Length && IsParameterNameCharacter(text[end]))
        {
            end++;
        }

        return end > start + 1;
    }
```

Update the class summary: "Splits SQL text into quoted regions, comments, hints, parameters and plain text."

- [ ] **Step 5: Run the lexer tests**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.Parsing.SqlLexerTests`
Expected: PASS.  Then run the whole solution: the existing tests that hold `@` in SQL fail where they compare lexemes or built SQL; the next steps fix the builder.

- [ ] **Step 6: Write the failing builder and parser tests**

`SqlTextBuilderTests`:

```csharp
[Fact]
public void Build_Parameters_AreCopiedAndTheirPlacesInTheSqlAreKept()
{
    const string Text = "SELECT @a, -- note @x\n    @b /* c */ FROM t\n\nWHERE x = @a;\n";
    var lexemes = SqlLexer.Lex(Text, SqlDialectRules.Ansi).Lexemes;

    var built = SqlTextBuilder.Build(Text, lexemes, 0, lexemes.Count, keepComments: false);

    built.Text.ShouldBe("SELECT @a,\n    @b   FROM t\nWHERE x = @a;");
    built.Parameters.Select(span => built.Text.Substring(span.Start, span.Length)).ShouldBe(["@a", "@b", "@a"]);
}
```

`SqlFileParserTests`:

```csharp
[Theory]
[InlineData("SELECT @a, @b, @a", new[] { "a", "b" })]
[InlineData("SELECT @Id, @ID, @id", new[] { "Id" })]
[InlineData("SELECT '@x', @y -- @z", new[] { "y" })]
[InlineData("SELECT @@ROWCOUNT, a @> b", new string[0])]
public void Parse_Parameters_AreThoseOfTheSqlInOrderOfFirstAppearance(string sql, string[] expected)
{
    var block = Blocks("-- name: Q\n" + sql + "\n").ShouldHaveSingleItem();

    block.Parameters.Select(static parameter => parameter.Name).ShouldBe(expected);
    block.Parameters.ShouldAllBe(static parameter => parameter.Type == null && parameter.Nullable == null && !parameter.IsDeclared);
    Sql(block).ShouldBe(sql.Replace(" -- @z", string.Empty, StringComparison.Ordinal));
}

[Fact]
public void Parse_KeptComment_HoldsNoParameter() =>
    Blocks("-- name: Q\n-- generator: keep-comments\nSELECT @a -- @b\n")
        .ShouldHaveSingleItem()
        .Parameters.Select(static parameter => parameter.Name)
        .ShouldBe(["a"]);
```

`SqlFileReaderTests`: in `Read_...` tests that compare with `new SqlQuery(...)`, add the parameters argument; and add:

```csharp
[Fact]
public void Read_QueryWithParameters_CopiesThem() =>
    Read("-- name: Q\nSELECT @a, @b;\n")
        .Queries.ShouldHaveSingleItem()
        .Parameters.Select(static parameter => parameter.Name)
        .ShouldBe(["a", "b"]);
```

- [ ] **Step 7: Copy a parameter in the builder and record where it lands**

`SqlBlockText.cs`: the constructor becomes `SqlBlockText(string text, (int Output, int Source)[] runs, TextSpan[] parameters)` and gains

```csharp
    /// <summary>
    /// Where each parameter of the SQL is in <see cref="Text" />, prefix included, in order.
    /// </summary>
    public TextSpan[] Parameters { get; } = parameters;
```

`SqlTextBuilder.Build`: the empty result is `new SqlBlockText(string.Empty, [], [])`.  In `Writer`:

```csharp
        private readonly List<TextSpan> _parameters = [];
```

`Append`, before the comment branches:

```csharp
            else if (lexeme.Kind == SqlLexemeKind.Parameter)
            {
                // A parameter is on a line with content and never on a marker's line, so the line is kept and the
                // offset stands.
                _parameters.Add(new TextSpan(_position, lexeme.Span.Length));
                AppendLines(lexeme);
            }
```

It goes after the `SqlMarkerReader.Read` branch and before `else if (keepComments || lexeme.Kind == SqlLexemeKind.Text)`.  `Finish` returns `new SqlBlockText(new string(buffer, 0, _finished), [.. _runs], [.. _parameters])`.  Add `using Microsoft.CodeAnalysis.Text;`.

- [ ] **Step 8: Add the parameter model and the list**

`src/SqlSource/Parsing/SqlQueryParameter.cs`:

```csharp
namespace SqlSource.Parsing;

/// <summary>
/// One parameter of a query.
/// </summary>
/// <param name="Name">
/// The name without its prefix: as the SQL first writes it, or as the <c>-- param:</c> marker writes it when the SQL
/// does not hold the parameter.
/// </param>
/// <param name="Type">The database type a <c>-- param:</c> marker gives, as written, or null.</param>
/// <param name="Nullable">
/// True when a marker says <c>null</c>, false when it says <c>not null</c>, and null when it says neither or there is
/// no marker.  Null and false both mean the parameter is not nullable.
/// </param>
/// <param name="IsDeclared">Whether a <c>-- param:</c> marker names the parameter.</param>
internal sealed record SqlQueryParameter(string Name, string? Type, bool? Nullable, bool IsDeclared);
```

`src/SqlSource/Parsing/SqlParameterList.cs`:

```csharp
using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// Builds the parameter list of one query: the parameters of its SQL in order of first appearance, compared
/// ignoring case.
/// </summary>
internal static class SqlParameterList
{
    public static EquatableArray<SqlQueryParameter> Create(SqlBlockText sql)
    {
        if (sql.Parameters.Length == 0)
        {
            return EquatableArray<SqlQueryParameter>.Empty;
        }

        var parameters = ImmutableArray.CreateBuilder<SqlQueryParameter>();
        foreach (var span in sql.Parameters)
        {
            if (IndexOf(parameters, sql.Text, span) < 0)
            {
                parameters.Add(new SqlQueryParameter(NameOf(sql.Text, span), null, null, false));
            }
        }

        return new EquatableArray<SqlQueryParameter>(parameters.ToImmutable());
    }

    // The span holds the prefix, which is one character.
    private static string NameOf(string sql, TextSpan span) => sql.Substring(span.Start + 1, span.Length - 1);

    private static int IndexOf(ImmutableArray<SqlQueryParameter>.Builder parameters, string sql, TextSpan span)
    {
        var name = sql.AsSpan(span.Start + 1, span.Length - 1);
        for (var index = 0; index < parameters.Count; index++)
        {
            if (name.Equals(parameters[index].Name.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }
}
```

`SqlBlock` and `SqlQuery` each gain a last member, with its documentation:

```csharp
/// <param name="Parameters">The query's parameters, in order of first appearance.  Empty when it has none.</param>
    EquatableArray<SqlQueryParameter> Parameters
```

`SqlFileParser.ReadBlock` passes `SqlParameterList.Create(sql)` as the last argument of `new SqlBlock(...)`; `SqlFileReader.Read` passes `block.Parameters` to `new SqlQuery(...)`.

- [ ] **Step 9: Fix the tests that build the models by hand**

Run `git grep -n 'new SqlQuery(\|new SqlBlock(' -- tests` and add `EquatableArray<SqlQueryParameter>.Empty` as the last argument where a query has no parameter, and `TestModels.Array(new SqlQueryParameter("id", null, null, false))` where its SQL has `@id`.  In `TypeEmitterTests`, `Query` and `TokenQuery` pass `EquatableArray<SqlQueryParameter>.Empty`: the emitter does not read the list.

- [ ] **Step 10: Run everything**

Run: `dotnet test --solution SqlSource.slnx`
Expected: PASS.  If `SqlFileParserAllocationTests` fails, stop and report the measured bytes for each character: the budget is not raised.

- [ ] **Step 11: Measure the parse**

Add a temporary line to `Parse_TypicalFile_AllocatesWithinItsBudget` that writes `allocated / (double)(Iterations * text.Length)` with `TestContext.Current.SendDiagnosticMessage`, run the class on this commit and on `main`, note both numbers for the pull request, and remove the line.

- [ ] **Step 12: Update the documents**

- `src/SqlSource/AGENTS.md`, under `Parsing/`: add "**A parameter is a lexeme.**  `SqlLexer` finds `@name` by `SqlDialectRules.ParameterPrefix`, outside quoted regions, comments and hints, and `SqlTextBuilder` records where each lands in the built SQL.  `SqlParameterList` is the one place that turns those into a query's list; names compare ignoring case."
- `docs/tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md`: add three rows to the table.

```markdown
| `@x` written for the absolute value of `x`, with no space | PostgreSQL | As the parameter `x`.  Npgsql reads it the same way; write `abs(x)` or `@ x`. |
| A local variable, `DECLARE @n int`, or a user variable, `SET @n := 1` | SQL Server, MySQL, MariaDB | As a parameter, at each place it is written. |
| A parameter inside a hint, `/*+ ... @p ... */` or `/*! ... @p ... */` | Every dialect | Not as a parameter: a hint is copied whole and is not searched. |
```

  And one sentence under Impact: "The parameter rows matter from the phase that generates a type for each parameter; until then a parameter list has no effect."

- [ ] **Step 13: Commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add -A && git commit -m "Find parameters in the lexer and carry a query's list

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Token defaults, the `-- token:` marker and `SqlMarkerScope`

**Files:**
- Modify: `src/SqlSource/Parsing/TokenScanner.cs`, `TokenScanResult.cs`, `SqlMarkerKind.cs`, `SqlMarkerReader.cs`, `SqlDialectMarker.cs`, `SqlParseErrorKind.cs`, `SqlFileParser.cs`, `SqlBlock.cs`, `SqlParameterList.cs`
- Create: `src/SqlSource/Parsing/SqlTokenOccurrence.cs`, `SqlToken.cs`, `SqlTokenDefault.cs`, `SqlTokenList.cs`, `SqlMarkerScope.cs`
- Modify: `src/SqlSource/Generation/SqlQuery.cs`, `SqlFileReader.cs`; `src/SqlSource/Diagnostics/SqlDiagnostics.cs`; `src/SqlSource/AnalyzerReleases.Unshipped.md`
- Modify: `README.md`, `docs/diagnostics.md`, `src/SqlSource/AGENTS.md`
- Test: `tests/SqlSource.Tests/Parsing/TokenScannerTests.cs`, `SqlMarkerReaderTests.cs`, `SqlFileParserTests.cs`; `tests/SqlSource.Tests/Diagnostics/SqlDiagnosticsTests.cs`; `tests/SqlSource.Tests/Generator/GeneratedSourceTests.cs`

**Interfaces:**
- Consumes: `SqlBlockText.Parameters`, `SqlParameterList`, `SqlLexer.Lex`.
- Produces:
  - `TokenScanner.TryReadToken(string sql, int start, out string name, out TextSpan? defaultSpan, out int end)`: `defaultSpan` is the trimmed default's place in `sql`, null when the token has no colon.
  - `SqlTokenOccurrence(string Name, string? Default, TextSpan Span)`, a `readonly record struct`; `TokenScanResult.Occurrences` (`EquatableArray<SqlTokenOccurrence>`), spans into the scanned SQL, in order.
  - `SqlToken(string Name, string? Default)`, a sealed record; `SqlBlock.Tokens` and `SqlQuery.Tokens` (`EquatableArray<SqlToken>`), after `Segments`.
  - `SqlTokenDefault(string Name, string Text, int Offset, SqlMarker Marker)`, a `readonly record struct`: a default a `-- token:` marker gives, and the offset in the file where its text starts.
  - `SqlMarkerKind.Token`; `SqlMarkerReader.WordOf(SqlMarkerKind)` and `SqlMarkerReader.Describe(string text, SqlMarker marker)`.
  - `SqlMarkerScope(string text, SqlDialectRules rules, List<SqlParseError> errors)` with `Read(SqlMarker marker, bool inQuery, bool inPreamble)`, `Generator` (`SqlGeneratorParameterScope`) and `TokenDefaults` (`IReadOnlyList<SqlTokenDefault>`).
  - `SqlParseErrorKind.MarkerNotAllowedHere`, `SqlDiagnostics.MarkerNotAllowedHere` (`SQLSRC116`).
  - `SqlParameterList.Create(SqlBlockText sql, EquatableArray<SqlTokenOccurrence> occurrences)`.

- [ ] **Step 1: Write the failing scanner tests**

`TokenScannerTests`: the helper `Scan` stays.  Add a helper and tests:

```csharp
private static string[] Defaults(string sql, params string[] ignoredNames) =>
    [
        .. TokenScanner
            .Scan(sql, new HashSet<string>(ignoredNames))
            .Occurrences.Select(token => token.Name + "=" + (token.Default ?? "<none>")),
    ];

[Theory]
[InlineData("{{a:b}}", "a=b")]
[InlineData("{{ a : b c }}", "a=b c")]
[InlineData("{{a:}}", "a=")]
[InlineData("{{a:  }}", "a=")]
[InlineData("{{cast:x::int}}", "cast=x::int")]
[InlineData("{{a:x\n  AND y}}", "a=x\n  AND y")]
[InlineData("{{a:{b} }}", "a={b}")]
[InlineData("{{a}}", "a=<none>")]
public void Scan_TokenWithADefault_ReadsTheNameAndTheTrimmedDefault(string sql, string expected)
{
    Defaults(sql).ShouldBe([expected]);
    Scan(sql).ShouldBe(["T:" + expected[..expected.IndexOf('=')]]);
}

[Theory]
[InlineData("{{a:b}")]
[InlineData("{{a:b")]
[InlineData("{{a b:c}}")]
[InlineData("{{1a:c}}")]
[InlineData("{{:c}}")]
public void Scan_TextThatOnlyLooksLikeATokenWithADefault_IsLiteral(string sql) => Scan(sql).ShouldBe(["L:" + sql]);

[Fact]
public void Scan_Occurrences_HaveTheirPlaceInTheSql()
{
    const string Sql = "a {{x}} b {{y:1}} c";

    var occurrences = TokenScanner.Scan(Sql, new HashSet<string>()).Occurrences;

    occurrences.Select(token => Sql.Substring(token.Span.Start, token.Span.Length)).ShouldBe(["{{x}}", "{{y:1}}"]);
}

[Fact]
public void Scan_IgnoredTokenWithADefault_StaysLiteralWithItsDefault()
{
    Scan("x {{raw:@a}} y", "raw").ShouldBe(["L:x {{raw:@a}} y"]);
    Defaults("x {{raw:@a}} y", "raw").ShouldBeEmpty();
}

// A keyword is a keyword with a default too.
[Fact]
public void Scan_ReservedNameWithADefault_IsAnError()
{
    var error = TokenScanner.Scan("{{default:x}}", new HashSet<string>()).Errors.ShouldHaveSingleItem();

    error.Kind.ShouldBe(SqlParseErrorKind.ReservedTokenName);
    error.Span.ShouldBe(new TextSpan(0, 13));
    error.Arguments.ShouldBe(["default"]);
}
```

- [ ] **Step 2: Read a default in the scanner**

`src/SqlSource/Parsing/SqlTokenOccurrence.cs`:

```csharp
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// One place a token is written in a block's SQL.
/// </summary>
/// <param name="Name">The token's name.</param>
/// <param name="Default">The default written there, trimmed.  Empty for <c>{{name:}}</c>, null for <c>{{name}}</c>.</param>
/// <param name="Span">The whole token, braces included, as offsets into the scanned SQL.</param>
internal readonly record struct SqlTokenOccurrence(string Name, string? Default, TextSpan Span);
```

`TokenScanResult` gains `EquatableArray<SqlTokenOccurrence> Occurrences` between `Segments` and `Errors`, documented "Each token of <paramref name="Segments" />, in order, with its place and its default."

`TokenScanner`: the summary says "`{{name}}` and `{{name:default}}` tokens".  `Scan` collects occurrences in the branch that adds a token segment:

```csharp
                var span = TextSpan.FromBounds(index, end);
                AddLiteral(segments, sql, literalStart, index);
                segments.Add(new SqlSegment(SqlSegmentKind.Token, name));
                occurrences.Add(
                    new SqlTokenOccurrence(
                        name,
                        defaultSpan is { } place ? sql.Substring(place.Start, place.Length) : null,
                        span
                    )
                );
```

with `var occurrences = ImmutableArray.CreateBuilder<SqlTokenOccurrence>();` beside the other builders, `TryReadToken(sql, index, out var name, out var defaultSpan, out var end)` in the loop, and the result built with `new EquatableArray<SqlTokenOccurrence>(occurrences.ToImmutable())`.  The reader becomes public and reads the default:

```csharp
    /// <summary>
    /// Reads the token that starts at <paramref name="start" />: <c>{{</c>, blanks, an identifier, blanks, then
    /// <c>}}</c>, or a colon and a default that runs to the first <c>}}</c>.  Anything else that starts with
    /// <c>{{</c> is literal text, and the result is false.
    /// </summary>
    /// <param name="defaultSpan">
    /// Where the default is in <paramref name="sql" />, without the white space around it; empty for
    /// <c>{{name:}}</c>, and null for a token with no colon.
    /// </param>
    public static bool TryReadToken(string sql, int start, out string name, out TextSpan? defaultSpan, out int end)
    {
        name = string.Empty;
        defaultSpan = null;
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

        var after = SkipBlanks(sql, nameEnd);
        int close;
        if (CharAt(sql, after) == ':')
        {
            close = sql.IndexOf("}}", after + 1, StringComparison.Ordinal);
            if (close < 0)
            {
                return false;
            }
        }
        else if (CharAt(sql, after) == '}' && CharAt(sql, after + 1) == '}')
        {
            close = after;
        }
        else
        {
            return false;
        }

        name = sql.Substring(nameStart, nameEnd - nameStart);
        if (!SqlIdentifier.IsValid(name))
        {
            return false;
        }

        if (close > after)
        {
            defaultSpan = Trim(sql, after + 1, close);
        }

        end = close + 2;
        return true;
    }

    private static TextSpan Trim(string sql, int start, int end)
    {
        while (start < end && char.IsWhiteSpace(sql[start]))
        {
            start++;
        }

        while (end > start && char.IsWhiteSpace(sql[end - 1]))
        {
            end--;
        }

        return TextSpan.FromBounds(start, end);
    }
```

Add `using System;`.

Run: `dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.Parsing.TokenScannerTests`
Expected: PASS.

- [ ] **Step 3: Write the failing marker and parser tests**

`SqlMarkerReaderTests`, following the file's existing theory for keywords: `-- token: {{a:b}}`, `-- TOKEN: x` and `--token:` are markers of kind `Token`; `-- tokens: x` and `-- token x` are not markers.

`SqlFileParserTests`:

```csharp
private static string[] Tokens(SqlBlock block) =>
    [.. block.Tokens.Select(static token => token.Name + "=" + (token.Default ?? "<none>"))];

[Fact]
public void Parse_Tokens_AreListedOnceInOrderOfFirstAppearanceWithTheirDefaults()
{
    const string Text =
        "-- name: Q\n-- token: {{filter:AND x = 1}}\nSELECT {{cols}} FROM {{table:users}} "
        + "WHERE 1 = 1 {{filter}} {{table}} {{filter}}\n";

    var block = Blocks(Text).ShouldHaveSingleItem();

    Tokens(block).ShouldBe(["cols=<none>", "table=users", "filter=AND x = 1"]);
    Sql(block).ShouldBe("SELECT {{cols}} FROM {{table}} WHERE 1 = 1 {{filter}} {{table}} {{filter}}");
}

[Fact]
public void Parse_TokenMarkerForATokenTheQueryLacks_IsNotAnError() =>
    Tokens(Blocks("-- name: Q\n-- token: {{other:x}}\nSELECT {{a}}\n").ShouldHaveSingleItem()).ShouldBe(["a=<none>"]);

[Fact]
public void Parse_TheSameDefaultTwice_IsNotAConflict() =>
    Tokens(Blocks("-- name: Q\n-- token: {{a:x}}\n-- token: {{a:x}}\nSELECT {{a:x}} {{a: x }}\n").ShouldHaveSingleItem())
        .ShouldBe(["a=x"]);

[Theory]
// Two inline defaults: at the second.
[InlineData("-- name: Q\nSELECT {{a:x}} {{a:y}}\n", "{{a:y}}", "{{a:y}}")]
// Two markers: at the second marker's value.
[InlineData("-- name: Q\n-- token: {{a:x}}\n-- token: {{a:y}}\nSELECT {{a}}\n", "{{a:y}}", "token: {{a:y}}")]
// A marker, then an inline default: at the inline one.
[InlineData("-- name: Q\n-- token: {{a:x}}\nSELECT {{a:y}}\n", "{{a:y}}", "{{a:y}}")]
// An inline default, then a marker: at the marker's value.
[InlineData("-- name: Q\nSELECT {{a:y}}\n-- token: {{a:x}}\nFROM t\n", "{{a:x}}", "token: {{a:x}}")]
public void Parse_TwoDefaultsForOneTokenThatDiffer_ConflictAtTheSecond(string text, string at, string argument) =>
    Errors(text).ShouldBe([SqlParseError.Create(SqlParseErrorKind.ConflictingSettings, SpanOf(text, at), argument)]);

[Theory]
[InlineData("{{a}}")]
[InlineData("{{a:x}} y")]
[InlineData("x {{a:y}}")]
[InlineData("{{a:x}}{{b:y}}")]
[InlineData("see below")]
[InlineData("{{a:'open}}")]
[InlineData("{{a:/* open}}")]
public void Parse_TokenMarkerThatIsNotOneTokenWithADefault_IsInvalid(string value)
{
    var text = "-- name: Q\n-- token: " + value + "\nSELECT {{a}}\n";

    Errors(text)
        .ShouldBe([SqlParseError.Create(SqlParseErrorKind.InvalidMarkerValue, SpanOf(text, value), "token: " + value)]);
}

[Fact]
public void Parse_TokenMarkerWithoutAValue_IsInvalidAtTheMarker()
{
    const string Text = "-- name: Q\n-- token:\nSELECT 1\n";

    Errors(Text)
        .ShouldBe([SqlParseError.Create(SqlParseErrorKind.InvalidMarkerValue, SpanOf(Text, "-- token:"), "token:")]);
}

[Fact]
public void Parse_TokenMarkerWithAReservedName_IsAnError()
{
    const string Text = "-- name: Q\n-- token: {{class:x}}\nSELECT 1\n";

    Errors(Text)
        .ShouldBe([SqlParseError.Create(SqlParseErrorKind.ReservedTokenName, SpanOf(Text, "{{class:x}}"), "class")]);
}

[Fact]
public void Parse_TokenMarkerInThePreamble_IsNotAllowedThere()
{
    const string Text = "-- token: {{a:x}}\n-- name: Q\nSELECT {{a}}\n";

    Errors(Text)
        .ShouldBe([
            SqlParseError.Create(
                SqlParseErrorKind.MarkerNotAllowedHere,
                SpanOf(Text, "-- token: {{a:x}}"),
                "token",
                "inside a query"
            ),
        ]);
}

[Fact]
public void Parse_TokenMarkerInAFileWithoutNameMarker_IsAllowed() =>
    Tokens(Blocks("-- token: {{a:x}}\nSELECT {{a}}\n").ShouldHaveSingleItem()).ShouldBe(["a=x"]);

[Fact]
public void Parse_ParameterInsideAToken_IsNotAParameterOfTheSql()
{
    var block = Blocks("-- name: Q\nSELECT @a {{f:AND x = @b}} {{t}}@c\n").ShouldHaveSingleItem();

    block.Parameters.Select(static parameter => parameter.Name).ShouldBe(["a", "c"]);
}

[Fact]
public void Parse_ParameterInsideAnIgnoredToken_IsAParameterOfTheSql() =>
    Blocks("-- name: Q\n-- generator: token-ignore=f\nSELECT {{f:@b}}\n")
        .ShouldHaveSingleItem()
        .Parameters.Select(static parameter => parameter.Name)
        .ShouldBe(["b"]);
```

`SqlDiagnosticsTests`: whatever it pins for each `SqlParseErrorKind` gains `MarkerNotAllowedHere` with `SQLSRC116`.

`GeneratedSourceTests`, one generator test that a default changes no generated code:

```csharp
[Fact]
public void Run_TokenWithADefault_GeneratesTheMethodItGeneratesWithout()
{
    const string Source = """
        using SqlSource;

        namespace App;

        [SqlSourceGenerate(SqlLocation = SqlLocation.Direct)]
        public partial class Sample;
        """;

    var plain = GeneratorHarness.Run(Source, new SqlFile("/app/Repo/Q.sql", "SELECT * FROM {{table}} {{filter}};\n"));
    var withDefaults = GeneratorHarness.Run(
        Source,
        new SqlFile("/app/Repo/Q.sql", "-- token: {{filter:WHERE x = 1}}\nSELECT * FROM {{table:users}} {{filter}};\n")
    );

    withDefaults.Diagnostics.ShouldBeEmpty();
    withDefaults.GeneratedCodeWarnings.ShouldBeEmpty();
    withDefaults.Sources["App.Sample.g.cs"].ShouldBe(plain.Sources["App.Sample.g.cs"]);
}
```

- [ ] **Step 4: Add the marker word, its helpers and the new error kind**

`SqlMarkerKind`: a member `Token`, documented "`-- token:` gives a token of its query a default."  `SqlMarkerReader.Keywords` gains `("token:", SqlMarkerKind.Token)`, and the class gains

```csharp
    /// <summary>The word of a marker, without its colon: <c>token</c>.</summary>
    public static string WordOf(SqlMarkerKind kind)
    {
        foreach (var (keyword, candidate) in Keywords)
        {
            if (candidate == kind)
            {
                return keyword.Substring(0, keyword.Length - 1);
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// The marker as a message names it: its word, a colon, a space and its value, or the word and the colon for a
    /// marker without a value.
    /// </summary>
    public static string Describe(string text, SqlMarker marker)
    {
        var word = WordOf(marker.Kind) + ":";
        return marker.ValueSpan.IsEmpty
            ? word
            : word + " " + text.Substring(marker.ValueSpan.Start, marker.ValueSpan.Length);
    }
```

`SqlDialectMarker.Describe` becomes `=> SqlMarkerReader.Describe(text, marker);` and its `Word` constant goes.

`SqlParseErrorKind`, at the end:

```csharp
    /// <summary>
    /// A marker stands where it is not allowed: one for a query in the preamble, or one for the file inside a query.
    /// Arguments: the marker's word, and where it is allowed.
    /// </summary>
    MarkerNotAllowedHere,
```

Update the comment on `MarkerAtEndOfBlock` to "A marker has no SQL after it in its block.  No argument.", and the type's summary to "one argument, two, or none".

`SqlDiagnostics`: reword `MarkerAtEndOfBlock`'s message to `"A marker comes before the SQL it describes, and no SQL follows this one in its query"`, and add after `MisplacedDialect`, in `All` and in `ForParseError`:

```csharp
    public static readonly DiagnosticDescriptor MarkerNotAllowedHere = new(
        id: "SQLSRC116",
        title: "Marker is not allowed here",
        messageFormat: "The '-- {0}:' marker is allowed only {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc116",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );
```

`AnalyzerReleases.Unshipped.md`: `SQLSRC116 | SqlSource | Error | Marker is not allowed here`.

- [ ] **Step 5: Add the token models**

`src/SqlSource/Parsing/SqlToken.cs`:

```csharp
namespace SqlSource.Parsing;

/// <summary>
/// One token of a query, with the default its query gives it.
/// </summary>
/// <param name="Name">The token's name.</param>
/// <param name="Default">
/// The sample that stands in the token's place when the query is described: inline or from a <c>-- token:</c> marker.
/// Empty for a default that is given and holds nothing, null for a token without one.  It reaches no generated code.
/// </param>
internal sealed record SqlToken(string Name, string? Default);
```

`src/SqlSource/Parsing/SqlTokenDefault.cs`:

```csharp
namespace SqlSource.Parsing;

/// <summary>
/// A default that a <c>-- token:</c> marker gives.
/// </summary>
/// <param name="Name">The token's name.</param>
/// <param name="Text">The default, trimmed, as the marker writes it.</param>
/// <param name="Offset">Where <paramref name="Text" /> starts in the file.</param>
/// <param name="Marker">The marker.</param>
internal readonly record struct SqlTokenDefault(string Name, string Text, int Offset, SqlMarker Marker);
```

`src/SqlSource/Parsing/SqlTokenList.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace SqlSource.Parsing;

/// <summary>
/// Builds the tokens of one query: each name once, in order of first appearance, with the default its query gives.
/// </summary>
internal static class SqlTokenList
{
    /// <summary>
    /// Two defaults for one token that differ are a conflict, reported at the one that comes second in the file.
    /// A marker for a token the query does not hold is not an error.
    /// </summary>
    public static EquatableArray<SqlToken> Create(
        string text,
        SqlBlockText sql,
        EquatableArray<SqlTokenOccurrence> occurrences,
        IReadOnlyList<SqlTokenDefault> markerDefaults,
        List<SqlParseError> errors
    )
    {
        if (occurrences.Count == 0)
        {
            return EquatableArray<SqlToken>.Empty;
        }

        var names = new List<string>();

        // For each name, the first occurrence that has a default, or null.
        var inline = new List<SqlTokenOccurrence?>();
        foreach (var occurrence in occurrences)
        {
            var index = names.IndexOf(occurrence.Name);
            if (index < 0)
            {
                index = names.Count;
                names.Add(occurrence.Name);
                inline.Add(null);
            }

            if (occurrence.Default is null)
            {
                continue;
            }

            if (inline[index] is not { } first)
            {
                inline[index] = occurrence;
            }
            else if (!string.Equals(first.Default, occurrence.Default, StringComparison.Ordinal))
            {
                errors.Add(Conflict(sql, occurrence));
            }
        }

        var tokens = ImmutableArray.CreateBuilder<SqlToken>(names.Count);
        for (var index = 0; index < names.Count; index++)
        {
            tokens.Add(new SqlToken(names[index], inline[index]?.Default));
        }

        foreach (var marker in markerDefaults)
        {
            var index = names.IndexOf(marker.Name);
            if (index < 0)
            {
                continue;
            }

            if (inline[index] is not { } written)
            {
                tokens[index] = new SqlToken(marker.Name, marker.Text);
            }
            else if (!string.Equals(written.Default, marker.Text, StringComparison.Ordinal))
            {
                errors.Add(
                    marker.Marker.Span.Start > sql.ToSourceSpan(written.Span).Start
                        ? SqlParseError.Create(
                            SqlParseErrorKind.ConflictingSettings,
                            marker.Marker.ValueSpan,
                            SqlMarkerReader.Describe(text, marker.Marker)
                        )
                        : Conflict(sql, written)
                );
            }
        }

        return new EquatableArray<SqlToken>(tokens.MoveToImmutable());
    }

    private static SqlParseError Conflict(SqlBlockText sql, SqlTokenOccurrence occurrence) =>
        SqlParseError.Create(
            SqlParseErrorKind.ConflictingSettings,
            sql.ToSourceSpan(occurrence.Span),
            sql.Text.Substring(occurrence.Span.Start, occurrence.Span.Length)
        );
}
```

`SqlBlock` and `SqlQuery` gain `EquatableArray<SqlToken> Tokens` after `Segments`, documented "The query's tokens, each once, in order of first appearance, with its default.  Empty when the SQL has none."  `SqlFileReader` copies it.  Fix the hand-built models in the tests as in Task 1, with `EquatableArray<SqlToken>.Empty`.

- [ ] **Step 6: Add `SqlMarkerScope`**

`src/SqlSource/Parsing/SqlMarkerScope.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace SqlSource.Parsing;

/// <summary>
/// What the markers of one scope give: a file's preamble, or one query.  <see cref="SqlFileParser" /> hands it each
/// marker but <c>-- name:</c>, <c>-- summary:</c> and <c>-- dialect:</c>, which it reads itself.
/// </summary>
/// <remarks>
/// This is the one place that knows which marker is allowed where, and what a valid value of each is.  A marker that
/// is not allowed, not valid or in conflict with an earlier one of the scope is reported and not applied.
/// </remarks>
internal sealed class SqlMarkerScope(string text, SqlDialectRules rules, List<SqlParseError> errors)
{
    private const string InsideAQuery = "inside a query";

    private const string BeforeTheFirstQuery = "before the file's first query";

    private List<SqlTokenDefault>? _tokenDefaults;

    /// <summary>The scope's generator parameters.</summary>
    public SqlGeneratorParameterScope Generator { get; } = new();

    /// <summary>The defaults the scope's <c>-- token:</c> markers give, in marker order, each name once.</summary>
    public IReadOnlyList<SqlTokenDefault> TokenDefaults => _tokenDefaults ?? (IReadOnlyList<SqlTokenDefault>)[];

    /// <summary>
    /// Reads one marker.  <paramref name="inQuery" /> and <paramref name="inPreamble" /> say what the scope is; both
    /// are true for a file with no <c>-- name:</c> marker, which is one query and its own preamble.
    /// </summary>
    public void Read(SqlMarker marker, bool inQuery, bool inPreamble)
    {
        if (!((inQuery && IsAllowedInQuery(marker.Kind)) || (inPreamble && IsAllowedInPreamble(marker.Kind))))
        {
            errors.Add(
                SqlParseError.Create(
                    SqlParseErrorKind.MarkerNotAllowedHere,
                    marker.Span,
                    SqlMarkerReader.WordOf(marker.Kind),
                    IsAllowedInQuery(marker.Kind) ? InsideAQuery : BeforeTheFirstQuery
                )
            );
            return;
        }

        switch (marker.Kind)
        {
            case SqlMarkerKind.GeneratorParameters:
                Generator.Read(text, marker, errors);
                break;
            case SqlMarkerKind.Token:
                ReadToken(marker);
                break;
        }
    }

    private static bool IsAllowedInQuery(SqlMarkerKind kind) =>
        kind is SqlMarkerKind.GeneratorParameters or SqlMarkerKind.Token;

    private static bool IsAllowedInPreamble(SqlMarkerKind kind) => kind is SqlMarkerKind.GeneratorParameters;

    // The value is exactly one token with a default, as the SQL would write it.  The default is lexed alone, as the
    // parameter list reads it later: a quote or a comment that does not close inside it would swallow what follows.
    private void ReadToken(SqlMarker marker)
    {
        var value = text.Substring(marker.ValueSpan.Start, marker.ValueSpan.Length);
        if (
            !TokenScanner.TryReadToken(value, 0, out var name, out var defaultSpan, out var end)
            || end != value.Length
            || defaultSpan is not { } place
        )
        {
            AddInvalid(marker);
            return;
        }

        if (SqlIdentifier.IsReservedKeyword(name))
        {
            errors.Add(SqlParseError.Create(SqlParseErrorKind.ReservedTokenName, marker.ValueSpan, name));
            return;
        }

        var defaultText = value.Substring(place.Start, place.Length);
        if (SqlLexer.Lex(defaultText, rules).Error is not null)
        {
            AddInvalid(marker);
            return;
        }

        _tokenDefaults ??= [];
        foreach (var existing in _tokenDefaults)
        {
            if (!string.Equals(existing.Name, name, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.Equals(existing.Text, defaultText, StringComparison.Ordinal))
            {
                AddConflict(marker);
            }

            return;
        }

        _tokenDefaults.Add(new SqlTokenDefault(name, defaultText, marker.ValueSpan.Start + place.Start, marker));
    }

    // At the value, or at the whole marker when it has none.
    private void AddInvalid(SqlMarker marker) => Add(SqlParseErrorKind.InvalidMarkerValue, marker);

    private void AddConflict(SqlMarker marker) => Add(SqlParseErrorKind.ConflictingSettings, marker);

    private void Add(SqlParseErrorKind kind, SqlMarker marker) =>
        errors.Add(
            SqlParseError.Create(
                kind,
                marker.ValueSpan.IsEmpty ? marker.Span : marker.ValueSpan,
                SqlMarkerReader.Describe(text, marker)
            )
        );
}
```

- [ ] **Step 7: Read the markers through the scope in `SqlFileParser`**

`Parse` passes the rules the lexer ended with: `new Parser(text, fileName, lexed.Lexemes, headerEnd, lexer.Rules).Run()`, and `Parser` gains the constructor parameter `SqlDialectRules rules`.

`ReadUnnamedFile` calls `ReadBlock(name, FileStart, null, 0, lexemes.Count)`.

`ReadPreamble` returns a `SqlMarkerScope`:

```csharp
        private SqlMarkerScope ReadPreamble(int end)
        {
            var scope = new SqlMarkerScope(text, rules, _errors);
            var sqlReported = false;
            for (var index = 0; index < end; index++)
            {
                var lexeme = lexemes[index];
                if (SqlMarkerReader.Read(text, lexeme) is { } marker)
                {
                    if (marker.Kind == SqlMarkerKind.Dialect)
                    {
                        ReadDialect(marker);
                    }
                    else if (marker.Kind == SqlMarkerKind.Summary)
                    {
                        AddError(SqlParseErrorKind.SummaryBeforeFirstName, marker.Span);
                    }
                    else
                    {
                        scope.Read(marker, inQuery: false, inPreamble: true);
                    }
                }
                else if (!sqlReported && lexeme.GetContentSpan(text) is { } content)
                {
                    AddError(SqlParseErrorKind.SqlBeforeFirstName, content);
                    sqlReported = true;
                }
            }

            return scope;
        }
```

`ReadBlock` becomes:

```csharp
        // preamble is null for a file with no name marker: the file is one query and its own preamble.
        private void ReadBlock(string name, TextSpan nameSpan, SqlMarkerScope? preamble, int start, int end)
        {
            var scope = new SqlMarkerScope(text, rules, _errors);
            var summary = new List<string>();
            var lastContent = FindLastContent(start, end);
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
                    scope.Read(marker, inQuery: true, inPreamble: preamble is null);
                }
            }

            if (lastContent < 0)
            {
                AddError(SqlParseErrorKind.EmptyBlock, nameSpan);
                return;
            }

            var inherited = preamble?.Generator;
            var keepComments = (inherited?.KeepComments ?? false) || scope.Generator.KeepComments;
            var sql = SqlTextBuilder.Build(text, lexemes, start, end, keepComments);
            HashSet<string> ignoredTokens = [.. scope.Generator.IgnoredTokens];
            if (inherited is not null)
            {
                ignoredTokens.UnionWith(inherited.IgnoredTokens);
            }

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
                    keepComments,
                    scope.Generator.TokenValidation ?? inherited?.TokenValidation,
                    scanned.Segments,
                    SqlTokenList.Create(text, sql, scanned.Occurrences, scope.TokenDefaults, _errors),
                    SqlParameterList.Create(sql, scanned.Occurrences)
                )
            );
        }
```

`SqlParameterList.Create` takes the occurrences and leaves out a parameter inside a token:

```csharp
    /// <summary>
    /// A parameter inside a token is part of a sample, not of the SQL, and is left out.
    /// </summary>
    public static EquatableArray<SqlQueryParameter> Create(
        SqlBlockText sql,
        EquatableArray<SqlTokenOccurrence> occurrences
    )
    {
        if (sql.Parameters.Length == 0)
        {
            return EquatableArray<SqlQueryParameter>.Empty;
        }

        var parameters = ImmutableArray.CreateBuilder<SqlQueryParameter>();
        var token = 0;
        foreach (var span in sql.Parameters)
        {
            // Both lists are in the order of the SQL.
            while (token < occurrences.Count && occurrences[token].Span.End <= span.Start)
            {
                token++;
            }

            if (token < occurrences.Count && occurrences[token].Span.Start <= span.Start)
            {
                continue;
            }

            if (IndexOf(parameters, sql.Text, span) < 0)
            {
                parameters.Add(new SqlQueryParameter(NameOf(sql.Text, span), null, null, false));
            }
        }

        return parameters.Count == 0
            ? EquatableArray<SqlQueryParameter>.Empty
            : new EquatableArray<SqlQueryParameter>(parameters.ToImmutable());
    }
```

- [ ] **Step 8: Run everything**

Run: `dotnet test --solution SqlSource.slnx`
Expected: PASS, the allocation budget included.

- [ ] **Step 9: Update the documents**

`README.md`:

- Under What reaches the generated SQL, the first bullet becomes "Every marker line is removed: `-- name:`, `-- summary:`, `-- generator:`, `-- dialect:` and `-- token:`."
- Under Tokens, replace the bullet "Braces around anything that is not a name ..." with:

```markdown
- Braces around anything that is not a name, or a name and a default, such as `{{table-name}}`, `{{1st}}` or `{{order by}}`, are not a token.  The text stays in the SQL as written, and nothing is reported.
```

- After the bullet list of Tokens, before "**Tokens are for trusted text only.**", add:

````markdown
### Defaults

A token can carry a default: `{{name:default}}`.  The default is everything after the first `:` up to the closing `}}`, without the white space around it, so `{{cast:x::int}}` is the token `cast` with the default `x::int`.  It may be empty, `{{extraWhere:}}`, and may run over several lines.

```sql
-- name: ListUsers
-- token: {{filter:AND deleted_at IS NULL}}
SELECT id, name FROM {{table:users}}
WHERE 1 = 1 {{filter}}
ORDER BY {{orderBy:name}};
```

A default is a sample of what the caller will pass.  It changes nothing that is generated today: the method still takes each token as a `string`, and nothing uses the default at run time.  It is there for the tool that will describe a query to its database, which needs SQL it can send.

- A `-- token:` marker inside a query gives the default for a token that the query writes several times, or whose sample is long.  Its value is exactly what the SQL would hold: one token with its default.
- Two defaults for one token of a query must be the same.  Two that differ are the error [SQLSRC112](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc112).
- `{{a:b}}` in SQL that does not mean a token, the text of a template for example, is kept as written by a `token-ignore=a` generator parameter.
````

`docs/diagnostics.md`:

- Index row and a section after `SQLSRC115`:

````markdown
## SQLSRC116

**Marker is not allowed here**

A marker stands in the wrong part of its file.  Some markers describe one query and go inside it, after its `-- name:` line; some describe the whole file and go before the first `-- name:` line.  The message says which this one is.

```sql
-- token: {{filter:AND deleted_at IS NULL}}

-- name: ListUsers
SELECT id FROM users WHERE 1 = 1 {{filter}};
```

Move the marker to where the message says.  A file with no `-- name:` line is one query, and takes every marker.
````

- `SQLSRC108`: the section's first sentence names markers in general, not three of them: "A marker comes before the SQL it describes.  This one stands after the last SQL of its query, where a reader would take it to belong to the next one."  Keep the example.
- `SQLSRC111`: add a paragraph "A `-- token:` marker holds exactly one token with a default, `{{name:default}}`, and nothing else; and a quote or a block comment inside the default must close there."
- `SQLSRC112`: add "Two defaults for one token of a query that differ: two `{{name:default}}` in its SQL, two `-- token:` markers, or one of each."
- `SQLSRC114`: add "The same holds for the name in a `-- token:` marker."

`src/SqlSource/AGENTS.md`, under `Parsing/`: "**`SqlMarkerScope` is the one place that knows a marker's scope and value.**  `SqlFileParser` reads `-- name:`, `-- summary:` and `-- dialect:` itself and hands every other marker to the scope of the preamble or of the query.  A new marker is a `SqlMarkerKind`, a keyword in `SqlMarkerReader`, a row in the scope's two `IsAllowedIn` lists and a `case` in `SqlMarkerScope.Read`."

- [ ] **Step 10: Commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add -A && git commit -m "Read a token's default, inline and from a token marker

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: `-- token-ignore:` becomes a marker

**Files:**
- Modify: `src/SqlSource/Parsing/SqlMarkerKind.cs`, `SqlMarkerReader.cs`, `SqlMarkerScope.cs`, `SqlGeneratorParameterScope.cs`, `SqlFileParser.cs`
- Modify: `README.md`, `docs/diagnostics.md`
- Test: `tests/SqlSource.Tests/Parsing/SqlGeneratorParameterScopeTests.cs`, `SqlFileParserTests.cs`, `SqlMarkerReaderTests.cs`, `SqlFileParserAllocationTests.cs`; `tests/SqlSource.Tests/Diagnostics/DiagnosticsDocumentTests.cs`; any test under `tests/` that `git grep -n 'token-ignore'` lists

**Interfaces:**
- Consumes: `SqlMarkerScope`, `SqlParseErrorKind.MarkerNotAllowedHere`.
- Produces: `SqlMarkerKind.TokenIgnore`; `SqlMarkerScope.IgnoredTokens` (`ISet<string>`, empty when the scope has none).  `SqlGeneratorParameterScope.IgnoredTokens` is gone.

- [ ] **Step 1: Write the failing tests**

`SqlFileParserTests`:

```csharp
[Fact]
public void Parse_TokenIgnoreMarkers_KeepEachNamedTokenAsText()
{
    const string Text =
        "-- name: Q\n-- token-ignore: a\n-- TOKEN-IGNORE: b\nSELECT '{{a}}', '{{b:x}}', {{c}}\n"
        + "-- name: R\nSELECT {{a}}\n";

    var blocks = Blocks(Text);

    Sql(blocks[0]).ShouldBe("SELECT '{{a}}', '{{b:x}}', {{c}}");
    Tokens(blocks[0]).ShouldBe(["c=<none>"]);
    // One query's marker does not reach the next.
    Tokens(blocks[1]).ShouldBe(["a=<none>"]);
}

[Theory]
[InlineData("a b")]
[InlineData("a-b")]
[InlineData("{{a}}")]
[InlineData("1a")]
public void Parse_TokenIgnoreMarkerThatIsNotOneName_IsInvalid(string value)
{
    var text = "-- name: Q\n-- token-ignore: " + value + "\nSELECT 1\n";

    Errors(text)
        .ShouldBe([
            SqlParseError.Create(SqlParseErrorKind.InvalidMarkerValue, SpanOf(text, value), "token-ignore: " + value),
        ]);
}

[Fact]
public void Parse_TokenIgnoreMarkerWithoutAName_IsInvalidAtTheMarker()
{
    const string Text = "-- name: Q\n-- token-ignore:\nSELECT 1\n";

    Errors(Text)
        .ShouldBe([
            SqlParseError.Create(SqlParseErrorKind.InvalidMarkerValue, SpanOf(Text, "-- token-ignore:"), "token-ignore:"),
        ]);
}

[Fact]
public void Parse_TokenIgnoreMarkerInThePreamble_IsNotAllowedThere()
{
    const string Text = "-- token-ignore: a\n-- name: Q\nSELECT {{a}}\n";

    Errors(Text)
        .ShouldBe([
            SqlParseError.Create(
                SqlParseErrorKind.MarkerNotAllowedHere,
                SpanOf(Text, "-- token-ignore: a"),
                "token-ignore",
                "inside a query"
            ),
        ]);
}

[Fact]
public void Parse_TokenIgnoreAsAGeneratorParameter_IsNotKnown()
{
    const string Text = "-- name: Q\n-- generator: token-ignore=a\nSELECT {{a}}\n";

    Errors(Text)
        .ShouldBe([
            SqlParseError.Create(
                SqlParseErrorKind.UnknownGeneratorParameter,
                SpanOf(Text, "token-ignore=a"),
                "token-ignore=a"
            ),
        ]);
}
```

Change `Parse_ParameterInsideAnIgnoredToken_IsAParameterOfTheSql` from Task 2 to use `-- token-ignore: f`.  In `SqlGeneratorParameterScopeTests`, delete the tests of `token-ignore=` and of `IgnoredTokens`, and remove `Token-Ignore=a` from the case-insensitivity test.  `SqlMarkerReaderTests`: `-- token-ignore: a` is kind `TokenIgnore`, and `-- token: {{a:b}}` is still kind `Token`.

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet build SqlSource.slnx`
Expected: FAIL, no `SqlMarkerKind.TokenIgnore`.

- [ ] **Step 3: Implement**

`SqlMarkerKind`: `TokenIgnore`, documented "`-- token-ignore:` names a token of its query that stays literal text."  `SqlMarkerReader.Keywords`: `("token-ignore:", SqlMarkerKind.TokenIgnore)`.

`SqlMarkerScope`:

```csharp
    private static readonly HashSet<string> NoNames = [];

    private HashSet<string>? _ignoredTokens;

    /// <summary>The names the scope's <c>-- token-ignore:</c> markers give.</summary>
    public ISet<string> IgnoredTokens => _ignoredTokens ?? NoNames;
```

`IsAllowedInQuery` gains `or SqlMarkerKind.TokenIgnore`, and `Read` gains

```csharp
            case SqlMarkerKind.TokenIgnore:
                ReadTokenIgnore(marker);
                break;
```

```csharp
    private void ReadTokenIgnore(SqlMarker marker)
    {
        var name = text.Substring(marker.ValueSpan.Start, marker.ValueSpan.Length);
        if (!SqlIdentifier.IsValid(name))
        {
            AddInvalid(marker);
            return;
        }

        _ignoredTokens ??= [];
        _ = _ignoredTokens.Add(name);
    }
```

`NoNames` is never written to: the only writer goes through `_ignoredTokens`.

`SqlGeneratorParameterScope`: remove `TokenIgnoreName`, `IgnoredTokens`, `ApplyTokenIgnore` and the branch of `Apply` that calls it, so that `token-ignore=a` reaches `UnknownGeneratorParameter`.

`SqlFileParser.ReadBlock`: the two lines that build `ignoredTokens` become `var scanned = TokenScanner.Scan(sql.Text, scope.IgnoredTokens);`.

- [ ] **Step 4: Move the allocation test's file to the new form**

In `SqlFileParserAllocationTests.CreateFile`, delete `.Append("-- generator: token-ignore=raw\n\n")` and append `"\n"` in its place, so that the preamble still ends with a blank line.  The comment above the method is unchanged.

- [ ] **Step 5: Update every other use**

Run `git grep -n 'token-ignore' -- . ':!docs/superpowers'`.  In tests, a file that used `-- generator: token-ignore=name` in a query uses `-- token-ignore: name`; one that used it in a preamble moves it into each query that needs it.  In `DiagnosticsDocumentTests.Document_UnknownGeneratorParameterSection_ListsEveryGeneratorParameter`, remove `"token-ignore=name"` from `parameters`.

- [ ] **Step 6: Run everything, and measure**

Run: `dotnet test --solution SqlSource.slnx`
Expected: PASS.  Measure the parse as in Task 1 Step 11 and note the number: the file is a line shorter, so the number is for a different text than Task 1's.

- [ ] **Step 7: Update the documents**

`README.md`:

- Generator parameters table: delete the `token-ignore=name` row.
- What reaches the generated SQL, first bullet: add `-- token-ignore:` to the list.
- Tokens: replace the bullet "Text that has the form of a token and is not meant as one ..." with

```markdown
- Text that has the form of a token and is not meant as one stays in the SQL when a `-- token-ignore: name` marker inside the query lists its name.  One name for each marker; a query can have several.
```

  and in the Defaults section added by Task 2, "`token-ignore=a` generator parameter" becomes "`-- token-ignore: a` marker".

`docs/diagnostics.md`: in `SQLSRC109`, remove `token-ignore=name` from the list of parameters and add "`token-ignore` was a generator parameter and is a marker now: `-- token-ignore: name`, inside the query."  In `SQLSRC111`, replace what it says of `token-ignore=` with "A `-- token-ignore:` marker holds one name."

- [ ] **Step 8: Commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add -A && git commit -m "Make token-ignore a marker of its query

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: The `-- param:` marker and the parameter list rule

**Files:**
- Modify: `src/SqlSource/Parsing/SqlMarkerKind.cs`, `SqlMarkerReader.cs`, `SqlMarkerScope.cs`, `SqlParameterList.cs`, `SqlParseErrorKind.cs`, `SqlFileParser.cs`
- Create: `src/SqlSource/Parsing/SqlParameterDeclaration.cs`
- Modify: `src/SqlSource/Diagnostics/SqlDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`
- Modify: `README.md`, `docs/diagnostics.md`
- Test: `tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs`, `SqlMarkerReaderTests.cs`; `tests/SqlSource.Tests/Diagnostics/SqlDiagnosticsTests.cs`

**Interfaces:**
- Consumes: `SqlLexer.IsParameterNameCharacter`, `SqlDialectRules.ParameterPrefix`, `SqlTokenDefault`, `SqlToken`, `SqlTokenOccurrence`.
- Produces:
  - `SqlMarkerKind.Param`; `SqlParameterDeclaration(string Name, string? Type, bool? Nullable, SqlMarker Marker)`, a `readonly record struct`; `SqlMarkerScope.Declarations` (`IReadOnlyList<SqlParameterDeclaration>`).
  - `SqlParameterList.Create(string text, SqlDialectRules rules, SqlBlockText sql, EquatableArray<SqlTokenOccurrence> occurrences, EquatableArray<SqlToken> tokens, IReadOnlyList<SqlTokenDefault> markerDefaults, IReadOnlyList<SqlParameterDeclaration> declarations, List<SqlParseError> errors)`.
  - `SqlParseErrorKind.MissingParameterType` (`SQLSRC117`) and `UndeclaredParameter` (`SQLSRC118`), each with one argument: the parameter with its prefix.

- [ ] **Step 1: Write the failing tests**

`SqlFileParserTests`:

```csharp
private static string[] Parameters(SqlBlock block) =>
    [
        .. block.Parameters.Select(static parameter =>
            parameter.Name
            + ":"
            + (parameter.Type ?? "<none>")
            + ":"
            + (parameter.Nullable is { } nullable ? (nullable ? "null" : "not null") : "<unsaid>")
            + (parameter.IsDeclared ? ":declared" : string.Empty)
        ),
    ];

[Theory]
[InlineData("@a", "a:<none>:<unsaid>:declared")]
[InlineData("@a int", "a:int:<unsaid>:declared")]
[InlineData("@a null", "a:<none>:null:declared")]
[InlineData("@a not null", "a:<none>:not null:declared")]
[InlineData("@a timestamptz null", "a:timestamptz:null:declared")]
[InlineData("@a decimal(18, 2) not null", "a:decimal(18, 2):not null:declared")]
[InlineData("@a double precision", "a:double precision:<unsaid>:declared")]
// As people type it.
[InlineData("@a\tint\tNULL", "a:int:null:declared")]
[InlineData("@a int NOT  NULL", "a:int:not null:declared")]
[InlineData("@A int", "a:int:<unsaid>:declared")]
// A type that only ends in the letters of the word.
[InlineData("@a mynull", "a:mynull:<unsaid>:declared")]
[InlineData("@a knot null", "a:knot:null:declared")]
public void Parse_ParamMarker_GivesItsParameterATypeAndNullability(string value, string expected) =>
    Parameters(Blocks("-- name: Q\n-- param: " + value + "\nSELECT @a\n").ShouldHaveSingleItem()).ShouldBe([expected]);

[Fact]
public void Parse_ParamMarkerWithWindowsLineEndings_IsRead() =>
    Parameters(Blocks("-- name: Q\r\n-- param: @a int null  \r\nSELECT @a\r\n").ShouldHaveSingleItem())
        .ShouldBe(["a:int:null:declared"]);

[Fact]
public void Parse_ParameterList_IsTheSqlsParametersThenTheDeclaredOnesInMarkerOrder()
{
    const string Text =
        "-- name: Q\n-- param: @z int\n-- param: @b text null\n-- param: @y int\nSELECT @a, @b {{f}}\n";

    Parameters(Blocks(Text).ShouldHaveSingleItem())
        .ShouldBe(["a:<none>:<unsaid>", "b:text:null:declared", "z:int:<unsaid>:declared", "y:int:<unsaid>:declared"]);
}

[Fact]
public void Parse_TheSameDeclarationTwice_IsNotAConflict() =>
    Parameters(Blocks("-- name: Q\n-- param: @a int null\n-- param: @A int null\nSELECT @a\n").ShouldHaveSingleItem())
        .ShouldBe(["a:int:null:declared"]);

[Theory]
[InlineData("@a text")]
[InlineData("@a int null")]
[InlineData("@a INT")]
[InlineData("@a")]
public void Parse_TwoDeclarationsOfOneParameterThatDiffer_Conflict(string second)
{
    var text = "-- name: Q\n-- param: @a int\n-- param: " + second + "\nSELECT @a\n";

    Errors(text)
        .ShouldBe([
            SqlParseError.Create(
                SqlParseErrorKind.ConflictingSettings,
                new TextSpan(text.IndexOf("-- param: " + second + "\n", StringComparison.Ordinal) + 10, second.Length),
                "param: " + second
            ),
        ]);
}

[Theory]
[InlineData("a int")]
[InlineData("@")]
[InlineData("@ a")]
[InlineData("@a-b int")]
[InlineData("@a,@b")]
[InlineData(":a int")]
public void Parse_ParamMarkerWithoutAPrefixedNameOnItsOwn_IsInvalid(string value)
{
    var text = "-- name: Q\n-- param: " + value + "\nSELECT 1\n";

    Errors(text)
        .ShouldBe([SqlParseError.Create(SqlParseErrorKind.InvalidMarkerValue, SpanOf(text, value), "param: " + value)]);
}

[Fact]
public void Parse_ParamMarkerInThePreamble_IsNotAllowedThere()
{
    const string Text = "-- param: @a int\n-- name: Q\nSELECT @a\n";

    Errors(Text)
        .ShouldBe([
            SqlParseError.Create(
                SqlParseErrorKind.MarkerNotAllowedHere,
                SpanOf(Text, "-- param: @a int"),
                "param",
                "inside a query"
            ),
        ]);
}

[Theory]
[InlineData("@page")]
[InlineData("@page null")]
[InlineData("@page not null")]
public void Parse_DeclaredOnlyParameterWithoutAType_IsAnError(string value)
{
    var text = "-- name: Q\n-- param: " + value + "\nSELECT 1 {{tail:LIMIT @page}}\n";

    Errors(text)
        .ShouldBe([SqlParseError.Create(SqlParseErrorKind.MissingParameterType, SpanOf(text, value), "@page")]);
}

[Fact]
public void Parse_ParameterOnlyInAnInlineDefault_MustBeDeclared()
{
    const string Text = "-- name: Q\nSELECT @a {{f:AND x = @b AND y = @a}}\n";

    Errors(Text).ShouldBe([SqlParseError.Create(SqlParseErrorKind.UndeclaredParameter, SpanOf(Text, "@b"), "@b")]);
}

[Fact]
public void Parse_ParameterOnlyInAMarkersDefault_MustBeDeclared()
{
    const string Text = "-- name: Q\n-- token: {{f:AND x = @b AND y = '@c'}}\nSELECT 1 {{f}}\n";

    Errors(Text).ShouldBe([SqlParseError.Create(SqlParseErrorKind.UndeclaredParameter, SpanOf(Text, "@b"), "@b")]);
}

[Fact]
public void Parse_ParameterInADefaultThatIsDeclaredWithAType_IsADeclaredOnlyParameter() =>
    Parameters(
            Blocks("-- name: Q\n-- param: @b int\n-- token: {{g:OFFSET @b}}\nSELECT @a {{f:LIMIT @b}} {{g}}\n")
                .ShouldHaveSingleItem()
        )
        .ShouldBe(["a:<none>:<unsaid>", "b:int:<unsaid>:declared"]);

// The query does not hold the token, so its marker's default is not the query's.
[Fact]
public void Parse_ParameterInTheDefaultOfATokenTheQueryLacks_IsNotChecked() =>
    Blocks("-- name: Q\n-- token: {{other:@x}}\nSELECT 1\n").ShouldHaveSingleItem().Parameters.ShouldBeEmpty();

// A fragment passed at run time may use it.
[Fact]
public void Parse_DeclaredParameterThatNothingHolds_IsKept() =>
    Parameters(Blocks("-- name: Q\n-- param: @later int\nSELECT 1 {{f}}\n").ShouldHaveSingleItem())
        .ShouldBe(["later:int:<unsaid>:declared"]);
```

`SqlMarkerReaderTests`: `-- param: @a int` is kind `Param`; `-- params: @a` is not a marker.  `SqlDiagnosticsTests`: the two new kinds and ids.

Task 2's `Parse_ParameterInsideAToken_IsNotAParameterOfTheSql` holds a parameter that only a default has, which this task makes an error.  Give its query `-- param: @b int` and expect `["a", "c", "b"]`.

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet build SqlSource.slnx`
Expected: FAIL, no `SqlMarkerKind.Param`.

- [ ] **Step 3: Add the kinds and the descriptors**

`SqlMarkerKind.Param`, documented "`-- param:` declares a parameter of its query: its type, whether it is nullable, or both."  Keyword `("param:", SqlMarkerKind.Param)`.

`SqlParseErrorKind`, at the end:

```csharp
    /// <summary>
    /// A <c>-- param:</c> marker gives no type for a parameter that the query's SQL does not hold.  Argument: the
    /// parameter, with its prefix.
    /// </summary>
    MissingParameterType,

    /// <summary>
    /// A parameter stands only in the default of a token.  Argument: the parameter, with its prefix.
    /// </summary>
    UndeclaredParameter,
```

`SqlDiagnostics`, after `MarkerNotAllowedHere`, in `All` and in `ForParseError`:

```csharp
    public static readonly DiagnosticDescriptor MissingParameterType = new(
        id: "SQLSRC117",
        title: "Parameter has no type",
        messageFormat: "'{0}' is not in the SQL of its query, so its '-- param:' marker must give its type",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc117",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor UndeclaredParameter = new(
        id: "SQLSRC118",
        title: "Parameter is not declared",
        messageFormat: "'{0}' appears only in the default of a token.  Declare it with a '-- param:' marker that "
            + "gives its type.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc118",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );
```

`AnalyzerReleases.Unshipped.md`: the two rows, `Parameter has no type` and `Parameter is not declared`.

- [ ] **Step 4: Read the marker**

`src/SqlSource/Parsing/SqlParameterDeclaration.cs`:

```csharp
namespace SqlSource.Parsing;

/// <summary>
/// What one <c>-- param:</c> marker declares.
/// </summary>
/// <param name="Name">The parameter's name as the marker writes it, without the prefix.</param>
/// <param name="Type">The database type, as written, or null when the marker gives none.</param>
/// <param name="Nullable">True for <c>null</c>, false for <c>not null</c>, null when the marker says neither.</param>
/// <param name="Marker">The marker.</param>
internal readonly record struct SqlParameterDeclaration(string Name, string? Type, bool? Nullable, SqlMarker Marker);
```

`SqlMarkerScope`: `IsAllowedInQuery` gains `or SqlMarkerKind.Param`; `Read` gains `case SqlMarkerKind.Param: ReadParam(marker); break;`; and

```csharp
    private List<SqlParameterDeclaration>? _declarations;

    /// <summary>What the scope's <c>-- param:</c> markers declare, in marker order, each parameter once.</summary>
    public IReadOnlyList<SqlParameterDeclaration> Declarations =>
        _declarations ?? (IReadOnlyList<SqlParameterDeclaration>)[];

    // "@name [type] [null | not null]".  The type is what stands between the name and those words, as written.
    private void ReadParam(SqlMarker marker)
    {
        var start = marker.ValueSpan.Start;
        var end = marker.ValueSpan.End;
        var nameEnd = start + 1;
        while (nameEnd < end && SqlLexer.IsParameterNameCharacter(text[nameEnd]))
        {
            nameEnd++;
        }

        if (
            start == end
            || text[start] != rules.ParameterPrefix
            || nameEnd == start + 1
            || (nameEnd < end && !char.IsWhiteSpace(text[nameEnd]))
        )
        {
            AddInvalid(marker);
            return;
        }

        var name = text.Substring(start + 1, nameEnd - start - 1);
        var rest = text.AsSpan(nameEnd, end - nameEnd).Trim();
        bool? nullable = null;
        if (TryTakeLastWord(ref rest, "null"))
        {
            nullable = !TryTakeLastWord(ref rest, "not");
        }

        var declaration = new SqlParameterDeclaration(name, rest.IsEmpty ? null : rest.ToString(), nullable, marker);
        _declarations ??= [];
        foreach (var existing in _declarations)
        {
            if (!string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (
                !string.Equals(existing.Type, declaration.Type, StringComparison.Ordinal)
                || existing.Nullable != nullable
            )
            {
                AddConflict(marker);
            }

            return;
        }

        _declarations.Add(declaration);
    }

    // Takes word off the end of rest when it stands there as a word: alone, or after white space.
    private static bool TryTakeLastWord(ref ReadOnlySpan<char> rest, string word)
    {
        if (!rest.EndsWith(word.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var before = rest.Length - word.Length;
        if (before > 0 && !char.IsWhiteSpace(rest[before - 1]))
        {
            return false;
        }

        rest = rest.Slice(0, before).TrimEnd();
        return true;
    }
```

- [ ] **Step 5: Apply the parameter list rule**

Replace `SqlParameterList` with:

```csharp
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// Builds the parameter list of one query.
/// </summary>
/// <remarks>
/// The list is the parameters of the query's static SQL in order of first appearance, then the parameters that a
/// <c>-- param:</c> marker declares and the static SQL does not hold, in marker order.  Static means outside every
/// token: a parameter inside <c>{{name:default}}</c> is part of a sample.  Names compare ignoring case.
/// </remarks>
internal static class SqlParameterList
{
    /// <summary>
    /// Reports a declared parameter that only a token can bring and that has no type, and a parameter that stands
    /// in the default of one of the query's tokens and is neither in the static SQL nor declared.
    /// </summary>
    public static EquatableArray<SqlQueryParameter> Create(
        string text,
        SqlDialectRules rules,
        SqlBlockText sql,
        EquatableArray<SqlTokenOccurrence> occurrences,
        EquatableArray<SqlToken> tokens,
        IReadOnlyList<SqlTokenDefault> markerDefaults,
        IReadOnlyList<SqlParameterDeclaration> declarations,
        List<SqlParseError> errors
    )
    {
        if (sql.Parameters.Length == 0 && declarations.Count == 0 && markerDefaults.Count == 0)
        {
            return EquatableArray<SqlQueryParameter>.Empty;
        }

        var parameters = ImmutableArray.CreateBuilder<SqlQueryParameter>();

        // The parameters of the defaults, each with its place in the file.
        List<(string Name, TextSpan Place)>? inDefaults = null;
        var token = 0;
        foreach (var span in sql.Parameters)
        {
            // Both lists are in the order of the SQL.
            while (token < occurrences.Count && occurrences[token].Span.End <= span.Start)
            {
                token++;
            }

            var name = sql.Text.Substring(span.Start + 1, span.Length - 1);
            if (token < occurrences.Count && occurrences[token].Span.Start <= span.Start)
            {
                (inDefaults ??= []).Add((name, sql.ToSourceSpan(span)));
            }
            else if (IndexOf(parameters, name) < 0)
            {
                parameters.Add(new SqlQueryParameter(name, null, null, false));
            }
        }

        foreach (var marker in markerDefaults)
        {
            if (!Holds(tokens, marker.Name))
            {
                continue;
            }

            foreach (var lexeme in SqlLexer.Lex(marker.Text, rules).Lexemes)
            {
                if (lexeme.Kind == SqlLexemeKind.Parameter)
                {
                    (inDefaults ??= []).Add(
                        (
                            marker.Text.Substring(lexeme.Span.Start + 1, lexeme.Span.Length - 1),
                            new TextSpan(marker.Offset + lexeme.Span.Start, lexeme.Span.Length)
                        )
                    );
                }
            }
        }

        foreach (var declaration in declarations)
        {
            var index = IndexOf(parameters, declaration.Name);
            if (index >= 0)
            {
                parameters[index] = parameters[index] with
                {
                    Type = declaration.Type,
                    Nullable = declaration.Nullable,
                    IsDeclared = true,
                };
                continue;
            }

            if (declaration.Type is null)
            {
                errors.Add(
                    SqlParseError.Create(
                        SqlParseErrorKind.MissingParameterType,
                        declaration.Marker.ValueSpan,
                        rules.ParameterPrefix + declaration.Name
                    )
                );
            }

            parameters.Add(new SqlQueryParameter(declaration.Name, declaration.Type, declaration.Nullable, true));
        }

        if (inDefaults is not null)
        {
            foreach (var (name, place) in inDefaults)
            {
                if (IndexOf(parameters, name) < 0)
                {
                    errors.Add(
                        SqlParseError.Create(
                            SqlParseErrorKind.UndeclaredParameter,
                            place,
                            rules.ParameterPrefix + name
                        )
                    );
                }
            }
        }

        return parameters.Count == 0
            ? EquatableArray<SqlQueryParameter>.Empty
            : new EquatableArray<SqlQueryParameter>(parameters.ToImmutable());
    }

    private static bool Holds(EquatableArray<SqlToken> tokens, string name)
    {
        foreach (var token in tokens)
        {
            if (string.Equals(token.Name, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static int IndexOf(ImmutableArray<SqlQueryParameter>.Builder parameters, string name)
    {
        for (var index = 0; index < parameters.Count; index++)
        {
            if (string.Equals(parameters[index].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }
}
```

The name of a parameter is now a string before it is known to be new.  That is one short string for each occurrence of a parameter; if Step 6's measurement shows it, compare spans first, as Task 1's `IndexOf` did, and create the string only for a new name.

`SqlFileParser.ReadBlock`: build the tokens into a local first, then

```csharp
            var tokens = SqlTokenList.Create(text, sql, scanned.Occurrences, scope.TokenDefaults, _errors);
            var parameters = SqlParameterList.Create(
                text,
                rules,
                sql,
                scanned.Occurrences,
                tokens,
                scope.TokenDefaults,
                scope.Declarations,
                _errors
            );
```

and pass `tokens` and `parameters` to `new SqlBlock(...)`.

- [ ] **Step 6: Run everything, and measure**

Run: `dotnet test --solution SqlSource.slnx`
Expected: PASS.  Measure the parse as in Task 1 Step 11.  If it rose against Task 3's number, apply the span comparison described in Step 5 and measure again.

- [ ] **Step 7: Update the documents**

`README.md`: add `-- param:` to the first bullet of What reaches the generated SQL, and a section before `## Tokens`:

````markdown
## Parameters

SqlSource finds the parameters of a query: `@` and a name of letters, digits and `_`, outside strings, quoted identifiers, comments and hints.  Names are compared ignoring case.  `@>`, `<@`, `@@`, `@?` and `@@ROWCOUNT` are not parameters, and neither is the `@` inside a word such as `user@host`.

Nothing is generated from a query's parameters yet, and a parameter stays in the SQL exactly as written.  The list is what a later release types.

A `-- param:` marker inside a query declares one parameter:

```sql
-- name: FindUsers
-- param: @since timestamptz not null
-- param: @name text null
-- param: @page int
SELECT id, name FROM users
WHERE created_at >= @since AND (@name IS NULL OR name = @name) {{paging:LIMIT 20 OFFSET @page}};
```

- The form is `@name`, then a type, then `null` or `not null`; the type and the last part are each optional.  The type is in the database's own words and is not checked here: `decimal(18,2)`, `double precision`.
- `not null` says what saying nothing says.  It is accepted because it is what a column definition says.
- A parameter that the query's SQL does not hold, because it comes only with a token, must be declared with its type: without one it is the error [SQLSRC117](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc117).  A parameter that appears only in a token's default and is not declared is [SQLSRC118](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc118).
- A T-SQL local variable or a MySQL user variable is read as a parameter, and so is `@x` written for PostgreSQL's absolute value of `x`: write `abs(x)`.
````

`docs/diagnostics.md`: index rows, and

````markdown
## SQLSRC117

**Parameter has no type**

A `-- param:` marker names a parameter that is not in the SQL of its query.  Such a parameter reaches the query only inside the text of a token, so nothing but the marker can say what type it has.

```sql
-- name: ListUsers
-- param: @page
SELECT id FROM users {{paging:LIMIT 20 OFFSET @page}};
```

Give the type, in the database's own words: `-- param: @page int`.

## SQLSRC118

**Parameter is not declared**

A parameter appears in the default of a token, in the SQL or in a `-- token:` marker, and nowhere else in the query.  A default is a sample, and a type taken from a sample alone would be a guess.

```sql
-- name: ListUsers
SELECT id FROM users {{paging:LIMIT 20 OFFSET @page}};
```

Declare the parameter with its type: `-- param: @page int`.
````

  In `SQLSRC111` add "A `-- param:` marker starts with the parameter as the SQL writes it, `@name`, alone or followed by a space."  In `SQLSRC112` add "Two `-- param:` markers for one parameter that give different types or different nullability."

- [ ] **Step 8: Commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add -A && git commit -m "Declare parameters with a param marker and apply the list rule

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: The hash

**Files:**
- Create: `src/SqlSource/Parsing/SqlQueryHash.cs`
- Modify: `src/SqlSource/Parsing/SqlDialectName.cs`, `SqlDialectRules.cs`, `SqlFileParseResult.cs`, `SqlFileParser.cs`
- Modify: `src/SqlSource/Generation/ParsedSqlFile.cs`, `SqlFileReader.cs`
- Modify: `src/SqlSource/AGENTS.md`
- Test: create `tests/SqlSource.Tests/Parsing/SqlQueryHashTests.cs`; modify `SqlDialectNameTests.cs`, `SqlDialectRulesTests.cs`, `SqlFileParserTests.cs`; `tests/SqlSource.Tests/Generation/SqlFileReaderTests.cs`

**Interfaces:**
- Consumes: `SqlBlock.Segments`, `SqlBlock.Tokens`, `SqlBlock.Parameters`.
- Produces: `SqlDialectName.Canonical(SqlDialect)` (`string`); `SqlDialectRules.Dialect` (`SqlDialect`); `SqlFileParseResult.Dialect` (`SqlDialect`, the last member); `ParsedSqlFile.Dialect` (`SqlDialect`, after `Errors`, default `SqlDialect.Ansi`); `SqlQueryHash.Compute(SqlDialect dialect, EquatableArray<SqlSegment> segments, EquatableArray<SqlToken> tokens, EquatableArray<SqlQueryParameter> parameters)` returning 64 lower-case hex characters.

- [ ] **Step 1: Write the failing tests**

`tests/SqlSource.Tests/Parsing/SqlQueryHashTests.cs`:

```csharp
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

// The hash ties a query to the description a later phase keeps of it.  Its input is fixed: the engine's name, a line
// feed, the SQL without comments and with each token as {{name:default}}, a line feed, and a line for each parameter
// that a marker declares.
public class SqlQueryHashTests
{
    private const string Simple = "-- name: Q\nSELECT 1;\n";

    // printf 'postgres\nSELECT 1;\n' | shasum -a 256
    [Fact]
    public void Compute_SimpleQuery_IsTheSha256OfItsInput() =>
        Hash(Simple).ShouldBe("3a2a9df518545e35e03bfdafb532ac9ee2b87ba74fbd0805047634d82b7c8cac");

    // printf 'mssql\nSELECT 1;\n' | shasum -a 256
    [Fact]
    public void Compute_AnotherEngine_StartsFromItsCanonicalName() =>
        Hash(Simple, SqlDialect.SqlServer).ShouldBe("5b37670f9fd87ce771169402163baa1ec54b97293d4fa4454897400defac7cf9");

    // printf 'postgres\nSELECT id FROM users WHERE id = @id {{filter:AND x = 1}} ORDER BY {{orderBy}}{{tail:}}\n
    // id\nsince timestamptz not null\npage int\nq text null\n' | shasum -a 256, on one line.
    [Fact]
    public void Compute_QueryWithTokensAndDeclarations_RendersEachAsTheDefinitionSays()
    {
        const string Text =
            "-- name: Q\n-- param: @since timestamptz not null\n-- param: @page int\n-- param: @q text null\n"
            + "-- param: @id\n-- token: {{filter:AND x = 1}}\n"
            + "SELECT id FROM users WHERE id = @id {{filter}} ORDER BY {{orderBy}}{{tail:}}\n";

        Hash(Text).ShouldBe("71e8d6c8d93f1de8eefcc9a1e00c48c4455a0d416c23002d24ead2c0c09399f9");
    }

    [Theory]
    [InlineData("-- name: Q\r\nSELECT 1;\r\n")]
    [InlineData("-- name: Q\n-- summary: Text.\nSELECT 1; -- a comment\n")]
    [InlineData("-- name: Q\n\n  /* a comment */\nSELECT 1;\n\n")]
    [InlineData("-- name: Other\nSELECT 1;\n")]
    public void Compute_ChangeThatLeavesTheSqlAlone_GivesTheSameHash(string text) => Hash(text).ShouldBe(Hash(Simple));

    [Theory]
    // A default, an empty default, and none.
    [InlineData("SELECT {{a}}", "SELECT {{a:x}}")]
    [InlineData("SELECT {{a}}", "SELECT {{a:}}")]
    [InlineData("SELECT {{a:x}}", "SELECT {{a:y}}")]
    // A token's name.
    [InlineData("SELECT {{a}}", "SELECT {{b}}")]
    // A parameter's name, and its case.
    [InlineData("SELECT @a", "SELECT @b")]
    [InlineData("SELECT @a", "SELECT @A")]
    // A declaration, its type and its nullability.
    [InlineData("SELECT @a", "-- param: @a\nSELECT @a")]
    [InlineData("-- param: @a\nSELECT @a", "-- param: @a int\nSELECT @a")]
    [InlineData("-- param: @a int\nSELECT @a", "-- param: @a bigint\nSELECT @a")]
    [InlineData("-- param: @a int\nSELECT @a", "-- param: @a int null\nSELECT @a")]
    [InlineData("-- param: @a int\nSELECT @a", "-- param: @a int not null\nSELECT @a")]
    [InlineData("-- param: @a int null\nSELECT @a", "-- param: @a int not null\nSELECT @a")]
    public void Compute_ChangeToWhatIsDescribed_GivesAnotherHash(string first, string second) =>
        Hash("-- name: Q\n" + first + "\n").ShouldNotBe(Hash("-- name: Q\n" + second + "\n"));

    // A marker that types a parameter of the SQL can move without changing what is described.
    [Fact]
    public void Compute_MarkersOfSqlParametersInAnotherOrder_GivesTheSameHash() =>
        Hash("-- name: Q\n-- param: @a int\n-- param: @b text\nSELECT @a, @b\n")
            .ShouldBe(Hash("-- name: Q\n-- param: @b text\n-- param: @a int\nSELECT @a, @b\n"));

    [Fact]
    public void Compute_AnyQuery_IsSixtyFourLowerCaseHexCharacters() => Hash(Simple).ShouldMatch("^[0-9a-f]{64}$");

    private static string Hash(string text, SqlDialect dialect = SqlDialect.PostgreSql)
    {
        var result = SqlFileParser.Parse(text, "Query.sql", new SqlDialectChoice(dialect, SqlDialectOptions.None));
        result.Errors.ShouldBeEmpty();
        var block = result.Blocks.ShouldHaveSingleItem();
        return SqlQueryHash.Compute(result.Dialect, block.Segments, block.Tokens, block.Parameters);
    }
}
```

`SqlDialectNameTests`:

```csharp
[Theory]
[InlineData("Ansi", "ansi")]
[InlineData("SqlServer", "mssql")]
[InlineData("PostgreSql", "postgres")]
[InlineData("CockroachDb", "cockroachdb")]
[InlineData("MySql", "mysql")]
[InlineData("MariaDb", "mariadb")]
[InlineData("Sqlite", "sqlite")]
[InlineData("Oracle", "oracle")]
public void Canonical_Dialect_IsItsFirstName(string dialect, string expected)
{
    var name = SqlDialectName.Canonical(Enum.Parse<SqlDialect>(dialect));

    name.ShouldBe(expected);
    SqlDialectName.TryParse(name, out var parsed).ShouldBeTrue();
    parsed.Dialect.ShouldBe(Enum.Parse<SqlDialect>(dialect));
}
```

`SqlDialectRulesTests`:

```csharp
[Fact]
public void Dialect_OfTheRulesOfAChoice_IsThatChoicesDialect()
{
    foreach (var dialect in Enum.GetValues<SqlDialect>())
    {
        SqlDialectRules.For(new SqlDialectChoice(dialect, SqlDialectOptions.None)).Dialect.ShouldBe(dialect);
    }
}
```

`SqlFileParserTests`:

```csharp
[Theory]
[InlineData("SELECT 1;\n", "MySql", "MySql")]
[InlineData("-- dialect: mssql\nSELECT 1;\n", "MySql", "SqlServer")]
[InlineData("-- dialect: nope\nSELECT 'open\n", "Oracle", "Oracle")]
public void Parse_Result_NamesTheDialectTheFileWasReadBy(string text, string given, string expected) =>
    SqlFileParser
        .Parse(text, "Query.sql", new SqlDialectChoice(Enum.Parse<SqlDialect>(given), SqlDialectOptions.None))
        .Dialect.ShouldBe(Enum.Parse<SqlDialect>(expected));
```

`SqlFileReaderTests`: a file read with `SqlDialect.MySql` has `Dialect` `MySql`, and one with a `-- dialect: postgres` marker has `PostgreSql`.

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet build SqlSource.slnx`
Expected: FAIL, no `SqlQueryHash`.

- [ ] **Step 3: Give the rules and the parse result their dialect**

`SqlDialectName`:

```csharp
    /// <summary>
    /// The name that stands for <paramref name="dialect" /> wherever one name is needed: its first in the list.
    /// </summary>
    public static string Canonical(SqlDialect dialect)
    {
        foreach (var (name, candidate) in Names)
        {
            if (candidate == dialect)
            {
                return name;
            }
        }

        return Names[0].Name;
    }
```

`SqlDialectRules`: a property

```csharp
    /// <summary>The dialect these rules are for.</summary>
    public SqlDialect Dialect { get; private init; }
```

set in each initialiser: `Dialect = SqlDialect.SqlServer` and so on for `SqlServer`, `PostgreSql`, `CockroachDb`, `Sqlite` and `Oracle`; `Ansi` keeps the default, which is `SqlDialect.Ansi`; and in `CreateMySqlFamily` each branch sets `Dialect = SqlDialect.MariaDb` or `Dialect = SqlDialect.MySql`.

`SqlFileParseResult` gains `SqlDialect Dialect`, documented "The dialect the file was read by: the one it was given, or the one its `-- dialect:` marker names."  `SqlFileParser.Parse` passes `lexer.Rules.Dialect` for a lexer error, and `Parser` passes `rules.Dialect` in both results of `Run`.

`ParsedSqlFile`: `SqlDialect Dialect = SqlDialect.Ansi` after `Errors`, documented "The dialect the file was read by."  `SqlFileReader.Read` passes `result.Dialect`, then `invalidDialect`.

- [ ] **Step 4: Write the hash**

`src/SqlSource/Parsing/SqlQueryHash.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace SqlSource.Parsing;

/// <summary>
/// The hash that ties a query to a description of it.
/// </summary>
/// <remarks>
/// SHA-256, in lower-case hex, of the UTF-8 bytes of: the engine's canonical name; a line feed; the query's SQL
/// without comments, with each token as <c>{{name:default}}</c>, <c>{{name:}}</c> for an empty default and
/// <c>{{name}}</c> for none; a line feed; and for each parameter that a <c>-- param:</c> marker declares, in the
/// order of the query's list, its name, a space and its type when it has one, <c> null</c> or <c> not null</c> when
/// the marker says so, and a line feed.  A comment changes no type and is not part of it; a parameter's name, a
/// default and a declaration are.  The definition is a contract with the files that hold a hash: changing it makes
/// every one of them stale.
/// </remarks>
internal static class SqlQueryHash
{
    private const string Hex = "0123456789abcdef";

    /// <summary>
    /// Computes the hash.  <paramref name="segments" /> is the comment-stripped SQL.  Nothing calls this while a
    /// file is parsed: a query that needs no description never pays for it.
    /// </summary>
    public static string Compute(
        SqlDialect dialect,
        EquatableArray<SqlSegment> segments,
        EquatableArray<SqlToken> tokens,
        EquatableArray<SqlQueryParameter> parameters
    )
    {
        var input = new StringBuilder();
        _ = input.Append(SqlDialectName.Canonical(dialect)).Append('\n');
        foreach (var segment in segments)
        {
            if (segment.Kind == SqlSegmentKind.Literal)
            {
                _ = input.Append(segment.Text);
                continue;
            }

            _ = input.Append("{{").Append(segment.Text);
            if (DefaultOf(tokens, segment.Text) is { } defaultText)
            {
                _ = input.Append(':').Append(defaultText);
            }

            _ = input.Append("}}");
        }

        _ = input.Append('\n');
        foreach (var parameter in parameters)
        {
            if (!parameter.IsDeclared)
            {
                continue;
            }

            _ = input.Append(parameter.Name);
            if (parameter.Type is not null)
            {
                _ = input.Append(' ').Append(parameter.Type);
            }

            if (parameter.Nullable is { } nullable)
            {
                _ = input.Append(nullable ? " null" : " not null");
            }

            _ = input.Append('\n');
        }

        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input.ToString()));
        var hex = new char[hash.Length * 2];
        for (var index = 0; index < hash.Length; index++)
        {
            hex[index * 2] = Hex[hash[index] >> 4];
            hex[(index * 2) + 1] = Hex[hash[index] & 0xF];
        }

        return new string(hex);
    }

    private static string? DefaultOf(EquatableArray<SqlToken> tokens, string name)
    {
        foreach (var token in tokens)
        {
            if (token.Name == name)
            {
                return token.Default;
            }
        }

        return null;
    }
}
```

- [ ] **Step 5: Build, and check the analyzer allows it**

Run: `dotnet build SqlSource.slnx`
Expected: PASS with no warning.  If the build reports `RS1035` or another banned-API rule for `SHA256`, stop and report it to the owner: the fix is a decision, not a suppression.

- [ ] **Step 6: Run the tests**

Run: `dotnet test --solution SqlSource.slnx`
Expected: PASS.

- [ ] **Step 7: Update `src/SqlSource/AGENTS.md`**

Under `Parsing/`: "**The hash has one definition, `SqlQueryHash`.**  Its input is a contract with every file that stores a hash, so a change to it, or to what `SqlTextBuilder` gives for stripped SQL, makes them all stale.  It is not computed during a parse."

- [ ] **Step 8: Commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add -A && git commit -m "Define the hash of a query

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: The `-> shape` suffix of the name marker

**Files:**
- Create: `src/SqlSource/Settings/ResultShape.cs`, `src/SqlSource/Settings/SettingValue.cs`
- Modify: `src/SqlSource/Parsing/SqlFileParser.cs`, `SqlBlock.cs`; `src/SqlSource/Generation/SqlQuery.cs`, `SqlFileReader.cs`
- Modify: `README.md`, `docs/diagnostics.md`, `src/SqlSource/AGENTS.md`
- Test: create `tests/SqlSource.Tests/Settings/SettingValueTests.cs`; modify `tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs`, `tests/SqlSource.Tests/Generation/SqlFileReaderTests.cs`

**Interfaces:**
- Produces: namespace `SqlSource.Settings`; `ResultShape { Many, One, OneOptional, None, RowCount }`; `SettingValue.TryReadChoice<T>(ReadOnlySpan<char> value, out T choice) where T : struct, Enum`, which matches a member's name ignoring case and the hyphens, spaces and tabs inside `value`; `SqlBlock.Shape` and `SqlQuery.Shape` (`ResultShape?`), after `Summary`.

- [ ] **Step 1: Write the failing tests**

`tests/SqlSource.Tests/Settings/SettingValueTests.cs`:

```csharp
using System;
using Shouldly;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Settings;

public class SettingValueTests
{
    [Theory]
    [InlineData("many", "Many")]
    [InlineData("ONE", "One")]
    [InlineData("one-optional", "OneOptional")]
    [InlineData("OneOptional", "OneOptional")]
    [InlineData("one optional", "OneOptional")]
    [InlineData("row-count", "RowCount")]
    [InlineData("rowcount", "RowCount")]
    [InlineData("none", "None")]
    public void TryReadChoice_MemberNameInAnySpelling_IsRead(string value, string expected)
    {
        SettingValue.TryReadChoice<ResultShape>(value.AsSpan(), out var shape).ShouldBeTrue();

        shape.ShouldBe(Enum.Parse<ResultShape>(expected));
    }

    [Theory]
    [InlineData("")]
    [InlineData("-")]
    [InlineData("on")]
    [InlineData("ones")]
    [InlineData("one_optional")]
    [InlineData("1")]
    [InlineData("one, many")]
    public void TryReadChoice_AnythingElse_IsNotRead(string value) =>
        SettingValue.TryReadChoice<ResultShape>(value.AsSpan(), out _).ShouldBeFalse();
}
```

`SqlFileParserTests`:

```csharp
[Theory]
[InlineData("-- name: A", "A", null)]
[InlineData("-- name: A -> many", "A", "Many")]
[InlineData("-- name: A->one", "A", "One")]
[InlineData("-- name: A  ->  One-Optional  ", "A", "OneOptional")]
[InlineData("-- name: A -> rowcount", "A", "RowCount")]
[InlineData("-- name: A -> none", "A", "None")]
public void Parse_NameMarker_ReadsTheNameAndTheShape(string marker, string name, string? shape)
{
    var text = marker + "\nSELECT 1\n";

    var block = Blocks(text).ShouldHaveSingleItem();

    block.Name.ShouldBe(name);
    block.NameSpan.ShouldBe(SpanOf(text, name));
    block.Shape.ShouldBe(shape is null ? null : Enum.Parse<ResultShape>(shape));
}

[Theory]
[InlineData("A ->", "->")]
[InlineData("A -> several", "-> several")]
[InlineData("A -> one -> many", "-> one -> many")]
[InlineData("A -> one, many", "-> one, many")]
public void Parse_NameMarkerWithAShapeThatIsNotOne_IsInvalidAtWhatFollowsTheName(string value, string at)
{
    var text = "-- name: " + value + "\nSELECT 1\n";

    Errors(text)
        .ShouldBe([SqlParseError.Create(SqlParseErrorKind.InvalidMarkerValue, SpanOf(text, at), "name: " + value)]);
}

[Fact]
public void Parse_NameMarkerWithAShapeAndNoName_IsAnInvalidName()
{
    const string Text = "-- name: -> one\nSELECT 1\n";

    Errors(Text)
        .ShouldBe([SqlParseError.Create(SqlParseErrorKind.InvalidName, SpanOf(Text, "-- name: -> one"), string.Empty)]);
}

[Fact]
public void Parse_FileWithoutNameMarker_HasNoShape() => Blocks("SELECT 1\n").ShouldHaveSingleItem().Shape.ShouldBeNull();
```

Add `using SqlSource.Settings;` to the file.

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet build SqlSource.slnx`
Expected: FAIL, no namespace `SqlSource.Settings`.

- [ ] **Step 3: Add the shape and the choice reader**

`src/SqlSource/Settings/ResultShape.cs`:

```csharp
namespace SqlSource.Settings;

/// <summary>
/// What a query's generated method returns, as the name marker's <c>-&gt; shape</c> says.
/// </summary>
internal enum ResultShape
{
    /// <summary>Every row, in the collection type.</summary>
    Many,

    /// <summary>One row.  None, or more than one, is an error at run time.</summary>
    One,

    /// <summary>One row or null.  More than one is an error at run time.</summary>
    OneOptional,

    /// <summary>Nothing.</summary>
    None,

    /// <summary>The number of rows the statement affected.</summary>
    RowCount,
}
```

`src/SqlSource/Settings/SettingValue.cs`:

```csharp
using System;

namespace SqlSource.Settings;

/// <summary>
/// Reads the values of settings.  A marker, an MSBuild property and the metadata of an item all read through here,
/// so that one setting has one set of values wherever it is written.
/// </summary>
internal static class SettingValue
{
    /// <summary>
    /// Reads a value from a fixed list: the name of a member of <typeparamref name="T" />, ignoring case and
    /// ignoring the hyphens, spaces and tabs inside <paramref name="value" />.  So <c>CodeGen</c>, <c>codegen</c>
    /// and <c>code-gen</c> are one value, and so are <c>SealedRecord</c> and <c>sealed record</c>.  Nothing is
    /// allocated.
    /// </summary>
    public static bool TryReadChoice<T>(ReadOnlySpan<char> value, out T choice)
        where T : struct, Enum
    {
        var names = Choices<T>.Names;
        for (var index = 0; index < names.Length; index++)
        {
            if (Matches(value, names[index]))
            {
                choice = Choices<T>.Values[index];
                return true;
            }
        }

        choice = default;
        return false;
    }

    private static bool Matches(ReadOnlySpan<char> value, string name)
    {
        var matched = 0;
        foreach (var character in value)
        {
            if (character is '-' or ' ' or '\t')
            {
                continue;
            }

            if (matched == name.Length || char.ToUpperInvariant(character) != char.ToUpperInvariant(name[matched]))
            {
                return false;
            }

            matched++;
        }

        return matched == name.Length;
    }

    // The members of an enum, read once for each enum.
    private static class Choices<T>
        where T : struct, Enum
    {
        public static readonly string[] Names = Enum.GetNames(typeof(T));

        public static readonly T[] Values = (T[])Enum.GetValues(typeof(T));
    }
}
```

`Enum.GetNames` and `Enum.GetValues` both order by value, so the two arrays line up.

- [ ] **Step 4: Read the shape in the parser**

`SqlFileParser.ReadNamedBlocks`, the body of the loop:

```csharp
                var (index, marker) = nameMarkers[position];
                var end = position + 1 < nameMarkers.Count ? nameMarkers[position + 1].Index : lexemes.Count;
                var (written, shape) = ReadName(marker);
                var name = text.Substring(written.Start, written.Length);
                var nameSpan = name.Length == 0 ? marker.Span : written;
                if (!SqlIdentifier.IsUsableName(name))
                {
                    AddError(SqlParseErrorKind.InvalidName, nameSpan, name);
                }
                else if (!_names.Add(name))
                {
                    AddError(SqlParseErrorKind.DuplicateName, nameSpan, name);
                }

                // A marker with an unusable name still starts a block, so that what follows is checked as a block.
                ReadBlock(name, nameSpan, shape, preamble, index + 1, end);
```

```csharp
        // "Name", or "Name -> shape".  What follows the name is reported whole when it is not an arrow and a shape.
        private (TextSpan Name, ResultShape? Shape) ReadName(SqlMarker marker)
        {
            var value = marker.ValueSpan;
            var arrow = text.IndexOf("->", value.Start, value.Length, StringComparison.Ordinal);
            if (arrow < 0)
            {
                return (value, null);
            }

            var nameEnd = arrow;
            while (nameEnd > value.Start && char.IsWhiteSpace(text[nameEnd - 1]))
            {
                nameEnd--;
            }

            ResultShape? shape = null;
            if (SettingValue.TryReadChoice<ResultShape>(text.AsSpan(arrow + 2, value.End - arrow - 2), out var read))
            {
                shape = read;
            }
            else
            {
                AddError(
                    SqlParseErrorKind.InvalidMarkerValue,
                    TextSpan.FromBounds(arrow, value.End),
                    SqlMarkerReader.Describe(text, marker)
                );
            }

            return (TextSpan.FromBounds(value.Start, nameEnd), shape);
        }
```

`ReadBlock` takes `ResultShape? shape` after `nameSpan` and passes it to `new SqlBlock(...)` after the summary; `ReadUnnamedFile` passes `null`.  `SqlBlock` and `SqlQuery` gain `ResultShape? Shape` after `Summary`, documented "The shape the name marker gives, or null when it gives none.  It has no effect yet."  `SqlFileReader` copies it.  Add `using System;` and `using SqlSource.Settings;` where needed, and fix the hand-built models in the tests with `null`.

- [ ] **Step 5: Run everything**

Run: `dotnet test --solution SqlSource.slnx`
Expected: PASS.

- [ ] **Step 6: Update the documents**

`README.md`, under Queries, after the paragraph on `-- name:`:

```markdown
A name can be followed by `->` and a shape: `-- name: GetUser -> one-optional`.  The shapes are `many`, `one`, `one-optional`, `none` and `rowcount`.  A shape says what the method that a later release generates for the query returns; it is checked now and has no effect yet.
```

`docs/diagnostics.md`, `SQLSRC111`: add "What follows the name in a `-- name:` marker must be `->` and one of `many`, `one`, `one-optional`, `none` and `rowcount`."  `SQLSRC103`: add "The name ends at `->`, when the marker has one."

`src/SqlSource/AGENTS.md`: a new section before `## Diagnostics`:

```markdown
## `Settings/`

Plain data and the readers of values: no file access, no symbols, no pipeline types.  `Parsing/` and `Generation/` both use it, and the command-line tool will.

- **A value from a fixed list is read by `SettingValue.TryReadChoice`**, from the names of an enum's members, ignoring case, hyphens and spaces.  Renaming a member changes what a user may write.
```

- [ ] **Step 7: Commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add -A && git commit -m "Read a result shape after a query's name

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: The generator parameters' new vocabulary

**Files:**
- Create: `src/SqlSource/Settings/GeneratorParameters.cs`, `src/SqlSource/Settings/GeneratorParameterList.cs`
- Modify: `src/SqlSource/Parsing/SqlGeneratorParameterScope.cs`, `SqlFileParser.cs`
- Modify: `tests/SqlSource.Tests/EndToEnd/Tokens/Search.sql`, `tools/package-install/Queries/Users.sql`
- Modify: `README.md`, `docs/diagnostics.md`
- Test: create `tests/SqlSource.Tests/Settings/GeneratorParameterListTests.cs`; rewrite `tests/SqlSource.Tests/Parsing/SqlGeneratorParameterScopeTests.cs`; modify `SqlFileParserTests.cs`, `tests/SqlSource.Tests/Generator/TokenValidationTests.cs`, `tests/SqlSource.Tests/Diagnostics/DiagnosticsDocumentTests.cs`

**Interfaces:**
- Produces:
  - `[Flags] enum GeneratorParameters { None = 0, KeepComments = 1, NoTokenValidation = 2, SortInput = 4, SortOutput = 8, NoTableModels = 16, AsyncMethodSuffix = 32 }`.
  - `GeneratorParameterList.Default` (`"default"`); `GeneratorParameterList.TryFind(ReadOnlySpan<char> word, out GeneratorParameters parameter)`; `GeneratorParameterList.Parse(string? value, List<string> invalid)` returning `GeneratorParameters?`: null for a value that is empty or gives no list, with each word that is not valid, or the whole value when `default` stands beside another word, added to `invalid`.
  - `SqlGeneratorParameterScope.Parameters` (`GeneratorParameters?`, null when the scope has no `-- generator:` marker that gave a parameter) with `KeepComments`, `TokenValidation` and the old flags gone.
- The block's `KeepComments` and `TokenValidation` stay until Task 8, computed from the list.

- [ ] **Step 1: Write the failing tests**

`tests/SqlSource.Tests/Settings/GeneratorParameterListTests.cs`:

```csharp
using System.Collections.Generic;
using Shouldly;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Settings;

public class GeneratorParameterListTests
{
    [Theory]
    // An internal enum cannot be the parameter of a public test method, so a row gives the flags as a number.
    [InlineData("keep-comments", 1)]
    [InlineData("NO-TOKEN-VALIDATION", 2)]
    [InlineData("sort-input", 4)]
    [InlineData("sort-output", 8)]
    [InlineData("no-table-models", 16)]
    [InlineData("async-method-suffix", 32)]
    [InlineData("  keep-comments\tsort-input  keep-comments ", 5)]
    [InlineData("default", 0)]
    [InlineData(" Default ", 0)]
    public void Parse_ValidList_IsItsParameters(string value, int expected)
    {
        var invalid = new List<string>();

        ((int?)GeneratorParameterList.Parse(value, invalid)).ShouldBe(expected);
        invalid.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \t ")]
    public void Parse_NoValue_GivesNoList(string? value)
    {
        var invalid = new List<string>();

        GeneratorParameterList.Parse(value, invalid).ShouldBeNull();
        invalid.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_UnknownWords_AreNamedAndTheOthersApply()
    {
        var invalid = new List<string>();

        GeneratorParameterList
            .Parse("keep-comments token-validation keep-comments=1 nope", invalid)
            .ShouldBe(GeneratorParameters.KeepComments);
        invalid.ShouldBe(["token-validation", "keep-comments=1", "nope"]);
    }

    [Fact]
    public void Parse_OnlyUnknownWords_GivesNoList()
    {
        var invalid = new List<string>();

        GeneratorParameterList.Parse("nope", invalid).ShouldBeNull();
        invalid.ShouldBe(["nope"]);
    }

    [Fact]
    public void Parse_DefaultBesideAnotherWord_IsInvalidWhole()
    {
        var invalid = new List<string>();

        GeneratorParameterList.Parse(" default  keep-comments ", invalid).ShouldBeNull();
        invalid.ShouldBe(["default  keep-comments"]);
    }
}
```

Rewrite `SqlGeneratorParameterScopeTests` around `Parameters`, keeping its `Read` helper:

```csharp
[Fact]
public void NewScope_HasNoList() => new SqlGeneratorParameterScope().Parameters.ShouldBeNull();

[Theory]
[InlineData("-- generator: keep-comments", 1)]
[InlineData("-- generator: KEEP-COMMENTS No-Token-Validation", 3)]
[InlineData("-- generator: sort-input sort-output no-table-models async-method-suffix", 60)]
[InlineData("-- generator: default", 0)]
[InlineData("-- generator: DEFAULT", 0)]
public void Read_KnownParameters_SetTheList(string line, int expected)
{
    var (scope, errors) = Read(line);

    errors.ShouldBeEmpty();
    ((int?)scope.Parameters).ShouldBe(expected);
}

[Fact]
public void Read_TwoMarkersOfOneScope_AddUp()
{
    var (scope, errors) = Read("-- generator: keep-comments", "-- generator: sort-input keep-comments");

    errors.ShouldBeEmpty();
    scope.Parameters.ShouldBe(GeneratorParameters.KeepComments | GeneratorParameters.SortInput);
}

[Theory]
[InlineData("token-validation")]
[InlineData("token-ignore=a")]
[InlineData("dialect=mysql")]
[InlineData("nope")]
public void Read_WordThatIsNoParameter_IsUnknown(string word)
{
    var (_, errors) = Read("-- generator: " + word);

    var error = errors.ShouldHaveSingleItem();
    error.Kind.ShouldBe(SqlParseErrorKind.UnknownGeneratorParameter);
    error.Arguments.ShouldBe([word]);
}

[Theory]
[InlineData("keep-comments=1")]
[InlineData("default=")]
[InlineData("sort-input=true")]
public void Read_ParameterWithAValue_IsInvalid(string word)
{
    var (_, errors) = Read("-- generator: " + word);

    var error = errors.ShouldHaveSingleItem();
    error.Kind.ShouldBe(SqlParseErrorKind.InvalidMarkerValue);
    error.Arguments.ShouldBe([word]);
}

[Theory]
[InlineData(new[] { "-- generator: default keep-comments" }, "keep-comments")]
[InlineData(new[] { "-- generator: keep-comments default" }, "default")]
[InlineData(new[] { "-- generator: default", "-- generator: sort-input" }, "sort-input")]
[InlineData(new[] { "-- generator: sort-input", "-- generator: default" }, "default")]
public void Read_DefaultBesideAnotherParameter_ConflictsAtTheSecond(string[] lines, string second)
{
    var (_, errors) = Read(lines);

    var error = errors.ShouldHaveSingleItem();
    error.Kind.ShouldBe(SqlParseErrorKind.ConflictingSettings);
    error.Arguments.ShouldBe([second]);
}

[Fact]
public void Read_DefaultTwice_IsNotAConflict()
{
    var (scope, errors) = Read("-- generator: default", "-- generator: default");

    errors.ShouldBeEmpty();
    scope.Parameters.ShouldBe(GeneratorParameters.None);
}
```

If the file's `Read` helper takes one line, make it `params string[] lines`: lex and read each line as its own text, into one scope and one error list.  Keep its tests of the empty marker (`SQLSRC110`) and of the spans of errors.

`SqlFileParserTests`:

```csharp
[Theory]
// The query has a list: it is the list, whole.
[InlineData("keep-comments", "no-token-validation", false, false)]
[InlineData("keep-comments no-token-validation", "default", false, true)]
[InlineData("no-token-validation", "keep-comments", true, true)]
// The query has none: the preamble's.
[InlineData("keep-comments no-token-validation", null, true, false)]
public void Parse_QueryWithItsOwnGeneratorParameters_ReplacesThePreambles(
    string preamble,
    string? query,
    bool keepComments,
    bool? tokenValidation
)
{
    var text =
        "-- generator: " + preamble + "\n-- name: Q\n"
        + (query is null ? string.Empty : "-- generator: " + query + "\n")
        + "SELECT 1 -- c\n";

    var block = Blocks(text).ShouldHaveSingleItem();

    block.KeepComments.ShouldBe(keepComments);
    block.TokenValidation.ShouldBe(tokenValidation);
}

[Fact]
public void Parse_QueryWithoutAnyGeneratorParameters_LeavesValidationToTheProject() =>
    Blocks("-- name: Q\nSELECT 1\n").ShouldHaveSingleItem().TokenValidation.ShouldBeNull();
```

Review the file's existing tests of generator parameters: one that expects a query to keep what the preamble gave while adding its own now expects the query's alone.

`TokenValidationTests`: replace the theory's rows with

```csharp
    // Nothing set: validate.
    [InlineData(null, null, null, true)]
    // The property alone.
    [InlineData(null, null, "false", false)]
    [InlineData(null, null, " False ", false)]
    [InlineData(null, null, "true", true)]
    [InlineData(null, null, "", true)]
    // The file's list beats the property, with or without the switch.
    [InlineData(null, "no-token-validation", null, false)]
    [InlineData(null, "no-token-validation", "true", false)]
    [InlineData(null, "default", "false", true)]
    [InlineData(null, "keep-comments", "false", true)]
    // The query's list replaces the file's.
    [InlineData("no-token-validation", null, "true", false)]
    [InlineData("default", null, "false", true)]
    [InlineData("no-token-validation", "default", "true", false)]
    [InlineData("default", "no-token-validation", "false", true)]
    [InlineData("keep-comments", "no-token-validation", "false", true)]
```

and reword the comment above the class: a list decides whole, the query's before the file's, and the property decides for a query that has neither.

`DiagnosticsDocumentTests.Document_UnknownGeneratorParameterSection_ListsEveryGeneratorParameter`: `parameters` becomes `["keep-comments", "no-token-validation", "sort-input", "sort-output", "no-table-models", "async-method-suffix", "default"]`.

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet build SqlSource.slnx`
Expected: FAIL, no `GeneratorParameters`.

- [ ] **Step 3: Add the flags and the list reader**

`src/SqlSource/Settings/GeneratorParameters.cs`:

```csharp
using System;

namespace SqlSource.Settings;

/// <summary>
/// The switches a list of generator parameters can hold.  Each is a departure from a default, so the empty list is
/// every default.
/// </summary>
[Flags]
internal enum GeneratorParameters
{
    /// <summary>The empty list.</summary>
    None = 0,

    /// <summary><c>keep-comments</c>: comments and blank lines stay in the SQL.</summary>
    KeepComments = 1,

    /// <summary><c>no-token-validation</c>: a query's method does not check its arguments.</summary>
    NoTokenValidation = 2,

    /// <summary><c>sort-input</c>: an input model's properties are sorted by name.</summary>
    SortInput = 4,

    /// <summary><c>sort-output</c>: an output model's properties are sorted by name.</summary>
    SortOutput = 8,

    /// <summary><c>no-table-models</c>: a query whose result is a table gets a model of its own.</summary>
    NoTableModels = 16,

    /// <summary><c>async-method-suffix</c>: a generated method's name ends in <c>Async</c>.</summary>
    AsyncMethodSuffix = 32,
}
```

`src/SqlSource/Settings/GeneratorParameterList.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace SqlSource.Settings;

/// <summary>
/// The names of the generator parameters, and a list of them as a property, the metadata of an item or the
/// attribute writes it.  This is the only place the names are known.
/// </summary>
/// <remarks>
/// A list is words separated by white space.  Each level that gives a list gives it whole: nothing is added to the
/// list of a level below.  The word <c>default</c>, alone, is the empty list.
/// </remarks>
internal static class GeneratorParameterList
{
    /// <summary>The word that stands for the empty list.</summary>
    public const string Default = "default";

    private static readonly (string Name, GeneratorParameters Parameter)[] Names =
    [
        ("keep-comments", GeneratorParameters.KeepComments),
        ("no-token-validation", GeneratorParameters.NoTokenValidation),
        ("sort-input", GeneratorParameters.SortInput),
        ("sort-output", GeneratorParameters.SortOutput),
        ("no-table-models", GeneratorParameters.NoTableModels),
        ("async-method-suffix", GeneratorParameters.AsyncMethodSuffix),
    ];

    /// <summary>Finds a parameter by its name, ignoring case.</summary>
    public static bool TryFind(ReadOnlySpan<char> word, out GeneratorParameters parameter)
    {
        foreach (var (name, candidate) in Names)
        {
            if (word.Equals(name.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                parameter = candidate;
                return true;
            }
        }

        parameter = GeneratorParameters.None;
        return false;
    }

    /// <summary>Whether <paramref name="word" /> is <see cref="Default" />, ignoring case.</summary>
    public static bool IsDefault(ReadOnlySpan<char> word) =>
        word.Equals(Default.AsSpan(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads a list that is not in a marker.  Null when the value is empty or holds no word that is a parameter:
    /// the level then gives no list.  A word that is no parameter is added to <paramref name="invalid" /> and the
    /// other words apply.  <c>default</c> beside another word makes the whole value invalid.
    /// </summary>
    public static GeneratorParameters? Parse(string? value, List<string> invalid)
    {
        var rest = value.AsSpan().Trim();
        if (rest.IsEmpty)
        {
            return null;
        }

        var whole = rest;
        GeneratorParameters? parameters = null;
        var words = 0;
        var hasDefault = false;
        while (!rest.IsEmpty)
        {
            var length = 0;
            while (length < rest.Length && !char.IsWhiteSpace(rest[length]))
            {
                length++;
            }

            var word = rest.Slice(0, length);
            rest = rest.Slice(length).TrimStart();
            words++;
            if (IsDefault(word))
            {
                hasDefault = true;
            }
            else if (TryFind(word, out var parameter))
            {
                parameters = (parameters ?? GeneratorParameters.None) | parameter;
            }
            else
            {
                invalid.Add(word.ToString());
            }
        }

        if (!hasDefault)
        {
            return parameters;
        }

        if (words == 1)
        {
            return GeneratorParameters.None;
        }

        invalid.Clear();
        invalid.Add(whole.ToString());
        return null;
    }
}
```

`invalid.Clear()` is safe because every caller passes a list of its own for one value.

- [ ] **Step 4: Rewrite the marker's scope**

`SqlGeneratorParameterScope` becomes:

```csharp
using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Settings;

namespace SqlSource.Parsing;

/// <summary>
/// The generator parameters given by the <c>-- generator:</c> markers of one scope: a block, or a file's preamble.
/// </summary>
/// <remarks>
/// The markers of one scope add up to the scope's list.  A list is never added to another scope's: a query that has
/// one uses it whole.
/// </remarks>
internal sealed class SqlGeneratorParameterScope
{
    // Whether the scope's list is the word "default".
    private bool _isDefault;

    /// <summary>The scope's list, or null when no marker of the scope gave a parameter.</summary>
    public GeneratorParameters? Parameters { get; private set; }

    /// <summary>
    /// Applies the generator parameters of one <c>-- generator:</c> marker to this scope, adding any problems to
    /// <paramref name="errors" />.
    /// </summary>
    public void Read(string text, SqlMarker marker, List<SqlParseError> errors)
    {
        if (marker.ValueSpan.IsEmpty)
        {
            errors.Add(SqlParseError.Create(SqlParseErrorKind.EmptyGeneratorLine, marker.Span));
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

            if (Apply(text.AsSpan(start, wordEnd - start)) is { } problem)
            {
                errors.Add(
                    SqlParseError.Create(
                        problem,
                        TextSpan.FromBounds(start, wordEnd),
                        text.Substring(start, wordEnd - start)
                    )
                );
            }

            start = wordEnd;
            while (start < end && char.IsWhiteSpace(text[start]))
            {
                start++;
            }
        }
    }

    private SqlParseErrorKind? Apply(ReadOnlySpan<char> word)
    {
        var separator = word.IndexOf('=');
        var name = separator < 0 ? word : word.Slice(0, separator);
        if (GeneratorParameterList.IsDefault(name))
        {
            if (separator >= 0)
            {
                return SqlParseErrorKind.InvalidMarkerValue;
            }

            if (Parameters is not null && !_isDefault)
            {
                return SqlParseErrorKind.ConflictingSettings;
            }

            _isDefault = true;
            Parameters = GeneratorParameters.None;
            return null;
        }

        if (!GeneratorParameterList.TryFind(name, out var parameter))
        {
            return SqlParseErrorKind.UnknownGeneratorParameter;
        }

        if (separator >= 0)
        {
            return SqlParseErrorKind.InvalidMarkerValue;
        }

        if (_isDefault)
        {
            return SqlParseErrorKind.ConflictingSettings;
        }

        Parameters = (Parameters ?? GeneratorParameters.None) | parameter;
        return null;
    }
}
```

`SqlFileParser.ReadBlock`: the lines that compute `inherited` and `keepComments` become

```csharp
            // A query that has a list uses it whole.  One that has none uses the preamble's.
            var list = scope.Generator.Parameters ?? preamble?.Generator.Parameters;
            var keepComments = list is { } given && (given & GeneratorParameters.KeepComments) != 0;
```

and the block's token validation is `list is { } decided ? (decided & GeneratorParameters.NoTokenValidation) == 0 : null`.

- [ ] **Step 5: Move the fixtures off `token-validation`**

`tests/SqlSource.Tests/EndToEnd/Tokens/Search.sql`: in `Checked`, `-- generator: token-validation` becomes `-- generator: default`, and the file's first comment becomes "Queries with tokens.  The test project turns token validation off, so only Checked, whose own list is the default, checks its arguments."  `tools/package-install/Queries/Users.sql`: the same change in `ListChecked`.  `tools/package-install/Program.cs`: the comment "A generator parameter turns it back on for this query." becomes "The query's own list, `default`, replaces the project's, so this one checks."  Run `git grep -n 'token-validation' -- . ':!docs/superpowers'` and change each remaining use of the bare `token-validation` the same way.

- [ ] **Step 6: Run everything**

Run: `dotnet test --solution SqlSource.slnx`
Expected: PASS.

- [ ] **Step 7: Update the documents**

`README.md`, the Generator parameters section, whole:

````markdown
### Generator parameters

A `-- generator:` line holds one or more generator parameters, separated by spaces.  Each one is a departure from a default, so a query with no list gets every default.

| Parameter | Effect |
|----|----|
| `keep-comments` | Comments and blank lines stay in the SQL |
| `no-token-validation` | The query's method does not check its arguments (see Tokens, below) |
| `sort-input`, `sort-output`, `no-table-models`, `async-method-suffix` | None yet: they shape what a later release generates |
| `default` | The empty list: every default.  It stands alone |

```sql
-- name: Report
-- generator: keep-comments
SELECT /* the database logs this comment */ id FROM users;
```

**A list replaces; it never adds.**  The `-- generator:` lines inside a query are that query's list.  The lines before the first `-- name:` line are the list of every query in the file that has none of its own.  A query whose file says `keep-comments` and which itself says `no-token-validation` does not keep its comments: it restates `keep-comments` if it wants it.  `-- generator: default` gives a query every default, whatever its file says.
````

Under Tokens, Validation: the three switches become

```markdown
1. The generator parameters inside the query, when it has any: with `no-token-validation` the method checks nothing, and without it the method checks.
2. Otherwise the generator parameters before the first `-- name:` line, the same way.
3. Otherwise the MSBuild property `SqlSourceTokenValidation`, which covers the project.  It accepts `true` and `false`; any other value is the error [SQLSRC010](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc010).
```

`docs/diagnostics.md`: `SQLSRC109` lists the seven words of the table and says that `token-validation` is gone because omitting `no-token-validation` says it.  `SQLSRC111`: "No generator parameter takes a value: `keep-comments=1` is this error."  `SQLSRC112`: replace what it says of the two validation parameters with "`default` beside another generator parameter in one scope: `default` is the empty list, and a list that holds something is not empty."

- [ ] **Step 8: Commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add -A && git commit -m "Give generator parameters their new vocabulary, and let a query's list replace its file's

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: `SettingsLevel`, `QuerySettings`, and the two forms of a query's SQL

**Files:**
- Create: `src/SqlSource/Settings/SettingsLevel.cs`, `src/SqlSource/Settings/QuerySettings.cs`
- Modify: `src/SqlSource/Parsing/SqlMarkerScope.cs`, `SqlFileParser.cs`, `SqlBlock.cs`
- Modify: `src/SqlSource/Generation/SqlQuery.cs`, `SqlFileReader.cs`, `TypeEmitter.cs`; `src/SqlSource/SqlSourceGenerator.cs`
- Modify: `src/SqlSource/AGENTS.md`
- Test: create `tests/SqlSource.Tests/Settings/QuerySettingsTests.cs`; modify `tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs`, `SqlQueryHashTests.cs`, `SqlModelTests.cs`; `tests/SqlSource.Tests/Generation/SqlFileReaderTests.cs`, `TypeEmitterTests.cs`

**Interfaces:**
- Consumes: `GeneratorParameters`, `SqlGeneratorParameterScope.Parameters`.
- Produces:
  - `SettingsLevel`, a sealed record with `init` members, all nullable; in this task one member, `GeneratorParameters? Parameters`.  `SettingsLevel.None` is the shared empty level; `Over(SettingsLevel other)` gives this level's values where it has them and `other`'s elsewhere, and returns one of the two unchanged when the other is `None`.
  - `QuerySettings`, a `readonly record struct`; in this task `GeneratorParameters Parameters`, with `KeepComments` and `ValidateTokens`.  `QuerySettings.Resolve(SettingsLevel markers, SettingsLevel attribute, SettingsLevel metadata, SettingsLevel property)`.
  - `SqlMarkerScope.Level` (`SettingsLevel`).
  - `SqlFileParser.Parse(string text, string fileName, SqlDialectChoice dialect = default, bool commentsWanted = false)`.
  - `SqlBlock(Name, NameSpan, Summary, Shape, Segments, KeptSegments, Tokens, Parameters, Markers)` and `SqlQuery(Name, NameLocation, Summary, Shape, Segments, KeptSegments, Tokens, Parameters, Markers)`: `KeptSegments` is `EquatableArray<SqlSegment>?`, `Markers` is `SettingsLevel`; `KeepComments` and `TokenValidation` are gone.
  - `SqlFileReader.Read(AdditionalText file, string normalizedPath, SqlDialectChoice dialect, string? invalidDialect, bool commentsWanted, CancellationToken cancellationToken)`.
  - `TypeEmitter.Emit(TypeQueries input, SettingsLevel property)`.

- [ ] **Step 1: Write the failing settings tests**

`tests/SqlSource.Tests/Settings/QuerySettingsTests.cs`:

```csharp
using Shouldly;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Settings;

// Every setting resolves by one rule: the most specific level that has a value gives it.
public class QuerySettingsTests
{
    private static readonly SettingsLevel Keep = new() { Parameters = GeneratorParameters.KeepComments };

    private static readonly SettingsLevel Sort = new() { Parameters = GeneratorParameters.SortInput };

    private static readonly SettingsLevel Empty = new() { Parameters = GeneratorParameters.None };

    [Fact]
    public void Resolve_NoLevelSaysAnything_GivesTheDefaults()
    {
        var settings = QuerySettings.Resolve(SettingsLevel.None, SettingsLevel.None, SettingsLevel.None, SettingsLevel.None);

        settings.Parameters.ShouldBe(GeneratorParameters.None);
        settings.KeepComments.ShouldBeFalse();
        settings.ValidateTokens.ShouldBeTrue();
    }

    [Fact]
    public void Resolve_Parameters_ComeWholeFromTheMostSpecificLevelThatGivesAList()
    {
        QuerySettings.Resolve(SettingsLevel.None, SettingsLevel.None, SettingsLevel.None, Keep).KeepComments.ShouldBeTrue();
        QuerySettings.Resolve(SettingsLevel.None, SettingsLevel.None, Sort, Keep).Parameters.ShouldBe(GeneratorParameters.SortInput);
        QuerySettings.Resolve(SettingsLevel.None, Keep, Sort, Sort).Parameters.ShouldBe(GeneratorParameters.KeepComments);
        QuerySettings.Resolve(Empty, Keep, Keep, Keep).Parameters.ShouldBe(GeneratorParameters.None);
    }

    [Fact]
    public void Resolve_NoTokenValidation_TurnsValidationOff() =>
        QuerySettings
            .Resolve(new SettingsLevel { Parameters = GeneratorParameters.NoTokenValidation }, Keep, Keep, Keep)
            .ValidateTokens.ShouldBeFalse();

    [Fact]
    public void Over_EachMember_ComesFromThisLevelWhenItHasOne()
    {
        Keep.Over(Sort).ShouldBe(Keep);
        SettingsLevel.None.Over(Sort).ShouldBeSameAs(Sort);
        Keep.Over(SettingsLevel.None).ShouldBeSameAs(Keep);
    }

    [Fact]
    public void None_IsEqualToALevelThatSetsNothing() => new SettingsLevel().ShouldBe(SettingsLevel.None);
}
```

- [ ] **Step 2: Add the level and the resolver**

`src/SqlSource/Settings/SettingsLevel.cs`:

```csharp
namespace SqlSource.Settings;

/// <summary>
/// What one source says about the settings: the project's properties, the metadata of a file's item, the attribute
/// of a type, or the markers of a query over those of its file's preamble.
/// </summary>
/// <remarks>
/// Every member is nullable, and null means the source does not say.  A source sets the members it has: not every
/// setting has every source.  <see cref="QuerySettings.Resolve" /> is the one rule that puts the levels together.
/// </remarks>
internal sealed record SettingsLevel
{
    /// <summary>A level that says nothing.  Shared, so that a query or a file without settings allocates none.</summary>
    public static SettingsLevel None { get; } = new();

    /// <summary>The list of generator parameters, whole, or null when the level gives no list.</summary>
    public GeneratorParameters? Parameters { get; init; }

    /// <summary>
    /// This level over <paramref name="other" />: each member from this level when it has one, and from
    /// <paramref name="other" /> when it has not.
    /// </summary>
    public SettingsLevel Over(SettingsLevel other)
    {
        if (ReferenceEquals(other, None))
        {
            return this;
        }

        if (ReferenceEquals(this, None))
        {
            return other;
        }

        return new SettingsLevel { Parameters = Parameters ?? other.Parameters };
    }
}
```

`src/SqlSource/Settings/QuerySettings.cs`:

```csharp
namespace SqlSource.Settings;

/// <summary>
/// The settings in effect for one query, for one type that claims its file.
/// </summary>
/// <param name="Parameters">The list of generator parameters.</param>
internal readonly record struct QuerySettings(GeneratorParameters Parameters)
{
    /// <summary>Whether comments and blank lines stay in the query's SQL.</summary>
    public bool KeepComments => (Parameters & GeneratorParameters.KeepComments) != 0;

    /// <summary>Whether the query's method checks its arguments.</summary>
    public bool ValidateTokens => (Parameters & GeneratorParameters.NoTokenValidation) == 0;

    /// <summary>
    /// Resolves each setting from the most specific level that has it: the markers of the query and its file, then
    /// the attribute, then the metadata of the file's item, then the project's property, then the default.
    /// </summary>
    public static QuerySettings Resolve(
        SettingsLevel markers,
        SettingsLevel attribute,
        SettingsLevel metadata,
        SettingsLevel property
    ) =>
        new(
            markers.Parameters
                ?? attribute.Parameters
                ?? metadata.Parameters
                ?? property.Parameters
                ?? GeneratorParameters.None
        );
}
```

Run: `dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.Settings.QuerySettingsTests`
Expected: PASS.

- [ ] **Step 3: Write the failing parser tests**

`SqlFileParserTests`: add the helper and tests, and move the existing tests to the new members as described after them.

```csharp
private static string? Kept(SqlBlock block) =>
    block.KeptSegments is { } segments
        ? string.Concat(
            segments.Select(static segment =>
                segment.Kind == SqlSegmentKind.Token ? "{{" + segment.Text + "}}" : segment.Text
            )
        )
        : null;

private static SqlBlock Block(string text, bool commentsWanted) =>
    SqlFileParser.Parse(text, "Query.sql", default, commentsWanted).Blocks.ShouldHaveSingleItem();

[Theory]
// The query's own list decides, whatever the file's input says.
[InlineData("-- name: Q\n-- generator: keep-comments\nSELECT 1 -- c\n", false, "SELECT 1 -- c")]
[InlineData("-- name: Q\n-- generator: sort-input\nSELECT 1 -- c\n", true, null)]
[InlineData("-- name: Q\n-- generator: default\nSELECT 1 -- c\n", true, null)]
// The preamble's list, for a query without one.
[InlineData("-- generator: keep-comments\n-- name: Q\nSELECT 1 -- c\n", false, "SELECT 1 -- c")]
[InlineData("-- generator: keep-comments\n-- name: Q\n-- generator: default\nSELECT 1 -- c\n", true, null)]
[InlineData("-- generator: sort-input\n-- name: Q\nSELECT 1 -- c\n", true, null)]
// No list at all: the file's input.
[InlineData("-- name: Q\nSELECT 1 -- c\n", true, "SELECT 1 -- c")]
[InlineData("-- name: Q\nSELECT 1 -- c\n", false, null)]
// Nothing to keep: one form serves.
[InlineData("-- name: Q\nSELECT 1\n", true, null)]
public void Parse_KeptForm_IsBuiltOnlyWhenItMayBeWanted(string text, bool commentsWanted, string? expected)
{
    var block = Block(text, commentsWanted);

    Sql(block).ShouldBe("SELECT 1");
    Kept(block).ShouldBe(expected);
}

[Fact]
public void Parse_TokenThatStandsOnlyInAComment_IsATokenOfTheKeptFormAlone()
{
    var block = Block("-- name: Q\n-- generator: keep-comments\nSELECT 1 /* {{note}} */ FROM {{t:users}}\n", false);

    Sql(block).ShouldBe("SELECT 1   FROM {{t}}");
    Kept(block).ShouldBe("SELECT 1 /* {{note}} */ FROM {{t}}");
    Tokens(block).ShouldBe(["t=users"]);
}

[Theory]
[InlineData("SELECT {{class}} -- c\n", "{{class}}")]
[InlineData("SELECT 1 -- {{class}}\n", "{{class}}")]
public void Parse_ReservedTokenInEitherForm_IsReportedOnce(string sql, string at)
{
    var text = "-- name: Q\n-- generator: keep-comments\n" + sql;

    Errors(text).ShouldBe([SqlParseError.Create(SqlParseErrorKind.ReservedTokenName, SpanOf(text, at), "class")]);
}

[Fact]
public void Parse_QueryWithoutMarkers_SharesTheEmptyLevel() =>
    Blocks("-- name: Q\nSELECT 1\n").ShouldHaveSingleItem().Markers.ShouldBeSameAs(SettingsLevel.None);

[Theory]
[InlineData("-- generator: keep-comments\n-- name: Q\nSELECT 1\n", 1)]
[InlineData("-- generator: keep-comments\n-- name: Q\n-- generator: sort-input\nSELECT 1\n", 4)]
[InlineData("-- generator: keep-comments\n-- name: Q\n-- generator: default\nSELECT 1\n", 0)]
public void Parse_Markers_HoldTheQuerysListOverThePreambles(string text, int expected) =>
    ((int?)Blocks(text).ShouldHaveSingleItem().Markers.Parameters).ShouldBe(expected);
```

Existing tests of this file: one that asserts `block.KeepComments` or `block.TokenValidation` asserts `block.Markers.Parameters`; one that sets `keep-comments` and compares `Sql(block)` with SQL that holds comments compares `Kept(block)`, and `Sql(block)` with the stripped SQL.  Task 7's `Parse_QueryWithItsOwnGeneratorParameters_ReplacesThePreambles` and `Parse_QueryWithoutAnyGeneratorParameters_LeavesValidationToTheProject` are replaced by `Parse_Markers_HoldTheQuerysListOverThePreambles` and `Parse_QueryWithoutMarkers_SharesTheEmptyLevel`.

`SqlQueryHashTests`, a row for the theory `Compute_ChangeThatLeavesTheSqlAlone_GivesTheSameHash`:

```csharp
    [InlineData("-- name: Q\n-- generator: keep-comments\nSELECT 1; -- kept\n")]
```

- [ ] **Step 4: Run them and see them fail**

Run: `dotnet build SqlSource.slnx`
Expected: FAIL, `SqlBlock` has no `KeptSegments`.

- [ ] **Step 5: Carry the level in the scope and build both forms**

`SqlMarkerScope` gains

```csharp
    /// <summary>What the scope's markers say about the settings.</summary>
    public SettingsLevel Level =>
        Generator.Parameters is { } parameters ? new SettingsLevel { Parameters = parameters } : SettingsLevel.None;
```

`SqlFileParser.Parse` takes `bool commentsWanted = false` and hands it to `Parser`; its documentation gains "`commentsWanted` says that a level the parser cannot see, a property, the metadata of the file's item or the attribute of a type that claims the file, may ask for `keep-comments`: the SQL of a query with no list of its own is then built with its comments too."

`ReadBlock`, from the check for an empty block to its end:

```csharp
            if (lastContent < 0)
            {
                AddError(SqlParseErrorKind.EmptyBlock, nameSpan);
                return;
            }

            // The stripped SQL is what the query is: its tokens, its parameters and its hash come from it.
            var sql = SqlTextBuilder.Build(text, lexemes, start, end, keepComments: false);
            var scanned = TokenScanner.Scan(sql.Text, scope.IgnoredTokens);
            var firstScanError = _errors.Count;
            foreach (var error in scanned.Errors)
            {
                _errors.Add(error with { Span = sql.ToSourceSpan(error.Span) });
            }

            var tokens = SqlTokenList.Create(text, sql, scanned.Occurrences, scope.TokenDefaults, _errors);
            var parameters = SqlParameterList.Create(
                text,
                rules,
                sql,
                scanned.Occurrences,
                tokens,
                scope.TokenDefaults,
                scope.Declarations,
                _errors
            );

            // A query that has a list uses it whole, so it alone says whether the comments can be wanted.  A query
            // without one may get the parameter from a level the parser does not see.
            var markers = preamble is null ? scope.Level : scope.Level.Over(preamble.Level);
            var keepsComments = markers.Parameters is { } list
                ? (list & GeneratorParameters.KeepComments) != 0
                : commentsWanted;

            _blocks.Add(
                new SqlBlock(
                    name,
                    nameSpan,
                    summary.Count == 0 ? null : string.Join(" ", summary),
                    shape,
                    scanned.Segments,
                    keepsComments ? BuildKept(start, end, sql, scope.IgnoredTokens, firstScanError) : null,
                    tokens,
                    parameters,
                    markers
                )
            );
        }

        // The SQL with its comments, or null when it is the stripped SQL: one form then serves.  A token can stand
        // in a comment, so this form is scanned too, and an error of its own is one the stripped form did not have
        // at the same place.
        private EquatableArray<SqlSegment>? BuildKept(
            int start,
            int end,
            SqlBlockText stripped,
            ISet<string> ignoredTokens,
            int firstScanError
        )
        {
            var kept = SqlTextBuilder.Build(text, lexemes, start, end, keepComments: true);
            if (string.Equals(kept.Text, stripped.Text, StringComparison.Ordinal))
            {
                return null;
            }

            var scanned = TokenScanner.Scan(kept.Text, ignoredTokens);
            foreach (var error in scanned.Errors)
            {
                var located = error with { Span = kept.ToSourceSpan(error.Span) };
                if (_errors.IndexOf(located, firstScanError) < 0)
                {
                    _errors.Add(located);
                }
            }

            return scanned.Segments;
        }
```

`SqlParseError` is a record whose `Arguments` is an `EquatableArray<string>`, so `IndexOf` compares by value.

`SqlBlock` and `SqlQuery`: remove `KeepComments` and `TokenValidation`; after `Segments` add

```csharp
/// <param name="KeptSegments">
/// The SQL with its comments and blank lines, or null when that form was not built: because nothing can ask for it,
/// or because it is the same text as <paramref name="Segments" />.
/// </param>
    EquatableArray<SqlSegment>? KeptSegments,
```

and as the last member

```csharp
/// <param name="Markers">What the query's markers say about the settings, over those of its file's preamble.</param>
    SettingsLevel Markers
```

Reword `Segments` on both: "The SQL without comments, split into literal text and tokens.  Never empty."

`SqlFileReader`: both `Read` overloads take `bool commentsWanted` before the cancellation token; the first passes `false` for now, the second passes it to `SqlFileParser.Parse`, and the copy to `SqlQuery` follows the new members.

- [ ] **Step 6: Resolve in the emitter**

`TypeEmitter.Emit(TypeQueries input, SettingsLevel property)`, documented "`property` is what the project's properties say."  `Write` takes `SettingsLevel property`, and the body of its loop becomes:

```csharp
            var (query, fileName) = members[index];
            var summaryXml = GetSummaryXml(query, fileName);
            var settings = QuerySettings.Resolve(query.Markers, SettingsLevel.None, SettingsLevel.None, property);

            // The member is built from the form that is emitted.  A form that was not built is the other one.
            var segments = settings.KeepComments ? query.KeptSegments ?? query.Segments : query.Segments;
            if (HasToken(segments))
            {
                MethodWriter.Append(builder, indent, summaryXml, query.Name, segments, settings.ValidateTokens);
                continue;
            }

            // Without a token the SQL is one literal segment.
            var sql = segments[0].Text;
```

`HasToken` takes `EquatableArray<SqlSegment> segments`.

`SqlSourceGenerator`: the last `Combine` of `typeOutputs` becomes

```csharp
            .Combine(
                tokenValidation.Select(static (setting, _) =>
                    setting.Validate
                        ? SettingsLevel.None
                        : new SettingsLevel { Parameters = GeneratorParameters.NoTokenValidation }
                )
            )
```

and `SqlFileReader.Read(input.Left, path, cancellationToken)` becomes `SqlFileReader.Read(input.Left, path, commentsWanted: false, cancellationToken)`.

- [ ] **Step 7: Move the hand-built models in the tests**

`TypeEmitterTests`: `Emit(TargetType type, bool validateTokens, ...)` builds the level as the generator does and passes it; `Query` and `TokenQuery` build `SqlQuery` with `null` for `Shape` and `KeptSegments` and a `Markers` level.  `TokenQuery(string name, bool? tokenValidation = null, ...)` maps `false` to `new SettingsLevel { Parameters = GeneratorParameters.NoTokenValidation }`, `true` to `new SettingsLevel { Parameters = GeneratorParameters.None }` and `null` to `SettingsLevel.None`.  Add:

```csharp
[Fact]
public void Emit_QueryThatKeepsItsComments_EmitsTheKeptForm()
{
    var query = new SqlQuery(
        "GetUser",
        NameLocation("Users.sql"),
        null,
        null,
        TestModels.Array(new SqlSegment(SqlSegmentKind.Literal, "SELECT 1;")),
        TestModels.Array(new SqlSegment(SqlSegmentKind.Literal, "SELECT 1; -- kept")),
        EquatableArray<SqlToken>.Empty,
        EquatableArray<SqlQueryParameter>.Empty,
        new SettingsLevel { Parameters = GeneratorParameters.KeepComments }
    );

    Emit(TestModels.Type(), File("Users.sql", query)).Source.ShouldNotBeNull().ShouldContain("\"SELECT 1; -- kept\"");
}

[Fact]
public void Emit_QueryThatKeepsCommentsButHasNoKeptForm_EmitsTheStrippedOne()
{
    var query = new SqlQuery(
        "GetUser",
        NameLocation("Users.sql"),
        null,
        null,
        TestModels.Array(new SqlSegment(SqlSegmentKind.Literal, "SELECT 1;")),
        null,
        EquatableArray<SqlToken>.Empty,
        EquatableArray<SqlQueryParameter>.Empty,
        new SettingsLevel { Parameters = GeneratorParameters.KeepComments }
    );

    Emit(TestModels.Type(), File("Users.sql", query)).Source.ShouldNotBeNull().ShouldContain("\"SELECT 1;\"");
}
```

`SqlFileReaderTests` and `SqlModelTests` follow the new members.

- [ ] **Step 8: Run everything, and measure**

Run: `dotnet test --solution SqlSource.slnx`
Expected: PASS.  Measure the parse as in Task 1 Step 11: the test's file has no generator parameter and is parsed with `commentsWanted` false, so the kept form is never built.

- [ ] **Step 9: Update `src/SqlSource/AGENTS.md`**

Under `Parsing/`: "**A block has two forms of its SQL.**  `Segments` is always the SQL without comments, and the tokens, the parameters and the hash come from it.  `KeptSegments` is the SQL with comments, built only when the query's own list has `keep-comments`, or the query has no list and `commentsWanted` is set; it is null when that text would be the same.  The emitter picks one after the settings are resolved.  Never build the kept form for every query: it doubles what a parse allocates."

Under `Settings/`: "**`SettingsLevel` is one source's say, and `QuerySettings.Resolve` is the one rule.**  A new setting is a nullable member of the level, a line in `Over`, a member of `QuerySettings` with its default, and a line in each reader that has it.  `SettingsLevel.None` is shared: a reader that finds nothing returns it, and never a new empty level."

- [ ] **Step 10: Commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add -A && git commit -m "Carry a query's settings as a level, and build its SQL with comments only when wanted

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Generator parameters at every level, and the two stages of the pipeline

**Files:**
- Create: `src/SqlSource/Generation/MSBuildSettings.cs`, `InvalidSetting.cs`, `ProjectSettings.cs`, `FileSettings.cs`, `FileMetadata.cs`, `FileParseInput.cs`
- Delete: `src/SqlSource/Generation/TokenValidationSetting.cs`, `FileDialect.cs`; `tests/SqlSource.Tests/Generation/TokenValidationSettingTests.cs`
- Modify: `src/SqlSource/Settings/SettingsLevel.cs`; `src/SqlSource/Generation/AttributeSource.cs`, `TargetType.cs`, `TargetTypeReader.cs`, `TypeQueries.cs`, `TypeEmitter.cs`, `SqlFileReader.cs`, `DialectSetting.cs`, `TrackingNames.cs`; `src/SqlSource/SqlSourceGenerator.cs`
- Modify: `src/SqlSource/Diagnostics/SqlDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`
- Modify: `src/SqlSource/build/SqlSource.props`, `src/SqlSource/build/SqlSource.targets`
- Modify: `tests/SqlSource.Tests/SqlSource.Tests.csproj`; create `tests/SqlSource.Tests/EndToEnd/Parameters/Kept.sql`, `Parameters/Shared.sql`, `EndToEnd/ParameterQueries.cs`
- Modify: `tools/package-install/Directory.Build.targets`, `Consumer.csproj`, `Queries.cs`, `Program.cs`, `expected-output.txt`, `Queries/Users.sql`; create `tools/package-install/Queries/Kept.sql`
- Modify: `README.md`, `docs/diagnostics.md`, `src/SqlSource/AGENTS.md`, `docs/tech-debt/TD-0016-dialect-of-a-file-added-by-a-late-target-is-lost.md`
- Test: create `tests/SqlSource.Tests/Generator/GeneratorParameterLevelTests.cs`, `tests/SqlSource.Tests/Generation/MSBuildSettingsTests.cs`; modify `Generator/GeneratorHarness.cs`, `TestOptionsProvider.cs`, `TokenValidationTests.cs`, `CachingTests.cs`, `TypeDiagnosticsTests.cs`, `DialectTests.cs`; `Generation/DialectSettingTests.cs`, `SqlFileReaderTests.cs`, `TypeEmitterTests.cs`, `TestModels.cs`; `Package/BuildFileTests.cs`; `EndToEnd/EndToEndTests.cs`; `Diagnostics/SqlDiagnosticsTests.cs`

**Interfaces:**
- Consumes: `SettingsLevel`, `QuerySettings.Resolve`, `GeneratorParameterList.Parse`, `SqlFileParser.Parse(..., commentsWanted)`.
- Produces:
  - `SettingsLevel.KeepsComments` (`bool`).
  - `InvalidSetting(string Name, string Value)`: the MSBuild name of a setting and a value of it that is not valid.
  - `MSBuildSettings.Property` and `MSBuildSettings.Metadata` (`SettingKeys`), and `MSBuildSettings.Read(AnalyzerConfigOptions options, SettingKeys keys, ref List<InvalidSetting>? invalid)` returning a `SettingsLevel`.
  - `ProjectSettings(SettingsLevel Level, EquatableArray<InvalidSetting> Invalid)` with `Read(AnalyzerConfigOptions globalOptions)`.
  - `FileSettings(string NormalizedPath, SettingsLevel Level, EquatableArray<InvalidSetting> Invalid)` with `Read(string path, AnalyzerConfigOptions fileOptions)` returning null for a file that sets nothing.
  - `FileMetadata(AdditionalText File, DialectSetting Dialect, FileSettings? Settings)` with `Read(AdditionalText file, AnalyzerConfigOptions fileOptions)`.
  - `FileParseInput(AdditionalText File, string? NormalizedPath, SqlDialectChoice Dialect, string? InvalidDialect, bool CommentsWanted)` with `Resolve(FileMetadata metadata, DialectSetting projectDialect, bool projectKeepsComments, EquatableArray<string> commentPaths)`.
  - `SqlFileReader.Read(FileParseInput file, CancellationToken cancellationToken)`.
  - `TargetType(Namespace, Types, Placement, Settings, Path, FilePath, AttributeLocation, Diagnostics)`: `Settings` is the attribute's `SettingsLevel`.
  - `TypeQueries(Type, Files, FileSettings, HintName)`: `FileSettings` is an `EquatableArray<SettingsLevel>` with one level for each of `Files`.
  - `AttributeSource.ParametersProperty` (`"Parameters"`).
  - `SqlDiagnostics.InvalidAttributeValue` (`SQLSRC006`, arguments: value, property) in place of `InvalidSqlLocation`; `SqlDiagnostics.InvalidSettingValue` (`SQLSRC014`, arguments: value, MSBuild name); `InvalidTokenValidation` (`SQLSRC010`) removed.
  - Item type `SqlSourceSettingsFile`; targets `SqlSourceTrimMetadataOfFiles` and `SqlSourceCollectSettingsFiles`.
  - Test harness: `GeneratorHarness.Run(sources, sqlFiles, supportedFramework, languageVersion, references, generatorParameters, dialect, properties)`; `SqlFile(string Path, string? Text, string? Dialect = null, IReadOnlyDictionary<string, string>? Metadata = null)`; `TestOptionsProvider(string? generatorParameters, string? dialect = null, IReadOnlyDictionary<string, string>? fileDialects = null, IReadOnlyDictionary<string, string?>? properties = null, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? fileMetadata = null)`.

- [ ] **Step 1: Move the test harness to the new names**

`TestOptionsProvider`: the keys stay spelled out in the file.

```csharp
// What MSBuild tells a generator, as the compiler hands it over: the project's properties, and the metadata of each
// .sql file by the file's path.  A null value is a project that does not set the property, and a path that is not
// listed is a file without metadata.  A property or a metadata is named as MSBuild names it: SqlSourceOutput.
internal sealed class TestOptionsProvider(
    string? generatorParameters,
    string? dialect = null,
    IReadOnlyDictionary<string, string>? fileDialects = null,
    IReadOnlyDictionary<string, string?>? properties = null,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? fileMetadata = null
) : AnalyzerConfigOptionsProvider
{
    private const string PropertyPrefix = "build_property.";

    private const string MetadataPrefix = "build_metadata.SqlSourceSettingsFile.";

    private static readonly Options None = new([]);

    public override AnalyzerConfigOptions GlobalOptions { get; } = CreateGlobal(generatorParameters, dialect, properties);

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => None;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
    {
        var values = new Dictionary<string, string?>();
        if (fileDialects is not null && fileDialects.TryGetValue(textFile.Path, out var fileDialect))
        {
            values[MetadataPrefix + "SqlSourceDialect"] = fileDialect;
        }

        if (fileMetadata is not null && fileMetadata.TryGetValue(textFile.Path, out var metadata))
        {
            foreach (var pair in metadata)
            {
                values[MetadataPrefix + pair.Key] = pair.Value;
            }
        }

        return values.Count == 0 ? None : new Options(values);
    }

    private static Options CreateGlobal(
        string? generatorParameters,
        string? dialect,
        IReadOnlyDictionary<string, string?>? properties
    )
    {
        var values = new Dictionary<string, string?>
        {
            [PropertyPrefix + "SqlSourceGeneratorParameters"] = generatorParameters,
            [PropertyPrefix + "SqlSourceDialect"] = dialect,
        };
        foreach (var pair in properties ?? new Dictionary<string, string?>())
        {
            values[PropertyPrefix + pair.Key] = pair.Value;
        }

        return new Options(values);
    }

    private sealed class Options(Dictionary<string, string?> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
            values.TryGetValue(key, out value) && value is not null;
    }
}
```

`GeneratorHarness`: `SqlFile` becomes

```csharp
// Dialect is the SqlSourceDialect metadata of the file's AdditionalFiles item, and Metadata its other metadata by
// MSBuild name.  Null is a file without it.
internal sealed record SqlFile(
    string Path,
    string? Text,
    string? Dialect = null,
    IReadOnlyDictionary<string, string>? Metadata = null
)
```

`Run`'s parameter `tokenValidation` becomes `generatorParameters`, with a new last parameter `IReadOnlyDictionary<string, string?>? properties = null`, and the options are built as

```csharp
        var options = new TestOptionsProvider(
            generatorParameters,
            dialect,
            sqlFiles.Where(file => file.Dialect is not null).ToDictionary(file => file.Path, file => file.Dialect!),
            properties,
            sqlFiles.Where(file => file.Metadata is not null).ToDictionary(file => file.Path, file => file.Metadata!)
        );
```

Every caller that passed `tokenValidation: "false"` passes `generatorParameters: "no-token-validation"`; one that passed `"true"` or `""` passes `null`.  `new TestOptionsProvider("false")` in `CachingTests` becomes `new TestOptionsProvider("no-token-validation")`.

- [ ] **Step 2: Write the failing generator tests**

`tests/SqlSource.Tests/Generator/GeneratorParameterLevelTests.cs`:

```csharp
using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.Generator;

// The list of generator parameters in effect for a query, for a type that claims its file, is the one from the most
// specific level that gives a list, whole: the query's markers, the preamble's, the attribute, the metadata of the
// file's item, the project's property.  Two parameters have an effect today, and each row shows both.
public class GeneratorParameterLevelTests
{
    private const string On = "keep-comments no-token-validation";

    private const string Other = "sort-input";

    private const string Default = "default";

    private const string Check = "global::System.ArgumentException.ThrowIfNullOrWhiteSpace(table);";

    [Theory]
    // property, metadata, attribute, preamble, query
    [InlineData(null, null, null, null, null, false)]
    [InlineData(On, null, null, null, null, true)]
    [InlineData(On, Default, null, null, null, false)]
    [InlineData(On, Other, null, null, null, false)]
    [InlineData(null, On, null, null, null, true)]
    [InlineData(Other, On, Default, null, null, false)]
    [InlineData(null, null, On, null, null, true)]
    [InlineData(Default, Other, On, null, null, true)]
    [InlineData(On, On, On, Default, null, false)]
    [InlineData(null, null, null, On, null, true)]
    [InlineData(On, On, On, On, Default, false)]
    [InlineData(On, On, On, On, Other, false)]
    [InlineData(null, null, null, Default, On, true)]
    public void Run_GeneratorParameters_ComeWholeFromTheMostSpecificLevelThatGivesAList(
        string? property,
        string? metadata,
        string? attribute,
        string? preamble,
        string? query,
        bool expected
    )
    {
        var sql =
            (preamble is null ? string.Empty : "-- generator: " + preamble + "\n")
            + "-- name: ListFrom\n"
            + (query is null ? string.Empty : "-- generator: " + query + "\n")
            + "SELECT * FROM {{table}}; -- kept\n";
        var file = new SqlFile(
            "/app/Repo/Users.sql",
            sql,
            Metadata: metadata is null
                ? null
                : new Dictionary<string, string> { ["SqlSourceGeneratorParameters"] = metadata }
        );

        var run = GeneratorHarness.Run(
            [new SourceFile(GeneratorHarness.SourcePath, Source(attribute))],
            [file],
            generatorParameters: property
        );

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        var generated = run.Sources["App.Sample.g.cs"];
        if (expected)
        {
            generated.ShouldContain("-- kept");
            generated.ShouldNotContain("Throw");
        }
        else
        {
            generated.ShouldNotContain("-- kept");
            generated.ShouldContain(Check);
        }
    }

    // One type asks for comments and the other does not.  The token stands only in a comment, so one gets a method
    // and the other a constant, each from its own form of the same file.
    [Fact]
    public void Run_TwoTypesClaimOneFileAndOneKeepsComments_EachGetsItsOwnSql()
    {
        const string Source = """
            using SqlSource;

            namespace App;

            [SqlSourceGenerate(SqlLocation = SqlLocation.Direct)]
            public partial class Plain
            {
                public const string Sql = Q;
            }

            [SqlSourceGenerate(SqlLocation = SqlLocation.Direct, Parameters = "keep-comments")]
            public partial class Kept
            {
                public static string Sql() => Q("x");
            }
            """;

        var run = GeneratorHarness.Run(
            Source,
            new SqlFile("/app/Repo/Q.sql", "-- name: Q\nSELECT 1 /* {{note}} */ FROM t;\n")
        );

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources["App.Plain.g.cs"].ShouldContain("public const string Q = \"SELECT 1   FROM t;\";");
        run.Sources["App.Kept.g.cs"].ShouldContain("public static string Q(string note)");
    }

    [Fact]
    public void Run_PropertyWithAWordThatIsNoParameter_IsAnErrorWithoutAPositionAndTheOthersApply()
    {
        var run = Run("keep-comments nope token-validation", null);

        run.Diagnostics.ShouldBe([
            "SQLSRC014 (1,1)-(1,1): 'nope' is not a valid value of SqlSourceGeneratorParameters",
            "SQLSRC014 (1,1)-(1,1): 'token-validation' is not a valid value of SqlSourceGeneratorParameters",
        ]);
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources["App.Sample.g.cs"].ShouldContain("-- kept");
    }

    [Fact]
    public void Run_TheSameInvalidWordInThePropertyAndInTwoFiles_IsReportedOnce()
    {
        var metadata = new Dictionary<string, string> { ["SqlSourceGeneratorParameters"] = "nope" };

        var run = GeneratorHarness.Run(
            [new SourceFile(GeneratorHarness.SourcePath, Source(null))],
            [
                new SqlFile("/app/Repo/A.sql", "SELECT 1;\n", Metadata: metadata),
                new SqlFile("/app/Repo/B.sql", "SELECT 2;\n", Metadata: metadata),
            ],
            generatorParameters: "nope"
        );

        run.Diagnostics.ShouldBe([
            "SQLSRC014 (1,1)-(1,1): 'nope' is not a valid value of SqlSourceGeneratorParameters",
        ]);
    }

    // A file that no type claims is never read, and neither is what MSBuild says about it.
    [Fact]
    public void Run_InvalidMetadataOfAFileThatNoTypeClaims_IsNotReported()
    {
        var run = GeneratorHarness.Run(
            [new SourceFile(GeneratorHarness.SourcePath, Source(null))],
            [
                new SqlFile("/app/Repo/A.sql", "SELECT 1;\n"),
                new SqlFile(
                    "/app/Migrations/001.sql",
                    "SELECT 2;\n",
                    Metadata: new Dictionary<string, string> { ["SqlSourceGeneratorParameters"] = "nope" }
                ),
            ]
        );

        run.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Run_DefaultBesideAnotherWordInTheProperty_IsReportedWholeAndGivesNoList()
    {
        var run = Run("default keep-comments", null);

        run.Diagnostics.ShouldBe([
            "SQLSRC014 (1,1)-(1,1): 'default keep-comments' is not a valid value of SqlSourceGeneratorParameters",
        ]);
        run.Sources["App.Sample.g.cs"].ShouldNotContain("-- kept");
    }

    [Fact]
    public void Run_AttributeWithAWordThatIsNoParameter_IsAnErrorAtTheAttribute()
    {
        var run = Run(null, "keep-comments nope");

        run.Diagnostics.ShouldHaveSingleItem()
            .ShouldBe(
                "SQLSRC006 /app/Repo/Sample.cs(5,2)-(5,87): 'nope' is not a valid value of Parameters"
            );
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Run_AttributeParametersWithoutText_IsNotSet(string value)
    {
        var run = Run("keep-comments", value);

        run.Diagnostics.ShouldBeEmpty();
        run.Sources["App.Sample.g.cs"].ShouldContain("-- kept");
    }

    private static string Source(string? attribute) =>
        "using SqlSource;\n\nnamespace App;\n\n[SqlSourceGenerate(SqlLocation = SqlLocation.Direct"
        + (attribute is null ? string.Empty : ", Parameters = \"" + attribute + "\"")
        + ")]\npublic partial class Sample;\n";

    private static GeneratorRun Run(string? property, string? attribute) =>
        GeneratorHarness.Run(
            [new SourceFile(GeneratorHarness.SourcePath, Source(attribute))],
            [new SqlFile("/app/Repo/Users.sql", "-- name: ListFrom\nSELECT * FROM {{table}}; -- kept\n")],
            generatorParameters: property
        );
}
```

The column of the attribute's end in `Run_AttributeWithAWordThatIsNoParameter_IsAnErrorAtTheAttribute` is counted from the source: take the span the test reports on its first run and check it against the text before fixing the expectation.

`TokenValidationTests`: the theory's third argument is now the list of `SqlSourceGeneratorParameters`; its rows become

```csharp
    [InlineData(null, null, null, true)]
    [InlineData(null, null, "no-token-validation", false)]
    [InlineData(null, null, " No-Token-Validation ", false)]
    [InlineData(null, null, "default", true)]
    [InlineData(null, null, "", true)]
    [InlineData(null, "no-token-validation", null, false)]
    [InlineData(null, "default", "no-token-validation", true)]
    [InlineData(null, "keep-comments", "no-token-validation", true)]
    [InlineData("no-token-validation", null, null, false)]
    [InlineData("default", null, "no-token-validation", true)]
    [InlineData("no-token-validation", "default", null, false)]
    [InlineData("default", "no-token-validation", "no-token-validation", true)]
```

`Run_PropertyOff_ChangesOnlyTheQueriesWithoutAGeneratorParameter` uses `-- generator: default` and `generatorParameters: "no-token-validation"`.  Delete `Run_PropertyThatIsNotTrueOrFalse_...`, which `GeneratorParameterLevelTests` replaces, and change `Run_InvalidPropertyInAProjectWithoutAnAttributedType_IsStillAnError` to expect `SQLSRC014`.  In `DialectTests`, the test at line 264 that sets both properties to a bad value expects `SQLSRC014 (1,1)-(1,1): '` at the start of the first diagnostic.

`TypeDiagnosticsTests`: the two expectations of `SQLSRC006` end `is not a valid value of SqlLocation`.

`CachingTests`: in the list of steps of `Run_MethodBodyEdited_TakesEveryStepFromThePreviousRun`, `TrackingNames.FileDialect` becomes `TrackingNames.FileParseInput`, `TrackingNames.TokenValidation` becomes `TrackingNames.ProjectSettings`, and `TrackingNames.FileSettings`, `TrackingNames.FilesSettings` and `TrackingNames.CommentPaths` join it.  A step with no output, `FileSettings` here, is absent from `TrackedSteps`: read it with `TryGetValue` in `AllReasons`, returning an empty array.  `Reasons<FileDialect>` becomes `Reasons<FileParseInput>`.  `Run_TokenValidationPropertyChanged_...` is renamed `Run_GeneratorParametersPropertyChanged_EmitsEveryTypeAgainAndParsesNoFile` and reads `TrackingNames.ProjectSettings`.  Add:

```csharp
// The files have no comment to keep, so asking for comments changes no parsed file: the files are read again, give
// equal values, and nothing is emitted again.
[Fact]
public void Run_KeepCommentsTurnedOnByTheProperty_ParsesTheFilesAgainAndOnlyThose()
{
    var compilation = GeneratorHarness.CreateCompilation(Sources);
    var users = new InMemoryAdditionalText(_users.Path, "-- name: GetUser\nSELECT 1; -- c\n");
    var driver = FirstRun(compilation, users);

    var result = Run(driver.WithUpdatedAnalyzerConfigOptions(new TestOptionsProvider("keep-comments")), compilation);

    AllReasons(result, TrackingNames.FileParseInput).ShouldAllBe(reason => reason == IncrementalStepRunReason.Modified);
    Reasons<ParsedSqlFile>(result, TrackingNames.ParsedFile, file => file.FileName)
        .ShouldBe(
            new Dictionary<string, IncrementalStepRunReason>
            {
                ["Users.sql"] = IncrementalStepRunReason.Modified,
                ["Orders.sql"] = IncrementalStepRunReason.Unchanged,
            },
            ignoreOrder: true
        );
    result
        .GeneratedTrees.Single(tree => tree.FilePath.EndsWith("UserQueries.g.cs", StringComparison.Ordinal))
        .ToString()
        .ShouldContain("SELECT 1; -- c");
}

[Fact]
public void Run_PropertyGainsAParameterThatIsNotKeepComments_ParsesNoFile()
{
    var compilation = GeneratorHarness.CreateCompilation(Sources);
    var driver = FirstRun(compilation);

    var result = Run(driver.WithUpdatedAnalyzerConfigOptions(new TestOptionsProvider("sort-input")), compilation);

    AllReasons(result, TrackingNames.ProjectSettings).ShouldBe([IncrementalStepRunReason.Modified]);
    AllReasons(result, TrackingNames.FileParseInput).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    AllReasons(result, TrackingNames.ParsedFile).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    AllReasons(result, TrackingNames.TypeOutput).ShouldAllBe(reason => reason == IncrementalStepRunReason.Unchanged);
}

[Fact]
public void Run_MetadataOfOneFileGainsAParameter_ParsesNoFileAndEmitsOnlyItsType()
{
    var compilation = GeneratorHarness.CreateCompilation(Sources);
    var driver = FirstRun(compilation);
    var metadata = new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
        [_users.Path] = new Dictionary<string, string> { ["SqlSourceGeneratorParameters"] = "sort-input" },
    };

    var result = Run(
        driver.WithUpdatedAnalyzerConfigOptions(new TestOptionsProvider(null, fileMetadata: metadata)),
        compilation
    );

    AllReasons(result, TrackingNames.ParsedFile).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    Reasons<TypeOutput>(result, TrackingNames.TypeOutput, output => output.HintName)
        .ShouldBe(
            new Dictionary<string, IncrementalStepRunReason>
            {
                ["App.Users.UserQueries.g.cs"] = IncrementalStepRunReason.Unchanged,
                ["App.Orders.OrderQueries.g.cs"] = IncrementalStepRunReason.Cached,
            },
            ignoreOrder: true
        );
}

[Fact]
public void Run_AttributeGainsKeepComments_ParsesOnlyTheFilesOfThatType()
{
    var compilation = GeneratorHarness.CreateCompilation(Sources);
    var driver = FirstRun(compilation);

    var tree = compilation.SyntaxTrees.Single(tree => tree.FilePath == UsersSourcePath);
    var edited = compilation.ReplaceSyntaxTree(
        tree,
        CSharpSyntaxTree.ParseText(
            UsersSource.Replace(
                "[SqlSourceGenerate]",
                "[SqlSourceGenerate(Parameters = \"keep-comments\")]",
                StringComparison.Ordinal
            ),
            GeneratorHarness.ParseOptions,
            UsersSourcePath,
            cancellationToken: TestContext.Current.CancellationToken
        )
    );
    var result = Run(driver, edited);

    AllReasons(result, TrackingNames.CommentPaths).ShouldBe([IncrementalStepRunReason.Modified]);
    Reasons<FileParseInput>(result, TrackingNames.FileParseInput, file => file.File.Path)
        .ShouldBe(
            new Dictionary<string, IncrementalStepRunReason>
            {
                [_users.Path] = IncrementalStepRunReason.Modified,
                [_orders.Path] = IncrementalStepRunReason.Unchanged,
            },
            ignoreOrder: true
        );
    Reasons<ParsedSqlFile>(result, TrackingNames.ParsedFile, file => file.FileName)["Orders.sql"]
        .ShouldBe(IncrementalStepRunReason.Cached);
}
```

The exact reason a step reports, `Unchanged` against `Cached`, follows from which of its inputs changed: where a first run shows the other of the two for a step that produced an equal value, correct the expectation and keep the assertion that no file was parsed.

`tests/SqlSource.Tests/Generation/MSBuildSettingsTests.cs`:

```csharp
using System.Collections.Generic;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Settings;
using SqlSource.Tests.Generator;
using Xunit;

namespace SqlSource.Tests.Generation;

public class MSBuildSettingsTests
{
    private const string Path = "/app/Repo/Users.sql";

    [Fact]
    public void ReadProject_NothingSet_IsTheSharedEmptyLevel()
    {
        var settings = ProjectSettings.Read(new TestOptionsProvider(null).GlobalOptions);

        settings.Level.ShouldBeSameAs(SettingsLevel.None);
        settings.Invalid.ShouldBeEmpty();
    }

    [Fact]
    public void ReadProject_GeneratorParameters_AreReadWithTheirInvalidWords()
    {
        var settings = ProjectSettings.Read(new TestOptionsProvider(" keep-comments nope ").GlobalOptions);

        settings.Level.Parameters.ShouldBe(GeneratorParameters.KeepComments);
        settings.Invalid.ShouldBe([new InvalidSetting("SqlSourceGeneratorParameters", "nope")]);
    }

    [Fact]
    public void ReadFile_NothingSet_IsNull() =>
        FileSettings.Read(Path, new TestOptionsProvider(null).GetOptions(new InMemoryAdditionalText(Path, ""))).ShouldBeNull();

    [Fact]
    public void ReadFile_Metadata_IsReadForThatFile()
    {
        var options = new TestOptionsProvider(
            null,
            fileMetadata: new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                [Path] = new Dictionary<string, string> { ["SqlSourceGeneratorParameters"] = "default" },
            }
        );

        var settings = FileSettings.Read("app/Repo/Users.sql", options.GetOptions(new InMemoryAdditionalText(Path, "")));

        settings.ShouldNotBeNull().Level.Parameters.ShouldBe(GeneratorParameters.None);
        settings.NormalizedPath.ShouldBe("app/Repo/Users.sql");
    }

    [Fact]
    public void ProjectSettings_ReadTwice_AreEqual() =>
        ProjectSettings
            .Read(new TestOptionsProvider("keep-comments nope").GlobalOptions)
            .ShouldBe(ProjectSettings.Read(new TestOptionsProvider("keep-comments nope").GlobalOptions));
}
```

`DialectSettingTests`: the tests of `FileDialect.Resolve` become tests of `FileParseInput.Resolve`, through a helper

```csharp
private static FileParseInput Resolve(
    InMemoryAdditionalText file,
    string? metadata,
    string? project,
    string? fileParameters = null,
    bool projectKeepsComments = false,
    params string[] commentPaths
) =>
    FileParseInput.Resolve(
        new FileMetadata(
            file,
            DialectSetting.Parse(metadata),
            fileParameters is null
                ? null
                : new FileSettings(
                    "app/Repo/Users.sql",
                    new SettingsLevel { Parameters = GeneratorParameterList.Parse(fileParameters, []) },
                    EquatableArray<InvalidSetting>.Empty
                )
        ),
        DialectSetting.Parse(project),
        projectKeepsComments,
        TestModels.Array(commentPaths)
    );
```

and one new theory, with the file at `/app/Repo/Users.sql`:

```csharp
[Theory]
// The metadata's list decides when it gives one; else the property's.
[InlineData(null, false, false, false)]
[InlineData(null, true, false, true)]
[InlineData("keep-comments", false, false, true)]
[InlineData("sort-input", true, false, false)]
[InlineData("default", true, false, false)]
// A type that claims the file asks for comments: wanted, whatever MSBuild says.
[InlineData("default", false, true, true)]
[InlineData(null, false, true, true)]
public void Resolve_CommentsWanted_IsWhatAnyLevelOutsideTheFileCanAskFor(
    string? fileParameters,
    bool projectKeepsComments,
    bool claimedByATypeThatKeepsComments,
    bool expected
) =>
    Resolve(
            new InMemoryAdditionalText("/app/Repo/Users.sql", ""),
            null,
            null,
            fileParameters,
            projectKeepsComments,
            claimedByATypeThatKeepsComments ? ["app/Repo/Users.sql"] : []
        )
        .CommentsWanted.ShouldBe(expected);
```

`SqlFileReaderTests`: `new FileDialect(text, SqlDialect.MySql, "nope")` becomes `new FileParseInput(text, "app/Repo/Users.sql", SqlDialect.MySql, "nope", false)`, and the calls follow `SqlFileReader.Read(FileParseInput, CancellationToken)`.

- [ ] **Step 3: Run them and see them fail**

Run: `dotnet build SqlSource.slnx`
Expected: FAIL, no `FileParseInput`.

- [ ] **Step 4: Change the diagnostics**

`SqlDiagnostics`: `InvalidSqlLocation` becomes

```csharp
    public static readonly DiagnosticDescriptor InvalidAttributeValue = new(
        id: "SQLSRC006",
        title: "Attribute value is not valid",
        messageFormat: "'{0}' is not a valid value of {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc006",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );
```

Delete `InvalidTokenValidation`, and add after `PathDiffersOnlyByCase`:

```csharp
    public static readonly DiagnosticDescriptor InvalidSettingValue = new(
        id: "SQLSRC014",
        title: "MSBuild setting is not valid",
        messageFormat: "'{0}' is not a valid value of {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc014",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );
```

`All` follows.  `AnalyzerReleases.Unshipped.md`: the row of `SQLSRC006` reads `Attribute value is not valid`, the row of `SQLSRC010` is deleted, and `SQLSRC014 | SqlSource | Error | MSBuild setting is not valid` is added.  Nothing has shipped, so no `### Removed Rules` entry is needed; `AnalyzerReleases.Shipped.md` is empty.

- [ ] **Step 5: Read the levels from MSBuild**

`SettingsLevel` gains

```csharp
    /// <summary>Whether the level gives a list that has <c>keep-comments</c>.</summary>
    public bool KeepsComments => Parameters is { } list && (list & GeneratorParameters.KeepComments) != 0;
```

`src/SqlSource/Generation/InvalidSetting.cs`:

```csharp
namespace SqlSource.Generation;

/// <summary>
/// A value of an MSBuild property, or of the metadata of an item, that is not valid.  It has no position: the
/// compiler does not say where either was set.
/// </summary>
/// <param name="Name">The name MSBuild knows the setting by: <c>SqlSourceGeneratorParameters</c>.</param>
/// <param name="Value">The value, or the part of it that is not valid, as written.</param>
internal sealed record InvalidSetting(string Name, string Value);
```

`src/SqlSource/Generation/MSBuildSettings.cs`:

```csharp
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Diagnostics;
using SqlSource.Settings;

namespace SqlSource.Generation;

/// <summary>
/// Reads what MSBuild says about the settings: the project's properties, and the metadata of one file's
/// <c>AdditionalFiles</c> item.  Both reach the generator only because <c>build/SqlSource.props</c> lists them.
/// </summary>
/// <remarks>
/// The dialect is not read here.  It is an input of the parse and is resolved before it, by
/// <see cref="DialectSetting" />; everything here joins after a type's queries are selected.
/// </remarks>
internal static class MSBuildSettings
{
    public const string GeneratorParametersName = "SqlSourceGeneratorParameters";

    /// <summary>Where the compiler puts the properties of the project.</summary>
    public static SettingKeys Property { get; } = new("build_property.");

    /// <summary>
    /// Where the compiler puts the metadata of an item of <c>SqlSourceSettingsFile</c>, the item type that
    /// <c>build/SqlSource.targets</c> fills with the <c>AdditionalFiles</c> items that have any.
    /// </summary>
    public static SettingKeys Metadata { get; } = new("build_metadata.SqlSourceSettingsFile.");

    /// <summary>
    /// Reads one level.  A value that is not valid is added to <paramref name="invalid" /> and is not set.  The
    /// shared empty level is returned when nothing is set.
    /// </summary>
    public static SettingsLevel Read(AnalyzerConfigOptions options, SettingKeys keys, ref List<InvalidSetting>? invalid)
    {
        var level = SettingsLevel.None;
        if (options.TryGetValue(keys.GeneratorParameters, out var list) && !string.IsNullOrWhiteSpace(list))
        {
            var words = new List<string>();
            if (GeneratorParameterList.Parse(list, words) is { } parameters)
            {
                level = level with { Parameters = parameters };
            }

            foreach (var word in words)
            {
                (invalid ??= []).Add(new InvalidSetting(GeneratorParametersName, word));
            }
        }

        return level;
    }

    /// <summary>The keys of the settings under one prefix, built once.</summary>
    internal sealed class SettingKeys(string prefix)
    {
        public string GeneratorParameters { get; } = prefix + GeneratorParametersName;
    }
}
```

`src/SqlSource/Generation/ProjectSettings.cs`:

```csharp
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;
using SqlSource.Settings;

namespace SqlSource.Generation;

/// <summary>
/// What the project's MSBuild properties say about the settings.
/// </summary>
/// <param name="Level">The properties that are set and valid.</param>
/// <param name="Invalid">The values that are not valid.</param>
internal sealed record ProjectSettings(SettingsLevel Level, EquatableArray<InvalidSetting> Invalid)
{
    public static ProjectSettings Read(AnalyzerConfigOptions globalOptions)
    {
        List<InvalidSetting>? invalid = null;
        var level = MSBuildSettings.Read(globalOptions, MSBuildSettings.Property, ref invalid);
        return new ProjectSettings(
            level,
            invalid is null
                ? EquatableArray<InvalidSetting>.Empty
                : new EquatableArray<InvalidSetting>(invalid.ToImmutableArray())
        );
    }
}
```

`src/SqlSource/Generation/FileSettings.cs`:

```csharp
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;
using SqlSource.Settings;

namespace SqlSource.Generation;

/// <summary>
/// What the metadata of one file's <c>AdditionalFiles</c> item says about the settings.  A file's own markers come
/// before it, and so does the attribute of a type that claims the file.
/// </summary>
/// <param name="NormalizedPath">The file's path in the form <see cref="SqlPath.Normalize" /> gives.</param>
/// <param name="Level">The metadata that is set and valid.</param>
/// <param name="Invalid">The values that are not valid.</param>
internal sealed record FileSettings(string NormalizedPath, SettingsLevel Level, EquatableArray<InvalidSetting> Invalid)
{
    /// <summary>Reads a file's metadata.  Null for a file that sets nothing, which is almost every file.</summary>
    public static FileSettings? Read(string normalizedPath, AnalyzerConfigOptions fileOptions)
    {
        List<InvalidSetting>? invalid = null;
        var level = MSBuildSettings.Read(fileOptions, MSBuildSettings.Metadata, ref invalid);
        if (ReferenceEquals(level, SettingsLevel.None) && invalid is null)
        {
            return null;
        }

        return new FileSettings(
            normalizedPath,
            level,
            invalid is null
                ? EquatableArray<InvalidSetting>.Empty
                : new EquatableArray<InvalidSetting>(invalid.ToImmutableArray())
        );
    }
}
```

`src/SqlSource/Generation/FileMetadata.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SqlSource.Generation;

/// <summary>
/// One <c>.sql</c> file with what the metadata of its item says: its dialect, and its settings.
/// </summary>
/// <param name="File">
/// The file.  Compared by reference: the compiler hands out the same object until the file changes.
/// </param>
/// <param name="Dialect">The dialect the metadata names, if any.</param>
/// <param name="Settings">The settings the metadata gives, or null when it gives none.</param>
internal sealed record FileMetadata(AdditionalText File, DialectSetting Dialect, FileSettings? Settings)
{
    public static FileMetadata Read(AdditionalText file, AnalyzerConfigOptions fileOptions) =>
        new(
            file,
            DialectSetting.ReadMetadata(fileOptions),
            SqlPath.Normalize(file.Path) is { } path ? FileSettings.Read(path, fileOptions) : null
        );
}
```

`src/SqlSource/Generation/FileParseInput.cs`, which replaces `FileDialect.cs`:

```csharp
using Microsoft.CodeAnalysis;
using SqlSource.Parsing;
using SqlSource.Settings;

namespace SqlSource.Generation;

/// <summary>
/// A <c>.sql</c> file with everything outside it that its parse depends on.  This is stage one of the settings:
/// what must be known before the file is parsed.  Everything else joins after a type's queries are selected.
/// </summary>
/// <param name="File">
/// The file.  Compared by reference: the compiler hands out the same object until the file changes.
/// </param>
/// <param name="NormalizedPath">The file's path as <see cref="SqlPath.Normalize" /> gives it, or null.</param>
/// <param name="Dialect">
/// The dialect the file is parsed with, unless a marker in it names another: its own metadata, or else the
/// project's property, or else <see cref="SqlDialect.Ansi" />.
/// </param>
/// <param name="InvalidDialect">The file's metadata as written when it is not a dialect, and null otherwise.</param>
/// <param name="CommentsWanted">
/// Whether a level the parser cannot see may ask for <c>keep-comments</c>: the metadata's list, or the property's
/// when the metadata gives none, or the attribute of a type that claims the file.  It may be true where no query
/// ends up keeping its comments; it is never false where one does.
/// </param>
internal sealed record FileParseInput(
    AdditionalText File,
    string? NormalizedPath,
    SqlDialectChoice Dialect,
    string? InvalidDialect,
    bool CommentsWanted
)
{
    /// <summary>
    /// A file's own metadata comes before the project's property, whether or not it is valid: a file with metadata
    /// that is not a dialect is read as <see cref="SqlDialect.Ansi" />, and its value is reported.
    /// </summary>
    public static FileParseInput Resolve(
        FileMetadata metadata,
        DialectSetting projectDialect,
        bool projectKeepsComments,
        EquatableArray<string> commentPaths
    )
    {
        var path = SqlPath.Normalize(metadata.File.Path);
        var commentsWanted =
            (metadata.Settings?.Level.Parameters is { } list
                ? (list & GeneratorParameters.KeepComments) != 0
                : projectKeepsComments) || (path is not null && SqlPath.Contains(commentPaths, path));

        return metadata.Dialect.Dialect is { } dialect
            ? new FileParseInput(metadata.File, path, dialect, metadata.Dialect.InvalidValue, commentsWanted)
            : new FileParseInput(metadata.File, path, projectDialect.Dialect ?? default, null, commentsWanted);
    }
}
```

`DialectSetting.MetadataName` becomes `"build_metadata.SqlSourceSettingsFile.SqlSourceDialect"`, and its comment names `SqlSourceSettingsFile`.

`SqlFileReader`: the first overload becomes

```csharp
    /// <summary>
    /// Parses <paramref name="file" />, whose path a type claims, with what MSBuild gives it.  A
    /// <c>-- dialect:</c> marker in the file replaces the dialect.
    /// </summary>
    public static ParsedSqlFile Read(FileParseInput file, CancellationToken cancellationToken) =>
        Read(
            file.File,
            file.NormalizedPath ?? string.Empty,
            file.Dialect,
            file.InvalidDialect,
            file.CommentsWanted,
            cancellationToken
        );
```

- [ ] **Step 6: Read the attribute's `Parameters`**

`AttributeSource`: `public const string ParametersProperty = "Parameters";`, and in `Text`, after the `SqlLocation` property:

```csharp
                /// <summary>
                /// Generator parameters for the queries of the type's files, separated by spaces, as a
                /// <c>-- generator:</c> line holds them.  A line in a file comes first.
                /// </summary>
                public string Parameters { get; set; }
```

`TargetType` gains `SettingsLevel Settings` after `Placement`, documented "What the attribute says about the settings."  `TargetTypeReader.Read` passes `ReadSettings(attribute, attributeLocation, diagnostics)`:

```csharp
    private static SettingsLevel ReadSettings(
        AttributeData attribute,
        LocationInfo attributeLocation,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics
    )
    {
        var level = SettingsLevel.None;
        if (GetNamedArgument(attribute, AttributeSource.ParametersProperty) is { Value: string list })
        {
            var words = new List<string>();
            if (GeneratorParameterList.Parse(list, words) is { } parameters)
            {
                level = level with { Parameters = parameters };
            }

            foreach (var word in words)
            {
                diagnostics.Add(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.InvalidAttributeValue,
                        attributeLocation,
                        word,
                        AttributeSource.ParametersProperty
                    )
                );
            }
        }

        return level;
    }
```

`ReadPlacement` reports `SqlDiagnostics.InvalidAttributeValue` with the arguments `value.ToString(CultureInfo.InvariantCulture)` and `AttributeSource.LocationProperty`.  `TestModels.Type` gains `SettingsLevel? settings = null` and passes `settings ?? SettingsLevel.None`.

- [ ] **Step 7: Rewire the pipeline**

`TrackingNames`: `FileDialect` becomes `FileParseInput`; `TokenValidation` becomes `ProjectSettings`; add `FileSettings`, `FilesSettings` and `CommentPaths`.

`TypeQueries`:

```csharp
/// <param name="Type">The type and the paths of its files.</param>
/// <param name="Files">The type's files, in member order.</param>
/// <param name="FileSettings">
/// What the metadata of each file's item says about the settings: one level for each of <paramref name="Files" />.
/// </param>
/// <param name="HintName">The name of the type's generated file.  Unique, ignoring case, in the compilation.</param>
internal sealed record TypeQueries(
    TypeFiles Type,
    EquatableArray<ParsedSqlFile> Files,
    EquatableArray<SettingsLevel> FileSettings,
    string HintName
);
```

`SqlSourceGenerator.Initialize`, from the comment "The project's dialect" to the end of the method:

```csharp
        // The project's dialect, which is the same value until the property itself changes.
        var projectDialect = context
            .AnalyzerConfigOptionsProvider.Select(
                static (options, _) => DialectSetting.ReadProperty(options.GlobalOptions)
            )
            .WithTrackingName(TrackingNames.ProjectDialect);

        // What the project's properties say about the settings, which is the same value until one of them changes.
        var projectSettings = context
            .AnalyzerConfigOptionsProvider.Select(
                static (options, _) => ProjectSettings.Read(options.GlobalOptions)
            )
            .WithTrackingName(TrackingNames.ProjectSettings);

        // Whether the property's list asks for comments.  It is an input of the parse, so it is a value of its own
        // that changes only when the answer does.
        var projectKeepsComments = projectSettings.Select(static (settings, _) => settings.Level.KeepsComments);

        // The files of the types whose attribute asks for comments.  Almost always none, so this value almost never
        // changes.
        var commentPaths = typeFiles
            .SelectMany(
                static (type, _) => type.Type.Settings.KeepsComments ? type.Files : EquatableArray<string>.Empty
            )
            .Collect()
            .Select(static (paths, _) => ToSortedSet(paths))
            .WithTrackingName(TrackingNames.CommentPaths);

        // What the metadata of each file's item says: its dialect and its settings.
        var fileMetadata = sqlFiles
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select(static (input, _) => FileMetadata.Read(input.Left, input.Right.GetOptions(input.Left)));

        // Stage one of the settings: each file with what its parse depends on.  It is resolved here, before the
        // parse, so that a change to a property parses only the files whose input it changes.
        var fileInputs = fileMetadata
            .Combine(projectDialect)
            .Combine(projectKeepsComments.Combine(commentPaths))
            .Select(
                static (input, _) =>
                    FileParseInput.Resolve(input.Left.Left, input.Left.Right, input.Right.Left, input.Right.Right)
            )
            .WithTrackingName(TrackingNames.FileParseInput);

        // A file that no type claims is never read.
        var parsedFiles = fileInputs
            .Combine(claimedPaths)
            .Select(
                static (input, cancellationToken) =>
                    input.Left.NormalizedPath is { } path && SqlPath.Contains(input.Right, path)
                        ? SqlFileReader.Read(input.Left, cancellationToken)
                        : null
            )
            .Where(static file => file is not null)
            .Select(static (file, _) => file!)
            .WithTrackingName(TrackingNames.ParsedFile)
            .Collect()
            .Select(static (files, _) => ToSortedFiles(files))
            .WithTrackingName(TrackingNames.ParsedFiles);

        // Reported from the collected files, so that a file the project lists twice is reported once.  A dialect that
        // is not valid is reported here too, once for each value: the compiler does not say where an MSBuild
        // property or the metadata of an item was set, so it has no position.
        context.RegisterSourceOutput(
            parsedFiles.Combine(projectDialect),
            static (output, input) =>
            {
                foreach (var error in input.Left.SelectMany(static file => file.Errors))
                {
                    output.ReportDiagnostic(error.ToDiagnostic());
                }

                foreach (var value in FindInvalidDialects(input.Left, input.Right))
                {
                    output.ReportDiagnostic(Diagnostic.Create(SqlDiagnostics.InvalidDialect, Location.None, value));
                }
            }
        );

        // Stage two of the settings: the metadata of the files that have any, which are few.  It never reaches the
        // parse of a file.
        var filesSettings = fileMetadata
            .Select(static (metadata, _) => metadata.Settings)
            .Where(static settings => settings is not null)
            .Select(static (settings, _) => settings!)
            .WithTrackingName(TrackingNames.FileSettings)
            .Collect()
            .Select(static (settings, _) => ToSortedSettings(settings))
            .WithTrackingName(TrackingNames.FilesSettings);

        // Not located, and once for each setting and value.  The metadata of a file that no type claims is not
        // reported, as nothing else about such a file is.
        context.RegisterSourceOutput(
            projectSettings.Combine(filesSettings).Combine(claimedPaths),
            static (output, input) =>
            {
                foreach (var setting in FindInvalidSettings(input.Left.Left, input.Left.Right, input.Right))
                {
                    output.ReportDiagnostic(
                        Diagnostic.Create(
                            SqlDiagnostics.InvalidSettingValue,
                            Location.None,
                            setting.Value,
                            setting.Name
                        )
                    );
                }
            }
        );

        // Two types whose files would have names equal ignoring case.  Almost always none, so this value almost
        // never changes and costs the steps after it nothing.
        var ambiguousHintNames = typeFiles
            .Select(static (type, _) => HintName.Create(type.Type))
            .Collect()
            .Select(static (names, _) => HintName.FindAmbiguous(names))
            .WithTrackingName(TrackingNames.AmbiguousHintNames);

        // The properties join after a type's queries are selected, so that they never reach the parse of a file.
        var typeOutputs = typeFiles
            .Combine(parsedFiles)
            .Combine(ambiguousHintNames.Combine(filesSettings))
            .Select(
                static (input, _) =>
                    SelectFiles(input.Left.Left, input.Left.Right, input.Right.Left, input.Right.Right)
            )
            .WithTrackingName(TrackingNames.TypeQueries)
            .Combine(projectSettings.Select(static (settings, _) => settings.Level))
            .Select(static (input, _) => TypeEmitter.Emit(input.Left, input.Right))
            .WithTrackingName(TrackingNames.TypeOutput);

        context.RegisterSourceOutput(
            typeOutputs,
            static (output, type) =>
            {
                foreach (var diagnostic in type.Diagnostics)
                {
                    output.ReportDiagnostic(diagnostic.ToDiagnostic());
                }

                if (type.Source is not null)
                {
                    output.AddSource(type.HintName, SourceText.From(type.Source, Encoding.UTF8));
                }
            }
        );
    }
```

`commentPaths` is declared after `claimedPaths`, which it stands beside.  The helpers at the end of the class gain:

```csharp
    // One entry for each path, the first the project lists, in the order of SqlPath.Comparer.
    private static EquatableArray<FileSettings> ToSortedSettings(ImmutableArray<FileSettings> settings) =>
        new(
            settings
                .GroupBy(static file => file.NormalizedPath, SqlPath.Comparer)
                .Select(static group => group.First())
                .OrderBy(static file => file.NormalizedPath, SqlPath.Comparer)
                .ToImmutableArray()
        );

    // Each setting and value once, in ordinal order, so that the errors of a build do not depend on the order of its
    // files.
    private static IEnumerable<InvalidSetting> FindInvalidSettings(
        ProjectSettings project,
        EquatableArray<FileSettings> files,
        EquatableArray<string> claimedPaths
    )
    {
        var found = new HashSet<InvalidSetting>(project.Invalid);
        foreach (var file in files)
        {
            if (SqlPath.Contains(claimedPaths, file.NormalizedPath))
            {
                found.UnionWith(file.Invalid);
            }
        }

        return found
            .OrderBy(static setting => setting.Name, StringComparer.Ordinal)
            .ThenBy(static setting => setting.Value, StringComparer.Ordinal);
    }
```

and `SelectFiles` becomes:

```csharp
    private static TypeQueries SelectFiles(
        TypeFiles type,
        EquatableArray<ParsedSqlFile> parsedFiles,
        EquatableArray<string> ambiguousHintNames,
        EquatableArray<FileSettings> filesSettings
    )
    {
        var files = ImmutableArray.CreateBuilder<ParsedSqlFile>(type.Files.Count);
        var settings = ImmutableArray.CreateBuilder<SettingsLevel>(type.Files.Count);

        // A search for each of the type's files, so that the cost does not grow with the files of other types.
        foreach (var path in type.Files)
        {
            var index = SqlPath.IndexOf(parsedFiles, path, static file => file.NormalizedPath);
            if (index < 0)
            {
                continue;
            }

            files.Add(parsedFiles[index]);
            var settingsIndex = SqlPath.IndexOf(filesSettings, path, static file => file.NormalizedPath);
            settings.Add(settingsIndex < 0 ? SettingsLevel.None : filesSettings[settingsIndex].Level);
        }

        return new TypeQueries(
            type,
            new EquatableArray<ParsedSqlFile>(files.ToImmutable()),
            new EquatableArray<SettingsLevel>(settings.ToImmutable()),
            HintName.MakeUnique(HintName.Create(type.Type), ambiguousHintNames)
        );
    }
```

Delete the `tokenValidation` step and its output, `TokenValidationSetting.cs` and its tests.

- [ ] **Step 8: Resolve all four levels in the emitter**

`TypeEmitter.SelectMembers` returns `List<(SqlQuery Query, string FileName, SettingsLevel Metadata)>`: it takes `EquatableArray<SettingsLevel> fileSettings`, loops over the files by index, and adds `(query, file.FileName, fileSettings[index])`.  `Emit` passes `input.FileSettings`.  In `Write`:

```csharp
            var (query, fileName, metadata) = members[index];
            var summaryXml = GetSummaryXml(query, fileName);
            var settings = QuerySettings.Resolve(query.Markers, type.Settings, metadata, property);
```

`TypeEmitterTests`: the helper `Emit` builds `TypeQueries` with one `SettingsLevel.None` for each file, and gains an overload that takes the levels; add

```csharp
[Fact]
public void Emit_FourLevels_ResolveForEachQuery()
{
    var keep = new SettingsLevel { Parameters = GeneratorParameters.KeepComments };
    var none = new SettingsLevel { Parameters = GeneratorParameters.None };
    SqlQuery Kept(string name, SettingsLevel markers) =>
        new(
            name,
            NameLocation("Users.sql"),
            null,
            null,
            TestModels.Array(new SqlSegment(SqlSegmentKind.Literal, "SELECT 1;")),
            TestModels.Array(new SqlSegment(SqlSegmentKind.Literal, "SELECT 1; -- " + name)),
            EquatableArray<SqlToken>.Empty,
            EquatableArray<SqlQueryParameter>.Empty,
            markers
        );

    var output = TypeEmitter.Emit(
        new TypeQueries(
            new TypeFiles(
                TestModels.Type(settings: none),
                TestModels.Array<string>(),
                TestModels.Array<DiagnosticInfo>()
            ),
            TestModels.Array(File("Users.sql", Kept("FromTheAttribute", SettingsLevel.None), Kept("FromItsMarker", keep))),
            TestModels.Array(keep),
            "App.UserRepository.g.cs"
        ),
        keep
    );

    // The attribute's empty list beats the metadata and the property; a query's own marker beats the attribute.
    output.Source.ShouldNotBeNull().ShouldNotContain("-- FromTheAttribute");
    output.Source.ShouldContain("-- FromItsMarker");
}
```

- [ ] **Step 9: Generalise the props and the targets**

`BuildFileTests` first.  Replace the tests of the properties, of the metadata, of the trim and of the collection with:

```csharp
    // What the generator reads: each as a property of the project and as metadata of a file's item.
    private static readonly string[] Settings = ["SqlSourceDialect", "SqlSourceGeneratorParameters"];

    // What the targets trim and the generator does not read.
    private static readonly string[] TrimmedOnly = [];

    [Fact]
    public void Props_PropertiesOfThePackage_ReachTheCompilerInEveryProject()
    {
        var items = Props.Descendants("CompilerVisibleProperty").ToList();

        items.Select(item => item.Attribute("Include").ShouldNotBeNull().Value).ShouldBe(Settings, ignoreOrder: true);

        // A project that sets SqlSourceIncludeFiles to false lists its own .sql files, and still needs the
        // properties.  So nothing may put a condition on the items.
        items.SelectMany(ConditionsAround).ShouldBeEmpty();
    }

    // The SDK writes a section into the file the compiler reads for every item of the type that is named here,
    // whether the item has the metadata or not.  AdditionalFiles would be every .sql file of the project.
    // SqlSourceSettingsFile holds only the files that have any: see
    // Targets_FilesWithMetadata_AreTheItemsWhoseMetadataTheCompilerReads.
    [Fact]
    public void Props_MetadataOfASqlFile_ReachesTheCompilerInEveryProject()
    {
        var items = Props.Descendants("CompilerVisibleItemMetadata").ToList();

        items.ShouldAllBe(item => item.Attribute("Include")!.Value == "SqlSourceSettingsFile");
        items
            .Select(item => item.Attribute("MetadataName").ShouldNotBeNull().Value)
            .ShouldBe(Settings, ignoreOrder: true);
        items.SelectMany(ConditionsAround).ShouldBeEmpty();
    }

    [Fact]
    public void Targets_EveryPropertyOfThePackage_IsTrimmed()
    {
        var trimmed = Targets
            .Descendants("Target")
            .Single(target => target.Attribute("Name")!.Value == "SqlSourceTrimProperties")
            .Descendants("PropertyGroup")
            .Elements()
            .ToList();

        trimmed.Select(element => element.Name.LocalName).ShouldBe(Settings.Concat(TrimmedOnly), ignoreOrder: true);
        trimmed.ShouldAllBe(element => element.Value == $"$({element.Name.LocalName}.Trim())");
        trimmed.SelectMany(ConditionsAround).ShouldBeEmpty();
    }

    // The target runs once for each combination of values that the metadata has, with only the items of that
    // combination in reach.  Each value goes through a property so that it is never text inside a property
    // function: see Targets_ItemMetadata_IsNeverTheArgumentOfAPropertyFunction.  Most files have no metadata, and
    // they are one batch with nothing to trim: nothing is written back to them, and a value that is not set is not
    // written at all.
    [Fact]
    public void Targets_MetadataOfEverySqlFile_IsTrimmed()
    {
        var names = Settings.Concat(TrimmedOnly).ToList();
        var item = Targets.Descendants("AdditionalFiles").ShouldHaveSingleItem();
        var target = item.Ancestors("Target").ShouldHaveSingleItem();

        target.Attribute("Name").ShouldNotBeNull().Value.ShouldBe("SqlSourceTrimMetadataOfFiles");
        target
            .Attribute("Outputs")
            .ShouldNotBeNull()
            .Value.Split('|')
            .ShouldBe(names.Select(name => $"%(AdditionalFiles.{name})"), ignoreOrder: true);
        foreach (var name in names)
        {
            target.Descendants(name + "AsWritten").ShouldHaveSingleItem().Value.ShouldBe($"%(AdditionalFiles.{name})");
            var metadata = item.Elements(name).ShouldHaveSingleItem();
            metadata.Value.ShouldBe($"$({name}AsWritten.Trim())");
            metadata.Attribute("Condition").ShouldNotBeNull().Value.ShouldBe($"'$({name}AsWritten)' != ''");
        }

        item.Attributes().ShouldBeEmpty();
        item.Elements().Count().ShouldBe(names.Count);
        item.Parent.ShouldNotBeNull()
            .Attribute("Condition")
            .ShouldNotBeNull()
            .Value.ShouldBe("'" + string.Concat(names.Select(name => $"$({name}AsWritten)")) + "' != ''");
    }

    // The compiler reads the metadata from the items of SqlSourceSettingsFile, and the files without any are not
    // among them.  The collecting has to come after the trim: the values it copies are trimmed by then, and a file
    // whose values were only white space is left out.  MSBuild does not promise an order for two targets that hook
    // the same one, so the target says what it depends on.
    [Fact]
    public void Targets_FilesWithMetadata_AreTheItemsWhoseMetadataTheCompilerReads()
    {
        var item = Targets.Descendants("SqlSourceSettingsFile").ShouldHaveSingleItem();
        var target = item.Ancestors("Target").ShouldHaveSingleItem();

        item.Attribute("Include").ShouldNotBeNull().Value.ShouldBe("@(AdditionalFiles)");
        item.Attribute("Condition")
            .ShouldNotBeNull()
            .Value.ShouldBe("'" + string.Concat(Settings.Select(name => $"%(AdditionalFiles.{name})")) + "' != ''");
        target.Attribute("Name").ShouldNotBeNull().Value.ShouldBe("SqlSourceCollectSettingsFiles");
        target.Attribute("DependsOnTargets").ShouldNotBeNull().Value.ShouldBe("SqlSourceTrimMetadataOfFiles");
    }
```

Tasks 10 and 11 add names to `Settings` and `TrimmedOnly`, and nothing else in this file.

`SqlSource.props`: rewrite the second comment for the item type's new name and for "the properties and metadata the generator reads", and the item group becomes

```xml
    <ItemGroup>
        <CompilerVisibleProperty Include="SqlSourceDialect" />
        <CompilerVisibleProperty Include="SqlSourceGeneratorParameters" />
        <CompilerVisibleItemMetadata Include="SqlSourceSettingsFile" MetadataName="SqlSourceDialect" />
        <CompilerVisibleItemMetadata Include="SqlSourceSettingsFile" MetadataName="SqlSourceGeneratorParameters" />
    </ItemGroup>
```

`SqlSource.targets`: the first three targets become the following, with the comments reworded for "each property" and "each metadata", for the new target names, and with the note that a target which adds a `.sql` file hooks `SqlSourceTrimMetadataOfFiles`:

```xml
    <Target Name="SqlSourceTrimProperties" BeforeTargets="GenerateMSBuildEditorConfigFileCore">
        <PropertyGroup>
            <SqlSourceDialect>$(SqlSourceDialect.Trim())</SqlSourceDialect>
            <SqlSourceGeneratorParameters>$(SqlSourceGeneratorParameters.Trim())</SqlSourceGeneratorParameters>
        </PropertyGroup>
    </Target>
    <Target
        Name="SqlSourceTrimMetadataOfFiles"
        BeforeTargets="GenerateMSBuildEditorConfigFileCore"
        Outputs="%(AdditionalFiles.SqlSourceDialect)|%(AdditionalFiles.SqlSourceGeneratorParameters)"
    >
        <PropertyGroup>
            <SqlSourceDialectAsWritten>%(AdditionalFiles.SqlSourceDialect)</SqlSourceDialectAsWritten>
            <SqlSourceGeneratorParametersAsWritten>%(AdditionalFiles.SqlSourceGeneratorParameters)</SqlSourceGeneratorParametersAsWritten>
        </PropertyGroup>
        <ItemGroup Condition="'$(SqlSourceDialectAsWritten)$(SqlSourceGeneratorParametersAsWritten)' != ''">
            <AdditionalFiles>
                <SqlSourceDialect Condition="'$(SqlSourceDialectAsWritten)' != ''">$(SqlSourceDialectAsWritten.Trim())</SqlSourceDialect>
                <SqlSourceGeneratorParameters Condition="'$(SqlSourceGeneratorParametersAsWritten)' != ''">$(SqlSourceGeneratorParametersAsWritten.Trim())</SqlSourceGeneratorParameters>
            </AdditionalFiles>
        </ItemGroup>
    </Target>
    <Target
        Name="SqlSourceCollectSettingsFiles"
        BeforeTargets="GenerateMSBuildEditorConfigFileCore"
        DependsOnTargets="SqlSourceTrimMetadataOfFiles"
    >
        <ItemGroup>
            <SqlSourceSettingsFile
                Include="@(AdditionalFiles)"
                Condition="'%(AdditionalFiles.SqlSourceDialect)%(AdditionalFiles.SqlSourceGeneratorParameters)' != ''"
            />
        </ItemGroup>
    </Target>
```

`./format.sh` may re-wrap the long elements; the tests read values, not layout.

If the build rejects the batching on two metadata, or a `Condition` on a metadata element inside a target, fall back to one trim target for each metadata, each the shape the dialect's has today and each named `SqlSourceTrim<Name>OfFiles`, with `SqlSourceCollectSettingsFiles` depending on all of them; change `Targets_MetadataOfEverySqlFile_IsTrimmed` to pin that shape, and record the fallback in a new `docs/tech-debt` entry, numbered after the highest in `docs/tech-debt/README.md`.

- [ ] **Step 10: Move the end-to-end project**

`tests/SqlSource.Tests/SqlSource.Tests.csproj`: the property becomes

```xml
        <!--
            Token validation is off, so that EndToEnd/ shows the property reaching the generator through the MSBuild
            files: a query there without a list of its own does not check its arguments.  The value is on a line of
            its own on purpose.  The compiler would read that as empty, and SqlSource.targets is what trims it.
        -->
        <SqlSourceGeneratorParameters>
            no-token-validation
        </SqlSourceGeneratorParameters>
```

and the item group of metadata gains

```xml
        <AdditionalFiles Update="EndToEnd/Parameters/Kept.sql">
            <SqlSourceGeneratorParameters>
                keep-comments
            </SqlSourceGeneratorParameters>
        </AdditionalFiles>
```

`tests/SqlSource.Tests/EndToEnd/Parameters/Kept.sql`:

```sql
-- The item of this file has SqlSourceGeneratorParameters metadata: keep-comments.

-- name: Kept
SELECT 1 /* kept by the metadata */ AS one;
```

`tests/SqlSource.Tests/EndToEnd/Parameters/Shared.sql`:

```sql
-- Two types claim this file, and one of them asks for comments on its attribute.

-- name: Shared
SELECT 2 /* kept by the attribute */ AS two;
```

`tests/SqlSource.Tests/EndToEnd/ParameterQueries.cs`:

```csharp
namespace SqlSource.Tests.EndToEnd;

// The files of the Parameters folder, with whatever MSBuild says about each.
[SqlSourceGenerate(Path = "Parameters", SqlLocation = SqlLocation.Direct)]
internal static partial class ParameterQueries;

// One of those files again, for a type whose attribute asks for comments.
[SqlSourceGenerate(Path = "Parameters/Shared.sql", SqlLocation = SqlLocation.Direct, Parameters = "keep-comments")]
internal static partial class KeptQueries;
```

`EndToEndTests`: rename the three tests that start `ProjectWithValidationOff_QueryWithTheGeneratorParameter_` to start `ProjectWithValidationOff_QueryWithItsOwnList_`, reword the comment above `ProjectWithValidationOff_QueryWithoutAGeneratorParameter_...` for `SqlSourceGeneratorParameters`, add `ParameterQueries` and `KeptQueries` to the theory of types, and add:

```csharp
    // The item of Kept.sql has SqlSourceGeneratorParameters metadata, written over several lines.  That the comment
    // is there shows the metadata reaching the generator through the MSBuild files the package ships, trimmed, and
    // replacing the project's list.
    [Fact]
    public void ProjectWithParameters_FileWithMetadata_UsesTheListOfItsItem() =>
        ParameterQueries.Kept.ShouldBe("SELECT 1 /* kept by the metadata */ AS one;");

    [Fact]
    public void ProjectWithParameters_TwoTypesClaimOneFile_EachUsesItsOwnAttribute()
    {
        ParameterQueries.Shared.ShouldBe("SELECT 2   AS two;");
        KeptQueries.Shared.ShouldBe("SELECT 2 /* kept by the attribute */ AS two;");
    }
```

`ProjectWithADialect_FileTheBuildWritesForTheCompiler_NamesOnlyTheFilesWithMetadata` reads every line of a section, since a section now has a line for each metadata the package lists:

```csharp
    [Fact]
    public void ProjectWithMetadata_FileTheBuildWritesForTheCompiler_NamesOnlyTheFilesWithMetadata()
    {
        var lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "build", "compiler.editorconfig"));

        var values = new List<string>();
        var section = string.Empty;
        foreach (var line in lines)
        {
            if (line.StartsWith('['))
            {
                section = line.EndsWith(".sql]", StringComparison.Ordinal) ? line[line.LastIndexOf('/')..] : string.Empty;
            }
            else if (section.Length > 0 && line.Contains(" = ", StringComparison.Ordinal) && !line.EndsWith(" = ", StringComparison.Ordinal))
            {
                values.Add(section + " " + line);
            }
        }

        values.ShouldBe(
            [
                "/ByMetadata.sql] build_metadata.SqlSourceSettingsFile.SqlSourceDialect = mysql",
                "/ByOption.sql] build_metadata.SqlSourceSettingsFile.SqlSourceDialect = mysql, no-backslash-escapes",
                "/Kept.sql] build_metadata.SqlSourceSettingsFile.SqlSourceGeneratorParameters = keep-comments",
            ],
            ignoreOrder: true
        );
    }
```

Add `using System.Collections.Generic;`.  If the SDK writes an empty value as `name =` with no trailing space, the second condition already leaves it out through `Contains(" = ")`; check the copied file once and adjust the two conditions to what it holds.

- [ ] **Step 11: Move the package-install project**

- `tools/package-install/Directory.Build.targets`: `SqlSourceTokenValidation` with `false` becomes `SqlSourceGeneratorParameters` with `no-token-validation`, on a line of its own as before.
- `Consumer.csproj`: the comment names `SqlSourceGeneratorParameters`, and the item group gains

```xml
        <AdditionalFiles Update="Queries/Kept.sql">
            <SqlSourceGeneratorParameters>
                keep-comments
            </SqlSourceGeneratorParameters>
        </AdditionalFiles>
```

- `Queries/Kept.sql`: `SELECT 1 /* kept */ AS one;` and a trailing newline.
- `Queries/Users.sql`: the first query becomes `SELECT id, name FROM users /* by key */ WHERE id = @id;`.
- `Queries.cs` gains

```csharp
// One of those files again, for a type whose attribute asks for comments.
[SqlSourceGenerate(Path = "Queries/Users.sql", SqlLocation = SqlLocation.Direct, Parameters = "keep-comments")]
internal static partial class KeptQueries;
```

- `Program.cs`: the comment on the second line of output names `SqlSourceGeneratorParameters`, and before the dialect lines add

```csharp
// The item of Kept.sql says keep-comments, and so does the attribute of KeptQueries, which claims Users.sql again.
Console.WriteLine($"comments kept by the item: {Queries.Kept}");
Console.WriteLine($"comments kept by the attribute: {KeptQueries.GetUser}");
```

- `expected-output.txt`: the first line becomes `constant: SELECT id, name FROM users   WHERE id = @id;`, and after the `validation on` line add

```text
comments kept by the item: SELECT 1 /* kept */ AS one;
comments kept by the attribute: SELECT id, name FROM users /* by key */ WHERE id = @id;
```

`KeptQueries` has the token methods of `Users.sql` too; nothing calls them.

- [ ] **Step 12: Run everything**

Run: `dotnet test --solution SqlSource.slnx`
Expected: PASS.

Run: `./pre-commit-validation.sh`
Expected: every check passes, `package-install` included.  A failure there with a diff of the output is an expectation to correct against what the project printed, after checking that what it printed is right.

- [ ] **Step 13: Update the documents**

`README.md`:

- The attribute: after Locations, add

````markdown
### Parameters

`Parameters` holds generator parameters for the queries of the type's files, as a `-- generator:` line holds them (see Generator parameters, below):

```csharp
[SqlSourceGenerate(Path = "Reports", Parameters = "keep-comments")]
internal static partial class Reports;
```

Two types that claim one file can ask for different things, and each gets its own SQL.
````

- Generator parameters: after the paragraph "**A list replaces; it never adds.**", add

```markdown
The same list can be given above the file, and the rule is the same at every level.  The list for a query, for a type that claims its file, is the first of these that gives one, whole:

1. The `-- generator:` lines inside the query.
2. The `-- generator:` lines before the file's first `-- name:` line.
3. `Parameters` on the type's attribute.
4. `SqlSourceGeneratorParameters` metadata on the file's `AdditionalFiles` item.
5. The MSBuild property `SqlSourceGeneratorParameters`.

A level that wants what the level below it gives restates it.  `default` at any level is the empty list.
```

- Tokens, Validation: the numbered list goes, and the text becomes "An empty fragment can be what you want, for an optional clause for example, so the check can be turned off with the `no-token-validation` generator parameter, at any level:", followed by the two examples with `SqlSourceGeneratorParameters` holding `no-token-validation` and with `-- generator: no-token-validation`.
- MSBuild: the section Token validation becomes

````markdown
### Generator parameters

`SqlSourceGeneratorParameters` holds generator parameters, separated by spaces.  It is a property for the project and metadata of an `AdditionalFiles` item for some of its files.  See Generator parameters, above.

```xml
<PropertyGroup>
    <SqlSourceGeneratorParameters>no-token-validation</SqlSourceGeneratorParameters>
</PropertyGroup>
<ItemGroup>
    <AdditionalFiles Update="Reports/**/*.sql" SqlSourceGeneratorParameters="keep-comments" />
</ItemGroup>
```

A word that is not a generator parameter is the error [SQLSRC014](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc014).  The compiler hands a generator only the part of a value before the first `;` or `#`, so separate the words with spaces.
````

`docs/diagnostics.md`: the index follows the descriptors.  `SQLSRC006`:

````markdown
## SQLSRC006

**Attribute value is not valid**

A property of `[SqlSourceGenerate]` has a value it does not take.  The message names the property and the value: a number that is no member of the property's enum, or a word in `Parameters` that is not a generator parameter.

```csharp
[SqlSourceGenerate(SqlLocation = (SqlLocation)5, Parameters = "keep-coments")]
public partial class UserRepository { }
```

Use a member of the enum, and for `Parameters` the words that [SQLSRC109](#sqlsrc109) lists.  The type gets no members until the value is fixed.
````

Delete the section of `SQLSRC010`, and add after `SQLSRC013`:

````markdown
## SQLSRC014

**MSBuild setting is not valid**

An MSBuild property of SqlSource, or the metadata of that name on an `AdditionalFiles` item, has a value it does not take.  The message names the setting and the value.  The error has no file and line, because the compiler does not tell a generator where a property or the metadata of an item was set: look in the project file, in `Directory.Build.props` and `Directory.Build.targets`, and at a `-p:` argument of the build command.

```xml
<PropertyGroup>
    <SqlSourceGeneratorParameters>keep-coments</SqlSourceGeneratorParameters>
</PropertyGroup>
```

Correct the value, or remove it to keep the default.  While it is wrong the setting is not set; in a list of generator parameters the other words still apply.  The same value in several places is reported once, and the metadata of a file that no type claims is not reported.

The compiler hands a generator only the part of a value before the first `;` or `#`.
````

In `docs/diagnostics.md` and the README, the sentence on `SQLSRC901` says "the types it adds to every project" in place of "the two types".

`src/SqlSource/AGENTS.md`, The pipeline: replace the bullet "**Token validation joins the pipeline after `TypeQueries`.**" with

```markdown
- **The settings have two stages, and only the first reaches the parse.**  Stage one is `Generation/FileParseInput.cs`: a file's dialect, and whether comments may be wanted, which is true when the list of the file's metadata, or else of the property, has `keep-comments`, or when the file is in `CommentPaths`, the files of the types whose attribute asks for it.  Stage two is everything else: `Generation/ProjectSettings.cs` reads the properties, `Generation/FileSettings.cs` the metadata of each file's item, `TargetTypeReader` the attribute, and `TypeEmitter` resolves the four levels with a query's markers through `QuerySettings.Resolve`.  A setting of stage two must never be an input of `SqlFileReader.Read`: a change to a property would then parse every file again.  `tests/SqlSource.Tests/Generator/CachingTests.cs` pins what each kind of edit parses.
- **A value MSBuild gives is trimmed by a target.**  The compiler reads it from a file the build writes with one line for each value, so a value on a line of its own would arrive empty, and no code in the generator can see that.  The trims run before `GenerateMSBuildEditorConfigFileCore`, the target of the SDK that writes the file, because a trim outside a target misses `Directory.Build.targets` and an item that a target adds.  They still miss an item added by a target that hooks the same SDK target and is declared later; the comment in the file says what such a target does instead.
```

In the same file: the bullet on the dialect names `SqlSourceSettingsFile` and `FileParseInput`; the bullet on `SQLSRC010` and `SQLSRC011` says "`SQLSRC011` and `SQLSRC014` have no position", and that an invalid setting travels as an `InvalidSetting`.

`docs/tech-debt/TD-0016-dialect-of-a-file-added-by-a-late-target-is-lost.md`: `SqlSourceTrimDialectOfFiles` becomes `SqlSourceTrimMetadataOfFiles`, `SqlSourceDialectFile` becomes `SqlSourceSettingsFile`, and one sentence says that the same holds for every metadata the package reads.

- [ ] **Step 14: Commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add -A && git commit -m "Give generator parameters every level, and resolve the settings in two stages

SqlSourceGeneratorParameters replaces SqlSourceTokenValidation, and SQLSRC010 goes with it.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: `-- database:`, `-- output:` and `GeneratorOutput`

**Files:**
- Create: `src/SqlSource/Settings/OutputKind.cs`
- Modify: `src/SqlSource/Settings/SettingValue.cs`, `SettingsLevel.cs`, `QuerySettings.cs`
- Modify: `src/SqlSource/Parsing/SqlMarkerKind.cs`, `SqlMarkerReader.cs`, `SqlMarkerScope.cs`
- Modify: `src/SqlSource/Generation/AttributeSource.cs`, `TargetTypeReader.cs`, `MSBuildSettings.cs`; `src/SqlSource/Diagnostics/AttributeConflictSuppressor.cs`
- Modify: `src/SqlSource/build/SqlSource.props`, `SqlSource.targets`
- Modify: `README.md`, `docs/diagnostics.md`, `src/SqlSource/AGENTS.md`, `docs/tech-debt/TD-0006-attribute-conflicts-across-friend-assemblies.md`
- Test: `tests/SqlSource.Tests/Settings/SettingValueTests.cs`, `QuerySettingsTests.cs`; `Parsing/SqlFileParserTests.cs`, `SqlMarkerReaderTests.cs`; `Generation/MSBuildSettingsTests.cs`; `Generator/AttributeConflictTests.cs`, `GeneratedSourceTests.cs`, `TypeDiagnosticsTests.cs`; create `Generator/SettingLevelTests.cs`, `Generation/AttributeSourceTests.cs`; `Package/BuildFileTests.cs`; `EndToEnd/EndToEndTests.cs`

**Interfaces:**
- Consumes: `SettingValue.TryReadChoice`, `SqlMarkerScope`, `MSBuildSettings.Read`, `SettingsLevel`, `QuerySettings`.
- Produces:
  - `OutputKind { Sql = 0, Models = 1, CodeGen = 2 }`; `SettingValue.IsDatabaseName(ReadOnlySpan<char>)`.
  - `SettingsLevel.Output` (`OutputKind?`) and `SettingsLevel.Database` (`string?`); `QuerySettings.Output` (`OutputKind`, default `CodeGen`), so `QuerySettings(GeneratorParameters Parameters, OutputKind Output)`.
  - `SqlMarkerKind.Database` and `SqlMarkerKind.Output`.
  - `AttributeSource.OutputProperty` (`"Output"`), `AttributeSource.OutputMetadataName` (`"SqlSource.GeneratorOutput"`), `AttributeSource.GeneratedTypes` (`ImmutableArray<string>` of metadata names).
  - `MSBuildSettings.OutputName` (`"SqlSourceOutput"`), and `SettingKeys.Output`.

- [ ] **Step 1: Write the failing tests**

`SettingValueTests`:

```csharp
[Theory]
[InlineData("sql", 0)]
[InlineData("Models", 1)]
[InlineData("codegen", 2)]
[InlineData("code-gen", 2)]
[InlineData("CodeGen", 2)]
public void TryReadChoice_Output_IsRead(string value, int expected)
{
    SettingValue.TryReadChoice<OutputKind>(value.AsSpan(), out var output).ShouldBeTrue();

    ((int)output).ShouldBe(expected);
}

[Theory]
[InlineData("billing", true)]
[InlineData("billing-v2", true)]
[InlineData("app_1.read", true)]
[InlineData("Größe", true)]
[InlineData("", false)]
[InlineData("two words", false)]
[InlineData("a/b", false)]
[InlineData("a=b", false)]
public void IsDatabaseName_OneWordOfLettersDigitsAndThreeMarks_IsAName(string value, bool expected) =>
    SettingValue.IsDatabaseName(value.AsSpan()).ShouldBe(expected);
```

`QuerySettingsTests`:

```csharp
[Fact]
public void Resolve_Output_IsCodeGenUnlessALevelSaysOtherwise()
{
    var sql = new SettingsLevel { Output = OutputKind.Sql };
    var models = new SettingsLevel { Output = OutputKind.Models };

    QuerySettings.Resolve(SettingsLevel.None, SettingsLevel.None, SettingsLevel.None, SettingsLevel.None).Output.ShouldBe(OutputKind.CodeGen);
    QuerySettings.Resolve(SettingsLevel.None, SettingsLevel.None, SettingsLevel.None, sql).Output.ShouldBe(OutputKind.Sql);
    QuerySettings.Resolve(SettingsLevel.None, SettingsLevel.None, models, sql).Output.ShouldBe(OutputKind.Models);
    QuerySettings.Resolve(SettingsLevel.None, sql, models, models).Output.ShouldBe(OutputKind.Sql);
    QuerySettings.Resolve(models, sql, sql, sql).Output.ShouldBe(OutputKind.Models);
}

[Fact]
public void Over_MembersAreTakenOneByOne()
{
    var query = new SettingsLevel { Output = OutputKind.Sql };
    var preamble = new SettingsLevel { Output = OutputKind.Models, Database = "billing" };

    query.Over(preamble).ShouldBe(new SettingsLevel { Output = OutputKind.Sql, Database = "billing" });
}
```

`SqlFileParserTests`:

```csharp
[Theory]
[InlineData("-- output: models\n-- database: billing\n-- name: Q\nSELECT 1\n", "Models", "billing")]
[InlineData("-- output: models\n-- name: Q\n-- output: SQL\n-- database: app\nSELECT 1\n", "Sql", "app")]
[InlineData("-- database: billing\n-- name: Q\n-- output: code-gen\nSELECT 1\n", "CodeGen", "billing")]
[InlineData("-- output: sql\n-- output: Sql\n-- database: a\n-- database: a\nSELECT 1\n", "Sql", "a")]
public void Parse_OutputAndDatabaseMarkers_AreCarriedWithTheQuerysOverThePreambles(
    string text,
    string output,
    string database
)
{
    var markers = Blocks(text).ShouldHaveSingleItem().Markers;

    markers.Output.ShouldBe(Enum.Parse<OutputKind>(output));
    markers.Database.ShouldBe(database);
    markers.Parameters.ShouldBeNull();
}

[Theory]
[InlineData("output", "models", "sql")]
[InlineData("database", "billing", "Billing")]
public void Parse_TwoValuesOfOneMarkerInOneScope_ConflictAtTheSecond(string word, string first, string second)
{
    var text = "-- name: Q\n-- " + word + ": " + first + "\n-- " + word + ": " + second + "\nSELECT 1\n";

    Errors(text)
        .ShouldBe([
            SqlParseError.Create(SqlParseErrorKind.ConflictingSettings, SpanOf(text, second), word + ": " + second),
        ]);
}

// A comment that only starts like a marker is a marker now, and says so at its value.
[Theory]
[InlineData("output", "the rows we need")]
[InlineData("output", "model")]
[InlineData("database", "see the wiki")]
[InlineData("database", "a/b")]
public void Parse_MarkerWithAValueItDoesNotTake_IsInvalidAtTheValue(string word, string value)
{
    var text = "-- name: Q\n-- " + word + ": " + value + "\nSELECT 1\n";

    Errors(text)
        .ShouldBe([
            SqlParseError.Create(SqlParseErrorKind.InvalidMarkerValue, SpanOf(text, value), word + ": " + value),
        ]);
}

[Theory]
[InlineData("output")]
[InlineData("database")]
public void Parse_MarkerWithoutAValue_IsInvalidAtTheMarker(string word)
{
    var text = "-- name: Q\n-- " + word + ":\nSELECT 1\n";

    Errors(text)
        .ShouldBe([
            SqlParseError.Create(SqlParseErrorKind.InvalidMarkerValue, SpanOf(text, "-- " + word + ":"), word + ":"),
        ]);
}
```

`SqlMarkerReaderTests`: `-- output: sql` and `-- database: a` are markers of their kinds; `-- outputs: x` is not.

`tests/SqlSource.Tests/Generation/AttributeSourceTests.cs`, which pins each of the generator's own enums against the text it emits:

```csharp
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Generation;

// The generator reads an enum argument of the attribute as a number and casts it to its own form of the enum.  The
// two must agree member by member, or a value a user writes would mean another.
public partial class AttributeSourceTests
{
    [Theory]
    [InlineData("SqlLocation", typeof(MemberPlacement))]
    [InlineData("GeneratorOutput", typeof(OutputKind))]
    public void EmittedEnum_HasTheMembersAndNumbersOfTheGeneratorsOwnForm(string emitted, Type own)
    {
        var body = Regex.Match(AttributeSource.Text, @"internal enum " + emitted + @"\s*\{(?<body>[^}]*)\}").Groups["body"].Value;

        var members = Member().Matches(body).Select(match => match.Groups["name"].Value + "=" + match.Groups["value"].Value);

        members.ShouldBe(Enum.GetValues(own).Cast<object>().Select(value => value + "=" + Convert.ToInt32(value)));
    }

    [Fact]
    public void GeneratedTypes_AreEveryTypeTheFileDeclares()
    {
        var declared = Regex
            .Matches(AttributeSource.Text, @"internal (?:enum|sealed class) (?<name>\w+)")
            .Select(match => "SqlSource." + match.Groups["name"].Value);

        AttributeSource.GeneratedTypes.ShouldBe(declared, ignoreOrder: true);
    }

    [GeneratedRegex(@"^\s*(?<name>\w+) = (?<value>\d+),", RegexOptions.Multiline)]
    private static partial Regex Member();
}
```

`MemberPlacement` is `internal`; if a `typeof` of it cannot be an attribute argument of a public method, make the theory `internal` or name the type by string and look it up from the generator's assembly.

`tests/SqlSource.Tests/Generator/SettingLevelTests.cs`:

```csharp
using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.Generator;

// The settings that have no effect yet are accepted at every level they have, and a value none of them takes is
// reported where it was written.
public class SettingLevelTests
{
    private const string Sql = "-- name: Q\nSELECT 1;\n";

    [Theory]
    [InlineData("SqlSourceOutput", "models")]
    [InlineData("SqlSourceOutput", " Code-Gen ")]
    [InlineData("SqlSourceDatabase", "not read by the generator, so never wrong")]
    public void Run_ValidSettingAsPropertyAndAsMetadata_ChangesNothing(string name, string value)
    {
        var plain = GeneratorHarness.Run(Source(null), new SqlFile("/app/Repo/Q.sql", Sql));

        var run = GeneratorHarness.Run(
            [new SourceFile(GeneratorHarness.SourcePath, Source(null))],
            [new SqlFile("/app/Repo/Q.sql", Sql, Metadata: new Dictionary<string, string> { [name] = value })],
            properties: new Dictionary<string, string?> { [name] = value }
        );

        run.Diagnostics.ShouldBeEmpty();
        run.Sources["App.Sample.g.cs"].ShouldBe(plain.Sources["App.Sample.g.cs"]);
    }

    [Theory]
    [InlineData("SqlSourceOutput", "model")]
    [InlineData("SqlSourceOutput", "sql models")]
    public void Run_InvalidSettingAsPropertyAndAsMetadata_IsReportedOnceWithoutAPosition(string name, string value)
    {
        var run = GeneratorHarness.Run(
            [new SourceFile(GeneratorHarness.SourcePath, Source(null))],
            [new SqlFile("/app/Repo/Q.sql", Sql, Metadata: new Dictionary<string, string> { [name] = value })],
            properties: new Dictionary<string, string?> { [name] = value }
        );

        run.Diagnostics.ShouldBe(["SQLSRC014 (1,1)-(1,1): '" + value + "' is not a valid value of " + name]);
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources.Keys.ShouldContain("App.Sample.g.cs");
    }

    [Theory]
    [InlineData("Output = GeneratorOutput.Sql")]
    [InlineData("Output = GeneratorOutput.Models")]
    [InlineData("Output = GeneratorOutput.CodeGen")]
    public void Run_ValidAttributeProperty_ChangesNothing(string argument)
    {
        var plain = GeneratorHarness.Run(Source(null), new SqlFile("/app/Repo/Q.sql", Sql));

        var run = GeneratorHarness.Run(Source(argument), new SqlFile("/app/Repo/Q.sql", Sql));

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        run.Sources["App.Sample.g.cs"].ShouldBe(plain.Sources["App.Sample.g.cs"]);
    }

    [Theory]
    [InlineData("Output = (GeneratorOutput)7", "7", "Output")]
    [InlineData("Output = (GeneratorOutput)(-1)", "-1", "Output")]
    public void Run_AttributePropertyWithANumberThatIsNoMember_IsAnErrorAtTheAttribute(
        string argument,
        string value,
        string property
    )
    {
        var run = GeneratorHarness.Run(Source(argument), new SqlFile("/app/Repo/Q.sql", Sql));

        var diagnostic = run.Diagnostics.ShouldHaveSingleItem();
        diagnostic.ShouldStartWith("SQLSRC006 /app/Repo/Sample.cs(5,2)-");
        diagnostic.ShouldEndWith(": '" + value + "' is not a valid value of " + property);
        run.Sources.Keys.ShouldNotContain("App.Sample.g.cs");
    }

    private static string Source(string? argument) =>
        "using SqlSource;\n\nnamespace App;\n\n[SqlSourceGenerate(SqlLocation = SqlLocation.Direct"
        + (argument is null ? string.Empty : ", " + argument)
        + ")]\npublic partial class Sample;\n";
}
```

`AttributeConflictTests`: where a test names the two generated types, it covers `SqlSource.GeneratorOutput` as well, by using it in the source that sees another assembly's internals: `[SqlSourceGenerate(Output = GeneratorOutput.Sql)]` gives no `CS0436`.  `GeneratedSourceTests`: the test that compiles the attribute file as C# 7.3 without warnings needs no change, and proves the new text.  `EndToEndTests.AttributeAndEnum_AreInternalTypesOfTheConsumingAssembly` checks every name of `AttributeSource.GeneratedTypes`.

`MSBuildSettingsTests`: a property `SqlSourceOutput` of `models` gives `Level.Output` `Models`; of `nope` gives no `Output` and `Invalid` of `[new InvalidSetting("SqlSourceOutput", "nope")]`; the same two for the metadata.

`BuildFileTests`: `Settings` gains `"SqlSourceOutput"` and `TrimmedOnly` gains `"SqlSourceDatabase"`.

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet build SqlSource.slnx`
Expected: FAIL, no `OutputKind`.

- [ ] **Step 3: Add the values and the members**

`src/SqlSource/Settings/OutputKind.cs`:

```csharp
namespace SqlSource.Settings;

/// <summary>
/// What SqlSource generates for a query.  The generator's own form of the <c>GeneratorOutput</c> enum that
/// <c>AttributeSource</c> emits, member for member.
/// </summary>
internal enum OutputKind
{
    /// <summary>The constant or the token method.</summary>
    Sql = 0,

    /// <summary><see cref="Sql" />, and the input and output types.</summary>
    Models = 1,

    /// <summary><see cref="Models" />, and the method that runs the query.</summary>
    CodeGen = 2,
}
```

`SettingValue`:

```csharp
    /// <summary>
    /// Whether <paramref name="value" /> names a database: one word of letters, digits, <c>-</c>, <c>_</c> and
    /// <c>.</c>.  The tool makes the name of an environment variable from it, so the rule is narrow on purpose.
    /// </summary>
    public static bool IsDatabaseName(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (!char.IsLetterOrDigit(character) && character is not ('-' or '_' or '.'))
            {
                return false;
            }
        }

        return !value.IsEmpty;
    }
```

`SettingsLevel`: two members, and `Over` takes each one by one.

```csharp
    /// <summary>What is generated for a query.</summary>
    public OutputKind? Output { get; init; }

    /// <summary>
    /// The name of the database a query belongs to.  The generator carries it from a marker and never reads it; the
    /// tool does.
    /// </summary>
    public string? Database { get; init; }
```

```csharp
        return new SettingsLevel
        {
            Parameters = Parameters ?? other.Parameters,
            Output = Output ?? other.Output,
            Database = Database ?? other.Database,
        };
```

`QuerySettings` becomes `QuerySettings(GeneratorParameters Parameters, OutputKind Output)`, with `/// <param name="Output">What is generated for the query.  It has no effect yet.</param>`, and `Resolve` adds

```csharp
            markers.Output ?? attribute.Output ?? metadata.Output ?? property.Output ?? OutputKind.CodeGen
```

- [ ] **Step 4: Read the two markers**

`SqlMarkerKind`: `Database` ("`-- database:` names the database of a query, or of every query of its file.") and `Output` ("`-- output:` says what is generated for a query, or for every query of its file.").  Keywords `("database:", ...)` and `("output:", ...)`.

`SqlMarkerScope`: both `IsAllowedIn` lists gain `or SqlMarkerKind.Database or SqlMarkerKind.Output`.  The scope holds its level as fields, and `Level` builds it:

```csharp
    private OutputKind? _output;
    private string? _database;

    /// <summary>What the scope's markers say about the settings.  The shared empty level when they say nothing.</summary>
    public SettingsLevel Level =>
        Generator.Parameters is null && _output is null && _database is null
            ? SettingsLevel.None
            : new SettingsLevel
            {
                Parameters = Generator.Parameters,
                Output = _output,
                Database = _database,
            };
```

`Read` gains

```csharp
            case SqlMarkerKind.Output:
                SetChoice(ref _output, marker);
                break;
            case SqlMarkerKind.Database:
                SetText(ref _database, marker, SettingValue.IsDatabaseName(Value(marker)));
                break;
```

and the helpers every later single-valued marker uses:

```csharp
    private ReadOnlySpan<char> Value(SqlMarker marker) => text.AsSpan(marker.ValueSpan.Start, marker.ValueSpan.Length);

    // A value from a fixed list.  The same value twice is fine; another value is a conflict, and the first stands.
    private void SetChoice<T>(ref T? field, SqlMarker marker)
        where T : struct, Enum
    {
        if (!SettingValue.TryReadChoice<T>(Value(marker), out var value))
        {
            AddInvalid(marker);
        }
        else if (field is { } existing && !EqualityComparer<T>.Default.Equals(existing, value))
        {
            AddConflict(marker);
        }
        else
        {
            field = value;
        }
    }

    // A value that is text, taken as written.  It is compared as written, so "billing" and "Billing" are two.
    private void SetText(ref string? field, SqlMarker marker, bool isValid)
    {
        if (!isValid)
        {
            AddInvalid(marker);
            return;
        }

        var value = Value(marker).ToString();
        if (field is not null && !string.Equals(field, value, StringComparison.Ordinal))
        {
            AddConflict(marker);
        }
        else
        {
            field = value;
        }
    }
```

Add `using SqlSource.Settings;`.

- [ ] **Step 5: Add `GeneratorOutput` and the attribute's `Output`**

`AttributeSource`:

```csharp
    public const string OutputMetadataName = "SqlSource.GeneratorOutput";

    public const string OutputProperty = "Output";

    /// <summary>
    /// The metadata name of every type that <see cref="Text" /> declares.  A project that sees the internals of
    /// another one that uses SqlSource sees each of them twice, and <c>AttributeConflictSuppressor</c> turns the
    /// compiler's warning off for exactly these.
    /// </summary>
    public static ImmutableArray<string> GeneratedTypes { get; } =
        ImmutableArray.Create(AttributeMetadataName, LocationMetadataName, OutputMetadataName);
```

In `Text`: the first comment says "sees these types twice"; after the `SqlLocation` enum add

```csharp
            /// <summary>
            /// What SqlSource generates for a query.
            /// </summary>
            internal enum GeneratorOutput
            {
                /// <summary>
                /// The SQL: a constant, or a method when the query has a token.
                /// </summary>
                Sql = 0,

                /// <summary>
                /// The SQL, and a type for the query's parameters and one for its result.
                /// </summary>
                Models = 1,

                /// <summary>
                /// The SQL, the types, and a method that runs the query.
                /// </summary>
                CodeGen = 2,
            }
```

and after the `SqlLocation` property

```csharp
                /// <summary>
                /// What is generated for the queries of the type's files.  The default is
                /// <see cref="global::SqlSource.GeneratorOutput.CodeGen" />.  A marker in a file comes first.
                /// </summary>
                public GeneratorOutput Output { get; set; }
```

The comment above `Text` on the suppressor says "A type that is added here is added to `GeneratedTypes`."

`AttributeConflictSuppressor.ReportSuppressions`: resolve every name of `AttributeSource.GeneratedTypes` with `assembly.GetTypeByMetadataName`, keep the ones that are not null in a list, and suppress when `UsedType` is one of them by `SymbolEqualityComparer.Default`.

`TargetTypeReader.ReadSettings` gains a generic reader of an enum argument, which Task 11 reuses:

```csharp
        if (ReadChoice<OutputKind>(attribute, AttributeSource.OutputProperty, attributeLocation, diagnostics) is { } output)
        {
            level = level with { Output = output };
        }
```

```csharp
    // An enum argument arrives as its number.  A number that is no member of the generator's own form of the enum
    // was written with a cast, and is reported.  An argument that is not a constant has no value here; the compiler
    // reports it.
    private static T? ReadChoice<T>(
        AttributeData attribute,
        string property,
        LocationInfo attributeLocation,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics
    )
        where T : struct, Enum
    {
        if (GetNamedArgument(attribute, property) is not { Value: int value })
        {
            return null;
        }

        if (Enum.IsDefined(typeof(T), value))
        {
            return (T)Enum.ToObject(typeof(T), value);
        }

        diagnostics.Add(
            DiagnosticInfo.Create(
                SqlDiagnostics.InvalidAttributeValue,
                attributeLocation,
                value.ToString(CultureInfo.InvariantCulture),
                property
            )
        );
        return null;
    }
```

- [ ] **Step 6: Read the property and the metadata**

`MSBuildSettings`: `public const string OutputName = "SqlSourceOutput";`, `SettingKeys.Output`, and in `Read`:

```csharp
        if (ReadChoice<OutputKind>(options, keys.Output, OutputName, ref invalid) is { } output)
        {
            level = level with { Output = output };
        }
```

```csharp
    private static T? ReadChoice<T>(
        AnalyzerConfigOptions options,
        string key,
        string name,
        ref List<InvalidSetting>? invalid
    )
        where T : struct, Enum
    {
        if (!options.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (SettingValue.TryReadChoice<T>(value.AsSpan().Trim(), out var choice))
        {
            return choice;
        }

        (invalid ??= []).Add(new InvalidSetting(name, value.Trim()));
        return null;
    }
```

`SqlSource.props`: `<CompilerVisibleProperty Include="SqlSourceOutput" />` and `<CompilerVisibleItemMetadata Include="SqlSourceSettingsFile" MetadataName="SqlSourceOutput" />`.  `SqlSource.targets`: `SqlSourceTrimProperties` trims `SqlSourceOutput` and `SqlSourceDatabase`; `SqlSourceTrimMetadataOfFiles` gains both names in `Outputs`, in the property group, in the item group's condition and as conditional metadata; `SqlSourceCollectSettingsFiles` gains `%(AdditionalFiles.SqlSourceOutput)` in its condition, and not the database, which the compiler is not shown.  Add to the comment of the props: "`SqlSourceDatabase` is trimmed and is not listed: the tool reads it, and the generator does not."

- [ ] **Step 7: Run everything**

Run: `dotnet test --solution SqlSource.slnx`
Expected: PASS.

- [ ] **Step 8: Update the documents**

`README.md`:

- What reaches the generated SQL, first bullet: "Every marker line is removed (see Markers, below)."
- A section after Summaries, which later tasks extend:

```markdown
### Markers

A line comment that starts its line and reads `-- word: rest`, where the word is one SqlSource knows, is a marker.  Nothing else in a comment is read.  Words are matched ignoring case.  A marker that describes one query goes inside it, after its `-- name:` line and before its last SQL; one that describes the file goes before the first `-- name:` line.  A file with no `-- name:` line is one query and takes both kinds.

| Marker | Before the first query | Inside a query | Value |
|----|----|----|----|
| `name` | | starts one | A name, and optionally `->` and a shape |
| `summary` | no | yes | Text |
| `dialect` | yes | no | A dialect and its options (see Dialects) |
| `generator` | yes | yes | Generator parameters |
| `param` | no | yes | `@name`, a type, `null` or `not null` (see Parameters) |
| `token` | no | yes | One `{{name:default}}` (see Tokens) |
| `token-ignore` | no | yes | A token's name |
| `database` | yes | yes | A database's name |
| `output` | yes | yes | `sql`, `models` or `codegen` |

A marker inside a query wins over the same marker before the first query.  Writing a marker twice in one place is fine when the value is the same, and the error [SQLSRC112](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc112) when it is not.  A comment of yours that happens to start with one of these words and a colon is read as a marker: reword it.
```

- A section before `## Errors`:

```markdown
## Settings for models and methods

These settings are read and checked today, and have no effect yet.  They belong to what later releases generate from a query: types for its parameters and its result, and a method that runs it.  They are listed so that a value SqlSource rejects can be looked up.

| Setting | Values | Property and metadata | Attribute | Marker |
|----|----|----|----|----|
| Output | `sql`, `models`, `codegen` | `SqlSourceOutput` | `Output` | `-- output:` |
| Database | A name: letters, digits, `-`, `_`, `.` | `SqlSourceDatabase` | | `-- database:` |

A value from a list is matched ignoring case, hyphens and spaces: `CodeGen`, `codegen` and `code-gen` are the same.  The most specific place wins: a marker inside a query, then one before the file's first query, then the attribute, then the metadata of the file's item, then the property.
```

- The attribute, a section after Parameters: "### Output", one sentence: "`Output` says what is generated for the queries of the type's files; see Settings for models and methods, below."
- Projects that share internals: "SqlSource adds the attribute and its enums to each project that uses it, as internal types.  ...  sees each of them twice ...  SqlSource turns that warning off for these types only".

`docs/diagnostics.md`: `SQLSRC111` gains "`-- output:` takes `sql`, `models` or `codegen`.  `-- database:` takes one word of letters, digits, `-`, `_` and `.`."  `SQLSRC006` and `SQLSRC014` each gain a sentence that names `Output` and `SqlSourceOutput` with their three values.  `SQLSRC116`'s section lists which markers go where, as the README's table does.

`src/SqlSource/AGENTS.md`: "the two types of `Generation/AttributeSource.cs`" becomes "the types that `AttributeSource.GeneratedTypes` lists", in the bullet on `SQLSRC901`; under `Settings/` add "**The generator's own form of an emitted enum has its numbers.**  `OutputKind` stands for `GeneratorOutput` as `MemberPlacement` stands for `SqlLocation`; `tests/SqlSource.Tests/Generation/AttributeSourceTests.cs` compares each pair."

`docs/tech-debt/TD-0006-attribute-conflicts-across-friend-assemblies.md`: wherever it says two types, it says the types of `AttributeSource.GeneratedTypes`, and names `GeneratorOutput` beside `SqlLocation`.

- [ ] **Step 9: Commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add -A && git commit -m "Accept a query's output and database at every level they have

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: The model settings, the collection type and the method location

**Files:**
- Create: `src/SqlSource/Settings/ModelKind.cs`, `CollectionKind.cs`, `MethodPlacement.cs`
- Modify: `src/SqlSource/Settings/SettingValue.cs`, `SettingsLevel.cs`, `QuerySettings.cs`
- Modify: `src/SqlSource/Parsing/SqlMarkerKind.cs`, `SqlMarkerReader.cs`, `SqlMarkerScope.cs`, `SqlParseErrorKind.cs`, `SqlFileParser.cs`, `SqlBlock.cs`
- Modify: `src/SqlSource/Generation/AttributeSource.cs`, `TargetType.cs`, `TargetTypeReader.cs`, `MSBuildSettings.cs`, `SqlQuery.cs`, `SqlFileReader.cs`
- Modify: `src/SqlSource/Diagnostics/SqlDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`
- Modify: `src/SqlSource/build/SqlSource.props`, `SqlSource.targets`
- Modify: `README.md`, `docs/diagnostics.md`, `docs/tech-debt/TD-0006-attribute-conflicts-across-friend-assemblies.md`
- Test: the files of Task 10's tests, each extended

**Interfaces:**
- Consumes: Task 10's `SetChoice`, `SetText`, `ReadChoice<T>` in both readers, `AttributeSource.GeneratedTypes`.
- Produces:
  - `ModelKind { Record, SealedRecord, Class, SealedClass }`; `CollectionKind { IEnumerable, ICollection, IReadOnlyCollection, IList, IReadOnlyList, Array, List, ImmutableArray, ImmutableList, IImmutableList }`; `MethodPlacement { ExtensionClass, Public, Internal, Private }`; each numbered from zero in that order.
  - `SettingValue.IsSuffix`, `IsNamespace` and `IsTypeName`, each `(ReadOnlySpan<char>)` to `bool`.
  - `SettingsLevel` members `InputModelSuffix`, `OutputModelSuffix`, `ModelNamespace` (`string?`), `InputModelType`, `OutputModelType` (`ModelKind?`), `CollectionType` (`CollectionKind?`).
  - `QuerySettings(Parameters, Output, InputModelSuffix, OutputModelSuffix, ModelNamespace, InputModelType, OutputModelType, CollectionType)`, with the defaults `Params`, `Dto`, null, `SealedRecord`, `SealedRecord`, `Array`.
  - `SqlMarkerKind` members `InputModelSuffix`, `OutputModelSuffix`, `ModelNamespace`, `InputModelType`, `OutputModelType`, `InputModel`, `OutputModel`, `CollectionType`.
  - `SqlMarkerScope.InputModel` and `OutputModel`: `(string Name, SqlMarker Marker)?`.
  - `SqlBlock.InputModelName`, `OutputModelName` and the same on `SqlQuery` (`string?`), the last two members.
  - `SqlParseErrorKind.InputModelWithoutParameters` (`SQLSRC119`, no argument).
  - `TargetType.MethodPlacement` (`MethodPlacement`), after `Placement`.
  - The emitted enums `GeneratorModelType`, `GeneratorCollectionType`, `MethodLocation`, and the attribute's `InputModelSuffix`, `OutputModelSuffix`, `ModelNamespace`, `InputModelType`, `OutputModelType`, `MethodLocation`, `CollectionType`.

- [ ] **Step 1: Write the failing tests**

`SettingValueTests`:

```csharp
[Theory]
[InlineData("record", 0)]
[InlineData("sealed record", 1)]
[InlineData("Sealed-Record", 1)]
[InlineData("SealedRecord", 1)]
[InlineData("class", 2)]
[InlineData("sealed class", 3)]
public void TryReadChoice_ModelType_IsRead(string value, int expected)
{
    SettingValue.TryReadChoice<ModelKind>(value.AsSpan(), out var kind).ShouldBeTrue();

    ((int)kind).ShouldBe(expected);
}

[Theory]
[InlineData("IEnumerable", 0)]
[InlineData("ilist", 3)]
[InlineData("list", 6)]
[InlineData("Array", 5)]
[InlineData("immutable-array", 7)]
[InlineData("ImmutableList", 8)]
[InlineData("IImmutableList", 9)]
public void TryReadChoice_CollectionType_IsRead(string value, int expected)
{
    SettingValue.TryReadChoice<CollectionKind>(value.AsSpan(), out var kind).ShouldBeTrue();

    ((int)kind).ShouldBe(expected);
}

[Theory]
[InlineData("Dto", true)]
[InlineData("_Row2", true)]
[InlineData("2", true)]
[InlineData("", false)]
[InlineData("A B", false)]
[InlineData("A-B", false)]
[InlineData("A.B", false)]
public void IsSuffix_CharactersThatCanFollowTheFirstOfAnIdentifier_IsASuffix(string value, bool expected) =>
    SettingValue.IsSuffix(value.AsSpan()).ShouldBe(expected);

[Theory]
[InlineData("App", true)]
[InlineData("App.Data.Models", true)]
[InlineData("", false)]
[InlineData("App.", false)]
[InlineData(".App", false)]
[InlineData("App..Data", false)]
[InlineData("App.class", false)]
[InlineData("App.1st", false)]
[InlineData("global::App", false)]
public void IsNamespace_IdentifiersJoinedByPeriods_IsANamespace(string value, bool expected) =>
    SettingValue.IsNamespace(value.AsSpan()).ShouldBe(expected);

[Theory]
[InlineData("UserRow", true)]
[InlineData("App.Models.UserRow", true)]
[InlineData("class", false)]
[InlineData("User Row", false)]
[InlineData("UserRow<T>", false)]
public void IsTypeName_AnIdentifierOrAFullName_IsATypeName(string value, bool expected) =>
    SettingValue.IsTypeName(value.AsSpan()).ShouldBe(expected);
```

`QuerySettingsTests`:

```csharp
[Fact]
public void Resolve_ModelSettings_HaveTheirDefaults()
{
    var settings = QuerySettings.Resolve(SettingsLevel.None, SettingsLevel.None, SettingsLevel.None, SettingsLevel.None);

    settings.InputModelSuffix.ShouldBe("Params");
    settings.OutputModelSuffix.ShouldBe("Dto");
    settings.ModelNamespace.ShouldBeNull();
    settings.InputModelType.ShouldBe(ModelKind.SealedRecord);
    settings.OutputModelType.ShouldBe(ModelKind.SealedRecord);
    settings.CollectionType.ShouldBe(CollectionKind.Array);
}

[Fact]
public void Resolve_EachModelSetting_ComesFromTheMostSpecificLevelThatHasIt()
{
    var property = new SettingsLevel
    {
        InputModelSuffix = "In",
        OutputModelSuffix = "Out",
        ModelNamespace = "P",
        InputModelType = ModelKind.Class,
        OutputModelType = ModelKind.Class,
        CollectionType = CollectionKind.List,
    };
    var metadata = new SettingsLevel { OutputModelSuffix = "Row", ModelNamespace = "M" };
    var attribute = new SettingsLevel { ModelNamespace = "A", InputModelType = ModelKind.Record };
    var markers = new SettingsLevel { CollectionType = CollectionKind.IReadOnlyList };

    var settings = QuerySettings.Resolve(markers, attribute, metadata, property);

    settings.InputModelSuffix.ShouldBe("In");
    settings.OutputModelSuffix.ShouldBe("Row");
    settings.ModelNamespace.ShouldBe("A");
    settings.InputModelType.ShouldBe(ModelKind.Record);
    settings.OutputModelType.ShouldBe(ModelKind.Class);
    settings.CollectionType.ShouldBe(CollectionKind.IReadOnlyList);
}
```

`SqlFileParserTests`:

```csharp
[Fact]
public void Parse_ModelMarkers_AreCarriedWithTheQuerysOverThePreambles()
{
    const string Text =
        "-- input-model-suffix: Args\n-- output-model-suffix: Row\n-- model-namespace: App.Models\n"
        + "-- input-model-type: class\n-- output-model-type: record\n-- collection-type: list\n"
        + "-- name: Q\n-- output-model-type: sealed class\n-- collection-type: IReadOnlyList\n"
        + "-- input-model: FindArgs\n-- output-model: App.Shared.UserRow\nSELECT @a\n";

    var block = Blocks(Text).ShouldHaveSingleItem();

    block.Markers.ShouldBe(
        new SettingsLevel
        {
            InputModelSuffix = "Args",
            OutputModelSuffix = "Row",
            ModelNamespace = "App.Models",
            InputModelType = ModelKind.Class,
            OutputModelType = ModelKind.SealedClass,
            CollectionType = CollectionKind.IReadOnlyList,
        }
    );
    block.InputModelName.ShouldBe("FindArgs");
    block.OutputModelName.ShouldBe("App.Shared.UserRow");
}

[Theory]
// A marker for the file, written inside a query; and a marker for a query, written before the first one.
[InlineData("input-model-suffix", "Args", false, "before the file's first query")]
[InlineData("output-model-suffix", "Row", false, "before the file's first query")]
[InlineData("model-namespace", "App", false, "before the file's first query")]
[InlineData("input-model", "FindArgs", true, "inside a query")]
[InlineData("output-model", "UserRow", true, "inside a query")]
public void Parse_ModelMarkerInTheWrongScope_IsNotAllowedThere(string word, string value, bool inPreamble, string allowed)
{
    var marker = "-- " + word + ": " + value;
    var text = inPreamble ? marker + "\n-- name: Q\nSELECT @a\n" : "-- name: Q\n" + marker + "\nSELECT @a\n";

    Errors(text)
        .ShouldBe([
            SqlParseError.Create(
                SqlParseErrorKind.MarkerNotAllowedHere,
                SpanOf(text, marker),
                word,
                allowed
            ),
        ]);
}

[Theory]
[InlineData("input-model-suffix", "A B")]
[InlineData("output-model-suffix", "A.B")]
[InlineData("model-namespace", "App.")]
[InlineData("input-model-type", "struct")]
[InlineData("output-model-type", "sealed")]
[InlineData("collection-type", "HashSet")]
[InlineData("input-model", "class")]
[InlineData("output-model", "User Row")]
public void Parse_ModelMarkerWithAValueItDoesNotTake_IsInvalidAtTheValue(string word, string value)
{
    var text = "-- " + word + ": " + value + "\nSELECT @a\n";

    Errors(text)
        .ShouldBe([
            SqlParseError.Create(SqlParseErrorKind.InvalidMarkerValue, SpanOf(text, value), word + ": " + value),
        ]);
}

[Fact]
public void Parse_TwoNamesForOneModel_ConflictAtTheSecond()
{
    const string Text = "-- name: Q\n-- output-model: A\n-- output-model: B\nSELECT 1\n";

    Errors(Text)
        .ShouldBe([
            SqlParseError.Create(
                SqlParseErrorKind.ConflictingSettings,
                new TextSpan(Text.IndexOf("-- output-model: B", StringComparison.Ordinal) + 17, 1),
                "output-model: B"
            ),
        ]);
}

[Fact]
public void Parse_InputModelOnAQueryWithoutParameters_IsAnError()
{
    const string Text = "-- name: Q\n-- input-model: FindArgs\nSELECT 1 {{f}}\n";

    Errors(Text)
        .ShouldBe([
            SqlParseError.Create(SqlParseErrorKind.InputModelWithoutParameters, SpanOf(Text, "-- input-model: FindArgs")),
        ]);
}

// A parameter that only a marker declares is a parameter.
[Fact]
public void Parse_InputModelOnAQueryWithADeclaredOnlyParameter_IsFine() =>
    Blocks("-- name: Q\n-- param: @page int\n-- input-model: FindArgs\nSELECT 1 {{f}}\n")
        .ShouldHaveSingleItem()
        .InputModelName.ShouldBe("FindArgs");
```

`SqlMarkerReaderTests`: each of the eight words gives its kind, and `-- input-model: A`, `-- input-model-suffix: A` and `-- input-model-type: class` give three different kinds.

`AttributeSourceTests`: the theory gains `("GeneratorModelType", typeof(ModelKind))`, `("GeneratorCollectionType", typeof(CollectionKind))` and `("MethodLocation", typeof(MethodPlacement))`.

`SettingLevelTests`: the valid property-and-metadata theory gains

```csharp
    [InlineData("SqlSourceInputModelSuffix", "Args")]
    [InlineData("SqlSourceOutputModelSuffix", "Row")]
    [InlineData("SqlSourceModelNamespace", "App.Models")]
    [InlineData("SqlSourceInputModelType", "sealed record")]
    [InlineData("SqlSourceOutputModelType", "Class")]
    [InlineData("SqlSourceCollectionType", "IReadOnlyList")]
```

the invalid one gains

```csharp
    [InlineData("SqlSourceInputModelSuffix", "A B")]
    [InlineData("SqlSourceOutputModelSuffix", "A.B")]
    [InlineData("SqlSourceModelNamespace", "App.")]
    [InlineData("SqlSourceInputModelType", "struct")]
    [InlineData("SqlSourceOutputModelType", "sealed")]
    [InlineData("SqlSourceCollectionType", "HashSet")]
```

the valid attribute theory gains

```csharp
    [InlineData("InputModelSuffix = \"Args\", OutputModelSuffix = \"Row\", ModelNamespace = \"App.Models\"")]
    [InlineData("InputModelType = GeneratorModelType.Class, OutputModelType = GeneratorModelType.SealedRecord")]
    [InlineData("CollectionType = GeneratorCollectionType.IReadOnlyList")]
    [InlineData("MethodLocation = MethodLocation.Internal")]
    [InlineData("InputModelSuffix = \"\", ModelNamespace = \"  \"")]
```

and the invalid attribute theory gains

```csharp
    [InlineData("InputModelType = (GeneratorModelType)4", "4", "InputModelType")]
    [InlineData("OutputModelType = (GeneratorModelType)9", "9", "OutputModelType")]
    [InlineData("CollectionType = (GeneratorCollectionType)10", "10", "CollectionType")]
    [InlineData("MethodLocation = (MethodLocation)4", "4", "MethodLocation")]
    [InlineData("InputModelSuffix = \"A B\"", "A B", "InputModelSuffix")]
    [InlineData("OutputModelSuffix = \"A.B\"", "A.B", "OutputModelSuffix")]
    [InlineData("ModelNamespace = \"App.\"", "App.", "ModelNamespace")]
```

Add a test that the method location has no MSBuild form and no marker:

```csharp
[Fact]
public void Run_MethodLocationAsAPropertyOrAMarker_IsNotASetting()
{
    var run = GeneratorHarness.Run(
        [new SourceFile(GeneratorHarness.SourcePath, Source(null))],
        [new SqlFile("/app/Repo/Q.sql", "-- name: Q\n-- method-location: public\nSELECT 1; -- c\n")],
        properties: new Dictionary<string, string?> { ["SqlSourceMethodLocation"] = "nope" }
    );

    // The property is not read, and the line is an ordinary comment.
    run.Diagnostics.ShouldBeEmpty();
    run.Sources["App.Sample.g.cs"].ShouldContain("\"SELECT 1;\"");
}
```

`BuildFileTests.Settings` gains the six names.  `MSBuildSettingsTests`: one valid and one invalid value of each of the six, as property and as metadata, in a theory over `(name, valid, invalid)`, asserting the level's member through `QuerySettings.Resolve(SettingsLevel.None, SettingsLevel.None, SettingsLevel.None, level)` and the `InvalidSetting`.  `SqlDiagnosticsTests`: the new kind and `SQLSRC119`.  `AttributeConflictTests`: the source that sees another assembly's internals uses each of the three new enums, and gets no `CS0436`.

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet build SqlSource.slnx`
Expected: FAIL, no `ModelKind`.

- [ ] **Step 3: Add the enums and the readers**

`src/SqlSource/Settings/ModelKind.cs`:

```csharp
namespace SqlSource.Settings;

/// <summary>
/// The shape of a generated model.  The generator's own form of <c>GeneratorModelType</c>, member for member.
/// </summary>
internal enum ModelKind
{
    Record = 0,
    SealedRecord = 1,
    Class = 2,
    SealedClass = 3,
}
```

`src/SqlSource/Settings/CollectionKind.cs`:

```csharp
namespace SqlSource.Settings;

/// <summary>
/// The type a generated method returns many rows in.  The generator's own form of <c>GeneratorCollectionType</c>,
/// member for member.
/// </summary>
internal enum CollectionKind
{
    IEnumerable = 0,
    ICollection = 1,
    IReadOnlyCollection = 2,
    IList = 3,
    IReadOnlyList = 4,
    Array = 5,
    List = 6,
    ImmutableArray = 7,
    ImmutableList = 8,
    IImmutableList = 9,
}
```

`src/SqlSource/Settings/MethodPlacement.cs`:

```csharp
namespace SqlSource.Settings;

/// <summary>
/// Where a type's generated methods go.  The generator's own form of <c>MethodLocation</c>, member for member.  It
/// is set by the attribute alone, as <c>SqlLocation</c> is: it has no MSBuild property, no metadata and no marker.
/// </summary>
internal enum MethodPlacement
{
    ExtensionClass = 0,
    Public = 1,
    Internal = 2,
    Private = 3,
}
```

If the build asks for documentation on each member, give each one line saying what it is.

`SettingValue`:

```csharp
    /// <summary>
    /// Whether <paramref name="value" /> can end a type's name: one or more characters that can follow the first of
    /// a C# identifier.
    /// </summary>
    public static bool IsSuffix(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (
                !SyntaxFacts.IsIdentifierPartCharacter(character)
                || char.GetUnicodeCategory(character) == UnicodeCategory.Format
            )
            {
                return false;
            }
        }

        return !value.IsEmpty;
    }

    /// <summary>
    /// Whether <paramref name="value" /> is a namespace: identifiers that are not reserved keywords, joined by
    /// periods.
    /// </summary>
    public static bool IsNamespace(ReadOnlySpan<char> value)
    {
        while (true)
        {
            var period = value.IndexOf('.');
            if (!SqlIdentifier.IsUsableName((period < 0 ? value : value.Slice(0, period)).ToString()))
            {
                return false;
            }

            if (period < 0)
            {
                return true;
            }

            value = value.Slice(period + 1);
        }
    }

    /// <summary>
    /// Whether <paramref name="value" /> names a type: an identifier that is not a reserved keyword, alone or after
    /// a namespace and a period.  With a period it is a full name.
    /// </summary>
    public static bool IsTypeName(ReadOnlySpan<char> value) => IsNamespace(value);
```

with `using System.Globalization;`, `using Microsoft.CodeAnalysis.CSharp;` and `using SqlSource.Parsing;`.  `IsTypeName` has the grammar of a namespace and a name of its own, because what it names is not one.  A part is turned into a string to be checked, which happens only where a value is being validated.

`SettingsLevel`: the six members, each with a line of documentation, and each in `Over`.  `QuerySettings`:

```csharp
/// <param name="Parameters">The list of generator parameters.</param>
/// <param name="Output">What is generated for the query.</param>
/// <param name="InputModelSuffix">What ends the name of the type of the query's parameters.</param>
/// <param name="OutputModelSuffix">What ends the name of the type of a row of the query's result.</param>
/// <param name="ModelNamespace">The namespace of the query's models, or null for the namespace of the type.</param>
/// <param name="InputModelType">The shape of the type of the query's parameters.</param>
/// <param name="OutputModelType">The shape of the type of a row.</param>
/// <param name="CollectionType">The type a method returns many rows in.</param>
internal readonly record struct QuerySettings(
    GeneratorParameters Parameters,
    OutputKind Output,
    string InputModelSuffix,
    string OutputModelSuffix,
    string? ModelNamespace,
    ModelKind InputModelType,
    ModelKind OutputModelType,
    CollectionKind CollectionType
)
```

with `Resolve` taking each member the same way, and the defaults as constants `DefaultInputModelSuffix = "Params"` and `DefaultOutputModelSuffix = "Dto"`.  Only `Parameters` has an effect; a comment on `Resolve` says so.

- [ ] **Step 4: Read the eight markers**

`SqlMarkerKind` and `SqlMarkerReader.Keywords` gain the eight, each documented in one line.  `SqlMarkerScope`:

- `IsAllowedInPreamble` gains `InputModelSuffix`, `OutputModelSuffix`, `ModelNamespace`, `InputModelType`, `OutputModelType`, `CollectionType`.
- `IsAllowedInQuery` gains `InputModelType`, `OutputModelType`, `CollectionType`, `InputModel`, `OutputModel`.
- Six fields for the level, set by `SetText` with `SettingValue.IsSuffix` or `IsNamespace`, or by `SetChoice`; `Level` builds them all, and is the shared empty level only when every field is null.
- Two more:

```csharp
    private string? _inputModel;
    private string? _outputModel;

    /// <summary>The name a <c>-- input-model:</c> marker gives, with the first such marker, or null.</summary>
    public (string Name, SqlMarker Marker)? InputModel { get; private set; }

    /// <summary>The name a <c>-- output-model:</c> marker gives, or null.</summary>
    public string? OutputModel => _outputModel;
```

  In `Read`:

```csharp
            case SqlMarkerKind.InputModel:
                SetText(ref _inputModel, marker, SettingValue.IsTypeName(Value(marker)));
                if (_inputModel is { } inputModel && InputModel is null)
                {
                    InputModel = (inputModel, marker);
                }

                break;
            case SqlMarkerKind.OutputModel:
                SetText(ref _outputModel, marker, SettingValue.IsTypeName(Value(marker)));
                break;
```

`SqlParseErrorKind`, at the end: `InputModelWithoutParameters`, documented "A `-- input-model:` marker is on a query that has no parameters.  No argument."  `SqlDiagnostics`, after `UndeclaredParameter`, in `All` and `ForParseError`:

```csharp
    public static readonly DiagnosticDescriptor InputModelWithoutParameters = new(
        id: "SQLSRC119",
        title: "Query has no parameters",
        messageFormat: "The query has no parameters, so '-- input-model:' names nothing",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "sqlsrc119",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );
```

`AnalyzerReleases.Unshipped.md`: `SQLSRC119 | SqlSource | Error | Query has no parameters`.

`SqlFileParser.ReadBlock`, after the parameter list is built:

```csharp
            if (scope.InputModel is { } inputModel && parameters.Count == 0)
            {
                AddError(SqlParseErrorKind.InputModelWithoutParameters, inputModel.Marker.Span);
            }
```

and `new SqlBlock(...)` ends with `scope.InputModel?.Name, scope.OutputModel`.  `SqlBlock` and `SqlQuery` gain the two members last, documented "The full name or the name a `-- input-model:` marker gives the type of the query's parameters, or null." and the same for the output; `SqlFileReader` copies them, and the hand-built models in the tests gain two `null`s.

- [ ] **Step 5: Add the enums and the properties to the attribute**

`AttributeSource`: metadata-name constants `ModelTypeMetadataName = "SqlSource.GeneratorModelType"`, `CollectionTypeMetadataName = "SqlSource.GeneratorCollectionType"`, `MethodLocationMetadataName = "SqlSource.MethodLocation"`, all three in `GeneratedTypes`; property-name constants for the seven properties.  In `Text`, after `GeneratorOutput`:

```csharp
            /// <summary>
            /// The shape of a type that SqlSource generates for a query.
            /// </summary>
            internal enum GeneratorModelType
            {
                /// <summary>
                /// A positional record.
                /// </summary>
                Record = 0,

                /// <summary>
                /// A sealed positional record.
                /// </summary>
                SealedRecord = 1,

                /// <summary>
                /// A class with a property for each member.
                /// </summary>
                Class = 2,

                /// <summary>
                /// A sealed class with a property for each member.
                /// </summary>
                SealedClass = 3,
            }

            /// <summary>
            /// The type a generated method returns many rows in.  Every one is filled before the method returns.
            /// </summary>
            internal enum GeneratorCollectionType
            {
                /// <summary>
                /// <c>IEnumerable&lt;T&gt;</c>, over an array.
                /// </summary>
                IEnumerable = 0,

                /// <summary>
                /// <c>ICollection&lt;T&gt;</c>, over a list.
                /// </summary>
                ICollection = 1,

                /// <summary>
                /// <c>IReadOnlyCollection&lt;T&gt;</c>, over an array.
                /// </summary>
                IReadOnlyCollection = 2,

                /// <summary>
                /// <c>IList&lt;T&gt;</c>, over a list.
                /// </summary>
                IList = 3,

                /// <summary>
                /// <c>IReadOnlyList&lt;T&gt;</c>, over an array.
                /// </summary>
                IReadOnlyList = 4,

                /// <summary>
                /// An array.
                /// </summary>
                Array = 5,

                /// <summary>
                /// <c>List&lt;T&gt;</c>.
                /// </summary>
                List = 6,

                /// <summary>
                /// <c>ImmutableArray&lt;T&gt;</c>.
                /// </summary>
                ImmutableArray = 7,

                /// <summary>
                /// <c>ImmutableList&lt;T&gt;</c>.
                /// </summary>
                ImmutableList = 8,

                /// <summary>
                /// <c>IImmutableList&lt;T&gt;</c>, over an immutable list.
                /// </summary>
                IImmutableList = 9,
            }

            /// <summary>
            /// Where the methods that run the queries of a type go.
            /// </summary>
            internal enum MethodLocation
            {
                /// <summary>
                /// In a generated static class beside the type, as extension methods of <c>DbConnection</c>.
                /// </summary>
                ExtensionClass = 0,

                /// <summary>
                /// On the type, as public static methods.
                /// </summary>
                Public = 1,

                /// <summary>
                /// On the type, as internal static methods.
                /// </summary>
                Internal = 2,

                /// <summary>
                /// On the type, as private static methods.
                /// </summary>
                Private = 3,
            }
```

and in the attribute class, after `Output`:

```csharp
                /// <summary>
                /// What ends the name of the type of a query's parameters.  The default is <c>Params</c>.
                /// </summary>
                public string InputModelSuffix { get; set; }

                /// <summary>
                /// What ends the name of the type of a row of a query's result.  The default is <c>Dto</c>.
                /// </summary>
                public string OutputModelSuffix { get; set; }

                /// <summary>
                /// The namespace of the types generated for the queries.  The default is the namespace of this
                /// type.
                /// </summary>
                public string ModelNamespace { get; set; }

                /// <summary>
                /// The shape of the type of a query's parameters.  The default is
                /// <see cref="global::SqlSource.GeneratorModelType.SealedRecord" />.
                /// </summary>
                public GeneratorModelType InputModelType { get; set; }

                /// <summary>
                /// The shape of the type of a row of a query's result.  The default is
                /// <see cref="global::SqlSource.GeneratorModelType.SealedRecord" />.
                /// </summary>
                public GeneratorModelType OutputModelType { get; set; }

                /// <summary>
                /// Where the methods that run the queries go.  The default is
                /// <see cref="global::SqlSource.MethodLocation.ExtensionClass" />.
                /// </summary>
                public MethodLocation MethodLocation { get; set; }

                /// <summary>
                /// The type a method returns many rows in.  The default is
                /// <see cref="global::SqlSource.GeneratorCollectionType.Array" />.
                /// </summary>
                public GeneratorCollectionType CollectionType { get; set; }
```

`Parameters` stays last.  The summary of the attribute class becomes "Generates code for each query in the `.sql` files of the type."

`TargetTypeReader.ReadSettings` reads the two model types and the collection type with `ReadChoice<T>`, and the three strings with:

```csharp
    // A string that is empty or white space is not set, as Path is.
    private static string? ReadText(
        AttributeData attribute,
        string property,
        Func<string, bool> isValid,
        LocationInfo attributeLocation,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics
    )
    {
        if (
            GetNamedArgument(attribute, property) is not { Value: string written }
            || string.IsNullOrWhiteSpace(written)
        )
        {
            return null;
        }

        var value = written.Trim();
        if (isValid(value))
        {
            return value;
        }

        diagnostics.Add(
            DiagnosticInfo.Create(SqlDiagnostics.InvalidAttributeValue, attributeLocation, value, property)
        );
        return null;
    }
```

called with `static value => SettingValue.IsSuffix(value.AsSpan())` and `static value => SettingValue.IsNamespace(value.AsSpan())`.  `TargetType` gains `MethodPlacement MethodPlacement` after `Placement`, documented "Where the type's methods go.  It has no effect yet.", read with `ReadChoice<MethodPlacement>(...) ?? MethodPlacement.ExtensionClass`; `TestModels.Type` passes `MethodPlacement.ExtensionClass`.

- [ ] **Step 6: Read the six from MSBuild**

`MSBuildSettings`: a name constant and a `SettingKeys` member for each of `SqlSourceInputModelSuffix`, `SqlSourceOutputModelSuffix`, `SqlSourceModelNamespace`, `SqlSourceInputModelType`, `SqlSourceOutputModelType` and `SqlSourceCollectionType`; `Read` reads the three choices with `ReadChoice<T>` and the three strings with

```csharp
    private delegate bool Validator(ReadOnlySpan<char> value);

    private static string? ReadText(
        AnalyzerConfigOptions options,
        string key,
        string name,
        Validator isValid,
        ref List<InvalidSetting>? invalid
    )
    {
        if (!options.TryGetValue(key, out var written) || string.IsNullOrWhiteSpace(written))
        {
            return null;
        }

        var value = written.Trim();
        if (isValid(value.AsSpan()))
        {
            return value;
        }

        (invalid ??= []).Add(new InvalidSetting(name, value));
        return null;
    }
```

called with `SettingValue.IsSuffix` and `SettingValue.IsNamespace`.  A `Func` cannot take a span, which is why the delegate is declared.

`SqlSource.props` and `SqlSource.targets` gain the six names, each in the five places Task 10 Step 6 lists for `SqlSourceOutput`.

- [ ] **Step 7: Run everything**

Run: `dotnet test --solution SqlSource.slnx`
Expected: PASS.  Then `./pre-commit-validation.sh`, for the packed package's check.

- [ ] **Step 8: Update the documents**

`README.md`:

- Markers table, six rows:

```markdown
| `input-model-suffix`, `output-model-suffix` | yes | no | What ends a model's name |
| `model-namespace` | yes | no | A namespace |
| `input-model-type`, `output-model-type` | yes | yes | `record`, `sealed record`, `class` or `sealed class` |
| `input-model`, `output-model` | no | yes | A type's name, or its full name |
| `collection-type` | yes | yes | A collection type |
```

- Settings for models and methods, the table whole:

```markdown
| Setting | Values | Property and metadata | Attribute | Marker |
|----|----|----|----|----|
| Output | `sql`, `models`, `codegen` | `SqlSourceOutput` | `Output` | `-- output:` |
| Database | A name: letters, digits, `-`, `_`, `.` | `SqlSourceDatabase` | | `-- database:` |
| Suffix of a model's name | Characters of an identifier | `SqlSourceInputModelSuffix`, `SqlSourceOutputModelSuffix` | `InputModelSuffix`, `OutputModelSuffix` | `-- input-model-suffix:`, `-- output-model-suffix:`, before the first query only |
| Namespace of the models | A namespace | `SqlSourceModelNamespace` | `ModelNamespace` | `-- model-namespace:`, before the first query only |
| Shape of a model | `record`, `sealed record`, `class`, `sealed class` | `SqlSourceInputModelType`, `SqlSourceOutputModelType` | `InputModelType`, `OutputModelType` | `-- input-model-type:`, `-- output-model-type:` |
| Name of one query's model | A type's name, or its full name | | | `-- input-model:`, `-- output-model:`, inside a query only |
| Collection type | `IEnumerable`, `ICollection`, `IReadOnlyCollection`, `IList`, `IReadOnlyList`, `Array`, `List`, `ImmutableArray`, `ImmutableList`, `IImmutableList` | `SqlSourceCollectionType` | `CollectionType` | `-- collection-type:` |
| Where the methods go | `ExtensionClass`, `Public`, `Internal`, `Private` | | `MethodLocation` | |
| Shape of a result | `many`, `one`, `one-optional`, `none`, `rowcount` | | | `-- name: Name -> shape` |
| Parameters | See Parameters | | | `-- param:` |
| Generator parameters `sort-input`, `sort-output`, `no-table-models`, `async-method-suffix` | | `SqlSourceGeneratorParameters` | `Parameters` | `-- generator:` |
```

  and one sentence after it: "Where the methods go is decided where the type is declared, as `SqlLocation` is: it is a property of the attribute and nothing else."

- The attribute: the section Output becomes "### The other properties", listing `Output`, `InputModelSuffix`, `OutputModelSuffix`, `ModelNamespace`, `InputModelType`, `OutputModelType`, `MethodLocation` and `CollectionType` in one sentence that points to Settings for models and methods.
- MSBuild: a short section "### Settings for models and methods" that lists the eight property names and says each is also metadata of an `AdditionalFiles` item.

`docs/diagnostics.md`: the index row and

````markdown
## SQLSRC119

**Query has no parameters**

An `-- input-model:` marker names the type of a query's parameters, and this query has none: no `@name` in its SQL and no `-- param:` marker.

```sql
-- name: CountUsers
-- input-model: CountArgs
SELECT COUNT(*) FROM users;
```

Remove the marker.
````

`SQLSRC111` gains a sentence for each of the eight markers' values; `SQLSRC006` and `SQLSRC014` list the properties and settings they cover; `SQLSRC116` lists the three markers that go before the first query only and the two that go inside a query only.

`docs/tech-debt/TD-0006-attribute-conflicts-across-friend-assemblies.md`: the list of generated types is the six of `AttributeSource.GeneratedTypes`.

- [ ] **Step 9: Commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add -A && git commit -m "Accept the model settings, the collection type and the method location

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 12: `InternalsVisibleTo` for the tool, and the epic's row

**Files:**
- Modify: `src/SqlSource/SqlSource.csproj`
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`

- [ ] **Step 1: Let the tool see the internals**

`src/SqlSource/SqlSource.csproj`, in the item group that holds the two existing entries:

```xml
        <InternalsVisibleTo Include="SqlSource.Tool" />
```

- [ ] **Step 2: Check what the spec promised**

Run each and read the result:

```bash
git grep -n 'SqlSourceTokenValidation\|SQLSRC010\|TokenValidationSetting\|SqlSourceDialectFile\|FileDialect\b' -- . ':!docs/superpowers'
```

Expected: no output.

```bash
git grep -n 'token-ignore=\|-- generator: token-validation' -- . ':!docs/superpowers'
```

Expected: only the tests and the two sections of `docs/diagnostics.md` that show them as errors.

Then read `README.md` from top to bottom against the spec's section What changes for a user, and `docs/diagnostics.md` against `SqlDiagnostics.All`: every id has its section, and no section names a setting that is gone.

- [ ] **Step 3: State the measurements**

Collect the bytes for each character that Tasks 1, 3, 4 and 8 measured, with the number on `main`, for the pull request's description.  The budget in `SqlFileParserAllocationTests` is unchanged; if the last measurement is well under it, lower the budget to leave the same margin it had, and say so.

- [ ] **Step 4: Mark the phase done**

In `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`, the row of phase 1 in the Phases table: `In progress` becomes `Done`.  Read the outline's Phase 1 section against what was built, and where the code settled something the outline still calls Recommended, say what was settled in one sentence under Decided.

- [ ] **Step 5: Commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add -A && git commit -m "Let the tool see the generator's internals, and mark phase 1 done

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 6: Before the pull request**

`AGENTS.md` asks for the version check: `git fetch --tags origin`, then `git tag --list 'v*' --sort=-v:refname | head -n 1`.  With no `v*` tag there is nothing to compare, and `VersionPrefix` stays.
