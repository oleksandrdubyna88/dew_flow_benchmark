# Generations of the quick pilot

## Generation 1 — D:/rsd/_q1 — VOID (instrument failure), 2026-10-14

All three cells (c1=A, c2=A, c3=B; seed in seed.txt) ran to completion and all passed the spec-implied hold-out tests,
but NO cell could build or test: every non-trivial Bash command was refused ("This command requires approval") and
every PowerShell call failed ("Command contains malformed syntax that cannot be parsed: pwsh exited with code 1: The
command line is too long"). Cause, bisected with $0.05 probes: the child environment dropped `CLAUDE_CODE_ENTRYPOINT`;
without it the CLI's own command checks fail on this machine. Fix: the child env sets `CLAUDE_CODE_ENTRYPOINT=claude-vscode`
(fixed, identical for both arms — the entrypoint the operator's own sessions run under). Spend of generation 1:
$5.75 + $6.63 + $9.61 = $21.99 (cells) — kept as a record, not as a comparison.

## Generation 2 — D:/rsd/_q2 — the same committed seed, so the same order

## Generation 3 — D:/rsd/_q3
B gets a test-only main-session edit bound (owner, 2026-10-14; conventions unchanged); A unchanged; same seed.
