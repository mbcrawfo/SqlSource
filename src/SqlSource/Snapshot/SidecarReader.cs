using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Snapshot;

/// <summary>
/// Reads a sidecar, by the reader rules of the sidecar format design.  It never throws, whatever the text: a file a
/// user can commit must not cost every type its generated code.
/// </summary>
/// <remarks>
/// Two passes.  The first checks that the text is one JSON object nested at most
/// <see cref="SidecarTokenizer.MaxDepth" /> levels.  The second reads <c>formatVersion</c> before anything else: a
/// reader of this format cannot say whether a file of another is well formed, so such a file is read as its version
/// alone.  Then it reads the model.  An unknown key is skipped with its value at every level, a key the format marks
/// "always" that is absent is read as null, and the first error ends the read.  A file the tool wrote is tokenized
/// twice and no more: keep it so.
/// </remarks>
internal static class SidecarReader
{
    private static readonly string[] VersionKeys = [SidecarKeys.FormatVersion];

    private static readonly string[] TopKeys =
    [
        SidecarKeys.FormatVersion,
        SidecarKeys.ToolVersion,
        SidecarKeys.Queries,
    ];

    private static readonly string[] EntryKeys =
    [
        SidecarKeys.Hash,
        SidecarKeys.Engine,
        SidecarKeys.Database,
        SidecarKeys.ServerVersion,
        SidecarKeys.ResultKind,
        SidecarKeys.MatchesTable,
        SidecarKeys.Plan,
        SidecarKeys.TableMatch,
        SidecarKeys.Parameters,
        SidecarKeys.Columns,
    ];

    private static readonly string[] TableKeys = [SidecarKeys.Schema, SidecarKeys.Table];

    private static readonly string[] ParameterKeys =
    [
        SidecarKeys.Name,
        SidecarKeys.Ordinal,
        SidecarKeys.Type,
        SidecarKeys.Nullable,
        SidecarKeys.TypeSource,
    ];

    private static readonly string[] ColumnKeys =
    [
        SidecarKeys.Ordinal,
        SidecarKeys.Name,
        SidecarKeys.Type,
        SidecarKeys.Nullable,
        SidecarKeys.NullableSource,
        SidecarKeys.Origin,
        SidecarKeys.Identity,
        SidecarKeys.Computed,
    ];

    private static readonly string[] OriginKeys = [SidecarKeys.Schema, SidecarKeys.Table, SidecarKeys.Column];

    /// <summary>
    /// Reads <paramref name="text" />, the text of a <c>.sql.json</c> file without a byte order mark.
    /// </summary>
    public static SidecarReadResult Read(string text)
    {
        if (CheckGrammar(text) is { } invalid)
        {
            return SidecarReadResult.Malformed(invalid);
        }

        var cursor = new SidecarCursor(text);
        var version = ReadVersion(cursor);
        if (cursor.Error is { } versionError)
        {
            return SidecarReadResult.Malformed(versionError);
        }

        if (version != SidecarFormat.Version)
        {
            return SidecarReadResult.OfAnotherVersion(version);
        }

        cursor.Position = 0;
        return ReadSidecar(cursor);
    }

    private static SidecarError? CheckGrammar(string text)
    {
        var tokens = new SidecarTokenizer(text);
        var first = tokens.Next();
        if (first.Kind != SidecarTokenKind.ObjectStart)
        {
            return Invalid(first.Span);
        }

        if (!tokens.TrySkipValue(first, 0, out var problem))
        {
            return Invalid(problem);
        }

        var after = tokens.Next();
        return after.Kind == SidecarTokenKind.End ? null : Invalid(after.Span);
    }

    private static SidecarError Invalid(TextSpan span) => new(SidecarErrorKind.InvalidJson, span, null);

