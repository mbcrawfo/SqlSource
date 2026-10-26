using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Generation.Methods;
using SqlSource.Settings;
using Conversion = SqlSource.Generation.Methods.Conversion;

namespace SqlSource.Generation.Emitting;

/// <summary>
/// Writes the asynchronous method of a query: it opens the connection when it is closed, binds the parameters by
/// value, runs the query and reads its rows as the shape says.
/// </summary>
/// <remarks>
/// A token's argument can have any name that is not a fixed argument's, so every local, and the parameters of the
/// lambda that builds the SQL, take a name no argument has from one <see cref="LocalNames" />.  Every framework type
/// is written from <c>global::</c>, an extension method is called as a static method, and <c>string</c>, <c>int</c>
/// and <c>bool</c> are written as keywords.
/// </remarks>
internal static class QueryMethodWriter
{
    private const string Connection = "connection";

    private const string Parameters = "parameters";

    private const string Transaction = "transaction";

    private const string CommandTimeout = "commandTimeout";

    private const string CancellationToken = "cancellationToken";

    private const string ExceptionType = "global::SqlSource.UnexpectedRowCountException";

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
        var locals = new Locals(
            tokens,
            method.Input,
            method.Shape is ResultShape.None or ResultShape.RowCount ? null : method.Output
        );
        var bodyIndent = indent + GeneratedText.Indent;
        var blockIndent = bodyIndent + GeneratedText.Indent;

        AppendDocumentation(builder, indent, method, tokens);
        AppendSignature(builder, indent, method, modifiers, isExtension, arguments);
        _ = builder.Append(indent).Append("{\n");
        AppendChecks(builder, bodyIndent, method, arguments);
        AppendSql(builder, bodyIndent, method, tokens, arguments, locals);

        _ = builder
            .Append(bodyIndent)
            .Append("var ")
            .Append(locals.WasClosed)
            .Append(" = ")
            .Append(Connection)
            .Append(".State == global::System.Data.ConnectionState.Closed;\n");
        AppendIf(builder, bodyIndent, locals.WasClosed);
        _ = builder
            .Append(blockIndent)
            .Append("await ")
            .Append(Connection)
            .Append(".OpenAsync(")
            .Append(CancellationToken)
            .Append(").ConfigureAwait(false);\n");
        AppendEnd(builder, bodyIndent);
        _ = builder.Append('\n').Append(bodyIndent).Append("try\n").Append(bodyIndent).Append("{\n");

        AppendCommand(builder, blockIndent, locals, builtAtRunTime: tokens.Count > 0);
        if (method.Input is { } input)
        {
            var binder = ParameterBinder.For(method.Engine);
            foreach (var parameter in input.Parameters)
            {
                _ = builder.Append(blockIndent).Append("{\n");
                binder.Append(
                    builder,
                    blockIndent + GeneratedText.Indent,
                    locals.Command,
                    locals.Parameter,
                    Parameters,
                    parameter,
                    locals.ConversionNames
                );
                AppendEnd(builder, blockIndent);
                _ = builder.Append('\n');
            }
        }

        AppendExecution(builder, blockIndent, method, locals, rowBufferType);

