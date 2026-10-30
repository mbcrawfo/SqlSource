using SqlSource.Settings.Libraries;

namespace SqlSource.Generation.Converters;

/// <summary>Which library a converter belongs to, and the engines and drivers it serves.</summary>
/// <param name="Library">The library whose setting turns the converter on.</param>
/// <param name="Targets">The engines and drivers the converter serves.</param>
internal sealed record LibraryScope(SqlLibrary Library, ConverterTargets Targets);
