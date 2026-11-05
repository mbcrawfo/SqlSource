using System;
using System.IO;
using Shouldly;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles;

// The lines replay prints of a describer's events: the decisions of the walk and of the table match, and every event
// under --verbose.
public sealed class ReplayLinesTests : IDisposable
{
    private readonly RecordingLog _inner = new();
    private readonly StringWriter _output = new() { NewLine = "\n" };

    public void Dispose() => _output.Dispose();

    private ReplayLines Lines(bool verbose = false) => new(_inner, _output, verbose);

    [Fact]
    public void Write_WalkNode_WritesALine()
    {
        Lines()
            .Write(
                "postgres.walk.node",
                new("node", "Hash Join"),
                new("depth", 2L),
                new("nullable", true),
                new("sides", LogValue.ListOfTexts(["left", "say \"hi\"\nthere"])),
                new("none", LogValue.Null),
                new("at", LogValue.MapOf(new LogMember("table", "users")))
            );

        _output
            .ToString()
            .ShouldBe(
                "sqlsource: postgres.walk.node: node=\"Hash Join\" depth=2 nullable=true "
                    + "sides=[\"left\",\"say \\\"hi\\\"\\nthere\"] none=null at={\"table\":\"users\"}\n"
            );
    }

    [Theory]
    [InlineData("postgres.walk.column")]
    [InlineData("postgres.table-match")]
    [InlineData("mssql.table-match")]
    public void Write_DecisionOfAnEngine_WritesALine(string name)
    {
        Lines().Write(name, new LogMember("query", "GetUser"));

        _output.ToString().ShouldBe($"sqlsource: {name}: query=\"GetUser\"\n");
    }

    [Fact]
    public void Write_OtherEvent_WritesNoLineUnlessVerbose()
    {
        Lines().Write("postgres.prepare", new LogMember("query", "GetUser"));
        Lines().Write("postgres.walk", new LogMember("query", "GetUser"));
        _output.ToString().ShouldBeEmpty();

        Lines(verbose: true).Write("postgres.prepare", new LogMember("query", "GetUser"));
        _output.ToString().ShouldBe("sqlsource: postgres.prepare: query=\"GetUser\"\n");
    }

    [Fact]
    public void Write_AnyEvent_PassesToTheInnerLog()
    {
        var lines = Lines();

        lines.Write("postgres.prepare", new LogMember("query", "GetUser"));
        lines.Write("postgres.walk.node", new LogMember("node", "Seq Scan"));

        _inner.Events.Count.ShouldBe(2);
        _inner.Events[0].Name.ShouldBe("postgres.prepare");
        RecordingLog.Member(_inner.Events[1].Members, "node").ShouldBe((LogValue)"Seq Scan");
        lines.Writes.ShouldBeTrue();
    }
}
