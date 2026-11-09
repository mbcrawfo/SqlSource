namespace SqlSource.Generation.Repositories;

/// <summary>
/// What a repository's connection and interface are resolved from: the candidates for its connection and the settings
/// and name of its interface, read from the compilation as data.
/// </summary>
/// <param name="TypeKey">
/// The type's <c>HintName.NameOf</c>, the key that a type's models and methods are joined on.
/// </param>
/// <param name="Candidates">
/// The members that can give the connection, in the order they are declared, the type's own first and then each base
/// type's: only the marked ones when any is, and the primary constructor's parameters only when no field or property
/// is one.  Empty for a static type.
/// </param>
/// <param name="InterfaceOn">
/// Whether the type gets its interface: the attribute's <c>GenerateInterface</c> when it is written, and the project's
/// <c>SqlSourceRepositoryInterfaces</c> otherwise.
/// </param>
/// <param name="InterfaceName">The attribute's <c>InterfaceName</c>, or the type's name after an <c>I</c>.</param>
/// <param name="InterfaceTakenBy">
/// What the project already declares with the interface's full name, such as <c>the class App.IUserRepository</c>, or
/// null when it declares nothing there, or only a partial interface, or the type gets no interface.
/// </param>
internal sealed record RepositoryShape(
    string TypeKey,
    EquatableArray<ConnectionCandidate> Candidates,
    bool InterfaceOn,
    string InterfaceName,
    string? InterfaceTakenBy
);
