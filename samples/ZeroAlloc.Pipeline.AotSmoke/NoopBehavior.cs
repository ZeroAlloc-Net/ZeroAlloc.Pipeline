using ZeroAlloc.Pipeline;

namespace ZeroAlloc.Pipeline.AotSmoke;

[PipelineBehavior(order: 0)]
internal sealed class NoopBehavior : IPipelineBehavior { }
