namespace SqlSource.Generation;

/// <summary>
/// A type with its own parsed files: the whole input of <see cref="TypeEmitter" />.  It compares equal until one of
/// the type's files or the type changes, so a change to another type's file does not emit this one again.
/// </summary>
/// <param name="Type">The type and the paths of its files.</param>
/// <param name="Files">The type's files, in member order.</param>
/// <param name="HintName">The name of the type's generated file.  Unique, ignoring case, in the compilation.</param>
internal sealed record TypeQueries(TypeFiles Type, EquatableArray<ParsedSqlFile> Files, string HintName);
