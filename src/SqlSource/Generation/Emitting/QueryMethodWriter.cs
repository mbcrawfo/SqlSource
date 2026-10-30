using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Generation.Methods;
using SqlSource.Settings;

namespace SqlSource.Generation.Emitting;

/// <summary>
/// Writes the asynchronous method of a query: it opens the connection when it is closed, binds each parameter with
/// the <see cref="ParameterBinder" /> of its file's engine, runs the query and reads its rows as the shape says.  A
/// many query of <see cref="CollectionKind.IAsyncEnumerable" /> checks its arguments and returns a local iterator
/// that does the rest as the loop asks for each row.
/// </summary>
/// <remarks>
/// A token's argument can have any name that is not a fixed argument's, so every local, and the parameters of the
/// lambda that builds the SQL, take a name no argument has from one <see cref="LocalNames" />, which
/// <see cref="QueryMethodLocals" /> holds.  <see cref="RowReaderWriter" /> writes the reading of each row.  Every
/// framework type is written from <c>global::</c>, an extension method is called as a static method, and
/// <c>string</c>, <c>int</c> and <c>bool</c> are written as keywords.
/// </remarks>
internal static class QueryMethodWriter
{
    internal const string Connection = "connection";

    internal const string Parameters = "parameters";

    internal const string Transaction = "transaction";

    internal const string CommandTimeout = "commandTimeout";

    internal const string CancellationToken = "cancellationToken";

    // The token of a call that nothing cancels.
    private const string NoCancellation = "global::System.Threading.CancellationToken.None";

    private const string ExceptionType = "global::SqlSource.UnexpectedRowCountException";

    private const string RolledBackType = "global::SqlSource.TransactionRolledBackException";

    private const string CompletedTransactionType = "global::System.InvalidOperationException";

    // The lines around the assignment of a command's SQL when its tokens build it.
    private const string SqlFromTokensStart =
        "#pragma warning disable CA2100 // The text of a token is the caller's own choice.\n";

    private const string SqlFromTokensEnd = "#pragma warning restore CA2100\n";

    /// <summary>
    /// The spelling of <see cref="MethodColumn.ReadText" /> that marks a column read as SqlClient's vector, which gives
    /// <c>SqlVector&lt;float&gt;.Null</c> on a <c>NULL</c> instead of throwing: a not-null column read as exactly this
    /// text is read into a local and refused when it <c>IsNull</c>, before a converter sees it.
    /// </summary>
    internal const string VectorType = "global::Microsoft.Data.SqlTypes.SqlVector<float>";

    // DbCommand and DbDataReader are IAsyncDisposable, whose ConfigureAwait is an extension method.  The generated file
    // imports no namespace, so it is called by its class.
    private const string ConfigureAwaitOfDisposable =
        "global::System.Threading.Tasks.TaskAsyncEnumerableExtensions.ConfigureAwait(";

    /// <summary>
    /// Appends the documentation comment and the method.
    /// </summary>
    /// <param name="builder">The source being built.</param>
    /// <param name="indent">The whitespace that starts each line of the member.</param>
    /// <param name="method">
    /// The query's method.  Its <see cref="QueryMethod.Output" /> is set when its shape reads rows.
    /// </param>
    /// <param name="modifiers"><c>public static</c>, <c>internal static</c> or <c>private static</c>.</param>
    /// <param name="isExtension">Whether the method extends the connection, which writes <c>this</c> before it.</param>
    /// <param name="rowBufferType">
    /// The row buffer's type from <see cref="RowBuffer.TypeTextFor" />, read only when
    /// <see cref="RowBuffer.IsUsedBy" />.
    /// </param>
    public static void Append(
        StringBuilder builder,
        string indent,
        QueryMethod method,
        string modifiers,
        bool isExtension,
        string rowBufferType
    )
    {
        var tokens = MethodWriter.GetTokens(method.Segments);
        var arguments = GetArguments(tokens);
        var streams = Streams(method);
        var locals = new QueryMethodLocals(
            tokens,
            method.Input,
            method.Shape is ResultShape.None or ResultShape.RowCount ? null : method.Output,
            streams,
            DrainsWhenLeftAtARow(method),
            ChecksTransaction(method)
        );
        var bodyIndent = indent + GeneratedText.Indent;
        var blockIndent = bodyIndent + GeneratedText.Indent;

        AppendDocumentation(builder, indent, method, tokens, streams);
        AppendSignature(builder, indent, method, modifiers, isExtension, arguments, streams);
        _ = builder.Append(indent).Append("{\n");
        AppendChecks(builder, bodyIndent, method, arguments);
        if (!streams)
        {
            AppendBody(builder, bodyIndent, method, tokens, arguments, locals, rowBufferType, streams);
            AppendEnd(builder, indent);
            return;
        }

        // The checks run when the method is called, and the rest when the loop asks for the first row.  The iterator
        // captures nothing: it takes every argument by the same name, so the body is written as in any other method.
        _ = builder.Append('\n').Append(bodyIndent).Append("return ").Append(locals.Iterator).Append('(');
        AppendCallArguments(builder, method, arguments);
        _ = builder
            .Append(");\n\n")
            .Append(bodyIndent)
            .Append("static async ")
            .Append(GetResultType(method))
            .Append(' ')
            .Append(locals.Iterator)
            .Append("(\n");
        AppendArguments(builder, bodyIndent, method, arguments, isExtension: false, isIterator: true);
        _ = builder.Append(bodyIndent).Append("{\n");
        AppendBody(builder, blockIndent, method, tokens, arguments, locals, rowBufferType, streams);
        AppendEnd(builder, bodyIndent);
        AppendEnd(builder, indent);
    }

