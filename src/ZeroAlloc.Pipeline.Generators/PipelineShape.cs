#nullable enable
namespace ZeroAlloc.Pipeline.Generators;

/// <summary>
/// Describes the delegate shape of a pipeline so <see cref="PipelineEmitter"/>
/// can generate the correct nested static lambda call chain.
/// </summary>
public sealed record PipelineShape
{
    /// <summary>
    /// Concrete type arguments for <c>Handle&lt;...&gt;</c>, without the state type: when
    /// <see cref="State"/> is set, the emitter appends <see cref="PipelineState.TypeName"/>.
    /// ZMediator: ["global::App.Ping", "string"].
    /// ZValidation: ["global::App.Order"].
    /// </summary>
    public required string[] TypeArguments { get; init; }

    /// <summary>
    /// Parameter names at the outermost call site, without the state: when <see cref="State"/>
    /// is set, the emitter passes <see cref="PipelineState.Argument"/> after the first of them.
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

    /// <summary>
    /// The state threaded through every level of the chain, or <c>null</c> for a pipeline whose
    /// innermost body reads no instance state.
    /// <para>
    /// Every lambda the emitter writes is <c>static</c>, so the innermost body cannot read
    /// <c>this</c> or a local. When it needs instance state, pass that state here and read it in
    /// the body through the state's lambda parameter, <c>{prefix}{depth}</c>, such as <c>s2</c>.
    /// A body that captures instead fails to compile rather than allocating a delegate per call.
    /// </para>
    /// </summary>
    public PipelineState? State { get; init; }

    /// <summary>
    /// The number of type parameters a behavior's <c>Handle</c> takes in this pipeline:
    /// <see cref="TypeArguments"/>, plus one for the state type when <see cref="State"/> is set.
    /// <see cref="PipelineDiagnosticRules.FindMissingHandleMethod(System.Collections.Generic.IEnumerable{PipelineBehaviorInfo}, PipelineShape)"/>
    /// checks the behaviors against it.
    /// </summary>
    public int HandleTypeParameterCount => (TypeArguments?.Length ?? 0) + (State is null ? 0 : 1);
}
