using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using SqlSource.Parsing;
using SqlSource.Snapshot;

namespace SqlSource.Tool.Bundles.Renaming;

/// <summary>
/// The check of every token of a renamed bundle that came from the user's database or query: each must be a
/// replacement of the mapping, a word of the engine's, a spelling the mapping kept from a row of the engine's, a
/// number or one character of punctuation, and a literal must hold a replacement.  The first that is none is a
/// refusal that names its place and the token.
/// </summary>
/// <remarks>
/// <para>
/// The capture's strings are checked by their members' rules, then <c>query.sql</c>, then the entry's strings.  SQL, a
/// type's spelling and <c>query.sql</c> are split by the generator's lexer and the engine's word rule, as the SQL
/// renamer splits them; an engine's own string by <see cref="IEngineRenaming.WordsOf" />; a name, a parameter, a label
/// and a whole name are one token, a name's nullability mark aside.  A name is asked about quoted or not as the
/// renamer renamed it: a token's name, in a placeholder, a <c>-- token:</c> or a <c>-- token-ignore:</c>, quoted.  The
/// tool's and the engine's fixed texts and closed codes are not checked, nor the tag of a dollar-quoted literal, which
/// is the engine's text.
/// </para>
/// <para>
/// The check reads the renamed bundle alone, and trusts nothing of the renaming but the mapping: a word the renaming
/// kept that is no word of the engine's, and that no row of the engine's held, is refused here, a spelling an
/// engine's part kept through <see cref="IRenamingScope.Keep" /> among them.
/// </para>
/// </remarks>
internal sealed partial class TokenCheck
{
    private readonly IEngineRenaming _engine;
    private readonly NameMapping _mapping;
    private readonly SqlDialectRules _rules;
    private readonly RenamingCopies _copies;
    private RenamingRefusal? _refusal;

    private TokenCheck(IEngineRenaming engine, NameMapping mapping, RenamingCopies copies)
    {
        _engine = engine;
        _mapping = mapping;
        _rules = SqlDialectRules.For(engine.Dialect);
        _copies = copies;
    }

    /// <summary>The first token that is not safe, as a refusal of <see cref="RefusalStep.Token" />, or null.</summary>
    /// <param name="engine">The engine's part.</param>
    /// <param name="mapping">The bundle's mapping.</param>
    /// <param name="calls">The calls, renamed.</param>
    /// <param name="query">The renamed <c>query.sql</c>.</param>
    /// <param name="expected">The renamed <c>expected.json</c>, or null when the bundle has none.</param>
    public static RenamingRefusal? Check(
        IEngineRenaming engine,
        NameMapping mapping,
        IReadOnlyList<RenamingCall> calls,
        string query,
        string? expected
    )
    {
        var check = new TokenCheck(engine, mapping, RenamingCopies.Of(engine, calls, static call => call.Renamed));
        foreach (var call in calls)
        {
            foreach (var part in (string[])[CaptureRenamer.Request, CaptureRenamer.Answer])
            {
                JsonStrings.Visit(
                    call.Renamed[part],
                    part,
                    found =>
                    {
                        var field = new RenamingField(call.Index, call.Method, found.Path, found.Value, found.Row);
                        check.Field(
                            engine.RuleOf(call.Method, found.Path),
                            field,
                            CaptureRenamer.PlaceOf(call, found.Path)
                        );
                        return null;
                    }
                );
            }
        }

        check.Sql(query, BundleFiles.Query, queryFile: true);
        if (expected is not null)
        {
            check.Entry(expected);
        }

        return check._refusal;
    }

    private void Refuse(string place, string token) =>
        _refusal ??= new RenamingRefusal(RefusalStep.Token, place, token);

