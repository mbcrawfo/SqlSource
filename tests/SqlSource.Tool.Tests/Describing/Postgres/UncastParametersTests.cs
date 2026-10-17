using System.Linq;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;
using static SqlSource.Tool.Tests.PostgresRequests;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The parameters of no declared type with a use that no cast fixes, which a type error at "prepare" may be the fault
// of.  The SQL is read in words as PostgreSQL reads them.
public sealed class UncastParametersTests
{
    [Theory]
    [InlineData("SELECT @x::int")]
    [InlineData("SELECT CAST(@x AS int)")]
    [InlineData("SELECT cast ( /* c */ @x as int)")]
    public void Of_EveryUseCast_IsNone(string sql) => UncastParameters.Of(Of(sql, Inferred("x"))).ShouldBeEmpty();

    [Theory]
    [InlineData("SELECT @x + 1")]
    [InlineData("SELECT @x::int, @x")]
    [InlineData("SELECT foo(@x AS int)")]
    // PostgreSQL reads € as a letter, so €CAST is one word, and no CAST.
    [InlineData("SELECT €CAST(@x AS int)")]
    [InlineData("SELECT x$CAST(@x AS int)")]
    public void Of_UseWithoutACast_IsTheParameter(string sql) =>
        UncastParameters.Of(Of(sql, Inferred("x"))).Select(parameter => parameter.Name).ShouldBe(["x"]);

    [Fact]
    public void Of_DeclaredParameter_IsNone() =>
        UncastParameters.Of(Of("SELECT @x + 1", Declared("x", "int"))).ShouldBeEmpty();

    // The help is no place to fail: a SQL that does not lex names every parameter of no declared type.
    [Fact]
    public void Of_SqlThatDoesNotLex_IsEveryUndeclaredParameter() =>
        UncastParameters
            .Of(Of("SELECT @x::int, @y, 'open", Inferred("x"), Declared("y", "int"), Inferred("z")))
            .Select(parameter => parameter.Name)
            .ShouldBe(["x", "z"]);
}
