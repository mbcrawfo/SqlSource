using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Snapshot.Reading;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Describing.Describers;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming;

// A raw bundle of FakeDescriber renamed by every step, with the fake engine part, and each refusal of a step.
public sealed partial class BundleRenamerTests
{
    private const string ColumnNames = "describe answer.description.columns[].name";

    private static RenamedBundle Renamed(Outcome<RenamedBundle, RenamingRefusal> renamed)
    {
        renamed.Failure.ShouldBeNull();
        return renamed.Value.ShouldNotBeNull();
    }

    private static RenamingRefusal Refused(Outcome<RenamedBundle, RenamingRefusal> renamed)
    {
        renamed.Value.ShouldBeNull();
        return renamed.Failure.ShouldNotBeNull();
    }

    private static string ReplacementOf(RenamedBundle renamed, string original) =>
        renamed.Mapping.Single(name => name.Original == original).Replacement;

    [Fact]
    public async Task RenameAsync_FakeBundle_ReplaysToItsRenamedEntry()
    {
        var raw = await RenamingBundles.RawAsync();

        var renamed = Renamed(await RenamingBundles.RenameAsync(raw));

        var bundle = renamed.Bundle;
        bundle.Info.ShouldBe(raw.Info.ShouldNotBeNull() with { Renamed = true });
        var expected = BundleReplay.ExpectedOf(bundle, BundleInfos.Path);
        expected.Failure.ShouldBeNull();
        var replayed = await BundleReplays.ReplayAsync(
            bundle,
            BundleInfos.Path,
            new DescriberRegistry([new FakeDescriber()])
        );
        replayed.ShouldBe(expected.Value);
        var entry = replayed.ShouldBeOfType<ReplayOutcome.Described>().Entry;
        entry.Name.ShouldBe(ReplacementOf(renamed, RenamingBundles.Name));
        SidecarReader
            .Read(bundle.Expected.ShouldNotBeNull())
            .Sidecar.ShouldNotBeNull()
            .ToolVersion.ShouldBe(RenamingBundles.ToolVersion);
    }

