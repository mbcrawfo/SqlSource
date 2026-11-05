namespace SqlSource.Tool.Bundles.Renaming;

/// <summary>What a renamed name is: the class that prefixes its replacement and that the mapping file records.</summary>
internal enum NameClass
{
    Schema,
    Table,
    Column,
    Alias,
    Function,
    Type,
    Index,
    Parameter,
    Token,
    Query,
    Database,
    Literal,
}

/// <summary>The prefix and the word of each <see cref="NameClass" />.</summary>
internal static class NameClasses
{
    /// <summary>The prefix of a replacement of the class: <c>t_</c> for a table.</summary>
    public static string PrefixOf(NameClass value) =>
        value switch
        {
            NameClass.Schema => "s_",
            NameClass.Table => "t_",
            NameClass.Column => "c_",
            NameClass.Alias => "a_",
            NameClass.Function => "f_",
            NameClass.Type => "y_",
            NameClass.Index => "i_",
            NameClass.Parameter => "p_",
            NameClass.Token => "k_",
            NameClass.Query => "q_",
            NameClass.Database => "d_",
            NameClass.Literal => "l_",
            _ => throw UnexpectedEnumValueException.Of(value),
        };

    /// <summary>The word of the class: what the mapping file calls it and what the hash is keyed by.</summary>
    public static string WordOf(NameClass value) =>
        value switch
        {
            NameClass.Schema => "schema",
            NameClass.Table => "table",
            NameClass.Column => "column",
            NameClass.Alias => "alias",
            NameClass.Function => "function",
            NameClass.Type => "type",
            NameClass.Index => "index",
            NameClass.Parameter => "parameter",
            NameClass.Token => "token",
            NameClass.Query => "query",
            NameClass.Database => "database",
            NameClass.Literal => "literal",
            _ => throw UnexpectedEnumValueException.Of(value),
        };
}
