using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Generation.Methods;
using SqlSource.Settings;

namespace SqlSource.Generation.Emitting.QueryMethods;

/// <summary>
/// Writes the asynchronous method of a query: it opens the connection when it is closed, binds each parameter with
/// the <see cref="ParameterBinder" /> of its file's engine, runs the query and reads its rows as the shape says.  A
/// many query of <see cref="CollectionKind.IAsyncEnumerable" /> checks its arguments and returns a local iterator
/// that does the rest as the loop asks for each row.
/// </summary>
/// <remarks>
/// The arguments, their documentation and their checks come from <see cref="MethodArguments" />.  A token's argument
/// can have any name that <see cref="MethodArguments.IsReserved" /> allows, so every local, and the parameters of the
/// lambda that builds the SQL, take a name no argument has from one <see cref="LocalNames" />, which
/// <see cref="QueryMethodLocals" /> holds.  <see cref="RowReaderWriter" /> writes the reading of each row.  What the
/// engine needs beyond that is a flag of <see cref="ExecutionCapabilities" />, which the writer asks, never the engine,
/// and whose statements <see cref="ExecutionCapabilityWriter" /> writes; only the binder is chosen by the engine.
/// Every framework type is written from <c>global::</c>, an extension method is called as a static method, and
/// <c>string</c>, <c>int</c> and <c>bool</c> are written as keywords.
/// </remarks>
internal static class QueryMethodWriter
{
    private const string ExceptionType = "global::SqlSource.UnexpectedRowCountException";

    // The exception as its documentation names it: a documentation ID, which the compiler does not bind.  A project
    // that uses a referenced copy of the exceptions and references another assembly's copy too would find a bound cref
    // ambiguous (CS0419), since a cref is bound without regard to accessibility.
    private const string ExceptionCref = "T:SqlSource.UnexpectedRowCountException";

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
    /// <param name="host">
    /// Where the method goes and how visible it is, which gives its modifiers and whether it extends the connection.
    /// </param>
    public static void Append(StringBuilder builder, string indent, QueryMethod method, MethodHost host) =>
        Append(builder, indent, method, host, ExecutionCapabilityRules.Of(method));

    /// <summary>
    /// Appends the documentation comment and the method with the capabilities given, which the tests choose to write
    /// a combination of flags that no engine has yet.
    /// </summary>
    internal static void Append(
        StringBuilder builder,
        string indent,
        QueryMethod method,
        MethodHost host,
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
            capabilities
        );
        var context = new QueryMethodContext(
            builder,
            method,
            host,
            tokens,
            MethodArguments.SpellTokens(tokens),
            arguments,
            locals,
            capabilities,
            streams
        );
        var bodyIndent = GeneratedText.Deeper(indent);
        var blockIndent = GeneratedText.Deeper(bodyIndent);

        AppendDocumentation(in context, indent);
        AppendSignature(in context, indent);
        _ = builder.Append(indent).Append("{\n");
        AppendChecks(in context, bodyIndent);
        if (!streams)
        {
            AppendBody(in context, bodyIndent);
            AppendEnd(builder, indent);
            return;
        }

