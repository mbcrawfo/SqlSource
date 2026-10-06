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
    // in Release and under coverage.  The budget leaves room for differences between runtimes, not for a regression:
    // lower it when the parser improves, and do not raise it to make a change pass.
    private const double BudgetInBytesPerCharacter = 12;

    private const int Queries = 50;

    [Fact]
    public void Parse_TypicalFile_AllocatesWithinItsBudget()
    {
        const int Iterations = 20;
        var text = CreateFile();
        SqlFileParser.Parse(text, "Queries.sql").Blocks.Count.ShouldBe(Queries);

        // The first parses pay for one-off work: JIT compilation, static initialisers and the shared buffer pool.
        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            _ = SqlFileParser.Parse(text, "Queries.sql");
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            _ = SqlFileParser.Parse(text, "Queries.sql");
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        (allocated / (double)(Iterations * text.Length)).ShouldBeLessThan(BudgetInBytesPerCharacter);
    }

    // A file like the ones the generator is written for: a preamble, and queries that mix line comments, block
    // comments, string literals, tokens and blank lines.
    private static string CreateFile()
    {
        var file = new StringBuilder("-- Copyright (c) Example\n-- SqlSource: token-ignore=raw\n\n");
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
