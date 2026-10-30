using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer.Statements;
using SqlSource.Tool.Describing.SqlServer.Types;

namespace SqlSource.Tool.Describing.SqlServer;

// What a describe makes of the answers, and reads no state of the session.
internal sealed partial class SqlServerSession
{
    // The rows of an answer of "columns" or "table", or what the query is reported as at the step of the call: the
    // error of the call is SQLSRC225, and the error rows of the describe are classified by their type.
    private static Outcome<IReadOnlyList<SqlServerColumnRow>, DescribeFailure> RowsOf(
        DescribeRequest request,
        SqlServerColumnsAnswer answer,
        DescribeStep step,
        string call
    )
    {
        if (answer.Error is { } error)
        {
            return Outcome<IReadOnlyList<SqlServerColumnRow>, DescribeFailure>.Failed(
                SqlServerErrors.Failed(request, error, step)
            );
        }

        if (answer.Errors is { Count: > 0 } errors)
        {
            return Outcome<IReadOnlyList<SqlServerColumnRow>, DescribeFailure>.Failed(
                SqlServerErrors.Columns(request, errors, step)
            );
        }

        return Outcome<IReadOnlyList<SqlServerColumnRow>, DescribeFailure>.Of(
            answer.Rows ?? throw new InvalidOperationException($"The answer to '{call}' holds no rows and no error.")
        );
    }

    // A parameter of the list with its type: declared, from "declared"; reused, from its copies; otherwise from its
    // row, which the comparison of the lists has found.
    private static DescribedParameter DescribedOf(
        SqlQueryParameter parameter,
        Dictionary<string, SqlServerType> declaredTypes,
        Dictionary<string, SqlServerType> reusedTypes,
        Dictionary<string, SqlServerParameterRow> rowOf
    )
    {
        if (declaredTypes.TryGetValue(parameter.Name, out var declared))
        {
            return new DescribedParameter(parameter.Name, declared, SidecarValues.TypeSource.Declared);
        }

        if (reusedTypes.TryGetValue(parameter.Name, out var reused))
        {
            return new DescribedParameter(parameter.Name, reused, SidecarValues.TypeSource.InferredFromCopies);
        }

        return rowOf.TryGetValue(parameter.Name, out var row)
            ? new DescribedParameter(
                parameter.Name,
                SqlServerTypes.FromParameter(row),
                SidecarValues.TypeSource.Inferred
            )
            : throw new InvalidOperationException(
                $"The server reported no type for the parameter '{parameter.Name}', and the lists agreed."
            );
    }

    // The declarations of "columns": every parameter of the list in its order, a declared one at its declaration as
    // DeclaredTypeText read it, and any other at the type it was described at, as SqlServerTypes.Declared writes it;
    // or null for a query with no parameter.
    private static string? ColumnsParams(
        EquatableArray<DescribedParameter> parameters,
        Dictionary<string, string> declarationOf
    ) =>
        parameters.Count == 0
            ? null
            : SqlServerLive.Declarations(
                parameters.Select(parameter =>
                    (
                        parameter.Name,
                        declarationOf.TryGetValue(parameter.Name, out var declaration)
                            ? declaration
                            : SqlServerTypes.Declared(
                                parameter.Type as SqlServerType
                                    ?? throw new InvalidOperationException(
                                        $"The parameter '{parameter.Name}' was described with no type of SQL Server."
                                    )
                            )
                    )
                )
            );

    // Below the floor, unless the edition is Azure's.  The leading digits of the version decide, as for PostgreSQL: a
    // version that starts with no digit is not refused, since the tool cannot tell what it is.
    private static bool IsBelowTheFloor(string version, int? edition) =>
        edition is not (AzureSqlDatabase or AzureSqlManagedInstance) && IsBelow(version, MinimumMajorVersion);

    private static ServerInfo ServerOf(SqlServerOpenAnswer answer) =>
        ServerOf(
            answer.ProductVersion,
            DriverName,
            answer.DriverVersion,
            (answer.Settings ?? []).Select(static setting => new ServerSetting(setting.Name, setting.Value))
        );

    /// <summary>
    /// The requests of <c>probe</c> for one describe: the SQL that <c>parameters</c> was asked, and the declarations
    /// of every parameter of the list in its order, each copy of a reused parameter in its place.
    /// </summary>
    /// <remarks>
    /// The copy that is probed is declared at the type it is probed with; every other copy and undeclared parameter
    /// at the system type that <c>parameters</c> gave it, and one it gave none is left out; a declared parameter as it
    /// is declared.
    /// </remarks>
    private sealed class Probes(
        DescribeRequest request,
        CopiedSql copied,
        Dictionary<string, string> declarationOf,
        Dictionary<string, SqlServerParameterRow> rowOf
    )
    {
        public SqlServerProbeRequest Request(string probed, string type)
        {
            var declared = new List<(string Name, string Type)>();
            foreach (var parameter in request.Parameters)
            {
                if (parameter.Type is not null)
                {
                    declared.Add((parameter.Name, declarationOf[parameter.Name]));
                    continue;
                }

                var copies = copied.Reused.FirstOrDefault(reused =>
                    string.Equals(reused.Name, parameter.Name, StringComparison.OrdinalIgnoreCase)
                );
                foreach (var name in copies?.Copies ?? [parameter.Name])
                {
                    if (string.Equals(name, probed, StringComparison.OrdinalIgnoreCase))
                    {
                        declared.Add((name, type));
                    }
                    else if (rowOf.TryGetValue(name, out var row) && row.SuggestedSystemTypeName is { } suggested)
                    {
                        declared.Add((name, suggested));
                    }
                }
            }

            return new SqlServerProbeRequest(copied.Sql, SqlServerLive.Declarations(declared));
        }
    }
}
