using System.Collections.Immutable;
using System.Linq;
using Shouldly;
using SqlSource.Generation;
using Xunit;

namespace SqlSource.Tests.Generation;

public class PathCollisionTests
{
    [Fact]
    public void Find_PathsThatDifferByMoreThanCase_IsEmpty() =>
        Find("/app/Repo/Users.sql", "/app/Repo/Orders.sql", "/app/Other/Users.sql").ShouldBeEmpty();

    [Fact]
    public void Find_NoPaths_IsEmpty() => Find().ShouldBeEmpty();

    [Fact]
    public void Find_OneFileUnderSpellingsThatNormalizeAlike_IsEmpty() =>
        Find("/app/Repo/Users.sql", "\\app\\Repo\\Users.sql", "/app/Repo/Sub/../Users.sql").ShouldBeEmpty();

    [Theory]
    [InlineData("/app/Repo/Users.sql", "/app/Repo/users.sql", "app/Repo/users.sql")]
    [InlineData("/app/Repo/Users.sql", "/app/REPO/Users.sql", "app/REPO/Users.sql")]
    [InlineData("C:\\app\\Repo\\Users.sql", "c:/app/Repo/Users.SQL", "c:/app/Repo/Users.SQL")]
    public void Find_PathThatDiffersOnlyByCaseFromAnEarlierOne_CollidesWithTheEarlierOne(
        string first,
        string second,
        string normalized
    ) => Find("/app/Repo/Orders.sql", first, second).ShouldBe([new PathCollision(normalized, second, first)]);

    [Fact]
    public void Find_SeveralSpellingsOfOnePath_CollideWithTheFirstListedOnceEach() =>
        Find("/app/b.sql", "/app/B.sql", "/app/a.sql", "/app/./B.sql", "/app/B.SQL", "/app/A.sql")
            .ShouldBe([
                new PathCollision("app/B.sql", "/app/B.sql", "/app/b.sql"),
                new PathCollision("app/B.SQL", "/app/B.SQL", "/app/b.sql"),
                new PathCollision("app/A.sql", "/app/A.sql", "/app/a.sql"),
            ]);

    // The paths go through the same steps as in the pipeline: normalised one at a time, then the distinct set in the
    // order of SqlPath.Comparer, which keeps the first spelling the project lists.
    private static EquatableArray<PathCollision> Find(params string[] paths)
    {
        var files = paths.Select(path => SqlFilePath.Create(path)!).ToImmutableArray();
        var sortedPaths = TestModels.Array([
            .. files
                .Select(file => file.NormalizedPath)
                .Distinct(SqlPath.Comparer)
                .OrderBy(path => path, SqlPath.Comparer),
        ]);

        return PathCollision.Find(files, sortedPaths);
    }
}
