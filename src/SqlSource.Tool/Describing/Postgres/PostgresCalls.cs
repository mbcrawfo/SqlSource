using System.Collections.Generic;
using SqlSource.Tool.Describing.Postgres.Types;
using SqlSource.Tool.Describing.Postgres.Walk;

namespace SqlSource.Tool.Describing.Postgres;

// What the describer of PostgreSQL passes through the exchange, in one file: plain records that a capture can hold.
// A method's name and a member's name are a contract with the captures that exist: a member is added, nullable,
// and none is renamed.  tests/SqlSource.Tool.Tests/Describing/Capture/CallRecordTests.cs holds each to the rule.

/// <summary>
/// The request of <c>open</c>.  It holds nothing: nothing of a connection may reach a capture.  Its one member is
/// static, so it is written as <c>{}</c>.
/// </summary>
internal sealed record PostgresOpenRequest
{
    public static PostgresOpenRequest Instance { get; } = new();
}

/// <summary>What a session knows of its server, or why none was opened.</summary>
/// <param name="ServerVersion">The server's <c>server_version</c>, cut at its first space.</param>
/// <param name="DriverVersion">The version of the Npgsql that asked.</param>
/// <param name="Settings">The settings of the session that change a description.</param>
/// <param name="Error">Why no session was opened, or null.</param>
internal sealed record PostgresOpenAnswer(
    string? ServerVersion,
    string? DriverVersion,
    IReadOnlyList<PostgresSetting>? Settings,
    PostgresServerError? Error
);

/// <summary>A setting of the session, as <c>current_setting</c> gives it.</summary>
internal sealed record PostgresSetting(string Name, string Value);

/// <summary>
/// What the server said of a call it refused, each part null where the server gave none.  An error of <c>open</c>, one
/// that ended the session and one of a connection that was lost or timed out have a fixed text of the describer's own
/// in place of the message, and keep nothing else but the SQLSTATE (<see cref="ConnectionErrors" />).  Npgsql's
/// refusal of a text at <c>check</c> keeps Npgsql's message, which speaks of the query.
/// </summary>
/// <param name="SqlState">The five characters of the SQLSTATE.</param>
/// <param name="Message">The primary message.</param>
/// <param name="Detail">The detail.</param>
/// <param name="Hint">The hint.</param>
/// <param name="Position">The character of the text as sent that the error is at, counted from one.</param>
internal sealed record PostgresServerError(
    string? SqlState,
    string Message,
    string? Detail,
    string? Hint,
    int? Position
);

/// <summary>Everything that decides the answer of <c>prepare</c>.</summary>
/// <param name="Statement">The name of the prepared statement: a counter within its session, never random.</param>
/// <param name="Sql">The query's one statement, with <c>$n</c> in the place of each parameter.</param>
/// <param name="Types">
/// The type list: a declared type as written or <c>unknown</c>, for each parameter in order.
/// </param>
internal sealed record PostgresPrepareRequest(string Statement, string Sql, IReadOnlyList<string> Types);

/// <summary>The types the server resolved, or why it prepared nothing.</summary>
/// <param name="ParameterTypes">
/// <c>parameter_types</c> of <c>pg_prepared_statements</c> for the statement, as OIDs, in order.
/// </param>
/// <param name="Error">What the server said, or null.</param>
internal sealed record PostgresPrepareAnswer(IReadOnlyList<long>? ParameterTypes, PostgresServerError? Error);

/// <summary>The request of <c>deallocate</c>: the statement to free.</summary>
internal sealed record PostgresDeallocateRequest(string Statement);

/// <summary>Null when the statement was freed.  An error here is logged and never reported.</summary>
internal sealed record PostgresDeallocateAnswer(PostgresServerError? Error);

/// <summary>The request of <c>types</c>.</summary>
/// <param name="Oids">The types to resolve, sorted and each once.</param>
internal sealed record PostgresTypesRequest(IReadOnlyList<long> Oids);

