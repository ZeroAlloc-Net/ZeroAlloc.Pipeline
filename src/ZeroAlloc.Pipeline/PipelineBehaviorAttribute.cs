namespace ZeroAlloc.Pipeline;

/// <summary>
/// Marks a class as a pipeline behavior and controls its position in the chain.
/// The class must also implement <see cref="IPipelineBehavior"/> and expose a
/// public static <c>Handle</c> method matching the host framework's delegate shape.
/// </summary>
/// <remarks>
/// This class is intentionally non-sealed. Framework-specific packages (such as
/// <c>ZeroAlloc.Mediator</c>) subclass it to provide a namespace-local alias that
/// preserves backward compatibility with existing consumer code.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class PipelineBehaviorAttribute(int order = 0) : Attribute
{
    /// <summary>Execution order. Lower values run first (outermost).</summary>
    public int Order { get; set; } = order;

    /// <summary>
    /// When set, this behavior only applies to the specified request/model type.
    /// When null, the behavior applies to all types in the pipeline.
    /// </summary>
    /// <remarks>
    /// This value is read by the Roslyn source generator via the Roslyn symbol model
    /// at compile time — it is never accessed via reflection at runtime.
    /// Annotating it with <c>[DynamicallyAccessedMembers]</c> is therefore incorrect.
    /// </remarks>
    public Type? AppliesTo { get; set; }
}