    // Reads to the first formatVersion.  When it is this reader's, the read stops there: the tool writes the key
    // third, so nothing of the queries has been passed over, and ReadSidecar starts again from the first brace and
    // finds a second formatVersion itself.  When it is another's, the read goes on to the end of the object, since
    // a second formatVersion is a mistake in any version and nothing else of such a file is judged.
    private static int ReadVersion(SidecarCursor cursor)
    {
        var brace = cursor.Next().Span;
        var seen = 0;
        var version = 0;
        while (cursor.NextKey(VersionKeys, ref seen) is not null)
        {
            version = cursor.ReadInt32(SidecarKeys.FormatVersion, orNull: false) ?? 0;
            if (version == SidecarFormat.Version)
            {
                return version;
            }
        }

        cursor.Require(VersionKeys, seen, brace, SidecarKeys.FormatVersion);
        return version;
    }

    private static SidecarReadResult ReadSidecar(SidecarCursor cursor)
    {
        var brace = cursor.Next().Span;
        var seen = 0;
        string? toolVersion = null;
        var queries = EquatableArray<SidecarEntry>.Empty;
        for (var key = cursor.NextKey(TopKeys, ref seen); key is not null; key = cursor.NextKey(TopKeys, ref seen))
        {
            switch (key)
            {
                case SidecarKeys.FormatVersion:
                    // ReadVersion read it.  It is a key here so that a second one is a duplicate.
                    _ = cursor.Skip(cursor.Next());
                    break;
                case SidecarKeys.ToolVersion:
                    toolVersion = cursor.ReadString(key, orNull: false);
                    break;
                default:
                    queries = ReadQueries(cursor);
                    break;
            }
        }

        cursor.Require(TopKeys, seen, brace, SidecarKeys.ToolVersion);
        cursor.Require(TopKeys, seen, brace, SidecarKeys.Queries);
        return cursor.Error is { } error
            ? SidecarReadResult.Malformed(error)
            : SidecarReadResult.Of(new Sidecar(SidecarFormat.Version, toolVersion ?? string.Empty, queries));
    }

    private static EquatableArray<SidecarEntry> ReadQueries(SidecarCursor cursor)
    {
        if (!cursor.BeginObject(SidecarKeys.Queries, orNull: false, out _))
        {
            return EquatableArray<SidecarEntry>.Empty;
        }

        var entries = ImmutableArray.CreateBuilder<SidecarEntry>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        while (cursor.NextMember(out var key))
        {
            var name = cursor.GetString(key);
            if (!names.Add(name))
            {
                cursor.Fail(SidecarErrorKind.DuplicateKey, key.Span, name);
            }
            else if (ReadEntry(cursor, name, key.Span) is { } entry)
            {
                entries.Add(entry);
            }
        }

        return new EquatableArray<SidecarEntry>(entries.ToImmutable());
    }