    // A many query of IAsyncEnumerable, whose method returns a local iterator that reads each row as it is asked for.
    private static bool Streams(QueryMethod method) =>
        method.Shape == ResultShape.Many && method.Collection == CollectionKind.IAsyncEnumerable;

    // A streaming method on SQL Server, whose iterator reads the later results in a finally when its loop is left at
    // a row.
    private static bool DrainsWhenLeftAtARow(QueryMethod method) =>
        Streams(method) && method.Engine == DatabaseEngine.SqlServer;

    // A method on SQL Server that reads rows, which can throw an exception of its own before it has read the rest of
    // the batch, so that the reader's disposal reads it, and an error there can roll back the transaction unannounced.
    private static bool ChecksTransaction(QueryMethod method) =>
        method.Engine == DatabaseEngine.SqlServer
        && method.Shape is ResultShape.Many or ResultShape.One or ResultShape.OneOptional;

    // On SQL Server the refusal of a completed transaction, then the SQL, the opening of a closed connection, the
    // command, its parameters and its execution, and the closing of the connection in a finally, at the indent of the
    // method's body.
    private static void AppendBody(
        StringBuilder builder,
        string indent,
        QueryMethod method,
        List<string> tokens,
        List<string> arguments,
        QueryMethodLocals locals,
        string rowBufferType,
        bool streams
    )
    {
        var blockIndent = indent + GeneratedText.Indent;
        AppendCompletedTransactionCheck(builder, indent, method);
        AppendSql(builder, indent, method, tokens, arguments, locals);

        _ = builder
            .Append(indent)
            .Append("var ")
            .Append(locals.WasClosed)
            .Append(" = ")
            .Append(Connection)
            .Append(".State == global::System.Data.ConnectionState.Closed;\n");
        AppendIf(builder, indent, locals.WasClosed);
        _ = builder
            .Append(blockIndent)
            .Append("await ")
            .Append(Connection)
            .Append(".OpenAsync(")
            .Append(CancellationToken)
            .Append(").ConfigureAwait(false);\n");
        AppendEnd(builder, indent);
        _ = builder.Append('\n').Append(indent).Append("try\n").Append(indent).Append("{\n");

        AppendCommand(builder, blockIndent, locals, builtAtRunTime: tokens.Count > 0);
        if (method.Input is { } input)
        {
            var binder = ParameterBinder.For(method.Engine);
            var context = new BindingContext(
                builder,
                blockIndent + GeneratedText.Indent,
                locals.Command,
                locals.Parameter,
                Parameters,
                locals.ConversionNames
            );
            foreach (var parameter in input.Parameters)
            {
                _ = builder.Append(blockIndent).Append("{\n");
                binder.Append(context, parameter);
                AppendEnd(builder, blockIndent);
                _ = builder.Append('\n');
            }
        }

        AppendExecution(builder, blockIndent, method, locals, rowBufferType, streams);

        _ = builder.Append(indent).Append("}\n").Append(indent).Append("finally\n").Append(indent).Append("{\n");
        AppendIf(builder, blockIndent, locals.WasClosed);
        _ = builder
            .Append(blockIndent)
            .Append(GeneratedText.Indent)
            .Append("await ")
            .Append(Connection)
            .Append(".CloseAsync().ConfigureAwait(false);\n");
        AppendEnd(builder, blockIndent);
        AppendEnd(builder, indent);
    }

