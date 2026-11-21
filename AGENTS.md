# AGENTS.md: ZeroAlloc.Pipeline

Two NuGet packages that give ZeroAlloc source generators a shared, allocation-free pipeline of
behaviors. `ZeroAlloc.Pipeline` holds the contracts (`IPipelineBehavior`, `[PipelineBehavior]`).
`ZeroAlloc.Pipeline.Generators` is the Roslyn helper library that generator authors ship privately next to
their own generator: behavior discovery, diagnostic rules and `PipelineEmitter`, which emits the static
call chain. Mediator and Validation consume both.

## Layout

- `src/ZeroAlloc.Pipeline` targets `netstandard2.0;net10.0;net11.0`, and its net10.0/net11.0 targets are
  `IsAotCompatible`. It keeps `netstandard2.0` because `ZeroAlloc.Pipeline.Generators` references it,
  and that project and the generators that consume it load into the compiler.
- `src/ZeroAlloc.Pipeline.Generators` targets `netstandard2.0` only. It is a plain library, not an
  analyzer project, so it never runs in this repo's own builds. `Polyfills.cs` supplies the C# features
  that `netstandard2.0` lacks.
- Both packable projects use the public API analyzers. RS0016 and RS0017 are build errors, so a public
  API change also edits that project's `PublicAPI.Unshipped.txt`. Never edit `PublicAPI.Shipped.txt`:
  the release workflow moves entries into it.
- `tests/ZeroAlloc.Pipeline.Generators.Tests` is the only test project. `tests/ZeroAlloc.Pipeline.Benchmarks`
  is an exe, not a test.
- `samples/ZeroAlloc.Pipeline.AotSmoke` is the NativeAOT smoke. It references the generators project as an
  analyzer only to prove the package wiring; no behavior is emitted in it.
- `Directory.Build.props` starts with a pack guard: `dotnet pack` fails with ZAVERSION001 or ZAVERSION002
  unless the version was passed with `-p:Version` or `-p:PackageVersion`, and the built assembly must carry
  that same version. Build, test and publish never run it.

## Build and test

Prerequisites: `global.json` pins SDK 11.0.100 (`rollForward: latestMinor`). Tests run on `net10.0` and
`net11.0`, so both the .NET 10 and the .NET 11 runtime must be installed. The AOT smoke targets
`net11.0` and needs clang and `zlib1g-dev`.

CI job `build` in `.github/workflows/ci.yml`:

```bash
dotnet tool restore
VERSION=$(dotnet gitversion /showvariable SemVer)
dotnet restore
dotnet build --no-restore -c Release -p:Version=$VERSION
dotnet test --no-build -c Release --verbosity normal --collect "XPlat Code Coverage" --settings coverage.runsettings --results-directory TestResults
dotnet pack src/ZeroAlloc.Pipeline/ZeroAlloc.Pipeline.csproj --no-build -c Release -p:PackageVersion=$VERSION -o ./artifacts
dotnet pack src/ZeroAlloc.Pipeline.Generators/ZeroAlloc.Pipeline.Generators.csproj --no-build -c Release -p:PackageVersion=$VERSION -o ./artifacts
```

The same job then runs the `coverage-gate` action from ci-baseline on `TestResults/**/coverage.cobertura.xml`.
That is a coverage floor plus a check of `codecov.yml`; the commands above produce the report but do not
reproduce the gate.

CI job `aot-smoke` in `.github/workflows/ci.yml`:

```bash
dotnet restore samples/ZeroAlloc.Pipeline.AotSmoke/ZeroAlloc.Pipeline.AotSmoke.csproj
dotnet publish samples/ZeroAlloc.Pipeline.AotSmoke/ZeroAlloc.Pipeline.AotSmoke.csproj -r linux-x64 -c Release -o ./aot-out
./aot-out/ZeroAlloc.Pipeline.AotSmoke
```

CI job `smoke` in `.github/workflows/benchmarks.yml` calls the shared `benchmark-smoke.yml` on
`tests/ZeroAlloc.Pipeline.Benchmarks`. The manual `full` job in the same file builds and runs everything
with a filter:

```bash
dotnet build tests/ZeroAlloc.Pipeline.Benchmarks -c Release
dotnet run --project tests/ZeroAlloc.Pipeline.Benchmarks -c Release --no-build -- --filter "${FILTER:-*}"
```

Fast inner loop, run locally and passing, not a CI job:

```bash
dotnet build -c Release -p:Version=9.9.9-local.test
dotnet test tests/ZeroAlloc.Pipeline.Generators.Tests -c Release -f net10.0 --filter "FullyQualifiedName~EmittedChain"
```

## Conventions and gotchas

- `TreatWarningsAsErrors` is on, together with `EnforceCodeStyleInBuild` and the Meziantou, Roslynator and
  ZeroAlloc analyzers. Those three apply to every target except `netstandard2.0`, so the generators
  project and the `netstandard2.0` target of the contracts project are not checked by them.
- `LangVersion` is `latest`, but `netstandard2.0` code sees only that framework's BCL plus `Polyfills.cs`. Do not use
  a BCL API that is missing there without a conditional-compilation branch.
- The hot path here is the code `PipelineEmitter` emits, not the emitter itself. The budget is exactly
  0 B per call for the static chain with and without state, asserted in `EmittedChainAllocationTests`
  and `PipelineStateChainTests`, which compile the emitted code and measure it with `AllocationGate`.
  Change the emitted shape only together with those tests, then re-measure and update `docs/performance.md`
  and the README figures.
- `tests/ZeroAlloc.Pipeline.Benchmarks` targets `net10.0` only, and `ZeroAlloc.Pipeline.AotSmoke` targets
  `net11.0` only. Both are deliberate and differ from the org default of every LTS framework. The benchmark
  behaviors mirror what `PipelineEmitter` emits by hand; keep them in step when the emitter changes.
