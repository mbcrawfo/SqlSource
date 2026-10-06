# SQL Dialects Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A project says which database its SQL is written for, by the `SqlSourceDialect` MSBuild property, by metadata of the same name on a file's `AdditionalFiles` item, or by a `dialect=` directive at the top of a file, and the file's comments, strings and quoted identifiers are found by that database's rules.

**Architecture:** A `SqlDialect` enum names seven dialects.  `SqlDialectRules` holds, for each, the comment rules as values and a `QuoteReader` strategy for each character that opens a quoted region; six stateless readers in `Parsing/Quoting/` are the ways a region ends.  `SqlLexer` becomes an object that reads by the rules it holds, names no dialect, and can be run in two stages, so that `SqlPreambleDialect` can find a file's `dialect=` directive among its leading comments and switch the rules before the rest is lexed.  In the generator, a file's dialect is resolved from the item metadata and the property in a pipeline step of its own, before the parse, so that a change to either parses only the files it affects.

**Tech Stack:** .NET SDK 10.0.401, C# (`LangVersion` preview) on `netstandard2.0`, Roslyn 4.8.0 (`Microsoft.CodeAnalysis.CSharp`) incremental generator API, xunit v3 on Microsoft.Testing.Platform, Shouldly, CSharpier, MSBuild.

**Spec:** `docs/superpowers/specs/2026-10-06-sql-dialects-design.md`.  It resolves the open part of `docs/tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md`.

## Global Constraints

- Work on the existing branch `claude/sql-dialect-support-d2a7dc`, which already holds the spec and this plan.  Commit after each task.  Do not push, do not open a pull request, and never merge `main` into the branch; ask the user first.
- `src/SqlSource` targets `netstandard2.0` only and may use no API newer than Roslyn 4.8.0.  Add no package reference to it.
- No value that flows between pipeline steps holds an `ISymbol`, a `SyntaxNode`, a `Compilation`, a `Location` or a `Diagnostic`.  Models are records and hold collections in `EquatableArray<T>`.
- No collection expression may target `ImmutableArray<T>` in `src/SqlSource` (CS9210).
- The files of `tests/SqlSource.Tests/Generator/` are also compiled into `tests/SqlSource.Tests.RoslynFloor` and run on Roslyn 4.8.0.  They use only Roslyn API that 4.8.0 has, and the C# they hand to the compiler is C# 12 at most.
- An internal type cannot be the parameter of a public test method.  A theory row names a dialect as a string, `nameof(SqlDialect.MySql)`, and the test parses it.
- The dialect names, copied from the spec: `ansi` (the default), `mssql`, `postgres`, `mysql`, `mariadb`, `sqlite`, `oracle`.  Aliases: `sqlserver` and `tsql` for `mssql`, `postgresql` for `postgres`.  Compared ignoring case, surrounding whitespace ignored.
- `ansi` reads exactly as the lexer reads today.  No test of today's behaviour may change its expectation.
- Precedence: the file's `dialect=` directive, then the `SqlSourceDialect` metadata of the file's `AdditionalFiles` item, then the `SqlSourceDialect` property, then `ansi`.  An empty value of the metadata or the property counts as not set.
- The generator reads the property as `build_property.SqlSourceDialect` and the metadata as `build_metadata.AdditionalFiles.SqlSourceDialect`.
- A `dialect=` directive is accepted only before the file's first `-- name:` marker and before its first SQL.  It takes effect from the line after it.
- Every MSBuild property of the package starts with `SqlSource` (root `AGENTS.md`, rule 5).
- The parse allocation budget in `tests/SqlSource.Tests/Parsing/SqlFileParserAllocationTests.cs`, 12 bytes for each character, is not raised.
- Diagnostics, each an error with category `SqlSource`, tagged `NotConfigurable`.  Do not reword them:

  | Id | Title | Message |
  |----|----|----|
  | `SQLSRC011` | `SqlSourceDialect is not valid` | `'{0}' is not a SQL dialect.  SqlSourceDialect accepts ansi, mssql, postgres, mysql, mariadb, sqlite and oracle.` |
  | `SQLSRC111` | `Directive value is not valid` | `The directive '{0}' lacks a value it needs, has one it does not take, or has one that is not valid` |
  | `SQLSRC112` | `Directives conflict` | `'{0}' conflicts with another directive in the same scope` |
  | `SQLSRC115` | `Dialect directive is misplaced` | `The 'dialect' directive must come before the file's first query and before any SQL` |

- Warnings are errors, and `AnalysisLevel` is `latest-all` with the Roslynator and Sonar analyzers.  Fix a finding in the code.  A rule that cannot be satisfied is turned off in `.editorconfig` with a comment, never with `NoWarn` or `#pragma`.
- Every text file obeys `.editorconfig`: 120 columns (comments and string literals included), 4-space indent, LF line endings, final newline.  Markdown is exempt from the column limit.  CSharpier owns C# and project-file layout: run `./format.sh` after writing them.
- Every commit must pass `./pre-commit-validation.sh`.  Run it as its own command before the commit command; a hook re-runs it and blocks the commit when it fails.  Docker must be running.  Never chain a formatter and a commit in one command.
- Every commit message ends with the line `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Markdown in this repository puts two spaces after a sentence-ending period.  `README.md` links are absolute URLs.
- Out of scope: dialects beyond the seven (`docs/deferred/D-0001` records them), options that change one rule of a dialect such as `ANSI_QUOTES` (`TD-0004` proposes them), a dedicated `SqlSourceDialect` item, a per-query override, and client commands that are not SQL.

All file contents and patches below were built, formatted and run in a throwaway copy of the repository before this plan was written, and the plan was then replayed from its own text on a fresh copy.  The whole change passes `./pre-commit-validation.sh` there, with 1,286 tests: 1,112 in `SqlSource.Tests` and 174 in `SqlSource.Tests.RoslynFloor`.  The branch starts at 816 (671 and 145).  The totals stated for each task were observed while the tasks were replayed in order.  The compiler error ids quoted for the "verify it fails" steps are what the missing members produce; the exact count of errors may differ.

### Where this plan differs from the spec

The spec is the intent; these were settled while the code was built.

- **Skipping plain text.**  `SqlDialectRules.FindStarter` finds the next character that can start a comment or a quoted region, and `SqlLexer.ReadToEnd` jumps to it.  The spec does not have it.  Without it the opener table made a parse about 9% slower than today; with it a parse is slightly faster than today.
- **Who reads the directive.**  `SqlPreambleDialect` asks `SqlDirectiveScope.TryFindDialect` for the dialect of a marker, so that the syntax of a `-- SqlSource:` line stays in one class.  The spec has it depend on `SqlDialectName` directly.
- **The message of `SQLSRC011`** is two sentences, in the form of `SQLSRC010`.  The spec joined them with a semicolon.
- **The spec's two open points are settled.**  Item metadata written over several lines does reach the compiler empty, and one item update in `SqlSource.targets` trims it reliably, at evaluation time, so in a design-time build too.  The SDK does write a section for every `.sql` file into the compiler's configuration file; `TD-0013`, added in Task 6, records what that costs.
- **`TD-0004` keeps its file name**, because closed specs link to it.  Its title and text change.

## Review Focus

Inputs the spec implies but does not spell out, most likely first.  Each is pinned by a test in the task that owns the code.

1. **A value that is nearly right.**  MSBuild hands over whatever the project file holds: `MSSQL`, ` postgres ` with the whitespace of a multi-line element, an empty element, `pgsql`, `sql server`.  The first three must work and the rest must be `SQLSRC011`, never a silent fall back to another dialect.  The same holds for metadata, where a multi-line element reaches the compiler empty unless the package trims it.  Pinned by `SqlDialectNameTests`, `DialectSettingTests` (Tasks 1 and 5), `Run_File_IsReadByItsDirectiveThenItsMetadataThenTheProperty`, `Run_PropertyThatIsNotADialect_IsAnErrorWithoutAPositionAndTheFilesAreReadAsAnsi` (Task 5), and by the test project itself, which writes both of its dialects over several lines (Task 6).
2. **A directive that is almost in the right place.**  Below a licence header in a block comment; followed at once by a comment form that only the new dialect has; after the file's first SQL; inside the first named query; given twice.  The first two must work, and the others must be an error that leaves the file read as it was, never a file read half one way and half the other.  Pinned by `SqlPreambleDialectTests`, `Parse_DialectDirectiveInsideAQueryOrAfterSql_IsMisplaced`, `Parse_MisplacedDialectDirective_IsNotApplied` and `Parse_TwoDialectsInOneHeader_IsAnErrorAtTheSecondAndTheSameDialectTwiceIsNot` (Task 4).
3. **A character that means something else in another database.**  `#` is a temporary table in SQL Server and part of a name in Oracle; `[` is array syntax in PostgreSQL; `q'a'` is a column with an alias in SQL Server and MySQL; a backtick is an operator character in PostgreSQL; `$$` is a name in MariaDB and Db2.  Each must be plain text in the dialects that do not give it meaning.  Pinned by the rows of `Lex_Construct_IsReadByTheRulesOfTheDialect`, each of which lists every dialect, and by `ReaderFor_Character_HasAReaderOnlyWhereTheDialectOpensAQuotedRegion` (Task 3).
4. **A marker where a quoted region could swallow it.**  A quoted region is copied as written and is not searched for `-- name:`.  A bracketed identifier or a `q'[` string that is never closed must be an error, not a region that runs to the end of the file, and a continued PostgreSQL string must not be able to hold a comment line.  Pinned by `Lex_UnterminatedQuoteOfADialect_ReportsTheOpeningDelimiter` (Task 3) and the `-- name: B` row of `FindEnd_EscapeStringNotFollowedByAContinuation_EndsAtItsOwnQuote` (Task 2).
5. **An unrelated settings change in the IDE.**  The compiler hands the generator new options whenever any MSBuild property or `.editorconfig` changes.  That must parse nothing again, and a change to the dialect must parse only the files it reaches.  Pinned by `Run_OptionsReplacedWithoutChangingTheProperty_EmitsNothingAgain`, `Run_DialectPropertyChanged_ParsesOnlyTheFilesThatFallBackToIt` and `Run_DialectMetadataOfOneFileChanged_ParsesOnlyThatFile` (Task 5).

---

## File Structure

| File | Change | Responsibility | Task |
|----|----|----|----|
| `src/SqlSource/Parsing/SqlDialect.cs` | New | The enum of seven dialects | 1 |
| `src/SqlSource/Parsing/SqlDialectName.cs` | New | A name or an alias to a `SqlDialect`; the only place the names are known | 1 |
| `src/SqlSource/Parsing/Quoting/QuoteReader.cs` | New | The strategy, and the two scans that readers share | 2 |
| `src/SqlSource/Parsing/Quoting/DoubledQuoteReader.cs`, `BackslashQuoteReader.cs`, `EscapeStringReader.cs`, `QuoteOperatorReader.cs`, `BracketReader.cs`, `DollarQuoteReader.cs` | New | One way a quoted region ends, each | 2 |
| `src/SqlSource/Parsing/SqlDialectRules.cs` | New | The table: comment rules as values, a reader for each opener, one instance for each dialect | 3 |
| `src/SqlSource/Parsing/SqlLexer.cs` | Rewritten | Reads by the rules it holds; can read leading comments first | 3 |
| `src/SqlSource/Parsing/SqlLexemeKind.cs` | Modify | Documentation of `#` comments and the new hints | 3 |
| `src/SqlSource/Parsing/SqlMarkerReader.cs` | Modify | A marker starts with two dashes | 3 |
| `src/SqlSource/Parsing/SqlPreambleDialect.cs` | New | Finds the file's `dialect=` directive and switches the lexer | 4 |
| `src/SqlSource/Parsing/SqlDirectiveScope.cs` | Modify | Knows `dialect=`; reports one that is wrong, repeated differently, or misplaced | 4 |
| `src/SqlSource/Parsing/SqlParseErrorKind.cs` | Modify | `MisplacedDialect` | 4 |
| `src/SqlSource/Parsing/SqlFileParser.cs` | Modify | `Parse(text, fileName, dialect)` | 3, 4 |
| `src/SqlSource/Diagnostics/SqlDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/diagnostics.md` | Modify | `SQLSRC115`, the widened `SQLSRC111` and `SQLSRC112`, then `SQLSRC011` | 4, 5 |
| `src/SqlSource/Generation/DialectSetting.cs` | New | Reads the property and the metadata | 5 |
| `src/SqlSource/Generation/FileDialect.cs` | New | A file with the dialect MSBuild gives it | 5 |
| `src/SqlSource/Generation/ParsedSqlFile.cs`, `SqlFileReader.cs`, `TrackingNames.cs`, `src/SqlSource/SqlSourceGenerator.cs` | Modify | The pipeline: resolve, parse with the dialect, report an invalid value once | 5 |
| `src/SqlSource/build/SqlSource.props`, `SqlSource.targets` | Modify | Show the property and the metadata to the compiler; trim both | 6 |
| `tests/SqlSource.Tests/Parsing/SqlDialectNameTests.cs` | New | Names, aliases, case, whitespace | 1 |
| `tests/SqlSource.Tests/Parsing/Quoting/*.cs` | New | A test class for each reader, and the `Region` helper | 2 |
| `tests/SqlSource.Tests/Parsing/SqlDialectRulesTests.cs` | New | The table itself | 3 |
| `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs` | Rewritten | Every construct under every dialect; the two-stage lexer | 3 |
| `tests/SqlSource.Tests/Parsing/SqlMarkerReaderTests.cs`, `SqlTextBuilderTests.cs` | Modify | The new comment and hint forms | 3 |
| `tests/SqlSource.Tests/Parsing/SqlPreambleDialectTests.cs` | New | Where the directive is found and where the header ends | 4 |
| `tests/SqlSource.Tests/Parsing/SqlDirectiveScopeTests.cs`, `SqlFileParserTests.cs`, `SqlFileParserAllocationTests.cs` | Modify | The directive, the parser's dialect, the budget under each | 3, 4 |
| `tests/SqlSource.Tests/Generator/FileDiagnosticsTests.cs` | Modify | The messages of `SQLSRC111`, `112` and `115` at their place in a file | 4 |
| `tests/SqlSource.Tests/Generation/DialectSettingTests.cs` | New | The setting and its resolution | 5 |
| `tests/SqlSource.Tests/Generator/DialectTests.cs` | New | Precedence and `SQLSRC011`, on both Roslyn versions | 5 |
| `tests/SqlSource.Tests/Generator/TestOptionsProvider.cs`, `GeneratorHarness.cs`, `CachingTests.cs` | Modify | Options for the project and for one file; what a change makes the generator redo | 5 |
| `tests/SqlSource.Tests/Generation/SqlFileReaderTests.cs`, `tests/SqlSource.Tests/Diagnostics/SqlDiagnosticsTests.cs` | Modify | The reader's dialect; the message lists the names | 5 |
| `tests/SqlSource.Tests/SqlSource.Tests.csproj`, `EndToEnd/DialectQueries.cs`, `EndToEnd/Dialects/*.sql`, `EndToEnd/EndToEndTests.cs`, `Package/BuildFileTests.cs` | New and modify | The real MSBuild files carrying all three settings | 6 |
| `docs/tech-debt/TD-0013-every-sql-file-gets-a-section-in-the-compiler-configuration.md`, `docs/tech-debt/README.md` | New and modify | What the metadata declaration costs | 6 |
| `README.md`, `src/SqlSource/AGENTS.md`, `docs/tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md`, `docs/tech-debt/README.md`, `docs/deferred/D-0001-dialects-not-delivered.md`, `docs/deferred/README.md` | New and modify | Documentation for users and contributors | 7 |

## Commands used throughout

| Purpose | Command |
|----|----|
| Build | `dotnet build SqlSource.slnx` |
| All tests | `dotnet test --solution SqlSource.slnx` |
| One test class | `dotnet test --project tests/SqlSource.Tests --filter-class '*SqlLexerTests'` |
| One namespace | `dotnet test --project tests/SqlSource.Tests --filter-namespace 'SqlSource.Tests.Parsing.Quoting'` |
| Format C# and project files | `./format.sh` |
| Every check | `./pre-commit-validation.sh` |

`dotnet test` builds first.  A test run ends with a summary that states `total`, `failed` and `succeeded`; the totals below are for the whole solution unless a step says otherwise.

A build error in a test file that names a member from a later step is the expected "red" of that task.

### Taking content from this plan

Every file and patch in this plan sits in a fenced block of four backticks, under a marker line such as `<!-- file:src/SqlSource/Parsing/SqlDialect.cs -->` or `<!-- patch:task3-marker-reader -->`.  Copying by hand is error-prone here: the tests hold many backslashes and quotes.  This script prints the block under a marker exactly as it is written.  Create it once; it is not part of the repository.

```bash
cat > "${TMPDIR:-/tmp}/plan-block" <<'EOF'
#!/usr/bin/env bash
# Prints the fenced block that follows the marker line "<!-- $1 -->" in the plan.
set -euo pipefail
awk -v marker="<!-- $1 -->" '
    $0 == marker { found = 1; next }
    found && /^````/ { if (printing) { done = 1; exit } printing = 1; next }
    printing { print }
    END { if (!done) { print "plan-block: no block for " marker > "/dev/stderr"; exit 1 } }
' docs/superpowers/plans/2026-10-06-sql-dialects.md
EOF
chmod +x "${TMPDIR:-/tmp}/plan-block"
```

Run it from the root of the repository:

- A file: `"${TMPDIR:-/tmp}/plan-block" 'file:src/SqlSource/Parsing/SqlDialect.cs' > src/SqlSource/Parsing/SqlDialect.cs`.  Create the folder first where a step says the folder is new.
- A patch: `"${TMPDIR:-/tmp}/plan-block" 'patch:task3-marker-reader' | git apply`.  A patch that does not apply means the file is not as this plan expects; stop and read both.

Writing a file with an editor from the block gives the same result, if every character is kept.

---

### Task 1: The names of the dialects

**Files:**
- Create: `src/SqlSource/Parsing/SqlDialect.cs`
- Create: `src/SqlSource/Parsing/SqlDialectName.cs`
- Test: `tests/SqlSource.Tests/Parsing/SqlDialectNameTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces, in namespace `SqlSource.Parsing`, both `internal`:
  - `enum SqlDialect { Ansi, SqlServer, PostgreSql, MySql, MariaDb, Sqlite, Oracle }`.  `Ansi` is the default value.
  - `static class SqlDialectName` with `const string Accepted`, `static bool TryParse(string? value, out SqlDialect dialect)` and `static bool TryParse(ReadOnlySpan<char> value, out SqlDialect dialect)`.  Both trim, compare ordinally ignoring case, and give `SqlDialect.Ansi` with `false` for anything that is not a name.

- [ ] **Step 1: Write the failing test**

Create `tests/SqlSource.Tests/Parsing/SqlDialectNameTests.cs`:

<!-- file:tests/SqlSource.Tests/Parsing/SqlDialectNameTests.cs -->
````csharp
using System;
using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

// An internal enum cannot be the parameter of a public test method, so a row gives the dialect's name.
public class SqlDialectNameTests
{
    [Theory]
    [InlineData("ansi", nameof(SqlDialect.Ansi))]
    [InlineData("mssql", nameof(SqlDialect.SqlServer))]
    [InlineData("sqlserver", nameof(SqlDialect.SqlServer))]
    [InlineData("tsql", nameof(SqlDialect.SqlServer))]
    [InlineData("postgres", nameof(SqlDialect.PostgreSql))]
    [InlineData("postgresql", nameof(SqlDialect.PostgreSql))]
    [InlineData("mysql", nameof(SqlDialect.MySql))]
    [InlineData("mariadb", nameof(SqlDialect.MariaDb))]
    [InlineData("sqlite", nameof(SqlDialect.Sqlite))]
    [InlineData("oracle", nameof(SqlDialect.Oracle))]
    // Any case, and surrounding whitespace as a value written on a line of its own has it.
    [InlineData("MSSQL", nameof(SqlDialect.SqlServer))]
    [InlineData("Postgres", nameof(SqlDialect.PostgreSql))]
    [InlineData("MySql", nameof(SqlDialect.MySql))]
    [InlineData("  oracle\t", nameof(SqlDialect.Oracle))]
    [InlineData("\n    sqlite\n", nameof(SqlDialect.Sqlite))]
    public void TryParse_NameOrAlias_GivesItsDialect(string name, string expected)
    {
        SqlDialectName.TryParse(name, out var dialect).ShouldBeTrue();

        dialect.ToString().ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("pgsql")]
    [InlineData("sql server")]
    [InlineData("my sql")]
    [InlineData("postgres;mysql")]
    [InlineData("oracle,")]
    [InlineData("SqlDialect.Oracle")]
    [InlineData("3")]
    // Turkish dotless i: the comparison is ordinal, so no culture turns this into "sqlite".
    [InlineData("sqlıte")]
    public void TryParse_AnythingElse_IsRejected(string? name)
    {
        SqlDialectName.TryParse(name, out var dialect).ShouldBeFalse();

        dialect.ShouldBe(SqlDialect.Ansi);
    }

    [Fact]
    public void TryParse_Span_ReadsTheSameNames()
    {
        SqlDialectName.TryParse("dialect=MariaDB".AsSpan(8), out var dialect).ShouldBeTrue();

        dialect.ShouldBe(SqlDialect.MariaDb);
    }

    [Fact]
    public void TryParse_EveryDialect_HasAName()
    {
        string[] names = ["ansi", "mssql", "postgres", "mysql", "mariadb", "sqlite", "oracle"];

        var parsed = names.Select(name =>
        {
            SqlDialectName.TryParse(name, out var dialect).ShouldBeTrue();
            return dialect;
        });

        parsed.ShouldBe(Enum.GetValues<SqlDialect>());
    }

    [Fact]
    public void Accepted_ListsTheNameOfEveryDialect() =>
        SqlDialectName.Accepted.ShouldBe("ansi, mssql, postgres, mysql, mariadb, sqlite and oracle");

    [Fact]
    public void Ansi_IsTheDefaultValue() => default(SqlDialect).ShouldBe(SqlDialect.Ansi);
}
````

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class '*SqlDialectNameTests'`
Expected: the build fails.  CS0103 and CS0246 name `SqlDialectName` and `SqlDialect`, which do not exist.

- [ ] **Step 3: Write the enum and the names**

Create `src/SqlSource/Parsing/SqlDialect.cs`:

<!-- file:src/SqlSource/Parsing/SqlDialect.cs -->
````csharp
namespace SqlSource.Parsing;

/// <summary>
/// The database whose rules decide where comments, strings and quoted identifiers start and end.
/// </summary>
internal enum SqlDialect
{
    /// <summary>
    /// ANSI SQL, plus the quoting forms of PostgreSQL and MySQL that cannot be mistaken for anything else.  The
    /// default.
    /// </summary>
    Ansi,

    /// <summary>SQL Server and Azure SQL.</summary>
    SqlServer,

    /// <summary>PostgreSQL.</summary>
    PostgreSql,

    /// <summary>MySQL.</summary>
    MySql,

    /// <summary>MariaDB.</summary>
    MariaDb,

    /// <summary>SQLite.</summary>
    Sqlite,

    /// <summary>Oracle.</summary>
    Oracle,
}
````

Create `src/SqlSource/Parsing/SqlDialectName.cs`:

<!-- file:src/SqlSource/Parsing/SqlDialectName.cs -->
````csharp
using System;

namespace SqlSource.Parsing;

/// <summary>
/// The names a dialect is set by: in the <c>dialect=</c> directive, in the <c>SqlSourceDialect</c> MSBuild property
/// and in the metadata of the same name.  This is the only place the names are known.
/// </summary>
internal static class SqlDialectName
{
    /// <summary>
    /// The names as a message lists them.  The text of <c>SQLSRC011</c> repeats this list, and a test compares the two.
    /// </summary>
    public const string Accepted = "ansi, mssql, postgres, mysql, mariadb, sqlite and oracle";

    private static readonly (string Name, SqlDialect Dialect)[] Names =
    [
        ("ansi", SqlDialect.Ansi),
        ("mssql", SqlDialect.SqlServer),
        ("sqlserver", SqlDialect.SqlServer),
        ("tsql", SqlDialect.SqlServer),
        ("postgres", SqlDialect.PostgreSql),
        ("postgresql", SqlDialect.PostgreSql),
        ("mysql", SqlDialect.MySql),
        ("mariadb", SqlDialect.MariaDb),
        ("sqlite", SqlDialect.Sqlite),
        ("oracle", SqlDialect.Oracle),
    ];

    /// <summary>
    /// Reads a name or an alias, ignoring case and surrounding whitespace.  False for anything else, and for null.
    /// </summary>
    public static bool TryParse(string? value, out SqlDialect dialect) => TryParse(value.AsSpan(), out dialect);

    /// <inheritdoc cref="TryParse(string?, out SqlDialect)" />
    public static bool TryParse(ReadOnlySpan<char> value, out SqlDialect dialect)
    {
        var name = value.Trim();
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
}
````

The span overload is what the directive uses, so that finding a file's dialect allocates nothing.  `string.AsSpan` on a null string gives an empty span, so the string overload needs no null check.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class '*SqlDialectNameTests'`
Expected: total 30, failed 0.

Run: `dotnet test --solution SqlSource.slnx`
Expected: total 846, failed 0.

- [ ] **Step 5: Format and validate**

Run: `./format.sh`

Run: `./pre-commit-validation.sh`
Expected: every step `passed`.

- [ ] **Step 6: Commit**

```bash
git add src/SqlSource/Parsing/SqlDialect.cs src/SqlSource/Parsing/SqlDialectName.cs tests/SqlSource.Tests/Parsing/SqlDialectNameTests.cs
```

```bash
git commit -m "Name the SQL dialects

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: The quote readers

A quoted region is a string or a quoted identifier.  The lexer copies it as written and looks for no comment inside it.  How a region ends is one of six algorithms; each is a small class with no state.

**Files:**
- Create: `src/SqlSource/Parsing/Quoting/QuoteReader.cs`, `DoubledQuoteReader.cs`, `BackslashQuoteReader.cs`, `EscapeStringReader.cs`, `QuoteOperatorReader.cs`, `BracketReader.cs`, `DollarQuoteReader.cs`
- Test: `tests/SqlSource.Tests/Parsing/Quoting/Region.cs` (a helper), `DoubledQuoteReaderTests.cs`, `BackslashQuoteReaderTests.cs`, `EscapeStringReaderTests.cs`, `QuoteOperatorReaderTests.cs`, `BracketReaderTests.cs`, `DollarQuoteReaderTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces, in namespace `SqlSource.Parsing.Quoting`, all `internal`:
  - `abstract class QuoteReader` with `const int Unterminated = -1`, `const int NotAQuote = -2` and `abstract int FindEnd(string text, int start)`.  `start` is the offset of the opening character.  The result is the offset after the closing delimiter, or one of the two constants.
  - `sealed class DoubledQuoteReader()`, `BackslashQuoteReader()`, `EscapeStringReader(bool continues)`, `QuoteOperatorReader()`, `BracketReader(bool doubledCloserEscapes)`, `DollarQuoteReader()`, each a `QuoteReader`.
  - In the tests: `Region.Read(QuoteReader reader, string text, char opener)` returns the region at the first `opener` in `text` as written, or `Region.Unterminated`, or `Region.NotAQuote`.

What each reader does, from the spec:

| Reader | Ends at |
|----|----|
| Doubled | The closing quote that is not doubled: `''`, `""`, ` `` ` |
| Backslash | As Doubled, and a backslash takes the next character with it |
| Escape-string | As Doubled.  As Backslash when the quote directly follows an `E` or `e` that does not end an identifier.  With `continues`, an `E` string goes on after whitespace that holds a line break and is followed by `'`; only whitespace may be in the gap. |
| Quote-operator | As Doubled.  When the quote directly follows `q` or `Q`, or one of those after `n` or `N`, and that prefix does not end an identifier: the character after the quote is the opening delimiter, and the region ends at the first closing delimiter directly followed by `'`.  `[ { < (` close with `] } > )`; any other character closes with itself.  A space, tab or line break after the quote is not a delimiter. |
| Bracket | The first `]`.  With `doubledCloserEscapes`, the first `]` not followed by `]`. |
| Dollar | The same `$tag$` again.  A `$` after an identifier character, or an opener with no closer, is `NotAQuote`. |

- [ ] **Step 1: Write the failing tests**

Create the folder `tests/SqlSource.Tests/Parsing/Quoting/`, and in it:

`Region.cs`:

<!-- file:tests/SqlSource.Tests/Parsing/Quoting/Region.cs -->
````csharp
using System;
using SqlSource.Parsing.Quoting;

namespace SqlSource.Tests.Parsing.Quoting;

// What a reader makes of the first place in a text where its opening character stands: the region as written, or
// the name of the answer that is not a region.
internal static class Region
{
    public const string Unterminated = "<unterminated>";

    public const string NotAQuote = "<not a quote>";

    public static string Read(QuoteReader reader, string text, char opener)
    {
        var start = text.IndexOf(opener, StringComparison.Ordinal);
        var end = reader.FindEnd(text, start);
        return end switch
        {
            QuoteReader.Unterminated => Unterminated,
            QuoteReader.NotAQuote => NotAQuote,
            _ => text[start..end],
        };
    }
}
````

`DoubledQuoteReaderTests.cs`:

<!-- file:tests/SqlSource.Tests/Parsing/Quoting/DoubledQuoteReaderTests.cs -->
````csharp
using Shouldly;
using SqlSource.Parsing.Quoting;
using Xunit;

namespace SqlSource.Tests.Parsing.Quoting;

public class DoubledQuoteReaderTests
{
    private static readonly DoubledQuoteReader Reader = new();

    [Theory]
    [InlineData("'abc' x", '\'', "'abc'")]
    [InlineData("x 'abc'", '\'', "'abc'")]
    [InlineData("''", '\'', "''")]
    [InlineData("'it''s' x", '\'', "'it''s'")]
    [InlineData("'''' x", '\'', "''''")]
    [InlineData("'a''' x", '\'', "'a'''")]
    [InlineData("\"a\"\"b\" x", '"', "\"a\"\"b\"")]
    [InlineData("`a``b` x", '`', "`a``b`")]
    [InlineData("'a\nb' x", '\'', "'a\nb'")]
    [InlineData("'a -- b /* c' x", '\'', "'a -- b /* c'")]
    [InlineData("'a\"b`c' x", '\'', "'a\"b`c'")]
    public void FindEnd_ClosedRegion_EndsAfterTheQuoteThatIsNotDoubled(string text, char opener, string expected) =>
        Region.Read(Reader, text, opener).ShouldBe(expected);

    // A backslash is an ordinary character here: the quote after it closes the region.
    [Theory]
    [InlineData("'a\\' x", "'a\\'")]
    [InlineData("'a\\'b' x", "'a\\'")]
    [InlineData("'\\\\' x", "'\\\\'")]
    public void FindEnd_Backslash_IsNotAnEscape(string text, string expected) =>
        Region.Read(Reader, text, '\'').ShouldBe(expected);

    [Theory]
    [InlineData("'abc")]
    [InlineData("'")]
    [InlineData("'abc''")]
    [InlineData("'abc''def")]
    public void FindEnd_RegionWithoutAClosingQuote_IsUnterminated(string text) =>
        Region.Read(Reader, text, '\'').ShouldBe(Region.Unterminated);
}
````

`BackslashQuoteReaderTests.cs`:

<!-- file:tests/SqlSource.Tests/Parsing/Quoting/BackslashQuoteReaderTests.cs -->
````csharp
using Shouldly;
using SqlSource.Parsing.Quoting;
using Xunit;

namespace SqlSource.Tests.Parsing.Quoting;

public class BackslashQuoteReaderTests
{
    private static readonly BackslashQuoteReader Reader = new();

    [Theory]
    [InlineData("'abc' x", '\'', "'abc'")]
    [InlineData("'a\\'b' x", '\'', "'a\\'b'")]
    [InlineData("'a\\\\' x", '\'', "'a\\\\'")]
    [InlineData("'a\\\\\\'b' x", '\'', "'a\\\\\\'b'")]
    [InlineData("'it''s' x", '\'', "'it''s'")]
    [InlineData("'a''\\' b' x", '\'', "'a''\\' b'")]
    [InlineData("\"a\\\"b\" x", '"', "\"a\\\"b\"")]
    [InlineData("\"a\"\"b\" x", '"', "\"a\"\"b\"")]
    [InlineData("'a\\\nb' x", '\'', "'a\\\nb'")]
    [InlineData("'a\\nb -- c' x", '\'', "'a\\nb -- c'")]
    public void FindEnd_ClosedRegion_EndsAfterTheQuoteThatIsNeitherEscapedNorDoubled(
        string text,
        char opener,
        string expected
    ) => Region.Read(Reader, text, opener).ShouldBe(expected);

    [Theory]
    [InlineData("'abc")]
    [InlineData("'abc\\'")]
    [InlineData("'abc\\")]
    [InlineData("'abc''")]
    public void FindEnd_RegionWithoutAClosingQuote_IsUnterminated(string text) =>
        Region.Read(Reader, text, '\'').ShouldBe(Region.Unterminated);
}
````

`EscapeStringReaderTests.cs`.  The row with `-- name: B` is Review Focus 4: a comment line between the two parts ends the string, so a marker can never be inside a quoted region.

<!-- file:tests/SqlSource.Tests/Parsing/Quoting/EscapeStringReaderTests.cs -->
````csharp
using Shouldly;
using SqlSource.Parsing.Quoting;
using Xunit;

namespace SqlSource.Tests.Parsing.Quoting;

public class EscapeStringReaderTests
{
    private static readonly EscapeStringReader Plain = new(continues: false);

    private static readonly EscapeStringReader Continued = new(continues: true);

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
    public void FindEnd_String_TakesBackslashEscapesOnlyAfterThePrefix(string text, string expected)
    {
        Region.Read(Plain, text, '\'').ShouldBe(expected);
        Region.Read(Continued, text, '\'').ShouldBe(expected);
    }

    // The E ends an identifier, so it is not a prefix.
    [Theory]
    [InlineData("typeE'a\\' x", "'a\\'")]
    [InlineData("_e'a\\' x", "'a\\'")]
    [InlineData("a$E'a\\' x", "'a\\'")]
    [InlineData("1e'a\\' x", "'a\\'")]
    public void FindEnd_LetterEAtTheEndOfAnIdentifier_IsNotAPrefix(string text, string expected) =>
        Region.Read(Plain, text, '\'').ShouldBe(expected);

    [Theory]
    [InlineData("E'abc")]
    [InlineData("E'abc\\'")]
    [InlineData("E'abc\\")]
    [InlineData("'abc")]
    public void FindEnd_StringWithoutAClosingQuote_IsUnterminated(string text) =>
        Region.Read(Plain, text, '\'').ShouldBe(Region.Unterminated);

