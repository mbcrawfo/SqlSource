using System.Collections.Generic;

namespace SqlSource.Tool.Describing.SqlServer;

// What the describer of SQL Server passes through the exchange, in one file: plain records that a capture can hold.
// A method's name and a member's name are a contract with the captures that exist: a member is added, nullable,
// and none is renamed.  tests/SqlSource.Tool.Tests/Describing/Capture/CallRecordTests.cs holds each to the rule.

/// <summary>
/// The request of <c>open</c>.  It holds nothing: nothing of a connection may reach a capture.  Its one member is
/// static, so it is written as <c>{}</c>.
/// </summary>
internal sealed record SqlServerOpenRequest
{
    public static SqlServerOpenRequest Instance { get; } = new();
}

/// <summary>What a session knows of its server, or why none was opened.</summary>
/// <param name="ProductVersion"><c>SERVERPROPERTY('ProductVersion')</c>: <c>14.0.3560.2</c>.</param>
/// <param name="EngineEdition">
/// <c>SERVERPROPERTY('EngineEdition')</c>: 5 for an Azure SQL Database and 8 for an Azure SQL Managed Instance, which
/// report a version of their own.
/// </param>
/// <param name="DriverVersion">The version of the Microsoft.Data.SqlClient that asked.</param>
/// <param name="Settings">The settings of the database and the session that change a description.</param>
/// <param name="Error">Why no session was opened, or null.</param>
/// <param name="Database">
/// <c>DB_NAME()</c>: the session's database, the one whose columns have an origin.  Null with an error, and in a
/// capture recorded before <c>open</c> asked it.
/// </param>
internal sealed record SqlServerOpenAnswer(
    string? ProductVersion,
    int? EngineEdition,
    string? DriverVersion,
    IReadOnlyList<SqlServerSetting>? Settings,
    SqlServerServerError? Error,
    string? Database
);

/// <summary>
/// A setting of the database or the session: <c>compatibility_level</c> as its number, <c>ANSI_NULLS</c> and
/// <c>QUOTED_IDENTIFIER</c> as <c>ON</c> or <c>OFF</c>, and <c>collation</c> as the server names it.
/// </summary>
internal sealed record SqlServerSetting(string Name, string Value);

/// <summary>
/// What the server or SqlClient said of a call it refused: every error of the exception, in its order.  An error of
/// <c>open</c>, and one of a connection that was lost or timed out, keep the numbers and have a fixed text of the
/// describer's own in place of the message (<see cref="ConnectionErrors" />).
/// </summary>
internal sealed record SqlServerServerError(IReadOnlyList<SqlServerErrorRow> Errors);

/// <summary>One error, as SQL Server numbers it.  The describer's own errors have 0 for each number.</summary>
/// <param name="Number">The error's number: <c>18456</c>, or <c>-2</c> for SqlClient's timeout.</param>
/// <param name="Class">Its severity, which SQL Server calls its level: 20 and above end the connection.</param>
/// <param name="State">Its state.</param>
/// <param name="Message">The message, or the describer's fixed text.</param>
internal sealed record SqlServerErrorRow(int Number, int Class, int State, string Message);

/// <summary>
/// The request of <c>parameters</c>: <c>sp_describe_undeclared_parameters</c> of the query's SQL under the
/// declarations of its declared parameters.
/// </summary>
/// <param name="Sql">The SQL with each use of a reused parameter renamed to a copy of its own.</param>
/// <param name="Params">The declarations of the declared parameters, <c>@a int, @b nvarchar(50)</c>, or null.</param>
internal sealed record SqlServerParametersRequest(string Sql, string? Params);

/// <summary>The parameters the server typed, or why it typed none.</summary>
/// <param name="Rows">One for each parameter that the declarations leave out, in the server's order.</param>
/// <param name="Error">Why the server typed none, or null.</param>
internal sealed record SqlServerParametersAnswer(
    IReadOnlyList<SqlServerParameterRow>? Rows,
    SqlServerServerError? Error
);