    // On SQL Server a transaction that has completed, whose Connection is null, is refused before anything runs: a
    // command given it would run outside any transaction and commit on its own, and the catch of
    // AppendTransactionCheck would blame this query for a rollback it did not cause.  A streaming method checks when
    // its loop starts, so a transaction that completes between the call and the loop is refused too.  The check is
    // followed by a blank line, as the SQL follows it.
    private static void AppendCompletedTransactionCheck(StringBuilder builder, string indent, QueryMethod method)
    {
        if (method.Engine != DatabaseEngine.SqlServer)
        {
            return;
        }

        AppendIf(builder, indent, Transaction + " is not null && " + Transaction + ".Connection is null");
        _ = builder
            .Append(indent)
            .Append(GeneratedText.Indent)
            .Append("throw new ")
            .Append(CompletedTransactionType)
            .Append('(')
            .Append(
                SymbolDisplay.FormatLiteral(
                    "The transaction given to the query '"
                        + method.QueryName
                        + "' has completed; it is no longer usable.",
                    quote: true
                )
            )
            .Append(");\n");
        AppendEnd(builder, indent);
        _ = builder.Append('\n');
    }

    private static void AppendDocumentation(
        StringBuilder builder,
        string indent,
        QueryMethod method,
        List<string> tokens,
        bool streams
    )
    {
        XmlDocWriter.AppendMember(builder, indent, method.SummaryXml, MethodWriter.GetSql(method.Segments));
        XmlDocWriter.AppendParam(
            builder,
            indent,
            Connection,
            streams
                ? "The connection to run the query on.  When it is closed, it is opened when the loop starts and "
                    + "closed again when the loop ends."
                : "The connection to run the query on.  It is opened when it is closed, and closed again when the "
                    + "method opened it."
        );
        if (method.Input is not null)
        {
            XmlDocWriter.AppendParam(builder, indent, Parameters, "The query's parameters.");
        }

        foreach (var token in tokens)
        {
            XmlDocWriter.AppendParam(builder, indent, token);
        }

        XmlDocWriter.AppendParam(builder, indent, Transaction, "The transaction to run the query in, or null.");
        XmlDocWriter.AppendParam(
            builder,
            indent,
            CommandTimeout,
            "The command's timeout in seconds, or null for the driver's."
        );
        XmlDocWriter.AppendParam(builder, indent, CancellationToken, "Cancels the query.");
        XmlDocWriter.AppendReturns(
            builder,
            indent,
            method.Shape switch
            {
                ResultShape.Many when streams => "The rows the query returns, each read as the loop asks for it.",
                ResultShape.Many => "The rows the query returned.",
                ResultShape.One => "The row.",
                ResultShape.OneOptional => "The row, or null when there is none.",
                ResultShape.RowCount => "The number of rows the query affected.",
                ResultShape.None or _ => "A task that completes when the query has run.",
            }
        );
        if (method.Shape == ResultShape.One)
        {
            XmlDocWriter.AppendException(
                builder,
                indent,
                ExceptionType,
                "The query returned no rows, or more than one."
            );
        }
        else if (method.Shape == ResultShape.OneOptional)
        {
            XmlDocWriter.AppendException(builder, indent, ExceptionType, "The query returned more than one row.");
        }

        if (method.Engine == DatabaseEngine.SqlServer)
        {
            XmlDocWriter.AppendException(
                builder,
                indent,
                CompletedTransactionType,
                "The transaction has completed: it was committed or rolled back before the query ran."
            );
        }

        if (ChecksTransaction(method))
        {
            XmlDocWriter.AppendException(
                builder,
                indent,
                RolledBackType,
                "The query threw an exception of its own, its inner exception, and an error that the rest of the "
                    + "query raised rolled back the transaction."
            );
        }
    }

    private static void AppendSignature(
        StringBuilder builder,
        string indent,
        QueryMethod method,
        string modifiers,
        bool isExtension,
        List<string> arguments,
        bool streams
    )
    {
        // A streaming method is not asynchronous itself: it returns its iterator.
        _ = builder.Append(indent).Append(modifiers);
        if (streams)
        {
            _ = builder.Append(' ').Append(GetResultType(method)).Append(' ');
        }
        else
        {
            _ = builder.Append(" async global::System.Threading.Tasks.Task");
            if (GetResultType(method) is { } resultType)
            {
                _ = builder.Append('<').Append(resultType).Append('>');
            }

            _ = builder.Append(' ');
        }

        _ = builder.Append(method.Name).Append("(\n");
        AppendArguments(builder, indent, method, arguments, isExtension, isIterator: false);
    }

