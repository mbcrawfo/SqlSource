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
        var result = PathResolver.Resolve(
            TestModels.Type(),
            SqlPaths,
            isSupportedFramework: true,
            unsupportedLanguageVersion: null
        );

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
        var result = PathResolver.Resolve(
            TestModels.Type(path),
            SqlPaths,
            isSupportedFramework: true,
            unsupportedLanguageVersion: null
        );

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
        var result = PathResolver.Resolve(
            TestModels.Type(path),
            SqlPaths,
            isSupportedFramework: true,
            unsupportedLanguageVersion: null
        );

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
        var result = PathResolver.Resolve(
            TestModels.Type(path),
            SqlPaths,
            isSupportedFramework: true,
            unsupportedLanguageVersion: null
        );

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
        var result = PathResolver.Resolve(
            TestModels.Type(filePath: filePath),
            SqlPaths,
            isSupportedFramework: true,
            unsupportedLanguageVersion: null
        );

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

        PathResolver
            .Resolve(type, paths, isSupportedFramework: true, unsupportedLanguageVersion: null)
            .Files.ShouldBe(["c:/app/repo/Users.sql"]);
    }

    [Theory]
    [InlineData("/UserRepository.cs", null)]
    [InlineData("UserRepository.cs", null)]
    [InlineData("/app/Repo/UserRepository.cs", "../..")]
    public void Resolve_FolderThatIsTheRoot_TakesTheFilesWithNoFolder(string filePath, string? path)
    {
        var paths = TestModels.Array("app/Repo/Users.sql", "app/Root.sql", "Root.sql", "zeta.sql");

        var result = PathResolver.Resolve(
            TestModels.Type(path, filePath: filePath),
            paths,
            isSupportedFramework: true,
            unsupportedLanguageVersion: null
        );

        result.Files.ShouldBe(["Root.sql", "zeta.sql"]);
        result.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_FolderWhoseNeighboursStartWithItsName_TakesOnlyItsOwnFiles()
    {
        // In order: the names sort before and after the separator that follows the folder's own.
        var paths = TestModels.Array(
            "app/Repo-old/Users.sql",
            "app/Repo.sql",
            "app/Repo/Sub/Users.sql",
            "app/Repo/Users.sql",
            "app/Repo0/Users.sql",
            "app/RepoArchive/Users.sql"
        );

        PathResolver
            .Resolve(TestModels.Type(), paths, isSupportedFramework: true, unsupportedLanguageVersion: null)
            .Files.ShouldBe(["app/Repo/Users.sql"]);
    }

    [Fact]
    public void Resolve_UnsupportedFramework_IsAnErrorAndTheFilesAreStillResolved()
    {
        var result = PathResolver.Resolve(
            TestModels.Type("Users.sql"),
            SqlPaths,
            isSupportedFramework: false,
            unsupportedLanguageVersion: null
        );

        result.Files.ShouldBe(["app/Repo/Users.sql"]);
        result.Diagnostics.ShouldBe([
            DiagnosticInfo.Create(SqlDiagnostics.UnsupportedTargetFramework, TestModels.AttributeLocation),
        ]);
    }

    [Fact]
    public void Resolve_UnsupportedLanguageVersion_IsAnErrorAndTheFilesAreStillResolved()
    {
        var result = PathResolver.Resolve(
            TestModels.Type("Users.sql"),
            SqlPaths,
            isSupportedFramework: true,
            unsupportedLanguageVersion: "11.0"
        );

        result.Files.ShouldBe(["app/Repo/Users.sql"]);
        result.Diagnostics.ShouldBe([
            DiagnosticInfo.Create(SqlDiagnostics.UnsupportedLanguageVersion, TestModels.AttributeLocation, "11.0"),
        ]);
    }

    // A project on an older framework has an older language by default.  Targeting .NET 8 fixes both, so the
    // framework is the one problem to report.
    [Fact]
    public void Resolve_UnsupportedFrameworkAndLanguageVersion_ReportsTheFrameworkOnly()
    {
        var result = PathResolver.Resolve(
            TestModels.Type("Users.sql"),
            SqlPaths,
            isSupportedFramework: false,
            unsupportedLanguageVersion: "7.3"
        );

        result.Diagnostics.ShouldBe([
            DiagnosticInfo.Create(SqlDiagnostics.UnsupportedTargetFramework, TestModels.AttributeLocation),
        ]);
    }

    [Fact]
    public void Resolve_TypeWithProblems_KeepsThemBeforeItsOwn()
    {
        var declared = DiagnosticInfo.Create(SqlDiagnostics.TypeNotPartial, TestModels.AttributeLocation, "Repo");
        var type = TestModels.Type("Missing", diagnostics: [declared]);

        var result = PathResolver.Resolve(
            type,
            SqlPaths,
            isSupportedFramework: false,
            unsupportedLanguageVersion: null
        );

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
            .Resolve(
                TestModels.Type("../Queries"),
                SqlPaths,
                isSupportedFramework: true,
                unsupportedLanguageVersion: null
            )
            .ShouldBe(
                PathResolver.Resolve(
                    TestModels.Type("../Queries"),
                    SqlPaths,
                    isSupportedFramework: true,
                    unsupportedLanguageVersion: null
                )
            );

    [Theory]
    [InlineData(null, "app/Repo/Count.sql|app/Repo/Users.sql")]
    [InlineData("../Queries", "app/Queries/Orders.sql|app/Queries/Users.sql")]
    [InlineData("users.SQL", "app/Repo/Users.sql")]
    [InlineData("Missing.sql", "")]
    [InlineData("../../..", "")]
    public void FindFiles_PathOfAnAttribute_GivesTheFilesItNames(string? path, string expected) =>
        string.Join('|', PathResolver.FindFiles("/app/Repo/UserRepository.cs", path, SqlPaths)).ShouldBe(expected);

    [Fact]
    public void FindFiles_SourceFileWhosePathLeavesTheRoot_GivesNone() =>
        PathResolver.FindFiles("../UserRepository.cs", null, SqlPaths).ShouldBeEmpty();
}
