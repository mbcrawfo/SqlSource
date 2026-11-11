using System;
using System.Text;
using Shouldly;
using SqlSource.Generation.Emitting.QueryMethods;
using SqlSource.Generation.Methods;
using Xunit;

namespace SqlSource.Tests.Generation.Emitting.QueryMethods;

public sealed class CallSpellingTests
{
    private static CallSpelling Async => CallSpelling.For(MethodSynchrony.Asynchronous);

    private static CallSpelling Sync => CallSpelling.For(MethodSynchrony.Synchronous);

    [Fact]
    public void For_EachSynchrony_IsItsSpelling()
    {
        Async.ShouldBeSameAs(AsyncCallSpelling.Instance);
        Sync.ShouldBeSameAs(SyncCallSpelling.Instance);
    }

    [Fact]
    public void For_ValueOfNoMember_Throws() =>
        Should.Throw<UnexpectedEnumValueException>(() => CallSpelling.For((MethodSynchrony)2));

    [Theory]
    [InlineData(nameof(DriverCall.Open), "await c.OpenAsync(", "c.Open(")]
    [InlineData(nameof(DriverCall.Close), "await c.CloseAsync(", "c.Close(")]
    [InlineData(nameof(DriverCall.OpenConnection), "await c.OpenConnectionAsync(", "c.OpenConnection(")]
    [InlineData(nameof(DriverCall.ExecuteReader), "await c.ExecuteReaderAsync(", "c.ExecuteReader(")]
    [InlineData(nameof(DriverCall.ExecuteNonQuery), "await c.ExecuteNonQueryAsync(", "c.ExecuteNonQuery(")]
    [InlineData(nameof(DriverCall.ExecuteScalar), "await c.ExecuteScalarAsync(", "c.ExecuteScalar(")]
    [InlineData(nameof(DriverCall.Read), "await c.ReadAsync(", "c.Read(")]
    [InlineData(nameof(DriverCall.NextResult), "await c.NextResultAsync(", "c.NextResult(")]
    [InlineData(nameof(DriverCall.Dispose), "await c.DisposeAsync(", "c.Dispose(")]
    [InlineData(nameof(DriverCall.BeginTransaction), "await c.BeginTransactionAsync(", "c.BeginTransaction(")]
    [InlineData(nameof(DriverCall.Commit), "await c.CommitAsync(", "c.Commit(")]
    [InlineData(nameof(DriverCall.Rollback), "await c.RollbackAsync(", "c.Rollback(")]
    public void AppendCallStart_EachCall_IsTheCallOfEachSpelling(string callName, string awaited, string synchronous)
    {
        var call = Enum.Parse<DriverCall>(callName);

        Async.AppendCallStart(new StringBuilder(), "c", call).ToString().ShouldBe(awaited);
        Sync.AppendCallStart(new StringBuilder(), "c", call).ToString().ShouldBe(synchronous);
    }

    [Theory]
    [InlineData(nameof(DriverCall.Open), ".OpenAsync(", ".Open(")]
    [InlineData(nameof(DriverCall.OpenConnection), ".OpenConnectionAsync(", ".OpenConnection(")]
    [InlineData(nameof(DriverCall.Dispose), ".DisposeAsync(", ".Dispose(")]
    public void AppendCallMember_Call_IsTheMemberAloneOfEachSpelling(
        string callName,
        string awaited,
        string synchronous
    )
    {
        var call = Enum.Parse<DriverCall>(callName);

        Async.AppendCallMember(new StringBuilder("t"), call).ToString().ShouldBe("t" + awaited);
        Sync.AppendCallMember(new StringBuilder("t"), call).ToString().ShouldBe("t" + synchronous);
    }

    // The start of a call is the await, the target and the member, so the member alone follows a target written into
    // the builder, as a data source's expression is.
    [Fact]
    public void AppendCallStart_EveryCall_IsTheAwaitTheTargetAndTheMember()
    {
        foreach (var spelling in new[] { Async, Sync })
        {
            foreach (var call in Enum.GetValues<DriverCall>())
            {
                var expected = spelling.AppendCallMember(spelling.AppendAwait(new StringBuilder()).Append("(c)"), call);

                spelling.AppendCallStart(new StringBuilder(), "(c)", call).ToString().ShouldBe(expected.ToString());
            }
        }
    }

    [Fact]
    public void AppendCallStart_EveryCall_HasAnAsynchronousAndASynchronousSpelling()
    {
        foreach (var call in Enum.GetValues<DriverCall>())
        {
            var sync = Sync.AppendCallStart(new StringBuilder(), "c", call).ToString();

            Async.AppendCallStart(new StringBuilder(), "c", call).ToString().ShouldBe("await " + sync[..^1] + "Async(");
        }
    }

