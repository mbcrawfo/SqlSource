using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests;

// NullabilityAnswers names what the tests expect of a column's nullability.  This holds Of to the columns' own
// answers, in their order.
public sealed class NullabilityAnswersTests
{
    private static SidecarColumn ColumnWith(int ordinal, bool? nullable, string? source) =>
        new(ordinal, $"c{ordinal}", new OtherEngineType("int4"), nullable, source, null, null, null);

    [Fact]
    public void Of_Columns_AreTheirNullabilityAndSourceInOrder() =>
        NullabilityAnswers
            .Of([
                ColumnWith(0, false, SidecarValues.NullableSource.Catalog),
                ColumnWith(1, null, SidecarValues.NullableSource.NoOrigin),
            ])
            .ShouldBe([NullabilityAnswers.NotNullCatalog, NullabilityAnswers.UnknownNoOrigin]);
}