    // The types of an entry's parameters and columns have the shape its engine picks.  The tool writes the engine
    // before them, and then they are read where they stand.  Key order is not significant to a reader, though: an
    // array that comes before the engine is passed over, and read once the object has ended.
    private static SidecarEntry? ReadEntry(SidecarCursor cursor, string name, TextSpan nameSpan)
    {
        if (!cursor.BeginObject(name, orNull: false, out var brace))
        {
            return null;
        }

        var seen = 0;
        string? hash = null;
        string? engine = null;
        string? database = null;
        string? serverVersion = null;
        SidecarResultKind? resultKind = null;
        SidecarTable? matchesTable = null;
        string? plan = null;
        string? tableMatch = null;
        var parameters = EquatableArray<SidecarParameter>.Empty;
        EquatableArray<SidecarColumn>? columns = null;
        var parametersAt = -1;
        var columnsAt = -1;
        var columnsKey = default(TextSpan);
        for (var key = cursor.NextKey(EntryKeys, ref seen); key is not null; key = cursor.NextKey(EntryKeys, ref seen))
        {
            switch (key)
            {
                case SidecarKeys.Hash:
                    hash = cursor.ReadString(key, orNull: false);
                    break;
                case SidecarKeys.Engine:
                    engine = cursor.ReadString(key, orNull: false);
                    break;
                case SidecarKeys.Database:
                    database = cursor.ReadString(key, orNull: true);
                    break;
                case SidecarKeys.ServerVersion:
                    serverVersion = cursor.ReadString(key, orNull: true);
                    break;
                case SidecarKeys.ResultKind:
                    resultKind = ReadResultKind(cursor);
                    break;
                case SidecarKeys.MatchesTable:
                    matchesTable = ReadTable(cursor);
                    break;
                case SidecarKeys.Plan:
                    plan = cursor.ReadString(key, orNull: true);
                    break;
                case SidecarKeys.TableMatch:
                    tableMatch = cursor.ReadString(key, orNull: true);
                    break;
                case SidecarKeys.Parameters:
                    parametersAt = ReadParametersOrDefer(cursor, engine, ref parameters);
                    break;
                default:
                    columnsKey = cursor.KeySpan;
                    columnsAt = ReadColumnsOrDefer(cursor, engine, ref columns);
                    break;
            }
        }

        RequireEntryKeys(cursor, seen, brace, resultKind);
        if (resultKind == SidecarResultKind.None && SidecarCursor.Has(EntryKeys, seen, SidecarKeys.Columns))
        {
            cursor.Fail(SidecarErrorKind.ColumnsWithoutRows, columnsKey);
        }

        if (cursor.Error is not null || hash is null || engine is null || resultKind is not { } kind)
        {
            return null;
        }

        ReadDeferred(cursor, engine, parametersAt, columnsAt, ref parameters, ref columns);
        if (cursor.Error is not null)
        {
            return null;
        }

        return new SidecarEntry(
            name,
            nameSpan,
            hash,
            engine,
            database,
            serverVersion,
            kind,
            matchesTable,
            plan,
            tableMatch,
            parameters,
            columns
        );
    }

    // Reads the parameters where they stand when the entry's engine is known.  Otherwise passes over them and
    // returns the offset to read them from; -1 when there is nothing left to read.
    private static int ReadParametersOrDefer(
        SidecarCursor cursor,
        string? engine,
        ref EquatableArray<SidecarParameter> parameters
    )
    {
        if (engine is null)
        {
            return cursor.DeferArray(SidecarKeys.Parameters);
        }

        parameters = ReadParameters(cursor, engine);
        return -1;
    }

    private static int ReadColumnsOrDefer(
        SidecarCursor cursor,
        string? engine,
        ref EquatableArray<SidecarColumn>? columns
    )
    {
        if (engine is null)
        {
            return cursor.DeferArray(SidecarKeys.Columns);
        }

        columns = ReadColumns(cursor, engine);
        return -1;
    }

    // Reads the arrays that were passed over, and comes back to the end of the entry.
    private static void ReadDeferred(
        SidecarCursor cursor,
        string engine,
        int parametersAt,
        int columnsAt,
        ref EquatableArray<SidecarParameter> parameters,
        ref EquatableArray<SidecarColumn>? columns
    )
    {
        var end = cursor.Position;
        if (parametersAt >= 0)
        {
            cursor.Position = parametersAt;
            parameters = ReadParameters(cursor, engine);
        }

        if (columnsAt >= 0)
        {
            cursor.Position = columnsAt;
            columns = ReadColumns(cursor, engine);
        }

        cursor.Position = end;
    }

    private static void RequireEntryKeys(SidecarCursor cursor, int seen, TextSpan brace, SidecarResultKind? resultKind)
    {
        cursor.Require(EntryKeys, seen, brace, SidecarKeys.Hash);
        cursor.Require(EntryKeys, seen, brace, SidecarKeys.Engine);
        cursor.Require(EntryKeys, seen, brace, SidecarKeys.ResultKind);
        cursor.Require(EntryKeys, seen, brace, SidecarKeys.Parameters);
        if (resultKind == SidecarResultKind.Rows)
        {
            cursor.Require(EntryKeys, seen, brace, SidecarKeys.Columns);
        }
    }

