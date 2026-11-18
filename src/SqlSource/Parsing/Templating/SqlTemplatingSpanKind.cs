namespace SqlSource.Parsing.Templating;

/// <summary>
/// What a span of a <see cref="SqlTemplatingPlan" /> is.  The order of the members is the order of two spans that
/// start at one place.
/// </summary>
internal enum SqlTemplatingSpanKind
{
    /// <summary>A token, <c>{{name}}</c> or <c>{{name:default}}</c>, in the SQL outside comments.</summary>
    Token,

    /// <summary>A parameter, or a variable the query declares, that is not a list.</summary>
    Parameter,

    /// <summary>A parameter used as a list, as in <c>id IN @ids</c> or <c>id NOT IN @ids</c>.</summary>
    List,

    /// <summary>A line comment in the form of a marker, wherever it stands.</summary>
    MarkerComment,

    /// <summary>
    /// The end of a query that is not the file's last and whose SQL does not end in <c>;</c>: an empty span right
    /// after its last SQL.
    /// </summary>
    QueryEnd,
}