/// <summary>
/// A row of <c>sp_describe_undeclared_parameters</c>: one parameter the server typed, and the type it suggests.  The
/// members are named as the procedure names its columns.
/// </summary>
/// <param name="Name">The parameter's name with its <c>@</c>, as the procedure gives it: <c>@name_1</c>.</param>
/// <param name="SuggestedSystemTypeName">
/// The system type with its facets, <c>nvarchar(100)</c>, or null when the server gave none.
/// </param>
/// <param name="SuggestedMaxLength">The length in bytes, <c>-1</c> for <c>max</c>.</param>
/// <param name="SuggestedPrecision">The precision, 0 for a type that has none.</param>
/// <param name="SuggestedScale">The scale, 0 for a type that has none.</param>
/// <param name="SuggestedUserTypeSchema">The schema of an alias or CLR type, or null.</param>
/// <param name="SuggestedUserTypeName">The name of an alias or CLR type, or null.</param>
/// <param name="SuggestedAssemblyQualifiedTypeName">The assembly-qualified name of a CLR type, or null.</param>
internal sealed record SqlServerParameterRow(
    string Name,
    string? SuggestedSystemTypeName,
    int? SuggestedMaxLength,
    int? SuggestedPrecision,
    int? SuggestedScale,
    string? SuggestedUserTypeSchema,
    string? SuggestedUserTypeName,
    string? SuggestedAssemblyQualifiedTypeName
) : ISqlServerTypeRow
{
    string? ISqlServerTypeRow.SystemTypeName => SuggestedSystemTypeName;

    int? ISqlServerTypeRow.MaxLength => SuggestedMaxLength;

    int? ISqlServerTypeRow.Precision => SuggestedPrecision;

    int? ISqlServerTypeRow.Scale => SuggestedScale;

    string? ISqlServerTypeRow.UserTypeSchema => SuggestedUserTypeSchema;

    string? ISqlServerTypeRow.UserTypeName => SuggestedUserTypeName;

    string? ISqlServerTypeRow.AssemblyQualifiedTypeName => SuggestedAssemblyQualifiedTypeName;
}

/// <summary>
/// The request of <c>probe</c>: whether the SQL binds with every copy and parameter declared, one copy at
/// <c>xml</c> or <c>int</c> and the others at the types the server gave them.
/// </summary>
/// <param name="Sql">The SQL as <c>parameters</c> was asked it.</param>
/// <param name="Params">The declarations.</param>
internal sealed record SqlServerProbeRequest(string Sql, string Params);

/// <summary>Whether the SQL binds under the declarations, or why the server could not say.</summary>
/// <param name="Binds">
/// True when <c>sp_describe_undeclared_parameters</c> raised nothing, false when it refused the batch, and null
/// with an error.
/// </param>
/// <param name="Error">
/// A failure that is not the batch's, such as a connection that was lost or a timeout, or null.
/// </param>
internal sealed record SqlServerProbeAnswer(bool? Binds, SqlServerServerError? Error);

/// <summary>
/// The request of <c>declared</c>: <c>sys.dm_exec_describe_first_result_set</c> of <c>SELECT @p AS [1]</c> under
/// one declared parameter's declaration, which gives the type the server reads it as.
/// </summary>
/// <param name="Sql">The <c>SELECT</c> of the parameter.</param>
/// <param name="Params">The parameter's declaration, <c>@p dbo.Email</c>.</param>
internal sealed record SqlServerDeclaredRequest(string Sql, string Params);

/// <summary>The type of a declared parameter, or what the server found wrong with its declaration.</summary>
/// <param name="Rows">The described column, one, when the server found no error; or null.</param>
/// <param name="Errors">The error rows of the describe, in their order, or null when it has none.</param>
/// <param name="Error">Why the server could not be asked, or null.</param>
internal sealed record SqlServerDeclaredAnswer(
    IReadOnlyList<SqlServerDeclaredRow>? Rows,
    IReadOnlyList<SqlServerDescribeError>? Errors,
    SqlServerServerError? Error
);

