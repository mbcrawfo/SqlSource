using System;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Postgres.Statements;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres.Statements;

// The user's SQL as the lexer reads it for PostgreSQL.  The plan's parser read the same text, so a text that does not
// lex is a bug, and throws.
public sealed class PostgresSqlTests
{
    [Fact]
    public void Lex_SqlThatLexes_IsItsLexemes()
    {
        var lexemes = PostgresSql.Lex("SELECT 'a' -- c");

        lexemes.ShouldContain(lexeme => lexeme.Kind == SqlLexemeKind.Quoted);
        lexemes.ShouldContain(lexeme => lexeme.Kind == SqlLexemeKind.LineComment);
    }

    [Fact]
    public void Lex_SqlThatDoesNotLex_ThrowsInvalidOperationException() =>
        Should
            .Throw<InvalidOperationException>(() => PostgresSql.Lex("SELECT 'open"))
            .Message.ShouldBe("The sample SQL of a query does not lex, and the plan's parser read it.");
}
