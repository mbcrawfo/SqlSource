namespace SqlSource.Tool.Describing;

/// <summary>A setting of a session that changes a description, <c>search_path</c> for one.</summary>
internal sealed record ServerSetting(string Name, string Value);
