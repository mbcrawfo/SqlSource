using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Generation;
using Xunit;

namespace SqlSource.Tests.Generation;

public class SqlPathTests
{
    [Theory]
    [InlineData("/app/Repo/Users.sql", "app/Repo/Users.sql")]
    [InlineData("C:\\app\\Repo\\Users.sql", "C:/app/Repo/Users.sql")]
    [InlineData("C:\\app/Repo\\Users.sql", "C:/app/Repo/Users.sql")]
    [InlineData("\\\\server\\share\\Users.sql", "server/share/Users.sql")]
    [InlineData("app//Repo///Users.sql", "app/Repo/Users.sql")]
    [InlineData("app/./Repo/./Users.sql", "app/Repo/Users.sql")]
    [InlineData("app/Repo/../Queries/Users.sql", "app/Queries/Users.sql")]
    [InlineData("app/Repo/Sub/../../Users.sql", "app/Users.sql")]
    [InlineData("app/Repo/", "app/Repo")]
    [InlineData("app/..", "")]
    [InlineData("", "")]
    [InlineData("/", "")]
    [InlineData("...", "...")]
    [InlineData("app/..hidden/.sql", "app/..hidden/.sql")]
    [InlineData("app/Répertoire 😀/Users.sql", "app/Répertoire 😀/Users.sql")]
    public void Normalize_Path_UsesOneSeparatorAndResolvesDots(string path, string expected) =>
        SqlPath.Normalize(path).ShouldBe(expected);

    [Theory]
    [InlineData("..")]
    [InlineData("../Users.sql")]
    [InlineData("app/../../Users.sql")]
    [InlineData("/..")]
    public void Normalize_DotDotWithNothingBeforeIt_IsNull(string path) => SqlPath.Normalize(path).ShouldBeNull();

    [Theory]
    [InlineData("app/Repo", "Users.sql", "app/Repo/Users.sql")]
    [InlineData("app/Repo", "Queries\\Users.sql", "app/Repo/Queries/Users.sql")]
    [InlineData("app/Repo", "../Queries", "app/Queries")]
    [InlineData("app/Repo", "/Queries", "app/Repo/Queries")]
    [InlineData("app/Repo", "C:\\Queries", "app/Repo/C:/Queries")]
    [InlineData("", "Users.sql", "Users.sql")]
    public void Combine_RelativePath_IsJoinedToTheFolderAndNeverRooted(string folder, string path, string expected) =>
        SqlPath.Combine(folder, path).ShouldBe(expected);

    [Fact]
    public void Combine_PathThatLeavesTheRoot_IsNull() => SqlPath.Combine("app", "../../Users.sql").ShouldBeNull();

    [Theory]
    [InlineData("app/Repo/Users.sql", "app/Repo")]
    [InlineData("Users.sql", "")]
    [InlineData("", "")]
    public void GetFolder_NormalizedPath_IsEverythingBeforeTheLastSegment(string path, string expected) =>
        SqlPath.GetFolder(path).ShouldBe(expected);

    [Theory]
    [InlineData("/app/Repo/Users.sql", "Users.sql")]
    [InlineData("C:\\app\\Repo\\Users.sql", "Users.sql")]
    [InlineData("Users.sql", "Users.sql")]
    [InlineData("app/Repo/", "")]
    public void GetFileName_Path_IsTheLastSegment(string path, string expected) =>
        SqlPath.GetFileName(path).ShouldBe(expected);

    [Theory]
    [InlineData("Users.sql", true)]
    [InlineData("Users.SQL", true)]
    [InlineData(".sql", true)]
    [InlineData("Users.sql.txt", false)]
    [InlineData("Users", false)]
    [InlineData("sql", false)]
    public void IsSqlFile_Path_ChecksTheExtensionIgnoringCase(string path, bool expected) =>
        SqlPath.IsSqlFile(path).ShouldBe(expected);

    [Fact]
    public void Contains_SortedPaths_FindsEachPathIgnoringCaseAndNothingElse()
    {
        string[] paths = ["app/A.sql", "app/b.sql", "app/C.sql", "app/sub/a.sql", "b/a.sql"];
        var sorted = TestModels.Array([.. paths.OrderBy(path => path, SqlPath.Comparer)]);

        foreach (var path in paths)
        {
            SqlPath.Contains(sorted, path).ShouldBeTrue(path);
            SqlPath.Contains(sorted, path.ToUpperInvariant()).ShouldBeTrue(path);
        }

        SqlPath.Contains(sorted, "app/D.sql").ShouldBeFalse();
        SqlPath.Contains(sorted, "").ShouldBeFalse();
        SqlPath.Contains(sorted, "zzz").ShouldBeFalse();
        SqlPath.Contains(TestModels.Array<string>(), "app/A.sql").ShouldBeFalse();
    }

