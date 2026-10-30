using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Generation.Paths;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;

namespace SqlSource.Tests.Generation.Paths;

public sealed class SqlPathTests
{
    [Theory]
    [InlineData(GeneratorHarness.UsersPath, GeneratorHarness.UsersNormalizedPath)]
    [InlineData("C:\\app\\Repo\\Users.sql", $"C:/{GeneratorHarness.UsersNormalizedPath}")]
    [InlineData("C:\\app/Repo\\Users.sql", $"C:/{GeneratorHarness.UsersNormalizedPath}")]
    [InlineData("\\\\server\\share\\Users.sql", "server/share/Users.sql")]
    [InlineData("app//Repo///Users.sql", GeneratorHarness.UsersNormalizedPath)]
    [InlineData("app/./Repo/./Users.sql", GeneratorHarness.UsersNormalizedPath)]
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
    [InlineData(GeneratorHarness.UsersPath)]
    [InlineData("/")]
    [InlineData("\\app\\Users.sql")]
    [InlineData("\\\\server\\share\\Users.sql")]
    [InlineData("C:\\app\\Users.sql")]
    [InlineData("c:/app/Users.sql")]
    [InlineData("Z:")]
    public void IsRooted_PathFromARoot_IsTrue(string path) => SqlPath.IsRooted(path).ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("app/Users.sql")]
    [InlineData("Other.Gen/Other.Gen.Generator/Extra.g.cs")]
    [InlineData("./app/Users.sql")]
    [InlineData("../app/Users.sql")]
    [InlineData("C")]
    [InlineData("1:/app/Users.sql")]
    [InlineData("é:/app/Users.sql")]
    [InlineData("app:/Users.sql")]
    public void IsRooted_RelativePath_IsFalse(string path) => SqlPath.IsRooted(path).ShouldBeFalse();

    [Theory]
    [InlineData("app/Repo", "Users.sql", GeneratorHarness.UsersNormalizedPath)]
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
    [InlineData(GeneratorHarness.UsersNormalizedPath, "app/Repo")]
    [InlineData("Users.sql", "")]
    [InlineData("", "")]
    public void GetFolder_NormalizedPath_IsEverythingBeforeTheLastSegment(string path, string expected) =>
        SqlPath.GetFolder(path).ShouldBe(expected);

    [Theory]
    [InlineData(GeneratorHarness.UsersPath, "Users.sql")]
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

    [Theory]
    [InlineData("Users.sql.json", true)]
    [InlineData("USERS.SQL.JSON", true)]
    [InlineData("Users.json", false)]
    [InlineData("Users.sql", false)]
    public void IsSidecarFile_Path_ChecksTheExtensionIgnoringCase(string path, bool expected) =>
        SqlPath.IsSidecarFile(path).ShouldBe(expected);

    [Theory]
    [InlineData("/app/Users.sql.json", "app/Users.sql")]
    [InlineData("C:\\app\\.\\Users.SQL.JSON", "C:/app/Users.SQL")]
    public void SqlPathOfSidecar_Path_IsTheNormalizedPathWithoutJson(string path, string expected) =>
        SqlPath.SqlPathOfSidecar(path).ShouldBe(expected);

    [Theory]
    [InlineData("../Users.sql.json")]
    [InlineData("app/Users.json")]
    public void SqlPathOfSidecar_PathThatIsNoSidecarOnceNormalized_IsNull(string path) =>
        SqlPath.SqlPathOfSidecar(path).ShouldBeNull();

    [Theory]
    [InlineData("/app/Users.sql.json", "app/Users.sql")]
    [InlineData("C:\\app\\.\\Users.sql.json", "C:/app/Users.sql")]
    public void IsSidecarPathOf_PathSpelledAsTheSqlPathWithJson_IsTrue(string path, string sqlPath) =>
        SqlPath.IsSidecarPathOf(path, sqlPath).ShouldBeTrue();

    [Theory]
    [InlineData("/app/users.sql.json", "app/Users.sql")]
    [InlineData("/app/Users.sql.JSON", "app/Users.sql")]
    [InlineData("/app/Users.sql", "app/Users.sql")]
    [InlineData("/app/Other.sql.json", "app/Users.sql")]
    [InlineData("../Users.sql.json", "Users.sql")]
    public void IsSidecarPathOf_PathSpelledOtherwise_IsFalse(string path, string sqlPath) =>
        SqlPath.IsSidecarPathOf(path, sqlPath).ShouldBeFalse();

    [Fact]
    public void Contains_SortedPaths_FindsEachPathIgnoringCaseAndNothingElse()
    {
        string[] paths = ["app/A.sql", "app/b.sql", "app/C.sql", "app/sub/a.sql", "b/a.sql"];
        var sorted = TestSidecars.Of([.. paths.OrderBy(path => path, SqlPath.Comparer)]);

        foreach (var path in paths)
        {
            SqlPath.Contains(sorted, path).ShouldBeTrue(path);
            SqlPath.Contains(sorted, path.ToUpperInvariant()).ShouldBeTrue(path);
        }

        SqlPath.Contains(sorted, "app/D.sql").ShouldBeFalse();
        SqlPath.Contains(sorted, "").ShouldBeFalse();
        SqlPath.Contains(sorted, "zzz").ShouldBeFalse();
        SqlPath.Contains(TestSidecars.Of<string>(), "app/A.sql").ShouldBeFalse();
    }

    [Fact]
    public void IndexOf_SortedItems_FindsEachByItsPathIgnoringCaseAndNothingElse()
    {
        string[] paths = ["app/A.sql", "app/b.sql", "app/C.sql", "app/sub/a.sql", "b/a.sql"];
        var sorted = TestSidecars.Of([.. paths.OrderBy(path => path, SqlPath.Comparer).Select(path => new Item(path))]);

        for (var index = 0; index < sorted.Count; index++)
        {
            SqlPath.IndexOf(sorted, sorted[index].Path, static item => item.Path).ShouldBe(index);
            SqlPath.IndexOf(sorted, sorted[index].Path.ToUpperInvariant(), static item => item.Path).ShouldBe(index);
        }

        SqlPath.IndexOf(sorted, "app/D.sql", static item => item.Path).ShouldBe(-1);
        SqlPath.IndexOf(sorted, "", static item => item.Path).ShouldBe(-1);
        SqlPath.IndexOf(sorted, "zzz", static item => item.Path).ShouldBe(-1);
        SqlPath.IndexOf(TestSidecars.Of<Item>(), "app/A.sql", static item => item.Path).ShouldBe(-1);
    }

    [Fact]
    public void IndexOf_FolderOfAPath_FindsItInTheOrderOfTheComparer()
    {
        // '-' sorts before the separator, and a letter that is not ASCII after every ASCII one, ignoring its case.
        string[] folders = ["", "app", "app-tests", "app/Queries", "Zed", "Äpp", "äpp/Queries"];
        var sorted = TestSidecars.Of([
            .. folders
                .Distinct(SqlPath.Comparer)
                .OrderBy(folder => folder, SqlPath.Comparer)
                .Select(folder => new Item(folder)),
        ]);
        const string Path = "ÄPP/QUERIES/a.sql";

        SqlPath.IndexOf(sorted, Path, "ÄPP/QUERIES".Length, static item => item.Path).ShouldBe(sorted.Count - 1);
        SqlPath.IndexOf(sorted, Path, "ÄPP".Length, static item => item.Path).ShouldBe(sorted.Count - 2);
        SqlPath.IndexOf(sorted, Path, 0, static item => item.Path).ShouldBe(0);
        SqlPath.IndexOf(sorted, "app/Queries/a.sql", "app/Queries".Length, static item => item.Path).ShouldBe(3);
        SqlPath.IndexOf(sorted, "app/Queries/a.sql", "app/Q".Length, static item => item.Path).ShouldBe(-1);
    }

    [Fact]
    public void FindRepeated_PathListedOnce_IsEmpty() =>
        FindRepeated(["app/A.sql", "app/b.sql"], ["app/A.sql", "app/b.sql"]).ShouldBeEmpty();

    [Fact]
    public void FindRepeated_NoFiles_IsEmpty() => FindRepeated([], []).ShouldBeEmpty();

    [Fact]
    public void FindRepeated_PathListedTwiceOrUnderTwoSpellings_IsThatPathOnce() =>
        FindRepeated(["a/A.sql", "a/a.sql", "a/A.sql", "b/B.sql"], ["a/A.sql", "b/B.sql"]).ShouldBe(["a/A.sql"]);

    [Fact]
    public void FindRepeated_SeveralRepeatedPaths_AreInTheOrderOfTheSortedPaths() =>
        FindRepeated(["c/C.sql", "b/B.sql", "b/b.sql", "a/A.sql", "c/C.sql"], ["a/A.sql", "b/B.sql", "c/C.sql"])
            .ShouldBe(["b/B.sql", "c/C.sql"]);

    private static EquatableArray<string> FindRepeated(string[] files, string[] sortedPaths) =>
        SqlPath.FindRepeated([.. files.Select(path => SqlFilePath.Create(path)!)], TestSidecars.Of(sortedPaths));

    [Theory]
    [InlineData("repo/obj/App.AssemblyInfo.cs", "repo/obj")]
    [InlineData("repo/obj/Debug/net10.0/App.AssemblyInfo.cs", "repo/obj")]
    [InlineData("REPO/Obj/x.cs", "repo/obj")]
    [InlineData("C:/repo/obj/x.cs", "C:/repo/obj")]
    [InlineData("repo/x.cs", "")]
    public void IsInFolder_PathUnderTheFolder_IsTrue(string path, string folder) =>
        SqlPath.IsInFolder(path, folder).ShouldBeTrue();

    [Theory]
    [InlineData("repo/objx/x.cs", "repo/obj")]
    [InlineData("repo/obj-old/x.cs", "repo/obj")]
    [InlineData("repo/obj.cs", "repo/obj")]
    [InlineData("repo/obj", "repo/obj")]
    [InlineData("repo/x.cs", "repo/obj")]
    [InlineData("other/repo/obj/x.cs", "repo/obj")]
    [InlineData("rep", "repo/obj")]
    public void IsInFolder_PathBesideOrAboveTheFolder_IsFalse(string path, string folder) =>
        SqlPath.IsInFolder(path, folder).ShouldBeFalse();

    [Theory]
    [InlineData("app/Repo", "app/Repo/a.sql", "app/Repo/Count.sql", GeneratorHarness.UsersNormalizedPath)]
    [InlineData("APP/repo", "app/Repo/a.sql", "app/Repo/Count.sql", GeneratorHarness.UsersNormalizedPath)]
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
        SqlPath.FindInFolder(TestSidecars.Of<string>(), "app").ShouldBeEmpty();
        SqlPath.FindInFolder(TestSidecars.Of<string>(), "").ShouldBeEmpty();
    }

    // FindInFolder steps over a subfolder with one search, from inside it; its output is the same if Seek steps one
    // path at a time, so only these pin the step.
    [Fact]
    public void Seek_PastASubfolderFromInsideIt_IsTheIndexAfterItsLastPath()
    {
        var first = SqlPath.IndexOf(FolderPaths, "app/Repo/Admin/Deep/x.sql", static path => path);
        var last = SqlPath.IndexOf(FolderPaths, "app/Repo/Admin/Roles.sql", static path => path);

        SqlPath
            .Seek(FolderPaths, first + 1, "app/Repo/Admin/Roles.sql", "app/Repo/Admin".Length, past: true)
            .ShouldBe(last + 1);
    }

    [Fact]
    public void Seek_NotPast_IsTheFirstPathUnderTheFolder() =>
        SqlPath
            .Seek(FolderPaths, 0, "app/Repo/Admin", "app/Repo/Admin".Length, past: false)
            .ShouldBe(SqlPath.IndexOf(FolderPaths, "app/Repo/Admin/Deep/x.sql", static path => path));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Seek_FromTheEnd_IsTheEnd(bool past) =>
        SqlPath.Seek(FolderPaths, FolderPaths.Count, "app", "app".Length, past).ShouldBe(FolderPaths.Count);

    // Checked against the plain definition: a file is in a folder when the folder of its path is that folder.
    [Fact]
    public void FindInFolder_GeneratedPaths_AgreesWithTheFolderOfEachPath()
    {
        // Names that sort before, at and after the separator, that differ only by case, and that are not ASCII.
        string[] names = ["a", "B", "a-b", "a.b", "a0", "a b", "ab", "é", "É2", "😀", "_", "Z"];
        string[] files = ["a.sql", "a0.sql", "_.sql"];
        var paths = TestSidecars.Of([
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
    private static readonly EquatableArray<string> FolderPaths = TestSidecars.Of([
        .. new[]
        {
            "Root.sql",
            "app/Repo/a.sql",
            "app/Repo/Admin/Deep/x.sql",
            "app/Repo/Admin/Roles.sql",
            "app/Repo/Count.sql",
            GeneratorHarness.UsersNormalizedPath,
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

    [Fact]
    public void ToSortedSet_Paths_AreEachOnceIgnoringCaseAndInOrder() =>
        SqlPath
            .ToSortedSet(["b/Users.sql", "a/Z.sql", "B/users.sql", "a/a.sql", "a/Z.sql"])
            .ShouldBe(["a/a.sql", "a/Z.sql", "b/Users.sql"]);

    [Fact]
    public void ToSortedSet_NoPaths_IsEmpty() => SqlPath.ToSortedSet([]).Count.ShouldBe(0);
}
