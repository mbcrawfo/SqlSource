namespace SqlSource.Tool.Tests;

// What the tool writes, where many tests expect the same text.
internal static class Expected
{
    // The line that ends a diagnostic and says where its id is explained.  The anchor of a heading is its text in
    // lower case, and an id is "SQLSRC" followed by digits.
    public static string See(string id) =>
        "    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc" + id[6..] + "\n";
}
