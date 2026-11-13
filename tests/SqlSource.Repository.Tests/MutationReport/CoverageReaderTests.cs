using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Shouldly;
using SqlSource.MutationReport;
using Xunit;

namespace SqlSource.Repository.Tests.MutationReport;

// The reader of a test project's Cobertura file, and the lines that only the database projects reach.
public sealed class CoverageReaderTests
{
    [Fact]
    public void Read_Coverage_GivesTheHitLinesOfSourceFilesOnly()
    {
        var lines = Read(
            """
            <coverage>
                <packages>
                    <package name="SqlSource">
                        <classes>
                            <class name="A" filename="/repo/src/SqlSource/Parsing/A.cs">
                                <methods>
                                    <method name="M">
                                        <lines>
                                            <line number="3" hits="2" branch="True" condition-coverage="50% (1/2)">
                                                <conditions><condition number="0" type="jump" /></conditions>
                                            </line>
                                            <line number="4" hits="0" branch="False" />
                                        </lines>
                                    </method>
                                </methods>
                                <lines>
                                    <line number="3" hits="2" branch="False" />
                                    <line number="9" hits="1" branch="False" />
                                </lines>
                            </class>
                        </classes>
                    </package>
                    <package name="SqlSource.Testing">
                        <classes>
                            <class name="T" filename="/repo/tests/SqlSource.Testing/T.cs">
                                <lines><line number="1" hits="5" /></lines>
                            </class>
                        </classes>
                    </package>
                </packages>
            </coverage>
            """
        );

        lines.Keys.ShouldBe(["src/SqlSource/Parsing/A.cs"]);
        lines["src/SqlSource/Parsing/A.cs"].Order().ShouldBe([3, 9]);
    }

    [Fact]
    public void Read_FileInTwoClasses_UnitesItsLines()
    {
        var lines = Read(
            Coverage(Class("/repo/src/SqlSource/A.cs", (1, "1")), Class("/repo/src/SqlSource/A.cs", (7, "1")))
        );

        lines["src/SqlSource/A.cs"].Order().ShouldBe([1, 7]);
    }

    [Fact]
    public void Read_FileWithNoHitLine_IsLeftOut() =>
        Read(Coverage(Class("/repo/src/SqlSource/A.cs", (1, "0")))).ShouldBeEmpty();

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("<assemblies />")]
    [InlineData("<coverage><packages>")]
    public void Read_TextThatIsNoCoverage_Throws(string text) => Should.Throw<InvalidDataException>(() => Read(text));

    [Fact]
    public void Read_LineWithoutANumberOfHits_Throws() =>
        Should.Throw<InvalidDataException>(() => Read(Coverage(Class("/repo/src/SqlSource/A.cs", (1, "some")))));

    [Fact]
    public void Only_LinesTheOthersAlsoHit_AreLeftOut()
    {
        var only = CoverageReader.Only(
            [Lines(("src/A.cs", [1, 2, 3, 9]), ("src/B.cs", [4])), Lines(("src/A.cs", [5]))],
            [Lines(("src/A.cs", [2])), Lines(("src/A.cs", [9]), ("src/B.cs", [4]))]
        );

        only.Select(file => file.File).ShouldBe(["src/A.cs"]);
        only[0].Lines.ShouldBe([(1, 1), (3, 3), (5, 5)]);
    }

    [Fact]
    public void Only_Lines_AreRangesInOrderByFile()
    {
        var only = CoverageReader.Only([Lines(("src/B.cs", [7])), Lines(("src/A.cs", [12, 3, 10, 4, 11, 5]))], []);

        only.Select(file => file.File).ShouldBe(["src/A.cs", "src/B.cs"]);
        only[0].Lines.ShouldBe([(3, 5), (10, 12)]);
        only[1].Lines.ShouldBe([(7, 7)]);
    }

    private static string Coverage(params string[] classes) =>
        $"<coverage><packages><package><classes>{string.Concat(classes)}</classes></package></packages></coverage>";

    private static string Class(string file, params (int Number, string Hits)[] lines) =>
        $"""<class name="A" filename="{file}"><lines>"""
        + string.Concat(lines.Select(line => $"""<line number="{line.Number}" hits="{line.Hits}" />"""))
        + "</lines></class>";

    private static Dictionary<string, IReadOnlySet<int>> Lines(params (string File, int[] Lines)[] files) =>
        files.ToDictionary(file => file.File, file => (IReadOnlySet<int>)file.Lines.ToHashSet());

    private static IReadOnlyDictionary<string, IReadOnlySet<int>> Read(string xml) =>
        CoverageReader.Read(new MemoryStream(Encoding.UTF8.GetBytes(xml)), "/repo");
}
