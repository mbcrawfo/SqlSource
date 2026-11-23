using System.Globalization;
using System.Linq;
using System.Text.Json;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Bundles;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles;

public sealed class BundleJsonTests
{
    private static readonly BundleInfo Info = BundleInfos.Sample(renamed: true, driverVersion: "Npgsql 9.0.3");

    [Fact]
    public void Write_ThenRead_GivesTheSameInfo()
    {
        var failure = Info with
        {
            Renamed = false,
            DriverVersion = null,
            Outcome = BundleOutcome.Failure("SQLSRC226", step: null),
        };
        var refused = Info with { Outcome = BundleOutcome.Failure("SQLSRC225", "describe parameters") };

        foreach (var info in new[] { Info, failure, refused })
        {
            var read = BundleJson.Read(BundleJson.Write(info), BundleInfos.Path);

            read.Failure.ShouldBeNull();
            read.Succeeded.ShouldBeTrue();
            read.Value.ShouldBe(info);
        }
    }

    [Fact]
    public void Write_Outcome_IsAnObjectWithItsKind()
    {
        OutcomeMembers(BundleJson.Write(Info)).ShouldBe([("kind", "entry")]);
        OutcomeMembers(
                BundleJson.Write(Info with { Outcome = BundleOutcome.Failure("SQLSRC226", "describe parameters") })
            )
            .ShouldBe([("kind", "failure"), ("id", "SQLSRC226"), ("step", "describe parameters")], ignoreOrder: true);
    }

    [Fact]
    public void Read_BundleVersionTwo_IsSqlsrc289()
    {
        var read = BundleJson.Read( /*lang=json,strict*/
            """{"bundleVersion": 2}""",
            BundleInfos.Path
        );

        read.Succeeded.ShouldBeFalse();
        read.Failure.Descriptor.Id.ShouldBe("SQLSRC289");
        read.Failure.Path.ShouldBe(BundleInfos.Path);
        read.Failure.Arguments.ToArray().ShouldBe(["bundleVersion", "2", "1"]);
    }

    // A sidecar format newer than this tool writes is a bundle of a newer tool, whose remedy is the same.
    [Fact]
    public void Read_FormatVersionNewerThanThisToolWrites_IsSqlsrc289()
    {
        var read = BundleJson.Read(
            BundleJson.Write(Info with { FormatVersion = SidecarFormat.Version + 1 }),
            BundleInfos.Path
        );

        read.Succeeded.ShouldBeFalse();
        read.Failure.Descriptor.Id.ShouldBe("SQLSRC289");
        read.Failure.Path.ShouldBe(BundleInfos.Path);
        read.Failure.Arguments.ToArray()
            .ShouldBe([
                "formatVersion",
                (SidecarFormat.Version + 1).ToString(CultureInfo.InvariantCulture),
                SidecarFormat.Version.ToString(CultureInfo.InvariantCulture),
            ]);
    }

    // An older sidecar format is one an older tool wrote, and is read.
    [Fact]
    public void Read_FormatVersionOlderThanThisToolWrites_IsRead()
    {
        var older = Info with { FormatVersion = SidecarFormat.Version - 1 };

        BundleJson.Read(BundleJson.Write(older), BundleInfos.Path).Value.ShouldBe(older);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Read_BundleVersionBelowOne_IsSqlsrc288(int version)
    {
        var text = BundleJson
            .Write(Info)
            .Replace("\"bundleVersion\": 1,", $"\"bundleVersion\": {version},", System.StringComparison.Ordinal);

        var read = BundleJson.Read(text, BundleInfos.Path);

        read.Succeeded.ShouldBeFalse();
        read.Failure.Descriptor.Id.ShouldBe("SQLSRC288");
        read.Failure.Path.ShouldBe(BundleInfos.Path);
        read.Failure.Arguments.Single()
            .ShouldBe($"the bundleVersion of bundle.json is {version}, which no sqlsource writes");
    }

    [Fact]
    public void Read_NoEngine_IsSqlsrc288NamingEngine()
    {
        var text = BundleJson.Write(Info).Replace("  \"engine\": \"postgres\",\n", "", System.StringComparison.Ordinal);

        var read = BundleJson.Read(text, BundleInfos.Path);

        read.Succeeded.ShouldBeFalse();
        read.Failure.Descriptor.Id.ShouldBe("SQLSRC288");
        read.Failure.Path.ShouldBe(BundleInfos.Path);
        read.Failure.Arguments.Single().ShouldContain("engine");
    }

    [Fact]
    public void Read_MistypedMember_IsSqlsrc288NamingIt()
    {
        var text = BundleJson
            .Write(Info)
            .Replace("\"renamed\": true", "\"renamed\": \"yes\"", System.StringComparison.Ordinal);

        var read = BundleJson.Read(text, BundleInfos.Path);

        read.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC288");
        read.Failure.Arguments.Single().ShouldContain("renamed");
    }

    [Fact]
    public void Read_NotJson_IsSqlsrc288()
    {
        var read = BundleJson.Read("{ not json", BundleInfos.Path);

        read.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC288");
        read.Failure.Arguments.Single().ShouldContain("bundle.json");
    }

    [Fact]
    public void Read_UnknownOutcomeKind_IsSqlsrc288OnOneLine()
    {
        var text = BundleJson.Write(Info).Replace("\"entry\"", "\"other\\nkind\"", System.StringComparison.Ordinal);

        var read = BundleJson.Read(text, BundleInfos.Path);

        read.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC288");
        read.Failure.Arguments.Single().ShouldContain("kind");
        read.Failure.Arguments.Single().ShouldNotContain("\n");
    }

    [Fact]
    public void Write_EndsWithOneLineBreak_AndHasNoCarriageReturn()
    {
        var text = BundleJson.Write(Info with { ServerVersion = "16.4 <\"x\">" });

        text.ShouldEndWith("}\n");
        text.ShouldNotEndWith("\n\n");
        text.ShouldNotContain("\r");
        text.ShouldContain("16.4 <\\\"x\\\">");
    }

    private static (string Name, string? Value)[] OutcomeMembers(string written)
    {
        using var document = JsonDocument.Parse(written);
        var outcome = document.RootElement.GetProperty("outcome");

        outcome.ValueKind.ShouldBe(JsonValueKind.Object);
        return [.. outcome.EnumerateObject().Select(member => (member.Name, member.Value.GetString()))];
    }
}
