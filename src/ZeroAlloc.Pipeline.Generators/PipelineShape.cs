#nullable enable
namespace ZeroAlloc.Pipeline.Generators;

/// <summary>
/// Describes the delegate shape of a pipeline so <see cref="PipelineEmitter"/>
/// can generate the correct nested static lambda call chain.
/// </summary>
public sealed record PipelineShape
{
    /// <summary>
    /// Concrete type arguments for <c>Handle&lt;...&gt;</c>.
    /// ZMediator: ["global::App.Ping", "string"].
    /// ZValidation: ["global::App.Order"].
    /// </summary>
    public required string[] TypeArguments { get; init; }

    /// <summary>
    /// Parameter names at the outermost call site.
    /// ZMediator: ["request", "ct"].
    /// ZValidation: ["instance"].
    /// </summary>
    public required string[] OuterParameterNames { get; init; }

    /// <summary>
    /// One prefix letter per outer parameter, used to name lambda params at each nesting level.
    /// Level N produces "{prefix}{N}" for each prefix.
    /// ZMediator: ["r", "c"] → r1,c1  r2,c2 …
    /// ZValidation: ["r"] → r1  r2 …
    /// </summary>
    public required string[] LambdaParameterPrefixes { get; init; }

    /// <summary>
    /// The body of the innermost (non-behavior) call, as a literal string.
    /// Use the lambda param names as they appear at the deepest nesting level.
    /// Example (ZMediator, 2 behaviors): "{ var h = factory?.Invoke() ?? new Handler(); return h.Handle(r2, c2); }"
    /// <para>
    /// Prefer <see cref="InnermostBodyFactory"/> when the body needs to embed the lambda
    /// parameter names at the correct depth — the emitter will pass the final depth count.
    /// </para>
    /// </summary>
    public string InnermostBodyTemplate { get; init; } = string.Empty;

    /// <summary>
    /// Alternative to <see cref="InnermostBodyTemplate"/>.
    /// Called by <see cref="PipelineEmitter.EmitChain"/> with the resolved behavior depth
    /// (i.e. the number of applicable behaviors) so the body can embed the correct lambda
    /// parameter names without the caller having to pre-compute the count.
    /// When set, takes precedence over <see cref="InnermostBodyTemplate"/>.
    /// </summary>
    public System.Func<int, string>? InnermostBodyFactory { get; init; }
}
