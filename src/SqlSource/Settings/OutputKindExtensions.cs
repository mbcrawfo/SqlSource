namespace SqlSource.Settings;

/// <summary>What an <see cref="OutputKind" /> brings, so that code asks for it and does not list the outputs.</summary>
internal static class OutputKindExtensions
{
    /// <summary>Whether the output gives each query a method that runs it, in place of a constant.</summary>
    public static bool HasMethod(this OutputKind output) => output is OutputKind.Methods or OutputKind.Repository;
}
