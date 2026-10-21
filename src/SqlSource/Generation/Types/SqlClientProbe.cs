using System;
using System.Globalization;
using Microsoft.CodeAnalysis;

namespace SqlSource.Generation.Types;

/// <summary>
/// What a project references of Microsoft.Data.SqlClient: whether it references the driver at all, and which of the
/// versions that the SQL Server map tells apart.  Each flag is the presence of a type that first appeared in that
/// version.  The value is equal from one edit to the next, so nothing after the probe runs again.
/// </summary>
/// <param name="Referenced">
/// Whether <c>Microsoft.Data.SqlClient.SqlConnection</c>, which every version has, is found.
/// </param>
/// <param name="AtLeast61">
/// Whether <c>Microsoft.Data.SqlTypes.SqlVector`1</c>, first in 6.1 on every target framework, is found.
/// </param>
/// <param name="AtLeast7">
/// Whether <c>Microsoft.Data.SqlClient.SspiContextProvider</c>, first in 7.0 on every target framework and in no 6.x,
/// is found.
/// </param>
/// <param name="Version">
/// The version of the newest referenced assembly named <c>Microsoft.Data.SqlClient</c>: its informational version up to
/// a <c>+</c>, which is the version of its package, or, without one, the major of its identity alone, since the
/// identity of a package's assembly keeps its major and no more.  Null when there is none.  For the diagnostics alone:
/// the flags choose the column.
/// </param>
internal sealed record SqlClientProbe(bool Referenced, bool AtLeast61, bool AtLeast7, string? Version)
{
    private const string AssemblyName = "Microsoft.Data.SqlClient";

    private const string InformationalVersionAttribute = "System.Reflection.AssemblyInformationalVersionAttribute";

    // The newest major whose types the map knows.
    private const int NewestKnownMajor = 7;

    /// <summary>The column of the SQL Server map that the driver chooses.</summary>
    public SqlClientColumn Column =>
        (AtLeast7, Referenced) switch
        {
            (true, _) => SqlClientColumn.SqlClient7,
            (_, true) => SqlClientColumn.SqlClient61,
            _ => SqlClientColumn.None,
        };

    /// <summary>
    /// Whether the project references a Microsoft.Data.SqlClient older than 6.1, which is <c>SQLSRC267</c>.
    /// </summary>
    public bool IsBelowTheFloor => Referenced && !AtLeast61;

    /// <summary>
    /// Whether the project references a Microsoft.Data.SqlClient of a major newer than the map knows, which is
    /// <c>SQLSRC268</c>.
    /// </summary>
    public bool IsNewerThanKnown => Referenced && LeadingInteger(Version) > NewestKnownMajor;

    /// <summary>The version as a diagnostic names it.</summary>
    public string VersionText => Version ?? "of an unknown version";

    /// <summary>
    /// Reads the probe from <paramref name="compilation" />: three type lookups and the identities of its references.
    /// Of two references named <c>Microsoft.Data.SqlClient</c>, the newer gives the version.  Never throws.
    /// </summary>
    public static SqlClientProbe Read(Compilation compilation) =>
        new(
            Has(compilation, "Microsoft.Data.SqlClient.SqlConnection"),
            Has(compilation, "Microsoft.Data.SqlTypes.SqlVector`1"),
            Has(compilation, "Microsoft.Data.SqlClient.SspiContextProvider"),
            ReadVersion(compilation)
        );

    private static string? ReadVersion(Compilation compilation)
    {
        // The module's references are already bound, so a name is checked before anything of an assembly is read.
        IAssemblySymbol? newest = null;
        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            if (
                string.Equals(assembly.Identity.Name, AssemblyName, StringComparison.OrdinalIgnoreCase)
                && (newest is null || assembly.Identity.Version > newest.Identity.Version)
            )
            {
                newest = assembly;
            }
        }

        return newest is null ? null : PackageVersionOf(newest);
    }

    // The informational version of a package's assembly is its package version followed by +, and the commit.  An
    // attribute that is missing, has no argument or has one that is not text gives the identity's major.
    private static string PackageVersionOf(IAssemblySymbol assembly)
    {
        foreach (var attribute in assembly.GetAttributes())
        {
            if (
                attribute.AttributeClass?.ToDisplayString() == InformationalVersionAttribute
                && attribute.ConstructorArguments.Length > 0
                && attribute.ConstructorArguments[0].Value is string text
            )
            {
                var end = text.IndexOf('+');
                var version = (end < 0 ? text : text.Substring(0, end)).Trim();
                if (version.Length > 0)
                {
                    return version;
                }
            }
        }

        return assembly.Identity.Version.Major.ToString(CultureInfo.InvariantCulture);
    }

    // The digits that start a version, or zero for a version that does not start with any or is too large.
    private static int LeadingInteger(string? version)
    {
        if (version is null)
        {
            return 0;
        }

        var length = 0;
        while (length < version.Length && version[length] is >= '0' and <= '9')
        {
            length++;
        }

        return int.TryParse(
            version.Substring(0, length),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var major
        )
            ? major
            : 0;
    }

    private static bool Has(Compilation compilation, string metadataName) =>
        !compilation.GetTypesByMetadataName(metadataName).IsEmpty;
}
