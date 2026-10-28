namespace SqlSource.Testing;

// The repository's root, only for starting a process in it, as the end-to-end CheckTests start the tool.  It is not a
// string, so that it does not reach Path.Combine by accident and become a read that RepositoryRoot.Path never checked.
internal readonly record struct RepositoryWorkingFolder
{
    internal RepositoryWorkingFolder(string path)
    {
        Path = path;
    }

    internal string Path { get; }
}
