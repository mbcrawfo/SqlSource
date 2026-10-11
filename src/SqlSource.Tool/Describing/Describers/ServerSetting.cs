namespace SqlSource.Tool.Describing.Describers;

/// <summary>A setting of a session that changes a description, <c>search_path</c> for one.</summary>
internal sealed record ServerSetting(string Name, string Value);
