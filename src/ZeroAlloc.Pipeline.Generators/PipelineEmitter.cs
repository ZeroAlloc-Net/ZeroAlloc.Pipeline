#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace ZeroAlloc.Pipeline.Generators;

public static class PipelineEmitter
{
    private const string Indent1 = "\n                ";
    private const string Indent2 = "\n                    ";

    /// <summary>
    /// Emits a nested static lambda call chain for the given behaviors and shape.
    /// </summary>
    /// <param name="behaviors">
    /// Behaviors to chain, pre-filtered (AppliesTo already checked) and sorted by Order ascending.
    /// </param>
    /// <param name="shape">Delegate shape describing type args, parameter names, and the innermost body.</param>
    /// <returns>A C# expression string ready to be placed after <c>return </c> in a generated method.</returns>
    public static string EmitChain(
        IReadOnlyList<PipelineBehaviorInfo> behaviors,
        PipelineShape shape)
        => EmitChain(behaviors, shape, static _ => "");

    /// <param name="behaviors">See <see cref="EmitChain(IReadOnlyList{PipelineBehaviorInfo}, PipelineShape)"/>.</param>
    /// <param name="shape">See <see cref="EmitChain(IReadOnlyList{PipelineBehaviorInfo}, PipelineShape)"/>.</param>
    /// <param name="nextPrefix">
    /// Written before the <c>next</c> lambda passed at each level, given the lambda's level.
    /// </param>
    private static string EmitChain(
        IReadOnlyList<PipelineBehaviorInfo> behaviors,
        PipelineShape shape,
        System.Func<int, string> nextPrefix)
    {
        if (behaviors == null) throw new System.ArgumentNullException(nameof(behaviors));
        if (shape == null) throw new System.ArgumentNullException(nameof(shape));
        if (shape.TypeArguments == null || shape.TypeArguments.Length == 0)
            throw new System.ArgumentException("TypeArguments must contain at least one type argument.", nameof(shape));
        if (shape.LambdaParameterPrefixes == null || shape.LambdaParameterPrefixes.Length == 0)
            throw new System.ArgumentException("LambdaParameterPrefixes must contain at least one prefix.", nameof(shape));
        if (shape.OuterParameterNames == null || shape.OuterParameterNames.Length == 0)
            throw new System.ArgumentException("OuterParameterNames must contain at least one parameter name.", nameof(shape));
        if (shape.InnermostBodyFactory == null && string.IsNullOrEmpty(shape.InnermostBodyTemplate))
            throw new System.ArgumentException("Either InnermostBodyFactory or InnermostBodyTemplate must be set.", nameof(shape));

        var depth = behaviors.Count;
        var innermostBody = shape.InnermostBodyFactory != null
            ? shape.InnermostBodyFactory(depth)
            : shape.InnermostBodyTemplate;

        if (behaviors.Count == 0)
            return innermostBody;

        var typeArgs = "<" + string.Join(", ", shape.TypeArguments) + ">";
        var staticPrefix = shape.EmitStaticLambdas ? "static " : "";

        // Build innermost lambda: [static] (r{depth}, c{depth}) => { ... }
        var lambdaParams = BuildLambdaParams(shape.LambdaParameterPrefixes, depth);
        var innermost = $"{nextPrefix(depth)}{staticPrefix}{lambdaParams} =>{Indent2}{innermostBody}";

        var result = innermost;

        for (var i = depth - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            if (i == 0)
            {
                // Outermost: use the real parameter names
                var outerParams = string.Join(", ", shape.OuterParameterNames);
                result = $"{behavior.BehaviorTypeName}.Handle{typeArgs}({Indent1}{outerParams}, {result})";
            }
            else
            {
                // Intermediate: wrap in a lambda using level-i param names
                var levelParams = BuildLambdaParams(shape.LambdaParameterPrefixes, i);
                var levelParamRefs = BuildParamRefs(shape.LambdaParameterPrefixes, i);
                result = $"{nextPrefix(i)}{staticPrefix}{levelParams} =>{Indent1}{behavior.BehaviorTypeName}.Handle{typeArgs}({Indent2}{levelParamRefs}, {result})";
            }
        }

        return result;
    }

