namespace SqlSource.SettingsFiles;

/// <summary>
/// The problems of a settings file.  Each member says what <see cref="SettingsFileError.Span" />,
/// <see cref="SettingsFileError.Setting" />, <see cref="SettingsFileError.Argument" /> and
/// <see cref="SettingsFileError.Other" /> hold for it; a field it does not name is null.
/// </summary>
internal enum SettingsFileErrorKind
{
    /// <summary>
    /// The text is not JSON with comments and trailing commas, or is nested deeper than
    /// <see cref="Snapshot.Reading.SidecarTokenizer.MaxDepth" /> levels.  It is the file's one error.  Span: the
    /// character or token that breaks the grammar, or the bracket that opens level 65; empty at the end of a text cut
    /// short.
    /// </summary>
    InvalidJson,

    /// <summary>The top level is JSON but not an object.  It is the file's one error.  Span: the whole value.</summary>
    NotAnObject,

    /// <summary>
    /// A key of the top level is not one of <see cref="SettingsFileKeys.All" />.  Span: the key, quotes included.
    /// Argument: the key.
    /// </summary>
    UnknownKey,

    /// <summary>
    /// A key of the top level is given a second time; the first applies.  Span: the second key, quotes included.
    /// Argument: the key.
    /// </summary>
    DuplicateKey,

    /// <summary>
    /// A value, or an element or a member's value, is not of the JSON type its key needs; <c>null</c> never is.
    /// Span: the whole value, or the element or the member's value.  Setting: the key of the top level.
    /// </summary>
    WrongType,

    /// <summary>
    /// A string, or an element or a member, is not valid for its setting.  Span: the string, quotes included; for a
    /// member, from its key's opening quote to its value's closing quote.  Setting: the key of the top level.
    /// Argument: the value trimmed; for a member, its key trimmed.  Other: for a member, its value trimmed.
    /// </summary>
    InvalidValue,

    /// <summary>
    /// A member has the key of an earlier member of its object, by the identity of its setting's map; the first
    /// applies.  Span: the repeated member's key, quotes included.  Setting: the key of the top level.  Argument: that
    /// key trimmed.  Other: the first member's key trimmed.
    /// </summary>
    RepeatedKey,
}
