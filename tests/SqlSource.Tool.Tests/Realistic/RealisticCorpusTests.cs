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
        RealisticCorpus.Postgres.Cases.ShouldBe(
            Path.Combine(RepositoryRoot.Folder, "tests", "SqlSource.Tests.Postgres", "Realistic", "Cases")
        );
        RealisticCorpus.SqlServer.Cases.ShouldBe(
            Path.Combine(RepositoryRoot.Folder, "tests", "SqlSource.Tests.SqlServer", "Realistic", "Cases")
        );
        Directory.Exists(RealisticCorpus.Postgres.Cases).ShouldBeTrue();
        Directory.Exists(RealisticCorpus.SqlServer.Cases).ShouldBeTrue();
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
}
