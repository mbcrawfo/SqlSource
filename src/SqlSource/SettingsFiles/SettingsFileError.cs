using Microsoft.CodeAnalysis.Text;

namespace SqlSource.SettingsFiles;

/// <summary>
/// A problem of a settings file.  <see cref="SettingsFileErrorKind" /> says what each field holds for each kind.
/// </summary>
/// <param name="Kind">What is wrong.</param>
/// <param name="Span">Where it is, as offsets into the text that was read.</param>
/// <param name="Setting">The key of the top level whose value is wrong, when the kind has one.</param>
/// <param name="Argument">The text a message quotes first, when the kind has one.</param>
/// <param name="Other">The text a message quotes second, when the kind has one.</param>
/// <param name="IsObjectForm">
/// For a member of the column types that is not valid, whether it is written as an object, so that the message can say
/// what that form takes.
/// </param>
internal sealed record SettingsFileError(
    SettingsFileErrorKind Kind,
    TextSpan Span,
    string? Setting,
    string? Argument,
    string? Other,
    bool IsObjectForm = false
)
{
    /// <summary>
    /// Whether the error is about the file as a file, which both the build and the tool report.  A value that is not
    /// valid, and a repeated key, are reported only by the side that reads <see cref="Setting" />.
    /// </summary>
    public bool IsAboutTheFile => Kind is not (SettingsFileErrorKind.InvalidValue or SettingsFileErrorKind.RepeatedKey);
}
