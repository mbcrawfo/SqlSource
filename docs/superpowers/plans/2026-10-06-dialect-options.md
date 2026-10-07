# Dialect Options, Continued Strings and CockroachDB Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** SQL written for MySQL's and MariaDB's `ANSI_QUOTES` and `NO_BACKSLASH_ESCAPES` modes, a continued PostgreSQL `E'...'` string with `--` comments between its parts, and CockroachDB's `b'\''` are read as their databases read them.

**Architecture:** A setting names a `SqlDialectChoice`: a `SqlDialect` and a flags enum of options, written `mysql,ansi-quotes`.  `SqlDialectName` parses it, and `SqlDialectRules.For` gives one shared rules instance for each choice.  A continued string is lexed as one quoted region for each part: `EscapeStringReader` reads one part and names the reader of a continuation, and `SqlLexer` carries that reader across a gap that the dialect's `StringContinuation` rule allows.  `cockroachdb` is the rules of `postgres` with a `b` prefix and a gap of whitespace only.

**Tech Stack:** .NET SDK 10, C# (`LangVersion` preview) on `netstandard2.0`, Roslyn 4.8.0 incremental generator API, xunit v3 on Microsoft.Testing.Platform, Shouldly, CSharpier, MSBuild.

**Spec:** `docs/superpowers/specs/2026-10-06-dialect-options-design.md`.  Read it before you start.

## Global Constraints

- Work on the existing branch `claude/td-0004-review-fix-edd171`.  Commit after each task.  Do not push and do not open a pull request; ask the user first.
- Every commit must pass `./pre-commit-validation.sh`.  Run `./format.sh`, then `./pre-commit-validation.sh`, and only then commit, in a command of its own.  Never put a fix and the commit in one command.  End each commit message with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- `src/SqlSource` targets `netstandard2.0` only and may use no API newer than Roslyn 4.8.0.  Add no package reference.
- The build has `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild` and `AnalysisLevel` `latest-all`.  C# lines are at most 120 characters, and CSharpier does not break a string literal: split one that is too long with `+`.
- No collection expression may target `ImmutableArray<T>` in `src/SqlSource` (CS9210).
- The files of `tests/SqlSource.Tests/Generator/` are also compiled into `tests/SqlSource.Tests.RoslynFloor`.  They use only Roslyn API that 4.8.0 has.
- An internal type cannot be the parameter of a public test method.  A theory row names a dialect or an option as a string, and the test parses it.
- A reader in `Parsing/Quoting/` holds no state and allocates nothing.  `SqlLexer` names no dialect and no quoting form.
- The parse allocation budget in `tests/SqlSource.Tests/Parsing/SqlFileParserAllocationTests.cs`, 12 bytes for each character, is not raised.
- Values, copied from the spec:
  - Options: `ansi-quotes` (also `ansi_quotes`) and `no-backslash-escapes` (also `no_backslash_escapes`).  Only `mysql` and `mariadb` take them.
  - New dialect: `cockroachdb`, alias `cockroach`.
  - A value is a name, then options, separated by commas.  Each part is trimmed and compared ignoring case.  Options come in any order, and a repeated one is accepted.
  - Not valid, reported by the existing `SQLSRC011` and `SQLSRC111`: an unknown option, an option of another dialect, an empty part, an option with no dialect before it.
- No new diagnostic, no new MSBuild property or metadata, and no change to `SqlTextBuilder`, `SqlMarkerReader` or the two files in `src/SqlSource/build/`.
- `ansi`, `mssql`, `sqlite`, `oracle`, and `mysql` and `mariadb` without options read exactly as they do today.  No test of them may change its expectation.
- Two sentences end with two spaces in comments and documents, as everywhere in this repository.
- Run one test class with `dotnet test --project tests/SqlSource.Tests --filter-class '<full class name>'`, and everything with `dotnet test --solution SqlSource.slnx`.

## Review Focus

Inputs the spec implies and a person is likely to meet.  Each has its test in the task named.

1. **A value with a comma set in a project file.**  It must reach the generator whole, through `SqlSource.targets` and the file the SDK writes for the compiler.  Task 3, the end-to-end file `ByOption.sql`.
2. **Options copied from `sql_mode` as the server prints them**, `ANSI_QUOTES,NO_BACKSLASH_ESCAPES`, in upper case and with underscores.  Task 3, `SqlDialectNameTests`.
3. **A file with Windows line endings.**  The gap of a continued string holds `\r\n`, and the continuation must still be read.  Task 4, the gap theory of `SqlLexerTests`.
4. **A continued string as the last thing in a file**, with nothing after the line break.  Nothing may be left pending or thrown.  Task 4, the rows `E'a'\n` and `E'a'\n x`.
5. **A string that only looks continued.**  A `'...'` on the line after an `E'...'` with a comma, an operator or a block comment between them is a string of its own, and must not take backslash escapes.  Task 4, the theory of what ends a continuation.

---

### Task 1: Options, and the rules of each choice

**Files:**
- Create: `src/SqlSource/Parsing/SqlDialectOptions.cs`
- Create: `src/SqlSource/Parsing/SqlDialectChoice.cs`
- Modify: `src/SqlSource/Parsing/SqlDialectRules.cs`
- Create: `tests/SqlSource.Tests/Parsing/SqlDialectChoiceTests.cs`
- Modify: `tests/SqlSource.Tests/Parsing/SqlDialectRulesTests.cs`
- Modify: `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs`

**Interfaces:**
- Consumes: nothing from another task.
- Produces:
  - `[Flags] internal enum SqlDialectOptions { None = 0, AnsiQuotes = 1, NoBackslashEscapes = 2 }`
  - `internal readonly record struct SqlDialectChoice(SqlDialect Dialect, SqlDialectOptions Options)`, with an implicit conversion from `SqlDialect` that gives no options.  `default` is `ansi` with no options.
  - `public static SqlDialectRules SqlDialectRules.For(SqlDialectChoice choice)`, replacing `For(SqlDialect)`.
  - In `SqlLexerTests`, `Rules(string)` reads `"MySql+AnsiQuotes+NoBackslashEscapes"`: a dialect, then options, separated by `+`.  The comma of the spec's value syntax already separates the dialects of a row there.

- [ ] **Step 1: Write the failing tests**

Create `tests/SqlSource.Tests/Parsing/SqlDialectChoiceTests.cs`:

```csharp
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class SqlDialectChoiceTests
{
    [Fact]
    public void Default_IsAnsiWithNoOptions() =>
        default(SqlDialectChoice).ShouldBe(new SqlDialectChoice(SqlDialect.Ansi, SqlDialectOptions.None));

    [Fact]
    public void Conversion_FromADialect_HasNoOptions()
    {
        SqlDialectChoice choice = SqlDialect.MySql;

        choice.ShouldBe(new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.None));
    }

    [Fact]
    public void Equality_ComparesTheDialectAndTheOptions()
    {
        var ansiQuotes = new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.AnsiQuotes);

        ansiQuotes.ShouldBe(new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.AnsiQuotes));
        ansiQuotes.ShouldNotBe(new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.None));
        ansiQuotes.ShouldNotBe(new SqlDialectChoice(SqlDialect.MariaDb, SqlDialectOptions.AnsiQuotes));
    }
}
```

In `tests/SqlSource.Tests/Parsing/SqlDialectRulesTests.cs`, in `For_EveryDialect_GivesItsOwnSharedInstance`, a method group no longer converts, because `For` takes a `SqlDialectChoice`.  Replace these three lines:

```csharp
        var rules = dialects.Select(SqlDialectRules.For).ToArray();

        rules.Distinct().Count().ShouldBe(dialects.Length);
        rules.ShouldBe(dialects.Select(SqlDialectRules.For));
```

with:

```csharp
        var rules = dialects.Select(dialect => SqlDialectRules.For(dialect)).ToArray();

        rules.Distinct().Count().ShouldBe(dialects.Length);
        rules.ShouldBe(dialects.Select(dialect => SqlDialectRules.For(dialect)));
```

Add these tests to the same class, after `For_ValueThatIsNotADialect_GivesTheAnsiRules`:

```csharp
    [Fact]
    public void For_EachSetOfOptions_GivesMySqlAndMariaDbTheirOwnSharedInstance()
    {
        var choices = MySqlFamilyChoices();

        var rules = choices.Select(choice => SqlDialectRules.For(choice)).ToArray();

        rules.Distinct().Count().ShouldBe(choices.Length);
        rules.ShouldBe(choices.Select(choice => SqlDialectRules.For(choice)));
        SqlDialectRules
            .For(new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.None))
            .ShouldBeSameAs(SqlDialectRules.MySql);
        SqlDialectRules
            .For(new SqlDialectChoice(SqlDialect.MariaDb, SqlDialectOptions.None))
            .ShouldBeSameAs(SqlDialectRules.MariaDb);
    }

    // An option changes how a quoted region ends, and nothing about comments, hints or what opens a region.
    [Fact]
    public void For_AnySetOfOptions_KeepsTheCommentRulesAndTheOpenersOfTheDialect()
    {
        foreach (var choice in MySqlFamilyChoices())
        {
            var rules = SqlDialectRules.For(choice);
            var isMariaDb = choice.Dialect == SqlDialect.MariaDb;

            rules.NestedComments.ShouldBeFalse(choice.ToString());
            rules.DashNeedsWhitespace.ShouldBeTrue(choice.ToString());
            rules.HashComments.ShouldBeTrue(choice.ToString());
            rules.LineHints.ShouldBeFalse(choice.ToString());
            rules.MariaDbHints.ShouldBe(isMariaDb, choice.ToString());
            (rules.ReaderFor('$') is null).ShouldBe(isMariaDb, choice.ToString());
            rules.ReaderFor('\'').ShouldNotBeNull(choice.ToString());
            rules.ReaderFor('"').ShouldNotBeNull(choice.ToString());
            rules.ReaderFor('`').ShouldNotBeNull(choice.ToString());
        }
    }

    [Fact]
    public void For_OptionsOfADialectThatHasNone_AreIgnored() =>
        SqlDialectRules
            .For(new SqlDialectChoice(SqlDialect.PostgreSql, SqlDialectOptions.AnsiQuotes))
            .ShouldBeSameAs(SqlDialectRules.PostgreSql);

    // A value that no name gives must not throw inside the compiler if it ever arrives.
    [Fact]
    public void For_OptionsThatAreNotDefined_AreIgnored() =>
        SqlDialectRules
            .For(new SqlDialectChoice(SqlDialect.MySql, (SqlDialectOptions)4))
            .ShouldBeSameAs(SqlDialectRules.MySql);
```

And this helper at the end of the class:

```csharp
    private static SqlDialectChoice[] MySqlFamilyChoices()
    {
        SqlDialect[] dialects = [SqlDialect.MySql, SqlDialect.MariaDb];
        SqlDialectOptions[] options =
        [
            SqlDialectOptions.None,
            SqlDialectOptions.AnsiQuotes,
            SqlDialectOptions.NoBackslashEscapes,
            SqlDialectOptions.AnsiQuotes | SqlDialectOptions.NoBackslashEscapes,
        ];

        return
        [
            .. dialects.SelectMany(dialect => options.Select(option => new SqlDialectChoice(dialect, option))),
        ];
    }
```

In `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs`:

Replace the comment above the class with:

```csharp
// A test without a dialect in its name reads by the ANSI rules, which are the default.  An internal type cannot be
// the parameter of a public test method, so a row names its dialects, separated by commas.  An option of a dialect
// follows it after a plus sign, as in "MySql+AnsiQuotes".
```

Replace the `Rules` helper with:

```csharp
    private static SqlDialectRules Rules(string choice)
    {
        var parts = choice.Split('+');
        var options = parts
            .Skip(1)
            .Aggregate(SqlDialectOptions.None, (all, option) => all | Enum.Parse<SqlDialectOptions>(option));

        return SqlDialectRules.For(new SqlDialectChoice(Enum.Parse<SqlDialect>(parts[0]), options));
    }
