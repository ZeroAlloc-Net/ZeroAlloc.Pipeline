#nullable enable
namespace ZeroAlloc.Pipeline.Generators;

/// <summary>
/// Describes the delegate shape of a pipeline so <see cref="PipelineEmitter"/>
/// can generate the correct nested static lambda call chain.
/// </summary>
public sealed class PipelineShape
{
    /// <summary>
    /// Concrete type arguments for <c>Handle&lt;...&gt;</c>.
    /// ZMediator: ["global::App.Ping", "string"].
    /// ZValidation: ["global::App.Order"].
    /// </summary>
    public string[] TypeArguments { get; set; } = System.Array.Empty<string>();

    /// <summary>
    /// Parameter names at the outermost call site.
    /// ZMediator: ["request", "ct"].
    /// ZValidation: ["instance"].
    /// </summary>
    public string[] OuterParameterNames { get; set; } = System.Array.Empty<string>();

    /// <summary>
    /// One prefix letter per outer parameter, used to name lambda params at each nesting level.
    /// Level N produces "{prefix}{N}" for each prefix.
    /// ZMediator: ["r", "c"] → r1,c1  r2,c2 …
    /// ZValidation: ["r"] → r1  r2 …
    /// </summary>
    public string[] LambdaParameterPrefixes { get; set; } = System.Array.Empty<string>();

    /// <summary>
    /// The body of the innermost (non-behavior) call.
    /// Use the lambda param names as they appear at the deepest nesting level.
    /// Example (ZMediator, 2 behaviors): "{ var h = factory?.Invoke() ?? new Handler(); return h.Handle(r2, c2); }"
    /// </summary>
    public string InnermostBodyTemplate { get; set; } = string.Empty;
}