- The AOT smoke treats IL2026, IL2067, IL2075, IL2091, IL3050 and IL3051 as errors. Do not suppress them.

<!-- org-core:start sha256=8faf417fa86f11bd8ad9bc98dfa4683fba44e440291e84c6cb9b0ec3ca0c0e7e (generated from docker-nuc scripts/agents-md/org-core-zeroalloc.md; do not edit here) -->
## Org rules

These rules hold in every ZeroAlloc-Net repo. Each library makes two promises: it works under
NativeAOT and trimming, and its hot path allocates nothing. The 2026-10-11 sweep found 16 of 28 repos
allocating on the hot path, most with docs claiming 0 B.

### Hot path

The operation a consumer runs repeatedly: `Send`, a cache hit, `Validate`, `Map`, `Serialize`, a
resilience call, a state transition, an append, a worker poll, including the idle poll. Startup and
registration are not hot path. For a source generator, the hot path is the code it emits: judge the
snapshots and the generated output.

### Allocation traps the sweep found

| Pattern | Instead |
|---|---|
| `async ValueTask` that only awaits and returns | `return new ValueTask<T>(inner.XAsync(ct));` when the method only forwards a `Task<T>` (return a forwarded `ValueTask<T>` directly). The wrapper stores a synchronous throw in the returned task; the direct return lets it, and the cancellation status of the callee, reach the caller. Keep the `async` wrapper when callers depend on either |
| `Task.WhenAll` over tasks that already completed | skip the await only when every task has `IsCompletedSuccessfully` (`Status == TaskStatus.RanToCompletion` on `netstandard2.0`); otherwise `await Task.WhenAll`, which also reports faults of completed tasks |
| `TagList` or log `params object[]` built before `Enabled`/`IsEnabled` | check first, or `LoggerMessage.Define` |
| event args allocated before checking for subscribers | copy the handler to a local, check, then allocate |
| `new Serializer()` / `new Utf8JsonWriter()` per call | cache stateless instances, reuse writers with `Reset` |
| `GetString()` / `ToString()` round-trips in JSON converters | parse and format through spans |
| `ToArray()` / `ToList()` copies between layers | pass `ReadOnlyMemory<T>` or spans, copy once at most |
| LINQ in emitted code | emit `for` loops |
| `yield` iterator behind a public `GetEnumerator` | public struct enumerator, from the first release |
| `Enum.IsDefined(typeof(E), v)` with a boxed value, `decimal.GetBits(v)` to read the scale | `Enum.IsDefined<E>(v)` when `v` has type `E` (.NET 5 or later, so not on .NET Framework, `netstandard2.0`, `netstandard2.1` or .NET Core 3.1); `v.Scale` for a scale read only (.NET 7 or later, never for the coefficient or sign). Keep the old call on older targets |
| configuration parsed or read per call | parse once and cache |
| an `AsyncLocal` write per call | pass state explicitly |
| a capturing lambda or method-group delegate per call | cache the delegate in a field, or a static lambda |
| a per-key object that is never removed | remove on release; otherwise it is a leak |

Inherent allocations are fine when documented: the consumer's own result, an exception on a failure
path, a one-time cache fill.

### Tests and benchmarks

- Every hot path has an allocation gate test (`AllocationGate` from ZeroAlloc.TestHelpers) that measures
  the whole public call, including any state setup.
- Budgets are exact. If a path allocates, assert that number and comment what it is.
- Test the generated code, not a hand-written stand-in, and cover every shape: each attribute, nested
  collections, and the asynchronous case with a fake that returns pending tasks.
- Benchmarks run in CI through the shared `benchmark-smoke.yml` and target the library's LTS
  frameworks. The benchmark body allocates nothing besides the library itself. Run locally with
  `--inProcess`.
- Publish exact allocations and same-run ratios against a `Baseline`, never absolute timings. Run with
  `--launchCount 3`, pin CPUs with `taskset`, give the spread, and label every table.

### NativeAOT and trimming

- Packable runtime projects, and projects consumers publish, set `IsAotCompatible=true` on modern
  target frameworks. Analyzers and source generators target `netstandard2.0`.
- No reflection or runtime code generation on a supported path. An opt-in reflection fallback carries
  honest `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]`. Every suppression must be true;
  remove dead ones.
- The AOT smoke sample publishes with `TreatWarningsAsErrors=true`. If a third-party warning really
  blocks it, use a narrow, justified suppression for that warning. `WarningsNotAsErrors` selects codes,
  not assemblies, so it would also downgrade the same warning from generated code.
- The smoke runs the hot path of every package in the repo. A package without a smoke has unknown AOT
  status. Adapters run against real services in CI service containers; publish-only is a last resort.
  App and template smokes make authenticated requests.

### Docs and shipping

- Docs match measurements. After a hot-path change, re-measure and update `docs/performance.md` and the
  README. Never write "0 B" without a test that asserts it.
- New public API is `feat:`, even for a performance change. Public API changes are additive: never add
  an optional parameter to a shipped signature. A breaking end state goes in a `next-major` issue.
- A behaviour change gets a `## Behaviour change` section in the PR body.
- Conventional Commits with release-please. Subjects start lowercase, body lines stay within 100
  characters, and a body never contains nested parentheses: release-please drops the whole commit.
- Local packs use `9.9.9-local.<sha>`. After a release, verify the package on NuGet.
- Fix review findings in the PR; file an issue only for what cannot go in it.

Full rules, template wiring lessons and the reasons behind each rule: https://github.com/ZeroAlloc-Net/.github/blob/main/AGENTS.md
<!-- org-core:end -->
