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
