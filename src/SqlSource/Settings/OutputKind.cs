namespace SqlSource.Settings;

/// <summary>
/// What SqlSource generates for a query.  The generator's own form of the <c>GeneratorOutput</c> enum that
/// <c>AttributeSource</c> emits, member for member.
/// </summary>
internal enum OutputKind
{
    /// <summary>The constant or the token method.</summary>
    Sql = 0,

    /// <summary><see cref="Sql" />, and the input and output types.</summary>
    Models = 1,

    /// <summary><see cref="Models" />, and the method that runs the query.</summary>
    CodeGen = 2,
}
