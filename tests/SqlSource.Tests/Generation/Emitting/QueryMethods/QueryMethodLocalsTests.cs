using Shouldly;
using SqlSource.Generation.Emitting.QueryMethods;
using SqlSource.Generation.Methods;
using Xunit;

namespace SqlSource.Tests.Generation.Emitting.QueryMethods;

public sealed class QueryMethodLocalsTests
{
    [Fact]
    public void ConnectionAndTransaction_OfTheLocals_AreTheArguments()
    {
        var locals = new QueryMethodLocals(
            new MethodArguments(input: null, tokens: [], streams: false),
            input: null,
            output: null,
            streams: false,
            ExecutionCapabilities.None
        );

        locals.Connection.ShouldBe("connection");
        locals.Transaction.ShouldBe("transaction");
    }
}