/// <summary>The rows of the closure, or why the catalog could not be read.</summary>
/// <param name="Rows">
/// One row for each type asked for and for every type they reach: the element of an array, the base of a domain,
/// the subtype of a range, the range of a multirange.  In <c>oid</c> order.
/// </param>
/// <param name="Error">What the server said, or null.</param>
internal sealed record PostgresTypesAnswer(IReadOnlyList<PostgresTypeRow>? Rows, PostgresServerError? Error);

/// <summary>What the catalog says of one type.  Each member is named as the catalog names it.</summary>
/// <param name="Oid">The type.</param>
/// <param name="Typname">Its name in <c>pg_type</c>: <c>int4</c>, <c>_text</c>.</param>
/// <param name="Nspname">Its schema.</param>
/// <param name="Typtype"><c>b</c>, <c>c</c>, <c>d</c>, <c>e</c>, <c>m</c>, <c>p</c> or <c>r</c>.</param>
/// <param name="Typcategory">The category: <c>A</c> for an array.</param>
/// <param name="Typelem">The element of an array, or zero.</param>
/// <param name="Typbasetype">The base of a domain, or zero.</param>
/// <param name="Rngsubtype">The subtype of a range, or null for a type that is none.</param>
/// <param name="MultirangeRngsubtype">The subtype of a multirange's range, or null for a type that is none.</param>
/// <param name="FormatType"><c>format_type(oid, -1)</c>: the SQL spelling without a modifier.</param>
/// <param name="EnumLabels">The labels of an enum in their sort order, or null for a type that is none.</param>
/// <param name="Typtypmod">
/// <c>typtypmod</c>: the modifier a domain fixes for its base type, <c>-1</c> for none and for a type that is no
/// domain.  Null in an answer recorded before the member existed.
/// </param>
internal sealed record PostgresTypeRow(
    long Oid,
    string Typname,
    string Nspname,
    string Typtype,
    string Typcategory,
    long Typelem,
    long Typbasetype,
    long? Rngsubtype,
    long? MultirangeRngsubtype,
    string FormatType,
    IReadOnlyList<string>? EnumLabels,
    int? Typtypmod = null
);

/// <summary>The request of <c>spellings</c>.</summary>
/// <param name="Pairs">The types and modifiers to spell, sorted and each once.</param>
internal sealed record PostgresSpellingsRequest(IReadOnlyList<PostgresTypmodPair> Pairs);

/// <summary>A type and a modifier of it, as <c>format_type</c> takes them.</summary>
/// <param name="Oid">The type.</param>
/// <param name="Modifier">The modifier, <c>-1</c> for none.</param>
internal sealed record PostgresTypmodPair(long Oid, int Modifier);

/// <summary>The spellings the server gave, or why it gave none.</summary>
/// <param name="Rows">One for each pair asked for, in <c>oid</c> and modifier order.</param>
/// <param name="Error">What the server said, or null.</param>
internal sealed record PostgresSpellingsAnswer(IReadOnlyList<PostgresSpellingRow>? Rows, PostgresServerError? Error);

/// <summary>The server's spelling of one type with one modifier.</summary>
/// <param name="Oid">The type.</param>
/// <param name="Modifier">The modifier, <c>-1</c> for none.</param>
/// <param name="FormatType"><c>format_type(oid, modifier)</c>: the SQL spelling.</param>
internal sealed record PostgresSpellingRow(long Oid, int Modifier, string FormatType);

/// <summary>Everything that decides the answer of <c>columns</c>.</summary>
/// <param name="Statement">The prepared statement to describe.</param>
/// <param name="Parameters">
/// How many parameters the query's list has: the statement is given a <c>NULL</c> for each.
/// </param>
internal sealed record PostgresColumnsRequest(string Statement, int Parameters);

/// <summary>The columns of a prepared statement, or why it could not be described.</summary>
/// <param name="Columns">The columns in order.  A statement without rows has none.</param>
/// <param name="Error">What the server said, or null.</param>
internal sealed record PostgresColumnsAnswer(IReadOnlyList<PostgresColumnRow>? Columns, PostgresServerError? Error);

