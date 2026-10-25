using System;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Parsing.Tokens;
using SqlSource.Settings;
using SqlSource.Settings.ColumnTypes;
using SqlSource.Settings.TypeOverrides;
using Xunit;
using static SqlSource.Tests.Parsing.ParseOf;

namespace SqlSource.Tests.Parsing;

// The scope and errors of generator parameters, and the output, database, model, type-override and column markers.
public sealed class SqlFileParserMarkerTests
{
    [Fact]
    public void Parse_PreambleGeneratorParameters_ApplyToEveryBlock()
    {
        const string Text = "-- generator: no-token-validation\n" + "-- name: A\nSELECT 1\n" + "-- name: B\nSELECT 2\n";

        var blocks = Blocks(Text);

        blocks[0].Markers.Parameters.ShouldBe(GeneratorParameters.NoTokenValidation);
        blocks[1].Markers.Parameters.ShouldBe(GeneratorParameters.NoTokenValidation);
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

    [Fact]
    public void Parse_BlockGeneratorParameter_DoesNotLeakIntoTheNextBlock()
    {
        const string Text =
            "-- name: A\n-- generator: keep-comments no-token-validation\n-- token-ignore: x\nSELECT {{x}} -- c\n"
            + "-- name: B\nSELECT {{x}} -- c\n";

        var blocks = Blocks(Text);

        blocks[1].Markers.ShouldBeSameAs(SettingsLevel.None);
        Sql(blocks[1]).ShouldBe("SELECT {{x}}");
        blocks[1].Segments[1].ShouldBe(new SqlSegment(SqlSegmentKind.Token, "x"));
    }

    [Theory]
    [InlineData("-- generator: keep-comment", nameof(SqlParseErrorKind.UnknownGeneratorParameter), "keep-comment")]
    [InlineData("-- generator:", nameof(SqlParseErrorKind.EmptyGeneratorLine), "-- generator:")]
    [InlineData("-- generator: keep-comments=", nameof(SqlParseErrorKind.InvalidMarkerValue), "keep-comments=")]
    [InlineData(
        "-- generator: default no-token-validation",
        nameof(SqlParseErrorKind.ConflictingSettings),
        "no-token-validation"
    )]
    public void Parse_GeneratorParameterProblem_IsReportedAtItsPlaceInTheFile(string line, string kind, string place)
    {
        var text = "-- name: A\n" + line + "\nSELECT 1";

        var error = Errors(text).ShouldHaveSingleItem();

        error.Kind.ToString().ShouldBe(kind);
        error.Span.ShouldBe(TextSpans.Of(text, place));
    }

    // A comment that has the form of a marker is one, whatever its author meant by it.
    [Fact]
    public void Parse_OrdinaryCommentThatStartsWithGenerator_IsReadAsGeneratorParameters()
    {
        const string Text = "-- generator: pgloader\n-- name: A\nSELECT 1\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.UnknownGeneratorParameter,
                    TextSpans.Of(Text, "pgloader"),
                    "pgloader"
                ),
            ]);
    }

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
    [InlineData("type-override", "int4=System.Int32", "int4 = int")]
    public void Parse_TwoValuesOfOneMarkerInOneScope_ConflictAtTheSecond(string word, string first, string second)
    {
        var text = "-- name: Q\n-- " + word + ": " + first + "\n-- " + word + ": " + second + "\nSELECT 1\n";

        Errors(text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.ConflictingSettings,
                    TextSpans.Of(text, second),
                    word + ": " + second
                ),
            ]);
    }

    // A comment that only starts like a marker is one, and is reported at its value.
    [Theory]
    [InlineData("output", "the rows we need")]
    [InlineData("output", "model")]
    [InlineData("database", "see the wiki")]
    [InlineData("database", "a/b")]
    [InlineData("type-override", "int4 = int?")]
    [InlineData("type-override", "timestampz = System.DateTimeOffset")]
    public void Parse_MarkerWithAValueItDoesNotTake_IsInvalidAtTheValue(string word, string value)
    {
        var text = "-- name: Q\n-- " + word + ": " + value + "\nSELECT 1\n";

        Errors(text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.InvalidMarkerValue,
                    TextSpans.Of(text, value),
                    word + ": " + value,
                    ""
                ),
            ]);
    }

    // A marker with nothing after its colon is invalid at the whole marker: in a query, and for the dialect marker in
    // the file's header.
    [Theory]
    [InlineData("output", true)]
    [InlineData("database", true)]
    [InlineData("type-override", true)]
    [InlineData("token", true)]
    [InlineData("param", true)]
    [InlineData("column", true)]
    [InlineData("token-ignore", true)]
    [InlineData("dialect", false)]
    public void Parse_MarkerWithoutAValue_IsInvalidAtTheMarker(string word, bool inAQuery)
    {
        var text = (inAQuery ? "-- name: Q\n" : "") + "-- " + word + ":\nSELECT 1\n";

        Errors(text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.InvalidMarkerValue,
                    TextSpans.Of(text, "-- " + word + ":"),
                    word + ":",
                    ""
                ),
            ]);
    }

    [Fact]
    public void Parse_SameTypeOverrideTwiceInOneScope_IsNoConflict() =>
        Blocks("-- name: Q\n-- type-override: int4 = long\n-- type-override: int4=long\nSELECT 1\n")
            .ShouldHaveSingleItem()
            .Markers.TypeOverrides.ShouldNotBeNull()
            .ShouldBe([new TypeOverride("int4", "long", SettingPlace.Query)]);

    // A query's entry replaces the header's for its key alone, and a default stays in the markers' level, so that it
    // also hides the key of a level below the markers.
    [Fact]
    public void Parse_TypeOverridesOfAQuery_ReplaceOnlyTheKeysTheyName()
    {
        const string Text =
            "-- type-override: int4 = long\n-- type-override: timestamptz = System.DateTimeOffset\n"
            + "-- type-override: text = string\n\n"
            + "-- name: A\n-- type-override: timestamptz = System.DateTime\n-- type-override: text = default\n"
            + "SELECT 1\n"
            + "-- name: B\nSELECT 2\n";

        var blocks = Blocks(Text);

        blocks[0]
            .Markers.TypeOverrides.ShouldNotBeNull()
            .ShouldBe([
                new TypeOverride("int4", "long", SettingPlace.FileHeader),
                new TypeOverride("text", null, SettingPlace.Query),
                new TypeOverride("timestamptz", "global::System.DateTime", SettingPlace.Query),
            ]);
        blocks[1]
            .Markers.TypeOverrides.ShouldNotBeNull()
            .ShouldBe([
                new TypeOverride("int4", "long", SettingPlace.FileHeader),
                new TypeOverride("text", "string", SettingPlace.FileHeader),
                new TypeOverride("timestamptz", "global::System.DateTimeOffset", SettingPlace.FileHeader),
            ]);
    }

    // A file with no name marker is one query, and its entries are the query's.
    [Fact]
    public void Parse_TypeOverrideOfAFileWithNoName_IsAtTheQuery() =>
        Blocks("-- type-override: int4 = long\nSELECT 1\n")
            .ShouldHaveSingleItem()
            .Markers.TypeOverrides.ShouldNotBeNull()
            .ShouldBe([new TypeOverride("int4", "long", SettingPlace.Query)]);

    // An mssql file's entries are read by SQL Server's rules, the file's dialect marker deciding as the project's does.
    [Fact]
    public void Parse_TypeOverridesOfAnMssqlFile_AreSqlServersEntries()
    {
        var block = Blocks(
                "-- dialect: mssql\n-- type-override: rowversion = byte[]\n"
                    + "-- name: Q\n-- type-override: nvarchar = string\nSELECT 1\n"
            )
            .ShouldHaveSingleItem();

        block.Markers.TypeOverrides.ShouldBeNull();
        block
            .Markers.SqlServerTypeOverrides.ShouldNotBeNull()
            .ShouldBe([
                new TypeOverride("nvarchar", "string", SettingPlace.Query),
                new TypeOverride("timestamp", "byte[]", SettingPlace.FileHeader),
            ]);
    }

    [Fact]
    public void Parse_TypeOverrideOfIntAndOfIntegerInOneScopeOfAnMssqlFile_Conflict() =>
        Errors(
                "-- name: Q\n-- type-override: int = long\n-- type-override: integer = short\nSELECT 1\n",
                dialect: new SqlDialectChoice(SqlDialect.SqlServer, SqlDialectOptions.None)
            )
            .Select(static error => error.Kind)
            .ShouldBe([SqlParseErrorKind.ConflictingSettings]);

    [Theory]
    [InlineData("SqlServer", "int4 = long")]
    [InlineData("PostgreSql", "nvarchar = string")]
    public void Parse_TypeOverrideWithAKeyTheFilesEngineDoesNotRead_IsInvalid(string dialect, string value)
    {
        var text = "-- name: Q\n-- type-override: " + value + "\nSELECT 1\n";

        Errors(text, dialect: new SqlDialectChoice(Enum.Parse<SqlDialect>(dialect), SqlDialectOptions.None))
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.InvalidMarkerValue,
                    TextSpans.Of(text, value),
                    "type-override: " + value,
                    ""
                ),
            ]);
    }

    [Fact]
    public void Parse_TypeOverrideWithAKeyInBrackets_IsInvalidWithTheFormsThatAreRead()
    {
        const string Value = "[dbo].[Email]=MyApp.Email";
        const string Text = "-- dialect: mssql\n-- name: Q\n-- type-override: " + Value + "\nSELECT 1\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.InvalidMarkerValue,
                    TextSpans.Of(Text, Value),
                    "type-override: " + Value,
                    ".  A key is not written in brackets: write dbo.Email, or \"dbo\".\"Email\" to match its case "
                        + "exactly"
                ),
            ]);
    }

    [Fact]
    public void Parse_PreambleSettings_AreOneLevelSharedByTheQueriesThatHaveNoMarkersOfTheirOwn()
    {
        var blocks = Blocks("-- output: models\n-- name: A\nSELECT 1\n-- name: B\nSELECT 2\n");

        blocks[0].Markers.Output.ShouldBe(OutputKind.Models);
        blocks[1].Markers.ShouldBeSameAs(blocks[0].Markers);
    }

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
        block.InputModel!.Name.ShouldBe("FindArgs");
        block.OutputModel!.Name.ShouldBe("App.Shared.UserRow");
    }

    [Fact]
    public void Parse_ModelMarkers_KeepTheirSpans()
    {
        const string Text = "-- name: Q\n-- input-model: FindArgs\n-- output-model: App.Shared.UserRow\nSELECT @a\n";

        var block = Blocks(Text).ShouldHaveSingleItem();

        block.InputModel.ShouldBe(new SqlModelName("FindArgs", TextSpans.Of(Text, "-- input-model: FindArgs")));
        block.OutputModel.ShouldBe(
            new SqlModelName("App.Shared.UserRow", TextSpans.Of(Text, "-- output-model: App.Shared.UserRow"))
        );
    }

    [Fact]
    public void Parse_QueryWithoutModelMarkers_HasNoModels()
    {
        var block = Blocks("-- name: Q\nSELECT @a\n").ShouldHaveSingleItem();

        block.InputModel.ShouldBeNull();
        block.OutputModel.ShouldBeNull();
    }

    [Theory]
    // A marker for the file, written inside a query; and a marker for a query, written before the first one.
    [InlineData("input-model-suffix", "Args", false, "before the file's first query")]
    [InlineData("output-model-suffix", "Row", false, "before the file's first query")]
    [InlineData("model-namespace", "App", false, "before the file's first query")]
    [InlineData("input-model", "FindArgs", true, "inside a query")]
    [InlineData("output-model", "UserRow", true, "inside a query")]
    public void Parse_ModelMarkerInTheWrongScope_IsNotAllowedThere(
        string word,
        string value,
        bool inPreamble,
        string allowed
    )
    {
        var marker = "-- " + word + ": " + value;
        var text = inPreamble ? marker + "\n-- name: Q\nSELECT @a\n" : "-- name: Q\n" + marker + "\nSELECT @a\n";

        Errors(text)
            .ShouldBe([
                SqlParseError.Create(SqlParseErrorKind.MarkerNotAllowedHere, TextSpans.Of(text, marker), word, allowed),
            ]);
    }

    // A marker of a query, given before the file's first name marker, is not allowed there.
    [Theory]
    [InlineData("-- token: {{a:x}}", "token", "SELECT {{a}}")]
    [InlineData("-- param: @a int", "param", "SELECT @a")]
    [InlineData("-- token-ignore: a", "token-ignore", "SELECT {{a}}")]
    public void Parse_MarkerOutsideAQuery_IsNotAllowedThere(string marker, string word, string sql = "SELECT 1")
    {
        var text = marker + "\n-- name: Q\n" + sql + "\n";

        Errors(text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.MarkerNotAllowedHere,
                    TextSpans.Of(text, marker),
                    word,
                    "inside a query"
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
                SqlParseError.Create(
                    SqlParseErrorKind.InvalidMarkerValue,
                    TextSpans.Of(text, value),
                    word + ": " + value,
                    ""
                ),
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

    // The same name twice is no conflict, and the model's marker is the first, where SQLSRC257 and SQLSRC259 point.
    [Fact]
    public void Parse_OneNameForOneModelTwice_KeepsTheFirstMarker()
    {
        const string Text = "-- name: Q\n-- output-model: A\n-- output-model: A\nSELECT 1\n";

        var block = Blocks(Text).ShouldHaveSingleItem();

        block.OutputModel.ShouldBe(
            new SqlModelName("A", new TextSpan(Text.IndexOf("-- output-model: A", StringComparison.Ordinal), 18))
        );
    }

    [Fact]
    public void Parse_InputModelOnAQueryWithoutParameters_IsAnError()
    {
        const string Text = "-- name: Q\n-- input-model: FindArgs\nSELECT 1 {{f}}\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.InputModelWithoutParameters,
                    TextSpans.Of(Text, "-- input-model: FindArgs")
                ),
            ]);
    }

    // A parameter that only a marker declares is a parameter.
    [Fact]
    public void Parse_InputModelOnAQueryWithADeclaredOnlyParameter_IsFine() =>
        Blocks("-- name: Q\n-- param: @page int\n-- input-model: FindArgs\nSELECT 1 {{f}}\n")
            .ShouldHaveSingleItem()
            .InputModel!.Name.ShouldBe("FindArgs");

    [Theory]
    [InlineData("total null", "total:null")]
    [InlineData("total not null", "total:not null")]
    // As people type it.
    [InlineData("total NOT NULL", "total:not null")]
    [InlineData("Total\tnot \t Null", "Total:not null")]
    // A quoted name, for white space, a name that is a word of nullability, or a quote.
    [InlineData("\"total amount\" not null", "total amount:not null")]
    [InlineData("\"null\" null", "null:null")]
    [InlineData("\"not\" not null", "not:not null")]
    [InlineData("\"say \"\"hi\"\"\" null", "say \"hi\":null")]
    // A name that only ends in the letters of a word.
    [InlineData("knot null", "knot:null")]
    [InlineData("isnull not null", "isnull:not null")]
    [InlineData("is_null null", "is_null:null")]
    public void Parse_ColumnMarker_GivesItsColumnANullability(string value, string expected) =>
        Columns(Blocks("-- name: Q\n-- column: " + value + "\nSELECT 1\n").ShouldHaveSingleItem()).ShouldBe([expected]);

    [Fact]
    public void Parse_ColumnMarker_KeepsTheSpanOfTheWholeMarker()
    {
        const string Text = "-- name: Q\n-- column: total not null\nSELECT 1\n";

        Blocks(Text)
            .ShouldHaveSingleItem()
            .ColumnMarkers.ShouldBe([
                new SqlColumnMarker("total", false, null, false, TextSpans.Of(Text, "-- column: total not null")),
            ]);
    }

    // A comment: the SQL, from which the query's hash is computed, is that of the query without the marker.
    [Fact]
    public void Parse_ColumnMarker_LeavesTheSqlAlone() =>
        Blocks("-- name: Q\n-- column: total null\nSELECT 1\n")
            .ShouldHaveSingleItem()
            .Segments.ShouldBe(Blocks("-- name: Q\nSELECT 1\n").ShouldHaveSingleItem().Segments);

    [Fact]
    public void Parse_ColumnMarkers_AreInMarkerOrderEachColumnOnce() =>
        Columns(
                Blocks("-- name: Q\n-- column: b null\n-- column: a not null\n-- column: B NULL\nSELECT 1\n")
                    .ShouldHaveSingleItem()
            )
            .ShouldBe(["b:null", "a:not null"]);

    [Theory]
    [InlineData("total not null")]
    [InlineData("TOTAL not null")]
    public void Parse_OneColumnWithTwoNullabilities_ConflictsAtTheSecond(string second)
    {
        var text = "-- name: Q\n-- column: total null\n-- column: " + second + "\nSELECT 1\n";

        Errors(text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.ConflictingSettings,
                    new TextSpan(text.IndexOf("-- column: " + second, StringComparison.Ordinal) + 11, second.Length),
                    "column: " + second
                ),
            ]);
    }

    [Theory]
    [InlineData("total")]
    [InlineData("total nullable")]
    [InlineData("total not")]
    [InlineData("null")]
    [InlineData("not null")]
    [InlineData("\"\" null")]
    [InlineData("\"total null")]
    [InlineData("\"to\"tal\" null")]
    [InlineData("\"total\"x null")]
    [InlineData("total\"\" null")]
    [InlineData("total amount null")]
    // A type that is not valid, or an arrow without a name.
    [InlineData("total -> not a type")]
    [InlineData("total ->")]
    [InlineData("total -> int?")]
    [InlineData("-> PostCount")]
    [InlineData("null -> PostCount")]
    [InlineData("total->PostCount")]
    public void Parse_ColumnMarkerWithoutANameAndANullability_IsInvalid(string value)
    {
        var text = "-- name: Q\n-- column: " + value + "\nSELECT 1\n";

        Errors(text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.InvalidMarkerValue,
                    TextSpans.Of(text, value),
                    "column: " + value,
                    ""
                ),
            ]);
    }

    // A file without a name marker is one query, and its markers are the query's.
    [Fact]
    public void Parse_ColumnMarkerInAFileWithoutANameMarker_IsTheQuerys() =>
        Columns(Blocks("-- column: total null\nSELECT 1\n").ShouldHaveSingleItem()).ShouldBe(["total:null"]);

    [Fact]
    public void Parse_QueryWithoutAColumnMarker_HasNoColumnMarkers() =>
        Blocks("-- name: Q\n-- param: @a int\nSELECT @a\n").ShouldHaveSingleItem().ColumnMarkers.ShouldBeEmpty();

    [Theory]
    [InlineData("-- column: total -> PostCount", null, "PostCount", false)]
    [InlineData("-- column: note null -> NoteText", true, "NoteText", false)]
    [InlineData("-- column: id -> default", null, null, true)]
    [InlineData("-- column: id not null -> DEFAULT", false, null, true)]
    [InlineData("-- column: total\t->\tMyApp.PostCount", null, "global::MyApp.PostCount", false)]
    [InlineData("-- column: \"a -> b\" -> PostCount", null, "PostCount", false)]
    [InlineData("-- column: \"posts.id\" -> PostId", null, "PostId", false)]
    public void Parse_ColumnMarker_InAQuery_WithAType(string marker, bool? isNullable, string? clrType, bool resetsType)
    {
        var column = Blocks("-- name: Q\n" + marker + "\nSELECT 1\n")
            .ShouldHaveSingleItem()
            .ColumnMarkers.ShouldHaveSingleItem();

        column.IsNullable.ShouldBe(isNullable);
        column.ClrType.ShouldBe(clrType);
        column.ResetsType.ShouldBe(resetsType);
    }

    [Fact]
    public void Parse_ColumnMarker_InAQuery_KeepsTheNameWithoutQuotes() =>
        Blocks("-- name: Q\n-- column: \"a -> b\" -> PostCount\nSELECT 1\n")
            .ShouldHaveSingleItem()
            .ColumnMarkers.ShouldHaveSingleItem()
            .Name.ShouldBe("a -> b");

    // A type is not a setting of the query's level: the result's column carries it.
    [Fact]
    public void Parse_ColumnMarker_InAQuery_IsNoColumnTypeOfTheLevel() =>
        Blocks("-- name: Q\n-- column: total -> PostCount\nSELECT 1\n")
            .ShouldHaveSingleItem()
            .Markers.ShouldBeSameAs(SettingsLevel.None);

    [Fact]
    public void Parse_ColumnMarker_InTheHeader_IsAColumnType()
    {
        var blocks = Blocks(
            "-- column: posts.id -> PostId\n-- column: auth.\"Users\".id -> default\n"
                + "-- name: A\nSELECT 1\n-- name: B\nSELECT 2\n"
        );

        foreach (var block in blocks)
        {
            block
                .Markers.ColumnTypes.ShouldNotBeNull()
                .ShouldBe([
                    new ColumnType("posts.id", "PostId", SettingPlace.FileHeader),
                    new ColumnType("auth.\"Users\".id", null, SettingPlace.FileHeader),
                ]);
            block.ColumnMarkers.ShouldBeEmpty();
        }
    }

    // The header's entries reach each query's level, under a type override of the query's own.
    [Fact]
    public void Parse_ColumnMarker_InTheHeader_ReachesAQueryWithMarkersOfItsOwn() =>
        Blocks("-- column: posts.id -> PostId\n-- name: A\n-- output: sql\nSELECT 1\n")
            .ShouldHaveSingleItem()
            .Markers.ColumnTypes.ShouldNotBeNull()
            .ShouldBe([new ColumnType("posts.id", "PostId", SettingPlace.FileHeader)]);

    [Theory]
    [InlineData("posts.id not null -> PostId")]
    [InlineData("posts.id null -> PostId")]
    [InlineData("posts.id null")]
    [InlineData("posts.id")]
    [InlineData("id -> PostId")]
    [InlineData("total null")]
    [InlineData("posts.id -> int?")]
    [InlineData("a.b.c.d -> PostId")]
    public void Parse_ColumnMarker_InTheHeader_WithNullability_IsSQLSRC111(string value)
    {
        var text = "-- column: " + value + "\n-- name: Q\nSELECT 1\n";

        Errors(text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.InvalidMarkerValue,
                    TextSpans.Of(text, value),
                    "column: " + value,
                    ""
                ),
            ]);
    }

    [Theory]
    [InlineData("posts.id -> PostId")]
    [InlineData("posts.id null")]
    [InlineData("public.posts.id not null -> PostId")]
    [InlineData("posts.id")]
    public void Parse_ColumnMarker_InAQuery_DottedName_IsSQLSRC111(string value)
    {
        var text = "-- name: Q\n-- column: " + value + "\nSELECT 1\n";

        Errors(text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.InvalidMarkerValue,
                    TextSpans.Of(text, value),
                    "column: " + value,
                    ".  A column's source is mapped in the file header"
                ),
            ]);
    }

    // A file without a name marker takes both kinds: a key of two or three parts is the header's, one part the
    // query's.
    [Fact]
    public void Parse_ColumnMarker_InAWholeFile_DottedIsTheHeaders()
    {
        var block = Blocks("-- column: posts.id -> PostId\n-- column: total -> PostCount\nSELECT 1\n")
            .ShouldHaveSingleItem();

        block
            .Markers.ColumnTypes.ShouldNotBeNull()
            .ShouldBe([new ColumnType("posts.id", "PostId", SettingPlace.FileHeader)]);
        var column = block.ColumnMarkers.ShouldHaveSingleItem();
        column.Name.ShouldBe("total");
        column.ClrType.ShouldBe("PostCount");
    }

    [Fact]
    public void Parse_ColumnMarker_InAWholeFile_DottedWithNullability_IsSQLSRC111()
    {
        const string Value = "posts.id not null -> PostId";
        const string Text = "-- column: " + Value + "\nSELECT 1\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.InvalidMarkerValue,
                    TextSpans.Of(Text, Value),
                    "column: " + Value,
                    ""
                ),
            ]);
    }

    [Fact]
    public void Parse_ColumnMarkers_TypeAndNullabilityApart_AreNoConflict()
    {
        var markers = Blocks(
                "-- name: Q\n-- column: note null\n-- column: NOTE -> NoteText\n-- column: note null -> NoteText\n"
                    + "SELECT 1\n"
            )
            .ShouldHaveSingleItem()
            .ColumnMarkers;

        markers
            .Select(static marker => (marker.Name, marker.IsNullable, marker.ClrType))
            .ShouldBe([("note", true, null), ("NOTE", null, "NoteText")]);
    }

    [Theory]
    [InlineData("-- column: total -> PostCount", "total -> Count")]
    [InlineData("-- column: total -> PostCount", "TOTAL -> default")]
    [InlineData("-- column: total -> default", "total null -> PostCount")]
    [InlineData("-- column: total null -> PostCount", "total not null")]
    public void Parse_ColumnMarkers_TwoTypes_IsSQLSRC112(string first, string second)
    {
        var text = "-- name: Q\n" + first + "\n-- column: " + second + "\nSELECT 1\n";

        Errors(text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.ConflictingSettings,
                    new TextSpan(text.IndexOf("-- column: " + second, StringComparison.Ordinal) + 11, second.Length),
                    "column: " + second
                ),
            ]);
    }

    [Fact]
    public void Parse_HeaderColumnMarkers_TwoTypesForOneKey_IsSQLSRC112()
    {
        const string Second = "\"posts\".id -> Id";
        const string Text = "-- column: posts.id -> PostId\n-- column: " + Second + "\n-- name: Q\nSELECT 1\n";

        Errors(Text)
            .ShouldBe([
                SqlParseError.Create(
                    SqlParseErrorKind.ConflictingSettings,
                    TextSpans.Of(Text, Second),
                    "column: " + Second
                ),
            ]);
    }

    // A quoted part and a plain part of one text are one key; keys that differ only by case are two.
    [Fact]
    public void Parse_HeaderColumnMarkers_OneTypeTwice_IsNoConflict() =>
        Blocks(
                "-- column: posts.id -> PostId\n-- column: \"posts\".id -> PostId\n-- column: POSTS.ID -> Other\n"
                    + "-- name: Q\nSELECT 1\n"
            )
            .ShouldHaveSingleItem()
            .Markers.ColumnTypes.ShouldNotBeNull()
            .ShouldBe([
                new ColumnType("POSTS.ID", "Other", SettingPlace.FileHeader),
                new ColumnType("posts.id", "PostId", SettingPlace.FileHeader),
            ]);

    private static string[] Columns(SqlBlock block) =>
        [.. block.ColumnMarkers.Select(static column => column.Name + ":" + Nullability(column.IsNullable))];

    private static string Nullability(bool? isNullable) =>
        isNullable switch
        {
            true => "null",
            false => "not null",
            null => "",
        };
}
