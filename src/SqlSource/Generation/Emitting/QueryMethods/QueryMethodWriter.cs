using System.Text;
using SqlSource.Generation.Methods;
using SqlSource.Settings;

namespace SqlSource.Generation.Emitting.QueryMethods;

/// <summary>
/// Writes the asynchronous method of a query: it opens the connection when it is closed, binds each parameter with
/// the <see cref="ParameterBinder" /> of its file's engine, runs the query and reads its rows as the shape says.  A
/// many query of <see cref="CollectionKind.IAsyncEnumerable" /> checks its arguments and returns a local iterator
/// that does the rest as the loop asks for each row.  It builds the method's <see cref="QueryMethodContext" /> and
/// writes the method in phases, in this order: <see cref="HeaderWriter" />, <see cref="CommandWriter" /> and
/// <see cref="RowsWriter" />.  A new kind of method or a new engine follows <c>docs/architecture/generator.md</c>,
/// "Extending query methods".
/// </summary>
/// <remarks>
/// The arguments, their documentation and their checks come from <see cref="MethodArguments" />.  A token's argument
/// can have any name that <see cref="MethodArguments.IsReserved" /> allows, so every local, and the parameters of the
/// lambda that builds the SQL, take a name no argument has from one <see cref="LocalNames" />, which
/// <see cref="QueryMethodLocals" /> holds.  <see cref="RowReaderWriter" /> writes the reading of each row.  What the
/// engine needs beyond that is a flag of <see cref="ExecutionCapabilities" />, which the phases ask, never the engine,
/// and whose statements <see cref="ExecutionCapabilityWriter" /> writes; only the binder is chosen by the engine.
/// Every framework type is written from <c>global::</c>, an extension method is called as a static method, and
/// <c>string</c>, <c>int</c> and <c>bool</c> are written as keywords.
/// </remarks>
internal static class QueryMethodWriter
{
    /// <summary>
    /// Appends the documentation comment and the method.
    /// </summary>
    /// <param name="builder">The source being built.</param>
    /// <param name="indent">The whitespace that starts each line of the member.</param>
    /// <param name="method">
    /// The query's method.  Its <see cref="QueryMethod.Output" /> is set when its shape reads rows, and its
    /// <see cref="QueryMethod.Host" /> gives its modifiers and whether it extends the connection.
    /// </param>
    public static void Append(StringBuilder builder, string indent, QueryMethod method) =>
        Append(builder, indent, method, ExecutionCapabilityRules.Of(method));

    /// <summary>
    /// Appends the method's member of its type's repository interface: the documentation comment and the signature
    /// that <see cref="Append(StringBuilder, string, QueryMethod)" /> writes, the default of the cancellation token
    /// too, without modifiers, and a semicolon in place of the body.
    /// </summary>
    /// <param name="builder">The source being built.</param>
    /// <param name="indent">The whitespace that starts each line of the member.</param>
    /// <param name="method">A repository's public method on its type.</param>
    public static void AppendInterfaceMember(StringBuilder builder, string indent, QueryMethod method)
    {
        var context = ContextOf(builder, method, ExecutionCapabilityRules.Of(method));
        HeaderWriter.AppendDocumentation(in context, indent);
        HeaderWriter.AppendInterfaceSignature(in context, indent);
    }

    /// <summary>
    /// Appends the documentation comment and the method with the capabilities given, which the tests choose to write
    /// a combination of flags that no engine has yet.
    /// </summary>
    internal static void Append(
        StringBuilder builder,
        string indent,
        QueryMethod method,
        ExecutionCapabilities capabilities
    )
    {
        var context = ContextOf(builder, method, capabilities);
        var streams = context.Streams;
        var bodyIndent = GeneratedText.Deeper(indent);
        var blockIndent = GeneratedText.Deeper(bodyIndent);

        HeaderWriter.AppendDocumentation(in context, indent);
        HeaderWriter.AppendSignature(in context, indent);
        _ = builder.Append(indent).Append("{\n");
        var checksStart = builder.Length;
        HeaderWriter.AppendChecks(in context, bodyIndent);
        if (!streams)
        {
            CommandWriter.AppendBody(in context, bodyIndent);
            ListWriter.AppendHelpers(in context, bodyIndent);
            AppendEnd(builder, indent);
            return;
        }

        // The checks run when the method is called, and the rest when the loop asks for the first row.  The iterator
        // takes every argument by the same name, so the body is written as in any other method, and captures nothing
        // but the locals that the checks count lists into.
        HeaderWriter.AppendIteratorCall(in context, bodyIndent, afterChecks: builder.Length > checksStart);
        CommandWriter.AppendBody(in context, blockIndent);
        AppendEnd(builder, bodyIndent);
        ListWriter.AppendHelpers(in context, bodyIndent);
        AppendEnd(builder, indent);
    }

    internal static void AppendIf(StringBuilder builder, string indent, string condition) =>
        _ = builder.Append(indent).Append("if (").Append(condition).Append(")\n").Append(indent).Append("{\n");

    internal static void AppendEnd(StringBuilder builder, string indent) => _ = builder.Append(indent).Append("}\n");

    // What every part of the method is written from, built once for it.
    private static QueryMethodContext ContextOf(
        StringBuilder builder,
        QueryMethod method,
        ExecutionCapabilities capabilities
    )
    {
        var tokens = MethodWriter.GetTokens(method.Segments);
        var arguments = MethodArguments.Of(method, tokens);
        var streams = method.Streams;
        var locals = new QueryMethodLocals(
            arguments,
            method.Input,
            method.Shape is ResultShape.None or ResultShape.RowCount ? null : method.Output,
            streams,
            capabilities,
            method.Source
        );
        return new QueryMethodContext(
            builder,
            method,
            method.Host,
            tokens,
            MethodArguments.SpellTokens(tokens),
            arguments,
            locals,
            capabilities,
            streams
        );
    }
}
