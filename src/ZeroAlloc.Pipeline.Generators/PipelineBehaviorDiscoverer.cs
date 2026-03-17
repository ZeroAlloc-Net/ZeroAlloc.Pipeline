#nullable enable
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ZeroAlloc.Pipeline.Generators;

public static class PipelineBehaviorDiscoverer
{
    private const string PipelineBehaviorAttributeFqn = "ZeroAlloc.Pipeline.PipelineBehaviorAttribute";
    private const string IPipelineBehaviorFqn = "ZeroAlloc.Pipeline.IPipelineBehavior";

    /// <summary>
    /// Per-symbol transform for use with
    /// <c>context.SyntaxProvider.ForAttributeWithMetadataName</c> in incremental generators.
    /// This is the preferred integration point — it lets Roslyn cache and diff at the syntax
    /// level rather than re-scanning the whole compilation on every keystroke.
    /// </summary>
    public static PipelineBehaviorInfo? FromAttributeSyntaxContext(GeneratorAttributeSyntaxContext ctx)
    {
        var symbol = ctx.TargetSymbol as INamedTypeSymbol;
        if (symbol == null) return null;

        // ForAttributeWithMetadataName already matched the attribute; take the first one.
        var pipelineAttr = ctx.Attributes.FirstOrDefault();
        if (pipelineAttr == null) return null;

        return BuildBehaviorInfo(symbol, pipelineAttr, ctx.SemanticModel);
    }

