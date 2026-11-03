using System.Text;
using Shouldly;
using SqlSource.Generation.Emitting.QueryMethods;
using Xunit;

namespace SqlSource.Tests.Generation.Emitting.QueryMethods;

public sealed class AsyncSpellingTests
{
    [Fact]
    public void AwaitedCall_WithItsArgumentsAndEnd_IsAnAwaitedCallThatConfiguresTheAwait()
    {
        var builder = new StringBuilder();

        _ = AsyncSpelling.AppendAwaitedCall(builder, "reader", "ReadAsync").Append("token");
        _ = AsyncSpelling.AppendAwaitedEnd(builder);

        builder.ToString().ShouldBe("await reader.ReadAsync(token).ConfigureAwait(false)");
    }

    [Fact]
    public void AwaitedCall_WithNoArguments_IsAnAwaitedCallOfNone()
    {
        var builder = new StringBuilder();

        _ = AsyncSpelling.AppendAwaitedCall(builder, "connection", "CloseAsync");
        _ = AsyncSpelling.AppendAwaitedEnd(builder);

        builder.ToString().ShouldBe("await connection.CloseAsync().ConfigureAwait(false)");
    }

    [Fact]
    public void Using_ForADisposable_IsAnAwaitUsingThatConfiguresTheAwait()
    {
        var builder = new StringBuilder();

        _ = AsyncSpelling.AppendUsing(builder, "    ", "commandScope", "command");

        builder
            .ToString()
            .ShouldBe(
                "    await using var commandScope = "
                    + "global::System.Threading.Tasks.TaskAsyncEnumerableExtensions.ConfigureAwait(command, false);\n"
            );
    }

    [Fact]
    public void TaskType_WithNoResult_IsTheTask()
    {
        var builder = new StringBuilder();

        _ = AsyncSpelling.AppendTaskType(builder, null);

        builder.ToString().ShouldBe(" async global::System.Threading.Tasks.Task");
    }

    [Fact]
    public void TaskType_WithAResult_IsTheTaskOfIt()
    {
        var builder = new StringBuilder();

        _ = AsyncSpelling.AppendTaskType(builder, "int");

        builder.ToString().ShouldBe(" async global::System.Threading.Tasks.Task<int>");
    }

    [Fact]
    public void IteratorModifiers_AreStaticAsync() => AsyncSpelling.IteratorModifiers.ShouldBe("static async ");
}
