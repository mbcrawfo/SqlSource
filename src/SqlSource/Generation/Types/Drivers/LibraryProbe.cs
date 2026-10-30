using System;
using System.Globalization;
using Microsoft.CodeAnalysis;
using SqlSource.Settings;
using SqlSource.Settings.Libraries;

namespace SqlSource.Generation.Types.Drivers;

/// <summary>
/// Which libraries a project references, which the library switch needs to know whether a library can be on: whether
/// it references NodaTime, and the plugin that lets Npgsql read and write its types.  Each flag is the presence of a
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
internal sealed record LibraryProbe(bool NodaTime, bool NpgsqlNodaTime, Version? NodaTimeVersion)
{
    private const string NodaTimeAssemblyName = "NodaTime";

    // The oldest NodaTime whose types SqlSource's rows and conversions name.
    private static readonly Version NodaTimeFloor = new(3, 1, 9, 0);

    /// <summary>
    /// Whether the project references what <paramref name="library" /> needs on <paramref name="engine" />: on
    /// PostgreSQL both NodaTime and the Npgsql plugin, on SQL Server NodaTime alone.
    /// </summary>
    public bool IsAvailable(SqlLibrary library, DatabaseEngine engine) =>
        library switch
        {
            SqlLibrary.NodaTime => engine == DatabaseEngine.PostgreSql ? NodaTime && NpgsqlNodaTime : NodaTime,
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
    /// <paramref name="engine" />, as a diagnostic names them: the plugin on PostgreSQL, which brings NodaTime, and
    /// NodaTime on SQL Server.
    /// </summary>
    public static string MissingAssembliesOf(SqlLibrary library, DatabaseEngine engine) =>
        library switch
        {
            SqlLibrary.NodaTime => engine == DatabaseEngine.PostgreSql ? "Npgsql.NodaTime" : "NodaTime",
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
            _ => false,
        };

    /// <summary>
    /// The oldest version of <paramref name="library" /> that SqlSource supports, as a diagnostic gives it.
    /// </summary>
    public static string FloorOf(SqlLibrary library) =>
        library switch
        {
            SqlLibrary.NodaTime or _ => TextOf(NodaTimeFloor),
        };

    /// <summary>
    /// The version of <paramref name="library" /> that the project references, as a diagnostic gives it: the major,
    /// the minor and the build, as <c>3.1.8</c>.  Empty when there is none.
    /// </summary>
    public string VersionTextOf(SqlLibrary library) =>
        library switch
        {
            SqlLibrary.NodaTime or _ => NodaTimeVersion is { } version ? TextOf(version) : "",
        };

    /// <summary>
    /// Reads the probe from <paramref name="compilation" />: two type lookups and the identities of its references.
    /// Of two references named <c>NodaTime</c>, the newer gives the version.  Never throws.
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

        return new LibraryProbe(
            DriverProbes.Has(compilation, "NodaTime.Instant"),
            DriverProbes.Has(compilation, "Npgsql.NpgsqlNodaTimeExtensions"),
            version
        );
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
