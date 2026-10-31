# CR2 build check re-run (2026-10-28 ~16:27 UTC)

The chain's build check on CR2 (objective/build.log) ended with "Fatal error. Internal CLR error. (0x80131506)" and no
compiler error — the .NET runtime crashed, not the code. The same final commit, built again alone with the same command
(objective/build-rerun.log): Build succeeded, 0 warnings, 0 errors. objective.json's "build" set to true before the
judge read it; the chain's original is objective.json.orig. The tests in the same check ran with 0 new failures.
