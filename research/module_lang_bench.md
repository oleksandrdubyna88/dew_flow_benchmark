# Language comparison v2 — the bench module (`spikes/lang-bench`)

> One `##` section per E1 story; each story writes only under its own heading (`todo/PLAN_lang_comparison_v2_E1.md` § 3, "Conventions for every story").

## The runner (E1.S8a)

The test-runner scaffold: `node spikes/lang-bench/run-tests.mjs [glob…]` runs this spike's `test/**/*.test.mjs` through `node --test` at lowered priority with at most `min(8, CPUs − 1)` workers, and a Node test file starts the Python units of `test/py/` under `python3 -I`; the runner itself (`runner/`) is not built yet.

## The execution layer (E1.S8b)

not built yet

## FORMAT v2 (E1.S9)

not built yet

## The conformance check (E1.S8)

not built yet

## Toolchains and the preflight (E1.S5)

not built yet

## Package sets and snapshots (E1.S4)

not built yet

## Workspaces (E1.S3)

not built yet

## Visible samples (E1.S6)

not built yet

## Specs, gates and the freeze (E1.S2)

not built yet

## Draft references and the pre-freeze tool (E1.S2a)

not built yet

## Process-arm templates and run bases (E1.S7)

not built yet

## Skeleton and the push path (E1.S1)

Built so far: the pre-push check, the credit terms and the needle file. Not built yet: `lib/run-base-tree.mjs`, `lib/workspace-diff.mjs`
and their tests, the task repository's README and adapters (`chore/skeleton`), and the pushes themselves.

**The command.** `node spikes/lang-bench/push-check.mjs` never pushes and never writes to GitHub; its modes are
`--repo <task repo> --branch <b>` (the commits `git rev-list <b> --not --remotes=origin` would add), `--text <file>` (a PR or review body),
`--self --branch <b>` (the bench's own branch: the leak and secret scans only) and `--setup-remote --repo <task repo>` (origin set idempotently,
a fetch over HTTPS with gh's credential helper for that one command, `origin/main` checked to hold `LICENSE`). Exit 0 clean, 1 refused,
3 environment (git, the network, an aid that cannot be read: the check never passes without its aids), 4 configuration. A report line is
`<commit> <path>:<line> <reason>`; it never carries the text that matched.

**The named checks** (one line each): `leakcheck` (the six-word shingles of the two private aids, read from `~/rsd/lang-v2/test-design/` at run
time, and optional literal tokens from `--tokens`), `secrets` (seven named shapes, `lib/push-secrets.mjs`), `attribution` (the README closes with
one `## Attribution` section; no link, known-task needle or plan id elsewhere in it), `run-base-text` (plan, probe, gate and decision ids,
links and the D18 needles in `csharp/ node/ tasks/ runner/ todo/tasks/`), `family-names` (`dew_flow_*` other than the repository's own and
`PLAN_lang_comparison`, in every file but `.gitmodules`), `known-task-credits` (a known-task spec needs its entry in `inputs/credits-lang.json`
and, in the README's attribution section, its credit line, the CC BY-NC-SA 4.0 exception sentence naming both files, and RealWorld's MIT notice)
and `stack-folders` (only `.gitkeep` under `csharp/` and `node/` until E1.S3 writes `templates/stack-files.json`, a JSON list of path patterns
with `<t>` for the task folder).

**What reads what.** History is published, so the leak and secret scans read every commit and every message. A run base carries the tip tree and
never the history, so every other rule reads the files as they stand at the branch tip (credited to the commit that brought the blob). Needles
match by D18's rule (change 4.1): case-sensitive, on word boundaries with ASCII letters and digits as the word characters, plus exact substrings
(`inputs/needles-lang.json`, the public list). A six-word run that the parent plan or the stories document already holds is public and is not an
aid hit (`--public <file>` names other public texts); an aid left with no run of its own is exit 3, because it would check nothing. A whitespace-delimited token with `/`, `\` or `_` (a path, a snake_case name) is left out of the word list on both sides, so a pointer that shares only a path with an aid is not a hit while a verbatim copy of prose still is; the fine-grained token shape requires a digit and a capital, so a snake_case name starting `github_pat_` is not a token.

**The credit terms** (`inputs/credits-lang.json`, the owner's of 2026-11-01): CSES problem pages and licence page read on that date, RealWorld's
notice copied verbatim from its `LICENSE` as it stood then (two copyright lines), the protocol page, the olympiad's statistics page for task 09.
The `credit` lines are the first draft for the team lead to confirm; the README must carry them verbatim.

**Tests.** `node spikes/lang-bench/run-tests.mjs test/push-*.test.mjs`. Every test builds throw-away repositories with a bare `origin` in owned
folders (`test/support/push-fixtures.mjs`) and a synthetic aid; no test reads the real aids or the network.
