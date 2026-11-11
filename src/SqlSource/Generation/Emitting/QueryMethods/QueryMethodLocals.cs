using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SqlSource.Generation.Methods;
using SqlSource.Generation.Repositories;

namespace SqlSource.Generation.Emitting.QueryMethods;

/// <summary>
/// The names of the locals of a query's method and of its lambdas' parameters, none of which an argument has, and of
/// the connection and the transaction the method runs its query on: the arguments' own, or, for a repository's method,
/// a local of the connection, which the name its source's access starts with does not take either, and no transaction.
/// The locals that read rows are named only for a method that reads them, whose output is given, the value of a
/// converted parameter, or of an array its property holds as any sequence, only for a method that has one, the element
/// of an array and its value only for a method that converts one by element, the iterator only for a method that
/// streams, the flag of a row being yielded only for one whose <see cref="ExecutionCapabilities" /> drain when its loop
/// is left at a row, the exception its catch takes only for one whose capabilities check for a rollback at its read or
/// after a database error, the flag of a later error only for one whose capabilities check after a database error, the
/// result of a read only for one of those that streams, the names of its lists only for a method that has one, and the
/// item and the names of its batches only for a collection method.
/// </summary>
internal sealed class QueryMethodLocals
{
    // How the access of an instance member starts.
    private const string This = "this.";

    /// <summary>
    /// Takes the name of each local the method declares, for what it has.
    /// </summary>
    /// <param name="arguments">The method's arguments, whose names no local takes.</param>
    /// <param name="input">The input model, or null when the query has no parameters.</param>
    /// <param name="output">The output model of a method that reads rows, or null.</param>
    /// <param name="streams">Whether the method streams its rows.</param>
    /// <param name="capabilities">What the engine needs of the method beyond its command and its rows.</param>
    /// <param name="source">The member that gives a repository's method its connection, or null.</param>
    /// <param name="takesItems">
    /// Whether the method is a collection method, which reads its parameters from each item.  A flag, as
    /// <paramref name="streams" /> is, since the locals are built from values and not from the method.
    /// </param>
    public QueryMethodLocals(
        MethodArguments arguments,
        MethodInput? input,
        MethodOutput? output,
        bool streams,
        ExecutionCapabilities capabilities,
        ConnectionSource? source,
        bool takesItems = false
    )
    {
        var names = new LocalNames(Seed(arguments, source));
        Input = takesItems ? names.Take("item") : MethodArguments.Parameters;
        if (arguments.TakesConnection)
        {
            Connection = MethodArguments.Connection;
            Transaction = MethodArguments.Transaction;
        }
        else
        {
            Connection = names.Take("connection");
            Transaction = string.Empty;
        }

        ConnectionScope = source is null or { Kind: ConnectionSourceKind.Borrowed }
            ? string.Empty
            : names.Take("connectionScope");
        (SourceLocal, SourceValue, SourceAccess) = SourceOf(source, streams, names);

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
        Exception =
            capabilities.Has(ExecutionCapabilities.ChecksRollbackAtRead)
            || capabilities.Has(ExecutionCapabilities.ChecksRollbackAfterDatabaseError)
                ? names.Take("exception")
                : string.Empty;
        RaisedLater = capabilities.Has(ExecutionCapabilities.ChecksRollbackAfterDatabaseError)
            ? names.Take("raisedLater")
            : string.Empty;
        HasRow =
            streams && capabilities.Has(ExecutionCapabilities.ChecksRollbackAfterDatabaseError)
                ? names.Take("hasRow")
                : string.Empty;
        Lists = ListLocals.Of(input, names);
        Batch = BatchLocals.Of(takesItems, names);
    }

    // What the bindings and the lists read the query's parameters from: the argument for a query method, and for a
    // collection method the local that holds the item being run, whose name no argument or other local has.
    public string Input { get; }

    // The connection the body runs the query on: the argument, or the local a repository's method gets it into from its
    // source (docs/architecture/generator.md, "Adding a kind of method").
    public string Connection { get; }

    // The transaction the body runs the query in: the argument, or empty for a repository's method, which takes none.
    public string Transaction { get; }

    // The scope that disposes the connection a repository's method made, or empty when it borrows one or takes it.
    public string ConnectionScope { get; }

    // The local that a struct repository's streaming method declares before its iterator, since a local function of a
    // struct reaches neither this nor a primary-constructor parameter: the source's value, or a copy of the struct for
    // an instance method, through which overloads resolve as written.  Empty, as SourceValue is, for any other method.
    public string SourceLocal { get; }

    // What SourceLocal is set to: the source's access, or this.
    public string SourceValue { get; }

    // The source as the body reaches it: its access, or through SourceLocal.  Empty for a method that takes its
    // connection.
    public string SourceAccess { get; }

    public string Sql { get; }

    public string WasClosed { get; }

    public string Command { get; }

    public string CommandScope { get; }

    public string Parameter { get; }

    // The pattern variable that holds the value of a converted parameter, or of an array that its property holds as
    // any sequence, in its binding, which is a block of its own, so that one name serves every parameter; and the
    // parameter of the static lambda that converts an array's element, and the element that is not null, which no
    // other lambda's parameter has.
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

    // The flag that is true when the rest of the batch, read after a database error, raised an error of its own.
    public string RaisedLater { get; }

    // The result of a streamed read, which the read assigns in a try of its own, as the loop cannot read in its
    // condition when a catch is to follow the read.
    public string HasRow { get; }

    // The names of what the method declares for its lists, or null when it has none.
    public ListLocals? Lists { get; }

    // The names of what a collection method declares to run its items in batches, or null for a query method.
    public BatchLocals? Batch { get; }

    // The local of a struct repository's streaming method, its value, and the source as the body reaches it.  A static
    // method is reached through its type, which the iterator can reach.
    private static (string Local, string Value, string Access) SourceOf(
        ConnectionSource? source,
        bool streams,
        LocalNames names
    )
    {
        if (source is null)
        {
            return (string.Empty, string.Empty, string.Empty);
        }

        if (!streams || !source.InStruct)
        {
            return (string.Empty, string.Empty, source.AccessText);
        }

        if (source.Member != CandidateMember.Method)
        {
            var local = names.Take("source");
            return (local, source.AccessText, local);
        }

        // An instance member's access is this, a dot and its name; a static one's starts with its type.
        if (!source.AccessText.StartsWith(This, StringComparison.Ordinal))
        {
            return (string.Empty, string.Empty, source.AccessText);
        }

        var self = names.Take("self");
        return (self, "this", self + source.AccessText.Substring(This.Length - 1));
    }

    // Whether a parameter's binding matches its value as one that can be null: one bound through a converter, or an
    // array that its property holds as any sequence.
    private static bool MatchesAValue(MethodInput? input) =>
        input is not null
        && input.Parameters.Any(static parameter => parameter.Write is not null || parameter.IsArraySequence);

    private static bool HasByElement(MethodInput? input, MethodOutput? output) =>
        (input is not null && input.Parameters.Any(static parameter => parameter.Write is { IsByElement: true }))
        || (output is not null && output.Columns.Any(static column => column.Read is { IsByElement: true }));

    // The names of the method's arguments, and the name its source's access starts with, which no local may take.
    private static IEnumerable<string> Seed(MethodArguments arguments, ConnectionSource? source)
    {
        foreach (var argument in arguments)
        {
            yield return argument.Name;
        }

        if (source?.LeadingName is { } leadingName)
        {
            yield return leadingName;
        }
    }
}
