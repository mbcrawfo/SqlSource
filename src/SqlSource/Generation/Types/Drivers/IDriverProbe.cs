using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using SqlSource.Settings;

namespace SqlSource.Generation.Types.Drivers;

/// <summary>
/// One diagnostic that a probe finds in the project's reference to a driver: the descriptor, and the one argument its
/// message takes, which is the probe's <c>VersionText</c>: it names the version the project references, or says that
/// it is unknown.
/// </summary>
/// <param name="Descriptor">The diagnostic, reported with <c>Location.None</c>.</param>
/// <param name="Argument">The text that fills the message's one argument.</param>
internal readonly record struct DriverFinding(DiagnosticDescriptor Descriptor, string Argument);

/// <summary>
/// What a driver's probe gives the steps that read it: the diagnostics of the project's reference to the driver, and
/// whether the project references the driver and whether it is below the floor.  The step that reports it reports
/// whatever the probe finds and decides nothing about the driver, so a probe that learns of another condition of its
/// driver adds a flag and a finding, and <c>ReportDriver</c> is unchanged.
/// </summary>
internal interface IDriverProbe
{
    /// <summary>Whether the project references the driver.</summary>
    bool Referenced { get; }

    /// <summary>Whether the project references a version of the driver below the floor.</summary>
    bool IsBelowTheFloor { get; }

    /// <summary>
    /// The diagnostics of the project's reference to the driver, each once, in the order they are reported: too old,
    /// then newer than known.  Empty when the project does not reference the driver or references one that the map
    /// knows.  Computed from the probe's members, never a member of the probe, so that it takes no part in the probe's
    /// equality.
    /// </summary>
    IEnumerable<DriverFinding> Findings { get; }
}

/// <summary>What the probes share of reading a compilation, and the choice of an engine's probe.</summary>
internal static class DriverProbes
{
    // What ends the version in an informational version: the commit's + and a description's space.
    private static readonly char[] VersionEnds = ['+', ' ', '\t'];

    /// <summary>The probe of the driver of <paramref name="engine" />.</summary>
    /// <param name="engine">The engine.</param>
    /// <param name="npgsql">The probe of Npgsql, the driver of PostgreSQL.</param>
    /// <param name="sqlClient">The probe of Microsoft.Data.SqlClient, the driver of SQL Server.</param>
    public static IDriverProbe Of(DatabaseEngine engine, NpgsqlProbe npgsql, SqlClientProbe sqlClient) =>
        engine switch
        {
            DatabaseEngine.PostgreSql => npgsql,
            DatabaseEngine.SqlServer => sqlClient,
            _ => throw UnexpectedEnumValueException.Of(engine),
        };

    /// <summary>Whether <paramref name="compilation" /> has a type named <paramref name="metadataName" />.</summary>
    public static bool Has(Compilation compilation, string metadataName) =>
        !compilation.GetTypesByMetadataName(metadataName).IsEmpty;

    /// <summary>
    /// Of the assemblies that define <paramref name="types" />, the newest by its identity that is named
    /// <paramref name="assemblyName" />, ignoring case, or null when there is none.  A package merged into an assembly
    /// of another name has no version of its own.  A name is checked before anything else of an assembly is read.
    /// </summary>
    public static IAssemblySymbol? NewestNamed(ImmutableArray<INamedTypeSymbol> types, string assemblyName)
    {
        IAssemblySymbol? newest = null;
        for (var i = 0; i < types.Length; i++)
        {
            var assembly = types[i].ContainingAssembly;
            if (
                assembly is not null
                && string.Equals(assembly.Identity.Name, assemblyName, StringComparison.OrdinalIgnoreCase)
                && (newest is null || assembly.Identity.Version > newest.Identity.Version)
            )
            {
                newest = assembly;
            }
        }

        return newest;
    }

    /// <summary>
    /// The version of the package of <paramref name="assembly" />, from its informational version: the text before a
    /// <c>+</c>, which the commit follows, and before a space, which a description follows, as in NetTopologySuite's
    /// <c>2.6.0 (compatible with JTS 1.20.0)+</c> and the commit.  Null when the attribute is missing, has no argument,
    /// has one that is not text or gives no version.
    /// </summary>
    public static string? PackageVersionOf(IAssemblySymbol assembly)
    {
        foreach (var attribute in assembly.GetAttributes())
        {
            if (
                IsInformationalVersion(attribute.AttributeClass)
                && attribute.ConstructorArguments.Length > 0
                && attribute.ConstructorArguments[0] is { Kind: TypedConstantKind.Primitive, Value: string text }
            )
            {
                var version = text.Trim();
                var end = version.IndexOfAny(VersionEnds);
                if (end >= 0)
                {
                    version = version.Substring(0, end);
                }

                if (version.Length > 0)
                {
                    return version;
                }
            }
        }

        return null;
    }

    // System.Reflection.AssemblyInformationalVersionAttribute, compared by its names so that no string is made for the
    // attribute of every assembly on every edit.
    private static bool IsInformationalVersion(INamedTypeSymbol? type) =>
        type
            is {
                MetadataName: "AssemblyInformationalVersionAttribute",
                ContainingNamespace:
                {
                    Name: "Reflection",
                    ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true }
                },
            };
}