    /// <summary>
    /// Discovers all pipeline behaviors in <paramref name="compilation"/>.
    /// Detects both direct <c>ZeroAlloc.Pipeline.PipelineBehaviorAttribute</c> usage and
    /// any subclasses of it (e.g. <c>ZeroAlloc.Mediator.PipelineBehaviorAttribute</c>).
    /// Falls back to syntax-level argument parsing when semantic information is unavailable
    /// (e.g., incomplete compilations missing runtime references).
    /// <para>
    /// Prefer <see cref="FromAttributeSyntaxContext"/> with
    /// <c>ForAttributeWithMetadataName</c> in production generators for better incremental
    /// performance. This overload is retained for test use and one-shot tool scenarios.
    /// </para>
    /// </summary>
    public static IEnumerable<PipelineBehaviorInfo> Discover(Compilation compilation)
    {
        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var classDeclarations = syntaxTree.GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Where(c => c.AttributeLists.Count > 0)
                .ToList();

            if (classDeclarations.Count == 0) continue;

            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            foreach (var classDecl in classDeclarations)
            {
                var symbol = semanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
                if (symbol == null) continue;

                var info = TryGetBehaviorInfo(symbol, semanticModel);
                if (info != null)
                    yield return info;
            }
        }
    }

    private static PipelineBehaviorInfo? TryGetBehaviorInfo(INamedTypeSymbol symbol, SemanticModel semanticModel)
    {
        // Must have an attribute that is or derives from PipelineBehaviorAttribute
        AttributeData? pipelineAttr = null;
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass == null) continue;

            // First attempt: semantic check directly on the AttributeClass symbol.
            // This works even for error types if the FQN display string is still available
            // (which Roslyn provides even when TypeKind == Error, e.g. when System.Runtime is missing).
            if (InheritsFrom(attr.AttributeClass, PipelineBehaviorAttributeFqn))
            {
                pipelineAttr = attr;
                break;
            }

            // Second attempt: when the attribute class is an error type (incomplete compilation),
            // the BaseType is null so InheritsFrom can't walk up. Resolve the class from syntax
            // to get a properly-parented symbol (for locally-defined subclasses).
            if (attr.AttributeClass.TypeKind == TypeKind.Error)
            {
                var resolvedClass = ResolveAttributeClassFromSyntax(attr, semanticModel);
                if (resolvedClass != null && InheritsFrom(resolvedClass, PipelineBehaviorAttributeFqn))
                {
                    pipelineAttr = attr;
                    break;
                }
            }
        }
        if (pipelineAttr == null) return null;

        return BuildBehaviorInfo(symbol, pipelineAttr, semanticModel);
    }

    private static PipelineBehaviorInfo? BuildBehaviorInfo(
        INamedTypeSymbol symbol,
        AttributeData pipelineAttr,
        SemanticModel semanticModel)
    {
        // Must implement IPipelineBehavior (or a sub-interface)
        var implementsPipeline = symbol.AllInterfaces.Any(i => InheritsFrom(i, IPipelineBehaviorFqn));
        if (!implementsPipeline) return null;

        var behaviorTypeName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var attrSyntax = pipelineAttr.ApplicationSyntaxReference?.GetSyntax() as AttributeSyntax;

        var order = ReadOrder(pipelineAttr, attrSyntax);
        var appliesTo = ReadAppliesTo(pipelineAttr, attrSyntax, semanticModel);
        var typeParamCount = GetHandleMethodTypeParamCount(symbol);

        return new PipelineBehaviorInfo(behaviorTypeName, order, appliesTo, typeParamCount);
    }

    /// <summary>
    /// When the attribute class is an error type, try to find its real symbol by looking up
    /// the class declaration in the same compilation.
    /// <para>
    /// <b>IMPORTANT:</b> This fallback is only reached when
    /// <c>AttributeClass.TypeKind == TypeKind.Error</c>, which happens in incomplete test
    /// compilations that are missing runtime/framework references. In a real source generator
    /// execution (where all referenced assemblies are present) this path is never taken.
    /// Do NOT remove this method — the generator tests rely on it to exercise attribute
    /// subclass discovery without a full BCL reference set.
    /// </para>
    /// </summary>
    private static INamedTypeSymbol? ResolveAttributeClassFromSyntax(AttributeData attr, SemanticModel semanticModel)
    {
        var attrSyntax = attr.ApplicationSyntaxReference?.GetSyntax() as AttributeSyntax;
        if (attrSyntax == null) return null;

        var attrName = attrSyntax.Name.ToString();

        var candidates = new System.Collections.Generic.List<INamedTypeSymbol>();

        foreach (var syntaxTree in semanticModel.Compilation.SyntaxTrees)
        {
            var sm = semanticModel.Compilation.GetSemanticModel(syntaxTree);
            var classDecls = syntaxTree.GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>();

            foreach (var classDecl in classDecls)
            {
                var name = classDecl.Identifier.Text;
                if (name == attrName
                    || name == attrName + "Attribute"
                    || (attrName.EndsWith("Attribute") && name == attrName.Substring(0, attrName.Length - "Attribute".Length)))
                {
                    var classSymbol = sm.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
                    if (classSymbol != null)
                        candidates.Add(classSymbol);
                }
            }
        }

        return candidates.Count == 1 ? candidates[0] : null;
    }

    private static bool InheritsFrom(INamedTypeSymbol symbol, string fullName)
    {
        // Check all interfaces transitively (AllInterfaces is already transitive)
        if (symbol.AllInterfaces.Any(i => i.ToDisplayString() == fullName)) return true;
        // Walk the base class chain for direct name match
        var current = symbol;
        while (current != null)
        {
            if (current.ToDisplayString() == fullName) return true;
            current = current.BaseType;
        }
        return false;
    }

    private static bool InheritsFrom(ITypeSymbol symbol, string fullName)
    {
        if (symbol.ToDisplayString() == fullName) return true;
        var named = symbol as INamedTypeSymbol;
        if (named != null) return InheritsFrom(named, fullName);
        return false;
    }

    private static int ReadOrder(AttributeData attr, AttributeSyntax? attrSyntax)
    {
        // Try semantic named arguments first (most reliable when compilation is complete).
        foreach (var named in attr.NamedArguments)
        {
            if (named.Key == "Order" && named.Value.Value is int namedOrder)
                return namedOrder;
        }

        // Try semantic constructor arguments (e.g. [PipelineBehavior(2)]).
        if (attr.ConstructorArguments.Length > 0
            && attr.ConstructorArguments[0].Value is int ctorOrder)
            return ctorOrder;

        // Fallback: parse the attribute argument list from syntax.
        if (attrSyntax?.ArgumentList != null)
        {
            foreach (var arg in attrSyntax.ArgumentList.Arguments)
            {
                // Named property setter: Order = 1
                if (arg.NameEquals?.Name.Identifier.Text == "Order"
                    && arg.Expression is LiteralExpressionSyntax namedLit
                    && namedLit.Token.Value is int syntaxNamedOrder)
                    return syntaxNamedOrder;

                // Named constructor parameter (colon syntax): order: 1
                if (arg.NameColon?.Name.Identifier.Text == "order"
                    && arg.Expression is LiteralExpressionSyntax colonLit
                    && colonLit.Token.Value is int syntaxColonOrder)
                    return syntaxColonOrder;

                // Positional first argument (no name)
                if (arg.NameEquals == null && arg.NameColon == null
                    && arg.Expression is LiteralExpressionSyntax posLit
                    && posLit.Token.Value is int syntaxPosOrder)
                    return syntaxPosOrder;
            }
        }

        return 0;
    }

    private static string? ReadAppliesTo(AttributeData attr, AttributeSyntax? attrSyntax, SemanticModel semanticModel)
    {
        // Try semantic named arguments first.
        foreach (var named in attr.NamedArguments)
        {
            if (named.Key == "AppliesTo" && named.Value.Value is ITypeSymbol typeSymbol)
                return typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        // Fallback: parse from syntax — look for AppliesTo = typeof(X)
        if (attrSyntax?.ArgumentList != null)
        {
            foreach (var arg in attrSyntax.ArgumentList.Arguments)
            {
                if (arg.NameEquals?.Name.Identifier.Text == "AppliesTo"
                    && arg.Expression is TypeOfExpressionSyntax typeofExpr)
                {
                    var typeInfo = semanticModel.GetTypeInfo(typeofExpr.Type);
                    if (typeInfo.Type != null)
                        return typeInfo.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    // If semantic model can't resolve it, return the syntax name.
                    return typeofExpr.Type.ToString();
                }
            }
        }

        return null;
    }

    private static int GetHandleMethodTypeParamCount(INamedTypeSymbol symbol)
    {
        foreach (var member in symbol.GetMembers())
        {
            if (member is IMethodSymbol method
                && method.Name == "Handle"
                && method.IsStatic
                && method.DeclaredAccessibility == Accessibility.Public
                && method.TypeParameters.Length > 0)
            {
                return method.TypeParameters.Length;
            }
        }
        return -1;
    }
}