```

In the theory `Lex_Construct_IsReadByTheRulesOfTheDialect`, after the existing row that starts `[InlineData("Ansi,SqlServer,PostgreSql,Sqlite,Oracle", "\"a\\\"b\" -- c\"",` and before the comment `// PostgreSQL's E prefix.`, add:

```csharp
    // The options of MySQL and MariaDB.  ANSI_QUOTES takes the backslash escape from "...", and
    // NO_BACKSLASH_ESCAPES takes it from both.
    [InlineData(
        "MySql+AnsiQuotes,MariaDb+AnsiQuotes",
        "'a\\'b' -- c'",
        "Quoted:'a\\'b' | Text:  | LineComment:-- c'"
    )]
    [InlineData(
        "MySql+NoBackslashEscapes,MariaDb+NoBackslashEscapes,"
            + "MySql+AnsiQuotes+NoBackslashEscapes,MariaDb+AnsiQuotes+NoBackslashEscapes",
        "'a\\'b' -- c'",
        "Quoted:'a\\' | Text:b | Quoted:' -- c'"
    )]
    [InlineData(
        "MySql+AnsiQuotes,MariaDb+AnsiQuotes,MySql+NoBackslashEscapes,MariaDb+NoBackslashEscapes,"
            + "MySql+AnsiQuotes+NoBackslashEscapes,MariaDb+AnsiQuotes+NoBackslashEscapes",
        "\"a\\\"b\" -- c\"",
        "Quoted:\"a\\\" | Text:b | Quoted:\" -- c\""
    )]
    // An option leaves the rest of the dialect alone.
    [InlineData(
        "MySql+AnsiQuotes+NoBackslashEscapes,MariaDb+AnsiQuotes+NoBackslashEscapes",
        "1 # c -- d\n2",
        "Text:1  | LineComment:# c -- d | Text:\n2"
    )]
    [InlineData("MySql+AnsiQuotes+NoBackslashEscapes,MariaDb+AnsiQuotes+NoBackslashEscapes", "5--3", "Text:5--3")]
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build tests/SqlSource.Tests`
Expected: the build fails with CS0246, `SqlDialectOptions` and `SqlDialectChoice` could not be found.

- [ ] **Step 3: Write the implementation**

Create `src/SqlSource/Parsing/SqlDialectOptions.cs`:

```csharp
using System;

namespace SqlSource.Parsing;

/// <summary>
/// Settings of a database server that change one rule of its dialect.  Only MySQL and MariaDB have any.
/// </summary>
[Flags]
internal enum SqlDialectOptions
{
    /// <summary>The dialect as the server reads it by default.</summary>
    None = 0,

    /// <summary>
    /// The SQL mode <c>ANSI_QUOTES</c>: <c>"..."</c> is a quoted identifier, and a backslash does not escape in it.
    /// </summary>
    AnsiQuotes = 1,

    /// <summary>
    /// The SQL mode <c>NO_BACKSLASH_ESCAPES</c>: a backslash is an ordinary character in every quoted region.
    /// </summary>
    NoBackslashEscapes = 2,
}
```

Create `src/SqlSource/Parsing/SqlDialectChoice.cs`:

```csharp
namespace SqlSource.Parsing;

/// <summary>
/// What a dialect setting names: a dialect, and the options of that dialect that are on.
/// </summary>
/// <remarks>
/// The default value is <see cref="SqlDialect.Ansi" /> with no options.  Compared by value, so it can travel between
/// the steps of the generator's pipeline.
/// </remarks>
internal readonly record struct SqlDialectChoice(SqlDialect Dialect, SqlDialectOptions Options)
{
    /// <summary>A dialect with no options.</summary>
    public static implicit operator SqlDialectChoice(SqlDialect dialect) => new(dialect, SqlDialectOptions.None);
}
```

If the build reports CA2225 for the operator, add this member below it and nothing else:

```csharp
    /// <inheritdoc cref="op_Implicit" />
    public static SqlDialectChoice FromSqlDialect(SqlDialect dialect) => dialect;
```

In `src/SqlSource/Parsing/SqlDialectRules.cs`:

After the field `Dollar`, add:

```csharp
    // One for each combination of SqlDialectOptions.
    private const int OptionSets = 4;

    // The rules of MySQL and of MariaDB under each set of options, at the index that the options have as a number.
    private static readonly SqlDialectRules[] MySqlByOptions = CreateMySqlFamily(isMariaDb: false);

    private static readonly SqlDialectRules[] MariaDbByOptions = CreateMySqlFamily(isMariaDb: true);
```

`OptionSets` is a constant, so move it up beside `TableSize` if the analyzers ask for constants before fields.

Replace the static properties `MySql` and `MariaDb` with:

```csharp
    /// <summary>MySQL with no options.  <see cref="For" /> gives the rules of a set of options.</summary>
    public static SqlDialectRules MySql => MySqlByOptions[(int)SqlDialectOptions.None];

    /// <summary>MariaDB with no options.  <see cref="For" /> gives the rules of a set of options.</summary>
    public static SqlDialectRules MariaDb => MariaDbByOptions[(int)SqlDialectOptions.None];
```

Replace `For` with:

```csharp
    /// <summary>
    /// The rules of <paramref name="choice" />.  One shared instance for each choice.  Options that the dialect
    /// does not have are ignored: a name never gives them.
    /// </summary>
    public static SqlDialectRules For(SqlDialectChoice choice) =>
        choice.Dialect switch
        {
            SqlDialect.Ansi => Ansi,
            SqlDialect.SqlServer => SqlServer,
            SqlDialect.PostgreSql => PostgreSql,
            SqlDialect.MySql => MySqlByOptions[(int)choice.Options & (OptionSets - 1)],
            SqlDialect.MariaDb => MariaDbByOptions[(int)choice.Options & (OptionSets - 1)],
            SqlDialect.Sqlite => Sqlite,
            SqlDialect.Oracle => Oracle,
            _ => Ansi,
        };
```

Add this method after `For`:

```csharp
    // MySQL and MariaDB read "..." as a string and take backslash escapes in both kinds of quote.  ANSI_QUOTES makes
    // "..." an identifier, which has none; NO_BACKSLASH_ESCAPES takes them from both.  MySQL reads dollar quotes,
    // and MariaDB has its own hint.
    private static SqlDialectRules[] CreateMySqlFamily(bool isMariaDb)
    {
        var family = new SqlDialectRules[OptionSets];
        for (var index = 0; index < OptionSets; index++)
        {
            var options = (SqlDialectOptions)index;
            var noBackslash = (options & SqlDialectOptions.NoBackslashEscapes) != 0;
            var ansiQuotes = (options & SqlDialectOptions.AnsiQuotes) != 0;
            var singleQuote = noBackslash ? Doubled : Backslash;
            var doubleQuote = noBackslash || ansiQuotes ? Doubled : Backslash;
            family[index] = isMariaDb
                ? new SqlDialectRules(('\'', singleQuote), ('"', doubleQuote), ('`', Doubled))
                {
                    DashNeedsWhitespace = true,
                    HashComments = true,
                    MariaDbHints = true,
                }
                : new SqlDialectRules(('\'', singleQuote), ('"', doubleQuote), ('`', Doubled), ('$', Dollar))
                {
                    DashNeedsWhitespace = true,
                    HashComments = true,
                };
        }

        return family;
    }
```

In the class remarks, replace `The static properties are the whole table:` with `The static properties, and the rules of MySQL and MariaDB under each set of options, are the whole table:`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --solution SqlSource.slnx`
Expected: every test passes.  Callers that pass a `SqlDialect` to `For` compile through the implicit conversion.

- [ ] **Step 5: Commit**

Run `./format.sh`, then `./pre-commit-validation.sh`; both must pass.  Then, in a command of its own:

```bash
git add src/SqlSource/Parsing/SqlDialectOptions.cs src/SqlSource/Parsing/SqlDialectChoice.cs src/SqlSource/Parsing/SqlDialectRules.cs tests/SqlSource.Tests/Parsing/SqlDialectChoiceTests.cs tests/SqlSource.Tests/Parsing/SqlDialectRulesTests.cs tests/SqlSource.Tests/Parsing/SqlLexerTests.cs
git commit -m "Give MySQL and MariaDB rules for each set of SQL-mode options" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: A setting carries a choice

A refactoring.  Every place that carries a `SqlDialect` from a setting carries a `SqlDialectChoice`.  No value has options yet, so nothing a user sees changes.

**Files:**
- Modify: `src/SqlSource/Parsing/SqlDialectName.cs`
- Modify: `src/SqlSource/Parsing/SqlDirectiveScope.cs`
- Modify: `src/SqlSource/Parsing/SqlFileParser.cs`
- Modify: `src/SqlSource/Generation/DialectSetting.cs`
- Modify: `src/SqlSource/Generation/FileDialect.cs`
- Modify: `src/SqlSource/Generation/SqlFileReader.cs`
- Modify: `tests/SqlSource.Tests/Parsing/SqlDialectNameTests.cs`
- Modify: `tests/SqlSource.Tests/Parsing/SqlDirectiveScopeTests.cs`
- Modify: `tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs`
- Modify: `tests/SqlSource.Tests/Generation/DialectSettingTests.cs`

**Interfaces:**
- Consumes: `SqlDialectChoice`, `SqlDialectOptions` and `SqlDialectRules.For(SqlDialectChoice)` from Task 1.
- Produces:
  - `SqlDialectName.TryParse(string? value, out SqlDialectChoice choice)` and `TryParse(ReadOnlySpan<char> value, out SqlDialectChoice choice)`.  On failure `choice` is `default`.
  - `SqlDirectiveScope.Dialect` of type `SqlDialectChoice?`, and `SqlDirectiveScope.TryFindDialect(string text, SqlMarker marker, out SqlDialectChoice dialect)`.
  - `SqlFileParser.Parse(string text, string fileName, SqlDialectChoice dialect = default)`.
  - `DialectSetting(SqlDialectChoice? Dialect, string? InvalidValue)`, `FileDialect(AdditionalText File, SqlDialectChoice Dialect, string? InvalidValue)`, and `SqlFileReader.Read(AdditionalText, string, SqlDialectChoice, string?, CancellationToken)`.

- [ ] **Step 1: Change the tests to the new types**

In `tests/SqlSource.Tests/Parsing/SqlDialectNameTests.cs`, replace the bodies of these four tests:

```csharp
    public void TryParse_NameOrAlias_GivesItsDialect(string name, string expected)
    {
        SqlDialectName.TryParse(name, out var choice).ShouldBeTrue();

        choice.Dialect.ToString().ShouldBe(expected);
        choice.Options.ShouldBe(SqlDialectOptions.None);
    }
```

```csharp
    public void TryParse_AnythingElse_IsRejected(string? name)
    {
        SqlDialectName.TryParse(name, out var choice).ShouldBeFalse();

        choice.ShouldBe(default(SqlDialectChoice));
    }
```

```csharp
    public void TryParse_Span_ReadsTheSameNames()
    {
        SqlDialectName.TryParse("dialect=MariaDB".AsSpan(8), out var choice).ShouldBeTrue();

        choice.ShouldBe(new SqlDialectChoice(SqlDialect.MariaDb, SqlDialectOptions.None));
    }
```

```csharp
    public void TryParse_EveryDialect_HasAName()
    {
        string[] names = ["ansi", "mssql", "postgres", "mysql", "mariadb", "sqlite", "oracle"];

        var parsed = names.Select(name =>
        {
            SqlDialectName.TryParse(name, out var choice).ShouldBeTrue();
            return choice.Dialect;
        });

        parsed.ShouldBe(Enum.GetValues<SqlDialect>());
    }
```

In `tests/SqlSource.Tests/Parsing/SqlDirectiveScopeTests.cs`:

- Add this helper at the end of the class:

  ```csharp
      private static SqlDialectChoice Plain(SqlDialect dialect) => new(dialect, SqlDialectOptions.None);
  ```

- In `Read_Dialect_KeepsTheDialectItNames`, replace `scope.Dialect.ToString().ShouldBe(expected);` with `scope.Dialect.ShouldBe(Plain(Enum.Parse<SqlDialect>(expected)));`.
- Replace each `scope.Dialect.ShouldBe(SqlDialect.SqlServer);` with `scope.Dialect.ShouldBe(Plain(SqlDialect.SqlServer));`, and each `scope.Dialect.ShouldBe(SqlDialect.MySql);` with `scope.Dialect.ShouldBe(Plain(SqlDialect.MySql));`.  There are four in all.
- In `TryFindDialect_MarkerWithADialect_GivesTheFirstThatIsValid`, replace `dialect.ToString().ShouldBe(expected);` with `dialect.ShouldBe(Plain(Enum.Parse<SqlDialect>(expected)));`.
- In `TryFindDialect_MarkerWithoutAValidDialect_FindsNone`, replace `dialect.ShouldBe(SqlDialect.Ansi);` with `dialect.ShouldBe(default(SqlDialectChoice));`.

In `tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs`, change the signature of the helper `Blocks`, and give `Errors` the same parameter:

```csharp
    private static SqlBlock[] Blocks(string text, string fileName = "Query.sql", SqlDialectChoice dialect = default)
    {
        var result = SqlFileParser.Parse(text, fileName, dialect);
        result.Errors.ShouldBeEmpty();
        return [.. result.Blocks];
    }

    private static SqlParseError[] Errors(string text, string fileName = "Query.sql", SqlDialectChoice dialect = default)
    {
        var result = SqlFileParser.Parse(text, fileName, dialect);
        result.Blocks.ShouldBeEmpty();
        return [.. result.Errors];
    }
