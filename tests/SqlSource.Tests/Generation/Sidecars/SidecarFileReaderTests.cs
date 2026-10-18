using System;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation.Sidecars;
using SqlSource.Snapshot.Reading;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generation.Sidecars;

public sealed class SidecarFileReaderTests
{
    private const string Path = "/app/Users.sql.json";

    private const string SqlPath = "app/Users.sql";

    private const string Entry =
        "{\"hash\": \"h\", \"engine\": \"postgres\", \"database\": \"app\", \"serverVersion\": \"16.4\""
        + ", \"resultKind\": \"none\", \"parameters\": []}";

    private const string TwoEntries =
        "{\n"
        + "  \"formatVersion\": 1,\n"
        + "  \"toolVersion\": \"1.2.3\",\n"
        + "  \"queries\": {\n"
        + "    \"GetUser\": "
        + Entry
        + ",\n"
        + "    \"ListUsers\": "
        + Entry
        + "\n"
        + "  }\n"
        + "}\n";

    [Fact]
    public void Read_CurrentFile_GivesTheSidecarAndWhereEachKeyIs()
    {
        var file = Read(TwoEntries);

        file.SqlPath.ShouldBe(SqlPath);
        file.Path.ShouldBe(Path);
        file.Result.ShouldBe(SidecarReader.Read(TwoEntries));
        file.Result.Sidecar.ShouldNotBeNull().Queries.Count.ShouldBe(2);
        file.Start.ShouldBe(new LocationInfo(Path, new TextSpan(0, 0), new LinePositionSpan(new(0, 0), new(0, 0))));
        file.ErrorLocation.ShouldBeNull();
        file.EntryLocations.ShouldBe([
            new LocationInfo(Path, KeySpan(TwoEntries, "GetUser"), new LinePositionSpan(new(4, 4), new(4, 13))),
            new LocationInfo(Path, KeySpan(TwoEntries, "ListUsers"), new LinePositionSpan(new(5, 4), new(5, 15))),
        ]);
    }

    // The compiler strips the mark from a file it reads, but a text handed over by other means may keep it.  The
    // reader takes a text without it, and the positions stay those of the file as it is.
    [Fact]
    public void Read_FileWithAByteOrderMark_ReadsItAndKeepsThePositionsOfTheFile()
    {
        var plain = Read(TwoEntries);

        var file = Read("\uFEFF" + TwoEntries);

        file.Result.ShouldBe(plain.Result);
        _ = file.Result.Sidecar.ShouldNotBeNull();
        file.EntryLocations[0].Span.Start.ShouldBe(plain.EntryLocations[0].Span.Start + 1);
        file.EntryLocations[0].LineSpan.ShouldBe(plain.EntryLocations[0].LineSpan);
    }

    [Fact]
    public void Read_TextThatCannotBeHad_IsMalformedAtTheStart()
    {
        var file = Read(null);

        file.Result.Error.ShouldNotBeNull().Kind.ShouldBe(SidecarErrorKind.InvalidJson);
        file.ErrorLocation.ShouldBe(
            new LocationInfo(Path, new TextSpan(0, 0), new LinePositionSpan(new(0, 0), new(0, 0)))
        );
        file.EntryLocations.ShouldBeEmpty();
    }

    [Fact]
    public void Read_MalformedFile_KeepsWhereTheErrorIs()
    {
        const string Text = "{\n  \"formatVersion\": 1,\n  oops\n}\n";

        var file = Read(Text);

        var error = file.Result.Error.ShouldNotBeNull();
        error.Kind.ShouldBe(SidecarErrorKind.InvalidJson);
        var location = file.ErrorLocation.ShouldNotBeNull();
        location.Path.ShouldBe(Path);
        location.Span.ShouldBe(error.Span);
        location.LineSpan.Start.ShouldBe(new LinePosition(2, 2));
        file.EntryLocations.ShouldBeEmpty();
    }

    [Fact]
    public void Read_MalformedFileWithAByteOrderMark_KeepsWhereTheErrorIsInTheFile()
    {
        const string Text = "{\n  \"formatVersion\": 1,\n  oops\n}\n";
        var plain = Read(Text);

        var file = Read("\uFEFF" + Text);

        file.Result.ShouldBe(plain.Result);
        var location = file.ErrorLocation.ShouldNotBeNull();
        location.Span.Start.ShouldBe(plain.ErrorLocation.ShouldNotBeNull().Span.Start + 1);
        location.LineSpan.ShouldBe(plain.ErrorLocation.LineSpan);
    }

    [Fact]
    public void Read_FileOfAnotherVersion_KeepsTheVersionAlone()
    {
        const string Text = /*lang=json*/
            "{\"formatVersion\": 2, \"queries\": 7}";

        var file = Read(Text);

        file.Result.ShouldBe(SidecarReadResult.OfAnotherVersion(2));
        file.ErrorLocation.ShouldBeNull();
        file.EntryLocations.ShouldBeEmpty();
    }

    private static SidecarFile Read(string? text) =>
        SidecarFileReader.Read(
            new SidecarInput(new InMemoryAdditionalText(Path, text), SqlPath),
            TestContext.Current.CancellationToken
        );

    // The key of an entry, quotes included.
    private static TextSpan KeySpan(string text, string name) =>
        new(text.IndexOf("\"" + name + "\"", StringComparison.Ordinal), name.Length + 2);
}
