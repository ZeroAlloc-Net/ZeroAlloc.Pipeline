# Documentation Design: ZeroAlloc.Pipeline (2026-03-17)

## Context

ZeroAlloc.Pipeline is a shared building block for pipeline-aware Roslyn source generators
(ZeroAlloc.Mediator, ZeroAlloc.Validation, etc.). It provides:
- `IPipelineBehavior` marker interface
- `PipelineBehaviorAttribute` (Order, AppliesTo)
- `PipelineBehaviorDiscoverer` — compile-time discovery
- `PipelineShape` + `PipelineEmitter` — static nested-lambda code generation
- `PipelineDiagnosticRules` — reusable validation helpers

Documentation targets **both audiences**:
- End users: developers writing behaviors consumed by ZeroAlloc.Mediator / Validation
- Generator authors: developers building their own pipeline-aware source generators

**Strategy:** Single flat docs tree, no audience gating. Concept pages (behaviors, AppliesTo,
ordering) are naturally end-user-facing. Generator-author pages (Discoverer, Emitter, Shape)
appear at the end of the sidebar under natural grouping. Cookbook covers both audiences via
recipe naming.

---

## README.md Structure

Mirrors ZeroAlloc.Mediator's README exactly:

1. Title + badges (NuGet version, CI, MIT license)
2. One-paragraph description — shared building block, no reflection, compile-time generation
3. `## Install` — `dotnet add package` + `.csproj` PackageReference with OutputItemType=Analyzer
4. `## Example` — write a behavior, decorate with `[PipelineBehavior(Order=1)]`, picked up automatically
5. `## Performance` — bold claim, benchmark table (static lambda chain vs reflection), link to `docs/performance.md`
6. `## Features` — bulleted: IPipelineBehavior, PipelineBehaviorAttribute, compile-time discovery,
   static emitter, diagnostic rules, zero allocation, netstandard2.0, Native AOT
7. `## Documentation` — two-column table (Page | Description), links to all docs pages
8. `## License` — MIT

---

## docs/ Folder Structure

```
docs/
  README.md                         sidebar_position: 0  — index / navigation hub
  getting-started.md                sidebar_position: 1
  pipeline-behaviors.md             sidebar_position: 2
  pipeline-shape.md                 sidebar_position: 3
  pipeline-emitter.md               sidebar_position: 4
  pipeline-discoverer.md            sidebar_position: 5
  diagnostics.md                    sidebar_position: 6
  performance.md                    sidebar_position: 7
  testing.md                        sidebar_position: 8
  cookbook/
    01-logging-behavior.md          end-user
    02-scoped-behavior-appliesto.md end-user
    03-ordered-behavior-chain.md    end-user
    04-build-a-pipeline-generator.md generator-author
    05-custom-diagnostic-rules.md   generator-author
```

---

## Frontmatter Convention (matches ZeroAlloc.Mediator exactly)

```yaml
---
id: <kebab-case-id matching filename>
title: <Title Case>
slug: /docs/<id>   (getting-started uses slug: /)
description: <One sentence ending with a period.>
sidebar_position: <integer>
---
```

---

## Per-Page Content Outlines

### docs/README.md — Index

- Frontmatter: id=docs-index, slug=/docs, sidebar_position=0
- H1 + one-line tagline
- `## Reference` — numbered table: #, Guide, Description (8 rows)
- `## Cookbook` — numbered table: #, Recipe, Scenario (5 rows)
- `## Quick Reference` — single code block showing all core APIs side by side

---

### getting-started.md

- Install section (bash)
- "Your First Behavior" — 4 steps: define class, add IPipelineBehavior, decorate with
  [PipelineBehavior], implement static Handle
- "What Gets Generated" — conceptual generated code block
- Architecture Overview — Mermaid sequenceDiagram (behavior → pipeline → handler)
- Key Concepts bullets
- Next Steps table

---

### pipeline-behaviors.md

- `IPipelineBehavior` marker — what it means, why it's a marker (no contract on the interface)
- `PipelineBehaviorAttribute` — Order property, AppliesTo property
- Subclassing the attribute (framework alias pattern, e.g. MediatorPipelineBehaviorAttribute)
- Execution order diagram (Mermaid flowchart showing Order 1, 2, 3 wrapping)
- Rules & Best Practices
- Common Pitfalls: wrong Handle signature, non-static Handle, missing IPipelineBehavior,
  duplicate Order values

---

### pipeline-shape.md

