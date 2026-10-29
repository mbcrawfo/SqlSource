namespace SqlSource.Generation.Types.MappedTypes;

/// <summary>
/// A short name in a mapped type that found a type of an assembly that only a NuGet package the project does not list
/// itself brings, one of <see cref="TransitivePackageAssemblies" />.  The package can stop bringing the assembly, or
/// bring another type of the name, when it is updated, so the type may not be the one the name means.
/// </summary>
/// <param name="ShortName">
/// The short name as written, without its arguments: <c>AccountId</c>, or <c>Range</c> of <c>Range&lt;int&gt;</c>.
/// </param>
/// <param name="FullName">
/// The full name of the type it found as a user writes it, <c>Microsoft.Identity.Client.AccountId</c>, and
/// <c>global::AccountId</c> for one of the global namespace.
/// </param>
/// <param name="Assembly">The name of the type's assembly, <c>Microsoft.Identity.Client</c>.</param>
internal sealed record PackageBinding(string ShortName, string FullName, string Assembly);