    [Fact]
    public void AppendCallMember_ValueOfNoMember_Throws()
    {
        _ = Should.Throw<UnexpectedEnumValueException>(() =>
            Async.AppendCallMember(new StringBuilder(), (DriverCall)12)
        );
        _ = Should.Throw<UnexpectedEnumValueException>(() =>
            Sync.AppendCallMember(new StringBuilder(), (DriverCall)12)
        );
    }

    [Fact]
    public void AppendToken_AsTheOnlyArgument_IsTheTokenOrNothing()
    {
        Async.AppendToken(new StringBuilder(), "token", afterArgument: false).ToString().ShouldBe("token");
        Sync.AppendToken(new StringBuilder(), "token", afterArgument: false).ToString().ShouldBeEmpty();
    }

    [Fact]
    public void AppendToken_AfterAnArgument_IsTheTokenAfterACommaOrNothing()
    {
        Async.AppendToken(new StringBuilder(), "token", afterArgument: true).ToString().ShouldBe(", token");
        Sync.AppendToken(new StringBuilder(), "token", afterArgument: true).ToString().ShouldBeEmpty();
    }

    [Fact]
    public void AppendCallEnd_OfEachSpelling_ClosesTheCall()
    {
        Async.AppendCallEnd(new StringBuilder()).ToString().ShouldBe(").ConfigureAwait(false)");
        Sync.AppendCallEnd(new StringBuilder()).ToString().ShouldBe(")");
    }

    [Fact]
    public void WholeCall_WithItsToken_IsTheAwaitedCallOrTheCall()
    {
        Spell(Async).ShouldBe("await reader.ReadAsync(token).ConfigureAwait(false)");
        Spell(Sync).ShouldBe("reader.Read()");

        static string Spell(CallSpelling spelling)
        {
            var builder = new StringBuilder();
            _ = spelling.AppendCallStart(builder, "reader", DriverCall.Read);
            _ = spelling.AppendToken(builder, "token", afterArgument: false);
            return spelling.AppendCallEnd(builder).ToString();
        }
    }

    [Fact]
    public void AppendAwait_OfEachSpelling_IsAwaitOrNothing()
    {
        Async.AppendAwait(new StringBuilder()).ToString().ShouldBe("await ");
        Sync.AppendAwait(new StringBuilder()).ToString().ShouldBeEmpty();
    }

    [Fact]
    public void AppendUsing_Asynchronous_IsAnAwaitUsingThatConfiguresTheAwait() =>
        Async
            .AppendUsing(new StringBuilder(), "    ", "commandScope", "command")
            .ToString()
            .ShouldBe(
                "    await using var commandScope = "
                    + "global::System.Threading.Tasks.TaskAsyncEnumerableExtensions.ConfigureAwait(command, false);\n"
            );

    [Fact]
    public void AppendUsing_Synchronous_IsAUsing() =>
        Sync.AppendUsing(new StringBuilder(), "    ", "commandScope", "command")
            .ToString()
            .ShouldBe("    using var commandScope = command;\n");

    [Fact]
    public void AppendReturnType_WithAResult_IsTheTaskOfItOrIt()
    {
        Async
            .AppendReturnType(new StringBuilder(), "int")
            .ToString()
            .ShouldBe(" async global::System.Threading.Tasks.Task<int>");
        Sync.AppendReturnType(new StringBuilder(), "int").ToString().ShouldBe(" int");
    }

    [Fact]
    public void AppendReturnType_WithoutAResult_IsTheTaskOrVoid()
    {
        Async
            .AppendReturnType(new StringBuilder(), null)
            .ToString()
            .ShouldBe(" async global::System.Threading.Tasks.Task");
        Sync.AppendReturnType(new StringBuilder(), null).ToString().ShouldBe(" void");
    }

    [Fact]
    public void AppendInterfaceReturnType_WithAResult_IsTheTaskOfItOrItWithoutAsync()
    {
        Async
            .AppendInterfaceReturnType(new StringBuilder(), "int")
            .ToString()
            .ShouldBe("global::System.Threading.Tasks.Task<int>");
        Sync.AppendInterfaceReturnType(new StringBuilder(), "int").ToString().ShouldBe("int");
    }

    [Fact]
    public void AppendInterfaceReturnType_WithoutAResult_IsTheTaskOrVoid()
    {
        Async
            .AppendInterfaceReturnType(new StringBuilder(), null)
            .ToString()
            .ShouldBe("global::System.Threading.Tasks.Task");
        Sync.AppendInterfaceReturnType(new StringBuilder(), null).ToString().ShouldBe("void");
    }

    [Fact]
    public void IteratorModifiersOf_CapturingNothing_IsStaticAsync() =>
        AsyncCallSpelling.IteratorModifiersOf(captures: false).ShouldBe("static async ");

    [Fact]
    public void IteratorModifiersOf_Capturing_IsAsyncAlone() =>
        AsyncCallSpelling.IteratorModifiersOf(captures: true).ShouldBe("async ");
}
