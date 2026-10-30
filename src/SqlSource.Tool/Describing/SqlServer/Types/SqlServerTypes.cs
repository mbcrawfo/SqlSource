using System;
using System.Collections.Generic;
using SqlSource.Snapshot;

namespace SqlSource.Tool.Describing.SqlServer.Types;

/// <summary>
/// The sidecar's record of a type, from a row of <c>sp_describe_undeclared_parameters</c> or of
/// <c>sys.dm_exec_describe_first_result_set</c>.
/// </summary>
/// <remarks>
/// The name is the system type name verbatim, facets included, and the length, precision and scale are the row's.  A
/// row that names a user type gives it its schema, name and, for a CLR type, assembly-qualified name: <c>sysname</c>
/// is a user type of <c>sys</c> over <c>nvarchar(128)</c>, and <c>hierarchyid</c> one of <c>sys</c> with
/// <c>Microsoft.SqlServer.Types</c>.  <c>bindName</c> is never written for SQL Server.
/// </remarks>
internal static class SqlServerTypes
{
    // The CLR types of the system, by their system type name, which a row of them names besides their assembly.
    private static readonly HashSet<string> ClrSystemTypes =
    [
        with(StringComparer.OrdinalIgnoreCase),
        "geometry",
        "geography",
        "hierarchyid",
    ];

    /// <summary>The type the server suggests for an undeclared parameter.</summary>
    /// <exception cref="InvalidOperationException">
    /// The row has no type name or lacks a facet: the server typed the parameter, or it would have raised an error.
    /// </exception>
    public static SqlServerType FromParameter(SqlServerParameterRow row) => Build(row);

    /// <summary>The type the server gives a declared parameter.</summary>
    /// <exception cref="InvalidOperationException">The row has no type name or lacks a facet.</exception>
    public static SqlServerType FromDeclared(SqlServerDeclaredRow row) => Build(row);

    /// <summary>The type the server gives a column of a result set.</summary>
    /// <exception cref="InvalidOperationException">The row has no type name or lacks a facet.</exception>
    public static SqlServerType FromColumn(SqlServerColumnRow row) => Build(row);

    /// <summary>
    /// The type as a message names it: <c>schema.name</c> of a user type that is not of <c>sys</c>, as
    /// <c>dbo.Email</c>, and the system type name otherwise.
    /// The schema is compared with <c>sys</c> exactly (<see cref="SystemSchemas.IsSqlServer" />): in a case-sensitive
    /// database <c>SYS</c> is a user's schema.
    /// </summary>
    public static string Spelled(SqlServerType type)
    {
        if (type.UserType is not { Name: { } name } user || SystemSchemas.IsSqlServer(user.Schema))
        {
            return type.Name;
        }

        return user.Schema is null ? name : user.Schema + "." + name;
    }

    /// <summary>
    /// The type as a declaration of <c>@params</c> writes it: <c>[schema].[name]</c> of a user type that is not of
    /// <c>sys</c>, each name in brackets with a <c>]</c> written twice, so that a name that is a keyword or holds a
    /// space, <c>[dbo].[User]</c>, declares; and the system type name otherwise, so <c>sysname</c> is
    /// <c>nvarchar(128)</c> and <c>hierarchyid</c> is <c>hierarchyid</c>.  <see cref="Spelled" /> is for messages.
    /// </summary>
    public static string Declared(SqlServerType type)
    {
        if (type.UserType is not { Name: { } name } user || SystemSchemas.IsSqlServer(user.Schema))
        {
            return type.Name;
        }

        return user.Schema is null
            ? SqlServerLive.Bracketed(name)
            : SqlServerLive.Bracketed(user.Schema) + "." + SqlServerLive.Bracketed(name);
    }

    /// <summary>
    /// True when the row's type may be a CLR type: it has an assembly-qualified name, as <c>geometry</c>,
    /// <c>geography</c>, <c>hierarchyid</c> and a user's CLR type have, its system type name is one of those three, or
    /// it has none.  A CLR type's <c>Parse</c> may answer its Null for a string such as <c>'NULL'</c>, so a conversion
    /// of a value that is not null to it may be <c>NULL</c>.  The one rule of what is a CLR type for the proofs.
    /// </summary>
    public static bool MayBeClr(ISqlServerTypeRow row) =>
        row.AssemblyQualifiedTypeName is not null
        || row.SystemTypeName is not { } name
        || ClrSystemTypes.Contains(name);

    private static SqlServerType Build(ISqlServerTypeRow row)
    {
        if (
            row
            is not { SystemTypeName: { } name, MaxLength: { } maxLength, Precision: { } precision, Scale: { } scale }
        )
        {
            throw new InvalidOperationException(
                "A row of the server's types has no type name or lacks a facet, and the server typed it."
            );
        }

        var userType = row.UserTypeName is null
            ? null
            : new SqlServerUserType(row.UserTypeSchema, row.UserTypeName, row.AssemblyQualifiedTypeName);
        return new SqlServerType(name, maxLength, precision, scale, userType);
    }
}