    [Fact]
    public async Task RenameAsync_FakeBundle_LeaksNoOriginalName()
    {
        var raw = await RenamingBundles.RawAsync();

        var renamed = Renamed(await RenamingBundles.RenameAsync(raw));

        string[] originals = ["users", "GetUsers", "status", "active", "tags", "accounts", "mood", "sad", "happy"];
        foreach (var original in originals)
        {
            RenamingBundles
                .FilesOf(raw)
                .ShouldContain(text => Regex.IsMatch(text, $"\\b{original}\\b", RegexOptions.IgnoreCase));
        }

        foreach (var text in RenamingBundles.FilesOf(renamed.Bundle))
        {
            foreach (var original in originals)
            {
                Regex
                    .IsMatch(text, $"\\b{original}\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                    .ShouldBeFalse($"'{original}' is in:\n{text}");
            }
        }
    }

    [Fact]
    public async Task RenameAsync_TwoKeys_GiveOtherNamesAndTheSameEntryShape()
    {
        var raw = await RenamingBundles.RawAsync();

        var one = Renamed(await RenamingBundles.RenameAsync(raw, key: 1));
        var two = Renamed(await RenamingBundles.RenameAsync(raw, key: 2));

        ReplacementOf(one, "users").ShouldNotBe(ReplacementOf(two, "users"));
        one.Bundle.Query.ShouldNotBe(two.Bundle.Query);
        var oneExpected = one.Bundle.Expected.ShouldNotBeNull();
        var twoExpected = two.Bundle.Expected.ShouldNotBeNull();
        oneExpected.ShouldNotBe(twoExpected);
        Made().Replace(oneExpected, "x").ShouldBe(Made().Replace(twoExpected, "x"));
        one.Mapping.Select(static name => (name.Class, name.Original))
            .ShouldBe(two.Mapping.Select(static name => (name.Class, name.Original)));
    }

    [Fact]
    public async Task RenameAsync_MappingHoldsEachPair()
    {
        var raw = await RenamingBundles.RawAsync();

        var renamed = Renamed(await RenamingBundles.RenameAsync(raw));

        var classes = renamed.Mapping.ToDictionary(static name => name.Original, static name => name.Class);
        classes.ShouldBe(
            new Dictionary<string, NameClass>
            {
                ["accounts"] = NameClass.Alias,
                ["users"] = NameClass.Table,
                ["id"] = NameClass.Column,
                ["tags"] = NameClass.Column,
                ["mood"] = NameClass.Column,
                ["GetUsers"] = NameClass.Query,
                ["sad"] = NameClass.Literal,
                ["happy"] = NameClass.Literal,
                ["u"] = NameClass.Alias,
                ["status"] = NameClass.Alias,
                ["active"] = NameClass.Literal,
            },
            ignoreOrder: true
        );
        renamed.Mapping.Select(static name => name.Replacement).ShouldBeUnique();
        renamed.Bundle.Query.ShouldBe(
            $"-- name: {ReplacementOf(renamed, "GetUsers")}\nSELECT {ReplacementOf(renamed, "u")}."
                + $"{ReplacementOf(renamed, "id")}, {ReplacementOf(renamed, "u")}.{ReplacementOf(renamed, "tags")} "
                + $"FROM {ReplacementOf(renamed, "users")} {ReplacementOf(renamed, "u")} WHERE "
                + $"{ReplacementOf(renamed, "u")}.{ReplacementOf(renamed, "status")} = "
                + $"'{ReplacementOf(renamed, "active")}' AND {ReplacementOf(renamed, "u")}."
                + $"{ReplacementOf(renamed, "id")} = @{ReplacementOf(renamed, "id")};\n"
        );
    }

    [Fact]
    public async Task RenameAsync_MemberWithNoRule_IsRefusedAtItsPlace()
    {
        var raw = await RenamingBundles.RawAsync();
        var engine = new FakeEngineRenaming(rules: new Dictionary<string, FieldRule?> { [ColumnNames] = null });

        var refusal = Refused(await RenamingBundles.RenameAsync(raw, engine));

        refusal.ShouldBe(
            new RenamingRefusal(
                RefusalStep.Token,
                "capture.json call 1 'describe' answer.description.columns[].name",
                "id"
            )
        );
    }

    [Fact]
    public async Task RenameAsync_DerivedRequestNotMade_IsRefusedAtTheRequestSql()
    {
        var raw = await RenamingBundles.RawAsync();
        var engine = new FakeEngineRenaming(
            derive: static (call, _) =>
            {
                if (call.Method == "describe" && call.Renamed["request"] is JsonObject request)
                {
                    request["sql"] = request["sql"]?.GetValue<string>() + " ";
                }
            }
        );

        var refusal = Refused(await RenamingBundles.RenameAsync(raw, engine));

        refusal.ShouldBe(new RenamingRefusal(RefusalStep.RequestSql, "capture.json call 'describe'", null));
    }

    // A bundle of another version of the tool, whose entry is that version's: the replay gives this one's, and the
    // renamed entry keeps the bundle's.
    [Fact]
    public async Task RenameAsync_EntryOfAnotherToolVersion_IsProvedAndKeepsItsVersion()
    {
        var raw = await RenamingBundles.RawAsync();
        var sidecar = SidecarReader.Read(raw.Expected.ShouldNotBeNull()).Sidecar.ShouldNotBeNull();
        var entry = sidecar.Queries.ShouldHaveSingleItem() with { ToolVersion = RenamingBundles.ToolVersion };
        var older = raw with
        {
            Expected = SidecarWriter.Write(sidecar with { Queries = new EquatableArray<SidecarEntry>([entry]) }),
        };

        var renamed = Renamed(await RenamingBundles.RenameAsync(older));

        SidecarReader
            .Read(renamed.Bundle.Expected.ShouldNotBeNull())
            .Sidecar.ShouldNotBeNull()
            .Queries.ShouldHaveSingleItem()
            .ToolVersion.ShouldBe(RenamingBundles.ToolVersion);
    }

    [Fact]
    public async Task RenameAsync_EntryThatDiffers_IsRefusedAtReplay()
    {
        var raw = await RenamingBundles.RawAsync();
        var engine = new FakeEngineRenaming(
            rules: new Dictionary<string, FieldRule?>
            {
                ["describe answer.description.plan"] = FieldRule.Name(NameClass.Alias),
            }
        );

        var refusal = Refused(await RenamingBundles.RenameAsync(raw, engine));

        refusal.Step.ShouldBe(RefusalStep.Replay);
        refusal.Place.ShouldBe("expected.json");
        refusal.Token.ShouldNotBeNull().ShouldStartWith("plan: \"walked\" -> \"a_");
    }

    [Fact]
    public async Task RenameAsync_RefusedQuery_KeepsItsFailure()
    {
        var describer = RenamingBundles.Describer();
        describer.Failures[RenamingBundles.Name] = FakeDescriber.Failure(RenamingBundles.Name);
        var raw = await RenamingBundles.RawAsync(describer);

        var renamed = Renamed(await RenamingBundles.RenameAsync(raw));

        raw.Info.ShouldNotBeNull().Outcome.ShouldBe(BundleOutcome.Failure("SQLSRC999", "describe columns"));
        renamed.Bundle.Info.ShouldBe(raw.Info with { Renamed = true });
        renamed.Bundle.Expected.ShouldBeNull();
        renamed.Bundle.Capture.ShouldNotContain(RenamingBundles.Name);
        (
            await BundleReplays.ReplayAsync(
                renamed.Bundle,
                BundleInfos.Path,
                new DescriberRegistry([new FakeDescriber()])
            )
        ).ShouldBe(new ReplayOutcome.Refused("SQLSRC999", "describe columns"));
    }

    [Fact]
    public async Task RenameAsync_RenamedBundle_Throws()
    {
        var raw = await RenamingBundles.RawAsync();
        var renamed = raw with { Info = raw.Info.ShouldNotBeNull() with { Renamed = true } };

        _ = await Should.ThrowAsync<ArgumentException>(() => RenamingBundles.RenameAsync(renamed));
        _ = await Should.ThrowAsync<ArgumentException>(() => RenamingBundles.RenameAsync(raw with { Info = null }));
    }

    // A replacement of the mapping, and the hash of a query.
    [GeneratedRegex("\\b[a-z]_[0-9a-f]{8,}\\b|\\b[0-9a-f]{64}\\b")]
    private static partial Regex Made();
}