    // The arguments of the method, one to a line, and the parenthesis that closes them.  The iterator of a streaming
    // method takes them without this or a default, and marks its token as the one WithCancellation gives.
    private static void AppendArguments(
        StringBuilder builder,
        string indent,
        QueryMethod method,
        List<string> arguments,
        bool isExtension,
        bool isIterator
    )
    {
        var argumentIndent = indent + GeneratedText.Indent;
        _ = builder
            .Append(argumentIndent)
            .Append(isExtension ? "this " : string.Empty)
            .Append("global::System.Data.Common.DbConnection ")
            .Append(Connection)
            .Append(",\n");
        if (method.Input is { } input)
        {
            _ = builder.Append(argumentIndent).Append(input.TypeText).Append(' ').Append(Parameters).Append(",\n");
        }

        foreach (var argument in arguments)
        {
            _ = builder.Append(argumentIndent).Append("string ").Append(argument).Append(",\n");
        }

        _ = builder
            .Append(argumentIndent)
            .Append("global::System.Data.Common.DbTransaction? ")
            .Append(Transaction)
            .Append(isIterator ? ",\n" : " = null,\n")
            .Append(argumentIndent)
            .Append("int? ")
            .Append(CommandTimeout)
            .Append(isIterator ? ",\n" : " = null,\n")
            .Append(argumentIndent)
            .Append(
                isIterator
                    ? "[global::System.Runtime.CompilerServices.EnumeratorCancellation] "
                        + "global::System.Threading.CancellationToken "
                    : "global::System.Threading.CancellationToken "
            )
            .Append(CancellationToken)
            .Append(isIterator ? ")\n" : " = default)\n");
    }

    // The arguments of the call to the iterator, in the order of the method's.
    private static void AppendCallArguments(StringBuilder builder, QueryMethod method, List<string> arguments)
    {
        _ = builder.Append(Connection);
        if (method.Input is not null)
        {
            _ = builder.Append(", ").Append(Parameters);
        }

        foreach (var argument in arguments)
        {
            _ = builder.Append(", ").Append(argument);
        }

        _ = builder
            .Append(", ")
            .Append(Transaction)
            .Append(", ")
            .Append(CommandTimeout)
            .Append(", ")
            .Append(CancellationToken);
    }

    // The type the task gives, or null for a task that gives nothing.
    private static string? GetResultType(QueryMethod method) =>
        method.Shape switch
        {
            ResultShape.Many => GetCollectionType(method.Collection, method.Output!.TypeText),
            ResultShape.One => method.Output!.TypeText,
            ResultShape.OneOptional => method.Output!.TypeText + "?",
            ResultShape.RowCount => "int",
            ResultShape.None or _ => null,
        };

    private static string GetCollectionType(CollectionKind collection, string row) =>
        collection switch
        {
            CollectionKind.Array => row + "[]",
            CollectionKind.IEnumerable => "global::System.Collections.Generic.IEnumerable<" + row + ">",
            CollectionKind.ICollection => "global::System.Collections.Generic.ICollection<" + row + ">",
            CollectionKind.IReadOnlyCollection => "global::System.Collections.Generic.IReadOnlyCollection<" + row + ">",
            CollectionKind.IList => "global::System.Collections.Generic.IList<" + row + ">",
            CollectionKind.IReadOnlyList => "global::System.Collections.Generic.IReadOnlyList<" + row + ">",
            CollectionKind.List => "global::System.Collections.Generic.List<" + row + ">",
            CollectionKind.ImmutableArray => "global::System.Collections.Immutable.ImmutableArray<" + row + ">",
            CollectionKind.ImmutableList => "global::System.Collections.Immutable.ImmutableList<" + row + ">",
            CollectionKind.IAsyncEnumerable => "global::System.Collections.Generic.IAsyncEnumerable<" + row + ">",
            CollectionKind.IImmutableList or _ => "global::System.Collections.Immutable.IImmutableList<" + row + ">",
        };

