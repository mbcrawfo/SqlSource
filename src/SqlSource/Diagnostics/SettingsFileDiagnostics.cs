using System;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Parsing;
using SqlSource.Settings;
using SqlSource.Settings.Libraries;
using SqlSource.Settings.TypeOverrides;
using SqlSource.SettingsFiles;
using SqlSource.Snapshot.Reading;

namespace SqlSource.Diagnostics;

/// <summary>
/// Turns a problem of a settings file into its diagnostic, <c>SQLSRC015</c> to <c>SQLSRC020</c>.  This is the one
/// place a <see cref="SettingsFileError" /> becomes a diagnostic, for the build and the tool alike.
/// </summary>
internal static class SettingsFileDiagnostics
{
    private const string GeneratorParametersDefaultHelp =
        ".  'default' is the empty list and stands alone: write [] for it.";

    // The end of SQLSRC019's message for a member of libraryTypes: the names of the libraries, as the manual spells
    // them, and the states.
    private static readonly string LibraryTypesHelp =
        ".  'libraryTypes' takes the name of a library ("
        + JoinOr([.. Enum.GetNames(typeof(SqlLibrary)).Select(LowerFirst)])
        + ") and "
        + JoinOr([.. Enum.GetValues(typeof(LibraryState)).Cast<LibraryState>().Select(SettingValue.NameOf)])
        + ".";

    // What SQLSRC018 says a key takes when the error names no key of the table, which the reader never gives.
    private const string OtherType = "another JSON type";

    private static readonly string InvalidJsonReason =
        "the text is not JSON, or nests deeper than "
        + SidecarTokenizer.MaxDepth.ToString(CultureInfo.InvariantCulture)
        + " levels";

    /// <summary>
    /// The diagnostic of <paramref name="error" />, at the line and column of its span in <paramref name="text" />, the
    /// text the error was read from.
    /// </summary>
    /// <param name="error">The problem.</param>
    /// <param name="path">The path of the settings file, as the diagnostic shows it.</param>
    /// <param name="text">The text of the settings file.</param>
    public static DiagnosticInfo Create(SettingsFileError error, string path, SourceText text)
    {
        var location = LocationInfo.From(path, text, error.Span);
        var argument = error.Argument ?? string.Empty;
        var setting = error.Setting ?? string.Empty;
        return error.Kind switch
        {
            SettingsFileErrorKind.InvalidJson => DiagnosticInfo.Create(
                SqlDiagnostics.SettingsFileNotAnObject,
                location,
                InvalidJsonReason
            ),
            SettingsFileErrorKind.NotAnObject => DiagnosticInfo.Create(
                SqlDiagnostics.SettingsFileNotAnObject,
                location,
                "its top level is not an object"
            ),
            SettingsFileErrorKind.UnknownKey => DiagnosticInfo.Create(
                SqlDiagnostics.SettingsFileUnknownKey,
                location,
                argument
            ),
            SettingsFileErrorKind.DuplicateKey => DiagnosticInfo.Create(
                SqlDiagnostics.SettingsFileDuplicateKey,
                location,
                argument
            ),
            SettingsFileErrorKind.WrongType => DiagnosticInfo.Create(
                SqlDiagnostics.SettingsFileWrongType,
                location,
                setting,
                SettingsFileKeys.All.Contains(setting) ? TypeNameOf(SettingsFileKeys.TypeOf(setting)) : OtherType
            ),
            SettingsFileErrorKind.InvalidValue => DiagnosticInfo.Create(
                SqlDiagnostics.SettingsFileInvalidValue,
                location,
                error.Other is { } type ? argument + ": " + type : argument,
                setting,
                HelpOf(error, argument)
            ),
            SettingsFileErrorKind.RepeatedKey => DiagnosticInfo.Create(
                SqlDiagnostics.SettingsFileRepeatedKey,
                location,
                argument,
                error.Other ?? string.Empty,
                setting
            ),
            // A kind that is no member names its number, never another kind's.
            _ => DiagnosticInfo.Create(
                SqlDiagnostics.SettingsFileNotAnObject,
                location,
                "it has an error of kind " + ((int)error.Kind).ToString(CultureInfo.InvariantCulture)
            ),
        };
    }

    // What SQLSRC018 says a key takes.
    private static string TypeNameOf(SettingsFileValueType type) =>
        type switch
        {
            SettingsFileValueType.String => "a string",
            SettingsFileValueType.Boolean => "true or false",
            SettingsFileValueType.ArrayOfStrings => "an array of strings",
            SettingsFileValueType.ObjectOfStrings => "an object whose values are strings",
            _ => OtherType,
        };

    // The end of SQLSRC019's message, after the setting: the names of the dialects, why default stands alone, the
    // forms of a type override's key that is written in brackets, or what libraryTypes takes.  Empty for every other
    // value.
    private static string HelpOf(SettingsFileError error, string argument) =>
        error.Setting switch
        {
            SettingsFileKeys.Dialect => ".  'dialect' takes " + SqlDialectName.Accepted + ".",
            SettingsFileKeys.GeneratorParameters when GeneratorParameterList.IsDefault(argument.AsSpan()) =>
                GeneratorParametersDefaultHelp,
            SettingsFileKeys.LibraryTypes => LibraryTypesHelp,
            SettingsFileKeys.TypeOverrides when error.Other is not null => TypeOverrideGrammar.BracketHelp(
                argument.AsSpan()
            ),
            _ => string.Empty,
        };

    private static string LowerFirst(string name) =>
        char.ToLowerInvariant(name[0]).ToString(CultureInfo.InvariantCulture) + name.Substring(1);

    // "a", "a or b", "a, b or c".
    private static string JoinOr(string[] words) =>
        words.Length < 2
            ? string.Concat(words)
            : string.Join(", ", words.Take(words.Length - 1)) + " or " + words[words.Length - 1];
}
