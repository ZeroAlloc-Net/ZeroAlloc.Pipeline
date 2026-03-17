using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ZeroAlloc.Pipeline.Generators.Tests;

public class PipelineBehaviorDiscovererTests
{
    private static Compilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        return CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IPipelineBehavior).Assembly.Location),
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>
    /// Creates a compilation with NO ZeroAlloc.Pipeline assembly reference.
    /// All types are defined inline, simulating the case where Roslyn may not be able to
    /// resolve the base attribute chain via normal metadata — exercising the
    /// <c>ResolveAttributeClassFromSyntax</c> fallback in <see cref="PipelineBehaviorDiscoverer"/>.
    /// </summary>
    private static Compilation CreateCompilationWithoutPipelineAssembly(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        return CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    [Fact]
    public void Discover_BehaviorWithAttribute_ReturnsInfo()
    {
        var source = """
            using ZeroAlloc.Pipeline;
            using System.Threading;
            using System.Threading.Tasks;

            [PipelineBehavior(Order = 1)]
            public class MyBehavior : IPipelineBehavior
            {
                public static ValueTask<TResponse> Handle<TRequest, TResponse>(
                    TRequest request, CancellationToken ct,
                    System.Func<TRequest, CancellationToken, ValueTask<TResponse>> next)
                    where TRequest : class
                    => next(request, ct);
            }
            """;

        var compilation = CreateCompilation(source);
        var results = PipelineBehaviorDiscoverer.Discover(compilation).ToList();

        Assert.Single(results);
        Assert.Equal(1, results[0].Order);
        Assert.Equal(2, results[0].HandleMethodTypeParameterCount);
        Assert.Null(results[0].AppliesTo);
    }

    [Fact]
    public void Discover_BehaviorWithAppliesTo_SetsAppliesTo()
    {
        var source = """
            using ZeroAlloc.Pipeline;

            public class MyModel { }

            [PipelineBehavior(AppliesTo = typeof(MyModel))]
            public class ScopedBehavior : IPipelineBehavior
            {
                public static string Handle<T>(T instance, System.Func<T, string> next) => next(instance);
            }
            """;

        var compilation = CreateCompilation(source);
        var results = PipelineBehaviorDiscoverer.Discover(compilation).ToList();

        Assert.Single(results);
        Assert.NotNull(results[0].AppliesTo);
        Assert.Equal("global::MyModel", results[0].AppliesTo);
    }

    [Fact]
    public void Discover_ClassWithoutAttribute_IsIgnored()
    {
        var source = """
            using ZeroAlloc.Pipeline;
            public class NotABehavior : IPipelineBehavior { }
            """;

        var compilation = CreateCompilation(source);
        var results = PipelineBehaviorDiscoverer.Discover(compilation).ToList();

        Assert.Empty(results);
    }

    [Fact]
    public void Discover_BehaviorWithoutHandleMethod_ReturnsNegativeTypeParamCount()
    {
        var source = """
            using ZeroAlloc.Pipeline;

            [PipelineBehavior]
            public class NoHandleBehavior : IPipelineBehavior { }
            """;

        var compilation = CreateCompilation(source);
        var results = PipelineBehaviorDiscoverer.Discover(compilation).ToList();

        Assert.Single(results);
        Assert.Equal(-1, results[0].HandleMethodTypeParameterCount);
    }

    [Fact]
    public void Discover_SubclassedAttribute_WithoutPipelineAssembly_IsDetected()
    {
        // All types (attribute base, interface, subclass attribute, behavior) are defined
        // inline — no ZeroAlloc.Pipeline assembly reference in this compilation.
        // This exercises discovery when Roslyn must fall back to syntax-level resolution
        // of the attribute base type chain.
        var source = """
            using System;

            namespace ZeroAlloc.Pipeline
            {
                [AttributeUsage(AttributeTargets.Class, Inherited = false)]
                public class PipelineBehaviorAttribute : Attribute
                {
                    public PipelineBehaviorAttribute(int order = 0) { Order = order; }
                    public int Order { get; set; }
                    public Type? AppliesTo { get; set; }
                }
                public interface IPipelineBehavior { }
            }

            public sealed class MediatorPipelineBehaviorAttribute : ZeroAlloc.Pipeline.PipelineBehaviorAttribute
            {
                public MediatorPipelineBehaviorAttribute(int order = 0) : base(order) { }
            }

            [MediatorPipelineBehavior(Order = 5)]
            public class MyBehavior : ZeroAlloc.Pipeline.IPipelineBehavior
            {
                public static string Handle<T>(T r, System.Func<T, string> next) => next(r);
            }
            """;

        var compilation = CreateCompilationWithoutPipelineAssembly(source);
        var results = PipelineBehaviorDiscoverer.Discover(compilation).ToList();

        Assert.Single(results);
        Assert.Equal(5, results[0].Order);
        Assert.Equal(1, results[0].HandleMethodTypeParameterCount);
    }

    [Fact]
    public void Discover_BehaviorWithInheritedHandle_ReturnsCorrectTypeParamCount()
    {
        var source = """
            using ZeroAlloc.Pipeline;
            using System.Threading;
            using System.Threading.Tasks;

            public abstract class BaseBehavior : IPipelineBehavior
            {
                public static ValueTask<TResponse> Handle<TRequest, TResponse>(
                    TRequest request, CancellationToken ct,
                    System.Func<TRequest, CancellationToken, ValueTask<TResponse>> next)
                    where TRequest : class
                    => next(request, ct);
            }

            [PipelineBehavior(Order = 1)]
            public class ConcreteBehavior : BaseBehavior { }
            """;

        var compilation = CreateCompilation(source);
        var results = PipelineBehaviorDiscoverer.Discover(compilation).ToList();

        Assert.Single(results);
        Assert.Equal(2, results[0].HandleMethodTypeParameterCount);
    }

    [Fact]
    public void Discover_SubclassedAttribute_IsDetected()
    {
        var source = """
            using ZeroAlloc.Pipeline;

            public sealed class MediatorPipelineBehaviorAttribute : PipelineBehaviorAttribute
            {
                public MediatorPipelineBehaviorAttribute(int order = 0) : base(order) { }
            }

            public interface IMediatorBehavior : IPipelineBehavior { }

            [MediatorPipelineBehavior(Order = 2)]
            public class MyBehavior : IMediatorBehavior
            {
                public static string Handle<T>(T r, System.Func<T, string> next) => next(r);
            }
            """;

        var compilation = CreateCompilation(source);
        var results = PipelineBehaviorDiscoverer.Discover(compilation).ToList();

        Assert.Single(results);
        Assert.Equal(2, results[0].Order);
    }
}
