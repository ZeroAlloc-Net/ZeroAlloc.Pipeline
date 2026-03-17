namespace ZeroAlloc.Pipeline;

/// <summary>
/// Marker interface for all pipeline behavior classes.
/// Implement this (or a framework-specific sub-interface) and decorate with
/// <see cref="PipelineBehaviorAttribute"/> to participate in the generated pipeline.
/// </summary>
public interface IPipelineBehavior;
