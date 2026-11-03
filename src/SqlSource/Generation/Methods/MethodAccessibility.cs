namespace SqlSource.Generation.Methods;

/// <summary>
/// How visible a query's method is, which the writer spells as a modifier before <c>static</c>.
/// </summary>
internal enum MethodAccessibility
{
    /// <summary>The method is <c>public</c>.</summary>
    Public,

    /// <summary>The method is <c>internal</c>.</summary>
    Internal,

    /// <summary>The method is <c>private</c>.</summary>
    Private,
}
