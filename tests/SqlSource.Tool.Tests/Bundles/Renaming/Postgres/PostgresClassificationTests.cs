using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Bundles.Renaming.Postgres;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using SqlSource.Tool.Testing.Bundles;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming.Postgres;

// The words of each fixture's plans that are the planner's or the engine's, found without asking the renaming: a word
// of a key the renaming keeps that neither the query, a catalog answer nor the schema holds is no name of the user's,
// so the renamed plan holds it verbatim, which it does only when the vocabulary or the planner's names hold it.  The
// schema is asked too, since a plan prints the columns of a table no catalog answer was asked of, and the tables of a
// view's definition.
public sealed partial class PostgresClassificationTests
{
    // The keys of one raw name, read whole; every other key the renaming keeps is read by PlanTokens.
    private static readonly FrozenSet<string> NameKeys = FrozenSet.ToFrozenSet(
        [PlanKeys.Alias, PlanKeys.RelationName, PlanKeys.Schema, PlanKeys.CteName],
        StringComparer.Ordinal
    );

    private static readonly FrozenSet<string> KeysRead = PostgresPlanRenamer.KeysRead.ToFrozenSet(
        StringComparer.Ordinal
    );

    public static TheoryData<string> Cases =>
        [with(FixtureCases.Of(PostgresRenamings.Engine).Select(static found => found.Name))];

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task PlanWord_HeldByNeitherQueryNorCatalog_IsKept(string name)
    {
        var fixture = PostgresRenamings.Fixture(name);
        var raw = RawBundles.OfFixture(fixture);
        var rawCapture = CaptureJson.Read(raw.Capture).Value.ShouldNotBeNull();
        var held = SourceWords(fixture.Query);
        held.UnionWith(SourceWords(PostgresRenamings.Schema));
        held.UnionWith(CatalogNames(rawCapture));

        var renamed = await PostgresRenamings.RenamedAsync(raw);

        var rawPlans = PlansOf(rawCapture);
        var renamedPlans = PlansOf(CaptureJson.Read(renamed.Bundle.Capture).Value.ShouldNotBeNull());
        renamedPlans.Count.ShouldBe(rawPlans.Count);
        for (var index = 0; index < rawPlans.Count; index++)
        {
            var kept = new HashSet<string>(PlanWords(renamedPlans[index]), StringComparer.Ordinal);
            var planners = PlanWords(rawPlans[index]).Where(word => !IsHeld(word, held)).Distinct().ToList();
            planners.Where(word => !kept.Contains(word)).ShouldBeEmpty();
        }
    }

    // The words and quoted names of a query or a schema, compared ignoring case as the plan may fold them.
    private static HashSet<string> SourceWords(string source)
    {
        var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in SourceWord().Matches(source))
        {
            _ = words.Add(match.Groups["quoted"].Success ? match.Groups["quoted"].Value : match.Value);
        }

        return words;
    }

    // The names of every catalog answer: each string of an answer whose member is a name.
    private static List<string> CatalogNames(DescribeCapture capture)
    {
        var names = new List<string>();
        foreach (var call in capture.Calls)
        {
            JsonStrings.Visit(
                JsonNode.Parse(call.Answer.GetRawText()),
                CaptureRenamer.Answer,
                found =>
                {
                    if (PostgresRenaming.Instance.RuleOf(call.Method, found.Path) is { Kind: FieldRuleKind.Name })
                    {
                        names.Add(found.Value);
                    }

                    return null;
                }
            );
        }

        return names;
    }

    // A word the query or a catalog holds, or the planner's <name>_<n> of one.
    private static bool IsHeld(string word, HashSet<string> held)
    {
        if (held.Contains(word))
        {
            return true;
        }

        var underscore = word.LastIndexOf('_');
        return underscore > 0
            && underscore < word.Length - 1
            && !word.AsSpan(underscore + 1).ContainsAnyExceptInRange('0', '9')
            && held.Contains(word[..underscore]);
    }

    private static List<string> PlansOf(DescribeCapture capture) =>
        [
            .. capture
                .Calls.Where(static call => call.Method == PostgresCallNames.Explain)
                .Select(static call => call.Answer.GetProperty("plan").GetString().ShouldNotBeNull()),
        ];

    // The words of the keys the renaming keeps, at any depth: a name key's value whole, and each word and quoted name
    // of any other key's strings by PlanTokens.
    private static List<string> PlanWords(string plan)
    {
        var words = new List<string>();
        using var document = JsonDocument.Parse(plan);
        Visit(document.RootElement, key: null, words);
        return words;
    }

    // An object's kept keys, an array's items, and a string by its key; a number, a boolean or a null holds no word.
    private static void Visit(JsonElement element, string? key, List<string> words)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var member in element.EnumerateObject().Where(static member => KeysRead.Contains(member.Name)))
            {
                Visit(member.Value, member.Name, words);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                Visit(item, key, words);
            }
        }
        else if (element.ValueKind == JsonValueKind.String && key is not null && NameKeys.Contains(key))
        {
            words.Add(element.GetString()!);
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            var text = element.GetString()!;
            foreach (var token in PlanTokens.Read(text).ShouldNotBeNull(text))
            {
                var written = text[token.Start..token.End];
                if (token.Kind == PlanTokens.Kind.Word)
                {
                    words.Add(written);
                }
                else if (token.Kind == PlanTokens.Kind.QuotedName)
                {
                    words.Add(PostgresIdentifiers.Unquote(written));
                }
            }
        }
    }

    [GeneratedRegex("\"(?<quoted>(?:[^\"]|\"\")*)\"|[A-Za-z_][A-Za-z0-9_$]*")]
    private static partial Regex SourceWord();
}
