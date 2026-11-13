using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SqlSource.MutationReport;

// A list that the test review keeps as a file: a value to a line, where '#' starts a comment and a line with no
// value is skipped.
internal static class ListFile
{
    public static async Task<string[]> ReadAsync(string path) =>
        [
            .. (await File.ReadAllLinesAsync(path).ConfigureAwait(false))
                .Select(line => line.Split('#')[0].Trim().TrimEnd('/'))
                .Where(line => line.Length > 0),
        ];
}
