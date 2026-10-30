using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace SqlSource.Testing.Generator;

// References to NodaTime and to its Npgsql plugin for the probe of the libraries.  The real ones are the packages that
// this library downloads and does not reference: each is read as metadata from the NuGet folder, whose path the
// assembly records, and never loaded.  A synthetic one is an assembly of any name and version, with the types the probe
// looks for.
//
// The versions of Downloaded are those of the PackageDownload items of SqlSource.Testing.csproj: a mismatch builds and
// fails only when a test runs.  The plugin's are those of the Npgsql of NpgsqlReferences that they pair with.
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
        var compilation = GeneratorHarness.CreateCompilation(
            [new SourceFile("/nodatime/NodaTime.cs", source)],
            assemblyName: assemblyName
        );

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
