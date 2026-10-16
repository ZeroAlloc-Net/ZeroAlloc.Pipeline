# ZeroAlloc.Pipeline

[![NuGet](https://img.shields.io/nuget/v/ZeroAlloc.Pipeline.svg)](https://www.nuget.org/packages/ZeroAlloc.Pipeline)
[![Build](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/actions/workflows/ci.yml/badge.svg)](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/blob/main/LICENSE)
[![AOT](https://img.shields.io/badge/AOT--Compatible-passing-brightgreen)](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)
[![GitHub Sponsors](https://img.shields.io/github/sponsors/MarcelRoozekrans?style=flat&logo=githubsponsors&color=ea4aaa&label=Sponsor)](https://github.com/sponsors/MarcelRoozekrans)

Compile-time pipeline behaviors for .NET source generators: behaviors are discovered at build time and
emitted as a static call chain, with no reflection, no allocation per call, and NativeAOT support.

It is the building block behind the pipelines of ZeroAlloc.Mediator and ZeroAlloc.Validation. Write a
behavior once against its contracts, or use its Roslyn helpers to give your own generator the same pipeline.

## Why ZeroAlloc.Pipeline

- **No allocation per call.** An emitted three-behavior chain is compiled and asserted at 0 B per call
  with `AllocationGate` on every test run. Chains that read instance state get a cached form that is also
  asserted at 0 B per call.
- **Faster as the pipeline grows.** In the same benchmark run, a pre-built delegate chain takes 4.25× the
  time of the static chain at 3 behaviors and 6.38× at 5. With a single behavior the delegate chain is
  faster; see [Performance](#performance).
- **Works under NativeAOT and trimming.** The contracts package is `IsAotCompatible`, and CI publishes and
  runs a NativeAOT smoke app on every build. The pipeline is plain static calls, so the trimmer has
  nothing to keep alive.
- **Mistakes caught at build time.** Rule helpers find a missing or invalid `Handle` method, duplicate
  `Order` values, and a `[PipelineBehavior]` class that forgot `IPipelineBehavior`; your generator reports
  them as compiler diagnostics instead of a pipeline that silently skips a step.

## Install

Writing behaviors for a host such as ZeroAlloc.Mediator? You need only the contracts:

```bash
dotnet add package ZeroAlloc.Pipeline
```

Building your own pipeline-aware generator? Add the Roslyn helper library as a private dependency and ship
its DLLs next to your generator, as described in
[Getting Started](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/blob/main/docs/getting-started.md):

```xml
<PackageReference Include="ZeroAlloc.Pipeline.Generators" Version="1.*" PrivateAssets="all" />
```

## Quick start

A behavior is a class with a static `Handle` method. The host generator finds it at compile time and
calls it directly; there is nothing to register.

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using ZeroAlloc.Pipeline;

[PipelineBehavior(Order = 1)]
public class LoggingBehavior : IPipelineBehavior
{
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
```

Inside a generator, `PipelineEmitter` turns the discovered behaviors into the call chain:

```csharp
using ZeroAlloc.Pipeline.Generators;

PipelineBehaviorInfo[] behaviors =
[
    new("global::App.AuthBehavior", order: 1, appliesTo: null, typeParamCount: 2),
    new("global::App.LoggingBehavior", order: 2, appliesTo: null, typeParamCount: 2),
];

var shape = new PipelineShape
{
    TypeArguments = ["global::App.Ping", "string"],
    OuterParameterNames = ["request", "ct"],
    LambdaParameterPrefixes = ["r", "c"],
    InnermostBodyFactory = depth => $"global::App.PingHandler.Handle(r{depth}, c{depth})",
};

string chain = PipelineEmitter.EmitChain(behaviors, shape);
// global::App.AuthBehavior.Handle<global::App.Ping, string>(request, ct, static (r1, c1) =>
//     global::App.LoggingBehavior.Handle<global::App.Ping, string>(r1, c1, static (r2, c2) =>
//         global::App.PingHandler.Handle(r2, c2)))
```

## What you get

- **Behavior contracts**: `IPipelineBehavior` and `[PipelineBehavior]`, with ordering and scoping a
  behavior to one request type through `AppliesTo`.
- **Your own attribute name**: host frameworks subclass `[PipelineBehavior]`, and discovery follows the
  inheritance chain.
- **Incremental discovery**: finds behaviors from `ForAttributeWithMetadataName` or a whole compilation.
- **Chain emission**: static lambda chains, plus a cached form for bodies that read instance state.
- **Reusable rules**: the three validation checks above, mapped to your generator's own diagnostic IDs.

## Performance

The static chain that `PipelineEmitter.EmitChain` generates, against a delegate chain built once at
startup, the fastest runtime-composed alternative. Ratio is mean time relative to the static chain in
the same run; lower is faster.

| Behaviors | Static chain | Pre-built delegate chain | Allocated, both |
|---|---:|---:|---:|
| 1 | 1.00 | 0.53 | 0 B |
| 3 | 1.00 | 4.25 | 0 B |
| 5 | 1.00 | 6.38 | 0 B |

BenchmarkDotNet v0.15.8, .NET 10.0.4, 12th Gen Intel Core i9-12900HK. Full results, method and
caveats: [docs/performance.md](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/blob/main/docs/performance.md).

## Learn more

- **Documentation:** [pipeline.zeroalloc.net](https://pipeline.zeroalloc.net), covering behaviors,
  shapes, the emitter and discoverer, the diagnostics reference, testing and a cookbook.
- **Related packages:**
  - [ZeroAlloc.Mediator](https://github.com/ZeroAlloc-Net/ZeroAlloc.Mediator): source-generated mediator
    whose pipeline behaviors are built on this library. Docs: [mediator.zeroalloc.net](https://mediator.zeroalloc.net)
  - [ZeroAlloc.Validation](https://github.com/ZeroAlloc-Net/ZeroAlloc.Validation): source-generated
    validation whose generator is built on this library's helpers, and plugs into Mediator through
    `ZeroAlloc.Mediator.Validation`. Docs: [validation.zeroalloc.net](https://validation.zeroalloc.net)
  - [ZeroAlloc.Authorization](https://github.com/ZeroAlloc-Net/ZeroAlloc.Authorization): policy
    authorization that gates Mediator dispatch through a pipeline behavior in
    `ZeroAlloc.Mediator.Authorization`. Docs: [authorization.zeroalloc.net](https://authorization.zeroalloc.net)
  - [ZeroAlloc.Results](https://github.com/ZeroAlloc-Net/ZeroAlloc.Results): the result type the Mediator
    validation and authorization behaviors return their failures in. Docs: [results.zeroalloc.net](https://results.zeroalloc.net)
- **Contributing:** issues and pull requests are welcome on
  [GitHub](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/issues).
- **License:** [MIT](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/blob/main/LICENSE).
