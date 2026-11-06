namespace SqlSource.Parsing;

/// <summary>
/// What one use of a parameter in the SQL of a query is.  <see cref="SqlListUses" /> tells them apart.
/// </summary>
internal enum SqlParameterUse
{
    /// <summary>A single value, as in <c>id = @id</c>.</summary>
    Value,

    /// <summary>A list, written directly after <c>IN</c>, as in <c>id IN @ids</c>.</summary>
    List,

    /// <summary>
    /// A list that a <c>NOT</c> negates, as in <c>id NOT IN @ids</c> or <c>NOT (id IN @ids)</c>, which is true of a row
    /// whose value is <c>NULL</c> when the list is empty.
    /// </summary>
    NegatedList,

    /// <summary>A test of whether the parameter is null: <c>@ids IS NULL</c> or <c>@ids IS NOT NULL</c>.</summary>
    NullTest,
}
