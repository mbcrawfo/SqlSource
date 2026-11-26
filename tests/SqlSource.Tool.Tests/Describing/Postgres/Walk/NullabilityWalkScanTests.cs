using System.Collections;
using System.Collections.Generic;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Statements;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;
using static SqlSource.Tool.Testing.NullabilityAnswers;
using static SqlSource.Tool.Tests.PostgresPlans;
using static SqlSource.Tool.Tests.PostgresWalkColumns;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk;

// NullabilityWalk over a column of no origin through a scan of another plan.  The plan does not say which entry of the
// scanned plan the column is, so every entry it can be must prove it.
public sealed class NullabilityWalkScanTests
{
    private static PlanNode Subquery(PlanNode child, string alias = "s", params string[] output) =>
        Over("Subquery Scan", child, "Subquery") with
        {
            Alias = alias,
            Output = output,
        };

    // A grouped Aggregate of "o" that prints the entries.
    private static PlanNode Grouped(params string[] entries) =>
        Aggregate("Hashed", true, Scan("o", "o.id", "o.note", "o.parent_id", "o.placed_at"), entries);

    private static PlanNode ScanOf(params string[] entries) =>
        Subquery(Grouped(entries), "s", "s.c") with
        {
            Output = ["s.c"],
        };

    private static (bool? Nullable, string? Source) AnswerOf(
        PlanNode root,
        long type,
        bool noCastAcrossFamilies = false,
        OldAndNewNames? oldAndNew = null
    )
    {
        var (columns, origins) = Built(false, Expression);
        var outcome = NullabilityWalk.Run(
            new PlanInstancesCache(root, null),
            columns,
            origins,
            new WalkScope(
                false,
                oldAndNew ?? OldAndNewNames.None,
                true,
                PostgresTables.Of(TableA, OrdersO),
                [type],
                NoCastAcrossFamilies: noCastAcrossFamilies
            )
        );
        return AnswersOf(outcome)[0];
    }

    [Fact]
    public void Run_EveryEntryProved_IsNotNull() =>
        AnswerOf(ScanOf("o.id", "count(*)"), BuiltInTypes.Int8).ShouldBe(NotNullExpression);

    // Either entry may be the column, and one may be NULL, whatever the casts of the server.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_SiblingOfTheFamilyThatMayBeNull_IsUnknown(bool noCastAcrossFamilies) =>
        AnswerOf(ScanOf("o.parent_id", "count(*)"), BuiltInTypes.Int8, noCastAcrossFamilies).ShouldBe(UnknownNoOrigin);

    // The text sibling is ruled out only when the server holds no cast from it to a number.
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Run_SiblingOfAnotherFamily_IsRuledOutOnlyWithNoCast(bool noCastAcrossFamilies, bool proved) =>
        AnswerOf(ScanOf("o.note", "count(*)"), BuiltInTypes.Int8, noCastAcrossFamilies)
            .ShouldBe(proved ? NotNullExpression : UnknownNoOrigin);

    // An inlined function's text column may be a nullable integer through a cast the plan does not print.
    [Fact]
    public void Run_ColumnOfText_RulesNoEntryOut() =>
        AnswerOf(ScanOf("o.parent_id", "'a'::text"), BuiltInTypes.Text, noCastAcrossFamilies: true)
            .ShouldBe(UnknownNoOrigin);

    // Nothing proves it, so a column that every entry is ruled out of is no proof.
    [Fact]
    public void Run_EveryEntryRuledOut_IsUnknown() =>
        AnswerOf(ScanOf("o.note"), BuiltInTypes.Int8, noCastAcrossFamilies: true).ShouldBe(UnknownNoOrigin);

    // An entry of a family the column's type does not carry from is no proof, however proved.
    [Fact]
    public void Run_ProvedEntryOfATypeThatDoesNotCarry_IsUnknown() =>
        AnswerOf(ScanOf("o.placed_at"), BuiltInTypes.Int8).ShouldBe(UnknownNoOrigin);

