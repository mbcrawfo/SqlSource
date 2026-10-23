using System;
using System.Text.RegularExpressions;
using Shouldly;
using SqlSource.Parsing.Tokens;
using Xunit;

namespace SqlSource.Tests.Parsing.Tokens;

public sealed partial class OpenDefaultFinderTests
{
    // The finder is made once for a series of offsets, and each answer is the rule's: the first opener of a default
    // after the last "}}" that ends at or before the offset.
    [Theory]
    [InlineData("{{f:a}} {{g:b }} c {{h:d ")]
    [InlineData("x}}}{{f:{{g:a}}}}{{ h :b}")]
    [InlineData("{{1:a {{f:b}}{{g:c")]
    [InlineData("{{f a {{ g\t: {{h:")]
    public void At_EveryOffsetInTurn_GivesTheRulesAnswer(string sql)
    {
        var finder = new OpenDefaultFinder(sql);

        for (var offset = 0; offset <= sql.Length; offset++)
        {
            finder.At(offset).ShouldBe(Expected(sql, offset), $"offset {offset}");
        }
    }

    [Fact]
    public void At_RepeatedAndSkippedOffsets_GivesTheRulesAnswer()
    {
        const string Sql = "{{f:a}} {{g:b }} c {{h:d ";
        var finder = new OpenDefaultFinder(Sql);

        foreach (var offset in new[] { 0, 3, 3, 7, 7, 15, 16, 25, 25 })
        {
            finder.At(offset).ShouldBe(Expected(Sql, offset), $"offset {offset}");
        }
    }

    [Fact]
    public void At_OffsetLessThanTheLast_Throws()
    {
        var finder = new OpenDefaultFinder("{{f:a ");
        _ = finder.At(5);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => finder.At(4));
    }

    [Theory]
    [InlineData("{{f:a}} {{g:b }} c {{h:d ")]
    [InlineData("x}}}{{f:{{g:a}}}}{{ h :b}")]
    public void FindOpenDefault_EveryOffset_GivesTheRulesAnswer(string sql)
    {
        for (var offset = 0; offset <= sql.Length; offset++)
        {
            TokenScanner.FindOpenDefault(sql, offset).ShouldBe(Expected(sql, offset), $"offset {offset}");
        }
    }

    // The rule, written out for ASCII names: search back for the last "}}" that ends at or before the offset, then
    // forward for the first opener that starts before it.
    private static string? Expected(string sql, int offset)
    {
        var bound = 0;
        for (var index = offset - 2; index >= 0; index--)
        {
            if (sql[index] == '}' && sql[index + 1] == '}')
            {
                bound = index + 2;
                break;
            }
        }

        for (var match = Opener().Match(sql, bound); match.Success; match = match.NextMatch())
        {
            if (match.Index < offset)
            {
                return match.Groups[1].Value;
            }
        }

        return null;
    }

    [GeneratedRegex(@"(?=\{\{[ \t]*([A-Za-z_][A-Za-z0-9_]*)[ \t]*:)", RegexOptions.CultureInvariant)]
    private static partial Regex Opener();
}
