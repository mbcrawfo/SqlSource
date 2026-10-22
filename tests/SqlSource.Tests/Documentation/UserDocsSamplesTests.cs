using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Documentation;

// The SQL a user copies from a document must be SQL that SqlSource reads.  A block is marked `sql`, and the word after
// it says how it is read: a dialect (`sql postgres`, or `sql mysql,ansi-quotes`), `fragment` for a part of a file
// that is not one whole, or `error` for SQL that shows a mistake.  A block with no second word is `ansi`.  GitHub
// highlights by the first word alone, so each of them shows as SQL.
public sealed class UserDocsSamplesTests
{
    private const string Fragment = "fragment";
    private const string Error = "error";

    // A parser that finds no block would make the test below pass.
    [Fact]
    public void UserDocs_HaveSqlSamples() =>
        UserDocs
            .FencedBlocks()
            .Count(block => block.Info.StartsWith("sql", StringComparison.OrdinalIgnoreCase))
            .ShouldBeGreaterThan(0);

    [Fact]
    public void SqlSamples_Parse()
    {
        var problems = new List<string>();

        foreach (var (file, line, info, text) in UserDocs.FencedBlocks())
        {
            var words = info.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0 || !words[0].Equals("sql", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var kind = words.Length > 1 ? words[1] : "ansi";
            if (kind is Fragment or Error)
            {
                continue;
            }

            if (!SqlDialectName.TryParse(kind, out var dialect))
            {
                problems.Add($"{file}:{line}: '{kind}' is neither a dialect, 'fragment' nor 'error'");
                continue;
            }

            problems.AddRange(
                SqlFileParser
                    .Parse(text, "Example.sql", dialect)
                    .Errors.Select(error => Describe(file, line, text, error))
            );
        }

        problems.ShouldBeEmpty();
    }

    private static string Describe(string file, int fence, string text, SqlParseError error)
    {
        // The block's text starts on the line after its fence.
        var line = fence + 1 + text.Take(error.Span.Start).Count(character => character == '\n');
        var descriptor = SqlDiagnostics.ForParseError(error.Kind);
        object?[] arguments = [.. error.Arguments];
        var message = Diagnostic.Create(descriptor, Location.None, arguments).GetMessage(CultureInfo.InvariantCulture);
        return $"{file}:{line}: {descriptor.Id}: {message}";
    }
}
