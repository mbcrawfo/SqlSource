using Shouldly;
using SqlSource.Generation.Emitting.QueryMethods;
using SqlSource.Generation.Methods;
using SqlSource.Generation.Repositories;
using Xunit;

namespace SqlSource.Tests.Generation.Emitting.QueryMethods;

public sealed class QueryMethodLocalsTests
{
    [Fact]
    public void ConnectionAndTransaction_OfTheLocals_AreTheArguments()
    {
        var locals = new QueryMethodLocals(
            new MethodArguments(input: null, tokens: [], streams: false, takesConnection: true),
            input: null,
            output: null,
            streams: false,
            ExecutionCapabilities.None,
            source: null
        );

        locals.Connection.ShouldBe("connection");
        locals.Transaction.ShouldBe("transaction");
    }

    // A repository's method gets its connection into a local of its own, which takes no name that its arguments or
    // the parameter its source is reached through have, and it has no transaction.
    [Fact]
    public void Connection_OfARepository_IsALocalThatNoArgumentOrSourceHas()
    {
        var locals = new QueryMethodLocals(
            new MethodArguments(input: null, tokens: [], streams: false, takesConnection: false),
            input: null,
            output: null,
            streams: false,
            ExecutionCapabilities.None,
            TestModels.Source(
                member: CandidateMember.Parameter,
                name: "connection",
                access: "connection",
                leadingName: "connection"
            )
        );

        locals.Connection.ShouldBe("connection1");
        locals.Transaction.ShouldBeEmpty();
    }
}
