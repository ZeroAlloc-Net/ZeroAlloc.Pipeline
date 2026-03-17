# ZeroAlloc.Pipeline

[![NuGet](https://img.shields.io/nuget/v/ZeroAlloc.Pipeline.svg)](https://www.nuget.org/packages/ZeroAlloc.Pipeline)
[![NuGet](https://img.shields.io/nuget/v/ZeroAlloc.Pipeline.Generators.svg?label=ZeroAlloc.Pipeline.Generators)](https://www.nuget.org/packages/ZeroAlloc.Pipeline.Generators)
[![CI](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/actions/workflows/ci.yml/badge.svg)](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

ZeroAlloc.Pipeline is the shared building block for pipeline-aware source generators in the ZeroAlloc ecosystem. It provides the `IPipelineBehavior` marker interface, `PipelineBehaviorAttribute`, and the Roslyn-based discovery, validation, and code-emission utilities that generators like ZeroAlloc.Mediator and ZeroAlloc.Validation build on. All pipeline wiring is resolved at compile time — no reflection, no virtual dispatch, no heap allocation per call.

## Install

```bash
dotnet add package ZeroAlloc.Pipeline
dotnet add package ZeroAlloc.Pipeline.Generators
```

```xml
<PackageReference Include="ZeroAlloc.Pipeline" Version="*" />
<PackageReference Include="ZeroAlloc.Pipeline.Generators" Version="*"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

## Example

```csharp
using ZeroAlloc.Pipeline;

// 1. Implement IPipelineBehavior and decorate with [PipelineBehavior]
[PipelineBehavior(Order = 1)]
public class LoggingBehavior : IPipelineBehavior
{
    // 2. Expose a public static Handle method matching the host framework's delegate shape
    public static async ValueTask<TResponse> Handle<TRequest, TResponse>(
        TRequest request,
        CancellationToken ct,
        Func<TRequest, CancellationToken, ValueTask<TResponse>> next)
    {
        Console.WriteLine($"Handling {typeof(TRequest).Name}");
        var response = await next(request, ct);
        Console.WriteLine($"Handled {typeof(TRequest).Name}");
        return response;
    }
}

// 3. The host generator (e.g. ZeroAlloc.Mediator) picks this up at compile time
//    and wires it into the generated pipeline — no registration required.
```

## Performance

ZeroAlloc.Pipeline emits static nested lambda chains instead of runtime-resolved delegate lists. This eliminates allocations for the pipeline dispatch itself.

| Operation | ZeroAlloc.Pipeline chain (ns) | Reflection-based (ns) | Speedup | Alloc |
|-----------|-------------------------------|-----------------------|---------|-------|
| 1 behavior | 8 | 310 | ~39× | 0 B |
| 3 behaviors | 21 | 890 | ~42× | 0 B |
| 5 behaviors | 34 | 1 450 | ~43× | 0 B |

\* Illustrative estimates; see [docs/performance.md](docs/performance.md) for methodology.

## Features

- **`IPipelineBehavior`** — marker interface; no contract imposed on Handle signature
- **`PipelineBehaviorAttribute`** — `Order` (execution position) and `AppliesTo` (type scoping)
- **Attribute subclassing** — framework packages define their own alias; discovery follows the inheritance chain
- **Compile-time discovery** — `PipelineBehaviorDiscoverer` and `FromAttributeSyntaxContext` for incremental generators
- **Static emitter** — `PipelineEmitter.EmitChain` generates a nested static lambda chain from a behavior list and a `PipelineShape`
- **Diagnostic rules** — `PipelineDiagnosticRules` for missing `Handle` methods and duplicate `Order` values
- **Zero allocation** — no reflection, no boxing, no delegate list per call
- **netstandard2.0 + Native AOT** — works in trimmed, ahead-of-time compiled applications

## Documentation

| Page | Description |
|------|-------------|
| [Getting Started](docs/getting-started.md) | Write your first behavior and see it get picked up |
| [Pipeline Behaviors](docs/pipeline-behaviors.md) | `IPipelineBehavior`, `PipelineBehaviorAttribute`, ordering, scoping |
| [Pipeline Shape](docs/pipeline-shape.md) | Describe the delegate shape for code generation |
| [Pipeline Emitter](docs/pipeline-emitter.md) | Generate a nested static lambda call chain |
| [Pipeline Discoverer](docs/pipeline-discoverer.md) | Discover behaviors at compile time |
| [Diagnostics](docs/diagnostics.md) | ZAP001, ZAP002 diagnostic rules reference |
| [Performance](docs/performance.md) | Why static lambda chains allocate nothing |
| [Testing](docs/testing.md) | Test behaviors and generators built on this library |

## License

MIT
