using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Bundles.Renaming;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming;

public sealed class SqlRenamerTests
{
    private const string Place = "capture.json call 1 'prepare' request.sql";

    private const string Hex = "[0-9a-f]{8}";

    private static (SqlRenamer Renamer, NameMapping Mapping, List<RenamingRefusal> Refusals) Renamer(
        SqlDialect dialect = SqlDialect.PostgreSql,
        params string[] queryParameters
    )
    {
        var engine = new FakeEngineRenaming(dialect);
        var mapping = new NameMapping(RenamingKey.Of(new byte[RenamingKey.Length]), engine.Fold);
        var parameters = new HashSet<string>(queryParameters, SqlParameterName.Comparer);
        return (new SqlRenamer(engine, mapping, parameters, RenamingCopies.None), mapping, []);
    }

    private static string Rename(string sql, SqlDialect dialect = SqlDialect.PostgreSql)
    {
        var (renamer, _, refusals) = Renamer(dialect);
        var renamed = renamer.Rename(sql, NameClass.Alias, Place, refusals.Add).Text;
        refusals.ShouldBeEmpty();
        return renamed;
    }

    private static string Of(NameMapping mapping, string spelling, bool quoted = false)
    {
        mapping.TryGet(spelling, quoted, out var replacement).ShouldBeTrue(spelling);
        return replacement;
    }

    // The dialect of a theory's row, which names it as a query file does.
    private static SqlDialect DialectOf(string name)
    {
        SqlDialectName.TryParse(name, out var choice).ShouldBeTrue(name);
        return choice.Dialect;
    }

    [Fact]
    public void Rename_Words_KeepsEngineWordsAndRenamesTheRest()
    {
        var (renamer, mapping, refusals) = Renamer();

        var renamed = renamer.Rename("SELECT u.id FROM users u", NameClass.Alias, Place, refusals.Add).Text;

        refusals.ShouldBeEmpty();
        renamed.ShouldMatch($"^SELECT a_{Hex}\\.a_{Hex} FROM a_{Hex} a_{Hex}$");
        renamed.ShouldBe(
            $"SELECT {Of(mapping, "u")}.{Of(mapping, "id")} FROM {Of(mapping, "users")} {Of(mapping, "u")}"
        );
        Of(mapping, "users").ShouldNotBe(Of(mapping, "u"));
    }

    [Fact]
    public void Rename_WordBeforeAParenthesis_IsAFunction() =>
        Rename("SELECT lower (name) FROM t").ShouldMatch($"^SELECT f_{Hex} \\(a_{Hex}\\) FROM a_{Hex}$");

    [Theory]
    [InlineData("E'", "'")]
    [InlineData("N'", "'")]
    [InlineData("U&'", "'")]
    [InlineData("B'", "'")]
    [InlineData("$q$", "$q$")]
    [InlineData("$$", "$$")]
    public void Rename_StringLiteral_KeepsItsForm(string open, string close) =>
        Rename($"SELECT {open}x y{close}").ShouldMatch($"^SELECT {Regex.Escape(open)}l_{Hex}{Regex.Escape(close)}$");

    [Fact]
    public void Rename_SameLiteralTwice_IsOneName()
    {
        var (renamer, mapping, refusals) = Renamer();

        var renamed = renamer.Rename("SELECT 'it''s', 'it''s', 'b'", NameClass.Alias, Place, refusals.Add).Text;

        var literal = mapping.Literal("it's");
        renamed.ShouldBe($"SELECT '{literal}', '{literal}', '{mapping.Literal("b")}'");
        literal.ShouldNotBe(mapping.Literal("b"));
    }

    [Fact]
    public void Rename_Comments_AreEmptiedKeepingLineBreaks() =>
        Rename("SELECT 1 -- the users\r\nFROM /* one\ntwo\r\nthree */ t")
            .ShouldMatch($"^SELECT 1 --\r\nFROM /\\*\n\r\n\\*/ a_{Hex}$");

    [Fact]
    public void Rename_Hint_KeepsItsFormAndRenamesWords() =>
        Rename("SELECT /*+ IndexScan(users) */ 1").ShouldMatch($"^SELECT /\\*\\+ f_{Hex}\\(a_{Hex}\\) \\*/ 1$");

