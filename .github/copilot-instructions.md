## GitHub Copilot instructions for LogManager

These guidelines tailor Copilot’s suggestions for this repository. Follow them to keep contributions consistent, safe, and production-ready.

### What this repo is
- .NET 9 class library providing a unified Serilog configuration for microservices.
- Sinks: Console, File (daily rolling), Elasticsearch, Grafana Loki.
- Extras: rich enrichers (service info, container, exceptions), HTTP correlation ID middleware, gRPC interceptor.

Key files:
- `src/LogManager/LoggerConfigurator.cs` – single source of truth for configuring Serilog, enrichers, and sinks.
- `src/LogManager/Extensions/LogManagerExtensions.cs` – DI/host integration helpers: `AddLogManager(...)`, `UseLogManager(...)`, `AddLogManagerFilePath(...)`, `CreateBootstrapLogger(...)`.
- `src/LogManager/Middleware/*` – HTTP correlation ID middleware + extension.
- `src/LogManager/Interceptors/CorrelationIdInterceptor.cs` – gRPC correlation propagation.
- `src/LogManager/Configuration/*` – Strongly-typed options bound from `"LogManager"` config section.
- Tests under `tests/LogManager.Tests/*` using NUnit, FluentAssertions, NSubstitute, and Testcontainers.

### Tech & constraints
- Target: .NET 9.0. Nullable reference types and implicit usings are enabled.
- Logging: Serilog + sinks (Console/File/Elasticsearch/Loki) and enrichers (Environment/Thread/Process/Exceptions).
- Prefer async APIs. Always prefer `await Log.CloseAndFlushAsync()` over the sync variant in tests and samples.
- Treat file paths in `FileLoggingOptions.Path` as directory roots; ensure directories exist when expanding.
- Elasticsearch `IndexFormat` requires double braces for the date portion (e.g., `logs-myapp-{{0:yyyy.MM.dd}}`) because the app name is injected via `string.Format` first.
- Don’t hardcode credentials; prefer environment variables/secret managers.

### Public API surface (don’t break without reason)
- Options classes in `Configuration/` (e.g., `LogManagerOptions`, `FileLoggingOptions`, `ElasticsearchOptions`, `LokiOptions`, `EnrichmentOptions`).
- DI extensions in `LogManagerExtensions`:
	- `IServiceCollection AddLogManager(IConfiguration, string sectionName = "LogManager")`
	- `IServiceCollection AddLogManager(Action<LogManagerOptions>)`
	- `IServiceCollection AddLogManagerFilePath(string logDirectoryPath)`
	- `IHostBuilder UseLogManager(string sectionName = "LogManager")`
	- `IHostBuilder UseLogManager(Action<LogManagerOptions>)`
	- `ILogger CreateBootstrapLogger(string applicationName = "BootstrapApp", string environment = "Development")`
- Middleware: `IApplicationBuilder UseCorrelationId()`
- gRPC interceptor: `CorrelationIdInterceptor`

If you must change these, search usages in tests and update docs (`README.md` files) accordingly.

### Coding patterns to follow
- Keep all sink/enricher wiring in `LoggerConfigurator`. Avoid scattering configuration logic.
- Use structured logging templates (`"Processed order {OrderId}"`).
- Enrichment defaults are on; new enrichers should be opt-in via `EnrichmentOptions` unless universally safe.
- When adding sinks:
	- Add a new options POCO (if needed) under `Configuration/`.
	- Extend `ConfigureSinks(...)` in `LoggerConfigurator` behind a guarded `Enabled` option.
	- Expose minimal, reasonable defaults and batching/period/queue knobs.
- Don’t change the HTTP middleware to static; it must be an instance method to access `_next`.

### Tests: how to write them
- Use NUnit and FluentAssertions.
- For I/O and containers, prefer resilient waits over sleeps. Use helpers in `tests/LogManager.Tests/Infrastructure/TestHelpers.cs`.
- Ensure isolation:
	- File tests: write under a unique temp directory per test.
	- Elasticsearch/Loki tests: use test-specific indices/labels.
- Always await `Log.CloseAndFlushAsync()` at the end of tests to ensure all events are flushed before assertions.
- Use Testcontainers for Elasticsearch and Loki (see existing tests). Gate container-heavy tests by category/filters if needed.

### Common tasks cookbook (for Copilot)
1) Add a new sink
	 - Create `Configuration/NewSinkOptions.cs` with `Enabled` and core properties.
	 - Update `LoggerConfigurator.ConfigureSinks(...)` to call `ConfigureNewSink(...)` when enabled.
	 - Add sane defaults. Provide output template or formatter if applicable.
	 - Add unit/integration tests under `tests/LogManager.Tests/`.

2) Add a new enricher
	 - Create an enricher under `src/LogManager/Enrichers/`.
	 - Add an opt-in flag/property under `EnrichmentOptions` (default false unless clearly safe).
	 - Wire it in `ConfigureEnrichers(...)` conditional on the option.
	 - Add focused tests verifying the new property appears in outputs.

3) Extend correlation ID support
	 - Keep headers/metadata keys backward compatible (`X-Correlation-ID`, `X-Request-ID`, gRPC `x-correlation-id`).
	 - Update both middleware and interceptor if semantics change. Add tests.

4) Update configuration docs
	 - Modify root `README.md` and `src/LogManager/README.md` with new settings.
	 - Include `appsettings.json` and code-based examples.

### Build, test, and quality gates
- After substantive changes, run:
	- Build: `dotnet build`
	- Tests (quick): `dotnet test tests/LogManager.Tests/LogManager.Tests.csproj -c Debug`
	- Filtered tests (containers): `dotnet test --filter FullyQualifiedName~ElasticsearchLoggingTests`
- Green-before-done: don’t leave broken builds. Fix warnings, especially nullable ones.

### PR checklist (Copilot should propose this on new PRs)
- [ ] Build passes locally
- [ ] All tests pass (or explain skips with clear reason)
- [ ] No new analyzer warnings
- [ ] Updated README(s) and inline XML docs for public API changes
- [ ] Considered performance and batching settings for new sinks
- [ ] No secrets in code or configs

### Style & tone for generated docs/examples
- Concise and task-oriented. Prefer minimal, runnable examples.
- Use the existing option names and defaults from the `Configuration/` classes.
- For Elasticsearch examples, remember the double-brace date token.

### Things to avoid
- Introducing global mutable state beyond Serilog’s well-known bootstrapping pattern.
- Blocking I/O in hot paths; prefer async.
- Tight coupling to specific hosting models; keep it generic across Web/Worker/gRPC.

### Where to ask Copilot for help (examples)
- “Add a new sink options class and wire it in LoggerConfigurator.”
- “Create a test that verifies File sink rolls over at size limit.”
- “Add a custom enricher that adds tenant info from an HTTP header.”
- “Update README with Loki configuration example including labels.”

By following these rules, Copilot suggestions should align with this repo’s architecture and keep contributions consistent.
