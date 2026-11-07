namespace SqlSource.Tool.Bundles.Renaming;

/// <summary>
/// What the renaming of one bundle gives an engine's part while it renames: the bundle's one mapping, its SQL
/// renamer, and the place of the member being renamed, which a refusal names.
/// </summary>
internal interface IRenamingScope
{
    /// <summary>
    /// The replacement of a name, made in the class given if the spelling has none yet; a spelling kept, or one that
    /// <see cref="IEngineRenaming.IsEngineWord" /> keeps, is its own.  One spelling, as the engine's
    /// <see cref="IEngineRenaming.Spelled" /> gives it, has one replacement throughout the bundle, and two spellings
    /// that <see cref="IEngineRenaming.Fold" /> takes for one name have replacements that differ by case alone.
    /// </summary>
    string Name(string spelling, bool quoted, NameClass cls);

    /// <summary>
    /// Keeps a spelling verbatim from here on: a name of the engine's, which then maps to itself.  The token check
    /// does not trust a spelling kept here: it passes only where <see cref="IEngineRenaming.IsEngineWord" /> keeps
    /// it, unlike a name the renaming kept from a row that <see cref="IEngineRenaming.IsEngineRow" /> said is the
    /// engine's.
    /// </summary>
    void Keep(string spelling, bool quoted);

    /// <summary>Whether a spelling already has a replacement or is kept.</summary>
    bool IsMapped(string spelling, bool quoted);

    /// <summary>The replacement of a string literal's content, the same for the same content.</summary>
    string Literal(string content);

    /// <summary>
    /// SQL renamed lexeme by lexeme, new words of the class given; a refusal is made at the member being renamed.
    /// </summary>
    RenamedText Sql(string sql, NameClass newWords);

    /// <summary>A parameter's name renamed: a query's parameter, or a copy of one through its parameter.</summary>
    string Parameter(string name);

    /// <summary>
    /// The query's parameter of a copy that the pipeline made, as <see cref="IEngineRenaming.CopiesIn" /> read it from
    /// the raw capture, or the name itself when it is no such copy.
    /// </summary>
    string ParameterOf(string name);

    /// <summary>Refuses the bundle; the renaming writes nothing.</summary>
    void Refuse(RenamingRefusal refusal);
}
