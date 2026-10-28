using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Parsing;
using SqlSource.Parsing.Markers;
using SqlSource.Testing;
using Xunit;

namespace SqlSource.Tests.Diagnostics;

// docs/manual/diagnostics.md is the list of diagnostics a user reads.  These tests keep it complete: a descriptor that
// is added, removed or renamed fails them until the document says the same.
public sealed class DiagnosticsDocumentTests
{
    private const string HeadingPrefix = "## ";

    // The generator's descriptors and the tool's together, in the order of their ids, which is the document's.
    private static readonly ImmutableArray<DiagnosticDescriptor> Descriptors =
    [
        .. SqlDiagnostics.All.Concat(ToolDiagnostics.All).OrderBy(descriptor => descriptor.Id, StringComparer.Ordinal),
    ];

    private static readonly string[] Lines = File.ReadAllLines(
        Path.Combine(RepositoryRoot.Folder, "docs", "manual", "diagnostics.md")
    );

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
    // It also goes in docs/manual/settings.md, in the Values cell of the Generator parameters row and in the table of
    // parameters under #setting-generator-parameters, which no test checks.
    [Fact]
    public void Document_UnknownGeneratorParameterSection_ListsEveryGeneratorParameter()
    {
        string[] parameters =
        [
            "keep-comments",
            "no-token-validation",
            "sort-input",
            "sort-output",
            "no-table-models",
            "async-method-suffix",
            "no-array-pool",
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
