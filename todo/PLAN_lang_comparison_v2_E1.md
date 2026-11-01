# PLAN — language comparison v2, E1 stories: the task repository — the runner, the execution layer, FORMAT v2, samples, specs gated and frozen after a pre-freeze execution gate, workspaces, toolchains, caches, process templates

> Status: **plan only, nothing implemented yet, 2026-10-31; rewritten for the parent's epoch 4.** Epic E1 of
> [PLAN_lang_comparison_v2.md](PLAN_lang_comparison_v2.md) (§14 step 1), written when the epic started — a stories document is
> not a plan change (`common.plan-change-control`) and keeps no ledger: its changes are entries in the parent. Its first
> version (`51a80ba`) found the two changes that stopped epoch 3 (3.1 and 3.2); the epoch-4 re-review judged it `fits` and
> asked for this rewrite — E1.S8b (the execution layer, D43), E1.S7 aligned with the GitHub broker (D42, built in E3.S7a),
> OP-2 to OP-11 as text, and no trap content (D41). The parent is read at `0ea0e25` (`docs/plan-lang-v2-epoch4`, PR #129,
> open when this was written). Twelve stories (E1.S1 … E1.S9, with E1.S2a, E1.S8a and E1.S8b); cornerstone **E1.S2**
> (`model: fable`), the one mark the parent's ledger gives this epic (`Cornerstones: 3 of 3 — E1.S2, E2.S2, E3.S2`); no further
> mark is possible. Scope: the public task repository `dew_flow_ai_language_comparison` (created by the owner: MIT, one
> LICENSE commit), the spike `spikes/lang-bench/` in this repository, and the private area `~/rsd/lang-v2/` outside every git
> tree. Open points: §9 — none is a change; OP-A (the conventions submodule under D42) was decided by the team lead as option
(c), built in E1.S7. Revised 2026-10-31 for the six findings of E1's plan round (PROCEED). **Revised 2026-11-01 from two
inputs, with no parent decision changed:** (1) the risk and cadence consult of 2026-11-01 (Fable, standing in for the coai
consultant with the owner's authorisation; its report is kept by the team lead outside git) — part A (review checkpoints
CP1–CP4 in §5, one record file per writer, the D40 gate lock, one `##` section per story in the module doc, E1.S8a and E1.S7
each built as two coder briefs inside the one story, so the epic keeps twelve stories), B1–B10, C1–C7, D1–D3 and D5–D7
written into the stories they name, each cited `(consult <id>)`; D4 adjusted by the team lead (§9 OP-I); B8 left to D15's
owner (E1.S8b); (2) the facts and owner decisions of 2026-11-01 (§4): the deploy key and the fine-grained PAT exist, the
repository's merge settings, and the credit terms of the known tasks (the rest of the parent's Q1). The parent is read at
main `d47a346` (epoch 4, holding change 4.1). What this revision could say only by changing the parent is listed, not
applied, in §12.

## 1. The epic, restated (parent § *Epics*, E1)

**Goal.** Ten specs written by the team lead, gated and frozen; ten workspaces per stack an agent can start in, identical
across stacks but for the stack, on pinned toolchains and offline caches; the one execution layer every program of the series
runs on; the process arm's run-base machinery; in a public repository that leaks nothing (parent D2, D5, D6, D11, D12, D19,
D21, D25, D31, D35–D43; §4.1, §4.5, §4.6, §5, §6.1).

**Done when (parent).** T-L1, T-L2, T-L13, T-L20's guard half green; `workspace-diff` clean on ten tasks; both snapshots
hashed; the preflight passes inside a box on SDK 10.0.403, Node 26.13.0, **TypeScript 6.0.x** (the patch pinned in E1.S5) and
the pinned Python; the execution layer's probes T-L30 … T-L34 green on the series host (E1.S8b); `FORMAT.md` v2 gated and its
three examples run; the ten specs conform to it, carry a gate verdict and the freeze tag; the README carries the credits and
the Node stack table; `leakcheck` clean.

**Not in this epic** (do not build here): the held-out suites, mutants, trap coverage table and limit calibration (E2); the
drivers' scenario logic and records, the judge's case loop, scoring per kind, D15's environment in the **agent** box, the
arm/task/round runner, the coai box and bridge, **the GitHub broker (E3.S7a, D42)**, the process runner, the compliance
checker (E3); the pre-registration and the session cap of **$50** (E4; the owner's fixed per-session cap, D7). **Nothing in
this epic launches `claude` for a data or pilot session**; the coai document gate and consultant calls of S2 and S9 are
review calls, not sessions under test.

**The held-out boundary (D41).** Both repositories are public. **No trap, trap-catching scenario, riskiest-contract scenario,
held-out case, mutant or reference appears in this document, in a commit message, a PR body, a test name, a fixture or a
code comment.** The test-design aid lives at `~/rsd/lang-v2/test-design/PLAN-trap-table.md` and
`~/rsd/lang-v2/test-design/PLAN-risky-scenarios.md` (mode 0600; their sha256 are in the parent's D41); the draft references at
`~/rsd/lang-v2/refs/`. This document points at those paths and names no content of them; its author did not read them.

## 2. What exists — verified 2026-10-31 (bench `53f248e`, parent read at `0ea0e25`)

| Fact | Where |
|---|---|
| **The public repository exists:** `oleksandrdubyna88/dew_flow_ai_language_comparison`, public, MIT, default branch `main` = `ed68ce1` (the LICENSE commit only) | `git ls-remote`, `gh repo view --json visibility,licenseInfo`, 2026-10-31 |
| **The local task repository has no remote**, and its history is unrelated to the remote's: `main` = `28bf777` (skeleton `b5e9598`, pilot spec 01 `60e468a`, FORMAT v1 `d7c73ff`, 01 conformed `28bf777`); `docs/spec-05` = `30af434` (spec 05 + four scenario samples); `docs/spec-04` = `0b69a50` (spec 04; its samples only in the spec text) | `/home/jinx/git/dew_flow_ai_language_comparison`, `git remote -v` (empty), `git log --all` |
| `main`'s tree: `README.md` (4 lines), `.gitkeep` in `csharp/`, `node/`, `tasks/`, `todo/tasks/`, the spec `01-descent-to-one.md`, `FORMAT.md` v1 (884 lines). No `AGENTS.md`, conventions pin, `runner/` or sample files for 01 | `git ls-tree -r main` |
| FORMAT v1: §1–§2 spec file, header, fixed sentences; §3 sample pairs; §4 the `scenario 1` grammar; §5 the `transcript 1` comparison; §6 three toy examples (the daemon toy names a real protocol); §7–§9 maintainer sections that name plan ids; §1.5 names `NOTES.md` | `git show main:todo/tasks/FORMAT.md` |
| Specs 01, 04, 05 hard-code T = 10 s and G = 5 s (the series defaults, D14); their samples are FORMAT v1 | the three branches |
| v1's held-out runner protocol (`/usr/bin/python3 -I runner.py --phase --binary --work --box <json argv> --timeout --out`, result `format: 1`); a synthetic Python suite written from JS strings into a test's folder; the spike test runner shape | `spikes/minigit-bench/lib/heldout-run.mjs:19`, `:116`, `:172`; `test/toy-suite.mjs:1-60`; `run-tests.mjs:1-41` |
| bwrap primitives and v1's scoring box: `isolation()` = `--unshare-net --unshare-pid --die-with-parent --new-session`; `scoreEnv` and `agentEnv` set `LANG: 'C.UTF-8'`; `scoreBoxArgv` | `spikes/minigit-bench/lib/bwrap.mjs:15`, `:32`, `:37`, `:58`; `lib/sandbox.mjs:93`, `:97` |
| Reusable: `fetchInputs`, `parseHashes`, `verifyFiles`, `treeHash`, `makeSnapshot`, `registerSnapshot`, `packagesOf`, `leakcheck` (literal tokens and six-word shingles), `runBounded`, `ok`/`fail`, `refuseInsideWorktree`, `preflight`, `removeUnder` | `spikes/minigit-bench/lib/fetch-inputs.mjs:57`; `inputs.mjs:11`, `:49`; `cache-snapshot.mjs:36`, `:162`, `:233`; `packages.mjs:54`; `heldout-leak.mjs:32`, `:111`; `run.mjs:35`; `result.mjs:2-3`; `git-guard.mjs:28`; `preflight.mjs:56`; `spikes/policy-ab-quick/lib/safe-rm.mjs:52` |
| The owned-name helper of `common.test-leftovers` (`ownedDir`, `ownedFor({ token })`) and the barrier scan | `.agents/conventions/tools/lib/owned.mjs:612`, `:725`; `.agents/conventions/tools/sweeper-scan.mjs` |
| `spikes/lang-bench/` does not exist | `ls spikes/` |
| Host: Python 3.14.4 at `/usr/bin/python3`; bubblewrap 0.11.1; `systemd-run`; cgroup2 with `memory`; **`locale -a` lists `de_DE.utf8`** (generated by the owner, 2026-10-31); the D36 scope probe (`memory.peak` readable) and the D43 namespace probe (`unshare -rn`, `lo` up, a Python server and driver exchanged `ping→PING` over 127.0.0.1 in one netns; `--mount-proc` refused) as the parent records them | `python3 --version`, `bwrap --version`, `/sys/fs/cgroup/cgroup.controllers`, `locale -a`; parent D36, D43 |
| The bench repository is public; the trap aid that stood in its plan moved to the private area (D41), the merged history keeps the earlier rows (T18) | parent D41, T18 |
| **Verified 2026-11-01.** The task repository's local branches beyond the three above: `docs/format-v2`; `docs/specs-olympiad` (`02-open-areas.md`, `03-wrapping-arithmetic.md`, `09-switched-routes.md`); `docs/specs-daemon` (`07-stream-processor.md`, `10-dag-scheduler.md`); `docs/specs-web` (`06-blog-api.md`, `08-ledger-api.md`). The known tasks' spec paths are therefore `01-descent-to-one.md`, `02-open-areas.md`, `05-key-value-server.md`, `06-blog-api.md`, `09-switched-routes.md` (the task-9 path as change 4.1 renamed it) | `git -C /home/jinx/git/dew_flow_ai_language_comparison ls-tree --name-only <branch> todo/tasks/` |
| **Verified 2026-11-01 (the team lead).** The deploy key `lang-v2-run-bases` (write access) is on the task repository; its private half is `~/.config/lang-v2/deploy_key_lang_v2` (mode 600) and it authenticates over `ssh.github.com:443` (*"Hi oleksandrdubyna88/dew_flow_ai_language_comparison! You've successfully authenticated"*); port 22 times out from this host | `ssh -T -p 443 -i <key> git@ssh.github.com` |
| **Verified 2026-11-01 (the team lead).** The fine-grained PAT `~/.config/lang-v2/github-pat` (mode 600): this repository only; Contents and Pull requests read/write; Actions, Checks and Commit statuses read; no Workflows. Its reads were verified, including `GET /repos/{owner}/{repo}/issues/comments` (the repository is public); its writes are verified by E1.S7's P0 lines with the owner's go | `gh api` with the PAT |
| **Verified 2026-11-01.** The task repository allows squash, merge and rebase merges and has 0 rulesets | `gh api repos/{owner}/{repo}`, `…/rulesets` |
| **Host mechanisms, per the consult of 2026-11-01** (probed by it on this host with bwrap 0.11.1, nothing written): bwrap does not forward SIGTERM to the program under it (exit 143, the program's TERM trap never ran; B1); with `--die-with-parent` a program's lingering child ends with the namespace before anyone reads `cgroup.procs`, without it the child stays (B4); `--new-session` puts the program in a process group of its own (B4). **Re-read by this revision's writer:** the user manager's `DefaultOOMPolicy=stop` (B3); CPython 3.14's `Popen._wait` sleeps `min(delay * 2, remaining, .05)` between polls (B6) | the consult; `systemctl --user show -p DefaultOOMPolicy`; `inspect.getsource(subprocess.Popen._wait)` |
| The exploratory pilot of task 1 (on main, not on this branch): **F1** — the per-case wall has about 50 ms resolution (`Popen.wait(timeout=…)` polls) and includes the `systemd-run`/`bwrap` start-up, 65 ms in the empty-program control; **F5** — a pair scored while other work ran was flagged `scored under load` | `research/lang-v2/EXPLORE_task1_2026-11-01_analysis.md` (main `cdd3349`), rows F1 and F5 |

## 3. Conventions for every story

- **Where things live.** Bench: `spikes/lang-bench/` — plain ESM `.mjs` on Node 26, no dependency (v1's spike conventions,
  [PLAN_minigit_stack_bench_E1.md](PLAN_minigit_stack_bench_E1.md) §3), plus the runner in Python 3 standard library only.
  Task repository: only what parent §5 lists — no tool, and **no `todo/PLAN_*`**: it would enter every run base; the
  cross-repository boundary is named in the README's `## Attribution` section (cut from every run base) and in the parent
  (D39). Private area `~/rsd/lang-v2/`: everything that must never be in git.
- **No trap content in public (D41).** Commit messages, PR descriptions, review bodies, test names, fixtures and comments
  describe a check by its mechanism ("a split frame", "a race of two clients"), never by the defect it is meant to catch in a
  stack. A brief that needs the aid gives its **path**; the reader opens it on the host, outside every repository.
- **Shape.** Functions < 50 lines, cyclomatic complexity ≤ 4, files 200–400 typical and 800 max (PROJECT §6,
  `common.coding-style`), in Node and Python alike; expected failures are values, never exceptions for control flow; absent is
  not zero (`{ known: false, why }` renders `unknown`).
- **Tests.** Everything runs through `node spikes/lang-bench/run-tests.mjs [glob…]` (E1.S8a creates it, v1's shape: lowered,
  `max(1, min(8, threads − 1))` workers). A test that needs `bwrap`, `systemd-run`, user namespaces, a toolchain or the task
  repository checkout and cannot find it SKIPS WITH ITS REASON; `BENCH_REQUIRE_SANDBOX=1` (the epic's completion run, parent
  §15) turns every such skip into a failure. Python units run under `python3 -I -m unittest`, started from one Node test file.
- **Leftovers (`common.test-leftovers`).** Every folder a test makes comes from `ownedFor({ token: 'bench' })`; a Python child
  gets an owned folder through `TMPDIR`. A test's transient systemd scope and its namespace holder are named with the owned
  prefix and removed by the test by exact name. Before the first owner-form producer lands,
  `node .agents/conventions/tools/sweeper-scan.mjs` lists no owner-blind sweeper; if it lists one, the coder stops and
  reports (§ *The barrier*).
- **Processes (`common.security`, `common.reliability`).** exe + argv, never a shell string; every wait has a ceiling; a timeout
  kills the whole process group or scope; every buffer and file the harness keeps is bounded.
- **Red, green, red.** A test written first and watched red **for the real symptom**, then green, then the production line the
  behaviour depends on removed and the test watched red again before it is restored (`common.testing`); both observations in
  the story's summary. A guard against a plausible wrong approach is named for it (`…_would_…_and_that_is_the_point`).
- **Roles (`common.subagent-models`).** Scenarios and briefs here are Opus's; code and tests by a Sonnet coder; review by Opus;
  fixes by Sonnet. The task specs, the samples and FORMAT v2 are the team lead's (Opus, main session, D21); E1.S2's story
  brief is Fable's — two kinds of "spec" (OP-10, now D21's text): Fable writes the story's per-task spec briefs, the team lead
  writes the task specs. Every launch names its model.
- **How work lands in the task repository.** Through pull requests with CI once E1.S1's first push has happened
  (`common.task-lifecycle`); until then a story branch waits locally. The bench side lands through pull requests as always.
  **S1's `chore/join-remote` is merged before any other task-repository branch is pushed** (otherwise every later PR's diff is
  the whole unrelated history; consult A, D7). After it, the stories' task-repository paths are disjoint (`docs/spec-NN`,
  `tasks/NN/samples/`, `csharp/`, `node/`, `runner/`). **FORMAT changes are serialised:** one FORMAT v2 change at a time, and
  every open spec branch is brought up to it by a merge (never a rebase of a pushed branch) and re-conformed (consult A).
- **One record file per writer (consult A).** Four stories would otherwise write one JSON file in parallel branches. Each
  writer owns one file under `spikes/lang-bench/inputs/specs.d/` and writes only that file: `runner.json` (E1.S8a: `runner:
  { version, files, manifestSha256 }`), `exec-layer.json` (E1.S8b: `execLayer: { treeSha256, probedOn, host }`), `format.json`
  (E1.S9: `format`, `grammar`), `spec-states.json` (E1.S2: per spec `sha256`, `state`, `gate`, `rIds`, `T`, `G`, and the freeze's
  commit and tag shas). **`inputs/specs.json` stays the one record the parent names** (D37, §5, the E1 epic's S2, S8a, S8b,
  S9): it is generated by `node spikes/lang-bench/specs-record.mjs` from the four files, byte-stable (sorted keys, LF), and
  never edited by hand; a merge conflict in it is resolved by re-running the tool; a test
  (`The_specs_record_is_the_assembly_of_its_writers`, beside its companion that still finds a hand edit) keeps the two equal.
  E1.S8a creates the tool with its first writer file. `inputs/references.json` (E1.S2a) was already separate.
- **One `##` section per story in `research/module_lang_bench.md` (consult A).** The first story to land creates the file with
  twelve headings, one per story (`## The runner (E1.S8a)`, `## The execution layer (E1.S8b)`, `## FORMAT v2 (E1.S9)`, `## The
  conformance check (E1.S8)`, `## Toolchains and the preflight (E1.S5)`, `## Package sets and snapshots (E1.S4)`, `## Workspaces
  (E1.S3)`, `## Visible samples (E1.S6)`, `## Specs, gates and the freeze (E1.S2)`, `## Draft references and the pre-freeze tool
  (E1.S2a)`, `## Process-arm templates and run bases (E1.S7)`, `## Skeleton and the push path (E1.S1)`), each `not built yet`;
  every story writes only under its own heading. A conflict on the heading list is resolved by taking the union.
