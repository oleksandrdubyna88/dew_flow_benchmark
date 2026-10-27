You write C# (.NET 10, latest C#). Output ONLY one code block with ONE method, no class, no usings, no explanation.

Write `private static void MustCarryNoFlags(ConsultantLaunch launch)` for a sealed class. Facts:
- `launch.Confinement` is of the abstract type `LaunchConfinement`; its case `LaunchConfinement.Planned` is a sealed record `Planned(Confinement.Planned Plan)`.
- `Confinement.Planned` has two list properties `Flags` and `Leading` (both `IReadOnlyList<string>`; they have `.Count`).
- Spec: when the launch's confinement is a `LaunchConfinement.Planned` whose `Plan.Flags` OR `Plan.Leading` is not empty, throw `new ArgumentException("a Kimi row's confinement is the shim's; a plan with flags is not one coai made for kimi", nameof(launch))`. Otherwise do nothing.
- Style: one `if` with a single property pattern, `LaunchConfinement.Planned { Plan: { Flags.Count: > 0 } or { Leading.Count: > 0 } }`, braces always, a one-line `<summary>` doc comment above. No other code.