/// <summary>One column, as the row description of the protocol and Npgsql's reading of it hold it.</summary>
/// <param name="Ordinal">The column's place, from zero.</param>
/// <param name="Name">The name as the server returned it.</param>
/// <param name="TypeOid">The type.  The protocol reports the base of a domain, with the domain's modifier.</param>
/// <param name="TableOid">The table the column comes from, or zero.</param>
/// <param name="AttributeNumber">The column of that table, zero for none and below zero for a system column.</param>
/// <param name="Length">Npgsql's <c>ColumnSize</c>, or null.</param>
/// <param name="Precision">Npgsql's <c>NumericPrecision</c>, or null.</param>
/// <param name="Scale">Npgsql's <c>NumericScale</c>, or null.</param>
/// <param name="TypeModifier">
/// The modifier the row description gave, <c>-1</c> for none; null in a capture recorded before it was read.
/// </param>
internal sealed record PostgresColumnRow(
    int Ordinal,
    string Name,
    long TypeOid,
    long TableOid,
    int AttributeNumber,
    int? Length,
    int? Precision,
    int? Scale,
    int? TypeModifier = null
);

/// <summary>The request of <c>tables</c>.</summary>
/// <param name="Oids">The tables the columns of a statement name, sorted and each once.</param>
internal sealed record PostgresTablesRequest(IReadOnlyList<long> Oids);

/// <summary>The tables asked for, or why the catalog could not be read.</summary>
/// <param name="Tables">One for each table that exists, in <c>oid</c> order.</param>
/// <param name="Error">What the server said, or null.</param>
internal sealed record PostgresTablesAnswer(IReadOnlyList<PostgresTableRow>? Tables, PostgresServerError? Error);

/// <summary>What the catalog says of one relation.</summary>
/// <param name="Oid">The relation.</param>
/// <param name="Relkind"><c>r</c> for a table, <c>v</c> for a view, <c>m</c>, <c>f</c>, <c>p</c>.</param>
/// <param name="Schema">Its schema.</param>
/// <param name="Name">Its name.</param>
/// <param name="Columns">
/// Every attribute with <c>attnum</c> above zero that is not dropped, in <c>attnum</c> order.  A table without a
/// column of its own has none.
/// </param>
/// <param name="Relhassubclass">
/// <c>relhassubclass</c>: whether the relation has or had inheritance children, which PostgreSQL clears only
/// lazily after the last is dropped.  Null in an answer recorded before the member existed.
/// </param>
/// <param name="HasForeignPartition">
/// Whether the relation is a partitioned table (<c>p</c>) with a foreign table (<c>f</c>) anywhere in its partition
/// tree, at any depth, by <c>pg_partition_tree</c>: a foreign partition inherits the parent's <c>NOT NULL</c>, which
/// nothing enforces on its remote rows.  False for a relation that is not partitioned.  It is no catalog column.
/// Null in an answer recorded before the member existed, which reads as no foreign partition.
/// </param>
/// <param name="HasInsteadRule">
/// Whether the relation has a <c>DO INSTEAD</c> rule on <c>INSERT</c>, <c>UPDATE</c> or <c>DELETE</c>, by
/// <c>pg_rewrite</c>: the statement is replaced by the rule's action, usually on another relation, whose rows the
/// <c>RETURNING</c> list gives, while the row description keeps this relation as each column's origin.  It is no
/// catalog column.  Null in an answer recorded before the member existed, which reads as no such rule.
/// </param>
internal sealed record PostgresTableRow(
    long Oid,
    string Relkind,
    string Schema,
    string Name,
    IReadOnlyList<PostgresAttributeRow> Columns,
    bool? Relhassubclass,
    bool? HasForeignPartition,
    bool? HasInsteadRule
);

