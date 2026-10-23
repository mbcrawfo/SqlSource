using System;
using System.Linq;
using System.Text.Json;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Capture;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Capture;

// The capture as a file: what CaptureJson writes, what it reads back, and what it says of a text that is none.
public sealed class CaptureJsonTests
{
    // A capture of two calls as its file holds it.
    private const string Text =
        "{\n"
        + "  \"captureVersion\": 1,\n"
        + "  \"engine\": \"postgres\",\n"
        + "  \"calls\": [\n"
        + "    {\n"
        + "      \"method\": \"open\",\n"
        + "      \"request\": {},\n"
        + "      \"answer\": {\n"
        + "        \"serverVersion\": \"18.4\",\n"
        + "        \"error\": null\n"
        + "      }\n"
        + "    },\n"
        + "    {\n"
        + "      \"method\": \"prepare\",\n"
        + "      \"request\": {\n"
        + "        \"sql\": \"SELECT id FROM users WHERE id = $1\",\n"
        + "        \"types\": [\n"
        + "          \"unknown\"\n"
        + "        ]\n"
        + "      },\n"
        + "      \"answer\": {\n"
        + "        \"parameterTypes\": [\n"
        + "          23\n"
        + "        ],\n"
        + "        \"error\": null\n"
        + "      }\n"
        + "    }\n"
        + "  ]\n"
        + "}\n";

    private static readonly string[] Unknown = ["unknown"];

    private static readonly int[] Int4 = [23];

    private static readonly string[] SplitValue = ["bad Host=db;Pass", "word=hunter22"];

    private static DescribeCapture TwoCalls()
    {
        var capture = new DescribeCapture(SidecarValues.Engine.Postgres);
        capture.Add(
            new CaptureCall(
                "open",
                CaptureJson.ToElement(new { }),
                CaptureJson.ToElement(new { ServerVersion = "18.4", Error = (string?)null })
            )
        );
        capture.Add(
            new CaptureCall(
                "prepare",
                CaptureJson.ToElement(new { Sql = "SELECT id FROM users WHERE id = $1", Types = Unknown }),
                CaptureJson.ToElement(new { ParameterTypes = Int4, Error = (string?)null })
            )
        );
        return capture;
    }

    private static DescribeCapture WithAwkwardText()
    {
        var capture = new DescribeCapture(SidecarValues.Engine.Postgres);
        capture.Add(
            new CaptureCall(
                "prepare",
                CaptureJson.ToElement(new { Sql = CaptureInputs.AwkwardSql }),
                CaptureJson.ToElement(new { Error = (string?)null })
            )
        );
        return capture;
    }

    [Fact]
    public void Write_CaptureWithTwoCalls_IsIndentedWithLineFeeds()
    {
        var text = CaptureJson.Write(TwoCalls());

        text.ShouldBe(Text);
        text.ShouldNotContain("\r");
    }

    [Fact]
    public void Read_WhatWriteGave_WritesTheSameTextAgain()
    {
        var read = CaptureJson.Read(Text).Value.ShouldNotBeNull();

        read.Engine.ShouldBe("postgres");
        read.Calls.Select(call => call.Method).ShouldBe(["open", "prepare"]);
        CaptureJson.Write(read).ShouldBe(Text);
    }

    [Fact]
    public void Read_CaptureVersionOfTwo_IsAnErrorThatNamesIt()
    {
        var read = CaptureJson.Read(
            Text.Replace("\"captureVersion\": 1", "\"captureVersion\": 2", StringComparison.Ordinal)
        );

        read.Value.ShouldBeNull();
        read.Failure.ShouldBe("The capture's captureVersion is 2, and this version of sqlsource reads 1.");
    }

    [Theory]
    [InlineData("", "The capture is not JSON: ")]
    [InlineData("{\"captureVersion\": 1, \"engine\": \"postgres\", \"calls\": [", "The capture is not JSON: ")]
    [InlineData("[]", "The capture is not a JSON object.")]
    [InlineData("{}", "The capture has no captureVersion.")]
    [InlineData( /*lang=json,strict*/
        "{\"captureVersion\": \"1\"}",
        "The capture has no captureVersion."
    )]
    [InlineData( /*lang=json,strict*/
        "{\"captureVersion\": 1}",
        "The capture has no engine."
    )]
    [InlineData( /*lang=json,strict*/
        "{\"captureVersion\": 1, \"engine\": 7}",
        "The capture has no engine."
    )]
    [InlineData( /*lang=json,strict*/
        "{\"captureVersion\": 1, \"engine\": \"postgres\"}",
        "The capture has no calls."
    )]
    [InlineData( /*lang=json,strict*/
        "{\"captureVersion\": 1, \"engine\": \"postgres\", \"calls\": {}}",
        "The capture has no calls."
    )]
    [InlineData(
        /*lang=json,strict*/
        "{\"captureVersion\": 1, \"engine\": \"postgres\", \"calls\": [7]}",
        "The call 1 of the capture has no method, request or answer."
    )]
    [InlineData(
        /*lang=json,strict*/
        "{\"captureVersion\": 1, \"engine\": \"postgres\", \"calls\": [{\"method\": \"open\", \"request\": {}}]}",
        "The call 1 of the capture has no method, request or answer."
    )]
    public void Read_TextThatIsNoCapture_SaysWhatIsWrong(string text, string reason)
    {
        var read = CaptureJson.Read(text);

        read.Value.ShouldBeNull();
        read.Failure.ShouldNotBeNull().ShouldStartWith(reason);
    }

