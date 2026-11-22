using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Columns;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;
using static SqlSource.Tool.Testing.NullabilityAnswers;
using static SqlSource.Tool.Tests.PostgresPlans;
using static SqlSource.Tool.Tests.PostgresWalkColumns;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk;

// NullabilityWalk over a RETURNING column of a value a root INSERT writes: each guard keeps a column the catalog
// answers nullable from being read as the value at its place.
public sealed class NullabilityWalkWrittenValueTests
{
    // A plain table whose second column is nullable, written with a literal.
    private static readonly PlanNode Written = Insert("t", ["opt", "1"], "1", "'x'::integer");

    private static WalkOutcome RunOf(
        PlanNode root,
        bool stand = true,
        bool oldAndNew = false,
        PostgresTableRow? table = null,
        int attnum = 2
    )
    {
        var (columns, origins) = Built(false, new Asked(Table, attnum), Expression);
        var tables = PostgresTables.Of(table ?? Table);
        origins[0] = ColumnOrigin.Find(
            PostgresTables.ColumnOf(0, "c0", PostgresRows.Int4.Oid, table ?? Table, attnum),
            tables
        );
        return NullabilityWalk.Run(
            new PlanInstancesCache(root, null),
            columns,
            origins,
            new WalkScope(
                false,
                oldAndNew,
                true,
                tables,
                [BuiltInTypes.Int4, BuiltInTypes.Int4],
                WrittenValuesStand: stand
            )
        );
    }

    [Fact]
    public void Run_ValueAtTheColumnsPlace_IsProved() =>
        AnswersOf(RunOf(Written)).ShouldBe([NotNullExpression, NotNullExpression]);

    [Fact]
    public void Run_NullAtTheColumnsPlace_IsNotProved() =>
        AnswersOf(RunOf(Insert("t", ["opt", "1"], "1", "NULL::integer")))
            .ShouldBe([NullableCatalog, NotNullExpression]);

    // Without the answer of "triggers" a BEFORE row trigger may have changed the value.
    [Fact]
    public void Run_WrittenValuesThatDoNotStand_IsNotProved() =>
        AnswersOf(RunOf(Written, stand: false)).ShouldBe([NullableCatalog, NotNullExpression]);

    [Fact]
    public void Run_StatementThatMayNameOldOrNew_IsNotProved() =>
        AnswersOf(RunOf(Written, oldAndNew: true))[0].ShouldBe(NullableCatalog);

    // ON CONFLICT DO UPDATE may return a row the statement did not write.
    [Fact]
    public void Run_OnConflictDoUpdate_IsNotProved() =>
        AnswersOf(RunOf(Written with { ConflictResolution = PlanConflicts.Update }))[0].ShouldBe(NullableCatalog);

    [Fact]
    public void Run_OnConflictDoNothing_IsProved() =>
        AnswersOf(RunOf(Written with { ConflictResolution = "NOTHING" }))[0].ShouldBe(NotNullExpression);

    [Fact]
    public void Run_UpdateOfTheTarget_IsNotProved() =>
        AnswersOf(RunOf(Written with { Operation = "Update" }))[0].ShouldBe(NullableCatalog);

    // The column is of another table than the one the plan writes.
    [Fact]
    public void Run_OriginThatIsNotTheTarget_IsNotProved() =>
        AnswersOf(RunOf(Insert("a", ["opt", "1"], "1", "'x'::integer")))[0].ShouldBe(NullableCatalog);

    [Fact]
    public void Run_OriginInAnotherSchema_IsNotProved() =>
        AnswersOf(RunOf(Written with { Schema = "other" }))[0].ShouldBe(NullableCatalog);

    // The RETURNING entry is another column, returned under this column's origin by no statement a server plans, or the
    // alias of another relation.
    [Fact]
    public void Run_EntryOfAnotherColumn_IsNotProved() =>
        AnswersOf(RunOf(Written with { Output = ["id", "1"] }))[0].ShouldBe(NullableCatalog);

    [Fact]
    public void Run_EntryOfAnotherAlias_IsNotProved() =>
        AnswersOf(RunOf(Written with { Output = ["other.opt", "1"] }))[0].ShouldBe(NullableCatalog);

    [Fact]
    public void Run_EntryOfTheTargetsAlias_IsProved() =>
        AnswersOf(RunOf(Written with { Output = ["t.opt", "1"] }))[0].ShouldBe(NotNullExpression);

    // A generated column is computed after the child's value.
    [Fact]
    public void Run_GeneratedColumn_IsNotProved()
    {
        var generated = Table with
        {
            Columns = [Table.Columns[0], PostgresTables.Attribute(2, "opt", PostgresRows.Int4.Oid, generated: "s")],
        };

        AnswersOf(RunOf(Written, table: generated))[0].ShouldBe(NullableCatalog);
    }

    // A foreign partition's server, an inheritance child and an INSTEAD rule each decide the row returned.
    [Theory]
    [InlineData("p", false, true, false)]
    [InlineData("r", true, false, false)]
    [InlineData("r", false, false, true)]
    public void Run_TableThatDoesNotKeepItsRow_IsNotProved(
        string relkind,
        bool hasSubclass,
        bool foreignPartition,
        bool insteadRule
    )
    {
        var table = Table with
        {
            Relkind = relkind,
            Relhassubclass = hasSubclass,
            HasForeignPartition = foreignPartition,
            HasInsteadRule = insteadRule,
        };

        ColumnNullability.KeepsNotNull(table).ShouldBeFalse();
        AnswersOf(RunOf(Written, table: table))[0].ShouldBe(NullableCatalog);
    }

    // The child of an INSERT is one node: a plan of two children is no plan of that shape.
    [Fact]
    public void Run_TwoChildren_IsNotProved() =>
        AnswersOf(RunOf(Written with { Plans = [.. Written.Plans, .. Written.Plans] }))[0].ShouldBe(NullableCatalog);

    // A column whose number is past what the child prints has no value to read.
    [Fact]
    public void Run_ColumnPastTheChildsValues_IsNotProved() =>
        AnswersOf(RunOf(Insert("t", ["opt", "1"], "1")))[0].ShouldBe(NullableCatalog);

    [Fact]
    public void InsertTargetOf_RootInsert_IsItsTable() =>
        NullabilityWalk.InsertTargetOf(Written).ShouldBe(new PostgresRelationName("public", "t"));

    [Fact]
    public void InsertTargetOf_NoModifyTable_IsNull() =>
        NullabilityWalk.InsertTargetOf(Scan("t", "t.id")).ShouldBeNull();
}
