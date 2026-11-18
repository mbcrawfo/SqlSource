using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.Parsing.Templating;

// The corpus of the sqlfluff templater: each case's templating plan is the text of its file, the names of the
// dialects and their options are the text of dialect-names.json, and the titles of the errors that stop a plan are
// the text of diagnostic-titles.json.
// SQLSOURCE_UPDATE_TEMPLATER_CORPUS=1 writes the files instead; see "The templater's corpus" in tests/README.md.
public sealed class TemplatingCorpusTests
{
    // Each case, with the trait source, so that `--filter-trait "source=cases"` runs the fidelity cases alone.
    public static IEnumerable<TheoryDataRow<string>> CaseNames =>
        TemplatingCorpus
            .Cases.Keys.OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => new TheoryDataRow<string>(name).WithTrait("source", TemplatingCorpus.SourceOf(name)));

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Corpus_EveryCase_MatchesTheParser(string name) =>
        TemplatingCorpus.Check(name, TemplatingCorpus.Render(TemplatingCorpus.Cases[name]));

    [Fact]
    public void Corpus_DialectNames_MatchTheParser() =>
        TemplatingCorpus.Check(TemplatingCorpus.DialectNames, TemplatingCorpus.RenderDialectNames());

    [Fact]
    public void Corpus_DiagnosticTitles_MatchTheDescriptors() =>
        TemplatingCorpus.Check(TemplatingCorpus.DiagnosticTitles, TemplatingCorpus.RenderDiagnosticTitles());

    // A corpus file of a case that was renamed or removed fails here, and the opt-in deletes it.
    [Fact]
    public void Corpus_HasNoOrphanFiles()
    {
        if (TemplatingCorpus.IsUpdating)
        {
            foreach (var leftover in TemplatingCorpus.Leftovers())
            {
                File.Delete(TemplatingCorpus.PathOf(leftover));
            }
        }

        TemplatingCorpus
            .Leftovers()
            .ShouldBeEmpty(
                "These corpus files have no case.  Run the test with "
                    + $"{TemplatingCorpus.UpdateVariable}=1 to delete them."
            );
    }
}
