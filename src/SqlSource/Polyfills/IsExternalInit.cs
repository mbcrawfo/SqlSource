using System.ComponentModel;

namespace System.Runtime.CompilerServices;

/// <summary>
/// Lets the compiler emit <see langword="init" /> accessors, and so records, on <c>netstandard2.0</c>, which does not
/// define this type.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class IsExternalInit;