- What a shape is — the delegate contract passed to EmitChain
- Each `required` property explained with code:
  - TypeArguments
  - OuterParameterNames
  - LambdaParameterPrefixes
  - InnermostBodyTemplate vs InnermostBodyFactory
- Example shapes: mediator pattern (2 type args) vs validation pattern (1 type arg)
- Pitfalls: mismatched prefix count, empty InnermostBodyTemplate with no factory

---

### pipeline-emitter.md

- EmitChain signature + parameters
- Example: input (behaviors list + shape) → output (generated C# string), shown verbatim
- Nesting depth — Mermaid flowchart showing how each behavior wraps the next
- Zero-behaviors edge case (returns InnermostBodyTemplate directly)
- Pitfalls: unsorted behaviors, wrong nesting expectations

---

### pipeline-discoverer.md

- `Discover` vs `FromAttributeSyntaxContext` — when to use each (one-shot vs incremental)
- `PipelineBehaviorInfo` fields: BehaviorTypeName, Order, AppliesTo, HandleMethodTypeParameterCount
- Attribute subclass detection (how the two-pass resolution works)
- Incomplete compilation fallback (TypeKind.Error path, test-time relevance)
- Pitfalls: using Discover in a hot path, forgetting to filter by AppliesTo

---

### diagnostics.md

- Diagnostic Reference Table: Code, Severity, Title, When it fires
  - ZAP001 — Error — Missing Handle method — behavior has wrong/missing Handle signature
  - ZAP002 — Warning — Duplicate Order — two behaviors share the same Order value
- One section per rule: What it means, triggering example (❌), fix (✅), common traps
- Suppressing Warnings: `#pragma warning disable` and `.csproj` NoWarn

---

### performance.md

- Why reflection-based pipelines allocate (virtual dispatch, boxing, dictionary lookup)
- How static lambda chains eliminate allocation (compile-time wiring, no dictionaries)
- Benchmark table: Method, Mean, Allocated — static chain vs delegate vs reflection
- Mermaid comparison: static dispatch vs reflection dispatch side by side
- Native AOT — why static approach is AOT-safe, trimming notes
- When it matters / When it doesn't
- Tips for maximum performance

---

### testing.md

- Testing behaviors in isolation (pure unit test, no generator needed)
- Testing generators with CSharpCompilation (CreateCompilation helper pattern from our tests)
- Asserting on PipelineBehaviorInfo (Order, AppliesTo, HandleMethodTypeParameterCount)
- Asserting on emitted strings (EmitChain output, Assert.Contains patterns)
- Testing diagnostics (FindMissingHandleMethod, FindDuplicateOrders)

---

### cookbook/01-logging-behavior.md (end-user)

- Build a structured logging behavior
- Cross-cutting: applies to all request types
- Shows: static Handle, injecting ILogger via constructor workaround
- Architecture diagram

### cookbook/02-scoped-behavior-appliesto.md (end-user)

- Scope a behavior to a single model/request type using AppliesTo
- Shows: [PipelineBehavior(AppliesTo = typeof(CreateOrderCommand))]
- Why this is compile-time not runtime filtering

### cookbook/03-ordered-behavior-chain.md (end-user)

- Compose multiple behaviors with explicit Order values
- Shows: auth (Order=1), logging (Order=2), validation (Order=3)
- Mermaid diagram of the resulting chain

### cookbook/04-build-a-pipeline-generator.md (generator-author)

- Full walkthrough: wire up Discoverer + Shape + Emitter inside an IIncrementalGenerator
- Shows: ForAttributeWithMetadataName → FromAttributeSyntaxContext → filter → sort →
  build PipelineShape → EmitChain → AddSource
- Architecture diagram

### cookbook/05-custom-diagnostic-rules.md (generator-author)

- Add ZAPxxx-style diagnostics to your generator using PipelineDiagnosticRules
- Shows: reporting FindMissingHandleMethod and FindDuplicateOrders as real Roslyn Diagnostics
- Suppression guidance

---

## Code Convention Rules (matches ZeroAlloc.Mediator)

- Wrong: `// ❌` comment on or above the broken line
- Correct: `// ✅` or `// ✅ Use X instead`
- Generated code stubs: `// Conceptual — what the generator emits...` header comment
- Mermaid: sequenceDiagram for flows, flowchart TB/LR for chains/pipelines
- Diagnostic codes: ZAP prefix + 3 digits (ZAP001, ZAP002, ...)
