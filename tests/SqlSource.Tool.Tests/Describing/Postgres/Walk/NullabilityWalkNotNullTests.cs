using Shouldly;
using SqlSource.Tool.Describing.Postgres.Columns;
using SqlSource.Tool.Describing.Postgres.Types;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;
using static SqlSource.Tool.Testing.NullabilityAnswers;
using static SqlSource.Tool.Tests.PostgresPlans;
using static SqlSource.Tool.Tests.PostgresWalkColumns;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk;

// NullabilityWalk over a NOT NULL that the catalog does not hold to: one that is not validated, and the columns of a
// table with children, a foreign partition or an INSTEAD rule.
public sealed class NullabilityWalkNotNullTests
{
    // The rule of a NOT NULL is written once, in ColumnNullability: one that is NOT VALID is nullable here too.
    [Fact]
    public void Run_NotNullThatIsNotValidated_IsNullableByTheCatalog()
    {
        var table = PostgresTables.TableOf(
            16702,
            "n",
            PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true, notNullValidated: false)
        );
        var row = PostgresTables.ColumnOf(0, "id", PostgresRows.Int4.Oid, table, 1);
        var origin = ColumnOrigin.Find(row, PostgresTables.Of(table));
        var column = PostgresColumns.Build(
            row,
            origin,
            PostgresRows.Of(PostgresRows.Int4),
            TypeSpellings.None,
            gated: true
        );

        var outcome = NullabilityWalk.Run(
            new PlanInstancesCache(Scan("n", "id"), null),
            [column],
            [origin],
            Scope(true)
        );

        AnswersOf(outcome).ShouldBe([NullableCatalog]);
    }

    // And a NOT NULL column of a table that has inheritance children, whose child may have dropped it.
    [Fact]
    public void Run_NotNullColumnOfATableWithChildren_IsNullableByTheCatalog()
    {
        var parent = PostgresTables.TableOf(
            16703,
            "p",
            PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true)
        ) with
        {
            Relhassubclass = true,
        };
        var row = PostgresTables.ColumnOf(0, "id", PostgresRows.Int4.Oid, parent, 1);
        var origin = ColumnOrigin.Find(row, PostgresTables.Of(parent));
        var column = PostgresColumns.Build(
            row,
            origin,
            PostgresRows.Of(PostgresRows.Int4),
            TypeSpellings.None,
            gated: true
        );

        var outcome = NullabilityWalk.Run(
            new PlanInstancesCache(Scan("p", "id"), null),
            [column],
            [origin],
            Scope(true)
        );

        AnswersOf(outcome).ShouldBe([NullableCatalog]);
    }

    // And a NOT NULL column of a partitioned table with a foreign partition, which nothing holds to it.
    [Fact]
    public void Run_NotNullColumnOfAPartitionedTableWithAForeignPartition_IsNullableByTheCatalog()
    {
        var parted = PostgresTables.TableOf(
            16704,
            "pt",
            PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true)
        ) with
        {
            Relkind = "p",
            Relhassubclass = true,
            HasForeignPartition = true,
        };
        var row = PostgresTables.ColumnOf(0, "id", PostgresRows.Int4.Oid, parted, 1);
        var origin = ColumnOrigin.Find(row, PostgresTables.Of(parted));
        var column = PostgresColumns.Build(
            row,
            origin,
            PostgresRows.Of(PostgresRows.Int4),
            TypeSpellings.None,
            gated: true
        );

        var outcome = NullabilityWalk.Run(
            new PlanInstancesCache(Scan("pt", "id"), null),
            [column],
            [origin],
            Scope(true)
        );

        AnswersOf(outcome).ShouldBe([NullableCatalog]);
    }

    // And a NOT NULL column of a table with an INSTEAD rule: the INSERT into t2 is replaced by the rule's INSERT
    // into t3, so the root ModifyTable is t3's and its Output names t3, while the row description keeps t2 as the
    // column's origin.  The walk maps t3, nothing extends it, and the answer is t2's.
    [Fact]
    public void Run_NotNullColumnOfATableWithAnInsteadRule_IsNullableByTheCatalog()
    {
        var ruled = PostgresTables.TableOf(
            16705,
            "t2",
            PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true)
        ) with
        {
            HasInsteadRule = true,
        };
        var row = PostgresTables.ColumnOf(0, "id", PostgresRows.Int4.Oid, ruled, 1);
        var origin = ColumnOrigin.Find(row, PostgresTables.Of(ruled));
        var column = PostgresColumns.Build(
            row,
            origin,
            PostgresRows.Of(PostgresRows.Int4),
            TypeSpellings.None,
            gated: true
        );
        var plan = Over("ModifyTable", Scan("x") with { NodeType = "Result", Alias = null }) with
        {
            Alias = "t3",
            Output = ["t3.id"],
        };

        var outcome = NullabilityWalk.Run(new PlanInstancesCache(plan, null), [column], [origin], Scope(true));

        AnswersOf(outcome).ShouldBe([NullableCatalog]);
    }
}
