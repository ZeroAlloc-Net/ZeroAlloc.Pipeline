#nullable enable
using System.Collections.Generic;

namespace ZeroAlloc.Pipeline.Generators;

/// <summary>
/// The result of <see cref="PipelineEmitter.EmitCachedChain"/>: a chain expression that reads each
/// level's <c>next</c> delegate from an instance field, and the declarations of those fields.
/// </summary>
public sealed class PipelineCachedChain
{
    /// <summary>
    /// A C# expression ready to be placed after <c>return </c> in a generated instance method.
    /// </summary>
    public string Expression { get; }

    /// <summary>
    /// One field declaration per behavior, such as
    /// <c>private global::System.Func&lt;global::App.Ping, string&gt;? __next1;</c>, to emit as
    /// members of the type that contains the chain. The declarations use a nullable reference type
    /// annotation, so emit them in a <c>#nullable enable</c> context. Empty when there are no
    /// behaviors.
    /// </summary>
    public IReadOnlyList<string> MemberDeclarations { get; }

    public PipelineCachedChain(string expression, IReadOnlyList<string> memberDeclarations)
    {
        Expression = expression;
        MemberDeclarations = memberDeclarations;
    }
}
