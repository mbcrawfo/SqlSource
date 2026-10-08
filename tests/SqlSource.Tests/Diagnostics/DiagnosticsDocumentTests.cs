using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Diagnostics;

// docs/diagnostics.md is the list of diagnostics a user reads.  These tests keep it complete: a descriptor that is
// added, removed or renamed fails the build of this project's tests until the document says the same.
public class DiagnosticsDocumentTests
{
    private const string HeadingPrefix = "## ";

    private static readonly string[] Lines = File.ReadAllLines(
        Path.Combine(AppContext.BaseDirectory, "docs", "diagnostics.md")
    );

    [Fact]
    public void Document_Sections_AreTheDescriptorsInOrder() =>
        Sections().Select(section => section.Id).ShouldBe(SqlDiagnostics.All.Select(descriptor => descriptor.Id));

    [Fact]
    public void Document_EachSection_StartsWithItsDescriptorsTitle()
    {
        var sections = Sections().ToDictionary(section => section.Id, section => section.Body);

        foreach (var descriptor in SqlDiagnostics.All)
        {
            sections[descriptor.Id].FirstOrDefault(line => line.Length > 0).ShouldBe($"**{descriptor.Title}**");
        }
    }

    [Fact]
    public void Document_Table_ListsEveryDescriptorWithItsTitleAndAnchor()
    {
        var rows = Lines.Where(line => line.StartsWith("| [SQLSRC", StringComparison.Ordinal));

        rows.ShouldBe(
            // The anchor of a heading is its text in lower case, and an id is "SQLSRC" followed by digits.
            SqlDiagnostics.All.Select(descriptor =>
                $"| [{descriptor.Id}](#sqlsrc{descriptor.Id[6..]}) | {descriptor.Title} |"
            )
        );
    }

    // The section of SQLSRC109 tells a user what the generator parameters are.  A generator parameter that is added is
    // added to this list and to the section together, and nothing the section names may be unknown to the parser.
    [Fact]
    public void Document_UnknownGeneratorParameterSection_ListsEveryGeneratorParameter()
    {
        string[] parameters =
        [
            "keep-comments",
            "token-validation",
            "no-token-validation",
            "token-ignore=name",
            "dialect=name",
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
            new SqlGeneratorParameterScope(int.MaxValue).Read(line, marker, errors);
            errors.ShouldNotContain(error => error.Kind == SqlParseErrorKind.UnknownGeneratorParameter, parameter);
        }
    }

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
