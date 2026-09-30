using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ZeroAlloc.Pipeline.Generators.Tests;

/// <summary>
/// <see cref="PipelineEmitter.EmitCachedChain"/>, issue #117. A chain whose innermost body reads
/// instance state cannot use static lambdas, and a lambda that captures <c>this</c> is allocated
/// on every call. The cached form keeps each level's delegate in an instance field instead, so
/// after the first call the chain allocates nothing.
/// </summary>
public class PipelineCachedChainTests
{
    private const string Ping = "global::App.Ping";
    private const string MediatorNext =
        "global::System.Func<global::App.Ping, global::System.Threading.CancellationToken, global::System.Threading.Tasks.ValueTask<string>>";
    private const string ValidationNext = "global::System.Func<global::App.Model, global::App.Result>";

    private static readonly PipelineBehaviorInfo[] TwoBehaviors =
    [
        new("global::App.Outer", 0, null, 2),
        new("global::App.Inner", 1, null, 2),
    ];

    // ZeroAlloc.Mediator's DI dispatch: the innermost body reads the injected provider.
    private static PipelineShape MediatorDiShape() => new()
    {
        TypeArguments = [Ping, "string"],
        OuterParameterNames = ["request", "ct"],
        LambdaParameterPrefixes = ["r", "c"],
        EmitStaticLambdas = false,
        InnermostBodyFactory = depth => $"{{ return _services.Handle(r{depth}, c{depth}); }}",
    };

    // ZeroAlloc.Validation with a nested validator: the innermost body reads the nested validator's field.
    private static PipelineShape ValidationNestedShape() => new()
    {
        TypeArguments = ["global::App.Model"],
        OuterParameterNames = ["instance"],
        LambdaParameterPrefixes = ["r"],
        EmitStaticLambdas = false,
        InnermostBodyFactory = depth => $"{{ return _childValidator.Validate({(depth == 0 ? "instance" : $"r{depth}")}.Child); }}",
    };

    [Fact]
    public void EmitCachedChain_TwoBehaviors_ReadsEachLevelFromItsField()
    {
        var chain = PipelineEmitter.EmitCachedChain(TwoBehaviors, MediatorDiShape(), MediatorNext, "__sendPingNext");

        Assert.Equal(
            [
                $"private {MediatorNext}? __sendPingNext1;",
                $"private {MediatorNext}? __sendPingNext2;",
            ],
            chain.MemberDeclarations);
        Assert.Equal(
            "global::App.Outer.Handle<global::App.Ping, string>(\n"
            + "                request, ct, __sendPingNext1 ??= (r1, c1) =>\n"
            + "                global::App.Inner.Handle<global::App.Ping, string>(\n"
            + "                    r1, c1, __sendPingNext2 ??= (r2, c2) =>\n"
            + "                    { return _services.Handle(r2, c2); }))",
            chain.Expression);
    }

    [Fact]
    public void EmitCachedChain_IgnoresEmitStaticLambdas()
    {
        // A static lambda could not read the next level's cache field.
        var shape = MediatorDiShape() with { EmitStaticLambdas = true };

        var chain = PipelineEmitter.EmitCachedChain(TwoBehaviors, shape, MediatorNext, "__next");

        Assert.DoesNotContain("static", chain.Expression, StringComparison.Ordinal);
    }