    private static SidecarResultKind? ReadResultKind(SidecarCursor cursor)
    {
        var token = cursor.Next();
        if (token.Kind != SidecarTokenKind.String)
        {
            cursor.WrongType(token, SidecarKeys.ResultKind);
            return null;
        }

        if (SidecarTokenizer.StringEquals(cursor.Text, token, SidecarValues.ResultKind.Rows))
        {
            return SidecarResultKind.Rows;
        }

        if (SidecarTokenizer.StringEquals(cursor.Text, token, SidecarValues.ResultKind.None))
        {
            return SidecarResultKind.None;
        }

        cursor.Fail(SidecarErrorKind.UnknownResultKind, token.Span, cursor.GetString(token));
        return null;
    }

    private static SidecarTable? ReadTable(SidecarCursor cursor)
    {
        if (!cursor.BeginObject(SidecarKeys.MatchesTable, orNull: true, out _))
        {
            return null;
        }

        var seen = 0;
        string? schema = null;
        string? table = null;
        for (var key = cursor.NextKey(TableKeys, ref seen); key is not null; key = cursor.NextKey(TableKeys, ref seen))
        {
            if (key == SidecarKeys.Schema)
            {
                schema = cursor.ReadString(key, orNull: true);
            }
            else
            {
                table = cursor.ReadString(key, orNull: true);
            }
        }

        return new SidecarTable(schema, table);
    }

    private static EquatableArray<SidecarParameter> ReadParameters(SidecarCursor cursor, string engine)
    {
        if (!cursor.BeginArray(SidecarKeys.Parameters, orNull: false))
        {
            return EquatableArray<SidecarParameter>.Empty;
        }

        var parameters = ImmutableArray.CreateBuilder<SidecarParameter>();
        while (cursor.NextElement(out var first))
        {
            if (ReadParameter(cursor, first, engine, parameters.Count) is { } parameter)
            {
                parameters.Add(parameter);
            }
        }

        return new EquatableArray<SidecarParameter>(parameters.ToImmutable());
    }

    private static SidecarParameter? ReadParameter(SidecarCursor cursor, SidecarToken first, string engine, int index)
    {
        if (first.Kind != SidecarTokenKind.ObjectStart)
        {
            cursor.WrongType(first, SidecarKeys.Parameters);
            return null;
        }

        var seen = 0;
        string? name = null;
        SidecarType? type = null;
        bool? nullable = null;
        string? typeSource = null;
        for (
            var key = cursor.NextKey(ParameterKeys, ref seen);
            key is not null;
            key = cursor.NextKey(ParameterKeys, ref seen)
        )
        {
            switch (key)
            {
                case SidecarKeys.Name:
                    name = cursor.ReadString(key, orNull: false);
                    break;
                case SidecarKeys.Ordinal:
                    ReadOrdinal(cursor, index);
                    break;
                case SidecarKeys.Type:
                    type = SidecarTypeReader.Read(cursor, engine, orNull: true);
                    break;
                case SidecarKeys.Nullable:
                    nullable = cursor.ReadBoolean(key);
                    break;
                default:
                    typeSource = cursor.ReadString(key, orNull: true);
                    break;
            }
        }

        cursor.Require(ParameterKeys, seen, first.Span, SidecarKeys.Name);
        cursor.Require(ParameterKeys, seen, first.Span, SidecarKeys.Ordinal);
        cursor.Require(ParameterKeys, seen, first.Span, SidecarKeys.Nullable);
        return cursor.Error is null && name is not null
            ? new SidecarParameter(name, index, type, nullable, typeSource)
            : null;
    }

