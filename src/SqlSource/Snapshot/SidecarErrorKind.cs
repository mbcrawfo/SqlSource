namespace SqlSource.Snapshot;

/// <summary>
/// The ways a sidecar is malformed.  Each member says what <see cref="SidecarError.Span" /> and
/// <see cref="SidecarError.Argument" /> hold for it.
/// </summary>
internal enum SidecarErrorKind
{
    /// <summary>
    /// The text is not one JSON object, or is nested deeper than <see cref="SidecarTokenizer.MaxDepth" /> levels.
    /// Span: the character that cannot be read or the token that breaks the grammar, empty at the end of a text that
    /// stops early.  No argument.
    /// </summary>
    InvalidJson,

    /// <summary>
    /// A key that a reader cannot do without is absent.  Span: the opening brace of the object.  Argument: the key.
    /// </summary>
    MissingKey,

    /// <summary>
    /// A key the reader reads holds a value of another JSON type, a number that is not a 32-bit integer included.
    /// Span: the whole value.  Argument: the key; the query's name for an entry; the array's key for an element.
    /// </summary>
    WrongType,

    /// <summary>
    /// A key the reader reads, or the name of a query, is written twice in one object.  Span: the second key, quotes
    /// included.  Argument: the key.
    /// </summary>
    DuplicateKey,

    /// <summary>
    /// An <c>ordinal</c> is not the index of its element.  Span: the number.  Argument: the number as written.
    /// </summary>
    OrdinalMismatch,

    /// <summary>
    /// <c>columns</c> is present and <c>resultKind</c> is <c>none</c>.  Span: the <c>columns</c> key, quotes
    /// included.  No argument.
    /// </summary>
    ColumnsWithoutRows,

    /// <summary>
    /// <c>resultKind</c> is not <c>rows</c> or <c>none</c>.  Span: the string, quotes included.  Argument: the
    /// value.
    /// </summary>
    UnknownResultKind,
}
