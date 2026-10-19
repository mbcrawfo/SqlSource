namespace SqlSource.Generation.Types;

/// <summary>
/// One claimed file's input to its types beside the types it gave, so that a step after the types reads the file's
/// usable entries and parsed queries without typing them again.
/// </summary>
/// <param name="Input">The file with everything its types read.</param>
/// <param name="Types">What <see cref="QueryTyper.Type" /> gave for <paramref name="Input" />.</param>
internal sealed record TypedFile(FileTypesInput Input, FileTypes Types);
