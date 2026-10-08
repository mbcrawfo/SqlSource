using Microsoft.CodeAnalysis.Diagnostics;
using SqlSource.Parsing;

namespace SqlSource.Generation;

/// <summary>
/// What MSBuild says about the dialect, for the project or for one file.  A <c>dialect=</c> generator parameter in a
/// file comes before both.
/// </summary>
/// <param name="Dialect">
/// The dialect that is set, with its options, or null when none is.  <see cref="SqlDialect.Ansi" /> with no
/// options when the value is not valid.
/// </param>
/// <param name="InvalidValue">The value as written when it is not valid, and null otherwise.</param>
internal sealed record DialectSetting(SqlDialectChoice? Dialect, string? InvalidValue)
{
    /// <summary>
    /// Where the compiler puts the <c>SqlSourceDialect</c> property of the project.  It is there only because
    /// <c>build/SqlSource.props</c> lists the property as a <c>CompilerVisibleProperty</c>.
    /// </summary>
    public const string PropertyName = "build_property.SqlSourceDialect";

    /// <summary>
    /// Where the compiler puts the <c>SqlSourceDialect</c> metadata of one <c>AdditionalFiles</c> item.  It is there
    /// only because <c>build/SqlSource.props</c> lists it as a <c>CompilerVisibleItemMetadata</c> of
    /// <c>SqlSourceDialectFile</c>, and <c>build/SqlSource.targets</c> puts the items that have a dialect there.
    /// </summary>
    public const string MetadataName = "build_metadata.SqlSourceDialectFile.SqlSourceDialect";

    private static readonly DialectSetting NotSet = new(null, null);

    /// <summary>Reads the project's property from the options that hold for the whole compilation.</summary>
    public static DialectSetting ReadProperty(AnalyzerConfigOptions globalOptions) =>
        globalOptions.TryGetValue(PropertyName, out var value) ? Parse(value) : NotSet;

    /// <summary>Reads a file's metadata from the options of that file.</summary>
    public static DialectSetting ReadMetadata(AnalyzerConfigOptions fileOptions) =>
        fileOptions.TryGetValue(MetadataName, out var value) ? Parse(value) : NotSet;

    public static DialectSetting Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return NotSet;
        }

        return SqlDialectName.TryParse(value, out var dialect)
            ? new DialectSetting(dialect, null)
            : new DialectSetting(default(SqlDialectChoice), value);
    }
}
