using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;
using static SqlSource.Tool.Testing.NullabilityAnswers;
using static SqlSource.Tool.Tests.PostgresPlans;
using static SqlSource.Tool.Tests.PostgresWalkColumns;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk;

// NullabilityWalk over plans a test writes: the answer of a column of a table, a view or a foreign table, of a query
// with no word of the gate, and the check of a reference against the column's origin.
public sealed class NullabilityWalkTests
{
    [Fact]
    public void Run_ColumnsOfATableThatNothingExtends_TakeTheCatalogsAnswer()
    {
        var outcome = Run(LeftJoin("a.id", "a.opt"), NotNull, Optional);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Walked);
        outcome.Reason.ShouldBeNull();
        AnswersOf(outcome).ShouldBe([NotNullCatalog, NullableCatalog]);
    }

    [Fact]
    public void Run_ColumnsOfATableThatAJoinExtends_AreNullableByTheOuterJoin() =>
        AnswersOf(Run(LeftJoin("b.id", "b.opt"), NotNull, Optional)).ShouldBe([NullableOuterJoin, NullableOuterJoin]);

    [Theory]
    [InlineData("a.id + 1")]
    [InlineData("(a.id)")]
    [InlineData("COALESCE(a.id, b.id)")]
    [InlineData("$1")]
    [InlineData("old.id")]
    [InlineData("id")]
    public void Run_ColumnOfATableWhoseEntryCannotBeMapped_KeepsTheHeuristic(string entry) =>
        AnswersOf(Run(LeftJoin(entry, entry), NotNull, Optional)).ShouldBe([NullableHeuristic, NullableHeuristic]);

    [Fact]
    public void Run_ColumnOfAView_SaysViewUnlessAJoinExtendsIt() =>
        AnswersOf(Run(LeftJoin("a.id", "b.id", "a.id + 1"), OfView, OfView, OfView))
            .ShouldBe([UnknownView, NullableOuterJoin, UnknownView]);

    // The walk's rule over a foreign relkind, through a plain scan, as through a Foreign Scan.
    [Fact]
    public void Run_ColumnOfAForeignTable_SaysForeignTableUnlessAJoinExtendsIt() =>
        AnswersOf(Run(LeftJoin("a.id", "b.id", "a.id + 1"), OfForeign, OfForeign, OfForeign))
            .ShouldBe([UnknownForeignTable, NullableOuterJoin, UnknownForeignTable]);

    // Under a join that null-extends, an expression is answered only where the walk finds the node that computes it:
    // here the scans print no Output, and may compute anything.  A column without an origin is never mapped.
    [Fact]
    public void Run_ColumnWithoutAnOrigin_IsUnknownAndIsNotLookedUp()
    {
        var outcome = Run(LeftJoin("b.id", "count(*)"), Expression, Expression);

        AnswersOf(outcome).ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);
        outcome.Columns.ShouldAllBe(static walked => !walked.Mapped && walked.Reference == null);
        outcome.Columns[0].Output.ShouldBe("b.id");
    }

    // A query without a word of the gate is planned for its expressions alone: a column with an origin keeps the
    // catalog's answer whatever its entry, since no outer join can have touched it.
    [Fact]
    public void Run_UngatedQueryWhoseEntryCannotBeMapped_KeepsTheCatalogsAnswer()
    {
        var plan = Over("Subquery Scan", Scan("t"), "Subquery") with { Alias = "s", Output = ["s.id", "count(*)"] };

        var outcome = RunUngated(plan, NotNull, Expression);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(outcome).ShouldBe([NotNullCatalog, NotNullExpression]);
    }

    [Fact]
    public void Run_UngatedQueryWithAPlanThatCannotBeUsed_KeepsTheCatalogsAnswer()
    {
        var plan = Join("Inner", Scan("a"), Scan("a")) with { Output = ["a.id"] };

        var outcome = RunUngated(plan, NotNull);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Unavailable);
        outcome.Reason.ShouldBe(WalkReasons.DoubledAlias);
        AnswersOf(outcome).ShouldBe([NotNullCatalog]);
    }

    // A plan whose reference names another column than the one the row description gives does not agree with it.  Its
    // expressions are not read, and a gated query's columns with an origin are walked.
    [Theory]
    [InlineData("t.opt")]
    [InlineData("opt")]
    public void Run_ReferenceThatNamesAnotherColumnThanTheOrigin_IsWalkedAndReadsNoExpression(string entry)
    {
        var outcome = Run(Scan("t", entry, "'k'::text"), NotNull, Expression);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Walked);
        outcome.Reason.ShouldBe(WalkReasons.OutputDiffers);
        AnswersOf(outcome).ShouldBe([NotNullCatalog, UnknownNoOrigin]);
    }

    // A query that gives a table's columns aliases, users u(a, b), prints them under the aliases: the check takes it
    // for another statement's plan, and the join's answer stands.
    [Fact]
    public void Run_ReferenceThatNamesAnotherColumnUnderALeftJoin_KeepsTheJoinsAnswer() =>
        AnswersOf(Run(LeftJoin("a.other", "b.other"), NotNull, NotNull)).ShouldBe([NotNullCatalog, NullableOuterJoin]);

    [Fact]
    public void Run_UngatedQueryWhoseReferenceNamesAnotherColumn_IsUnavailableAndKeepsTheCatalogsAnswer()
    {
        var outcome = RunUngated(Scan("t", "t.opt"), NotNull);

        outcome.Reason.ShouldBe(WalkReasons.OutputDiffers);
        AnswersOf(outcome).ShouldBe([NotNullCatalog]);
    }

    // A view's column prints as the column of the table under the view, under that column's name.
    [Fact]
    public void Run_ViewsColumnPrintedUnderTheBaseTablesName_IsWalked()
    {
        var outcome = Run(Scan("t", "t.opt"), OfView);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(outcome).ShouldBe([UnknownView]);
    }

    [Fact]
    public void Run_UnmappedReferenceAndExpression_PassTheCheck() =>
        Run(LeftJoin("x.other", "a.id + 1"), NotNull, NotNull).Plan.ShouldBe(SidecarValues.Plan.Walked);

    // A foreign table's scan is an instance: unknown when nothing extends it, and the join's when one does.
    [Fact]
    public void Run_ColumnOfAForeignScan_IsForeignTableOrTheOuterJoins()
    {
        var foreign = Scan("ft") with { NodeType = "Foreign Scan" };

        AnswersOf(Run(Join("Left", foreign, Scan("b")) with { Output = ["ft.id"] }, OfForeign))
            .ShouldBe([UnknownForeignTable]);
        AnswersOf(Run(Join("Left", Scan("b"), foreign) with { Output = ["ft.id"] }, OfForeign))
            .ShouldBe([NullableOuterJoin]);
    }
}
