using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using SqlSource.Tool.Describing.Postgres;

namespace SqlSource.Tool.Bundles.Renaming.Postgres;

/// <summary>
/// PostgreSQL's settings and errors as a bundle holds them: the schemas of <c>search_path</c> renamed, the values of
/// the other settings <c>open</c> reads kept, and an error's message replaced by the tool's own text.
/// </summary>
/// <remarks>
/// A decision reads a message only as one of the four constant texts the tool writes into an answer and compares, and
/// for the first <c>$n</c> of a <c>42P18</c> or <c>42P08</c>, the parameter it names.  So a message keeps that much and
/// nothing of the server's text, which may quote the user's names and values.
/// </remarks>
internal static class PostgresOwnStrings
{
    // The setting whose value holds the user's names, and the members the rules read of the object that holds a value.
    private const string SearchPath = "search_path";
    private const string SettingName = "name";
    private const string SqlState = "sqlState";

    // The text of a message that names a parameter, before its number.
    private const string ParameterText = "parameter $";

    // The elements of search_path that are the engine's, as PostgreSQL folds them.
    private static readonly FrozenSet<string> EngineSchemas = FrozenSet.ToFrozenSet(
        ["$user", "public", SystemSchemas.Postgres],
        StringComparer.Ordinal
    );

    // The settings open reads whose values are the server's closed words, every one but search_path.
    private static readonly FrozenSet<string> KeptSettings = PostgresLive
        .SettingNames.Where(static name => name != SearchPath)
        .ToFrozenSet(StringComparer.Ordinal);

    // The texts the tool writes into an answer and a decision compares, kept whatever the SQLSTATE.
    private static readonly FrozenSet<string> ToolTexts = FrozenSet.ToFrozenSet(
        [
            ConnectionErrors.MultiplexingRefused,
            ConnectionErrors.UnreadableTypes,
            PostgresLive.StatementNotListed,
            PostgresLive.TransactionRefused,
        ],
        StringComparer.Ordinal
    );

    /// <summary>
    /// A value of <c>open</c>'s settings renamed, by the setting's <c>name</c> in <see cref="RenamingField.Row" />:
    /// each schema of <c>search_path</c> through the scope but <c>$user</c>, <c>public</c> and <c>pg_catalog</c>, a
    /// quoted one written back quoted; the value of <c>plan_cache_mode</c> and of
    /// <c>standard_conforming_strings</c> kept.  A setting of any other name, and a <c>search_path</c> that
    /// <see cref="PostgresSearchPath" /> cannot read, are refused.
    /// </summary>
    public static string Setting(RenamingField field, IRenamingScope scope)
    {
        var name = NameOf(field);
        if (name == SearchPath)
        {
            if (PostgresSearchPath.Schemas(field.Value) is not { } schemas)
            {
                Refuse(field, scope, field.Value);
                return string.Empty;
            }

            return string.Join(
                ", ",
                schemas.Select(schema =>
                    Written(
                        IsEngines(schema) ? schema.Name : scope.Name(schema.Name, schema.Quoted, NameClass.Schema),
                        schema.Quoted
                    )
                )
            );
        }

        if (name is not null && KeptSettings.Contains(name))
        {
            return field.Value;
        }

        Refuse(field, scope, name ?? field.Value);
        return string.Empty;
    }

    /// <summary>
    /// The words of a renamed setting's value: each schema of <c>search_path</c> but the engine's, a quoted one as
    /// <c>"..."</c>, and nothing of a kept setting's value.  The value whole when it does not read, or is of any other
    /// setting, so that the token check refuses it.
    /// </summary>
    public static IEnumerable<string> WordsOfSetting(RenamingField field)
    {
        var name = NameOf(field);
        if (name == SearchPath)
        {
            return PostgresSearchPath.Schemas(field.Value) is { } schemas
                ? schemas.Where(static schema => !IsEngines(schema)).Select(static s => Written(s.Name, s.Quoted))
                : [field.Value];
        }

        return name is not null && KeptSettings.Contains(name) ? [] : [field.Value];
    }

    /// <summary>
    /// An error's message as the tool writes it: one of the tool's constant texts kept; <c>parameter $&lt;n&gt;</c>
    /// for a <c>42P18</c> or <c>42P08</c> that names one, by the <c>sqlState</c> of <see cref="RenamingField.Row" />;
    /// else the empty string, which <c>check</c>'s <c>driverError.message</c> always is.
    /// </summary>
    /// <param name="field">The message.</param>
    /// <param name="scope">Unused: a message the tool writes holds no name.</param>
    public static string Message(RenamingField field, IRenamingScope scope)
    {
        _ = scope;
        if (field.Method == PostgresCallNames.Check && field.Path == PostgresFieldRules.DriverErrorMessage)
        {
            return string.Empty;
        }

        if (ToolTexts.Contains(field.Value))
        {
            return field.Value;
        }

        return
            SqlStateOf(field) is PostgresErrors.IndeterminateDatatype or PostgresErrors.AmbiguousParameter
            && PostgresErrors.FirstParameterNumberIn(field.Value) is { } number
            ? ParameterText + number.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
    }

    /// <summary>
    /// The words of a renamed message: nothing for one of the forms <see cref="Message" /> writes, and the value whole
    /// for any other, which the renaming did not write and the token check refuses.
    /// </summary>
    public static IEnumerable<string> WordsOfMessage(RenamingField field) =>
        field.Value.Length == 0 || ToolTexts.Contains(field.Value) || IsParameterText(field.Value) ? [] : [field.Value];

    private static string? NameOf(RenamingField field) =>
        field.Row?[SettingName] is JsonValue value && value.TryGetValue<string>(out var name) ? name : null;

    private static string? SqlStateOf(RenamingField field) =>
        field.Row?[SqlState] is JsonValue value && value.TryGetValue<string>(out var state) ? state : null;

    private static void Refuse(RenamingField field, IRenamingScope scope, string token) =>
        scope.Refuse(new RenamingRefusal(RefusalStep.Token, CaptureRenamer.PlaceOf(field), token));

    // An element the engine names, or an empty one, which holds nothing of the user's.
    private static bool IsEngines((string Name, bool Quoted) schema) =>
        schema.Name.Length == 0 || EngineSchemas.Contains(PostgresWords.Fold(schema.Name, schema.Quoted));

    private static string Written(string name, bool quoted) => quoted ? PostgresIdentifiers.Quote(name) : name;

    private static bool IsParameterText(string value) =>
        value.Length > ParameterText.Length
        && value.StartsWith(ParameterText, StringComparison.Ordinal)
        && !value.AsSpan(ParameterText.Length).ContainsAnyExceptInRange('0', '9');
}
