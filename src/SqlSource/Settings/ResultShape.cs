namespace SqlSource.Settings;

/// <summary>
/// What a query's generated method returns, as the name marker's <c>-&gt; shape</c> says.
/// </summary>
internal enum ResultShape
{
    /// <summary>Every row, in the collection type.</summary>
    Many,

    /// <summary>One row.  None, or more than one, is an error at run time.</summary>
    One,

    /// <summary>One row or null.  More than one is an error at run time.</summary>
    OneOptional,

    /// <summary>Nothing.</summary>
    None,

    /// <summary>The number of rows the statement affected.</summary>
    RowCount,
}
