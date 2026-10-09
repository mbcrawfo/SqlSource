using System;
using System.Globalization;
using System.Text;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class SqlFileParserAllocationTests
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

    private const int Queries = 50;

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
        const int Iterations = 20;
        var dialect = Enum.Parse<SqlDialect>(dialectName);
        var text = CreateFile(hasDialect, keepsComments);
        SqlFileParser.Parse(text, "Queries.sql", dialect).Blocks.Count.ShouldBe(Queries);

        // The first parses pay for one-off work: JIT compilation, static initialisers and the shared buffer pool.
        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            _ = SqlFileParser.Parse(text, "Queries.sql", dialect);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            _ = SqlFileParser.Parse(text, "Queries.sql", dialect);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        (allocated / (double)(Iterations * text.Length)).ShouldBeLessThan(
            keepsComments ? BudgetInBytesPerCharacterWithComments : BudgetInBytesPerCharacter
        );
    }

    // A file like the ones the generator is written for: a preamble, and queries that mix line comments, block
    // comments, string literals, tokens and blank lines.
    private static string CreateFile(bool hasDialect, bool keepsComments)
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
