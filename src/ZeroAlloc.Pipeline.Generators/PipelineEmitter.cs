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
    /// <para>
    /// Every lambda is <c>static</c>, so the compiler creates each one once and a call through the
    /// chain allocates no delegate. When <see cref="PipelineShape.State"/> is set, the state is
    /// passed as each <c>Handle</c>'s second argument and each lambda's second parameter, and its
    /// type is appended to the type arguments, so the innermost body reads it as
    /// <c>{prefix}{depth}</c> instead of capturing it.
    /// </para>
    /// </summary>
    /// <param name="behaviors">
    /// Behaviors to chain, pre-filtered (AppliesTo already checked) and sorted by Order ascending.
    /// </param>
    /// <param name="shape">Delegate shape describing type args, parameter names, the state and the innermost body.</param>
    /// <returns>A C# expression string ready to be placed after <c>return </c> in a generated method.</returns>
    public static string EmitChain(
        IReadOnlyList<PipelineBehaviorInfo> behaviors,
        PipelineShape shape)
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
        ValidateState(shape);

        var depth = behaviors.Count;
        var innermostBody = shape.InnermostBodyFactory != null
            ? shape.InnermostBodyFactory(depth)
            : shape.InnermostBodyTemplate;

        if (behaviors.Count == 0)
            return innermostBody;

        var state = shape.State;
        var typeArgs = "<" + string.Join(", ", shape.TypeArguments) + (state is null ? "" : ", " + state.TypeName) + ">";

        // Build innermost lambda: static (r{depth}, s{depth}, c{depth}) => { ... }
        var result = $"static ({BuildParamRefs(shape.LambdaParameterPrefixes, state, depth)}) =>{Indent2}{innermostBody}";

        for (var i = depth - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            if (i == 0)
            {
                // Outermost: use the real parameter names, and the state's argument expression.
                var outerParams = InsertState(shape.OuterParameterNames, state?.Argument);
                result = $"{behavior.BehaviorTypeName}.Handle{typeArgs}({Indent1}{outerParams}, {result})";
            }
            else
            {
                // Intermediate: wrap in a lambda using level-i param names
                var levelParams = BuildParamRefs(shape.LambdaParameterPrefixes, state, i);
                result = $"static ({levelParams}) =>{Indent1}{behavior.BehaviorTypeName}.Handle{typeArgs}({Indent2}{levelParams}, {result})";
            }
        }

        return result;
    }

    private static void ValidateState(PipelineShape shape)
    {
        var state = shape.State;
        if (state is null)
            return;

        if (string.IsNullOrWhiteSpace(state.TypeName))
            throw new System.ArgumentException("State.TypeName must name the state type.", nameof(shape));
        if (string.IsNullOrWhiteSpace(state.Argument))
            throw new System.ArgumentException("State.Argument must be the expression passed as the state.", nameof(shape));
        if (string.IsNullOrWhiteSpace(state.LambdaParameterPrefix))
            throw new System.ArgumentException("State.LambdaParameterPrefix must be a valid identifier prefix.", nameof(shape));
        if (shape.LambdaParameterPrefixes.Contains(state.LambdaParameterPrefix, System.StringComparer.Ordinal))
            throw new System.ArgumentException(
                $"State.LambdaParameterPrefix '{state.LambdaParameterPrefix}' is also in LambdaParameterPrefixes; the lambda parameters would collide.",
                nameof(shape));
    }

    // The level's lambda parameters, with the state's after the first: r1, s1, c1.
    private static string BuildParamRefs(string[] prefixes, PipelineState? state, int level)
        => InsertState(prefixes.Select(p => $"{p}{level}"), state is null ? null : $"{state.LambdaParameterPrefix}{level}");

    // Joins the parameters, with the state, when there is one, as the second.
    private static string InsertState(IEnumerable<string> parameters, string? state)
    {
        var parts = parameters.ToList();
        if (state != null)
            parts.Insert(1, state);
        return string.Join(", ", parts);
    }
}