    [Fact]
    public void Rename_QuotedName_KeepsDelimitersAndNullabilityMark()
    {
        Rename("SELECT 1 AS \"name!\", 2 AS \"?column?\", 3 AS \"Users\", \"public\".t")
            .ShouldMatch($"^SELECT 1 AS \"a_{Hex}!\", 2 AS \"\\?column\\?\", 3 AS \"a_{Hex}\", \"public\"\\.a_{Hex}$");
        Rename("SELECT 1 AS [x?], 2 AS [y]", SqlDialect.SqlServer)
            .ShouldMatch($"^SELECT 1 AS \\[a_{Hex}\\?\\], 2 AS \\[a_{Hex}\\]$");
    }

    [Fact]
    public void Rename_QuotedAndUnquoted_AreNamesAsTheEngineFolds()
    {
        var (renamer, mapping, refusals) = Renamer();

        _ = renamer.Rename("SELECT Users, users, \"Users\", \"users\"", NameClass.Alias, Place, refusals.Add);

        Of(mapping, "users").ShouldBe(Of(mapping, "users", quoted: true));
        Of(mapping, "Users", quoted: true).ShouldNotBe(Of(mapping, "users"));
    }

    [Theory]
    [InlineData("postgres")]
    [InlineData("mssql")]
    public void Rename_Numbers_AreKept(string dialect)
    {
        const string Numbers = "SELECT 1, 2.5, 1e10, 1e5, .5, 1.5E-3, 1.5e-3, 2E+4, 1_000, 1_000.000_1, 1., 1.e5, 2-1";

        Rename(Numbers, DialectOf(dialect)).ShouldBe(Numbers);
    }

    // A run that starts with a digit and is no number is refused whole: PostgreSQL before 15 reads 1users as 1 AS
    // users, and a hex or binary constant holds a value as a string does.
    [Theory]
    [InlineData("postgres", "SELECT 1users", "1users")]
    [InlineData("postgres", "SELECT 2secret_column FROM t", "2secret_column")]
    [InlineData("postgres", "SELECT 1.users", "1.users")]
    [InlineData("postgres", "SELECT 1e5users", "1e5users")]
    [InlineData("postgres", "SELECT 1e", "1e")]
    [InlineData("postgres", "SELECT 1e+", "1e")]
    [InlineData("postgres", "SELECT 1_", "1_")]
    [InlineData("postgres", "SELECT 1__000", "1__000")]
    [InlineData("postgres", "SELECT 1.2.3", "1.2.3")]
    [InlineData("postgres", "SELECT 0x1F", "0x1F")]
    [InlineData("postgres", "SELECT 0b101", "0b101")]
    [InlineData("mssql", "SELECT 0x7365637265743132", "0x7365637265743132")]
    [InlineData("mssql", "SELECT 1users", "1users")]
    public void Rename_RunFromADigitThatIsNoNumber_IsRefused(string dialect, string sql, string token)
    {
        var (renamer, _, refusals) = Renamer(DialectOf(dialect));

        _ = renamer.Rename(sql, NameClass.Alias, Place, refusals.Add);

        refusals.ShouldHaveSingleItem().ShouldBe(new RenamingRefusal(RefusalStep.Token, Place, token));
    }

    [Fact]
    public void Rename_SqlServerPrefixes_AreKeptBeforeTheRenamedRest() =>
        Rename("SELECT @@ROWCOUNT FROM #orders JOIN ##totals ON 1 = 1", SqlDialect.SqlServer)
            .ShouldMatch($"^SELECT @@ROWCOUNT FROM #a_{Hex} JOIN ##a_{Hex} ON 1 = 1$");

    [Fact]
    public void Rename_Parameter_KeepsItsPrefix()
    {
        var (renamer, mapping, refusals) = Renamer(SqlDialect.PostgreSql, "id");

        var renamed = renamer.Rename("SELECT 1 WHERE id = @id", NameClass.Alias, Place, refusals.Add).Text;

        renamed.ShouldBe($"SELECT 1 WHERE {Of(mapping, "id")} = @{Of(mapping, "id")}");
        mapping.Names.ShouldHaveSingleItem().Class.ShouldBe(NameClass.Alias);
    }

