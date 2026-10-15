using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The reflection over the row description of the pinned Npgsql.  What it reads from a server is in
// tests/SqlSource.Tests.Postgres.
public class RowDescriptionsTests
{
    // Fails on a bump of Npgsql that moves the field, the count or the indexer, before any session depends on them.
    [Fact]
    public void Resolve_NpgsqlOfTheTool_FindsTheRowDescription() => Should.NotThrow(RowDescriptions.Resolve);
}