- **The D40 gate lock (consult A; the pilot's F5).** A gate judges T, G and step deadlines, so it must not run while the host
  is busy with this epic's own heavy work. `~/.cache/lang-v2/gate.lock` is created exclusively (`O_EXCL`) and holds the owner
  record of the owned-name helper (`ownedFor({ token: 'bench' })`: pid, start time, host). `prefreeze.mjs` (E1.S2a) takes it for
  its whole run; the `BENCH_REQUIRE_SANDBOX=1` completion run and `make-snapshots.mjs` (E1.S4) take it too. A taker that finds
  it held **refuses** (exit 3, `gate lock held by <owner>`), never waits; a lock whose owner is gone is removed by the next taker,
  naming it. Load the lock cannot stop (other repositories' work) is **recorded**: `/proc/loadavg` at the gate run's start and
  end goes into the gate record, and a run whose 1-minute load at either point exceeds the online CPU count minus one is flagged
  `under load`; a flagged `pass` is re-run once unflagged before the document gate, and a flagged `fail` is re-run before any
  spec or sample is changed because of it (the pilot's F5 remedy, applied to the gate). The lock's path is an argument with
  that default, so the tools' own tests use a lock in an owned folder and never meet the completion run's.
- **The private store** `~/rsd/lang-v2/`: E1.S5 (`toolchains/`), E1.S4 (`caches/`), E1.S2a (`refs/`), E1.S7 (`conventions/`) write
  disjoint folders and may run in parallel; `~/.cache/lang-v2/<sha>/` is shared by E1.S3's tests and everything later, and the
  owned names cover it (consult A).

## 4. The operator's part and the push

The owner has created the repository (public, MIT, `ed68ce1`), generated `de_DE.UTF-8` (`locale -a` lists `de_DE.utf8`) and
decided the licence of the repository's own content (MIT). **Still open and named where used:** the credit terms of the known
tasks' sources (the rest of Q1 — the push of a known-task text waits for it, below); the fine-grained PAT and the deploy key
(E1.S7); E0's parity release for the final conventions pin (E0.S3; E1.S1 pins the current `release` provisionally).
*(Superseded 2026-11-01 for the credit terms, the PAT and the deploy key — below. E0's parity release is still open.)*

**Facts and owner decisions, 2026-11-01.**

- **The deploy key exists:** title `lang-v2-run-bases`, write access, private half `~/.config/lang-v2/deploy_key_lang_v2`
  (mode 600). It authenticates over `ssh.github.com:443`; port 22 times out from this host. So the consult's precondition D1
  is **met**; E1.S7's run-base push uses SSH on port 443 with a pinned host key (E1.S7, *The deploy key's road*).
- **The fine-grained PAT exists:** `~/.config/lang-v2/github-pat` (mode 600), this repository only; Contents and Pull
  requests read/write; Actions, Checks and Commit statuses read; no Workflows. Reads verified; writes are verified by E1.S7's
  P0 lines with the owner's go. The repository is public, so the PAT reads PR conversation comments without the Issues
  permission (verified: `GET /repos/{owner}/{repo}/issues/comments` succeeds with it); `Issues: write` would be needed only if
  the broker must post conversation comments — an open point for E3.S7a (§9 OP-I), **no change to the PAT now** (consult D4,
  adjusted by the team lead).
- **The repository allows squash, merge and rebase today and has 0 rulesets.** Setting it to merge commits only (consult C7a,
  D7) is a GitHub write: operator step O1 below.
- **The credit terms of the known tasks, decided by the owner on 2026-11-01** (the parent's open question Q1, *Licences and
  credit*; recorded here, and in the parent at its next change, §12):
  - **specs 01 and 02, adapted from CSES:** those two spec files are licensed **CC BY-NC-SA 4.0**, the CSES problem set's
    licence (read 2026-11-01 at `https://cses.fi/problemset/text/2433`), with the credit and a link in the README's attribution
    section; the README states this per-file exception to the repository's MIT. Nothing of the credit or the licence is
    written inside the spec files themselves (D19, D39: a known-task statement names no source and carries no hint that it is
    known);
  - **spec 09, IOI 2018 *Werewolf*:** no licence was found for the 2018 materials, so the statement is written wholly in our
    own words (the problem idea only), credited "based on IOI 2018 Werewolf" in the attribution section, with no copied text;
  - **spec 06, RealWorld "Conduit":** MIT; the credit plus RealWorld's MIT notice in the attribution section;
  - **spec 05, the key-value server:** the RESP protocol subset is described in our own words; the name "Redis" is never the
    task's name; one credit line.
- **These terms are the content of `spikes/lang-bench/inputs/credits-lang.json`**, which E1.S1's push check reads. Its shape
  (E1.S1 fills the `<…>` values from the sources' own pages, and the team lead writes the `credit` lines, which go verbatim
  into the README's attribution section):

```json
{
  "schema": 1,
  "decided": { "by": "owner", "on": "2026-11-01" },
  "repositoryLicence": "MIT",
  "known": [
    { "task": "01", "spec": "todo/tasks/01-descent-to-one.md", "basis": "adapted",
      "source": { "name": "CSES Problem Set", "url": "<problem page>" },
      "fileLicence": { "spdx": "CC-BY-NC-SA-4.0", "readAt": "https://cses.fi/problemset/text/2433", "readOn": "2026-11-01" },
      "upstreamNotice": null, "copiedText": "adapted", "credit": "<one line>" },
    { "task": "02", "spec": "todo/tasks/02-open-areas.md", "basis": "adapted",
      "source": { "name": "CSES Problem Set", "url": "<problem page>" },
      "fileLicence": { "spdx": "CC-BY-NC-SA-4.0", "readAt": "https://cses.fi/problemset/text/2433", "readOn": "2026-11-01" },
      "upstreamNotice": null, "copiedText": "adapted", "credit": "<one line>" },
    { "task": "05", "spec": "todo/tasks/05-key-value-server.md", "basis": "own-words",
      "source": { "name": "the RESP protocol", "url": "<protocol page>" },
      "fileLicence": null, "upstreamNotice": null, "copiedText": "none",
      "nameRule": "the product name is never the task's name", "credit": "<one line>" },
    { "task": "06", "spec": "todo/tasks/06-blog-api.md", "basis": "adapted",
      "source": { "name": "RealWorld \"Conduit\"", "url": "<repository page>" },
      "fileLicence": null, "upstreamNotice": { "spdx": "MIT", "text": "<their MIT notice, verbatim>" },
      "copiedText": "adapted", "credit": "<one line>" },
    { "task": "09", "spec": "todo/tasks/09-switched-routes.md", "basis": "idea-only",
      "source": { "name": "IOI 2018, Werewolf", "url": "<task page>" },
      "fileLicence": null, "upstreamNotice": null, "licenceFound": false, "copiedText": "none",
      "credit": "based on IOI 2018 Werewolf" }
  ]
}
```

  `fileLicence: null` means the file is under the repository's MIT. `copiedText` is `adapted` (an adaptation the licence
  allows), or `none` (our own words; push-check cannot prove it, so the team lead's review of the spec against the source is the
  control and E1.S2's gate record says it was done). The file names the sources; it lives in the bench, which no agent box can
  reach (D42), and nothing of it enters the task repository outside the README's attribution section.

**Operator steps, each a GitHub write made only on the operator's go and read back after (`common.security`: a setting is
the receiver's until it is read back).**

- **O1 — before S1's join PR is opened:** set the task repository to merge commits only (`allow_merge_commit: true`,
  `allow_squash_merge: false`, `allow_rebase_merge: false`), then read the three back (consult C7a, D7). A conflict with
  `common.pull-requests` is open as §9 OP-H, for the team lead before this write.
- **O2 — before E1.S7's GitHub part:** pin GitHub's host key for `[ssh.github.com]:443` in `~/.config/lang-v2/known_hosts_github`
  (mode 600): `ssh-keyscan -p 443 ssh.github.com`, each key's fingerprint compared with the `ssh_keys` GitHub publishes at
  `api.github.com/meta` before the line is kept (consult D1).
- **O3 — E1.S7's P0 lines:** the go for each ruleset write, the deploy-key push of a run base, and the refused PAT push.

**E1.S1 starts from the remote.** The local skeleton and the drafts reach GitHub only through the pre-push check, one branch
per push:

1. **The remote, idempotently.** `URL=https://github.com/oleksandrdubyna88/dew_flow_ai_language_comparison.git` (HTTPS: SSH to
   GitHub times out from this host). `git -C <task repo> remote get-url origin`: no `origin` → `git remote add origin "$URL"`;
   an `origin` with another URL → `git remote set-url origin "$URL"`, the old URL printed; the same URL → nothing. Then `git
   fetch origin` and check that `origin/main` exists and holds `LICENSE`. Running the step twice changes nothing. **Recovery:**
   a fetch that fails (network, auth) leaves the local branches untouched — fix the cause and re-run step 1; a wrong remote
   added by hand is corrected by the same `set-url`; nothing is pushed before step 3 passes, so no step before it needs undoing
   on GitHub. The bench's `push-check.mjs --setup-remote` does this step and prints what it did.
2. **Join the two histories without rewriting either:** on a branch `chore/join-remote` from the local `main`,
   `git merge --allow-unrelated-histories origin/main` (it brings `LICENSE`; the local files and the remote's do not overlap,
   so no conflict is expected — if one appears it is resolved by hand and named). Force-push is never used.
3. **The pre-push check** `node spikes/lang-bench/push-check.mjs --repo <task repo> --branch <b>` over exactly the commits the
   push would add (`git rev-list <b> --not --remotes=origin`), each a named line, exit 0 / 1 / 3 / 4:
   - `leakcheck` (`spikes/minigit-bench/lib/heldout-leak.mjs:111`) with the v2 leak tokens **and six-word shingles of the two
     private aid files**, both read by the tool from `~/rsd/lang-v2/test-design/` at run time — a hit is reported by commit,
     path and line, never by the matched text;
   - the attribution section present in `README.md` only; no credit, link or bench pointer elsewhere;
   - no plan story, epic, decision or test id in an agent-visible path (`csharp/`, `node/`, `tasks/`, `runner/`, `todo/tasks/`);
   - no secret (the family's token shapes) — **named (consult D2):** `github_pat_[A-Za-z0-9_]{20,}`, `ghp_`, `gho_`, `ghs_`,
     `sk-ant-`, `BEGIN (OPENSSH|RSA|EC) PRIVATE KEY`, `AKIA[0-9A-Z]{16}`, each with the length rule of the family's shape list; a
     hit is reported by commit, path and line, never by the matched text;
   - **a known-task text** (specs 01, 02, 05, 06, 09) only when the credit terms of its source are recorded in
     `spikes/lang-bench/inputs/credits-lang.json` — otherwise that branch is refused and stays local *(the terms are recorded
     2026-11-01, above; the check also requires each entry's `credit` line, the CC BY-NC-SA exception sentence for 01 and 02,
     and RealWorld's MIT notice in the README's attribution section)*;
   - **the stack folders hold only the template (consult C6a):** until the series ends, any file under `csharp/` or `node/`
     outside E1.S3's template file set — and any `.cs` or `.ts` file beyond the template's skeleton — is refused; before E1.S3
     lands, anything there but `.gitkeep` is refused;
   - **the family's names are needles (consult D5):** `dew_flow_benchmark`, `PLAN_lang_comparison` and any `dew_flow_[a-z_]+`
     other than `dew_flow_ai_language_comparison` join the needle list for agent-visible paths, with `.gitmodules` the one path
     excluded (E1.S7: its conventions URL is the one allowed URL);
   - **PR bodies too (consult C6b):** `push-check.mjs --text <file>` runs the same scans over a text file; every PR and review
     body is written to a file, checked, and only then passed to `gh pr create --body-file` (or `gh api`).
   **The bench's own pushes (consult C6b, D2):** `push-check.mjs --self --branch <b>` runs `leakcheck` with the aid's shingles
   and the v2 tokens, and the secret shapes, over the bench branch's new commits — the bench is public too, and
   `inputs/bias-review/NN.md`, `inputs/gates/FORMAT-v2.md` and `inputs/exec-probe-<date>.json` carry reviewer or probe text
   into it. Every E1 bench branch runs it before its push; the bench's opt-in `.githooks/pre-push` (`bench scouts hygiene`)
   stays as it is.
4. Push `chore/join-remote` alone and open its PR into `main` (a normal merge on GitHub, CI from E1.S7 not yet required); then
   every other local branch alone, each its own PR. **A tag is never in a push with anything else**; the freeze tag, when it
   exists, is pushed alone and checked to have started no unexpected workflow (`common.task-lifecycle` §3).
   *(2026-11-01, consult D7:)* after operator step O1, the join PR is merged **with a merge commit**, never squash or rebase
   (either would leave `origin/main` unrelated to the local branches again); then local `main` is fast-forwarded to
   `origin/main` (`git merge --ff-only origin/main`) **before any other branch is pushed**. The local branches all descend
   from the local `main` the join merged, so each later PR's diff is its own work.
5. The bench's `spikes/lang-bench/lib/task-repo.mjs` `TASK_REPO_URL` is the GitHub URL from then on; a local-path fetch stays
   for tests only.

## 5. Story map

| Id | Title | Size | Depends on | Model |
|---|---|---|---|---|
| E1.S1 | Repository skeleton from the remote, the attribution cut, the provisional conventions pin, the pre-push check and the first pushes | S (~350 lines) | the remote (exists) | coder sonnet; operator for the credit terms |
| E1.S8a | The baseline visible-sample runner `runner/`, pinned | L (~2,000 Python + ~900 tests) — **two coder briefs, each with its own review** (consult A): part i ≈ 1,100 + ≈ 500 tests, part ii ≈ 900 + ≈ 400 tests (estimated) | — | coder sonnet · brief opus |
| E1.S8b | **The execution layer**: bwrap launch, D15's environment with the culture, the program-only scope, the shared loopback-only netns, deadlines, signals, cleanup; probes T-L30 … T-L34 | L (~900 + ~700 tests) | S8a (part ii), S5 | coder sonnet · brief opus |
| E1.S9 | `FORMAT.md` v2, checked against the runner, gated before any spec gate | M (the document; ~150 lines of tools) | S8a (part i's `--parse-only`; the gate after CP1) | writer: team lead (opus) · extractor: sonnet |
| E1.S8 | FORMAT v1 (built; its gate is v2's) and the conformance check derived from v2 | M (~550) | S9 draft, S8a (part i) | coder sonnet |
| E1.S5 | Toolchains pinned read-only with checksums (TypeScript 6.0.x); the preflight | S (~300) | — | coder sonnet |
| E1.S4 | Package sets and the two cache snapshots; `PACKAGES.md` from the manifest | M (~350) | S5 | coder sonnet |
| E1.S3 | The workspaces: templates (phase A), the ten task pairs after the freeze (phase B) | M (~600) | S4, S5, S8a, S8b; phase B: S2 frozen, S6 | coder sonnet |
| E1.S6 | The visible samples, per task, before the spec's conformance gate | M (10 tasks) | S9, S8 | author: team lead (opus) |
| E1.S2 | **The ten task specs, gated and frozen** | L (10 specs) | S9 gated, S8, per task S6, S2a and the D40 gate | **`model: fable`** (story brief) · spec text: team lead (opus) · review opus |
| E1.S2a | Draft references outside git; the pre-freeze tool on the execution layer | L (20 programs outside git; ~350-line tool) | S3 phase A, S4, S5, S6, S8b, the spec draft per task | coders sonnet (one launch per program) · briefs: team lead (opus) |
| E1.S7 | Process-arm templates, CI, rulesets, deploy key, PAT; run bases as orphan generated trees, shaped for the run-bound broker | M (~550) — **two coder briefs, each with its own review** (consult A): part *conventions snapshot* (≈ 150) and part *GitHub* (≈ 400) | part *conventions snapshot*: — ; part *GitHub*: S1's first push, S3 phase A, operator steps O2 and O3 | coder sonnet; operator (PAT, deploy key, each GitHub write) |

**Waves (§14 step 1's order).** W1: **S8a** ∥ S5 ∥ S1. W2: **S8b** (needs S8a and S5) ∥ **S9** drafted against S8a, then gated
∥ S4. W3: S8 ∥ S3 phase A (on S8b's layer). W4, per task in the order of §4.1 (the pilot specs 01, 05, 04 first, re-conformed
to v2): S6 → S2 draft and conformance → S2a → **the D40 pre-freeze gate on S8b's layer** → S2's document gate, consultant and
bias review → gated. W5: the freeze commit and tag after the tenth; S3 phase B. S7 any time after S1's first push and S3
phase A. *(Superseded 2026-11-01 by the waves below, consult A.)*

**Waves, revised 2026-11-01 (consult A).** W1: **S8a part i** ∥ S5 ∥ S1; then **S8a part ii**. W2: **S8b** (needs S8a part ii and
S5) ∥ **S9** drafted the moment part i's `--parse-only` exists, gated after **CP1** ∥ S4 ∥ **S7 part *conventions snapshot***.
**CP2** after S8b. W3: S8 (needs only part i) ∥ S3 phase A (on S8b's layer, after CP2). W4, per task in the order of §4.1 (01,
05, 04 first, re-conformed to v2): S6 → S2 draft and conformance → S2a → **the D40 gate on S8b's layer, behind the gate lock**
(§3) → S2's document gate, consultant and bias review → gated; **CP3** after task 01 has run this chain end to end, before
tasks 05, 04 and the other seven. W5: **CP4**, then the freeze commit and tag after the tenth — **precondition: E1.S7's tag
ruleset exists and was read back** (consult C7b) — then S3 phase B. **S7 part *GitHub*** any time after S1's first push, S3
phase A and the operator's steps O2 and O3. S1's `chore/join-remote` is merged before any other task-repository branch is
pushed (§3).

**Review checkpoints inside the epic (consult A).** Not one gate at the end: each is an Opus judge's reading
(`common.subagent-models`), its findings resolved like a review's, recorded as a dated line in
`spikes/lang-bench/inputs/checkpoints.md` (append-only, no trap content), and it blocks what it names until it is recorded.

| # | When | What is read together | Blocks |
|---|---|---|---|
| CP1 | after S8a part i and the S9 draft | the grammar rows (V1–V12) and the runner's delta side by side, since the runner follows a row change in the same branch | S9's gate |
| CP2 | after S8b, before the first `prefreeze` run and before S3 phase A | the launch hierarchy's probes T-L30 … T-L34 against the consult's B1–B7 and B10 (signal delivery, the scope's lifetime, `OOMPolicy`, the kill domain, launch failure, timing, the per-case view, locale): the series' verdict machinery is fixed here, and a later change re-dates every evidence (parent §6.1) | S3 phase A, every D40 gate |
| CP3 | after task 01 has run W4 end to end (S6 → S2 draft → conformance → S2a → D40 gate → document gate → gated) | a retrospective of the per-task chain: it runs ten times, and a defect found at task ten is nine tasks of rework | W4 for tasks 05, 04 and the other seven |
| CP4 | before the freeze commit | the staleness records (S2a), the tag procedure (S2, consult C7) and the push-check output of every spec PR, in one sitting | the freeze commit and tag |

---

## E1.S1 — repository skeleton from the remote, the attribution cut, the provisional conventions pin, the pre-push check

**Model:** scenarios + brief Opus (this text) · code Sonnet · review Opus · operator (the known tasks' credit terms).

**Goal.** The task repository on GitHub has the layout of parent §5, a README whose credits, links and the bench pointer live
in one attribution section that a generated run-base tree never carries, the family's conventions pinned, and a push path
that cannot publish a leak — so every later story fills a skeleton rather than inventing one.

**Files.**

| Repository | Path | Change |
|---|---|---|
| task | `README.md` | rewritten: what this is; the task list in our own titles; the §4.6 Node stack table and the Fastify reasoning; the closing `## Attribution` section with every credit, every link and the pointer to the bench plan — the only place any of them appears |
| task | `LICENSE` | from the remote's `ed68ce1` (MIT), by the history join of §4 |
| task | `AGENTS.md`, `CLAUDE.md`, `.agents/PROJECT.md`, `.gitmodules`, `.agents/conventions` | the family's adapters; the conventions submodule at the current `release` sha (provisional; E0.S3 moves it to the parity release on a snapshot branch; a process clone gets it from a local snapshot, E1.S7, OP-A (c)) |
| task | `todo/README.md`, `research/architecture.md`, `research/module_tests.md` | the index (FORMAT and the specs, no plan file); stubs as v1's templates; `module_tests.md` names CI's visible-sample job as the repository's scenario harness |
| task | `runner/.gitkeep`, `tasks/.gitkeep`, `csharp/.gitkeep`, `node/.gitkeep` | the folders of §5 |
| bench | `spikes/lang-bench/lib/run-base-tree.mjs` | new — `generateRunBaseTree({ repo, ref, out })` (the tree at `ref` minus the README's `## Attribution` section, minus any credit file) and `needleScan({ root, needles })`; S7 commits that tree as the orphan run base |
| bench | `spikes/lang-bench/lib/workspace-diff.mjs`, `workspace-diff.mjs` | new — per task, `csharp/<t>/` and `node/<t>/` differ only in the marked stack and framework sentences and the stack's pin files |
| bench | `spikes/lang-bench/push-check.mjs`, `lib/push-check.mjs`, `inputs/credits-lang.json` | new — §4's checks; the credit terms per known task (filled when the owner answers) *(answered 2026-11-01: filled from §4's terms in §4's shape)*; `--text <file>` for PR and review bodies and `--self` for the bench's own branch (consult C6b, D2) |
| bench | `spikes/lang-bench/test/{run-base-tree,workspace-diff,push-check}.test.mjs` | new |

**Scenarios.**

- *Given* a README with an `## Attribution` section holding a credit, a link and the bench pointer, *when*
  `generateRunBaseTree` runs, *then* every other section is byte for byte the same and no D18 or D39 needle is found in the
  tree. **Failure cases:** a README without the section, or with it twice, is refused; a credit planted under `tasks/`,
  `runner/` or a spec makes the needle scan fail naming file and line.
- *Given* an empty task pair, *then* `workspace-diff` passes; *given* an extra file in `node/<t>/` or one byte changed outside
  a marked sentence, *then* it fails naming the path.
- *Given* a branch whose new commits hold a known-task spec and no recorded credit terms, *then* `push-check` exits 1 with
  `known-task text without its credit terms`; *given* a leak token or an aid shingle in an old commit of the branch, *then* it
  fails naming the commit and path, never the text.
- *Given* no `origin`, an `origin` with another URL, or the right `origin` already, *when* `push-check.mjs --setup-remote`
  runs, *then* it adds, re-points (printing the old URL) or leaves the remote, and a second run changes nothing; *given* a
  failing fetch, *then* exit 3 with the cause and no local branch changed.
- *Given* the local `main` and the remote's unrelated `main`, *when* §4 step 2 runs, *then* `chore/join-remote` holds both
  histories and `LICENSE`, and `git push` of it needs no force.
- *Given* a commit adding a fine-grained token, an OpenSSH private key or an `AKIA…` key, *then* `push-check` exits 1 naming the
  commit, path, line and the shape's name, never the text (consult D2). *Given* a `.cs` file under `csharp/<t>/` outside the
  template file set, *then* it is refused naming the path (consult C6a). *Given* `--text <file>` with an aid shingle or a secret
  shape, *then* the same refusal (consult C6b). *Given* `dew_flow_benchmark` in an agent-visible file, *then* a needle hit; in
  `.gitmodules`, *then* none (consult D5).
- *Given* `credits-lang.json` and a README whose attribution section lacks one entry's `credit` line, the CC BY-NC-SA exception
  sentence for 01 and 02, or RealWorld's MIT notice, *then* the branch carrying that spec is refused naming the missing piece.
- *Given* the join PR, *then* it is merged with a merge commit after operator step O1 has been read back, and local `main` is
  fast-forwarded to `origin/main` before the next push (consult D7).

**Acceptance (parent S1 and OP-8 as its text).** `workspace-diff` passes on an empty task pair and fails on a planted
difference; the README's links and attribution appear nowhere under `csharp/`, `node/`, `tasks/`, `runner/` or `todo/tasks/`,
and the generated run-base tree holds none of them (a test greps the tree; the one-commit `git rev-list` is S7's, after the
push). Plus: `push-check` exits 0 for every pushed branch and its output is kept with the PR; the conventions pin is the current
`release` and `pin-check` is green; the first pushes happened as §4 says, with the shas recorded. Plus (2026-11-01): the join PR
merged with a merge commit after O1 was read back, and local `main` fast-forwarded before any other push (consult D7);
`credits-lang.json` holds the owner's terms in §4's shape and the README's attribution section carries every credit line, the
per-file exception and the MIT notice; every PR body of the epic went through `--text` and every E1 bench branch through
`--self` (consult C6b).

**Test plan (red, green, red).** `A_README_copied_whole_would_carry_the_credits_into_every_run_base_and_that_is_the_point`
(red against a generator that copies the tree, green with the cut, red with the cut line removed);
`Workspace_diff_fails_on_one_byte_outside_the_stack_sentence`; `Push_check_refuses_known_task_text_without_credit_terms`;
`Push_check_reports_an_aid_shingle_by_position_and_never_by_text` (with a synthetic aid file in an owned folder); the positive
companion of each scan, `The_needle_scan_still_finds_a_planted_needle`. Added 2026-11-01:
`A_secret_shape_list_without_github_pat_would_pass_a_fine_grained_token_and_that_is_the_point` (consult D2; red with the v1
shape list, green with the named shapes, red with `github_pat_` removed);
`Push_check_refuses_a_source_file_under_a_stack_folder_outside_the_template_set` (consult C6a);
`Push_check_scans_a_body_file_with_the_same_rules` (consult C6b); `The_family_names_are_needles_except_in_gitmodules`
(consult D5); `Push_check_refuses_a_known_task_whose_credit_line_is_missing_from_the_attribution_section`.

**Docs.** Task repository: `README.md`, `todo/README.md`, the research stubs. Bench: `research/architecture.md` names
`spikes/lang-bench/` in its module map; `research/module_tests.md` adds the lang-bench test command.

**Brief (Sonnet coder).** Worktrees: bench `feat/lang-v2-e1-s1`, task repository `chore/join-remote` then `chore/skeleton`.
(1) `generateRunBaseTree`: `git archive <ref> | tar -x` into an owned folder (never a checkout in a working tree), cut
`README.md` from the line `## Attribution` to the next `## ` or the end. (2) `needleScan`: every regular file, the literal
needles from a file given by argument (outside git), plus `https?://`, `\bE[0-9]\.S[0-9]+[ab]?\b`, `\bT-L[0-9]+\b`, `\bG-[0-9]+\b`
and `\bD[0-9]{1,2}\b` in agent-visible paths; report `{ path, line, needle }`. (3) `workspace-diff`: file sets minus each
stack's pin-file list (C#: `global.json`, `nuget.config`, `Directory.Build.props`, `*.csproj`; Node: `package.json`,
`package-lock.json`, `tsconfig.json`, `.npmrc`, `eslint.config.mjs`), then bytes; `STATEMENT.md` may differ only on the lines
after `<!-- stack-sentence -->` and `<!-- framework-sentence -->`. (4) `push-check`: compose the checks of §4 over
`git rev-list <branch> --not --remotes=origin`; read the aid files and the token list only from paths given by argument; print
positions only. (5) The README text is the team lead's; you write its structure check. Commands:
`node spikes/lang-bench/run-tests.mjs test/run-base-tree.test.mjs test/workspace-diff.test.mjs test/push-check.test.mjs`.
**Added 2026-11-01.** (6) The needles match by D18's rule of change 4.1 — case-sensitive on word boundaries (`\b<needle>\b`,
ASCII letters and digits as word characters) plus an explicit list of exact substrings (`redis.io`, `redis-cli`, …); a
normalised field FORMAT marks is not scanned (T-L37). (7) The secret shapes of §4 step 3, by name (consult D2). (8) The
stack-folder rule (consult C6a): read E1.S3's template file set from `spikes/lang-bench/templates/` once it exists; before
that, only `.gitkeep` is allowed under `csharp/` and `node/`. (9) The family's names as needles, `.gitmodules` excluded by
path (consult D5). (10) `--text <file>` and `--self --branch <b>` (consult C6b, D2): the same scanners over a text file, and
over the bench's own new commits. (11) `credits-lang.json`: validate §4's shape (every known spec path has one entry; the
fields its `basis` needs), then check the README's attribution section for each `credit` line verbatim, the per-file
exception sentence naming the two CC BY-NC-SA files, and the MIT notice of an entry with `upstreamNotice`.

---

## E1.S8a — the baseline visible-sample runner `runner/`, pinned

**Model:** scenarios + brief Opus · code Sonnet · review Opus. **Not marked** (the ledger is full); the spec is strict instead —
it is the one program the agent, CI, the pre-freeze gate and E2's held-out scoring all share (D37).

**Goal.** A Python standard-library runner reads a directory of `<id>.in` / `<id>.out` pairs, runs `bash build.sh` and `bash
run.sh` as a spec says, drives an olympiad case, a daemon scenario or a web scenario, compares what it observed with the
expected transcript under FORMAT v2's rules and reports `PASS`/`FAIL` with the first mismatch — built **before** FORMAT v2 is
written down, so that v2 is checked against a program that exists. It knows nothing of boxes, scopes or namespaces: the
execution layer (S8b) hands it a launch prefix and the cgroup to read.

**`runner/` is a directory** (OP-5, now the parent's S8a text): standard-library modules with `run_samples.py` as the entry,
hashed as one manifest in `spikes/lang-bench/inputs/specs.json`, the frozen list naming `runner/`; under `-I` Python leaves the
script's directory off `sys.path`, so the entry adds its own frozen, read-only directory itself. (2026-11-01: the manifest is
written to this story's own file `inputs/specs.d/runner.json` and assembled into `inputs/specs.json`, §3.)

**The grammar it implements — FORMAT v1 §4–§5 plus this delta, which E1.S9 writes into FORMAT v2.** The team lead may change a
row while S9 is drafted; the runner follows in the same branch and S9 is gated against the result.

| # | v2 decision | Runner behaviour |
|---|---|---|
| V1 | the version: `scenario 2` / `transcript 2` | any other version is a format error naming the file (exit 4); v1 files are re-conformed, never run |
| V2 | **exact-decimal JSON numbers**: an integer and a decimal compared by exact value (`int`/`Decimal`, never `float`); `-0` equals `0`; `2` equals `2.0` | `json.loads(text, parse_float=Decimal, parse_int=Decimal, parse_constant=<refuse>, object_pairs_hook=<refuse duplicate names>)` — `NaN`, `Infinity` and `-Infinity` refused, a repeated member name refused; numbers compared by exact numeric value (`Decimal` equality, or after `Decimal.normalize()`), **never by string or by `as_tuple()`** |
| V3 | **default request headers**: `Host: 127.0.0.1:<PORT>` and, with a body, `Content-Length: <n>`, nothing else; a `header` line whose name equals a default (ASCII case-insensitive) replaces it in place | the serialiser of v1 §4.3 with the replacement rule |
| V4 | **`connect` retries** a refused connection every 50 ms until its deadline; still refused → `refused <conn>`, the scenario ends | as stated; the readiness probe keeps v1's 50 ms retry within T |
| V5 | **a closed HTTP connection**: a request that meets a connection the program closed before a single response byte arrived is rewritten once on a new connection within the same step deadline; a second close is `eof <conn> ""` | at most one reconnect per request |
| V6 | **relational expectations** in `.out`: a normalised field may bind what it matched, `${:<type>=<name>}` (inside `repeat` the name may carry `${i}`); `check distinct <name>…` (names or a `<prefix>*` glob; pairwise distinct as bytes or exact JSON values) and `check equal <name> <decimal \| name>`; a `check` produces no entry; a failed one is `FAIL <id> — check: …` | evaluated after the entries matched, in file order |
| V7 | **T and G**: T is the `within` of `start`, G the `within` of the `wait-exit` after `signal TERM`; the conformance check (S8) requires them to equal the spec's T and G; a `--limits` file may carry `readyS` and `graceS`, and a scenario whose values differ is refused (exit 4), never re-timed | as stated |
| V8 | **`sleep` has no deadline** and takes no `within`; it counts toward the 600 s test bound | a `within` on `sleep` is a format error |
| V9 | **`split` offsets index the joined bytes** of the whole string expression after substitution and repetition; strictly increasing, 0 < o < length | as stated |
| V10 | **body precedence**: the transcript's body form decides — `body json` compares JSON values, `body <str>` bytes; the conformance check requires `body json` only where a requirement carries F-JSON | no ambiguity left in the runner |
| V11 | **the program's environment** is the runner's own (which the layer sets: D15's base plus the documented per-stack variables), plus `PORT`, plus the scenario's `env` lines; nothing else | a stub that prints its environment shows exactly that |
| V12 | **runner bounds**: undrained stdout and stderr to files capped at 16 MiB each, truncation recorded; a connection's unread buffer above 256 MiB ends the scenario with the actual-only entry `overflow <conn>`; one test ≤ 600 s; a regex capture runs on at most 16 MiB | as stated. **(consult B9)** The 16 MiB caps name the daemon and web undrained streams only. An olympiad case's stdout is compared **streaming** against the expected bytes, stopping at the first mismatch; a cap applies only to what is kept for the report, and a truncated capture is never a `PASS`. stderr always goes to a file, never a pipe, so a chatty program cannot block |

**Files (bench).** `spikes/lang-bench/run-tests.mjs`, `lib/lower-priority.mjs` (verbatim copy of
`.agents/conventions/tools/templates/lower-priority-preload.mjs`, only `MOUNT` changed); `runner/run_samples.py` (entry, CLI,
kind dispatch, summary, exit codes), `runner/rs_lex.py`, `rs_script.py`, `rs_transcript.py` (lexer, `.in` and `.out` parsers
with validation), `rs_match.py` (byte entries with fields, HTTP replies, exact-decimal JSON with `$unordered`, one-to-one
assignment, V6 checks), `rs_proc.py` (build, start with the launch prefix, readiness, `signal TERM`, `wait-exit`, the group or
cgroup kill and leftover check, memory files when given), `rs_net.py` (TCP with buffers and `recv` modes, `chunk`/`pause`,
`rcvbuf`, `quiet`/`closed`, `shutdown`; the HTTP/1.1 client with V3 and V5), `rs_exec.py`, `rs_olympiad.py`, `rs_report.py`;
`pin-runner.mjs`, `lib/runner-pin.mjs` (manifest → `inputs/specs.json` `runner: { version, files, manifestSha256 }`);
`inputs/specs.json` (new); `test/runner-*.test.mjs`, `test/py/test_*.py`, `test/fixtures/runner/**`. *(2026-11-01, §3:)*
`pin-runner.mjs` writes `inputs/specs.d/runner.json`; `specs-record.mjs`, `lib/specs-record.mjs` and
`test/specs-record.test.mjs` (new, part i) assemble `inputs/specs.json` from the writers' files.

**The CLI.** `python3 -I runner/run_samples.py [--workspace DIR] [--kind olympiad|daemon|web] [--no-build] [--limits FILE]
[--launch-prefix JSON] [--cgroup-of TEMPLATE] [--json OUT] [--only ID] [--parse-only] SAMPLES_DIR`. The kind is read from the
`Kind` row of the workspace's `STATEMENT.md` unless `--kind` is given; the build runs first unless `--no-build`; **without
`--limits` the olympiad time limit is the floor (1 s) and every time verdict says `against the floor`**; without `--cgroup-of`
every case says `memory: not certified`; `--launch-prefix` is a JSON argv put before `bash run.sh` with `{case}` replaced;
`--cgroup-of` is a path template with `{case}` from which `memory.peak`, `memory.events` and `cgroup.procs` are read after the
case; `--parse-only` parses and validates every pair and runs nothing (the conformance check's road in). A `cases.txt` beside
the pairs attaches its R-ids to each case. Exit `0` all passed · `1` a case failed · `3` environment · `4` configuration (usage,
a malformed sample, a version or T/G mismatch — file and line) · `5` the runner itself failed. **Added 2026-11-01:**
`--launch-status FD` reads the JSON lines the layer's bwrap writes with `--json-status-fd`: no `child-pid` line before the
launcher exits → exit **3 `launch failed`** with the prefix's stderr, never a case verdict (consult B5); `--case-view TEMPLATE`
names, with `{case}`, the fresh writable view of the built tree the layer made for that case, into which the runner writes a
scenario's `.scenario/` fixtures before launch (consult B7).

**Scenarios.**

- *Given* FORMAT's olympiad example and a replay stub, *then* `PASS s03 — 1 entries`; *given* a stub that drops the final line
  feed, *then* `FAIL s03` with the byte of the first difference, exit 1.
- *Given* the daemon toy example and a correct toy, *then* PASS; *given* a toy that replies to a split frame before it is
  complete, or one that stops serving a second client while a slow reader reads nothing, *then* the two failures FORMAT §6.2
  states (`noise …`, `timeout …`) at their entries.
- *Given* the web toy example, *then* PASS; *given* a toy that lets two racing requests on one key make two orders, *then* the
  report of FORMAT §6.3.
- *Given* `9007199254740993` against `9007199254740992`, *then* FAIL; `2.0` against `2`, *then* PASS; `-0` against `0`, *then*
  PASS; a body with a repeated member name, or with `NaN` or `Infinity`, *then* FAIL as `bad-json` naming the member or the
  token (V2).
- *Given* a server that closes keep-alive after each reply, *then* the second request on the connection is rewritten once and
  passes; closed twice, *then* `eof` (V5).
- *Given* `check distinct r*` over three bound tokens of which two are equal, *then* `FAIL … — check: r0 and r2 are equal` (V6).
- *Given* a stub that ignores SIGTERM, *then* `stop: still running … killed` and nothing of it is left; a stub whose child
  outlives it, *then* `leftover: 1 process`; a stub that never listens, *then* `not-ready`.
- *Given* a stub that writes 300 MiB nobody reads, *then* `overflow c1` and the runner stays bounded (V12).
- *Given* `scenario 1`, a `within` on `sleep`, a name bound twice or a `split` offset past the end, *then* exit 4 with file and
  line, and nothing is started.
- *Given* `--launch-prefix` and `--cgroup-of` pointing at fake files with `oom_kill 1`, *then* the case is `memory limit`
  whatever the exit status (the real scope is S8b's).
- *Given* a C# toy and a TypeScript toy of one fixture task, *then* the same case list for both (T-L21's shape).
- **Added 2026-11-01 (consult B):** *given* a prefix with bwrap before `bash run.sh` and a stub that traps TERM, *when*
  `signal TERM` runs with `--cgroup-of`, *then* the trap fires — the signal went to the program's pid from `cgroup.procs`, not
  to the launcher (B1); *given* a program behind `--new-session`, *then* the kill at G goes through the scope's `cgroup.kill`
  and nothing is left (B4); *given* a prefix that exits 1 before `run.sh`, *then* exit 3 `launch failed` (B5); *given* a stub
  that sleeps the limit plus 20 ms and exits by itself, *then* `time limit` (B6); *given* a stub that counts its starts in a file
  of its working directory, *then* every case sees 1 (B7); *given* an olympiad expected output above the cap and a stub that
  prints it exactly, *then* `PASS` by the streaming comparison, and *given* a capture truncated by the cap, *then* never `PASS`
  (B9); *given* `LC_ALL=C` with a non-UTF-8 charset and a failing case, *then* the report prints and the exit is 1, not 5; an
  invalid-UTF-8 JSON body is the verdict `bad-utf8` (B10); *given* a stub that prints its environment, *then* exactly V11's set
  and no key outside the runner's allow-list.

**Acceptance (parent S8a).** T-L21's shape for the three kinds against stub programs (stdlib-only imports; identical case lists
for a C# and a TS stub); FORMAT v2's three examples and the three pilot specs' samples (01, 05, 04, once re-conformed — OP-4,
now the parent's text) run through it with the expected verdicts; the pinned version and the manifest hash are in
`inputs/specs.json` before FORMAT v2's gate; the workspace copy equals the pinned tree (S3's check). Plus: module and function
shape measured by an AST test; no `float` reaches a comparison (an AST test); green with `BENCH_REQUIRE_SANDBOX=1`. Plus
(2026-11-01): each part reviewed on its own; the consult's B1, B4–B7, B9 and B10 tests below green, the bwrap-based ones
skipping with their reason only where bwrap is absent (never under `BENCH_REQUIRE_SANDBOX=1`); CP1 recorded before S9's gate.

**Test plan (red, green, red).** (1) `A_float_json_compare_would_equal_two_distinct_large_integers_and_that_is_the_point` — red
with `json.loads` defaults, green with V2, red with `parse_int=Decimal` removed; beside it
`A_repeated_member_name_is_refused`, `Two_and_two_point_zero_are_equal`, `Minus_zero_equals_zero`,
`NaN_and_Infinity_are_refused`, and `A_string_or_tuple_compare_would_call_2_and_2_point_0_different_and_that_is_the_point`
(red with `str()` or `as_tuple()` equality, green with numeric equality). (2)
`A_split_offset_counts_the_joined_bytes_not_the_first_string`. (3) `A_program_that_ignores_SIGTERM_is_killed_with_its_group`
— red before the group kill (the stub's marker survives in `/proc/*/environ`), green, red with `killpg` replaced by `kill`.
(4) `An_unread_flood_ends_the_scenario_instead_of_the_runner`. (5) `Every_runner_import_is_standard_library_or_a_sibling`
beside `The_import_scan_still_finds_a_known_stdlib_import` (`sys.stdlib_module_names`). (6) The FORMAT §6 verdicts, each with
its failing twin. Test (3) is the **plain path** (no `--cgroup-of`); the scope path's kill is `cgroup.kill` (B4).
**Added 2026-11-01 (consult B; the names are the consult's).** (7)
`A_SIGTERM_to_the_launcher_pid_would_never_reach_a_program_behind_bwrap_and_that_is_the_point` (B1) — the prefix is `bwrap
--unshare-pid --new-session --die-with-parent --ro-bind /usr /usr … --` alone (no systemd or nsenter needed to reproduce); red
signalling the `Popen` pid, green signalling the program's pid, red again; SKIP with its reason without bwrap. (8)
`A_group_kill_would_miss_a_program_behind_new_session_and_that_is_the_point` (B4) — red with `os.killpg`, green with
`cgroup.kill` (a scope from the owned helper; SKIP without `systemd-run`). (9)
`A_polling_wait_would_accept_an_exit_fifty_ms_past_the_limit_and_that_is_the_point` (B6) — a stub sleeping the limit + 20 ms;
red with `Popen.wait(timeout=…)`, green with `pidfd_open` + `select` and the wall rule, red again. (10)
`A_launch_prefix_that_exits_before_run_sh_is_launch_failed_never_a_case_verdict` (B5). (11) `Every_case_starts_in_a_fresh_view`
(B7). (12) `A_capped_capture_is_never_a_PASS` beside `A_long_expected_output_is_compared_streaming` (B9). (13)
`A_non_UTF_8_locale_does_not_turn_a_FAIL_into_a_crash` and `An_invalid_UTF_8_JSON_body_is_bad_utf8` (B10). (14)
`The_program_environment_is_exactly_the_V11_set` (a stub printing its environment) beside its companion that still finds a
planted extra key.

**Docs.** `research/module_tests.md`: the runner's flows (olympiad case, each `recv` mode, `together`, capture and reuse,
`unordered`, checks, stop, leftover, overflow, readiness), each `covered` with its test, and what it does not prove (memory
without a cgroup; behaviour beyond the scenarios run). `research/module_lang_bench.md` (new): CLI, exit codes, result format,
bounds; E3.S10 completes it.

**Brief (Sonnet coder).** Branch `feat/lang-v2-e1-s8a`. Read FORMAT v1 §3–§6 (`git -C
/home/jinx/git/dew_flow_ai_language_comparison show main:todo/tasks/FORMAT.md`) and the delta above; implement both, nothing
more. Lexer first with unit tests from §4.1; parsers return frozen dataclasses and a list of `(file, line, message)` — never
raise for a malformed sample. Matching: a small backtracking matcher for fields (shortest run first); JSON by recursion with
exact decimals; `unordered` by bipartite matching (≤ 64 entries; more is a format error). Processes: `subprocess.Popen(prefix
+ ['bash', 'run.sh'], start_new_session=True, env=…, stdin=PIPE)`; SIGTERM to the pid (D38: `run.sh` execs the program),
SIGKILL to the group at G; the leftover check by `os.killpg(pgid, 0)` or the given `cgroup.procs`, the result saying which.
Networking with `socket` and `selectors`, one reader thread per connection into a bounded buffer, deadlines from
`time.monotonic()`, `together` by a `threading.Barrier`. `.scenario/` fixtures: refuse a pre-existing `.scenario` you did not
create (`os.lstat`, `O_NOFOLLOW`). Report exactly FORMAT §5.5's lines; JSON result `{ format: 1, runner, grammar: 2, kind,
build, cases: [{ id, status, reason, rIds, entries, firstMismatch, lifecycle: { stop, leftover, leftoverEvidence, memory },
wallMs }], python }`. Stubs are toys (FORMAT's toys and a replay stub that serves a pair's expected bytes once it received the
pair's sent bytes) — **never an implementation of a task of the series**. `node spikes/lang-bench/pin-runner.mjs` last.
*(The processes and deadlines sentences above are superseded 2026-11-01 for the scope path and for every wait by the
consult's B1, B4 and B6 — part ii below; they stay true for the plain path where no `--cgroup-of` is given.)*

**Two coder briefs inside this one story, each with its own Opus review (consult A).** The story stays one of the parent's
twelve; its size needs two coders and two reviews.

- **Part i (branch `feat/lang-v2-e1-s8a-i`, in place of the one branch named above):** the lexer, both parsers, the
  matcher (V2, V6, V9, V10), the olympiad path with B9's streaming comparison, the report, `--parse-only`,
  `pin-runner.mjs`, `specs-record.mjs` (§3), `run-tests.mjs` and the preload; tests (1), (2), (5), (6), (12), (13). Its
  `--parse-only` unblocks S9's draft and S8. CP1 reads it with the S9 draft.
- **Part ii (branch `feat/lang-v2-e1-s8a-ii`, after part i merged):** `rs_proc`, `rs_net`, daemon and web, lifecycle, V3–V5,
  V7, V11, V12, and the consult's B1, B4–B7; tests (3), (4), (7)–(11), (14). S8b starts after it.

**Part ii's process rules (2026-11-01, consult B).**
- **Whom to signal (B1).** With `--cgroup-of`, `signal TERM` goes to **the program's pid**: the one pid in the scope's
  `cgroup.procs` that is neither a `bwrap` process (`/proc/<pid>/comm`) nor the layer's wrapper (whose pid it writes to
  `<out-dir>/wrapper.pid`, E1.S8b); D38 guarantees exactly one, and a count other than one is the lifecycle finding `fork`.
  Without `--cgroup-of` (the plain tests) it is the `Popen` pid. bwrap 0.11.1 does not forward SIGTERM (§2).
- **How to kill (B4).** With `--cgroup-of`, the kill at G is writing `1` to the scope's `cgroup.kill` (cgroup2, kernel ≥ 5.14;
  this host 6.18); `os.killpg` stays for the plain path only — behind `--new-session` the program is in its own group. The
  leftover evidence is `cgroup.procs` minus the `bwrap` pids and the wrapper, read after the program's exit and **before** the
  kill; the result says which road it took.
- **Launch status (B5).** `--launch-status FD`: the `child-pid` event marks the sandbox ready and gives the clock start;
  `exit-code` the program's status. No `child-pid` before the launcher exits → exit 3 `launch failed` with the prefix's stderr.
- **Every wait (B6; the pilot's F1).** `os.pidfd_open(pid)` + `select` with the exact remaining time, for the program's pid (from
  `child-pid` or `cgroup.procs`), never `Popen.wait(timeout=…)`'s poll; the rule: **a case whose measured wall exceeds its
  limit is `time limit` even when the process exited by itself**; the case clock starts at the `child-pid` event, so the
  `systemd-run`/`nsenter`/`bwrap` set-up is not in the wall. The record keeps `wallMs` (the program) beside `launchMs`, and once
  per run the runner times an empty program through the same prefix (`emptyProgramMs`), the control F1 asks for.
- **A fresh view per case (B7).** The runner never assumes the program's filesystem is its own or persists across cases: it
  writes `.scenario/` fixtures into the case's view (`--case-view`) before launch, and for a held-out run the samples directory
  is never inside the box's binds.
- **Encoding (B10).** `-I` implies `-E`, so `PYTHONIOENCODING` and `PYTHONUTF8` are ignored: `sys.stdout.reconfigure(encoding=
  'utf-8', errors='backslashreplace')` (and stderr) at entry, `encoding=` on every `open`, bytes everywhere in matching, an
  invalid-UTF-8 JSON body the verdict `bad-utf8`, never a traceback.
- **The JSON result** gains `launchMs`, `launch: { failed, stderr }`, `emptyProgramMs` per run and `lifecycle.signalled` (`pid`
  and how it was found).

---

## E1.S8b — the execution layer

**Model:** scenarios + brief Opus · code Sonnet · review Opus. **Not marked** (the ledger is full); strict spec. It runs every
program the series judges — the D40 gate here, E2's validation and calibration, E3's scoring — so a defect in it is a defect of
every verdict, and a change to it re-dates their evidence (parent §6.1).

**Goal.** One Node module, `spikes/lang-bench/lib/exec-layer/`, that starts a program's `run.sh` in a bwrap box with D15's
environment and `de_DE.UTF-8`, inside a systemd user scope that holds **only the program** (`MemoryMax`, `MemorySwapMax=0`),
in a private loopback-only network namespace it shares with the driver (the runner of S8a) while filesystems, PIDs and
cgroups stay separate; enforces T, the step deadlines and G through the runner; and leaves no process, scope or namespace
behind — with one identity (the tree's sha256) that every record names (D36, D43).

**Files (bench).** `spikes/lang-bench/lib/exec-layer/{netns.mjs, box.mjs, scope.mjs, env.mjs, run.mjs, cleanup.mjs,
identity.mjs}`; `spikes/lang-bench/exec-probe.mjs` (the host probe, printed and recorded); `spikes/lang-bench/inputs/specs.json`
(`execLayer: { treeSha256, probedOn, host }`); `spikes/lang-bench/inputs/exec-probe-<date>.json`;
`test/exec-layer-{netns,isolation,scope,env,exec,cleanup}.test.mjs`; `test/fixtures/exec-stubs/{csharp,node,python}/**`.
*(2026-11-01:)* the identity goes to this story's own file `inputs/specs.d/exec-layer.json` (§3); new
`lib/exec-layer/scope-wrap.sh` (consult B2) and `lib/exec-layer/case-view.mjs` (consult B7), both inside the identity's tree;
`test/exec-layer-runner.test.mjs` (T-L30 and T-L31 through the runner and the real prefix).

**The shape — the launch hierarchy validated on this host by the team lead, 2026-10-31, unprivileged** (`ping→PING` passed;
the program's cgroup was `…/app.slice/run-p…scope`, its netns `net:[4026532221]`, the driver's cgroup `0::/non-systemd` in the
same netns). Joining works because the holder's user namespace is owned by the same uid. The layer builds exactly this:

1. **Holder** — `unshare -rn bash -c 'ip link set lo up; exec sleep <case wall + margin>'`, started by the layer (owned name in
   tests); it owns the user and network namespace of one case, which hold `lo` and nothing else.
2. **Program** — `systemd-run --user --scope --unit=<owned name> -p MemoryMax=<bytes> -p MemorySwapMax=0 nsenter -t <holder>
   -U -n --preserve-credentials bwrap <v2 box> -- bash run.sh`, handed to the runner as `--launch-prefix` (everything before
   `bash run.sh`), with `--cgroup-of` naming the scope's cgroup directory. **bwrap runs inside the `nsenter`**, with
   `--unshare-pid --die-with-parent --new-session` and the binds below, **without `--unshare-net`** (the netns is the holder's).
3. **Driver** — `nsenter -t <holder> -U -n --preserve-credentials python3 -I runner/run_samples.py …` (the runner of S8a),
   **outside** the scope and in the same netns.

**The shape, amended 2026-11-01 (consult B1–B5, B7).** The validation of 2026-10-31 settled where the program, the driver and
the network sit; it did not test signal delivery, the scope's lifetime after a fast exit, the OOM policy or the kill domain,
and those four fail as written (§2). Item 2's prefix becomes:

`systemd-run --user --scope --unit=<owned name> -p MemoryMax=<bytes> -p MemorySwapMax=0 -p OOMPolicy=continue <scope-wrap>
<out-dir> nsenter -t <holder> -U -n --preserve-credentials bwrap --json-status-fd <N> <v2 box> -- bash run.sh`

- **`-p OOMPolicy=continue` (B3).** This host's `DefaultOOMPolicy=stop`, which a scope without `Delegate=` inherits: an OOM
  kill would stop the whole scope and remove its cgroup, so `memory limit` could never be read. The pilot's judge carries the
  same fix, dated 2026-10-31.
- **`scope-wrap` (B2).** The program's exit ends the bwrap monitor at once, the scope empties and systemd removes its cgroup
  within milliseconds — before the layer can read `memory.peak`. `scope-wrap.sh` is a POSIX `sh` script of about 20 lines (dash
  on this host), hashed into the layer's identity: it writes its own pid to `<out-dir>/wrapper.pid`, runs its argv, and after
  the command's exit copies its own cgroup's `memory.peak`, `memory.events` and `cgroup.procs` to `<out-dir>` and exits with the
  command's status. Its own memory is charged to the scope: the probe records the baseline `memory.peak` of an empty program
  under the wrapper (the same for both stacks), and the record names it (§12 item 3).
- **No `--die-with-parent` on the program's bwrap (B4).** With it, a lingering child dies with the namespace before anyone
  reads `cgroup.procs`, so D13's "no child left" could never fail. The scope is the kill domain: the runner kills through
  `cgroup.kill` (E1.S8a part ii). `--unshare-pid --new-session` stay.
- **`--json-status-fd <N>` (B5).** bwrap writes `{"child-pid": …}` once the sandbox is set up and later `{"exit-code": …}`;
  the layer passes the descriptor to the runner as `--launch-status N`. It also gives the program's pid (B1) and the clock
  start (B6).
- **The runner signals the program, never the launcher (B1),** by the rule of E1.S8a part ii.
- **A fresh view per case (B7).** `case-view.mjs` makes, per case and outside the timed window, a writable copy of the built
  tree in an owned folder (the pilot judge's shape) and binds it as the workspace; `{case}` in the prefix and the runner's
  `--case-view` name it; it is removed with the case. bwrap 0.11's `--overlay-src <built> --tmp-overlay` is an optimisation CP2
  may take once T-L34 passes with it.

**The box's binds — no home, nothing broad.** `--tmpfs /` is the base, never `--ro-bind / /`; then read-only only what a program
needs: `/usr` (with the `/bin`, `/lib`, `/lib64`, `/sbin` links), the locale archive `/usr/lib/locale/locale-archive` (for
`de_DE.UTF-8`), `/etc/ssl` and `/etc/ca-certificates` only if a stack's runtime reads them at start (the probe says which),
`/etc/passwd`, `/etc/group`, `/etc/nsswitch.conf`, `/etc/localtime`; the S5 toolchain trees (`dotnet-sdk-10.0.403`,
`node-v26.13.0-linux-x64`, `typescript-6.0.<p>`) and the S4 cache snapshots at their own full paths; the workspace copy
writable; `--dev /dev`, `--proc /proc`, a fresh `--tmpfs /tmp`. **No bind of `$HOME` or of any parent of it** (the toolchains
and caches under `~/rsd/lang-v2/` are bound by their full paths only, so nothing else of the home exists in the box), **no
`/mnt`, no `/run`** (so no `/run/user/<uid>/bus`, no ssh or gpg agent, no `/run/WSL`), no `/var/tmp`; `WSL_INTEROP` absent from
the environment and no `/mnt/*` on `PATH` — on this WSL host a box that only hides the home still reads `/mnt/c` and can start
`cmd.exe` through WSL interop (verified 2026-10-29). The host's abstract unix sockets are out of reach because the netns is the
holder's fresh one.
- **Environment** — `env.mjs` builds D15's base (`DOTNET_gcServer=0`, `DOTNET_CLI_TELEMETRY_OPTOUT=1`, `DOTNET_NOLOGO=1`,
  `NODE_OPTIONS` empty, `LANG` and `LC_ALL` = `de_DE.UTF-8`, no invariant switch) plus the documented per-stack variables,
  from one allow-list; `--clearenv` then exactly those keys. *(2026-11-01: plus `TZ=UTC`, D15's base time zone as read at
  `d47a346`; a scenario's `env` line may set another `TZ` for one program.)*
- **Cleanup** — after the case, in this order: the driver collects the transcript and the exit status; the layer reads
  `memory.peak`, `memory.events` and `cgroup.procs`; it stops the scope (`systemctl --user stop <unit>`; every descendant ends
  with it); then it **kills the holder** (SIGTERM, SIGKILL after 2 s), which ends the case's user and network namespace once the
  driver has left it; finally it confirms that no process of the case, no scope and no holder is left. A timeout or an exception
  takes the same path, never a bare "stop waiting". *(Order superseded 2026-11-01 by consult B2 and B4: the scope may already be
  gone when the layer reads it.)* **The order now:** (1) the runner sees the program exit (its pidfd) or kills the scope at G
  through `cgroup.kill`; (2) with the scope still present (a lingering child keeps it), the leftover evidence — `cgroup.procs`
  minus the `bwrap` pids and the wrapper — and `memory.peak` and `memory.events` are read live, before any kill; with the scope
  gone, they are read from the wrapper's copies in `<out-dir>`; neither available is `memory: not certified` for that case,
  reported as a layer defect (the tests below exist to make it impossible); (3) `cgroup.kill`, then `systemctl --user stop
  <unit>`; (4) the holder (SIGTERM, SIGKILL after 2 s); (5) the case view removed; (6) the confirmation that no process,
  scope, holder or view of the case is left.
- **Certification** — the layer refuses to start a data run when `memory.peak` is not readable (the named halt
  `memory-not-certifiable`, exit 3); a non-data run proceeds with `memory bound not certified` (D13, D36).

**Scenarios.**

- *Given* a driver and a stub program started by the layer, *then* their `/proc/<pid>/ns/net` are equal and differ from the
  host's; `/proc/<pid>/cgroup` puts the program in its scope and the driver not; `lo` is the only interface and the driver
  reaches the program's port on 127.0.0.1 (T-L33) — the same test body for a C#, a TS and a Python stub, the same facts.
- *Given* a sentinel file only the driver can see, *then* the program cannot read it; *given* a sentinel under `$HOME`, *then*
  the program cannot read it; inside the box `/mnt/c` does not exist, `cmd.exe` fails to start and `systemd-run --user true`
  fails (no user bus); *given* a program that forks a child,
  *when* the scope is stopped, *then* both end while the driver survives and collects `memory.peak`, `memory.events`, the exit
  status and the transcript; afterwards no process, scope or namespace of the case exists (T-L34).
- *Given* a child that allocates past `MemoryMax` while its parent stays small, *then* `memory.events` shows it and the case is
  `memory limit`; an OOM-killed child under a parent that exits 0 is `memory limit`, never a pass; a lingering child is
  `leftover` with `cgroup.procs` as the evidence (T-L30).
- *Given* each stack's stub `run.sh` ending with `exec`, *then* the started pid is `dotnet` or `node`, with no bash parent in
  the scope, and receives SIGTERM; without `exec`, *then* the test fails (T-L31, re-run by S3 on the real templates).
- *Given* the layer's box, *then* `LANG` and `LC_ALL` are `de_DE.UTF-8`, `locale -a` lists it, a .NET probe prints `de-DE`, a
  Node probe's locale starts with `de`, and no invariant switch is set (T-L32's scoring-box half; the agent-box half is E3's).
- **Failure cases:** a host where `memory.peak` cannot be read refuses a data run with `memory-not-certifiable`; a host
  without unprivileged user namespaces is exit 3 with the probe's output; a program still running at G is killed with its
  scope and reported `stop: … killed`.
- **Added 2026-11-01 (consult B).** *Given* a stub that exits 0 in 10 ms, *then* `memory.peak` is read (from the wrapper's
  copy) and the case is certified (B2). *Given* the allocating-child fixture, *then* `oom_kill ≥ 1` is read back from
  `memory.events` and the case is `memory limit` whatever the exit status (B3). *Given* the lingering-child fixture **through
  the runner and the real prefix**, *then* `leftover` with the live `cgroup.procs` as evidence, and after the kill nothing is
  left (B4). *Given* each stack's stub that traps TERM, *when* the runner's `signal TERM` step runs (never a `kill` by hand),
  *then* the trap fires (B1, T-L31). *Given* a prefix whose bwrap bind is missing, *then* `launch failed`, never a case
  verdict (B5). *Given* two cases of one stub that writes a file in its working directory, *then* the second does not see the
  first's file (B7). *Given* the box, *then* `bash -c true` prints nothing to stderr (no `setlocale` warning; the locale archive
  is bound), beside the `de-DE` probe (B10, T-L32). *Given* `exec-probe.mjs`, *then* its record lists allow-listed environment
  keys only, never the host's (consult C6c, D2), and prints what each runtime believes its memory limit is under
  `MemoryMax=256M` (B8, below).

**Acceptance (parent S8b).** T-L33, T-L34, T-L30, T-L31, T-L32 (scoring-box half) green on the series host with
`BENCH_REQUIRE_SANDBOX=1` and nothing skipped; the program cannot read a sentinel under `$HOME` nor `/mnt/c`, cannot start
`cmd.exe` and cannot reach the user bus; the probes identical for both stacks; the layer's identity in
`inputs/specs.json` beside the runner's; `exec-probe.mjs` output recorded with its date and host; the pre-freeze gate of S2
runs on this layer (D40 not weakened). Plus (2026-11-01): T-L30 and T-L31 run **through the runner and the real prefix**, not
by hand (consult B); the consult's B1–B5, B7 and B10 scenarios above green; the probe record carries the wrapper's baseline
`memory.peak`, the empty-program launch time, each runtime's believed memory limit and allow-listed keys only; CP2 recorded
before S3 phase A and the first D40 gate.

**Test plan (red, green, red).** (1) `A_box_with_its_own_network_namespace_would_leave_the_driver_unable_to_connect_and_that_is_the_point`
— red with v1's `isolation()` (`--unshare-net`), green with the shared holder, red again with the flag restored. (2)
`A_scope_around_the_driver_would_charge_its_memory_to_the_program_and_that_is_the_point` — red when the driver is launched
inside the scope (T-L33's cgroup check fails), green, red. (3) `Stopping_the_scope_ends_a_forked_child_and_the_driver_survives`
— red with a kill of the program's pid only (the child's marker survives), green, red. (4)
`A_box_that_only_hides_the_home_would_still_show_mnt_c_and_that_is_the_point` — red with `--ro-bind / /` plus a tmpfs over
`$HOME` (the `/mnt/c` probe succeeds), green with the `--tmpfs /` base and the explicit binds, red again with `--ro-bind / /`
restored; beside it `The_program_cannot_read_a_sentinel_under_HOME`. (5)
`The_box_environment_is_the_allow_list_and_nothing_else` beside `The_allow_list_still_carries_LANG` (the scan's companion).
**Added 2026-11-01 (consult B; names as the consult gives them where it does).** (6)
`Memory_peak_is_read_after_a_program_that_exits_in_10_ms` — red without the wrapper (ENOENT, `not certified`), green, red with
the copy step removed (B2). (7) `An_oom_killed_child_is_memory_limit_whatever_the_exit_status` — with `oom_kill ≥ 1` read
back; red without `-p OOMPolicy=continue` (the scope is stopped, no `memory.events`), green, red (B3). (8) T-L30's
lingering-child fixture through the real prefix — red with `--die-with-parent` on the program's bwrap (the child is gone before
the read, a false clean stop), green without it, red again (B4). (9) T-L31 through the runner — the signal sent by the
runner's `signal TERM` step, never by `kill` (B1). (10) `A_failed_launch_prefix_is_launch_failed_and_never_a_program_verdict`
(B5). (11) `Two_cases_never_share_a_writable_view` (B7). (12) `The_box_prints_nothing_to_stderr_for_bash_c_true` (B10). (13)
`The_probe_record_holds_only_allow_listed_keys` beside its companion that still finds a planted host key (consult C6c).

**Docs.** `research/module_lang_bench.md` (the layer, its shape, its probe, its identity, the invalidation rule of parent §6.1);
`research/module_tests.md` (the layer's flows: shared netns, separate cgroups, sentinel, scope kill, culture, refusal).

**Brief (Sonnet coder).** Branch `feat/lang-v2-e1-s8b`. Build the hierarchy of *The shape* exactly as written — holder; the
program under `systemd-run --user --scope … nsenter -t <holder> -U -n --preserve-credentials bwrap …`; the driver under `nsenter
-t <holder> -U -n --preserve-credentials` outside the scope. It is validated, not a choice. `exec-probe.mjs` re-runs that
validation on the host and records it (both `/proc/<pid>/ns/net`, both `/proc/<pid>/cgroup`, `ping→PING` over 127.0.0.1, the
bind list, the `$HOME` sentinel, `/mnt/c`, `cmd.exe` and user-bus probes) and exits 3 when any of it fails. Compose the box from
v1's primitives (`spikes/minigit-bench/lib/bwrap.mjs:10-19`: `tmpfs`, `roBind`, `bind`, `devProc`, `setenvAll`) — **not**
`roRoot()` and not `isolation()` (it carries `--unshare-net`); do not edit v1's file; `runBounded`
(`spikes/minigit-bench/lib/run.mjs:35`) for every child with a ceiling; scope and holder names from the owned helper in tests.
Cleanup is one function called on every path (normal, timeout, exception) and itself never throws; a resource it cannot remove
stays named and is reported. Commands: `node spikes/lang-bench/exec-probe.mjs`; `BENCH_REQUIRE_SANDBOX=1 node
spikes/lang-bench/run-tests.mjs test/exec-layer-*.test.mjs`. *("It is validated, not a choice" holds for where the
processes sit; the prefix's flags are amended 2026-11-01 — build *The shape, amended*.)* **Added 2026-11-01:** the prefix of
*The shape, amended* with `-p OOMPolicy=continue`, `scope-wrap.sh`, `--json-status-fd` passed on as `--launch-status`, and no
`--die-with-parent` on the program's bwrap; `case-view.mjs`; the cleanup order above; `TZ=UTC` in the allow-list. The probe
also records (a) the wrapper's baseline `memory.peak` with an empty program, (b) the empty program's launch time (the pilot's
F1 control), (c) an empty stderr from `bash -c true` in the box, (d) **what each runtime believes its memory limit is under
`MemoryMax=256M`** — .NET's `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes` and Node's `v8.getHeapStatistics().heap_size_limit`
(consult B8: .NET sizes its GC hard limit to about 75 % of a detected container limit, so the effective bound and the verdict
label could differ by stack), and (e) only allow-listed environment keys (consult C6c, D2). **B8 stays a probe line here:**
whether to set `DOTNET_GCHeapHardLimitPercent` is D15's decision, made by the team lead on that probe record before E2
calibrates; this story sets nothing for it (§9 OP-K).

---

## E1.S9 — `FORMAT.md` v2, checked against the runner, gated before any spec gate

**Model:** the document by the team lead (Opus, main session); the grammar extractor and its test by Sonnet; the gate by coai's
document gate plus a consultant; review Opus.

**Goal.** One versioned format that every spec, sample and runner agrees with, gated before the first spec gate, every gap the
pilot specs and consult `3ea9ef36` found closed in a section or a fixed sentence, no plan identifier in a file a run base
carries, and its grammar sections handed to the agent as `runner/GRAMMAR.md`.

**Files.**

| Repository | Path | Change |
|---|---|---|
| task | `todo/tasks/FORMAT.md` | v2 (branch `docs/format-v2`): `Format version: 2`; the delta V1–V12 of E1.S8a; an F-LOAD fixed sentence with slots for a load scenario's size and duration; `{ref}` lists of any length; a table belongs to the R paragraph it follows; the T and G series defaults (10 s, 5 s), the override with a reason and where the runner reads them (V7); F-BYTES-W against F-JSON (V10); a **neutral daemon example** over a framed TCP protocol that names no real product (D39); the reading block removed from the spec obligations (D37); the base-environment sentence following D15; §1.5's credit sentence without `NOTES.md` (OP-7, now the parent's S9 text); sections 7–9 moved out; a changelog entry per gap |
| task | `runner/GRAMMAR.md` | generated: v2's sections 3–5 verbatim under a two-line header naming the format version and FORMAT's sha256 |
| bench | `spikes/lang-bench/inputs/FORMAT-MAINTENANCE.md` | new — v1's §7–§9 for the maintainers, with the plan ids they need |
| bench | `spikes/lang-bench/tools/grammar-extract.mjs`, `test/grammar-extract.test.mjs` | new |
| bench | `spikes/lang-bench/inputs/specs.json`, `inputs/gates/FORMAT-v2.md` | `format: { version: 2, sha256, gate }`, `grammar: { sha256 }`; the gate's verdict, reviewer count, every finding and decision, the consultant's answer *(2026-11-01: written to `inputs/specs.d/format.json` and assembled into `inputs/specs.json`, §3; `FORMAT-v2.md` passes `push-check.mjs --self` before the bench push, consult C6b)* |

**Scenarios.** *Given* v2's three examples as files, *then* the runner gives each example's stated verdict against its toy and
each stated failure against its broken toy. *Given* v2, *then* no plan id matches (`E<n>.S<n>`, `D<n>`, `T-L<n>`, `G-<n>`);
FORMAT's own `F-…` ids stay. *Given* the daemon example, *then* no D18 needle hits — **failure case:** a planted product name
fails the check. *Given* a one-line edit to v2's §4, *then* the stored `GRAMMAR.md` no longer matches and S3's check fails.
*Given* a `revise` verdict that changes a grammar row, *then* the runner changes in the same branch before the next round.

**Acceptance (parent S9).** Every gap of the parent's list has a v2 section or fixed sentence, named in the changelog; the three
examples and the three pilot specs' samples run through the runner against stub programs with the expected verdicts; no plan
id anywhere in `FORMAT.md`; the daemon example names no real protocol or product; v2's gate (document gate + consultant)
recorded before the first spec gate; its sha256 replaces v1's in `inputs/specs.json`; specs 01, 05, 04 re-conformed before
their own gates (S2). Plus (2026-11-01): CP1 recorded before the gate round; after the gate, each later FORMAT change lands
alone and every open spec branch is merged up to it and re-conformed (§3, consult A).

**Test plan (red, green, red).** `GRAMMAR_md_is_the_verbatim_sections_of_FORMAT`; `FORMAT_carries_no_plan_identifier` — red on
v1 (its §7), green on v2, red with one id planted.

**Docs.** `FORMAT.md`; the task repository's `todo/README.md`; `research/module_lang_bench.md`.

**Brief (team lead; extractor to Sonnet).** Write v2 from v1, the delta of E1.S8a and the parent's S9 list; keep every v1
sentence no gap touches. Gate: `mcp__coai__open` on the task repository's checkout and branch; `mcp__coai__review_document`
with `documentPath: todo/tasks/FORMAT.md` and a `purposeText` naming who acts on it (the spec author, the suite author, the
runner's maintainers and, through `GRAMMAR.md`, the agents under test in both stacks), what they must be able to do (write and
read a spec and a sample unambiguously; implement the grammar with a standard library alone) and what it does not cover (the
tasks, the tests); `mcp__coai__resolve` every finding; one `mcp__coai__consult` ("is any rule ambiguous for a runner, a spec
author or an agent reading a sample?"). **Extractor:** `grammar-extract.mjs <FORMAT.md> <out>` copies from the `## 3.` heading
to the line before `## 6.`, prefixes `# Scenario and sample grammar (from FORMAT.md v<version>, sha256 <hex>)`, LF line ends;
refuses when a heading is missing.

---

## E1.S8 — FORMAT v1 (built; its gate is v2's) and the conformance check derived from v2

**Model:** scenarios + brief Opus · code Sonnet · review Opus.

**Goal.** FORMAT v1 stays the draft the epoch-3 re-review kept (`d7c73ff`); it has no gate of its own — v2's gate (S9) is its
gate (OP-3, now the parent's S8 text). What remains is **one conformance checker, derived from v2's maintainer list, that reads
a spec and its samples and names every deviation**.

**Files (bench).** `spikes/lang-bench/conformance.mjs`, `lib/conformance.mjs`, `lib/conformance-sentences.mjs` (the fixed
sentences read from FORMAT v2 itself, never retyped), `test/conformance.test.mjs`, `test/fixtures/conformance/**` (synthetic
specs only).

**The checks** (`FORMAT-MAINTENANCE.md`'s list): file name and title; the five header rows and their values; the eight sections
of the kind; R-ids unique, contiguous, ascending before the freeze, F-REQ's `{n}`; every fixed sentence the kind requires,
verbatim after whitespace collapse, slots filled with allowed values; F-INVALID xor F-ERRORS; tables inside their R paragraph;
**T and G stated and equal to every visible scenario's `start … within` and `wait-exit within`** (V7); the samples — exactly
`s01…s<k>`, the olympiad table equal to the files with `⏎` read as LF, each daemon and web pair parsed **through
`run_samples.py --parse-only`, never a second parser**; no needle (`--needles <file>` outside git, plus S1's patterns); `⏎`
only in section 6; no reading block.

**Scenarios.** *Given* a conforming synthetic spec of each kind, *then* exit 0, one `ok` per check. *Given* F-STOP saying 5 s
and a sample waiting `within 4s`, *then* `T/G: sample s02 waits 4 s, the spec says 5 s`. *Given* a table not preceded by an R
paragraph, *then* it is named. *Given* a pair that does not parse, *then* the runner's `(file, line, message)` is quoted.
**Failure case:** a FORMAT file missing a sentence the checker expects makes it refuse (exit 4), never pass every spec.

**Acceptance.** The parent's S8 criteria as they stand after v2; the checker follows a one-word change in a fixture FORMAT; its
output is part of every spec's gate record.

**Test plan (red, green, red).** `A_retyped_sentence_list_would_pass_a_spec_after_FORMAT_changed_and_that_is_the_point` — red
with a literal list, green reading FORMAT, red with the reader stubbed; `The_checker_still_finds_a_known_deviation` beside each
negative test.

**Docs.** `FORMAT-MAINTENANCE.md` points at the checker; `research/module_lang_bench.md`.

**Brief (Sonnet coder).** `node spikes/lang-bench/conformance.mjs --repo <task repo> --task NN-slug --format
<repo>/todo/tasks/FORMAT.md [--needles <file>]`; one line per check, `ok` or `FAIL <check> — <what, where>`; exit 0 / 1 / 4.
Fixed sentences from FORMAT's `text` blocks by id (the table of §2.1 maps id → kinds → section). The runner through
`runBounded`, 60 s.

---

## E1.S5 — toolchains pinned read-only with checksums; the preflight

**Model:** scenarios + brief Opus · code Sonnet · review Opus.

**Goal.** The gate, the references and later every session run on exactly .NET SDK 10.0.403, Node 26.13.0, **TypeScript
6.0.x** (the newest patch of the 6.0 line on npm on the pin day — the line the pinned `typescript-eslint` 8.72.x supports, D6;
7.0.2 is superseded) and the pinned Python, from read-only directories an unattended upgrade cannot move.

**Files.** Bench: `spikes/lang-bench/inputs/stacks-lang.json` (pins with the exact TS patch and the date it was read, checksums,
probe argv, directories), `install-toolchains.mjs`, `lib/toolchain-pins.mjs`, `lib/preflight-lang.mjs`,
`test/{toolchain-pins,preflight-lang}.test.mjs`. Private area: `~/rsd/lang-v2/toolchains/{dotnet-sdk-10.0.403,
node-v26.13.0-linux-x64, typescript-6.0.<p>}/`, `chmod -R a-w` after install.

**Scenarios.** *Given* the official artefacts, *then* the SDK archive's sha512 equals the .NET 10 release metadata's, Node's
tarball equals its `SHASUMS256.txt` line, TypeScript's tarball equals npm's `dist.integrity`, and each directory is read-only
with its tree hash recorded. *Given* a box binding them, *then* `dotnet --list-sdks`, `node --version`, `tsc --version` and the
sha256 of `/usr/bin/python3`'s real file equal the pins. **Failure cases:** a second SDK on the box's `PATH`, another Node
patch, a TypeScript outside 6.0.x, a changed Python binary — each refused naming the pin and the value seen; `locale -a`
without `de_DE.utf8` — refused with the command that generates it.

**Acceptance (parent S5).** The preflight refuses a box whose `dotnet --list-sdks`, `node --version` or Python checksum differs;
the checksums are in `inputs/stacks-lang.json`; plus `tsc --version` is the pinned 6.0 patch and the SDK directory holds only
10.0.403.

**Test plan (red, green, red).** `A_PATH_that_still_finds_the_apt_SDK_is_refused` (red when the probe trusts the first `dotnet`
found, green with the pinned directory alone, red again); `A_TypeScript_outside_the_pinned_line_is_refused`.

**Docs.** `research/module_lang_bench.md` (the toolchain table); the task repository's README names the versions.

**Brief (Sonnet coder).** Downloads with `curl -fsSL` into an owned folder, verify, extract, `chmod -R a-w`; the SDK by
`dotnet-install.sh --version 10.0.403 --install-dir <dir> --no-path` (the script's own sha256 recorded); TypeScript by `npm view
typescript@6.0 version` → the newest patch, then `npm install --prefix <dir> typescript@<that>` with the pinned Node; never apt.
The preflight probes inside a box built by S8b's `box.mjs` once it exists (v1's bwrap primitives until then).

---

## E1.S4 — package sets and the two cache snapshots

**Model:** scenarios + brief Opus · code Sonnet · review Opus.

**Goal.** Each stack's offered set (parent §4.6) is one hashed offline snapshot with a manifest; `PACKAGES.md` is generated from
it; a build in a box with no registry restores exactly that set and fails naming anything else.

**Files (bench).** `spikes/lang-bench/inputs/cache-recipe-lang.json` — C#: a project referencing
`Microsoft.Extensions.Hosting` 10.0.x, `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.x, `xunit.v3` and the test SDK
pieces; Node: `fastify@5.12.7`, `@fastify/jwt@10.2.6`, `@fastify/type-provider-typebox@6.3.0`, `@sinclair/typebox@0.34.52`, pino
as Fastify resolves it, `@types/node` 26.x, `typescript@6.0.<p>` (S5's pin), `typescript-eslint@8.72.x` with its type-aware
rules, and the lint packages S7's template needs — each with a `purpose` field; `inputs/cache-snapshots-lang.json`,
`make-snapshots.mjs` (with `--prune-superseded`, §8), `lib/packages-md.mjs`, `test/{cache-lang,packages-md}.test.mjs`. Private area:
`~/rsd/lang-v2/caches/<stack>-<treeSha256>/`.

**Scenarios.** *Given* the recipe, *when* `makeSnapshot` (`spikes/minigit-bench/lib/cache-snapshot.mjs:162`) runs per stack in a
box through the egress proxy, *then* the tree hash and the package list are registered (`registerSnapshot`, `:233`). *Given* a
box with the registries refused, *then* an offline `npm install fastify@5.12.7` and `dotnet restore` of the pinned project
succeed, and `typescript-eslint` parses a `.ts` file with type information under the pinned TypeScript. **Failure case:** a
package outside the set fails and the message names it (T-L13).

**Acceptance (parent S4).** T-L13; the manifest lists every package and version; a package outside the set fails naming it; both
hashes in `inputs/cache-snapshots-lang.json`; `PACKAGES.md` derived, never typed.

**Test plan (red, green, red).** `A_registry_reachable_from_the_box_would_hide_a_missing_package_and_that_is_the_point`;
`PACKAGES_md_is_derived_from_the_manifest`; `Type_aware_lint_runs_under_the_pinned_TypeScript` (red under a TypeScript outside
the linter's peer range, green under the pin).

**Docs.** `research/module_lang_bench.md` (recipe and snapshot record).

**Brief (Sonnet coder).** Reuse v1's `makeSnapshot`, `seedCaches`, `treeHash`, `packagesOf` as they are; add only the v2 recipe
and the `PACKAGES.md` renderer (package, version, purpose). Snapshots read-only after registration; the toolchains of S5 in
the box.

---

## E1.S3 — the workspaces

**Model:** scenarios + brief Opus · code Sonnet · review Opus.

**Goal.** Per task and stack a workspace an agent can start in: built by `bash build.sh` offline, started by `bash run.sh`
ending with `exec` (D38), carrying the statement, the samples, the read-only `runner/` and the pin files — identical across
stacks but for the stack. Phase A (the build and start contract the D40 gate needs) before any spec freezes; phase B (the
statements, which need a frozen spec) after.

**Files.** Bench: `spikes/lang-bench/templates/{csharp,node}/{olympiad,daemon,web}/**` (one source: `build.sh`, `run.sh`, the pin
files, the `.csproj` skeleton, `nuget.config` with the offline source only, `Directory.Build.props`, `package.json`,
`package-lock.json`, `tsconfig.json` — `strict`, `noEmit`, `erasableSyntaxOnly`, `verbatimModuleSyntax`, `module nodenext` —
`.npmrc` `offline=true`, `eslint.config.mjs`), `build-workspaces.mjs`, `lib/statement.mjs`, `lib/task-repo.mjs` (fetch at a sha
into `~/.cache/lang-v2/<sha>/`, verified against `inputs/hashes-lang.txt`, v1's `fetchInputs` shape), `inputs/hashes-lang.txt`,
`test/{templates,statement,task-repo,workspace-exec}.test.mjs`, `test/fixtures/hello/**`. Task repository: `runner/` (S8a's
pinned tree plus `GRAMMAR.md`), `csharp/<NN-slug>/**`, `node/<NN-slug>/**` (phase B).

**Phase B's statement.** `STATEMENT.md` = the frozen spec's text + the stack sentence (C#: *"Use C# 14 on .NET 10 (SDK 10.0.403
is installed; the project files in this folder are pinned)."*; TypeScript: *"Use TypeScript 6.0 with `strict`, run by Node 26
with native type stripping (no build step), with `tsc --noEmit` as the type check."*, with S5's exact patch written into the
registered sentence) + the framework sentence for a web task, each on its own marked line.

**Scenarios.**

- *Given* each stack's template on S8b's layer with no registry, *then* the hello program builds within 600 s and
  `python3 -I runner/run_samples.py samples/` reports its sample verdicts.
- *Given* the started pid, *then* it is `dotnet` or `node` with no bash parent and receives SIGTERM (T-L31 on the real
  templates); **failure case:** a `run.sh` without `exec` fails.
- *Given* a fetched task repository, *when* one workspace file differs from `hashes-lang.txt`, *then* the fetch is refused naming
  it (T-L1); a bare session's workspace holds exactly the task folder's files (T-L2).
- *Given* task 9, *then* both workspaces hold the same file set as every other olympiad workspace and no parser, adapter or
  grader file (D33).
- *Given* a frozen spec, *then* `STATEMENT.md` equals it but for the marked sentences; a one-word edit in the spec fails the
  test.

**Acceptance (parent S3).** Each empty workspace builds offline and `run.sh` starts a hello program; T-L1, T-L2, T-L31; the runner
runs inside the box; the `runner/` hash equals `inputs/specs.json`; the statements equal the spec plus the sentences; the
task-9 file set; `workspace-diff` clean on ten tasks (phase B).

**Test plan (red, green, red).** `A_run_sh_without_exec_leaves_a_bash_parent_that_eats_SIGTERM_and_that_is_the_point`;
`A_workspace_file_off_the_hash_list_is_refused`; `The_statement_differs_from_the_spec_only_in_the_marked_sentences`.

**Docs.** The task repository's `research/module_tests.md`; bench `research/module_lang_bench.md`.

**Brief (Sonnet coder).** C#: `build.sh` = `dotnet build App.csproj -c Release --nologo` against the offline source of
`nuget.config`; `run.sh` = `#!/usr/bin/env bash`, `set -euo pipefail`, `cd "$(dirname "$0")"`, `exec dotnet
bin/Release/net10.0/App.dll`. Node: `build.sh` = `npm ci --offline` and `npx --offline tsc --noEmit -p tsconfig.json`; `run.sh`
= … `exec node src/main.ts`. Neither script sets D15's variables (the layer does). Generate into a worktree of the task
repository, never into a checkout someone works in.

---

## E1.S6 — the visible samples

**Model:** the samples by the team lead (Opus — samples are test design); the checks are S8's and S8a's; review Opus.

**Goal.** Per task a few small, clean samples in v2's pair format, existing **before** the spec's conformance gate, run against
both draft references on the execution layer before the freeze (D40), and validated again with the held-out cases in E2.

**Files.** Task repository: `tasks/<NN-slug>/samples/s01.in` … `s<k>.out` (01's from its spec table, 04's from its text, 05's
re-conformed); the spec's section 6 with the same bytes. Bench: `spikes/lang-bench/inputs/samples-lang.json` — the files' sha256
and a dated line *"checked against the private test-design notes on <date>"*, with no case text and no reason.

**Scenarios.** *Given* a spec and its `samples/`, *then* the conformance check finds the table or code blocks equal to the files;
one byte changed in a file fails naming the sample. *Given* a scenario pair, *then* `--parse-only` parses it. *Given* both draft
references, *then* both pass the samples in the pre-freeze tool (S2a). **Failure case:** a sample a reference cannot meet within
the spec's T, G or step deadlines is changed before the freeze; after it, a sample change is a spec amendment. **(consult C5,
2026-11-01)** A sample is never bent to the references: every sample or spec change triggered by a failing reference is a
dated gate-record line naming the R-id and the verdict *spec*, *sample* or *reference at fault*, **decided by the team lead,
never the coder**; a changed sample re-runs conformance and the D40 gate.

**Acceptance (parent S6, OP-9 as its text).** Table and files hold the same bytes; both draft references pass them on the layer
before the freeze; the same against the final references closes in E2; "no sample reaches a trap" is checked now against the
private notes (`~/rsd/lang-v2/test-design/`, D41) and formally in E2 once the trap table exists.

**Test plan.** The checks are S8's; this story's red is real and recorded: the first conformance run on each pilot spec is red
(01 has no sample files, 05's are v1, 04's exist only in its text), then green.

**Docs.** The spec's section 6; `inputs/samples-lang.json`.

**Brief (team lead).** One sample per kind of behaviour section 1 names, at most five; every deadline stated in the file; T and G
as the spec states them; open the private notes on the host before writing and never copy anything held-out-shaped into
`samples/`.

---

## E1.S2 — the ten task specs, gated and frozen — cornerstone

**Model:** **`model: fable`** — Fable writes this story's per-task spec briefs; the spec text is the team lead's (Opus, main
session, D21); Opus reviews; the gate is coai's document gate plus a consultant plus the bias review. If Fable refuses on a
limit, Opus writes the briefs and the summary says so.

**Goal.** Ten stack-neutral specs that two references written apart implement identically, that name no source, trap or
language, that conform to FORMAT v2, whose riskiest contracts both draft references executed on the execution layer before the
freeze, gated per task and frozen together under one tag — because an ambiguous or biased spec makes every later failure
unadjudicable.

**Files.** Task repository: `todo/tasks/NN-slug.md` × 10 (branch `docs/spec-NN` each; 01, 04, 05 re-conformed to v2 by a fix
forward). Bench: `spikes/lang-bench/inputs/bias-review/NN.md` (verdict, reviewer count, notes, every finding and decision, the
bias answer per stack — no case text), `inputs/specs.json` (per spec `sha256`, `state: draft | gated | frozen`, `gate`, `rIds`,
`T`, `G`), `lib/spec-freeze.mjs` (`specBlobMatches({ repo, tag, path })`, T-L20's guard half) and its test. Private area:
`~/rsd/lang-v2/test-design/spec-briefs/NN.md` (Fable's briefs). *(2026-11-01:)* the per-spec rows go to this story's own file
`inputs/specs.d/spec-states.json`, assembled into `inputs/specs.json` (§3), and gain the freeze's `commitSha` and `tagObjectSha`
(consult C7a); `lib/spec-freeze.mjs` gains `planFreeze({ repo, rows })` (consult C7a, C7d).

**Per task, in the order of §4.1** (the pilot specs first): Fable's brief → the spec draft → the samples (S6) → conformance
green (S8) → the two draft references (S2a) → **the D40 gate on S8b's layer**: both drafts pass every visible sample and **the
task's riskiest held-out-shaped scenarios listed in `~/rsd/lang-v2/test-design/PLAN-risky-scenarios.md`** with the spec's T, G
and step deadlines in force — a value a reference cannot meet is changed in the spec now → the document gate, the consultant,
the bias question, two readers (an Opus judge and the consultant) with no open question → `Gate verdict` row, `state: gated`,
the sha256 → the PR merged. **Per task the spec is gated; one tag freezes all ten after the tenth** (OP-2, now the parent's
text): one commit on `main` sets every `Freeze tag` row, the annotated tag `spec-frozen-<yyyymmdd>` on it, `state: frozen` and
the frozen hashes, the tag pushed alone. From its gate on, a spec changes only by a dated amendment applied to both stacks;
**a change to the runner or the layer re-runs the D40 gate of every spec gated on the old one** (parent §6.1).

**Added 2026-11-01 (consult C1, C4, C5, C7, D6).**
- **The gate record is about this text (C1).** The `Gate verdict` row names the gate record's `specSha256`; a record whose
  `specSha256`, `formatSha256` or samples' hashes differ from the blobs at the gate commit is `stale` (E1.S2a), so a `revise`
  that changes a requirement after the D40 gate re-runs the gate before `state: gated`.
- **Ambiguity is asked, item by item (C4).** The consultant question of the document gate walks Fable's ambiguity list (c)
  item by item — spec questions only, never the aid; and both draft-reference coders' "requirements I found unclear" lists
  (E1.S2a) are closed by a text change, each, before the document gate.
- **Changes caused by a failing reference (C5)** follow E1.S6's rule: a dated gate-record line with the R-id and the fault
  verdict, decided by the team lead.
- **The freeze, step by step (C7).** (a) The freeze commit lands on `main` through a PR, like every spec change (never pushed
  by the deploy key, which is no bypass actor on `main`, C7c and D6); after its merge, `git fetch origin`, then the annotated tag
  on **`origin/main`'s sha** — never a local sha, which a merge on GitHub can leave off `main` — and the record keeps the
  commit sha and the tag object sha; every later check compares the **sha**, not the tag name. (b) The tag ruleset (E1.S7) must
  exist and be read back before the tag is pushed: a precondition of W5. (c) Dated amendments after the freeze also land
  through PRs. (d) The tag name is derived from the `Freeze tag` rows: `planFreeze` refuses a freeze whose ten rows do not name
  one and the same tag, or name a date other than the tag's.
- **The known tasks' credit terms (§4, 2026-11-01)** bind the spec text: 01 and 02 are adaptations (the README states their
  CC BY-NC-SA 4.0 exception; nothing in the file); 05 describes the protocol subset in our own words and never takes the
  product's name; 09 is written wholly in our own words from the problem idea only, with no copied text; 06 is an adaptation of
  an MIT spec. The team lead's review of each against its source is recorded in that spec's gate record (the push check cannot
  prove "own words").

**Scenarios.** *Given* a gated spec edited outside its header rows without an amendment item, *then* the check fails naming it.
*Given* the frozen tag and a run base whose spec blob differs, *then* `specBlobMatches` refuses it. *Given* a known task, *then*
D18's needles find nothing. *Given* `revise`, *then* the accepted findings change the text and conformance runs again before
the next round; *given* `call_human`, *then* the spec stops and the person is asked. *Given* a draft reference that misses a
deadline the spec states, *then* the spec or the reference is fixed before the gate round — never a wider deadline after the
freeze. **Added 2026-11-01:** *given* a `revise` that changes a requirement after the D40 gate passed, *then* the gate record is
`stale` and the spec cannot reach `gated` until the gate re-runs (C1); *given* ten rows naming two dates, *then* `planFreeze`
refuses (C7d); *given* a local freeze commit whose sha differs from `origin/main`'s, *then* the tag is placed on `origin/main`'s
sha and the record holds that sha (C7a); *given* no tag ruleset read back, *then* the tag push is refused (C7b); *given* a
known-task spec whose gate record lacks the team lead's source review, *then* `state: gated` is refused.

**Acceptance (parent S2, per task).** The verdict is `proceed`, `good_enough` or `continue_anyway`; the consultant answered; the
bias question answered per stack with no open finding; no id, title, site or trap in the text; a known task's needles find
nothing; the spec conforms to v2; the floors and rule, T, G and step deadlines, semantic bounds, I/O and start contracts, the
frozen list and "hidden tests exist" present; the D40 gate passed for both drafts, the private aid's scenarios for the task
included (their count is the aid's; the record names only the count); two readers list no open question. **Whole story:** the
ten verdicts and the tag exist before E2.S1 finalises a reference. Plus (2026-11-01): the `Gate verdict` row names the
record's `specSha256` and no record is `stale` (C1); every ambiguity-list item and every coder's unclear item closed by a text
change (C4); every reference-triggered change has its fault line (C5); the tag on `origin/main`'s sha with both shas recorded,
the tag ruleset read back first, the rows and the tag agreeing (C7); CP3 after task 01 and CP4 before the freeze recorded.

**Test plan (red, green, red).** `A_spec_edited_after_its_gate_without_an_amendment_is_refused` (red before the check, green, red
with the header-row exemption widened to the whole file); `A_run_base_spec_blob_off_the_tag_is_refused_before_launch`
(T-L20); per spec the first conformance run recorded red (or why it was green at once), then green. Added 2026-11-01:
`A_tag_on_a_local_sha_off_main_would_freeze_a_commit_main_never_had_and_that_is_the_point` (C7a: red tagging `HEAD` after a
squash-style merge fixture, green tagging the fetched `origin/main` sha, red again);
`The_freeze_refuses_rows_that_disagree_on_the_tag` (C7d).

**Docs.** The specs; the task repository's `todo/README.md`; `inputs/bias-review/NN.md`; `inputs/specs.json`.

**Brief (Fable spec writer).** For each task write `~/rsd/lang-v2/test-design/spec-briefs/NN.md` (outside git) from the parent's
§4.1 row, D3, D4, D13, D14, D18, D25, D33, D35, D39, FORMAT v2, and the private aid —
`~/rsd/lang-v2/test-design/PLAN-trap-table.md` and `PLAN-risky-scenarios.md`, opened on the host. It holds: (a) **the
requirement inventory** — every behaviour a held-out case will assert, as a sentence the spec must contain, so that D25's
"every held-out test checks behaviour the statement requires" holds by construction; (b) **the contract decisions** the spec
must make (protocol matters, semantic bounds, clock semantics, T and G or an override with its reason, error behaviour or
F-INVALID's scope); (c) **the ambiguity list** — every place two careful references could read differently, each with the
sentence that closes it; (d) **the bias check** — per requirement, whether either stack needs more than the required behaviour
to meet it, and the neutral wording, judged against the aid and the declared culture (D35); (e) **the sample plan** for S6;
(f) **the riskiest-contract scenarios** of the aid for the task, as scenario pairs beside the notes, never in git. Known tasks
stay paraphrased with no id, title, site or hint that they are known; no trap and no language in the text. Hand each brief to
the team lead; Opus reviews the spec against the brief before the gate. Nothing of the brief is quoted in a PR, commit or
review body. *(2026-11-01:)* item (c), the ambiguity list, is written so the document gate's consultant can be asked it item
by item (consult C4); for the known tasks the brief follows the owner's credit terms of §4 (09 and 05 in our own words; the
product's name never the task's).

---

## E1.S2a — draft references outside git; the pre-freeze tool on the execution layer

**Model:** briefs by the team lead (Opus) per task and stack · code Sonnet, one launch per program, the two stacks' coders never
seeing each other's code · review Opus.

**Goal.** For each task one C# and one TypeScript reference, written apart from the spec draft and samples, living in
`~/rsd/lang-v2/refs/` and never in a workspace, a run base or git, so that the D40 gate can execute them (E2.S1 finalises them);
and one tool that runs the gate on S8b's layer the same way every time and records under which identities it ran.

**Files.** Private area: `~/rsd/lang-v2/refs/<NN-slug>/{csharp,node}/` (each a full workspace: S3's template plus sources).
Bench: `spikes/lang-bench/inputs/references.json` (per task and stack `treeSha256`, `state: draft`, `specSha256`, `writtenOn`,
and per gate run `{ result, on, runnerManifest, execLayer, formatSha256, specSha256 }`), `prefreeze.mjs`, `lib/prefreeze.mjs`,
`lib/gate-staleness.mjs`, `test/{prefreeze,gate-staleness}.test.mjs`.

**The tool.** `node spikes/lang-bench/prefreeze.mjs --task NN-slug --refs ~/rsd/lang-v2/refs --extra <private dir>` copies each
reference to an owned pristine folder, runs the runner over the task's `samples/` and over the private scenario pairs given by
`--extra`, on S8b's layer, and records one line per reference with the case ids that failed (ids only). It refuses `--refs` or
`--extra` inside any git worktree (`refuseInsideWorktree`, `spikes/minigit-bench/lib/git-guard.mjs:28`).
`gate-staleness.mjs` is T-L36's E1 half: a gate record whose `runnerManifest` or `execLayer` differs from the current one is
`stale`, and the freeze refuses while any record of a task is stale (E2.S3 owns the limits half of T-L36).

**Added 2026-11-01 (consult A, C1–C4).**
- **Staleness covers the text and the samples (C1):** a record is also `stale` when its `specSha256`, `formatSha256` or the
  samples' hashes (`samplesSha256`, over the sorted `samples/` files) differ from the blobs at the gate commit; the freeze
  refuses on any stale record.
- **`--expect-private <n>` is required (C2):** `n` is the task's count of private pairs, read by the operator from the private
  aid; a different number of pairs under `--extra` is exit 4 naming both numbers, and `n` goes into the record as a checked
  input (the record still names only the count).
- **`--limits <file>` is required (C3):** written from the spec's T, G and step deadlines, so the runner's V7 refusal (exit 4)
  also covers the private pairs, which no conformance check reads; a run without `--limits` is refused.
- **The unclear lists are gate artefacts (C4):** each coder's "requirements I found unclear" list — or the sentence "no
  requirement unclear" — is kept at `~/rsd/lang-v2/refs/<NN-slug>/<stack>/UNCLEAR.md` (outside git: it is about the draft spec,
  and its wording is kept out of public), and the record names its sha256, its item count and, per item, the commit of the
  spec text change that closed it; a gate run with an open item is refused.
- **The gate lock and the load (consult A; the pilot's F5):** the tool takes the gate lock of §3 for its whole run and refuses
  if it is held; the record keeps `/proc/loadavg` at start and end and the `underLoad` flag, with §3's re-run rule.
- **The record** per gate run is now `{ result, on, runnerManifest, execLayer, formatSha256, specSha256, samplesSha256,
  privateCount, limitsSha256, load: { start, end }, underLoad, unclear: { sha256, items, closedBy } }`; an earlier record stays,
  marked `stale` when it is.

**Scenarios.** *Given* both drafts, *then* each line says `pass` with the hashes it ran under. *Given* `--refs` inside the bench
checkout, *then* exit 4 and nothing runs. *Given* a draft that fails one private scenario, *then* `fail` with its id only, exit 1.
*Given* the layer's identity changed after a gate, *then* that task's record is `stale` and the freeze is refused until the gate
runs again. **Failure case:** the layer refuses (`memory-not-certifiable` or no user namespace) — exit 3, nothing recorded as
passed. **Added 2026-11-01:** *given* a spec text changed after its gate record, *then* `stale` (C1); *given* `--expect-private
3` and two pairs, *then* exit 4 (C2); *given* no `--limits`, *then* refused; *given* a private pair with a `start … within`
other than the spec's T, *then* the runner's exit 4 (C3); *given* an unclear list with an open item, *then* the gate run is
refused (C4); *given* the gate lock held, *then* exit 3 naming the holder, nothing run (consult A).

**Acceptance (parent S2a).** For each task both drafts build and run on the layer through the runner and pass every visible sample
and the task's riskiest scenarios listed in the private aid; the drafts are hashed in `inputs/references.json` as `draft`; no
draft or its hash leaks into the task repository (`leakcheck`; a test asserts no file of either repository has a reference
file's sha256).

**Test plan (red, green, red).** `A_reference_inside_a_worktree_is_refused_before_it_runs`;
`A_gate_record_from_an_older_layer_is_stale_and_blocks_the_freeze` (red without the identity comparison, green, red);
the tool's own tests use toy programs, never a reference. Added 2026-11-01:
`A_gate_record_of_an_earlier_spec_text_would_freeze_a_pass_the_text_never_had_and_that_is_the_point` (C1: red comparing
the identities only, green with `specSha256`, `formatSha256` and `samplesSha256`, red with the spec hash dropped);
`A_private_pair_count_other_than_expected_is_refused` (C2); `A_prefreeze_run_without_limits_is_refused` (C3);
`A_gate_run_with_an_open_unclear_item_is_refused` (C4); `A_gate_run_while_the_lock_is_held_is_refused` beside
`A_lock_whose_owner_is_gone_is_removed_and_named` (consult A).

**Docs.** `research/module_lang_bench.md` (the tool, its record, the private area).

**Brief (team lead → Sonnet coder, per task and stack).** Give the coder the spec draft, the samples, S3's template, `PACKAGES.md`
and the build and run commands, and ask for the spec's requirements implemented completely and idiomatically for its stack —
**not** the private aid, the spec brief, the riskiest scenarios or the other stack's reference. The coder runs `python3 -I
runner/run_samples.py samples/` in its workspace until it passes and reports which requirements it found unclear; each goes back
to the spec before the gate. *(2026-11-01, consult A and C4:)* the unclear list is a required file (`UNCLEAR.md` beside the
reference, or the sentence "no requirement unclear"); each coder works in a fresh owned folder of its own (`ownedFor`,
outside every git tree and outside `~/rsd/lang-v2/`) holding only its inputs — never under the bench, the task repository
or `refs/`, since a shared parent folder is enough for one coder to read the other stack's code — and its result is
copied into `refs/<NN-slug>/<stack>/` when it has finished; the two stacks' coders of one task never run at the same time;
the twenty launches are otherwise independent.

---

## E1.S7 — process-arm templates, CI, rulesets, deploy key, PAT; run bases shaped for the run-bound broker

**Model:** scenarios + brief Opus · code Sonnet · review Opus · operator (the PAT, the deploy key, the go for each GitHub
setting). Built to v1's design ([PLAN_minigit_stack_bench_E2.md](PLAN_minigit_stack_bench_E2.md) E2.S4,
`PLAN_minigit_stack_bench.md` §6) for one repository with two stack folders (D32), **and matched to D42**: the process agent has
no direct `github.com` egress and works in an isolated bare repository per run that the broker (E3.S7a) serves and mirrors.

**Goal.** What a process session is given — conventions, instructions, CI, the stack folder — is one source, and each run base
is an orphan commit of the frozen tag's generated tree (D39), holding no credit, link or attribution history, cut only by the
deploy key, protected by rulesets — exported as exactly the one commit the broker's per-run bare repository may hold.

**Files.** Task repository: `.github/workflows/{ci.yml,family-checks.yml,pr-title.yml}` (one job per stack, path filters covering
each stack folder and every shared path it depends on — `runner/`, `todo/tasks/` — or unfiltered; the required `build · test`
job per stack; the visible-sample job not required; `branches: [main, 'run/**']`); settings and rulesets `run-main`,
`template-frozen`, the tag ruleset; the deploy key as the only bypass actor. Bench: `spikes/lang-bench/lib/run-base.mjs`
(`cutRunBase({ rid, tag })`: S1's `generateRunBaseTree`, the needle scan, `git commit-tree` with no parent, pushed to
`run/<rid>/main` with the deploy key; `exportRunBase({ rid, out })`: a bundle holding exactly that commit and its tree — the
seed E3.S7a's per-run bare repository is made from), `lib/github-lang.mjs` (settings and rulesets as data, applied with
`gh api` and read back), `lib/conventions-snapshot.mjs` (OP-A (c), below), `p0-lang.mjs`,
`test/{run-base,github-lang,templates-d42,conventions-snapshot}.test.mjs`. Private area: `~/rsd/lang-v2/conventions/<sha>.git`
(read-only).

**What the templates must not assume (D42).** No file of the generated tree tells the agent to reach `github.com` directly, names
another repository or another run, or carries a remote URL; CI runs on the branch the broker mirrors, so a workflow needs no
step the box would have to reach GitHub for; the PAT is host-side only (the broker's, then the harness's), never in a box
environment, argv or file the box can read. **`.gitmodules` is the one allowed URL (consult D5, 2026-11-01):** it names a
family repository (the conventions), not another run, and OP-A (c) keeps it as committed; the host check excludes it **by
path**, and egress (D17) is the control — so neither is the check weakened nor the seeded clone's `.gitmodules` made to differ
from the pinned tree.

**The deploy key's road (consult D1; precondition met 2026-11-01, §4).** `cutRunBase` pushes over SSH on port 443, the only
SSH road from this host: the remote `ssh://git@ssh.github.com:443/oleksandrdubyna88/dew_flow_ai_language_comparison.git`, with
`GIT_SSH_COMMAND='ssh -i ~/.config/lang-v2/deploy_key_lang_v2 -p 443 -o IdentitiesOnly=yes -o IdentityAgent=none -o
BatchMode=yes -o StrictHostKeyChecking=yes -o UserKnownHostsFile=~/.config/lang-v2/known_hosts_github -o ConnectTimeout=20'`,
whose `known_hosts_github` holds the pinned `[ssh.github.com]:443` entry of operator step O2. git runs that value through a
shell, so it is built from these fixed paths only, never from an input; it is set in the `env` of the one `git push` child,
never in `process.env`. The key and the known-hosts file are read only after `refuseInsideWorktree` (consult D2). Every other
push of the epic stays HTTPS.

**The PAT (consult D3, D4 adjusted).** The PAT is read from `~/.config/lang-v2/github-pat` per call and passed only in the
`env` of the one `gh` or `git` child (`GH_TOKEN` for `gh`); it is never assigned to `process.env`, so no later child — a
test's box, a coder's `claude -p` — can inherit it. `git` over HTTPS gets it through a one-shot `GIT_ASKPASS` script in an
owned folder, removed after the call — never a `https://x:<token>@` URL, which lands in `.git/config` and in error messages.
The PAT reads PR conversation comments without the Issues permission because the repository is public (verified 2026-11-01);
`Issues: write` is needed only if the broker must post conversation comments — E3.S7a's open point (§9 OP-I), no change now.

**The rulesets, as data (consult D6, C7b, C7c, with C7a's merge setting).** `lib/github-lang.mjs` holds them as JSON, applies
each on the operator's go and reads it back before and after every write:

| Ruleset | Target | Rules | Bypass |
|---|---|---|---|
| `template-frozen` (v1's name kept) | `refs/heads/main` | pull request required; the per-stack `build · test` checks required; conversation resolution; no force push; no deletion. **No "restrict updates"** (it would block every spec PR before the freeze and every dated amendment after it, C7c) and **no linear-history rule** (it would refuse the merge commits that C7a and D7 require; the consult's D6 named linear history, dropped for that reason) | none — the freeze commit and every amendment land through PRs, never by the deploy key |
| `run-main` | `refs/heads/run/*/main` | creation, update outside a PR, deletion and force push restricted; the per-stack checks required on PRs | **the deploy key only** (it cuts each run base) |
| the tag ruleset | `refs/tags/spec-frozen-*` | update, deletion and force push restricted for everyone; creation by the repository's admin | none (the deploy key is not a bypass actor here) |

The freeze itself is enforced by the tag, `specBlobMatches` and the amendment rule, not by a frozen `main`. **The tag ruleset
exists and is read back before W5** (C7b).

**Two coder briefs inside this one story, each with its own Opus review (consult A).** Part *conventions snapshot*
(`lib/conventions-snapshot.mjs`, `test/conventions-snapshot.test.mjs`, the private snapshot) needs no GitHub write and runs in
W2; part *GitHub* (templates, CI, the rulesets, `cutRunBase`, `exportRunBase`, `lib/github-lang.mjs`, `p0-lang.mjs`, the live P0
lines) runs after S1's first push, S3 phase A and operator steps O2 and O3.

**The conventions in the process clone — OP-A, option (c), decided by the team lead (not a change).** The run base keeps its
gitlink at `.agents/conventions` and `.gitmodules` as committed; the objects reach the clone from the host, never through the
broker or the network:

1. **Snapshot, once per pinned sha:** `git init --bare ~/rsd/lang-v2/conventions/<sha>.git`, then `git -C <that> fetch
   https://github.com/oleksandrdubyna88/dew_flow_conventions.git <sha>` (on the host, before any session), a ref
   `refs/heads/pinned` at `<sha>`, the sha checked, then `chmod -R a-w` — the snapshot path is read-only from then on.
2. **Seed, per run, before the session starts** (the harness, on the host, in the run's clone): `git submodule init`, then
   `git config submodule.<name>.url file://<snapshot>` where `<name>` is the `.gitmodules` name of the path `.agents/conventions`
   (S1 names it `.agents/conventions`, so the key is `submodule..agents/conventions.url`), then
   `git -c protocol.file.allow=always submodule update --init` — so `.git/modules/<name>` holds the objects and the work tree is
   at the pinned sha.
3. **In the box** the agent has no egress and the snapshot is not bound; the submodule is already checked out, so nothing
   needs fetching, and `.gitmodules` and the tree are unchanged (`git status --porcelain` empty).

`lib/conventions-snapshot.mjs` (`makeConventionsSnapshot({ sha })`, `seedConventions({ clone, snapshot })`) is built here and
called by E3.S7's process runner; E3.S7a's broker never serves it.

**Scenarios.** *Given* the frozen tag, *when* a run base is cut, *then* `git rev-list run/<rid>/main` has exactly one commit, its
tree equals the generated tree, and the needle scan over the tree and that one commit finds nothing; **failure case:** a planted
credit refuses the cut. *Given* the export, *then* the bundle lists one commit and no other object reachable from the task
repository's `main`. *Given* the PAT, *when* it pushes directly to `run/<rid>/main`, *then* the ruleset refuses (read back).
*Given* a PR into a run base, *then* CI runs on the non-default base with the stack's job required; a planted lint or analyzer
violation is red per stack; merge is blocked until threads resolve. *Given* the template tree, *then* no `github.com` URL, other
repository name or remote appears in it. *Given* a clone of a run base seeded by `seedConventions`, *when* `git submodule status`
runs **with no network** (under `unshare -rn`), *then* it shows the pinned sha with no `-` or `+` prefix and `git status
--porcelain` is empty; **failure cases:** a snapshot whose sha differs from the gitlink is refused before the seed; a writable
snapshot path is refused. **Added 2026-11-01:** *given* `cutRunBase`, *then* the push goes to `ssh.github.com:443` with the
pinned host key and `IdentitiesOnly`, and a host key off the pin is refused (D1); *given* any `githubLang` call, *then*
`process.env.GH_TOKEN` is undefined afterwards and no `.git/config` holds a token (D3); *given* the template tree, *then*
`.gitmodules` passes the host check by its path while a URL planted anywhere else fails (D5); *given* the rulesets' data,
*then* `main`'s ruleset restricts no update and requires no linear history, `run-main`'s only bypass actor is the deploy key,
and each applied ruleset reads back equal to its data (D6, C7c).

**Acceptance (parent S7, OP-8 as its text).** v1's P0 checks pass here — a run base cut by the deploy key, a direct push with the
PAT refused, CI green on a non-default base, a planted violation red per stack, merge blocked until threads resolve — run from
the harness side (the PAT never in a box); S1's run-base check completed with `git rev-list` of the base (one commit, D39); the
export holds one commit only; the template tree carries no direct-GitHub assumption; a seeded clone's `git submodule status`
shows the pinned sha offline. Plus (2026-11-01): the run base pushed by the deploy key over `ssh.github.com:443` with the pinned
host key (D1); the PAT in no `process.env` and no remote URL (D3); the three rulesets applied from their data and read back
equal, the tag ruleset before W5 (D6, C7b, C7c); each part reviewed on its own.

**Test plan (red, green, red).** `A_run_base_with_history_would_carry_the_attribution_commit_and_that_is_the_point` (red with a
normal branch from the tag, green with the orphan commit, red with `commit-tree -p` restored);
`The_export_holds_exactly_the_run_base_commit`; `The_template_tree_names_no_github_host_or_other_repository` beside its
companion that still finds a planted URL; `A_clone_without_the_seed_would_have_no_conventions_offline_and_that_is_the_point`
(red: `git submodule status` shows `-<sha>` and `git submodule update` fails under `unshare -rn`; green after `seedConventions`;
red again with the URL rewrite removed, the update then reaching for GitHub and failing). The GitHub checks are live, recorded
as dated P0 lines. Added 2026-11-01: `The_PAT_never_lands_in_process_env` (D3: red with a module that sets `process.env.GH_TOKEN`
once, green with the per-call child `env`, red again) beside `The_PAT_reaches_git_only_through_a_one_shot_askpass`;
`The_deploy_key_push_uses_port_443_and_the_pinned_host_key` (D1, argv and env asserted, no network);
`A_main_ruleset_that_restricts_updates_would_block_every_spec_PR_and_that_is_the_point` (C7c, D6, on the data);
`The_rulesets_are_read_back_equal_to_their_data` (live, a P0 line). `The_template_tree_names_no_github_host_or_other_repository`
excludes `.gitmodules` by path, and its companion still finds a URL planted elsewhere (D5).

**Docs.** The task repository's `README.md` (CI, the run-base namespace); bench `research/module_lang_bench.md`.

**Brief (Sonnet coder).** Follow v1 E2.S4's steps 2–5 with these changes: one repository, two stack folders; path filters cover
the shared paths or the job runs unfiltered (`common.platform-limits` §2); the deploy key's private half mode 600 under
`~/.config/lang-v2/`; every GitHub write listed for the operator's go before it is made; the PAT read from a mode-600 file, never
an argv or a long-lived environment variable; nothing of the broker is built here. *(2026-11-01:)* two briefs, as above — part
*conventions snapshot* first (W2), part *GitHub* after its preconditions; part *GitHub* builds *The deploy key's road*, *The
PAT* and *The rulesets, as data* exactly as written (the key exists, `~/.config/lang-v2/deploy_key_lang_v2`; the PAT exists,
`~/.config/lang-v2/github-pat`).

---

## 8. Growth: what E1's stores weigh and who retires them (`common.planning-docs`, parent §13)

Volumes: 10 tasks × 2 stacks; one draft reference per task and stack; the D40 gate run at least once per task and again for
every task gated on an older runner or layer (budgeted at ≤ 5 rounds per task). **Evidence stores are kept; scratch is the run's
own and is removed by it, then swept by owner and age under `common.test-leftovers` §4.**

| Store (story) | Projected size | Retired by | Interrupted |
|---|---|---|---|
| Toolchains `~/rsd/lang-v2/toolchains/` (E1.S5) | ≈ 1.2–1.5 GB (SDK ≈ 1 GB, Node ≈ 100 MB, TypeScript tens of MB); parent §13 puts toolchains, the CLI copy and the two snapshots at ≈ 3 GB | **kept for the series** (evidence: checksums pre-registered); `retire.mjs` after the record is promoted (parent §13) | an install that dies leaves an owned scratch folder, swept; a half-written toolchain directory is never registered (the checksum check refuses it) |
| Cache snapshots `~/rsd/lang-v2/caches/<stack>-<treeSha256>/` (E1.S4) | two registered, ≈ 0.5–1 GB together (parent §13); each recipe change before the pre-registration adds one superseded snapshot of ≈ 0.25–0.5 GB | the **registered** snapshot of each stack is kept for the series (its hash is pre-registered); a **superseded** one is kept until the pre-registration tag, then removed by `make-snapshots.mjs --prune-superseded` (by its tree hash, never by prefix, never the registered one); `retire.mjs` at the end | an unregistered snapshot directory is scratch: owned name, removed by the run, swept after 10 min when its owner is gone |
| Conventions snapshot `~/rsd/lang-v2/conventions/<sha>.git` (E1.S7) | a few MB per pinned sha; one or two shas (provisional, then E0's release) | kept for the series (the run bases' conventions come from it); `retire.mjs` | an unfinished snapshot is not chmodded read-only and is refused by `seedConventions`; it is re-made |
| Draft references `~/rsd/lang-v2/refs/<NN-slug>/{csharp,node}/` (E1.S2a) | sources only, < 5 MB for all 20 (parent §13: "MBs") | **kept** (the operator's record; E2.S1 finalises them; published after the series, E6) — never in git | — |
| Gate-run scratch: pristine copies with `bin/`, `obj/`, `node_modules/` (E1.S2a, on S8b's layer) | ≈ 0.1–0.3 GB per reference copy; one run at a time, so ≤ 0.6 GB live | **removed by the run** that made it (owned folder, `finally`); never kept | swept at the next run's start by owner and age (`common.test-leftovers` §4); scopes and namespace holders end with the case's cleanup, a lingering one is listed by name |
| Gate records in `spikes/lang-bench/inputs/references.json` and `inputs/specs.json` (E1.S2a, E1.S2) | ≈ 20 lines per gate round × ≤ 5 rounds = ≤ 100 lines, KB | **kept in git** (append only; a stale record stays, marked `stale`) | a tool that dies writes nothing: the record is written beside the file and moved over (`common.reliability` § *Paid work*) |
| Execution-layer probes `inputs/exec-probe-<date>.json` (E1.S8b) | KB each, one per probe day | kept in git | — |
| Spec briefs `~/rsd/lang-v2/test-design/spec-briefs/NN.md` (E1.S2) | KB each, ten | kept with the test-design aid (parent §13) | — |
| **Added 2026-11-01.** Per-case views of the built tree (E1.S8b, consult B7) | one built tree per live case (≈ 0.1–0.3 GB with `bin/`, `obj/`, `node_modules/`); cases run one at a time, so ≤ 0.3 GB live | **removed by the case's cleanup** (step 5); never kept | owned name; swept by owner and age like the gate-run scratch |
| The wrapper's copies `<out-dir>` per case (E1.S8b, consult B2) | three small files, KB per case | the case's evidence goes into the runner's JSON result; the folder is removed with the case | owned name, swept |
| The gate lock `~/.cache/lang-v2/gate.lock` (§3) | one file, bytes | removed by its holder in `finally`; a lock whose owner is gone is removed by the next taker, named | a crash leaves it with a dead owner: the next taker's check ends it |
| The writers' files `inputs/specs.d/*.json`, `inputs/specs.json`, `inputs/credits-lang.json`, `inputs/checkpoints.md` (§3, §4, §5) | KB | **kept in git** (append-only where they record a gate, a checkpoint or a freeze) | `specs.json` is regenerated, never half-written (written beside and moved over) |
| Unclear lists `~/rsd/lang-v2/refs/<NN-slug>/<stack>/UNCLEAR.md` (E1.S2a, consult C4) | KB each, twenty | kept with the references | — |
| `~/.config/lang-v2/known_hosts_github` (operator step O2) | bytes | kept while the deploy key is | — |

## 9. Open points

Classified under `common.plan-change-control`. **Epoch 4 has 0 changes; one recorded change is allowed, a second stops the
plan.** Nothing below is decided here. Line numbers are of the parent at `0ea0e25`. *(Superseded 2026-11-01: epoch 4 now
holds change 4.1 — the known-task needle contract, parent main `d47a346` — so a second change would stop the plan. Nothing in
this revision is a change; what would need one is §12. OP-H … OP-K below cite the parent at `d47a346`.)*

| # | Where | The point | Class |
|---|---|---|---|
| OP-A | `:240` (D42: the per-run bare repository holds "only the orphan run-base commit and the run's own refs"), `:225` (D11: the process clone "carries the conventions"), `:542` (§5: `.agents/conventions` pinned as a submodule on a snapshot branch) | **The conventions submodule under D42.** The run base carries a gitlink, not files; with no direct GitHub egress and nothing but the run base in the bare repository, the process agent cannot fetch `.agents/conventions` — and without it the rules, hooks and `pin-check` the process arm measures are absent. Options: (a) the per-run bare repository also carries the pinned conventions commit, the session's gitconfig redirecting the submodule URL to the broker — D42's allowed contents widened; (b) the generated run-base tree vendors the conventions files in place of the gitlink — D39's tree and the pin check change; (c) the harness checks the submodule out into the clone from a host-side read-only snapshot before the session starts, so the agent never fetches it — D42 and D11 as written, one more harness step in E3.S7. | **decided by the team lead, 2026-10-31: option (c), not a change** (it satisfies D11 and D42 as written); built in E1.S7 (`seedConventions`), called by E3.S7 |
| OP-B | `:942` (T-L32: "in the agent box and the scoring box"), `:1200` (S8b accepted when T-L32 green) | S8b proves T-L32 for the box it launches (the scoring side); the agent box is v1's sandbox, which gets D15's environment in E3.S5 — so T-L32's agent-box half closes in E3, as E3's done-when already lists T-L32. | not a change (reading) |
| OP-C | `:941` (T-L36), §6.1 (`:610`) | T-L36 is assigned to no story. Read as: the D40-gate half is E1.S2a's (`gate-staleness.mjs`), the registered-limit half E2.S3's. | not a change (a test placed inside the stories) |
| OP-D | `:1161-1165` (E1.S7: "the PAT"; "a direct push with the PAT refused") against `:240` (D42: the PAT is used by the broker host-side) | Under D42 the PAT never enters a box; S7's P0 checks run from the harness side, which is what they test. | not a change (reading) |
| OP-E | `:1091` (E1 scope lists D2 … D31 only) | E1 also builds D41–D43's parts (the layer, the template alignment, the D41 rule); the scope line was not widened when S8b was added. | not a change (wording) |
| OP-F | §4 here; the remote `main` = `ed68ce1` against the local `main` = `28bf777` | The two histories are unrelated; the first push needs `git merge --allow-unrelated-histories` on a branch, never a force push. | not a change (procedure) |
| OP-G | `:29-34` (status: the credit terms of the known tasks' sources still open) | A known-task spec cannot be pushed until its credit terms are recorded (§4 step 3); the work on it proceeds locally. | not a change (operator action) — **answered by the owner 2026-11-01** (§4); the parent records Q1's answer at its next change (§12) |
| OP-H | §4 operator step O1 against `common.pull-requests` rule 1 ("merged by rebase … or by squash … Merge commits are off"), which the task repository mounts through its conventions pin and the process agent reads | The team lead accepted merge commits only for the task repository (consult C7a, D7). The shared rule says the opposite for the family's repositories. Options for the team lead before O1's write: (a) merge commits only, with the deviation named in the task repository's README and a note to the conventions owner — the process agent's rules then disagree with the repository's setting; (b) allow merge commits beside rebase and squash (the join and the freeze need merge commits; C7a's sha comparison already guards the tag); (c) a `local.*` rule in the task repository that narrows rule 1 for it — it would enter every run base and change what the process agent is told. | not a change to the parent (no parent decision names the merge method); a conflict with a shared rule, for the team lead |
| OP-I | parent D42 (`:253`: the broker "creates and updates the run's PR and reads its checks and comments") | Consult D4, **adjusted by the team lead:** the public repository lets the PAT read conversation comments without the Issues permission (verified 2026-11-01). `Issues: write` is needed only if the broker must **post** conversation comments; E3.S7a decides that when it designs the broker's allowed operations. No change to the PAT now. | not a change (E3.S7a's open point) |
| OP-J | §4's credit terms; D39 (`:251`: the run base is "the frozen tag's tree minus the README's attribution section and minus any credit file") | Specs 01 and 02 are CC BY-NC-SA 4.0 by the README's per-file exception. Every run base is pushed to this public repository as `run/<rid>/main` without the attribution section (D39), so those branches carry the two files without their notice; the repository's `main` README carries it. Whether that satisfies the licence's attribution term for the run branches is the owner's to confirm before the first run base is cut (E1.S7 part *GitHub*); if not, the cut keeps one neutral licence line that names no source (D19's rule) — which the team lead would then weigh against D39. | not a change yet; the owner's question |
| OP-K | parent D15 (`:242`) | Consult B8: .NET may size its GC hard limit to about 75 % of a detected container limit, so the two stacks could meet one `MemoryMax` with different effective bounds and verdict labels. E1.S8b's probe records what each runtime believes its limit is; **whether to set `DOTNET_GCHeapHardLimitPercent` is D15's decision, made by the team lead on that record before E2 calibrates.** | not a change here; a D15 decision for the team lead |

**Read together:** none of these is a change; OP-A was resolved as (c). Epoch 4 stays at 0 changes. *(2026-11-01: epoch 4 holds
4.1 from another cause; OP-H … OP-K add none, and §12 lists what would.)*

## 10. Risk items (for the gate's `riskItems`)

1. **E1.S8a + E1.S8b — the runner and the execution layer.** Together they are the public contract and the environment every
   verdict of the series comes from — the agent (through `GRAMMAR.md`), CI, the D40 gate, E2's calibration and E3's scoring;
   a comparison, lifecycle, isolation or memory defect misjudges sessions that cost money, possibly in one stack only. Unmarked
   only because the cornerstone ledger is full.
2. **E1.S2 — the specs and their freeze.** Irreversible in the series' terms: after the tag a spec changes only by an amendment
   that voids the task's limits (G-17), and after the pre-registration never; an ambiguous or biased requirement makes every
   later failure unadjudicable. The cornerstone.
3. **E1.S1 / E1.S7 — the public pushes and the run bases.** Publication cannot be taken back (D2, D31, D41): a credit, an aid
   fragment, a reference or a secret in any pushed commit is public at once; the PAT and the deploy key carry write access.

**The consult's verdicts, 2026-11-01:** item 1 (E1.S8a with E1.S8b) **go with fixes** — the fixes are brief text, not a
redesign, but without B1–B4 the runner and the layer go green on stubs and every daemon and web verdict of a data run is
wrong; item 2 (E1.S2, E1.S2a) **go with fixes** (C1–C7); item 3 **E1.S1 go with fixes; E1.S7's GitHub part go with fixes behind
D1's precondition**, which is met (§4). Every fix is written into its story above.

## 11. Definition of Done (E1)

- [ ] The parent's E1 done-when holds (§1), with `BENCH_REQUIRE_SANDBOX=1` and nothing skipped.
- [ ] Every story's red, green and second red are in its summary with the messages seen.
- [ ] The runner is pinned and the execution layer probed and identified in `inputs/specs.json` before FORMAT v2's gate and the
      first D40 gate; FORMAT v2 gated before the first spec gate; every spec's samples existed before its conformance gate;
      every spec passed the D40 gate on the current runner and layer before its document gate (no record `stale`).
- [ ] The ten specs gated, frozen under one tag, their hashes, R-ids, T and G in `inputs/specs.json`.
- [ ] No trap content, held-out case, reference, mutant or riskiest scenario in either repository or in any PR, commit or review
      body (D41; `leakcheck` with the aid's shingles; the hash test).
- [ ] Every push went through `push-check`, one branch per push; tags alone.
- [x] OP-A decided by the team lead, 2026-10-31: option (c) — the harness checks the conventions submodule out into the run's clone from a local snapshot of the pinned sha before the session starts; it fits D11 (the clone carries the conventions) and D42 (the bare repository holds only the run base and the run's refs) as written, so it is NOT a change and no ledger entry is written; E1.S7 (run bases) and E3.S7a (the broker) build it.
- [ ] `research/module_tests.md`, `research/module_lang_bench.md` and `research/architecture.md` describe what E1 built; the task
      repository's README, `todo/README.md` and research stubs match its tree.
- [ ] `node .agents/conventions/tools/plan-lifecycle.mjs .` clean.
- [ ] *(Added 2026-11-01.)* CP1 … CP4 recorded in `inputs/checkpoints.md`, each before what it blocks (§5).
- [ ] E1.S8a and E1.S7 each built as two coder briefs with two reviews; the epic still twelve stories.
- [ ] `inputs/specs.json` equals the assembly of the four writers' files; no story wrote another's file (§3).
- [ ] Every D40 gate run held the gate lock and recorded its load; no flagged result stood without its re-run (§3).
- [ ] Operator steps O1 … O3 done on the operator's go and read back; OP-H decided by the team lead before O1; OP-J answered by
      the owner before the first run base.
- [ ] `inputs/credits-lang.json` holds the owner's terms of 2026-11-01 and the README's attribution section matches it.
- [ ] Every consult fix written into a story (B1–B10, C1–C7, D1–D3, D5–D7) has its named test green or its live P0 line
      recorded; B8's probe line recorded for D15's decision.

## 12. Parent changes this revision would need (not applied)

Added 2026-11-01. **This revision changes no decision of the parent:** epoch 4 already holds change 4.1, and a second change
would stop the plan for a whole re-review (`common.plan-change-control`). Each point below could be made only in the parent's
text, so it is listed here and not applied. Lines are of the parent at main `d47a346`.

1. **Q1's answer is to be recorded at the parent's next change.** Parent `:1034` (`- **Q1 Licences and credit.** …`: "v2's
   credit text is written after these are read") and the status's `:27-29` ("OPEN: the credit terms of the known tasks'
   sources (the rest of Q1); the fine-grained PAT and the deploy key for the process arm (E1.S7)"). Proposed, under Q1:
   "**Answered by the owner, 2026-11-01** (E1 stories §4): specs 01 and 02 are CC BY-NC-SA 4.0, the CSES problem set's licence,
   by a per-file exception the README states; spec 09 is written wholly in our own words, credited "based on IOI 2018
   Werewolf"; spec 06 is MIT, credited with RealWorld's MIT notice; spec 05 describes the protocol subset in our own words and
   never takes the product's name; the terms are the bench's `inputs/credits-lang.json`." In the status, the OPEN clause moves
   to DONE: "the credit terms (the rest of Q1), the fine-grained PAT and the deploy key, 2026-11-01". The parent's own status
   records operator answers as status, not as changes.
2. **(Only if the team lead drops the assembled record.)** D37 `:249` ("with its hash in `inputs/specs.json`") and `:312`,
   `:329`, `:561`, `:1125`, `:1156`, `:1191`, `:1200`, `:1207`, `:1219`, `:1241`, `:1278` name `inputs/specs.json` as the place
   of the runner's hash, the layer's identity, FORMAT's sha256 and the specs' rows. §3 keeps that true: one file per writer,
   with `inputs/specs.json` generated from them. Per-writer files with no assembled record would need, at each of those lines:
   "in the bench's `inputs/specs.d/<writer>.json` (assembled as `inputs/specs.json`)".
3. **D43 `:254` ("the program-only systemd scope (D36)"), §6.1 `:617` and the E1 epic's S8b `:1210`.** With consult B2 the
   scope also holds the layer's wrapper, a POSIX `sh` script whose baseline `memory.peak` is measured and recorded (E1.S8b).
   Proposed: "the program's systemd scope (the program and the layer's measured wrapper only)". D36 `:248` stays true as it
   is (the driver and the build are outside the scope). CP2 may instead take a delegated scope (`-p Delegate=yes`), with a
   holder in one leaf cgroup and the program alone in a child cgroup whose files outlast its exit; that keeps "program-only"
   as written and needs no parent wording.
4. **D38 `:250` and T-L31 `:951` ("the pid the harness starts is the program's").** Under D43's layered launch the harness
   starts `systemd-run`, and the program is the process `run.sh` execs into, behind `nsenter` and `bwrap`. Proposed: "the
   process `run.sh` becomes is the program (no bash parent in the scope); the runner signals that pid, found in the scope's
   `cgroup.procs` (E1.S8a, consult B1)". A wording repair with no change of intent; listed because this document never edits
   the parent.
5. **Consult C4's optional part — one of the two draft references written by Opus.** D40 `:255` ("by the team lead with
   Sonnet coders per the role table, one per stack per task"). Proposed: "… with Sonnet coders per the role table — or, where
   the task's ambiguity list is long, one of the two by an Opus coder, recorded — one per stack per task". This would be a
   change (the approach) and so the full stop; it also departs from `common.subagent-models`' coder row, a policy question
   for the conventions. Not applied; C4's required part (the unclear lists and the item-by-item consultant question) is in
   E1.S2 and E1.S2a.
