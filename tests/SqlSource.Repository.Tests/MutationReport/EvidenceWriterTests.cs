using System;
using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.MutationReport;
using Xunit;

namespace SqlSource.Repository.Tests.MutationReport;

// The text of a slice's evidence file, which .claude/skills/reviewing-test-quality/briefs.md teaches a reviewer to
// read: a change to the text here is a change to that brief.
public sealed class EvidenceWriterTests
{
    private const string Tests = "tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs";

    private static MatrixMutant Mutant(int line, string mutator = "Equality mutation", string replacement = "a != b") =>
        new(
            "src/SqlSource",
            new MutantKey("Parsing/SqlLexer.cs", line, 17, line, 30, mutator, replacement, 0),
            MutantStatus.Killed,
            false,
            "",
            [],
            []
        );

    private static MethodEvidence Kills(
        int kills,
        int alone = 0,
        string[]? cover = null,
        int rows = 1,
        int rowsNeeded = 1,
        int timeouts = 0,
        int covered = 0,
        bool broad = false
    ) =>
        new(
            Tests,
            "N.T.M",
            rows,
            broad,
            Math.Max(covered, kills),
            kills,
            [.. Enumerable.Range(0, alone).Select(index => Mutant(120 + index))],
            cover,
            rowsNeeded,
            timeouts,
            []
        );

    private static SliceEvidence Slice(params EvidenceFile[] files) =>
        new(
            "SqlSource.Tests.Parsing-1",
            files,
            [
                new EvidenceScope(
                    "generator-parsing",
                    2284,
                    ["tests/SqlSource.Tests/Parsing"],
                    2260,
                    ["src/SqlSource/Parsing"]
                ),
            ],
            [],
            [],
            "tests/SqlSource.Tests",
            56,
            []
        );

    private static string Write(SliceEvidence evidence)
    {
        using var writer = new StringWriter { NewLine = "\n" };
        EvidenceWriter.Write(writer, evidence);
        return writer.ToString();
    }

    // An expected text whose long lines are written as several, each but the last ending in the mark.
    private static string Joined(string text) =>
        text.ReplaceLineEndings("\n").Replace("↩\n", "", StringComparison.Ordinal);

