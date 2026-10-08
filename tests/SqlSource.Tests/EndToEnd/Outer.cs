namespace SqlSource.Tests.EndToEnd;

internal static partial class Outer
{
    // A nested record struct, with a path that leaves its folder and comes back.
    [SqlSourceGenerate(Path = "Orders/../CountUsers.sql", SqlLocation = SqlLocation.Direct)]
    internal readonly partial record struct Counts;
}
