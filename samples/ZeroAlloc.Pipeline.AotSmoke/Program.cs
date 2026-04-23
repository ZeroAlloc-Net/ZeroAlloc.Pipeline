using System;
using ZeroAlloc.Pipeline;
using ZeroAlloc.Pipeline.AotSmoke;

// Exercise the Pipeline contract types under PublishAot=true. The downstream
// source-gens (Mediator, Notify, etc.) carry end-to-end smokes of the full
// emitted pipeline; this one just guards the contract assembly itself.

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
