using System;
using System.Collections.Generic;
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
            (finder.At(offset), finder.OpenerStart).ShouldBe(Expected(sql, offset), $"offset {offset}");
        }
    }

    [Fact]
    public void At_RepeatedAndSkippedOffsets_GivesTheRulesAnswer()
    {
        const string Sql = "{{f:a}} {{g:b }} c {{h:d ";
        var finder = new OpenDefaultFinder(Sql);

        foreach (var offset in new[] { 0, 3, 3, 7, 7, 15, 16, 25, 25 })
        {
            (finder.At(offset), finder.OpenerStart).ShouldBe(Expected(Sql, offset), $"offset {offset}");
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
    [InlineData("{{f:a ", 6, "f", 0)]
    [InlineData("{{f:a}} ", 8, null, -1)]
    [InlineData("{{1:a ", 6, null, -1)]
    [InlineData("{{ :a ", 6, null, -1)]
    [InlineData("{{f a ", 6, null, -1)]
    [InlineData("{{f:{{g:a ", 10, "f", 0)]
    [InlineData("{{ f :a ", 8, "f", 0)]
    [InlineData("{{f:a}} {{g:b ", 14, "g", 8)]
    [InlineData("{{f:a}} {{g:b ", 8, null, -1)]
    [InlineData("{{1:x {{g:b ", 12, "g", 6)]
    [InlineData("{{f:a ", 0, null, -1)]
    [InlineData("", 0, null, -1)]
    public void At_Offset_GivesTheTokenWhoseDefaultIsOpenThereAndWhereItsOpenerStarts(
        string sql,
        int offset,
        string? expected,
        int openerStart
    )
    {
        var finder = new OpenDefaultFinder(sql);

        finder.At(offset).ShouldBe(expected);
        finder.OpenerStart.ShouldBe(openerStart);
    }

    // The scanner reads an ignored token to its closing braces as it does any other, so its default is open too.
    [Fact]
    public void At_OpenerOfAnIgnoredName_GivesThatName()
    {
        TokenScanner.Scan("{{f:x {{g:a}}", new HashSet<string> { "f" }).Occurrences.ShouldBeEmpty();
        new OpenDefaultFinder("{{f:x {{g:a ").At(12).ShouldBe("f");
    }

    [Theory]
    [InlineData("{{f:a}} {{g:b }} c {{h:d ")]
    [InlineData("x}}}{{f:{{g:a}}}}{{ h :b}")]
    public void At_NewFinderAtEachOffset_GivesTheRulesAnswer(string sql)
    {
        for (var offset = 0; offset <= sql.Length; offset++)
        {
            new OpenDefaultFinder(sql).At(offset).ShouldBe(Expected(sql, offset).Name, $"offset {offset}");
        }
    }

    // The rule, written out for ASCII names: search back for the last "}}" that ends at or before the offset, then
    // forward for the first opener that starts before it.
    private static (string? Name, int OpenerStart) Expected(string sql, int offset)
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
                return (match.Groups[1].Value, match.Index);
            }
        }

        return (null, -1);
    }

    [GeneratedRegex(@"(?=\{\{[ \t]*([A-Za-z_][A-Za-z0-9_]*)[ \t]*:)", RegexOptions.CultureInvariant)]
    private static partial Regex Opener();
}
