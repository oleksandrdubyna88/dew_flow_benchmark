Here's a thinking process:

1.  **Analyze User Input:**
   - **Language/Framework:** C# (.NET 10, latest C#, xUnit v3 + FluentAssertions)
   - **Output Format:** ONLY code blocks, each preceded by one line naming where it goes. No explanations.
   - **Existing Code:** `KimiConsultant.cs` in `src_mcp/runners/Kimi/`
     - Contains `Build` method and `Cwd` helper.
     - `LaunchConfinement.Planned` has `Flags` and `Leading` properties (both `IReadOnlyList<string>`).
   - **Task A (Production):**
     - Add `private static void MustCarryNoFlags(ConsultantLaunch launch)`
     - Style: switch/pattern, cyclomatic complexity <= 4, no nested ifs.
     - Called as second line in `Build`, right after `ConsultantLaunches.MustBeLaunchable(launch);`
     - Throws `ArgumentException` with message: `"a Kimi row's confinement is the shim's; a plan with flags is not one coai made for kimi"` (param name `nameof(launch)`)
     - Refuses if `Flags` or `Leading` is non-empty.
   - **Task B (Tests):**
     - New class `KimiQuestionLaunchTests` in its own file.
     - Namespace: `CoaiMcp.Tests` (file-scoped)
     - Usings: `CoaiMcp.Core.QuestionConsult`, `CoaiMcp.Runners.Acp`, `CoaiMcp.Runners.Consultation`, `CoaiMcp.Runners.Kimi`, `CoaiMcp.Runners.Platform`, `CoaiMcp.Runners.Reviewers`
     - Helpers provided in prompt: `RowFolder`, `RowTree`, `Planned`, `RowCwd`, `Consultant()`
     - Need to set up `Scratch = RowCwd` and `Workdir = RowTree` in `Planned`.
     - Two tests:
       1. `AKimiQuestionLaunch_IsTheShimRequest_InItsFolderOrItsCopy_OnTheRowsHome`
          - Tests `Planned("kimi", CapabilityGrant.None)` -> WorkingDirectory is `RowCwd`
          - Tests `Planned("kimi", CapabilityGrant.Disk(RootA, RootB))` -> WorkingDirectory is `RowTree`
          - Both: `--home` value's parent dir is `RowFolder`
          - No `--session` argument
          - No argument equals/contains RootA or RootB
          - `InheritsEnvironment` is false
       2. `APlanWithFlags_IsRefusedForKimi`
          - Real none plan with `Flags = ["--yolo"]` -> throws `ArgumentException` containing "shim"
          - Plan with `Leading = ["--search"]` -> throws
     - Constants: `RootA = "D:/projects/alpha"`, `RootB = "D:/projects/beta"`
     - Cross-platform compatible.
     - xUnit v3 syntax (attributes like `[Fact]`, `[Theory]`, etc., but here just `[Fact]`)
     - FluentAssertions for assertions.

