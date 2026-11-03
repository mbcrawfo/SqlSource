using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Tool.DotEnv;
using Xunit;

namespace SqlSource.Tool.Tests.DotEnv;

public sealed class DotEnvParserTests
{
    private const string Secret = "hunter2";

    // Each case names its rule; the pairs are the expected keys and values, in turn.
    public static TheoryData<string, string, string[]> Syntax =>
        new()
        {
            {
                "unquoted value keeps a # with no white space before it",
                "SQLSOURCE_CONNECTION_A=Host=x;Password=a#b",
                ["SQLSOURCE_CONNECTION_A", "Host=x;Password=a#b"]
            },
            {
                "unquoted value ends at a # that follows white space",
                "SQLSOURCE_CONNECTION_A=Host=x # the dev box",
                ["SQLSOURCE_CONNECTION_A", "Host=x"]
            },
            {
                "unquoted value is trimmed",
                "SQLSOURCE_CONNECTION_A=  Host=x \t ",
                ["SQLSOURCE_CONNECTION_A", "Host=x"]
            },
            {
                "unquoted value that is only a comment is empty",
                "SQLSOURCE_CONNECTION_A= # nothing",
                ["SQLSOURCE_CONNECTION_A", ""]
            },
            {
                "an equals sign after the first belongs to the value",
                "SQLSOURCE_CONNECTION_A=a=b=c",
                ["SQLSOURCE_CONNECTION_A", "a=b=c"]
            },
            {
                "export and spaces around the equals sign are dropped, single quotes take no escapes",
                "export  SQLSOURCE_CONNECTION_A = 'a \\n b'",
                ["SQLSOURCE_CONNECTION_A", "a \\n b"]
            },
            { "export with a tab is dropped", "export\tSQLSOURCE_CONNECTION_A=x", ["SQLSOURCE_CONNECTION_A", "x"] },
            {
                "double quotes take the escapes of .env",
                "SQLSOURCE_CONNECTION_A=\"a\\tb\\\"c\"",
                ["SQLSOURCE_CONNECTION_A", "a\tb\"c"]
            },
            {
                "double quotes take backslash, newline and return",
                "SQLSOURCE_CONNECTION_A=\"\\\\\\n\\r\"",
                ["SQLSOURCE_CONNECTION_A", "\\\n\r"]
            },
            {
                "double quoted value keeps a # and a ;",
                "SQLSOURCE_CONNECTION_A=\"Host=x;Password=a #b\"",
                ["SQLSOURCE_CONNECTION_A", "Host=x;Password=a #b"]
            },
            {
                "single quoted value keeps a # and a double quote",
                "SQLSOURCE_CONNECTION_A='a # \"b'",
                ["SQLSOURCE_CONNECTION_A", "a # \"b"]
            },
            {
                "white space then a comment may follow a closing quote",
                "SQLSOURCE_CONNECTION_A='x'  # dev",
                ["SQLSOURCE_CONNECTION_A", "x"]
            },
            {
                "white space may follow a closing quote",
                "SQLSOURCE_CONNECTION_A=\"x\"  ",
                ["SQLSOURCE_CONNECTION_A", "x"]
            },
            {
                "a byte order mark, a carriage return and no final break, the last of a key wins",
                "﻿SQLSOURCE_CONNECTION_A=1\r\nSQLSOURCE_CONNECTION_A=2",
                ["SQLSOURCE_CONNECTION_A", "2"]
            },
            {
                "only the tool's keys are kept, in the case written",
                "SQLSOURCE_CONNECTION=x\nOTHER=y\nsqlsource_connection_a=z",
                ["SQLSOURCE_CONNECTION", "x"]
            },
            { "a key that only starts with the unnamed name is not a tool key", "SQLSOURCE_CONNECTIONX=x", [] },
            {
                "the unnamed key and a named key are both kept",
                "SQLSOURCE_CONNECTION=x\nSQLSOURCE_CONNECTION_B_2=y",
                ["SQLSOURCE_CONNECTION", "x", "SQLSOURCE_CONNECTION_B_2", "y"]
            },
            { "a line that cannot be read and is not a tool line is passed over", "OTHER=\"unclosed\nNOT A LINE", [] },
            { "a line that has no equals sign and is not a tool key is passed over", "NOT_A_TOOL_KEY\nexport", [] },
            {
                "a line with a bad value is passed over when the key is not a tool key",
                "OTHER='unclosed\nOTHER2=\"a\\qb\"\nOTHER3='a'b",
                []
            },
            {
                "blank lines and comment lines are passed over",
                "\n   \n# SQLSOURCE_CONNECTION_A=x\n  # y\nSQLSOURCE_CONNECTION_B=z",
                ["SQLSOURCE_CONNECTION_B", "z"]
            },
            { "an empty value is the empty string", "SQLSOURCE_CONNECTION_A=", ["SQLSOURCE_CONNECTION_A", ""] },
            {
                "an empty quoted value is the empty string",
                "SQLSOURCE_CONNECTION_A=\"\"",
                ["SQLSOURCE_CONNECTION_A", ""]
            },
            { "an empty text has no values", "", [] },
            { "export is a key when no white space follows it", "export=x", [] },
        };