    [Fact]
    public void Run_ScanThatAJoinExtends_IsUnknown()
    {
        var plan = Join("Left", Scan("a", "a.id"), Subquery(Grouped("o.id", "count(*)"), "s", "s.c")) with
        {
            Output = ["s.c"],
        };

        AnswerOf(plan, BuiltInTypes.Int8).ShouldBe(UnknownNoOrigin);
    }

    [Fact]
    public void Run_ColumnOfNoKnownType_IsUnknown() => AnswerOf(ScanOf("count(*)"), 0).ShouldBe(UnknownNoOrigin);

    // The old or the new row may print under an alias of the scanned plan.
    [Fact]
    public void Run_StatementThatMayNameOldOrNew_IsUnknown() =>
        AnswerOf(ScanOf("count(*)"), BuiltInTypes.Int8, oldAndNew: OldAndNewNames.Unknown).ShouldBe(UnknownNoOrigin);

    // A scan inside the scan is read the same way, entry by entry.
    [Theory]
    [InlineData("count(*)", true)]
    [InlineData("o.parent_id", false)]
    public void Run_ScanInsideTheScan_IsReadInTurn(string inner, bool proved)
    {
        var plan = Subquery(Subquery(Grouped(inner), "i", "i.x"), "s", "s.c") with { Output = ["s.c"] };
        var nested = plan with { Plans = [plan.Plans[0] with { Output = ["i.x"] }] };

        AnswerOf(nested, BuiltInTypes.Int8).ShouldBe(proved ? NotNullExpression : UnknownNoOrigin);
    }

    // A set operation inside the scan prints its first input, here a proved column, which says nothing of the other.
    [Fact]
    public void Run_SetOperationInsideTheScan_IsUnknown()
    {
        var setOp = Join("Inner", Scan("o", "o.id"), Scan("a", "a.opt")) with
        {
            NodeType = "SetOp",
            JoinType = null,
            Output = ["o.id"],
        };

        AnswerOf(Subquery(setOp, "s", "s.c") with { Output = ["s.c"] }, BuiltInTypes.Int4).ShouldBe(UnknownNoOrigin);
    }

    // Every entry of a scan may be the column, so a chain of scans reads the plan under it once for each path to it
    // unless each answer is kept: 2^7 reads of the deepest plan here, and W^D for a chain of W columns and D scans.
    [Fact]
    public void Run_ColumnOfNoOriginThroughAChainOfScans_ReadsTheDeepestPlanOnce()
    {
        var entries = new CountedEntries("o.id", "count(*)");

        AnswerOf(Chain(Grouped("o.id") with { Output = entries }, 8), BuiltInTypes.Int8).ShouldBe(NotNullExpression);

        entries.Reads.ShouldBe(1);
    }

    // A column with an origin is resolved through the same chain, by every entry it can be.
    [Fact]
    public void Run_ColumnWithAnOriginThroughAChainOfScans_ReadsTheDeepestPlanOnce()
    {
        var entries = new CountedEntries("t.id", "t.opt");

        AnswersOf(Run(Chain(Scan("t") with { Output = entries }, 8), NotNull)).ShouldBe([NotNullCatalog]);

        entries.Reads.ShouldBe(1);
    }

    // Scans of two columns each over the plan given, the root printing one of them.
    private static PlanNode Chain(PlanNode deepest, int levels)
    {
        var plan = deepest;
        for (var level = 0; level < levels; level++)
        {
            plan = Subquery(plan, $"s{level}", $"s{level}.a", $"s{level}.b");
        }

        return plan with
        {
            Output = [$"s{levels - 1}.a"],
        };
    }

    // An Output that counts how many times a reader goes through it.
    private sealed class CountedEntries(params string[] entries) : IReadOnlyList<string>
    {
        public int Reads { get; private set; }

        public int Count => entries.Length;

        public string this[int index] => entries[index];

        public IEnumerator<string> GetEnumerator()
        {
            Reads++;
            return ((IEnumerable<string>)entries).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
