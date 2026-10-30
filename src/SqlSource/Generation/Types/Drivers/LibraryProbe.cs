using System;
using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using SqlSource.Settings;
using SqlSource.Settings.Libraries;

namespace SqlSource.Generation.Types.Drivers;

/// <summary>
/// Which libraries a project references, which the library switch needs to know whether a library can be on: whether
/// it references NodaTime and NetTopologySuite, the plugins that let Npgsql read and write their types, and
/// NetTopologySuite's reader and writer of SQL Server's bytes.  Each flag is the presence of a type that the library
/// has.  The value is equal from one edit to the next, so nothing after the probe runs again.
/// It is not an <see cref="IDriverProbe" />, since it reports nothing by itself.
/// </summary>
/// <param name="NodaTime">Whether <c>NodaTime.Instant</c> is found.</param>
/// <param name="NpgsqlNodaTime">
/// Whether <c>Npgsql.NpgsqlNodaTimeExtensions</c>, of the Npgsql.NodaTime plugin, is found.
/// </param>
/// <param name="NodaTimeVersion">
/// The version of the newest referenced assembly named <c>NodaTime</c>, or null when there is none.  It is for the
/// floor on NodaTime's version: the flags decide what is available.
/// </param>
/// <param name="NetTopologySuite">Whether <c>NetTopologySuite.Geometries.Geometry</c> is found.</param>
/// <param name="NetTopologySuiteAtFloor">
/// Whether <c>NetTopologySuite.Algorithm.Hull.ConcaveHull</c>, first in 2.5.0, is found, since the identity of every
/// 2.x is 2.0.0.0.
/// </param>
/// <param name="NetTopologySuiteVersion">
/// The version of the package of the newest assembly named <c>NetTopologySuite</c> that defines <c>Geometry</c>, from
/// its informational version, or null when there is none or it gives none.  For the diagnostics alone.
/// </param>
/// <param name="NpgsqlNetTopologySuite">
/// Whether <c>Npgsql.NpgsqlNetTopologySuiteExtensions</c>, of the Npgsql.NetTopologySuite plugin, is found.
/// </param>
/// <param name="SqlServerBytes">
/// Whether <c>NetTopologySuite.IO.SqlServerBytesReader</c>, of NetTopologySuite.IO.SqlServerBytes, is found.
/// </param>
/// <param name="SqlServerBytesVersion">
/// The version of the package of the newest assembly named <c>NetTopologySuite.IO.SqlServerBytes</c> that defines
/// <c>SqlServerBytesReader</c>, as <paramref name="NetTopologySuiteVersion" /> is read, since 2.0.0 and 2.1.0 have one
/// identity.  For the diagnostics alone.
/// </param>
internal sealed record LibraryProbe(
    bool NodaTime,
    bool NpgsqlNodaTime,
    Version? NodaTimeVersion,
    bool NetTopologySuite = false,
    bool NetTopologySuiteAtFloor = false,
    string? NetTopologySuiteVersion = null,
    bool NpgsqlNetTopologySuite = false,
    bool SqlServerBytes = false,
    string? SqlServerBytesVersion = null
)
{
    private const string NodaTimeAssemblyName = "NodaTime";

    private const string NetTopologySuiteAssemblyName = "NetTopologySuite";

    private const string SqlServerBytesAssemblyName = "NetTopologySuite.IO.SqlServerBytes";

    // The oldest NodaTime whose types SqlSource's rows and conversions name.
    private static readonly Version NodaTimeFloor = new(3, 1, 9, 0);

    /// <summary>
    /// Whether the project references what <paramref name="library" /> needs on <paramref name="engine" />: on
    /// PostgreSQL the library and its Npgsql plugin; on SQL Server NodaTime alone, and NetTopologySuite with its reader
    /// and writer of SQL Server's bytes.
    /// </summary>
    public bool IsAvailable(SqlLibrary library, DatabaseEngine engine) =>
        library switch
        {
            SqlLibrary.NodaTime => engine == DatabaseEngine.PostgreSql ? NodaTime && NpgsqlNodaTime : NodaTime,
            SqlLibrary.NetTopologySuite => NetTopologySuite
                && (engine == DatabaseEngine.PostgreSql ? NpgsqlNetTopologySuite : SqlServerBytes),
            _ => false,
        };

    /// <summary>
    /// The state that applies when no setting chooses one: on, on PostgreSQL only, when the project references what
    /// <paramref name="library" /> needs there, and off otherwise.
    /// </summary>
    public LibraryState DefaultOf(SqlLibrary library, DatabaseEngine engine) =>
        engine == DatabaseEngine.PostgreSql && IsAvailable(library, engine) ? LibraryState.On : LibraryState.Off;

    /// <summary>
    /// The assemblies that a project has to reference to use <paramref name="library" /> on
    /// <paramref name="engine" />, as a diagnostic names them: the plugin on PostgreSQL, which brings its library;
    /// NodaTime on SQL Server, and NetTopologySuite.IO.SqlServerBytes, which brings NetTopologySuite.
    /// </summary>
    public static string MissingAssembliesOf(SqlLibrary library, DatabaseEngine engine) =>
        library switch
        {
            SqlLibrary.NodaTime => engine == DatabaseEngine.PostgreSql ? "Npgsql.NodaTime" : "NodaTime",
            SqlLibrary.NetTopologySuite => engine == DatabaseEngine.PostgreSql
                ? "Npgsql.NetTopologySuite"
                : "NetTopologySuite.IO.SqlServerBytes",
            _ => "",
        };

    /// <summary>
    /// Whether the project references <paramref name="library" /> older than its floor: for NodaTime, an assembly
    /// named <c>NodaTime</c> older than 3.1.9 that has its types.  A library whose assembly gives no version, as one
    /// merged into an assembly of another name, is not below it.
    /// </summary>
    public bool IsBelowTheFloor(SqlLibrary library) =>
        library switch
        {
            SqlLibrary.NodaTime => NodaTime && NodaTimeVersion is { } version && version < NodaTimeFloor,
            SqlLibrary.NetTopologySuite or _ => false,
        };

    /// <summary>
    /// The oldest version of <paramref name="library" /> that SqlSource supports, as a diagnostic gives it.
    /// </summary>
    public static string FloorOf(SqlLibrary library) =>
        library switch
        {
            SqlLibrary.NodaTime => TextOf(NodaTimeFloor),
            SqlLibrary.NetTopologySuite or _ => "",
        };

    /// <summary>
    /// The version of <paramref name="library" /> that the project references, as a diagnostic gives it: the major,
    /// the minor and the build, as <c>3.1.8</c>.  Empty when there is none.
    /// </summary>
    public string VersionTextOf(SqlLibrary library) =>
        library switch
        {
            SqlLibrary.NodaTime => NodaTimeVersion is { } version ? TextOf(version) : "",
            SqlLibrary.NetTopologySuite or _ => "",
        };

    /// <summary>
    /// Reads the probe from <paramref name="compilation" />: six type lookups, the identities of its references, and
    /// the informational versions of the assemblies of two types.  Of two references named <c>NodaTime</c>, the newer
    /// gives the version.  Never throws.
    /// </summary>
    public static LibraryProbe Read(Compilation compilation)
    {
        Version? version = null;
        foreach (var identity in compilation.ReferencedAssemblyNames)
        {
            if (
                string.Equals(identity.Name, NodaTimeAssemblyName, StringComparison.OrdinalIgnoreCase)
                && (version is null || identity.Version > version)
            )
            {
                version = identity.Version;
            }
        }

        var geometries = compilation.GetTypesByMetadataName("NetTopologySuite.Geometries.Geometry");
        var readers = compilation.GetTypesByMetadataName("NetTopologySuite.IO.SqlServerBytesReader");
        return new LibraryProbe(
            DriverProbes.Has(compilation, "NodaTime.Instant"),
            DriverProbes.Has(compilation, "Npgsql.NpgsqlNodaTimeExtensions"),
            version,
            !geometries.IsEmpty,
            DriverProbes.Has(compilation, "NetTopologySuite.Algorithm.Hull.ConcaveHull"),
            PackageVersionOf(geometries, NetTopologySuiteAssemblyName),
            DriverProbes.Has(compilation, "Npgsql.NpgsqlNetTopologySuiteExtensions"),
            !readers.IsEmpty,
            PackageVersionOf(readers, SqlServerBytesAssemblyName)
        );
    }

    // The version of the package of the newest assembly of the name that defines one of the types, or null.
    private static string? PackageVersionOf(ImmutableArray<INamedTypeSymbol> types, string assemblyName) =>
        DriverProbes.NewestNamed(types, assemblyName) is { } assembly ? DriverProbes.PackageVersionOf(assembly) : null;

    // The major, the minor and the build, which a version of an assembly always has, without throwing on one that has
    // fewer.
    private static string TextOf(Version version) =>
        string.Join(
            ".",
            version.Major.ToString(CultureInfo.InvariantCulture),
            version.Minor.ToString(CultureInfo.InvariantCulture),
            Math.Max(version.Build, 0).ToString(CultureInfo.InvariantCulture)
        );
}
