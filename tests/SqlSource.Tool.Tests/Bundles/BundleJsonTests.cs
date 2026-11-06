using System.Globalization;
using System.Linq;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Bundles;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles;

public sealed class BundleJsonTests
{
    private const string Path = "/work/report.zip";

    private static readonly BundleInfo Info = new(
        BundleVersion: 1,
        Renamed: true,
        ToolVersion: "1.4.0",
        FormatVersion: SidecarFormat.Version,
        Engine: "postgres",
        ServerVersion: "18.6",
        DriverVersion: "Npgsql 9.0.3",
        Runtime: ".NET 8.0.11",
        OperatingSystem: "Linux 6.8",
        Outcome: BundleOutcome.Entry
    );

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
            var read = BundleJson.Read(BundleJson.Write(info), Path);

            read.Failure.ShouldBeNull();
            read.Succeeded.ShouldBeTrue();
            read.Value.ShouldBe(info);
        }
    }

    [Fact]
    public void Write_Outcome_IsAnObjectWithItsKind()
    {
        BundleJson.Write(Info).ShouldContain("\"outcome\": {\n    \"kind\": \"entry\"\n  }");
        BundleJson
            .Write(Info with { Outcome = BundleOutcome.Failure("SQLSRC226", "describe parameters") })
            .ShouldContain(
                "\"outcome\": {\n    \"kind\": \"failure\",\n    \"id\": \"SQLSRC226\",\n"
                    + "    \"step\": \"describe parameters\"\n  }"
            );
    }

    [Fact]
    public void Read_BundleVersionTwo_IsSqlsrc289()
    {
        var read = BundleJson.Read( /*lang=json,strict*/
            """{"bundleVersion": 2}""",
            Path
        );

        read.Succeeded.ShouldBeFalse();
        read.Failure.Descriptor.Id.ShouldBe("SQLSRC289");
        read.Failure.Path.ShouldBe(Path);
        read.Failure.Arguments.ToArray().ShouldBe(["bundleVersion", "2", "1"]);
    }

    // A sidecar format newer than this tool writes is a bundle of a newer tool, whose remedy is the same.
    [Fact]
    public void Read_FormatVersionNewerThanThisToolWrites_IsSqlsrc289()
    {
        var read = BundleJson.Read(BundleJson.Write(Info with { FormatVersion = SidecarFormat.Version + 1 }), Path);

        read.Succeeded.ShouldBeFalse();
        read.Failure.Descriptor.Id.ShouldBe("SQLSRC289");
        read.Failure.Path.ShouldBe(Path);
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

        BundleJson.Read(BundleJson.Write(older), Path).Value.ShouldBe(older);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Read_BundleVersionBelowOne_IsSqlsrc288(int version)
    {
        var text = BundleJson
            .Write(Info)
            .Replace("\"bundleVersion\": 1,", $"\"bundleVersion\": {version},", System.StringComparison.Ordinal);

        var read = BundleJson.Read(text, Path);

        read.Succeeded.ShouldBeFalse();
        read.Failure.Descriptor.Id.ShouldBe("SQLSRC288");
        read.Failure.Path.ShouldBe(Path);
        read.Failure.Arguments.Single()
            .ShouldBe($"the bundleVersion of bundle.json is {version}, which no sqlsource writes");
    }

    [Fact]
    public void Read_NoEngine_IsSqlsrc288NamingEngine()
    {
        var text = BundleJson.Write(Info).Replace("  \"engine\": \"postgres\",\n", "", System.StringComparison.Ordinal);

        var read = BundleJson.Read(text, Path);

        read.Succeeded.ShouldBeFalse();
        read.Failure.Descriptor.Id.ShouldBe("SQLSRC288");
        read.Failure.Path.ShouldBe(Path);
        read.Failure.Arguments.Single().ShouldContain("engine");
    }

    [Fact]
    public void Read_MistypedMember_IsSqlsrc288NamingIt()
    {
        var text = BundleJson
            .Write(Info)
            .Replace("\"renamed\": true", "\"renamed\": \"yes\"", System.StringComparison.Ordinal);

        var read = BundleJson.Read(text, Path);

        read.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC288");
        read.Failure.Arguments.Single().ShouldContain("renamed");
    }

    [Fact]
    public void Read_NotJson_IsSqlsrc288()
    {
        var read = BundleJson.Read("{ not json", Path);

        read.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC288");
        read.Failure.Arguments.Single().ShouldContain("bundle.json");
    }

    [Fact]
    public void Read_UnknownOutcomeKind_IsSqlsrc288OnOneLine()
    {
        var text = BundleJson.Write(Info).Replace("\"entry\"", "\"other\\nkind\"", System.StringComparison.Ordinal);

        var read = BundleJson.Read(text, Path);

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
}
