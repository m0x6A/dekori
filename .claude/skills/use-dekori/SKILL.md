---
name: use-dekori
description: Work effectively in the Dekori repository. Use when exploring, changing, testing, or documenting Dekori's attribute-driven .NET instrumentation library.
---

# Use Dekori

Use this skill when the task is about **Dekori itself** rather than a consumer app.

## Repository map

- `src/Dekori/` — the library
- `tests/Dekori.Tests/` — BDD/TDD specs and support fixtures
- `samples/Dekori.Demo/` — runnable demo that shows emitted spans, metrics, and logs
- `docs/` — DocFX content

## Working rules

- Read `CLAUDE.md` first and follow it exactly.
- Use Microsoft C# conventions, file-scoped namespaces, one top-level type per file, and braces on every control-flow block.
- Write tests first for behaviour changes. Specs live under `tests/Dekori.Tests/` and use xUnit + Shouldly + NSubstitute with the `Specification` base class.
- Prefer real telemetry assertions via `TelemetryProbe` over mocking internals.
- Keep the interceptor hot path simple: resolve reflection and attribute decisions in the plan cache, not in `DekoriInterceptor`.

## Useful companion skills

- `add-spec` — add or update a Dekori BDD specification
- `add-instrumentation` — add a new instrumentation attribute/behavior
- `check-conventions` — audit changed C# files against `CLAUDE.md`
- `verify-and-release` — run `dotnet build`, `dotnet test`, and the demo verification flow

## Common workflow

1. Read `README.md`, `CLAUDE.md`, and the relevant source and spec files.
2. For library behaviour changes, start with a failing spec in `tests/Dekori.Tests/`.
3. Make the smallest change that satisfies the scenario.
4. Run `dotnet build` and `dotnet test`.
5. For observable behaviour changes, run `dotnet run --project samples/Dekori.Demo`.

## Common touchpoints

- `src/Dekori/DekoriInterceptor.cs`
- `src/Dekori/Instrumentation/InstrumentationPlanCache.cs`
- `src/Dekori/Instrumentation/InstrumentationPlan.cs`
- `src/Dekori/DekoriServiceCollectionExtensions.cs`
- `tests/Dekori.Tests/Support/TestHost.cs`
- `tests/Dekori.Tests/Support/TelemetryProbe.cs`
