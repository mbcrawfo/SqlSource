using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Parsing;
using SqlSource.Parsing.Markers;
using Xunit;

namespace SqlSource.Repository.Tests.Diagnostics;

// site/diagnostics.md is the list of diagnostics a user reads.  These tests keep it complete: a descriptor that
// is added, removed or renamed fails them until the document says the same.
public sealed class DiagnosticsDocumentTests
{
    private const string HeadingPrefix = "## ";
    private const string ReportedByTheBuild = "Reported by: the build.";
    private const string ReportedByTheTool = "Reported by: the tool.";
    private const string ReportedByBoth = "Reported by: the build and the tool.";

    // The generator's descriptors and the tool's together, in the order of their ids, which is the document's.
    private static readonly ImmutableArray<DiagnosticDescriptor> Descriptors =
    [
        .. SqlDiagnostics.All.Concat(ToolDiagnostics.All).OrderBy(descriptor => descriptor.Id, StringComparer.Ordinal),
    ];

    private static readonly string[] Lines = File.ReadAllLines(RepositoryRoot.Path("site", "diagnostics.md"));

    [Fact]
    public void Document_Sections_AreTheDescriptorsInOrder() =>
        Sections().Select(section => section.Id).ShouldBe(Descriptors.Select(descriptor => descriptor.Id));

    [Fact]
    public void Document_EachSection_StartsWithItsDescriptorsTitle()
    {
        var sections = Sections().ToDictionary(section => section.Id, section => section.Body);

        foreach (var descriptor in Descriptors)
        {
            sections[descriptor.Id].FirstOrDefault(line => line.Length > 0).ShouldBe($"**{descriptor.Title}**");
        }
    }

    // Under its title each section says what reports its error: the build reports the generator's descriptors, the
    // tool its own, and a "both" line is right for either, since the tool reports some of the generator's and the build
    // two of the tool's.  Which of the 28 sections say "both" is the reviewer's to check, not this test's.
    [Fact]
    public void Document_EachSection_SaysWhoReportsIt()
    {
        var sections = Sections().ToDictionary(section => section.Id, section => section.Body);
        var tools = ToolDiagnostics.All.Select(descriptor => descriptor.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var descriptor in Descriptors)
        {
            var line = LinesAfterTitle(sections[descriptor.Id]).FirstOrDefault();
            string[] allowed = tools.Contains(descriptor.Id)
                ? [ReportedByTheTool, ReportedByBoth]
                : [ReportedByTheBuild, ReportedByBoth];

            line.ShouldBeOneOf(allowed, descriptor.Id);
        }
    }

    // The errors of describing a query against its database name the database under their "Reported by" line, and no
    // other section has such a line.  Which database each names is the reviewer's to check, against the files that make
    // the errors.
    [Fact]
    public void Document_DescribingSections_NameTheirDatabase()
    {
        string[] lines = ["Database: PostgreSQL.", "Database: SQL Server.", "Database: PostgreSQL and SQL Server."];

        foreach (var (id, body) in Sections())
        {
            var number = int.Parse(id[6..], CultureInfo.InvariantCulture);
            if (number is (>= 224 and <= 236) or (>= 262 and <= 266))
            {
                LinesAfterTitle(body).Skip(1).FirstOrDefault().ShouldBeOneOf(lines, id);
            }
            else
            {
                body.ShouldNotContain(line => line.StartsWith("Database:", StringComparison.Ordinal), id);
            }
        }
    }

    // The index is a table per area, and the rows of all the tables together must be in id order: a new id above the
    // last one goes in the last table, or in a new table after it, never back in an earlier area's table.
    [Fact]
    public void Document_Table_ListsEveryDescriptorWithItsTitleAndAnchor()
    {
        var rows = Lines.Where(line => line.StartsWith("| [SQLSRC", StringComparison.Ordinal));

        rows.ShouldBe(
            // The anchor of a heading is its text in lower case, and an id is "SQLSRC" followed by digits.
            Descriptors.Select(descriptor => $"| [{descriptor.Id}](#sqlsrc{descriptor.Id[6..]}) | {descriptor.Title} |")
        );
    }

    // The section of SQLSRC109 tells a user what the generator parameters are.  A generator parameter that is added is
    // added to this list and to the section together; each name here must be in the section and known to the parser.
    // It also goes in site/settings.md, in the Values cell of the Generator parameters row and in the table of
    // parameters under #setting-generator-parameters, which no test checks.
    [Fact]
    public void Document_UnknownGeneratorParameterSection_ListsEveryGeneratorParameter()
    {
        string[] parameters =
        [
            "keep-comments",
            "token-validation",
            "sort-input",
            "sort-output",
            "table-models",
            "async-method-suffix",
            "array-pool",
            "list-padding",
            "collection-method",
            "transaction",
            "method-type",
            "default",
        ];
        var section = string.Join('\n', Sections().Single(section => section.Id == "SQLSRC109").Body);

        foreach (var parameter in parameters)
        {
            section.ShouldContain($"`{parameter}`");

            var line = "-- generator: " + parameter;
            var marker = SqlMarkerReader
                .Read(line, SqlLexer.Lex(line, SqlDialectRules.Ansi).Lexemes[0])
                .ShouldNotBeNull();
            var errors = new List<SqlParseError>();
            new SqlGeneratorParameterScope().Read(line, marker, errors);
            errors.ShouldNotContain(error => error.Kind == SqlParseErrorKind.UnknownGeneratorParameter, parameter);
        }
    }

    // The lines of a section after its title that are not blank.
    private static IEnumerable<string> LinesAfterTitle(List<string> body) =>
        body.Where(line => line.Length > 0).Skip(1);

    private static IEnumerable<(string Id, List<string> Body)> Sections()
    {
        (string Id, List<string> Body)? current = null;
        foreach (var line in Lines)
        {
            if (line.StartsWith(HeadingPrefix, StringComparison.Ordinal))
            {
                if (current is { } finished)
                {
                    yield return finished;
                }

                current = (line[HeadingPrefix.Length..], []);
            }
            else
            {
                current?.Body.Add(line);
            }
        }

        if (current is { } last)
        {
            yield return last;
        }
    }
}
