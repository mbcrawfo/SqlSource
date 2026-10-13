using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The five rows of the spec's table, in its order: the first that fits a column is its answer.
public class ColumnNullabilityTests
{
    private static ColumnOrigin Origin(string relkind, bool notNull, bool? notNullValidated = null) =>
        new(
            PostgresTables.TableOf(7, "t") with
            {
                Relkind = relkind,
            },
            PostgresTables.Attribute(1, "c", PostgresRows.Int4.Oid, notNull, notNullValidated: notNullValidated)
        );

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Of_ColumnWithoutAnOrigin_IsUnknownAndSaysNoOrigin(bool gated) =>
        ColumnNullability.Of(null, gated).ShouldBe((null, SidecarValues.NullableSource.NoOrigin));

    // PostgreSQL does not record NOT NULL for a view's column, and does not enforce a foreign table's.  The answer
    // is the same whether the query is gated or not, and whatever the catalog says.
    [Theory]
    [InlineData("v")]
    [InlineData("m")]
    [InlineData("f")]
    public void Of_OriginInAViewAMaterializedViewOrAForeignTable_IsUnknownAndSaysView(string relkind)
    {
        foreach (var gated in new[] { false, true })
        {
            foreach (var notNull in new[] { false, true })
            {
                ColumnNullability
                    .Of(Origin(relkind, notNull), gated)
                    .ShouldBe((null, SidecarValues.NullableSource.View));
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Of_GatedQueryAndOriginInATable_IsNullableByTheHeuristic(bool notNull) =>
        ColumnNullability
            .Of(Origin("r", notNull), gated: true)
            .ShouldBe((true, SidecarValues.NullableSource.Heuristic));

    [Fact]
    public void Of_NotNullColumnOfATable_IsNotNullableByTheCatalog() =>
        ColumnNullability
            .Of(Origin("r", notNull: true), gated: false)
            .ShouldBe((false, SidecarValues.NullableSource.Catalog));

    [Fact]
    public void Of_NullableColumnOfATable_IsNullableByTheCatalog() =>
        ColumnNullability
            .Of(Origin("r", notNull: false), gated: false)
            .ShouldBe((true, SidecarValues.NullableSource.Catalog));

    // A partitioned table enforces its constraints as a table does.
    [Fact]
    public void Of_NotNullColumnOfAPartitionedTable_IsNotNullableByTheCatalog() =>
        ColumnNullability
            .Of(Origin("p", notNull: true), gated: false)
            .ShouldBe((false, SidecarValues.NullableSource.Catalog));

    // PostgreSQL 18 sets attnotnull for a NOT NULL constraint that is NOT VALID, and rows that were there before may
    // hold NULL.  Only a constraint that the server says is not validated changes the answer: null is an older
    // server, a column without the constraint, or an answer recorded before the member existed.
    [Fact]
    public void Of_NotNullColumnWhoseConstraintIsNotValidated_IsNullableByTheCatalog() =>
        ColumnNullability
            .Of(Origin("r", notNull: true, notNullValidated: false), gated: false)
            .ShouldBe((true, SidecarValues.NullableSource.Catalog));

    [Theory]
    [InlineData(true)]
    [InlineData(null)]
    public void Of_NotNullColumnWhoseConstraintIsValidatedOrUnsaid_IsNotNullableByTheCatalog(bool? validated) =>
        ColumnNullability
            .Of(Origin("r", notNull: true, notNullValidated: validated), gated: false)
            .ShouldBe((false, SidecarValues.NullableSource.Catalog));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void Of_NullableColumnWhateverTheConstraintMemberSays_IsNullableByTheCatalog(bool? validated) =>
        ColumnNullability
            .Of(Origin("r", notNull: false, notNullValidated: validated), gated: false)
            .ShouldBe((true, SidecarValues.NullableSource.Catalog));

    [Fact]
    public void Of_ConstraintNotValidatedInAView_IsStillUnknownAndSaysView() =>
        ColumnNullability
            .Of(Origin("v", notNull: true, notNullValidated: false), gated: false)
            .ShouldBe((null, SidecarValues.NullableSource.View));

    [Fact]
    public void Of_ConstraintNotValidatedInAGatedQuery_IsStillNullableByTheHeuristic() =>
        ColumnNullability
            .Of(Origin("r", notNull: true, notNullValidated: false), gated: true)
            .ShouldBe((true, SidecarValues.NullableSource.Heuristic));
}
