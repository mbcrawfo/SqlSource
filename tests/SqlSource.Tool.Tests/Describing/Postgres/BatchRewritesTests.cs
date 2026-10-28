using Npgsql;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The reflection over what the pinned Npgsql sent for a command of a batch.  What it reads after a describe is in
// tests/DatabaseTests/Postgres.
public sealed class BatchRewritesTests
{
    // Fails on a bump of Npgsql that moves the text or the order of the parameters, before any session depends on
    // them.
    [Fact]
    public void Resolve_PinnedNpgsql_FindsTheMembers() => Should.NotThrow(BatchRewrites.Resolve);

    // Whatever is wrong, the message names the member and which Npgsql it is.
    [Fact]
    public void MessageOf_AProblem_NamesTheMemberAndTheVersionOfNpgsql() =>
        BatchRewrites
            .MessageOf("NpgsqlBatchCommand.FinalCommandText", "is no property")
            .ShouldBe(
                "NpgsqlBatchCommand.FinalCommandText is no property; the describer reads what Npgsql sent from it, "
                    + "and Npgsql is "
                    + typeof(NpgsqlBatchCommand).Assembly.GetName().Version!.ToString(3)
            );

    // A command that was never sent has no text.
    [Fact]
    public void Of_CommandNotSent_Throws() =>
        Should
            .Throw<System.InvalidOperationException>(() => BatchRewrites.Of(new NpgsqlBatchCommand("SELECT 1")))
            .Message.ShouldStartWith("NpgsqlBatchCommand.FinalCommandText is null after a describe");
}
