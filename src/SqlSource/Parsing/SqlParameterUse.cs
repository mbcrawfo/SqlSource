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

    /// <summary>A test of whether the parameter is null: <c>@ids IS NULL</c> or <c>@ids IS NOT NULL</c>.</summary>
    NullTest,
}
