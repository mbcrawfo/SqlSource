namespace SqlSource.Generation.Models;

/// <summary>What a model is of its query.</summary>
internal enum ModelRole
{
    /// <summary>The query's parameters.</summary>
    Input,

    /// <summary>A row of the query's result.</summary>
    Output,
}
