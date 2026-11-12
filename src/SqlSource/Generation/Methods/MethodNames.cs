using SqlSource.Settings;

namespace SqlSource.Generation.Methods;

/// <summary>
/// The names of a query's methods, by its <c>method-type</c>, for the planner and for the placement's check of a
/// nested class's name, so that the two agree.  A synchronous method has the query's name.  An asynchronous one has it
/// with <c>Async</c> after it under <c>async-method-suffix</c>, and always under <c>method-type=both</c>, so that the
/// two never differ only by an argument with a default.  A collection method has the name of its query's method of
/// the same synchrony.
/// </summary>
internal static class MethodNames
{
    private const string AsyncSuffix = "Async";

    /// <summary>The name of the query's asynchronous method, and of a collection method that follows it.</summary>
    public static string AsyncNameOf(string query, QuerySettings settings) => query + AsyncSuffixOf(settings);

    /// <summary>
    /// What the query's asynchronous method adds to its name: <c>Async</c> under <c>async-method-suffix</c> or
    /// <c>method-type=both</c>, else nothing.
    /// </summary>
    public static string AsyncSuffixOf(QuerySettings settings) =>
        settings.MethodType == MethodType.Both || settings.Parameters.IsOn(GeneratorParameters.AsyncMethodSuffix)
            ? AsyncSuffix
            : string.Empty;

    /// <summary>
    /// Whether the query's output can give it a synchronous method, its shape aside: at <c>methods</c>, under
    /// <c>sync</c> or <c>both</c>.  A repository's methods stay asynchronous in this release.
    /// </summary>
    public static bool HasSynchronous(QuerySettings settings) =>
        settings.Output == OutputKind.Methods && settings.MethodType != MethodType.Async;

    /// <summary>
    /// Whether the query's output can give it an asynchronous method, its shape aside: at <c>methods</c>, under
    /// <c>async</c> or <c>both</c>, and at <c>repository</c> under any, since a repository's methods stay asynchronous
    /// in this release.
    /// </summary>
    public static bool HasAsynchronous(QuerySettings settings) =>
        settings.Output switch
        {
            OutputKind.Methods => settings.MethodType != MethodType.Sync,
            OutputKind.Repository => true,
            OutputKind.Sql or OutputKind.Models => false,
            _ => throw UnexpectedEnumValueException.Of(settings.Output),
        };
}
