using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SqlSource.Generation.Methods;

namespace SqlSource.Generation.Emitting.QueryMethods;

/// <summary>
/// The names of the locals of a query's method and of its lambdas' parameters, none of which an argument has, and of
/// the connection and the transaction the method runs its query on.  The locals that read rows are named only for a
/// method that reads them, whose output is given, the value of a converted parameter only for a method that has one,
/// the element of an array and its value only for a method that converts one by element, the iterator only for a
/// method that streams, the flag of a row being yielded only for one whose <see cref="ExecutionCapabilities" /> drain
/// when its loop is left at a row, and the exception its catch takes only for one whose capabilities check for a
/// rollback at its read.
/// </summary>
internal sealed class QueryMethodLocals
{
    public QueryMethodLocals(
        MethodArguments arguments,
        MethodInput? input,
        MethodOutput? output,
        bool streams,
        ExecutionCapabilities capabilities
    )
    {
        Connection = MethodArguments.Connection;
        Transaction = MethodArguments.Transaction;
        var names = new LocalNames(Seed(arguments));
        Sql = names.Take("sql");
        WasClosed = names.Take("wasClosed");
        Command = names.Take("command");
        CommandScope = names.Take("commandScope");
        Parameter = names.Take("parameter");
        var byElement = HasByElement(input, output);
        ConversionNames = new ConversionNames(
            MatchesAValue(input) ? names.Take("value") : string.Empty,
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
                if (!column.IsNullable && column.ReadText == RowReaderWriter.VectorType)
                {
                    Values[index] = names.Take("value" + index.ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        Span = names.Take("span");
        State = names.Take("state");
        Iterator = streams ? names.Take("ReadRows") : string.Empty;
        AtRow = capabilities.Has(ExecutionCapabilities.DrainsWhenLeftAtARow) ? names.Take("atRow") : string.Empty;
        Exception = capabilities.Has(ExecutionCapabilities.ChecksRollbackAtRead)
            ? names.Take("exception")
            : string.Empty;
    }

    // The connection and the transaction the body runs the query on, today the arguments themselves.  A receiver other
    // than the connection would take local names for them here, from the method's LocalNames, and copy into them once,
    // at the start of the method (docs/architecture/generator.md, "Adding a kind of method").
    public string Connection { get; }

    public string Transaction { get; }

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

    // The local function a streaming method returns, which reads its rows.
    public string Iterator { get; }

    // The flag that is true while the iterator is suspended at the yield of a row.
    public string AtRow { get; }

    // The exception that the catch of a method that checks for a rollback takes.
    public string Exception { get; }

    // Whether a parameter's binding matches its value as one that can be null: one bound through a converter, or an
    // array that its property holds as any sequence.
    private static bool MatchesAValue(MethodInput? input) =>
        input is not null
        && input.Parameters.Any(static parameter => parameter.Write is not null || parameter.IsArraySequence);

    private static bool HasByElement(MethodInput? input, MethodOutput? output) =>
        (input is not null && input.Parameters.Any(static parameter => parameter.Write is { IsByElement: true }))
        || (output is not null && output.Columns.Any(static column => column.Read is { IsByElement: true }));

    // The names of the method's arguments, which no local may take.
    private static IEnumerable<string> Seed(MethodArguments arguments)
    {
        foreach (var argument in arguments)
        {
            yield return argument.Name;
        }
    }
}
