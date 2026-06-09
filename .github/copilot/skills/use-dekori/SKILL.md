---
name: use-dekori
description: Use Dekori's repository structure, conventions, and validation flow when working on the library, tests, demo, or docs.
applyTo:
  - "src/Dekori/**/*.cs"
  - "tests/Dekori.Tests/**/*.cs"
  - "samples/Dekori.Demo/**/*.cs"
  - "README.md"
  - "docs/**/*.md"
---

# Use Dekori

Apply this skill when working in the Dekori repository.

## Repository map

- `src/Dekori/` — library code
- `tests/Dekori.Tests/` — BDD specifications and test support types
- `samples/Dekori.Demo/` — console demo that surfaces telemetry
- `docs/` — DocFX documentation

## Required conventions

- Read and follow `CLAUDE.md`.
- Keep changes small and simple.
- In C#, use file-scoped namespaces, one top-level type per file, and braces on every `if`, `else`, `for`, `foreach`, and `while`.
- Public APIs in `src/Dekori/` need XML doc comments.
- For behavioural changes, add or update a BDD-style spec first.

## Testing and validation

- Build with `dotnet build`.
- Run specs with `dotnet test`.
- For behavioural telemetry changes, run `dotnet run --project samples/Dekori.Demo` and confirm the expected spans, metrics, and logs appear.

## Important implementation areas

- `src/Dekori/DekoriInterceptor.cs` — runtime interception pipeline
- `src/Dekori/Instrumentation/InstrumentationPlanCache.cs` — attribute resolution and cached plans
- `src/Dekori/Instrumentation/InstrumentationPlan.cs` — resolved plan model
- `tests/Dekori.Tests/Support/TelemetryProbe.cs` — in-process telemetry capture
- `tests/Dekori.Tests/Support/TestHost.cs` — isolated test host setup
