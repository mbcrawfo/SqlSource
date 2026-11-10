using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Generation.Methods;
using SqlSource.Generation.Repositories;

namespace SqlSource.Generation.Emitting.QueryMethods;

/// <summary>
/// Writes the body of a query's method that runs its command, the phase of <see cref="QueryMethodWriter" /> after
/// <see cref="HeaderWriter" />: the refusal of a completed transaction, the SQL, for a repository's method the
/// connection from its source, the opening of a closed connection, the command and the binding of its parameters, and
/// the closing of the connection in a <c>finally</c>.  <see cref="RowsWriter" /> writes the execution between them.
/// </summary>
internal static class CommandWriter
{
    // The lines around the assignment of a command's SQL when its tokens or its lists build it.
    private const string SqlFromTokensStart =
        "#pragma warning disable CA2100 // The text of a token is the caller's own choice.\n";

    private const string SqlFromListsStart =
        "#pragma warning disable CA2100 // The text is the query's own, with a parameter for each value of a list.\n";

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
        if (context.Method.Source is { } source)
        {
            AppendAcquire(in context, source, indent);
        }

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
    // by the same code, which with lists writes each use of a list as the list's values.
    private static void AppendSql(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        var segments = context.Method.Segments;
        var locals = context.Locals;
        if (context.Tokens.Count == 0 && locals.Lists is null)
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
            locals.State,
            locals.Lists
        );
        _ = builder.Append(";\n");
    }

    // A command whose SQL is built at run time, from the text of its tokens or for the values of its lists, is what
    // CA2100 flags, and its tokens are text that the caller chooses, and its lists' uses the query's own text, so the
    // assignment turns the rule off around itself and nothing else.
    private static void AppendCommand(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        var locals = context.Locals;
        var builtAtRunTime = context.Tokens.Count > 0 || locals.Lists is not null;
        var start = context.Tokens.Count > 0 ? SqlFromTokensStart : SqlFromListsStart;
        _ = builder
            .Append(indent)
            .Append("var ")
            .Append(locals.Command)
            .Append(" = ")
            .Append(locals.Connection)
            .Append(".CreateCommand();\n");
        _ = AsyncSpelling.AppendUsing(builder, indent, locals.CommandScope, locals.Command);
        _ = builder
            .Append(builtAtRunTime ? indent + start : string.Empty)
            .Append(indent)
            .Append(locals.Command)
            .Append(".CommandText = ")
            .Append(locals.Sql)
            .Append(";\n")
            .Append(builtAtRunTime ? indent + SqlFromTokensEnd : string.Empty);
        // A repository's method takes neither a transaction nor a timeout, and leaves the command's own.
        if (context.Arguments.TakesConnection)
        {
            _ = builder
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
        }

        _ = builder.Append('\n');
    }

    // The statements that get a repository's method its connection from its source, into the connection's local: a
    // new connection of the engine's driver from a connection string, one opened from a data source, or the one a
    // factory returns, each disposed when the method ends, as the command and the reader are, or a borrowed one, which
    // is not.  A source that is null throws.
    private static void AppendAcquire(in QueryMethodContext context, ConnectionSource source, string indent)
    {
        var builder = context.Builder;
        var locals = context.Locals;
        var access = locals.SourceAccess;
        _ = builder.Append(indent).Append("var ").Append(locals.Connection).Append(" = ");
        switch (source.Kind)
        {
            case ConnectionSourceKind.ConnectionString:
                _ = builder.Append("new ").Append(context.Method.Engine.ConnectionTypeText()).Append('(');
                AppendSourceOrThrow(builder, source, access);
                _ = builder.Append(')');
                break;
            case ConnectionSourceKind.DataSource:
                _ = AsyncSpelling.AppendAwait(builder).Append('(');
                AppendSourceOrThrow(builder, source, access);
                _ = builder.Append(").OpenConnectionAsync(").Append(MethodArguments.CancellationToken);
                _ = AsyncSpelling.AppendAwaitedEnd(builder);
                break;
            case ConnectionSourceKind.Factory:
            case ConnectionSourceKind.Borrowed:
                AppendSourceOrThrow(builder, source, access);
                break;
            default:
                throw UnexpectedEnumValueException.Of(source.Kind);
        }

        _ = builder.Append(";\n");
        if (locals.ConnectionScope.Length > 0)
        {
            _ = AsyncSpelling.AppendUsing(builder, indent, locals.ConnectionScope, locals.Connection);
        }
    }

    // The source's value, or the exception that says it is null: its member, as the method reaches it, read, or called
    // with the token when it takes one, and awaited when it is an asynchronous factory.
    private static void AppendSourceOrThrow(StringBuilder builder, ConnectionSource source, string access)
    {
        var isAwaited = source is { Kind: ConnectionSourceKind.Factory, IsAsync: true };
        if (isAwaited)
        {
            _ = AsyncSpelling.AppendAwait(builder);
        }

        _ = builder.Append(access);
        if (source.Member == CandidateMember.Method)
        {
            _ = builder.Append('(');
            if (source.TakesCancellationToken)
            {
                _ = builder.Append(MethodArguments.CancellationToken);
            }

            _ = isAwaited ? AsyncSpelling.AppendAwaitedEnd(builder) : builder.Append(')');
        }

        _ = builder
            .Append(" ?? throw new global::System.InvalidOperationException(")
            .Append(
                SymbolDisplay.FormatLiteral(
                    "The connection source '" + source.MemberName + "' of '" + source.TypeName + "' is null.",
                    quote: true
                )
            )
            .Append(')');
    }

    // Each parameter of the input, in a block of its own, bound by the binder of the method's engine.  A list binds
    // each of its values, and its null test.
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
                locals.Input,
                locals.ConversionNames
            );
            var list = 0;
            foreach (var parameter in input.Parameters)
            {
                if (parameter.IsList)
                {
                    ListBindingWriter.AppendBindings(
                        in context,
                        binder,
                        bindingContext,
                        locals.Lists!.Each[list++],
                        indent
                    );
                }
                else
                {
                    _ = builder.Append(indent).Append("{\n");
                    binder.Append(bindingContext, parameter);
                    QueryMethodWriter.AppendEnd(builder, indent);
                }

                _ = builder.Append('\n');
            }
        }
    }
}
