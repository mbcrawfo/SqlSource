using System;

namespace SqlSource.Tests.Postgres;

// Whether a run records the expectations it would compare: tools/record-fixtures.sh and tools/record-realistic.sh
// set the variable of their test.
internal static class Recording
{
    public static bool IsAsked(string variable) => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variable));
}
