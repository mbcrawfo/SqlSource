using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.Tests.SqlServer.Realistic.Describer;

// What the describer gives for each realistic case against a real server: the refusal its case says or none, and
// the columns of its truth.  Whether its nullability is safe is RealisticNullabilitySafetyTests'.
[Collection(UsesSqlServer.Name)]
public sealed class RealisticDescriptionTests(SqlServerContainer sqlServer)
{
    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public async Task DescribeAsync_Case_IsRefusedOnlyAsItsCaseSays(string name)
    {
        var found = RealisticDescribe.Start(name, sqlServer.Major);

        var described = await RealisticDescribe.DescribeOnceAsync(found, sqlServer.RealisticConnectionString);

        if (found.Refusal is { } refusal)
        {
            described.Succeeded.ShouldBeFalse($"{found} is refused as {refusal}, and was described");
            described.Failure.Descriptor.Id.ShouldBe(refusal, $"{found} is refused as {refusal}");
            return;
        }

        _ = RealisticDescriptions.ColumnsOf(found, described);
    }
}