    [Theory]
    [InlineData("E'a'\n'b\\'c' x", "'a'\n'b\\'c'")]
    [InlineData("E'a'\r\n    'b\\'c' x", "'a'\r\n    'b\\'c'")]
    [InlineData("E'a' \n\n\t'b\\'c'\n 'd\\'e' x", "'a' \n\n\t'b\\'c'\n 'd\\'e'")]
    [InlineData("E'a'\r'b\\'c' x", "'a'\r'b\\'c'")]
    public void FindEnd_EscapeStringFollowedByALineBreakAndAQuote_ContinuesWithEscapes(string text, string expected) =>
        Region.Read(Continued, text, '\'').ShouldBe(expected);

    [Theory]
    // No line break between the two.
    [InlineData("E'a' 'b' x")]
    [InlineData("E'a''b' x", "'a''b'")]
    // Something other than whitespace between the two.
    [InlineData("E'a' -- c\n'b' x")]
    [InlineData("E'a' /* c */\n'b' x")]
    [InlineData("E'a',\n'b' x")]
    [InlineData("E'a'\n-- name: B\n'b' x")]
    // Nothing after the line break.
    [InlineData("E'a'\n")]
    [InlineData("E'a'\n x")]
    public void FindEnd_EscapeStringNotFollowedByAContinuation_EndsAtItsOwnQuote(
        string text,
        string expected = "'a'"
    ) => Region.Read(Continued, text, '\'').ShouldBe(expected);

    [Fact]
    public void FindEnd_ContinuationWithTheOptionOff_IsNotRead() =>
        Region.Read(Plain, "E'a'\n'b\\'c' x", '\'').ShouldBe("'a'");

    [Fact]
    public void FindEnd_StringWithoutThePrefix_IsNotContinued() =>
        Region.Read(Continued, "'a'\n'b\\' x", '\'').ShouldBe("'a'");

    [Fact]
    public void FindEnd_ContinuationWithoutAClosingQuote_IsUnterminated() =>
        Region.Read(Continued, "E'a'\n'b\\' x", '\'').ShouldBe(Region.Unterminated);
}
````

`QuoteOperatorReaderTests.cs`:

<!-- file:tests/SqlSource.Tests/Parsing/Quoting/QuoteOperatorReaderTests.cs -->
````csharp
using Shouldly;
using SqlSource.Parsing.Quoting;
using Xunit;

namespace SqlSource.Tests.Parsing.Quoting;

public class QuoteOperatorReaderTests
{
    private static readonly QuoteOperatorReader Reader = new();

    [Theory]
    [InlineData("q'[it's]' x", "'[it's]'")]
    [InlineData("q'{it's}' x", "'{it's}'")]
    [InlineData("q'<it's>' x", "'<it's>'")]
    [InlineData("q'(it's)' x", "'(it's)'")]
    [InlineData("Q'[it's]' x", "'[it's]'")]
    public void FindEnd_BracketDelimiter_EndsAtItsPairFollowedByAQuote(string text, string expected) =>
        Region.Read(Reader, text, '\'').ShouldBe(expected);

    [Theory]
    [InlineData("q'!it's!' x", "'!it's!'")]
    [InlineData("q'|a'b|' x", "'|a'b|'")]
    [InlineData("q'aitsa' x", "'aitsa'")]
    [InlineData("q']x]' x", "']x]'")]
    [InlineData("q''it's'' x", "''it's''")]
    [InlineData("q'\"it's\"' x", "'\"it's\"'")]
    public void FindEnd_AnyOtherDelimiter_EndsAtTheSameCharacterFollowedByAQuote(string text, string expected) =>
        Region.Read(Reader, text, '\'').ShouldBe(expected);

    [Theory]
    // The closing delimiter in the body, not followed by a quote.
    [InlineData("q'[a]b]' x", "'[a]b]'")]
    [InlineData("q'[a[b]c]' x", "'[a[b]c]'")]
    [InlineData("q'!a!b!' x", "'!a!b!'")]
    // Comment syntax and other quotes in the body.
    [InlineData("q'[it's -- x /* y]' z", "'[it's -- x /* y]'")]
    [InlineData("q'[a\nb]' x", "'[a\nb]'")]
    [InlineData("q'[]' x", "'[]'")]
    // The first closing delimiter that a quote follows ends it, whatever comes after.
    [InlineData("q'[a]'b]' x", "'[a]'")]
    public void FindEnd_Body_IsEverythingUpToTheFirstCloserFollowedByAQuote(string text, string expected) =>
        Region.Read(Reader, text, '\'').ShouldBe(expected);

    [Theory]
    [InlineData("nq'[it's]' x", "'[it's]'")]
    [InlineData("Nq'[it's]' x", "'[it's]'")]
    [InlineData("NQ'[it's]' x", "'[it's]'")]
    [InlineData("(nq'[it's]') x", "'[it's]'")]
    [InlineData("=q'[it's]' x", "'[it's]'")]
    public void FindEnd_NationalPrefixOrAPrefixAfterPunctuation_IsRead(string text, string expected) =>
        Region.Read(Reader, text, '\'').ShouldBe(expected);

    // Each of these is an ordinary string that ends at the first quote that is not doubled.
    [Theory]
    [InlineData("'[it's]' x", "'[it'")]
    [InlineData("x'[it's]' x", "'[it'")]
    [InlineData("seq'[it's]' x", "'[it'")]
    [InlineData("a_q'[it's]' x", "'[it'")]
    [InlineData("anq'[it's]' x", "'[it'")]
    [InlineData("n'[it's]' x", "'[it'")]
    [InlineData("q 'a' x", "'a'")]
    public void FindEnd_StringWithoutThePrefix_IsAnOrdinaryString(string text, string expected) =>
        Region.Read(Reader, text, '\'').ShouldBe(expected);

    // Oracle does not take whitespace as a delimiter.
    [Theory]
    [InlineData("q' a' x", "' a'")]
    [InlineData("q'\ta' x", "'\ta'")]
    [InlineData("q'\na' x", "'\na'")]
    [InlineData("q'\r\na' x", "'\r\na'")]
    public void FindEnd_WhitespaceAfterTheQuote_IsNotADelimiter(string text, string expected) =>
        Region.Read(Reader, text, '\'').ShouldBe(expected);

    [Theory]
    [InlineData("q'[abc")]
    [InlineData("q'[abc]")]
    [InlineData("q'[abc'")]
    [InlineData("q'[abc] '")]
    [InlineData("q'[")]
    [InlineData("q'")]
    // A quote is a delimiter like any other: this opens a string that would close at the next two quotes.
    [InlineData("q'' x")]
    [InlineData("'abc")]
    public void FindEnd_StringWithoutItsClosingDelimiter_IsUnterminated(string text) =>
        Region.Read(Reader, text, '\'').ShouldBe(Region.Unterminated);
}
````

`BracketReaderTests.cs`:

<!-- file:tests/SqlSource.Tests/Parsing/Quoting/BracketReaderTests.cs -->
````csharp
using Shouldly;
using SqlSource.Parsing.Quoting;
using Xunit;

namespace SqlSource.Tests.Parsing.Quoting;

public class BracketReaderTests
{
    private static readonly BracketReader SqlServer = new(doubledCloserEscapes: true);

    private static readonly BracketReader Sqlite = new(doubledCloserEscapes: false);

    [Theory]
    [InlineData("[a] x", "[a]")]
    [InlineData("x.[order details] y", "[order details]")]
    [InlineData("[] x", "[]")]
    [InlineData("[a'b] x", "[a'b]")]
    [InlineData("[a--b] x", "[a--b]")]
    [InlineData("[a/*b] x", "[a/*b]")]
    [InlineData("[a[b] x", "[a[b]")]
    [InlineData("[a\nb] x", "[a\nb]")]
    public void FindEnd_ClosedIdentifier_EndsAfterItsBracket(string text, string expected)
    {
        Region.Read(SqlServer, text, '[').ShouldBe(expected);
        Region.Read(Sqlite, text, '[').ShouldBe(expected);
    }

    [Theory]
    [InlineData("[a]]b] x", "[a]]b]")]
    [InlineData("[a]]] x", "[a]]]")]
    [InlineData("[]]] x", "[]]]")]
    [InlineData("[a]]]]b] x", "[a]]]]b]")]
    public void FindEnd_DoubledCloserInSqlServer_StandsForOneBracket(string text, string expected) =>
        Region.Read(SqlServer, text, '[').ShouldBe(expected);

    [Theory]
    [InlineData("[a]]b] x", "[a]")]
    [InlineData("[a]]] x", "[a]")]
    public void FindEnd_DoubledCloserInSqlite_EndsAtTheFirst(string text, string expected) =>
        Region.Read(Sqlite, text, '[').ShouldBe(expected);

    [Theory]
    [InlineData("[abc")]
    [InlineData("[")]
    public void FindEnd_IdentifierWithoutAClosingBracket_IsUnterminated(string text)
    {
        Region.Read(SqlServer, text, '[').ShouldBe(Region.Unterminated);
        Region.Read(Sqlite, text, '[').ShouldBe(Region.Unterminated);
    }

    [Theory]
    [InlineData("[abc]]")]
    [InlineData("[abc]]def")]
    public void FindEnd_IdentifierThatEndsInAnEscapedBracketInSqlServer_IsUnterminated(string text) =>
        Region.Read(SqlServer, text, '[').ShouldBe(Region.Unterminated);
}
````

`DollarQuoteReaderTests.cs`:

<!-- file:tests/SqlSource.Tests/Parsing/Quoting/DollarQuoteReaderTests.cs -->
````csharp
using Shouldly;
using SqlSource.Parsing.Quoting;
using Xunit;

namespace SqlSource.Tests.Parsing.Quoting;

public class DollarQuoteReaderTests
{
    private static readonly DollarQuoteReader Reader = new();

    [Theory]
    [InlineData("$$ -- x $$ y", "$$ -- x $$")]
    [InlineData("$$$$ y", "$$$$")]
    [InlineData("$fn$ /* x */ $fn$ y", "$fn$ /* x */ $fn$")]
    [InlineData("$a$ $b$ -- c $a$ y", "$a$ $b$ -- c $a$")]
    [InlineData("$_t1$ ' $_t1$ y", "$_t1$ ' $_t1$")]
    [InlineData("$größe$ -- x $größe$ y", "$größe$ -- x $größe$")]
    [InlineData("x = $$a$$ y", "$$a$$")]
    [InlineData("$a$ $A$ $a$ y", "$a$ $A$ $a$")]
    [InlineData("$$ $a$ $$ y", "$$ $a$ $$")]
    public void FindEnd_OpenerWithItsCloser_EndsAfterTheSameTag(string text, string expected) =>
        Region.Read(Reader, text, '$').ShouldBe(expected);

