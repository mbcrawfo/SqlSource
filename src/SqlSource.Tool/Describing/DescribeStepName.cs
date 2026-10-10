using System;

namespace SqlSource.Tool.Describing;

/// <summary>
/// The name of a step as an error prints it.
/// </summary>
internal static class DescribeStepName
{
    public static string Of(DescribeStep step) =>
        step switch
        {
            DescribeStep.DescribeParameters => "describe parameters",
            DescribeStep.CheckParameters => "check parameters",
            DescribeStep.DescribeColumns => "describe columns",
            DescribeStep.Catalog => "catalog",
            DescribeStep.Explain => "explain",
            DescribeStep.Walk => "walk",
            DescribeStep.TableMatch => "table match",
            DescribeStep.ResolveType => "resolve type",
            DescribeStep.WriteSidecar => "write sidecar",
            _ => throw new ArgumentOutOfRangeException(nameof(step), step, null),
        };
}
