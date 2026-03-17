# Code Review Fixes Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Fix all Important and Minor issues identified in the post-review of the PipelineBehaviorDiscoverer/PipelineShape/PipelineEmitter/PipelineDiagnosticRules commits.

**Architecture:** Seven independent fixes across three source files and one test file. Each fix is self-contained. No new public API surface is introduced. `PipelineShape` changes from a mutable class to an immutable record — callers using object-initializer syntax are unaffected.

**Tech Stack:** C# 13 / .NET 10, Roslyn (`Microsoft.CodeAnalysis.CSharp`), xUnit

---

### Task 1: Fix `Assert.Contains` → exact FQN assertion

**Files:**
- Modify: `tests/ZeroAlloc.Pipeline.Generators.Tests/PipelineBehaviorDiscovererTests.cs:69`

**Step 1: Change the assertion**

In `Discover_BehaviorWithAppliesTo_SetsAppliesTo`, replace:
```csharp
Assert.Contains("MyModel", results[0].AppliesTo);
```
with:
```csharp
Assert.Equal("global::MyModel", results[0].AppliesTo);
```

**Step 2: Run the test to verify it passes**

```bash
dotnet test --filter "Discover_BehaviorWithAppliesTo_SetsAppliesTo"
```
Expected: PASS (the discoverer already returns the fully-qualified name)

**Step 3: Commit**

```bash
git add tests/ZeroAlloc.Pipeline.Generators.Tests/PipelineBehaviorDiscovererTests.cs
git commit -m "test: tighten AppliesTo assertion to require exact FQN"
```

---

### Task 2: Convert `PipelineShape` to immutable record

**Files:**
- Modify: `src/ZeroAlloc.Pipeline.Generators/PipelineShape.cs`

**Step 1: Rewrite the class as a record**

Replace the entire file content:
```csharp
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
    /// The body of the innermost (non-behavior) call.
    /// Use the lambda param names as they appear at the deepest nesting level.
    /// Example (ZMediator, 2 behaviors): "{ var h = factory?.Invoke() ?? new Handler(); return h.Handle(r2, c2); }"
    /// </summary>
    public required string InnermostBodyTemplate { get; init; }
}
```

**Step 2: Run all tests**

```bash
dotnet test
```
Expected: 18 passed. The existing object-initializer syntax in tests (`new PipelineShape { ... }`) is compatible with `required init` — no test changes needed.

**Step 3: Commit**

```bash
git add src/ZeroAlloc.Pipeline.Generators/PipelineShape.cs
git commit -m "refactor: convert PipelineShape to immutable record with required init properties"
```

---

### Task 3: Extract indentation constants in `PipelineEmitter`

**Files:**
- Modify: `src/ZeroAlloc.Pipeline.Generators/PipelineEmitter.cs`

**Step 1: Add constants and update usages**

At the top of the `PipelineEmitter` class body (before `EmitChain`), add:
```csharp
private const string Indent1 = "\n                ";
private const string Indent2 = "\n                    ";
```

Then replace the hardcoded strings in `EmitChain`:
- `$"static {lambdaParams} =>\n                    {shape.InnermostBodyTemplate}"` → `$"static {lambdaParams} =>{Indent2}{shape.InnermostBodyTemplate}"`
- `$"{behavior.BehaviorTypeName}.Handle{typeArgs}(\n                {outerParams}, {result})"` → `$"{behavior.BehaviorTypeName}.Handle{typeArgs}({Indent1}{outerParams}, {result})"`
- `$"static {levelParams} =>\n                {behavior.BehaviorTypeName}.Handle{typeArgs}(\n                    {levelParamRefs}, {result})"` → `$"static {levelParams} =>{Indent1}{behavior.BehaviorTypeName}.Handle{typeArgs}({Indent2}{levelParamRefs}, {result})"`

**Step 2: Run all tests**

```bash
dotnet test
```
Expected: 18 passed. Output format is unchanged — the constants contain the same whitespace as before.

**Step 3: Commit**

```bash
git add src/ZeroAlloc.Pipeline.Generators/PipelineEmitter.cs
git commit -m "refactor: extract indentation constants in PipelineEmitter"
```

---

### Task 4: Lazy semantic model in `Discover`

**Files:**
- Modify: `src/ZeroAlloc.Pipeline.Generators/PipelineBehaviorDiscoverer.cs`

**Step 1: Move `GetSemanticModel` inside the attributed-class check**

In the `Discover` method, change:

```csharp
foreach (var syntaxTree in compilation.SyntaxTrees)
{
    var semanticModel = compilation.GetSemanticModel(syntaxTree);
    var classDeclarations = syntaxTree.GetRoot()
        .DescendantNodes()
        .OfType<ClassDeclarationSyntax>()
        .Where(c => c.AttributeLists.Count > 0);

    foreach (var classDecl in classDeclarations)
    {
        var symbol = semanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
```

to:

```csharp
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
```

**Step 2: Run all tests**

```bash
dotnet test
```
Expected: 18 passed.

**Step 3: Commit**

```bash
git add src/ZeroAlloc.Pipeline.Generators/PipelineBehaviorDiscoverer.cs
git commit -m "perf: skip semantic model acquisition for syntax trees with no attributed classes"
```

---

### Task 5: `ResolveAttributeClassFromSyntax` → null on ambiguity

**Files:**
- Modify: `src/ZeroAlloc.Pipeline.Generators/PipelineBehaviorDiscoverer.cs`

**Step 1: Collect all candidates, return null if more than one**

Replace the inner loop body of `ResolveAttributeClassFromSyntax` so it collects all matches:

```csharp
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
```

**Step 2: Run all tests**

```bash
dotnet test
```
Expected: 18 passed.

