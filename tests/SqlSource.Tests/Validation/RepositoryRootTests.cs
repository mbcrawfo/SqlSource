using System;
using Shouldly;
using SqlSource.Testing;
using Xunit;

namespace SqlSource.Tests.Validation;

// The run-time check of RepositoryRoot, on small scopes texts and projects of its own, so that no test here depends
// on the real table.  The tests of Path read real folders of the repository.
public sealed class RepositoryRootTests
{
    private const string Pg = "PgTests";
    private const string Ms = "MsTests";

    // A broad line under the engines' lines, as the real table has, so that a folder decided by its own path and not
    // by the files it holds would fall in it.
    private static readonly TestScopes Scopes = TestScopes.Parse(
        """
        project postgres tests/PgTests/PgTests.csproj
        project sqlserver tests/MsTests/MsTests.csproj
        postgres tests/PgTests/*
        sqlserver tests/MsTests/*
        none docs/*
        none tests/*
        """
    );

    private static readonly string[] Describers = ["tests", "SqlSource.Tool.Tests", "Fixtures", "Describers"];

    private static readonly TestScopes DescriberScopes = TestScopes.Parse(
        """
        project postgres tests/PgTests/PgTests.csproj
        postgres tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/*
        none tests/*
        """
    );

    [Fact]
    public void Check_PathOfTheOtherEngine_ThrowsNamingTheScopesFile()
    {
        var thrown = Should.Throw<InvalidOperationException>(() =>
            RepositoryRoot.Check(Pg, "tests/MsTests/schema.sql", Scopes)
        );

        thrown.Message.ShouldContain(Pg);
        thrown.Message.ShouldContain("tests/MsTests/schema.sql");
        thrown.Message.ShouldContain(nameof(TestScope.SqlServer));
        thrown.Message.ShouldContain("tools/test-scopes.txt");
    }

    [Fact]
    public void Check_PathOfNone_ThrowsForAContainerProject() =>
        Should.Throw<InvalidOperationException>(() => RepositoryRoot.Check(Ms, "docs/manual/README.md", Scopes));

    [Fact]
    public void Check_FolderWithAFileOfTheOtherEngine_Throws()
    {
        var thrown = Should.Throw<InvalidOperationException>(() =>
            RepositoryRoot.Check(Pg, "tests", Scopes, ["tests/PgTests/a.sql", "tests/MsTests/b.sql"])
        );

        thrown.Message.ShouldContain("tests/MsTests/b.sql");
    }

    [Fact]
    public void Check_FolderOfTheProjectsOwnScope_Passes() =>
        Should.NotThrow(() =>
            RepositoryRoot.Check(Pg, "tests/PgTests/", Scopes, ["tests/PgTests/a.sql", "tests/PgTests/Cases/b.json"])
        );

    [Fact]
    public void Check_UnlistedProject_PassesEverything() =>
        Should.NotThrow(() =>
            RepositoryRoot.Check("OtherTests", "docs", Scopes, ["docs/manual/README.md", "tests/MsTests/b.sql"])
        );

    [Fact]
    public void Path_FolderOfTheProjectsOwnScope_IsTheFolder() =>
        RepositoryRoot
            .Path(Pg, DescriberScopes, [.. Describers, "postgres"])
            .ShouldBe(RepositoryRoot.Path([.. Describers, "postgres"]));

    [Fact]
    public void Path_FolderOutsideTheProjectsScope_Throws()
    {
        var thrown = Should.Throw<InvalidOperationException>(() =>
            RepositoryRoot.Path(Pg, DescriberScopes, Describers)
        );

        thrown.Message.ShouldContain("tests/SqlSource.Tool.Tests/Fixtures/Describers/");
    }

    [Fact]
    public void Path_AboveTheRoot_Throws() =>
        Should.Throw<ArgumentException>(() => RepositoryRoot.Path(Pg, DescriberScopes, "..", "elsewhere"));

    [Fact]
    public void RunningProject_InThisProject_IsSqlSourceTests() =>
        RepositoryRoot.RunningProject.ShouldBe("SqlSource.Tests");
}
