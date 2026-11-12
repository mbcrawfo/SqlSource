using SqlSource.Generation.Methods;

namespace SqlSource.Generation.Repositories;

/// <summary>
/// The connection of one database of a repository: the member that gives its asynchronous methods their connection,
/// and the one that gives its synchronous methods theirs, which are one member unless the database has a pair.
/// </summary>
/// <param name="Database">The database's name, as the first of its queries spells it.</param>
/// <param name="Asynchronous">
/// The member that gives its asynchronous methods their connection: its one source, or the one of a pair that returns a
/// task.
/// </param>
/// <param name="Synchronous">
/// The member that gives its synchronous methods their connection: its one source when it returns no task, or the one
/// of a pair that returns none.  Null when its one source returns a task, which a synchronous method cannot call.
/// </param>
internal sealed record ResolvedConnection(string Database, ConnectionSource Asynchronous, ConnectionSource? Synchronous)
{
    /// <summary>
    /// The member that gives a method of the synchrony its connection, or null for a synchronous method of a database
    /// whose one source returns a task.
    /// </summary>
    public ConnectionSource? SourceFor(MethodSynchrony synchrony) =>
        synchrony switch
        {
            MethodSynchrony.Asynchronous => Asynchronous,
            MethodSynchrony.Synchronous => Synchronous,
            _ => throw UnexpectedEnumValueException.Of(synchrony),
        };
}
