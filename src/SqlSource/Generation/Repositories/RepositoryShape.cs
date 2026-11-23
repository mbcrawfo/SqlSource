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
/// is one, and then only those that no argument of the base type and no initializer of a field, property or event of
/// the type reads.  Empty for a static type.
/// </param>
/// <param name="HeldCandidates">
/// The marked members of the types of the fields, properties and parameters that the repository holds, which only a
/// database that <paramref name="Candidates" /> neither give a candidate nor fail uses.
/// </param>
/// <param name="GlobalCandidates">
/// The marked static members of the types of the project, which are the last resort of a database.
/// </param>
/// <param name="HeldParameters">
/// The names of the primary constructor's parameters that the conventions would take, but that an argument of the base
/// type or an initializer reads, in the order they are declared, which <c>SQLSRC028</c> names.  Empty unless
/// <paramref name="Candidates" /> are the parameters.
/// </param>
/// <param name="InterfaceOn">
/// Whether the type gets its interface: the attribute's <c>GenerateInterface</c> when it is written, and the project's
/// <c>SqlSourceRepositoryInterfaces</c> otherwise.
/// </param>
/// <param name="InterfaceName">The attribute's <c>InterfaceName</c>, or the type's name after an <c>I</c>.</param>
/// <param name="InterfaceTakenBy">
/// What already takes the interface's full name, such as <c>the class App.IUserRepository</c>: a type of the project,
/// unless it is a partial interface, a type of a referenced assembly that the project sees, or a member of the
/// containing type.  Null when nothing does, or the type gets no interface.
/// </param>
/// <param name="SimpleNameTakenBy">
/// What the type's simple name means inside it when that is not the type, an inherited member or a primary-constructor
/// parameter, such as <c>the property App.Base.Sample</c>, for a type nested in a generic type, whose methods reach a
/// static member through that name.  Null when the name reaches the type, or the type is not nested in a generic type.
/// </param>
internal sealed record RepositoryShape(
    string TypeKey,
    EquatableArray<ConnectionCandidate> Candidates,
    EquatableArray<ConnectionCandidate> HeldCandidates,
    EquatableArray<ConnectionCandidate> GlobalCandidates,
    EquatableArray<string> HeldParameters,
    bool InterfaceOn,
    string InterfaceName,
    string? InterfaceTakenBy,
    string? SimpleNameTakenBy = null
);
