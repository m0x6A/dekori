---
name: check-conventions
description: Check changed C# files against Dekori's mandatory CLAUDE.md coding conventions (bracketed control flow, one type per file, file-scoped namespaces, naming, XML docs, YAGNI). Use after editing src/Dekori, tests, or the demo, before committing, or when asked to review style/conventions. The repo has no .editorconfig/analyzer, so this fills that gap.
---

# Check Dekori coding conventions

Dekori has **no `.editorconfig` or analyzer** — its conventions live only in `CLAUDE.md` and are not
enforced by the compiler (CI's `-warnaserror` catches warnings, not style). This skill audits the
**changed** C# files against those mandatory rules.

## Scope
Default to the working diff:
```bash
git diff --name-only HEAD -- '*.cs'
git diff --name-only --cached -- '*.cs'
```
Also include untracked `*.cs` (`git status --porcelain`). If the user names files, check those.
Read each file and report findings — **read-only by default**.

## Rules to check

1. **Bracketed control flow (most common violation).** Every `if`, `else`, `for`, `foreach`,
   `while` uses braces — even one-line bodies and guard clauses. Flag any brace-less control flow:
   ```csharp
   if (activity is null) return null;          // ❌
   if (activity is null) { return null; }       // ✓ (own lines preferred)
   ```
   Expression-bodied members (`=> ...`) and ternaries are fine — they are not control-flow statements.

2. **One top-level type per file**, filename == type name. A type's *private nested* helper may stay
   with its parent; two top-level `class`/`record`/`interface`/`enum` in one file is a violation.

3. **File-scoped namespaces** (`namespace Dekori;`), not block-bodied (`namespace Dekori { }`).

4. **`using` directives outside the namespace** (above the `namespace` line).

5. **Naming.** `PascalCase` types/methods/properties; `camelCase` locals/parameters; `_camelCase`
   private fields; `I`-prefixed interfaces.

6. **Public APIs carry XML doc comments** (`/// <summary>`). Especially in `src/Dekori` —
   `GenerateDocumentationFile` is on there and DocFX publishes them. Internal/test types are exempt
   from strict doc coverage but still benefit.

7. **YAGNI / keep it simple.** Flag speculative abstraction, unused config "just in case", or a new
   primitive where an existing one (settings record, `DekoriTelemetry` factory method, test
   double) would do.

## Output
Group by file; cite `file:line`, the rule, and a concrete fix:
```
src/Dekori/FooAttribute.cs
  L23  Bracketed control flow — `if (x) return;` → wrap body in braces
  L1   File-scoped namespace — convert `namespace Dekori { ... }` to `namespace Dekori;`
```
End with a one-line verdict (clean, or N findings across M files).

## Optional `--fix`
When invoked with `--fix`, apply only the **mechanical, safe** corrections (add missing braces,
convert to file-scoped namespace, move `using`s out) and leave judgement calls (naming, splitting
files, YAGNI) as reported findings. After fixing, run `dotnet build` to confirm 0 warnings.

## Optional follow-up
If violations are recurring, suggest committing an `.editorconfig` that encodes these rules
(`csharp_prefer_braces = true:warning`, `csharp_style_namespace_declarations = file_scoped:warning`,
etc.) so the IDE and CI enforce them natively — which would make most of this skill redundant.
