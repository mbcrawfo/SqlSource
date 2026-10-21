using System;
using SqlSource.Snapshot;

namespace SqlSource.Tool.Describing.SqlServer;

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
    // The schema of the built-in types, whose user type a message does not name.
    private const string SystemSchema = "sys";

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
    /// </summary>
    public static string Spelled(SqlServerType type)
    {
        if (
            type.UserType is not { Name: { } name } user
            || string.Equals(user.Schema, SystemSchema, StringComparison.OrdinalIgnoreCase)
        )
        {
            return type.Name;
        }

        return user.Schema is null ? name : user.Schema + "." + name;
    }

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