```

In `tests/SqlSource.Tests/Generation/DialectSettingTests.cs`:

- Add `using System;` as the first line.
- In `Parse_NameOfADialect_IsThatDialect`, replace `setting.Dialect.ToString().ShouldBe(expected);` with:

  ```csharp
          setting.Dialect.ShouldBe(new SqlDialectChoice(Enum.Parse<SqlDialect>(expected), SqlDialectOptions.None));
  ```

- In `Resolve_MetadataThenPropertyThenAnsi`, replace `resolved.Dialect.ToString().ShouldBe(dialect);` with:

  ```csharp
          resolved.Dialect.ShouldBe(new SqlDialectChoice(Enum.Parse<SqlDialect>(dialect), SqlDialectOptions.None));
  ```

Leave every `new DialectSetting(SqlDialect.Oracle, null)` and `new FileDialect(text, SqlDialect.MySql, "nope")` as it is: the implicit conversion makes it a choice.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build tests/SqlSource.Tests`
Expected: the build fails.  `choice.Dialect` and `choice.Options` do not exist on `SqlDialect`, and `Parse` takes no `SqlDialectChoice`.

- [ ] **Step 3: Write the implementation**

In `src/SqlSource/Parsing/SqlDialectName.cs`, replace the two `TryParse` methods with:

```csharp
    /// <summary>
    /// Reads a name or an alias, ignoring case and surrounding whitespace.  False for anything else, and for null.
    /// </summary>
    public static bool TryParse(string? value, out SqlDialectChoice choice) => TryParse(value.AsSpan(), out choice);

    /// <inheritdoc cref="TryParse(string?, out SqlDialectChoice)" />
    public static bool TryParse(ReadOnlySpan<char> value, out SqlDialectChoice choice)
    {
        var name = value.Trim();
        foreach (var (candidate, candidateDialect) in Names)
        {
            if (name.Equals(candidate.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                choice = candidateDialect;
                return true;
            }
        }

        choice = default;
        return false;
    }
```

In `src/SqlSource/Parsing/SqlDirectiveScope.cs`:

- Replace `public SqlDialect? Dialect { get; private set; }` with `public SqlDialectChoice? Dialect { get; private set; }`.
- Replace the signature `public static bool TryFindDialect(string text, SqlMarker marker, out SqlDialect dialect)` with `public static bool TryFindDialect(string text, SqlMarker marker, out SqlDialectChoice dialect)`.
- In that method, replace `dialect = SqlDialect.Ansi;` with `dialect = default;`.

`ApplyDialect` needs no change: `existing != dialect` now compares the whole choice.

In `src/SqlSource/Parsing/SqlFileParser.cs`, replace the signature of `Parse` and the sentence of its summary that names the parameter:

```csharp
    /// <summary>
    /// Parses <paramref name="text" />.  <paramref name="fileName" /> is the file's name with its extension and
    /// without a directory; it names the block of a file that has no <c>-- name:</c> marker.
    /// <paramref name="dialect" /> is the dialect, with its options, that the project gives the file.  A
    /// <c>dialect=</c> directive in the file's header replaces it whole for the text after the directive.
    /// </summary>
    public static SqlFileParseResult Parse(string text, string fileName, SqlDialectChoice dialect = default)
```

In `src/SqlSource/Generation/DialectSetting.cs`:

- Replace the `<param name="Dialect">` text and the record's declaration with:

  ```csharp
  /// <param name="Dialect">
  /// The dialect that is set, with its options, or null when none is.  <see cref="SqlDialect.Ansi" /> with no
  /// options when the value is not valid.
  /// </param>
  /// <param name="InvalidValue">The value as written when it is not valid, and null otherwise.</param>
  internal sealed record DialectSetting(SqlDialectChoice? Dialect, string? InvalidValue)
  ```

- In `Parse`, replace `: new DialectSetting(SqlDialect.Ansi, value);` with `: new DialectSetting(default(SqlDialectChoice), value);`.

In `src/SqlSource/Generation/FileDialect.cs`:

- Replace the record's declaration with `internal sealed record FileDialect(AdditionalText File, SqlDialectChoice Dialect, string? InvalidValue)`.
- Replace `: new FileDialect(file, project.Dialect ?? SqlDialect.Ansi, null);` with `: new FileDialect(file, project.Dialect ?? default, null);`.

In `src/SqlSource/Generation/SqlFileReader.cs`, replace the parameter `SqlDialect dialect,` of the second `Read` with `SqlDialectChoice dialect,`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --solution SqlSource.slnx`
Expected: every test passes, with no expectation of a reading changed.

If an assertion such as `x.ShouldBe(SqlDialect.MySql)` does not compile because the compiler cannot infer the type, write the choice out: `x.ShouldBe(new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.None))`.

- [ ] **Step 5: Commit**

Run `./format.sh`, then `./pre-commit-validation.sh`; both must pass.  Then, in a command of its own:

```bash
git add src/SqlSource tests/SqlSource.Tests
git commit -m "Carry a dialect with its options wherever a setting travels" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Options in a dialect value

**Files:**
- Modify: `src/SqlSource/Parsing/SqlDialectName.cs`
- Modify: `tests/SqlSource.Tests/Parsing/SqlDialectNameTests.cs`
- Modify: `tests/SqlSource.Tests/Parsing/SqlDirectiveScopeTests.cs`
- Modify: `tests/SqlSource.Tests/Parsing/SqlPreambleDialectTests.cs`
- Modify: `tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs`
- Modify: `tests/SqlSource.Tests/Generation/DialectSettingTests.cs`
- Modify: `tests/SqlSource.Tests/Generator/DialectTests.cs`
- Create: `tests/SqlSource.Tests/EndToEnd/Dialects/ByOption.sql`
- Modify: `tests/SqlSource.Tests/SqlSource.Tests.csproj`
- Modify: `tests/SqlSource.Tests/EndToEnd/EndToEndTests.cs`

**Interfaces:**
- Consumes: the `TryParse` signatures of Task 2, and the rules of Task 1.
- Produces: `SqlDialectName.TryParse` accepts `name,option,option`.  No signature changes.

- [ ] **Step 1: Write the failing tests**

In `tests/SqlSource.Tests/Parsing/SqlDialectNameTests.cs`, add after `TryParse_NameOrAlias_GivesItsDialect`:

```csharp
    // The options of a flags enum print in the order of their values, separated by a comma and a space.
    [Theory]
    [InlineData("mysql,ansi-quotes", "MySql", "AnsiQuotes")]
    [InlineData("mysql,no-backslash-escapes", "MySql", "NoBackslashEscapes")]
    [InlineData("mysql,ansi-quotes,no-backslash-escapes", "MySql", "AnsiQuotes, NoBackslashEscapes")]
    [InlineData("mariadb,ansi-quotes", "MariaDb", "AnsiQuotes")]
    // In any order, and one that is repeated counts once.
    [InlineData("mariadb,no-backslash-escapes,ansi-quotes", "MariaDb", "AnsiQuotes, NoBackslashEscapes")]
    [InlineData("mysql,ansi-quotes,ansi_quotes,ANSI-QUOTES", "MySql", "AnsiQuotes")]
    // As the server prints sql_mode.
    [InlineData("mysql,ANSI_QUOTES,NO_BACKSLASH_ESCAPES", "MySql", "AnsiQuotes, NoBackslashEscapes")]
    [InlineData("MariaDB,Ansi-Quotes", "MariaDb", "AnsiQuotes")]
    // Whitespace around a part, as a value written over several lines has it.
    [InlineData("  mysql , ansi-quotes ,\tno_backslash_escapes\n", "MySql", "AnsiQuotes, NoBackslashEscapes")]
    [InlineData("\n    mysql,\n    ansi-quotes\n", "MySql", "AnsiQuotes")]
    public void TryParse_NameWithOptions_GivesTheDialectAndItsOptions(string value, string dialect, string options)
    {
        SqlDialectName.TryParse(value, out var choice).ShouldBeTrue();

        choice.Dialect.ToString().ShouldBe(dialect);
        choice.Options.ToString().ShouldBe(options);
    }
```

Add these rows to the theory `TryParse_AnythingElse_IsRejected`, after the row `[InlineData("3")]`:

```csharp
    // An empty part.
    [InlineData("mysql,")]
    [InlineData("mysql,,ansi-quotes")]
    [InlineData("mysql,ansi-quotes,")]
    [InlineData("mysql, ")]
    [InlineData(",mysql")]
    // An option that does not exist.
    [InlineData("mysql,ansi")]
    [InlineData("mysql,ansi quotes")]
    [InlineData("mysql,ansi-quotes=on")]
    [InlineData("mysql,mariadb")]
    // An option of another dialect, and an option with no dialect.
    [InlineData("postgres,ansi-quotes")]
    [InlineData("ansi,no-backslash-escapes")]
    [InlineData("oracle,ansi_quotes")]
    [InlineData("ansi-quotes")]
    [InlineData("ansi-quotes,mysql")]
    // Another separator.
    [InlineData("mysql;ansi-quotes")]
    [InlineData("mysql ansi-quotes")]
    [InlineData("mysql+ansi-quotes")]
    // Turkish dotless i in an option.
    [InlineData("mysql,ansı-quotes")]
```

Replace `TryParse_Span_ReadsTheSameNames` with:

```csharp
    [Fact]
    public void TryParse_Span_ReadsTheSameNamesAndOptions()
    {
        SqlDialectName.TryParse("dialect=MariaDB".AsSpan(8), out var plain).ShouldBeTrue();
        SqlDialectName.TryParse("dialect=MariaDB,ansi-quotes".AsSpan(8), out var withOption).ShouldBeTrue();

        plain.ShouldBe(new SqlDialectChoice(SqlDialect.MariaDb, SqlDialectOptions.None));
        withOption.ShouldBe(new SqlDialectChoice(SqlDialect.MariaDb, SqlDialectOptions.AnsiQuotes));
    }
```

In `tests/SqlSource.Tests/Parsing/SqlDirectiveScopeTests.cs`:

Add these rows to `Read_DialectWithoutAValueOrWithAnUnknownName_IsAnError`:

```csharp
    [InlineData("dialect=mysql,")]
    [InlineData("dialect=mysql,nope")]
    [InlineData("dialect=postgres,ansi-quotes")]
```

Add after `Read_SameDialectTwice_IsAllowed`:

```csharp
    [Fact]
    public void Read_DialectWithOptions_KeepsTheDialectAndItsOptions()
    {
        var (scope, errors) = Read("-- SqlSource: keep-comments DIALECT=MySql,ANSI_QUOTES");

        errors.ShouldBeEmpty();
        scope.Dialect.ShouldBe(new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.AnsiQuotes));
    }

    [Fact]
    public void Read_SameDialectAndOptionsTwice_IsAllowed()
    {
        var (scope, errors) = Read(
            "-- SqlSource: dialect=mysql,ansi-quotes,no-backslash-escapes",
            "-- SqlSource: dialect=MySQL,NO_BACKSLASH_ESCAPES,ANSI_QUOTES"
        );

        errors.ShouldBeEmpty();
        scope.Dialect.ShouldBe(
            new SqlDialectChoice(
                SqlDialect.MySql,
                SqlDialectOptions.AnsiQuotes | SqlDialectOptions.NoBackslashEscapes
            )
        );
    }

    [Fact]
    public void Read_SameDialectWithOtherOptions_ReportsTheSecondAndKeepsTheFirst()
    {
        const string Line = "-- SqlSource: dialect=mysql dialect=mysql,ansi-quotes";

        var (scope, errors) = Read(Line);

        errors.ShouldBe([
            SqlParseError.Create(
                SqlParseErrorKind.ConflictingDirectives,
                SpanOf(Line, "dialect=mysql,ansi-quotes"),
                "dialect=mysql,ansi-quotes"
            ),
        ]);
        scope.Dialect.ShouldBe(Plain(SqlDialect.MySql));
    }

    // A directive is one word.  A space after the comma ends it, and the option is read as a directive of its own.
    [Fact]
    public void Read_SpaceAfterTheCommaOfADialect_IsTwoErrors()
    {
        const string Line = "-- SqlSource: dialect=mysql, ansi-quotes";

        var (scope, errors) = Read(Line);

        errors.ShouldBe([
            SqlParseError.Create(
                SqlParseErrorKind.InvalidDirectiveValue,
                SpanOf(Line, "dialect=mysql,"),
                "dialect=mysql,"
            ),
            SqlParseError.Create(SqlParseErrorKind.UnknownDirective, SpanOf(Line, "ansi-quotes"), "ansi-quotes"),
        ]);
        scope.Dialect.ShouldBeNull();
    }

    [Fact]
    public void TryFindDialect_MarkerWithOptions_GivesTheFirstValueThatIsValid()
    {
        const string Line = "-- SqlSource: dialect=mysql, dialect=mariadb,no-backslash-escapes dialect=mysql";

        SqlDirectiveScope.TryFindDialect(Line, Marker(Line), out var dialect).ShouldBeTrue();

        dialect.ShouldBe(new SqlDialectChoice(SqlDialect.MariaDb, SqlDialectOptions.NoBackslashEscapes));
    }
```

In `tests/SqlSource.Tests/Parsing/SqlPreambleDialectTests.cs`, add after `Apply_DirectiveInTheHeader_SwitchesTheLexer`:

