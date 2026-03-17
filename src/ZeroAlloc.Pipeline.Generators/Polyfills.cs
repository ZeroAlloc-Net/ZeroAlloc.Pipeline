// Polyfills required to use C# records, init-only setters, and required members
// when targeting netstandard2.0.
#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    // Enables init-only property setters (C# 9 record / init syntax).
    internal static class IsExternalInit { }
}
#endif

#if !NET7_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    using System.Diagnostics;

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    [Conditional("NEVER")]
    internal sealed class RequiredMemberAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Constructor, AllowMultiple = true, Inherited = false)]
    [Conditional("NEVER")]
    internal sealed class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string featureName) { FeatureName = featureName; }
        public string FeatureName { get; }
        public bool IsOptional { get; init; }
    }
}

namespace System.Diagnostics.CodeAnalysis
{
    using System.Diagnostics;

    [AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
    [Conditional("NEVER")]
    internal sealed class SetsRequiredMembersAttribute : Attribute { }
}
#endif
