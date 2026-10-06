using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
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
        static SqlBlock Create(string sql) =>
            new(
                "GetUser",
                new TextSpan(9, 7),
                "Loads a user.",
                PreserveComments: false,
                TokenValidation: null,
                new EquatableArray<SqlSegment>([
                    new SqlSegment(SqlSegmentKind.Literal, sql),
                    new SqlSegment(SqlSegmentKind.Token, "table"),
                ])
            );

        Create("SELECT 1 FROM ").ShouldBe(Create("SELECT 1 FROM "));
        Create("SELECT 1 FROM ").ShouldNotBe(Create("SELECT 2 FROM "));
    }
}
