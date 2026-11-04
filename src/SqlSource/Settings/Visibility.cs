namespace SqlSource.Settings;

/// <summary>
/// The accessibility of a query's generated members, as the <c>visibility</c> of a marker or a settings file names it.
/// What it applies to depends on the <see cref="MemberLocation" />.
/// </summary>
internal enum Visibility
{
    /// <summary><c>public</c>.</summary>
    Public = 0,

    /// <summary><c>internal</c>.</summary>
    Internal = 1,

    /// <summary><c>protected</c>.</summary>
    Protected = 2,

    /// <summary><c>private</c>.</summary>
    Private = 3,
}
