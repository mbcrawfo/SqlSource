namespace SqlSource.Snapshot;

/// <summary>What a <see cref="SidecarToken" /> is.</summary>
internal enum SidecarTokenKind
{
    /// <summary>The end of the text.  Its span is empty.</summary>
    End,

    /// <summary>
    /// Text that is not JSON.  Its span is the character that cannot be read, or empty at the end of a text that
    /// stops inside a token.
    /// </summary>
    Invalid,

    ObjectStart,
    ObjectEnd,
    ArrayStart,
    ArrayEnd,
    Colon,
    Comma,

    /// <summary>A string.  Its span includes the quotes.</summary>
    String,

    Number,
    True,
    False,
    Null,
}
