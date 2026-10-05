using Microsoft.CodeAnalysis;

namespace SqlSource;

/// <summary>
/// Generates C# source for SQL queries.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SqlSourceGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Nothing is registered yet.
    }
}
