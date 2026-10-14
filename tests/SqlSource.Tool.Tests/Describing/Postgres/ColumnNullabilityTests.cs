using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The rows of the spec's table, in its order, and the rules of the catalog the reviews added: the first that fits a
// column is its answer.
public class ColumnNullabilityTests
{
    private static ColumnOrigin Origin(
        string relkind,
        bool notNull,
        bool? notNullValidated = null,
        bool? relhassubclass = false,
        bool? hasForeignPartition = false,
        bool? hasInsteadRule = false
    ) =>
        new(
            PostgresTables.TableOf(7, "t") with
            {
                Relkind = relkind,
                Relhassubclass = relhassubclass,
                HasForeignPartition = hasForeignPartition,
                HasInsteadRule = hasInsteadRule,
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

    // A child of a legacy inheritance parent may drop a NOT NULL it inherited, before PostgreSQL 18, and a query of
    // the parent reads the child's rows.  So a table that has or had children is nullable whatever its own column
    // says, on every version.
    [Fact]
    public void Of_NotNullColumnOfATableWithChildren_IsNullableByTheCatalog() =>
        ColumnNullability
            .Of(Origin("r", notNull: true, relhassubclass: true), gated: false)
            .ShouldBe((true, SidecarValues.NullableSource.Catalog));

    // False is a table that never had a child, and null an answer recorded before the member existed.
    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public void Of_NotNullColumnOfATableWithoutChildrenOrUnsaid_IsNotNullableByTheCatalog(bool? relhassubclass) =>
        ColumnNullability
            .Of(Origin("r", notNull: true, relhassubclass: relhassubclass), gated: false)
            .ShouldBe((false, SidecarValues.NullableSource.Catalog));

    // A partition can never drop its parent's NOT NULL, and none of this table's is a foreign table.
    [Fact]
    public void Of_NotNullColumnOfAPartitionedTableWithPartitions_IsNotNullableByTheCatalog() =>
        ColumnNullability
            .Of(Origin("p", notNull: true, relhassubclass: true), gated: false)
            .ShouldBe((false, SidecarValues.NullableSource.Catalog));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void Of_GatedQueryOverATableWithOrWithoutChildren_IsNullableByTheHeuristic(bool? relhassubclass) =>
        ColumnNullability
            .Of(Origin("r", notNull: true, relhassubclass: relhassubclass), gated: true)
            .ShouldBe((true, SidecarValues.NullableSource.Heuristic));

    // A foreign table may be a partition of a partitioned table: it inherits the parent's NOT NULL, which nothing
    // enforces on its remote rows, and a query of the parent reads them.
    [Fact]
    public void Of_NotNullColumnOfAPartitionedTableWithAForeignPartition_IsNullableByTheCatalog() =>
        ColumnNullability
            .Of(Origin("p", notNull: true, relhassubclass: true, hasForeignPartition: true), gated: false)
            .ShouldBe((true, SidecarValues.NullableSource.Catalog));

    // False is a partitioned table without a foreign partition, and null an answer recorded before the member
    // existed.
    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public void Of_NotNullColumnOfAPartitionedTableWithoutAForeignPartitionOrUnsaid_IsNotNullableByTheCatalog(
        bool? hasForeignPartition
    ) =>
        ColumnNullability
            .Of(
                Origin("p", notNull: true, relhassubclass: true, hasForeignPartition: hasForeignPartition),
                gated: false
            )
            .ShouldBe((false, SidecarValues.NullableSource.Catalog));

    // The member says something of a partitioned table alone, and tables never sets it for another.
    [Fact]
    public void Of_NotNullColumnOfATableThatSaysAForeignPartition_IsNotNullableByTheCatalog() =>
        ColumnNullability
            .Of(Origin("r", notNull: true, relhassubclass: false, hasForeignPartition: true), gated: false)
            .ShouldBe((false, SidecarValues.NullableSource.Catalog));

    [Fact]
    public void Of_GatedQueryOverAPartitionedTableWithAForeignPartition_IsNullableByTheHeuristic() =>
        ColumnNullability
            .Of(Origin("p", notNull: true, relhassubclass: true, hasForeignPartition: true), gated: true)
            .ShouldBe((true, SidecarValues.NullableSource.Heuristic));

    // A DO INSTEAD rule on INSERT, UPDATE or DELETE replaces the statement by its action, usually on another
    // relation, whose rows the RETURNING list gives, while the row description keeps this relation as the origin.  A
    // rule on a partitioned table is allowed.
    [Theory]
    [InlineData("r")]
    [InlineData("p")]
    public void Of_NotNullColumnOfATableWithAnInsteadRule_IsNullableByTheCatalog(string relkind) =>
        ColumnNullability
            .Of(Origin(relkind, notNull: true, hasInsteadRule: true), gated: false)
            .ShouldBe((true, SidecarValues.NullableSource.Catalog));

    // False is a relation without such a rule, and null an answer recorded before the member existed.
    [Theory]
    [InlineData("r", false)]
    [InlineData("r", null)]
    [InlineData("p", false)]
    [InlineData("p", null)]
    public void Of_NotNullColumnOfATableWithoutAnInsteadRuleOrUnsaid_IsNotNullableByTheCatalog(
        string relkind,
        bool? hasInsteadRule
    ) =>
        ColumnNullability
            .Of(Origin(relkind, notNull: true, hasInsteadRule: hasInsteadRule), gated: false)
            .ShouldBe((false, SidecarValues.NullableSource.Catalog));

    [Fact]
    public void Of_ViewWithAnInsteadRule_IsStillUnknownAndSaysView() =>
        ColumnNullability
            .Of(Origin("v", notNull: true, hasInsteadRule: true), gated: false)
            .ShouldBe((null, SidecarValues.NullableSource.View));

    [Fact]
    public void Of_GatedQueryOverATableWithAnInsteadRule_IsNullableByTheHeuristic() =>
        ColumnNullability
            .Of(Origin("r", notNull: true, hasInsteadRule: true), gated: true)
            .ShouldBe((true, SidecarValues.NullableSource.Heuristic));
}
