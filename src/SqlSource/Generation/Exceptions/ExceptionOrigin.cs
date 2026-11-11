namespace SqlSource.Generation.Exceptions;

/// <summary>
/// Where a project's exceptions come from, as <see cref="ExceptionProbe" /> finds them.
/// </summary>
internal enum ExceptionOrigin
{
    /// <summary>
    /// No referenced assembly that the project sees has any of them: the project writes its own.
    /// </summary>
    Own,

    /// <summary>
    /// One referenced assembly gives all three, with the constructors that the generated methods call and the
    /// overrides that the project's <c>DbException</c> lets <c>CollectionItemException</c> have: the project writes
    /// none, and its methods throw that assembly's.
    /// </summary>
    Referenced,

    /// <summary>
    /// Two or more referenced assemblies have one or more of them: <c>SQLSRC022</c>, and the project writes its own.
    /// </summary>
    SeenTwice,

    /// <summary>
    /// One referenced assembly has some without the others, or one without the constructor that the generated methods
    /// call: <c>SQLSRC023</c>, and the project writes its own.
    /// </summary>
    OtherShape,

    /// <summary>
    /// One referenced assembly gives all three, but its <c>CollectionItemException</c> lacks an override of
    /// <c>SqlState</c>, <c>IsTransient</c> or <c>DbBatchCommand</c> that the project's <c>DbException</c> has, as a
    /// copy compiled for a framework before .NET 6 does: <c>SQLSRC038</c>, and the project writes none and uses that
    /// assembly's, so that it sees one copy and the error is the only one.
    /// </summary>
    BeforeNet6,
}
