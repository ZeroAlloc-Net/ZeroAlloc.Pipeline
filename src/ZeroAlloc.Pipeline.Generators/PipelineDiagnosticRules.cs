#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace ZeroAlloc.Pipeline.Generators;

public static class PipelineDiagnosticRules
{
    /// <summary>
    /// Returns behaviors that do not have a valid <c>Handle</c> method
    /// with the expected number of type parameters.
    /// Map these to your own diagnostic ID (e.g. ZAM005, ZV005).
    /// </summary>
    public static IEnumerable<PipelineBehaviorInfo> FindMissingHandleMethod(
        IEnumerable<PipelineBehaviorInfo> behaviors,
        int expectedTypeParamCount)
        => behaviors.Where(b => !b.HasValidHandleMethod(expectedTypeParamCount));

    /// <summary>
    /// Returns groups of behaviors that share the same <see cref="PipelineBehaviorInfo.Order"/> value.
    /// Only groups with more than one entry are returned.
    /// Map these to your own diagnostic ID (e.g. ZAM006, ZV006).
    /// </summary>
    public static IEnumerable<IGrouping<int, PipelineBehaviorInfo>> FindDuplicateOrders(
        IEnumerable<PipelineBehaviorInfo> behaviors)
        => behaviors
            .GroupBy(b => b.Order)
            .Where(g => g.Count() > 1);
}