    [Fact]
    public void Write_SliceWithEveryKindOfLine_IsTheDocumentedText()
    {
        var evidence = Slice(
            new EvidenceFile(
                Tests,
                1400,
                [
                    new EvidenceMethod(
                        "SqlFileParserTests.Parse_Case_MatchesItsGoldenFile",
                        40,
                        2.1,
                        false,
                        Kills(300, broad: true)
                    ),
                    new EvidenceMethod(
                        "SqlFileParserTests.Parse_EmptyFile_GivesNoQueries",
                        1,
                        0.004,
                        false,
                        Kills(
                            12,
                            cover:
                            [
                                "N.SqlLexerTests.Read_Empty_GivesNothing",
                                "N.SqlFileParserTests.Parse_OnlyComments_GivesNoQueries",
                            ]
                        )
                    ),
                    new EvidenceMethod(
                        "SqlFileParserTests.Parse_Marker_IsRead",
                        14,
                        0.12,
                        false,
                        Kills(80, alone: 2, rows: 14, rowsNeeded: 5, timeouts: 2)
                    ),
                    new EvidenceMethod("SqlFileParserTests.Parse_Skipped_OnThisSystem", 1, 0, true, null),
                    new EvidenceMethod(
                        "SqlFileParserTests.Parse_Text_DoesNotThrow",
                        1,
                        0.002,
                        false,
                        Kills(0, covered: 31)
                    ),
                    new EvidenceMethod("SqlFileParserTests.Parse_Nothing_ReachesNoMutant", 1, null, false, Kills(0)),
                ]
            ),
            new EvidenceFile("tests/SqlSource.Tests/Parsing/ParserCases.cs", 12, [])
        ) with
        {
            Survivors =
            [
                new EvidenceSurvivor(
                    Mutant(88, "Conditional (true) mutation", "(true?a:b)") with
                    {
                        Status = MutantStatus.Survived,
                    },
                    ["N.SqlLexerTests.Read_Quote_Ends", "N.SqlLexerTests.Read_Dollar_Ends"]
                ),
            ],
            Source =
            [
                new EvidenceSource(
                    "src/SqlSource/Parsing/SqlLexer.cs",
                    [(40, 40), (52, 53)],
                    [(60, 75)],
                    [(5, 12)],
                    [140, 152]
                ),
                new EvidenceSource("src/SqlSource/Parsing/SqlText.cs", [], [], [], [9]),
                new EvidenceSource("src/SqlSource/Parsing/SqlToken.cs", [], [(3, 4)], [], []),
            ],
            Times = [new EvidenceTime("SqlFileParserTests", 2.3), new EvidenceTime("ParserCases", 0.0004)],
        };

        Write(evidence)
            .ShouldBe(
                Joined(
                    """
                    # Evidence: SqlSource.Tests.Parsing-1

                    Files: 2, lines: 1,412.

                    - tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs (1,400)
                    - tests/SqlSource.Tests/Parsing/ParserCases.cs (12)

                    ## Mutation scopes

                    - generator-parsing: ran 2,284 tests of tests/SqlSource.Tests/Parsing; tested 2,260 mutants ↩
                    of src/SqlSource/Parsing.

                    A kill by a test outside a scope is not seen.  The rows of a theory are told apart only by ↩
                    their count.

                    ## Tests

                    ### tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs

                    - `SqlFileParserTests.Parse_Case_MatchesItsGoldenFile` (broad): 40 rows, 2.1 s; kills 300
                    - `SqlFileParserTests.Parse_EmptyFile_GivesNoQueries`: 1 row, 0.004 s; kills 12, alone 0, ↩
                    timeouts under it 0; cover: `SqlLexerTests.Read_Empty_GivesNothing`, ↩
                    `SqlFileParserTests.Parse_OnlyComments_GivesNoQueries`
                    - `SqlFileParserTests.Parse_Marker_IsRead`: 14 rows, 0.120 s; kills 80, alone 2, timeouts ↩
                    under it 2; rows needed 5; no cover
                        - alone: src/SqlSource/Parsing/SqlLexer.cs:120:17 Equality mutation; ↩
                    src/SqlSource/Parsing/SqlLexer.cs:121:17 Equality mutation
                    - `SqlFileParserTests.Parse_Skipped_OnThisSystem`: 1 row, skipped; not measured: in no ↩
                    scope's run
                    - `SqlFileParserTests.Parse_Text_DoesNotThrow`: 1 row, 0.002 s; covers 31 mutants and kills ↩
                    none
                    - `SqlFileParserTests.Parse_Nothing_ReachesNoMutant`: 1 row, no time; reaches no tested mutant

                    ### tests/SqlSource.Tests/Parsing/ParserCases.cs

                    No tests.

                    ## Survivors under these tests

                    - src/SqlSource/Parsing/SqlLexer.cs:88:17 Conditional (true) mutation -> `(true?a:b)`: ↩
                    covered by 2 methods here: `SqlLexerTests.Read_Quote_Ends`, `SqlLexerTests.Read_Dollar_Ends`

                    ## Timeouts under these tests

                    A timeout is read before it is believed: the report names no test that fails on it.

                    - src/SqlSource/Parsing/SqlLexer.cs: 2 (lines 140, 152)
                    - src/SqlSource/Parsing/SqlText.cs: 1 (line 9)

                    ## Source these tests reach: uncovered and blind

                    - src/SqlSource/Parsing/SqlLexer.cs: no test covers lines 40, 52-53; blind, does not ↩
                    compile: 60-75; blind, static: 5-12
                    - src/SqlSource/Parsing/SqlToken.cs: blind, does not compile: 3-4

                    ## Time

                    - `SqlFileParserTests`: 2.3 s, 4.1% of tests/SqlSource.Tests (56.0 s)
                    - `ParserCases`: 0.000 s, 0.0% of tests/SqlSource.Tests (56.0 s)

                    """
                )
            );
    }

