using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using SqlSource.Tool.Bundles.Renaming;

namespace SqlSource.Tool.Tests.Bundles.Renaming;

// Both engines' field tests walk the records of their calls through this, to find a string member with no rule and a
// rule with no member.
internal static class CallMembers
{
    private static readonly Type[] Scalars = [typeof(string), typeof(int), typeof(long), typeof(bool)];

    // Every member of every call, at every depth, as CaptureJson.Options names it: an object and an array are members
    // too, and an element of an array is [] in the path.
    public static IEnumerable<(string Method, string Path, bool IsString)> Of(
        IReadOnlyDictionary<string, (Type Request, Type Answer)> calls
    ) =>
        calls.SelectMany(static call =>
            MembersOf(call.Value.Request, "request")
                .Concat(MembersOf(call.Value.Answer, "answer"))
                .Select(found => (call.Key, found.Path, found.IsString))
        );

    // Whether the member or one that holds it is dropped, as CaptureRenamer.Drop removes it.
    public static bool IsUnderADrop(Func<string, string, FieldRule?> ruleOf, string method, string path)
    {
        for (var at = path; at.Length > 0; at = Parent(at))
        {
            if (ruleOf(method, at) is { Kind: FieldRuleKind.Drop })
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<(string Path, bool IsString)> MembersOf(Type record, string path)
    {
        foreach (var property in record.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var at = path + "." + JsonNamingPolicy.CamelCase.ConvertName(property.Name);
            foreach (var found in ValuesOf(property.PropertyType, at))
            {
                yield return found;
            }
        }
    }

    private static IEnumerable<(string Path, bool IsString)> ValuesOf(Type type, string path)
    {
        var plain = Nullable.GetUnderlyingType(type) ?? type;
        if (Scalars.Contains(plain))
        {
            yield return (path, plain == typeof(string));
            yield break;
        }

        yield return (path, false);
        var inner =
            plain.IsGenericType && plain.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
                ? ValuesOf(plain.GetGenericArguments()[0], path + "[]")
                : MembersOf(plain, path);
        foreach (var found in inner)
        {
            yield return found;
        }
    }

    private static string Parent(string path)
    {
        if (path.EndsWith("[]", StringComparison.Ordinal))
        {
            return path[..^2];
        }

        var dot = path.LastIndexOf('.');
        return dot > 0 ? path[..dot] : string.Empty;
    }
}
