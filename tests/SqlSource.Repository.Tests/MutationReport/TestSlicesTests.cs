using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.MutationReport;
using Xunit;

namespace SqlSource.Repository.Tests.MutationReport;

// How a test folder is cut into the slices that the reviewers of the test review read, a slice of at most a limit
// of lines unless one file is longer.
public sealed class TestSlicesTests
{
    private static SliceFile File(string path, int lines, string? partialClass = null) =>
        new("tests/P/" + path, lines, partialClass);

    private static IReadOnlyList<Slice> Cut(int limit, params SliceFile[] files) =>
        TestSlices.Cut("tests/P", files, limit);

    private static string[] Paths(Slice slice) => [.. slice.Files.Select(file => file["tests/P/".Length..])];

    [Fact]
    public void Cut_FolderUnderTheLimit_IsOneSliceOfItsFilesInPathOrder()
    {
        var slice = Cut(100, File("B.cs", 30), File("Sub/C.cs", 30), File("A.cs", 40)).ShouldHaveSingleItem();

        Paths(slice).ShouldBe(["A.cs", "B.cs", "Sub/C.cs"]);
        slice.Lines.ShouldBe(100);
    }

    [Fact]
    public void Cut_FolderWithNoFiles_HasNoSlices() => Cut(100).ShouldBeEmpty();

    [Fact]
    public void Cut_SiblingFoldersThatFitTogether_AreJoined()
    {
        var slices = Cut(100, File("A/1.cs", 40), File("B/1.cs", 50), File("C/1.cs", 60), File("D/1.cs", 30));

        slices
            .Select(Paths)
            .ShouldBe([
                ["A/1.cs", "B/1.cs"],
                ["C/1.cs", "D/1.cs"],
            ]);
    }

    [Fact]
    public void Cut_FolderOverTheLimit_IsSplitByItsSubFoldersWithItsOwnFilesFirst()
    {
        var slices = Cut(100, File("Z.cs", 60), File("A/1.cs", 70), File("B/1.cs", 80));

        slices
            .Select(Paths)
            .ShouldBe([
                ["Z.cs"],
                ["A/1.cs"],
                ["B/1.cs"],
            ]);
    }

    [Fact]
    public void Cut_SubFolderOverTheLimit_IsCutByTheSameRuleAndNotJoinedWithItsSiblings()
    {
        var slices = Cut(100, File("A/1.cs", 10), File("B/X/1.cs", 90), File("B/Y/1.cs", 20), File("C/1.cs", 10));

        slices
            .Select(Paths)
            .ShouldBe([
                ["A/1.cs"],
                ["B/X/1.cs"],
                ["B/Y/1.cs"],
                ["C/1.cs"],
            ]);
    }

    [Fact]
    public void Cut_FolderOfFilesOverTheLimit_IsPackedInPathOrder()
    {
        var slices = Cut(100, File("D.cs", 50), File("A.cs", 40), File("C.cs", 30), File("B.cs", 40));

        slices
            .Select(Paths)
            .ShouldBe([
                ["A.cs", "B.cs"],
                ["C.cs", "D.cs"],
            ]);
    }

    [Fact]
    public void Cut_LastPartOfAFoldersOwnFiles_IsJoinedWithTheSubFoldersThatFit()
    {
        var slices = Cut(100, File("A.cs", 90), File("B.cs", 30), File("Sub/1.cs", 40), File("Tub/1.cs", 50));

        slices
            .Select(Paths)
            .ShouldBe([
                ["A.cs"],
                ["B.cs", "Sub/1.cs"],
                ["Tub/1.cs"],
            ]);
    }

    [Fact]
    public void Cut_FileOverTheLimit_IsASliceOfItsOwn()
    {
        var slices = Cut(100, File("A.cs", 10), File("B.cs", 500), File("C.cs", 10));

        slices
            .Select(Paths)
            .ShouldBe([
                ["A.cs"],
                ["B.cs"],
                ["C.cs"],
            ]);
        slices[1].Lines.ShouldBe(500);
    }

    [Fact]
    public void Cut_FilesOfOnePartialClass_StayTogetherAcrossAPartBoundary()
    {
        var slices = Cut(
            100,
            File("A.cs", 60),
            File("B.cs", 30, "BTests"),
            File("C.cs", 10),
            File("D.cs", 30, "BTests"),
            File("E.cs", 20)
        );

        slices
            .Select(Paths)
            .ShouldBe([
                ["A.cs"],
                ["B.cs", "C.cs", "D.cs", "E.cs"],
            ]);
    }

    [Fact]
    public void Cut_FilesOfOnePartialClassNameInTwoFolders_AreApart()
    {
        var slices = Cut(100, File("A/1.cs", 80, "Tests"), File("B/1.cs", 80, "Tests"));

        slices.Count.ShouldBe(2);
    }

    [Fact]
    public void Cut_Slices_AreNamedForTheirFolderAndNumbered()
    {
        var slices = TestSlices.Cut(
            "tests/SqlSource.Tests/Parsing",
            [
                new SliceFile("tests/SqlSource.Tests/Parsing/A.cs", 90, null),
                new SliceFile("tests/SqlSource.Tests/Parsing/B.cs", 90, null),
                new SliceFile("tests/SqlSource.Tests/Parsing/Markers/C.cs", 90, null),
            ],
            100
        );

        slices
            .Select(slice => slice.Name)
            .ShouldBe(["SqlSource.Tests.Parsing-1", "SqlSource.Tests.Parsing-2", "SqlSource.Tests.Parsing.Markers-1"]);
    }

    // Review focus 2 of docs/superpowers/plans/2026-11-12-reviewing-test-quality-skill.md: a test file in no slice is
    // never reviewed, and one in two is reviewed twice.
    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(5000)]
    public void Cut_AnyTree_PutsEveryFileInExactlyOneSliceWithinTheLimit(int limit)
    {
        // Forty-two files at three depths, of 7 to 620 lines, some of them halves of a partial class.
        string[] folders = ["", "A/", "A/X/", "A/Y/", "B/", "B/X/", "C/"];
        var files = Enumerable
            .Range(0, 42)
            .Select(index =>
                File(
                    $"{folders[index % folders.Length]}F{index:00}.cs",
                    7 + (index * 37 % 200) + (index % 11 == 0 ? 420 : 0),
                    index % 9 == 3 ? "Shared" : null
                )
            )
            .ToArray();

        var slices = Cut(limit, files);

        slices.SelectMany(slice => slice.Files).Order().ShouldBe(files.Select(file => file.Path).Order());
        slices.Select(slice => slice.Name).ShouldBeUnique();
        foreach (var slice in slices)
        {
            var lines = files.Where(file => slice.Files.Contains(file.Path)).Sum(file => file.Lines);
            slice.Lines.ShouldBe(lines);
            var partial = slice.Files.Count(path => files.Single(file => file.Path == path).PartialClass is not null);
            (lines <= limit || slice.Files.Count == 1 || partial == slice.Files.Count).ShouldBeTrue(slice.Name);
        }
    }
}