        _ = builder
            .Append(bodyIndent)
            .Append("}\n")
            .Append(bodyIndent)
            .Append("finally\n")
            .Append(bodyIndent)
            .Append("{\n");
        AppendIf(builder, blockIndent, locals.WasClosed);
        _ = builder
            .Append(blockIndent)
            .Append(GeneratedText.Indent)
            .Append("await ")
            .Append(Connection)
            .Append(".CloseAsync().ConfigureAwait(false);\n");
        AppendEnd(builder, blockIndent);
        AppendEnd(builder, bodyIndent);
        AppendEnd(builder, indent);
    }

    private static void AppendDocumentation(
        StringBuilder builder,
        string indent,
        QueryMethod method,
        List<string> tokens
    )
    {
        XmlDocWriter.AppendMember(builder, indent, method.SummaryXml, MethodWriter.GetSql(method.Segments));
        XmlDocWriter.AppendParam(
            builder,
            indent,
            Connection,
            "The connection to run the query on.  It is opened when it is closed, and closed again when the method "
                + "opened it."
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
    }

    private static void AppendSignature(
        StringBuilder builder,
        string indent,
        QueryMethod method,
        string modifiers,
        bool isExtension,
        List<string> arguments
    )
    {
        var argumentIndent = indent + GeneratedText.Indent;
        _ = builder.Append(indent).Append(modifiers).Append(" async global::System.Threading.Tasks.Task");
        if (GetResultType(method) is { } resultType)
        {
            _ = builder.Append('<').Append(resultType).Append('>');
        }

        _ = builder
            .Append(' ')
            .Append(method.Name)
            .Append("(\n")
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
            .Append(" = null,\n")
            .Append(argumentIndent)
            .Append("int? ")
            .Append(CommandTimeout)
            .Append(" = null,\n")
            .Append(argumentIndent)
            .Append("global::System.Threading.CancellationToken ")
            .Append(CancellationToken)
            .Append(" = default)\n");
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
            return ("new " + rowBufferType + "<" + row + ">()", rows + ".ToArray()");
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
            CollectionKind.Array
            or CollectionKind.IEnumerable
            or CollectionKind.IReadOnlyCollection
            or CollectionKind.IReadOnlyList
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
        Locals locals
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
    private static void AppendCommand(StringBuilder builder, string indent, Locals locals, bool builtAtRunTime)
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
        Locals locals,
        string rowBufferType
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
        var innerIndent = indent + GeneratedText.Indent;
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

        if (method.Shape == ResultShape.Many)
        {
            var (buffer, result) = GetBuffer(method, output.TypeText, locals.Rows, rowBufferType);
            _ = builder.Append(indent).Append("var ").Append(locals.Rows).Append(" = ").Append(buffer).Append(";\n");
            _ = builder.Append(indent).Append("while (");
            AppendRead(builder, locals);
            _ = builder.Append(")\n").Append(indent).Append("{\n");
            AppendVectorChecks(builder, innerIndent, output, locals);
            _ = builder.Append(innerIndent).Append(locals.Rows).Append(".Add(");
            AppendRow(builder, output, locals);
            _ = builder.Append(");\n");
            AppendEnd(builder, indent);
            _ = builder.Append('\n').Append(indent).Append("return ").Append(result).Append(";\n");
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
            _ = builder.Append(innerIndent).Append("return null;\n");
        }

        AppendEnd(builder, indent);
        _ = builder.Append('\n');
        AppendVectorChecks(builder, indent, output, locals);
        _ = builder.Append(indent).Append("var ").Append(locals.Row).Append(" = ");
        AppendRow(builder, output, locals);
        _ = builder.Append(";\n").Append(indent).Append("if (");
        AppendRead(builder, locals);
        _ = builder.Append(")\n").Append(indent).Append("{\n");
        AppendThrow(builder, innerIndent, method.QueryName, moreThanOne: true);
        AppendEnd(builder, indent);
        _ = builder.Append('\n').Append(indent).Append("return ").Append(locals.Row).Append(";\n");
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

    private static void AppendRead(StringBuilder builder, Locals locals) =>
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

    // A not-null column read as SqlVector<float> is read into a local first, and its Null is refused as a NULL of
    // any other not-null column is.
    private static void AppendVectorChecks(StringBuilder builder, string indent, MethodOutput output, Locals locals)
    {
        for (var index = 0; index < output.Columns.Count; index++)
        {
            if (locals.Values[index] is not { } value)
            {
                continue;
            }

            var column = output.Columns[index];
            _ = builder
                .Append(indent)
                .Append("var ")
                .Append(value)
                .Append(" = ")
                .Append(locals.Reader)
                .Append(".GetFieldValue<")
                .Append(column.ReadText)
                .Append(">(")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .Append(");\n");
            AppendIf(builder, indent, value + ".IsNull");
            _ = builder
                .Append(indent)
                .Append(GeneratedText.Indent)
                .Append("throw new global::System.InvalidCastException(")
                .Append(SymbolDisplay.FormatLiteral("Column '" + column.Name + "' is null.", quote: true))
                .Append(");\n");
            AppendEnd(builder, indent);
            _ = builder.Append('\n');
        }
    }

    // A record by its positional constructor, its arguments in the order of its properties; a class by an object
    // initializer, in column order.
    private static void AppendRow(StringBuilder builder, MethodOutput output, Locals locals)
    {
        _ = builder.Append("new ").Append(output.TypeText);
        if (output.IsRecord)
        {
            _ = builder.Append('(');
            for (var index = 0; index < output.ConstructorOrder.Count; index++)
            {
                _ = builder.Append(index > 0 ? ", " : string.Empty);
                AppendColumn(builder, output, output.ConstructorOrder[index], locals);
            }

            _ = builder.Append(')');
            return;
        }

        _ = builder.Append(" { ");
        for (var index = 0; index < output.Columns.Count; index++)
        {
            _ = builder.Append(index > 0 ? ", " : string.Empty).Append(output.Columns[index].Property).Append(" = ");
            AppendColumn(builder, output, index, locals);
        }

        _ = builder.Append(" }");
    }

    // A not-null column by GetFieldValue<T> alone; a nullable one by IsDBNull first, and never by GetFieldValue<T?>,
    // which SqlClient refuses on a NULL.  A column with a converter is read as the converter's database side and
    // passed to it, so that the converter never sees a NULL; an array converted by element is read as the database's
    // array and each of its elements passed to it.
    private static void AppendColumn(StringBuilder builder, MethodOutput output, int ordinal, Locals locals)
    {
        var column = output.Columns[ordinal];
        var read = column.Read;
        if (locals.Values[ordinal] is { } value)
        {
            _ = read is null
                ? builder.Append(value)
                : builder.Append(read.Method).Append('(').Append(value).Append(')');
            return;
        }

        var ordinalText = ordinal.ToString(CultureInfo.InvariantCulture);
        if (column.IsNullable)
        {
            _ = builder
                .Append(locals.Reader)
                .Append(".IsDBNull(")
                .Append(ordinalText)
                .Append(") ? default(")
                .Append(column.TypeText)
                .Append("?) : ");
        }

        if (read is { IsByElement: true })
        {
            AppendByElement(builder, column, read, ordinalText, locals);
            return;
        }

        if (read is not null)
        {
            _ = builder.Append(read.Method).Append('(');
        }

        AppendGetFieldValue(builder, column, ordinalText, locals);
        if (read is not null)
        {
            _ = builder.Append(')');
        }
    }

    // Array.ConvertAll of the database's array, by a static lambda that converts each element that is not null.  A
    // NULL element of a value type throws, naming the column, which Npgsql's throw does not; one of a reference type,
    // or of an element the property writes nullable, stays null.
    private static void AppendByElement(
        StringBuilder builder,
        MethodColumn column,
        Conversion read,
        string ordinalText,
        Locals locals
    )
    {
        var names = locals.ConversionNames;
        _ = builder
            .Append("global::System.Array.ConvertAll<")
            .Append(read.ReadElementText)
            .Append(", ")
            .Append(read.ElementText)
            .Append(">(");
        AppendGetFieldValue(builder, column, ordinalText, locals);
        _ = builder
            .Append(", static ")
            .Append(names.Element)
            .Append(" => ")
            .Append(names.Element)
            .Append(" is { } ")
            .Append(names.Item)
            .Append(" ? ")
            .Append(read.Method)
            .Append('(')
            .Append(names.Item)
            .Append(") : ");
        if (read.ElementIsNullable)
        {
            _ = builder.Append("null");
        }
        else if (!read.ElementIsValueType)
        {
            _ = builder.Append("null!");
        }
        else
        {
            _ = builder
                .Append("throw new global::System.InvalidCastException(")
                .Append(SymbolDisplay.FormatLiteral("Column '" + column.Name + "' holds a null element.", quote: true))
                .Append(')');
        }

        _ = builder.Append(')');
    }

    private static void AppendGetFieldValue(
        StringBuilder builder,
        MethodColumn column,
        string ordinalText,
        Locals locals
    ) =>
        _ = builder
            .Append(locals.Reader)
            .Append(".GetFieldValue<")
            .Append(column.ReadText)
            .Append(">(")
            .Append(ordinalText)
            .Append(')');

    private static void AppendIf(StringBuilder builder, string indent, string condition) =>
        _ = builder.Append(indent).Append("if (").Append(condition).Append(")\n").Append(indent).Append("{\n");

    private static void AppendEnd(StringBuilder builder, string indent) => _ = builder.Append(indent).Append("}\n");

    // The names of the method's locals and of the lambdas' parameters, none of which an argument has.  The locals
    // that read rows are named only for a method that reads them, whose output is given, the value of a converted
    // parameter only for a method that has one, and the element of an array and its value only for a method that
    // converts one by element.
    private sealed class Locals
    {
        public Locals(List<string> tokens, MethodInput? input, MethodOutput? output)
        {
            var names = new LocalNames(Seed(tokens));
            Sql = names.Take("sql");
            WasClosed = names.Take("wasClosed");
            Command = names.Take("command");
            CommandScope = names.Take("commandScope");
            Parameter = names.Take("parameter");
            var byElement = HasByElement(input, output);
            ConversionNames = new ConversionNames(
                HasWrite(input) ? names.Take("value") : string.Empty,
                byElement ? names.Take("element") : string.Empty,
                byElement ? names.Take("item") : string.Empty
            );
            if (output is null)
            {
                Reader = ReaderScope = Rows = Row = string.Empty;
                Values = [];
            }
            else
            {
                Reader = names.Take("reader");
                ReaderScope = names.Take("readerScope");
                Rows = names.Take("rows");
                Row = names.Take("row");
                Values = new string?[output.Columns.Count];
                for (var index = 0; index < output.Columns.Count; index++)
                {
                    var column = output.Columns[index];
                    if (!column.IsNullable && column.ReadText == VectorType)
                    {
                        Values[index] = names.Take("value" + index.ToString(CultureInfo.InvariantCulture));
                    }
                }
            }

            Span = names.Take("span");
            State = names.Take("state");
        }

        public string Sql { get; }

        public string WasClosed { get; }

        public string Command { get; }

        public string CommandScope { get; }

        public string Parameter { get; }

        // The pattern variable that holds the value of a converted parameter in its binding, which is a block of its
        // own, so that one name serves every parameter; and the parameter of the static lambda that converts an
        // array's element, and the element that is not null, which no other lambda's parameter has.
        public ConversionNames ConversionNames { get; }

        public string Reader { get; }

        public string ReaderScope { get; }

        public string Rows { get; }

        public string Row { get; }

        // For each column, the local its value is read into before the row, or null when the row reads it.
        public string?[] Values { get; }

        public string Span { get; }

        public string State { get; }

        private static bool HasWrite(MethodInput? input) =>
            input is not null && input.Parameters.Any(static parameter => parameter.Write is not null);

        private static bool HasByElement(MethodInput? input, MethodOutput? output) =>
            (input is not null && input.Parameters.Any(static parameter => parameter.Write is { IsByElement: true }))
            || (output is not null && output.Columns.Any(static column => column.Read is { IsByElement: true }));

        private static IEnumerable<string> Seed(List<string> tokens)
        {
            yield return Connection;
            yield return Parameters;
            yield return Transaction;
            yield return CommandTimeout;
            yield return CancellationToken;
            foreach (var token in tokens)
            {
                yield return token;
            }
        }
    }
}
