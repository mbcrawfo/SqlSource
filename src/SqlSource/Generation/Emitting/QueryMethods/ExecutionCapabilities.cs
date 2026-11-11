using System;

namespace SqlSource.Generation.Emitting.QueryMethods;

/// <summary>
/// The behaviours of a query's method beyond running its command and reading its rows, each named for what it does
/// rather than for the engine that needs it.  <see cref="ExecutionCapabilityRules.Of" /> decides them for a method, in
/// the output step, and <see cref="ExecutionCapabilityWriter" /> writes the statements each turns on.  The writer asks
/// a flag, never an engine; see <c>docs/architecture/generator.md</c>, "Adding a database engine".
/// </summary>
[Flags]
internal enum ExecutionCapabilities
{
    /// <summary>No behaviour beyond the command and its rows.</summary>
    None = 0,

    /// <summary>The method refuses a transaction that has completed before it runs anything.</summary>
    RefusesCompletedTransaction = 1,

    /// <summary>Every path that returns rows reads the later results of the batch first.</summary>
    DrainsLaterResults = 2,

    /// <summary>
    /// The reading of rows is in a <c>try</c> whose <c>catch</c>, for an exception of the method's own, reports a
    /// transaction that the rest of the batch rolled back.
    /// </summary>
    ChecksRollbackAtRead = 4,

    /// <summary>
    /// A streaming method drains the later results in a <c>finally</c> when its loop is left at a row.
    /// </summary>
    DrainsWhenLeftAtARow = 8,

    /// <summary>
    /// The reading of rows is in a <c>try</c> whose <c>catch</c>, for the database's error, reads the rest of the
    /// batch and reports a transaction that a later error there rolled back.
    /// </summary>
    ChecksRollbackAfterDatabaseError = 16,

    /// <summary>
    /// A collection method's chunk takes a database error that names none of its commands as the error of the first
    /// command that did not complete.
    /// </summary>
    FindsFailedItemByCompletion = 32,
}
