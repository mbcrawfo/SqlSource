using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Describers;

// The three errors both describers build, with what each engine passes in: the descriptor, the arguments in their
// order, the step, the server's lines and the help are what a run reports.
public sealed class DescribeFailuresTests
{
    private static readonly DescribeRequest Query = new("GetUser", "SELECT 1", EquatableArray<SqlQueryParameter>.Empty);

    [Fact]
    public void Texts_OfTheFailures_AreTheOnesBothEnginesReport()
    {
        DescribeFailures.NoSuchType.ShouldBe("the database has no such type");
        DescribeFailures.NotATypeName.ShouldBe("it is not written as a type name");
    }

    [Fact]
    public void Open_WithLinesAndHelp_GivesTheDatabaseTheLinesAndTheHelp()
    {
        var lines = new EquatableArray<string>(["28P01: password authentication failed", "detail"]);

        var failure = DescribeFailures.Open("app", lines, "check the database");

        failure.Descriptor.ShouldBe(ToolDiagnostics.DatabaseCannotBeOpened);
        failure.Arguments.ShouldBe(["app"]);
        failure.Step.ShouldBeNull();
        failure.ServerLines.ShouldBe(lines);
        failure.Help.ShouldBe("check the database");
    }

    [Fact]
    public void VersionNotSupported_OfAServer_GivesTheDatabaseTheEngineTheVersionAndTheMinimumInOrder()
    {
        var failure = DescribeFailures.VersionNotSupported("app", "PostgreSQL", "13.4", "14", "upgrade the server");

        failure.Descriptor.ShouldBe(ToolDiagnostics.ServerVersionNotSupported);
        failure.Arguments.ShouldBe(["app", "PostgreSQL", "13.4", "14"]);
        failure.Step.ShouldBeNull();
        failure.ServerLines.ShouldBe(EquatableArray<string>.Empty);
        failure.Help.ShouldBe("upgrade the server");
    }

    [Fact]
    public void DeclaredType_ThatIsNotValid_GivesTheTypeTheParameterTheQueryAndTheReasonInOrder()
    {
        var lines = new EquatableArray<string>(["42704: type \"nope_t\" does not exist"]);

        var failure = DescribeFailures.DeclaredType(
            Query,
            "@p",
            "nope_t",
            DescribeFailures.NoSuchType,
            DescribeStep.DescribeParameters,
            lines,
            "write a type"
        );

        failure.Descriptor.ShouldBe(ToolDiagnostics.DeclaredTypeNotValid);
        failure.Arguments.ShouldBe(["nope_t", "@p", "GetUser", DescribeFailures.NoSuchType]);
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        failure.ServerLines.ShouldBe(lines);
        failure.Help.ShouldBe("write a type");
    }

    [Fact]
    public void DeclaredType_NoTypeWritten_ReadsTheTypeAsEmpty()
    {
        var failure = DescribeFailures.DeclaredType(
            Query,
            "@p",
            null,
            "it is a pseudo-type",
            DescribeStep.ResolveType,
            EquatableArray<string>.Empty,
            "declare a type"
        );

        failure.Arguments.ShouldBe(["", "@p", "GetUser", "it is a pseudo-type"]);
        failure.Step.ShouldBe(DescribeStep.ResolveType);
    }
}
