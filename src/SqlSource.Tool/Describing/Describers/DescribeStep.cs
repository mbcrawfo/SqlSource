namespace SqlSource.Tool.Describing.Describers;

/// <summary>
/// The steps of describing a query, which the <c>step:</c> line of an error names.  The epic's design
/// (<c>docs/superpowers/specs/2026-10-07-query-generation-epic-design.md</c>, "Error messages") fixes the list.
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