```csharp
    [Fact]
    public void Apply_DirectiveWithAnOption_SwitchesTheLexerToTheRulesOfThatOption()
    {
        const string Text = "-- SqlSource: dialect=mysql,no-backslash-escapes\nSELECT 'C:\\temp\\'";
        var lexer = new SqlLexer(Text, SqlDialectRules.Ansi);

        _ = SqlPreambleDialect.Apply(lexer, Text);

        lexer.Rules.ShouldBeSameAs(
            SqlDialectRules.For(new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.NoBackslashEscapes))
        );
        lexer.Rules.ShouldNotBeSameAs(SqlDialectRules.MySql);
    }
```

In `tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs`, add after `Parse_DialectDirective_ReplacesTheDialectOfTheProject`:

```csharp
    // Under plain MySQL the backslash takes the closing quote with it, and the string is not closed.
    [Fact]
    public void Parse_DialectDirectiveWithAnOption_ReadsTheFileByThatOption()
    {
        const string Text =
            "-- SqlSource: dialect=mysql,no-backslash-escapes\n-- name: A\nSELECT 'C:\\temp\\' AS path -- c\n";

        Sql(Blocks(Text).ShouldHaveSingleItem()).ShouldBe("SELECT 'C:\\temp\\' AS path");
    }

    [Fact]
    public void Parse_OptionOfTheProject_IsUsedByAFileWithoutADirective()
    {
        const string Text = "-- name: A\nSELECT \"a\\\" AS b -- c\n";
        var project = new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.AnsiQuotes);

        Sql(Blocks(Text, dialect: project).ShouldHaveSingleItem()).ShouldBe("SELECT \"a\\\" AS b");
    }

    // The directive names no option, so the file has none: the option of the project is not kept.
    [Fact]
    public void Parse_DialectDirectiveWithoutOptions_ReplacesTheOptionsOfTheProjectToo()
    {
        const string Text = "-- SqlSource: dialect=mysql\n-- name: A\nSELECT 'it\\'s' -- c\n";
        var project = new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.NoBackslashEscapes);

        Sql(Blocks(Text, dialect: project).ShouldHaveSingleItem()).ShouldBe("SELECT 'it\\'s'");
    }

    [Fact]
    public void Parse_TwoDialectDirectivesThatDifferOnlyInOptions_IsAnError()
    {
        const string Text =
            "-- SqlSource: dialect=mysql\n-- SqlSource: dialect=mysql,ansi-quotes\n-- name: A\nSELECT 1\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.ConflictingDirectives,
                    SpanOf(Text, "dialect=mysql,ansi-quotes"),
                    "dialect=mysql,ansi-quotes"
                ),
            ]);
    }
```

In `tests/SqlSource.Tests/Generation/DialectSettingTests.cs`:

Add after `Parse_NameOfADialect_IsThatDialect`:

```csharp
    [Theory]
    [InlineData("mysql,ansi-quotes")]
    [InlineData("\n    MySQL ,\n    ANSI_QUOTES\n  ")]
    public void Parse_NameWithAnOption_IsThatDialectWithTheOption(string value) =>
        DialectSetting
            .Parse(value)
            .ShouldBe(new DialectSetting(new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.AnsiQuotes), null));
```

Add these rows to `Parse_AnythingElse_IsAnsiAndKeepsTheValueAsWritten`:

```csharp
    [InlineData("postgres,ansi-quotes")]
    [InlineData("mysql,")]
    [InlineData("mysql,ansi")]
```

Add after `Resolve_MetadataThenPropertyThenAnsi`:

```csharp
    // A value replaces the one it wins over whole: the option of the property is not added to the metadata.
    [Fact]
    public void Resolve_MetadataWithoutAnOption_DoesNotTakeTheOptionOfTheProperty()
    {
        var file = new InMemoryAdditionalText(Path, "SELECT 1;");

        var resolved = FileDialect.Resolve(
            file,
            DialectSetting.Parse("mysql"),
            DialectSetting.Parse("mysql,ansi-quotes")
        );

        resolved.Dialect.ShouldBe(new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.None));
    }

    [Fact]
    public void Resolve_NoMetadata_TakesTheOptionOfTheProperty()
    {
        var file = new InMemoryAdditionalText(Path, "SELECT 1;");

        var resolved = FileDialect.Resolve(file, DialectSetting.Parse(null), DialectSetting.Parse("mysql,ansi-quotes"));

        resolved.Dialect.ShouldBe(new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.AnsiQuotes));
    }
```

In `tests/SqlSource.Tests/Generator/DialectTests.cs`, add after the constant `Invalid`:

```csharp
    // Valid only where a backslash does not escape.  Under plain MySQL the string is never closed.
    private const string PathQuery = "SELECT 'C:\\temp\\' AS p;\n";
```

And add after `Run_FilesWithDifferentMetadata_AreEachReadByTheirOwnDialect`:

```csharp
    [Theory]
    // In the directive, in the metadata and in the property.
    [InlineData("mysql,no-backslash-escapes", null, null)]
    [InlineData(null, "mysql,no-backslash-escapes", null)]
    [InlineData(null, null, "mysql,no-backslash-escapes")]
    // As the server spells it, with the whitespace of a value written over several lines.
    [InlineData(null, null, "\n    MySQL,\n    NO_BACKSLASH_ESCAPES\n  ")]
    [InlineData(null, "mariadb , ansi_quotes , no_backslash_escapes", null)]
    // A value with the option wins over one without it.
    [InlineData("mariadb,no-backslash-escapes", "mysql", "postgres")]
    [InlineData(null, "mysql,no-backslash-escapes", "mysql")]
    public void Run_OptionOfADialect_IsReadFromTheDirectiveTheMetadataAndTheProperty(
        string? directive,
        string? metadata,
        string? property
    )
    {
        var sql = (directive is null ? string.Empty : "-- SqlSource: dialect=" + directive + "\n") + PathQuery;

        var run = Run(property, new SqlFile("/app/Repo/Q.sql", sql, metadata));

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
    }

    // The value that wins names no option, so the file is read without one: options are not merged.
    [Theory]
    [InlineData("mysql", null, "mysql,no-backslash-escapes")]
    [InlineData("mysql", "mysql,no-backslash-escapes", null)]
    [InlineData(null, "mysql", "mysql,no-backslash-escapes")]
    public void Run_ValueWithoutTheOption_ReplacesAValueWithItWhole(
        string? directive,
        string? metadata,
        string? property
    )
    {
        var sql = (directive is null ? string.Empty : "-- SqlSource: dialect=" + directive + "\n") + PathQuery;

        var run = Run(property, new SqlFile("/app/Repo/Q.sql", sql, metadata));

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC101 ");
    }

    [Theory]
    [InlineData("postgres,ansi-quotes")]
    [InlineData("mysql,")]
    [InlineData("mysql,ansi")]
    public void Run_PropertyWithAnOptionThatIsNotValid_IsAnErrorThatQuotesTheWholeValue(string property)
    {
        var run = Run(property, new SqlFile("/app/Repo/Q.sql", Query));

        run.Diagnostics.ShouldBe(["SQLSRC011 (1,1)-(1,1): '" + property + Invalid]);
        run.Sources["App.Sample.g.cs"].ShouldContain("public const string Q = \"" + Ansi + "\";");
    }

    [Fact]
    public void Run_DirectiveWithAnOptionThatIsNotValid_IsAnErrorAtTheDirective()
    {
        var run = Run(null, new SqlFile("/app/Repo/Q.sql", "-- SqlSource: dialect=postgres,ansi-quotes\n" + Query));

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC111 /app/Repo/Q.sql(1,15)-(1,43): ");
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class 'SqlSource.Tests.Parsing.SqlDialectNameTests'`
Expected: `TryParse_NameWithOptions_GivesTheDialectAndItsOptions` fails on every row, because `TryParse` returns false for a value with a comma.

- [ ] **Step 3: Write the implementation**

In `src/SqlSource/Parsing/SqlDialectName.cs`, replace the class summary, add the table of options after `Names`, and replace the span overload of `TryParse`:

```csharp
/// <summary>
/// The names a dialect and its options are set by: in the <c>dialect=</c> directive, in the
/// <c>SqlSourceDialect</c> MSBuild property and in the metadata of the same name.  This is the only place the names
/// are known.
/// </summary>
/// <remarks>
/// A value is the name of a dialect, then any options of that dialect, separated by commas:
/// <c>mysql,ansi-quotes</c>.
/// </remarks>
```

```csharp
    // An option is also accepted as the server spells its SQL mode, with underscores.
    private static readonly (string Name, SqlDialectOptions Option)[] Options =
    [
        ("ansi-quotes", SqlDialectOptions.AnsiQuotes),
        ("ansi_quotes", SqlDialectOptions.AnsiQuotes),
        ("no-backslash-escapes", SqlDialectOptions.NoBackslashEscapes),
        ("no_backslash_escapes", SqlDialectOptions.NoBackslashEscapes),
    ];
```

```csharp
    /// <summary>
    /// Reads a name or an alias and the options after it, ignoring case and the whitespace around each part.  False
    /// for anything else, and for null: a name that is not a dialect, an option that does not exist or that the
    /// dialect does not have, and an empty part.
    /// </summary>
    public static bool TryParse(string? value, out SqlDialectChoice choice) => TryParse(value.AsSpan(), out choice);

    /// <inheritdoc cref="TryParse(string?, out SqlDialectChoice)" />
    public static bool TryParse(ReadOnlySpan<char> value, out SqlDialectChoice choice)
    {
        choice = default;
        var rest = value;
        var comma = rest.IndexOf(',');
        if (!TryFindDialect(comma < 0 ? rest : rest.Slice(0, comma), out var dialect))
        {
            return false;
        }

        var allowed = OptionsOf(dialect);
        var options = SqlDialectOptions.None;
        while (comma >= 0)
        {
            rest = rest.Slice(comma + 1);
            comma = rest.IndexOf(',');
            if (!TryFindOption(comma < 0 ? rest : rest.Slice(0, comma), out var option) || (allowed & option) == 0)
            {
                return false;
            }

            options |= option;
        }

        choice = new SqlDialectChoice(dialect, options);
        return true;
    }

    private static SqlDialectOptions OptionsOf(SqlDialect dialect) =>
        dialect is SqlDialect.MySql or SqlDialect.MariaDb
            ? SqlDialectOptions.AnsiQuotes | SqlDialectOptions.NoBackslashEscapes
            : SqlDialectOptions.None;

    private static bool TryFindDialect(ReadOnlySpan<char> part, out SqlDialect dialect)
    {
        var name = part.Trim();
        foreach (var (candidate, candidateDialect) in Names)
        {
            if (name.Equals(candidate.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                dialect = candidateDialect;
                return true;
            }
        }

        dialect = SqlDialect.Ansi;
        return false;
    }

    private static bool TryFindOption(ReadOnlySpan<char> part, out SqlDialectOptions option)
    {
        var name = part.Trim();
        foreach (var (candidate, candidateOption) in Options)
        {
            if (name.Equals(candidate.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                option = candidateOption;
                return true;
            }
        }

        option = SqlDialectOptions.None;
        return false;
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --solution SqlSource.slnx`
Expected: every test passes.

- [ ] **Step 5: Add the end-to-end file**

This shows a value with a comma reaching the generator through the MSBuild files the package ships.

Create `tests/SqlSource.Tests/EndToEnd/Dialects/ByOption.sql`:

```sql
-- The item of this file has the metadata SqlSourceDialect="mysql, no-backslash-escapes".
SELECT 'C:\temp\' AS path; -- a comment
```

In `tests/SqlSource.Tests/SqlSource.Tests.csproj`, add inside the `ItemGroup` that holds `ByMetadata.sql`, after that item:

```xml
        <AdditionalFiles Update="EndToEnd/Dialects/ByOption.sql">
            <SqlSourceDialect>
                mysql, no-backslash-escapes
            </SqlSourceDialect>
        </AdditionalFiles>
```

In `tests/SqlSource.Tests/EndToEnd/EndToEndTests.cs`, add after `ProjectWithADialect_FileWithMetadata_IsReadByTheDialectOfItsItem`:

```csharp
    // The item of ByOption.sql names an option after the dialect, with a comma and a space, over several lines.
    // Plain MySQL would not close the string, and this project would not build.
    [Fact]
    public void ProjectWithADialect_FileWithAnOptionInItsMetadata_IsReadByThatOption() =>
        DialectQueries.ByOption.ShouldBe("SELECT 'C:\\temp\\' AS path;");
```

Run: `dotnet test --solution SqlSource.slnx`
Expected: every test passes.

If the build fails with `SQLSRC011` or `SQLSRC101` for `ByOption.sql`, the comma did not survive the path from MSBuild to the compiler.  Stop and tell the user: the syntax the spec chose does not work as written, and that is the user's decision to remake.  Do not change the separator yourself.

- [ ] **Step 6: Commit**

Run `./format.sh`, then `./pre-commit-validation.sh`; both must pass.  Then, in a command of its own:

