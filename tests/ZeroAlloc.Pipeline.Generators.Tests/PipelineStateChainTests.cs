using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ZeroAlloc.TestHelpers;

namespace ZeroAlloc.Pipeline.Generators.Tests;

/// <summary>
/// <see cref="PipelineShape.State"/>, issue #121. A chain whose innermost body reads instance state
/// passes that state through every level instead of capturing it, so every lambda stays
/// <c>static</c>, the compiler creates each one once, and a call allocates no delegate: not per
/// call, and not per instance either.
/// </summary>
/// <remarks>
/// Each probe compiles the chain into a host class and returns a delegate bound once to its
/// <c>Send</c> or <c>Validate</c>, so a call through the delegate allocates nothing itself.
/// <see cref="AllocationGate"/> measures the whole call: it settles the heap, warms up, and rounds
/// the per-call figure up, so an allocation on only some calls still reads above 0.
/// </remarks>
public class PipelineStateChainTests
{
    private const int Iterations = 1_000;

    private const string Ping = "global::App.Ping";

    private static readonly PipelineBehaviorInfo[] TwoBehaviors =
    [
        new("global::App.Outer", 0, null, 3),
        new("global::App.Inner", 1, null, 3),
    ];

    // ZeroAlloc.Mediator's DI dispatch: the innermost body reads the scope's services.
    private static PipelineShape MediatorDiShape() => new()
    {
        TypeArguments = [Ping, "string"],
        OuterParameterNames = ["request", "ct"],
        LambdaParameterPrefixes = ["r", "c"],
        State = new PipelineState { TypeName = "global::App.Services", Argument = "_services" },
        InnermostBodyFactory = depth => depth == 0
            ? "_services.Handle(request, ct)"
            : $"{{ return s{depth}.Handle(r{depth}, c{depth}); }}",
    };

    // ZeroAlloc.Validation with a nested validator: the validator passes itself as the state.
    private static PipelineShape ValidationNestedShape() => new()
    {
        TypeArguments = ["global::App.Model"],
        OuterParameterNames = ["instance"],
        LambdaParameterPrefixes = ["r"],
        State = new PipelineState { TypeName = "global::App.ModelValidator", Argument = "this", LambdaParameterPrefix = "v" },
        InnermostBodyFactory = depth => depth == 0
            ? "_childValidator.Validate(instance.Child)"
            : $"{{ return v{depth}._childValidator.Validate(r{depth}.Child); }}",
    };

    [Fact]
    public void EmitChain_WithState_PassesItAtEveryLevel()
    {
        Assert.Equal(
            "global::App.Outer.Handle<global::App.Ping, string, global::App.Services>(\n"
            + "                request, _services, ct, static (r1, s1, c1) =>\n"
            + "                global::App.Inner.Handle<global::App.Ping, string, global::App.Services>(\n"
            + "                    r1, s1, c1, static (r2, s2, c2) =>\n"
            + "                    { return s2.Handle(r2, c2); }))",
            PipelineEmitter.EmitChain(TwoBehaviors, MediatorDiShape()));
    }

    [Fact]
    public void EmitChain_WithState_SingleOuterParameter_PutsStateSecond()
    {
        Assert.Equal(
            "global::App.Outer.Handle<global::App.Model, global::App.ModelValidator>(\n"
            + "                instance, this, static (r1, v1) =>\n"
            + "                global::App.Inner.Handle<global::App.Model, global::App.ModelValidator>(\n"
            + "                    r1, v1, static (r2, v2) =>\n"
            + "                    { return v2._childValidator.Validate(r2.Child); }))",
            PipelineEmitter.EmitChain(TwoBehaviors, ValidationNestedShape()));
    }

    [Fact]
    public void EmitChain_WithState_NoBehaviors_ReturnsInnermostBody()
        => Assert.Equal("_services.Handle(request, ct)", PipelineEmitter.EmitChain([], MediatorDiShape()));

    [Fact]
    public void EmitChain_WithoutState_OutputIsUnchanged()
    {
        Assert.Equal(
            "global::App.Outer.Handle<global::App.Ping, string>(\n"
            + "                request, ct, static (r1, c1) =>\n"
            + "                global::App.Inner.Handle<global::App.Ping, string>(\n"
            + "                    r1, c1, static (r2, c2) =>\n"
            + "                    { return Handler.Handle(r2, c2); }))",
            PipelineEmitter.EmitChain(TwoBehaviors, MediatorDiShape() with
            {
                State = null,
                InnermostBodyFactory = depth => $"{{ return Handler.Handle(r{depth}, c{depth}); }}",
            }));
    }

