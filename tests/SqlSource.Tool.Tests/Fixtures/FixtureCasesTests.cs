using System;
using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Fixtures;

// Which cases FixtureCases finds under a root: a folder of each case under the engine's, in name order.
public sealed class FixtureCasesTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void Of_EngineWithNoFolder_IsEmpty() => FixtureCases.Of("no-such-engine").ShouldBeEmpty();

    [Fact]
    public void InFolder_EngineWithCases_GivesThemInNameOrder()
    {
        _ = FixtureCaseFiles.Create(_folder.Path, SidecarValues.Engine.Postgres, "b", "SELECT 1;\n");
        _ = FixtureCaseFiles.Create(_folder.Path, SidecarValues.Engine.Postgres, "a", "SELECT 1;\n");
        _ = _folder.WriteFile("postgres/schema.sql", "CREATE TABLE users ();\n");

        var cases = FixtureCases.InFolder(_folder.PathOf(SidecarValues.Engine.Postgres));

        cases.Select(found => found.Name).ShouldBe(["a", "b"]);
        cases.ShouldAllBe(found => found.Engine == "postgres");
        cases[0].Folder.ShouldBe(_folder.PathOf("postgres/a"));
    }

    // A folder without a query is no case, whether empty, as an incremental build leaves the folder of a case
    // renamed or removed in the output, or holding other files of a case.
    [Fact]
    public void InFolder_FolderWithoutAQuery_IsNoCase()
    {
        _ = FixtureCaseFiles.Create(_folder.Path, SidecarValues.Engine.Postgres, "a", "SELECT 1;\n");
        _ = Directory.CreateDirectory(_folder.PathOf("postgres/gone"));
        _ = _folder.WriteFile("postgres/partial/capture.json", "[]\n");

        var cases = FixtureCases.InFolder(_folder.PathOf(SidecarValues.Engine.Postgres));

        cases.Select(found => found.Name).ShouldBe(["a"]);
    }
}
