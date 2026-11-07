using System.Linq;
using System.Text.Json.Nodes;
using Shouldly;
using SqlSource.Tool.Bundles.Renaming;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming;

// The copies that the calls record, found by a copy's name as the pipeline compares parameters' names.
public sealed class RenamingCopiesTests
{
    private static readonly FakeEngineRenaming Engine = new();

    // The copies that calls of describe record, each call's request recording the copies given.
    private static RenamingCopies Of(params (string Name, string Parameter, string Suffix)[][] calls)
    {
        var found = calls.Select(
            static (copies, index) =>
            {
                var request = new JsonObject { ["copies"] = FakeEngineRenaming.Copies(copies) };
                return new RenamingCall(index, "describe", new JsonObject { ["request"] = request }, []);
            }
        );
        return RenamingCopies.Of(Engine, found, static call => call.Original);
    }

    [Fact]
    public void Find_ACopySpelledInAnotherCase_IsFound() =>
        Of([("Id_1", "Id", "_1")]).Find("ID_1").ShouldBe(new ParameterCopy("Id_1", "Id", "_1"));

    [Fact]
    public void Find_NoCopy_IsNull()
    {
        Of([("id_1", "id", "_1")]).Find("id_2").ShouldBeNull();
        Of([("id_1", "id", "_1")]).Find("id").ShouldBeNull();
        RenamingCopies.None.Find("id_1").ShouldBeNull();
    }

    [Fact]
    public void Of_TwoRecordsOfOneName_KeepsTheFirst() =>
        Of([("id_1", "id", "_1")], [("ID_1", "other", "_2")])
            .Find("id_1")
            .ShouldBe(new ParameterCopy("id_1", "id", "_1"));

    [Fact]
    public void Of_TheRenamedSide_ReadsRenamed()
    {
        var original = new JsonObject
        {
            ["request"] = new JsonObject { ["copies"] = FakeEngineRenaming.Copies(("id_1", "id", "_1")) },
        };
        var renamed = new JsonObject
        {
            ["request"] = new JsonObject { ["copies"] = FakeEngineRenaming.Copies(("p_1", "p", "_1")) },
        };
        RenamingCall[] calls = [new(0, "describe", original, renamed)];

        var copies = RenamingCopies.Of(Engine, calls, static call => call.Renamed);

        copies.Find("p_1").ShouldBe(new ParameterCopy("p_1", "p", "_1"));
        copies.Find("id_1").ShouldBeNull();
    }
}
