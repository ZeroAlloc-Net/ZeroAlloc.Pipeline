# Design: Fix Code Review Issues (2026-03-17)

## Context

After a code review of the PipelineBehaviorDiscoverer, PipelineShape, PipelineEmitter, and
PipelineDiagnosticRules commits, seven issues were identified. This document captures the
approved design for resolving them.

Note: The `InnermostBodyTemplate` guard in `PipelineEmitter.EmitChain` was already present in
the codebase — that reviewer item is already resolved.

---

## Issues and Fixes

### 1. `PipelineShape` → Immutable record

**Problem:** All properties have public setters. A caller can mutate a shape after passing it,
and there is no enforcement that all fields are set before use.

**Fix:** Convert `PipelineShape` from a mutable class to a `sealed record` with `required init`
properties. Existing object-initializer syntax (`new PipelineShape { ... }`) is unchanged for
callers.

---

### 2. `GetHandleMethodTypeParamCount` → Walk base type chain

**Problem:** `symbol.GetMembers()` returns only members declared directly on the type. A behavior
that inherits its `Handle` method from a base class returns `-1` and is incorrectly flagged as
invalid by `PipelineDiagnosticRules`.

**Fix:** After checking the declaring type, walk up `symbol.BaseType` recursively until `object`
or `null` is reached. Return the type parameter count of the first matching `Handle` method found.

---

### 3. `ResolveAttributeClassFromSyntax` → Null on ambiguity

**Problem:** When searching all syntax trees for a class matching an attribute name, the method
returns the first match found. Two classes with the same name in different namespaces would cause
silent mis-attribution.

**Fix:** Collect all candidates. If exactly one match is found, return it. If zero or more than
one match is found, return `null` (safe conservative fallback — the behavior is simply not
attributed).

---

### 4. Fix `Assert.Contains` → Exact FQN assertion

**Problem:** `PipelineBehaviorDiscovererTests.cs:69` asserts `Assert.Contains("MyModel", ...)`.
This passes even if the returned string is an unexpected FQN. The intent is to verify the FQN is
resolved correctly.

**Fix:** Change to `Assert.Equal("global::MyModel", results[0].AppliesTo)`.

---

### 5. Add test for `TypeKind.Error` fallback path

**Problem:** `ResolveAttributeClassFromSyntax` is documented as the fallback for incomplete
compilations, but no test actually forces this code path. The existing `Discover_SubclassedAttribute_IsDetected`
test includes the `ZeroAlloc.Pipeline` metadata reference, so the error-type branch may never be
exercised.

**Fix:** Add a new test `Discover_SubclassedAttribute_WithIncompleteCompilation_IsDetected` that
creates a compilation *without* the `ZeroAlloc.Pipeline` metadata reference. The test defines a
local `PipelineBehaviorAttribute` subclass so the attribute resolves as `TypeKind.Error`, forcing
the syntax fallback path.

---

### 6. Extract indentation constants in `PipelineEmitter`

**Problem:** Indentation whitespace strings (e.g. `"\n                "`) are hardcoded inline in
string interpolations throughout `EmitChain`. They are unnamed and inconsistent to maintain.

**Fix:** Extract to `private const string` fields (`Indent1`, `Indent2`, etc.) at the top of the
class.

---

### 7. Lazy semantic model acquisition in `Discover`

**Problem:** `compilation.GetSemanticModel(syntaxTree)` is called unconditionally for every
syntax tree before checking whether it contains any attributed classes. This allocates a semantic
model for trees with no relevant content.

**Fix:** Move the `GetSemanticModel` call inside the loop body, after the attributed-class check
confirms at least one candidate exists in the tree.

---

## Success Criteria

- All 18 existing tests remain green.
- New test `Discover_SubclassedAttribute_WithIncompleteCompilation_IsDetected` passes and
  verifiably exercises the `TypeKind.Error` branch.
- New test `Discover_BehaviorWithInheritedHandle_ReturnsCorrectTypeParamCount` passes and
  verifiably exercises the base-type walking branch.
- `PipelineShape` is a record; no public mutable setters remain.
- `ResolveAttributeClassFromSyntax` returns `null` when multiple candidates match.