        // The checks run when the method is called, and the rest when the loop asks for the first row.  The iterator
        // captures nothing: it takes every argument by the same name, so the body is written as in any other method.
        _ = builder.Append('\n').Append(bodyIndent).Append("return ").Append(locals.Iterator).Append('(');
        arguments.AppendCall(builder);
        _ = builder
            .Append(");\n\n")
            .Append(bodyIndent)
            .Append("static async ")
            .Append(GetResultType(in context))
            .Append(' ')
            .Append(locals.Iterator)
            .Append("(\n");
        arguments.AppendIteratorDeclaration(builder, bodyIndent);
        _ = builder.Append(bodyIndent).Append("{\n");
        AppendBody(in context, blockIndent);
        AppendEnd(builder, bodyIndent);
        AppendEnd(builder, indent);
    }

    // The refusal of a completed transaction when the capabilities have it, then the SQL, the opening of a closed
    // connection, the command, its parameters and its execution, and the closing of the connection in a finally, at
    // the indent of the method's body.
    private static void AppendBody(in QueryMethodContext context, string indent)
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
        AppendIf(builder, indent, locals.WasClosed);
        _ = builder
            .Append(blockIndent)
            .Append("await ")
            .Append(locals.Connection)
            .Append(".OpenAsync(")
            .Append(MethodArguments.CancellationToken)
            .Append(").ConfigureAwait(false);\n");
        AppendEnd(builder, indent);
        _ = builder.Append('\n').Append(indent).Append("try\n").Append(indent).Append("{\n");

        AppendCommand(in context, blockIndent);
        if (context.Method.Input is { } input)
        {
            var binder = ParameterBinder.For(context.Method.Engine);
            var bindingContext = new BindingContext(
                builder,
                GeneratedText.Deeper(blockIndent),
                locals.Command,
                locals.Parameter,
                MethodArguments.Parameters,
                locals.ConversionNames
            );
            foreach (var parameter in input.Parameters)
            {
                _ = builder.Append(blockIndent).Append("{\n");
                binder.Append(bindingContext, parameter);
                AppendEnd(builder, blockIndent);
                _ = builder.Append('\n');
            }
        }

        AppendExecution(in context, blockIndent);

        _ = builder.Append(indent).Append("}\n").Append(indent).Append("finally\n").Append(indent).Append("{\n");
        AppendIf(builder, blockIndent, locals.WasClosed);
        _ = builder
            .Append(blockIndent)
            .Append(GeneratedText.Indent)
            .Append("await ")
            .Append(locals.Connection)
            .Append(".CloseAsync().ConfigureAwait(false);\n");
        AppendEnd(builder, blockIndent);
        AppendEnd(builder, indent);
    }

    private static void AppendDocumentation(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        var method = context.Method;
        XmlDocWriter.AppendMember(builder, indent, method.SummaryXml, MethodWriter.GetSql(method.Segments));
        foreach (var argument in context.Arguments)
        {
            if (argument.DocumentationXml is { } documentationXml)
            {
                XmlDocWriter.AppendParam(builder, indent, argument.Name, documentationXml);
            }
            else
            {
                XmlDocWriter.AppendParam(builder, indent, argument.Name);
            }
        }

        XmlDocWriter.AppendReturns(
            builder,
            indent,
            method.Shape switch
            {
                ResultShape.Many when context.Streams =>
                    "The rows the query returns, each read as the loop asks for it.",
                ResultShape.Many => "The rows the query returned.",
                ResultShape.One => "The row.",
                ResultShape.OneOptional => "The row, or null when there is none.",
                ResultShape.RowCount => "The number of rows the query affected.",
                ResultShape.None => "A task that completes when the query has run.",
            }
        );
        if (method.Shape == ResultShape.One)
        {
            XmlDocWriter.AppendException(
                builder,
                indent,
                ExceptionCref,
                "The query returned no rows, or more than one."
            );
        }
        else if (method.Shape == ResultShape.OneOptional)
        {
            XmlDocWriter.AppendException(builder, indent, ExceptionCref, "The query returned more than one row.");
        }

        ExecutionCapabilityWriter.AppendDocumentation(in context, indent);
    }

    private static void AppendSignature(in QueryMethodContext context, string indent)
    {
        // A streaming method is not asynchronous itself: it returns its iterator.
        var builder = context.Builder;
        _ = builder.Append(indent).Append(ModifiersOf(context.Host.Accessibility));
        if (context.Streams)
        {
            _ = builder.Append(' ').Append(GetResultType(in context)).Append(' ');
        }
        else
        {
            _ = builder.Append(" async global::System.Threading.Tasks.Task");
            if (GetResultType(in context) is { } resultType)
            {
                _ = builder.Append('<').Append(resultType).Append('>');
            }

            _ = builder.Append(' ');
        }

        _ = builder.Append(context.Method.Name).Append("(\n");
        context.Arguments.AppendDeclaration(builder, indent, context.Host);
    }

    private static string ModifiersOf(MethodAccessibility accessibility) =>
        accessibility switch
        {
            MethodAccessibility.Public => "public static",
            MethodAccessibility.Internal => "internal static",
            MethodAccessibility.Private => "private static",
        };

    // The type the task gives, or null for a task that gives nothing.
    private static string? GetResultType(in QueryMethodContext context) =>
        context.Method.Shape switch
        {
            ResultShape.Many => GetCollectionType(context.Method.Collection, context.Method.Output!.TypeText),
            ResultShape.One => context.Method.Output!.TypeText,
            ResultShape.OneOptional => context.Method.Output!.TypeText + "?",
            ResultShape.RowCount => "int",
            ResultShape.None => null,
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
            CollectionKind.IImmutableList => "global::System.Collections.Immutable.IImmutableList<" + row + ">",
        };

    // The buffer the rows are read into, and the expression that gives the collection from it.
    private static (string Buffer, string Result) GetBuffer(in QueryMethodContext context, string row)
    {
        var method = context.Method;
        var rows = context.Locals.Rows;
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
                or CollectionKind.IAsyncEnumerable => ".ToArray()",
            };
            return ("new " + RowBuffer.TypeText + "<" + row + ">()", rows + toResult);
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
            or CollectionKind.IAsyncEnumerable => (
                "new global::System.Collections.Generic.List<" + row + ">()",
                rows + ".ToArray()"
            ),
        };
    }

    // The receiver and the input are refused when null, and under validate-tokens each token's text when it is null
    // or white space.
    private static void AppendChecks(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        foreach (var argument in context.Arguments)
        {
            var check = argument.Role switch
            {
                ArgumentRole.Receiver or ArgumentRole.Input => "global::System.ArgumentNullException.ThrowIfNull(",
                ArgumentRole.Token => context.Method.ValidateTokens
                    ? "global::System.ArgumentException.ThrowIfNullOrWhiteSpace("
                    : null,
                ArgumentRole.Transaction or ArgumentRole.CommandTimeout or ArgumentRole.Cancellation => null,
            };
            if (check is not null)
            {
                _ = builder.Append(indent).Append(check);
                MethodArguments.AppendName(builder, argument.Name);
                _ = builder.Append(");\n");
            }
        }
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
        AppendScope(in context, indent, locals.CommandScope, locals.Command);
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
        AppendIf(builder, indent, MethodArguments.CommandTimeout + ".HasValue");
        _ = builder
            .Append(indent)
            .Append(GeneratedText.Indent)
            .Append(locals.Command)
            .Append(".CommandTimeout = ")
            .Append(MethodArguments.CommandTimeout)
            .Append(".Value;\n");
        AppendEnd(builder, indent);
        _ = builder.Append('\n');
    }

    private static void AppendExecution(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        var method = context.Method;
        var locals = context.Locals;
        if (method.Shape is ResultShape.None or ResultShape.RowCount)
        {
            _ = builder
                .Append(indent)
                .Append(method.Shape == ResultShape.RowCount ? "return await " : "await ")
                .Append(locals.Command)
                .Append(".ExecuteNonQueryAsync(")
                .Append(MethodArguments.CancellationToken)
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
            .Append(MethodArguments.CancellationToken)
            .Append(").ConfigureAwait(false);\n");
        AppendScope(in context, indent, locals.ReaderScope, locals.Reader);

        // A streaming method yields each row as it reads it, where any other reads them into its buffer and returns
        // the collection.
        if (context.Streams)
        {
            AppendStream(in context, indent, output);
            return;
        }

        // A method that checks for a rollback has the read, its drain and its returns in a try whose catch checks.
        if (!context.Capabilities.Has(ExecutionCapabilities.ChecksRollbackAtRead))
        {
            AppendRows(in context, indent, output);
            return;
        }

        _ = builder.Append(indent).Append("try\n").Append(indent).Append("{\n");
        AppendRows(in context, GeneratedText.Deeper(indent), output);
        AppendEnd(builder, indent);
        ExecutionCapabilityWriter.AppendRollbackCheck(in context, indent);
    }

    // The rows read into the buffer and the collection returned, or the one row read and returned, each return after
    // the drain when the capabilities have it.
    private static void AppendRows(in QueryMethodContext context, string indent, MethodOutput output)
    {
        var builder = context.Builder;
        var locals = context.Locals;
        var innerIndent = GeneratedText.Deeper(indent);
        if (context.Method.Shape == ResultShape.Many)
        {
            var (buffer, result) = GetBuffer(in context, output.TypeText);
            _ = builder.Append(indent).Append("var ").Append(locals.Rows).Append(" = ").Append(buffer).Append(";\n");
            _ = builder.Append(indent).Append("while (");
            AppendRead(in context);
            _ = builder.Append(")\n").Append(indent).Append("{\n");
            RowReaderWriter.AppendVectorChecks(in context, innerIndent, output);
            _ = builder.Append(innerIndent).Append(locals.Rows).Append(".Add(");
            RowReaderWriter.AppendRow(in context, output);
            _ = builder.Append(");\n");
            AppendEnd(builder, indent);
            _ = builder.Append('\n');
            AppendDrain(in context, indent);
            _ = builder.Append(indent).Append("return ").Append(result).Append(";\n");
            return;
        }

        _ = builder.Append(indent).Append("if (!");
        AppendRead(in context);
        _ = builder.Append(")\n").Append(indent).Append("{\n");
        if (context.Method.Shape == ResultShape.One)
        {
            AppendThrow(in context, innerIndent, moreThanOne: false);
        }
        else
        {
            AppendDrain(in context, innerIndent);
            _ = builder.Append(innerIndent).Append("return null;\n");
        }

        AppendEnd(builder, indent);
        _ = builder.Append('\n');
        RowReaderWriter.AppendVectorChecks(in context, indent, output);
        _ = builder.Append(indent).Append("var ").Append(locals.Row).Append(" = ");
        RowReaderWriter.AppendRow(in context, output);
        _ = builder.Append(";\n").Append(indent).Append("if (");
        AppendRead(in context);
        _ = builder.Append(")\n").Append(indent).Append("{\n");
        AppendThrow(in context, innerIndent, moreThanOne: true);
        AppendEnd(builder, indent);
        _ = builder.Append('\n');
        AppendDrain(in context, indent);
        _ = builder.Append(indent).Append("return ").Append(locals.Row).Append(";\n");
    }

    // The iterator's loop, which yields each row as it reads it.  A method that drains when its loop is left at a row
    // has the loop, and the drain after it, in a try whose finally drains then.  C# allows no catch on a try that holds
    // a yield return, so a method that checks for a rollback builds each row in a try of its own, whose catch checks
    // when the row's checks or construction throw; a read's exception is the database's or the caller's cancellation,
    // which the catch would throw as it is.  Either builds the row into a local before it yields it.
    private static void AppendStream(in QueryMethodContext context, string indent, MethodOutput output)
    {
        var builder = context.Builder;
        var locals = context.Locals;
        var tracksRow = context.Capabilities.Has(ExecutionCapabilities.DrainsWhenLeftAtARow);
        var checksRollback = context.Capabilities.Has(ExecutionCapabilities.ChecksRollbackAtRead);
        var loopIndent = tracksRow ? GeneratedText.Deeper(indent) : indent;
        var innerIndent = GeneratedText.Deeper(loopIndent);
        if (tracksRow)
        {
            ExecutionCapabilityWriter.AppendLeftAtARowStart(in context, indent);
        }

        _ = builder.Append(loopIndent).Append("while (");
        AppendRead(in context);
        _ = builder.Append(")\n").Append(loopIndent).Append("{\n");
        if (!tracksRow && !checksRollback)
        {
            RowReaderWriter.AppendVectorChecks(in context, innerIndent, output);
            _ = builder.Append(innerIndent).Append("yield return ");
            RowReaderWriter.AppendRow(in context, output);
            _ = builder.Append(";\n");
        }
        else
        {
            AppendRowIntoLocal(in context, innerIndent, output);
            _ = builder.Append('\n');
            if (tracksRow)
            {
                ExecutionCapabilityWriter.AppendYieldAtARow(in context, innerIndent);
            }
            else
            {
                _ = builder.Append(innerIndent).Append("yield return ").Append(locals.Row).Append(";\n");
            }
        }

        AppendEnd(builder, loopIndent);
        if (context.Capabilities.Has(ExecutionCapabilities.DrainsLaterResults))
        {
            ExecutionCapabilityWriter.AppendDrainAfterLoop(in context, loopIndent);
        }

        if (tracksRow)
        {
            ExecutionCapabilityWriter.AppendLeftAtARowEnd(in context, indent, loopIndent, innerIndent);
        }
    }

    // The row's local, declared, then its checks and its construction, in a try whose catch checks for a rollback when
    // the method has it.
    private static void AppendRowIntoLocal(in QueryMethodContext context, string indent, MethodOutput output)
    {
        var builder = context.Builder;
        var locals = context.Locals;
        var checksRollback = context.Capabilities.Has(ExecutionCapabilities.ChecksRollbackAtRead);
        _ = builder.Append(indent).Append(output.TypeText).Append(' ').Append(locals.Row).Append(";\n");
        var rowIndent = indent;
        if (checksRollback)
        {
            _ = builder.Append(indent).Append("try\n").Append(indent).Append("{\n");
            rowIndent = GeneratedText.Deeper(indent);
        }

        RowReaderWriter.AppendVectorChecks(in context, rowIndent, output);
        _ = builder.Append(rowIndent).Append(locals.Row).Append(" = ");
        RowReaderWriter.AppendRow(in context, output);
        _ = builder.Append(";\n");
        if (checksRollback)
        {
            AppendEnd(builder, indent);
            ExecutionCapabilityWriter.AppendRollbackCheck(in context, indent);
        }
    }

    // The drain before a buffered read returns, when the capabilities have it.
    private static void AppendDrain(in QueryMethodContext context, string indent)
    {
        if (context.Capabilities.Has(ExecutionCapabilities.DrainsLaterResults))
        {
            ExecutionCapabilityWriter.AppendDrainBeforeReturn(in context, indent);
        }
    }

    private static void AppendScope(in QueryMethodContext context, string indent, string scope, string disposable) =>
        _ = context
            .Builder.Append(indent)
            .Append("await using var ")
            .Append(scope)
            .Append(" = ")
            .Append(ConfigureAwaitOfDisposable)
            .Append(disposable)
            .Append(", false);\n");

    private static void AppendRead(in QueryMethodContext context) =>
        _ = context
            .Builder.Append("await ")
            .Append(context.Locals.Reader)
            .Append(".ReadAsync(")
            .Append(MethodArguments.CancellationToken)
            .Append(").ConfigureAwait(false)");

    private static void AppendThrow(in QueryMethodContext context, string indent, bool moreThanOne) =>
        _ = context
            .Builder.Append(indent)
            .Append("throw new ")
            .Append(ExceptionType)
            .Append('(')
            .Append(SymbolDisplay.FormatLiteral(context.Method.QueryName, quote: true))
            .Append(moreThanOne ? ", true);\n" : ", false);\n");

    internal static void AppendIf(StringBuilder builder, string indent, string condition) =>
        _ = builder.Append(indent).Append("if (").Append(condition).Append(")\n").Append(indent).Append("{\n");

    internal static void AppendEnd(StringBuilder builder, string indent) => _ = builder.Append(indent).Append("}\n");
}
