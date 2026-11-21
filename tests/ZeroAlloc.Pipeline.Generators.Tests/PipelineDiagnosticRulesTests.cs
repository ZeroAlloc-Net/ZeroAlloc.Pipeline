namespace ZeroAlloc.Pipeline.Generators.Tests;

public class PipelineDiagnosticRulesTests
{
    [Fact]
    public void FindMissingHandleMethod_ReturnsOnlyInvalid()
    {
        var behaviors = new[]
        {
            new PipelineBehaviorInfo("global::App.Good", 0, null, typeParamCount: 2),
            new PipelineBehaviorInfo("global::App.Bad", 1, null, typeParamCount: -1),
        };

        var invalid = PipelineDiagnosticRules.FindMissingHandleMethod(behaviors, expectedTypeParamCount: 2).ToList();

        Assert.Single(invalid);
        Assert.Equal("global::App.Bad", invalid[0].BehaviorTypeName);
    }

    [Fact]
    public void FindMissingHandleMethod_StatefulShape_ExpectsTheStateTypeParameter()
    {
        // A behavior written for the stateless shape, Handle<TRequest, TResponse>, has one type
        // parameter too few for a pipeline that threads state.
        var behaviors = new[]
        {
            new PipelineBehaviorInfo("global::App.Stateful", 0, null, typeParamCount: 3),
            new PipelineBehaviorInfo("global::App.Stateless", 1, null, typeParamCount: 2),
        };
        var shape = new PipelineShape
        {
            TypeArguments = ["global::App.Ping", "string"],
            OuterParameterNames = ["request", "ct"],
            LambdaParameterPrefixes = ["r", "c"],
            InnermostBodyTemplate = "body",
            State = new PipelineState { TypeName = "global::System.IServiceProvider", Argument = "_services" },
        };

        var invalid = PipelineDiagnosticRules.FindMissingHandleMethod(behaviors, shape).ToList();

        Assert.Equal(["global::App.Stateless"], invalid.Select(b => b.BehaviorTypeName), StringComparer.Ordinal);
    }

    [Fact]
    public void FindMissingHandleMethod_StatelessShape_ExpectsTheTypeArgumentsOnly()
    {
        var behaviors = new[]
        {
            new PipelineBehaviorInfo("global::App.Stateful", 0, null, typeParamCount: 2),
            new PipelineBehaviorInfo("global::App.Stateless", 1, null, typeParamCount: 1),
        };
        var shape = new PipelineShape
        {
            TypeArguments = ["global::App.Order"],
            OuterParameterNames = ["instance"],
            LambdaParameterPrefixes = ["r"],
            InnermostBodyTemplate = "body",
        };

        var invalid = PipelineDiagnosticRules.FindMissingHandleMethod(behaviors, shape).ToList();

        Assert.Equal(["global::App.Stateful"], invalid.Select(b => b.BehaviorTypeName), StringComparer.Ordinal);
    }

    [Fact]
    public void FindMissingHandleMethod_NullShape_Throws()
        => Assert.Throws<ArgumentNullException>(
            () => PipelineDiagnosticRules.FindMissingHandleMethod([], (PipelineShape)null!));

    [Fact]
    public void FindMissingPipelineBehaviorInterface_ReturnsOnlyTypesWithoutTheInterface()
    {
        var candidates = new[]
        {
            new PipelineBehaviorCandidateInfo("global::App.Good", implementsPipelineBehavior: true, isStatic: false),
            new PipelineBehaviorCandidateInfo("global::App.Static", implementsPipelineBehavior: false, isStatic: true),
            new PipelineBehaviorCandidateInfo("global::App.NoInterface", implementsPipelineBehavior: false, isStatic: false),
        };

        var invalid = PipelineDiagnosticRules.FindMissingPipelineBehaviorInterface(candidates).ToList();

        Assert.Equal(["global::App.Static", "global::App.NoInterface"], invalid.Select(c => c.BehaviorTypeName), StringComparer.Ordinal);
        Assert.Equal([true, false], invalid.Select(c => c.IsStatic));
    }

    [Fact]
    public void FindDuplicateOrders_ReturnsDuplicateGroups()
    {
        var behaviors = new[]
        {
            new PipelineBehaviorInfo("global::App.A", 1, null, 2),
            new PipelineBehaviorInfo("global::App.B", 1, null, 2),
            new PipelineBehaviorInfo("global::App.C", 2, null, 2),
        };

        var duplicates = PipelineDiagnosticRules.FindDuplicateOrders(behaviors).ToList();

        Assert.Single(duplicates);
        Assert.Equal(2, duplicates[0].Count());
    }

    [Fact]
    public void FindDuplicateOrders_NoDuplicates_ReturnsEmpty()
    {
        var behaviors = new[]
        {
            new PipelineBehaviorInfo("global::App.A", 0, null, 2),
            new PipelineBehaviorInfo("global::App.B", 1, null, 2),
        };

        var duplicates = PipelineDiagnosticRules.FindDuplicateOrders(behaviors).ToList();

        Assert.Empty(duplicates);
    }
}
