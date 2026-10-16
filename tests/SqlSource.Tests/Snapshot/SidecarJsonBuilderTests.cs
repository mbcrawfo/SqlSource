using System.Globalization;
using Shouldly;
using SqlSource.Snapshot;
using Xunit;

namespace SqlSource.Tests.Snapshot;

public sealed class SidecarJsonBuilderTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(-1, "-1")]
    [InlineData(2147483647, "2147483647")]
    [InlineData(null, "null")]
    public void Number_Value_IsWhatTheWriterWrites(int? value, string expected)
    {
        var json = new SidecarJsonBuilder();
        json.BeginArray(null);
        json.WriteNumber(null, value);
        json.EndArray();

        SidecarJsonBuilder.Number(value).ShouldBe(expected);
        json.ToText().ShouldBe("[\n  " + expected + "\n]\n");
    }

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    [InlineData(null, "null")]
    public void Flag_Value_IsWhatTheWriterWrites(bool? value, string expected)
    {
        var json = new SidecarJsonBuilder();
        json.BeginArray(null);
        json.WriteBoolean(null, value);
        json.EndArray();

        SidecarJsonBuilder.Flag(value).ShouldBe(expected);
        json.ToText().ShouldBe("[\n  " + expected + "\n]\n");
    }

    // In Swedish the minus sign of a number is U+2212.  A difference of --check reads the same on every machine.
    [Fact]
    public void Number_UnderAnotherCulture_IsWrittenTheSameWay()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("sv-SE");

            SidecarJsonBuilder.Number(-1).ShouldBe("-1");
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }
}
