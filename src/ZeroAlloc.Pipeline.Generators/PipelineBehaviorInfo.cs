#nullable enable
namespace ZeroAlloc.Pipeline.Generators;

public sealed class PipelineBehaviorInfo : IEquatable<PipelineBehaviorInfo>
{
    /// <summary>Fully qualified type name, e.g. "global::App.LoggingBehavior".</summary>
    public string BehaviorTypeName { get; }

    public int Order { get; }

    /// <summary>Fully qualified type name this behavior is scoped to, or null for all types.</summary>
    public string? AppliesTo { get; }

    /// <summary>
    /// Number of type parameters on the public static Handle method found.
    /// -1 means no Handle method was found at all.
    /// </summary>
    public int HandleMethodTypeParameterCount { get; }

    public PipelineBehaviorInfo(
        string behaviorTypeName,
        int order,
        string? appliesTo,
        int typeParamCount)
    {
        BehaviorTypeName = behaviorTypeName;
        Order = order;
        AppliesTo = appliesTo;
        HandleMethodTypeParameterCount = typeParamCount;
    }

    /// <summary>Returns true when a Handle method exists with the expected number of type parameters.</summary>
    public bool HasValidHandleMethod(int expectedTypeParamCount)
        => HandleMethodTypeParameterCount == expectedTypeParamCount;

    public bool Equals(PipelineBehaviorInfo? other)
    {
        if (other is null) return false;
        return BehaviorTypeName == other.BehaviorTypeName
            && Order == other.Order
            && AppliesTo == other.AppliesTo
            && HandleMethodTypeParameterCount == other.HandleMethodTypeParameterCount;
    }

    public override bool Equals(object? obj) => Equals(obj as PipelineBehaviorInfo);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + BehaviorTypeName.GetHashCode();
            hash = hash * 31 + Order.GetHashCode();
            hash = hash * 31 + (AppliesTo?.GetHashCode() ?? 0);
            hash = hash * 31 + HandleMethodTypeParameterCount.GetHashCode();
            return hash;
        }
    }

    public static bool operator ==(PipelineBehaviorInfo? left, PipelineBehaviorInfo? right)
        => left is null ? right is null : left.Equals(right);

    public static bool operator !=(PipelineBehaviorInfo? left, PipelineBehaviorInfo? right)
        => !(left == right);
}
