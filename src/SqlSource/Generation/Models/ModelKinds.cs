using SqlSource.Settings;

namespace SqlSource.Generation.Models;

/// <summary>
/// A <see cref="ModelKind" /> as a user writes it, and as the generated model declares it.
/// </summary>
internal static class ModelKinds
{
    /// <summary>
    /// The C# keywords that declare <paramref name="kind" />: <c>record</c>, <c>sealed record</c>, <c>class</c> or
    /// <c>sealed class</c>.
    /// </summary>
    public static string Keyword(ModelKind kind) =>
        kind switch
        {
            ModelKind.Record => "record",
            ModelKind.SealedRecord => "sealed record",
            ModelKind.Class => "class",
            ModelKind.SealedClass or _ => "sealed class",
        };
}