    /// <summary>
    /// Emits the same call chain as <see cref="EmitChain(IReadOnlyList{PipelineBehaviorInfo}, PipelineShape)"/>, but caches each level's <c>next</c>
    /// delegate in an instance field, so the chain allocates no delegate after its first call.
    /// Use it when the innermost body reads instance state: <see cref="EmitChain(IReadOnlyList{PipelineBehaviorInfo}, PipelineShape)"/> then needs
    /// <see cref="PipelineShape.EmitStaticLambdas"/> set to <c>false</c>, and every call allocates
    /// one delegate per behavior, because the compiler does not cache a lambda that captures
    /// <c>this</c>.
    /// <para>
    /// Each level reads <c>field ??= lambda</c>. The lambdas read the next level's field, so they
    /// are never <c>static</c> and <see cref="PipelineShape.EmitStaticLambdas"/> is ignored. Emit
    /// the chain in an instance member of the type that declares
    /// <see cref="PipelineCachedChain.MemberDeclarations"/>. Two threads that make the first call
    /// together may each create a delegate; both are equivalent and one is kept.
    /// </para>
    /// </summary>
    /// <param name="behaviors">
    /// Behaviors to chain, pre-filtered (AppliesTo already checked) and sorted by Order ascending.
    /// </param>
    /// <param name="shape">Delegate shape describing type args, parameter names, and the innermost body.</param>
    /// <param name="nextDelegateType">
    /// The fully qualified type of the <c>next</c> parameter of the behaviors' <c>Handle</c>, with
    /// the shape's type arguments substituted, e.g.
    /// <c>global::System.Func&lt;global::App.Ping, global::System.Threading.CancellationToken, global::System.Threading.Tasks.ValueTask&lt;string&gt;&gt;</c>.
    /// It is the type of every cache field.
    /// </param>
    /// <param name="cacheFieldPrefix">
    /// The name of the cache fields before their level number, e.g. <c>__sendPingNext</c> gives
    /// <c>__sendPingNext1</c>, <c>__sendPingNext2</c>. It must be unique among the chains of the
    /// containing type.
    /// </param>
    public static PipelineCachedChain EmitCachedChain(
        IReadOnlyList<PipelineBehaviorInfo> behaviors,
        PipelineShape shape,
        string nextDelegateType,
        string cacheFieldPrefix)
    {
        if (string.IsNullOrWhiteSpace(nextDelegateType))
            throw new System.ArgumentException("nextDelegateType must name the type of the behaviors' next delegate.", nameof(nextDelegateType));
        if (string.IsNullOrWhiteSpace(cacheFieldPrefix))
            throw new System.ArgumentException("cacheFieldPrefix must be a valid identifier prefix.", nameof(cacheFieldPrefix));

        var nonStaticShape = shape?.EmitStaticLambdas == true ? shape with { EmitStaticLambdas = false } : shape;
        var expression = EmitChain(behaviors, nonStaticShape!, level => $"{cacheFieldPrefix}{level} ??= ");

        var members = new string[behaviors.Count];
        for (var level = 1; level <= behaviors.Count; level++)
            members[level - 1] = $"private {nextDelegateType}? {cacheFieldPrefix}{level};";

        return new PipelineCachedChain(expression, members);
    }

    private static string BuildLambdaParams(string[] prefixes, int level)
    {
        if (prefixes.Length == 1)
            return $"({prefixes[0]}{level})";

        var parts = prefixes.Select(p => $"{p}{level}");
        return "(" + string.Join(", ", parts) + ")";
    }

    private static string BuildParamRefs(string[] prefixes, int level)
    {
        var parts = prefixes.Select(p => $"{p}{level}");
        return string.Join(", ", parts);
    }
}