    // The buffer the rows are read into, and the expression that gives the collection from it.
    private static (string Buffer, string Result) GetBuffer(
        QueryMethod method,
        string row,
        string rows,
        string rowBufferType
    )
    {
        if (RowBuffer.IsUsedBy(method))
        {
            var toResult = method.Collection switch
            {
                CollectionKind.List or CollectionKind.IList or CollectionKind.ICollection => ".ToList()",
                CollectionKind.ImmutableArray => ".ToImmutableArray()",
                // The two immutable lists and IAsyncEnumerable never reach this switch, since RowBuffer.IsUsedBy leaves
                // them out; they are named so that the switch lists every member.
                CollectionKind.Array
                or CollectionKind.IEnumerable
                or CollectionKind.IReadOnlyCollection
                or CollectionKind.IReadOnlyList
                or CollectionKind.ImmutableList
                or CollectionKind.IImmutableList
                or CollectionKind.IAsyncEnumerable
                or _ => ".ToArray()",
            };
            return ("new " + rowBufferType + "<" + row + ">()", rows + toResult);
        }

        return method.Collection switch
        {
            CollectionKind.List or CollectionKind.IList or CollectionKind.ICollection => (
                "new global::System.Collections.Generic.List<" + row + ">()",
                rows
            ),
            CollectionKind.ImmutableArray => (
                "global::System.Collections.Immutable.ImmutableArray.CreateBuilder<" + row + ">()",
                rows + ".DrainToImmutable()"
            ),
            CollectionKind.ImmutableList or CollectionKind.IImmutableList => (
                "global::System.Collections.Immutable.ImmutableList.CreateBuilder<" + row + ">()",
                rows + ".ToImmutable()"
            ),
            // A streaming method buffers nothing and never reaches this switch; IAsyncEnumerable is named so that the
            // switch lists every member.
            CollectionKind.Array
            or CollectionKind.IEnumerable
            or CollectionKind.IReadOnlyCollection
            or CollectionKind.IReadOnlyList
            or CollectionKind.IAsyncEnumerable
            or _ => ("new global::System.Collections.Generic.List<" + row + ">()", rows + ".ToArray()"),
        };
    }

    private static void AppendChecks(StringBuilder builder, string indent, QueryMethod method, List<string> arguments)
    {
        AppendThrowIfNull(builder, indent, Connection);
        if (method.Input is not null)
        {
            AppendThrowIfNull(builder, indent, Parameters);
        }

        if (method.ValidateTokens)
        {
            foreach (var argument in arguments)
            {
                _ = builder
                    .Append(indent)
                    .Append("global::System.ArgumentException.ThrowIfNullOrWhiteSpace(")
                    .Append(argument)
                    .Append(");\n");
            }
        }
    }

    // How each token's argument is written in the method.  An asynchronous method cannot use await as an identifier,
    // so that one is verbatim; its documentation keeps the name.
    private static List<string> GetArguments(List<string> tokens)
    {
        if (!tokens.Contains("await"))
        {
            return tokens;
        }

        var arguments = new List<string>(tokens.Count);
        foreach (var token in tokens)
        {
            arguments.Add(token == "await" ? "@await" : token);
        }

        return arguments;
    }

    private static void AppendThrowIfNull(StringBuilder builder, string indent, string argument) =>
        _ = builder
            .Append(indent)
            .Append("global::System.ArgumentNullException.ThrowIfNull(")
            .Append(argument)
            .Append(");\n");

    // The SQL the constant would have held, or, with tokens, the string the token method would have returned, built
    // by the same code.
    private static void AppendSql(
        StringBuilder builder,
        string indent,
        QueryMethod method,
        List<string> tokens,
        List<string> arguments,
        QueryMethodLocals locals
    )
    {
        if (tokens.Count == 0)
        {
            _ = builder
                .Append(indent)
                .Append("const string ")
                .Append(locals.Sql)
                .Append(" = ")
                .Append(SymbolDisplay.FormatLiteral(MethodWriter.GetSql(method.Segments), quote: true))
                .Append(";\n");
            return;
        }

        _ = builder.Append(indent).Append("var ").Append(locals.Sql).Append(" = ");
        MethodWriter.AppendCreate(
            builder,
            indent + GeneratedText.Indent,
            indent,
            method.Segments,
            tokens,
            arguments,
            locals.Span,
            locals.State
        );
        _ = builder.Append(";\n");
    }

