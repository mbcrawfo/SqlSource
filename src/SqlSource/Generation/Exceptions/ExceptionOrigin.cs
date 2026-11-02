namespace SqlSource.Generation.Exceptions;

/// <summary>
/// Where a project's exceptions come from, as <see cref="ExceptionProbe" /> finds them.
/// </summary>
internal enum ExceptionOrigin
{
    /// <summary>
    /// No referenced assembly that the project sees has either: the project writes its own.
    /// </summary>
    Own,

    /// <summary>
    /// One referenced assembly gives both, with the constructors that the generated methods call: the project writes
    /// none, and its methods throw that assembly's.
    /// </summary>
    Referenced,

    /// <summary>
    /// Two or more referenced assemblies have one or both: <c>SQLSRC022</c>, and the project writes its own.
    /// </summary>
    SeenTwice,

    /// <summary>
    /// One referenced assembly has one without the other, or one without the constructor that the generated methods
    /// call: <c>SQLSRC023</c>, and the project writes its own.
    /// </summary>
    OtherShape,
}
