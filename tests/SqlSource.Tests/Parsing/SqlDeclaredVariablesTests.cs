using System;
using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class SqlDeclaredVariablesTests
{
    [Theory]
    [InlineData("DECLARE @n int", new[] { "n" })]
    [InlineData("declare @n int = 1;", new[] { "n" })]
    [InlineData("Declare\t@größe int", new[] { "größe" })]
    [InlineData("DECLARE @a int, @b varchar(10)", new[] { "a", "b" })]
    [InlineData("DECLARE\n    @a int,\n    @b int", new[] { "a", "b" })]
    [InlineData("DECLARE @a int\nDECLARE @b int", new[] { "a", "b" })]
    [InlineData("DECLARE @a int DECLARE @b int", new[] { "a", "b" })]
    [InlineData("DECLARE @t TABLE (id int, name varchar(10))", new[] { "t" })]
    [InlineData("DECLARE @c CURSOR", new[] { "c" })]
    [InlineData("IF @p = 1 BEGIN DECLARE @a int = 1 END", new[] { "a" })]
    [InlineData("SELECT xDECLARE, DECLARED DECLARE @a int", new[] { "a" })]
    [InlineData("DECLARE @a int; SELECT 1, @p DECLARE @b int", new[] { "a", "b" })]
    [InlineData("SELECT 'x'DECLARE @a int", new[] { "a" })]
    [InlineData("DECLARE", new string[0])]
    public void Find_NameAfterDeclareOrAfterACommaOfItsList_IsDeclared(string sql, string[] expected) =>
        Find(sql).ShouldBe(expected);

    [Theory]
    [InlineData("DECLARE @a int = @p", new[] { "a" })]
    [InlineData("DECLARE @a int = @p, @b int = @a + @q", new[] { "a", "b" })]
    [InlineData("DECLARE @a int = dbo.f(@p, @q), @b int", new[] { "a", "b" })]
    [InlineData("DECLARE @a int = (SELECT max(x), @p FROM t), @b int", new[] { "a", "b" })]
    [InlineData("DECLARE @a int = CASE WHEN @p = 1 THEN 1 ELSE 2 END, @b int", new[] { "a", "b" })]
    [InlineData("DECLARE @t TABLE (id int)\nINSERT @t VALUES (@p), (@q)", new[] { "t" })]
    public void Find_NameInsideAnInitializer_IsNotDeclared(string sql, string[] expected) =>
        Find(sql).ShouldBe(expected);

    // T-SQL needs no semicolon, so a word that starts a statement is what ends a list.
    [Theory]
    [InlineData("DECLARE @a int; SELECT x, @p FROM t")]
    [InlineData("DECLARE @a int = 1 SELECT x, @p FROM t")]
    [InlineData("DECLARE @a int select x, @p FROM t")]
    [InlineData("DECLARE @a int = 1 SET @a = 2 SELECT x, @p")]
    public void Find_NameAfterTheDeclarationHasEnded_IsNotDeclared(string sql) => Find(sql).ShouldBe(["a"]);

    // Every reserved word of T-SQL that starts a statement, and THROW, which is not reserved.
    [Theory]
    [InlineData("ALTER")]
    [InlineData("BACKUP")]
    [InlineData("BEGIN")]
    [InlineData("BREAK")]
    [InlineData("BULK")]
    [InlineData("CHECKPOINT")]
    [InlineData("CLOSE")]
    [InlineData("COMMIT")]
    [InlineData("CONTINUE")]
    [InlineData("CREATE")]
    [InlineData("DBCC")]
    [InlineData("DEALLOCATE")]
    [InlineData("DELETE")]
    [InlineData("DENY")]
    [InlineData("DROP")]
    [InlineData("EXEC")]
    [InlineData("EXECUTE")]
    [InlineData("FETCH")]
    [InlineData("GOTO")]
    [InlineData("GRANT")]
    [InlineData("IF")]
    [InlineData("INSERT")]
    [InlineData("KILL")]
    [InlineData("MERGE")]
    [InlineData("OPEN")]
    [InlineData("PRINT")]
    [InlineData("RAISERROR")]
    [InlineData("READTEXT")]
    [InlineData("RECONFIGURE")]
    [InlineData("RESTORE")]
    [InlineData("RETURN")]
    [InlineData("REVERT")]
    [InlineData("REVOKE")]
    [InlineData("ROLLBACK")]
    [InlineData("SAVE")]
    [InlineData("SELECT")]
    [InlineData("SET")]
    [InlineData("SETUSER")]
    [InlineData("SHUTDOWN")]
    [InlineData("THROW")]
    [InlineData("TRUNCATE")]
    [InlineData("UPDATE")]
    [InlineData("UPDATETEXT")]
    [InlineData("USE")]
    [InlineData("WAITFOR")]
    [InlineData("WHILE")]
    [InlineData("WITH")]
    [InlineData("WRITETEXT")]
    public void Find_WordThatStartsAStatement_EndsTheDeclaration(string word) =>
        Find("DECLARE @a int = 1 " + word + " x, @p").ShouldBe(["a"]);

    // Inside parentheses the word is part of an expression: a subquery.
    [Fact]
    public void Find_WordThatStartsAStatementInsideParentheses_DoesNotEndTheDeclaration() =>
        Find("DECLARE @a int = (SELECT 1), @b int").ShouldBe(["a", "b"]);

    [Theory]
    [InlineData("SELECT @p, @q")]
    [InlineData("DECLARE c CURSOR FOR SELECT a, @p FROM t")]
    [InlineData("SELECT 'DECLARE', @p")]
    [InlineData("SELECT [DECLARE], @p")]
    [InlineData("SELECT \"DECLARE\" @p")]
    [InlineData("SELECT x_DECLARE @p")]
    [InlineData("SELECT #DECLARE @p")]
    [InlineData("SELECT DECLARED @p")]
    [InlineData("DECLARE = @p")]
    public void Find_SqlThatDeclaresNoVariable_FindsNone(string sql) => Find(sql).ShouldBeEmpty();

    // A parenthesis or a comma inside a string is not one of the list.
    [Theory]
    [InlineData("DECLARE @a varchar(10) = '(', @b int", new[] { "a", "b" })]
    [InlineData("DECLARE @a varchar(10) = 'x, @p', @b int", new[] { "a", "b" })]
    [InlineData("DECLARE @a varchar(10) = [x;y], @b int", new[] { "a", "b" })]
    public void Find_QuotedRegionInsideADeclaration_IsPassedOver(string sql, string[] expected) =>
        Find(sql).ShouldBe(expected);

    // A hint is kept in the SQL of a query, and is no part of a declaration.
    [Fact]
    public void Find_HintBetweenDeclareAndTheName_IsPassedOver() =>
        Find("DECLARE /*+ h */ @a int, /*+ h */ @b int").ShouldBe(["a", "b"]);

    [Fact]
    public void Find_ClosingParenthesisWithoutAnOpeningOne_DoesNotHideTheRestOfTheList() =>
        Find("DECLARE @a int = 1), @b int").ShouldBe(["a", "b"]);

    // The lexemes are the file's, so a comment is still among them.
    [Theory]
    [InlineData("DECLARE /* c */ @a int, -- c\n @b int")]
    [InlineData("DECLARE -- generator: default\n @a int /* , @p */, @b int")]
    public void Find_CommentInsideADeclaration_IsPassedOver(string sql) => Find(sql).ShouldBe(["a", "b"]);

    [Fact]
    public void Find_WordDeclareOnlyInAComment_FindsNone() => Find("SELECT @p -- DECLARE @p int\n, @q").ShouldBeEmpty();

    // A query is a range of its file's lexemes, and a declaration of another query is not its own.
    [Fact]
    public void Find_RangeOfLexemes_ReadsOnlyThatRange()
    {
        const string Sql = "DECLARE @a int; DECLARE @b int";
        var lexemes = SqlLexer.Lex(Sql, SqlDialectRules.SqlServer).Lexemes;
        lexemes.Count.ShouldBe(5);

        Names(Sql, SqlDeclaredVariables.Find(Sql, lexemes, 0, 2, SqlDialectRules.SqlServer)).ShouldBe(["a"]);
        Names(Sql, SqlDeclaredVariables.Find(Sql, lexemes, 2, 5, SqlDialectRules.SqlServer)).ShouldBe(["b"]);
        SqlDeclaredVariables.Find(Sql, lexemes, 4, 5, SqlDialectRules.SqlServer).ShouldBeNull();
        SqlDeclaredVariables.Find(Sql, lexemes, 2, 2, SqlDialectRules.SqlServer).ShouldBeNull();
    }

    [Theory]
    [InlineData("n", true)]
    [InlineData("N", true)]
    [InlineData("m", false)]
    [InlineData("nn", false)]
    [InlineData("", false)]
    public void Holds_Name_ComparesIgnoringCase(string name, bool expected)
    {
        const string Sql = "DECLARE @n int";
        var lexemes = SqlLexer.Lex(Sql, SqlDialectRules.SqlServer).Lexemes;

        SqlDeclaredVariables
            .Find(Sql, lexemes, 0, lexemes.Count, SqlDialectRules.SqlServer)
            .ShouldNotBeNull()
            .Holds(name.AsSpan())
            .ShouldBe(expected);
    }

    [Theory]
    [InlineData(nameof(SqlDialect.Ansi))]
    [InlineData(nameof(SqlDialect.PostgreSql))]
    [InlineData(nameof(SqlDialect.CockroachDb))]
    [InlineData(nameof(SqlDialect.MySql))]
    [InlineData(nameof(SqlDialect.MariaDb))]
    [InlineData(nameof(SqlDialect.Sqlite))]
    [InlineData(nameof(SqlDialect.Oracle))]
    public void Find_DialectWithoutDeclaredVariables_FindsNone(string dialect)
    {
        const string Sql = "DECLARE @n int";
        var rules = SqlDialectRules.For(Enum.Parse<SqlDialect>(dialect));
        var lexemes = SqlLexer.Lex(Sql, rules).Lexemes;

        SqlDeclaredVariables.Find(Sql, lexemes, 0, lexemes.Count, rules).ShouldBeNull();
    }

    private static string[] Find(string sql)
    {
        var lexemes = SqlLexer.Lex(sql, SqlDialectRules.SqlServer).Lexemes;
        return Names(sql, SqlDeclaredVariables.Find(sql, lexemes, 0, lexemes.Count, SqlDialectRules.SqlServer));
    }

    private static string[] Names(string sql, SqlDeclaredVariables? variables) =>
        variables is null ? [] : [.. variables.Names.Select(span => sql.Substring(span.Start, span.Length))];
}
