using System;

namespace SqlSource.Tests.Postgres;

// Whether a run records the expectations it would compare: tools/record-fixtures.sh sets the variable of its test.
internal static class Recording
{
    public static bool IsAsked(string variable) => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variable));
}
