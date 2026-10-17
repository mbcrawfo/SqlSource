using Npgsql;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The reflection over the row description of the pinned Npgsql.  What it reads from a server is in
// tests/SqlSource.Tests.Postgres.
public sealed class RowDescriptionsTests
{
    // Fails on a bump of Npgsql that moves the field, the count or the indexer, before any session depends on them.
    [Fact]
    public void Resolve_NpgsqlOfTheTool_FindsTheRowDescription() => Should.NotThrow(RowDescriptions.Resolve);

    // Whatever is wrong, the message says where the describer reads from and which Npgsql it is.
    [Fact]
    public void MessageOf_AProblem_NamesTheFieldAndTheVersionOfNpgsql() =>
        RowDescriptions
            .MessageOf("there is no property RowDescriptionMessage.Count")
            .ShouldBe(
                "there is no property RowDescriptionMessage.Count; the describer reads the type modifiers from "
                    + "NpgsqlDataReader.RowDescription, and Npgsql is "
                    + typeof(NpgsqlDataReader).Assembly.GetName().Version!.ToString(3)
            );
}
