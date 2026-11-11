namespace SqlSource.Generation.Emitting.QueryMethods;

/// <summary>
/// The names of what a collection method declares to run its items in batches, from its <see cref="LocalNames" />: the
/// enumerator, the place of the next item and of a chunk's first, the batch and its scope, the place of a result set,
/// the count of a rowcount, the transaction it owns, and the database's error and its command.  The item itself is
/// <see cref="QueryMethodLocals.Input" />, and each batch command is <see cref="QueryMethodLocals.Command" />, so that
/// the bindings and the lists read them as a query method's.  A query method has none of them.
/// </summary>
internal sealed class BatchLocals
{
    private BatchLocals(LocalNames names)
    {
        Items = names.Take("items");
        ItemIndex = names.Take("itemIndex");
        First = names.Take("first");
        More = names.Take("more");
        Batch = names.Take("batch");
        BatchScope = names.Take("batchScope");
        Result = names.Take("result");
        Affected = names.Take("affected");
        OwnsTransaction = names.Take("ownsTransaction");
        OwnTransaction = names.Take("ownTransaction");
        Error = names.Take("error");
        Failed = names.Take("failed");
    }

    /// <summary>The enumerator of the items, which the method walks once.</summary>
    public string Items { get; }

    /// <summary>The position of the next item to add, counted from 0: the number of items added.</summary>
    public string ItemIndex { get; }

    /// <summary>The position of the first item of the chunk being run.</summary>
    public string First { get; }

    /// <summary>Whether an item is left after the chunk that was run.</summary>
    public string More { get; }

    /// <summary>The chunk's batch.</summary>
    public string Batch { get; }

    /// <summary>The scope that disposes the batch before the next chunk.</summary>
    public string BatchScope { get; }

    /// <summary>The place in the chunk of the command whose result set is read, or whose count is added.</summary>
    public string Result { get; }

    /// <summary>The number of rows the items affected, which a rowcount method returns.</summary>
    public string Affected { get; }

    /// <summary>Whether the method begins a transaction: neither the caller's nor an ambient one is open.</summary>
    public string OwnsTransaction { get; }

    /// <summary>The transaction the method began, or null.</summary>
    public string OwnTransaction { get; }

    /// <summary>The database's error that the catch of a chunk takes.</summary>
    public string Error { get; }

    /// <summary>The batch command the database's error names.</summary>
    public string Failed { get; }

    /// <summary>
    /// The names of a collection method's locals, or null for a query method.
    /// </summary>
    public static BatchLocals? Of(bool takesItems, LocalNames names) => takesItems ? new BatchLocals(names) : null;
}
