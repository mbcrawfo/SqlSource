using SqlSource.Diagnostics;
using SqlSource.Parsing.Tokens;
using SqlSource.Settings;

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
    MethodInput? Input,
    MethodOutput? Output,
    LocationInfo NameLocation
);

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
/// <param name="IsValueType">Whether the property's type is a value type.</param>
/// <param name="IsNullable">Whether the model calls the property nullable.</param>
/// <param name="Write">
/// The converter the property's value is converted through before it is bound, or null for a value bound as it is.
/// </param>
internal sealed record MethodParameter(
    string Name,
    string Property,
    bool IsValueType,
    bool IsNullable,
    Conversion? Write = null
);

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
/// <param name="TypeText">
/// The property's type without <c>?</c>, from <c>global::</c> or as a keyword.  A not-null column whose text is exactly
/// <see cref="Emitting.QueryMethodWriter.VectorType" />,
/// <c>global::Microsoft.Data.SqlTypes.SqlVector&lt;float&gt;</c>, is checked with <c>IsNull</c> after its read, since
/// SqlClient gives <c>SqlVector&lt;float&gt;.Null</c> for a <c>NULL</c>.
/// </param>
/// <param name="IsNullable">Whether the model calls the property nullable.</param>
/// <param name="Read">
/// The converter the value read is converted through into the property's type, or null for a value read as the
/// property's type.
/// </param>
internal sealed record MethodColumn(
    string Name,
    string Property,
    string TypeText,
    bool IsNullable,
    Conversion? Read = null
)
{
    /// <summary>
    /// The type the column is read as: the database side of <see cref="Read" />, or <see cref="TypeText" /> when it
    /// has none.
    /// </summary>
    public string ReadText => Read?.DatabaseText ?? TypeText;
}
