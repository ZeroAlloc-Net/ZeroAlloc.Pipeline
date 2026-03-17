using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

// ── request / response ────────────────────────────────────────────────────

public readonly record struct Ping(string Message);

// ── static behaviors  ─────────────────────────────────────────────────────
// These mirror exactly what PipelineEmitter.EmitChain generates — static
// methods, no instance state, static lambdas as the next delegate.

public static class SB1 { public static ValueTask<TRes> Handle<TReq, TRes>(TReq r, CancellationToken ct, Func<TReq, CancellationToken, ValueTask<TRes>> next) => next(r, ct); }
public static class SB2 { public static ValueTask<TRes> Handle<TReq, TRes>(TReq r, CancellationToken ct, Func<TReq, CancellationToken, ValueTask<TRes>> next) => next(r, ct); }
public static class SB3 { public static ValueTask<TRes> Handle<TReq, TRes>(TReq r, CancellationToken ct, Func<TReq, CancellationToken, ValueTask<TRes>> next) => next(r, ct); }
public static class SB4 { public static ValueTask<TRes> Handle<TReq, TRes>(TReq r, CancellationToken ct, Func<TReq, CancellationToken, ValueTask<TRes>> next) => next(r, ct); }
public static class SB5 { public static ValueTask<TRes> Handle<TReq, TRes>(TReq r, CancellationToken ct, Func<TReq, CancellationToken, ValueTask<TRes>> next) => next(r, ct); }

// ── instance behaviors  ───────────────────────────────────────────────────
// Classic interface-based pipeline, pre-built at startup (delegate chain
// allocated once — no per-call allocation, but uses interface dispatch).

public interface IBehavior<TReq, TRes>
{
    ValueTask<TRes> Handle(TReq request, CancellationToken ct, Func<TReq, CancellationToken, ValueTask<TRes>> next);
}

public class IB1 : IBehavior<Ping, string> { public ValueTask<string> Handle(Ping r, CancellationToken ct, Func<Ping, CancellationToken, ValueTask<string>> next) => next(r, ct); }
public class IB2 : IBehavior<Ping, string> { public ValueTask<string> Handle(Ping r, CancellationToken ct, Func<Ping, CancellationToken, ValueTask<string>> next) => next(r, ct); }
public class IB3 : IBehavior<Ping, string> { public ValueTask<string> Handle(Ping r, CancellationToken ct, Func<Ping, CancellationToken, ValueTask<string>> next) => next(r, ct); }
public class IB4 : IBehavior<Ping, string> { public ValueTask<string> Handle(Ping r, CancellationToken ct, Func<Ping, CancellationToken, ValueTask<string>> next) => next(r, ct); }
public class IB5 : IBehavior<Ping, string> { public ValueTask<string> Handle(Ping r, CancellationToken ct, Func<Ping, CancellationToken, ValueTask<string>> next) => next(r, ct); }

// ── benchmarks ────────────────────────────────────────────────────────────

[MemoryDiagnoser]
[HideColumns(Column.StdDev, Column.Median, Column.RatioSD, Column.Error)]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class PipelineBenchmarks
{
    private static readonly Ping _ping = new("hello");

    // Pre-built delegate chains — allocated once at startup, not per call.
    private Func<Ping, CancellationToken, ValueTask<string>> _delegate1 = null!;
    private Func<Ping, CancellationToken, ValueTask<string>> _delegate3 = null!;
    private Func<Ping, CancellationToken, ValueTask<string>> _delegate5 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _delegate1 = BuildChain([new IB1()]);
        _delegate3 = BuildChain([new IB1(), new IB2(), new IB3()]);
        _delegate5 = BuildChain([new IB1(), new IB2(), new IB3(), new IB4(), new IB5()]);
    }

    static Func<Ping, CancellationToken, ValueTask<string>> BuildChain(IList<IBehavior<Ping, string>> behaviors)
    {
        Func<Ping, CancellationToken, ValueTask<string>> innermost = static (r, _) => ValueTask.FromResult(r.Message);
        for (var i = behaviors.Count - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var next = innermost;
            innermost = (r, ct) => behavior.Handle(r, ct, next);
        }
        return innermost;
    }

    // ── 0 behaviors ──────────────────────────────────────────────────────

    [BenchmarkCategory("0 behaviors"), Benchmark(Baseline = true)]
    public ValueTask<string> Static_0()
        => ValueTask.FromResult(_ping.Message);

    [BenchmarkCategory("0 behaviors"), Benchmark]
    public ValueTask<string> Delegate_0()
        => ValueTask.FromResult(_ping.Message);

    // ── 1 behavior ───────────────────────────────────────────────────────

    [BenchmarkCategory("1 behavior"), Benchmark(Baseline = true)]
    public ValueTask<string> Static_1()
        => SB1.Handle<Ping, string>(_ping, default,
            static (r1, c1) => ValueTask.FromResult(r1.Message));

    [BenchmarkCategory("1 behavior"), Benchmark]
    public ValueTask<string> Delegate_1()
        => _delegate1(_ping, default);

    // ── 3 behaviors ──────────────────────────────────────────────────────

    [BenchmarkCategory("3 behaviors"), Benchmark(Baseline = true)]
    public ValueTask<string> Static_3()
        => SB1.Handle<Ping, string>(_ping, default,
            static (r1, c1) => SB2.Handle<Ping, string>(r1, c1,
                static (r2, c2) => SB3.Handle<Ping, string>(r2, c2,
                    static (r3, c3) => ValueTask.FromResult(r3.Message))));

    [BenchmarkCategory("3 behaviors"), Benchmark]
    public ValueTask<string> Delegate_3()
        => _delegate3(_ping, default);

    // ── 5 behaviors ──────────────────────────────────────────────────────

    [BenchmarkCategory("5 behaviors"), Benchmark(Baseline = true)]
    public ValueTask<string> Static_5()
        => SB1.Handle<Ping, string>(_ping, default,
            static (r1, c1) => SB2.Handle<Ping, string>(r1, c1,
                static (r2, c2) => SB3.Handle<Ping, string>(r2, c2,
                    static (r3, c3) => SB4.Handle<Ping, string>(r3, c3,
                        static (r4, c4) => SB5.Handle<Ping, string>(r4, c4,
                            static (r5, c5) => ValueTask.FromResult(r5.Message))))));

    [BenchmarkCategory("5 behaviors"), Benchmark]
    public ValueTask<string> Delegate_5()
        => _delegate5(_ping, default);
}
