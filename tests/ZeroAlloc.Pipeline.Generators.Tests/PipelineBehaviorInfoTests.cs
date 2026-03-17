namespace ZeroAlloc.Pipeline.Generators.Tests;

public class PipelineBehaviorInfoTests
{
    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new PipelineBehaviorInfo("global::App.Foo", 1, "global::App.Bar", 2);
        var b = new PipelineBehaviorInfo("global::App.Foo", 1, "global::App.Bar", 2);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentOrder_NotEqual()
    {
        var a = new PipelineBehaviorInfo("global::App.Foo", 1, null, 2);
        var b = new PipelineBehaviorInfo("global::App.Foo", 2, null, 2);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void HasValidHandleMethod_WhenCountMatchesExpected_ReturnsTrue()
    {
        var info = new PipelineBehaviorInfo("global::App.Foo", 0, null, typeParamCount: 2);
        Assert.True(info.HasValidHandleMethod(expectedTypeParamCount: 2));
        Assert.False(info.HasValidHandleMethod(expectedTypeParamCount: 1));
    }

    [Fact]
    public void HasValidHandleMethod_WhenNoHandleMethod_ReturnsFalse()
    {
        var info = new PipelineBehaviorInfo("global::App.Foo", 0, null, typeParamCount: -1);
        Assert.False(info.HasValidHandleMethod(expectedTypeParamCount: 2));
    }
}
