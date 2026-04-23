using System;
using ZeroAlloc.Pipeline;

// Exercise the Pipeline contract types under PublishAot=true. Pipeline itself
// is a shared contract library consumed by other ZeroAlloc libs (Mediator,
// etc.) whose own AOT smoke samples verify the full end-to-end pipeline.
// This sample just proves the attribute + interface publish cleanly on their
// own — a minimal guard against a regression that leaks reflection into the
// contract assembly.

var attr = new PipelineBehaviorAttribute(order: 42);
if (attr.Order != 42) return Fail($"PipelineBehavior.Order round-trip expected 42, got {attr.Order}");

if (typeof(NoopBehavior).GetInterface(nameof(IPipelineBehavior)) is null)
    return Fail("NoopBehavior should implement IPipelineBehavior");

Console.WriteLine("AOT smoke: PASS");
return 0;

static int Fail(string message)
{
    Console.Error.WriteLine($"AOT smoke: FAIL — {message}");
    return 1;
}

[PipelineBehavior(order: 0)]
internal sealed class NoopBehavior : IPipelineBehavior { }
