using Microsoft.CodeAnalysis;

namespace SqlSource.Generation;

/// <summary>
/// The pipeline's own operators on a provider of values.
/// </summary>
internal static class IncrementalValuesProviderExtensions
{
    /// <summary>
    /// The values that are not null.  Its result is a <c>Select</c> after the filter, so that a name given after it
    /// names that <c>Select</c>, as it would after a <c>Where</c> and a <c>Select</c> written out.
    /// </summary>
    public static IncrementalValuesProvider<T> WhereNotNull<T>(this IncrementalValuesProvider<T?> source)
        where T : class => source.Where(static value => value is not null).Select(static (value, _) => value!);
}
