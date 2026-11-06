using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Parsing.Tokens;
using Xunit;
using static SqlSource.Tests.Parsing.ParseOf;

namespace SqlSource.Tests.Parsing.Tokens;

// Each use of a list is a segment of its own, in both forms of a query's SQL, with its text as written.
public sealed class SqlListSegmentsTests
{
    private const int Iterations = 20;

    [Fact]
    public void Split_ListUse_IsASegmentOfItsOwn() =>
        Blocks("-- name: Q\nWHERE id IN @ids;\n")
            .ShouldHaveSingleItem()
            .Segments.ShouldBe([Literal("WHERE id IN "), List("@ids"), Literal(";")]);

    [Fact]
    public void Split_NullTestAndListUse() =>
        Blocks("-- name: Q\nWHERE (@ids IS NULL OR id NOT IN @IDS)\n")
            .ShouldHaveSingleItem()
            .Segments.ShouldBe([
                Literal("WHERE ("),
                ListNullTest("@ids"),
                Literal(" IS NULL OR id NOT IN "),
                List("@IDS"),
                Literal(")"),
            ]);

    // The test for NULL of a parameter that is no list stays in its literal, though the query has a list.
    [Fact]
    public void Split_NullTestOfAValueBesideAList_StaysInItsLiteral() =>
        Blocks("-- name: Q\nWHERE (@name IS NULL OR name = @name) AND id IN @ids\n")
            .ShouldHaveSingleItem()
            .Segments.ShouldBe([Literal("WHERE (@name IS NULL OR name = @name) AND id IN "), List("@ids")]);

    // A use at either end of the SQL leaves no empty literal, and two uses side by side none between them.
    [Fact]
    public void Split_UseAtAnEdge_AddsNoEmptyLiteral() =>
        Blocks("-- name: Q\n@ids IS NULL OR a IN @ids\n")
            .ShouldHaveSingleItem()
            .Segments.ShouldBe([ListNullTest("@ids"), Literal(" IS NULL OR a IN "), List("@ids")]);

    // A value that is no list stays in its literal, and so does a parameter in a token's default: the walk steps over
    // each token to the end of its occurrence.
    [Fact]
    public void Split_WithTokens_KeepsTokenSegments() =>
        Blocks("-- name: Q\nSELECT id FROM t WHERE a = @x AND {{filter:b = @x}} AND id IN @ids ORDER BY {{order:id}}\n")
            .ShouldHaveSingleItem()
            .Segments.ShouldBe([
                Literal("SELECT id FROM t WHERE a = @x AND "),
                Token("filter"),
                Literal(" AND id IN "),
                List("@ids"),
                Literal(" ORDER BY "),
                Token("order"),
            ]);

    // The kept form has its own offsets, and its uses are found at them.
    [Fact]
    public void Split_KeptForm_SplitsTheSameUses()
    {
        var block = Blocks(
                "-- name: Q\n-- generator: keep-comments\nSELECT id FROM t -- rows\nWHERE id IN /* ids */ @ids;\n"
            )
            .ShouldHaveSingleItem();

        block
            .KeptSegments.ShouldNotBeNull()
            .ShouldBe([Literal("SELECT id FROM t -- rows\nWHERE id IN /* ids */ "), List("@ids"), Literal(";")]);
        block.Segments.ShouldBe([Literal("SELECT id FROM t\nWHERE id IN   "), List("@ids"), Literal(";")]);
    }

    // A comment can hold a token in the kept form alone, so the kept form has a token that the form without comments
    // lacks, and the walk steps over it to the right offset.
    [Fact]
    public void Split_KeptFormWithATokenInAComment_SplitsAfterTheToken()
    {
        var block = Blocks("-- name: Q\n-- generator: keep-comments\nSELECT /* {{note:}} */ 1 WHERE id IN @ids;\n")
            .ShouldHaveSingleItem();

        block
            .KeptSegments.ShouldNotBeNull()
            .ShouldBe([
                Literal("SELECT /* "),
                Token("note"),
                Literal(" */ 1 WHERE id IN "),
                List("@ids"),
                Literal(";"),
            ]);
    }

    // Without a list nothing is split, and the segments come back as they were, with nothing allocated.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Split_NoList_ReturnsTheSameInstance(bool hasUses)
    {
        const string Sql = "WHERE id IN @v;";
        var segments = new EquatableArray<SqlSegment>([Literal(Sql)]);
        var sql = new SqlBlockText(Sql, [(0, 0)], [new TextSpan(12, 2)]);
        var uses = hasUses ? [SqlParameterUse.List] : ImmutableArray<SqlParameterUse>.Empty;
        var parameters = new EquatableArray<SqlQueryParameter>([new SqlQueryParameter("other", null, null, true)]);

        var split = SqlListSegments.Split(segments, EquatableArray<SqlTokenOccurrence>.Empty, sql, uses, parameters);

        split.ShouldBe(segments);
        split[0].ShouldBeSameAs(segments[0]);

        // The first calls pay for one-off work: JIT compilation and static initialisers.
        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            _ = SqlListSegments.Split(segments, EquatableArray<SqlTokenOccurrence>.Empty, sql, uses, parameters);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            _ = SqlListSegments.Split(segments, EquatableArray<SqlTokenOccurrence>.Empty, sql, uses, parameters);
        }

        (GC.GetAllocatedBytesForCurrentThread() - before).ShouldBe(0);
    }

    // A query without the word IN has no list, and its one literal is the parse's own.
    [Fact]
    public void Split_QueryWithoutAList_IsOneLiteral() =>
        Blocks("-- name: Q\nSELECT 1 WHERE a = @a OR @a IS NULL;\n")
            .ShouldHaveSingleItem()
            .Segments.ShouldBe([Literal("SELECT 1 WHERE a = @a OR @a IS NULL;")]);

    private static SqlSegment Literal(string text) => new(SqlSegmentKind.Literal, text);

    private static SqlSegment Token(string name) => new(SqlSegmentKind.Token, name);

    private static SqlSegment List(string text) => new(SqlSegmentKind.List, text);

    private static SqlSegment ListNullTest(string text) => new(SqlSegmentKind.ListNullTest, text);
}
