using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Describing.Capture;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming;

// The steps of the renaming over a capture's calls.
public sealed class CaptureRenamerTests
{
    [Fact]
    public async Task Drop_RemovesAtEveryDepth()
    {
        var raw = await RenamingBundles.RawAsync();
        var calls = CaptureRenamer.CallsOf(CaptureJson.Read(raw.Capture).Value.ShouldNotBeNull());
        var engine = new FakeEngineRenaming(
            rules: new Dictionary<string, FieldRule?>
            {
                ["describe answer.description.plan"] = FieldRule.Drop,
                ["describe answer.description.columns[].origin"] = FieldRule.Drop,
                ["describe answer.description.columns[].type.element.internalName"] = FieldRule.Drop,
                ["describe answer.description.columns[].type.labels[]"] = FieldRule.Drop,
            }
        );

        CaptureRenamer.Drop(engine, calls);

        // The rules of the fake drop the driver of open's server.
        var describe = calls.Single(static call => call.Method == "describe");
        calls.Single(static call => call.Method == "open").Renamed["answer"]!["server"]!
            .AsObject()
            .Select(static member => member.Key)
            .ShouldBe(["version", "settings"]);
        var description = describe.Renamed["answer"]!["description"]!.AsObject();
        description.ContainsKey("plan").ShouldBeFalse();
        description.ContainsKey("tableMatch").ShouldBeTrue();
        var columns = description["columns"]!.AsArray().Select(static column => column!.AsObject()).ToList();
        columns.ShouldAllBe(static column => !column.ContainsKey("origin") && column.ContainsKey("name"));
        var element = columns[1]["type"]!["element"]!.AsObject();
        element.ContainsKey("internalName").ShouldBeFalse();
        element.ContainsKey("schema").ShouldBeTrue();
        columns[2]["type"]!["labels"]!.AsArray().ShouldBeEmpty();
        describe.Original["answer"]!["description"]!["plan"]!.GetValue<string>().ShouldBe("walked");
        describe.Original["answer"]!["description"]!["columns"]![0]!.AsObject().ContainsKey("origin").ShouldBeTrue();
    }

    [Fact]
    public async Task CaptureOf_CallsNotRenamed_IsTheCapture()
    {
        var raw = await RenamingBundles.RawAsync();
        var capture = CaptureJson.Read(raw.Capture).Value.ShouldNotBeNull();

        var calls = CaptureRenamer.CallsOf(capture);

        CaptureJson.Write(CaptureRenamer.CaptureOf(capture.Engine, calls)).ShouldBe(raw.Capture);
        calls.Select(static call => (call.Index, call.Method)).ShouldBe([(0, "open"), (1, "describe")]);
        calls[1].Renamed.ShouldNotBeSameAs(calls[1].Original);
        JsonNode.DeepEquals(calls[1].Renamed, calls[1].Original).ShouldBeTrue();
    }
}
