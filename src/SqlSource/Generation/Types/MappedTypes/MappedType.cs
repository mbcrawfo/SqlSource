namespace SqlSource.Generation.Types.MappedTypes;

/// <summary>
/// A C# type that the type override, the column types or a <c>-- param:</c> or <c>-- column:</c> marker names, as the
/// compilation resolves it.  It holds no symbol, so it is equal from one edit to the next until the type itself
/// changes.
/// </summary>
/// <param name="Name">The type as <see cref="Settings.ClrTypes.ClrTypeName.Text" /> writes it.</param>
/// <param name="Status">Whether the type is found, and if not, why.</param>
/// <param name="IsValueType">Whether the type is a value type.  False unless it is found.</param>
/// <param name="IsPublic">
/// Whether the type, its containing types and its arguments are all declared public.  False unless it is found.
/// </param>
/// <param name="WarningIds">
/// The ids of the compiler's warnings that a use of the type gives, for the type, a containing type or an argument
/// marked <c>[Obsolete]</c> or <c>[Experimental]</c>, in ordinal order, each once.  Empty unless it is found.
/// </param>
/// <param name="Text">
/// The type as generated code writes it: <paramref name="Name" /> with each short name in it written as the full name
/// of the type it found, <c>global::App.UserId</c> for <c>UserId</c>.  <paramref name="Name" /> itself for a name with
/// no short name in it, or one that is not found.
/// </param>
/// <param name="Candidates">
/// For a name whose status is <see cref="MappedTypeStatus.Ambiguous" /> because a short name in it has several
/// types, the full names of those types as a user writes them, <c>App.UserId</c>, and <c>global::UserId</c> for one of
/// the global namespace, in ordinal order.  Empty otherwise.
/// </param>
/// <param name="Package">
/// For a name that is found, the first short name in it, the name's own before its arguments' in their order, whose
/// type is of an assembly that only a package the project does not list itself brings.  Null for a name with no such
/// short name, a full name, a keyword, a type of the project, or a name that is not found.
/// </param>
/// <param name="ElementIsValueType">
/// Whether the type is an array of one rank whose element, without the <c>?</c> of a nullable element, is a value
/// type.  False unless it is found.
/// </param>
internal sealed record MappedType(
    string Name,
    MappedTypeStatus Status,
    bool IsValueType,
    bool IsPublic,
    EquatableArray<string> WarningIds,
    string Text,
    EquatableArray<string> Candidates,
    PackageBinding? Package,
    bool ElementIsValueType = false
);
