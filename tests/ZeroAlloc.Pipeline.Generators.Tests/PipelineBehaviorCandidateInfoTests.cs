namespace ZeroAlloc.Pipeline.Generators.Tests;

public class PipelineBehaviorCandidateInfoTests
{
    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new PipelineBehaviorCandidateInfo("global::App.Foo", implementsPipelineBehavior: false, isStatic: true);
        var b = new PipelineBehaviorCandidateInfo("global::App.Foo", implementsPipelineBehavior: false, isStatic: true);
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Theory]
    [InlineData("global::App.Bar", false, true)]
    [InlineData("global::App.Foo", true, true)]
    [InlineData("global::App.Foo", false, false)]
    public void Equality_DifferentValues_NotEqual(string typeName, bool implementsPipelineBehavior, bool isStatic)
    {
        var a = new PipelineBehaviorCandidateInfo("global::App.Foo", implementsPipelineBehavior: false, isStatic: true);
        var b = new PipelineBehaviorCandidateInfo(typeName, implementsPipelineBehavior, isStatic);
        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }
}
