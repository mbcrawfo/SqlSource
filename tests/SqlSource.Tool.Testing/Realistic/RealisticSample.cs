using System;
using System.Collections.Generic;

namespace SqlSource.Tool.Testing.Realistic;

// A sample of a case built by a test, from its names and values, compared as RealisticCaseReader compares a real
// sample's, ignoring case.  It is the one builder, so that no test builds one with another comparer.
internal static class RealisticSample
{
    public static Dictionary<string, string> Of(params (string Name, string Value)[] values)
    {
        var sample = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in values)
        {
            sample.Add(name, value);
        }

        return sample;
    }
}
