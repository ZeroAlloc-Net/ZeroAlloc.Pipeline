using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ZeroAlloc.TestHelpers;

namespace ZeroAlloc.Pipeline.Generators.Tests;

/// <summary>
/// Backs the README's "0 B" claim: discovers real behaviors, emits the chain with
/// <see cref="PipelineEmitter.EmitChain"/>, compiles it, and measures one call of the compiled
/// pipeline with <see cref="AllocationGate"/>.
/// </summary>
public class EmittedChainAllocationTests
{
    private const int Iterations = 10_000;

    // Three behaviors in the README's shape. Logging is async, as in the README example: an async
    // ValueTask method that completes synchronously allocates no state machine box.
    private const string BehaviorSource = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using ZeroAlloc.Pipeline;

        namespace App;

        public sealed record Ping(string Message);

        [PipelineBehavior(Order = 1)]
        public class AuthBehavior : IPipelineBehavior
        {
            public static ValueTask<TResponse> Handle<TRequest, TResponse>(
                TRequest request, CancellationToken ct,
                Func<TRequest, CancellationToken, ValueTask<TResponse>> next)
                => next(request, ct);
        }

        [PipelineBehavior(Order = 2)]
        public class LoggingBehavior : IPipelineBehavior
        {
            public static int Calls;

            public static async ValueTask<TResponse> Handle<TRequest, TResponse>(
                TRequest request, CancellationToken ct,
                Func<TRequest, CancellationToken, ValueTask<TResponse>> next)
            {
                Calls++;
                var response = await next(request, ct);
                Calls++;
                return response;
            }
        }

        [PipelineBehavior(Order = 3)]
        public class ValidationBehavior : IPipelineBehavior
        {
            public static ValueTask<TResponse> Handle<TRequest, TResponse>(
                TRequest request, CancellationToken ct,
                Func<TRequest, CancellationToken, ValueTask<TResponse>> next)
                => ct.IsCancellationRequested ? ValueTask.FromCanceled<TResponse>(ct) : next(request, ct);
        }

        public static class PingHandler
        {
            public static ValueTask<string> Handle(Ping request, CancellationToken ct)
                => new(request.Message);
        }
        """;

    [Fact]
    public void EmittedChain_ThreeBehaviors_AllocatesNothingPerCall()
    {
        var send = CompileEmittedPipeline(out var ping);

        AllocationGate.AssertBudgetValueTask(
            budgetBytes: 0,
            Iterations,
            () => send(ping, CancellationToken.None),
            "emitted static chain, 3 behaviors");
    }

    [Fact]
    public async Task EmittedChain_ThreeBehaviors_ReturnsHandlerResult()
    {
        var send = CompileEmittedPipeline(out var ping);

        Assert.Equal("hello", await send(ping, CancellationToken.None));
    }

    // Compiles the behaviors, discovers them, emits the chain into a static Send method, compiles
    // that, and returns a delegate bound once to Send, so a call through it allocates nothing itself.
    private static Func<object, CancellationToken, ValueTask<string>> CompileEmittedPipeline(out object ping)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(IPipelineBehavior).Assembly.Location))
            .ToList();
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release);

        var behaviorTree = CSharpSyntaxTree.ParseText(BehaviorSource);
        var discovery = CSharpCompilation.Create("Discovery", [behaviorTree], references, options);
        var behaviors = PipelineBehaviorDiscoverer.Discover(discovery).OrderBy(b => b.Order).ToList();
        Assert.Equal(3, behaviors.Count);

        var chain = PipelineEmitter.EmitChain(behaviors, new PipelineShape
        {
            TypeArguments = ["global::App.Ping", "string"],
            OuterParameterNames = ["request", "ct"],
            LambdaParameterPrefixes = ["r", "c"],
            InnermostBodyFactory = depth => $"global::App.PingHandler.Handle(r{depth}, c{depth})",
        });

        var hostSource = $$"""
            namespace App;

            public static class Pipeline
            {
                public static global::System.Threading.Tasks.ValueTask<string> Send(
                    object request, global::System.Threading.CancellationToken ct)
                {
                    var typed = (global::App.Ping)request;
                    return Run(typed, ct);
                }

                private static global::System.Threading.Tasks.ValueTask<string> Run(
                    global::App.Ping request, global::System.Threading.CancellationToken ct)
                    => {{chain}};
            }
            """;

        var compilation = CSharpCompilation.Create(
            "EmittedPipeline_" + Guid.NewGuid().ToString("N"),
            [behaviorTree, CSharpSyntaxTree.ParseText(hostSource)],
            references,
            options);

        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        stream.Position = 0;

        var assembly = new AssemblyLoadContext(name: null, isCollectible: false).LoadFromStream(stream);
        var pipeline = assembly.GetType("App.Pipeline", throwOnError: true)!;
        var pingType = assembly.GetType("App.Ping", throwOnError: true)!;
        ping = Activator.CreateInstance(pingType, "hello")!;

        return pipeline
            .GetMethod("Send", BindingFlags.Public | BindingFlags.Static)!
            .CreateDelegate<Func<object, CancellationToken, ValueTask<string>>>();
    }
}