    [Theory]
    [MemberData(nameof(Syntax))]
    public void Parse_Text_HoldsTheValuesOfTheToolsKeys(string rule, string text, string[] pairs)
    {
        var contents = DotEnvParser.Parse(text);

        contents.Errors.ShouldBeEmpty(rule);
        var expected = Enumerable.Range(0, pairs.Length / 2).Select(i => (pairs[2 * i], pairs[(2 * i) + 1]));
        Sorted(contents).ShouldBe(expected.OrderBy(pair => pair.Item1, StringComparer.Ordinal), rule);
    }

    private static IEnumerable<(string Key, string Value)> Sorted(DotEnvContents contents) =>
        contents.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => (pair.Key, pair.Value));

    // Each case: the text, then the error's line, column, key and reason.
    public static TheoryData<string, int, int, string, string> Failures =>
        new()
        {
            { "  SQLSOURCE_CONNECTION_A=\"Password=hunter2", 0, 2, "SQLSOURCE_CONNECTION_A", "a quote is not closed" },
            { "SQLSOURCE_CONNECTION_A='Password=hunter2", 0, 0, "SQLSOURCE_CONNECTION_A", "a quote is not closed" },
            { "SQLSOURCE_CONNECTION_A=\"Password=hunter2\\", 0, 0, "SQLSOURCE_CONNECTION_A", "a quote is not closed" },
            {
                "# c\nexport SQLSOURCE_CONNECTION=\"Password=hunter2\" tail",
                1,
                7,
                "SQLSOURCE_CONNECTION",
                "text follows the closing quote"
            },
            {
                "SQLSOURCE_CONNECTION_A='Password=hunter2'#c",
                0,
                0,
                "SQLSOURCE_CONNECTION_A",
                "text follows the closing quote"
            },
            {
                "SQLSOURCE_CONNECTION_A=\"Password=hunter2\" x # c",
                0,
                0,
                "SQLSOURCE_CONNECTION_A",
                "text follows the closing quote"
            },
            {
                "SQLSOURCE_CONNECTION_A=\"Password=hunter2\\q\"",
                0,
                0,
                "SQLSOURCE_CONNECTION_A",
                "a backslash starts no escape that .env takes"
            },
            { "SQLSOURCE_CONNECTION_A", 0, 0, "SQLSOURCE_CONNECTION_A", "it has no '='" },
            { "ok\r\n  export SQLSOURCE_CONNECTION  \r\n", 1, 9, "SQLSOURCE_CONNECTION", "it has no '='" },
            {
                "SQLSOURCE_CONNECTION_A-B=Password=hunter2",
                0,
                0,
                "SQLSOURCE_CONNECTION_A",
                "the key is not a valid name"
            },
            { "SQLSOURCE_CONNECTION A=Password=hunter2", 0, 0, "SQLSOURCE_CONNECTION", "the key is not a valid name" },
            {
                "﻿ SQLSOURCE_CONNECTION_é=Password=hunter2",
                0,
                1,
                "SQLSOURCE_CONNECTION_",
                "the key is not a valid name"
            },
        };

    [Theory]
    [MemberData(nameof(Failures))]
    public void Parse_ToolLineThatCannotBeRead_IsOneErrorThatNamesNoPartOfTheValue(
        string text,
        int line,
        int column,
        string key,
        string reason
    )
    {
        var contents = DotEnvParser.Parse(text);

        var error = contents.Errors.ShouldHaveSingleItem();
        error.ShouldBe(new DotEnvLineError(line, column, key, reason));
        contents.Values.Keys.ShouldNotContain(key);
        error.Key.ShouldNotContain(Secret);
        error.Reason.ShouldNotContain(Secret);
    }

    [Fact]
    public void Parse_ErrorAmongGoodLines_LeavesTheGoodLines()
    {
        var contents = DotEnvParser.Parse(
            "SQLSOURCE_CONNECTION_A=1\nSQLSOURCE_CONNECTION_B=\"x\nSQLSOURCE_CONNECTION_C=3"
        );

        Sorted(contents).ShouldBe([("SQLSOURCE_CONNECTION_A", "1"), ("SQLSOURCE_CONNECTION_C", "3")]);
        contents.Errors.Single().Line.ShouldBe(1);
    }

    [Theory]
    [InlineData("SQLSOURCE_CONNECTION", true)]
    [InlineData("SQLSOURCE_CONNECTION_A", true)]
    [InlineData("SQLSOURCE_CONNECTION_", true)]
    [InlineData("SQLSOURCE_CONNECTIONX", false)]
    [InlineData("sqlsource_connection_a", false)]
    [InlineData("SQLSOURCE_CONNECTION_A-B", false)]
    [InlineData("SQLSOURCE_CONNECTION_é", false)]
    [InlineData("SQLSOURCE_CONNECTION A", false)]
    [InlineData("", false)]
    public void IsToolKey_Key_IsTrueForTheUnnamedAndTheNamedKeys(string key, bool expected) =>
        DotEnvParser.IsToolKey(key).ShouldBe(expected);

    [Fact]
    public void ToString_ContentsWithAValue_LeavesTheValueOut()
    {
        var contents = DotEnvParser.Parse(
            $"SQLSOURCE_CONNECTION_A=Password={Secret}\nSQLSOURCE_CONNECTION_B=\"{Secret}"
        );

        contents.ToString().ShouldNotContain(Secret);
    }
}