    [Fact]
    public void Options_ObjectWithANestedObjectAListANullAndAnInteger_WritesEveryMemberInCamelCase()
    {
        var value = new
        {
            Name = "a",
            Count = 3,
            Missing = (string?)null,
            Items = new[] { new { Flag = true } },
            Nested = new { Flag = false },
        };

        JsonSerializer
            .Serialize(value, CaptureJson.Options)
            .ShouldBe(
                /*lang=json,strict*/
                "{\"name\":\"a\",\"count\":3,\"missing\":null,\"items\":[{\"flag\":true}],\"nested\":{\"flag\":false}}"
            );
    }

    // A change to the options would change every fixture, so no caller can make one.
    [Fact]
    public void Options_AreReadOnly()
    {
        CaptureJson.Options.IsReadOnly.ShouldBeTrue();
        _ = Should.Throw<InvalidOperationException>(() => CaptureJson.Options.WriteIndented = true);
    }

    // A request that was read from a file must have the key of the request it was written from.
    [Fact]
    public void KeyOf_ElementReadFromAnIndentedFile_IsTheKeyOfTheRequestItWasWrittenFrom()
    {
        var request = new { Sql = CaptureInputs.AwkwardSql, Types = new[] { "unknown", "int4" } };

        var read = CaptureJson.Read(CaptureJson.Write(WithRequest(request))).Value.ShouldNotBeNull();

        var key = CaptureJson.KeyOf(read.Calls[0].Request);
        key.ShouldBe(CaptureJson.KeyOf(CaptureJson.ToElement(request)));
        key.ShouldBe(JsonSerializer.Serialize(request, CaptureJson.Options));
    }

    // A request's record may gain a nullable member: a key has no null member, at any depth, so a request that was
    // recorded before the member was there and the same request asked now are one question.
    [Fact]
    public void KeyOf_RequestWithNullMembers_LeavesThemOutAtEveryDepth()
    {
        var request = new
        {
            Sql = "a",
            Added = (string?)null,
            Items = new[] { new { Name = "id", Type = (string?)null } },
            Nested = new { Flag = (bool?)null, Count = 0 },
        };

        CaptureJson
            .KeyOf(CaptureJson.ToElement(request))
            .ShouldBe( /*lang=json,strict*/
                "{\"sql\":\"a\",\"items\":[{\"name\":\"id\"}],\"nested\":{\"count\":0}}"
            );
    }

    // A null in a list is a value of the list and no member, so it stays, and two lists that differ by one stay two
    // questions.  A zero, a false, an empty text and an empty list are values too.
    [Fact]
    public void KeyOf_NullInAListAndValuesThatAreNotNull_Stay()
    {
        using var document = JsonDocument.Parse(
            "{\"items\":[\"a\",null],\"count\":0,\"flag\":false,\"text\":\"\",\"empty\":[],\"gone\":null}"
        );

        CaptureJson
            .KeyOf(document.RootElement)
            .ShouldBe( /*lang=json,strict*/
                "{\"items\":[\"a\",null],\"count\":0,\"flag\":false,\"text\":\"\",\"empty\":[]}"
            );
    }

    // What a capture holds, and what a message shows, is the request with every member.
    [Fact]
    public void TextOf_RequestWithNullMembers_WritesEveryMember() =>
        CaptureJson
            .TextOf(CaptureJson.ToElement(new { Sql = "a", Added = (string?)null }))
            .ShouldBe( /*lang=json,strict*/
                "{\"sql\":\"a\",\"added\":null}"
            );

    private static DescribeCapture WithRequest<T>(T request)
    {
        var capture = new DescribeCapture(SidecarValues.Engine.Postgres);
        capture.Add(new CaptureCall("prepare", CaptureJson.ToElement(request), CaptureJson.ToElement(new { })));
        return capture;
    }

    // A reviewer reads the SQL of a capture: a quote is \" and nothing else of it is escaped but its line break.
    [Fact]
    public void Write_TextWithQuotesAnglesAndALineBreak_StaysReadableAndHoldsNoCarriageReturn()
    {
        var text = CaptureJson.Write(WithAwkwardText());

        text.ShouldContain("\"sql\": \"SELECT '<é>' AS \\\"a\\\"\\r\\nFROM t\"");
        text.ShouldNotContain("\r");
        var read = CaptureJson.Read(text).Value.ShouldNotBeNull();
        read.Calls[0].Request.GetProperty("sql").GetString().ShouldBe(CaptureInputs.AwkwardSql);
    }