    // A command whose SQL is built at run time, from the text of its tokens, is what CA2100 flags, and its tokens are
    // text that the caller chooses, so the assignment turns the rule off around itself and nothing else.
    private static void AppendCommand(
        StringBuilder builder,
        string indent,
        QueryMethodLocals locals,
        bool builtAtRunTime
    )
    {
        _ = builder
            .Append(indent)
            .Append("var ")
            .Append(locals.Command)
            .Append(" = ")
            .Append(Connection)
            .Append(".CreateCommand();\n");
        AppendScope(builder, indent, locals.CommandScope, locals.Command);
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
            .Append(Transaction)
            .Append(";\n");
        AppendIf(builder, indent, CommandTimeout + ".HasValue");
        _ = builder
            .Append(indent)
            .Append(GeneratedText.Indent)
            .Append(locals.Command)
            .Append(".CommandTimeout = ")
            .Append(CommandTimeout)
            .Append(".Value;\n");
        AppendEnd(builder, indent);
        _ = builder.Append('\n');
    }

    private static void AppendExecution(
        StringBuilder builder,
        string indent,
        QueryMethod method,
        QueryMethodLocals locals,
        string rowBufferType,
        bool streams
    )
    {
        if (method.Shape is ResultShape.None or ResultShape.RowCount)
        {
            _ = builder
                .Append(indent)
                .Append(method.Shape == ResultShape.RowCount ? "return await " : "await ")
                .Append(locals.Command)
                .Append(".ExecuteNonQueryAsync(")
                .Append(CancellationToken)
                .Append(").ConfigureAwait(false);\n");
            return;
        }

        var output = method.Output!;
        _ = builder
            .Append(indent)
            .Append("var ")
            .Append(locals.Reader)
            .Append(" = await ")
            .Append(locals.Command)
            .Append(".ExecuteReaderAsync(global::System.Data.CommandBehavior.Default, ")
            .Append(CancellationToken)
            .Append(").ConfigureAwait(false);\n");
        AppendScope(builder, indent, locals.ReaderScope, locals.Reader);

        // A streaming method yields each row as it reads it, where any other reads them into its buffer and returns
        // the collection.
        if (streams)
        {
            AppendStream(builder, indent, method, output, locals);
            return;
        }

        // On SQL Server the read, its drain and its returns are in a try whose catch checks the transaction.
        if (!ChecksTransaction(method))
        {
            AppendRows(builder, indent, method, output, locals, rowBufferType);
            return;
        }

        _ = builder.Append(indent).Append("try\n").Append(indent).Append("{\n");
        AppendRows(builder, indent + GeneratedText.Indent, method, output, locals, rowBufferType);
        AppendEnd(builder, indent);
        AppendTransactionCheck(builder, indent, method, locals);
    }

    // The rows read into the buffer and the collection returned, or the one row read and returned.
    private static void AppendRows(
        StringBuilder builder,
        string indent,
        QueryMethod method,
        MethodOutput output,
        QueryMethodLocals locals,
        string rowBufferType
    )
    {
        var innerIndent = indent + GeneratedText.Indent;
        if (method.Shape == ResultShape.Many)
        {
            var (buffer, result) = GetBuffer(method, output.TypeText, locals.Rows, rowBufferType);
            _ = builder.Append(indent).Append("var ").Append(locals.Rows).Append(" = ").Append(buffer).Append(";\n");
            _ = builder.Append(indent).Append("while (");
            AppendRead(builder, locals);
            _ = builder.Append(")\n").Append(indent).Append("{\n");
            RowReaderWriter.AppendVectorChecks(builder, innerIndent, output, locals);
            _ = builder.Append(innerIndent).Append(locals.Rows).Append(".Add(");
            RowReaderWriter.AppendRow(builder, output, locals);
            _ = builder.Append(");\n");
            AppendEnd(builder, indent);
            _ = builder.Append('\n');
            AppendDrain(builder, indent, method, locals);
            _ = builder.Append(indent).Append("return ").Append(result).Append(";\n");
            return;
        }

        _ = builder.Append(indent).Append("if (!");
        AppendRead(builder, locals);
        _ = builder.Append(")\n").Append(indent).Append("{\n");
        if (method.Shape == ResultShape.One)
        {
            AppendThrow(builder, innerIndent, method.QueryName, moreThanOne: false);
        }
        else
        {
            AppendDrain(builder, innerIndent, method, locals);
            _ = builder.Append(innerIndent).Append("return null;\n");
        }

        AppendEnd(builder, indent);
        _ = builder.Append('\n');
        RowReaderWriter.AppendVectorChecks(builder, indent, output, locals);
        _ = builder.Append(indent).Append("var ").Append(locals.Row).Append(" = ");
        RowReaderWriter.AppendRow(builder, output, locals);
        _ = builder.Append(";\n").Append(indent).Append("if (");
        AppendRead(builder, locals);
        _ = builder.Append(")\n").Append(indent).Append("{\n");
        AppendThrow(builder, innerIndent, method.QueryName, moreThanOne: true);
        AppendEnd(builder, indent);
        _ = builder.Append('\n');
        AppendDrain(builder, indent, method, locals);
        _ = builder.Append(indent).Append("return ").Append(locals.Row).Append(";\n");
    }

