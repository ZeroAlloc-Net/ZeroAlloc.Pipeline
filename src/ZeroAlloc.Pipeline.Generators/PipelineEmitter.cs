#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace ZeroAlloc.Pipeline.Generators;

public static class PipelineEmitter
{
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
    {
        if (behaviors == null) throw new System.ArgumentNullException(nameof(behaviors));
        if (shape == null) throw new System.ArgumentNullException(nameof(shape));
        if (shape.TypeArguments == null || shape.TypeArguments.Length == 0)
            throw new System.ArgumentException("TypeArguments must contain at least one type argument.", nameof(shape));
        if (shape.LambdaParameterPrefixes == null || shape.LambdaParameterPrefixes.Length == 0)
            throw new System.ArgumentException("LambdaParameterPrefixes must contain at least one prefix.", nameof(shape));
        if (shape.OuterParameterNames == null || shape.OuterParameterNames.Length == 0)
            throw new System.ArgumentException("OuterParameterNames must contain at least one parameter name.", nameof(shape));
        if (string.IsNullOrEmpty(shape.InnermostBodyTemplate))
            throw new System.ArgumentException("InnermostBodyTemplate must not be null or empty.", nameof(shape));

        if (behaviors.Count == 0)
            return shape.InnermostBodyTemplate;

        var typeArgs = "<" + string.Join(", ", shape.TypeArguments) + ">";
        var depth = behaviors.Count;

        // Build innermost lambda: static (r{depth}, c{depth}) => { ... }
        var lambdaParams = BuildLambdaParams(shape.LambdaParameterPrefixes, depth);
        var innermost = $"static {lambdaParams} =>\n                    {shape.InnermostBodyTemplate}";

        var result = innermost;

        for (var i = depth - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            if (i == 0)
            {
                // Outermost: use the real parameter names
                var outerParams = string.Join(", ", shape.OuterParameterNames);
                result = $"{behavior.BehaviorTypeName}.Handle{typeArgs}(\n                {outerParams}, {result})";
            }
            else
            {
                // Intermediate: wrap in a lambda using level-i param names
                var levelParams = BuildLambdaParams(shape.LambdaParameterPrefixes, i);
                var levelParamRefs = BuildParamRefs(shape.LambdaParameterPrefixes, i);
                result = $"static {levelParams} =>\n                {behavior.BehaviorTypeName}.Handle{typeArgs}(\n                    {levelParamRefs}, {result})";
            }
        }

        return result;
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
