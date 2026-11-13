using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

namespace SqlSource.MutationReport;

// A test of a test project's run, one row of a theory each.  File is the test's path from the repository root, with
// '/', or "" when the report gives none, and Result is xUnit's: "Pass", "Fail", "Skip" or "NotRun".
internal sealed record TestTime(string File, string Type, string Method, string Result, double Seconds);

// Reads the report that a test project writes with --report-xunit-xml, xUnit's own format, which gives each test
// its class, its method, its time and the file it is in.
internal static class TestTimeReader
{
    // Throws InvalidDataException for text that is no such report or a test without a type, method, result or time.
    public static IReadOnlyList<TestTime> Read(Stream xml, string repositoryRoot)
    {
        var tests = new List<TestTime>();
        try
        {
            using var reader = XmlReader.Create(xml);
            if (reader.MoveToContent() != XmlNodeType.Element || reader.Name != "assemblies")
            {
                throw new InvalidDataException("It is no xUnit report: its root is not <assemblies>.");
            }

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element || reader.Name != "test")
                {
                    continue;
                }

                if (
                    reader.GetAttribute("type") is not { } type
                    || reader.GetAttribute("method") is not { } method
                    || reader.GetAttribute("result") is not { } result
                    || !double.TryParse(
                        reader.GetAttribute("time"),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var seconds
                    )
                )
                {
                    throw new InvalidDataException(
                        "It is no xUnit report: a test has no type, method, result or time."
                    );
                }

                var file = reader.GetAttribute("source-file") is { } source
                    ? ReportReader.FromRoot(repositoryRoot, source)
                    : "";
                tests.Add(new TestTime(file, type, method, result, seconds));
            }
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException($"It is no xUnit report: {exception.Message}", exception);
        }

        return tests;
    }
}