/// <summary>What the catalog says of one column of a relation.</summary>
/// <param name="Attnum">The column's number in its relation.</param>
/// <param name="Name">Its name.</param>
/// <param name="NotNull"><c>attnotnull</c>.</param>
/// <param name="Identity"><c>attidentity</c>: empty, <c>a</c> or <c>d</c>.</param>
/// <param name="Generated"><c>attgenerated</c>: empty, <c>s</c> or <c>v</c>.</param>
/// <param name="TypeOid"><c>atttypid</c>: the declared type, a domain where the column is one.</param>
/// <param name="NotNullValidated">
/// <c>convalidated</c> of the column's <c>NOT NULL</c> constraint in <c>pg_constraint</c>, which PostgreSQL 18 keeps
/// and may hold as not validated while <c>attnotnull</c> is set.  Null when the server said nothing: an older server,
/// a column without the constraint, or an answer recorded before the member existed.
/// </param>
internal sealed record PostgresAttributeRow(
    int Attnum,
    string Name,
    bool NotNull,
    string Identity,
    string Generated,
    long TypeOid,
    bool? NotNullValidated
);

/// <summary>Everything that decides the answer of <c>check</c>.</summary>
/// <param name="Sql">
/// The query's one statement as it was written, <c>@name</c> for each parameter, without its trailing <c>;</c>.
/// </param>
/// <param name="Parameters">The parameters of the query's list, in order.</param>
internal sealed record PostgresCheckRequest(string Sql, IReadOnlyList<PostgresCheckParameter> Parameters);

/// <summary>A parameter as generated code will bind it.</summary>
/// <param name="Name">The name, as the query's list has it.</param>
/// <param name="DataTypeName">
/// The <c>bindName</c> of the type the describer gave the parameter (<see cref="PostgresBindNames" />).  A capture
/// recorded before the sidecar had one holds the type's <c>name</c>, which is the same for every type that Npgsql
/// bound by its <c>name</c> but a type of <c>pg_catalog</c> that a user type hides on the <c>search_path</c>, whose
/// <c>name</c> has the schema and whose <c>bindName</c> does not.
/// </param>
internal sealed record PostgresCheckParameter(string Name, string DataTypeName);

/// <summary>What Npgsql's own describe of the SQL gave.  One of Columns, Error and DriverError is not null.</summary>
/// <param name="Columns">The columns of the result, in order.</param>
/// <param name="Error">
/// What the server said, or what Npgsql said of a text it would not send: it has no SQLSTATE then.
/// </param>
/// <param name="DriverError">The parameter whose type Npgsql cannot bind.</param>
/// <param name="Sent">
/// What Npgsql sent for the SQL, set with <paramref name="Columns" />.  Null in an answer recorded before the member
/// existed, whose check compares the columns alone.
/// </param>
/// <param name="TextRefused">
/// True when <paramref name="Error" /> is Npgsql's refusal of the text, and false for any other error: one of the
/// server, or a connection that was lost.  Null in an answer recorded before the member existed, which reads as
/// true: an error of such an answer without a SQLSTATE was the refusal of the text.
/// </param>
internal sealed record PostgresCheckAnswer(
    IReadOnlyList<PostgresCheckColumn>? Columns,
    PostgresServerError? Error,
    PostgresBindError? DriverError,
    PostgresCheckSent? Sent = null,
    bool? TextRefused = null
);

/// <summary>What Npgsql sent for the SQL of <c>check</c> (<see cref="BatchRewrites" />).</summary>
/// <param name="Text">Npgsql's rewrite of the SQL, with <c>$n</c> in the place of each parameter it found.</param>
/// <param name="Parameters">
/// The <c>ParameterName</c> of the parameter each <c>$n</c> stands for, in the order of the numbers.  A parameter that
/// the SQL does not reference is not among them.
/// </param>
internal sealed record PostgresCheckSent(string Text, IReadOnlyList<string> Parameters);

/// <summary>A column as Npgsql's describe of the original SQL reports it.</summary>
/// <param name="Name">The name.</param>
/// <param name="TypeOid">The type.</param>
/// <param name="DataTypeName">Npgsql's own name for the type.</param>
internal sealed record PostgresCheckColumn(string Name, long TypeOid, string DataTypeName);

/// <summary>A type name that Npgsql refused before it sent anything.</summary>
/// <param name="Parameter">The first parameter of the list whose type is refused.</param>
/// <param name="Message">Npgsql's own message.</param>
internal sealed record PostgresBindError(string Parameter, string Message);

/// <summary>A relation as a plan names it: its schema and its name apart, since a name may hold a dot.</summary>
internal sealed record PostgresRelationName(string Schema, string Name);