    [Fact]
    public void IndexOf_SortedItems_FindsEachByItsPathIgnoringCaseAndNothingElse()
    {
        string[] paths = ["app/A.sql", "app/b.sql", "app/C.sql", "app/sub/a.sql", "b/a.sql"];
        var sorted = TestModels.Array([
            .. paths.OrderBy(path => path, SqlPath.Comparer).Select(path => new Item(path)),
        ]);

        for (var index = 0; index < sorted.Count; index++)
        {
            SqlPath.IndexOf(sorted, sorted[index].Path, static item => item.Path).ShouldBe(index);
            SqlPath.IndexOf(sorted, sorted[index].Path.ToUpperInvariant(), static item => item.Path).ShouldBe(index);
        }

        SqlPath.IndexOf(sorted, "app/D.sql", static item => item.Path).ShouldBe(-1);
        SqlPath.IndexOf(sorted, "", static item => item.Path).ShouldBe(-1);
        SqlPath.IndexOf(sorted, "zzz", static item => item.Path).ShouldBe(-1);
        SqlPath.IndexOf(TestModels.Array<Item>(), "app/A.sql", static item => item.Path).ShouldBe(-1);
    }

    [Theory]
    [InlineData("app/Repo", "app/Repo/a.sql", "app/Repo/Count.sql", "app/Repo/Users.sql")]
    [InlineData("APP/repo", "app/Repo/a.sql", "app/Repo/Count.sql", "app/Repo/Users.sql")]
    [InlineData("app", "app/Repo.sql", "app/Root.sql")]
    [InlineData("", "Root.sql")]
    [InlineData("app/Repo/Admin", "app/Repo/Admin/Roles.sql")]
    [InlineData("app/Repo/Zeta", "app/Repo/Zeta/z.sql")]
    [InlineData("app/Repo-old", "app/Repo-old/x.sql")]
    [InlineData("app/Repo0", "app/Repo0/x.sql")]
    [InlineData("app/repo2", "app/REPO2/x.sql")]
    public void FindInFolder_Folder_TakesItsFilesInOrderAndNotThoseOfItsSubfoldersOrNeighbours(
        string folder,
        params string[] expected
    ) => SqlPath.FindInFolder(FolderPaths, folder).ShouldBe(expected);

    [Theory]
    [InlineData("app/Rep")]
    [InlineData("app/Repo/a.sql")]
    [InlineData("app/Repo/Admin/Deep/Deeper")]
    [InlineData("app/Repo/Missing")]
    [InlineData("a")]
    [InlineData("zzz")]
    public void FindInFolder_FolderWithNoFileOfItsOwn_IsEmpty(string folder) =>
        SqlPath.FindInFolder(FolderPaths, folder).ShouldBeEmpty();

    [Fact]
    public void FindInFolder_NoPaths_IsEmpty()
    {
        SqlPath.FindInFolder(TestModels.Array<string>(), "app").ShouldBeEmpty();
        SqlPath.FindInFolder(TestModels.Array<string>(), "").ShouldBeEmpty();
    }

    // Checked against the plain definition: a file is in a folder when the folder of its path is that folder.
    [Fact]
    public void FindInFolder_GeneratedPaths_AgreesWithTheFolderOfEachPath()
    {
        // Names that sort before, at and after the separator, that differ only by case, and that are not ASCII.
        string[] names = ["a", "B", "a-b", "a.b", "a0", "a b", "ab", "é", "É2", "😀", "_", "Z"];
        string[] files = ["a.sql", "a0.sql", "_.sql"];
        var paths = TestModels.Array([
            .. Nest(names, 2)
                .Concat(Nest(["a", "a-b", "a0"], 4))
                .SelectMany(folder => files.Select(file => Join(folder, file)))
                .Distinct(SqlPath.Comparer)
                .OrderBy(path => path, SqlPath.Comparer),
        ]);
        var folders = paths
            .SelectMany(path => new[] { SqlPath.GetFolder(path), path, SqlPath.GetFolder(path).ToUpperInvariant() })
            .Concat(names)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (var folder in folders)
        {
            SqlPath
                .FindInFolder(paths, folder)
                .ShouldBe(paths.Where(path => SqlPath.Comparer.Equals(SqlPath.GetFolder(path), folder)), folder);
        }

        // Every folder of up to depth names, the root among them.
        static IEnumerable<string> Nest(string[] names, int depth) =>
            depth == 0
                ? [string.Empty]
                : Nest(names, depth - 1)
                    .SelectMany(folder => names.Select(name => Join(folder, name)))
                    .Prepend(string.Empty);

        static string Join(string folder, string name) => folder.Length == 0 ? name : folder + "/" + name;
    }

    // In the order of SqlPath.Comparer, as the pipeline supplies them.  The files of app/Repo are not adjacent: those
    // of its subfolders sort among them, and its neighbours' names sort on both sides of the separator.
    private static readonly EquatableArray<string> FolderPaths = TestModels.Array([
        .. new[]
        {
            "Root.sql",
            "app/Repo/a.sql",
            "app/Repo/Admin/Deep/x.sql",
            "app/Repo/Admin/Roles.sql",
            "app/Repo/Count.sql",
            "app/Repo/Users.sql",
            "app/Repo/Zeta/z.sql",
            "app/Repo-old/x.sql",
            "app/Repo.sql",
            "app/Repo0/x.sql",
            "app/REPO2/x.sql",
            "app/Root.sql",
            "b/a.sql",
        }.OrderBy(path => path, SqlPath.Comparer),
    ]);

    private sealed record Item(string Path);
}
