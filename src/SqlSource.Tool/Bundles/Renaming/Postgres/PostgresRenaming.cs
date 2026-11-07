using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Postgres;

namespace SqlSource.Tool.Bundles.Renaming.Postgres;

/// <summary>
/// PostgreSQL's part of the renaming, which <see cref="EngineRenamings.For" /> gives: its words by
/// <see cref="PostgresWords" />, its members' rules and its own rows by <see cref="PostgresFieldRules" />, its settings
/// and errors by <see cref="PostgresOwnStrings" />, its plans by <see cref="PostgresPlanRenamer" />, and what its
/// pipeline makes by <see cref="PostgresDerivations" />.
/// </summary>
internal sealed class PostgresRenaming : IEngineRenaming
{
    // The members of an Own rule, as PostgresFieldRules gives them.
    private const string SettingValue = "answer.settings[].value";
    private const string ErrorMessage = "answer.error.message";
    private const string DriverErrorMessage = "answer.driverError.message";
    private const string Plan = "answer.plan";

    private PostgresRenaming() { }

    /// <summary>The one instance, which holds nothing of a bundle.</summary>
    public static PostgresRenaming Instance { get; } = new();

    /// <inheritdoc />
    public SqlDialect Dialect => SqlDialect.PostgreSql;

    /// <summary>
    /// An ASCII digit: a <c>$</c> before a digit is a parameter or a dollar quote, which the lexer reads apart.
    /// </summary>
    public bool IsNumberStart(string text, int at, int end) => char.IsAsciiDigit(text[at]);

    /// <inheritdoc />
    public bool IsWordStart(char character) => PostgresWords.IsWordStart(character);

    /// <inheritdoc />
    public bool IsWordPart(char character) => PostgresWords.IsWordPart(character);

    /// <inheritdoc />
    public string Fold(string spelling, bool quoted) => PostgresWords.Fold(spelling, quoted);

    /// <inheritdoc />
    public string Spelled(string spelling, bool quoted) => PostgresWords.Fold(spelling, quoted);

    /// <inheritdoc />
    public bool IsEngineWord(string spelling, bool quoted) => PostgresWords.IsEngineWord(spelling, quoted);

    /// <inheritdoc />
    public FieldRule? RuleOf(string method, string path) => PostgresFieldRules.RuleOf(method, path);

    /// <inheritdoc />
    public bool IsEngineRow(string method, string path, JsonObject row) => PostgresFieldRules.IsEngineRow(row);

    /// <summary>None: PostgreSQL's pipeline makes no copy of a parameter.</summary>
    public IEnumerable<ParameterCopy> CopiesIn(string method, JsonObject call) => [];

    /// <summary>The names of an <c>explain</c> call's plan; no other call has a name only the engine finds.</summary>
    public void Register(RenamingCall call, IRenamingScope scope)
    {
        if (call.Method == PostgresCallNames.Explain)
        {
            PostgresPlanRenamer.Register(call, scope);
        }
    }

    /// <summary>
    /// <c>open</c>'s setting, an error's message, <c>check</c>'s driver error or <c>explain</c>'s plan, by the
    /// field's method and path.
    /// </summary>
    /// <exception cref="InvalidOperationException">The field is no member that has the rule.</exception>
    public string RenameOwn(RenamingField field, IRenamingScope scope)
    {
        var own = OwnOf(field);
        return own switch
        {
            Own.Setting => PostgresOwnStrings.Setting(field, scope),
            Own.Message => PostgresOwnStrings.Message(field, scope),
            Own.Plan => PostgresPlanRenamer.Rename(field, scope),
            _ => throw UnexpectedEnumValueException.Of(own),
        };
    }

    /// <summary>The words of a renamed member, by the same dispatch as <see cref="RenameOwn" />.</summary>
    /// <exception cref="InvalidOperationException">The field is no member that has the rule.</exception>
    public IEnumerable<string> WordsOf(RenamingField field)
    {
        var own = OwnOf(field);
        return own switch
        {
            Own.Setting => PostgresOwnStrings.WordsOfSetting(field),
            Own.Message => PostgresOwnStrings.WordsOfMessage(field),
            Own.Plan => PostgresPlanRenamer.WordsOf(field),
            _ => throw UnexpectedEnumValueException.Of(own),
        };
    }

    /// <inheritdoc />
    public void Derive(RenamingCall call, IRenamingScope scope) => PostgresDerivations.Derive(call, scope);

    // Which of PostgreSQL's own strings a field is.  The field table gives the rule to these members alone, so any
    // other is a bug.
    private static Own OwnOf(RenamingField field) =>
        (field.Method, field.Path) switch
        {
            (PostgresCallNames.Open, SettingValue) => Own.Setting,
            (_, ErrorMessage) => Own.Message,
            (PostgresCallNames.Check, DriverErrorMessage) => Own.Message,
            (PostgresCallNames.Explain, Plan) => Own.Plan,
            _ => throw new InvalidOperationException(
                $"The member '{field.Method} {field.Path}' has no rule {nameof(FieldRuleKind.Own)} of PostgreSQL's."
            ),
        };

    private enum Own
    {
        Setting,
        Message,
        Plan,
    }
}
