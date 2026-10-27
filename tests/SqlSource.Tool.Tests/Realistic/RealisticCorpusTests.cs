using System.IO;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.Tool.Tests.Realistic;

// Each engine's corpus is the folder Realistic/ of that engine's project, read for that engine, so that a corpus
// cannot read the other engine's cases or check them against the other engine's majors.
public sealed class RealisticCorpusTests
{
    [Fact]
    public void Cases_OfEachCorpus_IsItsProjectsCasesFolder()
    {
        RelativeToTheRepository(RealisticCorpus.Postgres.Cases)
            .ShouldBe("tests/SqlSource.Tests.Postgres/Realistic/Cases");
        RelativeToTheRepository(RealisticCorpus.SqlServer.Cases)
            .ShouldBe("tests/SqlSource.Tests.SqlServer/Realistic/Cases");
        Directory.Exists(RealisticCorpus.Postgres.Cases).ShouldBeTrue();
        Directory.Exists(RealisticCorpus.SqlServer.Cases).ShouldBeTrue();
    }

    [Fact]
    public void Files_OfEachCorpus_AreInTheRepository()
    {
        foreach (var corpus in new[] { RealisticCorpus.Postgres, RealisticCorpus.SqlServer })
        {
            File.Exists(corpus.Schema).ShouldBeTrue(corpus.Schema);
            File.Exists(corpus.Seed).ShouldBeTrue(corpus.Seed);
            File.Exists(corpus.Queries).ShouldBeTrue(corpus.Queries);
            File.Exists(corpus.Methods).ShouldBeTrue(corpus.Methods);
        }
    }

    [Fact]
    public void SettingsFiles_OfEachCorpus_AreTheProjectsAndTheCasesFolders()
    {
        RelativeToTheRepository(RealisticCorpus.Postgres.ProjectSettingsFile)
            .ShouldBe("tests/SqlSource.Tests.Postgres/sqlsource.json");
        RelativeToTheRepository(RealisticCorpus.Postgres.CasesSettingsFile)
            .ShouldBe("tests/SqlSource.Tests.Postgres/Realistic/Cases/sqlsource.json");
        RelativeToTheRepository(RealisticCorpus.SqlServer.ProjectSettingsFile)
            .ShouldBe("tests/SqlSource.Tests.SqlServer/sqlsource.json");
        RelativeToTheRepository(RealisticCorpus.SqlServer.CasesSettingsFile)
            .ShouldBe("tests/SqlSource.Tests.SqlServer/Realistic/Cases/sqlsource.json");
        foreach (var corpus in new[] { RealisticCorpus.Postgres, RealisticCorpus.SqlServer })
        {
            File.Exists(corpus.ProjectSettingsFile).ShouldBeTrue(corpus.ProjectSettingsFile);
            File.Exists(corpus.CasesSettingsFile).ShouldBeTrue(corpus.CasesSettingsFile);
        }
    }

    [Fact]
    public void Engine_OfEachCorpus_HasTheEnginesRecordedMajors()
    {
        RealisticCorpus.Postgres.Engine.Engine.ShouldBe(SidecarValues.Engine.Postgres);
        RealisticCorpus.Postgres.Engine.Product.ShouldBe(EngineImage.Postgres.Product);
        RealisticCorpus.Postgres.Engine.RecordedMajors.ShouldBe(EngineImage.Postgres.RecordedMajors);
        RealisticCorpus.SqlServer.Engine.Engine.ShouldBe(SidecarValues.Engine.SqlServer);
        RealisticCorpus.SqlServer.Engine.Product.ShouldBe(EngineImage.SqlServer.Product);
        RealisticCorpus.SqlServer.Engine.RecordedMajors.ShouldBe(EngineImage.SqlServer.RecordedMajors);
    }

    // The path from the repository's root, with / between its folders on every system.
    private static string RelativeToTheRepository(string path) =>
        Path.GetRelativePath(RepositoryRoot.Folder, path).Replace(Path.DirectorySeparatorChar, '/');
}
