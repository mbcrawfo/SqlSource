using SqlSource.Settings;

namespace SqlSource.Generation.Placement;

/// <summary>
/// One static class nested in a type that holds the members of its queries under
/// <see cref="MemberLocation.NestedClass" />.  The type's file and its methods' file each write a partial declaration
/// of it, with this accessibility.
/// </summary>
/// <param name="Name">The class's name.</param>
/// <param name="Visibility">
/// The class's accessibility: the first that one of its queries writes, else <see cref="Visibility.Private" />.
/// </param>
internal readonly record struct NestedClassPlacement(string Name, Visibility Visibility);
