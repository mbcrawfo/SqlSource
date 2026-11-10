using SqlSource.Diagnostics;
using SqlSource.Generation.Models;
using SqlSource.Generation.Repositories;
using SqlSource.Parsing.Tokens;
using SqlSource.Settings;
using SqlSource.Snapshot;

namespace SqlSource.Generation.Methods;

/// <summary>
/// What the method of one query needs: everything <c>QueryMethodWriter</c> writes it from.
/// </summary>
/// <param name="Name">The method's name: the query's, with <c>Async</c> after it under <c>async-method-suffix</c>.
/// </param>
/// <param name="QueryName">The query's name, as <c>UnexpectedRowCountException</c> names it.</param>
/// <param name="SummaryXml">The summary as XML: already escaped, and the default when the query has none.</param>
/// <param name="Shape">What the method returns, resolved: the marker's, or the default of the entry.</param>
/// <param name="Collection">The type that holds the rows of <see cref="ResultShape.Many" />.</param>
/// <param name="Engine">The engine of the query's file, which chooses how a parameter is bound.</param>
/// <param name="Segments">The SQL in the form that is emitted, its comments kept under <c>keep-comments</c>.</param>
/// <param name="ValidateTokens">Whether the method checks that each token's argument has text.</param>
/// <param name="UseArrayPool">
/// Whether a method of <see cref="ResultShape.Many" /> may read its rows into buffers from the shared array pool: false
/// under <c>array-pool=off</c>.  It is the resolved setting, true for a method of any shape unless the parameter is
/// off, so a reader checks the shape and the collection type as well, as <see cref="Emitting.RowBuffer.IsUsedBy" />
/// does.
/// </param>
/// <param name="PadsLists">
/// Whether the method pads each list query parameter to a fixed size: false under <c>list-padding=off</c>.  It is the
/// resolved setting, true for a method without a list as well unless the parameter is off.
/// </param>
/// <param name="Input">The input model, or null when the query has no parameters.</param>
/// <param name="Output">The output model, or null when the query's entry has no rows.</param>
/// <param name="NameLocation">Where the query's name is, for a diagnostic about the method.</param>
internal sealed record QueryMethod(
    string Name,
    string QueryName,
    string SummaryXml,
    ResultShape Shape,
    CollectionKind Collection,
    DatabaseEngine Engine,
    EquatableArray<SqlSegment> Segments,
    bool ValidateTokens,
    bool UseArrayPool,
    bool PadsLists,
    MethodInput? Input,
    MethodOutput? Output,
    LocationInfo NameLocation
)
{
    /// <summary>
    /// Whether the method streams its rows: a <see cref="ResultShape.Many" /> query of
    /// <see cref="CollectionKind.IAsyncEnumerable" />, whose method returns a local iterator that reads each row as it
    /// is asked for.  It is computed, so it takes no part in equality.
    /// </summary>
    public bool Streams => Shape == ResultShape.Many && Collection == CollectionKind.IAsyncEnumerable;

    /// <summary>
    /// Where the method goes and how visible it is, from its query's placement: the default is a public method of the
    /// extension class.
    /// </summary>
    public MethodHost Host { get; init; }

    /// <summary>
    /// The member that gives the method its connection, for a repository's method, which is an instance method that
    /// takes no connection, transaction or command timeout.  Null for any other method, which takes its connection.
    /// </summary>
    public ConnectionSource? Source { get; init; }

    /// <summary>
    /// How the method runs its query once for each item of a collection, in batches, when it is the collection method
    /// of a query: the query's method with this set, placed right after it.  Null for the query's own method.  A
    /// type's methods may then hold two of one name, the query method and its collection method, so a consumer of
    /// <see cref="TypeMethods.Methods" /> must not key them by name alone.
    /// </summary>
    public BatchCall? Batch { get; init; }
}

/// <summary>
/// What makes a method a query's collection method.
/// </summary>
/// <param name="Transaction">How the method wraps its batches in a transaction: the query's <c>transaction</c>.</param>
internal sealed record BatchCall(TransactionMode Transaction);

/// <summary>
/// The input model of a query's method.
/// </summary>
/// <param name="TypeText">The model's type, from <c>global::</c>.</param>
/// <param name="Parameters">The query's parameters, in the order of its parameter list.</param>
internal sealed record MethodInput(string TypeText, EquatableArray<MethodParameter> Parameters);