```bash
git add src/SqlSource tests/SqlSource.Tests
git commit -m "Read the options of a dialect from the value that names it" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Continued strings

**Files:**
- Create: `src/SqlSource/Parsing/SqlStringContinuation.cs`
- Modify: `src/SqlSource/Parsing/Quoting/QuoteReader.cs`
- Modify: `src/SqlSource/Parsing/Quoting/EscapeStringReader.cs`
- Modify: `src/SqlSource/Parsing/SqlDialectRules.cs`
- Modify: `src/SqlSource/Parsing/SqlLexer.cs`
- Modify: `tests/SqlSource.Tests/Parsing/Quoting/EscapeStringReaderTests.cs`
- Modify: `tests/SqlSource.Tests/Parsing/SqlDialectRulesTests.cs`
- Modify: `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs`
- Modify: `tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs`
- Modify: `tests/SqlSource.Tests/EndToEnd/Dialects/ByProperty.sql`

**Interfaces:**
- Consumes: nothing new from Tasks 1 to 3.
- Produces:
  - `internal enum SqlStringContinuation { None, AcrossWhitespace, AcrossLineComments }`
  - `public SqlStringContinuation SqlDialectRules.StringContinuation { get; }`, `AcrossLineComments` for `PostgreSql` and `None` for every other dialect.
  - `public virtual QuoteReader? QuoteReader.FindContinuation(string text, int start)`, null by default.
  - `EscapeStringReader(string prefixes)`: the characters that give a string backslash escapes.  It reads one part.

**Notes for the implementer:**
- PostgreSQL's `scan.l` allows whitespace and `--` comments between the parts of a string, with at least one line break.  A block comment is not allowed there.
- A part that continues a string keeps the pending reader for a part after it.  The spec says "step 1 applies to it again"; the Backslash reader names no continuation, so the lexer keeps the one it has.  The outcome is the spec's: a third part continues the second.
- A line comment ends before its line break, so the line break after it is found as whitespace of the gap.  Nothing special is needed for it.

- [ ] **Step 1: Write the failing tests**

Replace the whole of `tests/SqlSource.Tests/Parsing/Quoting/EscapeStringReaderTests.cs` with:

```csharp
using Shouldly;
using SqlSource.Parsing.Quoting;
using Xunit;

namespace SqlSource.Tests.Parsing.Quoting;

public class EscapeStringReaderTests
{
    private static readonly EscapeStringReader Reader = new("Ee");

    [Theory]
    // Without the prefix a backslash is an ordinary character.
    [InlineData("'a\\' x", "'a\\'")]
    [InlineData("'it''s' x", "'it''s'")]
    [InlineData("x 'a\\' y", "'a\\'")]
    // With it, a backslash takes the next character with it.
    [InlineData("E'it\\'s' x", "'it\\'s'")]
    [InlineData("e'it\\'s' x", "'it\\'s'")]
    [InlineData("(E'a\\'b') x", "'a\\'b'")]
    [InlineData("E'a''b\\\\' x", "'a''b\\\\'")]
    [InlineData("x = E'a\\'b' y", "'a\\'b'")]
    public void FindEnd_String_TakesBackslashEscapesOnlyAfterThePrefix(string text, string expected) =>
        Region.Read(Reader, text, '\'').ShouldBe(expected);

    // The E ends an identifier, so it is not a prefix.
    [Theory]
    [InlineData("typeE'a\\' x", "'a\\'")]
    [InlineData("_e'a\\' x", "'a\\'")]
    [InlineData("a$E'a\\' x", "'a\\'")]
    [InlineData("1e'a\\' x", "'a\\'")]
    public void FindEnd_LetterEAtTheEndOfAnIdentifier_IsNotAPrefix(string text, string expected) =>
        Region.Read(Reader, text, '\'').ShouldBe(expected);

    [Theory]
    [InlineData("E'abc")]
    [InlineData("E'abc\\'")]
    [InlineData("E'abc\\")]
    [InlineData("'abc")]
    public void FindEnd_StringWithoutAClosingQuote_IsUnterminated(string text) =>
        Region.Read(Reader, text, '\'').ShouldBe(Region.Unterminated);

    // A reader reads one part.  Whether a string goes on after a gap is for the lexer.
    [Theory]
    [InlineData("E'a'\n'b\\'c' x")]
    [InlineData("E'a' -- c\n'b\\'c' x")]
    [InlineData("E'a'\r\n    'b\\'c' x")]
    public void FindEnd_EscapeStringFollowedByAnotherPart_EndsAtItsOwnQuote(string text) =>
        Region.Read(Reader, text, '\'').ShouldBe("'a'");

    [Theory]
    [InlineData("E'a' x", 1)]
    [InlineData("x = e'a\\'b' y", 5)]
    public void FindContinuation_StringWithThePrefix_NamesAReaderThatTakesBackslashEscapes(string text, int start)
    {
        var continuation = Reader.FindContinuation(text, start).ShouldNotBeNull();

        continuation.ShouldBeOfType<BackslashQuoteReader>();
        continuation.FindContinuation(text, start).ShouldBeNull();
    }

    [Theory]
    [InlineData("'a' x", 0)]
    [InlineData("typeE'a' x", 5)]
    [InlineData("b'a' x", 1)]
    public void FindContinuation_StringWithoutThePrefix_NamesNone(string text, int start) =>
        Reader.FindContinuation(text, start).ShouldBeNull();

    [Fact]
    public void FindContinuation_AnyReaderWithAPrefix_NamesTheOneSharedReader() =>
        Reader.FindContinuation("E'a'", 1).ShouldBeSameAs(new EscapeStringReader("Ee").FindContinuation("e'b'", 1));
}
```

In `tests/SqlSource.Tests/Parsing/SqlDialectRulesTests.cs`, add after `CommentRules_OfEachDialect_AreTheRowOfTheTable`:

```csharp
    [Theory]
    [InlineData("Ansi", "None")]
    [InlineData("SqlServer", "None")]
    [InlineData("PostgreSql", "AcrossLineComments")]
    [InlineData("MySql", "None")]
    [InlineData("MariaDb", "None")]
    [InlineData("Sqlite", "None")]
    [InlineData("Oracle", "None")]
    public void StringContinuation_OfEachDialect_IsTheRowOfTheTable(string dialect, string expected) =>
        SqlDialectRules.For(Enum.Parse<SqlDialect>(dialect)).StringContinuation.ToString().ShouldBe(expected);
```

In `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs`:

In `Lex_Construct_IsReadByTheRulesOfTheDialect`, replace the `PostgreSql` row of the continued string:

```csharp
    [InlineData("PostgreSql", "E'a'\n'b\\'c' -- d'", "Text:E | Quoted:'a'\n'b\\'c' | Text:  | LineComment:-- d'")]
```

with:

```csharp
    [InlineData(
        "PostgreSql",
        "E'a'\n'b\\'c' -- d'",
        "Text:E | Quoted:'a' | Text:\n | Quoted:'b\\'c' | Text:  | LineComment:-- d'"
    )]
```

Leave the `Ansi` row below it as it is.

In `Lex_UnterminatedQuoteOfADialect_ReportsTheOpeningDelimiter`, replace:

```csharp
    // The second part of a continued string is not closed.  The error is at the quote that opened the string.
    [InlineData(nameof(SqlDialect.PostgreSql), "E'a'\n'b\\'", 1)]
```

with:

```csharp
    // The second part of a continued string is not closed.  The error is at the quote of that part.
    [InlineData(nameof(SqlDialect.PostgreSql), "E'a'\n'b\\'", 5)]
    [InlineData(nameof(SqlDialect.PostgreSql), "E'a' -- c\n'b\\'c'\n  'd\\'", 19)]
```

Add after `Lex_HashInMySql_StartsACommentToTheEndOfItsLine`:

```csharp
    // PostgreSQL reads a string that is followed by whitespace with a line break, and then a quote, as one string
    // with the part after the quote.  A -- comment may stand in the gap.  After an E string the later parts take
    // backslash escapes too.  Each part is a lexeme of its own, and the gap is ordinary text and comments.
    [Theory]
    [InlineData("E'a'\n'b\\'c' x", "Text:E | Quoted:'a' | Text:\n | Quoted:'b\\'c' | Text: x")]
    [InlineData("e'a'\n'b\\'c' x", "Text:e | Quoted:'a' | Text:\n | Quoted:'b\\'c' | Text: x")]
    // Each kind of line break, and blank lines.
    [InlineData("E'a'\r\n    'b\\'c' x", "Text:E | Quoted:'a' | Text:\r\n     | Quoted:'b\\'c' | Text: x")]
    [InlineData("E'a'\r'b\\'c' x", "Text:E | Quoted:'a' | Text:\r | Quoted:'b\\'c' | Text: x")]
    [InlineData("E'a' \n\n\t'b\\'c' x", "Text:E | Quoted:'a' | Text: \n\n\t | Quoted:'b\\'c' | Text: x")]
    // A comment before the line break, and comments on lines of their own.
    [InlineData(
        "E'a' -- c\n'b\\'d' x",
        "Text:E | Quoted:'a' | Text:  | LineComment:-- c | Text:\n | Quoted:'b\\'d' | Text: x"
    )]
    [InlineData(
        "E'a'\n-- c\n  -- d\r\n'b\\'e' x",
        "Text:E | Quoted:'a' | Text:\n | LineComment:-- c | Text:\n   | LineComment:-- d | Text:\r\n | Quoted:'b\\'e'"
            + " | Text: x"
    )]
    // A marker is a comment to the lexer.  The part after it is still read as PostgreSQL reads it.
    [InlineData(
        "E'a'\n-- name: B\n'b\\'c' x",
        "Text:E | Quoted:'a' | Text:\n | LineComment:-- name: B | Text:\n | Quoted:'b\\'c' | Text: x"
    )]
    // A third part continues the second.
    [InlineData(
        "E'a'\n'b\\'c'\n -- d\n 'e\\'f' x",
        "Text:E | Quoted:'a' | Text:\n | Quoted:'b\\'c' | Text:\n  | LineComment:-- d | Text:\n  | Quoted:'e\\'f'"
            + " | Text: x"
    )]
    public void Lex_EscapeStringOfPostgreSqlThatGoesOnAfterAGap_ReadsEachPartWithBackslashEscapes(
        string text,
        string expected
    ) => string.Join(" | ", Lex(text, SqlDialectRules.PostgreSql)).ShouldBe(expected);

    // Each of these would fail with an unclosed quote if the second string took backslash escapes.
    [Theory]
    // No line break in the gap.
    [InlineData("E'a' 'b\\' x", "Text:E | Quoted:'a' | Text:  | Quoted:'b\\' | Text: x")]
    // Something other than whitespace and -- comments in the gap.
    [InlineData("E'a',\n'b\\' x", "Text:E | Quoted:'a' | Text:,\n | Quoted:'b\\' | Text: x")]
    [InlineData("E'a'\n- 'b\\' x", "Text:E | Quoted:'a' | Text:\n-  | Quoted:'b\\' | Text: x")]
    [InlineData("E'a'\n|| 'b\\' x", "Text:E | Quoted:'a' | Text:\n||  | Quoted:'b\\' | Text: x")]
    [InlineData(
        "E'a' /* c */\n'b\\' x",
        "Text:E | Quoted:'a' | Text:  | BlockComment:/* c */ | Text:\n | Quoted:'b\\' | Text: x"
    )]
    [InlineData(
        "E'a' /*+ h */\n'b\\' x",
        "Text:E | Quoted:'a' | Text:  | Hint:/*+ h */ | Text:\n | Quoted:'b\\' | Text: x"
    )]
    [InlineData(
        "E'a'\n\"q\"\n'b\\' x",
        "Text:E | Quoted:'a' | Text:\n | Quoted:\"q\" | Text:\n | Quoted:'b\\' | Text: x"
    )]
    [InlineData(
        "E'a'\n$$q$$\n'b\\' x",
        "Text:E | Quoted:'a' | Text:\n | Quoted:$$q$$ | Text:\n | Quoted:'b\\' | Text: x"
    )]
    // The first string has no prefix, so no part of it takes backslash escapes.
    [InlineData("'a'\n'b\\' x", "Quoted:'a' | Text:\n | Quoted:'b\\' | Text: x")]
    [InlineData("typeE'a'\n'b\\' x", "Text:typeE | Quoted:'a' | Text:\n | Quoted:'b\\' | Text: x")]
    // A string on the same line as a continued part is not a part of it.
    [InlineData(
        "E'a'\n'b\\'c' 'd\\' x",
        "Text:E | Quoted:'a' | Text:\n | Quoted:'b\\'c' | Text:  | Quoted:'d\\' | Text: x"
    )]
    // Nothing after the line break.
    [InlineData("E'a'\n", "Text:E | Quoted:'a' | Text:\n")]
    [InlineData("E'a'\n x", "Text:E | Quoted:'a' | Text:\n x")]
    [InlineData("E'a' -- c", "Text:E | Quoted:'a' | Text:  | LineComment:-- c")]
    public void Lex_StringOfPostgreSqlThatIsNotContinued_IsReadByItsOwnPrefix(string text, string expected) =>
        string.Join(" | ", Lex(text, SqlDialectRules.PostgreSql)).ShouldBe(expected);

    // The union of rules that is ANSI continues no string, and neither does a dialect that has no E strings.
    [Theory]
    [InlineData(nameof(SqlDialect.Ansi), "E'a'\n'b\\' x", "Text:E | Quoted:'a' | Text:\n | Quoted:'b\\' | Text: x")]
    [InlineData(nameof(SqlDialect.MySql), "E'a'\n'b\\'c' x", "Text:E | Quoted:'a' | Text:\n | Quoted:'b\\'c' | Text: x")]
    [InlineData(nameof(SqlDialect.Sqlite), "E'a'\n'b\\' x", "Text:E | Quoted:'a' | Text:\n | Quoted:'b\\' | Text: x")]
    public void Lex_StringOnTheLineAfterAnEscapeStringInAnotherDialect_IsAStringOfItsOwn(
        string dialect,
        string text,
        string expected
    ) => string.Join(" | ", Lex(text, Rules(dialect))).ShouldBe(expected);
