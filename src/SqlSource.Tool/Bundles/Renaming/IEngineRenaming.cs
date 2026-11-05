using System.Collections.Generic;
using System.Text.Json.Nodes;
using SqlSource.Parsing;

namespace SqlSource.Tool.Bundles.Renaming;

/// <summary>
/// The engine's part of renaming a bundle: what its fields are, which words and rows are its own, and what its
/// pipeline derives.  The steps that are the same for every engine, the mapping, the SQL's lexemes, the entry, the
/// token check and the replay, are the renaming's, which asks this part and never names an engine.
/// </summary>
/// <remarks>
/// Each engine builds its part against this contract and does not change it: PostgreSQL's in 10.2 and SQL Server's in
/// 10.3.  A member never reads a database, the host or a file, holds nothing of one bundle once that bundle is renamed,
/// and never writes a name of its own making: every replacement comes from the scope's mapping.
/// </remarks>
internal interface IEngineRenaming
{
    /// <summary>
    /// The dialect whose <see cref="SqlDialectRules" /> the generator's lexer reads the engine's SQL by: its quoted
    /// regions, comments, hints and parameters.
    /// </summary>
    SqlDialect Dialect { get; }

    /// <summary>
    /// Whether a character starts a word in a <see cref="SqlLexemeKind.Text" /> lexeme, by the engine's own rule.
    /// An ASCII digit is read as the start of a number before this is asked, so its answer for one is never read.
    /// </summary>
    bool IsWordStart(char character);

    /// <summary>Whether a character goes on with a word that <see cref="IsWordStart" /> started.</summary>
    bool IsWordPart(char character);

    /// <summary>
    /// A spelling as the engine compares it, by which the mapping finds one name: PostgreSQL lower-cases an unquoted
    /// one and keeps a quoted one; SQL Server ignores case.  The same spelling and quoting always fold to the same
    /// text, and two spellings the engine takes for one name fold alike.
    /// </summary>
    string Fold(string spelling, bool quoted);

    /// <summary>
    /// Whether a spelling is kept verbatim: a keyword or word of the engine's vocabulary, the engine's own schema or
    /// database, or a name the planner or the tool makes, such as the <c>1</c> of SQL Server's <c>AS [1]</c>.  The
    /// spelling is folded by <see cref="Fold" /> before it is compared, so <c>EXCLUDED</c> is <c>excluded</c>.  A
    /// name of the user's spelled as such a word is kept too.  It never answers by what the mapping holds.
    /// </summary>
    bool IsEngineWord(string spelling, bool quoted);

    /// <summary>
    /// The rule of a string member of a call, by the call's method (<c>open</c>, <c>prepare</c>, ...) and the member's
    /// path from the call (<c>request.sql</c>, <c>answer.rows[].name</c>, <c>answer.error.message</c>), or of a member
    /// of the entry, by the method <see cref="BundleFiles.Expected" /> and its path from the entry's type record
    /// (<c>columns[].type.name</c>).  An element of an array is <c>[]</c> in the path.
    /// </summary>
    /// <returns>
    /// The rule, or null when the member has none: the renaming refuses a string with none, so every member the
    /// pipeline reads has a rule, and every other member has <see cref="FieldRule.Drop" />.
    /// </returns>
    FieldRule? RuleOf(string method, string path);

    /// <summary>
    /// Whether a row of an answer is the engine's own, whose names are kept: a type or relation of
    /// <c>pg_catalog</c> or <c>information_schema</c>, a row of <c>sys</c> or of a system database.  The renaming asks
    /// before it renames the row's names, and keeps every name of a row this says is the engine's.  It decides by the
    /// row alone and changes nothing.
    /// </summary>
    /// <param name="method">The call's method.</param>
    /// <param name="path">The path of the row from the call, such as <c>answer.rows[]</c>.</param>
    /// <param name="row">The row as captured.</param>
    bool IsEngineRow(string method, string path, JsonObject row);

    /// <summary>
    /// A copy of a reused parameter that the pipeline made, such as SQL Server's <c>before_1</c> of <c>before</c>, as
    /// the parameter it copies and the text after it.  Asked for every parameter name that is not one of the query's;
    /// the renaming writes the renamed parameter and the suffix as given.
    /// </summary>
    /// <param name="name">The name, without its prefix.</param>
    /// <param name="queryParameters">The query's parameters, without their prefix.</param>
    /// <returns>
    /// The parameter, which must be one of <paramref name="queryParameters" />, and the suffix, which must hold nothing
    /// of the user's; or null when the name is no copy the engine's pipeline makes.
    /// </returns>
    (string Parameter, string Suffix)? CopyOf(string name, IReadOnlySet<string> queryParameters);

    /// <summary>
    /// Registers the names that only the engine finds, before the query's SQL is renamed and after the catalog fields
    /// of every call: a plan's name keys, through <see cref="IRenamingScope.Name" /> or
    /// <see cref="IRenamingScope.Keep" />.  Called once for each call, in order.  It writes nothing into the call.
    /// </summary>
    void Register(RenamingCall call, IRenamingScope scope);

    /// <summary>
    /// A string of a <see cref="FieldRuleKind.Own" /> member renamed, such as a plan, an error's message or a
    /// setting's value: every name through the scope, every literal through <see cref="IRenamingScope.Literal" />,
    /// and nothing of the user's kept.  A string it cannot read safely is refused through the scope, and its return is
    /// then not used.
    /// </summary>
    string RenameOwn(RenamingField field, IRenamingScope scope);

    /// <summary>
    /// The words of a renamed <see cref="FieldRuleKind.Own" /> member, for the token check: every word, quoted name
    /// and literal of the string, as a plan's string splits through <c>PlanTokens</c>.  Every token it omits goes
    /// unchecked, so it omits only the engine's fixed texts.
    /// </summary>
    /// <param name="field">The member, whose <see cref="RenamingField.Value" /> is the renamed string.</param>
    IEnumerable<string> WordsOf(RenamingField field);

    /// <summary>
    /// Derives, once every string of every call is renamed, what the engine's pipeline makes by its own rule and the
    /// renaming must not rename token by token: a list asked in name order sorted again, a position mapped through
    /// <see cref="RenamedText.MapPosition" />.  It writes into <see cref="RenamingCall.Renamed" /> alone, reading
    /// <see cref="RenamingCall.Original" />, and refuses through the scope what it cannot derive.
    /// </summary>
    void Derive(RenamingCall call, IRenamingScope scope);
}
