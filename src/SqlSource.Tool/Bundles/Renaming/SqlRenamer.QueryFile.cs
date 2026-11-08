using System;
using System.Linq;
using SqlSource.Parsing;
using SqlSource.Parsing.Markers;
using SqlSource.Settings;

namespace SqlSource.Tool.Bundles.Renaming;

internal sealed partial class SqlRenamer
{
    // The markers of a query file: read as the generator reads them, each kept with its value renamed.
    private sealed partial class Pass
    {
        private void LineCommentOfQueryFile(SqlLexeme comment)
        {
            if (SqlMarkerReader.Read(text, comment) is not { } marker)
            {
                _into.ReplaceTo(comment.Span.End, "--");
                return;
            }

            var head = text[comment.Span.Start..marker.ValueSpan.Start];
            var value = text.Substring(marker.ValueSpan.Start, marker.ValueSpan.Length);
            var renamed = marker.Kind switch
            {
                SqlMarkerKind.Name => NameMarker(marker),
                SqlMarkerKind.Dialect => SqlDialectMarker.TryRead(text, marker, out _) ? value : null,
                SqlMarkerKind.Database => DatabaseMarker(value),
                SqlMarkerKind.Param => ParamMarker(marker),
                SqlMarkerKind.Token => TokenMarker(marker),
                SqlMarkerKind.TokenIgnore => TokenIgnoreMarker(marker),
                SqlMarkerKind.Summary
                or SqlMarkerKind.GeneratorParameters
                or SqlMarkerKind.Output
                or SqlMarkerKind.InputModelSuffix
                or SqlMarkerKind.OutputModelSuffix
                or SqlMarkerKind.ModelNamespace
                or SqlMarkerKind.InputModelType
                or SqlMarkerKind.OutputModelType
                or SqlMarkerKind.InputModel
                or SqlMarkerKind.OutputModel
                or SqlMarkerKind.CollectionType
                or SqlMarkerKind.TypeOverride
                or SqlMarkerKind.Column
                or SqlMarkerKind.LibraryTypes
                or SqlMarkerKind.MemberLocation
                or SqlMarkerKind.Visibility
                or SqlMarkerKind.NestedClass => Unheld(marker),
                _ => throw UnexpectedEnumValueException.Of(marker.Kind),
            };

            if (renamed is null)
            {
                // A marker that does not read: its value is the user's text, which the refusal names.
                Refuse(value);
                _into.ReplaceTo(comment.Span.End, "--");
                return;
            }

            _into.ReplaceTo(comment.Span.End, head + renamed + text[marker.ValueSpan.End..comment.Span.End]);
        }

        // A marker that a bundle's query.sql never holds, since BundleQueryFile writes none: refused, named by its
        // word.
        private string Unheld(SqlMarker marker)
        {
            Refuse("-- " + SqlMarkerReader.WordOf(marker.Kind) + ":");
            return string.Empty;
        }

        // "Name" or "Name -> shape", read as the parser reads it: the name a query's, the shape kept.
        private string? NameMarker(SqlMarker marker)
        {
            var value = marker.ValueSpan;
            var (name, arrow) = SqlMarkerReader.SplitName(text, value);
            if (
                name.IsEmpty
                || (
                    arrow >= 0
                    && !SettingValue.TryReadChoice<ResultShape>(text.AsSpan(arrow + 2, value.End - arrow - 2), out _)
                )
            )
            {
                return null;
            }

            var written = text.Substring(name.Start, name.Length);
            return renamer.NameOf(written, quoted: false, NameClass.Query) + text[name.End..value.End];
        }

        // The logical database, kept when it is the dialect's name, which is the tool's word.
        private string? DatabaseMarker(string value)
        {
            if (value.Length == 0)
            {
                return null;
            }

            return value == SqlDialectName.Canonical(renamer._engine.Dialect)
                ? value
                : renamer._mapping.Name(value, quoted: false, NameClass.Database);
        }

        // "@name [type] [null | not null] [-> C# type]": the parameter renamed, its type renamed as a type's spelling,
        // its nullability kept and its C# type dropped.
        private string? ParamMarker(SqlMarker marker)
        {
            var declarations = BundleQueryFile.ReadMarker(
                text,
                renamer._rules,
                marker,
                static scope => scope.Declarations
            );
            if (declarations is not [var declaration])
            {
                return null;
            }

            return BundleQueryFile.WriteParam(
                renamer._rules.ParameterPrefix,
                renamer.ParameterName(declaration.Name),
                declaration.Type is { } type ? Nested(type, NameClass.Type) : null,
                declaration.Nullable
            );
        }

        // "{{name:default}}": the name a token's, quoted, since the generator compares it as written, and the default
        // renamed as SQL.
        private string? TokenMarker(SqlMarker marker)
        {
            var defaults = BundleQueryFile.ReadMarker(
                text,
                renamer._rules,
                marker,
                static scope => scope.TokenDefaults
            );
            if (defaults is not [var token])
            {
                return null;
            }

            var name = renamer.NameOf(token.Name, quoted: true, NameClass.Token);
            return "{{" + name + ":" + Nested(token.Text, NameClass.Alias) + "}}";
        }

        // "name": a token's name, quoted, as the token it keeps is.
        private string? TokenIgnoreMarker(SqlMarker marker) =>
            BundleQueryFile.ReadMarker(text, renamer._rules, marker, static scope => scope.IgnoredTokens)
                is { Count: 1 } names
                ? renamer.NameOf(names.Single(), quoted: true, NameClass.Token)
                : null;

        // A part of a marker renamed as SQL at the same place, its refusals this pass's.
        private string Nested(string sql, NameClass newWords) => renamer.Rename(sql, newWords, place, refuse).Text;
    }
}
