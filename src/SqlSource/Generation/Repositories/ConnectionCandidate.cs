using SqlSource.Diagnostics;

namespace SqlSource.Generation.Repositories;

/// <summary>
/// A member of a repository, or a parameter of its primary constructor, that can give its methods a connection: one
/// that is marked <c>[SqlSourceConnection]</c>, or one that the conventions accept when none is.
/// </summary>
/// <param name="Name">The member's name, as a value: without the <c>@</c> of an escaped keyword.</param>
/// <param name="Member">What kind of member it is.</param>
/// <param name="Type">What it gives.</param>
/// <param name="IsAsync">
/// Whether it is a method that returns a <c>Task</c> or a <c>ValueTask</c> of a connection.
/// </param>
/// <param name="TakesCancellationToken">Whether it is a method that takes a <c>CancellationToken</c>.</param>
/// <param name="IsStatic">Whether it is a static member.</param>
/// <param name="IsInherited">Whether it is a member of a base type.</param>
/// <param name="IsMarked">Whether it is marked <c>[SqlSourceConnection]</c>.</param>
/// <param name="Database">
/// The attribute's <c>Database</c>, or null when it is not set or is empty, or the member is not marked.
/// </param>
/// <param name="DeclaredTypeName">
/// The metadata name, with its namespace, of the member's type, of a method's return type, or of the connection type
/// of a factory's task.
/// </param>
/// <param name="Location">
/// Where the member is declared, or where the type's attribute is for a member of a base type in metadata.
/// </param>
/// <param name="Tier">
/// Where the candidate is found: in the repository, in a holder it holds, or anywhere in the project.
/// </param>
/// <param name="Holder">
/// The member of the repository that holds the candidate's object, for a held candidate; else null.
/// </param>
/// <param name="GlobalTypeText">
/// The fully qualified type that declares a global candidate, such as <c>global::App.Outer.Db</c>; else null.
/// </param>
internal sealed record ConnectionCandidate(
    string Name,
    CandidateMember Member,
    CandidateType Type,
    bool IsAsync,
    bool TakesCancellationToken,
    bool IsStatic,
    bool IsInherited,
    bool IsMarked,
    string? Database,
    string DeclaredTypeName,
    LocationInfo Location,
    CandidateTier Tier = CandidateTier.Own,
    CandidateHolder? Holder = null,
    string? GlobalTypeText = null
)
{
    /// <summary>
    /// The name that a message gives the candidate: its own for an own one, the holder's then a dot then its own for a
    /// held one, and the declaring type's simple name, without the <c>@</c> of an escaped keyword, then a dot then its
    /// own for a global one.
    /// </summary>
    public string DisplayName =>
        Tier switch
        {
            CandidateTier.Own => Name,
            CandidateTier.Held when Holder is not null => Holder.Name + "." + Name,
            CandidateTier.Global when GlobalTypeText is not null => SimpleNameOf(GlobalTypeText) + "." + Name,
            _ => Name,
        };

    // The last name of the text, as a value: without the @ of an escaped keyword, which the text keeps for access.
    private static string SimpleNameOf(string typeText)
    {
        var start = typeText.LastIndexOf('.') + 1;
        if (start < typeText.Length && typeText[start] == '@')
        {
            start++;
        }

        return typeText.Substring(start);
    }
}