2.  **Deconstruct Task A (Production Code Changes):**
   - Need to modify `KimiConsultant.cs` slightly: add the call to `MustCarryNoFlags(launch);` as the second line in `Build`.
   - Add the new method `MustCarryNoFlags`.
   - Let's draft `MustCarryNoFlags`:
     ```csharp
     private static void MustCarryNoFlags(ConsultantLaunch launch)
     {
         if (launch.Confinement is not LaunchConfinement.Planned planned) return;
         if (planned.Flags.Count > 0 || planned.Leading.Count > 0)
             throw new ArgumentException("a Kimi row's confinement is the shim's; a plan with flags is not one coai made for kimi", nameof(launch));
     }
     ```
     Wait, the prompt says "switch/pattern, cyclomatic complexity <= 4, no nested ifs". The above uses an early return and a simple condition. That's fine. To strictly follow "switch/pattern", I could use pattern matching:
     ```csharp
     private static void MustCarryNoFlags(ConsultantLaunch launch)
     {
         var planned = launch.Confinement as LaunchConfinement.Planned;
         if (planned is not null && (planned.Flags.Count > 0 || planned.Leading.Count > 0))
             throw new ArgumentException("a Kimi row's confinement is the shim's; a plan with flags is not one coai made for kimi", nameof(launch));
     }
     ```
     Or using `is not null` pattern:
     ```csharp
     private static void MustCarryNoFlags(ConsultantLaunch launch)
     {
         if (launch.Confinement is LaunchConfinement.Planned { Flags.Count: > 0 or > 0, Leading.Count: > 0 or > 0 } p)
             throw new ArgumentException("a Kimi row's confinement is the shim's; a plan with flags is not one coai made for kimi", nameof(launch));
     }
     ```
     Actually, simpler:
     ```csharp
     private static void MustCarryNoFlags(ConsultantLaunch launch)
     {
         if (launch.Confinement is LaunchConfinement.Planned { Flags.Count: > 0 or > 0, Leading.Count: > 0 or > 0 } _)
             throw new ArgumentException("a Kimi row's confinement is the shim's; a plan with flags is not one coai made for kimi", nameof(launch));
     }
     ```
     Wait, `Flags.Count > 0 || Leading.Count > 0` is easier to read. The prompt says "in the style of the existing private helpers (switch/pattern, cyclomatic complexity <= 4, no nested ifs)". I'll stick to a straightforward pattern match or early return. I'll use:
     ```csharp
     private static void MustCarryNoFlags(ConsultantLaunch launch)
     {
         if (launch.Confinement is LaunchConfinement.Planned { Flags.Count: > 0 or > 0, Leading.Count: > 0 or > 0 } _)
             throw new ArgumentException("a Kimi row's confinement is the shim's; a plan with flags is not one coai made for kimi", nameof(launch));
     }
     ```
     Actually, `Flags.Count > 0 || Leading.Count > 0` is fine. I'll just write it clearly.
     Let's adjust `Build` to include the call:
     ```csharp
     public ReviewerInvocation Build(ConsultantLaunch launch)
     {
         ConsultantLaunches.MustBeLaunchable(launch);
         MustCarryNoFlags(launch);
         // ... rest unchanged
     }
     ```
     I will output the full updated `KimiConsultant.cs` or just the relevant parts? The prompt says "Give me the new method and the exact new line." I'll provide the full file content for clarity, or just the method and the modified Build. I'll provide the full file to be safe, or just the additions. I'll stick to providing the full file as requested by "Output ONLY code blocks, each preceded by one line naming where it goes."

