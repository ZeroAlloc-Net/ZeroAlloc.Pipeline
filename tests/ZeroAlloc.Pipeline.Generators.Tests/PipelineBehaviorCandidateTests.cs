using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ZeroAlloc.Pipeline.Generators.Tests;

/// <summary>
/// A type carrying <c>[PipelineBehavior]</c> that does not implement <c>IPipelineBehavior</c> never
/// joins the pipeline, but discovery must still surface it so a consumer can report it.
/// </summary>
public class PipelineBehaviorCandidateTests
{
    private const string Source = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using ZeroAlloc.Pipeline;

        namespace App;

        [PipelineBehavior(Order = 0)]
        public static class StaticLogging
        {
            public static ValueTask<TResponse> Handle<TRequest, TResponse>(TRequest request, CancellationToken ct,
                Func<TRequest, CancellationToken, ValueTask<TResponse>> next) => next(request, ct);
        }

        [PipelineBehavior(Order = 1)]
        public class MissingInterface
        {
            public static ValueTask<TResponse> Handle<TRequest, TResponse>(TRequest request, CancellationToken ct,
                Func<TRequest, CancellationToken, ValueTask<TResponse>> next) => next(request, ct);
        }

        [PipelineBehavior(Order = 2)]
        public class Correct : IPipelineBehavior
        {
            public static ValueTask<TResponse> Handle<TRequest, TResponse>(TRequest request, CancellationToken ct,
                Func<TRequest, CancellationToken, ValueTask<TResponse>> next) => next(request, ct);
        }
        """;

    private static CSharpCompilation CreateCompilation(params string[] sources)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(IPipelineBehavior).Assembly.Location));
        return CSharpCompilation.Create(
            "TestAssembly",
            sources.Select(source => CSharpSyntaxTree.ParseText(source)),
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    [Fact]
    public void DiscoverCandidates_StaticClass_IsSurfacedAsStaticWithoutInterface()
    {
        var candidate = PipelineBehaviorDiscoverer.DiscoverCandidates(CreateCompilation(Source))
            .Single(c => string.Equals(c.BehaviorTypeName, "global::App.StaticLogging", StringComparison.Ordinal));

        Assert.False(candidate.ImplementsPipelineBehavior);
        Assert.True(candidate.IsStatic);
    }

    [Fact]
    public void DiscoverCandidates_NonStaticClassWithoutInterface_IsSurfacedAsNonStatic()
    {
        var candidate = PipelineBehaviorDiscoverer.DiscoverCandidates(CreateCompilation(Source))
            .Single(c => string.Equals(c.BehaviorTypeName, "global::App.MissingInterface", StringComparison.Ordinal));

        Assert.False(candidate.ImplementsPipelineBehavior);
        Assert.False(candidate.IsStatic);
    }

    [Fact]
    public void DiscoverCandidates_CorrectBehavior_ImplementsInterface()
    {
        var candidate = PipelineBehaviorDiscoverer.DiscoverCandidates(CreateCompilation(Source))
            .Single(c => string.Equals(c.BehaviorTypeName, "global::App.Correct", StringComparison.Ordinal));

        Assert.True(candidate.ImplementsPipelineBehavior);
        Assert.False(candidate.IsStatic);
    }

    [Fact]
    public void DiscoverCandidates_ClassWithoutAttribute_IsIgnored()
    {
        var source = """
            using ZeroAlloc.Pipeline;
            public class NotABehavior : IPipelineBehavior { }
            public static class AlsoNot { }
            """;

        Assert.Empty(PipelineBehaviorDiscoverer.DiscoverCandidates(CreateCompilation(source)));
    }

    [Fact]
    public void Discover_TypesWithoutInterface_DoNotJoinThePipeline()
    {
        var behaviors = PipelineBehaviorDiscoverer.Discover(CreateCompilation(Source)).ToList();

        var behavior = Assert.Single(behaviors);
        Assert.Equal("global::App.Correct", behavior.BehaviorTypeName);
    }

    [Fact]
    public void Discover_PartialClassWithAttributesOnTwoParts_IsReturnedOnce()
    {
        // Each part is its own declaration with an attribute list, but both resolve to one symbol.
        var source = """
            using System;
            using ZeroAlloc.Pipeline;

            [PipelineBehavior(Order = 1)]
            public partial class Split : IPipelineBehavior { }

            [Obsolete]
            public partial class Split { }

            [PipelineBehavior(Order = 2)]
            public static partial class StaticSplit { }

