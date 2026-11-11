using System.Text;

namespace SqlSource.Generation.Emitting.QueryMethods;

/// <summary>
/// The spelling of an asynchronous method: each call of the driver awaited in its <c>Async</c> form with
/// <c>ConfigureAwait(false)</c> and the method's token, <c>await using</c>, a task returned, and the modifiers of a
/// streaming method's iterator, which only an asynchronous method has.
/// </summary>
internal sealed class AsyncCallSpelling : CallSpelling
{
    /// <summary>The one instance.</summary>
    public static readonly AsyncCallSpelling Instance = new();

    // DbCommand and DbDataReader are IAsyncDisposable, whose ConfigureAwait is an extension method.  The generated file
    // imports no namespace, so it is called by its class.
    private const string ConfigureAwaitOfDisposable =
        "global::System.Threading.Tasks.TaskAsyncEnumerableExtensions.ConfigureAwait(";

    private const string Task = "global::System.Threading.Tasks.Task";

    private AsyncCallSpelling() { }

    /// <summary>
    /// The modifiers of the local iterator of a streaming method: <c>static async</c>, or <c>async</c> for one that
    /// captures the locals the method counts its lists into before it returns the iterator, or the member or parameter
    /// a repository's method gets its connection from.
    /// </summary>
    internal static string IteratorModifiersOf(bool captures) => captures ? "async " : "static async ";

    /// <inheritdoc />
    public override StringBuilder AppendCallMember(StringBuilder builder, DriverCall call) =>
        builder.Append('.').Append(MemberOf(call)).Append('(');

    /// <inheritdoc />
    public override StringBuilder AppendToken(StringBuilder builder, string token, bool afterArgument) =>
        (afterArgument ? builder.Append(", ") : builder).Append(token);

    /// <inheritdoc />
    public override StringBuilder AppendCallEnd(StringBuilder builder) => builder.Append(").ConfigureAwait(false)");

    /// <inheritdoc />
    public override StringBuilder AppendAwait(StringBuilder builder) => builder.Append("await ");

    /// <inheritdoc />
    public override StringBuilder AppendUsing(StringBuilder builder, string indent, string scope, string disposable) =>
        builder
            .Append(indent)
            .Append("await using var ")
            .Append(scope)
            .Append(" = ")
            .Append(ConfigureAwaitOfDisposable)
            .Append(disposable)
            .Append(", false);\n");

    /// <inheritdoc />
    public override StringBuilder AppendReturnType(StringBuilder builder, string? resultType) =>
        AppendInterfaceReturnType(builder.Append(" async "), resultType);

    /// <inheritdoc />
    public override StringBuilder AppendInterfaceReturnType(StringBuilder builder, string? resultType)
    {
        _ = builder.Append(Task);
        return resultType is null ? builder : builder.Append('<').Append(resultType).Append('>');
    }

    private static string MemberOf(DriverCall call) =>
        call switch
        {
            DriverCall.Open => "OpenAsync",
            DriverCall.Close => "CloseAsync",
            DriverCall.OpenConnection => "OpenConnectionAsync",
            DriverCall.ExecuteReader => "ExecuteReaderAsync",
            DriverCall.ExecuteNonQuery => "ExecuteNonQueryAsync",
            DriverCall.ExecuteScalar => "ExecuteScalarAsync",
            DriverCall.Read => "ReadAsync",
            DriverCall.NextResult => "NextResultAsync",
            DriverCall.Dispose => "DisposeAsync",
            DriverCall.BeginTransaction => "BeginTransactionAsync",
            DriverCall.Commit => "CommitAsync",
            DriverCall.Rollback => "RollbackAsync",
            _ => throw UnexpectedEnumValueException.Of(call),
        };
}
