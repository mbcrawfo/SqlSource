using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;
using Xunit;
using TypeNameFound = SqlSource.Tool.Describing.Describers.DescribeFailures.TypeNameFound;

namespace SqlSource.Tool.Tests.Describing.Describers;

// The three errors both describers build, with what each engine passes in: the descriptor, the arguments in their
// order, the step, the server's lines and the help are what a run reports.  And what both engines' readers of a
// declared type say they found.
public sealed class DescribeFailuresTests
{
    private const string Shape = "the shape";

    private static readonly DescribeRequest Query = new("GetUser", "SELECT 1", EquatableArray<SqlQueryParameter>.Empty);

    [Fact]
    public void Texts_OfTheFailures_AreTheOnesBothEnginesReport()
    {
        DescribeFailures.NoSuchType.ShouldBe("the database has no such type");
        DescribeFailures.NotATypeName.ShouldBe("it is not written as a type name");
    }

    [Fact]
    public void Texts_OfWhatWasFound_AreTheOnesBothEnginesReport()
    {
        TypeNameFound.Semicolon.ShouldBe("a ; is not part of a type name");
        TypeNameFound.Comment.ShouldBe("a comment is not part of a type name");
        TypeNameFound.Parts.ShouldBe("a type name has one or two parts");
        TypeNameFound.OneLine.ShouldBe("a type name is on one line");
    }

    [Fact]
    public void Reason_OfWhatWasFound_FollowsTheHead() =>
        TypeNameFound.Reason("found").ShouldBe("it is not written as a type name: found");

    // What a reader found where the grammar was left: a ";", a comment and a line break by name, and the end or
    // anything else as the shape the reader passes in.
    [Theory]
    [InlineData("int;", 3, TypeNameFound.Semicolon)]
    [InlineData("int --", 4, TypeNameFound.Comment)]
    [InlineData("int/*", 3, TypeNameFound.Comment)]
    [InlineData("int\n", 3, TypeNameFound.OneLine)]
    [InlineData("int\r", 3, TypeNameFound.OneLine)]
    [InlineData("int\u0085", 3, TypeNameFound.OneLine)]
    [InlineData("int\u2028", 3, TypeNameFound.OneLine)]
    [InlineData("int\u2029", 3, TypeNameFound.OneLine)]
    [InlineData("int-", 3, Shape)]
    [InlineData("int-1", 3, Shape)]
    [InlineData("int/", 3, Shape)]
    [InlineData("int)", 3, Shape)]
    [InlineData("int", 3, Shape)]
    [InlineData("", 0, Shape)]
    public void At_APlace_SaysWhatStandsThere(string text, int position, string found) =>
        TypeNameFound.At(text, position, Shape).ShouldBe(found, text);

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
            new DeclaredParameter("@p", "nope_t"),
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
            new DeclaredParameter("@p", null),
            "it is a pseudo-type",
            DescribeStep.ResolveType,
            EquatableArray<string>.Empty,
            "declare a type"
        );

        failure.Arguments.ShouldBe(["", "@p", "GetUser", "it is a pseudo-type"]);
        failure.Step.ShouldBe(DescribeStep.ResolveType);
    }
}
