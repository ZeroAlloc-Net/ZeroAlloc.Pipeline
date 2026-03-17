---
id: docs-index
title: ZeroAlloc.Pipeline Docs
slug: /docs
description: Reference and cookbook documentation for ZeroAlloc.Pipeline.
sidebar_position: 0
---

# ZeroAlloc.Pipeline Documentation

Shared building block for pipeline-aware Roslyn source generators.

## Reference

| # | Guide | Description |
|---|-------|-------------|
| 1 | [Getting Started](getting-started.md) | Write your first behavior in 4 steps |
| 2 | [Pipeline Behaviors](pipeline-behaviors.md) | `IPipelineBehavior`, `[PipelineBehavior]`, Order, AppliesTo |
| 3 | [Pipeline Shape](pipeline-shape.md) | Describe the delegate shape for code generation |
| 4 | [Pipeline Emitter](pipeline-emitter.md) | Emit a nested static lambda call chain |
| 5 | [Pipeline Discoverer](pipeline-discoverer.md) | Discover behaviors at compile time |
| 6 | [Diagnostics](diagnostics.md) | ZAP001 and ZAP002 reference |
| 7 | [Performance](performance.md) | Zero-allocation pipeline dispatch |
| 8 | [Testing](testing.md) | Test behaviors and generators |

## Cookbook

| # | Recipe | Scenario |
|---|--------|----------|
| 1 | [Logging Behavior](cookbook/01-logging-behavior.md) | Add structured logging to all pipeline calls |
| 2 | [Scoped Behavior with AppliesTo](cookbook/02-scoped-behavior-appliesto.md) | Restrict a behavior to one request type |
| 3 | [Ordered Behavior Chain](cookbook/03-ordered-behavior-chain.md) | Compose auth + logging + validation in order |
| 4 | [Build a Pipeline Generator](cookbook/04-build-a-pipeline-generator.md) | Wire Discoverer + Emitter in an IIncrementalGenerator |
| 5 | [Custom Diagnostic Rules](cookbook/05-custom-diagnostic-rules.md) | Report ZAPxxx diagnostics from your own generator |

## Quick Reference

```csharp
// Define a behavior
[PipelineBehavior(Order = 1)]
public class MyBehavior : IPipelineBehavior
{
    public static TResult Handle<T, TResult>(T input, Func<T, TResult> next) => next(input);
}

// Scope to one type
[PipelineBehavior(Order = 2, AppliesTo = typeof(CreateOrderCommand))]
public class OrderBehavior : IPipelineBehavior { ... }

// In a generator: discover behaviors
var behaviors = PipelineBehaviorDiscoverer.Discover(compilation).ToList();
// or (preferred, incremental):
context.SyntaxProvider
    .ForAttributeWithMetadataName("ZeroAlloc.Pipeline.PipelineBehaviorAttribute", ...)
    .Select((ctx, _) => PipelineBehaviorDiscoverer.FromAttributeSyntaxContext(ctx));

// Check for issues
var invalid = PipelineDiagnosticRules.FindMissingHandleMethod(behaviors, expectedTypeParamCount: 2);
var dupes   = PipelineDiagnosticRules.FindDuplicateOrders(behaviors);

// Emit the chain
var shape = new PipelineShape
{
    TypeArguments           = ["global::App.Ping", "string"],
    OuterParameterNames     = ["request", "ct"],
    LambdaParameterPrefixes = ["r", "c"],
    InnermostBodyTemplate   = "{ var h = new PingHandler(); return h.Handle(r1, c1); }",
};
string chain = PipelineEmitter.EmitChain(behaviors, shape);
```