```

Add after `TryReadLeadingComment_ThenReadToEnd_GivesTheLexemesOfLexingInOneCall`:

```csharp
    [Fact]
    public void TryReadLeadingComment_ThenReadToEnd_ReadsAContinuedStringAsLexingInOneCallDoes()
    {
        const string Text = "-- a\nSELECT E'b' -- c\n'd\\'e' -- f\n";
        var lexer = new SqlLexer(Text, SqlDialectRules.PostgreSql);

        _ = ReadLeadingComments(lexer, Text);

        lexer.ReadToEnd().ShouldBe(SqlLexer.Lex(Text, SqlDialectRules.PostgreSql));
    }
```

In `tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs`, add after the tests of Task 3:

```csharp
    // The comments between the parts are removed with the rest.  The line break stays, so PostgreSQL still reads
    // one string.
    [Fact]
    public void Parse_ContinuedStringWithCommentsBetweenItsParts_StripsThemAndKeepsTheLineBreak()
    {
        const string Text =
            "-- name: A\nSELECT E'it' -- first  \n\n    -- second\n    '\\'s -- not a comment' AS note; -- c\n";

        Sql(Blocks(Text, dialect: SqlDialect.PostgreSql).ShouldHaveSingleItem())
            .ShouldBe("SELECT E'it'\n    '\\'s -- not a comment' AS note;");
    }

    [Fact]
    public void Parse_ContinuedStringUnderKeepComments_KeepsTheCommentsBetweenItsParts()
    {
        const string Text =
            "-- SqlSource: keep-comments\n-- name: A\nSELECT E'it' -- first\n    '\\'s' AS note; -- c\n";

        Sql(Blocks(Text, dialect: SqlDialect.PostgreSql).ShouldHaveSingleItem())
            .ShouldBe("SELECT E'it' -- first\n    '\\'s' AS note; -- c");
    }

    // A marker is a marker wherever a comment can be.  Both halves are still read as PostgreSQL reads them.
    [Fact]
    public void Parse_NameMarkerBetweenThePartsOfAContinuedString_StartsABlockThere()
    {
        const string Text = "-- name: A\nSELECT E'a'\n-- name: B\n'b\\'c' AS x; -- d\n";

        Blocks(Text, dialect: SqlDialect.PostgreSql).Select(Sql).ShouldBe(["SELECT E'a'", "'b\\'c' AS x;"]);
    }

    [Fact]
    public void Parse_ContinuedStringWithAnUnclosedPart_IsAnErrorAtTheQuoteOfThatPart()
    {
        const string Text = "-- name: A\nSELECT E'a' -- c\n    'b\\' AS x;\n";

        Errors(Text, dialect: SqlDialect.PostgreSql)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.UnterminatedQuote, new TextSpan(Text.IndexOf("'b", StringComparison.Ordinal), 1)),
            ]);
    }
```

Let `./format.sh` wrap the long line of the last test.

Replace `tests/SqlSource.Tests/EndToEnd/Dialects/ByProperty.sql` with:

```sql
-- The project's dialect is postgres, which reads the third line as part of the escape string.
SELECT E'it' -- a comment between the parts
    '\'s -- not a comment' AS note; -- a comment
```

`ProjectWithADialect_FileWithoutItsOwn_IsReadByTheDialectOfTheProject` in `EndToEndTests.cs` keeps its expectation, `"SELECT E'it'\n    '\\'s -- not a comment' AS note;"`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build tests/SqlSource.Tests`
Expected: the build fails.  `EscapeStringReader` has no constructor that takes a string, and `FindContinuation` and `StringContinuation` do not exist.

- [ ] **Step 3: Write the implementation**

Create `src/SqlSource/Parsing/SqlStringContinuation.cs`:

```csharp
namespace SqlSource.Parsing;

/// <summary>
/// Whether a string goes on after it closes, and what may stand in the gap before its next part.  A gap always
/// needs a line break.
/// </summary>
internal enum SqlStringContinuation
{
    /// <summary>A string ends at its closing quote.</summary>
    None,

    /// <summary>Only whitespace may stand in the gap, as in CockroachDB.</summary>
    AcrossWhitespace,

    /// <summary>Whitespace and line comments may stand in the gap, as in PostgreSQL.</summary>
    AcrossLineComments,
}
```

In `src/SqlSource/Parsing/Quoting/QuoteReader.cs`, add after `FindEnd`:

```csharp
    /// <summary>
    /// The reader of a part that continues the region that opens at <paramref name="start" />, or null when a
    /// later part would be read as the region itself.  A reader reads one part: whether another follows, and after
    /// what, is for the lexer and the rules of the dialect.
    /// </summary>
    public virtual QuoteReader? FindContinuation(string text, int start) => null;
```

Replace the whole of `src/SqlSource/Parsing/Quoting/EscapeStringReader.cs` with:

```csharp
namespace SqlSource.Parsing.Quoting;

/// <summary>
/// A <c>'...'</c> string that takes backslash escapes only after a prefix, as in PostgreSQL's <c>E'it\'s'</c>.  The
/// prefix stays in the text before the region.
/// </summary>
/// <param name="prefixes">
/// The characters that are a prefix, each in the case it is accepted in: <c>Ee</c> for PostgreSQL.
/// </param>
internal sealed class EscapeStringReader(string prefixes) : QuoteReader
{
    // A part that continues an escape string takes backslash escapes with no prefix of its own.
    private static readonly QuoteReader Continued = new BackslashQuoteReader();

    public override int FindEnd(string text, int start) =>
        HasPrefix(text, start) ? FindBackslashEnd(text, start) : FindDoubledEnd(text, start);

    public override QuoteReader? FindContinuation(string text, int start) =>
        HasPrefix(text, start) ? Continued : null;

    // A prefix is one character that does not end an identifier: the E of typeE'a' is not one.
    private bool HasPrefix(string text, int quote) =>
        quote > 0
        && prefixes.IndexOf(text[quote - 1]) >= 0
        && (quote < 2 || !IsIdentifierCharacter(text[quote - 2]));
}
```

In `src/SqlSource/Parsing/SqlDialectRules.cs`:

- Replace the two fields `EscapeString` and `ContinuedEscapeString` with one:

  ```csharp
      private static readonly QuoteReader EscapeString = new EscapeStringReader("Ee");
  ```

- Replace the property `PostgreSql` with:

  ```csharp
      public static SqlDialectRules PostgreSql { get; } =
          new(('\'', EscapeString), ('"', Doubled), ('$', Dollar))
          {
              NestedComments = true,
              StringContinuation = SqlStringContinuation.AcrossLineComments,
          };
  ```

- Add after the property `MariaDbHints`:

  ```csharp
      /// <summary>
      /// Whether a string goes on after a gap with a line break, and what the gap may hold.  It matters after a
      /// string whose reader names a continuation: the part after the gap is then read by that reader.
      /// </summary>
      public SqlStringContinuation StringContinuation { get; private init; }
  ```

In `src/SqlSource/Parsing/SqlLexer.cs`:

Replace the class remarks with:

```csharp
/// <remarks>
/// The lexer reads by the <see cref="SqlDialectRules" /> it holds and names no dialect and no quoting form.  It knows
/// nothing about markers.  Where the rules leave a choice it keeps text, because a comment left in the SQL is
/// harmless and SQL removed from it is a bug.  The one thing it carries from a lexeme to the next is a string that
/// may go on after a gap: each part of it is a quoted region of its own, and the gap is ordinary text and comments.
/// </remarks>
```

Add these fields after `_textStart`:

```csharp
    // A string that may go on after a gap: the reader of its next part, or null when no string is waiting for one;
    // the quote that opens that part; how far the gap has been checked; and whether it has held a line break.
    private QuoteReader? _continuation;
    private char _continuationQuote;
    private int _gapStart;
    private bool _gapHasLineBreak;
```

Replace `Step` with:

```csharp
    private void Step()
    {
        var start = Position;
        var current = text[start];
        if (_continuation is not null)
        {
            ReadGap(start);
        }

        if (current == '-' && IsDashComment(start))
        {
            var isHint = IsLineHint(start);
            Add(isHint ? SqlLexemeKind.Hint : SqlLexemeKind.LineComment, start, FindLineEnd(start));
            PassGap(!isHint && Rules.StringContinuation == SqlStringContinuation.AcrossLineComments);
        }
        else if (current == '#' && Rules.HashComments)
        {
            Add(SqlLexemeKind.LineComment, start, FindLineEnd(start));
            PassGap(allowed: false);
        }
        else if (current == '/' && CharAt(start + 1) == '*')
        {
            ReadBlockComment(start);
            PassGap(allowed: false);
        }
        else if (Rules.ReaderFor(current) is { } reader)
        {
            ReadQuoted(reader, start);
        }
        else
        {
            Position++;
            PassGap(allowed: false);
        }
    }

    // Checks the text of the gap up to end, where something other than plain text starts.  Anything but whitespace
    // there ends the string that was waiting for a part.
    private void ReadGap(int end)
    {
        for (var index = _gapStart; index < end; index++)
        {
            var value = text[index];
            if (!char.IsWhiteSpace(value))
            {
                _continuation = null;
                return;
            }

            _gapHasLineBreak |= value is '\r' or '\n';
        }

        _gapStart = end;
    }

    // What was just read stood after a string.  The string can still go on only if its gap may hold that.
    private void PassGap(bool allowed)
    {
        if (allowed)
        {
            _gapStart = Position;
        }
        else
        {
            _continuation = null;
        }
    }
```

Replace `ReadQuoted` with:

```csharp
    private void ReadQuoted(QuoteReader reader, int start)
    {
        // A part that continues a string is read as the string was, whatever stands before its own quote.
        var pending = _gapHasLineBreak && text[start] == _continuationQuote ? _continuation : null;
        var end = (pending ?? reader).FindEnd(text, start);
        if (end == QuoteReader.NotAQuote)
        {
            Position++;
            _continuation = null;
        }
        else if (end == QuoteReader.Unterminated)
        {
            _error = SqlParseError.Create(SqlParseErrorKind.UnterminatedQuote, new TextSpan(start, 1));
        }
        else
        {
            Add(SqlLexemeKind.Quoted, start, end);
            if (pending is null)
            {
                _continuation =
                    Rules.StringContinuation == SqlStringContinuation.None
                        ? null
                        : reader.FindContinuation(text, start);
                _continuationQuote = text[start];
            }

            _gapStart = end;
            _gapHasLineBreak = false;
        }
    }
```

A part read by the pending reader leaves `_continuation` as it is, so a further part can follow it.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --solution SqlSource.slnx`
Expected: every test passes, `SqlFileParserAllocationTests` and the end-to-end `ProjectWithADialect_FileWithoutItsOwn_IsReadByTheDialectOfTheProject` among them.

- [ ] **Step 5: Commit**

Run `./format.sh`, then `./pre-commit-validation.sh`; both must pass.  Then, in a command of its own:

```bash
git add src/SqlSource tests/SqlSource.Tests
git commit -m "Read a continued PostgreSQL string part by part, with comments between" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: The `cockroachdb` dialect

**Files:**
- Modify: `src/SqlSource/Parsing/SqlDialect.cs`
- Modify: `src/SqlSource/Parsing/SqlDialectName.cs`
- Modify: `src/SqlSource/Parsing/SqlDialectRules.cs`
- Modify: `src/SqlSource/Diagnostics/SqlDiagnostics.cs`
- Modify: `tests/SqlSource.Tests/Parsing/SqlDialectNameTests.cs`
- Modify: `tests/SqlSource.Tests/Parsing/SqlDialectRulesTests.cs`
- Modify: `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs`
- Modify: `tests/SqlSource.Tests/Parsing/Quoting/EscapeStringReaderTests.cs`
- Modify: `tests/SqlSource.Tests/Generator/DialectTests.cs`

