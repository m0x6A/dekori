# CLAUDE.md

Guidance for working in the **Dekori** repository. These conventions are mandatory — follow them
for every change.

## Project

Dekori is an attribute-driven instrumentation library for .NET. Decorating classes/methods with
attributes (`[Trace]`, `[Metric]`, `[CaptureException]`, `[LogCall]`, `[Instrument]`) emits
OpenTelemetry spans, metrics and logs via Castle DynamicProxy interception through the DI container.

```
src/Dekori/            # the library
samples/Dekori.Demo/   # runnable console demo wiring OpenTelemetry console exporters
tests/Dekori.Tests/    # BDD/TDD specs
```

## Commands

```bash
dotnet build            # must be clean: 0 warnings, 0 errors
dotnet test             # all specs must pass
dotnet run --project samples/Dekori.Demo
```

## Coding standards

- **Follow Microsoft conventions.** Adhere to the
  [C# coding conventions](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions)
  and the [.NET Framework Design Guidelines](https://learn.microsoft.com/dotnet/standard/design-guidelines/).
  PascalCase for types/methods/properties, camelCase for locals/parameters, `_camelCase` for private
  fields, `I`-prefixed interfaces, file-scoped namespaces, `using` directives outside the namespace.
- **Always use bracketed `if`s.** Every `if`, `else`, `for`, `foreach`, `while` uses braces — even
  one-line bodies and guard clauses. No brace-less control flow.
  ```csharp
  if (activity is null)
  {
      return null;
  }
  ```
- **One class/record/interface/enum per file.** The file name matches the type name. (A type's own
  *private nested* helper may stay with its parent; top-level types do not share a file.)
- **Keep it simple.** Prefer the smallest design that satisfies the requirement. No speculative
  abstraction or configuration "just in case" (YAGNI). Reach for an existing primitive before adding
  a new one.
- `Nullable` and `ImplicitUsings` are enabled. Public APIs carry XML doc comments.

## Testing — TDD + BDD

Write tests first (TDD), expressed as behaviour specifications (BDD).

- **Frameworks:** [xUnit](https://xunit.net) (runner), [Shouldly](https://docs.shouldly.org)
  (assertions), [NSubstitute](https://nsubstitute.github.io) (test doubles). Do not introduce other
  test/assertion/mocking libraries.
- **BDD shape:** scenarios derive from `Specification` (`tests/.../Support/Specification.cs`) and use
  `Given` / `When` / `Then`. Class names read as behaviour (`When_an_instrumented_method_throws`) and
  each `[Fact]` is a `Then_...` assertion. One scenario (class) per file.
- **Assert real signals, not mocks, where possible.** `TelemetryProbe` captures genuine in-process
  spans/metrics via `ActivityListener`/`MeterListener`; prefer it over asserting on internals.
- **Shouldly** for all assertions (`result.ShouldBe(...)`, `Should.Throw<T>(...)`). **NSubstitute**
  for collaborators (`Substitute.For<T>()`, `.Returns(...)`, `.Received(...)`).

## Definition of done

`dotnet build` is clean (0 warnings), `dotnet test` is fully green, and — for behavioural changes —
`dotnet run --project samples/Dekori.Demo` shows the expected spans/metrics/logs.

## Mandatory critique

**Every non-trivial change must be critiqued by the `csharp-critic` agent before it is considered
done — no exceptions.** Do not assume your first approach is correct. After implementing a change
(and before declaring it finished or proposing a commit), delegate the diff to `csharp-critic` and
address its findings: fix every **Blocker**, fix or explicitly justify every **Should-fix**, and
answer every **Question/assumption** it raises. The critic reviews best practices, maintainability,
and performance, and challenges the assumed path forward. "Trivial" means typos, comments, or
formatting only — anything touching behaviour, public surface, or the hot path is non-trivial.