/// <summary>The request of <c>relations</c>.  Its answer is the one of <c>tables</c>.</summary>
/// <param name="Relations">
/// The relations a plan scans (<see cref="PlanRelations" />) that <c>tables</c> did not answer, in schema and name
/// order, each once.
/// </param>
internal sealed record PostgresRelationsRequest(IReadOnlyList<PostgresRelationName> Relations);

/// <summary>The request of <c>overloads</c>.</summary>
/// <param name="Functions">
/// The names of the functions, aggregates among them, that the plan's expressions print and that the walk reads by
/// their name in <c>pg_catalog</c>, in ordinal order, each once.
/// </param>
/// <param name="Operators">The names of the operators of the same kind, in ordinal order, each once.</param>
internal sealed record PostgresOverloadsRequest(IReadOnlyList<string> Functions, IReadOnlyList<string> Operators);

/// <summary>
/// Which of the names asked for a schema other than <c>pg_catalog</c> holds, or <c>pg_catalog</c> holds as an object
/// that is not built in, or why it could not be read.
/// </summary>
/// <param name="Functions">
/// The names of the request's functions that some other schema holds, or <c>pg_catalog</c> holds as an object that is
/// not built in, whatever the arguments, in ordinal order, or null with an error.
/// </param>
/// <param name="Operators">
/// The names of the request's operators that some other schema holds, or <c>pg_catalog</c> holds as an object that is
/// not built in, or null with an error.
/// </param>
/// <param name="Error">What the server said, or null.  It is logged and never reported.</param>
internal sealed record PostgresOverloadsAnswer(
    IReadOnlyList<string>? Functions,
    IReadOnlyList<string>? Operators,
    PostgresServerError? Error
);

/// <summary>The request of <c>partitions</c>.</summary>
/// <param name="Relations">
/// The relations the members of a plan's <c>Append</c>s scan (<see cref="PlanPartitions" />), in schema and name
/// order, each once.
/// </param>
internal sealed record PostgresPartitionsRequest(IReadOnlyList<PostgresRelationName> Relations);

/// <summary>The ancestors of each relation asked for, or why the catalog could not be read.</summary>
/// <param name="Rows">One for each relation that exists, in schema and name order.</param>
/// <param name="Error">What the server said, or null.  It is logged and never reported.</param>
internal sealed record PostgresPartitionsAnswer(IReadOnlyList<PostgresPartitionRow>? Rows, PostgresServerError? Error);

/// <summary>What <c>pg_partition_ancestors</c> says of one relation.</summary>
/// <param name="Schema">The relation's schema.</param>
/// <param name="Name">Its name.</param>
/// <param name="Ancestors">
/// The partitioned tables it is a partition of, by OID, nearest first, without itself: none for a relation that is
/// no partition.
/// </param>
internal sealed record PostgresPartitionRow(string Schema, string Name, IReadOnlyList<long> Ancestors);

/// <summary>Everything that decides the answer of <c>explain</c>.</summary>
/// <param name="Statement">The prepared statement to plan.</param>
/// <param name="Parameters">
/// The number of parameter types the server resolved for the statement, a <c>$n</c> the tool's lexer did not see
/// included: a <c>NULL</c> is passed for each, since <c>EXPLAIN EXECUTE</c> takes no other number.
/// </param>
/// <param name="Settings">
/// Settings of the planner as <c>name=value</c>, each of <see cref="PlannerSettings" />.  The tool passes none; a
/// test forces a join method with them.
/// </param>
internal sealed record PostgresExplainRequest(string Statement, int Parameters, IReadOnlyList<string> Settings);

/// <summary>The generic plan of a statement, or why the server gave none.</summary>
/// <param name="Plan">
/// The text <c>EXPLAIN (VERBOSE, FORMAT JSON)</c> returned, as the server wrote it.
/// </param>
/// <param name="Error">
/// What the server said, or null.  It is logged and never reported, but <c>26000</c>, a statement that is gone,
/// which is <c>SQLSRC236</c>.
/// </param>
internal sealed record PostgresExplainAnswer(string? Plan, PostgresServerError? Error);