    [Fact]
    public void HandleTypeParameterCount_CountsTheStateType()
    {
        Assert.Equal(3, MediatorDiShape().HandleTypeParameterCount);
        Assert.Equal(2, (MediatorDiShape() with { State = null }).HandleTypeParameterCount);
        Assert.Equal(2, ValidationNestedShape().HandleTypeParameterCount);
    }

    public static TheoryData<string?, string?, string?> InvalidStates => new()
    {
        { null, "_services", "s" },
        { " ", "_services", "s" },
        { "global::App.Services", null, "s" },
        { "global::App.Services", "", "s" },
        { "global::App.Services", "_services", null },
        { "global::App.Services", "_services", "" },
        // Collides with the request's prefix: the lambda would declare r1 twice.
        { "global::App.Services", "_services", "r" },
    };

    [Theory]
    [MemberData(nameof(InvalidStates))]
    public void EmitChain_InvalidState_Throws(string? typeName, string? argument, string? prefix)
    {
        var shape = MediatorDiShape() with
        {
            State = new PipelineState { TypeName = typeName!, Argument = argument!, LambdaParameterPrefix = prefix! },
        };

        Assert.Throws<ArgumentException>(() => PipelineEmitter.EmitChain(TwoBehaviors, shape));
    }

    [Fact]
    public void MediatorDiShape_StateChain_AllocatesNothingPerCall()
    {
        var chain = PipelineEmitter.EmitChain(TwoBehaviors, MediatorDiShape());

        AllocationGate.AssertBudgetValueTask(budgetBytes: 0, Iterations, MediatorSend(chain), "Mediator DI chain with state");
    }

    [Fact]
    public void ValidationNestedShape_StateChain_AllocatesNothingPerCall()
    {
        var chain = PipelineEmitter.EmitChain(TwoBehaviors, ValidationNestedShape());

        AllocationGate.AssertBudget(budgetBytes: 0, Iterations, ValidationValidate(chain), "nested-validator chain with state");
    }

    [Fact]
    public async Task MediatorDiShape_StateChain_ReachesTheInstanceState()
    {
        var send = MediatorSend(PipelineEmitter.EmitChain(TwoBehaviors, MediatorDiShape()));

        Assert.Equal("pong", await send());
    }

    [Fact]
    public void StateChain_RunsEveryBehaviorInOrderOnEveryCall()
    {
        var validator = CreateValidator(PipelineEmitter.EmitChain(TwoBehaviors, ValidationNestedShape()));
        var validate = validator.GetType().GetMethod("Validate")!;
        var model = validator.GetType().Assembly.GetType("App.Model")!;
        var log = validator.GetType().Assembly.GetType("App.Log")!.GetField("Entries")!;

        log.SetValue(null, new List<string>());
        validate.Invoke(validator, [Activator.CreateInstance(model)]);
        validate.Invoke(validator, [Activator.CreateInstance(model)]);

        Assert.Equal(["outer", "inner", "child", "outer", "inner", "child"], (List<string>)log.GetValue(null)!);
    }

    [Fact]
    public void CapturingBody_FailsToCompile()
    {
        // A body that reads instance state without the state slot cannot be emitted silently as an
        // allocating chain: the static lambda rejects the capture at compile time.
        var chain = PipelineEmitter.EmitChain(TwoBehaviors, MediatorDiShape() with
        {
            State = null,
            InnermostBodyFactory = depth => $"{{ return _services.Handle(r{depth}, c{depth}); }}",
        });

        var diagnostics = TryCompile(MediatorSource(chain)).Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

        Assert.Contains(diagnostics, d => string.Equals(d.Id, "CS8821", StringComparison.Ordinal));
    }

