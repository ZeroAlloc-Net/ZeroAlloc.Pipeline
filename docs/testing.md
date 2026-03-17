---
id: testing
title: Testing
slug: /docs/testing
description: How to test pipeline behaviors in isolation and test generators built on ZeroAlloc.Pipeline.
sidebar_position: 8
---

# Testing

ZeroAlloc.Pipeline has no runtime state, so testing is straightforward. Behaviors are pure static functions. Generators can be tested with a real `CSharpCompilation`.

## Testing a Behavior in Isolation

A behavior's `Handle` method is `public static` — call it directly.

```csharp
[Fact]
public async Task LoggingBehavior_CallsNext_AndReturnsResult()
{
    var callCount = 0;
    ValueTask<string> Next(Ping r, CancellationToken ct)
    {
        callCount++;
        return ValueTask.FromResult("ok");
    }

    var result = await LoggingBehavior.Handle<Ping, string>(
        new Ping(), CancellationToken.None, Next);

    Assert.Equal("ok", result);
    Assert.Equal(1, callCount);
}
```

## Testing Discovery with `CSharpCompilation`

Use a real Roslyn compilation in tests — no mocking needed.

```csharp
private static Compilation CreateCompilation(string source)
{
    var syntaxTree = CSharpSyntaxTree.ParseText(source);
    return CSharpCompilation.Create(
        "TestAssembly",
        [syntaxTree],
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IPipelineBehavior).Assembly.Location),
        ],
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
}

[Fact]
public void Discover_LoggingBehavior_ReturnsExpectedInfo()
{
    var source = """
        using ZeroAlloc.Pipeline;
        using System.Threading;
        using System.Threading.Tasks;

        [PipelineBehavior(Order = 1)]
        public class LoggingBehavior : IPipelineBehavior
        {
            public static ValueTask<TResponse> Handle<TRequest, TResponse>(
                TRequest request, CancellationToken ct,
                System.Func<TRequest, CancellationToken, ValueTask<TResponse>> next)
                => next(request, ct);
        }
        """;

    var compilation = CreateCompilation(source);
    var results     = PipelineBehaviorDiscoverer.Discover(compilation).ToList();

    Assert.Single(results);
    Assert.Equal(1,    results[0].Order);
    Assert.Equal(2,    results[0].HandleMethodTypeParameterCount);
    Assert.Null(results[0].AppliesTo);
}
```

## Testing `EmitChain` Output

Assert on the emitted string directly.

```csharp
[Fact]
public void EmitChain_OneBehavior_ContainsExpectedCallSite()
{
    var behaviors = new[]
    {
        new PipelineBehaviorInfo("global::App.LoggingBehavior", order: 1, appliesTo: null, typeParamCount: 2),
    };

    var shape = new PipelineShape
    {
        TypeArguments           = ["global::App.Ping", "string"],
        OuterParameterNames     = ["request", "ct"],
        LambdaParameterPrefixes = ["r", "c"],
        InnermostBodyTemplate   = "{ return handler.Handle(r1, c1); }",
    };

    var result = PipelineEmitter.EmitChain(behaviors, shape);

    Assert.Contains("LoggingBehavior.Handle<global::App.Ping, string>", result);
    Assert.Contains("request, ct",  result);
    Assert.Contains("static (r1, c1)", result);
}
```

## Testing Diagnostic Helpers

```csharp
[Fact]
public void FindMissingHandleMethod_ReturnsBehaviorsWithWrongTypeParamCount()
{
    var behaviors = new[]
    {
        new PipelineBehaviorInfo("global::App.Good", order: 1, appliesTo: null, typeParamCount: 2),
        new PipelineBehaviorInfo("global::App.Bad",  order: 2, appliesTo: null, typeParamCount: 1),
    };

    var invalid = PipelineDiagnosticRules.FindMissingHandleMethod(behaviors, expectedTypeParamCount: 2).ToList();

    Assert.Single(invalid);
    Assert.Equal("global::App.Bad", invalid[0].BehaviorTypeName);
}

[Fact]
public void FindDuplicateOrders_ReturnsGroupsWithMoreThanOneEntry()
{
    var behaviors = new[]
    {
        new PipelineBehaviorInfo("global::App.A", order: 1, appliesTo: null, typeParamCount: 2),
        new PipelineBehaviorInfo("global::App.B", order: 1, appliesTo: null, typeParamCount: 2),
        new PipelineBehaviorInfo("global::App.C", order: 2, appliesTo: null, typeParamCount: 2),
    };

    var dupes = PipelineDiagnosticRules.FindDuplicateOrders(behaviors).ToList();

    Assert.Single(dupes);
    Assert.Equal(1, dupes[0].Key);
    Assert.Equal(2, dupes[0].Count());
}
```

## Rules & Best Practices

- Always use a real `CSharpCompilation` — mock semantic models diverge from real behavior
- Include both `typeof(object).Assembly` and `typeof(IPipelineBehavior).Assembly` in test compilation references
- Assert on `BehaviorTypeName` using the `global::` FQN form — that is what the discoverer returns
- Keep test source strings minimal — only define what the test needs to verify
