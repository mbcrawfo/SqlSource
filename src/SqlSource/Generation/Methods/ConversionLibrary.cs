using SqlSource.Settings.Libraries;

namespace SqlSource.Generation.Methods;

/// <summary>
/// A library whose generated conversion a method calls.  The library is held in a record, which an
/// <see cref="EquatableArray{T}" /> can hold, since an enum does not implement <see cref="System.IEquatable{T}" />.
/// </summary>
/// <param name="Library">The library.</param>
internal readonly record struct ConversionLibrary(SqlLibrary Library);
