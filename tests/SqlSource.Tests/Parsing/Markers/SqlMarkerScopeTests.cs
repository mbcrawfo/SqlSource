using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Parsing.Markers;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Parsing.Markers;

public sealed class SqlMarkerScopeTests
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
    [InlineData("-- type-override: int4 = long", true, true)]
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

    // The keyword list, the two allowed lists and the scope's reading are four lists of the same kinds.  The kinds the
    // parser reads itself are the ones no scope allows; every other kind has a keyword that reads back as it and is
    // read by the scope without a throw, whatever its value.  A row gives the name of the kind, as an internal enum
    // cannot be the parameter of a public test method.
    [Theory]
    [MemberData(nameof(MarkerKindNames))]
    public void Marker_OfEveryKind_HasAKeywordAndAScope(string kindName)
    {
        var kind = Enum.Parse<SqlMarkerKind>(kindName);
        var word = SqlMarkerReader.WordOf(kind);
        word.ShouldNotBeEmpty();

        var line = "-- " + word + ": x";
        var marker = SqlMarkerReader.Read(line, SqlLexer.Lex(line, SqlDialectRules.Ansi).Lexemes[0]);
        marker.ShouldNotBeNull().Kind.ShouldBe(kind);

        var isAllowed = SqlMarkerScope.IsAllowedInQuery(kind) || SqlMarkerScope.IsAllowedInPreamble(kind);
        isAllowed.ShouldBe(kind is not (SqlMarkerKind.Name or SqlMarkerKind.Summary or SqlMarkerKind.Dialect));
        if (isAllowed)
        {
            _ = Should.NotThrow(() => Scope(SqlScopeKind.WholeFile, line));
        }
    }

    public static TheoryData<string> MarkerKindNames => [.. Enum.GetNames<SqlMarkerKind>()];

    // A kind that is none of the three takes nothing: the scope does not guess what it is.
    [Fact]
    public void Read_ScopeOfAnUnknownKind_TakesNoMarker() =>
        IsTaken("-- generator: keep-comments", (SqlScopeKind)99).ShouldBeFalse();

    // The header of a file is the place of its preamble's entries, and a query the place of its own.  A file with no
    // name marker is one query.
    // An internal enum cannot be the parameter of a public test method, so a row gives the names.
    [Theory]
    [InlineData("Preamble", "FileHeader")]
    [InlineData("Query", "Query")]
    [InlineData("WholeFile", "Query")]
    public void Read_TypeOverrides_AreTheLevelsSortedByKeyAtThePlaceOfTheScope(string kind, string placeName)
    {
        var place = Enum.Parse<SettingPlace>(placeName);
        var (scope, errors) = Scope(
            Enum.Parse<SqlScopeKind>(kind),
            "-- type-override: timestamptz = default",
            "-- type-override: int4 = long",
            "-- type-override: public.user_status=App.Status"
        );

        errors.ShouldBeEmpty();
        scope
            .Level.TypeOverrides.ShouldNotBeNull()
            .ShouldBe([
                new TypeOverride("int4", "long", place),
                new TypeOverride("public.user_status", "global::App.Status", place),
                new TypeOverride("timestamptz", null, place),
            ]);
    }

    // One value written two ways, and the same entry written twice, are the same value: no conflict.
    [Fact]
    public void Read_SameTypeOverrideTwice_IsOneEntry()
    {
        var (scope, errors) = Scope(
            SqlScopeKind.Query,
            "-- type-override: int4 = System.Int32",
            "-- type-override: integer=global::System.Int32",
            "-- type-override: INT4 = System.Int32"
        );

        errors.ShouldBeEmpty();
        scope
            .Level.TypeOverrides.ShouldNotBeNull()
            .ShouldBe([new TypeOverride("int4", "global::System.Int32", SettingPlace.Query)]);
    }

    // int and System.Int32 are one type to the compiler, and two values to the setting.
    [Theory]
    [InlineData("-- type-override: int4 = int")]
    [InlineData("-- type-override: int4 = default")]
    [InlineData("-- type-override: integer = long")]
    public void Read_TwoValuesForOneKeyInOneScope_ConflictAtTheSecondAndTheFirstStands(string second)
    {
        var (scope, errors) = Scope(SqlScopeKind.Preamble, "-- type-override: int4=System.Int32", second);

        errors.Select(static error => error.Kind).ShouldBe([SqlParseErrorKind.ConflictingSettings]);
        scope
            .Level.TypeOverrides.ShouldNotBeNull()
            .ShouldBe([new TypeOverride("int4", "global::System.Int32", SettingPlace.FileHeader)]);
    }

    // Names that differ by case are two types of a catalog that quotes them, so they are two keys.
    [Fact]
    public void Read_TypeOverrideKeysThatDifferByCase_AreTwoEntries()
    {
        var (scope, errors) = Scope(
            SqlScopeKind.Query,
            "-- type-override: public.Status = App.A",
            "-- type-override: public.status = App.B"
        );

        errors.ShouldBeEmpty();
        scope
            .Level.TypeOverrides.ShouldNotBeNull()
            .ShouldBe([
                new TypeOverride("public.Status", "global::App.A", SettingPlace.Query),
                new TypeOverride("public.status", "global::App.B", SettingPlace.Query),
            ]);
    }

    // The marker reader hands the rest of the line over as written, quotes and spaces inside them included.
    [Fact]
    public void Read_TypeOverrideWithAQuotedKey_IsReadWithTheKeyIntact()
    {
        var (scope, errors) = Scope(SqlScopeKind.Query, "-- type-override: \"my schema\".\"order status\" = App.A");

        errors.ShouldBeEmpty();
        scope
            .Level.TypeOverrides.ShouldNotBeNull()
            .ShouldBe([new TypeOverride("\"my schema\".\"order status\"", "global::App.A", SettingPlace.Query)]);
    }

    // A quoted part and a plain one that read as one text are one key.
    [Fact]
    public void Read_QuotedAndPlainSpellingOfOneKeyWithTwoValues_ConflictAtTheSecond()
    {
        var (scope, errors) = Scope(
            SqlScopeKind.Query,
            "-- type-override: public.status = App.A",
            "-- type-override: public.\"status\" = App.B"
        );

        errors.Select(static error => error.Kind).ShouldBe([SqlParseErrorKind.ConflictingSettings]);
        scope
            .Level.TypeOverrides.ShouldNotBeNull()
            .ShouldBe([new TypeOverride("public.status", "global::App.A", SettingPlace.Query)]);
    }

    // _int4 is the catalog's name of int4[], and the two are one key.
    [Fact]
    public void Read_ArrayKeySpelledTwoWaysWithTwoValuesInOneScope_ConflictAtTheSecond()
    {
        var (scope, errors) = Scope(
            SqlScopeKind.Query,
            "-- type-override: _int4 = long",
            "-- type-override: int4[] = int"
        );

        errors.Select(static error => error.Kind).ShouldBe([SqlParseErrorKind.ConflictingSettings]);
        scope.Level.TypeOverrides.ShouldNotBeNull().ShouldBe([new TypeOverride("int4[]", "long", SettingPlace.Query)]);
    }

    [Theory]
    [InlineData("-- type-override: int4 = int?")]
    [InlineData("-- type-override: timestampz = System.DateTimeOffset")]
    [InlineData("-- type-override: int4 = long int8 = long")]
    [InlineData("-- type-override:")]
    public void Read_TypeOverrideThatIsNotValid_IsInvalidAndNotApplied(string line)
    {
        var (scope, errors) = Scope(SqlScopeKind.Query, line);

        errors.Select(static error => error.Kind).ShouldBe([SqlParseErrorKind.InvalidMarkerValue]);
        scope.Level.ShouldBeSameAs(SettingsLevel.None);
    }

    // A file's markers are read by its engine: an mssql file's by SQL Server's rules, into SQL Server's entries alone.
    [Fact]
    public void Read_TypeOverrideOfAnMssqlFile_IsSqlServersEntryAlone()
    {
        var (scope, errors) = Scope(
            SqlDialectRules.SqlServer,
            SqlScopeKind.Query,
            "-- type-override: nvarchar = string"
        );

        errors.ShouldBeEmpty();
        scope.Level.TypeOverrides.ShouldBeNull();
        scope
            .Level.SqlServerTypeOverrides.ShouldNotBeNull()
            .ShouldBe([new TypeOverride("nvarchar", "string", SettingPlace.Query)]);
    }

    // int and integer are one key in SQL Server, as int4 and integer are in PostgreSQL.
    [Fact]
    public void Read_TwoValuesForOneSqlServerKeyInAnMssqlFile_ConflictAtTheSecond()
    {
        var (scope, errors) = Scope(
            SqlDialectRules.SqlServer,
            SqlScopeKind.Query,
            "-- type-override: int = long",
            "-- type-override: integer = short"
        );

        errors.Select(static error => error.Kind).ShouldBe([SqlParseErrorKind.ConflictingSettings]);
        scope
            .Level.SqlServerTypeOverrides.ShouldNotBeNull()
            .ShouldBe([new TypeOverride("int", "long", SettingPlace.Query)]);
    }

    // A key the file's engine does not read is not valid there, though the other engine reads it.
    [Theory]
    [InlineData("mssql", "-- type-override: int4 = long")]
    [InlineData("mssql", "-- type-override: pg_catalog.text = string")]
    [InlineData("postgres", "-- type-override: nvarchar = string")]
    [InlineData("postgres", "-- type-override: rowversion = byte[]")]
    public void Read_TypeOverrideWithAKeyTheFilesEngineDoesNotRead_IsInvalid(string dialect, string line)
    {
        var rules = dialect == "mssql" ? SqlDialectRules.SqlServer : SqlDialectRules.PostgreSql;

        var (scope, errors) = Scope(rules, SqlScopeKind.Query, line);

        errors.Select(static error => error.Kind).ShouldBe([SqlParseErrorKind.InvalidMarkerValue]);
        scope.Level.ShouldBeSameAs(SettingsLevel.None);
    }

    // A key in brackets is not read, and the error shows the forms that are.
    [Fact]
    public void Read_TypeOverrideWithAKeyInBrackets_IsInvalidWithTheFormsThatAreRead()
    {
        const string Line = "-- type-override: [dbo].[Email]=MyApp.Email";
        const int Start = 18;

        var (_, errors) = Scope(SqlDialectRules.SqlServer, SqlScopeKind.Query, Line);

        errors.ShouldBe([
            SqlParseError.Create(
                SqlParseErrorKind.InvalidMarkerValue,
                new Microsoft.CodeAnalysis.Text.TextSpan(Start, Line.Length - Start),
                "type-override: [dbo].[Email]=MyApp.Email",
                ".  A key is not written in brackets: write dbo.Email, or \"dbo\".\"Email\" to match its case exactly"
            ),
        ]);
    }

    // A value that is not valid is another error, and is not what is asked here.
    private static bool IsTaken(string line, SqlScopeKind kind)
    {
        var marker = SqlMarkerReader.Read(line, SqlLexer.Lex(line, SqlDialectRules.Ansi).Lexemes[0]);
        var errors = new List<SqlParseError>();

        new SqlMarkerScope(line, SqlDialectRules.Ansi, kind, errors).Read(marker.ShouldNotBeNull());

        return !errors.Any(static error => error.Kind == SqlParseErrorKind.MarkerNotAllowedHere);
    }

    // A scope over the lines, each a marker, read in order.
    private static (SqlMarkerScope Scope, List<SqlParseError> Errors) Scope(SqlScopeKind kind, params string[] lines) =>
        Scope(SqlDialectRules.Ansi, kind, lines);

    private static (SqlMarkerScope Scope, List<SqlParseError> Errors) Scope(
        SqlDialectRules rules,
        SqlScopeKind kind,
        params string[] lines
    )
    {
        var text = string.Join("\n", lines);
        var errors = new List<SqlParseError>();
        var scope = new SqlMarkerScope(text, rules, kind, errors);
        foreach (var lexeme in SqlLexer.Lex(text, rules).Lexemes)
        {
            if (SqlMarkerReader.Read(text, lexeme) is { } marker)
            {
                scope.Read(marker);
            }
        }

        return (scope, errors);
    }
}