**Step 3: Commit**

```bash
git add src/ZeroAlloc.Pipeline.Generators/PipelineBehaviorDiscoverer.cs
git commit -m "fix: return null from ResolveAttributeClassFromSyntax when multiple name matches exist"
```

---

### Task 6: `GetHandleMethodTypeParamCount` → walk base type chain

**Files:**
- Modify: `src/ZeroAlloc.Pipeline.Generators/PipelineBehaviorDiscoverer.cs`
- Modify: `tests/ZeroAlloc.Pipeline.Generators.Tests/PipelineBehaviorDiscovererTests.cs`

**Step 1: Write a failing test**

Add this test to `PipelineBehaviorDiscovererTests`:

```csharp
[Fact]
public void Discover_BehaviorWithInheritedHandle_ReturnsCorrectTypeParamCount()
{
    var source = """
        using ZeroAlloc.Pipeline;
        using System.Threading;
        using System.Threading.Tasks;

        public abstract class BaseBehavior : IPipelineBehavior
        {
            public static ValueTask<TResponse> Handle<TRequest, TResponse>(
                TRequest request, CancellationToken ct,
                System.Func<TRequest, CancellationToken, ValueTask<TResponse>> next)
                where TRequest : class
                => next(request, ct);
        }

        [PipelineBehavior(Order = 1)]
        public class ConcreteBehavior : BaseBehavior { }
        """;

    var compilation = CreateCompilation(source);
    var results = PipelineBehaviorDiscoverer.Discover(compilation).ToList();

    Assert.Single(results);
    Assert.Equal(2, results[0].HandleMethodTypeParameterCount);
}
```

**Step 2: Run the test to verify it fails**

```bash
dotnet test --filter "Discover_BehaviorWithInheritedHandle_ReturnsCorrectTypeParamCount"
```
Expected: FAIL — `HandleMethodTypeParameterCount` is `-1` because `GetHandleMethodTypeParamCount` does not walk base types.

**Step 3: Fix `GetHandleMethodTypeParamCount` to walk the base type chain**

Replace the method:

```csharp
private static int GetHandleMethodTypeParamCount(INamedTypeSymbol symbol)
{
    var current = symbol;
    while (current != null && current.SpecialType != SpecialType.System_Object)
    {
        foreach (var member in current.GetMembers())
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
        current = current.BaseType;
    }
    return -1;
}
```

**Step 4: Run all tests**

```bash
dotnet test
```
Expected: 19 passed.

**Step 5: Commit**

```bash
git add src/ZeroAlloc.Pipeline.Generators/PipelineBehaviorDiscoverer.cs
git add tests/ZeroAlloc.Pipeline.Generators.Tests/PipelineBehaviorDiscovererTests.cs
git commit -m "fix: walk base type chain in GetHandleMethodTypeParamCount to find inherited Handle methods"
```

---

### Task 7: Add test for `TypeKind.Error` fallback path

**Files:**
- Modify: `tests/ZeroAlloc.Pipeline.Generators.Tests/PipelineBehaviorDiscovererTests.cs`

**Step 1: Add a helper that creates a compilation without the ZeroAlloc.Pipeline reference**

Add a second factory method to the test class:

```csharp
private static Compilation CreateIncompleteCompilation(string source)
{
    var syntaxTree = CSharpSyntaxTree.ParseText(source);
    return CSharpCompilation.Create(
        "TestAssembly",
        [syntaxTree],
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            // Deliberately omit ZeroAlloc.Pipeline reference to force TypeKind.Error path
        ],
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
}
```

**Step 2: Add the test**

```csharp
[Fact]
public void Discover_SubclassedAttribute_WithIncompleteCompilation_IsDetected()
{
    // No ZeroAlloc.Pipeline reference — PipelineBehaviorAttribute resolves as TypeKind.Error.
    // ResolveAttributeClassFromSyntax must find the locally-defined subclass instead.
    var source = """
        using System;

        // Local stub — PipelineBehaviorAttribute is not available from a real reference
        public abstract class PipelineBehaviorAttribute : Attribute
        {
            protected PipelineBehaviorAttribute(int order = 0) { }
        }

        public interface IPipelineBehavior { }

        public sealed class MediatorPipelineBehaviorAttribute : PipelineBehaviorAttribute
        {
            public MediatorPipelineBehaviorAttribute(int order = 0) : base(order) { }
        }

        public interface IMediatorBehavior : IPipelineBehavior { }

        [MediatorPipelineBehavior(order: 3)]
        public class MyBehavior : IMediatorBehavior
        {
            public static string Handle<T>(T r, System.Func<T, string> next) => next(r);
        }
        """;

    var compilation = CreateIncompleteCompilation(source);
    var results = PipelineBehaviorDiscoverer.Discover(compilation).ToList();

    Assert.Single(results);
    Assert.Equal(3, results[0].Order);
    Assert.Equal(1, results[0].HandleMethodTypeParameterCount);
}
```

**Step 3: Run the test**

```bash
dotnet test --filter "Discover_SubclassedAttribute_WithIncompleteCompilation_IsDetected"
```
Expected: PASS — `ResolveAttributeClassFromSyntax` finds the locally-defined `MediatorPipelineBehaviorAttribute` via the syntax fallback.

**Step 4: Run all tests**

```bash
dotnet test
```
Expected: 20 passed.

**Step 5: Commit**

```bash
git add tests/ZeroAlloc.Pipeline.Generators.Tests/PipelineBehaviorDiscovererTests.cs
git commit -m "test: add coverage for TypeKind.Error fallback path in ResolveAttributeClassFromSyntax"
```

---

## Final verification

```bash
dotnet test
```
Expected: 20 passed, 0 failed.