/// <summary>
/// The row of <c>sys.dm_exec_describe_first_result_set</c> for <c>SELECT @p AS [1]</c> under the parameter's
/// declaration: the type the server gives a declared parameter.  The members are named as the function names its
/// columns.
/// </summary>
/// <param name="ColumnOrdinal">The column's place, counted from one: 1.</param>
/// <param name="SystemTypeName">The system type with its facets, <c>nvarchar(320)</c>, or null.</param>
/// <param name="MaxLength">The length in bytes, <c>-1</c> for <c>max</c>.</param>
/// <param name="Precision">The precision, 0 for a type that has none.</param>
/// <param name="Scale">The scale, 0 for a type that has none.</param>
/// <param name="UserTypeSchema">The schema of an alias or CLR type, or null.</param>
/// <param name="UserTypeName">The name of an alias or CLR type, or null.</param>
/// <param name="AssemblyQualifiedTypeName">The assembly-qualified name of a CLR type, or null.</param>
internal sealed record SqlServerDeclaredRow(
    int ColumnOrdinal,
    string? SystemTypeName,
    int? MaxLength,
    int? Precision,
    int? Scale,
    string? UserTypeSchema,
    string? UserTypeName,
    string? AssemblyQualifiedTypeName
) : ISqlServerTypeRow;

/// <summary>
/// An error row of <c>sys.dm_exec_describe_first_result_set</c>: the function answers what it could not describe
/// as rows, the cause first and <c>11501</c> or <c>11529</c> last.
/// </summary>
/// <param name="Number">The row's <c>error_number</c>: <c>2715</c> for a type the database lacks.</param>
/// <param name="Type">The row's <c>error_type</c>.</param>
/// <param name="Message">The row's <c>error_message</c>.</param>
internal sealed record SqlServerDescribeError(int Number, int Type, string Message);

/// <summary>
/// The request of <c>columns</c> and of <c>operands</c>: <c>sys.dm_exec_describe_first_result_set</c> of a text in
/// browse mode 2 under declarations.
/// </summary>
/// <param name="Sql">
/// The query's SQL as written for <c>columns</c>, or the text of <c>OperandsQuery</c> for <c>operands</c>.
/// </param>
/// <param name="Params">
/// The declarations of the parameters, each at its described type, <c>@a int, @b nvarchar(50)</c>, or the declared
/// parameters alone for the <c>columns</c> asked after 11501; or null.
/// </param>
internal sealed record SqlServerColumnsRequest(string Sql, string? Params);

/// <summary>
/// The request of <c>table</c>: <c>sys.dm_exec_describe_first_result_set</c> of <c>SELECT *</c> from one origin's
/// table or view, in browse mode 2, which gives its column list and each column's nullability read whole.
/// </summary>
/// <param name="Schema">The origin's schema, as the server named it.</param>
/// <param name="Table">The origin's table or view, as the server named it.</param>
internal sealed record SqlServerTableRequest(string Schema, string Table);

/// <summary>
/// The columns of the first result set that <c>columns</c>, <c>operands</c> or <c>table</c> described, or what the
/// server found wrong with its text.
/// </summary>
/// <param name="Rows">
/// Every row of the describe, the hidden ones included, in the order of their ordinals, when the server found no
/// error; or null.  No row is a statement with no result set.
/// </param>
/// <param name="Errors">The error rows of the describe, in their order, or null when it has none.</param>
/// <param name="Error">Why the server could not be asked, or null.</param>
internal sealed record SqlServerColumnsAnswer(
    IReadOnlyList<SqlServerColumnRow>? Rows,
    IReadOnlyList<SqlServerDescribeError>? Errors,
    SqlServerServerError? Error
);