**Interfaces:**
- Consumes: `EscapeStringReader(string prefixes)`, `SqlStringContinuation` and the lexer of Task 4; `SqlDialectName` of Task 3.
- Produces: `SqlDialect.CockroachDb`, the last member of the enum; `SqlDialectRules.CockroachDb`; the names `cockroachdb` and `cockroach`; `SqlDialectName.Accepted` is `"ansi, mssql, postgres, cockroachdb, mysql, mariadb, sqlite and oracle"`.

**Notes for the implementer:**
- From CockroachDB's `scan.go`: `b'` with a lower-case `b` takes backslash escapes; `B'` is a bit string and `x'` a hex string, with none.  A continued string allows only whitespace in its gap.  Block comments nest.
- `postgres` keeps reading `b'\''` as PostgreSQL does.

- [ ] **Step 1: Write the failing tests**

In `tests/SqlSource.Tests/Parsing/SqlDialectNameTests.cs`:

- Add to `TryParse_NameOrAlias_GivesItsDialect`, after the `oracle` row:

  ```csharp
      [InlineData("cockroachdb", nameof(SqlDialect.CockroachDb))]
      [InlineData("cockroach", nameof(SqlDialect.CockroachDb))]
      [InlineData("CockroachDB", nameof(SqlDialect.CockroachDb))]
  ```

- Add to `TryParse_AnythingElse_IsRejected`: `[InlineData("cockroachdb,ansi-quotes")]` and `[InlineData("crdb")]`.
- In `TryParse_EveryDialect_HasAName`, replace the array with:

  ```csharp
          string[] names = ["ansi", "mssql", "postgres", "mysql", "mariadb", "sqlite", "oracle", "cockroachdb"];
  ```

- Replace the expectation of `Accepted_ListsTheNameOfEveryDialect` with `"ansi, mssql, postgres, cockroachdb, mysql, mariadb, sqlite and oracle"`.

In `tests/SqlSource.Tests/Parsing/SqlDialectRulesTests.cs`:

- In `For_EveryDialect_GivesItsOwnSharedInstance`, add as the last line:

  ```csharp
          SqlDialectRules.For(SqlDialect.CockroachDb).ShouldBeSameAs(SqlDialectRules.CockroachDb);
  ```

- Add the row `[InlineData("CockroachDb", "'\"$")]` to `ReaderFor_Character_HasAReaderOnlyWhereTheDialectOpensAQuotedRegion`.
- Add the row `[InlineData("CockroachDb", true, false, false, false, false)]` to `CommentRules_OfEachDialect_AreTheRowOfTheTable`.
- Add the row `[InlineData("CockroachDb", "AcrossWhitespace")]` to `StringContinuation_OfEachDialect_IsTheRowOfTheTable`.

In `tests/SqlSource.Tests/Parsing/Quoting/EscapeStringReaderTests.cs`, add at the end of the class:

```csharp
    // CockroachDB's bytes literal.  The prefix is the lower-case letter only: B'...' is a bit string.
    [Theory]
    [InlineData("b'a\\'b' x", "'a\\'b'")]
    [InlineData("E'a\\'b' x", "'a\\'b'")]
    [InlineData("B'a\\' x", "'a\\'")]
    [InlineData("ab'a\\' x", "'a\\'")]
    [InlineData("x'a\\' x", "'a\\'")]
    public void FindEnd_ReaderWithTheBytesPrefix_TakesBackslashEscapesAfterItToo(string text, string expected) =>
        Region.Read(new EscapeStringReader("Eeb"), text, '\'').ShouldBe(expected);

    [Fact]
    public void FindContinuation_ReaderWithTheBytesPrefix_NamesAReaderAfterABytesLiteral()
    {
        var reader = new EscapeStringReader("Eeb");

        reader.FindContinuation("b'a'", 1).ShouldBeOfType<BackslashQuoteReader>();
        reader.FindContinuation("B'a'", 1).ShouldBeNull();
    }
```

In `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs`:

- Replace the constant `AllDialects` with:

  ```csharp
      private const string AllDialects = "Ansi,SqlServer,PostgreSql,CockroachDb,MySql,MariaDb,Sqlite,Oracle";
  ```

- In `Lex_Construct_IsReadByTheRulesOfTheDialect`, add `,CockroachDb` after `PostgreSql` in the dialect list of every row that names `PostgreSql` today.  CockroachDB reads each of those constructs as PostgreSQL does, the row of the continued string included.  There are thirteen such rows; `"PostgreSql"` alone becomes `"PostgreSql,CockroachDb"`.  Do this before the next change, whose second row names `PostgreSql` without `CockroachDb` on purpose.
- In the same theory, then add before the comment `// Oracle's quote operator.`, exactly as written:

  ```csharp
      // CockroachDB's bytes literal.  MySQL and MariaDB take the backslash whatever stands before the quote.
      [InlineData(
          "CockroachDb,MySql,MariaDb",
          "b'a\\'b' -- c'",
          "Text:b | Quoted:'a\\'b' | Text:  | LineComment:-- c'"
      )]
      [InlineData(
          "Ansi,SqlServer,PostgreSql,Sqlite,Oracle",
          "b'a\\'b' -- c'",
          "Text:b | Quoted:'a\\' | Text:b | Quoted:' -- c'"
      )]
  ```

- In `Lex_UnterminatedQuoteOfADialect_ReportsTheOpeningDelimiter`, replace the comment above the last row:

  ```csharp
      // CockroachDB reads b'\'' as one literal.  The PostgreSQL rules have no backslash escape there.
  ```

  with:

  ```csharp
      // PostgreSQL has no backslash escape in b'...'.  CockroachDB has one, under its own dialect.
  ```

  The comment above the `MySql` row still names `docs/tech-debt/TD-0004`; change its first sentence from `Two readings that differ from the database, which docs/tech-debt/TD-0004 lists.` to `A reading that differs from the database, which docs/tech-debt/TD-0004 lists.`

- Add after `Lex_StringOnTheLineAfterAnEscapeStringInAnotherDialect_IsAStringOfItsOwn`:

  ```csharp
      [Theory]
      [InlineData("b'\\''", "Text:b | Quoted:'\\''")]
      // A bit string and a hex string take no escapes, and neither does a b that ends an identifier.
      [InlineData("B'a\\' x", "Text:B | Quoted:'a\\' | Text: x")]
      [InlineData("x'a\\' x", "Text:x | Quoted:'a\\' | Text: x")]
      [InlineData("ab'a\\' x", "Text:ab | Quoted:'a\\' | Text: x")]
      // A string goes on after whitespace with a line break, and a part after a bytes literal takes escapes too.
      [InlineData("E'a'\n'b\\'c' x", "Text:E | Quoted:'a' | Text:\n | Quoted:'b\\'c' | Text: x")]
      [InlineData("b'a'\r\n  'b\\'c' x", "Text:b | Quoted:'a' | Text:\r\n   | Quoted:'b\\'c' | Text: x")]
      // A comment in the gap ends the string, where PostgreSQL would go on.
      [InlineData(
          "E'a' -- c\n'b\\' x",
          "Text:E | Quoted:'a' | Text:  | LineComment:-- c | Text:\n | Quoted:'b\\' | Text: x"
      )]
      [InlineData(
          "E'a'\n-- c\n'b\\' x",
          "Text:E | Quoted:'a' | Text:\n | LineComment:-- c | Text:\n | Quoted:'b\\' | Text: x"
      )]
      // A reading that differs from the database, which docs/tech-debt/TD-0004 lists.  CockroachDB ends a line only
      // at a line feed, so to it this gap holds no line break.
      [InlineData("E'a'\r'b\\'c' x", "Text:E | Quoted:'a' | Text:\r | Quoted:'b\\'c' | Text: x")]
      public void Lex_StringInCockroachDb_IsReadByItsRules(string text, string expected) =>
          string.Join(" | ", Lex(text, SqlDialectRules.CockroachDb)).ShouldBe(expected);
  ```

In `tests/SqlSource.Tests/Generator/DialectTests.cs`:

- Replace the constant `Invalid` with:

  ```csharp
      private const string Invalid =
          "' is not a SQL dialect.  SqlSourceDialect accepts ansi, mssql, postgres, cockroachdb, mysql, mariadb, "
          + "sqlite and oracle.";
  ```

- Add after `Run_DirectiveWithAnOptionThatIsNotValid_IsAnErrorAtTheDirective`:

  ```csharp
      // A bytes literal that ends with an escaped quote.  PostgreSQL has no escape there, and does not close it.
      [Theory]
      [InlineData("cockroachdb", null)]
      [InlineData("Cockroach", null)]
      [InlineData("postgres", "SQLSRC101 ")]
      public void Run_BytesLiteralWithAnEscapedQuote_IsReadOnlyByCockroachDb(string property, string? error)
      {
          var run = Run(property, new SqlFile("/app/Repo/Q.sql", "SELECT b'\\'' AS x; -- c\n"));

          if (error is null)
          {
              run.Diagnostics.ShouldBeEmpty();
              run.CompilationErrors.ShouldBeEmpty();
          }
          else
          {
              run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith(error);
          }
      }
  ```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build tests/SqlSource.Tests`
Expected: the build fails with CS0117, `SqlDialect` does not contain a definition for `CockroachDb`.

- [ ] **Step 3: Write the implementation**

In `src/SqlSource/Parsing/SqlDialect.cs`, add after `Oracle`:

```csharp

    /// <summary>CockroachDB: PostgreSQL's rules, with its own bytes literal and its own gap in a continued string.</summary>
    CockroachDb,
```

If the line is longer than 120 characters, break the summary over three lines.

In `src/SqlSource/Parsing/SqlDialectName.cs`:

- Replace `Accepted` with:

  ```csharp
      public const string Accepted = "ansi, mssql, postgres, cockroachdb, mysql, mariadb, sqlite and oracle";
  ```

- Add to `Names`, after the `postgresql` entry:

  ```csharp
          ("cockroachdb", SqlDialect.CockroachDb),
          ("cockroach", SqlDialect.CockroachDb),
  ```

In `src/SqlSource/Parsing/SqlDialectRules.cs`:

- Add after the field `EscapeString`:

  ```csharp
      // CockroachDB's bytes literal, b'...', takes backslash escapes as an E string does.
      private static readonly QuoteReader BytesEscapeString = new EscapeStringReader("Eeb");
  ```

- Add after the property `PostgreSql`:

  ```csharp
      public static SqlDialectRules CockroachDb { get; } =
          new(('\'', BytesEscapeString), ('"', Doubled), ('$', Dollar))
          {
              NestedComments = true,
              StringContinuation = SqlStringContinuation.AcrossWhitespace,
          };
  ```

- Add to the switch of `For`, after the `Oracle` arm: `SqlDialect.CockroachDb => CockroachDb,`.

In `src/SqlSource/Diagnostics/SqlDiagnostics.cs`, replace the `messageFormat` of `InvalidDialect` with:

```csharp
        messageFormat: "'{0}' is not a SQL dialect.  SqlSourceDialect accepts ansi, mssql, postgres, cockroachdb, "
            + "mysql, mariadb, sqlite and oracle.",
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --solution SqlSource.slnx`
Expected: every test passes.  The tests that loop over `Enum.GetValues<SqlDialect>()` now cover `CockroachDb` too.

- [ ] **Step 5: Commit**

Run `./format.sh`, then `./pre-commit-validation.sh`; both must pass.  Then, in a command of its own:

```bash
git add src/SqlSource tests/SqlSource.Tests
git commit -m "Add a cockroachdb dialect, with its bytes literal" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: The documents

**Files:**
- Modify: `README.md`
- Modify: `docs/diagnostics.md`
- Modify: `docs/tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md`
- Modify: `docs/tech-debt/README.md`
- Modify: `docs/deferred/D-0001-dialects-not-delivered.md`
- Modify: `docs/deferred/README.md`
- Modify: `src/SqlSource/AGENTS.md`

**Interfaces:**
- Consumes: the behaviour of Tasks 1 to 5.
- Produces: nothing that code uses.

`README.md` is the package readme on nuget.org: every link in it is an absolute URL.

- [ ] **Step 1: `README.md`, the dialect table**

Replace the `postgres` row of the table under `## Dialects` with these two rows:

```markdown
| `postgres` | `postgresql` | PostgreSQL, DuckDB |
| `cockroachdb` | `cockroach` | CockroachDB |
```

- [ ] **Step 2: `README.md`, the options**

Insert this section before the heading `### What a dialect changes`:

````markdown
### Options of a dialect

MySQL and MariaDB have SQL modes that change how a string is read.  If your server runs with one, name it after the dialect, with a comma:

| Option | Also accepted | SQL mode | What it changes |
|----|----|----|----|
| `ansi-quotes` | `ansi_quotes` | `ANSI_QUOTES` | `"..."` is a quoted identifier, and a backslash does not escape in it |
| `no-backslash-escapes` | `no_backslash_escapes` | `NO_BACKSLASH_ESCAPES` | A backslash does not escape in `'...'` or in `"..."` |

```xml
<PropertyGroup>
    <SqlSourceDialect>mysql,ansi-quotes</SqlSourceDialect>
</PropertyGroup>
<ItemGroup>
    <AdditionalFiles Update="Legacy/**/*.sql" SqlSourceDialect="mariadb,ansi-quotes,no-backslash-escapes" />
</ItemGroup>
```