            [Obsolete]
            public static partial class StaticSplit { }
            """;
        var compilation = CreateCompilation(source);

        Assert.Single(PipelineBehaviorDiscoverer.Discover(compilation));
        Assert.Equal(
            [
                new PipelineBehaviorCandidateInfo("global::Split", implementsPipelineBehavior: true, isStatic: false),
                new PipelineBehaviorCandidateInfo("global::StaticSplit", implementsPipelineBehavior: false, isStatic: true),
            ],
            PipelineBehaviorDiscoverer.DiscoverCandidates(compilation));
    }

    [Fact]
    public void Discover_PartialClassAcrossFiles_IsReturnedOnceWithTheAttributeArguments()
    {
        // The part without the pipeline attribute is in the first file, so it is visited first.
        var compilation = CreateCompilation(
            """
            [System.Obsolete]
            public partial class Split { }
            """,
            """
            using ZeroAlloc.Pipeline;

            public class Model { }

            [PipelineBehavior(Order = 3, AppliesTo = typeof(Model))]
            public partial class Split : IPipelineBehavior
            {
                public static string Handle<T>(T r, System.Func<T, string> next) => next(r);
            }
            """);

        var behavior = Assert.Single(PipelineBehaviorDiscoverer.Discover(compilation));
        Assert.Equal(new PipelineBehaviorInfo("global::Split", 3, "global::Model", 1), behavior);
    }

    [Fact]
    public void FindMissingPipelineBehaviorInterface_FlagsStaticAndNonStatic_NotTheCorrectBehavior()
    {
        var candidates = PipelineBehaviorDiscoverer.DiscoverCandidates(CreateCompilation(Source));

        var flagged = PipelineDiagnosticRules.FindMissingPipelineBehaviorInterface(candidates)
            .OrderBy(c => c.BehaviorTypeName, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            [
                new PipelineBehaviorCandidateInfo("global::App.MissingInterface", implementsPipelineBehavior: false, isStatic: false),
                new PipelineBehaviorCandidateInfo("global::App.StaticLogging", implementsPipelineBehavior: false, isStatic: true),
            ],
            flagged);
    }

    [Fact]
    public void CandidateFromAttributeSyntaxContext_SurfacesEveryAttributedType()
    {
        var generator = new CapturingGenerator();
        CSharpGeneratorDriver.Create(generator).RunGenerators(CreateCompilation(Source));

        Assert.Equal(
            [
                new PipelineBehaviorCandidateInfo("global::App.Correct", implementsPipelineBehavior: true, isStatic: false),
                new PipelineBehaviorCandidateInfo("global::App.MissingInterface", implementsPipelineBehavior: false, isStatic: false),
                new PipelineBehaviorCandidateInfo("global::App.StaticLogging", implementsPipelineBehavior: false, isStatic: true),
            ],
            generator.Candidates.OrderBy(c => c.BehaviorTypeName, StringComparer.Ordinal));
    }

    [Fact]
    public void FromAttributeSyntaxContext_TypesWithoutInterface_DoNotJoinThePipeline()
    {
        var generator = new CapturingGenerator();
        CSharpGeneratorDriver.Create(generator).RunGenerators(CreateCompilation(Source));

        var behavior = Assert.Single(generator.Behaviors);
        Assert.Equal("global::App.Correct", behavior.BehaviorTypeName);
    }

    /// <summary>Runs both per-symbol transforms the way a consumer generator does.</summary>
    private sealed class CapturingGenerator : IIncrementalGenerator
    {
        public List<PipelineBehaviorCandidateInfo> Candidates { get; } = [];

        public List<PipelineBehaviorInfo> Behaviors { get; } = [];

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var provider = context.SyntaxProvider.ForAttributeWithMetadataName(
                "ZeroAlloc.Pipeline.PipelineBehaviorAttribute",
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => (
                    Candidate: PipelineBehaviorDiscoverer.CandidateFromAttributeSyntaxContext(ctx),
                    Behavior: PipelineBehaviorDiscoverer.FromAttributeSyntaxContext(ctx)));

            context.RegisterSourceOutput(provider.Collect(), (_, results) => Capture(results));
        }

        private void Capture(ImmutableArray<(PipelineBehaviorCandidateInfo? Candidate, PipelineBehaviorInfo? Behavior)> results)
        {
            foreach (var (candidate, behavior) in results)
            {
                if (candidate != null) Candidates.Add(candidate);
                if (behavior != null) Behaviors.Add(behavior);
            }
        }
    }
}
