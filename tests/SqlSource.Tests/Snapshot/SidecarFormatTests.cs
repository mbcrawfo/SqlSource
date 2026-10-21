using Shouldly;
using SqlSource.Generation.Paths;
using SqlSource.Snapshot;
using Xunit;

namespace SqlSource.Tests.Snapshot;

public sealed class SidecarFormatTests
{
    // The tool writes a sidecar at PathFor and the generator finds it by SqlPath: they must name one file.
    [Fact]
    public void PathFor_SqlPath_IsTheSidecarPathThatSqlPathRecognises()
    {
        var sidecarPath = SidecarFormat.PathFor("a/Q.sql");

        sidecarPath.ShouldBe("a/Q.sql.json");
        SqlPath.IsSidecarFile(sidecarPath).ShouldBeTrue();
        SqlPath.SqlPathOfSidecar(sidecarPath).ShouldBe("a/Q.sql");
        SqlPath.IsSidecarPathOf(sidecarPath, "a/Q.sql").ShouldBeTrue();
    }
}
