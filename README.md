# ZeroAlloc.Pipeline

[![NuGet](https://img.shields.io/nuget/v/ZeroAlloc.Pipeline.svg)](https://www.nuget.org/packages/ZeroAlloc.Pipeline)
[![Build](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/actions/workflows/ci.yml/badge.svg)](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![AOT](https://img.shields.io/badge/AOT--Compatible-passing-brightgreen)](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)
[![GitHub Sponsors](https://img.shields.io/github/sponsors/MarcelRoozekrans?style=flat&logo=githubsponsors&color=ea4aaa&label=Sponsor)](https://github.com/sponsors/MarcelRoozekrans)

ZeroAlloc.Pipeline is the shared building block for pipeline-aware source generators in the ZeroAlloc ecosystem. It provides the `IPipelineBehavior` marker interface, `PipelineBehaviorAttribute`, and the Roslyn-based discovery, validation, and code-emission utilities that generators like ZeroAlloc.Mediator and ZeroAlloc.Validation build on. All pipeline wiring is resolved at compile time — no reflection, no virtual dispatch, no heap allocation per call.

Multiple packages in this family — see [Documentation](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/tree/main/docs/) or NuGet for the full list.

## Install

Writing behaviors for a host framework such as ZeroAlloc.Mediator or ZeroAlloc.Validation? You need only the
contracts package; the host's own generator discovers your behaviors:

```bash
dotnet add package ZeroAlloc.Pipeline
```

Building your own pipeline-aware source generator? `ZeroAlloc.Pipeline.Generators` is a Roslyn helper library,
not a generator. Reference it from your generator project as a normal, private dependency, and ship
`ZeroAlloc.Pipeline.Generators.dll` and `ZeroAlloc.Pipeline.dll` next to your generator in
`analyzers/dotnet/cs`, because Roslyn does not load a generator's dependencies on its own:

```xml
<PackageReference Include="ZeroAlloc.Pipeline.Generators" Version="1.*" PrivateAssets="all" />
```

See [Build a Pipeline Generator](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/blob/main/docs/cookbook/04-build-a-pipeline-generator.md).

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

ZeroAlloc.Pipeline emits static nested lambda chains. Static lambdas carry no closure allocation, and the JIT inlines static call chains aggressively — collapsing N behaviors into straight-line code.

| | 1 behavior | 3 behaviors | 5 behaviors | Allocated |
|---|:---:|:---:|:---:|:---:|
| **Static chain** | 4.1 ns | 2.3 ns | 2.8 ns | **0 B** |
| Pre-built delegate chain | 2.2 ns | 9.9 ns | 17.6 ns | 0 B |
| Speedup | 0.5× | **4.3×** | **6.4×** | — |

BenchmarkDotNet v0.15.8, .NET 10.0.4, 12th Gen Intel Core i9-12900HK — [`tests/ZeroAlloc.Pipeline.Benchmarks`](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/blob/main/tests/ZeroAlloc.Pipeline.Benchmarks)

## Features

- **`IPipelineBehavior`** — marker interface; no contract imposed on Handle signature
- **`PipelineBehaviorAttribute`** — `Order` (execution position) and `AppliesTo` (type scoping)
- **Attribute subclassing** — framework packages define their own alias; discovery follows the inheritance chain
- **Compile-time discovery** — `PipelineBehaviorDiscoverer` and `FromAttributeSyntaxContext` for incremental generators
- **Static emitter** — `PipelineEmitter.EmitChain` generates a nested static lambda chain from a behavior list and a `PipelineShape`; `EmitCachedChain` caches the delegates in instance fields when the body reads instance state
- **`PipelineShape`** — describes type arguments, parameter names, and the innermost body; supports a factory delegate when the body embeds depth-indexed lambda names
- **Diagnostic helpers** — `PipelineDiagnosticRules.FindMissingHandleMethod`, `FindDuplicateOrders` and `FindMissingPipelineBehaviorInterface` return offending behaviors; the caller maps them to framework-specific diagnostic IDs
- **Zero allocation** — no reflection, no boxing, no delegate list per call; `EmittedChainAllocationTests` compiles an emitted 3-behavior chain and asserts 0 B per call with `AllocationGate`
- **Native AOT** — `ZeroAlloc.Pipeline` targets netstandard2.0, net8.0 and net10.0 and works in trimmed, ahead-of-time compiled applications; `ZeroAlloc.Pipeline.Generators` targets netstandard2.0 for Roslyn

## Documentation

| Page | Description |
|------|-------------|
| [Getting Started](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/blob/main/docs/getting-started.md) | Write your first behavior and see it get picked up |
| [Pipeline Behaviors](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/blob/main/docs/pipeline-behaviors.md) | `IPipelineBehavior`, `PipelineBehaviorAttribute`, ordering, scoping |
| [Pipeline Shape](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/blob/main/docs/pipeline-shape.md) | Describe the delegate shape for code generation |
| [Pipeline Emitter](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/blob/main/docs/pipeline-emitter.md) | Generate a nested static lambda call chain |
| [Pipeline Discoverer](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/blob/main/docs/pipeline-discoverer.md) | Discover behaviors at compile time |
| [Diagnostics](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/blob/main/docs/diagnostics.md) | ZAP001 to ZAP003 diagnostic rules reference |
| [Performance](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/blob/main/docs/performance.md) | Why static lambda chains allocate nothing |
| [Testing](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/blob/main/docs/testing.md) | Test behaviors and generators built on this library |

## License

MIT