    // The behaviors are not inlined, as a real behavior that does work or awaits next is not.
    // The stateless overloads serve CapturingBody_FailsToCompile, whose chain passes no state.
    private const string Behaviors = """
        public static class Outer
        {
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            public static global::System.Threading.Tasks.ValueTask<TResponse> Handle<TRequest, TResponse, TState>(
                TRequest request, TState state, global::System.Threading.CancellationToken ct,
                global::System.Func<TRequest, TState, global::System.Threading.CancellationToken, global::System.Threading.Tasks.ValueTask<TResponse>> next)
                => next(request, state, ct);

            public static global::System.Threading.Tasks.ValueTask<TResponse> Handle<TRequest, TResponse>(
                TRequest request, global::System.Threading.CancellationToken ct,
                global::System.Func<TRequest, global::System.Threading.CancellationToken, global::System.Threading.Tasks.ValueTask<TResponse>> next)
                => next(request, ct);

            public static Result Handle<TModel, TState>(TModel instance, TState state, global::System.Func<TModel, TState, Result> next)
            {
                Log.Add("outer");
                return next(instance, state);
            }
        }

        public static class Inner
        {
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            public static global::System.Threading.Tasks.ValueTask<TResponse> Handle<TRequest, TResponse, TState>(
                TRequest request, TState state, global::System.Threading.CancellationToken ct,
                global::System.Func<TRequest, TState, global::System.Threading.CancellationToken, global::System.Threading.Tasks.ValueTask<TResponse>> next)
                => next(request, state, ct);

            public static global::System.Threading.Tasks.ValueTask<TResponse> Handle<TRequest, TResponse>(
                TRequest request, global::System.Threading.CancellationToken ct,
                global::System.Func<TRequest, global::System.Threading.CancellationToken, global::System.Threading.Tasks.ValueTask<TResponse>> next)
                => next(request, ct);

            public static Result Handle<TModel, TState>(TModel instance, TState state, global::System.Func<TModel, TState, Result> next)
            {
                Log.Add("inner");
                return next(instance, state);
            }
        }

        public static class Log
        {
            public static global::System.Collections.Generic.List<string>? Entries;
            public static void Add(string entry) => Entries?.Add(entry);
        }

        public sealed class Result { public static readonly Result Valid = new(); }
        """;

    private static string MediatorSource(string expression) => $$"""
        #nullable enable
        namespace App;

        public sealed class Ping { }

        public sealed class Services
        {
            public global::System.Threading.Tasks.ValueTask<string> Handle(Ping request, global::System.Threading.CancellationToken ct)
                => new("pong");
        }

        public sealed class MediatorService
        {
            private readonly Services _services = new();

            public global::System.Threading.Tasks.ValueTask<string> Send(Ping request, global::System.Threading.CancellationToken ct)
                => {{expression}};
        }

        public static class Probe
        {
            public static global::System.Func<global::System.Threading.Tasks.ValueTask<string>> Send()
            {
                var service = new MediatorService();
                var ping = new Ping();
                return () => service.Send(ping, default);
            }
        }

        {{Behaviors}}
        """;

    // A delegate bound once to the compiled MediatorService.Send.
    private static Func<ValueTask<string>> MediatorSend(string expression)
        => (Func<ValueTask<string>>)Compile(MediatorSource(expression)).GetType("App.Probe")!.GetMethod("Send")!.Invoke(null, null)!;

    // A delegate bound once to the compiled ModelValidator.Validate.
    private static Action ValidationValidate(string expression)
        => (Action)CreateValidator(expression).GetType().Assembly
            .GetType("App.Probe")!.GetMethod("Validate")!.Invoke(null, null)!;

    private static object CreateValidator(string expression)
    {
        var source = $$"""
            #nullable enable
            namespace App;

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
            }

            public static class Probe
            {
                public static global::System.Action Validate()
                {
                    var validator = new ModelValidator();
                    var model = new Model();
                    return () => validator.Validate(model);
                }
            }

            {{Behaviors}}
            """;
        return Activator.CreateInstance(Compile(source).GetType("App.ModelValidator")!)!;
    }

    private static Assembly Compile(string source)
    {
        using var stream = new MemoryStream();
        var problems = TryCompile(source, stream)
            .Where(d => d.Severity >= DiagnosticSeverity.Warning)
            .Select(d => d.ToString())
            .ToList();
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
        return Assembly.Load(stream.ToArray());
    }

    private static IReadOnlyList<Diagnostic> TryCompile(string source, Stream? output = null)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            "StateChain_" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release));

        return compilation.Emit(output ?? Stream.Null).Diagnostics;
    }
}
