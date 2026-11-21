#nullable enable
namespace ZeroAlloc.Pipeline.Generators;

/// <summary>
/// A value that <see cref="PipelineEmitter"/> threads through every level of a pipeline, so the
/// innermost body can reach instance state, such as a scope's <c>IServiceProvider</c> or the
/// validator itself, without a lambda that captures it.
/// <para>
/// Every lambda of the chain stays <c>static</c>, so the compiler creates each one once and a
/// call allocates no delegate, whether the chain runs in a static or an instance member.
/// </para>
/// <para>
/// The behaviors' <c>Handle</c> takes the state type as its <b>last</b> type parameter and the
/// state as its <b>second</b> parameter, right after the request or model, and passes it on to
/// <c>next</c> in the same position. For a request/response pipeline:
/// <code>
/// public static ValueTask&lt;TResponse&gt; Handle&lt;TRequest, TResponse, TState&gt;(
///     TRequest request, TState state, CancellationToken ct,
///     Func&lt;TRequest, TState, CancellationToken, ValueTask&lt;TResponse&gt;&gt; next)
///     =&gt; next(request, state, ct);
/// </code>
/// A behavior never sees the host's concrete state type, so it works with any host.
/// </para>
/// </summary>
public sealed record PipelineState
{
    /// <summary>
    /// The fully qualified state type, passed as the last type argument of every <c>Handle</c>
    /// call, e.g. <c>global::System.IServiceProvider</c>.
    /// </summary>
    public required string TypeName { get; init; }

    /// <summary>
    /// The expression passed as the state at the outermost call, e.g. <c>_services</c> or <c>this</c>.
    /// </summary>
    public required string Argument { get; init; }

    /// <summary>
    /// The prefix of the state's lambda parameter at each nesting level: level N names it
    /// <c>{prefix}{N}</c>. Defaults to <c>"s"</c>, giving <c>s1</c>, <c>s2</c> and so on. It must
    /// differ from every prefix in <see cref="PipelineShape.LambdaParameterPrefixes"/>.
    /// </summary>
    public string LambdaParameterPrefix { get; init; } = "s";
}