    private static EquatableArray<SidecarColumn> ReadColumns(SidecarCursor cursor, string engine)
    {
        if (!cursor.BeginArray(SidecarKeys.Columns, orNull: false))
        {
            return EquatableArray<SidecarColumn>.Empty;
        }

        var columns = ImmutableArray.CreateBuilder<SidecarColumn>();
        while (cursor.NextElement(out var first))
        {
            if (ReadColumn(cursor, first, engine, columns.Count) is { } column)
            {
                columns.Add(column);
            }
        }

        return new EquatableArray<SidecarColumn>(columns.ToImmutable());
    }

    private static SidecarColumn? ReadColumn(SidecarCursor cursor, SidecarToken first, string engine, int index)
    {
        if (first.Kind != SidecarTokenKind.ObjectStart)
        {
            cursor.WrongType(first, SidecarKeys.Columns);
            return null;
        }

        var seen = 0;
        string? name = null;
        SidecarType? type = null;
        bool? nullable = null;
        string? nullableSource = null;
        SidecarOrigin? origin = null;
        bool? identity = null;
        bool? computed = null;
        for (
            var key = cursor.NextKey(ColumnKeys, ref seen);
            key is not null;
            key = cursor.NextKey(ColumnKeys, ref seen)
        )
        {
            switch (key)
            {
                case SidecarKeys.Ordinal:
                    ReadOrdinal(cursor, index);
                    break;
                case SidecarKeys.Name:
                    name = cursor.ReadString(key, orNull: false);
                    break;
                case SidecarKeys.Type:
                    type = SidecarTypeReader.Read(cursor, engine, orNull: false);
                    break;
                case SidecarKeys.Nullable:
                    nullable = cursor.ReadBoolean(key);
                    break;
                case SidecarKeys.NullableSource:
                    nullableSource = cursor.ReadString(key, orNull: true);
                    break;
                case SidecarKeys.Origin:
                    origin = ReadOrigin(cursor);
                    break;
                case SidecarKeys.Identity:
                    identity = cursor.ReadBoolean(key);
                    break;
                default:
                    computed = cursor.ReadBoolean(key);
                    break;
            }
        }

        cursor.Require(ColumnKeys, seen, first.Span, SidecarKeys.Ordinal);
        cursor.Require(ColumnKeys, seen, first.Span, SidecarKeys.Name);
        cursor.Require(ColumnKeys, seen, first.Span, SidecarKeys.Type);
        cursor.Require(ColumnKeys, seen, first.Span, SidecarKeys.Nullable);
        return cursor.Error is null && name is not null && type is not null
            ? new SidecarColumn(index, name, type, nullable, nullableSource, origin, identity, computed)
            : null;
    }

    // An ordinal is kept in the file so that a reordered list and a hand edit show.  It must be its index, so the
    // model takes the index.
    private static void ReadOrdinal(SidecarCursor cursor, int index)
    {
        var token = cursor.Next();
        if (!SidecarTokenizer.TryGetInt32(cursor.Text, token, out var ordinal))
        {
            cursor.WrongType(token, SidecarKeys.Ordinal);
        }
        else if (ordinal != index)
        {
            cursor.Fail(
                SidecarErrorKind.OrdinalMismatch,
                token.Span,
                cursor.Text.Substring(token.Span.Start, token.Span.Length)
            );
        }
    }

    private static SidecarOrigin? ReadOrigin(SidecarCursor cursor)
    {
        if (!cursor.BeginObject(SidecarKeys.Origin, orNull: true, out _))
        {
            return null;
        }

        var seen = 0;
        string? schema = null;
        string? table = null;
        string? column = null;
        for (
            var key = cursor.NextKey(OriginKeys, ref seen);
            key is not null;
            key = cursor.NextKey(OriginKeys, ref seen)
        )
        {
            switch (key)
            {
                case SidecarKeys.Schema:
                    schema = cursor.ReadString(key, orNull: true);
                    break;
                case SidecarKeys.Table:
                    table = cursor.ReadString(key, orNull: true);
                    break;
                default:
                    column = cursor.ReadString(key, orNull: true);
                    break;
            }
        }

        return new SidecarOrigin(schema, table, column);
    }
}
