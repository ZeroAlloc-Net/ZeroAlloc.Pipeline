namespace ZeroAlloc.Pipeline.Generators.Tests;

public class PipelineEmitterTests
{
    // Shape that matches ZeroAlloc.Mediator's Send(request, ct) pattern
    private static PipelineShape MediatorShape(string requestType, string responseType, string innermostBody)
        => new PipelineShape
        {
            TypeArguments = [requestType, responseType],
            OuterParameterNames = ["request", "ct"],
            LambdaParameterPrefixes = ["r", "c"],
            InnermostBodyTemplate = innermostBody,
        };

    // Shape that matches ZeroAlloc.Validation's Validate(instance) pattern
    private static PipelineShape ValidationShape(string modelType, string innermostBody)
        => new PipelineShape
        {
            TypeArguments = [modelType],
            OuterParameterNames = ["instance"],
            LambdaParameterPrefixes = ["r"],
            InnermostBodyTemplate = innermostBody,
        };

    [Fact]
    public void EmitChain_NullBehaviors_Throws()
    {
        var shape = MediatorShape("global::App.Ping", "string", "body");
        Assert.Throws<System.ArgumentNullException>(() => PipelineEmitter.EmitChain(null!, shape));
    }

    [Fact]
    public void EmitChain_EmptyTypeArguments_Throws()
    {
        var shape = new PipelineShape
        {
            TypeArguments = [],
            OuterParameterNames = ["request"],
            LambdaParameterPrefixes = ["r"],
            InnermostBodyTemplate = "body",
        };
        Assert.Throws<System.ArgumentException>(() =>
            PipelineEmitter.EmitChain([new PipelineBehaviorInfo("global::App.B", 0, null, 1)], shape));
    }

    [Fact]
    public void EmitChain_NoBehaviors_ReturnsInnermostBody()
    {
        var shape = MediatorShape("global::App.Ping", "string", "handler.Handle(request, ct)");
        var result = PipelineEmitter.EmitChain([], shape);
        Assert.Equal("handler.Handle(request, ct)", result);
    }

    [Fact]
    public void EmitChain_OneBehavior_WrapsInnermost()
    {
        var behaviors = new[]
        {
            new PipelineBehaviorInfo("global::App.LoggingBehavior", 0, null, 2)
        };
        var shape = MediatorShape(
            "global::App.Ping", "string",
            "{ var h = new PingHandler(); return h.Handle(r1, c1); }");

        var result = PipelineEmitter.EmitChain(behaviors, shape);

        Assert.Contains("LoggingBehavior.Handle<global::App.Ping, string>", result, StringComparison.Ordinal);
        Assert.Contains("request, ct", result, StringComparison.Ordinal);
        Assert.Contains("static (r1, c1)", result, StringComparison.Ordinal);
    }

    [Fact]
    public void EmitChain_TwoBehaviors_NestedCorrectly()
    {
        var behaviors = new[]
        {
            new PipelineBehaviorInfo("global::App.LoggingBehavior", 0, null, 2),
            new PipelineBehaviorInfo("global::App.ValidationBehavior", 1, null, 2),
        };
        var shape = MediatorShape(
            "global::App.Ping", "string",
            "{ var h = new PingHandler(); return h.Handle(r2, c2); }");

        var result = PipelineEmitter.EmitChain(behaviors, shape);

        // Outer uses original params
        Assert.Contains("LoggingBehavior.Handle<global::App.Ping, string>(\n                request, ct,", result, StringComparison.Ordinal);
        // Inner uses lambda params
        Assert.Contains("ValidationBehavior.Handle<global::App.Ping, string>", result, StringComparison.Ordinal);
        Assert.Contains("r1, c1,", result, StringComparison.Ordinal);
        Assert.Contains("static (r2, c2)", result, StringComparison.Ordinal);
    }

    [Fact]
    public void EmitChain_ValidationShape_SingleTypeArg()
    {
        var behaviors = new[]
        {
            new PipelineBehaviorInfo("global::App.CachingBehavior", 0, null, 1)
        };
        var shape = ValidationShape(
            "global::App.Order",
            "{ return new OrderValidator().Validate(r1); }");

        var result = PipelineEmitter.EmitChain(behaviors, shape);

        Assert.Contains("CachingBehavior.Handle<global::App.Order>", result, StringComparison.Ordinal);
        Assert.Contains("instance,", result, StringComparison.Ordinal);
        Assert.Contains("static (r1)", result, StringComparison.Ordinal);
    }

    [Fact]
    public void EmitChain_InnermostBodyFactory_ReceivesDepth()
    {
        var behaviors = new[]
        {
            new PipelineBehaviorInfo("global::App.LoggingBehavior", 0, null, 2),
            new PipelineBehaviorInfo("global::App.ValidationBehavior", 1, null, 2),
        };
        var shape = new PipelineShape
        {
            TypeArguments = ["global::App.Ping", "string"],
            OuterParameterNames = ["request", "ct"],
            LambdaParameterPrefixes = ["r", "c"],
            InnermostBodyFactory = depth => $"{{ return h.Handle(r{depth}, c{depth}); }}",
        };

        var result = PipelineEmitter.EmitChain(behaviors, shape);

        // Factory was called with depth=2, so innermost body uses r2,c2
        Assert.Contains("static (r2, c2)", result, StringComparison.Ordinal);
        Assert.Contains("h.Handle(r2, c2)", result, StringComparison.Ordinal);
    }
}
