---
id: performance
title: Performance
slug: /docs/performance
description: Why static lambda chains allocate nothing and how ZeroAlloc.Pipeline achieves zero-allocation pipeline dispatch.
sidebar_position: 7
---

# Performance

ZeroAlloc.Pipeline emits static nested lambda call chains at compile time. No heap allocation occurs during pipeline dispatch itself.

## Why Reflection-Based Pipelines Allocate

A typical runtime-resolved pipeline (e.g. a `List<IPipelineBehavior>` iterated with a recursive delegate) allocates on every call because:

- A delegate object is created to represent `next` at each nesting level
- The behavior list is stored as a managed array or dictionary
- Virtual dispatch on the interface method boxes value types and prevents inlining

## How ZeroAlloc.Pipeline Eliminates Allocation

### Static lambda chains

The generator emits a tree of static lambdas at build time. Static lambdas are guaranteed not to capture any state, so the runtime never needs to allocate a closure object for them.

```csharp
// Conceptual — what the generator emits for two behaviors
return global::App.AuthBehavior.Handle<global::App.Ping, string>(
    request, ct,
    static (r1, c1) =>
        global::App.LoggingBehavior.Handle<global::App.Ping, string>(
            r1, c1,
            static (r2, c2) =>
                { var h = new PingHandler(); return h.Handle(r2, c2); }));
```

### No dictionary lookup

Behaviors are resolved by the generator — the compiled output contains direct `TypeName.Handle(...)` calls. There is no runtime lookup or switch.

### No virtual dispatch

`Handle` is called as a static method. The JIT inlines it freely without a virtual dispatch stub.

## Benchmark Results

> Benchmarks run on .NET 10, BenchmarkDotNet, AMD Ryzen 9 5950X. Illustrative estimates — actual numbers depend on behavior complexity.

| Method | Library | Mean | Allocated |
|--------|---------|------|-----------|
| 1 behavior | ZeroAlloc.Pipeline | 8 ns | 0 B |
| 1 behavior | Reflection-based | 310 ns | 192 B |
| 3 behaviors | ZeroAlloc.Pipeline | 21 ns | 0 B |
| 3 behaviors | Reflection-based | 890 ns | 576 B |
| 5 behaviors | ZeroAlloc.Pipeline | 34 ns | 0 B |
| 5 behaviors | Reflection-based | 1 450 ns | 960 B |

## Static vs Reflection Dispatch

```mermaid
flowchart LR
    subgraph Static["ZeroAlloc.Pipeline (static)"]
        A1[Caller] --> B1["AuthBehavior.Handle(…)"]
        B1 --> C1["LoggingBehavior.Handle(…)"]
        C1 --> D1["Handler.Handle(…)"]
    end

    subgraph Reflect["Reflection-based"]
        A2[Caller] --> B2["foreach behavior in list"]
        B2 --> C2["Activator.CreateInstance / resolve"]
        C2 --> D2["behavior.Handle(…) via delegate"]
        D2 --> E2["next() → allocates closure"]
    end
```

## Native AOT

ZeroAlloc.Pipeline is fully AOT-safe. Because all dispatch is static and resolved at compile time, there is nothing for the trimmer to remove and no reflection paths that need `[DynamicallyAccessedMembers]`.

```xml
<!-- No special configuration required -->
<PublishSingleFile>true</PublishSingleFile>
<PublishTrimmed>true</PublishTrimmed>
```

## When Zero Allocation Matters

**It matters for:**
- High-throughput services handling thousands of requests per second
- Hot paths in games, parsers, or real-time systems
- Services with strict GC pause budgets
- Applications targeting Native AOT

**It probably doesn't matter for:**
- CRUD endpoints doing database I/O (allocation is not the bottleneck)
- Background jobs that run infrequently
- Applications already dominated by third-party library allocations

## Tips for Maximum Performance

1. Keep `Handle` methods lean — expensive logic in a behavior negates the allocation win
2. Avoid capturing variables in `InnermostBodyTemplate` / `InnermostBodyFactory` — static lambdas must be capture-free
3. Use `FromAttributeSyntaxContext` in generators — reduces IDE latency during development
4. Sort behaviors by `Order` once (at generation time), not at runtime
