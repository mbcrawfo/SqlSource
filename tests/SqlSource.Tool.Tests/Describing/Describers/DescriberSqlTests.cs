using System;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Describers;

// The user's SQL as the lexer reads it for a dialect.  The plan's parser read the same text, so a text that does not
// lex is a bug, and throws.
public sealed class DescriberSqlTests
{
    private const string DoesNotLex = "The sample SQL of a query does not lex, and the plan's parser read it.";

    [Fact]
    public void Lex_PostgreSqlThatLexes_IsItsLexemes()
    {
        var lexemes = DescriberSql.Lex("SELECT 'a' -- c", SqlDialectRules.PostgreSql);

        lexemes.ShouldContain(lexeme => lexeme.Kind == SqlLexemeKind.Quoted);
        lexemes.ShouldContain(lexeme => lexeme.Kind == SqlLexemeKind.LineComment);
    }

    [Fact]
    public void Lex_PostgreSqlThatDoesNotLex_ThrowsInvalidOperationException() =>
        Should
            .Throw<InvalidOperationException>(() => DescriberSql.Lex("SELECT 'open", SqlDialectRules.PostgreSql))
            .Message.ShouldBe(DoesNotLex);

    [Fact]
    public void Lex_SqlServerSqlThatLexes_IsItsLexemes()
    {
        var lexemes = DescriberSql.Lex("SELECT [@a], @b -- c", SqlDialectRules.SqlServer);

        lexemes.ShouldContain(lexeme => lexeme.Kind == SqlLexemeKind.Quoted);
        lexemes.ShouldContain(lexeme => lexeme.Kind == SqlLexemeKind.Parameter);
        lexemes.ShouldContain(lexeme => lexeme.Kind == SqlLexemeKind.LineComment);
    }

    [Fact]
    public void Lex_SqlServerSqlThatDoesNotLex_ThrowsInvalidOperationException() =>
        Should
            .Throw<InvalidOperationException>(() => DescriberSql.Lex("SELECT [open", SqlDialectRules.SqlServer))
            .Message.ShouldBe(DoesNotLex);
}
