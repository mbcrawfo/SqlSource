using System.Text;

namespace SqlSource.Generation.Emitting.QueryMethods;

/// <summary>
/// Holds every spelling that makes a query's method asynchronous: the awaited call, the asynchronous disposal scope,
/// the task a method returns and the modifiers of an iterator.  It is the one place a synchronous kind of method would
/// change (see "Adding a kind of method" in the generator's architecture page), and it has no synchronous mode.
/// </summary>
internal static class AsyncSpelling
{
    /// <summary>
    /// The modifiers of the local iterator of a streaming method: <c>static async</c>, or <c>async</c> for one that
    /// captures the locals the method counts its lists into before it returns the iterator, or the member or parameter
    /// a repository's method gets its connection from.
    /// </summary>
    internal static string IteratorModifiersOf(bool captures) => captures ? "async " : "static async ";

    // DbCommand and DbDataReader are IAsyncDisposable, whose ConfigureAwait is an extension method.  The generated file
    // imports no namespace, so it is called by its class.
    private const string ConfigureAwaitOfDisposable =
        "global::System.Threading.Tasks.TaskAsyncEnumerableExtensions.ConfigureAwait(";

    /// <summary>
    /// Appends <c>await </c>, which the caller follows with a call and <see cref="AppendAwaitedEnd" />.
    /// </summary>
    public static StringBuilder AppendAwait(StringBuilder builder) => builder.Append("await ");

    /// <summary>
    /// Appends <c>await target.member(</c>, which the caller follows with the arguments and
    /// <see cref="AppendAwaitedEnd" />.
    /// </summary>
    public static StringBuilder AppendAwaitedCall(StringBuilder builder, string target, string member) =>
        builder.Append("await ").Append(target).Append('.').Append(member).Append('(');

    /// <summary>
    /// Appends the <c>)</c> that closes the arguments of an awaited call and the <c>ConfigureAwait</c> that follows it.
    /// </summary>
    public static StringBuilder AppendAwaitedEnd(StringBuilder builder) => builder.Append(").ConfigureAwait(false)");

    /// <summary>
    /// Appends the line that declares an asynchronously disposed local, <c>await using var scope = ...</c>, over the
    /// disposable.
    /// </summary>
    public static StringBuilder AppendUsing(StringBuilder builder, string indent, string scope, string disposable) =>
        builder
            .Append(indent)
            .Append("await using var ")
            .Append(scope)
            .Append(" = ")
            .Append(ConfigureAwaitOfDisposable)
            .Append(disposable)
            .Append(", false);\n");

    /// <summary>
    /// Appends <c> async Task</c>, or <c> async Task&lt;resultType&gt;</c> when the method returns a result.
    /// </summary>
    public static StringBuilder AppendTaskType(StringBuilder builder, string? resultType) =>
        AppendTask(builder.Append(" async "), resultType);

    /// <summary>
    /// Appends <c>Task</c>, or <c>Task&lt;resultType&gt;</c> when the method returns a result, without <c>async</c>:
    /// the type that a member of a repository's interface returns.
    /// </summary>
    public static StringBuilder AppendTask(StringBuilder builder, string? resultType)
    {
        _ = builder.Append("global::System.Threading.Tasks.Task");
        return resultType is null ? builder : builder.Append('<').Append(resultType).Append('>');
    }
}
