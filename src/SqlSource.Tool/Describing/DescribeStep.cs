namespace SqlSource.Tool.Describing;

/// <summary>
/// The steps of describing a query, which the <c>step:</c> line of an error names.  The epic outline fixes the list.
/// </summary>
internal enum DescribeStep
{
    DescribeParameters,
    CheckParameters,
    DescribeColumns,
    Catalog,
    Explain,
    Walk,
    TableMatch,
    ResolveType,
    WriteSidecar,
}
