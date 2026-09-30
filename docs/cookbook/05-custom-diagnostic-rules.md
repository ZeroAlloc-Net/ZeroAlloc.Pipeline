---
id: cookbook-custom-diagnostic-rules
title: "Cookbook: Custom Diagnostic Rules"
slug: /docs/cookbook/custom-diagnostic-rules
description: Report ZAPxxx-style diagnostics from your own generator using PipelineDiagnosticRules.
sidebar_position: 5
---

# Cookbook: Custom Diagnostic Rules

Report `ZAPxxx`-style diagnostics from your own generator using `PipelineDiagnosticRules`.

## What We're Building

- Three `DiagnosticDescriptor` objects (one Error, two Warnings)
- Integration with `PipelineDiagnosticRules.FindMissingHandleMethod`, `FindDuplicateOrders` and `FindMissingPipelineBehaviorInterface`
- Correct locations so the squiggle appears under the offending class attribute

## DiagnosticDescriptor Setup

Define your codes in a static class:

```csharp
internal static class Diagnostics
{
    private const string Category = "MyGenerator";

    // ZAP001-equivalent for your generator
    public static readonly DiagnosticDescriptor MissingHandle = new(
        id:                 "ZAP001",
        title:              "Missing or invalid Handle method",
        messageFormat:      "'{0}' does not have a public static Handle method with {1} type parameter(s)",
        category:           Category,
        defaultSeverity:    DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // ZAP002-equivalent for your generator
    public static readonly DiagnosticDescriptor DuplicateOrder = new(
        id:                 "ZAP002",
        title:              "Duplicate pipeline behavior Order",
        messageFormat:      "Order {0} is used by more than one behavior ('{1}')",
        category:           Category,
        defaultSeverity:    DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    // ZAP003-equivalent for your generator. A Warning, so code that compiled before still does.
    public static readonly DiagnosticDescriptor MissingInterface = new(
        id:                 "ZAP003",
        title:              "Pipeline behavior does not implement IPipelineBehavior",
        messageFormat:      "'{0}' has [PipelineBehavior] but does not implement IPipelineBehavior, so it never runs; {1}",
        category:           Category,
        defaultSeverity:    DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
```

## Reporting in the Generator

```csharp
// Inside your SourceProductionContext callback:

// ZAP001 — missing Handle
var invalid = PipelineDiagnosticRules.FindMissingHandleMethod(behaviors, expectedTypeParamCount: 2);
foreach (var b in invalid)
{
    spc.ReportDiagnostic(Diagnostic.Create(
        Diagnostics.MissingHandle,
        GetLocation(b.BehaviorTypeName, compilation),  // see helper below
        b.BehaviorTypeName,
        2));
}

// ZAP002 — duplicate Order
foreach (var group in PipelineDiagnosticRules.FindDuplicateOrders(behaviors))
    foreach (var b in group)
        spc.ReportDiagnostic(Diagnostic.Create(
            Diagnostics.DuplicateOrder,
            GetLocation(b.BehaviorTypeName, compilation),
            b.Order,
            b.BehaviorTypeName));

// ZAP003 — [PipelineBehavior] without the interface. The candidates come from
// PipelineBehaviorDiscoverer.CandidateFromAttributeSyntaxContext, which also returns the types
// that FromAttributeSyntaxContext drops.
foreach (var c in PipelineDiagnosticRules.FindMissingPipelineBehaviorInterface(candidates))
    spc.ReportDiagnostic(Diagnostic.Create(
        Diagnostics.MissingInterface,
        GetLocation(c.BehaviorTypeName, compilation),
        c.BehaviorTypeName,
        c.IsStatic ? "make the class non-static and implement IPipelineBehavior" : "implement IPipelineBehavior"));
```

## Getting the Source Location

```csharp
private static Location GetLocation(string behaviorTypeName, Compilation compilation)
{
    // Find the class declaration to attach the diagnostic to the attribute, not line 1
    foreach (var tree in compilation.SyntaxTrees)
    {
        var model = compilation.GetSemanticModel(tree);
        var classDecl = tree.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => model.GetDeclaredSymbol(c)
                ?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == behaviorTypeName);

        if (classDecl != null)
            return classDecl.GetLocation();
    }
    return Location.None;
}
```

## Related

- [Diagnostics](../diagnostics.md) — ZAP001 to ZAP003 reference
- [Cookbook: Build a Pipeline Generator](04-build-a-pipeline-generator.md)
