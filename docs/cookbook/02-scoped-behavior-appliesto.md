---
id: cookbook-scoped-behavior-appliesto
title: "Cookbook: Scoped Behavior with AppliesTo"
slug: /docs/cookbook/scoped-behavior-appliesto
description: Restrict a behavior to a single request type at compile time using AppliesTo.
sidebar_position: 2
---

# Cookbook: Scoped Behavior with AppliesTo

Restrict a behavior to a single request type using the `AppliesTo` property — resolved at compile time, no runtime branching.

## What We're Building

- An `OrderValidationBehavior` that only runs for `CreateOrderCommand`
- Other request types pass through without the behavior in their chain

## Implementation

```csharp
using ZeroAlloc.Pipeline;

// Only applies to CreateOrderCommand — not included in any other request's generated chain
[PipelineBehavior(Order = 2, AppliesTo = typeof(CreateOrderCommand))]
public class OrderValidationBehavior : IPipelineBehavior
{
    public static async ValueTask<TResponse> Handle<TRequest, TResponse>(
        TRequest request,
        CancellationToken ct,
        Func<TRequest, CancellationToken, ValueTask<TResponse>> next)
        where TRequest : CreateOrderCommand
    {
        if (request.Items.Count == 0)
            throw new InvalidOperationException("Order must have at least one item");

        return await next(request, ct);
    }
}
```

## What Gets Generated

```csharp
// Conceptual — generated chain for CreateOrderCommand (includes OrderValidationBehavior)
return global::App.LoggingBehavior.Handle<global::App.CreateOrderCommand, global::App.Result>(
    request, ct,
    static (r1, c1) =>
        global::App.OrderValidationBehavior.Handle<global::App.CreateOrderCommand, global::App.Result>(
            r1, c1,
            static (r2, c2) =>
                { var h = new CreateOrderHandler(); return h.Handle(r2, c2); }));

// Conceptual — generated chain for GetOrderQuery (OrderValidationBehavior absent)
return global::App.LoggingBehavior.Handle<global::App.GetOrderQuery, global::App.Order>(
    request, ct,
    static (r1, c1) =>
        { var h = new GetOrderHandler(); return h.Handle(r1, c1); });
```

`AppliesTo` filtering happens at compile time — there is no `if (request is CreateOrderCommand)` at runtime.

## Related

- [Pipeline Behaviors](../pipeline-behaviors.md) — `AppliesTo` property reference
- [Pipeline Discoverer](../pipeline-discoverer.md) — how `AppliesTo` is read from the attribute
