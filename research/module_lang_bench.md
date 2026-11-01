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

Built: the pre-push check, the credit terms, the needle list, `generateRunBaseTree` with `needleScan`, and `workspace-diff`. Not built here: the task
repository's README, adapters and research stubs (`chore/skeleton`, the team lead's text) and the pushes themselves (the team lead's, with the owner's go).

**The command.** `node spikes/lang-bench/push-check.mjs` never pushes and never writes to GitHub; its modes are
`--repo <task repo> --branch <b>` (the commits `git rev-list <b> --not --remotes=origin` would add), `--text <file>` (a PR or review body; with
`--self`, a body of a bench PR: the leak and secret scans only), `--self --branch <b>` (the bench's own branch: the leak and secret scans only) and
`--setup-remote --repo <task repo>` (origin set idempotently, `fetch --prune --no-tags` over HTTPS with gh's credential helper for that one command,
`origin/main` checked to hold `LICENSE`). Exit 0 clean, 1 refused, 3 environment (git, the network, an aid or a named secret file that cannot be
read, public plans unreadable at `origin/main`: the check never passes without them), 4 configuration (no such branch, an origin that is not the task
repository, a remote-tracking ref that is not what `git ls-remote` says now). A report line is `<commit> <path>:<line> <reason>`; it never carries the
text that matched (a secret in a file NAME is masked to `[secret]`). The report also prints `leak tokens N (source)`, the `tip` sha it checked and
`push git push <url> <tip>:refs/heads/<branch>`: **publish that sha, not the branch name**, so what was checked is what is published.

**The named checks** (one line each): `leakcheck` (six-word shingles of the two private aids, read from `~/rsd/lang-v2/test-design/` at run time,
and the literal tokens of `leak-tokens.txt` in that folder when it is present, or of `--tokens`), `secrets` (named shapes: fine-grained token,
`gh[pousr]_`, Anthropic key, any `BEGIN … PRIVATE KEY`, AWS key id; and the values of the two real secret files `~/.config/lang-v2/github-pat` and
`deploy_key_lang_v2`, or of `--secret-file`), `attribution` (the README closes with one `## Attribution` section; no link, known-task needle or plan id
elsewhere in it), `run-base-text` (links and the D18 needles in every file's text and path, `.gitmodules` excepted and pin files excepted from links;
plan, probe, gate and decision ids in `csharp/ node/ tasks/ runner/ todo/tasks/` and the README), `family-names` (`dew_flow_*` other than the
repository's own and `PLAN_lang_comparison`, in every file's text and path at the tip, in commit messages, `.gitmodules` excepted),
`known-task-credits` (a known-task text — the spec, or a task folder under `tasks/ csharp/ node/`, added anywhere in the range — needs its entry
in `inputs/credits-lang.json` and, in the README's attribution section outside HTML comments, its credit as a substring, the CC BY-NC-SA 4.0
sentence naming both files, and RealWorld's MIT notice), `stack-folders` (a `.gitkeep` is the empty blob only, and only at the root of `csharp/` and
`node/` until E1.S3 writes `templates/stack-files.json`, a JSON list of path patterns with `<t>` for the task folder) and `tree-shape` (no symlink,
no submodule but `.agents/conventions`, no `.gitmodules` path or URL but the conventions repository's, no file with a NUL byte in a task repository;
UTF-16 is decoded and other binaries are read as latin1 for every scan).

**What reads what.** History is published, so the leak and secret scans read every commit and every message. A run base carries the tip tree and
never the history, so every other rule reads the files as they stand at the tip (credited to the commit that brought the blob). Needles match by
D18's rule (change 4.1): case-sensitive, on word boundaries with ASCII letters and digits as the word characters, plus exact substrings
(`inputs/needles-lang.json`, the public list); a multi-word needle is matched over the whole text with any whitespace between its words. Shingles
tokenise like v1's `normalise` and drop nothing. A six-word run that the parent plan or the stories document already holds is public and is not an
aid hit; those two texts, and every other `.md` file at that ref, are read with `git show origin/main:<path>` of the bench repository, never from
the working tree (`--public <file>` names the public texts instead, in tests); an aid left with no run of its own is exit 3, because it would check nothing.

**The credit terms** (`inputs/credits-lang.json`, the owner's of 2026-11-01): CSES problem pages and licence page read on that date, RealWorld's
notice copied verbatim from its `LICENSE` as it stood then (two copyright lines), the protocol page, the olympiad's statistics page for task 09. The
`credit` lines are checked as substrings of the README's squashed attribution section; the README sentence of task 09 ("… is based on IOI 2018
Werewolf (…)") carries the recorded credit.

**The run base's tree.** `lib/run-base-tree.mjs`: `generateRunBaseTree({ repo, ref, out, needles })` unpacks `git archive <ref>` into an empty
folder outside every worktree, cuts the README from `## Attribution` to the end (a README without the section, with it twice, or with a section after
it is refused), removes credit files (`CREDITS*`, `credits*`, `ATTRIBUTION*`) and runs `needleScan` over every entry; any hit (a symlink, a binary and
a file name count) refuses the cut, removes the folder and names `path:line needle`. E1.S7 commits that tree as the orphan run base.
`workspace-diff.mjs --root <checkout>`: per task, `csharp/<t>` and `node/<t>` hold the same files byte for byte except each stack's own pin files
(C#: `global.json`, `nuget.config`, `Directory.Build.props`, `*.csproj`; Node: `package.json`, `package-lock.json`, `tsconfig.json`, `.npmrc`,
`eslint.config.mjs`) and the one line after `<!-- stack-sentence -->` and `<!-- framework-sentence -->` in `STATEMENT.md`.

**Tests.** `node spikes/lang-bench/run-tests.mjs 'test/push-*.test.mjs' test/run-base-tree.test.mjs test/workspace-diff.test.mjs`. Every test builds
throw-away repositories with a bare `origin` in owned folders (`test/support/push-fixtures.mjs`) and a synthetic aid; no test reads the real aids,
the real secret files or the network.
