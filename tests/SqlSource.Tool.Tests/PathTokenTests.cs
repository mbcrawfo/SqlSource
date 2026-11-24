using System;
using System.IO;
using Shouldly;
using Xunit;

namespace SqlSource.Tool.Tests;

// What reads as a path is repeated, and a password typed where a path goes is not.
public sealed class PathTokenTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("sqlsource-path-token-").FullName;

    public PathTokenTests()
    {
        _ = Directory.CreateDirectory(Path.Combine(_folder, "src"));
        File.WriteAllText(Path.Combine(_folder, "Notes"), "");
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Theory]
    [InlineData("")]
    [InlineData("Missing.csproj")]
    [InlineData("Missing.SLN")]
    [InlineData("Missing.slnx")]
    [InlineData("q.SQL")]
    [InlineData("typo/q.sql")]
    [InlineData(".env")]
    [InlineData("run.log")]
    [InlineData("run.jsonl")]
    [InlineData("bundle.zip")]
    [InlineData("src")]
    [InlineData("Notes")]
    [InlineData("src/Typo/App")]
    [InlineData("./Typo/App")]
    [InlineData("../Typo/App")]
    public void MayBePrinted_TokenThatReadsAsAPath_IsTrue(string token) =>
        PathToken.MayBePrinted(token, _folder).ShouldBeTrue();

    [Theory]
    [InlineData("S3cret")]
    [InlineData("aB3/xY9+Qz")]
    [InlineData("aB3\\xY9+Qz")]
    [InlineData("/aB3")]
    [InlineData("/aB3xY9/Qz")]
    [InlineData("S3cret.word")]
    [InlineData("a=b.sql")]
    [InlineData("a;b/c")]
    [InlineData("src/a=b")]
    public void MayBePrinted_TokenThatDoesNotReadAsAPath_IsFalse(string token) =>
        PathToken.MayBePrinted(token, _folder).ShouldBeFalse();

    // A log or a bundle that an earlier run wrote under a password does not make that password a path.
    [Theory]
    [InlineData("Notes", false)]
    [InlineData("src", true)]
    [InlineData("src/Typo/App", true)]
    [InlineData("run.log", true)]
    public void MayBePrinted_TokenOfAFileTheToolWrites_CountsNoFileThatExists(string token, bool expected) =>
        PathToken.MayBePrinted(token, _folder, isWritten: true).ShouldBe(expected);

    [Fact]
    public void MayBePrinted_FullPathInAFolderThatExists_IsTrue() =>
        PathToken.MayBePrinted(Path.Combine(_folder, "src", "Typo", "App"), _folder).ShouldBeTrue();
}
