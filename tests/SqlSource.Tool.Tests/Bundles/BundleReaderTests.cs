using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Reporting;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles;

public sealed class BundleReaderTests : IDisposable
{
    private static readonly BundleInfo Info = new(
        BundleVersion: 1,
        Renamed: true,
        ToolVersion: "1.4.0",
        FormatVersion: SidecarFormat.Version,
        Engine: "postgres",
        ServerVersion: "18.6",
        DriverVersion: null,
        Runtime: ".NET 8.0.11",
        OperatingSystem: "Linux 6.8",
        Outcome: BundleOutcome.Entry
    );

    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private static Outcome<Bundle, ToolDiagnostic> Read(string path) =>
        BundleReader.Read(path, ToolHost.OpenRead, ToolHost.ReadText);

    [Fact]
    public void Read_FolderOfAFixture_GivesItsFiles()
    {
        var fixture = FixtureCases.Named("postgres", "walk-left-hash");

        var read = Read(fixture.Folder);

        read.Failure.ShouldBeNull();
        read.Succeeded.ShouldBeTrue();
        read.Value.Info.ShouldBeNull();
        read.Value.Query.ShouldBe(fixture.Query);
        read.Value.Capture.ShouldBe(fixture.Capture);
        read.Value.Expected.ShouldBe(fixture.Expected);
        read.Value.ExplainSettings.ShouldBe(fixture.ExplainSettings);
        read.Value.ExplainSettings.ShouldNotBeEmpty();
    }

    [Fact]
    public void Read_FolderWithBundleJsonAndNoExpected_GivesItsInfo()
    {
        _ = _folder.WriteFile("case/query.sql", "-- dialect: postgres\r\nselect 1;\r\n");
        _ = _folder.WriteFile("case/capture.json", "{}");
        _ = _folder.WriteFile("case/bundle.json", BundleJson.Write(Info));

        var read = Read(_folder.PathOf("case"));

        read.Failure.ShouldBeNull();
        read.Succeeded.ShouldBeTrue();
        read.Value.Info.ShouldBe(Info);
        read.Value.Query.ShouldBe("-- dialect: postgres\r\nselect 1;\r\n");
        read.Value.Expected.ShouldBeNull();
        read.Value.ExplainSettings.ShouldBeEmpty();
    }

    [Fact]
    public void Read_ZipWrittenByBundleWriter_GivesTheSameBundle()
    {
        var bundle = new Bundle(
            Info,
            "-- dialect: postgres\r\nselect 'é';\n",
            /*lang=json,strict*/
            "{ \"capture\": 1 }\n",
            /*lang=json,strict*/
            "{ \"expected\": 1 }\n",
            ["enable_mergejoin=off", "enable_nestloop=off"]
        );
        var path = WriteZip(bundle);

        var read = Read(path);

        read.Failure.ShouldBeNull();
        read.Succeeded.ShouldBeTrue();
        read.Value.Info.ShouldBe(bundle.Info);
        read.Value.Query.ShouldBe(bundle.Query);
        read.Value.Capture.ShouldBe(bundle.Capture);
        read.Value.Expected.ShouldBe(bundle.Expected);
        read.Value.ExplainSettings.ShouldBe(bundle.ExplainSettings);
    }

    [Fact]
    public void Read_ZipWithBundleVersionTwo_IsSqlsrc289()
    {
        var path = _folder.PathOf("newer.zip");
        WriteRawZip(
            path,
            (
                "bundle.json", /*lang=json,strict*/
                """{"bundleVersion": 2}"""
            )
        );

        var read = Read(path);

        read.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC289");
        read.Failure.Path.ShouldBe(path);
    }

    [Fact]
    public void Read_ZipWithATopFolder_IsSqlsrc288AtTheRoot()
    {
        var path = _folder.PathOf("rezipped.zip");
        WriteRawZip(
            path,
            ("case/bundle.json", BundleJson.Write(Info)),
            ("case/query.sql", "select 1;"),
            ("case/capture.json", "{}")
        );

        var read = Read(path);

        read.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC288");
        read.Failure.Path.ShouldBe(path);
        read.Failure.Arguments.Single().ShouldContain("query.sql");
        read.Failure.Arguments.Single().ShouldContain("root");
    }

    [Fact]
    public void Read_ZipWithoutBundleJson_IsSqlsrc288NamingIt()
    {
        var path = _folder.PathOf("raw.zip");
        WriteRawZip(path, ("query.sql", "select 1;"), ("capture.json", "{}"));

        var read = Read(path);

        read.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC288");
        read.Failure.Arguments.Single().ShouldContain("bundle.json");
    }

    [Fact]
    public void Read_NotAZipNorAFolder_IsSqlsrc288()
    {
        var path = _folder.PathOf("nothing-here");

        var read = Read(path);

        read.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC288");
        read.Failure.Path.ShouldBe(path);
        read.Failure.Arguments.Single()
            .ShouldBe("it is neither a zip nor a folder that holds query.sql and capture.json");
    }

    [Fact]
    public void Read_FolderWithoutCapture_IsSqlsrc288()
    {
        _ = _folder.WriteFile("case/query.sql", "select 1;");

        var read = Read(_folder.PathOf("case"));

        read.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC288");
        read.Failure.Arguments.Single().ShouldContain("capture.json");
    }

    [Fact]
    public void Read_FileThatIsNoZip_IsSqlsrc288()
    {
        var path = _folder.WriteFile("report.zip", "select 1;");

        var read = Read(path);

        read.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC288");
        read.Failure.Path.ShouldBe(path);
        read.Failure.Arguments.Single().ShouldContain("zip");
    }

    [Fact]
    public void Read_FileThatCannotBeOpened_IsSqlsrc288WithTheReason()
    {
        var read = BundleReader.Read(
            "/work/report.zip",
            static _ => throw new IOException("The disk\nis gone."),
            static _ => null
        );

        read.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC288");
        // The system's reason is kept on its line, its line break written as its escape.
        read.Failure.Arguments.Single().ShouldContain("The disk\\u000Ais gone.");
    }

    private string WriteZip(Bundle bundle)
    {
        var path = _folder.PathOf("bundle.zip");
        using var stream = File.Create(path);
        BundleWriter.Write(bundle, stream);
        return path;
    }

    private static void WriteRawZip(string path, params (string Name, string Text)[] entries)
    {
        using var stream = File.Create(path);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var (name, text) in entries)
        {
            using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
            writer.Write(text);
        }
    }
}
