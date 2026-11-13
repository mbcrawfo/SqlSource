using System.Collections.Generic;
using System.IO;
using System.Text;
using Shouldly;
using SqlSource.MutationReport;
using Xunit;

namespace SqlSource.Repository.Tests.MutationReport;

// The reader of the report that a test project writes with --report-xunit-xml: each test with its time.
public sealed class TestTimeReaderTests
{
    private const string Root = "/repo";

    [Fact]
    public void Read_Tests_GivesEachRowItsTypeMethodFileAndTime()
    {
        var tests = Read(
            """
            <assemblies schema-version="3">
                <assembly name="/repo/tests/P/bin/P.dll" total="2">
                    <collection name="one" total="1">
                        <test name="N.ATests.Reads(text: &quot;a&quot;)" result="Pass" time="0.025" type="N.ATests"
                            method="Reads" source-file="/repo/tests/P/ATests.cs" source-line="58" />
                    </collection>
                    <collection name="two" total="1">
                        <test name="N.BTests.Fails" result="Fail" time="1.5" type="N.BTests" method="Fails"
                            source-file="/repo/tests/P/Sub/BTests.cs" source-line="9">
                            <failure><message>no</message></failure>
                        </test>
                    </collection>
                </assembly>
            </assemblies>
            """
        );

        tests.ShouldBe([
            new TestTime("tests/P/ATests.cs", "N.ATests", "Reads", "Pass", 0.025),
            new TestTime("tests/P/Sub/BTests.cs", "N.BTests", "Fails", "Fail", 1.5),
        ]);
    }

    [Fact]
    public void Read_SkippedTest_KeepsItsResult() =>
        Read(
                Report(
                    """
                    <test name="N.ATests.Waits" result="Skip" time="0" type="N.ATests" method="Waits"
                        source-file="/repo/tests/P/ATests.cs"><reason>no Docker</reason></test>
                    """
                )
            )
            .ShouldBe([new TestTime("tests/P/ATests.cs", "N.ATests", "Waits", "Skip", 0)]);

    [Fact]
    public void Read_TestWithNoSourceFile_HasNoFile() =>
        Read(Report("""<test name="N.ATests.Reads" result="Pass" time="0.5" type="N.ATests" method="Reads" />"""))
            .ShouldBe([new TestTime("", "N.ATests", "Reads", "Pass", 0.5)]);

    [Fact]
    public void Read_TestOfAWindowsCheckout_IsFromTheRootWithSlashes() =>
        TestTimeReader
            .Read(
                Stream(
                    Report(
                        """
                        <test result="Pass" time="0.5" type="N.ATests" method="Reads"
                            source-file="C:\repo\tests\P\ATests.cs" />
                        """
                    )
                ),
                @"C:\repo"
            )
            .ShouldHaveSingleItem()
            .File.ShouldBe("tests/P/ATests.cs");

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("<coverage />")]
    [InlineData("<assemblies><assembly>")]
    public void Read_TextThatIsNoReport_Throws(string text) => Should.Throw<InvalidDataException>(() => Read(text));

    [Theory]
    [InlineData("""<test result="Pass" time="0.5" method="M" />""")]
    [InlineData("""<test result="Pass" time="0.5" type="T" />""")]
    [InlineData("""<test time="0.5" type="T" method="M" />""")]
    [InlineData("""<test result="Pass" type="T" method="M" />""")]
    [InlineData("""<test result="Pass" time="soon" type="T" method="M" />""")]
    public void Read_TestWithoutATypeMethodResultOrTime_Throws(string test) =>
        Should.Throw<InvalidDataException>(() => Read(Report(test)));

    private static string Report(string tests) =>
        $"<assemblies><assembly><collection>{tests}</collection></assembly></assemblies>";

    private static IReadOnlyList<TestTime> Read(string xml) => TestTimeReader.Read(Stream(xml), Root);

    private static MemoryStream Stream(string text) => new(Encoding.UTF8.GetBytes(text));
}