/// <summary>
/// A row of <c>sys.dm_exec_describe_first_result_set</c> in browse mode 2: one column of a result set.  The members
/// are named as the function names its columns, but <see cref="SourceType" />, which is the tool's own.
/// </summary>
/// <param name="IsHidden">
/// Whether the browse mode added the column: mode 2 adds one, <c>ROWSTAT</c>, which no query returns.
/// </param>
/// <param name="ColumnOrdinal">The column's place, counted from one.</param>
/// <param name="Name">The column's name as the server returned it, or null for a column with none.</param>
/// <param name="IsNullable">Whether the column may be null, or null when the server cannot say.</param>
/// <param name="SystemTypeName">The system type with its facets, <c>nvarchar(320)</c>, or null.</param>
/// <param name="MaxLength">The length in bytes, <c>-1</c> for <c>max</c>.</param>
/// <param name="Precision">The precision, 0 for a type that has none.</param>
/// <param name="Scale">The scale, 0 for a type that has none.</param>
/// <param name="UserTypeSchema">The schema of an alias or CLR type, or null.</param>
/// <param name="UserTypeName">The name of an alias or CLR type, or null.</param>
/// <param name="AssemblyQualifiedTypeName">The assembly-qualified name of a CLR type, or null.</param>
/// <param name="SourceServer">The linked server of the source column, or null for the session's own.</param>
/// <param name="SourceDatabase">
/// The database of the source column, or null: <c>mssqlsystemresource</c> for a system view.
/// </param>
/// <param name="SourceSchema">The schema of the source column, or null.</param>
/// <param name="SourceTable">The table or view of the source column, or null.</param>
/// <param name="SourceColumn">The source column, or null for an expression.</param>
/// <param name="IsIdentityColumn">Whether the source column is an identity column, or null.</param>
/// <param name="IsComputedColumn">Whether the source column is computed, or null.</param>
/// <param name="SourceType">
/// The tool's own column, which the function does not have: the <c>OBJECTPROPERTYEX</c> <c>BaseType</c> of the source
/// table, <c>U</c> for a table, <c>V</c> for a view, <c>IF</c> or <c>TF</c> for a table-valued function; or null
/// when the source is of a linked server or not in the session's database, or the row names none.  A capture recorded
/// before it has no <c>columns</c> call at all.
/// </param>
/// <param name="PersistedNotNull">
/// The tool's own column too: true when the source is a table of the session's database, <see cref="SourceType" />
/// <c>U</c>, and its column is computed <c>PERSISTED NOT NULL</c> in <c>sys.computed_columns</c>, whose constraint
/// the server's <c>is_nullable</c> does not show; false otherwise.  Null in a capture recorded before it.  Only
/// <c>ColumnProofs</c> may act on it, since a row of the result read through an outer join is no row of the table.
/// </param>
internal sealed record SqlServerColumnRow(
    bool IsHidden,
    int ColumnOrdinal,
    string? Name,
    bool? IsNullable,
    string? SystemTypeName,
    int? MaxLength,
    int? Precision,
    int? Scale,
    string? UserTypeSchema,
    string? UserTypeName,
    string? AssemblyQualifiedTypeName,
    string? SourceServer,
    string? SourceDatabase,
    string? SourceSchema,
    string? SourceTable,
    string? SourceColumn,
    bool? IsIdentityColumn,
    bool? IsComputedColumn,
    string? SourceType,
    bool? PersistedNotNull = null
) : ISqlServerTypeRow;

/// <summary>
/// The type a row of <c>sp_describe_undeclared_parameters</c> or of <c>sys.dm_exec_describe_first_result_set</c> gives,
/// under the names of the latter, which <see cref="Types.SqlServerTypes" /> reads.  It adds no member to a record: a
/// row of <c>sp_describe_undeclared_parameters</c> gives its <c>suggested_</c> columns through an explicit
/// implementation, which a capture does not write.
/// </summary>
internal interface ISqlServerTypeRow
{
    /// <summary>The system type with its facets, or null.</summary>
    string? SystemTypeName { get; }

    /// <summary>The length in bytes, <c>-1</c> for <c>max</c>.</summary>
    int? MaxLength { get; }

    /// <summary>The precision, 0 for a type that has none.</summary>
    int? Precision { get; }

    /// <summary>The scale, 0 for a type that has none.</summary>
    int? Scale { get; }

    /// <summary>The schema of an alias or CLR type, or null.</summary>
    string? UserTypeSchema { get; }

    /// <summary>The name of an alias or CLR type, or null.</summary>
    string? UserTypeName { get; }

    /// <summary>The assembly-qualified name of a CLR type, or null.</summary>
    string? AssemblyQualifiedTypeName { get; }
}
