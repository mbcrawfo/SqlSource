using System.Text;

namespace SqlSource.Generation.Emitting.QueryMethods;

/// <summary>
/// The spelling of a synchronous method: each call of the driver in its synchronous form, without a token,
/// <c>default</c> for a connection source's token, a <c>using</c> for each disposal scope, and the result returned as
/// it is, or <c>void</c>.  A synchronous method never streams, so it has no iterator.
/// </summary>
internal sealed class SyncCallSpelling : CallSpelling
{
    /// <summary>The one instance.</summary>
    public static readonly SyncCallSpelling Instance = new();

    private SyncCallSpelling() { }

    /// <inheritdoc />
    public override StringBuilder AppendCallMember(StringBuilder builder, DriverCall call) =>
        builder.Append('.').Append(MemberOf(call)).Append('(');

    /// <inheritdoc />
    public override StringBuilder AppendToken(StringBuilder builder, string token, bool afterArgument) => builder;

    /// <inheritdoc />
    public override StringBuilder AppendSourceToken(StringBuilder builder, string token) => builder.Append("default");

    /// <inheritdoc />
    public override StringBuilder AppendCallEnd(StringBuilder builder) => builder.Append(')');

    /// <inheritdoc />
    public override StringBuilder AppendAwait(StringBuilder builder) => builder;

    /// <inheritdoc />
    public override StringBuilder AppendUsing(StringBuilder builder, string indent, string scope, string disposable) =>
        builder.Append(indent).Append("using var ").Append(scope).Append(" = ").Append(disposable).Append(";\n");

    /// <inheritdoc />
    public override StringBuilder AppendReturnType(StringBuilder builder, string? resultType) =>
        AppendInterfaceReturnType(builder.Append(' '), resultType);

    /// <inheritdoc />
    public override StringBuilder AppendInterfaceReturnType(StringBuilder builder, string? resultType) =>
        builder.Append(resultType ?? "void");

    private static string MemberOf(DriverCall call) =>
        call switch
        {
            DriverCall.Open => "Open",
            DriverCall.Close => "Close",
            DriverCall.OpenConnection => "OpenConnection",
            DriverCall.ExecuteReader => "ExecuteReader",
            DriverCall.ExecuteNonQuery => "ExecuteNonQuery",
            DriverCall.ExecuteScalar => "ExecuteScalar",
            DriverCall.Read => "Read",
            DriverCall.NextResult => "NextResult",
            DriverCall.Dispose => "Dispose",
            DriverCall.BeginTransaction => "BeginTransaction",
            DriverCall.Commit => "Commit",
            DriverCall.Rollback => "Rollback",
            _ => throw UnexpectedEnumValueException.Of(call),
        };
}