```sql
-- SqlSource: dialect=mysql,no-backslash-escapes

-- name: GetPath
SELECT 'C:\temp\' AS path;
```

- Options are not case-sensitive and come in any order.
- Only `mysql` and `mariadb` have options.  An option of another dialect, or one that does not exist, is the same error as a name that is not a dialect.
- A value replaces the one it wins over whole.  A file with `dialect=mysql` in a project that sets `mysql,ansi-quotes` is read as plain `mysql`.
- In the directive, write no space after the comma.
````

- [ ] **Step 3: `README.md`, what a dialect changes**

Replace the table under `### What a dialect changes`, and the list after it that starts `Two details:`, with:

```markdown
| | `ansi` | `mssql` | `postgres` | `cockroachdb` | `mysql` | `mariadb` | `sqlite` | `oracle` |
|----|----|----|----|----|----|----|----|----|
| A backslash escapes in `'...'` and `"..."` | No | No | No | No | Yes | Yes | No | No |
| A backslash escapes in `E'...'` | Yes | No | Yes | Yes | Yes | Yes | No | No |
| `` `...` `` is a quoted identifier | Yes | No | No | No | Yes | Yes | Yes | No |
| `[...]` is a quoted identifier | No | Yes | No | No | No | No | Yes | No |
| `$tag$...$tag$` is a string | Yes | No | Yes | Yes | Yes | No | No | No |
| `q'[...]'` is a string | No | No | No | No | No | No | No | Yes |
| A `/*` inside a block comment needs its own `*/` | Yes | Yes | Yes | Yes | No | No | No | No |
| `--` is a comment with no whitespace after it | Yes | Yes | Yes | Yes | No | No | Yes | Yes |
| `#` starts a comment | No | No | No | No | Yes | Yes | No | No |
| Kept as hints, besides `/*+ ... */` and `/*! ... */` | | | | | | `/*M! ... */` | | `--+ ...` |

Four details:

- In `mssql` a `]]` inside brackets stands for one `]`.  In `sqlite` the first `]` ends the identifier.
- In `mysql` and `mariadb` an option changes the first row (see Options of a dialect, above).
- In `postgres` and `cockroachdb` an `E'...'` string that is continued on the next line is one string, and each later part takes backslash escapes too.  In `postgres`, `--` comments may stand between the parts, as PostgreSQL allows, and they are removed like any other comment.  In `cockroachdb` only whitespace may.
- In `cockroachdb` a bytes literal, `b'...'`, takes backslash escapes as an `E'...'` string does.
```

- [ ] **Step 4: `README.md`, what is still read differently**

In the table under `### What is still read differently`, delete the three rows that start `| SQL written for the SQL modes`, `| A comment between the parts of a continued` and `| A bytes literal with a backslash escape`.  Add this row after the row of SQLite's block comment:

```markdown
| A file whose lines end with a carriage return alone, with no line feed | CockroachDB | A line ends there, as in every dialect.  CockroachDB ends a line only at a line feed. |
```

- [ ] **Step 5: `docs/diagnostics.md`**

In the section `## SQLSRC011`, replace the paragraph that starts `The names are` with:

```markdown
The names are `ansi`, `mssql`, `postgres`, `cockroachdb`, `mysql`, `mariadb`, `sqlite` and `oracle`, in any case.  `sqlserver` and `tsql` also mean `mssql`, `postgresql` also means `postgres`, and `cockroach` also means `cockroachdb`.

After `mysql` or `mariadb` the value may name options, each after a comma, as in `mysql,ansi-quotes`.  The options are `ansi-quotes` and `no-backslash-escapes`, also written `ansi_quotes` and `no_backslash_escapes`.  An option that does not exist, an option after any other dialect, and a comma with nothing after it each make the whole value wrong.
```

In the section `## SQLSRC111`, replace the bullet that starts ``- `dialect` needs the name of a dialect`` with:

```markdown
- `dialect` needs the name of a dialect, as in `dialect=postgres`, with any options after it, as in `dialect=mysql,ansi-quotes`.  The names and the options are those of [SQLSRC011](#sqlsrc011).  The value is one word: a space after a comma ends it.
```

In the section `## SQLSRC112`, replace the bullet that starts ``- Two `dialect` directives`` with:

```markdown
- Two `dialect` directives name different dialects, or one dialect with different options.  A file has one dialect.
```

and in the paragraph after the list, replace the sentence `The same dialect given twice is not a conflict either.` with `The same dialect given twice with the same options is not a conflict either.`

- [ ] **Step 6: `docs/tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md`**

Replace the whole file with:

```markdown
# TD-0004 - Some SQL is misread whatever the dialect

## Problem

A file is read by the rules of its dialect and of the dialect's options: [`SqlDialectRules`](../../src/SqlSource/Parsing/SqlDialectRules.cs) and the readers in [`Parsing/Quoting/`](../../src/SqlSource/Parsing/Quoting).  These constructs are still read differently from the database they are written for:

| Construct | Database | How SqlSource reads it |
|----|----|----|
| A versioned comment whose body holds a string that contains `*/`, such as `/*!50700 SELECT '*/' */` | MySQL, MariaDB | The comment ends at the first `*/`.  The server reads the body as SQL when its version is high enough, and as a comment when it is not, so where it ends depends on the server. |
| A block comment that is still open at the end of the file | SQLite | `UnterminatedBlockComment` |
| A carriage return with no line feed after it | CockroachDB | As the end of a line, as in every dialect.  CockroachDB ends a `--` comment, and counts a line break between the parts of a continued string, only at a line feed. |

The default dialect, `ansi`, is one set of rules for every database.  By design it also misreads what the table in `README.md` says it does not read: backslash escapes in plain strings, `[...]` identifiers, `q'...'` strings, `#` comments, `--` that needs whitespace, and comments that do not nest.  Setting the dialect fixes those, and only those: each construct in the table above is still misread under its own dialect.

A misread has one of two outcomes:

- **An error.**  The quotes or comments no longer balance, and the file gets `UnterminatedQuote` or `UnterminatedBlockComment`.  The file produces nothing until the construct is rewritten.  The `keep-comments` directive does not help, because a lexer error ends the pass.
- **Silent removal.**  The misread quotes happen to balance, and SQL after them is read as a comment and stripped.  The `keep-comments` directive avoids the removal.

In [`SqlLexerTests`](../../tests/SqlSource.Tests/Parsing/SqlLexerTests.cs), the `Lex_KnownLimit_*` tests pin the readings of `ansi`.  The `MySql` row of `Lex_UnterminatedQuoteOfADialect_ReportsTheOpeningDelimiter` pins the versioned comment, `Lex_UnterminatedBlockComment_IsAnErrorInEveryDialect` pins SQLite's comment, and the last row of `Lex_StringInCockroachDb_IsReadByItsRules` pins the carriage return.

## Why it exists

- The end of a versioned comment cannot be known without the version of the server.
- SQLite's reading of an open comment would silently take every later query of the file with it.  An error is the safer reading, and it is deliberate.
- What ends a line is the same for every dialect, in `SqlLexer`, in `SqlMarkerReader` and in `SqlTextBuilder`.  A rule for CockroachDB alone would reach all three, for files with the line endings of classic Mac OS.

## Impact

Low: the constructs are rare.  A user of one gets either an error that names an unterminated quote or comment in valid SQL (`SQLSRC101`, `SQLSRC102`), or a generated constant that is missing part of the query.  The second is silent at build time, though the truncated SQL will almost always fail when it runs.  `README.md` lists the constructs.

## Proposed fix

1. For the versioned comment, an option of `mysql` and `mariadb` that gives the version of the server.  The lexer then knows whether the body is SQL, and reads it as SQL or as a comment.  `SqlDialectName` already reads options after a dialect's name.
2. Leave SQLite's open comment as it is.
3. For the carriage return, a value in `SqlDialectRules` for what ends a line, read by the three places that look for one.

## Trigger

A user reports one of these constructs.
```

In `docs/tech-debt/README.md`, replace the description of the `TD-0004` row, its last cell, with:

```markdown
A few constructs are misread whatever the dialect: a MySQL versioned comment that holds `*/` in a string, a block comment of SQLite left open at the end of a file, a carriage return alone as a line end in CockroachDB
```

- [ ] **Step 7: `docs/deferred/D-0001-dialects-not-delivered.md`**

Make these four changes:

- Replace the title with `# D-0001 - Dialects beyond the first eight`.
- Replace the paragraph under `## Delivered instead` with:

  ```markdown
  Eight dialects: `ansi`, `mssql`, `postgres`, `cockroachdb`, `mysql`, `mariadb`, `sqlite` and `oracle`, in [`SqlDialectRules`](../../src/SqlSource/Parsing/SqlDialectRules.cs).  `README.md` says which of them to use for DuckDB, Firebird and Db2.
  ```

- Replace the paragraph under `## Why deferred` with:

  ```markdown
  Seven of the eight cover every engine whose ADO.NET providers have more than 100 million NuGet downloads, and `cockroachdb` was added after them because it needed only a prefix for a reader that exists.  The next engine has under 40 million.  BigQuery needs lexer machinery that nothing else does, and several rules of the rest could not be verified from a primary source.
  ```

- Delete the `CockroachDB` row of the table under `## Remaining work`.

In `docs/deferred/README.md`, replace the description of the `D-0001` row, its last cell, with:

```markdown
Dialects for Redshift, Snowflake, BigQuery, ClickHouse, Spark and Trino, and names of their own for DuckDB, Firebird and Db2
```

- [ ] **Step 8: `src/SqlSource/AGENTS.md`**

In the section ``## `Parsing/` ``, replace the four bullets that start `**Dialect-sensitive choices live in`, `**A reader holds no state and allocates nothing.**`, `**A quoted region must never be able to hold a marker.**` and `**A dialect's names are in` with:

```markdown
- **Dialect-sensitive choices live in `SqlDialectRules` and the readers of `Parsing/Quoting/`.**  `SqlLexer` reads by the rules it is given and names no dialect and no quoting form.  A value in `SqlDialectRules` is a rule that dialects differ on by a setting; a `QuoteReader` is one way a quoted region ends.  The static properties of `SqlDialectRules`, and the rules it builds for `mysql` and `mariadb` under each set of options, are the whole table, and `Lex_Construct_IsReadByTheRulesOfTheDialect` in `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs` pins every cell.  Changing one changes the SQL users get.  `docs/tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md` lists what is still misread.
- **A reader holds no state and allocates nothing.**  One instance serves every file of every compilation, on any thread.  It may look at the characters before its opening character for a prefix, which stays in the text lexeme before the region.  It reads one part of a string: whether a string goes on after a gap is decided by the lexer, from `SqlDialectRules.StringContinuation` and from the reader that `QuoteReader.FindContinuation` names.
- **A quoted region must never be able to hold a marker.**  It is copied as written and is not searched for `-- name:`.  That is why each part of a continued string is a quoted region of its own, and the gap between two parts is ordinary text and comments.  The pending continuation is the only thing `SqlLexer` carries from one lexeme to the next.
- **A dialect's names, and the names of its options, are in `SqlDialectName` only.**  The directive, the MSBuild property and the metadata all read through it, and what they get is a `SqlDialectChoice`: a `SqlDialect` and its `SqlDialectOptions`.  The text of `SQLSRC011` and of `docs/diagnostics.md` repeats the list of names; `SqlDialectName.Accepted` and a test keep the message in step.
```

- [ ] **Step 9: Verify**

Run: `grep -rn "TD-0004" . --include='*.md' --include='*.cs' --exclude-dir=obj --exclude-dir=bin --exclude-dir=superpowers`
Expected: the item file, its row in `docs/tech-debt/README.md`, `src/SqlSource/AGENTS.md`, and two comments in `SqlLexerTests.cs`.  Read each, and confirm that it is true of the rewritten item.

Run: `grep -rin "cockroach" docs/deferred`
Expected: two lines of `D-0001`, the list of eight dialects and the reason `cockroachdb` was added.  No row of a table.

Run: `grep -n '](docs/\|](\.\./\|](#' README.md`
Expected: no output.  Every link in `README.md` is an absolute URL.

Run: `grep -rn "continues: \|ContinuedEscapeString\|For(SqlDialect dialect)" src tests --include='*.cs' --exclude-dir=obj --exclude-dir=bin`
Expected: no output.

Run: `./pre-commit-validation.sh`
Expected: every check passes.

- [ ] **Step 10: Commit**

In a command of its own:

```bash
git add README.md docs/diagnostics.md docs/tech-debt docs/deferred src/SqlSource/AGENTS.md
git commit -m "Document the dialect options, the continued string and cockroachdb" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

Then stop.  Do not push and do not open a pull request.  Tell the user the work is ready, and that before a pull request `git fetch --tags origin` must be run and `VersionPrefix` in `Directory.Build.props` compared with the highest `v*` tag.