    [Fact]
    public void Rename_ParameterSpelledAsAnEngineWord_IsKept()
    {
        var (renamer, _, refusals) = Renamer(SqlDialect.PostgreSql, "count");

        renamer.Rename("SELECT @count", NameClass.Alias, Place, refusals.Add).Text.ShouldBe("SELECT @count");
    }

    // A copy is renamed through what the capture records, whatever its spelling: a name that looks like a copy and
    // that no call records is a name of its own.
    [Fact]
    public void Rename_CopyOfAReusedParameter_IsRenamedThroughIt()
    {
        var engine = new FakeEngineRenaming(SqlDialect.SqlServer);
        var mapping = new NameMapping(RenamingKey.Of(new byte[RenamingKey.Length]), engine.Fold);
        var request = new JsonObject
        {
            ["copies"] = FakeEngineRenaming.Copies(("before_1", "before", "_1"), ("Before__x", "before", "_2")),
        };
        var copies = RenamingCopies.Of(
            engine,
            [new RenamingCall(0, "describe", new JsonObject { ["request"] = request }, [])],
            static call => call.Original
        );
        var renamer = new SqlRenamer(
            engine,
            mapping,
            new HashSet<string>(["before"], SqlParameterName.Comparer),
            copies
        );
        List<RenamingRefusal> refusals = [];

        var renamed = renamer
            .Rename("SELECT @before, @before_1, @BEFORE__X, @before_2", NameClass.Alias, Place, refusals.Add)
            .Text;

        refusals.ShouldBeEmpty();
        var before = Of(mapping, "before");
        before.ShouldMatch($"^p_{Hex}$");
        renamed.ShouldBe($"SELECT @{before}, @{before}_1, @{before}_2, @{Of(mapping, "before_2")}");
    }

    // PostgreSQL reads every character outside ASCII as a word's, so SQL Server's rule, which reads letters alone, has
    // the characters that are none.
    [Fact]
    public void Rename_NonAsciiPunctuation_IsRefused()
    {
        var (renamer, _, refusals) = Renamer(SqlDialect.SqlServer);

        _ = renamer.Rename("SELECT 1 ≠ 2", NameClass.Alias, Place, refusals.Add);

        refusals.ShouldHaveSingleItem().ShouldBe(new RenamingRefusal(RefusalStep.Token, Place, "≠"));
    }

    [Fact]
    public void Rename_CharacterOutsideTheBasicPlane_IsRefusedWhole()
    {
        var (renamer, _, refusals) = Renamer(SqlDialect.SqlServer);

        _ = renamer.Rename("SELECT 1 \U0001F600 2", NameClass.Alias, Place, refusals.Add);

        refusals.ShouldHaveSingleItem().Token.ShouldBe("\U0001F600");
    }

    [Fact]
    public void Rename_NonAsciiOfAPostgresWord_IsRenamed() =>
        Rename("SELECT 1 ≠ 2, café").ShouldMatch($"^SELECT 1 a_{Hex} 2, a_{Hex}$");

    [Fact]
    public void Rename_SqlThatDoesNotLex_IsRefused()
    {
        var (renamer, _, refusals) = Renamer();

        _ = renamer.Rename("SELECT 'open", NameClass.Alias, Place, refusals.Add);

        refusals.ShouldHaveSingleItem().ShouldBe(new RenamingRefusal(RefusalStep.Token, Place, "'"));
    }

    [Fact]
    public void MapPosition_AfterALongerName_MovesByTheDifference()
    {
        var (renamer, _, refusals) = Renamer();
        const string Sql = "SELECT 1 FROM users WHERE";

        var renamed = renamer.Rename(Sql, NameClass.Alias, Place, refusals.Add);

        var growth = renamed.Text.Length - Sql.Length;
        growth.ShouldBe("a_12345678".Length - "users".Length);
        renamed.MapPosition(1).ShouldBe(1);
        renamed.MapPosition(Sql.IndexOf("users", System.StringComparison.Ordinal) + 1).ShouldBe(15);
        renamed.MapPosition(Sql.IndexOf("WHERE", System.StringComparison.Ordinal) + 1).ShouldBe(21 + growth);
        renamed.MapPosition(Sql.Length + 1).ShouldBe(renamed.Text.Length + 1);
    }

