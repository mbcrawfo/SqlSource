using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlSource;

/// <summary>
/// SQL Server's rules for names, which the generator and the tool share: how a name is put in brackets, and the CLR
/// types of <c>sys</c>, of which two are spatial.
/// </summary>
/// <remarks>
/// A name has two bracketed forms, for two readers.  T-SQL text, such as a declaration the tool sends, needs a name
/// that is a keyword in brackets even when it is all letters, <c>[dbo].[User]</c>, so <see cref="Bracketed" />
/// always brackets.  A <c>UdtTypeName</c> is read by SqlClient, which only splits it at each dot outside brackets, so
/// <see cref="UdtPart" /> brackets a part only when it holds more than letters, digits and underscores, the form
/// generated code has always written.
/// </remarks>
internal static class SqlServerNames
{
    /// <summary>The CLR type of <c>sys</c> for a hierarchy.</summary>
    public const string HierarchyId = "hierarchyid";

    /// <summary>The CLR type of <c>sys</c> for a round-earth shape.</summary>
    public const string Geography = "geography";

    /// <summary>The CLR type of <c>sys</c> for a flat shape.</summary>
    public const string Geometry = "geometry";

    /// <summary>
    /// The CLR types of <c>sys</c>, by their system type name: a row of one names its assembly, and SqlClient reads
    /// one only with the package Microsoft.SqlServer.Types.
    /// </summary>
    public static IReadOnlyList<string> ClrSystemTypes { get; } = [HierarchyId, Geography, Geometry];

    /// <summary>
    /// Whether <paramref name="name" /> is one of <see cref="ClrSystemTypes" />, ignoring case, as the server matches
    /// a type's name.
    /// </summary>
    public static bool IsClrSystemType(string name) => ClrSystemTypes.Contains(name, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether <paramref name="name" /> is a spatial CLR type of <c>sys</c>, <see cref="Geography" /> or
    /// <see cref="Geometry" />, ignoring case.
    /// </summary>
    public static bool IsSpatial(string name) =>
        name.Equals(Geography, StringComparison.OrdinalIgnoreCase)
        || name.Equals(Geometry, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A name in brackets for T-SQL text, which hold any character but a <c>]</c> that is not written twice:
    /// <c>[User]</c>, <c>[odd ]]type]</c>.
    /// </summary>
    public static string Bracketed(string name) => "[" + name.Replace("]", "]]") + "]";

    /// <summary>
    /// A part of a <c>UdtTypeName</c>: as it is when it is all letters, digits and underscores, and
    /// <see cref="Bracketed" /> otherwise, so that SqlClient's split at each dot outside brackets keeps it whole.
    /// </summary>
    public static string UdtPart(string part) =>
        part.All(static character => char.IsLetterOrDigit(character) || character == '_') ? part : Bracketed(part);
}
