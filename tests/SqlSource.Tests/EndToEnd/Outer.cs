namespace SqlSource.Tests.EndToEnd;

internal static partial class Outer
{
    // A nested record struct, with a path that leaves its folder and comes back.
    [SqlSourceGenerate(Source = "Orders/../CountUsers.sql")]
    internal readonly partial record struct Counts;
}
