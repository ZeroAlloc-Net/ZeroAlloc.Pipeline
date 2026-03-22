# Changelog

## [0.1.5](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/compare/v0.1.4...v0.1.5) (2026-03-22)


### Bug Fixes

* correct dotnet-version format (10.0.x not net10.0) ([b70495a](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/b70495aa9fbae3728d8be5e7ff4cd734c109fa50))

## [0.1.4](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/compare/v0.1.3...v0.1.4) (2026-03-19)


### Bug Fixes

* update PackageProjectUrl and trigger CI on release-please branches ([896cd69](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/896cd693c08aec3ae4a448ad6218e5ea3b578bf1))

## [0.1.3](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/compare/v0.1.2...v0.1.3) (2026-03-19)


### Bug Fixes

* update PackageProjectUrl to docs site ([760c6b0](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/760c6b05901c36bae482ebc615921afe21724897))
* update PackageProjectUrl to docs site ([51bcaab](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/51bcaab375f033d715ad67074957bba49a1731a7))

## [0.1.2](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/compare/v0.1.1...v0.1.2) (2026-03-17)


### Bug Fixes

* add PackageReadmeFile and PackageIcon to show README on NuGet.org ([7dd8823](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/7dd882369443ec6f6c85ec56d6a7e5dfa1713f83))
* add PackageReadmeFile and PackageIcon to show README on NuGet.org ([1f89051](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/1f890511a6ea789fa664862105b1e4de6b18cd16))

## [0.1.1](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/compare/v0.1.0...v0.1.1) (2026-03-17)


### Features

* add benchmark project and update performance results with real numbers ([d059d7c](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/d059d7c9b2d0d38e61ae4567ac6734560a90aebe))
* add IPipelineBehavior and PipelineBehaviorAttribute ([f0d7788](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/f0d7788f837147418a781a533e57dfd500708d51))
* add PipelineBehaviorDiscoverer ([9801268](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/9801268ed0e957748c8c78a0956ee683b67db7bd))
* add PipelineBehaviorInfo data class ([9cde2a3](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/9cde2a3f75d8c2621b1bef51e96d257dd00d9246))
* add PipelineDiagnosticRules ([8e07fa4](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/8e07fa4ab5539fc49121456379de22ae428fa549))
* add PipelineShape and PipelineEmitter ([b46cd06](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/b46cd06c5b243d85e0b41eab70d96008fb8cdaaf))
* incremental discovery, InnermostBodyFactory, fallback test ([ba06162](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/ba06162fc2c8fa02ba773fd6b68fa4c7aa7f7ad6))


### Bug Fixes

* add == and != operator overloads to PipelineBehaviorInfo ([889b05b](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/889b05b59c1316a0ec523de26119027d16012454))
* add input validation to PipelineEmitter ([7080390](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/7080390c1d215806f4534e385f29b59d7468185f))
* correct ITypeSymbol cast in ReadAppliesTo and simplify InheritsFrom loop ([a5fdf85](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/a5fdf85765eb616237aef8bbd6def6b7a09c9f9e))
* return null from ResolveAttributeClassFromSyntax when multiple name matches exist ([1cb1226](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/1cb122691022a918cb8ab61bd1c29c0634ea6cdb))
* validate InnermostBodyTemplate is not null or empty in PipelineEmitter ([7630a2e](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/7630a2e8d3f42c3ae7a7a9fd798d94d3feaf7ddc))
* walk base type chain in GetHandleMethodTypeParamCount to find inherited Handle methods ([374d3a3](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/374d3a37efb40cbbf257127e0200a8ca2237b1cd))


### Performance

* skip semantic model acquisition for syntax trees with no attributed classes ([47add82](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/47add8263d5d6d428bcd4cd0960a532c75252624))


### Refactoring

* convert PipelineShape to immutable record with required init properties ([3972279](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/3972279dcd40cda8237175603fe901fbca9da6b4))
* extract indentation constants in PipelineEmitter ([8aac28f](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/8aac28fe2c84e3602f07d362ccbb6b6fd89c3472))


### Documentation

* add cookbook pages (01–05) ([e8315af](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/e8315af45877979beac827b379152ac793493d37))
* add design doc for code review fixes ([978860c](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/978860c7b8285b08eca0bf66948349e1be8eaf90))
* add diagnostics page ([34500ca](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/34500cae3b1be23a4f6f3b8b88c27915b946573f))
* add docs index page ([3f9c4ea](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/3f9c4ea2d03e5f932a678ea2e84cfb10605588d1))
* add documentation design doc ([1615e92](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/1615e921f3b7a0a2d72e2a0bf8572c03a8d67e45))
* add documentation implementation plan ([ede3826](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/ede3826c5498889d0f38e8a74a454e04b8fc7c6e))
* add getting-started page ([5e71dbd](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/5e71dbd9c51da107359b6bb39f9a857f50c17eec))
* add implementation plan for code review fixes ([276ca74](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/276ca743d0881b41893deac8dda6e8450ce04b2a))
* add performance page ([a0fd7dd](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/a0fd7dd70ef841bea1bb38321d5777c753eb9fb3))
* add pipeline-behaviors page ([ed47703](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/ed47703dbd37cb2e4cc7ff297cd8d9a8a92180eb))
* add pipeline-discoverer page ([6b39d45](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/6b39d45ea8729833f308b8e7edbe251ab17c7b7e))
* add pipeline-emitter page ([5e5fd8b](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/5e5fd8b077bd5325d4abadd7b83a1c8481217ccc))
* add pipeline-shape page ([75a26ef](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/75a26ef3cf64f8c4a99a37c3897c18d405cb6c83))
* add README.md ([51e82e3](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/51e82e31dd6c77821dc47a7dc0e80a00f89da91d))
* add testing page ([2c42381](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/2c4238161983f21e35641f3c2db7bf56e45a44ee))
* clarify PipelineBehaviorAttribute subclassing intent and AppliesTo usage ([3aaa59b](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/3aaa59b08316c85ac700f7385864910fda9b424b))
* fix cookbook frontmatter, diagnostics suppression note, and type errors ([cb25d23](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/cb25d23f1a15d0aa68b4175ce9bd20e0323c78b1))
* fix README feature bullets — clarify diagnostic helpers and add PipelineShape ([fde4425](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/fde4425a7676411e75205b8973d983de56d6b8c8))


### Tests

* tighten AppliesTo assertion to require exact FQN ([77ae077](https://github.com/ZeroAlloc-Net/ZeroAlloc.Pipeline/commit/77ae07792d4215186ae7850792f498f8c50edb92))
