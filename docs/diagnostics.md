---
id: diagnostics
title: Diagnostics
slug: /docs/diagnostics
description: Reference for the ZAP001 to ZAP003 diagnostic rules provided by PipelineDiagnosticRules.
sidebar_position: 6
---

# Diagnostics

`PipelineDiagnosticRules` provides three reusable rule helpers. They return the offending `PipelineBehaviorInfo` or `PipelineBehaviorCandidateInfo` entries — your generator maps them to actual Roslyn `Diagnostic` objects using your own diagnostic IDs (e.g. `ZAM005`, `ZV005`).

## Diagnostic Reference

| Code | Severity | Title | When it fires |
|------|----------|-------|---------------|
| ZAP001 | Error | Missing or invalid Handle method | Behavior has no `public static Handle` with the expected number of type parameters |
| ZAP002 | Warning | Duplicate Order value | Two or more behaviors share the same `Order` |
| ZAP003 | Warning | Pipeline behavior does not implement `IPipelineBehavior` | A type has `[PipelineBehavior]` but does not implement `IPipelineBehavior`, so it never runs |

---

## ZAP001 — Missing or Invalid Handle Method

### What it means

The discoverer found a class decorated with `[PipelineBehavior]` and implementing `IPipelineBehavior`, but no `public static Handle` method with the right number of type parameters was found (including base classes).

### Example that triggers ZAP001

```csharp
// ❌ Handle method has only 1 type parameter; framework expects 2
[PipelineBehavior(Order = 1)]
public class BadBehavior : IPipelineBehavior
{
    public static ValueTask<string> Handle<TRequest>(
        TRequest request,
        CancellationToken ct,
        Func<TRequest, CancellationToken, ValueTask<string>> next)
        => next(request, ct);
}
```

### Fix

```csharp
// ✅ Match the host framework's expected type parameter count
[PipelineBehavior(Order = 1)]
public class GoodBehavior : IPipelineBehavior
{
    public static ValueTask<TResponse> Handle<TRequest, TResponse>(
        TRequest request,
        CancellationToken ct,
        Func<TRequest, CancellationToken, ValueTask<TResponse>> next)
        => next(request, ct);
}
```

### Checking in your generator

```csharp
var invalid = PipelineDiagnosticRules.FindMissingHandleMethod(behaviors, expectedTypeParamCount: 2);
foreach (var b in invalid)
{
    context.ReportDiagnostic(Diagnostic.Create(
        MissingHandleDescriptor,  // your DiagnosticDescriptor
        location,
        b.BehaviorTypeName));
}
```

---

## ZAP002 — Duplicate Order Value

### What it means

Two or more behaviors in the same pipeline share the same `Order` value. Execution order within the tie is undefined and generator-dependent.

### Example that triggers ZAP002

```csharp
// ❌ Both behaviors have Order = 1
[PipelineBehavior(Order = 1)] public class AuthBehavior    : IPipelineBehavior { ... }
[PipelineBehavior(Order = 1)] public class LoggingBehavior : IPipelineBehavior { ... }
```

### Fix

```csharp
// ✅ Unique Order values
[PipelineBehavior(Order = 1)] public class AuthBehavior    : IPipelineBehavior { ... }
[PipelineBehavior(Order = 2)] public class LoggingBehavior : IPipelineBehavior { ... }
```

### Checking in your generator

```csharp
var dupeGroups = PipelineDiagnosticRules.FindDuplicateOrders(behaviors);
foreach (var group in dupeGroups)
{
    foreach (var b in group)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            DuplicateOrderDescriptor,  // your DiagnosticDescriptor
            location,
            b.Order,
            b.BehaviorTypeName));
    }
}
```

---

## ZAP003 — Pipeline Behavior Does Not Implement IPipelineBehavior

### What it means

A type carries `[PipelineBehavior]` but does not implement `IPipelineBehavior` or a sub-interface of it. Discovery leaves it out of the pipeline, so its `Handle` method is never called. It is a warning rather than an error so that code which compiled before keeps compiling, but the behavior does nothing until it is fixed.

### Examples that trigger ZAP003

```csharp
// ❌ A static class cannot implement an interface, so it can never be a behavior
[PipelineBehavior(Order = 0)]
public static class LoggingBehavior
{
    public static ValueTask<TResponse> Handle<TRequest, TResponse>(
        TRequest request,
        CancellationToken ct,
        Func<TRequest, CancellationToken, ValueTask<TResponse>> next)
        => next(request, ct);
}

// ❌ The interface is missing
[PipelineBehavior(Order = 1)]
public class ValidationBehavior
{
    public static ValueTask<TResponse> Handle<TRequest, TResponse>(
        TRequest request,
        CancellationToken ct,
        Func<TRequest, CancellationToken, ValueTask<TResponse>> next)
        => next(request, ct);
}
```

### Fix

```csharp
// ✅ A non-static class that implements IPipelineBehavior. Handle stays static.
[PipelineBehavior(Order = 0)]
public class LoggingBehavior : IPipelineBehavior
{
    public static ValueTask<TResponse> Handle<TRequest, TResponse>(
        TRequest request,
        CancellationToken ct,
        Func<TRequest, CancellationToken, ValueTask<TResponse>> next)
        => next(request, ct);
}
```

### Checking in your generator

Discover candidates with `CandidateFromAttributeSyntaxContext`, which, unlike `FromAttributeSyntaxContext`, also returns the types without the interface. `IsStatic` lets the message say what to change.

```csharp
var invalid = PipelineDiagnosticRules.FindMissingPipelineBehaviorInterface(candidates);
foreach (var c in invalid)
{
    context.ReportDiagnostic(Diagnostic.Create(
        MissingInterfaceDescriptor,  // your DiagnosticDescriptor, DiagnosticSeverity.Warning
        location,
        c.BehaviorTypeName,
        c.IsStatic ? "make the class non-static and implement IPipelineBehavior" : "implement IPipelineBehavior"));
}
```

---

## Suppressing Warnings

> **Note:** `ZAP001` to `ZAP003` are the reference codes used throughout this documentation. The actual diagnostic ID emitted to the user depends on the host generator (e.g. ZeroAlloc.Mediator may emit `ZAM001`). Check your generator's documentation for the exact codes to suppress.

Using `#pragma`:

```csharp
#pragma warning disable ZAP002
[PipelineBehavior(Order = 1)] public class AuthBehavior    : IPipelineBehavior { ... }
[PipelineBehavior(Order = 1)] public class LoggingBehavior : IPipelineBehavior { ... }
#pragma warning restore ZAP002
```

Using `.csproj`:

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);ZAP002</NoWarn>
</PropertyGroup>
```

Note: ZAP001 is an error and cannot be suppressed with `NoWarn` alone without addressing the root cause.
