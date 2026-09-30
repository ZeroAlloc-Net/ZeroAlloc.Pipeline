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
    /// Returns types that carry <c>[PipelineBehavior]</c> but do not implement
    /// <c>ZeroAlloc.Pipeline.IPipelineBehavior</c>. Discovery leaves them out of the pipeline, so
    /// they silently never run. <see cref="PipelineBehaviorCandidateInfo.IsStatic"/> tells a static
    /// class, which cannot implement an interface, apart from a class that forgot it.
    /// Map these to your own diagnostic ID, reported as a Warning.
    /// </summary>
    public static IEnumerable<PipelineBehaviorCandidateInfo> FindMissingPipelineBehaviorInterface(
        IEnumerable<PipelineBehaviorCandidateInfo> candidates)
        => candidates.Where(c => !c.ImplementsPipelineBehavior);

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
