

```csharp
/// <summary>Throws if the confinement plan contains flags or leading items.</summary>
private static void MustCarryNoFlags(ConsultantLaunch launch)
{
    if (launch.Confinement is LaunchConfinement.Planned { Plan: { Flags.Count: > 0 } or { Leading.Count: > 0 } })
    {
        throw new ArgumentException("a Kimi row's confinement is the shim's; a plan with flags is not one coai made for kimi", nameof(launch));
    }
}
```