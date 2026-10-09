using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class SqlModelTests
{
    [Fact]
    public void SqlParseError_Create_HoldsItsArguments()
    {
        var error = SqlParseError.Create(SqlParseErrorKind.InvalidName, new TextSpan(3, 4), "1abc");

        error.Kind.ShouldBe(SqlParseErrorKind.InvalidName);
        error.Span.ShouldBe(new TextSpan(3, 4));
        error.Arguments.ShouldBe(["1abc"]);
    }

    [Fact]
    public void SqlParseError_SameValuesInSeparateInstances_AreEqual()
    {
        var left = SqlParseError.Create(SqlParseErrorKind.DuplicateName, new TextSpan(1, 2), "A");
        var right = SqlParseError.Create(SqlParseErrorKind.DuplicateName, new TextSpan(1, 2), "A");

        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
        left.ShouldNotBe(SqlParseError.Create(SqlParseErrorKind.DuplicateName, new TextSpan(1, 2), "B"));
    }

    [Fact]
    public void SqlBlock_SameValuesInSeparateInstances_AreEqual()
    {
        static SqlBlock Create(
            string sql,
            string parameter = "id",
            string? tokenDefault = null,
            ResultShape? shape = null,
            string? keptSql = null,
            SettingsLevel? markers = null
        )
        {
            EquatableArray<SqlSegment>? kept = keptSql is null
                ? null
                : new EquatableArray<SqlSegment>([new SqlSegment(SqlSegmentKind.Literal, keptSql)]);
            return new SqlBlock(
                "GetUser",
                new TextSpan(9, 7),
                "Loads a user.",
                shape,
                new EquatableArray<SqlSegment>([
                    new SqlSegment(SqlSegmentKind.Literal, sql),
                    new SqlSegment(SqlSegmentKind.Token, "table"),
                ]),
                kept,
                new EquatableArray<SqlToken>([new SqlToken("table", tokenDefault)]),
                new EquatableArray<SqlQueryParameter>([new SqlQueryParameter(parameter, null, null, false)]),
                markers ?? SettingsLevel.None,
                null,
                null
            );
        }

        Create("SELECT 1 FROM ").ShouldBe(Create("SELECT 1 FROM "));
        Create("SELECT 1 FROM ").ShouldNotBe(Create("SELECT 2 FROM "));
        Create("SELECT 1 FROM ").ShouldNotBe(Create("SELECT 1 FROM ", parameter: "other"));
        Create("SELECT 1 FROM ").ShouldNotBe(Create("SELECT 1 FROM ", tokenDefault: "users"));
        Create("SELECT 1 FROM ").ShouldNotBe(Create("SELECT 1 FROM ", shape: ResultShape.One));
        Create("SELECT 1 FROM ", keptSql: "SELECT 1 -- c").ShouldBe(Create("SELECT 1 FROM ", keptSql: "SELECT 1 -- c"));
        Create("SELECT 1 FROM ").ShouldNotBe(Create("SELECT 1 FROM ", keptSql: "SELECT 1 -- c"));
        Create("SELECT 1 FROM ", markers: new SettingsLevel { Parameters = GeneratorParameters.SortInput })
            .ShouldBe(
                Create("SELECT 1 FROM ", markers: new SettingsLevel { Parameters = GeneratorParameters.SortInput })
            );
        Create("SELECT 1 FROM ")
            .ShouldNotBe(
                Create("SELECT 1 FROM ", markers: new SettingsLevel { Parameters = GeneratorParameters.SortInput })
            );
    }
}
