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
}
