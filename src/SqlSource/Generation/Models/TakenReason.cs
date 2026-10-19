namespace SqlSource.Generation.Models;

/// <summary>Why the project already has a model's full name, so that the model cannot be generated.</summary>
internal enum TakenReason
{
    /// <summary>A type of the project has the full name.</summary>
    Type,

    /// <summary>A namespace of the project has the full name.</summary>
    Namespace,

    /// <summary>A part of the model's namespace, from its start, is the full name of a type of the project.</summary>
    NamespacePartIsType,
}
