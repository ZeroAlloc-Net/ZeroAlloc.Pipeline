---
id: pipeline-emitter
title: Pipeline Emitter
slug: /docs/pipeline-emitter
description: How PipelineEmitter.EmitChain builds a nested static lambda call chain from a behavior list and a PipelineShape.
sidebar_position: 4
---

# Pipeline Emitter

`PipelineEmitter.EmitChain` takes a pre-filtered, pre-sorted list of behaviors and a `PipelineShape` and returns the C# source string for the nested static lambda call chain. Paste the result after `return ` in your generated method body.

## Signature

```csharp
public static string EmitChain(
    IReadOnlyList<PipelineBehaviorInfo> behaviors,
    PipelineShape shape)
```

`behaviors` must be:
1. Already filtered by `AppliesTo` for the request type being generated
2. Already sorted by `Order` ascending
3. Non-null (throws `ArgumentNullException`)

`shape` must have all three `required` properties set and a non-empty body source (throws `ArgumentException` if not).

## Example

**Input:**

```csharp
var behaviors = new[]
{
    new PipelineBehaviorInfo("global::App.AuthBehavior",       order: 1, appliesTo: null, typeParamCount: 2),
    new PipelineBehaviorInfo("global::App.LoggingBehavior",    order: 2, appliesTo: null, typeParamCount: 2),
    new PipelineBehaviorInfo("global::App.ValidationBehavior", order: 3, appliesTo: null, typeParamCount: 2),
};

var shape = new PipelineShape
{
    TypeArguments           = ["global::App.Ping", "string"],
    OuterParameterNames     = ["request", "ct"],
    LambdaParameterPrefixes = ["r", "c"],
    InnermostBodyFactory    = depth =>
        $"{{ var h = new PingHandler(); return h.Handle(r{depth}, c{depth}); }}",
};

string chain = PipelineEmitter.EmitChain(behaviors, shape);
```

**Output:**

```csharp
// Conceptual — what the generator emits for the chain above
global::App.AuthBehavior.Handle<global::App.Ping, string>(
    request, ct,
    static (r1, c1) =>
        global::App.LoggingBehavior.Handle<global::App.Ping, string>(
            r1, c1,
            static (r2, c2) =>
                global::App.ValidationBehavior.Handle<global::App.Ping, string>(
                    r2, c2,
                    static (r3, c3) =>
                        { var h = new PingHandler(); return h.Handle(r3, c3); })))
```

## Nesting Depth

Each behavior adds one nesting level. The lambda parameter names increment with the depth so inner lambdas never shadow outer ones.

```mermaid
flowchart TB
    A["EmitChain(behaviors, shape)"]
    B["Behavior 0 (Order=1)\nHandle&lt;T&gt;(request, ct, next)"]
    C["Behavior 1 (Order=2)\nstatic (r1,c1) => Handle&lt;T&gt;(r1,c1,next)"]
    D["Behavior 2 (Order=3)\nstatic (r2,c2) => Handle&lt;T&gt;(r2,c2,next)"]
    E["Innermost\nstatic (r3,c3) => { handler.Handle(r3,c3) }"]

    A --> B --> C --> D --> E
```

## Zero Behaviors

When `behaviors` is empty, `EmitChain` returns the innermost body directly — no wrapping lambda.

```csharp
var result = PipelineEmitter.EmitChain([], shape);
// Returns: "{ var h = new PingHandler(); return h.Handle(r0, c0); }"
```

## Instance State: `EmitCachedChain`

When the innermost body reads instance state, such as an injected `IServiceProvider` or a nested validator held in a field, the lambdas cannot be `static`, so the shape sets `EmitStaticLambdas = false`. The compiler does not cache a lambda that captures `this`, so `EmitChain` then allocates one delegate per behavior on every call.

`EmitCachedChain` emits the same chain but keeps each level's `next` delegate in an instance field, created on first use:

```csharp
public static PipelineCachedChain EmitCachedChain(
    IReadOnlyList<PipelineBehaviorInfo> behaviors,
    PipelineShape shape,
    string nextDelegateType,   // type of the behaviors' next parameter, fully qualified
    string cacheFieldPrefix)   // unique per chain in the containing type
```

```csharp
var chain = PipelineEmitter.EmitCachedChain(
    behaviors,
    shape,
    "global::System.Func<global::App.Ping, global::System.Threading.CancellationToken, global::System.Threading.Tasks.ValueTask<string>>",
    "__sendPingNext");

sb.AppendLine($"        return {chain.Expression};");   // inside an instance method
foreach (var member in chain.MemberDeclarations)          // at class scope
    sb.AppendLine($"    {member}");
```

With two behaviors this gives:

```csharp
return global::App.Outer.Handle<global::App.Ping, string>(
    request, ct, __sendPingNext1 ??= (r1, c1) =>
    global::App.Inner.Handle<global::App.Ping, string>(
        r1, c1, __sendPingNext2 ??= (r2, c2) =>
        { return _services.Handle(r2, c2); }));

private global::System.Func<global::App.Ping, global::System.Threading.CancellationToken, global::System.Threading.Tasks.ValueTask<string>>? __sendPingNext1;
private global::System.Func<global::App.Ping, global::System.Threading.CancellationToken, global::System.Threading.Tasks.ValueTask<string>>? __sendPingNext2;
```

After the first call the chain allocates nothing. Notes:

- The lambdas read the next level's field, so they are never `static`; `EmitStaticLambdas` is ignored.
- The declarations are annotated nullable; emit them in a `#nullable enable` context.
- Two threads making the first call together may each create a delegate. They are equivalent, and one is kept.
- A chain whose body reads no instance state does not need this: `EmitChain` with static lambdas already allocates nothing.

`EmitChain` itself is unchanged.

## Rules & Best Practices

- Sort by `Order` **before** calling `EmitChain` — the emitter does not sort
- Filter by `AppliesTo` **before** calling `EmitChain`
- Use `InnermostBodyFactory` (not `InnermostBodyTemplate`) unless the body truly has no depth-dependent parameter names
- Place the result after `return ` in the generated method, not as a statement

## Common Pitfalls

**Pitfall 1 — Unsorted behaviors**

```csharp
// ❌ Behaviors passed in arbitrary order — chain runs in wrong sequence
var behaviors = GetBehaviors(); // unsorted

// ✅ Sort ascending by Order before emitting
var behaviors = GetBehaviors().OrderBy(b => b.Order).ToList();
string chain  = PipelineEmitter.EmitChain(behaviors, shape);
```

**Pitfall 2 — Not filtering by AppliesTo**

```csharp
// ❌ Scoped behaviors applied to every request type
var behaviors = allBehaviors;

// ✅ Filter: include global behaviors + those scoped to this specific type
var behaviors = allBehaviors
    .Where(b => b.AppliesTo == null || b.AppliesTo == requestTypeFqn)
    .OrderBy(b => b.Order)
    .ToList();
```
