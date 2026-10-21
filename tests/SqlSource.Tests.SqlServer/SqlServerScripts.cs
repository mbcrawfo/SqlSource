using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace SqlSource.Tests.SqlServer;

// Applies a script of several batches, as sqlcmd would, through SqlClient: a batch ends at a line that holds GO
// alone, ignoring case and the white space around it.  SqlClient runs each batch with QUOTED_IDENTIFIER on, where
// sqlcmd has it off and refuses a persisted computed column.
internal static partial class SqlServerScripts
{
    // The batches of the script in order, each with its line endings, and none that holds only white space.  GO with
    // a count, which sqlcmd runs that many times, is refused rather than run once.
    public static IReadOnlyList<string> Batches(string script)
    {
        var batches = new List<string>();
        var batch = new StringBuilder();
        var number = 0;
        foreach (var line in Lines(script))
        {
            number++;
            var text = line.Trim();
            if (text.Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                Add(batches, batch);
                continue;
            }

            if (GoWithACount().IsMatch(text))
            {
                throw new InvalidOperationException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Line {number} of the script is GO with a count, and a count is not supported."
                    )
                );
            }

            _ = batch.Append(line);
        }

        Add(batches, batch);
        return batches;
    }

    // Runs each batch of the file at that path as one command, in order, on the open connection.
    public static async Task RunAsync(SqlConnection connection, string path, CancellationToken token)
    {
        foreach (var batch in Batches(await File.ReadAllTextAsync(path, token)))
        {
            await using var command = new SqlCommand(batch, connection);
            _ = await command.ExecuteNonQueryAsync(token);
        }
    }

    // The lines of the script, each with the line feed that ends it.
    private static IEnumerable<string> Lines(string script)
    {
        var start = 0;
        while (start < script.Length)
        {
            var end = script.IndexOf('\n', start);
            var next = end < 0 ? script.Length : end + 1;
            yield return script[start..next];
            start = next;
        }
    }

    private static void Add(List<string> batches, StringBuilder batch)
    {
        var text = batch.ToString();
        if (!string.IsNullOrWhiteSpace(text))
        {
            batches.Add(text);
        }

        _ = batch.Clear();
    }

    [GeneratedRegex(@"^GO\s+\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GoWithACount();
}
