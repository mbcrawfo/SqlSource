using System.Linq;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Logging;

namespace SqlSource.Tool.Tests;

// What the classes of the SQL Server events' tests share: the error a server refuses with, and the maps in which an
// event writes an error, a parameter and a column row.
internal static class SqlServerEventCases
{
    public static readonly SqlServerServerError Refused = new([
        new SqlServerErrorRow(11508, 16, 1, "The parameter '@p' is used twice."),
        new SqlServerErrorRow(8180, 16, 1, "Statement(s) could not be prepared."),
    ]);

    public static LogValue ErrorsOf(params (long Number, long Class, long State, string Message)[] errors) =>
        LogValue.ListOf(
            errors.Select(error =>
                LogValue.MapOf(
                    new("number", error.Number),
                    new("class", error.Class),
                    new("state", error.State),
                    new("message", error.Message)
                )
            )
        );

    // A row of "parameters" as the event writes it: every member, named as the capture names it.
    public static LogValue ParameterMap(
        string name,
        string? type,
        long? length,
        long? precision,
        long? scale,
        string? userSchema = null,
        string? userName = null,
        string? assembly = null
    ) =>
        LogValue.MapOf(
            new("name", name),
            new("suggestedSystemTypeName", type),
            new("suggestedMaxLength", LogValue.Of(length)),
            new("suggestedPrecision", LogValue.Of(precision)),
            new("suggestedScale", LogValue.Of(scale)),
            new("suggestedUserTypeSchema", userSchema),
            new("suggestedUserTypeName", userName),
            new("suggestedAssemblyQualifiedTypeName", assembly)
        );

    // A row of "columns" or "table" as the event writes it: every member, named as the capture names it.
    public static LogValue ColumnRowMap(SqlServerColumnRow row) =>
        LogValue.MapOf(
            new("isHidden", row.IsHidden),
            new("columnOrdinal", row.ColumnOrdinal),
            new("name", row.Name),
            new("isNullable", LogValue.Of(row.IsNullable)),
            new("systemTypeName", row.SystemTypeName),
            new("maxLength", LogValue.Of(row.MaxLength)),
            new("precision", LogValue.Of(row.Precision)),
            new("scale", LogValue.Of(row.Scale)),
            new("userTypeSchema", row.UserTypeSchema),
            new("userTypeName", row.UserTypeName),
            new("assemblyQualifiedTypeName", row.AssemblyQualifiedTypeName),
            new("sourceServer", row.SourceServer),
            new("sourceDatabase", row.SourceDatabase),
            new("sourceSchema", row.SourceSchema),
            new("sourceTable", row.SourceTable),
            new("sourceColumn", row.SourceColumn),
            new("isIdentityColumn", LogValue.Of(row.IsIdentityColumn)),
            new("isComputedColumn", LogValue.Of(row.IsComputedColumn)),
            new("sourceType", row.SourceType),
            new("persistedNotNull", LogValue.Of(row.PersistedNotNull))
        );
}