    private const string Refused = "The capture holds the connection's value or a password of it.";

    private static DescribeCapture WithAnswer<T>(T answer)
    {
        var capture = new DescribeCapture(SidecarValues.Engine.Postgres);
        capture.Add(new CaptureCall("open", CaptureJson.ToElement(new { }), CaptureJson.ToElement(answer)));
        return capture;
    }

    [Fact]
    public void Write_WithASecretNotInTheCapture_IsTheFile()
    {
        var secret = new ConnectionSecret(
            "Host=db;Password=hunter22",
            ["hunter22"],
            forms: ["host=db;password=hunter22"]
        );

        var written = CaptureJson.Write(TwoCalls(), secret);

        written.Value.ShouldBe(CaptureJson.Write(TwoCalls()));
        written.Failure.ShouldBeNull();
    }

    [Fact]
    public void Write_ValueInAnAnswer_IsRefused()
    {
        var secret = new ConnectionSecret("Host=db;Password=hunter22");

        var written = CaptureJson.Write(WithAnswer(new { Error = "bad Host=db;Password=hunter22 here" }), secret);

        written.Value.ShouldBeNull();
        written.Failure.ShouldBe(Refused);
    }

    [Fact]
    public void Write_RewriteInAnAnswer_IsRefused()
    {
        var secret = new ConnectionSecret("host=db;password=hunter22", forms: ["Host=db;Password=hunter22"]);

        var written = CaptureJson.Write(WithAnswer(new { Error = "bad Host=db;Password=hunter22 here" }), secret);

        written.Value.ShouldBeNull();
        written.Failure.ShouldBe(Refused);
    }

    [Fact]
    public void Write_PasswordAsATokenInAnError_IsRefused()
    {
        var secret = new ConnectionSecret("Host=db;Password=hunter22", ["hunter22"]);

        var written = CaptureJson.Write(WithAnswer(new { Error = "password hunter22 is wrong" }), secret);

        written.Value.ShouldBeNull();
        written.Failure.ShouldBe(Refused);
    }

    [Fact]
    public void Write_PasswordInsideAWord_IsTheFile()
    {
        var secret = new ConnectionSecret("Host=db;Password=hunter22", ["hunter22"]);
        var capture = WithAnswer(new { Error = "password hunter22x is wrong" });

        var written = CaptureJson.Write(capture, secret);

        written.Value.ShouldBe(CaptureJson.Write(capture));
    }

    // The file holds the value as the writer escapes it, so a search for the value as it is would miss it.
    [Fact]
    public void Write_ValueWithAQuoteAndABackslash_IsFoundAsEscaped()
    {
        const string Value = "Password=\"a\\b\"";
        var secret = new ConnectionSecret(Value);

        var written = CaptureJson.Write(WithAnswer(new { Error = $"bad {Value} here" }), secret);

        written.Value.ShouldBeNull();
        written.Failure.ShouldBe(Refused);
    }

    // An escaped line break is a backslash and an n in the file, which would put a letter before the password.
    [Fact]
    public void Write_PasswordRightAfterALineBreak_IsRefused()
    {
        var secret = new ConnectionSecret("Host=db;Password=hunter22", ["hunter22"]);

        var written = CaptureJson.Write(WithAnswer(new { Error = "failed\nhunter22" }), secret);

        written.Value.ShouldBeNull();
        written.Failure.ShouldBe(Refused);
    }

    [Fact]
    public void Write_PasswordWithAQuote_IsRefused()
    {
        var secret = new ConnectionSecret("Host=db;Password=hu\"nter22", ["hu\"nter22"]);

        var written = CaptureJson.Write(WithAnswer(new { Error = "password hu\"nter22 is wrong" }), secret);

        written.Value.ShouldBeNull();
        written.Failure.ShouldBe(Refused);
    }

    [Fact]
    public void Write_ValueSplitOverTwoStringsOfAnAnswer_IsRefused()
    {
        var secret = new ConnectionSecret("Host=db;Password=hunter22");

        var written = CaptureJson.Write(WithAnswer(new { Lines = SplitValue }), secret);

        written.Value.ShouldBeNull();
        written.Failure.ShouldBe(Refused);
    }

    // The names of the members are the records' own, so a password that equals one is no finding.
    [Fact]
    public void Write_PasswordThatEqualsAMemberNameAndStandsOnlyAsOne_IsTheFile()
    {
        var secret = new ConnectionSecret("Host=db;Password=error", ["error"]);
        var capture = WithAnswer(new { Error = (string?)null });

        var written = CaptureJson.Write(capture, secret);

        written.Value.ShouldBe(CaptureJson.Write(capture));
    }
}
