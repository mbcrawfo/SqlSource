using System;
using System.Globalization;
using System.Text;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public sealed class SqlFileParserAllocationTests
{
    // The generator parses a file again each time it is edited in the IDE, so what a parse allocates is tracked here.
    // A parse of this file allocated 9.7 bytes for each character of input when the budget was set, the same in Debug,
    // in Release and under coverage, and allocates 11.4 since a query carries its parameters and its tokens.  The
    // budget leaves room for differences between runtimes, not for a regression: lower it when the parser improves,
    // and do not raise it to make a change pass.
    private const double BudgetInBytesPerCharacter = 12;

    // A query that keeps its comments has two forms of its SQL, each built and scanned for tokens, so its parse costs
    // about twice a plain one.  A parse of this file with a preamble that keeps comments allocated 20.3 bytes for
    // each character when this budget was set.  It is a budget of its own, with the same room and the same rule.
    private const double BudgetInBytesPerCharacterWithComments = 24;

    // A query with a marker of its own, other than -- name: and -- summary:, which the parser reads itself, has a scope
    // of its own, which a parse of this file, whose queries have none, does not make.  A -- param: on each query added
    // 712 bytes a query to the parse when this budget was set, the same in Debug, in Release and under coverage: its
    // line, its scope and its declaration.  It is a budget of its own, with the same room and the same rule.
    private const double BudgetInBytesPerQueryForAParam = 750;

    private const int Queries = 50;

    private const int Pairs = 5;

    [Theory]
    [InlineData(nameof(SqlDialect.Ansi), false, false)]
    [InlineData(nameof(SqlDialect.MySql), false, false)]
    [InlineData(nameof(SqlDialect.Oracle), false, false)]
    // Each query has a parameter, so each is searched for a declared variable.
    [InlineData(nameof(SqlDialect.SqlServer), false, false)]
    // The file names its own dialect, so its header is read before the rest.
    [InlineData(nameof(SqlDialect.Ansi), true, false)]
    // The preamble keeps comments, so each query is built in both forms.
    [InlineData(nameof(SqlDialect.Ansi), false, true)]
    public void Parse_TypicalFile_AllocatesWithinItsBudget(string dialectName, bool hasDialect, bool keepsComments)
    {
        var dialect = Enum.Parse<SqlDialect>(dialectName);
        var text = CreateFile(hasDialect, keepsComments, hasParams: false);

        (AllocatedByAParse(text, dialect) / text.Length).ShouldBeLessThan(
            keepsComments ? BudgetInBytesPerCharacterWithComments : BudgetInBytesPerCharacter
        );
    }

    [Fact]
    public void Parse_QueriesWithAParamEach_AllocateWithinTheirBudgetForTheParam()
    {
        var plain = CreateFile(hasDialect: false, keepsComments: false, hasParams: false);
        var withParams = CreateFile(hasDialect: false, keepsComments: false, hasParams: true);

        (AddedByAParse(withParams, plain, SqlDialect.Ansi) / Queries).ShouldBeLessThan(BudgetInBytesPerQueryForAParam);
    }

    // The bytes that one parse of the file allocates, as an average over a number of parses.
    private static double AllocatedByAParse(string text, SqlDialect dialect)
    {
        CheckParses(text, dialect);

        return PooledAllocations.Of(() => SqlFileParser.Parse(text, "Queries.sql", dialect))
            / (double)PooledAllocations.Iterations;
    }

    // The bytes that one parse of the file allocates beyond one of the baseline, the median of a few pairs of measures.
    // The runtime replaces the parser's code as it runs, in steps that move what a parse allocates down and, in
    // Release, partly back up, so two measures either side of a step differ by more or less than the files do.  Each
    // step falls in one pair, as a collection that empties the shared pool does, and the median leaves those
    // pairs out.
    private static double AddedByAParse(string text, string baseline, SqlDialect dialect)
    {
        CheckParses(text, dialect);
        CheckParses(baseline, dialect);

        // The first parses pay for one-off work: JIT compilation, static initialisers and the shared buffer pool.
        _ = AllocatedByParses(text, dialect);
        _ = AllocatedByParses(baseline, dialect);

        var differences = new long[Pairs];
        for (var pair = 0; pair < Pairs; pair++)
        {
            var allocated = AllocatedByParses(text, dialect);
            differences[pair] = allocated - AllocatedByParses(baseline, dialect);
        }

        Array.Sort(differences);
        return differences[Pairs / 2] / (double)PooledAllocations.Iterations;
    }

    private static long AllocatedByParses(string text, SqlDialect dialect)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < PooledAllocations.Iterations; iteration++)
        {
            _ = SqlFileParser.Parse(text, "Queries.sql", dialect);
        }

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static void CheckParses(string text, SqlDialect dialect)
    {
        var parsed = SqlFileParser.Parse(text, "Queries.sql", dialect);
        parsed.Blocks.Count.ShouldBe(Queries);
        parsed.Errors.ShouldBeEmpty();
    }

    // A file like the ones the generator is written for: a preamble, and queries that mix line comments, block
    // comments, string literals, tokens and blank lines.  With params, each query declares its parameter in a marker.
    private static string CreateFile(bool hasDialect, bool keepsComments, bool hasParams)
    {
        var file = new StringBuilder("-- Copyright (c) Example\n")
            .Append(hasDialect ? "-- dialect: postgres\n" : string.Empty)
            .Append(keepsComments ? "-- generator: keep-comments\n" : string.Empty)
            .Append('\n');
        for (var query = 0; query < Queries; query++)
        {
            var number = query.ToString(CultureInfo.InvariantCulture);
            _ = file.Append("-- name: Query")
                .Append(number)
                .Append("\n-- summary: Loads the rows for report ")
                .Append(number)
                .Append(".\n")
                .Append(hasParams ? "-- param: @from timestamptz\n" : string.Empty)
                .Append("SELECT u.id, u.name, u.email, /* inline note */ o.total -- trailing note\n")
                .Append("FROM {{schema}}.users AS u\n")
                .Append("    INNER JOIN {{schema}}.orders AS o ON o.user_id = u.id -- join\n")
                .Append("    /* a block comment\n       over two lines */\n")
                .Append("WHERE u.status = 'active' AND u.note <> 'it''s -- fine'\n")
                .Append("    AND o.created_at >= @from\n\n")
                .Append("ORDER BY {{orderBy}}, u.id;\n\n");
        }

        return file.ToString();
    }
}
