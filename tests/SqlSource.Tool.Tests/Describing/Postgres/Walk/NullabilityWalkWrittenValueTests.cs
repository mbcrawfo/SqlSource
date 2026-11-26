using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Columns;
using SqlSource.Tool.Describing.Postgres.Statements;
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
        OldAndNewNames? oldAndNew = null,
        PostgresTableRow? table = null,
        int attnum = 2,
        bool partitionsKeep = true
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
                oldAndNew ?? OldAndNewNames.None,
                true,
                tables,
                [BuiltInTypes.Int4, BuiltInTypes.Int4],
                WrittenValuesStand: stand,
                PartitionsKeepWrittenValues: partitionsKeep
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
        AnswersOf(RunOf(Written, oldAndNew: OldAndNewNames.Unknown))[0].ShouldBe(NullableCatalog);

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

    // Before 16 a partition's generated column may replace the value, and a plain table has no partition to.
    [Theory]
    [InlineData("p", true, true)]
    [InlineData("p", false, false)]
    [InlineData("r", false, true)]
    public void Run_ValueWrittenByTableKind_IsProvedWherePartitionsKeepIt(string relkind, bool keep, bool proved) =>
        AnswersOf(RunOf(Written, table: Table with { Relkind = relkind }, partitionsKeep: keep))[0]
            .ShouldBe(proved ? NotNullExpression : NullableCatalog);

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

    // A number below 1 places no value of the child: the walk leaves the column as it was built and does not throw.
    [Fact]
    public void Run_ColumnBeforeTheChildsValues_IsNotProved()
    {
        var (columns, origins) = Built(false, Optional, Expression);
        origins[0] = new ColumnOrigin(Table, PostgresTables.Attribute(-1, "opt", PostgresRows.Int4.Oid));

        var outcome = NullabilityWalk.Run(
            new PlanInstancesCache(Written, null),
            columns,
            origins,
            new WalkScope(
                false,
                OldAndNewNames.None,
                true,
                PostgresTables.Of(Table),
                [BuiltInTypes.Int4, BuiltInTypes.Int4],
                WrittenValuesStand: true
            )
        );

        AnswersOf(outcome)[0].ShouldBe(NullableCatalog);
    }

    // Over a join a RETURNING list prints bare names that map to no instance, so only the name tells the column.
    [Theory]
    [InlineData("opt", true)]
    [InlineData("id", false)]
    public void Run_BareEntryOverAJoin_IsProvedOnlyForTheColumnsOwnName(string entry, bool proved)
    {
        var join = Join("Left", Scan("a", "a.id"), Scan("b", "b.id")) with { Output = ["1", "'x'::integer"] };
        var root = Written with { Plans = [join with { ParentRelationship = Outer }], Output = [entry, "1"] };

        AnswersOf(RunOf(root))[0].ShouldBe(proved ? NotNullExpression : NullableCatalog);
    }

    // The catalog already answers a NOT NULL column, and keeps its source.
    [Fact]
    public void Run_NotNullColumn_KeepsTheCatalog() =>
        AnswersOf(RunOf(Written with { Output = ["id", "1"] }, attnum: 1))[0].ShouldBe(NotNullCatalog);

    [Fact]
    public void Run_EntryThatIsNoReference_IsNotProved() =>
        AnswersOf(RunOf(Written with { Output = ["(opt + 1)", "1"] }))[0].ShouldBe(NullableCatalog);

    [Fact]
    public void InsertTargetOf_RootInsert_IsItsTable() =>
        NullabilityWalk.InsertTargetOf(Written).ShouldBe(new PostgresRelationName("public", "t"));

    [Fact]
    public void InsertTargetOf_NoModifyTable_IsNull() =>
        NullabilityWalk.InsertTargetOf(Scan("t", "t.id")).ShouldBeNull();
}
