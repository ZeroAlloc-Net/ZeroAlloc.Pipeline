---
id: pipeline-discoverer
title: Pipeline Discoverer
slug: /docs/pipeline-discoverer
description: How PipelineBehaviorDiscoverer finds behaviors in a Roslyn compilation at compile time.
sidebar_position: 5
---

# Pipeline Discoverer

`PipelineBehaviorDiscoverer` locates every class in a Roslyn compilation that implements `IPipelineBehavior` and carries a `[PipelineBehaviorAttribute]` (or a subclass of it). It returns a `PipelineBehaviorInfo` per match.

## Two Entry Points

### `FromAttributeSyntaxContext` — preferred for incremental generators

```csharp
public static PipelineBehaviorInfo? FromAttributeSyntaxContext(GeneratorAttributeSyntaxContext ctx)
```

Use this with `IIncrementalGenerator` and `ForAttributeWithMetadataName`. Roslyn caches and diffs at the syntax level, so only changed classes are re-processed on each keystroke. This is the production-ready path.

```csharp
context.SyntaxProvider
    .ForAttributeWithMetadataName(
        "ZeroAlloc.Pipeline.PipelineBehaviorAttribute",
        predicate: (node, _) => node is ClassDeclarationSyntax,
        transform: (ctx, _) => PipelineBehaviorDiscoverer.FromAttributeSyntaxContext(ctx))
    .Where(info => info != null)
    .Select((info, _) => info!);
```

### `Discover` — for tests and one-shot tools

```csharp
public static IEnumerable<PipelineBehaviorInfo> Discover(Compilation compilation)
```

Scans the entire compilation. Use in test helpers and stand-alone analysis tools. Do not use in a hot generator path — it re-scans all syntax trees on every call.

## `PipelineBehaviorInfo`

```csharp
public sealed class PipelineBehaviorInfo
{
    public string  BehaviorTypeName               { get; } // e.g. "global::App.LoggingBehavior"
    public int     Order                          { get; }
    public string? AppliesTo                      { get; } // FQN or null
    public int     HandleMethodTypeParameterCount { get; } // -1 if no Handle found
}
```

`HasValidHandleMethod(int expected)` returns `true` when `HandleMethodTypeParameterCount == expected`.

## Attribute Subclass Detection

The discoverer performs a two-pass attribute resolution:

1. **Semantic pass** — checks whether the attribute class or any of its base types matches `ZeroAlloc.Pipeline.PipelineBehaviorAttribute` by FQN. This covers all normal compilations.
2. **Syntax fallback** — when the attribute class resolves as `TypeKind.Error` (incomplete compilation, missing references), the discoverer searches all syntax trees by class name to find a locally-defined subclass. This is primarily relevant in test compilations created without the full `ZeroAlloc.Pipeline` assembly reference.

## `AppliesTo` Resolution

`AppliesTo` is read from the attribute's named argument `AppliesTo = typeof(X)`. The discoverer returns the fully qualified type name (e.g. `"global::App.CreateOrderCommand"`) or `null` if the argument is absent.

Filter behavior lists by `AppliesTo` before passing them to `PipelineEmitter.EmitChain`:

```csharp
var applicable = behaviors
    .Where(b => b.AppliesTo == null || b.AppliesTo == requestTypeFqn)
    .OrderBy(b => b.Order)
    .ToList();
```

## Rules & Best Practices

- Use `FromAttributeSyntaxContext` in all `IIncrementalGenerator` implementations
- Use `Discover` only in tests or one-shot tools
- Always filter by `AppliesTo` before emitting
- Check `HandleMethodTypeParameterCount` with `FindMissingHandleMethod` before emitting — invalid behaviors should produce a diagnostic, not a compile error in the generated code

## Common Pitfalls

**Pitfall 1 — Using `Discover` in a generator hot path**

```csharp
// ❌ Re-scans the whole compilation on every change
void Initialize(IncrementalGeneratorInitializationContext context)
{
    var behaviors = context.CompilationProvider
        .Select((comp, _) => PipelineBehaviorDiscoverer.Discover(comp).ToList());
}

// ✅ Use ForAttributeWithMetadataName + FromAttributeSyntaxContext
context.SyntaxProvider
    .ForAttributeWithMetadataName("ZeroAlloc.Pipeline.PipelineBehaviorAttribute", ...)
    .Select((ctx, _) => PipelineBehaviorDiscoverer.FromAttributeSyntaxContext(ctx));
```

**Pitfall 2 — Forgetting to filter by `AppliesTo`**

```csharp
// ❌ Scoped behaviors included for every request type
string chain = PipelineEmitter.EmitChain(behaviors, shape);

// ✅ Filter first
var applicable = behaviors
    .Where(b => b.AppliesTo == null || b.AppliesTo == requestTypeFqn)
    .OrderBy(b => b.Order)
    .ToList();
string chain = PipelineEmitter.EmitChain(applicable, shape);
```
