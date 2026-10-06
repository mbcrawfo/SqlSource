using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation;
using Xunit;

namespace SqlSource.Tests.Generation;

public class PathResolverTests
{
    // Normalised and in order, as the pipeline supplies them.
    private static readonly EquatableArray<string> SqlPaths = TestModels.Array(
        "app/Queries/Orders.sql",
        "app/Queries/Users.sql",
        "app/Repo/Admin/Roles.sql",
        "app/Repo/Count.sql",
        "app/Repo/Users.sql",
        "app/Root.sql"
    );

    [Fact]
    public void Resolve_NoPath_TakesTheFilesInTheFolderOfTheSourceFileAndNotItsSubfolders()
    {
        var result = PathResolver.Resolve(TestModels.Type(), SqlPaths, isSupportedFramework: true);

        result.Files.ShouldBe(["app/Repo/Count.sql", "app/Repo/Users.sql"]);
        result.Diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("../Queries")]
    [InlineData("..\\Queries\\")]
    [InlineData("../queries")]
    [InlineData("./../Repo/../Queries")]
    public void Resolve_FolderPath_TakesTheFilesInThatFolder(string path)
    {
        var result = PathResolver.Resolve(TestModels.Type(path), SqlPaths, isSupportedFramework: true);

        result.Files.ShouldBe(["app/Queries/Orders.sql", "app/Queries/Users.sql"]);
        result.Diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Users.sql", "app/Repo/Users.sql")]
    [InlineData("users.SQL", "app/Repo/Users.sql")]
    [InlineData("Admin/Roles.sql", "app/Repo/Admin/Roles.sql")]
    [InlineData("Admin\\Roles.sql", "app/Repo/Admin/Roles.sql")]
    [InlineData("../Root.sql", "app/Root.sql")]
    [InlineData("/Users.sql", "app/Repo/Users.sql")]
    public void Resolve_FilePath_TakesThatFile(string path, string expected)
    {
        var result = PathResolver.Resolve(TestModels.Type(path), SqlPaths, isSupportedFramework: true);

        result.Files.ShouldBe([expected]);
        result.Diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Missing.sql")]
    [InlineData("Missing")]
    [InlineData("Users")]
    [InlineData("Admin/Roles.sql/")]
    [InlineData("../../../Root.sql")]
    [InlineData("/app/Queries")]
    [InlineData(" ")]
    public void Resolve_PathThatMatchesNothing_IsAnErrorAtTheAttribute(string path)
    {
        var result = PathResolver.Resolve(TestModels.Type(path), SqlPaths, isSupportedFramework: true);

        result.Files.ShouldBeEmpty();
        result.Diagnostics.ShouldBe([
            DiagnosticInfo.Create(SqlDiagnostics.PathMatchesNothing, TestModels.AttributeLocation, path),
        ]);
    }

    [Theory]
    [InlineData("/app/Empty/UserRepository.cs")]
    [InlineData("UserRepository.cs")]
    [InlineData("")]
    public void Resolve_NoPathAndNoSqlFileInTheFolder_IsAnErrorAtTheAttribute(string filePath)
    {
        var result = PathResolver.Resolve(TestModels.Type(filePath: filePath), SqlPaths, isSupportedFramework: true);

        result.Files.ShouldBeEmpty();
        result.Diagnostics.ShouldBe([
            DiagnosticInfo.Create(SqlDiagnostics.FolderHasNoSqlFile, TestModels.AttributeLocation),
        ]);
    }

    [Fact]
    public void Resolve_SourceFileWithWindowsPath_MatchesTheSameFolder()
    {
        var type = TestModels.Type(filePath: "C:\\App\\REPO\\UserRepository.cs");
        var paths = TestModels.Array("c:/app/repo/Users.sql", "c:/app/repo2/Users.sql");

        PathResolver.Resolve(type, paths, isSupportedFramework: true).Files.ShouldBe(["c:/app/repo/Users.sql"]);
    }

    [Fact]
    public void Resolve_UnsupportedFramework_IsAnErrorAndTheFilesAreStillResolved()
    {
        var result = PathResolver.Resolve(TestModels.Type("Users.sql"), SqlPaths, isSupportedFramework: false);

        result.Files.ShouldBe(["app/Repo/Users.sql"]);
        result.Diagnostics.ShouldBe([
            DiagnosticInfo.Create(SqlDiagnostics.UnsupportedTargetFramework, TestModels.AttributeLocation),
        ]);
    }

    [Fact]
    public void Resolve_TypeWithProblems_KeepsThemBeforeItsOwn()
    {
        var declared = DiagnosticInfo.Create(SqlDiagnostics.TypeNotPartial, TestModels.AttributeLocation, "Repo");
        var type = TestModels.Type("Missing", diagnostics: [declared]);

        var result = PathResolver.Resolve(type, SqlPaths, isSupportedFramework: false);

        result.Type.ShouldBeSameAs(type);
        result.Diagnostics.ShouldBe([
            declared,
            DiagnosticInfo.Create(SqlDiagnostics.UnsupportedTargetFramework, TestModels.AttributeLocation),
            DiagnosticInfo.Create(SqlDiagnostics.PathMatchesNothing, TestModels.AttributeLocation, "Missing"),
        ]);
    }

    [Fact]
    public void Resolve_SameInputs_GiveEqualResults() =>
        PathResolver
            .Resolve(TestModels.Type("../Queries"), SqlPaths, isSupportedFramework: true)
            .ShouldBe(PathResolver.Resolve(TestModels.Type("../Queries"), SqlPaths, isSupportedFramework: true));
}
