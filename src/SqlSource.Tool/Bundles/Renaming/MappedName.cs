namespace SqlSource.Tool.Bundles.Renaming;

/// <summary>One line of the mapping file: a replacement, the class it was given and the name it stands for.</summary>
internal sealed record MappedName(string Replacement, NameClass Class, string Original);