/// <summary>
/// One parameter of a query, and the property of the input model that gives its value.
/// </summary>
/// <param name="Name">The parameter's name in the SQL, without the <c>@</c>.</param>
/// <param name="Property">The property's name.</param>
/// <param name="TypeText">
/// The type the parameter maps to without <c>?</c>, from <c>global::</c> or as a keyword: the property's type, unless
/// <paramref name="Sequence" /> declares the property as a sequence of its element.
/// </param>
/// <param name="IsValueType">Whether the property's type is a value type.</param>
/// <param name="IsNullable">Whether the model calls the property nullable.</param>
/// <param name="Write">
/// The converter the property's value is converted through before it is bound, or null for a value bound as it is.
/// </param>
/// <param name="Described">
/// The type the entry describes for the parameter, or null.  The PostgreSQL binding sets the canonical name that
/// <c>PostgresParameterBinder.DataTypeNameOf</c> derives from it, or its <see cref="SidecarType.BindName" /> where none
/// can be derived; the SQL Server binding sets the <c>SqlDbType</c> and facets it names.
/// </param>
/// <param name="Sequence">
/// Whether the property is a sequence, the property's <c>ModelProperty.Sequence</c>: for
/// <see cref="ParameterSequence.Array" />, <paramref name="TypeText" /> is the array <c>E[]</c> and the property an
/// <c>IEnumerable&lt;E&gt;</c>, which the PostgreSQL binding sends as an <c>IList&lt;E&gt;</c>; for
/// <see cref="ParameterSequence.List" />, <paramref name="TypeText" />, <paramref name="IsValueType" />,
/// <paramref name="Write" /> and <paramref name="Described" /> are the element's, and <paramref name="IsNullable" />
/// says whether the list can be null.
/// </param>
/// <param name="HasNullTest">
/// Whether the SQL tests the list with <c>IS NULL</c> or <c>IS NOT NULL</c>, for which the method sends a parameter of
/// the list's own name that says whether it is null.
/// </param>
/// <param name="ExpansionPrefix">
/// For a list, the prefix of the names its values are sent by, before each value's index: <c>ids_</c>, as
/// <see cref="ListNames.PrefixOf" /> gives it.  Null for any other parameter.
/// </param>
internal sealed record MethodParameter(
    string Name,
    string Property,
    string TypeText,
    bool IsValueType,
    bool IsNullable,
    Conversion? Write = null,
    SidecarType? Described = null,
    ParameterSequence Sequence = ParameterSequence.None,
    bool HasNullTest = false,
    string? ExpansionPrefix = null
)
{
    /// <summary>
    /// Whether the property holds the parameter's array as any sequence, <see cref="ParameterSequence.Array" />, which
    /// is matched, as a value that can be null, and copied into an array where it must be one.  A list's elements are
    /// each bound as a value of the element's type.  It is computed, so it takes no part in equality.
    /// </summary>
    public bool IsArraySequence => ParameterSequences.IsArray(Sequence);

    /// <summary>
    /// Whether the parameter is a list, <see cref="ParameterSequence.List" />, whose values the method sends one by
    /// one.  It is computed, so it takes no part in equality.
    /// </summary>
    public bool IsList => Sequence == ParameterSequence.List;
}

/// <summary>
/// The output model of a query's method.
/// </summary>
/// <param name="TypeText">The model's type, from <c>global::</c>.</param>
/// <param name="IsRecord">Whether the model is a record, built by its positional constructor, or a class.</param>
/// <param name="Columns">The columns, in the order the query returns them.</param>
/// <param name="ConstructorOrder">
/// The ordinals of the columns in the order of the model's properties, which is the column order unless the model is
/// sorted: the record's constructor takes column <c>ConstructorOrder[k]</c> as its argument <c>k</c>.
/// </param>
internal sealed record MethodOutput(
    string TypeText,
    bool IsRecord,
    EquatableArray<MethodColumn> Columns,
    EquatableArray<int> ConstructorOrder
);

/// <summary>
/// One column of a query's rows, and the property of the output model it is read into.
/// </summary>
/// <param name="Name">The column's name, as the server gives it.</param>
/// <param name="Property">The property's name.</param>
/// <param name="TypeText">The property's type without <c>?</c>, from <c>global::</c> or as a keyword.</param>
/// <param name="IsNullable">Whether the model calls the property nullable.</param>
/// <param name="Read">
/// The converter the value read is converted through into the property's type, or null for a value read as the
/// property's type.
/// </param>
/// <param name="ThrowsOnJsonNull">
/// Whether the read of the column, which is not null, can give null for a value that is not a <c>NULL</c>, a JSON
/// <c>null</c>, which then throws: for a property of a <see cref="Converters.JsonTypes.IsNode" /> type, whether
/// converted or read by the driver, and for the serializer's read of the whole value.
/// </param>
internal sealed record MethodColumn(
    string Name,
    string Property,
    string TypeText,
    bool IsNullable,
    Conversion? Read = null,
    bool ThrowsOnJsonNull = false
)
{
    /// <summary>
    /// The type the column is read as: the database side of a <see cref="Read" /> of the whole value; for one by
    /// element, the database's array with elements that can be null, <c>long?[]</c> when its element is a value
    /// type, which Npgsql refuses to read as <c>long[]</c> when it holds a <c>NULL</c>, and the array as it is,
    /// <c>string[]</c>, otherwise; or <see cref="TypeText" /> when it has no converter.  A not-null column read as
    /// exactly <see cref="Emitting.QueryMethods.RowReaderWriter.VectorType" />,
    /// <c>global::Microsoft.Data.SqlTypes.SqlVector&lt;float&gt;</c>, is checked with <c>IsNull</c> after its read
    /// and before its converter, since SqlClient gives <c>SqlVector&lt;float&gt;.Null</c> for a <c>NULL</c>.
    /// </summary>
    public string ReadText =>
        Read switch
        {
            null => TypeText,
            { IsByElement: false } read => read.DatabaseText,
            var read => read.ReadElementText + "[]",
        };
}
