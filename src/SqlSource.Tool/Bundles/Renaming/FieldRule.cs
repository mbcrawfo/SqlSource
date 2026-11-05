namespace SqlSource.Tool.Bundles.Renaming;

/// <summary>What the renaming does with a string member of a call or of the entry.</summary>
internal enum FieldRuleKind
{
    /// <summary>Removed from the bundle: no decision of the pipeline reads it.</summary>
    Drop,

    /// <summary>A text of the tool or of the engine, such as a statement's name or a version: kept and not checked.</summary>
    Fixed,

    /// <summary>A code of a closed set, such as <c>relkind</c>: kept and not checked.</summary>
    Closed,

    /// <summary>SQL, renamed lexeme by lexeme; a new word is of the rule's class, or a function before <c>(</c>.</summary>
    Sql,

    /// <summary>A type's spelling, lexed as SQL whose new words are types.</summary>
    TypeSpelling,

    /// <summary>An enum's label, renamed as the literal of the same spelling.</summary>
    Label,

    /// <summary>A parameter's name, renamed as the query's parameter or a copy of one.</summary>
    Parameter,

    /// <summary>One name of the rule's class, as a catalog holds it.</summary>
    Name,

    /// <summary>
    /// One name of the rule's class for the whole string, such as the assembly-qualified name of a user's CLR type;
    /// kept when the engine says it is its word.
    /// </summary>
    WholeName,

    /// <summary>A string only the engine's part reads, renamed by <see cref="IEngineRenaming.RenameOwn" />.</summary>
    Own,
}

/// <summary>The rule of one member: its kind, and the class of the names it makes, for the kinds that make any.</summary>
/// <param name="Kind">What is done with the member.</param>
/// <param name="Class">
/// The class of a name the member holds: of the new words of <see cref="FieldRuleKind.Sql" /> and
/// <see cref="FieldRuleKind.TypeSpelling" />, of the one name of <see cref="FieldRuleKind.Name" /> and
/// <see cref="FieldRuleKind.WholeName" />.  Null for the other kinds.
/// </param>
internal readonly record struct FieldRule(FieldRuleKind Kind, NameClass? Class = null)
{
    /// <summary>Removed from the bundle.</summary>
    public static FieldRule Drop => new(FieldRuleKind.Drop);

    /// <summary>A text of the tool or the engine, kept.</summary>
    public static FieldRule Fixed => new(FieldRuleKind.Fixed);

    /// <summary>A closed code, kept.</summary>
    public static FieldRule Closed => new(FieldRuleKind.Closed);

    /// <summary>SQL whose new words are aliases.</summary>
    public static FieldRule Sql => new(FieldRuleKind.Sql, NameClass.Alias);

    /// <summary>A type's spelling, whose new words are types.</summary>
    public static FieldRule TypeSpelling => new(FieldRuleKind.TypeSpelling, NameClass.Type);

    /// <summary>An enum's label.</summary>
    public static FieldRule Label => new(FieldRuleKind.Label);

    /// <summary>A parameter's name.</summary>
    public static FieldRule Parameter => new(FieldRuleKind.Parameter);

    /// <summary>A string of the engine's part.</summary>
    public static FieldRule Own => new(FieldRuleKind.Own);

    /// <summary>One name of the class.</summary>
    public static FieldRule Name(NameClass cls) => new(FieldRuleKind.Name, cls);

    /// <summary>One name of the class for the whole string.</summary>
    public static FieldRule WholeName(NameClass cls) => new(FieldRuleKind.WholeName, cls);
}
