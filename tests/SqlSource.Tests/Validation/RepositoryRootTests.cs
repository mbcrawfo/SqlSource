using System;
using System.IO;
using Shouldly;
using SqlSource.Testing;
using Xunit;

namespace SqlSource.Tests.Validation;

// The run-time check of RepositoryRoot, on small scopes texts and projects of its own, so that no test here depends
// on the real table.  The tests of PathFor read real folders of the repository.
public sealed class RepositoryRootTests
{
    private const string Pg = TestScopes.AssemblyPrefix + "PgTests";
    private const string Ms = TestScopes.AssemblyPrefix + "MsTests";

    // A broad line under the engines' lines, as the real table has, so that a folder decided by its own path and not
    // by the files it holds would fall in it.
    private static readonly TestScopes Scopes = TestScopes.Parse(
        """
        project postgres tests/PgTests/PgTests.csproj
        project sqlserver tests/MsTests/MsTests.csproj
        sqlserver tests/PgTests/Shared/*
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
            RepositoryRoot.Check(Pg, "tests/PgTests/", Scopes, ["tests/PgTests/a.sql", "tests/PgTests/Shared/b.sql"])
        );

        thrown.Message.ShouldContain("tests/PgTests/Shared/b.sql under tests/PgTests/");
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
    public void PathFor_FolderOfTheProjectsOwnScope_IsTheFolder() =>
        RepositoryRoot
            .PathFor(Pg, DescriberScopes, [.. Describers, "postgres"])
            .ShouldBe(RepositoryRoot.Path([.. Describers, "postgres"]));

    [Fact]
    public void PathFor_FolderOutsideTheProjectsScope_Throws()
    {
        var thrown = Should.Throw<InvalidOperationException>(() =>
            RepositoryRoot.PathFor(Pg, DescriberScopes, Describers)
        );

        thrown.Message.ShouldContain("tests/SqlSource.Tool.Tests/Fixtures/Describers/");
    }

    [Fact]
    public void PathFor_AboveTheRoot_Throws() =>
        Should.Throw<ArgumentException>(() => RepositoryRoot.PathFor(Pg, DescriberScopes, "..", "elsewhere"));

    [Fact]
    public void PathFor_UnlistedProject_PassesUnchecked() =>
        RepositoryRoot
            .PathFor("OtherTests", Scopes, "docs", "manual", "README.md")
            .ShouldBe(RepositoryRoot.Path("docs", "manual", "README.md"));

    // This project's own bin/ and obj/, which its build makes, are put in the other engine, and one of its sources is
    // too: the check never lists the build output, and still lists every other file of the folder.
    [Fact]
    public void PathFor_FolderWithBinAndObjOfTheOtherEngine_ChecksOnlyItsOtherFiles()
    {
        string[] folder = ["tests", "SqlSource.Tests"];
        Directory.Exists(RepositoryRoot.Path([.. folder, "bin"])).ShouldBeTrue();
        Directory.Exists(RepositoryRoot.Path([.. folder, "obj"])).ShouldBeTrue();
        const string Build = """
            project postgres tests/PgTests/PgTests.csproj
            sqlserver tests/SqlSource.Tests/bin/*
            sqlserver tests/SqlSource.Tests/obj/*
            postgres tests/SqlSource.Tests/*
            """;
        var source = "tests/SqlSource.Tests/Validation/RepositoryRootTests.cs";

        _ = Should.NotThrow(() => RepositoryRoot.PathFor(Pg, TestScopes.Parse(Build), folder));
        var thrown = Should.Throw<InvalidOperationException>(() =>
            RepositoryRoot.PathFor(Pg, TestScopes.Parse($"sqlserver {source}\n{Build}"), folder)
        );

        thrown.Message.ShouldContain(source);
    }

    [Fact]
    public void RunningProject_InThisProject_IsSqlSourceTests() =>
        RepositoryRoot.RunningProject.ShouldBe("SqlSource.Tests");
}