    // Review focus 4 of docs/superpowers/plans/2026-11-12-reviewing-test-quality-skill.md: tests that no scope ran
    // are not shown as tests that kill nothing.
    [Fact]
    public void Write_SliceThatNoScopeRan_SaysItIsNotMeasured()
    {
        var evidence = Slice(
            new EvidenceFile(Tests, 10, [new EvidenceMethod("ATests.Reads", 2, 1.5, false, null)])
        ) with
        {
            Scopes = [],
        };

        var text = Write(evidence);

        text.ShouldContain(
            "## Mutation scopes\n\nNone: no mutation scope ran these tests, so they are judged by reading and the "
                + "named-cover rule.\n\n## Tests"
        );
        text.ShouldContain("- `ATests.Reads`: 2 rows, 1.5 s\n");
        text.ShouldNotContain("kills");
        text.ShouldNotContain("## Survivors");
        text.ShouldNotContain("## Timeouts");
        text.ShouldNotContain("## Source");
    }

    [Fact]
    public void Write_LongLists_AreCutWithTheirCount()
    {
        var cover = Enumerable.Range(1, 11).Select(index => $"N.T.Cover{index}").ToArray();
        var evidence = Slice(
            new EvidenceFile(
                Tests,
                10,
                [
                    new EvidenceMethod("T.Covered", 1, 1, false, Kills(20, cover: cover)),
                    new EvidenceMethod("T.Alone", 1, 1, false, Kills(20, alone: 12)),
                ]
            )
        ) with
        {
            Survivors =
            [
                new EvidenceSurvivor(
                    Mutant(1, replacement: "if (first)\n    {\n        " + new string('x', 80) + "\n    }"),
                    ["N.T.A", "N.T.B", "N.T.C", "N.T.D", "N.T.E"]
                ),
            ],
        };

        var text = Write(evidence);

        text.ShouldContain(
            "cover: `T.Cover1`, `T.Cover2`, `T.Cover3`, `T.Cover4`, `T.Cover5`, `T.Cover6`, `T.Cover7`, `T.Cover8` "
                + "and 3 more\n"
        );
        text.ShouldContain("SqlLexer.cs:129:17 Equality mutation and 2 more\n");
        text.ShouldContain("-> `if (first) { " + new string('x', 47) + "…`: covered by 5 methods here\n");
    }

    [Fact]
    public void Write_ListsAtTheirLimits_AreWholeWithNoCount()
    {
        var cover = Enumerable.Range(1, 8).Select(index => $"N.T.Cover{index}").ToArray();
        var evidence = Slice(
            new EvidenceFile(
                Tests,
                10,
                [
                    new EvidenceMethod("T.Covered", 1, 1, false, Kills(20, cover: cover)),
                    new EvidenceMethod("T.Alone", 1, 1, false, Kills(20, alone: 10)),
                ]
            )
        ) with
        {
            Survivors = [new EvidenceSurvivor(Mutant(1), ["N.T.A", "N.T.B", "N.T.C"])],
        };

        var text = Write(evidence);

        text.ShouldContain("`T.Cover7`, `T.Cover8`\n");
        text.ShouldContain("SqlLexer.cs:129:17 Equality mutation\n");
        text.ShouldContain("covered by 3 methods here: `T.A`, `T.B`, `T.C`\n");
        text.ShouldNotContain(" more");
    }

    [Fact]
    public void Write_DatabaseSlice_NamesTheLinesFile()
    {
        var evidence = Slice(new EvidenceFile("tests/DatabaseTests/Postgres/ATests.cs", 10, [])) with
        {
            Scopes = [],
            Project = "tests/DatabaseTests/Postgres",
        };

        Write(evidence).ShouldEndWith("\nLines only the database projects reach: see database-only-lines.md.\n");
    }

    [Fact]
    public void Write_SliceOfAProjectThatWasNotRun_SaysItHasNoTimes()
    {
        var evidence = Slice(new EvidenceFile(Tests, 10, [])) with { ProjectSeconds = null };

        Write(evidence).ShouldEndWith("## Time\n\nNot run: tests/SqlSource.Tests has no times in this run.\n");
    }
}
