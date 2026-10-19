using SqlSource.Settings;

namespace SqlSource.Generation.Models;

/// <summary>Everything one model's file is written from.</summary>
/// <param name="HintName">The name of the model's file, unique ignoring case among the models'.</param>
/// <param name="NamespaceText">The model's namespace as code writes it.  Empty for the global namespace.</param>
/// <param name="Name">The model's name.</param>
/// <param name="Role">Whether the model is of a query's parameters or of a row.</param>
/// <param name="Kind">The model's shape.</param>
/// <param name="IsPublic">Whether the model is public; otherwise it is internal.</param>
/// <param name="Properties">The model's properties, in order.</param>
/// <param name="SummaryXml">The model's summary, as escaped XML, its lines separated by line feeds.</param>
internal sealed record Model(
    string HintName,
    string NamespaceText,
    string Name,
    ModelRole Role,
    ModelKind Kind,
    bool IsPublic,
    EquatableArray<ModelProperty> Properties,
    string SummaryXml
);
