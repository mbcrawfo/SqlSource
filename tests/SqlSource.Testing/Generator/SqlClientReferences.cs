using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace SqlSource.Testing.Generator;

// References to Microsoft.Data.SqlClient for the probe of the driver.  The real drivers are the packages that this
// library downloads and does not reference: each is read as metadata from the NuGet folder, whose path the assembly
// records, and never loaded.  A synthetic one is an assembly named Microsoft.Data.SqlClient of any version, with the
// types the probe looks for.
//
// Like the other files of this folder, it stays within Roslyn 4.8.0, since tests/SqlSource.Tests.RoslynFloor
// references this library.
internal static class SqlClientReferences
{
    // The newest 6.0, which has no SqlVector.
    public static MetadataReference SqlClient60 { get; } = Downloaded("6.0.5");

    // The newest 6.1.
    public static MetadataReference SqlClient61 { get; } = Downloaded("6.1.7");

    // The newest 7.
    public static MetadataReference SqlClient7 { get; } = Downloaded("7.1.1");

    // The driver the project is likely to reference by mistake, which defines nothing in the namespace of
    // Microsoft.Data.SqlClient.
    public static MetadataReference SystemDataSqlClient { get; } =
        Emit("System.Data.SqlClient", "namespace System.Data.SqlClient\n{\n    public class SqlConnection;\n}\n");

    // An assembly of the AssemblyVersion, with the AssemblyInformationalVersion when one is given, with
    // Microsoft.Data.SqlClient.SqlConnection, Microsoft.Data.SqlTypes.SqlVector<T> and
    // Microsoft.Data.SqlClient.SspiContextProvider when asked for.  It is named Microsoft.Data.SqlClient, unless
    // another name is given, as for a driver merged into an assembly of another name.
    public static MetadataReference Synthetic(
        string version,
        bool connection,
        bool vector,
        bool seven,
        string? informationalVersion = null,
        string assemblyName = "Microsoft.Data.SqlClient"
    )
    {
        var source =
            $"[assembly: System.Reflection.AssemblyVersion(\"{version}\")]\n"
            + (
                informationalVersion is null
                    ? ""
                    : $"[assembly: System.Reflection.AssemblyInformationalVersion(\"{informationalVersion}\")]\n"
            )
            + "\nnamespace Microsoft.Data.SqlClient\n{\n"
            + (connection ? "    public class SqlConnection;\n" : "")
            + (seven ? "    public abstract class SspiContextProvider;\n" : "")
            + "}\n"
            + (
                vector
                    ? "\nnamespace Microsoft.Data.SqlTypes\n"
                        + "{\n    public readonly struct SqlVector<T> where T : unmanaged;\n}\n"
                    : ""
            );
        return Emit(assemblyName, source);
    }

    private static PortableExecutableReference Emit(string assemblyName, string source)
    {
        var compilation = GeneratorHarness.CreateCompilation(
            [new SourceFile("/sqlclient/" + assemblyName + ".cs", source)],
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

    // The driver's reference assembly for .NET 8 in the package of the version.
    private static PortableExecutableReference Downloaded(string version) =>
        MetadataReference.CreateFromFile(
            Path.Combine(
                typeof(SqlClientReferences)
                    .Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                    .Single(attribute => attribute.Key == "NuGetPackageRoot")
                    .Value!,
                "microsoft.data.sqlclient",
                version,
                "ref",
                "net8.0",
                "Microsoft.Data.SqlClient.dll"
            )
        );
}