    // The iterator's loop, which yields each row as it reads it.  On SQL Server the loop and the drain after it are in
    // a try whose finally drains when the loop was left at a row: a break, a throw in the loop's body or a disposal
    // disposes the iterator at its yield, and the rest of the batch can hold an error that the caller is to see.  The
    // flag is set once the row is built, so an exception of the iterator's own, from a read, a check or a converter,
    // drains nothing.  The finally's drain takes no token, as the reader's disposal would read the rest: a token the
    // caller cancelled before an early exit neither throws nor cancels the command, and the drain throws only what the
    // database raises.  C# allows no catch on a try that holds a yield return, so each row's checks and construction
    // are in a try of their own, whose catch checks the transaction when one of them throws.  A read's exception is the
    // database's or the caller's cancellation, which the catch would throw as it is.
    private static void AppendStream(
        StringBuilder builder,
        string indent,
        QueryMethod method,
        MethodOutput output,
        QueryMethodLocals locals
    )
    {
        var tracksRow = DrainsWhenLeftAtARow(method);
        var loopIndent = tracksRow ? indent + GeneratedText.Indent : indent;
        var innerIndent = loopIndent + GeneratedText.Indent;
        if (tracksRow)
        {
            _ = builder
                .Append(indent)
                .Append("var ")
                .Append(locals.AtRow)
                .Append(" = false;\n")
                .Append(indent)
                .Append("try\n")
                .Append(indent)
                .Append("{\n");
        }

        _ = builder.Append(loopIndent).Append("while (");
        AppendRead(builder, locals);
        _ = builder.Append(")\n").Append(loopIndent).Append("{\n");
        if (!tracksRow)
        {
            RowReaderWriter.AppendVectorChecks(builder, innerIndent, output, locals);
            _ = builder.Append(innerIndent).Append("yield return ");
            RowReaderWriter.AppendRow(builder, output, locals);
            _ = builder.Append(";\n");
            AppendEnd(builder, loopIndent);
            return;
        }

        var tryIndent = innerIndent + GeneratedText.Indent;
        _ = builder
            .Append(innerIndent)
            .Append(output.TypeText)
            .Append(' ')
            .Append(locals.Row)
            .Append(";\n")
            .Append(innerIndent)
            .Append("try\n")
            .Append(innerIndent)
            .Append("{\n");
        RowReaderWriter.AppendVectorChecks(builder, tryIndent, output, locals);
        _ = builder.Append(tryIndent).Append(locals.Row).Append(" = ");
        RowReaderWriter.AppendRow(builder, output, locals);
        _ = builder.Append(";\n");
        AppendEnd(builder, innerIndent);
        AppendTransactionCheck(builder, innerIndent, method, locals);
        _ = builder
            .Append('\n')
            .Append(innerIndent)
            .Append(locals.AtRow)
            .Append(" = true;\n")
            .Append(innerIndent)
            .Append("yield return ")
            .Append(locals.Row)
            .Append(";\n")
            .Append(innerIndent)
            .Append(locals.AtRow)
            .Append(" = false;\n");
        AppendEnd(builder, loopIndent);
        _ = builder.Append('\n');
        AppendDrainLoop(builder, loopIndent, locals, CancellationToken);
        _ = builder.Append(indent).Append("}\n").Append(indent).Append("finally\n").Append(indent).Append("{\n");
        AppendIf(builder, loopIndent, locals.AtRow);
        AppendDrainLoop(builder, innerIndent, locals, NoCancellation);
        AppendEnd(builder, loopIndent);
        AppendEnd(builder, indent);
    }

