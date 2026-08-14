# Testing

## Running tests — executables only, never `dotnet test` (MANDATORY)

The test project is **xUnit v3 on native Microsoft Testing Platform**. There is no
`Microsoft.NET.Test.Sdk` and no VSTest `testhost`, so `dotnet test` aborts with a `testhost.deps.json`
error — a tooling mismatch, not a test failure. **Never** run it, and never report its output as a test
result. An MTP project builds into a self-contained runner executable; run that.

```bash
dotnet build dew_flow_benchmark.slnx -c Release        # the exe is only as fresh as the last build
./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests
```

The CI workflow runs exactly these two commands. If a change makes them disagree with what CI does, the
workflow is the thing to fix.

Runner flags are xUnit v3 MTP syntax — **not** VSTest (`--filter "FullyQualifiedName~X"` does not exist):

| Need | Flag |
|------|------|
| One test method | `--filter-method "*MyTestName*"` |
| One class | `--filter-class "*MyTests"` |
| One namespace | `--filter-namespace "Bench.Tests.Runs"` |
| Exclude | `--filter-not-method` / `--filter-not-class` / `--filter-not-trait` |
| Enumerate without running | `--list-tests` |
| Global timeout | `--timeout 5m` |

## Every feature ships with tests, in the same task

Happy path plus the failure and edge paths the feature introduces. A feature without tests is not done
and is not committed. Say in the summary what was added and what the runner printed.

## Every bug fix starts with a RED test (MANDATORY)

The order is fixed — never fix first and test after:

1. Write the test that reproduces the defect, named after the guarantee a user expects.
2. **Run it and watch it FAIL** — and confirm the failure message describes the real symptom, not a
   setup error. A test that errors for the wrong reason proves nothing.
3. Fix the code.
4. Run it again and watch it pass.
5. Run the whole suite.
6. **Report both observations** — the step-2 failure message and the step-4 pass. "Tests pass" alone is
   not evidence.

**If the fix already landed**, prove the test has teeth: revert the fix, watch the test go red with the
real symptom, restore it, confirm green.

## Name the guarantee, and give a refuted approach a shape

A test name states what must be true — `First_position_is_balanced_across_the_whole_matrix_at_an_odd_repeat_count`,
not `Bug123Test`.

Where a guard exists because a specific plausible approach was **measured and refuted**, reproduce that
approach inside the test so the defect is visible in the suite rather than only in a commit message.
`MatrixOrderTests.The_naive_per_repeat_rotation_would_deal_two_to_one_and_that_is_the_point` is the
reference: it computes what the refuted scheme produces (6 against 3 over nine slots) beside the
guarantee the real one gives. A future reader who thinks the global counter is over-engineering meets
the reason immediately.

## Architecture is a test, not a review comment

`ArchitectureTests` asserts the layer rules by reading assembly references. A violation is a red build.
Do not relax it to make a reference convenient — the reference is the problem.

## Scope

- No test is needed where there is no observable behaviour (a comment, formatting, a pure rename) — say
  so explicitly in the summary when skipping.
- If a defect seems untestable, prefer making it testable (extract the decision into a pure function, as
  `Retarget` does with its probe) over skipping.

## Definition of Done

- [ ] The runner executable was used; `dotnet test` was not.
- [ ] New behaviour has tests; a fix has a test that was observed failing for the real symptom.
- [ ] Both observations are in the summary.
- [ ] Test names state guarantees.
