---
id: pipeline-shape
title: Pipeline Shape
slug: /docs/pipeline-shape
description: How to describe the delegate contract for PipelineEmitter using PipelineShape.
sidebar_position: 3
---

# Pipeline Shape

`PipelineShape` describes the delegate contract of the pipeline so `PipelineEmitter.EmitChain` can generate the correct nested static lambda call chain. Each generator defines one shape per pipeline type it supports.

## The Record

```csharp
public sealed record PipelineShape
{
    public required string[] TypeArguments           { get; init; }
    public required string[] OuterParameterNames     { get; init; }
    public required string[] LambdaParameterPrefixes { get; init; }
    public string            InnermostBodyTemplate   { get; init; } = string.Empty;
    public Func<int, string>? InnermostBodyFactory   { get; init; }
    public PipelineState?    State                   { get; init; }
    public int               HandleTypeParameterCount { get; }   // TypeArguments + 1 with State
}

public sealed record PipelineState
{
    public required string TypeName              { get; init; }
    public required string Argument              { get; init; }
    public string          LambdaParameterPrefix { get; init; } = "s";
}
```

All properties are `init`-only — a shape is immutable once constructed.

## Properties

### `TypeArguments`

The concrete type arguments for `Handle<...>`. One string per type parameter, fully qualified.

```csharp
// ZeroAlloc.Mediator: Handle<TRequest, TResponse>
TypeArguments = ["global::App.Ping", "string"]

// ZeroAlloc.Validation: Handle<T>
TypeArguments = ["global::App.Order"]
```

### `OuterParameterNames`

The parameter names at the outermost call site — the names that appear in the generated method signature.

```csharp
// Mediator: Send(request, ct)
OuterParameterNames = ["request", "ct"]

// Validation: Validate(instance)
OuterParameterNames = ["instance"]
```

### `LambdaParameterPrefixes`

One prefix letter per outer parameter. The emitter uses `{prefix}{depth}` to name lambda parameters at each nesting level, avoiding name shadowing.

```csharp
// Mediator: r1/c1 at depth 1, r2/c2 at depth 2, ...
LambdaParameterPrefixes = ["r", "c"]

// Validation: r1 at depth 1, r2 at depth 2, ...
LambdaParameterPrefixes = ["r"]
```

### `InnermostBodyTemplate` vs `InnermostBodyFactory`

The innermost body is the non-behavior call — the handler invocation that lives at the deepest nesting level.

Use **`InnermostBodyTemplate`** when the body is a fixed string:

```csharp
InnermostBodyTemplate = "{ var h = new PingHandler(); return h.Handle(r1, c1); }"
```

Use **`InnermostBodyFactory`** when the body needs to embed the lambda parameter names at the correct depth (which is only known after the behavior list is filtered):

```csharp
// The factory receives the resolved behavior count (depth) as an argument
InnermostBodyFactory = depth =>
    $"{{ var h = new PingHandler(); return h.Handle(r{depth}, c{depth}); }}"
```

When both are set, `InnermostBodyFactory` takes precedence.

## Examples

**Mediator shape (2 type args, 2 params):**

```csharp
var shape = new PipelineShape
{
    TypeArguments           = ["global::App.Ping", "string"],
    OuterParameterNames     = ["request", "ct"],
    LambdaParameterPrefixes = ["r", "c"],
    InnermostBodyFactory    = depth =>
        $"{{ var h = new PingHandler(); return h.Handle(r{depth}, c{depth}); }}",
};
```

**Validation shape (1 type arg, 1 param):**

```csharp
var shape = new PipelineShape
{
    TypeArguments           = ["global::App.Order"],
    OuterParameterNames     = ["instance"],
    LambdaParameterPrefixes = ["r"],
    InnermostBodyFactory    = depth =>
        $"{{ return new OrderValidator().Validate(r{depth}); }}",
};
```

### `State`

Every lambda the emitter writes is `static`, so the compiler creates each one once and the chain allocates nothing. A `static` lambda cannot read `this` or a local, so when the innermost body needs instance state, such as a scope's `IServiceProvider` or the validator's nested validators, pass that state through the chain instead of capturing it:

```csharp
State = new PipelineState
{
    TypeName = "global::System.IServiceProvider", // appended to the Handle type arguments
    Argument = "_services",                       // passed at the outermost call
    // LambdaParameterPrefix = "s"                // s1, s2, ... at each level, the default
}
```

The state goes in a fixed position, so one behavior works with every host:

- its type is the **last** type argument of `Handle`;
- the state is the **second** argument, right after the request or model, of both `Handle` and `next`.

```csharp
// The behavior a stateful request/response pipeline calls
public static ValueTask<TResponse> Handle<TRequest, TResponse, TState>(
    TRequest request, TState state, CancellationToken ct,
    Func<TRequest, TState, CancellationToken, ValueTask<TResponse>> next)
    => next(request, state, ct);
```

The innermost body reads the state through its lambda parameter at the deepest level, `s{depth}`. With no behaviors, `EmitChain` returns the body itself after `return`, so the depth-0 body is an expression over the outer names:

```csharp
InnermostBodyFactory = depth => depth == 0
    ? "_services.GetRequiredService<PingHandler>().Handle(request, ct)"
    : $"{{ return s{depth}.GetRequiredService<PingHandler>().Handle(r{depth}, c{depth}); }}",
```

A body that reads instance state without `State` does not compile: the compiler reports CS8821 on the `static` lambda, instead of the chain allocating a delegate per call. Leave `State` null when the body reads no instance state.

`HandleTypeParameterCount` is the number of type parameters a behavior's `Handle` takes in this pipeline, `TypeArguments.Length` plus one with `State`. Pass the shape to `PipelineDiagnosticRules.FindMissingHandleMethod(behaviors, shape)` to check the behaviors against it.

## Rules & Best Practices

- `LambdaParameterPrefixes.Length` must equal `OuterParameterNames.Length`
- Supply either `InnermostBodyTemplate` or `InnermostBodyFactory` — not both, not neither
- Use `InnermostBodyFactory` when the body references lambda parameter names (the common case)
- Type arguments must be fully qualified (`global::` prefix) to be safe in any namespace context
- `State.LambdaParameterPrefix` must differ from every prefix in `LambdaParameterPrefixes` (throws `ArgumentException` if not)

## Common Pitfalls

**Pitfall 1 — Template instead of factory for depth-sensitive bodies**

```csharp
// ❌ Hardcodes depth 1 — breaks when 2+ behaviors are applied
InnermostBodyTemplate = "{ return h.Handle(r1, c1); }"

// ✅ Use factory so depth is correct regardless of behavior count
InnermostBodyFactory = depth => $"{{ return h.Handle(r{depth}, c{depth}); }}"
```

**Pitfall 2 — Prefix count mismatch**

```csharp
// ❌ 2 outer params but only 1 prefix — emitter will generate wrong lambda signatures
OuterParameterNames     = ["request", "ct"],
LambdaParameterPrefixes = ["r"],            // missing "c"

// ✅ One prefix per outer parameter
OuterParameterNames     = ["request", "ct"],
LambdaParameterPrefixes = ["r", "c"],
```
