namespace ZeroAlloc.Pipeline;

/// <summary>
/// Marks a class as a pipeline behavior and controls its position in the chain.
/// The class must also implement <see cref="IPipelineBehavior"/> and expose a
/// public static <c>Handle</c> method matching the host framework's delegate shape.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class PipelineBehaviorAttribute(int order = 0) : Attribute
{
    /// <summary>Execution order. Lower values run first (outermost).</summary>
    public int Order { get; set; } = order;

    /// <summary>
    /// When set, this behavior only applies to the specified request/model type.
    /// When null, the behavior applies to all types in the pipeline.
    /// </summary>
    public Type? AppliesTo { get; set; }
}