    [Fact]
    public void EmitCachedChain_NoBehaviors_ReturnsInnermostBodyAndNoMembers()
    {
        var chain = PipelineEmitter.EmitCachedChain([], MediatorDiShape(), MediatorNext, "__next");

        Assert.Equal("{ return _services.Handle(r0, c0); }", chain.Expression);
        Assert.Empty(chain.MemberDeclarations);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void EmitCachedChain_MissingNextDelegateType_Throws(string? nextDelegateType)
        => Assert.ThrowsAny<ArgumentException>(
            () => PipelineEmitter.EmitCachedChain(TwoBehaviors, MediatorDiShape(), nextDelegateType!, "__next"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void EmitCachedChain_MissingFieldPrefix_Throws(string? cacheFieldPrefix)
        => Assert.ThrowsAny<ArgumentException>(
            () => PipelineEmitter.EmitCachedChain(TwoBehaviors, MediatorDiShape(), MediatorNext, cacheFieldPrefix!));

    [Fact]
    public void EmitChain_OutputIsUnchanged()
    {
        // EmitCachedChain is opt-in: the existing EmitChain output stays byte-identical.
        Assert.Equal(
            "global::App.Outer.Handle<global::App.Ping, string>(\n"
            + "                request, ct, (r1, c1) =>\n"
            + "                global::App.Inner.Handle<global::App.Ping, string>(\n"
            + "                    r1, c1, (r2, c2) =>\n"
            + "                    { return _services.Handle(r2, c2); }))",
            PipelineEmitter.EmitChain(TwoBehaviors, MediatorDiShape()));
        Assert.Equal(
            "global::App.Outer.Handle<global::App.Ping, string>(\n"
            + "                request, ct, static (r1, c1) =>\n"
            + "                global::App.Inner.Handle<global::App.Ping, string>(\n"
            + "                    r1, c1, static (r2, c2) =>\n"
            + "                    { return _services.Handle(r2, c2); }))",
            PipelineEmitter.EmitChain(TwoBehaviors, MediatorDiShape() with { EmitStaticLambdas = true }));
    }

    [Fact]
    public void MediatorDiShape_CachedChain_AllocatesNothingPerCall()
    {
        var chain = PipelineEmitter.EmitCachedChain(TwoBehaviors, MediatorDiShape(), MediatorNext, "__sendPingNext");

        Assert.Equal(0, MediatorBytesPerCall(chain.Expression, chain.MemberDeclarations));
    }

    [Fact]
    public void MediatorDiShape_UncachedChain_AllocatesPerCall()
    {
        // The cost #117 removes: without the cache, each call allocates the capturing delegates.
        var chain = PipelineEmitter.EmitChain(TwoBehaviors, MediatorDiShape());

        Assert.True(MediatorBytesPerCall(chain, []) > 0);
    }

    [Fact]
    public void ValidationNestedShape_CachedChain_AllocatesNothingPerCall()
    {
        var chain = PipelineEmitter.EmitCachedChain(TwoBehaviors, ValidationNestedShape(), ValidationNext, "__validateNext");

        Assert.Equal(0, ValidationBytesPerCall(chain.Expression, chain.MemberDeclarations));
    }

    [Fact]
    public void ValidationNestedShape_UncachedChain_AllocatesPerCall()
    {
        var chain = PipelineEmitter.EmitChain(TwoBehaviors, ValidationNestedShape());

        Assert.True(ValidationBytesPerCall(chain, []) > 0);
    }

    [Fact]
    public void CachedChain_RunsEveryBehaviorInOrderOnEveryCall()
    {
        var chain = PipelineEmitter.EmitCachedChain(TwoBehaviors, ValidationNestedShape(), ValidationNext, "__validateNext");
        var validator = CreateValidator(chain.Expression, chain.MemberDeclarations);
        var validate = validator.GetType().GetMethod("Validate")!;
        var model = validator.GetType().Assembly.GetType("App.Model")!;
        var log = validator.GetType().Assembly.GetType("App.Log")!.GetField("Entries")!;

        log.SetValue(null, new List<string>());
        validate.Invoke(validator, [Activator.CreateInstance(model)]);
        validate.Invoke(validator, [Activator.CreateInstance(model)]);

        Assert.Equal(["outer", "inner", "child", "outer", "inner", "child"], (List<string>)log.GetValue(null)!);
    }

    private const string Behaviors = """
        public static class Outer
        {
            public static global::System.Threading.Tasks.ValueTask<TResponse> Handle<TRequest, TResponse>(
                TRequest request, global::System.Threading.CancellationToken ct,
                global::System.Func<TRequest, global::System.Threading.CancellationToken, global::System.Threading.Tasks.ValueTask<TResponse>> next)
                => next(request, ct);

            public static Result Handle<TModel>(TModel instance, global::System.Func<TModel, Result> next)
            {
                Log.Add("outer");
                return next(instance);
            }
        }

        public static class Inner
        {
            public static global::System.Threading.Tasks.ValueTask<TResponse> Handle<TRequest, TResponse>(
                TRequest request, global::System.Threading.CancellationToken ct,
                global::System.Func<TRequest, global::System.Threading.CancellationToken, global::System.Threading.Tasks.ValueTask<TResponse>> next)
                => next(request, ct);

            public static Result Handle<TModel>(TModel instance, global::System.Func<TModel, Result> next)
            {
                Log.Add("inner");
                return next(instance);
            }
        }

        public static class Log
        {
            public static global::System.Collections.Generic.List<string>? Entries;
            public static void Add(string entry) => Entries?.Add(entry);
        }
        """;

    private static long MediatorBytesPerCall(string expression, IReadOnlyList<string> members)
    {
        var source = $$"""
            #nullable enable
            namespace App;

            public sealed class Ping { }

            public sealed class Services
            {
                public global::System.Threading.Tasks.ValueTask<string> Handle(Ping request, global::System.Threading.CancellationToken ct)
                    => new("pong");
            }

            public sealed class Result { }

            public sealed class MediatorService
            {
                private readonly Services _services = new();

                public global::System.Threading.Tasks.ValueTask<string> Send(Ping request, global::System.Threading.CancellationToken ct)
                    => {{expression}};

                {{string.Join("\n    ", members)}}
            }

            public static class Probe
            {
                public static long BytesPerCall()
                {
                    var service = new MediatorService();
                    var ping = new Ping();
                    for (var i = 0; i < 100; i++) _ = service.Send(ping, default).Result;
                    var before = global::System.GC.GetAllocatedBytesForCurrentThread();
                    for (var i = 0; i < 1000; i++) _ = service.Send(ping, default).Result;
                    return (global::System.GC.GetAllocatedBytesForCurrentThread() - before) / 1000;
                }
            }

            {{Behaviors}}
            """;
        return (long)Compile(source).GetType("App.Probe")!.GetMethod("BytesPerCall")!.Invoke(null, null)!;
    }

    private static long ValidationBytesPerCall(string expression, IReadOnlyList<string> members)
        => (long)CreateValidator(expression, members).GetType().Assembly
            .GetType("App.Probe")!.GetMethod("BytesPerCall")!.Invoke(null, null)!;

    private static object CreateValidator(string expression, IReadOnlyList<string> members)
    {
        var source = $$"""
            #nullable enable
            namespace App;

            public sealed class Result { public static readonly Result Valid = new(); }

            public sealed class Child { }

            public sealed class Model { public Child Child { get; } = new(); }

            public sealed class ChildValidator
            {
                public Result Validate(Child child)
                {
                    Log.Add("child");
                    return Result.Valid;
                }
            }

            public sealed class ModelValidator
            {
                private readonly ChildValidator _childValidator = new();

                public Result Validate(Model instance)
                {
                    return {{expression}};
                }

                {{string.Join("\n    ", members)}}
            }

            public static class Probe
            {
                public static long BytesPerCall()
                {
                    var validator = new ModelValidator();
                    var model = new Model();
                    for (var i = 0; i < 100; i++) validator.Validate(model);
                    var before = global::System.GC.GetAllocatedBytesForCurrentThread();
                    for (var i = 0; i < 1000; i++) validator.Validate(model);
                    return (global::System.GC.GetAllocatedBytesForCurrentThread() - before) / 1000;
                }
            }

            {{Behaviors}}
            """;
        return Activator.CreateInstance(Compile(source).GetType("App.ModelValidator")!)!;
    }

    private static Assembly Compile(string source)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            "CachedChain_" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release));

        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        var problems = emit.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).Select(d => d.ToString()).ToList();
        Assert.True(emit.Success && problems.Count == 0, string.Join(Environment.NewLine, problems));
        return Assembly.Load(stream.ToArray());
    }
}