    [Fact]
    public void MapPosition_CountsCodePoints()
    {
        var (renamer, _, refusals) = Renamer();
        const string Sql = "SELECT '\U0001F600' FROM t";

        var renamed = renamer.Rename(Sql, NameClass.Alias, Place, refusals.Add);

        // The literal is one code point and two chars; FROM is the 12th code point and the 13th char.
        renamed.MapPosition(12).ShouldBe(renamed.Text.IndexOf("FROM", System.StringComparison.Ordinal) + 1);
        renamed.Offsets.Length.ShouldBe(Sql.Length);
    }

    [Fact]
    public void RenameQueryFile_Markers_KeepTheirKindAndRenameTheirValues()
    {
        var (renamer, mapping, refusals) = Renamer(SqlDialect.PostgreSql, "user_id");
        const string File = """
            -- dialect: postgres
            -- database: postgres
            -- A comment of the file.
            -- name: GetUsers -> one-optional
            -- database: shop
            -- param: @user_id my_id not null -> UserId
            -- token: {{filter:AND deleted /* gone */}}
            -- token-ignore: raw
            SELECT id FROM users WHERE id = @user_id {{filter}} {{ other : AND 'x' }}
            """;

        var renamed = renamer.RenameQueryFile(File, refusals.Add).Text;

        refusals.ShouldBeEmpty();
        var filter = Of(mapping, "filter");
        renamed.ShouldBe(
            $"""
            -- dialect: postgres
            -- database: postgres
            --
            -- name: {Of(mapping, "GetUsers")} -> one-optional
            -- database: {Of(mapping, "shop")}
            -- param: @{Of(mapping, "user_id")} {Of(mapping, "my_id")} not null
            -- token: {"{{"}{filter}:AND {Of(mapping, "deleted")} /**/{"}}"}
            -- token-ignore: {Of(mapping, "raw")}
            SELECT {Of(mapping, "id")} FROM {Of(mapping, "users")} WHERE {Of(mapping, "id")} = @{Of(
                mapping,
                "user_id"
            )} {"{{"}{filter}{"}}"} {"{{"} {Of(mapping, "other")} : AND '{mapping.Literal("x")}' {"}}"}
            """
        );
        mapping.Names.ShouldContain(new MappedName(Of(mapping, "GetUsers"), NameClass.Query, "GetUsers"));
        mapping.Names.ShouldContain(new MappedName(Of(mapping, "shop"), NameClass.Database, "shop"));
        mapping.Names.ShouldContain(new MappedName(Of(mapping, "user_id"), NameClass.Parameter, "user_id"));
        mapping.Names.ShouldContain(new MappedName(Of(mapping, "my_id"), NameClass.Type, "my_id"));
        mapping.Names.ShouldContain(new MappedName(filter, NameClass.Token, "filter"));
        mapping.Names.ShouldContain(new MappedName(Of(mapping, "other"), NameClass.Token, "other"));
        mapping.Names.ShouldContain(new MappedName(Of(mapping, "raw"), NameClass.Token, "raw"));
    }

    [Theory]
    [InlineData("-- summary: Gets the users", "-- summary:")]
    [InlineData("-- output: codegen", "-- output:")]
    [InlineData("-- column: total not null", "-- column:")]
    public void RenameQueryFile_UnknownMarker_IsRefused(string marker, string token)
    {
        var (renamer, _, refusals) = Renamer();

        _ = renamer.RenameQueryFile($"-- name: Get\n{marker}\nSELECT 1", refusals.Add);

        refusals.ShouldHaveSingleItem().ShouldBe(new RenamingRefusal(RefusalStep.Token, "query.sql", token));
    }

    [Theory]
    [InlineData("-- name: Get -> sometimes")]
    [InlineData("-- dialect: postgres-ish")]
    [InlineData("-- param: id int")]
    [InlineData("-- token: {{filter}}")]
    public void RenameQueryFile_MarkerThatDoesNotRead_IsRefused(string marker)
    {
        var (renamer, _, refusals) = Renamer();

        _ = renamer.RenameQueryFile($"{marker}\nSELECT 1", refusals.Add);

        var refusal = refusals.ShouldHaveSingleItem();
        refusal.Step.ShouldBe(RefusalStep.Token);
        refusal.Place.ShouldBe("query.sql");
    }
}
