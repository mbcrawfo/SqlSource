using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Generation.Methods;

namespace SqlSource.Generation.Emitting.QueryMethods;

/// <summary>
/// Writes the body of a query's method that runs its command, the phase of <see cref="QueryMethodWriter" /> after
/// <see cref="HeaderWriter" />: the refusal of a completed transaction, the SQL, the opening of a closed connection,
/// the command and the binding of its parameters, and the closing of the connection in a <c>finally</c>.
/// <see cref="RowsWriter" /> writes the execution between them.
/// </summary>
internal static class CommandWriter
{
    // The lines around the assignment of a command's SQL when its tokens build it.
    private const string SqlFromTokensStart =
        "#pragma warning disable CA2100 // The text of a token is the caller's own choice.\n";

    private const string SqlFromTokensEnd = "#pragma warning restore CA2100\n";

    // The refusal of a completed transaction when the capabilities have it, then the SQL, the opening of a closed
    // connection, the command, its parameters and its execution, and the closing of the connection in a finally, at
    // the indent of the method's body.
    internal static void AppendBody(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        var locals = context.Locals;
        var blockIndent = GeneratedText.Deeper(indent);
        if (context.Capabilities.Has(ExecutionCapabilities.RefusesCompletedTransaction))
        {
            ExecutionCapabilityWriter.AppendCompletedTransactionCheck(in context, indent);
        }

        AppendSql(in context, indent);

        _ = builder
            .Append(indent)
            .Append("var ")
            .Append(locals.WasClosed)
            .Append(" = ")
            .Append(locals.Connection)
            .Append(".State == global::System.Data.ConnectionState.Closed;\n");
        QueryMethodWriter.AppendIf(builder, indent, locals.WasClosed);
        _ = builder.Append(blockIndent);
        _ = AsyncSpelling
            .AppendAwaitedCall(builder, locals.Connection, "OpenAsync")
            .Append(MethodArguments.CancellationToken);
        _ = AsyncSpelling.AppendAwaitedEnd(builder).Append(";\n");
        QueryMethodWriter.AppendEnd(builder, indent);
        _ = builder.Append('\n').Append(indent).Append("try\n").Append(indent).Append("{\n");

        AppendCommand(in context, blockIndent);
        AppendBindings(in context, blockIndent);

        RowsWriter.AppendExecution(in context, blockIndent);

        _ = builder.Append(indent).Append("}\n").Append(indent).Append("finally\n").Append(indent).Append("{\n");
        QueryMethodWriter.AppendIf(builder, blockIndent, locals.WasClosed);
        _ = builder.Append(blockIndent).Append(GeneratedText.Indent);
        _ = AsyncSpelling.AppendAwaitedCall(builder, locals.Connection, "CloseAsync");
        _ = AsyncSpelling.AppendAwaitedEnd(builder).Append(";\n");
        QueryMethodWriter.AppendEnd(builder, blockIndent);
        QueryMethodWriter.AppendEnd(builder, indent);
    }

    // The SQL the constant would have held, or, with tokens, the string the token method would have returned, built
    // by the same code.
    private static void AppendSql(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        var segments = context.Method.Segments;
        var locals = context.Locals;
        if (context.Tokens.Count == 0)
        {
            _ = builder
                .Append(indent)
                .Append("const string ")
                .Append(locals.Sql)
                .Append(" = ")
                .Append(SymbolDisplay.FormatLiteral(MethodWriter.GetSql(segments), quote: true))
                .Append(";\n");
            return;
        }

        _ = builder.Append(indent).Append("var ").Append(locals.Sql).Append(" = ");
        MethodWriter.AppendCreate(
            builder,
            GeneratedText.Deeper(indent),
            indent,
            segments,
            context.Tokens,
            context.SpelledTokens,
            locals.Span,
            locals.State
        );
        _ = builder.Append(";\n");
    }

    // A command whose SQL is built at run time, from the text of its tokens, is what CA2100 flags, and its tokens are
    // text that the caller chooses, so the assignment turns the rule off around itself and nothing else.
    private static void AppendCommand(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        var locals = context.Locals;
        var builtAtRunTime = context.Tokens.Count > 0;
        _ = builder
            .Append(indent)
            .Append("var ")
            .Append(locals.Command)
            .Append(" = ")
            .Append(locals.Connection)
            .Append(".CreateCommand();\n");
        _ = AsyncSpelling.AppendUsing(builder, indent, locals.CommandScope, locals.Command);
        _ = builder
            .Append(builtAtRunTime ? indent + SqlFromTokensStart : string.Empty)
            .Append(indent)
            .Append(locals.Command)
            .Append(".CommandText = ")
            .Append(locals.Sql)
            .Append(";\n")
            .Append(builtAtRunTime ? indent + SqlFromTokensEnd : string.Empty)
            .Append(indent)
            .Append(locals.Command)
            .Append(".Transaction = ")
            .Append(locals.Transaction)
            .Append(";\n");
        QueryMethodWriter.AppendIf(builder, indent, MethodArguments.CommandTimeout + ".HasValue");
        _ = builder
            .Append(indent)
            .Append(GeneratedText.Indent)
            .Append(locals.Command)
            .Append(".CommandTimeout = ")
            .Append(MethodArguments.CommandTimeout)
            .Append(".Value;\n");
        QueryMethodWriter.AppendEnd(builder, indent);
        _ = builder.Append('\n');
    }

    // Each parameter of the input, in a block of its own, bound by the binder of the method's engine.
    private static void AppendBindings(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        var locals = context.Locals;
        if (context.Method.Input is { } input)
        {
            var binder = ParameterBinder.For(context.Method.Engine);
            var bindingContext = new BindingContext(
                builder,
                GeneratedText.Deeper(indent),
                locals.Command,
                locals.Parameter,
                MethodArguments.Parameters,
                locals.ConversionNames
            );
            foreach (var parameter in input.Parameters)
            {
                _ = builder.Append(indent).Append("{\n");
                binder.Append(bindingContext, parameter);
                QueryMethodWriter.AppendEnd(builder, indent);
                _ = builder.Append('\n');
            }
        }
    }
}
