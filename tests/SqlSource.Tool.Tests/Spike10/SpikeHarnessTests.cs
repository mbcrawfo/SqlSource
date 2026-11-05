using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Spike10;

// The harness of the phase 10.0 spike: every case replays to its expected entry, and a capture with a call removed is
// a miss.  Run with SPIKE10_OUT=<folder> dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class
// "SqlSource.Tool.Tests.Spike10.SpikeHarnessTests" --explicit only
public sealed class SpikeHarnessTests
{
    [Fact(Explicit = true)]
    public async Task Baseline_EveryCase_ReplaysToItsExpectedEntry()
    {
        var table = new StringBuilder("engine\tcase\tkind\n");
        var differing = new StringBuilder();
        var cases = SpikeCases.All();
        foreach (var fixture in cases)
        {
            var outcome = await SpikeReplay.RunAsync(fixture);
            var name = SpikeCases.IsRealistic(fixture) ? "realistic/" + fixture.Name : fixture.Name;
            _ = table.Append(CultureInfo.InvariantCulture, $"{fixture.Engine}\t{name}\t{outcome.Kind}\n");
            if (outcome.Kind != SpikeKind.Entry || outcome.Text != fixture.Expected)
            {
                _ = differing.Append(
                    CultureInfo.InvariantCulture,
                    $"{fixture}: {outcome.Kind} {Shorten(outcome.Text)}\n"
                );
            }
        }

        await System.IO.File.WriteAllTextAsync(
            SpikeCases.Out("t1-baseline.tsv"),
            table.ToString(),
            TestContext.Current.CancellationToken
        );
        differing.ToString().ShouldBeEmpty($"of {cases.Count} cases, these differ:\n");
    }

    [Fact(Explicit = true)]
    public async Task Baseline_CaptureWithACallRemoved_IsAMiss()
    {
        var source = FixtureCases.Of("postgres").Single(found => found.Name == "walk-left");
        var capture = CaptureJson.Read(source.Capture).Value.ShouldNotBeNull();
        var cut = new DescribeCapture(capture.Engine);
        foreach (var call in capture.Calls.Where(call => call.Method != "columns"))
        {
            cut.Add(call);
        }

        var copy = SpikeCases.Copy(source, "walk-left-without-columns", capture: CaptureJson.Write(cut));

        var outcome = await SpikeReplay.RunAsync(copy);

        outcome.Kind.ShouldBe(SpikeKind.Miss);
        outcome.Text.ShouldStartWith("columns ");
    }

    private static string Shorten(string text) => text.Length > 300 ? string.Concat(text.AsSpan(0, 300), "...") : text;
}
