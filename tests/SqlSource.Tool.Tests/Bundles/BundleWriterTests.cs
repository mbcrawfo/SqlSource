using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Shouldly;
using SqlSource.Tool.Bundles;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles;

public sealed class BundleWriterTests
{
    private static readonly Bundle Bundle = new(
        new BundleInfo(
            BundleVersion: 1,
            Renamed: true,
            ToolVersion: "1.4.0",
            FormatVersion: 3,
            Engine: "postgres",
            ServerVersion: "18.6",
            DriverVersion: "Npgsql 9.0.3",
            Runtime: ".NET 8.0.11",
            OperatingSystem: "Linux 6.8",
            Outcome: BundleOutcome.Entry
        ),
        "select 1;\n",
        "{}\n",
        /*lang=json,strict*/
        "{ \"expected\": 1 }\n",
        ["enable_mergejoin=off", "enable_nestloop=off"]
    );

    private static byte[] Written(Bundle bundle)
    {
        using var stream = new MemoryStream();
        BundleWriter.Write(bundle, stream);
        return stream.ToArray();
    }

    private static ZipArchive Open(byte[] bytes) => new(new MemoryStream(bytes), ZipArchiveMode.Read);

    [Fact]
    public void Write_Twice_GivesTheSameBytes() => Written(Bundle).ShouldBe(Written(Bundle));

    [Fact]
    public void Write_Bundle_HasItsEntriesInOrderAtOneTime()
    {
        using var zip = Open(Written(Bundle));

        zip.Entries.Select(static entry => entry.FullName)
            .ShouldBe(["bundle.json", "query.sql", "capture.json", "expected.json", "explain-settings.txt"]);
        zip.Entries.ShouldAllBe(static entry =>
            entry.LastWriteTime.DateTime == new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)
        );
        using var settings = new StreamReader(zip.GetEntry("explain-settings.txt")!.Open());
        settings.ReadToEnd().ShouldBe("enable_mergejoin=off\nenable_nestloop=off\n");
    }

    [Fact]
    public void Write_NoExpected_HasNoExpectedEntry()
    {
        using var zip = Open(Written(Bundle with { Expected = null, ExplainSettings = [] }));

        zip.Entries.Select(static entry => entry.FullName).ShouldBe(["bundle.json", "query.sql", "capture.json"]);
    }

    [Fact]
    public void Write_NoInfo_Throws() => Should.Throw<ArgumentException>(() => Written(Bundle with { Info = null }));
}
