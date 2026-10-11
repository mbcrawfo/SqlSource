using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class SqlMarkerScopeTests
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

    // A kind that is none of the three takes nothing: the scope does not guess what it is.
    [Fact]
    public void Read_ScopeOfAnUnknownKind_TakesNoMarker() =>
        IsTaken("-- generator: keep-comments", (SqlScopeKind)99).ShouldBeFalse();

    // A value that is not valid is another error, and is not what is asked here.
    private static bool IsTaken(string line, SqlScopeKind kind)
    {
        var marker = SqlMarkerReader.Read(line, SqlLexer.Lex(line, SqlDialectRules.Ansi).Lexemes[0]);
        var errors = new List<SqlParseError>();

        new SqlMarkerScope(line, SqlDialectRules.Ansi, kind, errors).Read(marker.ShouldNotBeNull());

        return !errors.Any(static error => error.Kind == SqlParseErrorKind.MarkerNotAllowedHere);
    }
}