    [Theory]
    // After an identifier character.
    [InlineData("v$session$ x $session$")]
    [InlineData("a$$ b $$")]
    [InlineData("_$$ b $$")]
    [InlineData("1$$ b $$")]
    // Not the form of an opener.
    [InlineData("$1 x")]
    [InlineData("$5.00 x")]
    [InlineData("$1$ x $1$")]
    [InlineData("$a-b$ x $a-b$")]
    [InlineData("$")]
    [InlineData("$a")]
    // An opener with no closer.
    [InlineData("$$ x")]
    [InlineData("$a$ x")]
    [InlineData("$a$ $b$ x")]
    [InlineData("$a$ x $A$")]
    [InlineData("$$")]
    public void FindEnd_DollarThatOpensNoQuote_IsNotAQuote(string text) =>
        Region.Read(Reader, text, '$').ShouldBe(Region.NotAQuote);
}
````

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/SqlSource.Tests --filter-namespace 'SqlSource.Tests.Parsing.Quoting'`
Expected: the build fails.  CS0234 says the namespace `Quoting` does not exist in `SqlSource.Parsing`.

- [ ] **Step 3: Write the readers**

Create the folder `src/SqlSource/Parsing/Quoting/`, and in it:

`QuoteReader.cs`:

<!-- file:src/SqlSource/Parsing/Quoting/QuoteReader.cs -->
````csharp
namespace SqlSource.Parsing.Quoting;

/// <summary>
/// Finds where one kind of quoted region ends.  A quoted region is a string or a quoted identifier: the lexer copies
/// it as written and looks for no comment inside it.
/// </summary>
/// <remarks>
/// A reader holds no state between calls and allocates nothing, so one instance serves every file.  It is called at
/// the character that opens the region, and may look at the characters before it for a prefix such as <c>E</c>.
/// </remarks>
internal abstract class QuoteReader
{
    /// <summary>The region is opened and never closed.</summary>
    public const int Unterminated = -1;

    /// <summary>The character opens no region here, and is plain text.</summary>
    public const int NotAQuote = -2;

    /// <summary>
    /// Returns the offset after the closing delimiter of the region that opens at <paramref name="start" />, or
    /// <see cref="Unterminated" />, or <see cref="NotAQuote" />.
    /// </summary>
    public abstract int FindEnd(string text, int start);

    /// <summary>True for a character that can be part of an unquoted identifier.</summary>
    protected static bool IsIdentifierCharacter(char value) => char.IsLetterOrDigit(value) || value is '_' or '$';

    /// <summary>
    /// The end of a region that closes with the character it opens with, where that character doubled stands for
    /// itself.
    /// </summary>
    protected static int FindDoubledEnd(string text, int start)
    {
        var quote = text[start];
        var index = start + 1;
        while (true)
        {
            var close = text.IndexOf(quote, index);
            if (close < 0)
            {
                return Unterminated;
            }

            if (close + 1 < text.Length && text[close + 1] == quote)
            {
                index = close + 2;
            }
            else
            {
                return close + 1;
            }
        }
    }

    /// <summary>
    /// As <see cref="FindDoubledEnd" />, and a backslash takes the character after it with it, whatever that is.
    /// </summary>
    protected static int FindBackslashEnd(string text, int start)
    {
        var quote = text[start];
        var index = start + 1;
        while (index < text.Length)
        {
            var current = text[index];
            if (current == '\\')
            {
                index += 2;
            }
            else if (current != quote)
            {
                index++;
            }
            else if (index + 1 < text.Length && text[index + 1] == quote)
            {
                index += 2;
            }
            else
            {
                return index + 1;
            }
        }

        return Unterminated;
    }
}
````

`DoubledQuoteReader.cs`:

<!-- file:src/SqlSource/Parsing/Quoting/DoubledQuoteReader.cs -->
````csharp
namespace SqlSource.Parsing.Quoting;

/// <summary>
/// The ANSI form: <c>'it''s'</c>, <c>"a""b"</c>, and <c>`a``b`</c> where a backtick is a quote.
/// </summary>
internal sealed class DoubledQuoteReader : QuoteReader
{
    public override int FindEnd(string text, int start) => FindDoubledEnd(text, start);
}
````

`BackslashQuoteReader.cs`:

<!-- file:src/SqlSource/Parsing/Quoting/BackslashQuoteReader.cs -->
````csharp
namespace SqlSource.Parsing.Quoting;

/// <summary>
/// The MySQL and MariaDB form of a string: <c>'it\'s'</c> and <c>"a\"b"</c>.  A doubled quote still stands for one.
/// </summary>
internal sealed class BackslashQuoteReader : QuoteReader
{
    public override int FindEnd(string text, int start) => FindBackslashEnd(text, start);
}
````

`EscapeStringReader.cs`:

<!-- file:src/SqlSource/Parsing/Quoting/EscapeStringReader.cs -->
````csharp
namespace SqlSource.Parsing.Quoting;

/// <summary>
/// A <c>'...'</c> string that takes backslash escapes only after PostgreSQL's <c>E</c> prefix, as in
/// <c>E'it\'s'</c>.  The prefix stays in the text before the region.
/// </summary>
/// <param name="continues">
/// Whether an <c>E</c> string goes on after whitespace that holds a line break, as PostgreSQL reads it:
/// <c>E'a'</c>, a new line, <c>'b\'c'</c> is one string, and its second part takes backslash escapes too.
/// </param>
internal sealed class EscapeStringReader(bool continues) : QuoteReader
{
    public override int FindEnd(string text, int start)
    {
        if (!HasPrefix(text, start))
        {
            return FindDoubledEnd(text, start);
        }

        var end = FindBackslashEnd(text, start);
        while (continues && end >= 0 && FindContinuation(text, end) is var next && next >= 0)
        {
            end = FindBackslashEnd(text, next);
        }

        return end;
    }

    private static bool HasPrefix(string text, int quote) =>
        quote > 0 && text[quote - 1] is 'E' or 'e' && (quote < 2 || !IsIdentifierCharacter(text[quote - 2]));

    // The offset of the quote that continues a string that closed at end, or -1.  Only whitespace may come between
    // the two, with at least one line break.  A comment there ends the string: a region that could hold a comment
    // could hold a marker, and a marker must never be inside a quoted region.
    private static int FindContinuation(string text, int end)
    {
        var index = end;
        var lineBreak = false;
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            lineBreak |= text[index] is '\r' or '\n';
            index++;
        }

        return lineBreak && index < text.Length && text[index] == '\'' ? index : -1;
    }
}
````

`QuoteOperatorReader.cs`:

<!-- file:src/SqlSource/Parsing/Quoting/QuoteOperatorReader.cs -->
````csharp
namespace SqlSource.Parsing.Quoting;

/// <summary>
/// A <c>'...'</c> string that is read by Oracle's quote operator after a <c>q</c> prefix, as in <c>q'[it's]'</c>
/// and <c>nq'!it's!'</c>.  The prefix stays in the text before the region.
/// </summary>
/// <remarks>
/// The character after the quote is the opening delimiter.  The region ends at the first closing delimiter that is
/// directly followed by a quote: delimiters are not counted for depth, so <c>q'[a[b]c]'</c> is one string.
/// </remarks>
internal sealed class QuoteOperatorReader : QuoteReader
{
    public override int FindEnd(string text, int start)
    {
        var open = start + 1;
        if (!HasPrefix(text, start) || open >= text.Length || text[open] is ' ' or '\t' or '\r' or '\n')
        {
            return FindDoubledEnd(text, start);
        }

        var close = GetClosingDelimiter(text[open]);
        for (var index = text.IndexOf(close, open + 1); index >= 0; index = text.IndexOf(close, index + 1))
        {
            if (index + 1 < text.Length && text[index + 1] == '\'')
            {
                return index + 2;
            }
        }

        return Unterminated;
    }

    private static char GetClosingDelimiter(char open) =>
        open switch
        {
            '[' => ']',
            '{' => '}',
            '<' => '>',
            '(' => ')',
            _ => open,
        };

    // q or Q, or one of those after n or N, where the prefix does not end an identifier.
    private static bool HasPrefix(string text, int quote)
    {
        if (quote == 0 || text[quote - 1] is not ('q' or 'Q'))
        {
            return false;
        }

        var before = quote - 2;
        if (before >= 0 && text[before] is 'n' or 'N')
        {
            before--;
        }

        return before < 0 || !IsIdentifierCharacter(text[before]);
    }
}
````

`BracketReader.cs`:

<!-- file:src/SqlSource/Parsing/Quoting/BracketReader.cs -->
````csharp
namespace SqlSource.Parsing.Quoting;

/// <summary>
/// A bracketed identifier, <c>[order details]</c>.
/// </summary>
/// <param name="doubledCloserEscapes">
/// True for SQL Server, where <c>]]</c> stands for one <c>]</c>.  False for SQLite, where the first <c>]</c> ends the
/// identifier.
/// </param>
internal sealed class BracketReader(bool doubledCloserEscapes) : QuoteReader
{
    public override int FindEnd(string text, int start)
    {
        var index = start + 1;
        while (true)
        {
            var close = text.IndexOf(']', index);
            if (close < 0)
            {
                return Unterminated;
            }

            if (doubledCloserEscapes && close + 1 < text.Length && text[close + 1] == ']')
            {
                index = close + 2;
            }
            else
            {
                return close + 1;
            }
        }
    }
}
````

`DollarQuoteReader.cs`.  It is today's `TryFindDollarQuoteEnd` from `SqlLexer`, with the delimiter compared as a span, so that it no longer allocates a string for each opener.

<!-- file:src/SqlSource/Parsing/Quoting/DollarQuoteReader.cs -->
````csharp
using System;

namespace SqlSource.Parsing.Quoting;

/// <summary>
/// PostgreSQL's dollar quote, <c>$tag$ ... $tag$</c>, where the tag may be empty.
/// </summary>
/// <remarks>
/// A dollar sign has other meanings: a parameter such as <c>$1</c>, a money literal, a character of an identifier.
/// So one that follows an identifier character, or that opens a quote which is never closed, is plain text.
/// </remarks>
internal sealed class DollarQuoteReader : QuoteReader
{
    public override int FindEnd(string text, int start)
    {
        if (start > 0 && IsIdentifierCharacter(text[start - 1]))
        {
            return NotAQuote;
        }

        var tagEnd = start + 1;
        if (tagEnd < text.Length && (char.IsLetter(text[tagEnd]) || text[tagEnd] == '_'))
        {
            while (tagEnd < text.Length && (char.IsLetterOrDigit(text[tagEnd]) || text[tagEnd] == '_'))
            {
                tagEnd++;
            }
        }

        if (tagEnd >= text.Length || text[tagEnd] != '$')
        {
            return NotAQuote;
        }

        var delimiter = text.AsSpan(start, tagEnd - start + 1);
        var body = tagEnd + 1;
        var close = text.AsSpan(body).IndexOf(delimiter, StringComparison.Ordinal);
        return close < 0 ? NotAQuote : body + close + delimiter.Length;
    }
}
````

Nothing uses the readers yet.  `SqlLexer` still has its own copies of three of these scans; Task 3 removes them.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/SqlSource.Tests --filter-namespace 'SqlSource.Tests.Parsing.Quoting'`
Expected: total 147, failed 0.

Run: `dotnet test --solution SqlSource.slnx`
Expected: total 993, failed 0.

- [ ] **Step 5: Format and validate**

Run: `./format.sh`

Run: `./pre-commit-validation.sh`
Expected: every step `passed`.

- [ ] **Step 6: Commit**

```bash
git add src/SqlSource/Parsing/Quoting tests/SqlSource.Tests/Parsing/Quoting
```

```bash
git commit -m "Add a reader for each way a quoted region ends

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: The rules of each dialect, and a lexer that reads by them

**Files:**
- Create: `src/SqlSource/Parsing/SqlDialectRules.cs`
- Rewrite: `src/SqlSource/Parsing/SqlLexer.cs`
- Modify: `src/SqlSource/Parsing/SqlLexemeKind.cs`, `src/SqlSource/Parsing/SqlMarkerReader.cs`, `src/SqlSource/Parsing/SqlFileParser.cs`
- Test: `tests/SqlSource.Tests/Parsing/SqlDialectRulesTests.cs` (new), `SqlLexerTests.cs` (rewritten), `SqlMarkerReaderTests.cs`, `SqlTextBuilderTests.cs`, `SqlDirectiveScopeTests.cs`

**Interfaces:**
- Consumes: `SqlDialect` (Task 1); `QuoteReader` and the six readers (Task 2).
- Produces, in namespace `SqlSource.Parsing`, `internal`:
  - `sealed class SqlDialectRules`:
    - `static SqlDialectRules Ansi`, `SqlServer`, `PostgreSql`, `MySql`, `MariaDb`, `Sqlite`, `Oracle`, one shared instance each;
    - `static SqlDialectRules For(SqlDialect dialect)`;
    - `bool NestedComments`, `DashNeedsWhitespace`, `HashComments`, `LineHints`, `MariaDbHints`;
    - `QuoteReader? ReaderFor(char opener)`;
    - `int FindStarter(string text, int start)`: the offset of the next character that can start a comment or a quoted region, or -1.
  - `sealed class SqlLexer(string text, SqlDialectRules rules)`:
    - `SqlDialectRules Rules { get; set; }`;
    - `int Position { get; }`: the offset after the last lexeme read;
    - `static SqlLexResult Lex(string text, SqlDialectRules rules)`;
    - `bool TryReadLeadingComment(out SqlLexeme comment)`;
    - `SqlLexResult ReadToEnd()`.
  - `SqlLexer.Lex(string)`, with one argument, no longer exists.  Every caller passes rules.

The table that `SqlDialectRules` holds, from the spec.  A dash means the character opens nothing and is plain text.

| | `Ansi` | `SqlServer` | `PostgreSql` | `MySql` | `MariaDb` | `Sqlite` | `Oracle` |
|----|----|----|----|----|----|----|----|
| `'` | Escape-string | Doubled | Escape-string, continued | Backslash | Backslash | Doubled | Quote-operator |
| `"` | Doubled | Doubled | Doubled | Backslash | Backslash | Doubled | Doubled |
| `` ` `` | Doubled | - | - | Doubled | Doubled | Doubled | - |
| `[` | - | Bracket, `]]` escapes | - | - | - | Bracket | - |
| `$` | Dollar | - | Dollar | Dollar | - | - | - |
| `NestedComments` | Yes | Yes | Yes | No | No | No | No |
| `DashNeedsWhitespace` | No | No | No | Yes | Yes | No | No |
| `HashComments` | No | No | No | Yes | Yes | No | No |
| `MariaDbHints` (`/*M!`) | No | No | No | No | Yes | No | No |
| `LineHints` (`--+`) | No | No | No | No | No | No | Yes |

`/*+` and `/*!` are hints in every dialect, as today.

- [ ] **Step 1: Write the failing tests**

Create `tests/SqlSource.Tests/Parsing/SqlDialectRulesTests.cs`:

<!-- file:tests/SqlSource.Tests/Parsing/SqlDialectRulesTests.cs -->
````csharp
using System;
using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

// What each dialect makes of each construct is pinned through the lexer, in SqlLexerTests.  These are about the table
// itself.
public class SqlDialectRulesTests
{
    [Fact]
    public void For_EveryDialect_GivesItsOwnSharedInstance()
    {
        var dialects = Enum.GetValues<SqlDialect>();

        var rules = dialects.Select(SqlDialectRules.For).ToArray();

        rules.Distinct().Count().ShouldBe(dialects.Length);
        rules.ShouldBe(dialects.Select(SqlDialectRules.For));
        SqlDialectRules.For(SqlDialect.Ansi).ShouldBeSameAs(SqlDialectRules.Ansi);
        SqlDialectRules.For(SqlDialect.SqlServer).ShouldBeSameAs(SqlDialectRules.SqlServer);
        SqlDialectRules.For(SqlDialect.PostgreSql).ShouldBeSameAs(SqlDialectRules.PostgreSql);
        SqlDialectRules.For(SqlDialect.MySql).ShouldBeSameAs(SqlDialectRules.MySql);
        SqlDialectRules.For(SqlDialect.MariaDb).ShouldBeSameAs(SqlDialectRules.MariaDb);
        SqlDialectRules.For(SqlDialect.Sqlite).ShouldBeSameAs(SqlDialectRules.Sqlite);
        SqlDialectRules.For(SqlDialect.Oracle).ShouldBeSameAs(SqlDialectRules.Oracle);
    }

    // A value that is not a dialect cannot come from a name, and must not throw inside the compiler if it ever does.
    [Fact]
    public void For_ValueThatIsNotADialect_GivesTheAnsiRules() =>
        SqlDialectRules.For((SqlDialect)99).ShouldBeSameAs(SqlDialectRules.Ansi);

    [Theory]
    [InlineData("Ansi", "'\"`$")]
    [InlineData("SqlServer", "'\"[")]
    [InlineData("PostgreSql", "'\"$")]
    [InlineData("MySql", "'\"`$")]
    [InlineData("MariaDb", "'\"`")]
    [InlineData("Sqlite", "'\"`[")]
    [InlineData("Oracle", "'\"")]
    public void ReaderFor_Character_HasAReaderOnlyWhereTheDialectOpensAQuotedRegion(string dialect, string openers)
    {
        var rules = SqlDialectRules.For(Enum.Parse<SqlDialect>(dialect));

        var found = Enumerable
            .Range(0, char.MaxValue + 1)
            .Select(value => (char)value)
            .Where(value => rules.ReaderFor(value) is not null);

        new string([.. found]).ShouldBe(new string([.. openers.Order()]));
    }

    [Theory]
    [InlineData("Ansi", true, false, false, false, false)]
    [InlineData("SqlServer", true, false, false, false, false)]
    [InlineData("PostgreSql", true, false, false, false, false)]
    [InlineData("MySql", false, true, true, false, false)]
    [InlineData("MariaDb", false, true, true, false, true)]
    [InlineData("Sqlite", false, false, false, false, false)]
    [InlineData("Oracle", false, false, false, true, false)]
    public void CommentRules_OfEachDialect_AreTheRowOfTheTable(
        string dialect,
        bool nestedComments,
        bool dashNeedsWhitespace,
        bool hashComments,
        bool lineHints,
        bool mariaDbHints
    )
    {
        var rules = SqlDialectRules.For(Enum.Parse<SqlDialect>(dialect));

        rules.NestedComments.ShouldBe(nestedComments);
        rules.DashNeedsWhitespace.ShouldBe(dashNeedsWhitespace);
        rules.HashComments.ShouldBe(hashComments);
        rules.LineHints.ShouldBe(lineHints);
        rules.MariaDbHints.ShouldBe(mariaDbHints);
    }

    [Theory]
    [InlineData("SELECT a, b FROM t WHERE x = 1", 0, -1)]
    [InlineData("", 0, -1)]
    [InlineData("a - b", 0, 2)]
    [InlineData("a / b", 0, 2)]
    [InlineData("a # b", 0, 2)]
    [InlineData("a 'b'", 0, 2)]
    [InlineData("a \"b\"", 0, 2)]
    [InlineData("a [b]", 0, 2)]
    [InlineData("a - b - c", 3, 6)]
    [InlineData("a - b", 5, -1)]
    public void FindStarter_Text_FindsTheNextCharacterThatCanStartACommentOrAQuotedRegion(
        string text,
        int start,
        int expected
    ) => SqlDialectRules.SqlServer.FindStarter(text, start).ShouldBe(expected);

    // A character that opens nothing in a dialect is plain text there, and is skipped with the rest.
    [Fact]
    public void FindStarter_OpenerOfAnotherDialect_IsSkipped()
    {
        SqlDialectRules.SqlServer.FindStarter("a `b` $$ c", 0).ShouldBe(-1);
        SqlDialectRules.Oracle.FindStarter("a [b] `c` $$ d", 0).ShouldBe(-1);
        SqlDialectRules.Ansi.FindStarter("a [b] `c`", 0).ShouldBe(6);
    }
}
````

Replace `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs` with the file below.  Every test it had is still there with the expectation it had, read by the ANSI rules.  The three single-argument calls that pin a known limit now name `SqlDialectRules.Ansi`.  New: the theory over every construct and dialect, the tests of MySQL's `--` and `#`, the unterminated cases of each dialect, the invariants under every dialect, and the tests of `TryReadLeadingComment`.

<!-- file:tests/SqlSource.Tests/Parsing/SqlLexerTests.cs -->
````csharp
using System;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

// A test without a dialect in its name reads by the ANSI rules, which are the default.  An internal type cannot be
// the parameter of a public test method, so a row names its dialects, separated by commas.
public class SqlLexerTests
{
    private const string AllDialects = "Ansi,SqlServer,PostgreSql,MySql,MariaDb,Sqlite,Oracle";

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
        var result = SqlLexer.Lex("'a\\'b'", SqlDialectRules.Ansi);

        result.Error.ShouldBe(SqlParseError.Create(SqlParseErrorKind.UnterminatedQuote, new TextSpan(5, 1)));
    }

    [Fact]
    public void Lex_KnownLimit_CommentOpenerInsideCommentMustBeClosed()
    {
        var result = SqlLexer.Lex("/* a /* b */ SELECT 1", SqlDialectRules.Ansi);

        result.Error.ShouldBe(SqlParseError.Create(SqlParseErrorKind.UnterminatedBlockComment, new TextSpan(0, 2)));
    }

    [Fact]
    public void Lex_KnownLimit_OracleQuoteLiteralIsNotRecognised()
    {
        var result = SqlLexer.Lex("q'[it's]'", SqlDialectRules.Ansi);

        result.Error.ShouldBe(SqlParseError.Create(SqlParseErrorKind.UnterminatedQuote, new TextSpan(8, 1)));
    }

    // The silent form of the limits above: when the misread quotes happen to balance, SQL is read as a comment.
    [Theory]
    [InlineData("'a\\'b -- c', 2", "LineComment:-- c', 2")]
    [InlineData("[a'b], 'x -- y'", "LineComment:-- y'")]
    [InlineData("q'[it's -- x]', q'[y's]'", "LineComment:-- x]', q'[y's]'")]
    public void Lex_KnownLimit_MisreadQuotesThatBalance_TurnSqlIntoAComment(string text, string comment) =>
        Lex(text)[^1].ShouldBe(comment);

    [Theory]
    [InlineData("'abc", 0)]
    [InlineData("x \"abc", 2)]
    [InlineData("`abc", 0)]
    [InlineData("'abc''", 0)]
    [InlineData("E'abc\\'", 1)]
    [InlineData("E'abc\\", 1)]
    public void Lex_UnterminatedQuote_ReportsTheOpeningQuote(string text, int position)
    {
        var result = SqlLexer.Lex(text, SqlDialectRules.Ansi);

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
        var result = SqlLexer.Lex(text, SqlDialectRules.Ansi);

        result.Error.ShouldBe(
            SqlParseError.Create(SqlParseErrorKind.UnterminatedBlockComment, new TextSpan(position, 2))
        );
        result.Lexemes.Count.ShouldBe(0);
    }

    // Each construct of the dialect table, read by every dialect.  A row lists the dialects that share a reading, and
    // its lexemes are separated by " | ".
    [Theory]
    // A backslash in a plain string.
    [InlineData("MySql,MariaDb", "'a\\'b' -- c'", "Quoted:'a\\'b' | Text:  | LineComment:-- c'")]
    [InlineData("Ansi,SqlServer,PostgreSql,Sqlite,Oracle", "'a\\'b' -- c'", "Quoted:'a\\' | Text:b | Quoted:' -- c'")]
    // A backslash in a double-quoted region.
    [InlineData("MySql,MariaDb", "\"a\\\"b\" -- c\"", "Quoted:\"a\\\"b\" | Text:  | LineComment:-- c\"")]
    [InlineData(
        "Ansi,SqlServer,PostgreSql,Sqlite,Oracle",
        "\"a\\\"b\" -- c\"",
        "Quoted:\"a\\\" | Text:b | Quoted:\" -- c\""
    )]
    // PostgreSQL's E prefix.  MySQL and MariaDB take the backslash with or without it.
    [InlineData(
        "Ansi,PostgreSql,MySql,MariaDb",
        "E'a\\'b' -- c'",
        "Text:E | Quoted:'a\\'b' | Text:  | LineComment:-- c'"
    )]
    [InlineData("SqlServer,Sqlite,Oracle", "E'a\\'b' -- c'", "Text:E | Quoted:'a\\' | Text:b | Quoted:' -- c'")]
    // An E string continued on the next line.
    [InlineData("PostgreSql", "E'a'\n'b\\'c' -- d'", "Text:E | Quoted:'a'\n'b\\'c' | Text:  | LineComment:-- d'")]
    [InlineData(
        "Ansi",
        "E'a'\n'b\\'c' -- d'",
        "Text:E | Quoted:'a' | Text:\n | Quoted:'b\\' | Text:c | Quoted:' -- d'"
    )]
    // Oracle's quote operator.
    [InlineData("Oracle", "q'[it's -- x]', q'[y's]'", "Text:q | Quoted:'[it's -- x]' | Text:, q | Quoted:'[y's]'")]
    [InlineData(
        "Ansi,SqlServer,PostgreSql,Sqlite",
        "q'[it's -- x]', q'[y's]'",
        "Text:q | Quoted:'[it' | Text:s  | LineComment:-- x]', q'[y's]'"
    )]
    // A backtick.
    [InlineData("Ansi,MySql,MariaDb,Sqlite", "`a -- b` c", "Quoted:`a -- b` | Text: c")]
    [InlineData("SqlServer,PostgreSql,Oracle", "`a -- b` c", "Text:`a  | LineComment:-- b` c")]
    // A bracketed identifier.
    [InlineData("SqlServer,Sqlite", "[a'b], 'x -- y'", "Quoted:[a'b] | Text:,  | Quoted:'x -- y'")]
    [InlineData(
        "Ansi,PostgreSql,MySql,MariaDb,Oracle",
        "[a'b], 'x -- y'",
        "Text:[a | Quoted:'b], ' | Text:x  | LineComment:-- y'"
    )]
    [InlineData("SqlServer", "[a]]--b] c", "Quoted:[a]]--b] | Text: c")]
    [InlineData("Sqlite", "[a]]--b] c", "Quoted:[a] | Text:] | LineComment:--b] c")]
    // A dollar quote.
    [InlineData("Ansi,PostgreSql,MySql", "$$ -- x $$ y", "Quoted:$$ -- x $$ | Text: y")]
    [InlineData("SqlServer,MariaDb,Sqlite,Oracle", "$$ -- x $$ y", "Text:$$  | LineComment:-- x $$ y")]
    // A comment opener inside a block comment.
    [InlineData("Ansi,SqlServer,PostgreSql", "/* a /* b */ c */ d", "BlockComment:/* a /* b */ c */ | Text: d")]
    [InlineData("MySql,MariaDb,Sqlite,Oracle", "/* a /* b */ c */ d", "BlockComment:/* a /* b */ | Text: c */ d")]
    // Two dashes with no whitespace after them.
    [InlineData("MySql,MariaDb", "5--3", "Text:5--3")]
    [InlineData("Ansi,SqlServer,PostgreSql,Sqlite,Oracle", "5--3", "Text:5 | LineComment:--3")]
    // A hash sign.
    [InlineData("MySql,MariaDb", "1 # c -- d\n2", "Text:1  | LineComment:# c -- d | Text:\n2")]
    [InlineData(
        "Ansi,SqlServer,PostgreSql,Sqlite,Oracle",
        "1 # c -- d\n2",
        "Text:1 # c  | LineComment:-- d | Text:\n2"
    )]
    // The hints every dialect keeps.
    [InlineData(AllDialects, "/*+ h */ x", "Hint:/*+ h */ | Text: x")]
    [InlineData(AllDialects, "/*! h */ x", "Hint:/*! h */ | Text: x")]
    // MariaDB's own executable comment.
    [InlineData("MariaDb", "/*M! h */ x", "Hint:/*M! h */ | Text: x")]
    [InlineData("Ansi,SqlServer,PostgreSql,MySql,Sqlite,Oracle", "/*M! h */ x", "BlockComment:/*M! h */ | Text: x")]
    // Oracle's line hint.  MySQL and MariaDB do not read it as a comment at all: no whitespace follows the dashes.
    [InlineData("Oracle", "--+ h\nx", "Hint:--+ h | Text:\nx")]
    [InlineData("Ansi,SqlServer,PostgreSql,Sqlite", "--+ h\nx", "LineComment:--+ h | Text:\nx")]
    [InlineData("MySql,MariaDb", "--+ h\nx", "Text:--+ h\nx")]
    public void Lex_Construct_IsReadByTheRulesOfTheDialect(string dialects, string text, string expected)
    {
        foreach (var dialect in dialects.Split(','))
        {
            string.Join(" | ", Lex(text, Rules(dialect))).ShouldBe(expected, dialect);
        }
    }

    [Theory]
    [InlineData("5 -- 3", "Text:5  | LineComment:-- 3")]
    [InlineData("5 --\t3", "Text:5  | LineComment:--\t3")]
    [InlineData("5 --\n3", "Text:5  | LineComment:-- | Text:\n3")]
    [InlineData("5 --\r\n3", "Text:5  | LineComment:-- | Text:\r\n3")]
    [InlineData("5 --", "Text:5  | LineComment:--")]
    [InlineData("5 --\u00013", "Text:5  | LineComment:--\u00013")]
    [InlineData("5 --- 3", "Text:5 - | LineComment:-- 3")]
    [InlineData("5 --3 -- c", "Text:5 --3  | LineComment:-- c")]
    [InlineData("5 --'a -- b'", "Text:5 -- | Quoted:'a -- b'")]
    public void Lex_TwoDashesInMySql_AreACommentOnlyBeforeWhitespaceAControlCharacterOrTheEnd(
        string text,
        string expected
    )
    {
        string.Join(" | ", Lex(text, SqlDialectRules.MySql)).ShouldBe(expected);
        string.Join(" | ", Lex(text, SqlDialectRules.MariaDb)).ShouldBe(expected);
    }

    [Theory]
    [InlineData("# c\nx", "LineComment:# c | Text:\nx")]
    [InlineData("#c\r\nx", "LineComment:#c | Text:\r\nx")]
    [InlineData("#", "LineComment:#")]
    [InlineData("x #'a\n'b'", "Text:x  | LineComment:#'a | Text:\n | Quoted:'b'")]
    [InlineData("'a # b' # c", "Quoted:'a # b' | Text:  | LineComment:# c")]
    [InlineData("/* # */ x", "BlockComment:/* # */ | Text: x")]
    public void Lex_HashInMySql_StartsACommentToTheEndOfItsLine(string text, string expected) =>
        string.Join(" | ", Lex(text, SqlDialectRules.MySql)).ShouldBe(expected);

    [Theory]
    [InlineData(nameof(SqlDialect.SqlServer), "x [abc", 2)]
    [InlineData(nameof(SqlDialect.SqlServer), "[abc]]", 0)]
    [InlineData(nameof(SqlDialect.Sqlite), "[abc", 0)]
    [InlineData(nameof(SqlDialect.Oracle), "q'[abc]", 1)]
    [InlineData(nameof(SqlDialect.Oracle), "nq'[abc' x", 2)]
    [InlineData(nameof(SqlDialect.MySql), "'abc\\'", 0)]
    [InlineData(nameof(SqlDialect.MariaDb), "\"abc\\\"", 0)]
    // The second part of a continued string is not closed.  The error is at the quote that opened the string.
    [InlineData(nameof(SqlDialect.PostgreSql), "E'a'\n'b\\'", 1)]
    // Two readings that differ from the database, which docs/tech-debt/TD-0004 lists.  A MySQL comment for a version
    // ends at the first */, inside a string of its body too, so the quote after it opens a string.
    [InlineData(nameof(SqlDialect.MySql), "/*!50700 '*/' */", 12)]
    // CockroachDB reads b'\'' as one literal.  The PostgreSQL rules have no backslash escape there.
    [InlineData(nameof(SqlDialect.PostgreSql), "b'\\''", 1)]
    public void Lex_UnterminatedQuoteOfADialect_ReportsTheOpeningDelimiter(string dialect, string text, int position)
    {
        var result = SqlLexer.Lex(text, Rules(dialect));

        result.Error.ShouldBe(SqlParseError.Create(SqlParseErrorKind.UnterminatedQuote, new TextSpan(position, 1)));
        result.Lexemes.Count.ShouldBe(0);
    }

    // SQLite itself accepts a block comment that runs to the end of the input.  It would take every later query of
    // the file with it, so it is an error here, as in every dialect.
    [Fact]
    public void Lex_UnterminatedBlockComment_IsAnErrorInEveryDialect()
    {
        foreach (var dialect in Enum.GetValues<SqlDialect>())
        {
            var result = SqlLexer.Lex("SELECT 1 /* x", SqlDialectRules.For(dialect));

            result.Error.ShouldBe(
                SqlParseError.Create(SqlParseErrorKind.UnterminatedBlockComment, new TextSpan(9, 2)),
                dialect.ToString()
            );
        }
    }

    [Fact]
    public void Lex_AnyText_CoversItWithoutGapsInEveryDialect()
    {
        const string Text =
            "SELECT 'a', \"b\" /* c */ -- d\r\nFROM $$e$$ /*+ f */ `g` [h] # i\n, q'[j]', E'k'\n'l' /*M! m */ --+ n\n";

        foreach (var dialect in Enum.GetValues<SqlDialect>())
        {
            var result = SqlLexer.Lex(Text, SqlDialectRules.For(dialect));

            result.Error.ShouldBeNull(dialect.ToString());
            var position = 0;
            foreach (var lexeme in result.Lexemes)
            {
                lexeme.Span.Start.ShouldBe(position, dialect.ToString());
                lexeme.Span.Length.ShouldBeGreaterThan(0, dialect.ToString());
                position = lexeme.Span.End;
            }

            position.ShouldBe(Text.Length, dialect.ToString());
        }
    }

    [Theory]
    [InlineData("-")]
    [InlineData("x -")]
    [InlineData("/")]
    [InlineData("$")]
    [InlineData("a$")]
    [InlineData("$a")]
    [InlineData("E")]
    [InlineData("q")]
    [InlineData("nq")]
    [InlineData("]")]
    [InlineData("*")]
    public void Lex_TextEndingWhereAConstructCouldStart_IsTextInEveryDialect(string text)
    {
        foreach (var dialect in Enum.GetValues<SqlDialect>())
        {
            Lex(text, SqlDialectRules.For(dialect)).ShouldBe(["Text:" + text], dialect.ToString());
        }
    }

    [Fact]
    public void Lex_NonAsciiText_IsKeptIntact() =>
        Lex("SELECT 'é😀' -- ñ").ShouldBe(["Text:SELECT ", "Quoted:'é😀'", "Text: ", "LineComment:-- ñ"]);

    // A character beyond the table of openers opens nothing, in any dialect.
    [Fact]
    public void Lex_NonAsciiCharacterThatLooksLikeAQuote_IsText()
    {
        foreach (var dialect in Enum.GetValues<SqlDialect>())
        {
            Lex("a ‘b’ ＄ c", SqlDialectRules.For(dialect)).ShouldBe(["Text:a ‘b’ ＄ c"], dialect.ToString());
        }
    }

    [Fact]
    public void Lex_DeeplyNestedBlockComments_IsOneComment()
    {
        const int Depth = 100_000;
        var text = string.Concat(Enumerable.Repeat("/*", Depth)) + string.Concat(Enumerable.Repeat("*/", Depth));

        var result = SqlLexer.Lex(text, SqlDialectRules.Ansi);

        result.Error.ShouldBeNull();
        result.Lexemes.Count.ShouldBe(1);
        result.Lexemes[0].ShouldBe(new SqlLexeme(SqlLexemeKind.BlockComment, new TextSpan(0, text.Length)));
    }

    [Fact]
    public void TryReadLeadingComment_CommentsBeforeTheFirstSql_AreReadOneAtATime()
    {
        const string Text = "  -- a\n\n/* b */ -- c\nSELECT 1 -- d\n";
        var lexer = new SqlLexer(Text, SqlDialectRules.Ansi);

        var comments = ReadLeadingComments(lexer, Text);

        comments.ShouldBe(["LineComment:-- a", "BlockComment:/* b */", "LineComment:-- c"]);
        lexer.Position.ShouldBe(Text.IndexOf("\nSELECT", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("SELECT 1 -- a")]
    [InlineData("'a' -- b")]
    [InlineData("/*+ h */ -- a")]
    [InlineData("/*! h */ -- a")]
    [InlineData("- - a")]
    [InlineData("/ * a */")]
    [InlineData("   ")]
    [InlineData("")]
    public void TryReadLeadingComment_TextThatDoesNotStartWithAComment_ReadsNothing(string text)
    {
        var lexer = new SqlLexer(text, SqlDialectRules.Ansi);

        lexer.TryReadLeadingComment(out _).ShouldBeFalse();

        lexer.Position.ShouldBe(0);
    }

    // What counts as a comment is decided by the rules the lexer holds.
    [Theory]
    [InlineData(nameof(SqlDialect.MySql), "# a\nx", true)]
    [InlineData(nameof(SqlDialect.Ansi), "# a\nx", false)]
    [InlineData(nameof(SqlDialect.MySql), "--a\nx", false)]
    [InlineData(nameof(SqlDialect.Ansi), "--a\nx", true)]
    [InlineData(nameof(SqlDialect.Oracle), "--+ a\nx", false)]
    [InlineData(nameof(SqlDialect.Ansi), "--+ a\nx", true)]
    [InlineData(nameof(SqlDialect.MariaDb), "/*M! a */ x", false)]
    [InlineData(nameof(SqlDialect.MySql), "/*M! a */ x", true)]
    public void TryReadLeadingComment_CommentFormOfOneDialect_IsReadOnlyByThatDialect(
        string dialect,
        string text,
        bool expected
    ) => new SqlLexer(text, Rules(dialect)).TryReadLeadingComment(out _).ShouldBe(expected);

    [Fact]
    public void TryReadLeadingComment_RulesChangedBetweenComments_ApplyToTheTextAfterTheLastLexeme()
    {
        const string Text = "-- a\n# b\nSELECT 'c\\'d' # e\n";
        var lexer = new SqlLexer(Text, SqlDialectRules.Ansi);

        lexer.TryReadLeadingComment(out _).ShouldBeTrue();
        lexer.TryReadLeadingComment(out _).ShouldBeFalse();
        lexer.Rules = SqlDialectRules.MySql;
        lexer.TryReadLeadingComment(out _).ShouldBeTrue();
        lexer.TryReadLeadingComment(out _).ShouldBeFalse();
        var result = lexer.ReadToEnd();

        result.Error.ShouldBeNull();
        Describe(result, Text)
            .ShouldBe([
                "LineComment:-- a",
                "Text:\n",
                "LineComment:# b",
                "Text:\nSELECT ",
                "Quoted:'c\\'d'",
                "Text: ",
                "LineComment:# e",
                "Text:\n",
            ]);
    }

    [Fact]
    public void TryReadLeadingComment_ThenReadToEnd_GivesTheLexemesOfLexingInOneCall()
    {
        const string Text = "/* a */\n-- b\nSELECT 'c' -- d\n";
        var lexer = new SqlLexer(Text, SqlDialectRules.Ansi);

        _ = ReadLeadingComments(lexer, Text);

        lexer.ReadToEnd().ShouldBe(SqlLexer.Lex(Text, SqlDialectRules.Ansi));
    }

    [Fact]
    public void TryReadLeadingComment_UnterminatedBlockComment_ReadsNothingAndReadToEndReportsIt()
    {
        var lexer = new SqlLexer("-- a\n/* b", SqlDialectRules.Ansi);

        lexer.TryReadLeadingComment(out _).ShouldBeTrue();
        lexer.TryReadLeadingComment(out _).ShouldBeFalse();
        lexer.TryReadLeadingComment(out _).ShouldBeFalse();

        var result = lexer.ReadToEnd();
        result.Error.ShouldBe(SqlParseError.Create(SqlParseErrorKind.UnterminatedBlockComment, new TextSpan(5, 2)));
        result.Lexemes.Count.ShouldBe(0);
    }

    [Theory]
    [InlineData("  SELECT 1 \n", 2, 8)]
    [InlineData("'a'", 0, 3)]
    [InlineData("/*+ h */", 0, 8)]
    public void GetContentSpan_LexemeWithContent_ReturnsItWithoutSurroundingWhitespace(
        string text,
        int start,
        int length
    ) => SqlLexer.Lex(text, SqlDialectRules.Ansi).Lexemes[0].GetContentSpan(text).ShouldBe(new TextSpan(start, length));

    [Theory]
    [InlineData(" \r\n\t ")]
    [InlineData("-- c")]
    [InlineData("/* c */")]
    public void GetContentSpan_WhitespaceOrComment_ReturnsNull(string text) =>
        SqlLexer.Lex(text, SqlDialectRules.Ansi).Lexemes[0].GetContentSpan(text).ShouldBeNull();

    [Fact]
    public void GetContentSpan_HashCommentOrLineHint_FollowsItsKind()
    {
        SqlLexer.Lex("# c", SqlDialectRules.MySql).Lexemes[0].GetContentSpan("# c").ShouldBeNull();
        SqlLexer.Lex("--+ h", SqlDialectRules.Oracle).Lexemes[0].GetContentSpan("--+ h").ShouldBe(new TextSpan(0, 5));
    }

    private static SqlDialectRules Rules(string dialect) => SqlDialectRules.For(Enum.Parse<SqlDialect>(dialect));

    private static string[] Lex(string text) => Lex(text, SqlDialectRules.Ansi);

    private static string[] Lex(string text, SqlDialectRules rules)
    {
        var result = SqlLexer.Lex(text, rules);
        result.Error.ShouldBeNull();
        return Describe(result, text);
    }

    private static string[] Describe(SqlLexResult result, string text) =>
        [.. result.Lexemes.Select(lexeme => Describe(lexeme, text))];

    private static string Describe(SqlLexeme lexeme, string text) =>
        $"{lexeme.Kind}:{text.Substring(lexeme.Span.Start, lexeme.Span.Length)}";

    private static string[] ReadLeadingComments(SqlLexer lexer, string text)
    {
        var comments = new System.Collections.Generic.List<string>();
        while (lexer.TryReadLeadingComment(out var comment))
        {
            comments.Add(Describe(comment, text));
        }

        return [.. comments];
    }
}
````

`Lex_Construct_IsReadByTheRulesOfTheDialect` is the spec's matrix.  Every construct has rows that between them list all seven dialects, which is Review Focus 3: a character that only one database gives meaning to is plain text in the others.

Patch `tests/SqlSource.Tests/Parsing/SqlMarkerReaderTests.cs`: the calls pass rules, and a `#` comment, MySQL's `--` without whitespace and Oracle's line hint are not markers.

<!-- patch:task3-marker-reader-tests -->
````diff
diff --git a/tests/SqlSource.Tests/Parsing/SqlMarkerReaderTests.cs b/tests/SqlSource.Tests/Parsing/SqlMarkerReaderTests.cs
index 1d2e91e..d248f11 100644
--- a/tests/SqlSource.Tests/Parsing/SqlMarkerReaderTests.cs
+++ b/tests/SqlSource.Tests/Parsing/SqlMarkerReaderTests.cs
@@ -52,11 +52,33 @@ public class SqlMarkerReaderTests
     [InlineData("$$\n-- name: A\n$$")]
     public void Read_AnythingElse_IsNotAMarker(string text) => Markers(text).ShouldBeEmpty();
 
+    // A # comment is a line comment in MySQL and MariaDB.  A marker starts with two dashes, there as everywhere.
+    [Theory]
+    [InlineData("# name: A")]
+    [InlineData("#-name: A")]
+    [InlineData("## summary: x")]
+    [InlineData("#  SqlSource: keep-comments")]
+    public void Read_HashComment_IsNotAMarker(string text) => Markers(text, SqlDialectRules.MySql).ShouldBeEmpty();
+
+    // MySQL and MariaDB read two dashes as a comment only when whitespace follows them.
+    [Fact]
+    public void Read_TwoDashesWithoutWhitespaceInMySql_IsNotAMarker()
+    {
+        Markers("--name: A\nSELECT 1", SqlDialectRules.MySql).ShouldBeEmpty();
+        Markers("-- name: A\nSELECT 1", SqlDialectRules.MySql).ShouldBe(["Name:A"]);
+        Markers("--\tname: A\nSELECT 1", SqlDialectRules.MariaDb).ShouldBe(["Name:A"]);
+    }
+
+    // Oracle's line hint is kept in the SQL, so it is never a marker.
+    [Fact]
+    public void Read_LineHintOfOracle_IsNotAMarker() =>
+        Markers("--+ name: A\nSELECT 1", SqlDialectRules.Oracle).ShouldBeEmpty();
+
     [Fact]
     public void Read_Marker_ReportsTheCommentAndTheTrimmedValue()
     {
         const string Text = "SELECT 1\n  -- name:  GetUser  \nSELECT 2";
-        var lexeme = SqlLexer.Lex(Text).Lexemes[1];
+        var lexeme = SqlLexer.Lex(Text, SqlDialectRules.Ansi).Lexemes[1];
 
         var marker = SqlMarkerReader.Read(Text, lexeme);
 
@@ -70,15 +92,17 @@ public class SqlMarkerReaderTests
     {
         const string Text = "-- name:  ";
 
-        var marker = SqlMarkerReader.Read(Text, SqlLexer.Lex(Text).Lexemes[0]);
+        var marker = SqlMarkerReader.Read(Text, SqlLexer.Lex(Text, SqlDialectRules.Ansi).Lexemes[0]);
 
         _ = marker.ShouldNotBeNull();
         marker.Value.ValueSpan.IsEmpty.ShouldBeTrue();
     }
 
-    private static string[] Markers(string text)
+    private static string[] Markers(string text) => Markers(text, SqlDialectRules.Ansi);
+
+    private static string[] Markers(string text, SqlDialectRules rules)
     {
-        var lexed = SqlLexer.Lex(text);
+        var lexed = SqlLexer.Lex(text, rules);
         lexed.Error.ShouldBeNull();
         return
         [
````

Patch `tests/SqlSource.Tests/Parsing/SqlTextBuilderTests.cs`: a `#` comment is stripped like a line comment, and the two new hints are copied as written.

<!-- patch:task3-text-builder-tests -->
````diff
diff --git a/tests/SqlSource.Tests/Parsing/SqlTextBuilderTests.cs b/tests/SqlSource.Tests/Parsing/SqlTextBuilderTests.cs
index 334b264..793b90b 100644
--- a/tests/SqlSource.Tests/Parsing/SqlTextBuilderTests.cs
+++ b/tests/SqlSource.Tests/Parsing/SqlTextBuilderTests.cs
@@ -100,7 +100,7 @@ public class SqlTextBuilderTests
     public void Build_LexemeRange_UsesOnlyThatRange()
     {
         const string Text = "A\n-- name: X\nB\n";
-        var lexemes = SqlLexer.Lex(Text).Lexemes;
+        var lexemes = SqlLexer.Lex(Text, SqlDialectRules.Ansi).Lexemes;
 
         SqlTextBuilder.Build(Text, lexemes, 0, 1, keepComments: false).Text.ShouldBe("A");
         SqlTextBuilder.Build(Text, lexemes, 2, 3, keepComments: false).Text.ShouldBe("B");
@@ -112,7 +112,7 @@ public class SqlTextBuilderTests
     public void ToSourceSpan_SpanInBuiltText_MapsToTheSameTextInTheFile(bool keepComments)
     {
         const string Text = "-- c\r\n/* x */ SELECT {{a}} -- d\r\n\r\n-- summary: s\r\n  FROM {{b}}";
-        var lexemes = SqlLexer.Lex(Text).Lexemes;
+        var lexemes = SqlLexer.Lex(Text, SqlDialectRules.Ansi).Lexemes;
 
         var built = SqlTextBuilder.Build(Text, lexemes, 0, lexemes.Count, keepComments);
 
@@ -135,7 +135,7 @@ public class SqlTextBuilderTests
             "  -- lead\r\n\r\n-- summary: s\r\nSELECT 'a  \r\n\r\n b', /* c */ x   \r\n"
             + "\t-- SqlSource: token-ignore=q\r\n"
             + "\r\n  /*+ h\r\n  i */ FROM t -- d  \r\n   \r\nWHERE {{y}} = $$ z\n $$  \r\n";
-        var lexemes = SqlLexer.Lex(Text).Lexemes;
+        var lexemes = SqlLexer.Lex(Text, SqlDialectRules.Ansi).Lexemes;
 
         var built = SqlTextBuilder.Build(Text, lexemes, 0, lexemes.Count, keepComments);
 
@@ -153,9 +153,25 @@ public class SqlTextBuilderTests
         }
     }
 
-    private static string Build(string text, bool keepComments = false)
+    [Theory]
+    [InlineData("SELECT 1 # c\n# d\nFROM t # e", false, "SELECT 1\nFROM t")]
+    [InlineData("SELECT 1 # c\n# d\nFROM t # e", true, "SELECT 1 # c\n# d\nFROM t # e")]
+    public void Build_HashCommentOfMySql_IsTreatedAsALineComment(string text, bool keepComments, string expected) =>
+        Build(text, keepComments, SqlDialectRules.MySql).ShouldBe(expected);
+
+    [Fact]
+    public void Build_LineHintOfOracle_IsCopiedAsWritten() =>
+        Build("SELECT --+ FULL(e)  \n  1 -- c\nFROM e", rules: SqlDialectRules.Oracle)
+            .ShouldBe("SELECT --+ FULL(e)  \n  1\nFROM e");
+
+    [Fact]
+    public void Build_HintOfMariaDb_IsCopiedAsWritten() =>
+        Build("SELECT /*M! SQL_NO_CACHE */ 1 /* c */", rules: SqlDialectRules.MariaDb)
+            .ShouldBe("SELECT /*M! SQL_NO_CACHE */ 1");
+
+    private static string Build(string text, bool keepComments = false, SqlDialectRules? rules = null)
     {
-        var lexed = SqlLexer.Lex(text);
+        var lexed = SqlLexer.Lex(text, rules ?? SqlDialectRules.Ansi);
         lexed.Error.ShouldBeNull();
         return SqlTextBuilder.Build(text, lexed.Lexemes, 0, lexed.Lexemes.Count, keepComments).Text;
     }
````

Patch `tests/SqlSource.Tests/Parsing/SqlDirectiveScopeTests.cs`: its one call to the lexer passes rules.

<!-- patch:task3-directive-scope-tests -->
````diff
diff --git a/tests/SqlSource.Tests/Parsing/SqlDirectiveScopeTests.cs b/tests/SqlSource.Tests/Parsing/SqlDirectiveScopeTests.cs
index 0fb131c..35ae8c3 100644
--- a/tests/SqlSource.Tests/Parsing/SqlDirectiveScopeTests.cs
+++ b/tests/SqlSource.Tests/Parsing/SqlDirectiveScopeTests.cs
@@ -189,7 +189,7 @@ public class SqlDirectiveScopeTests
         var errors = new List<SqlParseError>();
         foreach (var line in lines)
         {
-            var marker = SqlMarkerReader.Read(line, SqlLexer.Lex(line).Lexemes[0]);
+            var marker = SqlMarkerReader.Read(line, SqlLexer.Lex(line, SqlDialectRules.Ansi).Lexemes[0]);
             _ = marker.ShouldNotBeNull();
             scope.Read(line, marker.Value, errors);
         }
````

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class '*SqlLexerTests'`
Expected: the build fails.  CS0103 and CS0246 name `SqlDialectRules`; CS1501 says no overload of `Lex` takes two arguments.

- [ ] **Step 3: Write the rules**

Create `src/SqlSource/Parsing/SqlDialectRules.cs`:

<!-- file:src/SqlSource/Parsing/SqlDialectRules.cs -->
````csharp
using SqlSource.Parsing.Quoting;

namespace SqlSource.Parsing;

/// <summary>
/// How one dialect reads comments and quoted regions.  The lexer reads by the rules it is given and names no dialect.
/// </summary>
/// <remarks>
/// Every dialect-sensitive choice is here or in a <see cref="QuoteReader" />.  The static properties are the whole
/// table: a value where dialects differ by a setting, a reader where they differ by how a quoted region ends.
/// Changing a cell changes the SQL users get.
/// </remarks>
internal sealed class SqlDialectRules
{
    private const int TableSize = 128;

    // The first characters of --, # and /*.
    private const string CommentStarters = "-#/";

    private static readonly QuoteReader Doubled = new DoubledQuoteReader();

    private static readonly QuoteReader Backslash = new BackslashQuoteReader();

    private static readonly QuoteReader EscapeString = new EscapeStringReader(continues: false);

    private static readonly QuoteReader ContinuedEscapeString = new EscapeStringReader(continues: true);

    private static readonly QuoteReader QuoteOperator = new QuoteOperatorReader();

    private static readonly QuoteReader Bracket = new BracketReader(doubledCloserEscapes: false);

    private static readonly QuoteReader EscapedBracket = new BracketReader(doubledCloserEscapes: true);

    private static readonly QuoteReader Dollar = new DollarQuoteReader();

    private readonly QuoteReader?[] _readers = new QuoteReader?[TableSize];

    // Every character that can start something other than plain text: a comment in any dialect, or a quoted region
    // in this one.
    private readonly char[] _starters;

    private SqlDialectRules(params (char Opener, QuoteReader Reader)[] readers)
    {
        _starters = new char[CommentStarters.Length + readers.Length];
        CommentStarters.CopyTo(0, _starters, 0, CommentStarters.Length);
        for (var index = 0; index < readers.Length; index++)
        {
            var (opener, reader) = readers[index];
            _readers[opener] = reader;
            _starters[CommentStarters.Length + index] = opener;
        }
    }

    /// <summary>Today's rules for every database, unchanged by the dialect setting.</summary>
    public static SqlDialectRules Ansi { get; } =
        new(('\'', EscapeString), ('"', Doubled), ('`', Doubled), ('$', Dollar)) { NestedComments = true };

    public static SqlDialectRules SqlServer { get; } =
        new(('\'', Doubled), ('"', Doubled), ('[', EscapedBracket)) { NestedComments = true };

    public static SqlDialectRules PostgreSql { get; } =
        new(('\'', ContinuedEscapeString), ('"', Doubled), ('$', Dollar)) { NestedComments = true };

    public static SqlDialectRules MySql { get; } =
        new(('\'', Backslash), ('"', Backslash), ('`', Doubled), ('$', Dollar))
        {
            DashNeedsWhitespace = true,
            HashComments = true,
        };

    public static SqlDialectRules MariaDb { get; } =
        new(('\'', Backslash), ('"', Backslash), ('`', Doubled))
        {
            DashNeedsWhitespace = true,
            HashComments = true,
            MariaDbHints = true,
        };

    public static SqlDialectRules Sqlite { get; } =
        new(('\'', Doubled), ('"', Doubled), ('`', Doubled), ('[', Bracket));

    public static SqlDialectRules Oracle { get; } = new(('\'', QuoteOperator), ('"', Doubled)) { LineHints = true };

    /// <summary>Whether a <c>/*</c> inside a block comment opens a comment that needs its own <c>*/</c>.</summary>
    public bool NestedComments { get; private init; }

    /// <summary>
    /// Whether <c>--</c> starts a comment only when whitespace, a control character or the end of the text follows
    /// it, as in MySQL, where <c>5--3</c> is arithmetic.
    /// </summary>
    public bool DashNeedsWhitespace { get; private init; }

    /// <summary>Whether <c>#</c> starts a comment that runs to the end of its line.</summary>
    public bool HashComments { get; private init; }

    /// <summary>Whether <c>--+</c> starts a hint that runs to the end of its line, as in Oracle.</summary>
    public bool LineHints { get; private init; }

    /// <summary>Whether a block comment that starts <c>/*M!</c> is a hint, as in MariaDB.</summary>
    public bool MariaDbHints { get; private init; }

    /// <summary>The rules of <paramref name="dialect" />.  One shared instance for each dialect.</summary>
    public static SqlDialectRules For(SqlDialect dialect) =>
        dialect switch
        {
            SqlDialect.Ansi => Ansi,
            SqlDialect.SqlServer => SqlServer,
            SqlDialect.PostgreSql => PostgreSql,
            SqlDialect.MySql => MySql,
            SqlDialect.MariaDb => MariaDb,
            SqlDialect.Sqlite => Sqlite,
            SqlDialect.Oracle => Oracle,
            _ => Ansi,
        };

    /// <summary>
    /// The offset of the first character from <paramref name="start" /> on that can start a comment or a quoted
    /// region, or -1 when the rest of the text is plain.  Most of a SQL file is plain text, and this skips it in one
    /// search.
    /// </summary>
    public int FindStarter(string text, int start) => text.IndexOfAny(_starters, start);

    /// <summary>
    /// The reader of the quoted region that <paramref name="opener" /> opens, or null when it opens none.
    /// </summary>
    public QuoteReader? ReaderFor(char opener) => opener < TableSize ? _readers[opener] : null;
}
````

The reader fields come before the dialect properties on purpose: static initialisers run in the order they are written, and a property that ran first would capture null readers.

- [ ] **Step 4: Rewrite the lexer**

Replace `src/SqlSource/Parsing/SqlLexer.cs` with:

<!-- file:src/SqlSource/Parsing/SqlLexer.cs -->
````csharp
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Parsing.Quoting;

namespace SqlSource.Parsing;

/// <summary>
/// Splits SQL text into quoted regions, comments, hints and plain text.
/// </summary>
/// <remarks>
/// The lexer reads by the <see cref="SqlDialectRules" /> it holds and names no dialect and no quoting form.  It knows
/// nothing about markers.  Where the rules leave a choice it keeps text, because a comment left in the SQL is
/// harmless and SQL removed from it is a bug.
/// </remarks>
internal sealed class SqlLexer(string text, SqlDialectRules rules)
{
    private static readonly char[] LineTerminators = ['\r', '\n'];

    private readonly ImmutableArray<SqlLexeme>.Builder _lexemes = ImmutableArray.CreateBuilder<SqlLexeme>();
    private SqlParseError? _error;
    private int _textStart;

    /// <summary>
    /// The rules for the text that has not been read yet.  They may be changed between two lexemes, which is how a
    /// file's <c>dialect=</c> directive takes effect from the line after it.
    /// </summary>
    public SqlDialectRules Rules { get; set; } = rules;

    /// <summary>The offset after the last lexeme that was read.</summary>
    public int Position { get; private set; }

    /// <summary>Lexes a text that has one dialect.</summary>
    public static SqlLexResult Lex(string text, SqlDialectRules rules) => new SqlLexer(text, rules).ReadToEnd();

    /// <summary>
    /// Reads the next comment, if only whitespace comes before it.  Returns false, and reads nothing, when the next
    /// thing in the text is anything else, a hint included, and at the end of the text.
    /// </summary>
    public bool TryReadLeadingComment(out SqlLexeme comment)
    {
        comment = default;
        var index = Position;
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        if (_error is not null || index >= text.Length || !IsComment(index))
        {
            return false;
        }

        Position = index;
        Step();
        if (_error is not null)
        {
            return false;
        }

        comment = _lexemes[_lexemes.Count - 1];
        return true;
    }

    /// <summary>Reads the rest of the text and returns every lexeme, or the error that stopped the lexer.</summary>
    public SqlLexResult ReadToEnd()
    {
        while (Position < text.Length && _error is null)
        {
            var starter = Rules.FindStarter(text, Position);
            Position = starter < 0 ? text.Length : starter;
            if (starter >= 0)
            {
                Step();
            }
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
        var start = Position;
        var current = text[start];
        if (current == '-' && IsDashComment(start))
        {
            Add(IsLineHint(start) ? SqlLexemeKind.Hint : SqlLexemeKind.LineComment, start, FindLineEnd(start));
        }
        else if (current == '#' && Rules.HashComments)
        {
            Add(SqlLexemeKind.LineComment, start, FindLineEnd(start));
        }
        else if (current == '/' && CharAt(start + 1) == '*')
        {
            ReadBlockComment(start);
        }
        else if (Rules.ReaderFor(current) is { } reader)
        {
            ReadQuoted(reader, start);
        }
        else
        {
            Position++;
        }
    }

    // A comment that is stripped: not a hint, which is kept and can hold SQL.
    private bool IsComment(int index) =>
        text[index] switch
        {
            '-' => IsDashComment(index) && !IsLineHint(index),
            '#' => Rules.HashComments,
            '/' => CharAt(index + 1) == '*' && !IsBlockHint(index),
            _ => false,
        };

    private bool IsDashComment(int index) =>
        CharAt(index + 1) == '-' && (!Rules.DashNeedsWhitespace || IsDashBoundary(CharAt(index + 2)));

    // What MySQL wants after the two dashes.  The end of the text is '\0' here, which is a control character.
    private static bool IsDashBoundary(char value) => char.IsWhiteSpace(value) || char.IsControl(value);

    private bool IsLineHint(int index) => Rules.LineHints && CharAt(index + 2) == '+';

    private bool IsBlockHint(int index) =>
        CharAt(index + 2) is '+' or '!' || (Rules.MariaDbHints && CharAt(index + 2) == 'M' && CharAt(index + 3) == '!');

    private char CharAt(int index) => index < text.Length ? text[index] : '\0';

    private void Add(SqlLexemeKind kind, int start, int end)
    {
        AddPendingText(start);
        _lexemes.Add(new SqlLexeme(kind, TextSpan.FromBounds(start, end)));
        _textStart = end;
        Position = end;
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
            if (Rules.NestedComments && text[index] == '/' && CharAt(index + 1) == '*')
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

        Add(IsBlockHint(start) ? SqlLexemeKind.Hint : SqlLexemeKind.BlockComment, start, index);
    }

    private void ReadQuoted(QuoteReader reader, int start)
    {
        var end = reader.FindEnd(text, start);
        if (end == QuoteReader.NotAQuote)
        {
            Position++;
        }
        else if (end == QuoteReader.Unterminated)
        {
            _error = SqlParseError.Create(SqlParseErrorKind.UnterminatedQuote, new TextSpan(start, 1));
        }
        else
        {
            Add(SqlLexemeKind.Quoted, start, end);
        }
    }
}
````

What changed from the lexer it replaces:

- It is an object, not a static class with a nested `Scanner`, so that it can be read in two stages.
- `Step` asks the rules: `IsDashComment` for the whitespace that MySQL wants after `--`, `Rules.HashComments` for `#`, `Rules.ReaderFor` for a quoted region.  `ReadQuoted`, `IsEscapeStringPrefix` and `TryFindDollarQuoteEnd` are gone; the readers of Task 2 do that work.
- `ReadBlockComment` counts depth only when `Rules.NestedComments` is true.
- A hint is `/*+`, `/*!`, `/*M!` where `Rules.MariaDbHints`, and a line that starts `--+` where `Rules.LineHints`.
- `ReadToEnd` jumps to the next character that can start something, with `Rules.FindStarter`, and does not look at the plain text between.
- `TryReadLeadingComment` reads one comment if only whitespace is before it.  A hint is not a comment for it: a hint is kept in the SQL and can hold SQL.

- [ ] **Step 5: Update the lexeme kinds, the marker reader and the parser's call**

Replace `src/SqlSource/Parsing/SqlLexemeKind.cs` with:

<!-- file:src/SqlSource/Parsing/SqlLexemeKind.cs -->
````csharp
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

    /// <summary>
    /// <c>--</c> to the end of the line, without the line terminator.  Where the dialect has them, <c>#</c> to the end
    /// of the line too.
    /// </summary>
    LineComment,

    /// <summary><c>/* ... */</c>, with any comments nested in it where the dialect nests them.</summary>
    BlockComment,

    /// <summary>
    /// A block comment that starts <c>/*+</c> or <c>/*!</c>, and where the dialect has them one that starts
    /// <c>/*M!</c> or a line that starts <c>--+</c>.  Never stripped.
    /// </summary>
    Hint,
}
````

Patch `src/SqlSource/Parsing/SqlMarkerReader.cs`.  A `#` comment is a `LineComment` lexeme now, and the reader skips two characters before it looks for a keyword, so without this check `# name: A` would be a marker.

<!-- patch:task3-marker-reader -->
````diff
diff --git a/src/SqlSource/Parsing/SqlMarkerReader.cs b/src/SqlSource/Parsing/SqlMarkerReader.cs
index faf77eb..574682f 100644
--- a/src/SqlSource/Parsing/SqlMarkerReader.cs
+++ b/src/SqlSource/Parsing/SqlMarkerReader.cs
@@ -21,7 +21,12 @@ internal static class SqlMarkerReader
     /// </summary>
     public static SqlMarker? Read(string text, SqlLexeme lexeme)
     {
-        if (lexeme.Kind != SqlLexemeKind.LineComment || !StartsLine(text, lexeme.Span.Start))
+        // A line comment can start with a # in some dialects.  A marker always starts with two dashes.
+        if (
+            lexeme.Kind != SqlLexemeKind.LineComment
+            || text[lexeme.Span.Start] != '-'
+            || !StartsLine(text, lexeme.Span.Start)
+        )
         {
             return null;
         }
````

Patch `src/SqlSource/Parsing/SqlFileParser.cs`.  The parser reads as ANSI until Task 4 gives it a dialect.

<!-- patch:task3-parser -->
````diff
diff --git a/src/SqlSource/Parsing/SqlFileParser.cs b/src/SqlSource/Parsing/SqlFileParser.cs
index 196d270..00535a7 100644
--- a/src/SqlSource/Parsing/SqlFileParser.cs
+++ b/src/SqlSource/Parsing/SqlFileParser.cs
@@ -19,7 +19,7 @@ internal static class SqlFileParser
     /// </summary>
     public static SqlFileParseResult Parse(string text, string fileName)
     {
-        var lexed = SqlLexer.Lex(text);
+        var lexed = SqlLexer.Lex(text, SqlDialectRules.Ansi);
         return lexed.Error is null
             ? new Parser(text, fileName, lexed.Lexemes).Run()
             : new SqlFileParseResult(
````

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class '*SqlLexerTests' --filter-class '*SqlDialectRulesTests' --filter-class '*SqlMarkerReaderTests' --filter-class '*SqlTextBuilderTests'`
Expected: total 276, failed 0.

Run: `dotnet test --solution SqlSource.slnx`
Expected: total 1112, failed 0.  Every test that existed before this task passes unchanged: the default dialect reads as the lexer read before.

- [ ] **Step 7: Format and validate**

Run: `./format.sh`

Run: `./pre-commit-validation.sh`
Expected: every step `passed`.

- [ ] **Step 8: Commit**

```bash
git add src/SqlSource/Parsing tests/SqlSource.Tests/Parsing
```

```bash
git commit -m "Lex by the rules of a dialect

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: The `dialect=` directive

A file names its own dialect in its header: the comments before its first SQL and before its first `-- name:` marker.  The directive has to be found before the text after it is lexed, because it changes how that text is read.

**Files:**
- Create: `src/SqlSource/Parsing/SqlPreambleDialect.cs`
- Modify: `src/SqlSource/Parsing/SqlDirectiveScope.cs`, `SqlParseErrorKind.cs`, `SqlFileParser.cs`
- Modify: `src/SqlSource/Diagnostics/SqlDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/diagnostics.md`
- Test: `tests/SqlSource.Tests/Parsing/SqlPreambleDialectTests.cs` (new), `SqlDirectiveScopeTests.cs`, `SqlFileParserTests.cs`, `SqlFileParserAllocationTests.cs`, `tests/SqlSource.Tests/Generator/FileDiagnosticsTests.cs`

**Interfaces:**
- Consumes: `SqlDialect`, `SqlDialectName.TryParse` (Task 1); `SqlDialectRules.For`, `SqlLexer.TryReadLeadingComment`, `SqlLexer.Rules`, `SqlLexer.Position`, `SqlLexer.ReadToEnd` (Task 3).
- Produces, `internal`:
  - `static int SqlPreambleDialect.Apply(SqlLexer lexer, string text)`: reads the leading comments, switches `lexer.Rules` at the first `dialect=` directive that names a dialect, and returns the offset where the header ends.
  - `SqlDirectiveScope(int headerEnd)`: the constructor takes the header's end.  `SqlDialect? Dialect { get; }`.  `static bool TryFindDialect(string text, SqlMarker marker, out SqlDialect dialect)`.
  - `SqlParseErrorKind.MisplacedDialect`, added at the end of the enum.
  - `SqlFileParser.Parse(string text, string fileName, SqlDialect dialect = SqlDialect.Ansi)`.
  - `SqlDiagnostics.MisplacedDialect`, `SQLSRC115`.

How the pieces meet:

1. `SqlFileParser.Parse` creates a lexer with the rules of the dialect it is given.
2. `SqlPreambleDialect.Apply` reads leading comments from it until none is left or one is a `-- name:` marker.  The first `-- SqlSource:` marker with a valid `dialect=` switches the lexer's rules.  It returns the header's end: the start of the `-- name:` marker, or else the lexer's position.
3. The parser calls `ReadToEnd`, and runs its existing pass over the lexemes.
4. That pass reads every `-- SqlSource:` marker through a `SqlDirectiveScope`, which now knows the header's end.  A `dialect=` directive that starts at or after it is `MisplacedDialect`; a name that is not a dialect is `InvalidDirectiveValue`; a second, different dialect is `ConflictingDirectives`.

So the directive is applied in one place and reported in another.  Both read it through `SqlDirectiveScope.TryFindDialect` or `SqlDialectName.TryParse`, so they cannot disagree about what a valid one is.

- [ ] **Step 1: Write the failing tests**

Create `tests/SqlSource.Tests/Parsing/SqlPreambleDialectTests.cs`:

<!-- file:tests/SqlSource.Tests/Parsing/SqlPreambleDialectTests.cs -->
````csharp
using System;
using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class SqlPreambleDialectTests
{
    [Theory]
    [InlineData("-- SqlSource: dialect=mysql\n-- name: A\nSELECT 1")]
    [InlineData("-- SqlSource: DIALECT=MySql\n-- name: A\nSELECT 1")]
    [InlineData("-- SqlSource: keep-comments dialect=mysql token-ignore=a\n-- name: A\nSELECT 1")]
    [InlineData("\n\n  -- SqlSource: dialect=mysql\nSELECT 1")]
    [InlineData("-- a comment\n-- SqlSource: keep-comments\n-- SqlSource: dialect=mysql\nSELECT 1")]
    [InlineData("/* Copyright\n   (c) Example */\n-- SqlSource: dialect=mysql\n-- name: A\nSELECT 1")]
    public void Apply_DirectiveInTheHeader_SwitchesTheLexer(string text)
    {
        var lexer = new SqlLexer(text, SqlDialectRules.Ansi);

        _ = SqlPreambleDialect.Apply(lexer, text);

        lexer.Rules.ShouldBeSameAs(SqlDialectRules.MySql);
    }

    [Theory]
    // No directive.
    [InlineData("SELECT 1")]
    [InlineData("-- name: A\nSELECT 1")]
    [InlineData("")]
    // After SQL.
    [InlineData("SELECT 1;\n-- SqlSource: dialect=mysql\n")]
    [InlineData("-- a\nSELECT 1; -- SqlSource: dialect=mysql")]
    // After a hint, which is kept in the SQL.
    [InlineData("/*+ h */\n-- SqlSource: dialect=mysql\nSELECT 1")]
    // Inside a named query.
    [InlineData("-- name: A\n-- SqlSource: dialect=mysql\nSELECT 1")]
    // Not a dialect.
    [InlineData("-- SqlSource: dialect=pgsql\nSELECT 1")]
    [InlineData("-- SqlSource: dialect=\nSELECT 1")]
    [InlineData("-- SqlSource: dialect\nSELECT 1")]
    [InlineData("-- SqlSource: xdialect=mysql dialects=mysql\nSELECT 1")]
    // Not a marker.
    [InlineData("-- dialect=mysql\nSELECT 1")]
    [InlineData("/* -- SqlSource: dialect=mysql */\nSELECT 1")]
    public void Apply_NoValidDirectiveInTheHeader_LeavesTheLexerAsItWas(string text)
    {
        var lexer = new SqlLexer(text, SqlDialectRules.Ansi);

        _ = SqlPreambleDialect.Apply(lexer, text);

        lexer.Rules.ShouldBeSameAs(SqlDialectRules.Ansi);
    }

    [Fact]
    public void Apply_SeveralDirectives_TakesTheFirstThatNamesADialect()
    {
        const string Text =
            "-- SqlSource: dialect=nope dialect=oracle\n-- SqlSource: dialect=mysql\n-- name: A\nSELECT 1";
        var lexer = new SqlLexer(Text, SqlDialectRules.Ansi);

        _ = SqlPreambleDialect.Apply(lexer, Text);

        lexer.Rules.ShouldBeSameAs(SqlDialectRules.Oracle);
    }

    // The header is read under the dialect the lexer starts with, up to the directive.
    [Fact]
    public void Apply_CommentFormOfTheStartingDialectAboveTheDirective_IsRead()
    {
        const string Text = "# licence\n-- SqlSource: dialect=postgres\nSELECT 1";
        var underMySql = new SqlLexer(Text, SqlDialectRules.MySql);
        var underAnsi = new SqlLexer(Text, SqlDialectRules.Ansi);

        _ = SqlPreambleDialect.Apply(underMySql, Text);
        var headerEnd = SqlPreambleDialect.Apply(underAnsi, Text);

        underMySql.Rules.ShouldBeSameAs(SqlDialectRules.PostgreSql);

        // To ANSI the first line is SQL, so the header is empty and the directive is not in it.
        underAnsi.Rules.ShouldBeSameAs(SqlDialectRules.Ansi);
        headerEnd.ShouldBe(0);
    }

    // The directive applies from the line after it: a comment form of the new dialect is read there.
    [Fact]
    public void Apply_CommentFormOfTheNewDialectBelowTheDirective_IsPartOfTheHeader()
    {
        const string Text = "-- SqlSource: dialect=mysql\n# c\nSELECT 1";
        var lexer = new SqlLexer(Text, SqlDialectRules.Ansi);

        var headerEnd = SqlPreambleDialect.Apply(lexer, Text);

        headerEnd.ShouldBe(Text.IndexOf("\nSELECT", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("-- a\n-- name: A\nSELECT 1", "-- name: A")]
    [InlineData("-- SqlSource: dialect=mysql\n\n  -- name: A\nSELECT 1", "-- name: A")]
    [InlineData("-- name: A\n-- name: B\nSELECT 1", "-- name: A")]
    public void Apply_FileWithANameMarker_EndsTheHeaderAtTheMarker(string text, string marker)
    {
        var lexer = new SqlLexer(text, SqlDialectRules.Ansi);

        SqlPreambleDialect.Apply(lexer, text).ShouldBe(text.IndexOf(marker, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("SELECT 1", 0)]
    [InlineData("  \n SELECT 1", 0)]
    [InlineData("", 0)]
    [InlineData("-- a\nSELECT 1", 4)]
    [InlineData("-- a\n/* b */ SELECT 1", 12)]
    [InlineData("-- a\n-- b", 9)]
    [InlineData("-- a\n/*+ h */ SELECT 1", 4)]
    public void Apply_FileWithoutANameMarker_EndsTheHeaderAfterItsLastLeadingComment(string text, int expected)
    {
        var lexer = new SqlLexer(text, SqlDialectRules.Ansi);

        SqlPreambleDialect.Apply(lexer, text).ShouldBe(expected);
    }

    [Fact]
    public void Apply_ThenReadToEnd_GivesEveryLexemeOfTheFile()
    {
        const string Text = "-- SqlSource: dialect=mysql\n-- name: A\nSELECT 'a\\'b' # c\n";
        var lexer = new SqlLexer(Text, SqlDialectRules.Ansi);

        _ = SqlPreambleDialect.Apply(lexer, Text);
        var result = lexer.ReadToEnd();

        result.Error.ShouldBeNull();
        result.Lexemes[0].Span.Start.ShouldBe(0);
        result.Lexemes[^1].Span.End.ShouldBe(Text.Length);
        result.Lexemes.Count(lexeme => lexeme.Kind == SqlLexemeKind.LineComment).ShouldBe(3);
        result.Lexemes.Count(lexeme => lexeme.Kind == SqlLexemeKind.Quoted).ShouldBe(1);
    }
}
````

Replace `tests/SqlSource.Tests/Parsing/SqlDirectiveScopeTests.cs` with the file below.  The tests it had are unchanged.  New: the `dialect` directive, a scope's header end, and `TryFindDialect`.  The `Read` helper lexes each line alone, so a directive is at the offset it has in its own line, and the header's end is given in the same terms.

<!-- file:tests/SqlSource.Tests/Parsing/SqlDirectiveScopeTests.cs -->
````csharp
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
        var scope = new SqlDirectiveScope(headerEnd: 0);

        scope.KeepComments.ShouldBeFalse();
        scope.TokenValidation.ShouldBeNull();
        scope.Dialect.ShouldBeNull();
        scope.IgnoredTokens.ShouldBeEmpty();
    }

    [Fact]
    public void Read_KeepComments_SetsTheFlag()
    {
        var (scope, errors) = Read("-- SqlSource: keep-comments");

        errors.ShouldBeEmpty();
        scope.KeepComments.ShouldBeTrue();
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
        var (scope, errors) = Read("-- SqlSource: KEEP-COMMENTS No-Token-Validation Token-Ignore=a");

        errors.ShouldBeEmpty();
        scope.KeepComments.ShouldBeTrue();
        scope.TokenValidation.ShouldBe(false);
        scope.IgnoredTokens.ShouldBe(["a"]);
    }

    [Fact]
    public void Read_SeveralDirectivesOnOneLine_AppliesEach()
    {
        var (scope, errors) = Read("-- SqlSource: keep-comments   token-ignore=a\ttoken-ignore=b");

        errors.ShouldBeEmpty();
        scope.KeepComments.ShouldBeTrue();
        scope.IgnoredTokens.ShouldBe(["a", "b"], ignoreOrder: true);
    }

    [Fact]
    public void Read_SeveralMarkers_Accumulate()
    {
        var (scope, errors) = Read(
            "-- SqlSource: keep-comments",
            "-- SqlSource: token-validation",
            "-- SqlSource: token-ignore=a"
        );

        errors.ShouldBeEmpty();
        scope.KeepComments.ShouldBeTrue();
        scope.TokenValidation.ShouldBe(true);
        scope.IgnoredTokens.ShouldBe(["a"]);
    }

    [Fact]
    public void Read_RepeatedDirective_IsAllowed()
    {
        var (scope, errors) = Read(
            "-- SqlSource: keep-comments keep-comments token-validation token-ignore=a",
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
    [InlineData("keep-comment")]
    [InlineData("preserve-comments")]
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
    [InlineData("keep-comments=x")]
    [InlineData("token-validation=true")]
    [InlineData("no-token-validation=")]
    public void Read_MissingOrUnexpectedValue_IsAnError(string directive)
    {
        var line = "-- SqlSource: " + directive;

        var (scope, errors) = Read(line);

        errors.ShouldBe([
            SqlParseError.Create(SqlParseErrorKind.InvalidDirectiveValue, SpanOf(line, directive), directive),
        ]);
        scope.KeepComments.ShouldBeFalse();
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
        var (scope, errors) = Read("-- SqlSource: bogus keep-comments");

        errors.Count.ShouldBe(1);
        scope.KeepComments.ShouldBeTrue();
    }

    [Theory]
    [InlineData("dialect=mysql", nameof(SqlDialect.MySql))]
    [InlineData("DIALECT=Postgres", nameof(SqlDialect.PostgreSql))]
    [InlineData("Dialect=TSQL", nameof(SqlDialect.SqlServer))]
    [InlineData("dialect=ansi", nameof(SqlDialect.Ansi))]
    public void Read_Dialect_KeepsTheDialectItNames(string directive, string expected)
    {
        var (scope, errors) = Read("-- SqlSource: " + directive);

        errors.ShouldBeEmpty();
        scope.Dialect.ToString().ShouldBe(expected);
    }

    [Theory]
    [InlineData("dialect")]
    [InlineData("dialect=")]
    [InlineData("dialect=pgsql")]
    [InlineData("dialect=mysql,postgres")]
    [InlineData("dialect=mysql=x")]
    public void Read_DialectWithoutAValueOrWithAnUnknownName_IsAnError(string directive)
    {
        var line = "-- SqlSource: " + directive;

        var (scope, errors) = Read(line);

        errors.ShouldBe([
            SqlParseError.Create(SqlParseErrorKind.InvalidDirectiveValue, SpanOf(line, directive), directive),
        ]);
        scope.Dialect.ShouldBeNull();
    }

    [Fact]
    public void Read_SameDialectTwice_IsAllowed()
    {
        var (scope, errors) = Read("-- SqlSource: dialect=mssql dialect=tsql", "-- SqlSource: dialect=SqlServer");

        errors.ShouldBeEmpty();
        scope.Dialect.ShouldBe(SqlDialect.SqlServer);
    }

    [Fact]
    public void Read_TwoDialectsOnOneLine_ReportsTheSecondAndKeepsTheFirst()
    {
        const string Line = "-- SqlSource: dialect=mysql dialect=oracle";

        var (scope, errors) = Read(Line);

        errors.ShouldBe([
            SqlParseError.Create(
                SqlParseErrorKind.ConflictingDirectives,
                SpanOf(Line, "dialect=oracle"),
                "dialect=oracle"
            ),
        ]);
        scope.Dialect.ShouldBe(SqlDialect.MySql);
    }

    [Fact]
    public void Read_TwoDialectsOnSeparateLines_IsAnError()
    {
        var (scope, errors) = Read("-- SqlSource: dialect=mysql", "-- SqlSource: dialect=mariadb");

        errors.ShouldHaveSingleItem().Kind.ShouldBe(SqlParseErrorKind.ConflictingDirectives);
        scope.Dialect.ShouldBe(SqlDialect.MySql);
    }

    // The directive starts at offset 14 of the line.
    [Theory]
    [InlineData(0)]
    [InlineData(14)]
    public void Read_DialectAtOrAfterTheHeaderEnd_IsMisplacedWhateverItNames(int headerEnd)
    {
        var (scope, errors) = Read(headerEnd, "-- SqlSource: dialect=mysql keep-comments", "-- SqlSource: dialect=x");

        errors.ShouldBe([
            SqlParseError.Create(SqlParseErrorKind.MisplacedDialect, new TextSpan(14, 13), "dialect=mysql"),
            SqlParseError.Create(SqlParseErrorKind.MisplacedDialect, new TextSpan(14, 9), "dialect=x"),
        ]);
        scope.Dialect.ShouldBeNull();
        scope.KeepComments.ShouldBeTrue();
    }

    [Fact]
    public void Read_DialectJustBeforeTheHeaderEnd_IsAccepted()
    {
        var (scope, errors) = Read(15, "-- SqlSource: dialect=mysql");

        errors.ShouldBeEmpty();
        scope.Dialect.ShouldBe(SqlDialect.MySql);
    }

    [Theory]
    [InlineData("-- SqlSource: dialect=mysql", nameof(SqlDialect.MySql))]
    [InlineData("-- SqlSource: keep-comments DIALECT=Oracle token-ignore=a", nameof(SqlDialect.Oracle))]
    [InlineData("-- SqlSource: dialect=nope dialect= dialect dialect=sqlite", nameof(SqlDialect.Sqlite))]
    [InlineData("-- SqlSource: dialect=mysql dialect=oracle", nameof(SqlDialect.MySql))]
    public void TryFindDialect_MarkerWithADialect_GivesTheFirstThatIsValid(string line, string expected)
    {
        SqlDirectiveScope.TryFindDialect(line, Marker(line), out var dialect).ShouldBeTrue();

        dialect.ToString().ShouldBe(expected);
    }

    [Theory]
    [InlineData("-- SqlSource: keep-comments")]
    [InlineData("-- SqlSource:")]
    [InlineData("-- SqlSource: dialect")]
    [InlineData("-- SqlSource: dialect=")]
    [InlineData("-- SqlSource: dialect=pgsql")]
    [InlineData("-- SqlSource: xdialect=mysql")]
    [InlineData("-- SqlSource: dialects=mysql")]
    [InlineData("-- SqlSource: dialect:mysql")]
    [InlineData("-- SqlSource: dialect = mysql")]
    public void TryFindDialect_MarkerWithoutAValidDialect_FindsNone(string line)
    {
        SqlDirectiveScope.TryFindDialect(line, Marker(line), out var dialect).ShouldBeFalse();

        dialect.ShouldBe(SqlDialect.Ansi);
    }

    private static (SqlDirectiveScope Scope, List<SqlParseError> Errors) Read(params string[] lines) =>
        Read(int.MaxValue, lines);

    // Each line is lexed alone, so every directive is at the offset it has in its own line.
    private static (SqlDirectiveScope Scope, List<SqlParseError> Errors) Read(int headerEnd, params string[] lines)
    {
        var scope = new SqlDirectiveScope(headerEnd);
        var errors = new List<SqlParseError>();
        foreach (var line in lines)
        {
            scope.Read(line, Marker(line), errors);
        }

        return (scope, errors);
    }

    private static SqlMarker Marker(string line)
    {
        var marker = SqlMarkerReader.Read(line, SqlLexer.Lex(line, SqlDialectRules.Ansi).Lexemes[0]);
        return marker.ShouldNotBeNull();
    }

    private static TextSpan SpanOf(string text, string value) =>
        new(text.LastIndexOf(value, StringComparison.Ordinal), value.Length);
}
````

Patch `tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs`: new tests before the helpers, and `Blocks` takes a dialect.

<!-- patch:task4-parser-tests -->
````diff
diff --git a/tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs b/tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs
index 75dbe0d..5a7fbc7 100644
--- a/tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs
+++ b/tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs
@@ -465,9 +465,126 @@ public class SqlFileParserTests
     public void Parse_DifferentText_GivesUnequalResults() =>
         SqlFileParser.Parse("SELECT 1", "Query.sql").ShouldNotBe(SqlFileParser.Parse("SELECT 2", "Query.sql"));
 
-    private static SqlBlock[] Blocks(string text, string fileName = "Query.sql")
+    // ANSI ends the string at the second quote, takes the rest of the line for a comment, and would keep the #.
+    [Theory]
+    [InlineData(nameof(SqlDialect.Ansi), "SELECT 'a\\'b")]
+    [InlineData(nameof(SqlDialect.MySql), "SELECT 'a\\'b -- c', 2")]
+    public void Parse_Dialect_DecidesHowTheSqlIsRead(string dialect, string expected)
     {
-        var result = SqlFileParser.Parse(text, fileName);
+        const string Text = "-- name: A\nSELECT 'a\\'b -- c', 2 # d\n";
+
+        Sql(Blocks(Text, dialect: Enum.Parse<SqlDialect>(dialect)).ShouldHaveSingleItem()).ShouldBe(expected);
+    }
+
+    [Fact]
+    public void Parse_DialectDirectiveInThePreamble_AppliesToEveryBlockAndIsNotInTheSql()
+    {
+        const string Text =
+            "/* Copyright (c) Example */\n-- SqlSource: dialect=mysql\n\n"
+            + "-- name: A\nSELECT 'a\\'b' # c\n-- name: B\nSELECT 5--3 # d\n";
+
+        Blocks(Text).Select(Sql).ShouldBe(["SELECT 'a\\'b'", "SELECT 5--3"]);
+    }
+
+    [Fact]
+    public void Parse_DialectDirective_ReplacesTheDialectOfTheProject()
+    {
+        const string Text = "-- SqlSource: dialect=mssql\n-- name: A\nSELECT [a'b] -- c\n";
+
+        Sql(Blocks(Text, dialect: SqlDialect.MySql).ShouldHaveSingleItem()).ShouldBe("SELECT [a'b]");
+    }
+
+    [Fact]
+    public void Parse_DialectDirectiveInAFileWithoutANameMarker_GoesAboveItsSql()
+    {
+        const string Text = "-- summary: S\n-- SqlSource: dialect=oracle keep-comments\nSELECT q'[it's]' --+ h\n";
+
+        var block = Blocks(Text).ShouldHaveSingleItem();
+
+        Sql(block).ShouldBe("SELECT q'[it's]' --+ h");
+        block.Summary.ShouldBe("S");
+        block.KeepComments.ShouldBeTrue();
+    }
+
+    // The header is read under the dialect of the project, and the rest of the file under the directive's.
+    [Fact]
+    public void Parse_CommentAboveTheDialectDirective_IsReadUnderTheDialectOfTheProject()
+    {
+        const string Text = "# licence\n-- SqlSource: dialect=postgres\n-- name: A\nSELECT 1 # 2\n";
+
+        Sql(Blocks(Text, dialect: SqlDialect.MySql).ShouldHaveSingleItem()).ShouldBe("SELECT 1 # 2");
+    }
+
+    [Theory]
+    [InlineData("-- name: A\n-- SqlSource: dialect=mysql\nSELECT 1\n")]
+    [InlineData("-- SqlSource: dialect=mysql\n-- name: A\n-- SqlSource: dialect=mysql\nSELECT 1\n")]
+    [InlineData("SELECT 1\n-- SqlSource: dialect=mysql\nFROM t\n")]
+    [InlineData("-- name: A\nSELECT 1\n-- name: B\n-- SqlSource: dialect=nope\nSELECT 2\n")]
+    public void Parse_DialectDirectiveInsideAQueryOrAfterSql_IsMisplaced(string text)
+    {
+        var error = Errors(text).ShouldHaveSingleItem();
+
+        error.Kind.ShouldBe(SqlParseErrorKind.MisplacedDialect);
+        error.Span.Start.ShouldBe(text.LastIndexOf("dialect=", StringComparison.Ordinal));
+    }
+
+    // A misplaced directive does not change how the file is read: the # would be a comment under MySQL.
+    [Fact]
+    public void Parse_MisplacedDialectDirective_IsNotApplied() =>
+        Errors("-- name: A\n-- SqlSource: dialect=mysql\nSELECT 'it''s' # '\n")
+            .ShouldHaveSingleItem()
+            .Kind.ShouldBe(SqlParseErrorKind.UnterminatedQuote);
+
+    [Fact]
+    public void Parse_DialectDirectiveAfterSqlInThePreamble_IsReportedWithTheSql()
+    {
+        const string Text = "SELECT 0;\n-- SqlSource: dialect=mysql\n-- name: A\nSELECT 1\n";
+
+        Errors(Text)
+            .Select(static error => error.Kind)
+            .ShouldBe([SqlParseErrorKind.SqlBeforeFirstName, SqlParseErrorKind.MisplacedDialect]);
+    }
+
+    [Theory]
+    [InlineData("-- SqlSource: dialect=pgsql\n-- name: A\nSELECT 1\n", "dialect=pgsql")]
+    [InlineData("-- SqlSource: dialect\nSELECT 1\n", "dialect")]
+    public void Parse_DialectDirectiveWithoutAValidName_IsAnErrorAtTheDirective(string text, string directive) =>
+        Errors(text)
+            .ShouldBe([
+                SqlParseError.Create(SqlParseErrorKind.InvalidDirectiveValue, SpanOf(text, directive), directive),
+            ]);
+
+    [Fact]
+    public void Parse_TwoDialectsInOneHeader_IsAnErrorAtTheSecondAndTheSameDialectTwiceIsNot()
+    {
+        const string Conflict = "-- SqlSource: dialect=mysql\n-- SqlSource: dialect=oracle\n-- name: A\nSELECT 1\n";
+        const string Repeat = "-- SqlSource: dialect=mysql\n-- SqlSource: dialect=MYSQL\n-- name: A\nSELECT 1 # c\n";
+
+        Errors(Conflict)
+            .ShouldBe([
+                SqlParseError.Create(
+                    SqlParseErrorKind.ConflictingDirectives,
+                    SpanOf(Conflict, "dialect=oracle"),
+                    "dialect=oracle"
+                ),
+            ]);
+        Sql(Blocks(Repeat).ShouldHaveSingleItem()).ShouldBe("SELECT 1");
+    }
+
+    [Fact]
+    public void Parse_SameTextUnderTwoDialects_GivesUnequalResultsOnlyWhereTheyReadItDifferently()
+    {
+        SqlFileParser
+            .Parse("SELECT 1 # c", "Query.sql", SqlDialect.MySql)
+            .ShouldNotBe(SqlFileParser.Parse("SELECT 1 # c", "Query.sql", SqlDialect.Ansi));
+        SqlFileParser
+            .Parse("SELECT 1 -- c", "Query.sql", SqlDialect.MySql)
+            .ShouldBe(SqlFileParser.Parse("SELECT 1 -- c", "Query.sql", SqlDialect.Ansi));
+    }
+
+    private static SqlBlock[] Blocks(string text, string fileName = "Query.sql", SqlDialect dialect = SqlDialect.Ansi)
+    {
+        var result = SqlFileParser.Parse(text, fileName, dialect);
         result.Errors.ShouldBeEmpty();
         return [.. result.Blocks];
     }
````

`Parse_MisplacedDialectDirective_IsNotApplied` is Review Focus 2: a directive in the wrong place must leave the file read as it was.  Under MySQL the last quote of its text would be inside a `#` comment and the only error would be the misplaced directive; under ANSI it opens a string that is never closed.

Replace `tests/SqlSource.Tests/Parsing/SqlFileParserAllocationTests.cs` with the file below.  The budget is the same constant; the test now runs under three dialects, and once for a file that names its own.

<!-- file:tests/SqlSource.Tests/Parsing/SqlFileParserAllocationTests.cs -->
````csharp
using System;
using System.Globalization;
using System.Text;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class SqlFileParserAllocationTests
{
    // The generator parses a file again each time it is edited in the IDE, so what a parse allocates is tracked here.
    // A parse of this file allocated 9.7 bytes for each character of input when the budget was set, the same in Debug,
    // in Release and under coverage.  The budget leaves room for differences between runtimes, not for a regression:
    // lower it when the parser improves, and do not raise it to make a change pass.
    private const double BudgetInBytesPerCharacter = 12;

    private const int Queries = 50;

    [Theory]
    [InlineData(nameof(SqlDialect.Ansi), false)]
    [InlineData(nameof(SqlDialect.MySql), false)]
    [InlineData(nameof(SqlDialect.Oracle), false)]
    // The file names its own dialect, so its header is read before the rest.
    [InlineData(nameof(SqlDialect.Ansi), true)]
    public void Parse_TypicalFile_AllocatesWithinItsBudget(string dialectName, bool hasDirective)
    {
        const int Iterations = 20;
        var dialect = Enum.Parse<SqlDialect>(dialectName);
        var text = CreateFile(hasDirective);
        SqlFileParser.Parse(text, "Queries.sql", dialect).Blocks.Count.ShouldBe(Queries);

        // The first parses pay for one-off work: JIT compilation, static initialisers and the shared buffer pool.
        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            _ = SqlFileParser.Parse(text, "Queries.sql", dialect);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            _ = SqlFileParser.Parse(text, "Queries.sql", dialect);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        (allocated / (double)(Iterations * text.Length)).ShouldBeLessThan(BudgetInBytesPerCharacter);
    }

    // A file like the ones the generator is written for: a preamble, and queries that mix line comments, block
    // comments, string literals, tokens and blank lines.
    private static string CreateFile(bool hasDirective)
    {
        var file = new StringBuilder("-- Copyright (c) Example\n-- SqlSource: token-ignore=raw")
            .Append(hasDirective ? " dialect=postgres" : string.Empty)
            .Append("\n\n");
        for (var query = 0; query < Queries; query++)
        {
            var number = query.ToString(CultureInfo.InvariantCulture);
            _ = file.Append("-- name: Query")
                .Append(number)
                .Append("\n-- summary: Loads the rows for report ")
                .Append(number)
                .Append(".\n")
                .Append("SELECT u.id, u.name, u.email, /* inline note */ o.total -- trailing note\n")
                .Append("FROM {{schema}}.users AS u\n")
                .Append("    INNER JOIN {{schema}}.orders AS o ON o.user_id = u.id -- join\n")
                .Append("    /* a block comment\n       over two lines */\n")
                .Append("WHERE u.status = 'active' AND u.note <> 'it''s -- fine'\n")
                .Append("    AND o.created_at >= @from\n\n")
                .Append("ORDER BY {{orderBy}}, u.id;\n\n");
        }

        return file.ToString();
    }
}
````

Patch `tests/SqlSource.Tests/Generator/FileDiagnosticsTests.cs`: the three errors of a directive, with their messages, at their line and column in the `.sql` file.

<!-- patch:task4-file-diagnostics-tests -->
````diff
diff --git a/tests/SqlSource.Tests/Generator/FileDiagnosticsTests.cs b/tests/SqlSource.Tests/Generator/FileDiagnosticsTests.cs
index 19f4933..4f27f11 100644
--- a/tests/SqlSource.Tests/Generator/FileDiagnosticsTests.cs
+++ b/tests/SqlSource.Tests/Generator/FileDiagnosticsTests.cs
@@ -59,6 +59,40 @@ public class FileDiagnosticsTests
         run.CompilationErrors.ShouldBeEmpty();
     }
 
+    [Fact]
+    public void Run_DialectDirectiveInsideAQuery_IsAnErrorAtTheDirective()
+    {
+        var run = GeneratorHarness.Run(
+            Source,
+            new SqlFile("/app/Repo/Users.sql", "-- name: GetUser\n-- SqlSource: dialect=mysql\nSELECT 1;\n")
+        );
+
+        run.Diagnostics.ShouldBe([
+            "SQLSRC115 /app/Repo/Users.sql(2,15)-(2,28): The 'dialect' directive must come before the file's first "
+                + "query and before any SQL",
+        ]);
+        run.Sources["App.Sample.g.cs"].ShouldBe(EmptySqlClass);
+    }
+
+    [Fact]
+    public void Run_DialectDirectiveThatIsNotValidOrConflicts_IsAnErrorAtTheDirective()
+    {
+        var run = GeneratorHarness.Run(
+            Source,
+            new SqlFile(
+                "/app/Repo/Users.sql",
+                "-- SqlSource: dialect=pgsql\n-- SqlSource: dialect=mysql dialect=oracle\n-- name: GetUser\nSELECT 1;\n"
+            )
+        );
+
+        run.Diagnostics.ShouldBe([
+            "SQLSRC111 /app/Repo/Users.sql(1,15)-(1,28): The directive 'dialect=pgsql' lacks a value it needs, has "
+                + "one it does not take, or has one that is not valid",
+            "SQLSRC112 /app/Repo/Users.sql(2,29)-(2,43): 'dialect=oracle' conflicts with another directive in the "
+                + "same scope",
+        ]);
+    }
+
     [Fact]
     public void Run_FileWithAnError_GivesNoMembersAndTheTypeKeepsItsEmptySqlClass()
     {
````

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class '*SqlPreambleDialectTests'`
Expected: the build fails.  CS0103 names `SqlPreambleDialect`; CS1729 says `SqlDirectiveScope` has no constructor that takes one argument; CS0117 says `SqlParseErrorKind` has no `MisplacedDialect`; CS1739 or CS1501 says `Parse` takes no `dialect`.

- [ ] **Step 3: Add the error kind**

Replace `src/SqlSource/Parsing/SqlParseErrorKind.cs` with:

<!-- file:src/SqlSource/Parsing/SqlParseErrorKind.cs -->
````csharp
namespace SqlSource.Parsing;

/// <summary>
/// The problems the parser reports.  Each member says what <see cref="SqlParseError.Arguments" /> holds for it.
/// </summary>
internal enum SqlParseErrorKind
{
    /// <summary>A quoted region is not closed.  No argument.</summary>
    UnterminatedQuote,

    /// <summary>A block comment or hint is not closed.  No argument.</summary>
    UnterminatedBlockComment,

    /// <summary>
    /// A <c>-- name:</c> value is not a usable C# identifier.  Argument: the value, which may be empty.
    /// </summary>
    InvalidName,

    /// <summary>A name is used twice in the file.  Argument: the name.</summary>
    DuplicateName,

    /// <summary>
    /// The file has no name marker and its name is not a usable C# identifier.  Argument: the file name.
    /// </summary>
    InvalidFileName,

    /// <summary>There is SQL before the first name marker.  No argument.</summary>
    SqlBeforeFirstName,

    /// <summary>There is a <c>-- summary:</c> marker before the first name marker.  No argument.</summary>
    SummaryBeforeFirstName,

    /// <summary>
    /// A <c>-- summary:</c> or <c>-- SqlSource:</c> marker has no SQL after it in its block.  No argument.
    /// </summary>
    MarkerAtEndOfBlock,

    /// <summary>A directive is not recognised.  Argument: the directive as written.</summary>
    UnknownDirective,

    /// <summary>A <c>-- SqlSource:</c> marker has no directives.  No argument.</summary>
    EmptyDirectiveLine,

    /// <summary>
    /// A directive lacks a value it needs, has one it does not take, or has one that is not valid.  Argument: the
    /// directive as written.
    /// </summary>
    InvalidDirectiveValue,

    /// <summary>
    /// Two directives of one scope contradict each other: both validation directives, or two dialects.  Argument: the
    /// second directive as written.
    /// </summary>
    ConflictingDirectives,

    /// <summary>A block has no SQL.  No argument.</summary>
    EmptyBlock,

    /// <summary>A token's name is a reserved C# keyword.  Argument: the name.</summary>
    ReservedTokenName,

    /// <summary>
    /// A <c>dialect=</c> directive is inside a named query or after SQL.  Argument: the directive as written.
    /// </summary>
    MisplacedDialect,
}
````

`MisplacedDialect` is last: the ids of the file diagnostics follow the order of this enum, and a test checks that.

- [ ] **Step 4: Teach the directive scope the directive**

Replace `src/SqlSource/Parsing/SqlDirectiveScope.cs` with:

<!-- file:src/SqlSource/Parsing/SqlDirectiveScope.cs -->
````csharp
using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// The directives given by the <c>-- SqlSource:</c> markers of one scope: a block, or a file's preamble.
/// </summary>
/// <param name="headerEnd">
/// Where the file's header ends, as <see cref="SqlPreambleDialect.Apply" /> gives it.  A <c>dialect=</c> directive
/// is accepted only before it.
/// </param>
internal sealed class SqlDirectiveScope(int headerEnd)
{
    private const string KeepCommentsName = "keep-comments";
    private const string TokenValidationName = "token-validation";
    private const string NoTokenValidationName = "no-token-validation";
    private const string TokenIgnoreName = "token-ignore";
    private const string DialectName = "dialect";

    public bool KeepComments { get; private set; }

    public bool? TokenValidation { get; private set; }

    /// <summary>
    /// The dialect that a <c>dialect=</c> directive of this scope names, or null.  It is already in effect by the
    /// time the scope is read; it is kept here to find a second directive that names another.
    /// </summary>
    public SqlDialect? Dialect { get; private set; }

    public HashSet<string> IgnoredTokens { get; } = [];

    /// <summary>
    /// Finds the first <c>dialect=</c> directive of <paramref name="marker" /> that names a dialect.  Nothing is
    /// allocated and nothing is reported.
    /// </summary>
    public static bool TryFindDialect(string text, SqlMarker marker, out SqlDialect dialect)
    {
        var start = marker.ValueSpan.Start;
        var end = marker.ValueSpan.End;
        var valueOffset = DialectName.Length + 1;
        while (start < end)
        {
            var wordEnd = FindWordEnd(text, start, end);
            if (
                wordEnd - start >= valueOffset
                && text[start + DialectName.Length] == '='
                && string.Compare(text, start, DialectName, 0, DialectName.Length, StringComparison.OrdinalIgnoreCase)
                    == 0
                && SqlDialectName.TryParse(text.AsSpan(start + valueOffset, wordEnd - start - valueOffset), out dialect)
            )
            {
                return true;
            }

            start = SkipWhiteSpace(text, wordEnd, end);
        }

        dialect = SqlDialect.Ansi;
        return false;
    }

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
            var wordEnd = FindWordEnd(text, start, end);
            Apply(text.Substring(start, wordEnd - start), TextSpan.FromBounds(start, wordEnd), errors);
            start = SkipWhiteSpace(text, wordEnd, end);
        }
    }

    private static int FindWordEnd(string text, int start, int end)
    {
        while (start < end && !char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        return start;
    }

    private static int SkipWhiteSpace(string text, int start, int end)
    {
        while (start < end && char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        return start;
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
        else if (Is(name, DialectName))
        {
            problem = ApplyDialect(value, span);
        }
        else if (Is(name, KeepCommentsName) || Is(name, TokenValidationName) || Is(name, NoTokenValidationName))
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

    // The place is checked first: a directive in the wrong place is reported as that, whatever it names.
    private SqlParseErrorKind? ApplyDialect(string? value, TextSpan span)
    {
        if (span.Start >= headerEnd)
        {
            return SqlParseErrorKind.MisplacedDialect;
        }

        if (!SqlDialectName.TryParse(value, out var dialect))
        {
            return SqlParseErrorKind.InvalidDirectiveValue;
        }

        if (Dialect is { } existing && existing != dialect)
        {
            return SqlParseErrorKind.ConflictingDirectives;
        }

        Dialect = dialect;
        return null;
    }

    private SqlParseErrorKind? ApplyFlag(string name)
    {
        if (Is(name, KeepCommentsName))
        {
            KeepComments = true;
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
````

`ApplyDialect` checks the place first.  A directive in the wrong place is reported as that, whatever it names, so that a user who moves it is not then told its name is wrong as well; the name is checked once it is where it belongs.

- [ ] **Step 5: Find the directive before the file is lexed**

Create `src/SqlSource/Parsing/SqlPreambleDialect.cs`:

<!-- file:src/SqlSource/Parsing/SqlPreambleDialect.cs -->
````csharp
namespace SqlSource.Parsing;

/// <summary>
/// Finds the <c>dialect=</c> directive of a file and puts it into effect, before the text after it is lexed.
/// </summary>
/// <remarks>
/// A file's dialect is set in its header: the comments that come before its first SQL and before its first
/// <c>-- name:</c> marker.  The header is read under the dialect the lexer starts with, and the directive applies
/// from the line after it.  Reporting a directive that is wrong or in the wrong place is left to
/// <see cref="SqlDirectiveScope" />.
/// </remarks>
internal static class SqlPreambleDialect
{
    /// <summary>
    /// Reads the comments at the start of <paramref name="text" /> from <paramref name="lexer" /> and switches the
    /// lexer to the dialect that the first valid <c>dialect=</c> directive among them names.  Returns the offset
    /// where the header ends: a <c>dialect=</c> directive that starts at or after it is misplaced.
    /// </summary>
    public static int Apply(SqlLexer lexer, string text)
    {
        var switched = false;
        while (lexer.TryReadLeadingComment(out var comment))
        {
            if (SqlMarkerReader.Read(text, comment) is not { } marker)
            {
                continue;
            }

            if (marker.Kind == SqlMarkerKind.Name)
            {
                return marker.Span.Start;
            }

            if (
                !switched
                && marker.Kind == SqlMarkerKind.Directives
                && SqlDirectiveScope.TryFindDialect(text, marker, out var dialect)
            )
            {
                lexer.Rules = SqlDialectRules.For(dialect);
                switched = true;
            }
        }

        return lexer.Position;
    }
}
````

It keeps reading after it has switched the lexer, because the header's end is wanted too, and only the first valid directive switches.  A later one that names another dialect is found by the parser's pass and reported there.

- [ ] **Step 6: Give the parser a dialect**

Patch `src/SqlSource/Parsing/SqlFileParser.cs`:

<!-- patch:task4-parser -->
````diff
diff --git a/src/SqlSource/Parsing/SqlFileParser.cs b/src/SqlSource/Parsing/SqlFileParser.cs
index 00535a7..9f15fb0 100644
--- a/src/SqlSource/Parsing/SqlFileParser.cs
+++ b/src/SqlSource/Parsing/SqlFileParser.cs
@@ -16,19 +16,23 @@ internal static class SqlFileParser
     /// <summary>
     /// Parses <paramref name="text" />.  <paramref name="fileName" /> is the file's name with its extension and
     /// without a directory; it names the block of a file that has no <c>-- name:</c> marker.
+    /// <paramref name="dialect" /> is the dialect the project gives the file.  A <c>dialect=</c> directive in the
+    /// file's header replaces it for the text after the directive.
     /// </summary>
-    public static SqlFileParseResult Parse(string text, string fileName)
+    public static SqlFileParseResult Parse(string text, string fileName, SqlDialect dialect = SqlDialect.Ansi)
     {
-        var lexed = SqlLexer.Lex(text, SqlDialectRules.Ansi);
+        var lexer = new SqlLexer(text, SqlDialectRules.For(dialect));
+        var headerEnd = SqlPreambleDialect.Apply(lexer, text);
+        var lexed = lexer.ReadToEnd();
         return lexed.Error is null
-            ? new Parser(text, fileName, lexed.Lexemes).Run()
+            ? new Parser(text, fileName, lexed.Lexemes, headerEnd).Run()
             : new SqlFileParseResult(
                 EquatableArray<SqlBlock>.Empty,
                 new EquatableArray<SqlParseError>(ImmutableArray.Create(lexed.Error))
             );
     }
 
-    private sealed class Parser(string text, string fileName, EquatableArray<SqlLexeme> lexemes)
+    private sealed class Parser(string text, string fileName, EquatableArray<SqlLexeme> lexemes, int headerEnd)
     {
         private static readonly TextSpan FileStart = new(0, 0);
 
@@ -84,7 +88,7 @@ internal static class SqlFileParser
                 AddError(SqlParseErrorKind.InvalidFileName, FileStart, fileName);
             }
 
-            ReadBlock(name, FileStart, new SqlDirectiveScope(), 0, lexemes.Count);
+            ReadBlock(name, FileStart, new SqlDirectiveScope(headerEnd), 0, lexemes.Count);
         }
 
         private void ReadNamedBlocks(List<(int Index, SqlMarker Marker)> nameMarkers)
@@ -112,7 +116,7 @@ internal static class SqlFileParser
 
         private SqlDirectiveScope ReadPreamble(int end)
         {
-            var scope = new SqlDirectiveScope();
+            var scope = new SqlDirectiveScope(headerEnd);
             var sqlReported = false;
             for (var index = 0; index < end; index++)
             {
@@ -138,7 +142,7 @@ internal static class SqlFileParser
 
         private void ReadBlock(string name, TextSpan nameSpan, SqlDirectiveScope inherited, int start, int end)
         {
-            var scope = new SqlDirectiveScope();
+            var scope = new SqlDirectiveScope(headerEnd);
             var summary = new List<string>();
             var lastContent = FindLastContent(start, end);
             for (var index = start; index < end; index++)
````

- [ ] **Step 7: Add `SQLSRC115` and widen two messages**

Patch `src/SqlSource/Diagnostics/SqlDiagnostics.cs`:

<!-- patch:task4-diagnostics -->
````diff
diff --git a/src/SqlSource/Diagnostics/SqlDiagnostics.cs b/src/SqlSource/Diagnostics/SqlDiagnostics.cs
index c4389c3..8048906 100644
--- a/src/SqlSource/Diagnostics/SqlDiagnostics.cs
+++ b/src/SqlSource/Diagnostics/SqlDiagnostics.cs
@@ -244,7 +244,8 @@ internal static class SqlDiagnostics
     public static readonly DiagnosticDescriptor InvalidDirectiveValue = new(
         id: "SQLSRC111",
         title: "Directive value is not valid",
-        messageFormat: "The directive '{0}' lacks a value it needs, or has one it does not take",
+        messageFormat: "The directive '{0}' lacks a value it needs, has one it does not take, or has one that is not "
+            + "valid",
         category: Category,
         defaultSeverity: DiagnosticSeverity.Error,
         isEnabledByDefault: true,
@@ -255,7 +256,7 @@ internal static class SqlDiagnostics
     public static readonly DiagnosticDescriptor ConflictingDirectives = new(
         id: "SQLSRC112",
         title: "Directives conflict",
-        messageFormat: "'{0}' conflicts with the other token validation directive in the same scope",
+        messageFormat: "'{0}' conflicts with another directive in the same scope",
         category: Category,
         defaultSeverity: DiagnosticSeverity.Error,
         isEnabledByDefault: true,
@@ -285,6 +286,17 @@ internal static class SqlDiagnostics
         customTags: WellKnownDiagnosticTags.NotConfigurable
     );
 
+    public static readonly DiagnosticDescriptor MisplacedDialect = new(
+        id: "SQLSRC115",
+        title: "Dialect directive is misplaced",
+        messageFormat: "The 'dialect' directive must come before the file's first query and before any SQL",
+        category: Category,
+        defaultSeverity: DiagnosticSeverity.Error,
+        isEnabledByDefault: true,
+        helpLinkUri: HelpLinkBase + "sqlsrc115",
+        customTags: WellKnownDiagnosticTags.NotConfigurable
+    );
+
     /// <summary>
     /// Every descriptor, in the order of its id.
     /// </summary>
@@ -313,7 +325,8 @@ internal static class SqlDiagnostics
             InvalidDirectiveValue,
             ConflictingDirectives,
             EmptyBlock,
-            ReservedTokenName
+            ReservedTokenName,
+            MisplacedDialect
         );
 
     /// <summary>
@@ -336,6 +349,7 @@ internal static class SqlDiagnostics
             SqlParseErrorKind.ConflictingDirectives => ConflictingDirectives,
             SqlParseErrorKind.EmptyBlock => EmptyBlock,
             SqlParseErrorKind.ReservedTokenName => ReservedTokenName,
+            SqlParseErrorKind.MisplacedDialect => MisplacedDialect,
             _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No descriptor is defined for this kind."),
         };
 }
````

Patch `src/SqlSource/AnalyzerReleases.Unshipped.md`.  The build fails with RS2000 without this line.

<!-- patch:task4-analyzer-releases -->
````diff
diff --git a/src/SqlSource/AnalyzerReleases.Unshipped.md b/src/SqlSource/AnalyzerReleases.Unshipped.md
index c1a20c0..71bb245 100644
--- a/src/SqlSource/AnalyzerReleases.Unshipped.md
+++ b/src/SqlSource/AnalyzerReleases.Unshipped.md
@@ -29,3 +29,4 @@ SQLSRC111 | SqlSource | Error | Directive value is not valid
 SQLSRC112 | SqlSource | Error | Directives conflict
 SQLSRC113 | SqlSource | Error | Query has no SQL
 SQLSRC114 | SqlSource | Error | Token name is a keyword
+SQLSRC115 | SqlSource | Error | Dialect directive is misplaced
````

Patch `docs/diagnostics.md`.  `DiagnosticsDocumentTests` fails until the table and the sections match the descriptors.  The sections of `SQLSRC101` and `SQLSRC102` now say to check the dialect, and link to a `Dialects` section of the README that Task 7 writes.

<!-- patch:task4-diagnostics-document -->
````diff
diff --git a/docs/diagnostics.md b/docs/diagnostics.md
index 5e05bb2..b8b5c2f 100644
--- a/docs/diagnostics.md
+++ b/docs/diagnostics.md
@@ -28,6 +28,7 @@ Every problem SqlSource finds is a build error, and none can be turned off or ma
 | [SQLSRC112](#sqlsrc112) | Directives conflict |
 | [SQLSRC113](#sqlsrc113) | Query has no SQL |
 | [SQLSRC114](#sqlsrc114) | Token name is a keyword |
+| [SQLSRC115](#sqlsrc115) | Dialect directive is misplaced |
 
 Ids below 100 are about the type that carries `[SqlQueries]`, or about the project.  Ids from 101 are about the contents of a `.sql` file.
 
@@ -156,26 +157,28 @@ The compiler hands a generator only the part of a value before the first `;` or
 
 **Quote is not closed**
 
-A string or a quoted identifier starts and never ends.  The error is at the opening quote.
+A string or a quoted identifier starts and never ends.  The error is at the opening quote or bracket.
 
 ```sql
 SELECT 'unfinished FROM users;
 ```
 
-Close the quote.  If the SQL is valid for your database, it uses a quoting form that SqlSource reads differently; see the dialect limits in the README.  Rewrite the construct in a form that SqlSource reads correctly.
+Close the quote.  If the SQL is valid for your database, SqlSource is reading it by the rules of another one: `'it\'s'` is one string in MySQL and an unclosed one elsewhere.  Set the dialect of the file; see [Dialects](https://github.com/mbcrawfo/SqlSource/blob/main/README.md#dialects) in the README, which also lists the few constructs that no dialect setting reads correctly.
 
 ## SQLSRC102
 
 **Comment is not closed**
 
-A block comment or a hint starts with `/*` and never ends.  The error is at the `/*`.  SqlSource nests block comments, as PostgreSQL does: each `/*` inside a comment needs its own `*/`.
+A block comment or a hint starts with `/*` and never ends.  The error is at the `/*`.
 
 ```sql
 /* outer /* inner */
 SELECT 1;
 ```
 
-Close the comment.  In a dialect that does not nest comments, remove the inner `/*`.
+Close the comment.  Whether a `/*` inside a comment needs its own `*/` depends on the dialect: it does in the default dialect, in SQL Server and in PostgreSQL, and it does not in MySQL, MariaDB, SQLite and Oracle.  If the SQL is valid for your database, set the dialect of the file; see [Dialects](https://github.com/mbcrawfo/SqlSource/blob/main/README.md#dialects) in the README.
+
+SQLite accepts a comment that is still open at the end of the file.  SqlSource does not, in any dialect, because the comment would take every later query of the file with it.
 
 ## SQLSRC103
 
@@ -280,22 +283,30 @@ Add a directive, or delete the line.
 
 **Directive value is not valid**
 
-`token-ignore` needs a value that is a C# identifier, as in `token-ignore=table`.  No other directive takes a value.
+A directive lacks a value it needs, has one it does not take, or has one that is not valid.
+
+- `token-ignore` needs a value that is a C# identifier, as in `token-ignore=table`.
+- `dialect` needs the name of a dialect, as in `dialect=postgres`.  The names are `ansi`, `mssql`, `postgres`, `mysql`, `mariadb`, `sqlite` and `oracle`, in any case; `sqlserver` and `tsql` also mean `mssql`, and `postgresql` also means `postgres`.
+- No other directive takes a value.
 
 ```sql
 -- SqlSource: token-ignore
+-- SqlSource: dialect=pgsql
 -- SqlSource: keep-comments=true
 ```
 
-Add the missing value, or remove the one that does not belong.
+Add the missing value, correct the one that is wrong, or remove the one that does not belong.
 
 ## SQLSRC112
 
 **Directives conflict**
 
-`token-validation` and `no-token-validation` both appear in one scope: both in the lines before the first `-- name:` marker, or both in one query.  The error is at the second.
+Two directives in one scope contradict each other.  A scope is the lines before the first `-- name:` marker, or one query.  The error is at the second directive.
 
-Remove one of the two.  A directive in a query overrides the same directive before the first `-- name:` marker, and that is not a conflict.
+- `token-validation` and `no-token-validation` both appear.
+- Two `dialect` directives name different dialects.  A file has one dialect.
+
+Remove one of the two.  A validation directive in a query overrides the one before the first `-- name:` marker, and that is not a conflict.  The same dialect given twice is not a conflict either.
 
 ## SQLSRC113
 
@@ -321,3 +332,26 @@ SELECT * FROM {{class}};
 ```
 
 Rename the token.  If the braces are literal text and not a token, add `-- SqlSource: token-ignore=class` to the query.
+
+## SQLSRC115
+
+**Dialect directive is misplaced**
+
+A `dialect` directive sets the dialect of a whole file, and it changes how the text after it is read.  So it must come before the file's first `-- name:` marker and before the file's first SQL.  This one is inside a named query, or after SQL.
+
+```sql
+-- name: GetUser
+-- SqlSource: dialect=mysql
+SELECT 1;
+```
+
+Move the directive to the top of the file.  Comments may come before it, such as a licence header.  In a file with no `-- name:` marker, which is one query, put it above the query's SQL.
+
+```sql
+-- SqlSource: dialect=mysql
+
+-- name: GetUser
+SELECT 1;
+```
+
+A file cannot mix dialects.  Put the queries for another database in a file of their own.
````

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class '*SqlPreambleDialectTests' --filter-class '*SqlDirectiveScopeTests' --filter-class '*SqlFileParserTests' --filter-class '*SqlFileParserAllocationTests' --filter-class '*FileDiagnosticsTests'`
Expected: total 208, failed 0.

Run: `dotnet test --solution SqlSource.slnx`
Expected: total 1196, failed 0.

- [ ] **Step 9: Format and validate**

Run: `./format.sh`

Run: `./pre-commit-validation.sh`
Expected: every step `passed`.

- [ ] **Step 10: Commit**

```bash
git add src/SqlSource docs/diagnostics.md tests/SqlSource.Tests/Parsing tests/SqlSource.Tests/Generator/FileDiagnosticsTests.cs
```

```bash
git commit -m "Read a file's dialect directive and report one that is misplaced

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: The dialect from MSBuild, and `SQLSRC011`

**Files:**
- Create: `src/SqlSource/Generation/DialectSetting.cs`, `src/SqlSource/Generation/FileDialect.cs`
- Modify: `src/SqlSource/Generation/ParsedSqlFile.cs`, `SqlFileReader.cs`, `TrackingNames.cs`, `src/SqlSource/SqlSourceGenerator.cs`
- Modify: `src/SqlSource/Diagnostics/SqlDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/diagnostics.md`
- Test: `tests/SqlSource.Tests/Generation/DialectSettingTests.cs` (new), `tests/SqlSource.Tests/Generator/DialectTests.cs` (new), `tests/SqlSource.Tests/Generator/TestOptionsProvider.cs`, `GeneratorHarness.cs`, `CachingTests.cs`, `tests/SqlSource.Tests/Generation/SqlFileReaderTests.cs`, `tests/SqlSource.Tests/Diagnostics/SqlDiagnosticsTests.cs`

**Interfaces:**
- Consumes: `SqlDialect`, `SqlDialectName` (Task 1); `SqlFileParser.Parse(text, fileName, dialect)` (Task 4).
- Produces, in namespace `SqlSource.Generation`, `internal`:
  - `sealed record DialectSetting(SqlDialect? Dialect, string? InvalidValue)` with `const string PropertyName = "build_property.SqlSourceDialect"`, `const string MetadataName = "build_metadata.AdditionalFiles.SqlSourceDialect"`, `static DialectSetting ReadProperty(AnalyzerConfigOptions globalOptions)`, `static DialectSetting ReadMetadata(AnalyzerConfigOptions fileOptions)` and `static DialectSetting Parse(string? value)`.  `Dialect` is null when nothing is set, and `SqlDialect.Ansi` with `InvalidValue` set when the value is not a dialect.
  - `sealed record FileDialect(AdditionalText File, SqlDialect Dialect, string? InvalidValue)` with `static FileDialect Resolve(AdditionalText file, DialectSetting metadata, DialectSetting project)`.
  - `ParsedSqlFile` gains a last parameter, `string? InvalidDialect = null`.
  - `SqlFileReader.Read(FileDialect file, string normalizedPath, CancellationToken cancellationToken)` and `SqlFileReader.Read(AdditionalText file, string normalizedPath, SqlDialect dialect, string? invalidDialect, CancellationToken cancellationToken)`.  The three-argument overload that took an `AdditionalText` is gone.
  - `TrackingNames.ProjectDialect` and `TrackingNames.FileDialect`.
  - `SqlDiagnostics.InvalidDialect`, `SQLSRC011`.
- Produces, in the tests:
  - `TestOptionsProvider(string? tokenValidation, string? dialect = null, IReadOnlyDictionary<string, string>? fileDialects = null)`; the dictionary is keyed by a file's path.
  - `SqlFile(string Path, string? Text, string? Dialect = null)`; `Dialect` is the file's metadata.
  - `GeneratorHarness.Run(..., string? dialect = null)`, and `GeneratorHarness.CreateDriver(IEnumerable<AdditionalText> sqlFiles, CSharpParseOptions? parseOptions = null, TestOptionsProvider? options = null)`, whose third parameter was `string? tokenValidation`.

The pipeline:

```
sqlFiles + options provider   -> (file, metadata value)                          cached by value
         + project dialect    -> FileDialect(file, dialect, invalid value)       [FileDialect]
         + claimed paths      -> SqlFileReader.Read(file, path, ...)             [ParsedFile]
```

The dialect is resolved before the parse, so the parse step sees one dialect for a file.  A change to the property then parses only the files that fall back to it.  This is the opposite of `SqlSourceTokenValidation`, which is kept out of the parse on purpose: a dialect decides what the parse produces.

- [ ] **Step 1: Write the failing tests**

Replace `tests/SqlSource.Tests/Generator/TestOptionsProvider.cs` with:

<!-- file:tests/SqlSource.Tests/Generator/TestOptionsProvider.cs -->
````csharp
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SqlSource.Tests.Generator;

// What MSBuild tells a generator, as the compiler hands it over: the project's SqlSourceTokenValidation and
// SqlSourceDialect properties, and the SqlSourceDialect metadata of each .sql file, by the file's path.  A null value
// is a project that does not set the property, and a path that is not listed is a file without the metadata.
internal sealed class TestOptionsProvider(
    string? tokenValidation,
    string? dialect = null,
    IReadOnlyDictionary<string, string>? fileDialects = null
) : AnalyzerConfigOptionsProvider
{
    private static readonly Options None = new([]);

    // The keys are spelled out here, not taken from the generator, so that a change to the generator's spelling
    // fails a test.
    public override AnalyzerConfigOptions GlobalOptions { get; } =
        new Options(
            new Dictionary<string, string?>
            {
                ["build_property.SqlSourceTokenValidation"] = tokenValidation,
                ["build_property.SqlSourceDialect"] = dialect,
            }
        );

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => None;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) =>
        fileDialects is not null && fileDialects.TryGetValue(textFile.Path, out var value)
            ? new Options(
                new Dictionary<string, string?> { ["build_metadata.AdditionalFiles.SqlSourceDialect"] = value }
            )
            : None;

    private sealed class Options(Dictionary<string, string?> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
            values.TryGetValue(key, out value) && value is not null;
    }
}
````

Patch `tests/SqlSource.Tests/Generator/GeneratorHarness.cs`:

<!-- patch:task5-harness -->
````diff
diff --git a/tests/SqlSource.Tests/Generator/GeneratorHarness.cs b/tests/SqlSource.Tests/Generator/GeneratorHarness.cs
index e2a7ea4..ebd758b 100644
--- a/tests/SqlSource.Tests/Generator/GeneratorHarness.cs
+++ b/tests/SqlSource.Tests/Generator/GeneratorHarness.cs
@@ -42,12 +42,18 @@ internal static class GeneratorHarness
         bool supportedFramework = true,
         LanguageVersion languageVersion = LanguageVersion.CSharp12,
         MetadataReference[]? references = null,
-        string? tokenValidation = null
+        string? tokenValidation = null,
+        string? dialect = null
     )
     {
         var parseOptions = ParseOptions.WithLanguageVersion(languageVersion);
         var compilation = CreateCompilation(sources, supportedFramework, parseOptions, references);
-        var driver = CreateDriver(sqlFiles.Select(file => file.ToAdditionalText()), parseOptions, tokenValidation)
+        var options = new TestOptionsProvider(
+            tokenValidation,
+            dialect,
+            sqlFiles.Where(file => file.Dialect is not null).ToDictionary(file => file.Path, file => file.Dialect!)
+        );
+        var driver = CreateDriver(sqlFiles.Select(file => file.ToAdditionalText()), parseOptions, options)
             .RunGeneratorsAndUpdateCompilation(
                 compilation,
                 out var updated,
@@ -100,18 +106,18 @@ internal static class GeneratorHarness
             new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
         );
 
-    // Step tracking is on, so that a test can read why each step of the pipeline ran.  tokenValidation is the value
-    // of the project's SqlSourceTokenValidation property, and null is a project that does not set it.
+    // Step tracking is on, so that a test can read why each step of the pipeline ran.  Without options the project
+    // sets none of the package's properties and no file has metadata.
     public static GeneratorDriver CreateDriver(
         IEnumerable<AdditionalText> sqlFiles,
         CSharpParseOptions? parseOptions = null,
-        string? tokenValidation = null
+        TestOptionsProvider? options = null
     ) =>
         CSharpGeneratorDriver.Create(
             [new SqlSourceGenerator().AsSourceGenerator()],
             sqlFiles,
             parseOptions ?? ParseOptions,
-            new TestOptionsProvider(tokenValidation),
+            options ?? new TestOptionsProvider(null),
             new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true)
         );
 
@@ -149,7 +155,8 @@ internal static class GeneratorHarness
 
 internal sealed record SourceFile(string Path, string Text);
 
-internal sealed record SqlFile(string Path, string? Text)
+// Dialect is the SqlSourceDialect metadata of the file's AdditionalFiles item, and null is a file without it.
+internal sealed record SqlFile(string Path, string? Text, string? Dialect = null)
 {
     public AdditionalText ToAdditionalText() => new InMemoryAdditionalText(Path, Text);
 }
````

Create `tests/SqlSource.Tests/Generation/DialectSettingTests.cs`:

<!-- file:tests/SqlSource.Tests/Generation/DialectSettingTests.cs -->
````csharp
using System.Collections.Generic;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Parsing;
using SqlSource.Tests.Generator;
using Xunit;

namespace SqlSource.Tests.Generation;

public class DialectSettingTests
{
    private const string Path = "/app/Repo/Users.sql";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n")]
    public void Parse_MissingOrEmpty_IsNotSet(string? value) =>
        DialectSetting.Parse(value).ShouldBe(new DialectSetting(null, null));

    [Theory]
    [InlineData("postgres", nameof(SqlDialect.PostgreSql))]
    [InlineData("MSSQL", nameof(SqlDialect.SqlServer))]
    [InlineData("\n    mysql\n  ", nameof(SqlDialect.MySql))]
    [InlineData("ansi", nameof(SqlDialect.Ansi))]
    public void Parse_NameOfADialect_IsThatDialect(string value, string expected)
    {
        var setting = DialectSetting.Parse(value);

        setting.Dialect.ToString().ShouldBe(expected);
        setting.InvalidValue.ShouldBeNull();
    }

    [Theory]
    [InlineData("pgsql")]
    [InlineData("sql server")]
    [InlineData(" Postgres 16 ")]
    [InlineData("true")]
    public void Parse_AnythingElse_IsAnsiAndKeepsTheValueAsWritten(string value) =>
        DialectSetting.Parse(value).ShouldBe(new DialectSetting(SqlDialect.Ansi, value));

    [Fact]
    public void ReadProperty_ProjectWithAndWithoutTheProperty_ReadsItFromTheGlobalOptions()
    {
        DialectSetting
            .ReadProperty(new TestOptionsProvider(null).GlobalOptions)
            .ShouldBe(new DialectSetting(null, null));
        DialectSetting
            .ReadProperty(new TestOptionsProvider(null, "oracle").GlobalOptions)
            .ShouldBe(new DialectSetting(SqlDialect.Oracle, null));
        DialectSetting
            .ReadProperty(new TestOptionsProvider(null, "orcl").GlobalOptions)
            .ShouldBe(new DialectSetting(SqlDialect.Ansi, "orcl"));
    }

    [Fact]
    public void ReadMetadata_FileWithAndWithoutTheMetadata_ReadsItFromTheOptionsOfTheFile()
    {
        var file = new InMemoryAdditionalText(Path, "SELECT 1;");
        var other = new InMemoryAdditionalText("/app/Repo/Orders.sql", "SELECT 2;");
        var options = new TestOptionsProvider(null, "oracle", new Dictionary<string, string> { [Path] = "sqlite" });

        DialectSetting.ReadMetadata(options.GetOptions(file)).ShouldBe(new DialectSetting(SqlDialect.Sqlite, null));
        DialectSetting.ReadMetadata(options.GetOptions(other)).ShouldBe(new DialectSetting(null, null));
    }

    // The property is not in a file's options and the metadata is not in the global ones: each is read from its own.
    [Fact]
    public void Read_PropertyAndMetadata_AreNotMistakenForEachOther()
    {
        var file = new InMemoryAdditionalText(Path, "SELECT 1;");
        var options = new TestOptionsProvider(null, "oracle", new Dictionary<string, string> { [Path] = "sqlite" });

        DialectSetting.ReadMetadata(options.GlobalOptions).Dialect.ShouldBeNull();
        DialectSetting.ReadProperty(options.GetOptions(file)).Dialect.ShouldBeNull();
    }

    [Theory]
    // The file's metadata, when it is set.
    [InlineData("mysql", "oracle", nameof(SqlDialect.MySql), null)]
    [InlineData("ansi", "oracle", nameof(SqlDialect.Ansi), null)]
    [InlineData("mysql", null, nameof(SqlDialect.MySql), null)]
    // Or else the project's property.
    [InlineData(null, "oracle", nameof(SqlDialect.Oracle), null)]
    [InlineData("", "oracle", nameof(SqlDialect.Oracle), null)]
    // Or else ANSI.
    [InlineData(null, null, nameof(SqlDialect.Ansi), null)]
    // Metadata that is not a dialect is ANSI, not the project's dialect, and is carried to be reported.
    [InlineData("nope", "oracle", nameof(SqlDialect.Ansi), "nope")]
    // A property that is not a dialect is ANSI too.  It is reported from the project's setting, not from each file.
    [InlineData(null, "nope", nameof(SqlDialect.Ansi), null)]
    public void Resolve_MetadataThenPropertyThenAnsi(
        string? metadata,
        string? property,
        string dialect,
        string? invalid
    )
    {
        var file = new InMemoryAdditionalText(Path, "SELECT 1;");

        var resolved = FileDialect.Resolve(file, DialectSetting.Parse(metadata), DialectSetting.Parse(property));

        resolved.File.ShouldBeSameAs(file);
        resolved.Dialect.ToString().ShouldBe(dialect);
        resolved.InvalidValue.ShouldBe(invalid);
    }

    [Fact]
    public void Resolve_SameFileAndSettings_GivesEqualValues()
    {
        var file = new InMemoryAdditionalText(Path, "SELECT 1;");

        FileDialect
            .Resolve(file, DialectSetting.Parse("mysql"), DialectSetting.Parse(null))
            .ShouldBe(FileDialect.Resolve(file, DialectSetting.Parse(" MySQL "), DialectSetting.Parse("oracle")));
    }
}
````

Create `tests/SqlSource.Tests/Generator/DialectTests.cs`.  It is in `Generator/`, so it also runs on Roslyn 4.8.0.

<!-- file:tests/SqlSource.Tests/Generator/DialectTests.cs -->
````csharp
using Shouldly;
using Xunit;

namespace SqlSource.Tests.Generator;

// Which dialect a file is read by: the dialect= directive at the top of the file decides, then the SqlSourceDialect
// metadata of the file's item, then the project's SqlSourceDialect property, and without any of them it is ANSI.
public class DialectTests
{
    private const string Source = """
        using SqlSource;

        namespace App;

        [SqlQueries(Mode = SqlQueriesMode.Direct)]
        public partial class Sample;
        """;

    // Three dialects read this line three ways.  ANSI: the # is SQL and the two dashes start a comment.  MySQL: the #
    // starts a comment.  SQL Server: the brackets are an identifier, and the comment starts after it.
    private const string Query = "SELECT 1 # x [y--z] -- c\n";

    private const string Ansi = "SELECT 1 # x [y";

    private const string MySql = "SELECT 1";

    private const string SqlServer = "SELECT 1 # x [y--z]";

    private const string Invalid =
        "' is not a SQL dialect.  SqlSourceDialect accepts ansi, mssql, postgres, mysql, mariadb, sqlite and oracle.";

    [Theory]
    // Nothing set.
    [InlineData(null, null, null, Ansi)]
    [InlineData(null, null, "", Ansi)]
    [InlineData(null, "", null, Ansi)]
    // The property alone, in any case and with the whitespace of a value on its own line.
    [InlineData(null, null, "mysql", MySql)]
    [InlineData(null, null, "\n    MySQL\n  ", MySql)]
    [InlineData(null, null, "tsql", SqlServer)]
    // The file's metadata beats the property.  Empty metadata is none.
    [InlineData(null, "mssql", null, SqlServer)]
    [InlineData(null, "mssql", "mysql", SqlServer)]
    [InlineData(null, "ansi", "mysql", Ansi)]
    [InlineData(null, "", "mysql", MySql)]
    // The file's directive beats both.
    [InlineData("mysql", null, null, MySql)]
    [InlineData("mysql", "mssql", null, MySql)]
    [InlineData("mssql", "mysql", "mysql", SqlServer)]
    [InlineData("ansi", "mssql", "mysql", Ansi)]
    public void Run_File_IsReadByItsDirectiveThenItsMetadataThenTheProperty(
        string? directive,
        string? metadata,
        string? property,
        string expected
    )
    {
        var sql = (directive is null ? string.Empty : "-- SqlSource: dialect=" + directive + "\n") + Query;

        var run = Run(property, new SqlFile("/app/Repo/Q.sql", sql, metadata));

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        run.Sources["App.Sample.g.cs"].ShouldContain("public const string Q = \"" + expected + "\";");
    }

    [Fact]
    public void Run_FilesWithDifferentMetadata_AreEachReadByTheirOwnDialect()
    {
        var run = Run(
            "mysql",
            new SqlFile("/app/Repo/A.sql", Query),
            new SqlFile("/app/Repo/B.sql", Query, "mssql"),
            new SqlFile("/app/Repo/C.sql", "-- SqlSource: dialect=ansi\n" + Query, "mssql")
        );

        run.Diagnostics.ShouldBeEmpty();
        var source = run.Sources["App.Sample.g.cs"];
        source.ShouldContain("public const string A = \"" + MySql + "\";");
        source.ShouldContain("public const string B = \"" + SqlServer + "\";");
        source.ShouldContain("public const string C = \"" + Ansi + "\";");
    }

    [Theory]
    [InlineData("pgsql")]
    [InlineData("sql server")]
    [InlineData(" Postgres 16 ")]
    public void Run_PropertyThatIsNotADialect_IsAnErrorWithoutAPositionAndTheFilesAreReadAsAnsi(string property)
    {
        var run = Run(property, new SqlFile("/app/Repo/Q.sql", Query));

        run.Diagnostics.ShouldBe(["SQLSRC011 (1,1)-(1,1): '" + property + Invalid]);

        // The members are still there, so the build reports this error and not one for each use of a query.
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources["App.Sample.g.cs"].ShouldContain("public const string Q = \"" + Ansi + "\";");
    }

    [Fact]
    public void Run_MetadataThatIsNotADialect_IsReportedOnceForEachValueAndTheFileIsReadAsAnsi()
    {
        var run = Run(
            "mysql",
            new SqlFile("/app/Repo/A.sql", Query, "zeta"),
            new SqlFile("/app/Repo/B.sql", Query, "alpha"),
            new SqlFile("/app/Repo/C.sql", Query, "zeta"),
            new SqlFile("/app/Repo/D.sql", Query)
        );

        // In ordinal order, whatever the order of the files.
        run.Diagnostics.ShouldBe(["SQLSRC011 (1,1)-(1,1): 'alpha" + Invalid, "SQLSRC011 (1,1)-(1,1): 'zeta" + Invalid]);
        run.CompilationErrors.ShouldBeEmpty();

        // Not the project's dialect: the file said something, and what it said is wrong.
        var source = run.Sources["App.Sample.g.cs"];
        source.ShouldContain("public const string A = \"" + Ansi + "\";");
        source.ShouldContain("public const string D = \"" + MySql + "\";");
    }

    [Fact]
    public void Run_SameInvalidValueInThePropertyAndInMetadata_IsReportedOnce()
    {
        var run = Run("nope", new SqlFile("/app/Repo/Q.sql", Query, "nope"));

        run.Diagnostics.ShouldBe(["SQLSRC011 (1,1)-(1,1): 'nope" + Invalid]);
    }

    [Fact]
    public void Run_InvalidMetadataOfAFileThatNoTypeClaims_IsNotReported()
    {
        var run = Run(
            null,
            new SqlFile("/app/Repo/Q.sql", Query),
            new SqlFile("/app/Migrations/001_init.sql", "SELECT 'x;\n", "nope")
        );

        run.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Run_InvalidMetadataOfAFileWithADirective_IsStillReportedAndTheDirectiveIsUsed()
    {
        var run = Run(null, new SqlFile("/app/Repo/Q.sql", "-- SqlSource: dialect=mysql\n" + Query, "nope"));

        run.Diagnostics.ShouldBe(["SQLSRC011 (1,1)-(1,1): 'nope" + Invalid]);
        run.Sources["App.Sample.g.cs"].ShouldContain("public const string Q = \"" + MySql + "\";");
    }

    [Fact]
    public void Run_InvalidPropertyInAProjectWithoutAnAttributedType_IsStillAnError()
    {
        var run = GeneratorHarness.Run(
            [new SourceFile(GeneratorHarness.SourcePath, "public class Sample;")],
            [],
            dialect: "nope"
        );

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC011 ");
        run.Sources.Keys.ShouldBe(["SqlQueriesAttribute.g.cs"]);
    }

    // The two settings of the project are read apart: a value of one is never taken for the other.
    [Fact]
    public void Run_BothPropertiesInvalid_ReportsEachUnderItsOwnId()
    {
        var run = GeneratorHarness.Run(
            [new SourceFile(GeneratorHarness.SourcePath, Source)],
            [new SqlFile("/app/Repo/Q.sql", Query)],
            tokenValidation: "mysql",
            dialect: "false"
        );

        run.Diagnostics.Count.ShouldBe(2);
        run.Diagnostics.ShouldContain(diagnostic =>
            diagnostic.StartsWith("SQLSRC010 (1,1)-(1,1): The MSBuild property ")
        );
        run.Diagnostics.ShouldContain("SQLSRC011 (1,1)-(1,1): 'false" + Invalid);
    }

    private static GeneratorRun Run(string? property, params SqlFile[] files) =>
        GeneratorHarness.Run([new SourceFile(GeneratorHarness.SourcePath, Source)], files, dialect: property);
}
````

The query of these tests is chosen so that three dialects read it three ways and none reports an error, which lets one theory show the whole precedence.

Patch `tests/SqlSource.Tests/Generator/CachingTests.cs`: the two new steps join the list of those an unrelated edit leaves alone, and three tests pin what a change to the dialect redoes.

<!-- patch:task5-caching-tests -->
````diff
diff --git a/tests/SqlSource.Tests/Generator/CachingTests.cs b/tests/SqlSource.Tests/Generator/CachingTests.cs
index b5651be..2028b51 100644
--- a/tests/SqlSource.Tests/Generator/CachingTests.cs
+++ b/tests/SqlSource.Tests/Generator/CachingTests.cs
@@ -123,6 +123,8 @@ public class CachingTests
             TrackingNames.SqlPaths,
             TrackingNames.TypeFiles,
             TrackingNames.ClaimedPaths,
+            TrackingNames.ProjectDialect,
+            TrackingNames.FileDialect,
             TrackingNames.ParsedFile,
             TrackingNames.ParsedFiles,
             TrackingNames.TypeQueries,
@@ -223,16 +225,140 @@ public class CachingTests
         var result = Run(driver.WithUpdatedAnalyzerConfigOptions(new TestOptionsProvider(null)), compilation);
 
         AllReasons(result, TrackingNames.TokenValidation).ShouldBe([IncrementalStepRunReason.Unchanged]);
+        AllReasons(result, TrackingNames.ProjectDialect).ShouldBe([IncrementalStepRunReason.Unchanged]);
+        AllReasons(result, TrackingNames.FileDialect).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
+        AllReasons(result, TrackingNames.ParsedFile).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
         AllReasons(result, TrackingNames.TypeOutput).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
         OutputReasons(result).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
     }
 
+    [Fact]
+    public void Run_DialectPropertyChanged_ParsesOnlyTheFilesThatFallBackToIt()
+    {
+        var compilation = GeneratorHarness.CreateCompilation(Sources);
+        var users = new InMemoryAdditionalText(_users.Path, "-- name: GetUser\nSELECT 1; # c\n");
+        var orderDialect = new Dictionary<string, string> { [_orders.Path] = "mssql" };
+        var driver = FirstRun(compilation, users, new TestOptionsProvider(null, null, orderDialect));
+
+        var result = Run(
+            driver.WithUpdatedAnalyzerConfigOptions(new TestOptionsProvider(null, "mysql", orderDialect)),
+            compilation
+        );
+
+        AllReasons(result, TrackingNames.ProjectDialect).ShouldBe([IncrementalStepRunReason.Modified]);
+        Reasons<FileDialect>(result, TrackingNames.FileDialect, file => file.File.Path)
+            .ShouldBe(
+                new Dictionary<string, IncrementalStepRunReason>
+                {
+                    [_users.Path] = IncrementalStepRunReason.Modified,
+                    [_orders.Path] = IncrementalStepRunReason.Unchanged,
+                },
+                ignoreOrder: true
+            );
+        Reasons<ParsedSqlFile>(result, TrackingNames.ParsedFile, file => file.FileName)
+            .ShouldBe(
+                new Dictionary<string, IncrementalStepRunReason>
+                {
+                    ["Users.sql"] = IncrementalStepRunReason.Modified,
+                    ["Orders.sql"] = IncrementalStepRunReason.Cached,
+                },
+                ignoreOrder: true
+            );
+        Reasons<TypeOutput>(result, TrackingNames.TypeOutput, output => output.HintName)
+            .ShouldBe(
+                new Dictionary<string, IncrementalStepRunReason>
+                {
+                    ["App.Users.UserQueries.g.cs"] = IncrementalStepRunReason.Modified,
+                    ["App.Orders.OrderQueries.g.cs"] = IncrementalStepRunReason.Cached,
+                },
+                ignoreOrder: true
+            );
+
+        // MySQL reads the # as a comment, where ANSI kept it.
+        result
+            .GeneratedTrees.Single(tree => tree.FilePath.EndsWith("UserQueries.g.cs", StringComparison.Ordinal))
+            .ToString()
+            .ShouldContain("\"SELECT 1;\"");
+    }
+
+    [Fact]
+    public void Run_DialectMetadataOfOneFileChanged_ParsesOnlyThatFile()
+    {
+        var compilation = GeneratorHarness.CreateCompilation(Sources);
+        var users = new InMemoryAdditionalText(_users.Path, "-- name: GetUser\nSELECT 1; # c\n");
+        var driver = FirstRun(
+            compilation,
+            users,
+            new TestOptionsProvider(null, null, new Dictionary<string, string> { [_users.Path] = "mssql" })
+        );
+
+        var result = Run(
+            driver.WithUpdatedAnalyzerConfigOptions(
+                new TestOptionsProvider(null, null, new Dictionary<string, string> { [_users.Path] = "mysql" })
+            ),
+            compilation
+        );
+
+        AllReasons(result, TrackingNames.ProjectDialect).ShouldBe([IncrementalStepRunReason.Unchanged]);
+        Reasons<FileDialect>(result, TrackingNames.FileDialect, file => file.File.Path)
+            .ShouldBe(
+                new Dictionary<string, IncrementalStepRunReason>
+                {
+                    [_users.Path] = IncrementalStepRunReason.Modified,
+                    [_orders.Path] = IncrementalStepRunReason.Cached,
+                },
+                ignoreOrder: true
+            );
+        Reasons<ParsedSqlFile>(result, TrackingNames.ParsedFile, file => file.FileName)
+            .ShouldBe(
+                new Dictionary<string, IncrementalStepRunReason>
+                {
+                    ["Users.sql"] = IncrementalStepRunReason.Modified,
+                    ["Orders.sql"] = IncrementalStepRunReason.Cached,
+                },
+                ignoreOrder: true
+            );
+        Reasons<TypeOutput>(result, TrackingNames.TypeOutput, output => output.HintName)
+            .ShouldBe(
+                new Dictionary<string, IncrementalStepRunReason>
+                {
+                    ["App.Users.UserQueries.g.cs"] = IncrementalStepRunReason.Modified,
+                    ["App.Orders.OrderQueries.g.cs"] = IncrementalStepRunReason.Cached,
+                },
+                ignoreOrder: true
+            );
+    }
+
+    [Fact]
+    public void Run_DialectChangedToOneThatReadsTheFilesTheSameWay_EmitsNothingAgain()
+    {
+        var compilation = GeneratorHarness.CreateCompilation(Sources);
+        var driver = FirstRun(compilation);
+
+        var result = Run(
+            driver.WithUpdatedAnalyzerConfigOptions(new TestOptionsProvider(null, "postgres")),
+            compilation
+        );
+
+        // Every file was parsed again, under the new dialect, and gave an equal value.  So nothing after the parse
+        // ran.
+        AllReasons(result, TrackingNames.FileDialect)
+            .ShouldAllBe(reason => reason == IncrementalStepRunReason.Modified);
+        AllReasons(result, TrackingNames.ParsedFile).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
+        AllReasons(result, TrackingNames.TypeOutput).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
+        result.Diagnostics.ShouldBeEmpty();
+    }
+
     private GeneratorDriver FirstRun(Compilation compilation) => FirstRun(compilation, _users);
 
-    private GeneratorDriver FirstRun(Compilation compilation, InMemoryAdditionalText users)
+    private GeneratorDriver FirstRun(
+        Compilation compilation,
+        InMemoryAdditionalText users,
+        TestOptionsProvider? options = null
+    )
     {
         var driver = GeneratorHarness
-            .CreateDriver([users, _orders])
+            .CreateDriver([users, _orders], options: options)
             .RunGenerators(compilation, TestContext.Current.CancellationToken);
         driver.GetRunResult().Diagnostics.ShouldBeEmpty();
         return driver;
````

A note on reading these tests: the step with the tracking name `ParsedFile` is the `Select` after the parse.  When a file is parsed again and gives an equal value, that step is not run, and its reason is `Cached`.  `Run_DialectChangedToOneThatReadsTheFilesTheSameWay_EmitsNothingAgain` relies on it, as `Run_SqlFileEditedWithoutChangingItsQueries_EmitsNothingAgain` already does.

Patch `tests/SqlSource.Tests/Generation/SqlFileReaderTests.cs`:

<!-- patch:task5-reader-tests -->
````diff
diff --git a/tests/SqlSource.Tests/Generation/SqlFileReaderTests.cs b/tests/SqlSource.Tests/Generation/SqlFileReaderTests.cs
index b3af3d0..db4b87a 100644
--- a/tests/SqlSource.Tests/Generation/SqlFileReaderTests.cs
+++ b/tests/SqlSource.Tests/Generation/SqlFileReaderTests.cs
@@ -153,10 +153,53 @@ public class SqlFileReaderTests
 
     private static SqlSegment Literal(string text) => new(SqlSegmentKind.Literal, text);
 
-    private static ParsedSqlFile Read(string? text, string path = Path) =>
+    [Fact]
+    public void Read_Dialect_IsTheDialectTheFileIsParsedWith()
+    {
+        const string Text = "-- name: A\nSELECT 'a\\'b' # c\n";
+
+        Read(Text, dialect: SqlDialect.MySql)
+            .Queries.ShouldHaveSingleItem()
+            .Segments.ShouldBe(TestModels.Array(Literal("SELECT 'a\\'b'")));
+        Read(Text).Errors.ShouldHaveSingleItem().Descriptor.ShouldBe(SqlDiagnostics.UnterminatedQuote);
+    }
+
+    [Fact]
+    public void Read_InvalidDialectOfTheFile_IsCarriedAndTheFileIsStillParsed()
+    {
+        var file = Read("SELECT 1;\n", invalidDialect: "pgsql");
+
+        file.InvalidDialect.ShouldBe("pgsql");
+        file.Queries.Count.ShouldBe(1);
+        Read("SELECT 1;\n").InvalidDialect.ShouldBeNull();
+    }
+
+    [Fact]
+    public void Read_FileDialect_ReadsTheFileWithItsDialectAndCarriesItsInvalidValue()
+    {
+        var text = new InMemoryAdditionalText(Path, "SELECT 1 # c\n");
+
+        var file = SqlFileReader.Read(
+            new FileDialect(text, SqlDialect.MySql, "nope"),
+            "app/Repo/Users.sql",
+            TestContext.Current.CancellationToken
+        );
+
+        file.Queries.ShouldHaveSingleItem().Segments.ShouldBe(TestModels.Array(Literal("SELECT 1")));
+        file.InvalidDialect.ShouldBe("nope");
+    }
+
+    private static ParsedSqlFile Read(
+        string? text,
+        string path = Path,
+        SqlDialect dialect = SqlDialect.Ansi,
+        string? invalidDialect = null
+    ) =>
         SqlFileReader.Read(
             new InMemoryAdditionalText(path, text),
             SqlPath.Normalize(path)!,
+            dialect,
+            invalidDialect,
             TestContext.Current.CancellationToken
         );
 
````

Patch `tests/SqlSource.Tests/Diagnostics/SqlDiagnosticsTests.cs`: the message of `SQLSRC011` lists the names that `SqlDialectName` accepts.

<!-- patch:task5-diagnostics-tests -->
````diff
diff --git a/tests/SqlSource.Tests/Diagnostics/SqlDiagnosticsTests.cs b/tests/SqlSource.Tests/Diagnostics/SqlDiagnosticsTests.cs
index 6fdbafb..850d9e7 100644
--- a/tests/SqlSource.Tests/Diagnostics/SqlDiagnosticsTests.cs
+++ b/tests/SqlSource.Tests/Diagnostics/SqlDiagnosticsTests.cs
@@ -58,6 +58,12 @@ public class SqlDiagnosticsTests
             .ShouldBe(kinds.Select((_, index) => string.Create(CultureInfo.InvariantCulture, $"SQLSRC{101 + index}")));
     }
 
+    [Fact]
+    public void InvalidDialect_Message_ListsTheNamesThatAreAccepted() =>
+        SqlDiagnostics
+            .InvalidDialect.MessageFormat.ToString(CultureInfo.InvariantCulture)
+            .ShouldEndWith("SqlSourceDialect accepts " + SqlDialectName.Accepted + ".");
+
     [Fact]
     public void ForParseError_UndefinedKind_Throws() =>
         Should.Throw<ArgumentOutOfRangeException>(() => SqlDiagnostics.ForParseError((SqlParseErrorKind)(-1)));
````

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class '*DialectSettingTests'`
Expected: the build fails.  CS0246 and CS0103 name `DialectSetting` and `FileDialect`; CS0117 says `TrackingNames` has no `ProjectDialect` and `SqlDiagnostics` has no `InvalidDialect`; CS1501 says no overload of `Read` takes five arguments.

- [ ] **Step 3: Read the setting**

Create `src/SqlSource/Generation/DialectSetting.cs`:

<!-- file:src/SqlSource/Generation/DialectSetting.cs -->
````csharp
using Microsoft.CodeAnalysis.Diagnostics;
using SqlSource.Parsing;

namespace SqlSource.Generation;

/// <summary>
/// What MSBuild says about the dialect, for the project or for one file.  A <c>dialect=</c> directive in a file
/// comes before both.
/// </summary>
/// <param name="Dialect">
/// The dialect that is set, or null when none is.  <see cref="SqlDialect.Ansi" /> when the value is not valid.
/// </param>
/// <param name="InvalidValue">The value as written when it is not valid, and null otherwise.</param>
internal sealed record DialectSetting(SqlDialect? Dialect, string? InvalidValue)
{
    /// <summary>
    /// Where the compiler puts the <c>SqlSourceDialect</c> property of the project.  It is there only because
    /// <c>build/SqlSource.props</c> lists the property as a <c>CompilerVisibleProperty</c>.
    /// </summary>
    public const string PropertyName = "build_property.SqlSourceDialect";

    /// <summary>
    /// Where the compiler puts the <c>SqlSourceDialect</c> metadata of one <c>AdditionalFiles</c> item.  It is there
    /// only because <c>build/SqlSource.props</c> lists it as a <c>CompilerVisibleItemMetadata</c>.
    /// </summary>
    public const string MetadataName = "build_metadata.AdditionalFiles.SqlSourceDialect";

    private static readonly DialectSetting NotSet = new(null, null);

    /// <summary>Reads the project's property from the options that hold for the whole compilation.</summary>
    public static DialectSetting ReadProperty(AnalyzerConfigOptions globalOptions) =>
        globalOptions.TryGetValue(PropertyName, out var value) ? Parse(value) : NotSet;

    /// <summary>Reads a file's metadata from the options of that file.</summary>
    public static DialectSetting ReadMetadata(AnalyzerConfigOptions fileOptions) =>
        fileOptions.TryGetValue(MetadataName, out var value) ? Parse(value) : NotSet;

    public static DialectSetting Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return NotSet;
        }

        return SqlDialectName.TryParse(value, out var dialect)
            ? new DialectSetting(dialect, null)
            : new DialectSetting(SqlDialect.Ansi, value);
    }
}
````

Create `src/SqlSource/Generation/FileDialect.cs`:

<!-- file:src/SqlSource/Generation/FileDialect.cs -->
````csharp
using Microsoft.CodeAnalysis;
using SqlSource.Parsing;

namespace SqlSource.Generation;

/// <summary>
/// A <c>.sql</c> file with the dialect that MSBuild gives it: its own metadata, or else the project's property, or
/// else <see cref="SqlDialect.Ansi" />.
/// </summary>
/// <param name="File">
/// The file.  Compared by reference: the compiler hands out the same object until the file changes.
/// </param>
/// <param name="Dialect">The dialect the file is parsed with, unless a directive in it names another.</param>
/// <param name="InvalidValue">The file's metadata as written when it is not a dialect, and null otherwise.</param>
internal sealed record FileDialect(AdditionalText File, SqlDialect Dialect, string? InvalidValue)
{
    /// <summary>
    /// A file's own metadata comes before the project's property, whether or not it is valid: a file with metadata
    /// that is not a dialect is read as <see cref="SqlDialect.Ansi" />, and its value is reported.
    /// </summary>
    public static FileDialect Resolve(AdditionalText file, DialectSetting metadata, DialectSetting project) =>
        metadata.Dialect is { } dialect
            ? new FileDialect(file, dialect, metadata.InvalidValue)
            : new FileDialect(file, project.Dialect ?? SqlDialect.Ansi, null);
}
````

A file whose own metadata is not a dialect is read as `ansi`, not by the project's dialect: the file said something, and what it said is wrong.  The property's invalid value is not put on each file; the project's own setting carries it.

- [ ] **Step 4: Carry the dialect through the reader**

Replace `src/SqlSource/Generation/ParsedSqlFile.cs` with:

<!-- file:src/SqlSource/Generation/ParsedSqlFile.cs -->
````csharp
using SqlSource.Diagnostics;

namespace SqlSource.Generation;

/// <summary>
/// One <c>.sql</c> file that a type claims, parsed.
/// </summary>
/// <param name="NormalizedPath">The path in the form <see cref="SqlPath.Normalize" /> gives.</param>
/// <param name="FileName">The file's name with its extension.</param>
/// <param name="Queries">The file's queries, in file order.  Empty when <paramref name="Errors" /> is not.</param>
/// <param name="Errors">The file's problems, located in the file.</param>
/// <param name="InvalidDialect">
/// The <c>SqlSourceDialect</c> metadata of the file as written when it is not a dialect, and null otherwise.  It has
/// no position, so it does not travel in <paramref name="Errors" />.
/// </param>
internal sealed record ParsedSqlFile(
    string NormalizedPath,
    string FileName,
    EquatableArray<SqlQuery> Queries,
    EquatableArray<DiagnosticInfo> Errors,
    string? InvalidDialect = null
);
````

Replace `src/SqlSource/Generation/SqlFileReader.cs` with:

<!-- file:src/SqlSource/Generation/SqlFileReader.cs -->
````csharp
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Diagnostics;
using SqlSource.Parsing;

namespace SqlSource.Generation;

/// <summary>
/// Parses one <c>.sql</c> file and turns the result into the generator's models.
/// </summary>
internal static class SqlFileReader
{
    /// <summary>
    /// Parses <paramref name="file" /> with the dialect that MSBuild gives it.  A <c>dialect=</c> directive in the
    /// file replaces that dialect.
    /// </summary>
    public static ParsedSqlFile Read(FileDialect file, string normalizedPath, CancellationToken cancellationToken) =>
        Read(file.File, normalizedPath, file.Dialect, file.InvalidValue, cancellationToken);

    public static ParsedSqlFile Read(
        AdditionalText file,
        string normalizedPath,
        SqlDialect dialect,
        string? invalidDialect,
        CancellationToken cancellationToken
    )
    {
        // A file that cannot be read is parsed as empty, which reports that the query has no SQL.
        var text = file.GetText(cancellationToken) ?? SourceText.From(string.Empty);
        var fileName = SqlPath.GetFileName(file.Path);
        var result = SqlFileParser.Parse(text.ToString(), fileName, dialect);

        var errors = ImmutableArray.CreateBuilder<DiagnosticInfo>(result.Errors.Count);
        foreach (var error in result.Errors)
        {
            errors.Add(
                new DiagnosticInfo(
                    SqlDiagnostics.ForParseError(error.Kind),
                    LocationInfo.From(file.Path, text, error.Span),
                    error.Arguments
                )
            );
        }

        var queries = ImmutableArray.CreateBuilder<SqlQuery>(result.Blocks.Count);
        foreach (var block in result.Blocks)
        {
            queries.Add(
                new SqlQuery(
                    block.Name,
                    LocationInfo.From(file.Path, text, block.NameSpan),
                    block.Summary,
                    block.Segments,
                    block.TokenValidation
                )
            );
        }

        return new ParsedSqlFile(
            normalizedPath,
            fileName,
            new EquatableArray<SqlQuery>(queries.ToImmutable()),
            new EquatableArray<DiagnosticInfo>(errors.ToImmutable()),
            invalidDialect
        );
    }
}
````

Replace `src/SqlSource/Generation/TrackingNames.cs` with:

<!-- file:src/SqlSource/Generation/TrackingNames.cs -->
````csharp
namespace SqlSource.Generation;

/// <summary>
/// The names of the pipeline's steps.  A test reads a step's run reasons by name to check that it was cached.
/// </summary>
internal static class TrackingNames
{
    public const string TargetTypes = nameof(TargetTypes);

    public const string SqlPaths = nameof(SqlPaths);

    public const string SupportedFramework = nameof(SupportedFramework);

    public const string TypeFiles = nameof(TypeFiles);

    public const string ClaimedPaths = nameof(ClaimedPaths);

    public const string ProjectDialect = nameof(ProjectDialect);

    public const string FileDialect = nameof(FileDialect);

    public const string ParsedFile = nameof(ParsedFile);

    public const string ParsedFiles = nameof(ParsedFiles);

    public const string AmbiguousHintNames = nameof(AmbiguousHintNames);

    public const string TypeQueries = nameof(TypeQueries);

    public const string TokenValidation = nameof(TokenValidation);

    public const string TypeOutput = nameof(TypeOutput);
}
````

- [ ] **Step 5: Add `SQLSRC011`**

Patch `src/SqlSource/Diagnostics/SqlDiagnostics.cs`:

<!-- patch:task5-diagnostics -->
````diff
diff --git a/src/SqlSource/Diagnostics/SqlDiagnostics.cs b/src/SqlSource/Diagnostics/SqlDiagnostics.cs
index 8048906..c7e318a 100644
--- a/src/SqlSource/Diagnostics/SqlDiagnostics.cs
+++ b/src/SqlSource/Diagnostics/SqlDiagnostics.cs
@@ -129,6 +129,18 @@ internal static class SqlDiagnostics
         customTags: WellKnownDiagnosticTags.NotConfigurable
     );
 
+    public static readonly DiagnosticDescriptor InvalidDialect = new(
+        id: "SQLSRC011",
+        title: "SqlSourceDialect is not valid",
+        messageFormat: "'{0}' is not a SQL dialect.  SqlSourceDialect accepts ansi, mssql, postgres, mysql, mariadb, "
+            + "sqlite and oracle.",
+        category: Category,
+        defaultSeverity: DiagnosticSeverity.Error,
+        isEnabledByDefault: true,
+        helpLinkUri: HelpLinkBase + "sqlsrc011",
+        customTags: WellKnownDiagnosticTags.NotConfigurable
+    );
+
     public static readonly DiagnosticDescriptor UnterminatedQuote = new(
         id: "SQLSRC101",
         title: "Quote is not closed",
@@ -312,6 +324,7 @@ internal static class SqlDiagnostics
             DuplicateQueryName,
             QueryNamedLikeContainingType,
             InvalidTokenValidation,
+            InvalidDialect,
             UnterminatedQuote,
             UnterminatedBlockComment,
             InvalidName,
````

Patch `src/SqlSource/AnalyzerReleases.Unshipped.md`:

<!-- patch:task5-analyzer-releases -->
````diff
diff --git a/src/SqlSource/AnalyzerReleases.Unshipped.md b/src/SqlSource/AnalyzerReleases.Unshipped.md
index 71bb245..a1ea2ad 100644
--- a/src/SqlSource/AnalyzerReleases.Unshipped.md
+++ b/src/SqlSource/AnalyzerReleases.Unshipped.md
@@ -15,6 +15,7 @@ SQLSRC007 | SqlSource | Error | Type has a member named Sql
 SQLSRC008 | SqlSource | Error | Query name is used in two files
 SQLSRC009 | SqlSource | Error | Query is named like its containing type
 SQLSRC010 | SqlSource | Error | SqlSourceTokenValidation is not valid
+SQLSRC011 | SqlSource | Error | SqlSourceDialect is not valid
 SQLSRC101 | SqlSource | Error | Quote is not closed
 SQLSRC102 | SqlSource | Error | Comment is not closed
 SQLSRC103 | SqlSource | Error | Query name is not valid
````

Patch `docs/diagnostics.md`:

<!-- patch:task5-diagnostics-document -->
````diff
diff --git a/docs/diagnostics.md b/docs/diagnostics.md
index b8b5c2f..f596c52 100644
--- a/docs/diagnostics.md
+++ b/docs/diagnostics.md
@@ -14,6 +14,7 @@ Every problem SqlSource finds is a build error, and none can be turned off or ma
 | [SQLSRC008](#sqlsrc008) | Query name is used in two files |
 | [SQLSRC009](#sqlsrc009) | Query is named like its containing type |
 | [SQLSRC010](#sqlsrc010) | SqlSourceTokenValidation is not valid |
+| [SQLSRC011](#sqlsrc011) | SqlSourceDialect is not valid |
 | [SQLSRC101](#sqlsrc101) | Quote is not closed |
 | [SQLSRC102](#sqlsrc102) | Comment is not closed |
 | [SQLSRC103](#sqlsrc103) | Query name is not valid |
@@ -153,6 +154,29 @@ Set it to `false` to turn validation off for the project, or remove it to keep t
 
 The compiler hands a generator only the part of a value before the first `;` or `#`.  So `false;true` is read as `false` and is not reported, and `off;false` is reported as `off`.
 
+## SQLSRC011
+
+**SqlSourceDialect is not valid**
+
+`SqlSourceDialect` says which database a project's SQL is written for, so that SqlSource finds its comments and strings by that database's rules.  It is set as an MSBuild property for the project, or as metadata on the `AdditionalFiles` item of a `.sql` file, and one of the two has a value that is not a dialect.  The message quotes the value.
+
+The names are `ansi`, `mssql`, `postgres`, `mysql`, `mariadb`, `sqlite` and `oracle`, in any case.  `sqlserver` and `tsql` also mean `mssql`, and `postgresql` also means `postgres`.
+
+```xml
+<PropertyGroup>
+    <SqlSourceDialect>pgsql</SqlSourceDialect>
+</PropertyGroup>
+<ItemGroup>
+    <AdditionalFiles Update="Reporting/**/*.sql" SqlSourceDialect="sql server" />
+</ItemGroup>
+```
+
+The error has no file and line, because the compiler does not tell a generator where a property or the metadata of an item was set: look in the project file, in `Directory.Build.props`, and at a `-p:` argument of the build command.  It is reported once for each wrong value, however many files have it, and only for a `.sql` file that a type uses.
+
+Correct the name, or remove the setting to get the default, `ansi`.  While a value is wrong the files it covers are read as `ansi`; a file whose own metadata is wrong does not fall back to the project's property.
+
+The compiler hands a generator only the part of a value before the first `;` or `#`, as for [SQLSRC010](#sqlsrc010).
+
 ## SQLSRC101
 
 **Quote is not closed**
````

- [ ] **Step 6: Wire the pipeline**

Replace `src/SqlSource/SqlSourceGenerator.cs` with:

<!-- file:src/SqlSource/SqlSourceGenerator.cs -->
````csharp
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Diagnostics;
using SqlSource.Generation;

namespace SqlSource;

/// <summary>
/// Generates C# source for SQL queries.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SqlSourceGenerator : IIncrementalGenerator
{
    // The generated code targets .NET 8 and later.  This method first appeared there, and a generated method calls
    // it to check an argument, so its presence is the test.
    private const string FloorType = "System.ArgumentException";

    private const string FloorMember = "ThrowIfNullOrWhiteSpace";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static output =>
            output.AddSource(AttributeSource.HintName, SourceText.From(AttributeSource.Text, Encoding.UTF8))
        );

        var targetTypes = context
            .SyntaxProvider.ForAttributeWithMetadataName(
                AttributeSource.AttributeMetadataName,
                static (node, _) => TargetTypeReader.IsCandidate(node),
                TargetTypeReader.Read
            )
            .Where(static type => type is not null)
            .Select(static (type, _) => type!)
            .WithTrackingName(TrackingNames.TargetTypes);

        var sqlFiles = context.AdditionalTextsProvider.Where(static file => SqlPath.IsSqlFile(file.Path));

        // The paths alone, so that resolving a type's Path does not depend on the text of any file.
        var sqlPaths = sqlFiles
            .Select(static (file, _) => SqlPath.Normalize(file.Path))
            .Collect()
            .Select(static (paths, _) => ToSortedSet(paths.RemoveAll(static path => path is null)!))
            .WithTrackingName(TrackingNames.SqlPaths);

        var isSupportedFramework = context
            .CompilationProvider.Select(
                static (compilation, _) =>
                    compilation.GetTypeByMetadataName(FloorType)?.GetMembers(FloorMember).IsEmpty == false
            )
            .WithTrackingName(TrackingNames.SupportedFramework);

        var typeFiles = targetTypes
            .Combine(sqlPaths)
            .Combine(isSupportedFramework)
            .Select(static (input, _) => PathResolver.Resolve(input.Left.Left, input.Left.Right, input.Right))
            .WithTrackingName(TrackingNames.TypeFiles);

        var claimedPaths = typeFiles
            .SelectMany(static (type, _) => type.Files)
            .Collect()
            .Select(static (paths, _) => ToSortedSet(paths))
            .WithTrackingName(TrackingNames.ClaimedPaths);

        // The project's dialect, which is the same value until the property itself changes.
        var projectDialect = context
            .AnalyzerConfigOptionsProvider.Select(
                static (options, _) => DialectSetting.ReadProperty(options.GlobalOptions)
            )
            .WithTrackingName(TrackingNames.ProjectDialect);

        // Each file with the dialect that MSBuild gives it.  The dialect is resolved here, before the parse, so that
        // a change to the property parses only the files that fall back to it.
        var fileDialects = sqlFiles
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select(
                static (input, _) =>
                    (File: input.Left, Metadata: DialectSetting.ReadMetadata(input.Right.GetOptions(input.Left)))
            )
            .Combine(projectDialect)
            .Select(static (input, _) => FileDialect.Resolve(input.Left.File, input.Left.Metadata, input.Right))
            .WithTrackingName(TrackingNames.FileDialect);

        // A file that no type claims is never read.
        var parsedFiles = fileDialects
            .Combine(claimedPaths)
            .Select(
                static (input, cancellationToken) =>
                    SqlPath.Normalize(input.Left.File.Path) is { } path && SqlPath.Contains(input.Right, path)
                        ? SqlFileReader.Read(input.Left, path, cancellationToken)
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

        // Two types whose files would have names equal ignoring case.  Almost always none, so this value almost
        // never changes and costs the steps after it nothing.
        var ambiguousHintNames = typeFiles
            .Select(static (type, _) => HintName.Create(type.Type))
            .Collect()
            .Select(static (names, _) => HintName.FindAmbiguous(names))
            .WithTrackingName(TrackingNames.AmbiguousHintNames);

        // The project's setting, which is the same value until the property itself changes.
        var tokenValidation = context
            .AnalyzerConfigOptionsProvider.Select(
                static (options, _) => TokenValidationSetting.Read(options.GlobalOptions)
            )
            .WithTrackingName(TrackingNames.TokenValidation);

        // Not located: the compiler does not say where an MSBuild property was set.
        context.RegisterSourceOutput(
            tokenValidation,
            static (output, setting) =>
            {
                if (setting.InvalidValue is { } value)
                {
                    output.ReportDiagnostic(
                        Diagnostic.Create(SqlDiagnostics.InvalidTokenValidation, Location.None, value)
                    );
                }
            }
        );

        // The setting joins after a type's queries are selected, so that it never reaches the parse of a file.
        var typeOutputs = typeFiles
            .Combine(parsedFiles)
            .Combine(ambiguousHintNames)
            .Select(static (input, _) => SelectFiles(input.Left.Left, input.Left.Right, input.Right))
            .WithTrackingName(TrackingNames.TypeQueries)
            .Combine(tokenValidation.Select(static (setting, _) => setting.Validate))
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

    // Distinct ignoring case and in the order of SqlPath.Comparer, which is also the order of a type's members.
    private static EquatableArray<string> ToSortedSet(ImmutableArray<string> paths) =>
        new(paths.Distinct(SqlPath.Comparer).OrderBy(static path => path, SqlPath.Comparer).ToImmutableArray());

    // One file for each path, the first the project lists, in the order of SqlPath.Comparer.
    private static EquatableArray<ParsedSqlFile> ToSortedFiles(ImmutableArray<ParsedSqlFile> files) =>
        new(
            files
                .GroupBy(static file => file.NormalizedPath, SqlPath.Comparer)
                .Select(static group => group.First())
                .OrderBy(static file => file.NormalizedPath, SqlPath.Comparer)
                .ToImmutableArray()
        );

    // Each value once, in ordinal order, so that the errors of a build do not depend on the order of its files.
    private static SortedSet<string> FindInvalidDialects(EquatableArray<ParsedSqlFile> files, DialectSetting project)
    {
        var values = new SortedSet<string>(StringComparer.Ordinal);
        if (project.InvalidValue is { } property)
        {
            _ = values.Add(property);
        }

        foreach (var file in files)
        {
            if (file.InvalidDialect is { } metadata)
            {
                _ = values.Add(metadata);
            }
        }

        return values;
    }

    private static TypeQueries SelectFiles(
        TypeFiles type,
        EquatableArray<ParsedSqlFile> parsedFiles,
        EquatableArray<string> ambiguousHintNames
    )
    {
        var files = ImmutableArray.CreateBuilder<ParsedSqlFile>(type.Files.Count);
        var index = 0;

        // Both lists are in the same order, so one pass over the parsed files finds each of the type's.
        foreach (var path in type.Files)
        {
            while (index < parsedFiles.Count && SqlPath.Comparer.Compare(parsedFiles[index].NormalizedPath, path) < 0)
            {
                index++;
            }

            if (index < parsedFiles.Count && SqlPath.Comparer.Equals(parsedFiles[index].NormalizedPath, path))
            {
                files.Add(parsedFiles[index]);
            }
        }

        return new TypeQueries(
            type,
            new EquatableArray<ParsedSqlFile>(files.ToImmutable()),
            HintName.MakeUnique(HintName.Create(type.Type), ambiguousHintNames)
        );
    }
}
````

What changed:

- `projectDialect` and `fileDialects` are new steps.  The tuple of a file and its metadata compares the file by reference and the setting by value, so the `FileDialect` step is taken from the previous run when neither changed, whatever else in the options did.
- `parsedFiles` starts from `fileDialects`, not from `sqlFiles`.
- The step that reports file errors also takes the project's setting, and reports `SQLSRC011` once for each distinct invalid value among the property and the claimed files.  `FindInvalidDialects` sorts them, so that the errors of a build do not depend on the order of its files.

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class '*DialectSettingTests' --filter-class 'SqlSource.Tests.Generator.DialectTests' --filter-class '*CachingTests' --filter-class '*SqlFileReaderTests' --filter-class '*SqlDiagnosticsTests'`
Expected: total 80, failed 0.

Run: `dotnet test --solution SqlSource.slnx`
Expected: total 1278, failed 0.  `SqlSource.Tests.RoslynFloor` reports 174: `DialectTests` and the new caching tests pass on Roslyn 4.8.0 too.

- [ ] **Step 8: Format and validate**

Run: `./format.sh`

Run: `./pre-commit-validation.sh`
Expected: every step `passed`.

- [ ] **Step 9: Commit**

```bash
git add src/SqlSource docs/diagnostics.md tests/SqlSource.Tests/Generation tests/SqlSource.Tests/Generator tests/SqlSource.Tests/Diagnostics
```

```bash
git commit -m "Take a file's dialect from MSBuild and report one that is not valid

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: The package's MSBuild files, end to end

The generator reads the property and the metadata, but a real build does not hand them over yet: a generator sees only what `build/SqlSource.props` lists.  The test project imports the real `.props` and `.targets`, so its `EndToEnd/` folder can show all three ways of setting a dialect working in a real build.

**Files:**
- Modify: `src/SqlSource/build/SqlSource.props`, `src/SqlSource/build/SqlSource.targets`
- Modify: `tests/SqlSource.Tests/SqlSource.Tests.csproj`
- Create: `tests/SqlSource.Tests/EndToEnd/DialectQueries.cs`, `tests/SqlSource.Tests/EndToEnd/Dialects/ByProperty.sql`, `ByMetadata.sql`, `ByDirective.sql`
- Test: `tests/SqlSource.Tests/EndToEnd/EndToEndTests.cs`, `tests/SqlSource.Tests/Package/BuildFileTests.cs`
- Create: `docs/tech-debt/TD-0013-every-sql-file-gets-a-section-in-the-compiler-configuration.md`
- Modify: `docs/tech-debt/README.md`

**Interfaces:**
- Consumes: `DialectSetting.PropertyName` and `DialectSetting.MetadataName` (Task 5), by their values.
- Produces: `CompilerVisibleProperty` for `SqlSourceDialect`; `CompilerVisibleItemMetadata` for `AdditionalFiles` with `MetadataName="SqlSourceDialect"`; both values trimmed before the compiler reads them.

- [ ] **Step 1: Write the failing tests**

Create the folder `tests/SqlSource.Tests/EndToEnd/Dialects/` with three files.  Each holds one query that ANSI misreads.

`ByProperty.sql`:

<!-- file:tests/SqlSource.Tests/EndToEnd/Dialects/ByProperty.sql -->
````sql
-- The project's dialect is postgres, which reads the second line as part of the escape string.
SELECT E'it'
    '\'s -- not a comment' AS note; -- a comment
````

`ByMetadata.sql`:

<!-- file:tests/SqlSource.Tests/EndToEnd/Dialects/ByMetadata.sql -->
````sql
-- The item of this file has the metadata SqlSourceDialect="mysql".
SELECT 'it\'s' AS note, 5--3 AS eight; # a comment
````

`ByDirective.sql`:

<!-- file:tests/SqlSource.Tests/EndToEnd/Dialects/ByDirective.sql -->
````sql
/* The directive is below this comment, which the project's dialect reads. */
-- SqlSource: dialect=mssql
SELECT [it's] FROM #orders; -- a comment
````

Create `tests/SqlSource.Tests/EndToEnd/DialectQueries.cs`:

<!-- file:tests/SqlSource.Tests/EndToEnd/DialectQueries.cs -->
````csharp
namespace SqlSource.Tests.EndToEnd;

// One folder whose three files get their dialect in the three ways there are: from the project's property, from the
// metadata of the file's item, and from a directive in the file.
[SqlQueries(Path = "Dialects", Mode = SqlQueriesMode.Direct)]
internal static partial class DialectQueries;
````

Patch `tests/SqlSource.Tests/SqlSource.Tests.csproj`.  Both values are written over several lines on purpose: that is the form that would reach the compiler empty, and the package has to make it work.

<!-- patch:task6-test-project -->
````diff
diff --git a/tests/SqlSource.Tests/SqlSource.Tests.csproj b/tests/SqlSource.Tests/SqlSource.Tests.csproj
index d0eed3b..dec3881 100644
--- a/tests/SqlSource.Tests/SqlSource.Tests.csproj
+++ b/tests/SqlSource.Tests/SqlSource.Tests.csproj
@@ -16,7 +16,17 @@
         <SqlSourceTokenValidation>
             false
         </SqlSourceTokenValidation>
+        <SqlSourceDialect>
+            postgres
+        </SqlSourceDialect>
     </PropertyGroup>
+    <ItemGroup>
+        <AdditionalFiles Update="EndToEnd/Dialects/ByMetadata.sql">
+            <SqlSourceDialect>
+                mysql
+            </SqlSourceDialect>
+        </AdditionalFiles>
+    </ItemGroup>
     <ItemGroup>
         <PackageReference Include="GitHubActionsTestLogger" />
         <PackageReference Include="Microsoft.CodeAnalysis.CSharp" VersionOverride="5.9.0" />
````

Patch `tests/SqlSource.Tests/EndToEnd/EndToEndTests.cs`:

<!-- patch:task6-end-to-end-tests -->
````diff
diff --git a/tests/SqlSource.Tests/EndToEnd/EndToEndTests.cs b/tests/SqlSource.Tests/EndToEnd/EndToEndTests.cs
index 0c5816b..8e3905c 100644
--- a/tests/SqlSource.Tests/EndToEnd/EndToEndTests.cs
+++ b/tests/SqlSource.Tests/EndToEnd/EndToEndTests.cs
@@ -84,6 +84,22 @@ public class EndToEndTests
     public void ProjectWithValidationOff_QueryWithTheDirective_ReturnsTheSqlForArgumentsWithText() =>
         TokenQueries.Checked("users", "id = @id").ShouldBe("SELECT id FROM users WHERE id = @id;");
 
+    // SqlSource.Tests.csproj sets the SqlSourceDialect property to postgres, on a line of its own.  ANSI would end the
+    // string at the quote after the backslash and take the rest of the line for a comment.
+    [Fact]
+    public void ProjectWithADialect_FileWithoutItsOwn_IsReadByTheDialectOfTheProject() =>
+        DialectQueries.ByProperty.ShouldBe("SELECT E'it'\n    '\\'s -- not a comment' AS note;");
+
+    // The item of ByMetadata.sql has SqlSourceDialect metadata, written over several lines.  That the file is read as
+    // MySQL shows the metadata reaching the generator through the MSBuild files the package ships, trimmed.
+    [Fact]
+    public void ProjectWithADialect_FileWithMetadata_IsReadByTheDialectOfItsItem() =>
+        DialectQueries.ByMetadata.ShouldBe("SELECT 'it\\'s' AS note, 5--3 AS eight;");
+
+    [Fact]
+    public void ProjectWithADialect_FileWithADirective_IsReadByTheDialectItNames() =>
+        DialectQueries.ByDirective.ShouldBe("SELECT [it's] FROM #orders;");
+
     [Fact]
     public void Method_Call_AllocatesTheStringItReturnsAndNothingElse()
     {
@@ -115,6 +131,7 @@ public class EndToEndTests
     [InlineData(typeof(Repository<>))]
     [InlineData(typeof(Outer.Counts))]
     [InlineData(typeof(TokenQueries))]
+    [InlineData(typeof(DialectQueries))]
     public void Attribute_IsNotInTheMetadataOfTheTypesThatCarryIt(Type type) =>
         type.GetCustomAttributesData()
             .Select(attribute => attribute.AttributeType.FullName)
````

Patch `tests/SqlSource.Tests/Package/BuildFileTests.cs`.  `Props_TokenValidationProperty_ReachesTheCompilerInEveryProject` expected one `CompilerVisibleProperty`; it becomes a test of both.  The others read what the test project cannot show: that nothing is under a condition, and that each value is trimmed.

<!-- patch:task6-build-file-tests -->
````diff
diff --git a/tests/SqlSource.Tests/Package/BuildFileTests.cs b/tests/SqlSource.Tests/Package/BuildFileTests.cs
index 20d6a34..5d6eb45 100644
--- a/tests/SqlSource.Tests/Package/BuildFileTests.cs
+++ b/tests/SqlSource.Tests/Package/BuildFileTests.cs
@@ -24,15 +24,57 @@ public partial class BuildFileTests
     private static readonly XDocument Targets = Load("SqlSource.targets");
 
     [Fact]
-    public void Props_TokenValidationProperty_ReachesTheCompilerInEveryProject()
+    public void Props_PropertiesOfThePackage_ReachTheCompilerInEveryProject()
     {
-        var item = Props.Descendants("CompilerVisibleProperty").ShouldHaveSingleItem();
+        var items = Props.Descendants("CompilerVisibleProperty").ToList();
 
-        item.Attribute("Include").ShouldNotBeNull().Value.ShouldBe("SqlSourceTokenValidation");
+        items
+            .Select(item => item.Attribute("Include").ShouldNotBeNull().Value)
+            .ShouldBe(["SqlSourceTokenValidation", "SqlSourceDialect"]);
 
         // A project that sets SqlSourceIncludeFiles to false lists its own .sql files, and still needs the
-        // property.  So nothing may put a condition on the item.
-        item.AncestorsAndSelf().SelectMany(element => element.Attributes("Condition")).ShouldBeEmpty();
+        // properties.  So nothing may put a condition on the items.
+        items.SelectMany(ConditionsAround).ShouldBeEmpty();
+    }
+
+    [Fact]
+    public void Props_DialectMetadataOfASqlFile_ReachesTheCompilerInEveryProject()
+    {
+        var item = Props.Descendants("CompilerVisibleItemMetadata").ShouldHaveSingleItem();
+
+        item.Attribute("Include").ShouldNotBeNull().Value.ShouldBe("AdditionalFiles");
+        item.Attribute("MetadataName").ShouldNotBeNull().Value.ShouldBe("SqlSourceDialect");
+        ConditionsAround(item).ShouldBeEmpty();
+    }
+
+    // The compiler reads a property, and the metadata of an item, from a file with one line for each.  A value on a
+    // line of its own would arrive empty.  SqlSource.Tests.csproj writes both of its dialects that way, so the
+    // end-to-end tests show the trimming at work; this pins that each value has it.
+    [Theory]
+    [InlineData("SqlSourceTokenValidation")]
+    [InlineData("SqlSourceDialect")]
+    public void Targets_PropertyOfThePackage_IsTrimmed(string property)
+    {
+        var element = Targets.Descendants(property).ShouldHaveSingleItem();
+
+        element.Value.ShouldBe($"$({property}.Trim())");
+        element
+            .Parent.ShouldNotBeNull()
+            .Attribute("Condition")
+            .ShouldNotBeNull()
+            .Value.ShouldBe($"'$({property})' != ''");
+    }
+
+    [Fact]
+    public void Targets_DialectMetadataOfEverySqlFile_IsTrimmed()
+    {
+        var item = Targets.Descendants("AdditionalFiles").ShouldHaveSingleItem();
+
+        item.Attribute("Update").ShouldNotBeNull().Value.ShouldBe("@(AdditionalFiles)");
+        item.Attribute("SqlSourceDialect")
+            .ShouldNotBeNull()
+            .Value.ShouldBe("$([System.String]::Copy('%(SqlSourceDialect)').Trim())");
+        ConditionsAround(item).ShouldBeEmpty();
     }
 
     [Fact]
@@ -65,6 +107,9 @@ public partial class BuildFileTests
         names.ShouldAllBe(name => name.StartsWith(Prefix, StringComparison.Ordinal));
     }
 
+    private static IEnumerable<XAttribute> ConditionsAround(XElement element) =>
+        element.AncestorsAndSelf().SelectMany(ancestor => ancestor.Attributes("Condition"));
+
     private static XDocument Load(string file) => XDocument.Load(Path.Combine(AppContext.BaseDirectory, "build", file));
 
     private static IEnumerable<string> PropertiesSet(XDocument document) =>
````

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build tests/SqlSource.Tests`
Expected: the build fails with `SQLSRC101` at `EndToEnd/Dialects/ByMetadata.sql(2,14)`.  Nothing hands the dialects to the compiler yet, so the file is read as ANSI, which does not take the backslash for an escape.

- [ ] **Step 3: Show the property and the metadata to the compiler**

Replace `src/SqlSource/build/SqlSource.props` with:

<!-- file:src/SqlSource/build/SqlSource.props -->
````xml
<Project>
    <!--
        Hands every .sql file of the project to the compiler, which is the only way a source generator can see one.
        NuGet imports this file into each project that references the package, before the project's own content, so a
        project can remove files with <AdditionalFiles Remove="..." />.  A project that sets SqlSourceIncludeFiles to
        false lists its .sql files as AdditionalFiles itself.

        Items are evaluated after every property, so DefaultItemExcludes already holds the output and intermediate
        folders here, whatever the project sets them to.
    -->
    <ItemGroup Condition="'$(SqlSourceIncludeFiles)' != 'false'">
        <AdditionalFiles Include="**/*.sql" Exclude="$(DefaultItemExcludes);$(DefaultExcludesInProjectFolder)" />
    </ItemGroup>
    <!--
        A source generator sees only the MSBuild properties and the item metadata that are listed here.
        SqlSourceTokenValidation turns the argument checks of the generated methods off for the project when it is
        false.  SqlSourceDialect names the database whose rules the .sql files are read by: as a property for the
        project, and as metadata of an AdditionalFiles item for one file or for the files of a glob.  None of this is
        under the condition above: a project that lists its own .sql files still needs it.
    -->
    <ItemGroup>
        <CompilerVisibleProperty Include="SqlSourceTokenValidation" />
        <CompilerVisibleProperty Include="SqlSourceDialect" />
        <CompilerVisibleItemMetadata Include="AdditionalFiles" MetadataName="SqlSourceDialect" />
    </ItemGroup>
</Project>
````

Replace `src/SqlSource/build/SqlSource.targets` with:

<!-- file:src/SqlSource/build/SqlSource.targets -->
````xml
<Project>
    <!--
        NuGet imports this file into each project that references the package, after the project's own content.

        The compiler reads SqlSourceTokenValidation and SqlSourceDialect from a file that the build writes with one
        line for each property.  A value on a line of its own, as an element written over several lines has it, would
        arrive empty there and be taken for the default.  Trimming it here makes that form mean what it says.
    -->
    <PropertyGroup Condition="'$(SqlSourceTokenValidation)' != ''">
        <SqlSourceTokenValidation>$(SqlSourceTokenValidation.Trim())</SqlSourceTokenValidation>
    </PropertyGroup>
    <PropertyGroup Condition="'$(SqlSourceDialect)' != ''">
        <SqlSourceDialect>$(SqlSourceDialect.Trim())</SqlSourceDialect>
    </PropertyGroup>
    <ItemGroup>
        <AdditionalFiles
            Update="@(AdditionalFiles)"
            SqlSourceDialect="$([System.String]::Copy('%(SqlSourceDialect)').Trim())"
        />
    </ItemGroup>
</Project>
````

About the item update:

- It is in an `ItemGroup` outside any target, so MSBuild applies it when it evaluates the project.  A design-time build in an IDE evaluates the project the same way, so the IDE sees the trimmed value too.
- `$([System.String]::Copy('%(SqlSourceDialect)').Trim())` is the usual way to call a string method on item metadata: the property function is evaluated for each item, after the metadata is put in.
- It touches every `AdditionalFiles` item, and gives one without the metadata an empty value, which is what it has in the compiler's eyes anyway.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class '*EndToEndTests' --filter-class '*BuildFileTests'`
Expected: total 33, failed 0.

Run: `grep -A1 'Dialects/' tests/SqlSource.Tests/obj/Debug/net10.0/SqlSource.Tests.GeneratedMSBuildEditorConfig.editorconfig`
Expected: the line after `Dialects/ByMetadata.sql]` is `build_metadata.AdditionalFiles.SqlSourceDialect = mysql`, on one line, and the other two files have an empty value.

Run: `dotnet test --solution SqlSource.slnx`
Expected: total 1286, failed 0.  The other files of `EndToEnd/` give the same SQL as before: the project's dialect is `postgres` now, and it reads them as ANSI did.

- [ ] **Step 5: Record what the declaration costs**

The SDK writes a section for every `AdditionalFiles` item into the configuration file it generates for the compiler, used by a type or not.  That is a cost this change adds and does not remove, so root `AGENTS.md` rule 2 wants it written down.

Create `docs/tech-debt/TD-0013-every-sql-file-gets-a-section-in-the-compiler-configuration.md`:

<!-- file:docs/tech-debt/TD-0013-every-sql-file-gets-a-section-in-the-compiler-configuration.md -->
````markdown
# TD-0013 - Every `.sql` file gets a section in the compiler's configuration file

## Problem

[`build/SqlSource.props`](../../src/SqlSource/build/SqlSource.props) lists `SqlSourceDialect` as a `CompilerVisibleItemMetadata` of `AdditionalFiles`, so that a file can have its own dialect.  The .NET SDK then writes a section for every `AdditionalFiles` item into the configuration file it generates for the compiler, with an empty value where the metadata is not set.  That is every `.sql` file of the project, including the ones no type uses.

[`build/SqlSource.targets`](../../src/SqlSource/build/SqlSource.targets) also updates every `AdditionalFiles` item, to trim the metadata.

## Why it exists

Item metadata is the only way the compiler hands a generator a value for one file.  The SDK decides what it writes, and it does not leave out an empty value.

## Impact

A project with 5,000 `.sql` files that no type uses, measured with .NET SDK 10.0.401 on the day this was written: a build that compiles took 0.86 s before the declaration, 1.13 s with it, and 1.21 s with the trim as well.  The generated configuration file grew from 1 KB to 1.1 MB.  That is about 0.07 ms for each file.

A project of ordinary size does not notice.  One with thousands of migration scripts pays a third of a second on each build, and can avoid it by leaving the scripts out, as `README.md` already advises: `<AdditionalFiles Remove="Migrations/**/*.sql" />`.

## Proposed fix

None is known that keeps the dialect of a file in the project file.  If the cost is reported, document the `Remove` line next to the dialect metadata in `README.md`, and consider a property that turns the metadata off for a project that does not use it.

## Trigger

A user reports slow builds in a project with many `.sql` files.
````

Patch `docs/tech-debt/README.md`: the row, and `Next id`.

<!-- patch:task6-tech-debt-table -->
````diff
diff --git a/docs/tech-debt/README.md b/docs/tech-debt/README.md
index 5f5dcb2..18ffd00 100644
--- a/docs/tech-debt/README.md
+++ b/docs/tech-debt/README.md
@@ -2,7 +2,7 @@
 
 Problems that were identified and not resolved, and intentional choices known to be sub-optimal.  Each active item is fully documented in its own file, linked from the ID column.  When an item is resolved, its row is removed and its file deleted in the same PR as the fix.
 
-Next id: `TD-0013`
+Next id: `TD-0014`
 
 ## Active items
 
@@ -18,6 +18,7 @@ Next id: `TD-0013`
 | [TD-0010](TD-0010-path-resolution-scales-with-types-times-files.md) | Open | 2026-10-06 | Low | Resolving paths costs time and memory in proportion to the number of types times the number of `.sql` files |
 | [TD-0011](TD-0011-language-version-is-not-checked.md) | Open | 2026-10-06 | Low | A project that sets `LangVersion` below 12 gets compiler errors in generated code, not a diagnostic |
 | [TD-0012](TD-0012-token-method-shapes-are-compiled-but-not-run.md) | Open | 2026-10-06 | Low | A token-only query and one with more than seven tokens are compiled in tests and never run, and awkward token names are compiled as C# 12 only |
+| [TD-0013](TD-0013-every-sql-file-gets-a-section-in-the-compiler-configuration.md) | Open | 2026-10-06 | Low | Every `.sql` file of a project, used or not, adds a section to the configuration file the SDK writes for the compiler |
 
 ## Columns
 
````

If `Next id` is not `TD-0013` when you come to this step, another branch took the id.  Use the id it shows, rename the file and the row to match, and add one to it.

- [ ] **Step 6: Format and validate**

Run: `./format.sh`

Run: `./pre-commit-validation.sh`
Expected: every step `passed`.  The `package` step lists `build/SqlSource.props` and `build/SqlSource.targets`.

- [ ] **Step 7: Commit**

```bash
git add src/SqlSource/build tests/SqlSource.Tests docs/tech-debt
```

```bash
git commit -m "Hand the dialect to the compiler from the package

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Documentation

Root `AGENTS.md` rule 3: a change that makes a document wrong updates it in the same pull request.  This task has no failing test; its checks are the greps and the validation at its end.

**Files:**
- Modify: `README.md`
- Modify: `src/SqlSource/AGENTS.md`
- Rewrite: `docs/tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md`
- Modify: `docs/tech-debt/README.md`
- Create: `docs/deferred/D-0001-dialects-not-delivered.md`
- Modify: `docs/deferred/README.md`

**Interfaces:**
- Consumes: the names of every type and test from Tasks 1 to 6, which these documents mention.
- Produces: the anchor `#dialects` in `README.md`, which `docs/diagnostics.md` links to since Task 4.

- [ ] **Step 1: Update the README**

Patch `README.md`:

<!-- patch:task7-readme -->
````diff
diff --git a/README.md b/README.md
index 3af6bac..71cfc08 100644
--- a/README.md
+++ b/README.md
@@ -90,8 +90,8 @@ A `-- summary:` line inside a query becomes the documentation of its member.  Se
 
 - The `-- name:`, `-- summary:` and `-- SqlSource:` lines are removed.
 - Comments are removed: a line comment is deleted and a block comment becomes one space.  Lines left blank are removed.
-- Optimizer hints, `/*+ ... */` and `/*! ... */`, are kept.
-- Strings and quoted identifiers are copied exactly as written.
+- Optimizer hints, `/*+ ... */` and `/*! ... */`, are kept.  So are MariaDB's `/*M! ... */` and Oracle's `--+ ...` when the dialect is theirs.
+- Strings and quoted identifiers are copied exactly as written.  Where one starts and ends depends on the dialect (see Dialects, below).
 - Line endings are always `\n`, so the SQL does not depend on how the file was checked out.
 
 ### Directives
@@ -103,6 +103,7 @@ A `-- SqlSource:` line holds one or more directives, separated by spaces.  Insid
 | `keep-comments` | Comments and blank lines stay in the SQL |
 | `token-ignore=name` | `{{name}}` is literal text, not a token |
 | `token-validation`, `no-token-validation` | The query's method checks its arguments, or does not, whatever the project says (see Tokens, below) |
+| `dialect=name` | The file is read by the rules of that database (see Dialects, below).  Allowed only before the first `-- name:` line and before any SQL. |
 
 ```sql
 -- name: Report
@@ -110,23 +111,96 @@ A `-- SqlSource:` line holds one or more directives, separated by spaces.  Insid
 SELECT /* the database logs this comment */ id FROM users;
 ```
 
-### Dialect limits
+## Dialects
 
-SqlSource finds comments and strings with one set of rules for every database: ANSI SQL, plus the PostgreSQL and MySQL quoting forms that cannot be mistaken for anything else.  It reads the constructs below differently from the database they are written for.
+Databases disagree about where a comment or a string ends.  `'it\'s'` is one string in MySQL and an unclosed one in PostgreSQL; `#` starts a comment in MySQL and names a temporary table in SQL Server.  SqlSource removes comments, so it has to know which rules your SQL follows.  Tell it the dialect.
 
-| Construct | Dialect | How SqlSource reads it |
+| Dialect | Also accepted | Use it for |
 |----|----|----|
-| A backslash escape in a plain string, `'a\'b'` or `"a\"b"` | MySQL | The backslash is an ordinary character, so the string ends at the escaped quote |
-| A quote-operator literal, `q'[it's]'` | Oracle | An ordinary string that ends at the first quote inside it |
-| A bracketed identifier that contains a quote, `--` or `/*`, such as `[a'b]` | SQL Server | Plain text, so the quote opens a string and `--` starts a comment |
-| `--` with no whitespace after it, `5--3` | MySQL | A comment |
-| A `#` comment | MySQL | Plain text, so the comment stays in the SQL |
-| A `/*` inside a block comment | MySQL, Oracle, SQLite | A nested comment that needs its own `*/` |
+| `ansi` | | The default.  Any database without a dialect of its own, such as Db2. |
+| `mssql` | `sqlserver`, `tsql` | SQL Server, Azure SQL |
+| `postgres` | `postgresql` | PostgreSQL, DuckDB, CockroachDB |
+| `mysql` | | MySQL |
+| `mariadb` | | MariaDB |
+| `sqlite` | | SQLite |
+| `oracle` | | Oracle, Firebird |
+
+Names are not case-sensitive.
+
+### Setting the dialect
+
+For the project, with an MSBuild property:
+
+```xml
+<PropertyGroup>
+    <SqlSourceDialect>postgres</SqlSourceDialect>
+</PropertyGroup>
+```
+
+For some of its files, with metadata on their items.  Where two lines match a file, the later one wins:
+
+```xml
+<ItemGroup>
+    <AdditionalFiles Update="Reporting/**/*.sql" SqlSourceDialect="mssql" />
+</ItemGroup>
+```
+
+For one file, with a directive in the file:
+
+```sql
+-- SqlSource: dialect=mysql
+
+-- name: FindByNote
+SELECT id FROM notes WHERE body = 'it\'s here'; # MySQL reads this as a comment
+```
+
+The directive wins over the metadata, and the metadata over the property.  A file has one dialect:
+
+- The directive goes before the file's first `-- name:` line and before its first SQL.  Anywhere else it is the error [SQLSRC115](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc115).
+- It takes effect on the line after it.  Comments above it, such as a licence header, are read by the dialect that the metadata or the property gives.
+- A name that is not a dialect is an error: [SQLSRC011](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc011) in the property or the metadata, [SQLSRC111](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc111) in the directive.
+
+### What a dialect changes
+
+| | `ansi` | `mssql` | `postgres` | `mysql` | `mariadb` | `sqlite` | `oracle` |
+|----|----|----|----|----|----|----|----|
+| A backslash escapes in `'...'` and `"..."` | No | No | No | Yes | Yes | No | No |
+| A backslash escapes in `E'...'` | Yes | No | Yes | Yes | Yes | No | No |
+| `` `...` `` is a quoted identifier | Yes | No | No | Yes | Yes | Yes | No |
+| `[...]` is a quoted identifier | No | Yes | No | No | No | Yes | No |
+| `$tag$...$tag$` is a string | Yes | No | Yes | Yes | No | No | No |
+| `q'[...]'` is a string | No | No | No | No | No | No | Yes |
+| A `/*` inside a block comment needs its own `*/` | Yes | Yes | Yes | No | No | No | No |
+| `--` is a comment with no whitespace after it | Yes | Yes | Yes | No | No | Yes | Yes |
+| `#` starts a comment | No | No | No | Yes | Yes | No | No |
+| Kept as hints, besides `/*+ ... */` and `/*! ... */` | | | | | `/*M! ... */` | | `--+ ...` |
+
+Two details:
+
+- In `mssql` a `]]` inside brackets stands for one `]`.  In `sqlite` the first `]` ends the identifier.
+- In `postgres` an `E'...'` string that is continued on the next line, as PostgreSQL allows, is one string, and its second part takes backslash escapes too.
+
+Under `mysql` and `mariadb` a marker needs the space that those databases need: `-- name: GetUser` is a marker, and `--name: GetUser` is not a comment at all.
+
+### What is still read differently
+
+SqlSource reads these differently from the database, whatever the dialect:
+
+| Construct | Database | How SqlSource reads it |
+|----|----|----|
+| SQL written for the SQL modes `ANSI_QUOTES` or `NO_BACKSLASH_ESCAPES` | MySQL, MariaDB | As in the default mode: a backslash escapes in `'...'` and `"..."` |
+| A versioned comment whose body holds a string that contains `*/`, such as `/*!50700 SELECT '*/' */` | MySQL, MariaDB | The comment ends at the first `*/`.  Where the server ends it depends on the server's version. |
+| A comment between the parts of a continued `E'...'` string | PostgreSQL | The string ends before the comment, and the part after it is a plain string |
+| A bytes literal with a backslash escape, `b'\''` | CockroachDB | PostgreSQL's reading: a string that ends at the second quote |
+| A block comment that is still open at the end of the file | SQLite | The error [SQLSRC102](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc102) |
+| A client command that is not SQL: `DELIMITER`, `GO`, SQL*Plus `PROMPT` and `REM`, a psql `\` command | All | As SQL, so a quote in it can open a string |
+
+And `ansi` reads the SQL of every database by one set of rules, so it misreads each construct in the table above that it says No to and your database says Yes to.  The fix for those is to set the dialect.
 
 A misread has one of two results:
 
-- **A build error** that reports an unclosed quote or comment ([SQLSRC101](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc101), [SQLSRC102](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc102)) in SQL that is valid for your database.  The construct has to be rewritten; `keep-comments` does not help.
-- **Missing SQL.**  The misread quotes happen to balance, and the SQL after them is taken for a comment and removed: `SELECT 'a\'b -- c', 2` becomes `SELECT 'a\'b`.  Nothing is reported.  `keep-comments` on the query prevents the removal.
+- **A build error** that reports an unclosed quote or comment ([SQLSRC101](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc101), [SQLSRC102](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc102)) in SQL that is valid for your database.  `keep-comments` does not help.
+- **Missing SQL.**  The misread quotes happen to balance, and the SQL after them is taken for a comment and removed: under `ansi`, `SELECT 'a\'b -- c', 2` becomes `SELECT 'a\'b`.  Nothing is reported.  `keep-comments` on the query prevents the removal.
 
 If your SQL uses one of these constructs, check the generated SQL: hover over the member, or read its documentation.
 
@@ -213,6 +287,18 @@ To turn the default off and list the files yourself:
 
 `SqlSourceTokenValidation` turns the argument checks of the generated methods off for a project when it is `false`.  See Tokens, above.
 
+### Dialect
+
+`SqlSourceDialect` names the database whose rules the `.sql` files are read by.  It is a property for the project and metadata of an `AdditionalFiles` item for some of its files.  See Dialects, above.
+
+A project that lists its own files can give the metadata where it lists them:
+
+```xml
+<ItemGroup>
+    <AdditionalFiles Include="Queries/**/*.sql" SqlSourceDialect="postgres" />
+</ItemGroup>
+```
+
 ### Deleting or renaming a file
 
 Editing a `.sql` file is always picked up by the next build.  Deleting, renaming or moving one is not: an incremental `dotnet build` can succeed with the old members still in place, because MSBuild does not notice that the list of files changed.  Run `dotnet build --no-incremental` afterwards.  A clean build, such as a CI build, is not affected.
````

What the patch does:

- "Dialect limits" becomes a section of its own, "Dialects": the names, the three places to set one, what each changes, and what is still read differently.
- The directive table gains `dialect=name`, and the two lines about hints and quoted regions point to the new section.
- The MSBuild section gains "Dialect".
- Every link is an absolute URL, because the README is also the package's readme on nuget.org.

- [ ] **Step 2: Rewrite `TD-0004` to what remains**

Replace `docs/tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md` with the file below.  The item is not resolved: it now describes only the constructs that no dialect reads correctly.  It keeps its id and its file name, which closed specs link to.

<!-- file:docs/tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md -->
````markdown
# TD-0004 - Some SQL is misread whatever the dialect

## Problem

A file is read by the rules of its dialect: [`SqlDialectRules`](../../src/SqlSource/Parsing/SqlDialectRules.cs) and the readers in [`Parsing/Quoting/`](../../src/SqlSource/Parsing/Quoting).  These constructs are still read differently from the database they are written for:

| Construct | Database | How SqlSource reads it |
|----|----|----|
| SQL written for the SQL mode `ANSI_QUOTES`, where `"a\"` is a complete identifier | MySQL, MariaDB | As in the default mode: the backslash escapes the quote, so the region is not closed |
| SQL written for the SQL mode `NO_BACKSLASH_ESCAPES`, where `'a\'` is a complete string | MySQL, MariaDB | As in the default mode |
| A versioned comment whose body holds a string that contains `*/`, such as `/*!50700 SELECT '*/' */` | MySQL, MariaDB | The comment ends at the first `*/`.  The server reads the body as SQL when its version is high enough, and as a comment when it is not, so where it ends depends on the server. |
| A comment between the parts of a continued `E'...'` string | PostgreSQL | The string ends before the comment, and the part after it is a plain string with no backslash escapes |
| A bytes literal with a backslash escape, `b'\''` | CockroachDB, under `postgres` | PostgreSQL's reading: `b'\'` is complete |
| A block comment that is still open at the end of the file | SQLite | `UnterminatedBlockComment` |

The default dialect, `ansi`, is one set of rules for every database.  By design it also misreads what the table in `README.md` says it does not read: backslash escapes in plain strings, `[...]` identifiers, `q'...'` strings, `#` comments, `--` that needs whitespace, and comments that do not nest.  A project that sets a dialect is not affected.

A misread has one of two outcomes:

- **An error.**  The quotes or comments no longer balance, and the file gets `UnterminatedQuote` or `UnterminatedBlockComment`.  The file produces nothing until the construct is rewritten.  The `keep-comments` directive does not help, because a lexer error ends the pass.
- **Silent removal.**  The misread quotes happen to balance, and SQL after them is read as a comment and stripped.  The `keep-comments` directive avoids the removal.

In [`SqlLexerTests`](../../tests/SqlSource.Tests/Parsing/SqlLexerTests.cs), the `Lex_KnownLimit_*` tests pin the readings of `ansi`, and the last rows of `Lex_UnterminatedQuoteOfADialect_ReportsTheOpeningDelimiter` pin the versioned comment and the bytes literal.  `FindEnd_EscapeStringNotFollowedByAContinuation_EndsAtItsOwnQuote` in [`EscapeStringReaderTests`](../../tests/SqlSource.Tests/Parsing/Quoting/EscapeStringReaderTests.cs) pins the comment in a continued string.

## Why it exists

- A dialect is one fixed set of rules.  The two SQL modes change a rule of MySQL at run time, and nothing in a `.sql` file or a project says which mode its SQL is for.
- The end of a versioned comment cannot be known without the version of the server.
- A quoted region is copied as written and is never searched for markers.  If a comment could sit inside a continued string, a `-- name:` line could too, and a marker must never be inside a quoted region.
- CockroachDB has no dialect of its own; `docs/deferred/D-0001-dialects-not-delivered.md` records it.
- SQLite's reading of an open comment would silently take every later query of the file with it.  An error is the safer reading, and it is deliberate.

## Impact

Low for most projects: the SQL modes are off by default, and the other constructs are rare.  A user of one gets either an error that names an unterminated quote or comment in valid SQL (`SQLSRC101`, `SQLSRC102`), or a generated constant that is missing part of the query.  The second is silent at build time, though the truncated SQL will almost always fail when it runs.  `README.md` lists the constructs.

## Proposed fix

1. Let a dialect take options that change one rule, such as `dialect=mysql` with `ansi-quotes` or `no-backslash-escapes`, in the directive, the metadata and the property.  `SqlDialectRules` already holds each rule as a value or a reader, so an option is another instance: `ANSI_QUOTES` is the Doubled reader in the `"` slot.
2. Allow a comment between the parts of a continued `E` string by returning the parts as separate quoted regions, with the lexer remembering that the next plain string continues an escape string.  The comment between them is then an ordinary lexeme, and a marker there is still seen.
3. Leave the versioned comment and SQLite's open comment as they are.

## Trigger

A user reports one of these constructs.
````

Patch `docs/tech-debt/README.md`: the row of `TD-0004` says what remains, and its impact goes from Medium to Low.

<!-- patch:task7-tech-debt-table -->
````diff
diff --git a/docs/tech-debt/README.md b/docs/tech-debt/README.md
index 18ffd00..32181c4 100644
--- a/docs/tech-debt/README.md
+++ b/docs/tech-debt/README.md
@@ -10,7 +10,7 @@ Next id: `TD-0014`
 |----|----|----|----|----|
 | [TD-0002](TD-0002-no-coverage-comment-on-fork-pull-requests.md) | Open | 2026-10-05 | Low | Fork and Dependabot pull requests get no coverage comment |
 | [TD-0003](TD-0003-run-number-limited-by-assembly-version.md) | Open | 2026-10-05 | Low | A run number above 65534 fails the build, because it is a part of the assembly version |
-| [TD-0004](TD-0004-lexer-misreads-dialect-specific-sql.md) | Open | 2026-10-05 | Medium | The SQL lexer misreads some MySQL, Oracle and SQL Server constructs, as an error or by stripping SQL |
+| [TD-0004](TD-0004-lexer-misreads-dialect-specific-sql.md) | Open | 2026-10-05 | Low | A few constructs are misread whatever the dialect: MySQL's SQL modes, a versioned comment that holds `*/` in a string, a comment inside a continued PostgreSQL string |
 | [TD-0006](TD-0006-attribute-conflicts-across-friend-assemblies.md) | Open | 2026-10-05 | Medium | Two projects that share internals and both use SqlSource get warning CS0436 for the generated attribute |
 | [TD-0007](TD-0007-sql-files-that-differ-only-by-case.md) | Open | 2026-10-05 | Low | Two `.sql` files whose paths differ only by case are treated as one, and the second is ignored |
 | [TD-0008](TD-0008-package-is-not-installed-in-a-test.md) | Open | 2026-10-05 | Medium | No test installs the packed package into a project and builds it |
````

- [ ] **Step 3: Record the dialects that were not delivered**

Create `docs/deferred/D-0001-dialects-not-delivered.md`:

<!-- file:docs/deferred/D-0001-dialects-not-delivered.md -->
````markdown
# D-0001 - Dialects beyond the first seven

## Planned

The [dialect design](../superpowers/specs/2026-10-06-sql-dialects-design.md) researched every engine a .NET project is likely to use, and lists what each would need in its section "Engines not delivered".

## Delivered instead

Seven dialects: `ansi`, `mssql`, `postgres`, `mysql`, `mariadb`, `sqlite` and `oracle`, in [`SqlDialectRules`](../../src/SqlSource/Parsing/SqlDialectRules.cs).  `README.md` says which of them to use for DuckDB, CockroachDB, Firebird and Db2.

## Why deferred

The seven cover every engine whose ADO.NET providers have more than 100 million NuGet downloads.  The next engine has under 40 million.  BigQuery needs lexer machinery that nothing else does, and several rules of the rest could not be verified from a primary source.

## Remaining work

A dialect is a value of `SqlDialect`, a name in `SqlDialectName`, a static property of `SqlDialectRules`, a row in the theory `Lex_Construct_IsReadByTheRulesOfTheDialect`, and a column in `README.md`.  A quoting form that no reader has is a new `QuoteReader` in `Parsing/Quoting/`.

| Engine | Read correctly today by | What it needs |
|----|----|----|
| DuckDB | `postgres` | A name of its own, if wanted.  No difference was found. |
| CockroachDB | `postgres`, but for `b'\''` | A reader for `'` that takes backslash escapes after a `b` prefix |
| Firebird | `oracle` | A name of its own, if wanted.  Its `q'...'` has no `n` prefix and the same pairing rule. |
| Db2 | `ansi` | Confirm whether block comments nest on Linux, UNIX and Windows: the documentation is silent.  `$`, `#` and `@` are letters there, so `$$` is an identifier and dollar quotes should be off. |
| Redshift | Neither | `postgres` with the Backslash reader for `'`.  Confirm whether block comments nest outside PL/pgSQL. |
| Snowflake | Neither | `//` line comments, but not in `://`; untagged `$$...$$` strings; the Backslash reader for `'`; no nesting.  Confirm whether `#` starts a comment: sqlparser-rs says yes, sqlglot says no, the documentation is silent. |
| BigQuery | Neither | `#` comments; `"..."` as a string; backslash escapes with no doubling; `'''` and `"""` strings over several lines; `r` and `b` prefixes, where a raw string still does not end at `\'`; backtick identifiers with backslash escapes; no nesting |
| ClickHouse | Neither | `#` as a comment only before a space or `!`; `//` comments; backslash escapes in strings and in both identifier quotes; `$tag$` heredocs; nesting |
| Spark and Databricks | Neither | Backslash escapes; `"..."` as a string by default; `r'...'` raw strings that end at the first quote; a backslash before a line break continues a `--` comment; nesting.  Confirm `$tag$` on Databricks. |
| Trino and Presto | Neither | No nesting; otherwise `ansi` without backticks and dollar quotes |

The sources are in the design's "Sources" section.

## Trigger

A user asks for one of these engines.
````

Patch `docs/deferred/README.md`: the row, and `Next id`.

<!-- patch:task7-deferred-table -->
````diff
diff --git a/docs/deferred/README.md b/docs/deferred/README.md
index 0d212af..b83baf6 100644
--- a/docs/deferred/README.md
+++ b/docs/deferred/README.md
@@ -2,12 +2,13 @@
 
 Planned work that has not been delivered as planned.  Each active item is fully documented in its own file, linked from the ID column.  When an item is resolved, its row is removed and its file deleted in the same PR as the fix.
 
-Next id: `D-0001`
+Next id: `D-0002`
 
 ## Active items
 
 | ID | Status | Added | Planned in | Description |
 |----|----|----|----|----|
+| [D-0001](D-0001-dialects-not-delivered.md) | Open | 2026-10-06 | [SQL dialects design](../superpowers/specs/2026-10-06-sql-dialects-design.md) | Dialects for Redshift, Snowflake, BigQuery, ClickHouse, Spark and Trino, and names of their own for DuckDB, CockroachDB, Firebird and Db2 |
 
 ## Columns
 
````

- [ ] **Step 4: Update the guidance for agents**

Patch `src/SqlSource/AGENTS.md`:

<!-- patch:task7-agents -->
````diff
diff --git a/src/SqlSource/AGENTS.md b/src/SqlSource/AGENTS.md
index 1553794..cd4d24b 100644
--- a/src/SqlSource/AGENTS.md
+++ b/src/SqlSource/AGENTS.md
@@ -27,7 +27,8 @@ The source generator.  It is loaded into the C# compiler of whoever consumes it,
 - **An exception in the generator costs every type its generated code.**  The compiler reports one warning, CS8785, and drops all output.  Nothing a user can write may reach one: the test harness fails a run whose result holds an exception.
 - **The name of a generated file comes from `Generation/HintName.cs`.**  The compiler compares these names ignoring case and throws on a clash, and a name may hold only some characters.  `HintName` builds a name from identifier values, not from the text as written, and adds a hash to a name that would clash.
 - **A query that has a token is a method, written by `Generation/MethodWriter.cs`.**  Any identifier that is not a reserved keyword can be a token name, and so a parameter name, and nothing the method writes may be hidden by one.  The state tuple is read by position (`state.Item1`), never by element name.  The lambda's parameters come from `GetUnusedName`.  A framework type is written from `global::`, and `string` is the keyword.  A new identifier in the method body needs the same care, and a row in `Run_TokenWithAnAwkwardName_Compiles` in `tests/SqlSource.Tests/Generator/GeneratedSourceTests.cs`.
-- **The project's setting joins the pipeline after `TypeQueries`.**  `Generation/TokenValidationSetting.cs` reads the `SqlSourceTokenValidation` property, which reaches the generator only because `build/SqlSource.props` lists it as a `CompilerVisibleProperty`.  `build/SqlSource.targets` trims the value first: the compiler reads it from a file the build writes with one line for each property, so a value on a line of its own would arrive empty, and no code in the generator can see that.  The setting must never be an input of `SqlFileReader.Read`: a change to the property would then parse every file again.  Whether a method validates is `SqlQuery.TokenValidation ?? setting`, decided in `TypeEmitter`.
+- **The dialect is an input of the parse, and it is resolved for each file before the parse.**  `Generation/DialectSetting.cs` reads the `SqlSourceDialect` property and the metadata of the same name on a file's `AdditionalFiles` item; both reach the generator only because `build/SqlSource.props` lists them.  `Generation/FileDialect.cs` joins the two in the `FileDialect` step, so that the parse step sees one dialect for a file and a change to the property parses only the files that fall back to it.  This is the opposite of token validation, below, and deliberate: a dialect decides what the parse produces.  `build/SqlSource.targets` trims both values.
+- **Token validation joins the pipeline after `TypeQueries`.**  `Generation/TokenValidationSetting.cs` reads the `SqlSourceTokenValidation` property, which reaches the generator only because `build/SqlSource.props` lists it as a `CompilerVisibleProperty`.  `build/SqlSource.targets` trims the value first: the compiler reads it from a file the build writes with one line for each property, so a value on a line of its own would arrive empty, and no code in the generator can see that.  The setting must never be an input of `SqlFileReader.Read`: a change to the property would then parse every file again.  Whether a method validates is `SqlQuery.TokenValidation ?? setting`, decided in `TypeEmitter`.
 
 ## Diagnostics
 
@@ -35,7 +36,7 @@ Every diagnostic is a `DiagnosticDescriptor` in `Diagnostics/SqlDiagnostics.cs`:
 
 - **Adding, removing or changing one touches four places:** the descriptor and `SqlDiagnostics.All`; `AnalyzerReleases.Unshipped.md`, which the build checks (rules RS2000 to RS2008); `docs/diagnostics.md`, which a test checks; and, for a parser error, `SqlDiagnostics.ForParseError`.
 - **Never remove or renumber an id that a release has shipped** without recording it under `### Removed Rules` in `AnalyzerReleases.Unshipped.md`.  `AnalyzerReleases.Shipped.md` says what has shipped.
-- **`SQLSRC010` has no position.**  The compiler does not say where an MSBuild property was set, so `SqlSourceGenerator` reports it with `Location.None`, built in the output step.  It is the one diagnostic that does not travel as a `DiagnosticInfo`.
+- **`SQLSRC010` and `SQLSRC011` have no position.**  The compiler does not say where an MSBuild property or the metadata of an item was set, so `SqlSourceGenerator` reports them with `Location.None`, built in an output step.  They are the two diagnostics that do not travel as a `DiagnosticInfo`.  An invalid dialect of a file travels as `ParsedSqlFile.InvalidDialect`, and is reported once for each distinct value.
 - **Write each `new DiagnosticDescriptor(...)` out in full, with a literal id.**  The release-tracking analyzer reads the arguments, so a helper method that builds descriptors hides them from it.
 - **An id below 100 is about the attributed type or the project, and one from 101 is about a `.sql` file.**  The parser ids follow the order of `SqlParseErrorKind`; add a new kind at the end of the enum.
 
@@ -47,7 +48,11 @@ Every diagnostic is a `DiagnosticDescriptor` in `Diagnostics/SqlDiagnostics.cs`:
 - **Every span is an offset into the file's text**, including for a problem found in a block's cleaned SQL.  `SqlBlockText.ToSourceSpan` maps such a span back to the file.
 - **Quoted regions and hints are copied as written**, apart from line endings.  Trimming and blank-line removal must never reach inside one.
 - **Lexemes cover the text without gaps.**  `SqlMarkerReader` and `SqlTextBuilder` depend on that, and on a quoted region or block comment ending with its closing delimiter.
-- **Dialect-sensitive choices live in `SqlLexer` only.**  The `Lex_KnownLimit_*` tests pin where it reads SQL differently from some databases, and `docs/tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md` lists the cases.  Changing one changes the SQL users get.
+- **Dialect-sensitive choices live in `SqlDialectRules` and the readers of `Parsing/Quoting/`.**  `SqlLexer` reads by the rules it is given and names no dialect and no quoting form.  A value in `SqlDialectRules` is a rule that dialects differ on by a setting; a `QuoteReader` is one way a quoted region ends.  The static properties of `SqlDialectRules` are the whole table, and `Lex_Construct_IsReadByTheRulesOfTheDialect` in `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs` pins every cell.  Changing one changes the SQL users get.  `docs/tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md` lists what is still misread.
+- **A reader holds no state and allocates nothing.**  One instance serves every file of every compilation, on any thread.  It may look at the characters before its opening character for a prefix, which stays in the text lexeme before the region.
+- **A quoted region must never be able to hold a marker.**  It is copied as written and is not searched for `-- name:`.  That is why a continued `E` string of PostgreSQL allows only whitespace between its parts.
+- **A dialect's names are in `SqlDialectName` only.**  The directive, the MSBuild property and the metadata all read through it.  The text of `SQLSRC011` and of `docs/diagnostics.md` repeats the list; `SqlDialectName.Accepted` and a test keep the message in step.
+- **A file's `dialect=` directive is found before the text after it is lexed.**  `SqlPreambleDialect` reads the leading comments from the lexer one at a time and switches its rules at the directive; `SqlFileParser` then reads the rest.  The errors of a directive are reported later, by `SqlDirectiveScope`, from the header's end that `SqlPreambleDialect` returns.  The two must agree on what a valid directive is, so both go through `SqlDirectiveScope.TryFindDialect` or `SqlDialectName.TryParse`.
 - **A result with errors has no blocks.**  The generator must never emit from a partly valid file.
 
 > Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
````

What the patch does:

- "Dialect-sensitive choices live in `SqlLexer` only" was true until Task 3.  They live in `SqlDialectRules` and the readers now, and four rules that follow from that are added.
- The pipeline section gains the dialect, and says why it is an input of the parse when token validation is not.
- `SQLSRC010` is no longer the one diagnostic without a position.

- [ ] **Step 5: Check the documents against the code**

Run: `grep -rn "TD-0004" . --include='*.md' --include='*.cs' --exclude-dir=obj --exclude-dir=bin --exclude-dir=superpowers`
Expected: four lines: the title of `docs/tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md`, its row in `docs/tech-debt/README.md`, the bullet in `src/SqlSource/AGENTS.md` that says it lists what is still misread, and the comment above two rows of `Lex_UnterminatedQuoteOfADialect_ReportsTheOpeningDelimiter` in `SqlLexerTests.cs`.  Each is true of the rewritten item.  The specs and plans under `docs/superpowers/` are left out: they record the intent of their day and are not updated.

Run: `grep -n 'Dialect limits\|dialect limits\|`SqlLexer` only' README.md docs/diagnostics.md src/SqlSource/AGENTS.md`
Expected: no output.  Nothing still says that the lexer has one set of rules.

Run: `grep -n '](docs/\|](\.\./\|](#' README.md`
Expected: no output.  The README has no relative link.

- [ ] **Step 6: Validate**

Run: `./pre-commit-validation.sh`
Expected: every step `passed`; the test step reports total 1286.

- [ ] **Step 7: Commit**

```bash
git add README.md src/SqlSource/AGENTS.md docs/tech-debt docs/deferred
```

```bash
git commit -m "Document dialects for users and contributors

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## After the last task

- [ ] **Step 1: Check the spec's verification list**

Run: `./pre-commit-validation.sh`
Expected: every step `passed`; the test step reports total 1286.

Run: `git status --short`
Expected: no output.

- [ ] **Step 2: Measure a parse**

`src/SqlSource/AGENTS.md` requires a change to a hot path to state its time and allocation, before and after, in its pull request.

Create `tests/SqlSource.Tests/Parsing/Measure.cs`.  It is a throwaway and is not committed; it does not meet the repository's style rules, so it is built with them off:

```csharp
using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class Measure
{
    [Fact]
    public void Run()
    {
        var text = CreateFile();
        var report = new StringBuilder();
        foreach (var dialect in new[] { SqlDialect.Ansi, SqlDialect.MySql, SqlDialect.Oracle })
        {
            for (var i = 0; i < 2000; i++) { _ = SqlFileParser.Parse(text, "Queries.sql", dialect); }
            const int Iterations = 20000;
            var before = GC.GetAllocatedBytesForCurrentThread();
            var watch = Stopwatch.StartNew();
            for (var i = 0; i < Iterations; i++) { _ = SqlFileParser.Parse(text, "Queries.sql", dialect); }
            watch.Stop();
            var bytes = (GC.GetAllocatedBytesForCurrentThread() - before) / Iterations;
            report.Append(CultureInfo.InvariantCulture, $"{dialect}: {text.Length} characters, {watch.Elapsed.TotalMilliseconds * 1000 / Iterations:F1} us, {bytes / 1024.0:F1} KB\n");
        }
        System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "measure.txt"), report.ToString());
    }

    // The file of SqlFileParserAllocationTests: 50 queries with comments, strings and tokens.
    private static string CreateFile()
    {
        var file = new StringBuilder("-- Copyright (c) Example\n-- SqlSource: token-ignore=raw\n\n");
        for (var query = 0; query < 50; query++)
        {
            var number = query.ToString(CultureInfo.InvariantCulture);
            _ = file.Append("-- name: Query").Append(number).Append("\n-- summary: Loads the rows for report ").Append(number).Append(".\n")
                .Append("SELECT u.id, u.name, u.email, /* inline note */ o.total -- trailing note\n")
                .Append("FROM {{schema}}.users AS u\n")
                .Append("    INNER JOIN {{schema}}.orders AS o ON o.user_id = u.id -- join\n")
                .Append("    /* a block comment\n       over two lines */\n")
                .Append("WHERE u.status = 'active' AND u.note <> 'it''s -- fine'\n")
                .Append("    AND o.created_at >= @from\n\n")
                .Append("ORDER BY {{orderBy}}, u.id;\n\n");
        }
        return file.ToString();
    }
}
```

```bash
dotnet build tests/SqlSource.Tests -c Release -p:TreatWarningsAsErrors=false -p:EnforceCodeStyleInBuild=false
```

```bash
dotnet test --project tests/SqlSource.Tests -c Release --no-build --filter-class '*Measure'
```

```bash
cat tests/SqlSource.Tests/bin/Release/net10.0/measure.txt
```

That is "after".  For "before", run the same three commands in a second checkout of the commit this branch started from (`git worktree add ../SqlSource-before 190fbe6`, copy `Measure.cs` into it, and `git worktree remove --force ../SqlSource-before` afterwards).  There `Parse` takes no dialect: loop over one name only and remove the third argument of the two calls.

When this plan was written, on an Apple silicon laptop, one 19,487-character file of 50 queries gave:

| | Time for a parse | Allocated |
|----|----|----|
| Before | 70.5 to 71.0 µs | 182.7 KB |
| After, `ansi` | 68.7 to 69.6 µs | 183.1 KB |
| After, `mysql` | 67.4 to 70.2 µs | 183.1 KB |
| After, `oracle` | 66.4 to 67.9 µs | 183.1 KB |

The 0.4 KB is the lexer object and the lexeme list it holds, once for a file; it is 9.70 bytes for each character before and after, against a budget of 12.  Without `FindStarter` the same parse took 76 to 78 µs.  State the numbers you measure in the pull request, with this reading of them.

Delete the file:

```bash
rm tests/SqlSource.Tests/Parsing/Measure.cs
```

Run: `git status --short`
Expected: no output.

- [ ] **Step 3: Check the version**

```bash
git fetch --tags origin
```

```bash
git tag --list 'v*' --sort=-v:refname | head -n 1
```

Expected: no output.  The repository has no release tag, so there is nothing to compare `VersionPrefix` (`0.1.0` in `Directory.Build.props`) with.  If a tag is printed and `VersionPrefix` is not greater than it, ask the user what the new version should be.

- [ ] **Step 4: Hand over**

Do not push and do not open a pull request.  Tell the user that the branch is ready, with the test total, the measurements of Step 2, and anything that differed from this plan.  Opening the pull request is the user's call; when they ask for it, rebase on `origin/main` first (never merge), and ask before any force-push.
