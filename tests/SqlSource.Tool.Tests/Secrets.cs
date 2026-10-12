namespace SqlSource.Tool.Tests;

// The values that stand for a secret.  A test searches what the tool wrote for one of them, so none is a word the
// tool writes itself, and each differs from the others where a test tells them apart.
internal static class Secrets
{
    // A secret of one word.
    public const string Word = "s3cret";

    // A word that no other value of a test holds, for a search of everything a run wrote.
    public const string Marker = "s3cret-marker";

    // The connection of the databases of DescribeScene.
    public const string Connection = "Host=db;Password=hunter2";

    public const string ConnectionWithWord = "Host=db;Password=" + Word;

    public const string ConnectionWithMarker = "Host=db;Password=" + Marker;
}