    // On SQL Server, the results after the rows are read before the method returns, since SqlClient raises an error
    // that a later statement of the batch holds only when NextResultAsync reaches it, and drops it when the reader is
    // disposed.  The method's own throws do not drain, so that a later statement's error never replaces one; the
    // catch of AppendTransactionCheck reports the one such an error can leave, a rolled-back transaction.  A
    // PostgreSQL query is one statement, so its reader has no later result.  The loop is followed by a blank line, as
    // a return follows it.
    private static void AppendDrain(StringBuilder builder, string indent, QueryMethod method, QueryMethodLocals locals)
    {
        if (method.Engine != DatabaseEngine.SqlServer)
        {
            return;
        }

        AppendDrainLoop(builder, indent, locals, CancellationToken);
        _ = builder.Append('\n');
    }

    // The catch of a SQL Server method's read, for an exception of the method's own: every one but the database's
    // error, which reports itself, and the caller's cancellation.  With a transaction given, it disposes the reader,
    // which reads the rest of the batch and drops any error there, and when such an error rolled the transaction back,
    // which leaves its Connection null, it throws the exception that says so, with the method's as its inner
    // exception.  A transaction that had completed before the query ran was refused by
    // AppendCompletedTransactionCheck, so a null Connection here comes from this query's batch.  Otherwise it throws
    // the method's own as it is.  A database error at a row is thrown as it is too, so a later error of the batch that
    // rolls the transaction back goes unreported (TD-0090).  A call that does not throw runs none of it, though the
    // await in the catch makes the method's state machine keep room for the exception.
    private static void AppendTransactionCheck(
        StringBuilder builder,
        string indent,
        QueryMethod method,
        QueryMethodLocals locals
    )
    {
        var blockIndent = indent + GeneratedText.Indent;
        _ = builder
            .Append(indent)
            .Append("catch (global::System.Exception ")
            .Append(locals.Exception)
            .Append(") when (")
            .Append(Transaction)
            .Append(" is not null && ")
            .Append(locals.Exception)
            .Append(" is not (global::System.Data.Common.DbException or global::System.OperationCanceledException))\n")
            .Append(indent)
            .Append("{\n")
            .Append(blockIndent)
            .Append("await ")
            .Append(locals.Reader)
            .Append(".DisposeAsync().ConfigureAwait(false);\n");
        AppendIf(builder, blockIndent, Transaction + ".Connection is null");
        _ = builder
            .Append(blockIndent)
            .Append(GeneratedText.Indent)
            .Append("throw new ")
            .Append(RolledBackType)
            .Append('(')
            .Append(SymbolDisplay.FormatLiteral(method.QueryName, quote: true))
            .Append(", ")
            .Append(locals.Exception)
            .Append(");\n");
        AppendEnd(builder, blockIndent);
        _ = builder.Append('\n').Append(blockIndent).Append("throw;\n");
        AppendEnd(builder, indent);
    }

    // The loop that reads the results after the rows and does nothing with them, with the token given.
    private static void AppendDrainLoop(StringBuilder builder, string indent, QueryMethodLocals locals, string token)
    {
        _ = builder
            .Append(indent)
            .Append("while (await ")
            .Append(locals.Reader)
            .Append(".NextResultAsync(")
            .Append(token)
            .Append(").ConfigureAwait(false))\n")
            .Append(indent)
            .Append("{\n");
        AppendEnd(builder, indent);
    }

    private static void AppendScope(StringBuilder builder, string indent, string scope, string disposable) =>
        _ = builder
            .Append(indent)
            .Append("await using var ")
            .Append(scope)
            .Append(" = ")
            .Append(ConfigureAwaitOfDisposable)
            .Append(disposable)
            .Append(", false);\n");

    private static void AppendRead(StringBuilder builder, QueryMethodLocals locals) =>
        _ = builder
            .Append("await ")
            .Append(locals.Reader)
            .Append(".ReadAsync(")
            .Append(CancellationToken)
            .Append(").ConfigureAwait(false)");

    private static void AppendThrow(StringBuilder builder, string indent, string queryName, bool moreThanOne) =>
        _ = builder
            .Append(indent)
            .Append("throw new ")
            .Append(ExceptionType)
            .Append('(')
            .Append(SymbolDisplay.FormatLiteral(queryName, quote: true))
            .Append(moreThanOne ? ", true);\n" : ", false);\n");

    internal static void AppendIf(StringBuilder builder, string indent, string condition) =>
        _ = builder.Append(indent).Append("if (").Append(condition).Append(")\n").Append(indent).Append("{\n");

    internal static void AppendEnd(StringBuilder builder, string indent) => _ = builder.Append(indent).Append("}\n");
}
