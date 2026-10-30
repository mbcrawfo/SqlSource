using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace SqlSource.Testing.Generator;

// References to NodaTime, NetTopologySuite and their Npgsql plugins, and to NetTopologySuite's reader and writer of SQL
// Server's bytes, for the probe of the libraries.  The real ones are the packages that
// this library downloads and does not reference: each is read as metadata from the NuGet folder, whose path the
// assembly records, and never loaded.  A synthetic one is an assembly of any name and version, with the types the probe
// looks for.
//
// The versions of Downloaded are those of the PackageDownload items of SqlSource.Testing.csproj: a mismatch builds and
// fails only when a test runs.  The plugins' are those of the Npgsql of NpgsqlReferences that they pair with.
//
// Like the other files of this folder, it stays within Roslyn 4.8.0, since tests/SqlSource.Tests.RoslynFloor
// references this library.
internal static class LibraryReferences
{
    // The value types of NodaTime that its rows of SQL Server and of the plugin name, as declarations.
    private const string NodaTimeTypes = """

        namespace NodaTime
        {
            public readonly struct Instant;
            public readonly struct LocalDate;
            public readonly struct LocalTime;
            public readonly struct LocalDateTime;
            public readonly struct OffsetDateTime;
        }

        """;

    // NodaTime 3.1.9 is the oldest the support floor of the library allows, and 3.3.7 the newest.
    public static MetadataReference NodaTime(string version) =>
        Downloaded("nodatime", version, "netstandard2.0", "NodaTime");

    // Npgsql.NodaTime, which pairs with the Npgsql of the same version: 8.0.3 and 10.0.3.
    public static MetadataReference NpgsqlNodaTime(string version) =>
        Downloaded(
            "npgsql.nodatime",
            version,
            version.StartsWith("8.", System.StringComparison.Ordinal) ? "net6.0" : "net8.0",
            "Npgsql.NodaTime"
        );

    // NetTopologySuite's geometries, whose classes derive from Geometry, as declarations, without ConcaveHull.
    private const string NetTopologySuiteTypes = """

        namespace NetTopologySuite.Geometries
        {
            public class Geometry;
            public class Point : Geometry;
            public class LineString : Geometry;
            public class LinearRing : LineString;
            public class Polygon : Geometry;
            public class GeometryCollection : Geometry;
            public class MultiPoint : GeometryCollection;
            public class MultiLineString : GeometryCollection;
            public class MultiPolygon : GeometryCollection;
        }

        """;

    // NetTopologySuite 2.5.0 is the oldest the support floor of the library allows, and 2.6.0 the newest.
    public static MetadataReference NetTopologySuite(string version) =>
        Downloaded("nettopologysuite", version, "netstandard2.0", "NetTopologySuite");

    // Npgsql.NetTopologySuite, which pairs with the Npgsql of its major: 8.0.9 and 10.0.3.
    public static MetadataReference NpgsqlNetTopologySuite(string version) =>
        Downloaded(
            "npgsql.nettopologysuite",
            version,
            version.StartsWith("8.", System.StringComparison.Ordinal) ? "netstandard2.0" : "net8.0",
            "Npgsql.NetTopologySuite"
        );

    // NetTopologySuite.IO.SqlServerBytes, 2.0.0 and 2.1.0, which only their informational versions tell apart.
    public static MetadataReference SqlServerBytes(string version) =>
        Downloaded(
            "nettopologysuite.io.sqlserverbytes",
            version,
            "netstandard2.0",
            "NetTopologySuite.IO.SqlServerBytes"
        );

    // An assembly named NetTopologySuite of the identity 2.0.0.0, which every 2.x has, and the informational version,
    // with NetTopologySuite's geometries and, when asked for, NetTopologySuite.Algorithm.Hull.ConcaveHull, first in
    // 2.5.0.  The types have no members.
    public static MetadataReference SyntheticNetTopologySuite(string informationalVersion, bool concaveHull)
    {
        var source =
            "[assembly: System.Reflection.AssemblyVersion(\"2.0.0.0\")]\n"
            + $"[assembly: System.Reflection.AssemblyInformationalVersion(\"{informationalVersion}\")]\n"
            + NetTopologySuiteTypes
            + (concaveHull ? "\nnamespace NetTopologySuite.Algorithm.Hull\n{\n    public class ConcaveHull;\n}\n" : "");
        return Emit(new SourceFile("/nettopologysuite/NetTopologySuite.cs", source), "NetTopologySuite");
    }

    // An assembly of the AssemblyVersion, with NodaTime's value types that a row names, Instant among them, and
    // Npgsql.NpgsqlNodaTimeExtensions, when asked for.  It is named NodaTime, unless another name is given, as for a
    // library merged into an assembly of another name.  The types have no members: a model can name them, and a method
    // that converts them does not compile.
    public static MetadataReference Synthetic(
        string version,
        bool nodaTimeTypes,
        bool pluginExtensions,
        string assemblyName = "NodaTime"
    )
    {
        var source =
            $"[assembly: System.Reflection.AssemblyVersion(\"{version}\")]\n"
            + (nodaTimeTypes ? NodaTimeTypes : "")
            + (pluginExtensions ? "\nnamespace Npgsql\n{\n    public static class NpgsqlNodaTimeExtensions;\n}\n" : "");
        return Emit(new SourceFile("/nodatime/NodaTime.cs", source), assemblyName);
    }

    // The image of an assembly of the name, compiled from the source alone, which must compile without an error.
    private static PortableExecutableReference Emit(SourceFile source, string assemblyName)
    {
        var compilation = GeneratorHarness.CreateCompilation([source], assemblyName: assemblyName);

        using var image = new MemoryStream();
        var emitted = compilation.Emit(image, cancellationToken: TestContext.Current.CancellationToken);
        emitted
            .Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.ToString())
            .ShouldBeEmpty();
        emitted.Success.ShouldBeTrue();
        return MetadataReference.CreateFromImage(image.ToArray());
    }

    // The library's assembly of the target framework in the package of the version.
    private static PortableExecutableReference Downloaded(
        string package,
        string version,
        string framework,
        string assembly
    )
    {
        var root = typeof(LibraryReferences)
            .Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "NuGetPackageRoot")
            .Value!;
        return MetadataReference.CreateFromFile(
            Path.Combine(root, package, version, "lib", framework, assembly + ".dll")
        );
    }
}
