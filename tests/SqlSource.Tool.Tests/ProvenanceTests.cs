using Shouldly;
using SqlSource.Tool.Testing.Bundles;
using Xunit;

namespace SqlSource.Tool.Tests;

// The lines of an entry that a renaming must not change, read from a raw bundle of SQL Server's fixtures.
public sealed class ProvenanceTests
{
    [Fact]
    public void Provenance_Entry_IsItsPlanTableMatchAndSources()
    {
        var raw = SqlServerRenamings.Raw("select-left-join");

        var provenance = Provenance.Of(raw.Expected.ShouldNotBeNull());

        provenance.ShouldBe(
            string.Join(
                '\n',
                "plan \"skipped\"",
                "tableMatch \"several-tables\"",
                "columns[0].nullableSource \"server\"",
                "columns[1].nullableSource \"server\"",
                "columns[2].nullableSource \"server\"",
                "columns[3].nullableSource \"server\""
            )
        );
    }
}