3.  **Deconstruct Task B (Test Class):**
   - Need to construct `KimiQuestionLaunchTests.cs`.
   - File-scoped namespace: `namespace CoaiMcp.Tests;`
   - Usings: `global using System; global using System.IO; global using System.Linq; global using System.Threading.Tasks; global using CoaiMcp.Core.QuestionConsult; global using CoaiMcp.Runners.Acp; global using CoaiMcp.Runners.Consultation; global using CoaiMcp.Runners.Kimi; global using CoaiMcp.Runners.Platform; global using CoaiMcp.Runners.Reviewers; global using FluentAssertions; global using Xunit;` (xUnit v3 uses `[Fact]`, `[Theory]`, etc. same as v2, but maybe `Xunit.v3` attributes? The prompt says xUnit v3, but syntax is largely the same. I'll stick to standard `[Fact]`.)
   - Helpers from prompt need to be adapted:
     - `RowFolder`, `RowTree`, `RowCwd`
     - `Planned` helper needs `Scratch = RowCwd` and `Workdir = RowTree`
     - `Consultant()` helper
     - `RootA`, `RootB` constants
   - Test 1: `AKimiQuestionLaunch_IsTheShimRequest_InItsFolderOrItsCopy_OnTheRowsHome`
     - Needs to test both `CapabilityGrant.None` and `CapabilityGrant.Disk(RootA, RootB)`
     - Check `WorkingDirectory`
     - Check `--home` parent dir
     - Check no `--session`
     - Check no RootA/RootB in arguments
     - Check `InheritsEnvironment == false`
   - Test 2: `APlanWithFlags_IsRefusedForKimi`
     - Create a launch with `Flags = ["--yolo"]` on a none plan
     - Assert `Build` throws `ArgumentException` with message containing "shim"
     - Same for `Leading = ["--search"]`
   - Let's draft the test class carefully.

   Structure:
   ```csharp
   namespace CoaiMcp.Tests;

   using System;
   using System.IO;
   using System.Linq;
   using CoaiMcp.Core.QuestionConsult;
   using CoaiMcp.Runners.Acp;
   using CoaiMcp.Runners.Consultation;
   using CoaiMcp.Runners.Kimi;
   using CoaiMcp.Runners.Platform;
   using CoaiMcp.Runners.Reviewers;
   using FluentAssertions;
   using Xunit;

   public class KimiQuestionLaunchTests
   {
       private const string RootA = "D:/projects/alpha";
       private const string RootB = "D:/projects/beta";
       private static readonly string RowFolder = Path.Combine(Path.GetTempPath(), "coai-question-" + Guid.NewGuid().ToString("N"));
       private static readonly string RowTree = Path.Combine(RowFolder, "tree");
       private static readonly string RowCwd = Path.Combine(RowFolder, "cwd");

       private static ConsultantLaunch Planned(string runtime, CapabilityGrant grant, string model = "") => new(
           "repo", "the question", string.Empty, "answers", new ReviewerSettings(runtime) { Model = model, OwnedScratch = RowFolder })
       {
           Confinement = Plan(runtime, grant),
           Workdir = RowTree,
           Scratch = RowCwd,
       };

       private static Confinement.Planned Plan(string runtime, CapabilityGrant grant)
       {
           var plan = ConfinementPlanner.Plan(runtime, grant);
           plan.Should().BeOfType<Confinement.Planned>();
           return (Confinement.Planned)plan;
       }

       private static KimiConsultant Consultant() => new(new KimiRuntime("kimi", HostKind.Linux,
           new KimiSeams(root => KimiPlaces.ForShim(root, Path.Combine(RowFolder, "persons-bin")), () => ("coai-mcp", Array.Empty<string>()))));

       [Fact]
       public void AKimiQuestionLaunch_IsTheShimRequest_InItsFolderOrItsCopy_OnTheRowsHome()
       {
           var noneLaunch = Planned("kimi", CapabilityGrant.None);
           var diskLaunch = Planned("kimi", CapabilityGrant.Disk(RootA, RootB));

           var noneInv = Consultant().Build(noneLaunch);
           var diskInv = Consultant().Build(diskLaunch);

           noneInv.Request.WorkingDirectory.Should().Be(RowCwd);
           diskInv.Request.WorkingDirectory.Should().Be(RowTree);

           var homeArg = noneInv.Request.Arguments.FirstOrDefault(a => a.StartsWith("--home="));
           homeArg.Should().NotBeNull();
           Path.GetDirectoryName(homeArg!.Substring("--home=".Length)).Should().Be(RowFolder);

           homeArg = diskInv.Request.Arguments.FirstOrDefault(a => a.StartsWith("--home="));
           homeArg.Should().NotBeNull();
           Path.GetDirectoryName(homeArg.Substring("--home=".Length)).Should().Be(RowFolder);

           noneInv.Request.Arguments.Should().NotContain("--session");
           diskInv.Request.Arguments.Should().NotContain("--session");

           noneInv.Request.Arguments.Should().NotContain(a => a == RootA || a.Contains(RootA));
           noneInv.Request.Arguments.Should().NotContain(a => a == RootB || a.Contains(RootB));
           diskInv.Request.Arguments.Should().NotContain(a => a == RootA || a.Contains(RootA));
           diskInv.Request.Arguments.Should().NotContain(a => a == RootB || a.Contains(RootB));

           noneInv.Request.InheritsEnvironment.Should().BeFalse();
           diskInv.Request.InheritsEnvironment.Should().BeFalse();
       }

       [Fact]
       public void APlanWithFlags_IsRefusedForKimi()
       {
           var nonePlan = Plan("kimi", CapabilityGrant.None);
           var yoloLaunch = Planned("kimi", CapabilityGrant.None) with { Confinement = nonePlan with { Flags = new[] { "--yolo" } } };
           var searchLaunch = Planned("kimi", CapabilityGrant.None) with { Confinement = nonePlan with { Leading = new[] { "--search" } } };

           Consultant().Build(yoloLaunch).Should().Throw<ArgumentException>("shim").WithMessage("*shim*");
           Consultant().Build(searchLaunch).Should().Throw<ArgumentException>("shim").WithMessage("*shim*");
       }
   }
   ```

   Wait, I need to check the exact wording for FluentAssertions `Throw<ArgumentException>()`. In v3, it's usually `.Should().Throw<ArgumentException>().WithMessage("*shim*")`. The prompt says "throws `ArgumentException` whose message contains 'shim'". I'll use `.Should().Throw<ArgumentException>().WithMessage("*shim*")`.

   Also, `CapabilityGrant.Disk(RootA, RootB)` might take strings or paths. I'll assume it matches the prompt.
   The prompt says: `Plan(runtime, grant)` = `ConfinementPlanner.Plan(runtime, grant).Should().BeOfType<Confinement.Planned>().Which`. I'll implement it exactly as hinted.
   I need to make sure `Consultant()` uses `HostKind.Linux` or just `HostKind.Linux` since cross-platform. The prompt says "The test host may be Linux; do not use Windows-only APIs." I'll use `HostKind.Linux`.
   Also, `KimiSeams` constructor signature: `new KimiSeams(root => KimiPlaces.ForShim(root, Path.Combine(RowFolder, "persons-bin")), () => ("coai-mcp", []))` matches prompt.
   `ConsultantLaunch` constructor: `new(Repo, "the question", string.Empty, Answers, new ReviewerSettings(runtime) { Model = model, OwnedScratch = RowFolder })` matches prompt. I'll use `"repo"` and `"answers"` as placeholders or keep `Repo` and `Answers` as static strings if needed. The prompt says `Repo` and `Answers` are strings. I'll define them as constants or just inline them. I'll inline `"repo"` and `"answers"`.

   Let's refine the test class to match the prompt's style exactly:
   ```csharp
   namespace CoaiMcp.Tests;

   using System;
   using System.IO;
   using System.Linq;
   using CoaiMcp.Core.QuestionConsult;
   using CoaiMcp.Runners.Acp;
   using CoaiMcp.Runners.Consultation;
   using CoaiMcp.Runners.Kimi;
   using CoaiMcp.Runners.Platform;
   using CoaiMcp.Runners.Reviewers;
   using FluentAssertions;
   using Xunit;

   public class KimiQuestionLaunchTests
   {
       private const string RootA = "D:/projects/alpha";
       private const string RootB = "D:/projects/beta";
       private static readonly string RowFolder = Path.Combine(Path.GetTempPath(), "coai-question-" + Guid.NewGuid().ToString("N"));
       private static readonly string RowTree = Path.Combine(RowFolder, "tree");
       private static readonly string RowCwd = Path.Combine(RowFolder, "cwd");

       private static ConsultantLaunch Planned(string runtime, CapabilityGrant grant, string model = "") => new(
           "repo", "the question", string.Empty, "answers", new ReviewerSettings(runtime) { Model = model, OwnedScratch = RowFolder })
       {
           Confinement = Plan(runtime, grant),
           Workdir = RowTree,
           Scratch = RowCwd,
       };

       private static Confinement.Planned Plan(string runtime, CapabilityGrant grant) =>
           ConfinementPlanner.Plan(runtime, grant).Should().BeOfType<Confinement.Planned>().Which;

       private static KimiConsultant Consultant() => new(new KimiRuntime("kimi", HostKind.Linux,
           new KimiSeams(root => KimiPlaces.ForShim(root, Path.Combine(RowFolder, "persons-bin")), () => ("coai-mcp", Array.Empty<string>()))));

       [Fact]
       public void AKimiQuestionLaunch_IsTheShimRequest_InItsFolderOrItsCopy_OnTheRowsHome()
       {
           var noneLaunch = Planned("kimi", CapabilityGrant.None);
           var diskLaunch = Planned("kimi", CapabilityGrant.Disk(RootA, RootB));

           var noneInv = Consultant().Build(noneLaunch);
           var diskInv = Consultant().Build(diskLaunch);

           noneInv.Request.WorkingDirectory.Should().Be(RowCwd);
           diskInv.Request.WorkingDirectory.Should().Be(RowTree);

           var GetHomeParent = (string arg) => Path.GetDirectoryName(arg.Substring("--home=".Length));
           GetHomeParent(noneInv.Request.Arguments.First(a => a.StartsWith("--home="))).Should().Be(RowFolder);
           GetHomeParent(diskInv.Request.Arguments.First(a => a.StartsWith("--home="))).Should().Be(RowFolder);

           noneInv.Request.Arguments.Should().NotContain("--session");
           diskInv.Request.Arguments.Should().NotContain("--session");

           noneInv.Request.Arguments.Should().NotContain(a => a == RootA || a.Contains(RootA));
           noneInv.Request.Arguments.Should().NotContain(a => a == RootB || a.Contains(RootB));
           diskInv.Request.Arguments.Should().NotContain(a => a == RootA || a.Contains(RootA));
           diskInv.Request.Arguments.Should().NotContain(a => a == RootB || a.Contains(RootB));

           noneInv.Request.InheritsEnvironment.Should().BeFalse();
           diskInv.Request.InheritsEnvironment.Should().BeFalse();
       }

       [Fact]
       public void APlanWithFlags_IsRefusedForKimi()
       {
           var nonePlan = Plan("kimi", CapabilityGrant.None);
           var yoloLaunch = Planned("kimi", CapabilityGrant.None) with { Confinement = nonePlan with { Flags = new[] { "--yolo" } } };
           var searchLaunch = Planned("kimi", CapabilityGrant.None) with { Confinement = nonePlan with { Leading = new[] { "--search" } } };

           Consultant().Build(yoloLaunch).Should().Throw<ArgumentException>().WithMessage("*shim*");
           Consultant().Build(searchLaunch).Should().Throw<ArgumentException>().WithMessage("*shim*");
       }
   }
   ```

   Check constraints:
   - xUnit v3: `[Fact]` is correct.
   - FluentAssertions: `.Should().Throw<ArgumentException>().WithMessage("*shim*")` is correct.
   - `CapabilityGrant.Disk` might take `params string[]` or similar. I'll assume it works.
   - `Confinement.Planned` has `Flags` and `Leading` as `IReadOnlyList<string>`. Using `new[] { "--yolo" }` implements `IReadOnlyList<string>`. That's fine.
   - The prompt says: `Plan(runtime, grant)` = `ConfinementPlanner.Plan(runtime, grant).Should().BeOfType<Confinement.Planned>().Which`. I'll keep it as a method.
   - `ConsultantLaunch` constructor: `new(Repo, "the question", string.Empty, Answers, new ReviewerSettings(runtime) { Model = model, OwnedScratch = RowFolder })`. I'll use literal strings for `Repo` and `Answers` as placeholders, or define them as constants. The prompt says "`Repo` and `Answers` are strings". I'll just use `"repo"` and `"answers"`.
   - `KimiConsultant` production code: I need to output the updated file or just the changes. The prompt says "Give me the new method and the exact new line." I'll provide the full `KimiConsultant.cs` for completeness, or just the method and the modified `Build`. I'll provide the full file to be safe, but I'll mark it clearly.
   - Cyclomatic complexity <= 4 for `MustCarryNoFlags`: It's 1.
   - Switch/pattern style: I'll use pattern matching as requested.
   - `MustCarryNoFlags` implementation:
     ```csharp
     private static void MustCarryNoFlags(ConsultantLaunch launch)
     {
         if (launch.Confinement is LaunchConfinement.Planned { Flags.Count: > 0 or > 0, Leading.Count: > 0 or > 0 } _)
             throw new ArgumentException("a Kimi row's confinement is the shim's; a plan with flags is not one coai made for kimi", nameof(launch));
     }
     ```
     This uses pattern matching. Complexity is 1. No nested ifs. Good.
   - Call site: `MustCarryNoFlags(launch);` as second line in `Build`.
   - I will output `KimiConsultant.cs` with the changes, and `KimiQuestionLaunchTests.cs`.

   Let's verify the exact wording for the exception message: `"a Kimi row's confinement is the shim's; a plan with flags is not one coai made for kimi"`
   Param name: `nameof(launch)`
   All matches.

   One detail: `CapabilityGrant.Disk(RootA, RootB)` might expect `IEnumerable<string>` or similar. I'll pass `RootA, RootB` directly.
   Also, `Confinement.Planned` properties `Flags` and `Leading` are likely `IReadOnlyList<string>`. Using `new[] { ... }` works.
   The prompt says: `Plan(runtime, grant)` = `ConfinementPlanner.Plan(runtime, grant).Should().BeOfType<Confinement.Planned>().Which`. I'll implement it exactly.
   I'll make sure namespaces match: `CoaiMcp.Tests` file-scoped.
   I'll add necessary usings.
   I'll ensure xUnit v3 compatibility (it's mostly the same, but I'll stick to `[Fact]`).
   I'll double-check the `Build` method signature and structure.
   The prompt says: "Output ONLY code blocks, each preceded by one line naming where it goes. No explanations."

   I will produce:
   ```csharp
   // src_mcp/runners/Kimi/KimiConsultant.cs
   ... code ...
   ```
   ```csharp
   // tests/CoaiMcp.Tests/KimiQuestionLaunchTests.cs
   ... code ...
   ```

   Let's draft carefully.

   Production code adjustment:
   ```csharp
   namespace CoaiMcp.Runners.Kimi;

   using System;
   using CoaiMcp.Runners.Consultation;
   using CoaiMcp.Runners.Reviewers;

   public class KimiConsultant
   {
       private readonly ConsultantBuilder inner;

       public KimiConsultant(ConsultantBuilder inner) => this.inner = inner;

       public ReviewerInvocation Build(ConsultantLaunch launch)
       {
           ConsultantLaunches.MustBeLaunchable(launch);
           MustCarryNoFlags(launch);
           var settings = launch.Settings with { OwnedScratch = OwnedFolder(launch), Model = ModelOf(launch.Settings) };
           var built = inner.Build(ConsultantLaunches.RoleOf(launch), launch.Prompt, Cwd(launch), string.Empty, launch.OutputDir, settings);
           var request = built.Request with { Arguments = [.. built.Request.Arguments, .. SessionOf(launch)] };
           ConsultantLaunches.MustCarryNoLineBreak(request.Arguments);

           return built with { Request = launch.Confinement is LaunchConfinement.Planned ? ConsultantLaunches.ForQuestion(request) : request };
       }

       private static string Cwd(ConsultantLaunch launch) =>
           launch.Confinement is LaunchConfinement.Planned planned ? ConsultantLaunches.Cwd(launch, planned.Plan) : SnapshotOf(launch);

       private static void MustCarryNoFlags(ConsultantLaunch launch)
       {
           if (launch.Confinement is LaunchConfinement.Planned { Flags.Count: > 0 or > 0, Leading.Count: > 0 or > 0 } _)
               throw new ArgumentException("a Kimi row's confinement is the shim's; a plan with flags is not one coai made for kimi", nameof(launch));
       }
   }
   ```
   Wait, `OwnedFolder`, `ModelOf`, `SessionOf`, `SnapshotOf` are presumably existing members. I'll keep them as is. The prompt only shows `Build` and `Cwd`. I'll just output the full class as requested, assuming other members exist or are stubbed. I'll stick to the exact code provided plus the additions.

   Test code adjustment:
   I need to make sure `ConfinementPlanner` is accessible. I'll assume it's in scope or fully qualified. The prompt says `Plan(runtime, grant)` = `ConfinementPlanner.Plan(runtime, grant).Should().BeOfType<Confinement.Planned>().Which`. I'll use that.
   I'll also ensure `CapabilityGrant` and `HostKind` are used correctly.
   I'll format it cleanly.

   One thing: `CapabilityGrant.Disk(RootA, RootB)` might be `CapabilityGrant.Disk([RootA, RootB])` or similar. I'll stick to `CapabilityGrant.Disk(RootA, RootB)` as per prompt.
   The prompt says: `Plan(runtime, grant)` = `ConfinementPlanner.Plan(runtime, grant).Should().BeOfType<Confinement.Planned>().Which`. I'll implement it as a local function or method.
   I'll write out the full test class.

   Check xUnit v3: `[Fact]` is still valid. `FluentAssertions` syntax is standard.
   I'll ensure all usings are present.
   I'll verify `InheritsEnvironment` is a bool property on `Request`.
   I'll verify `Arguments` is `IReadOnlyList<string>` or `string[]`. The prompt says `Arguments` list.
   I'll use `.Should().NotContain("--session")` which works for collections.
   I'll use `.Should().NotContain(a => a == RootA || a.Contains(RootA))` which works.
   All good.

   Final check of the prompt: "Do it with a new `private static void MustCarryNoFlags(ConsultantLaunch launch)` in the style of the existing private helpers (switch/pattern, cyclomatic complexity <= 4, no nested ifs), called as the second line of `Build`, right after `ConsultantLaunches.MustBeLaunchable(launch);`. Give me the new method and the exact new line."
   I will provide the full updated `KimiConsultant.cs` and the full `KimiQuestionLaunchTests.cs`.

   One minor point: `Confinement.Planned` might be a nested class or record. The prompt says `LaunchConfinement.Planned` is a record. So `launch.Confinement is LaunchConfinement.Planned { ... }` is correct.
   I'll make sure the code compiles conceptually.
   I'll output exactly as requested.
   Ready. 
   Output generation. 
   Self-Correction/Refinement during thought:
   - The prompt says `Plan(runtime, grant)` = `ConfinementPlanner.Plan(runtime, grant).Should().BeOfType<Confinement.Planned>().Which`. I'll implement it exactly.
   - `CapabilityGrant.Disk(RootA, RootB)` might need `params` or array. I'll assume it matches the prompt's signature.
   - `ConsultantLaunch` constructor: `new(Repo, "the question", string.Empty, Answers, new ReviewerSettings(runtime) { Model = model, OwnedScratch = RowFolder })`. I'll use `"repo"` and `"answers"` as literals for `Repo` and `Answers`.
   - `KimiSeams` constructor: `new KimiSeams(root => KimiPlaces.ForShim(root, Path.Combine(RowFolder, "persons-bin")), () => ("coai-mcp", []))`. I'll use `Array.Empty<string>()` for safety in .NET 10, though `[]` is fine.
   - I'll ensure namespaces match exactly.
   - I'll wrap everything in code blocks with filenames.

   All set. Output matches response. 
   Proceeds. 
   [Output Generation] -> *Self-Correction/Verification during drafting*:
   Wait, `Confinement.Planned` has `Flags` and `Leading` as `IReadOnlyList<string>`. Using `new[] { "--yolo" }` is fine.
   The prompt says: `Plan(runtime, grant)` = `ConfinementPlanner.Plan(runtime, grant).Should().BeOfType<Confinement.Planned>().Which`. I'll make it a static method in the test class.
   I'll add `using static System.Array;` or just use `new[]`.
   I'll keep it simple.
   The prompt says "Output ONLY code blocks, each preceded by one line naming where it goes. No explanations."
   I will strictly follow this.
   Code block 1: `src_mcp/runners/Kimi/KimiConsultant.cs`
   Code block 2: `tests/CoaiMcp.Tests/KimiQuestionLaunchTests.cs`
   Done. 
   Generating. 
   [Output] -> *Proceeds*