    private void Entry(string expected)
    {
        var queries = JsonNode.Parse(expected)![SidecarKeys.Queries]!.AsObject();
        var (name, entry) = queries.Single();
        Name(name, NameClass.Query, EntryRenamer.Place);
        JsonStrings.Visit(
            entry,
            string.Empty,
            found =>
            {
                var place = EntryRenamer.Place + " " + found.Path;
                var field = new RenamingField(
                    RenamingField.NoCall,
                    BundleFiles.Expected,
                    found.Path,
                    found.Value,
                    found.Row
                );
                Field(EntryRenamer.RuleOf(_engine, found.Path), field, place);
                return null;
            }
        );
    }

    private void Field(FieldRule? rule, RenamingField field, string place)
    {
        if (rule is not { } found)
        {
            Refuse(place, field.Value);
            return;
        }

        var value = field.Value;
        switch (found.Kind)
        {
            case FieldRuleKind.Fixed:
            case FieldRuleKind.Closed:
                break;
            case FieldRuleKind.Drop:
                // A dropped member is in no renamed bundle, so one that is holds the user's text.
                Refuse(place, value);
                break;
            case FieldRuleKind.Sql:
            case FieldRuleKind.TypeSpelling:
                Sql(value, place, queryFile: false);
                break;
            case FieldRuleKind.Label:
                Whole(_mapping.IsReplacement(value), place, value);
                break;
            case FieldRuleKind.Parameter:
                Whole(IsParameter(value), place, value);
                break;
            case FieldRuleKind.Name:
                Name(value, RenamingRun.ClassOf(found), place);
                break;
            case FieldRuleKind.WholeName:
                Whole(IsSafe(value, quoted: true), place, value);
                break;
            case FieldRuleKind.Own:
                foreach (var word in _engine.WordsOf(field))
                {
                    OwnWord(word, place);
                }

                break;
            default:
                throw UnexpectedEnumValueException.Of(found.Kind);
        }
    }

    private void Whole(bool safe, string place, string token)
    {
        if (!safe)
        {
            Refuse(place, token);
        }
    }

    // A name: PostgreSQL's ?column? whole, the dialect's name as a logical database, else its stem.
    private void Name(string value, NameClass cls, string place)
    {
        var safe =
            value == SqlRenamer.UnnamedColumn
            || (cls == NameClass.Database && value == SqlDialectName.Canonical(_engine.Dialect))
            || IsSafe(RenamingRun.MarkOf(value).Stem, RenamingRun.IsQuoted(cls));
        Whole(safe, place, value);
    }

    // A word, a quoted name or a literal of an engine's own string.
    private void OwnWord(string word, string place)
    {
        if (word.Length >= 2 && word[0] == '\'' && word[^1] == '\'')
        {
            var content = SqlRenamer.ContentOf(word, 0, word.Length);
            Whole(_mapping.IsReplacement(content), place, content);
        }
        else if (word.Length >= 2 && word[0] == '"' && word[^1] == '"')
        {
            QuotedName(SqlRenamer.ContentOf(word, 0, word.Length), place);
        }
        else
        {
            Whole(IsSafe(word, quoted: false) || IsPunctuation(word), place, word);
        }
    }

    // A parameter's name after its prefix: safe itself, or a copy that the renamed capture records, spelled as its
    // parameter, which is safe, and its suffix.
    private bool IsParameter(string value)
    {
        var name = value.Length > 1 && value[0] == _rules.ParameterPrefix ? value[1..] : value;
        return IsSafe(name, quoted: false)
            || (
                _copies.Find(name) is { } copy
                && IsSafe(copy.Parameter, quoted: false)
                && SqlParameterName.Comparer.Equals(name, copy.Parameter + copy.Suffix)
            );
    }

    // A replacement, a word of the engine's, a spelling the mapping kept from a row of the engine's, or a number.
    private bool IsSafe(string token, bool quoted) =>
        _mapping.IsReplacement(token)
        || _engine.IsEngineWord(token, quoted)
        || _mapping.IsKeptFromEngineRow(token, quoted)
        || SqlRenamer.IsNumber(token);

    private static bool IsPunctuation(string token) =>
        token.Length == 1 && SqlRenamer.Punctuation.Contains(token[0], StringComparison.Ordinal);
}
