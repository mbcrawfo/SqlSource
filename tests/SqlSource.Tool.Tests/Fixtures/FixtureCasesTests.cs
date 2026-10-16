using System;
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
    public void Of_EngineWithCases_GivesThemInNameOrder()
    {
        _ = FixtureCaseFiles.Create(_folder.Path, SidecarValues.Engine.Postgres, "b", "SELECT 1;\n");
        _ = FixtureCaseFiles.Create(_folder.Path, SidecarValues.Engine.Postgres, "a", "SELECT 1;\n");
        _ = _folder.WriteFile("postgres/schema.sql", "CREATE TABLE users ();\n");

        var cases = FixtureCases.Of(SidecarValues.Engine.Postgres, _folder.Path);

        cases.Select(found => found.Name).ShouldBe(["a", "b"]);
        cases.ShouldAllBe(found => found.Engine == "postgres");
        cases[0].Folder.ShouldBe(_folder.PathOf("postgres/a"));
    }
}
