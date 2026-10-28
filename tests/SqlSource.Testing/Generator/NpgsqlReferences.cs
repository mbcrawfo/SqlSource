using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace SqlSource.Testing.Generator;

// References to Npgsql for the probe of the driver.  The real drivers are the packages that this library downloads and
// does not reference: each is read as metadata from the NuGet folder, whose path the assembly records, and never
// loaded.  A synthetic one is an assembly named Npgsql of any version, with the types the probe looks for.
//
// The versions of Downloaded are those of the PackageDownload items of SqlSource.Testing.csproj: a mismatch builds and
// fails only when a test runs.
//
// Like the other files of this folder, it stays within Roslyn 4.8.0, since tests/SqlSource.Tests.RoslynFloor
// references this library.
internal static class NpgsqlReferences
{
    public static MetadataReference Npgsql8 { get; } = Downloaded("8.0.3");

    public static MetadataReference Npgsql9 { get; } = Downloaded("9.0.3");

    public static MetadataReference Npgsql10 { get; } = Downloaded("10.0.3");

    // An assembly of the AssemblyVersion, with Npgsql.NpgsqlConnection, and with Npgsql.NpgsqlSlimDataSourceBuilder
    // and NpgsqlTypes.NpgsqlCube when asked for.  It is named Npgsql, unless another name is given, as for a driver
    // merged into an assembly of another name.
    public static MetadataReference Synthetic(
        string version,
        bool slimBuilder,
        bool cube,
        string assemblyName = "Npgsql"
    )
    {
        var source =
            $"[assembly: System.Reflection.AssemblyVersion(\"{version}\")]\n\n"
            + "namespace Npgsql\n{\n    public class NpgsqlConnection;\n"
            + (slimBuilder ? "\n    public class NpgsqlSlimDataSourceBuilder;\n" : "")
            + "}\n"
            + (cube ? "\nnamespace NpgsqlTypes\n{\n    public readonly struct NpgsqlCube;\n}\n" : "");
        var compilation = GeneratorHarness.CreateCompilation(
            [new SourceFile("/npgsql/Npgsql.cs", source)],
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

    // The driver's assembly for .NET 8 in the package of the version.
    private static PortableExecutableReference Downloaded(string version)
    {
        var root = typeof(NpgsqlReferences)
            .Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "NuGetPackageRoot")
            .Value!;
        return MetadataReference.CreateFromFile(Path.Combine(root, "npgsql", version, "lib", "net8.0", "Npgsql.dll"));
    }
}
