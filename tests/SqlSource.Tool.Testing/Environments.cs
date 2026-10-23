using System;
using System.Collections.Generic;

namespace SqlSource.Tool.Testing;

// An environment of a test's own, for the parts of the tool that are handed ToolHost.GetEnvironmentVariable: each
// name that the test gives has its value, and any other is not set.
internal static class Environments
{
    public static Func<string, string?> Of(params (string Name, string Value)[] variables)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in variables)
        {
            values[name] = value;
        }

        return name => values.GetValueOrDefault(name);
    }
}
