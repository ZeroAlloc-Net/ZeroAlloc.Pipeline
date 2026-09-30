#nullable enable
namespace ZeroAlloc.Pipeline.Generators;

/// <summary>
/// A type that carries <c>[PipelineBehavior]</c> (or a subclass of it), whether or not it is a
/// valid behavior. Only a candidate that implements <c>ZeroAlloc.Pipeline.IPipelineBehavior</c>
/// joins the pipeline; pass the others to
/// <see cref="PipelineDiagnosticRules.FindMissingPipelineBehaviorInterface"/> to report them.
/// </summary>
public sealed class PipelineBehaviorCandidateInfo : IEquatable<PipelineBehaviorCandidateInfo>
{
    /// <summary>Fully qualified type name, e.g. "global::App.LoggingBehavior".</summary>
    public string BehaviorTypeName { get; }

    /// <summary>
    /// True when the type implements <c>ZeroAlloc.Pipeline.IPipelineBehavior</c> or a sub-interface,
    /// so discovery returns it as a <see cref="PipelineBehaviorInfo"/>.
    /// </summary>
    public bool ImplementsPipelineBehavior { get; }

    /// <summary>
    /// True when the type is a static class. A static class cannot implement an interface, so it
    /// can never join the pipeline; the fix is to make it non-static and implement the interface.
    /// </summary>
    public bool IsStatic { get; }

    public PipelineBehaviorCandidateInfo(
        string behaviorTypeName,
        bool implementsPipelineBehavior,
        bool isStatic)
    {
        BehaviorTypeName = behaviorTypeName;
        ImplementsPipelineBehavior = implementsPipelineBehavior;
        IsStatic = isStatic;
    }

    public bool Equals(PipelineBehaviorCandidateInfo? other)
    {
        if (other is null) return false;
        return BehaviorTypeName == other.BehaviorTypeName
            && ImplementsPipelineBehavior == other.ImplementsPipelineBehavior
            && IsStatic == other.IsStatic;
    }

    public override bool Equals(object? obj) => Equals(obj as PipelineBehaviorCandidateInfo);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + BehaviorTypeName.GetHashCode();
            hash = hash * 31 + ImplementsPipelineBehavior.GetHashCode();
            hash = hash * 31 + IsStatic.GetHashCode();
            return hash;
        }
    }

    public static bool operator ==(PipelineBehaviorCandidateInfo? left, PipelineBehaviorCandidateInfo? right)
        => left is null ? right is null : left.Equals(right);

    public static bool operator !=(PipelineBehaviorCandidateInfo? left, PipelineBehaviorCandidateInfo? right)
        => !(left == right);
}
