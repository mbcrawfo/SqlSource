using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using SqlSource.Settings;
using SqlSource.Settings.Libraries;

namespace SqlSource.Generation.Types.Drivers;

/// <summary>
/// Which libraries a project references, which the library switch needs to know whether a library can be on: whether
/// it references NodaTime and NetTopologySuite, the plugins that let Npgsql read and write their types,
/// NetTopologySuite's reader and writer of SQL Server's bytes, and System.Text.Json.  Each flag is the presence of a
/// type that the library has.  The value is equal from one edit to the next, so nothing after the probe runs again.
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
/// <param name="Json">Whether <c>System.Text.Json.JsonElement</c> is found.</param>
internal sealed record LibraryProbe(
    bool NodaTime,
    bool NpgsqlNodaTime,
    Version? NodaTimeVersion,
    bool NetTopologySuite = false,
    bool NetTopologySuiteAtFloor = false,
    string? NetTopologySuiteVersion = null,
    bool NpgsqlNetTopologySuite = false,
    bool SqlServerBytes = false,
    string? SqlServerBytesVersion = null,
    bool Json = false
)
{
    private const string NodaTimeAssemblyName = "NodaTime";

    private const string NetTopologySuiteAssemblyName = "NetTopologySuite";

    private const string SqlServerBytesAssemblyName = "NetTopologySuite.IO.SqlServerBytes";

    // What a diagnostic says of a package whose assembly gives no version.
    private const string UnknownVersion = "of an unknown version";

    // The oldest NodaTime whose types SqlSource's rows and conversions name.
    private static readonly Version NodaTimeFloor = new(3, 1, 9, 0);

    // The oldest NetTopologySuite that Npgsql.NetTopologySuite 8.0 takes.
    private static readonly Version NetTopologySuiteFloor = new(2, 5, 0);

    // The oldest reader and writer of SQL Server's bytes that write M values and check a geography polygon for
    // emptiness before its ring orientation.
    private static readonly Version SqlServerBytesFloor = new(2, 1, 0);

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
            SqlLibrary.Json => Json,
            _ => false,
        };

    /// <summary>
    /// The state that applies when no setting chooses one: for NodaTime and NetTopologySuite, on, on PostgreSQL only,
    /// when the project references what the library needs there, and off otherwise; for JSON, off on both engines,
    /// since a string is what a project written before the library reads.
    /// </summary>
    public LibraryState DefaultOf(SqlLibrary library, DatabaseEngine engine) =>
        library switch
        {
            SqlLibrary.NodaTime or SqlLibrary.NetTopologySuite => engine == DatabaseEngine.PostgreSql
            && IsAvailable(library, engine)
                ? LibraryState.On
                : LibraryState.Off,
            SqlLibrary.Json or _ => LibraryState.Off,
        };

    /// <summary>
    /// The assemblies that a project has to reference to use <paramref name="library" /> on
    /// <paramref name="engine" />, as a diagnostic names them: the plugin on PostgreSQL, which brings its library;
    /// NodaTime on SQL Server, and NetTopologySuite.IO.SqlServerBytes, which brings NetTopologySuite; System.Text.Json
    /// for JSON, on both.
    /// </summary>
    public static string MissingAssembliesOf(SqlLibrary library, DatabaseEngine engine) =>
        library switch
        {
            SqlLibrary.NodaTime => engine == DatabaseEngine.PostgreSql ? "Npgsql.NodaTime" : "NodaTime",
            SqlLibrary.NetTopologySuite => engine == DatabaseEngine.PostgreSql
                ? "Npgsql.NetTopologySuite"
                : "NetTopologySuite.IO.SqlServerBytes",
            SqlLibrary.Json => "System.Text.Json",
            _ => "",
        };

    /// <summary>
    /// Whether the project references a package of <paramref name="library" /> that <paramref name="engine" />'s
    /// queries use older than its floor, as <see cref="PackagesBelowTheFloor" /> names them.
    /// </summary>
    public bool IsBelowTheFloor(SqlLibrary library, DatabaseEngine engine) =>
        library switch
        {
            SqlLibrary.NodaTime => IsNodaTimeBelowTheFloor,
            SqlLibrary.NetTopologySuite => IsNetTopologySuiteBelowTheFloor
                || (engine == DatabaseEngine.SqlServer && IsSqlServerBytesBelowTheFloor),
            SqlLibrary.Json or _ => false,
        };

    /// <summary>
    /// The packages of <paramref name="library" /> that <paramref name="engine" />'s queries use and that the project
    /// references older than their floor, each with the version found and the floor, as <c>SQLSRC282</c> names them:
    /// NodaTime below 3.1.9 on both engines; NetTopologySuite below 2.5.0 on both, and on SQL Server
    /// NetTopologySuite.IO.SqlServerBytes below 2.1.0, which only SQL Server's queries use.
    /// </summary>
    public IEnumerable<(string Name, string Version, string Floor)> PackagesBelowTheFloor(
        SqlLibrary library,
        DatabaseEngine engine
    )
    {
        if (library == SqlLibrary.NodaTime && IsNodaTimeBelowTheFloor)
        {
            yield return (NodaTimeAssemblyName, TextOf(NodaTimeVersion!), TextOf(NodaTimeFloor));
        }

        if (library != SqlLibrary.NetTopologySuite)
        {
            yield break;
        }

        if (IsNetTopologySuiteBelowTheFloor)
        {
            yield return (
                NetTopologySuiteAssemblyName,
                NetTopologySuiteVersion ?? UnknownVersion,
                TextOf(NetTopologySuiteFloor)
            );
        }

        if (engine == DatabaseEngine.SqlServer && IsSqlServerBytesBelowTheFloor)
        {
            yield return (SqlServerBytesAssemblyName, SqlServerBytesVersion!, TextOf(SqlServerBytesFloor));
        }
    }

    // An assembly named NodaTime older than 3.1.9 that has its types.  A library whose assembly gives no version, as
    // one merged into an assembly of another name, is not below it.
    private bool IsNodaTimeBelowTheFloor => NodaTime && NodaTimeVersion is { } version && version < NodaTimeFloor;

    // NetTopologySuite's geometries without ConcaveHull, first in 2.5.0, whatever version the package says, since
    // every 2.x has one identity.
    private bool IsNetTopologySuiteBelowTheFloor => NetTopologySuite && !NetTopologySuiteAtFloor;

    // A reader of SQL Server's bytes whose package says a version older than 2.1.0.  One that says none, or none that
    // starts with a number, is not below it.
    private bool IsSqlServerBytesBelowTheFloor =>
        SqlServerBytes && NumbersOf(SqlServerBytesVersion) is { } version && version < SqlServerBytesFloor;

    /// <summary>
    /// Reads the probe from <paramref name="compilation" />: seven type lookups, the identities of its references, and
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
            PackageVersionOf(readers, SqlServerBytesAssemblyName),
            DriverProbes.Has(compilation, "System.Text.Json.JsonElement")
        );
    }

    // The version of the package of the newest assembly of the name that defines one of the types, or null.
    private static string? PackageVersionOf(ImmutableArray<INamedTypeSymbol> types, string assemblyName) =>
        DriverProbes.NewestNamed(types, assemblyName) is { } assembly ? DriverProbes.PackageVersionOf(assembly) : null;

    // The leading numbers and dots of a package's version, as 2.1.0 of 2.1.0-beta1, with a build of 0 where it has
    // none, so that 2.1 is 2.1.0, or null when they make no version.
    private static Version? NumbersOf(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var length = 0;
        while (length < text.Length && (text[length] is (>= '0' and <= '9') or '.'))
        {
            length++;
        }

        return Version.TryParse(text.Substring(0, length), out var version)
            ? new Version(version.Major, version.Minor, Math.Max(version.Build, 0))
            : null;
    }

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
