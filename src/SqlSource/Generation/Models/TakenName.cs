namespace SqlSource.Generation.Models;

/// <summary>A model's full name that the project already has.</summary>
/// <param name="FullName">The model's full name.</param>
/// <param name="Reason">What has it.</param>
/// <param name="Part">
/// The name the project has: for <see cref="TakenReason.NamespacePartIsType" /> the part of the model's namespace that
/// is a type, and otherwise the full name itself.
/// </param>
internal sealed record TakenName(string FullName, TakenReason Reason, string Part);
