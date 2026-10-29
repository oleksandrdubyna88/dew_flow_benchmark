# PLAN — E2 stories: isolation and the treatment of the minigit stack bench (process fixes, the agent sandbox, coai's box and bridge, the templates)

> Status: **plan only, nothing implemented yet, 2026-10-29.** Scope: epic E2 of
> [PLAN_minigit_stack_bench.md](PLAN_minigit_stack_bench.md) (§15 steps 1, 4–6a) — fixes in `spikes/policy-ab-quick/`, new
> modules in `spikes/minigit-bench/`, the fork `oleksandrdubyna88/minigit-bench` (template branches, rulesets, settings) and
> one snapshot branch plus a short boundary plan in `dew_flow_conventions`. Four stories, E2.S1–E2.S4; cornerstone **E2.S2**
> (`model: fable`, the agent sandbox and launch — its text below is an Opus DRAFT for Fable to rewrite before any code); the
> parent's ledger reads `Cornerstones: 2 of 2 — E1.S3, E2.S2`. Money spent here, all pilot spend: the E2.S2 design run (2026-10-29,
> $0.112), the broker proof ($0.018) and E2.S2's bare-session smoke (≤ $0.20); E2.S3's live check spends vendor quota, not the ceiling.

## 1. Goal, before any solution

A session of the series must be **unable** to reach what would change its result — the operator's files, the held-out suite,
the partner cell, a Windows process, a host service that runs commands for it — and where prevention is impossible (the
network is shared, the fork is public) every reach must be **recorded** and scanned. What each arm and stack was GIVEN must be
pinned and proven equal except where the treatment says it differs. The parent's §7 lists the barriers; E1 built the scoring
box and proved its effect on this machine. E2 builds the agent's box, coai's box and the bridge between them, fixes the three
defects the old spike carries into the new one, and pins the treatment (conventions snapshot, one-source templates, GitHub).

### Bare first (operator decision, 2026-10-29)

The bare arm runs before the process arm is built, so that the first C# against TS numbers exist in about 6–8 h. So the order
of this document is the order of use:

| Story | Serves | Depends on |
|---|---|---|
| **E2.S1** — process fixes and the detection half | the bare runner and everything after | nothing in E2 |
| **E2.S2** — the agent sandbox and the launch (cornerstone) | the bare runner and everything after | E2.S1 (group kill, audit hook); E1 (receiver, counters, transcripts copy) |
| E2.S3 — coai in its own box, the bridge | the process arm only | E2.S2 (the agent box it plugs into) |
| E2.S4 — the treatment pinned: conventions snapshot, templates, GitHub | the process arm only | E2.S2 (the session layout and the secrets reader) |

**E2.S1 and E2.S2 together are the prerequisite of a thin bare-arm runner** (prepare / run / collect / score / summarize for
the bare arm, pairs C# ∥ TS started together, `taskset` masks, the $6 cap, voids), which is pulled forward from E3.S1 as a
separate small epic or story set and is **not written here**. Neither S1 nor S2 may depend on the bridge, the templates or
GitHub. E2.S3 and E2.S4 follow afterwards, for the process arm.

**Public repository, networked sessions (parent §5.1).** Nothing here names a held-out case, reference or mutant.

## 2. What exists — verified at `0085d44`, 2026-10-29, on the series machine

| Fact | Where |
|---|---|
| `run()` spawns without `detached`; `killTree` sends `SIGKILL` to ONE pid on Linux, so a grandchild survives and, holding the stdout pipe, keeps `run()` from resolving | `spikes/policy-ab-quick/lib/proc.mjs:8`, `:11`, `:32-36` (`:35` the kill) |
| `npmInvocation` falls back to `cmd.exe /d /s /c npm.cmd …` — the only route from spike code into a shell interpreter (a scan of every non-test `.mjs` of both spikes for `cmd.exe`, `shell: true`, `exec`/`execSync`, `sh|bash -c` found this one site) | `proc.mjs:53-57` (`:56`) |
| Code-scanning alert **#8** `js/shell-command-injection-from-environment`, open, at `proc.mjs:8`; its flows run from `consts.mjs:7` (`REPO_ROOT`) through `harness.mjs:10-15` and `git.mjs:16` into `run()`; related location `proc.mjs:54` | `gh api …/code-scanning/alerts/8`, SARIF of analysis 2057887679 |
| Alert **#9**, the same rule at `spikes/minigit-bench/lib/run.mjs:46` (a launcher with NO shell route), was dismissed `false positive` **with an empty comment** | `gh api …/code-scanning/alerts?state=dismissed` |
| Two more kill helpers already kill the GROUP: `killGroup` (`-pid`, then `pid`) and the capture's `killTree(child, { kill, log })` (`-pid`); both spawn `detached: true` | `spikes/minigit-bench/lib/run.mjs:27-32`, `:46`; `spikes/minigit-bench/capture/capture-run.mjs:18-27`, `:131` |
| `killTree` callers: `proc.mjs:11`; `run-cell.mjs:61`, `:67` over a spawn at `run-cell.mjs:56` that is not detached | `spikes/policy-ab-quick/run-cell.mjs` |
| The audit hook records `ts, event, agent_id, agent_type, tool_name, effort, …, input` — no `tool_use_id`, no duration, and for Bash only `command`; it writes to `<repo>/.git/audit.ndjson`, no `--out` | `spikes/policy-ab-quick/hooks/audit.mjs:13-32` (Bash `:16-17`), `:35-52`, `:82-90` |
| E1's span builder already expects `{ ts, event, tool_use_id, tool_name, agent_id, input }` and reads `input.run_in_background === true` as the `BACKGROUND` category | `spikes/minigit-bench/lib/spans.mjs:9`, `:54-89`; `lib/classify.mjs:147-157` (`:156`) |
| `env.mjs` keeps DROP/KEEP and the fixed git block private; `buildClaudeArgs` passes **`--no-session-persistence`** (the new spike must not use it: transcripts are the third view) | `spikes/policy-ab-quick/lib/env.mjs:4-8`, `:22-39` (git `:33-37`), `:42-49` (`:46`) |
| `leak.mjs` resolves with `path.win32` only | `spikes/policy-ab-quick/lib/leak.mjs:4`, `:19-24`, `:46-52` |
| Token and canary scans, canary planting with a manifest first | `spikes/policy-ab-quick/lib/scan.mjs:5` (`scanTokens`); `lib/canaries.mjs:11-15`, `:23-35` |
| The pinned effort block | `spikes/policy-ab-quick/lib/settings.mjs:27-30` (`PINNED_EFFORT`) |
| `policy-ab-quick`'s suite on this Linux machine: 122 tests, **4 fail before any change** — all Windows literals in fixtures (`D:/x/y.json`, a `;` PATH, `D:\` roots): `spike.test.mjs:62`, `review2.test.mjs:300`, `:353`, `review4.test.mjs:149` | run 2026-10-29: `node .agents/conventions/tools/low-priority.mjs node --test --test-concurrency=4 spikes/policy-ab-quick/test/` |
| E1's box primitives: `roRoot`, `tmpfs`, `bind`, `roBind`, `devProc`, `isolation` (always `--unshare-net`), `setenvAll` (values on **argv**), the hide list `[home, /mnt, /run, /tmp, /var/tmp]` inside `scoreBoxArgv` | `spikes/minigit-bench/lib/bwrap.mjs:10-19`, `:15`, `:51-66` (`:52`) |
| E1's effect tests with controls (`probe`, `withoutPair`, `listenOn`), run on this machine | `spikes/minigit-bench/test/bwrap.test.mjs:15-44`, `:113-228` |
| **`/etc/resolv.conf` → `/mnt/wsl/resolv.conf`.** A box that hides `/mnt` has no DNS. It is the only `/etc` link (depth ≤ 3) into a hidden prefix | `readlink -f`, `find /etc -type l`, 2026-10-29 |
| Listening on the shared network namespace: 1 abstract unix socket (`@…`, another user's), and loopback TCP `127.0.0.1:35841`, `:38261`, `:45584` plus DNS stubs | `ss -xl`, `ss -tlnH`, 2026-10-29 |
| WSL interop: `WSL_INTEROP=/run/WSL/641_interop` in the operator's shell; `/run/WSL/*_interop` sockets; 20+ `/mnt/c/…` PATH entries | `env`, `ls /run/WSL`, 2026-10-29 |
| Toolchains: `claude` 2.1.310 = `~/.local/share/claude/versions/2.1.310` (one file); node v26.12.1 under `~/.nvm/versions/node/v26.12.1`; dotnet SDK 10.0.113 only (`/usr/lib/dotnet`); `/usr/bin/gh` 2.103.2; `/usr/bin/make`; bwrap 0.11.1; **no `tsc` installed** | checked 2026-10-29 |
| The token reader: a mode-600 file in a mode-700 dir, never the environment | `spikes/minigit-bench/capture/capture-env.mjs:8`, `:15-25`, `:28-34` |
| The capture's launch: allow-list env, argv, settings with `permissions.allow`, the subagent tool's two names, the transcripts walk/copy | `capture/capture-plan.mjs:12`, `:22`, `:29-38`, `:43-49`, `:52-57`; `capture/capture-run.mjs:34-66` |
| Headless facts of CLI 2.1.310 (capture fixture): init lists `Task` while `--tools Agent` is accepted; `query_source` `sdk` / `agent:builtin:general-purpose`; `-p` denies every tool not in `permissions.allow`; `messaging_socket_path` under `/tmp`; `apiKeySource: none` with the OAuth token | `test/fixtures/real/claude-2.1.310-capture/stream.ndjson` line 1, `CAPTURE.md`; E1 amendment 2026-10-29 |
| The receiver binds `127.0.0.1`, keeps a live `cost_usd` sum | `spikes/minigit-bench/lib/otel-receiver.mjs:14`, `:78-99` |
| The pinned conventions settings allow only `Edit, Write, NotebookEdit, Bash, PowerShell` and wire `SessionStart`, `PreToolUse` | `.agents/conventions/settings/settings.json` |
| coai is launched as `~/.vscode-server/data/User/globalStorage/remsoftdev.connect-other-ais/coai-mcp` (0.63.0, beside six `.so` files) with `COAI_DATA_DIR=/mnt/v/connectOtherAis`, `COAI_DATA_SIDE=wsl-ubuntu` — **its data lives on a Windows drive under `/mnt`** | `~/.claude.json` `mcpServers.coai`; `coai-mcp --version` |
| coai's settings path is `<COAI_DATA_DIR>/<side>/settings.json` | `dew_flow_connect_other_ais · src_mcp/src/Server/SettingsFile.cs:168-188` |
| The operator's coai settings today: five reviewer rows — `codex`, `antigravity` (plan, code, document) and `codex-3`, `antigravity-2`, `vibe` (feature only); question-consultant rows on `codex-2`, `antigravity-2`, `vibe`; `COAI_QCONSULT_ROOTS` **set** to two roots; `COAI_ON_EXHAUSTED=good_enough` | `/mnt/v/connectOtherAis/wsl-ubuntu/settings.json` (read for names only) |
| coai tool parameters that carry paths: `repoPath` (every tool), `committedPlan` (review_plan), `documentPath` (review_document), `planPath` (review_feature), `document`/`plan`/`feature` (resolve, status, ask_human, ask_consultants), `suspectedFiles` (consult, a JSON array) | `dew_flow_connect_other_ais · src_mcp/src/Tools.cs:47`, `:83`, `:178-179`, `:238`, `:356`, `:387`, `:492`, `:546` |
| A question prompt's capability is `none` / `disk` / `web`; `disk` reads the listed roots | `dew_flow_connect_other_ais · src_mcp/core/QuestionConsult/Capability.cs:7-17`, `QuestionPromptSet.cs:12`, `QuestionConsultSettings.cs:77` |
| The fork: `main` 7f6ffdc, `inputs` df00cac, four `template/*` (`process-csharp` 0d0bd66, `process-node` bac8a6a, `bare-csharp` 0544d9e, `bare-node` 1116866), ruleset `templates-and-main-frozen-history` (26137146); **and a `data` branch (7a7a7fb)** — upstream's orphan branch of generated implementations, 20 trials × 15 languages **including `minigit-typescript-*-v1/v2`** (no C#) | `gh api repos/oleksandrdubyna88/minigit-bench/{branches,rulesets}`, `git/trees/data:generated` |
| **Each template branch holds `SPEC-v1.txt`, `SPEC-v2.txt` and the UPSTREAM `test-v1.sh`, `test-v2.sh`** (sha256 equal to `hashes.txt`'s `upstream/*` lines), a README, a `.gitignore` | `gh api …/contents/<f>?ref=template/…`, hashed 2026-10-29 |
| Fork settings today: `has_wiki` true, `has_projects` true, merge commits allowed, `delete_branch_on_merge` false | `gh api repos/oleksandrdubyna88/minigit-bench` |
| The conventions `release` is `e29883f` (this repository's pin); its `common/test-leftovers.md` (77 lines) has **no `## Tokens` table** and no owner/repo segment; `main` (2602416, unreleased) has both (`## Tokens` at `:294`) | `git ls-remote`, `gh api …/contents/common/test-leftovers.md?ref=release` |
| Vendor CLIs and where they keep their sign-ins (details in E2.S3) | `~/.codex/auth.json`; `~/.gemini/antigravity-cli/antigravity-oauth-token`; `~/.vibe/.env` |

## 3. Conventions for every story

E1's §3 holds unchanged (plain ESM, Node 26, no dependency, values not throws, absent is not zero, `run-tests.mjs`,
`BENCH_REQUIRE_SANDBOX=1` for the completion run, RED first). Added here:

- **A secret never reaches an argv.** `bwrap --setenv K V` puts V in `/proc/<pid>/cmdline`, which every process on the
  machine can read. Secrets travel in the spawn's `env` object only; `setenvAll` is for non-secret values.
- **One kill implementation**, one hide list, one secrets reader, one stamped-stream writer — each extracted, never copied.
- `policy-ab-quick`'s tests run with `node .agents/conventions/tools/low-priority.mjs node --test --test-concurrency=4
  spikes/policy-ab-quick/test/`; its four pre-existing Linux failures (§2) are not this epic's unless a story touches the
  module under test (E2.S1 touches `env.mjs`, so its fixture is fixed there).
- Leftovers a test creates on disk keep E1's `testsToDelete-mgb-…` names (the pinned rule's form — §2: the release has no
  owner segment).

---

## E2.S1 — process fixes and the detection half: group kill (alert #8), the audit hook, env export, POSIX contamination scan, token scan, canary targets (T-P1, T-P2)

**Model:** scenarios + brief Opus (this text) · code Sonnet · review Opus.

**Goal.** A timed-out or killed launch ends with its whole tree, through ONE kill function, and no spike launcher can reach a
shell; every tool call is recorded with what the time partition needs; the session's records can be scanned for every
contamination signal of parent §7.4–7.6 with POSIX paths. Detection only: what a box prevents is E2.S2.

**Files.**

| Path | Change |
|---|---|
| `spikes/policy-ab-quick/lib/proc.mjs` | `run` spawns `detached: process.platform !== 'win32'`; `killTree(pid, { kill, log })` becomes the group kill; `npmInvocation` loses its `cmd.exe` fallback |
| `spikes/policy-ab-quick/run-cell.mjs` | the spawn at `:56` gets the same `detached` so `:61`, `:67` kill a group |
| `spikes/minigit-bench/lib/run.mjs`, `spikes/minigit-bench/capture/capture-run.mjs` | `killGroup` (`:27-32`) and the capture's `killTree` (`:18-27`) delegate to `proc.mjs`'s `killTree`; the capture's exported signature `killTree(child, { kill, log })` stays (its tests at `test/capture-session.test.mjs:387-394`) |
| `spikes/policy-ab-quick/hooks/audit.mjs` | `--out <file>`; `tool_use_id`, `session_id`, `duration_ms`; Bash `run_in_background` |
| `spikes/policy-ab-quick/lib/env.mjs` | export `DROP`, `KEEP`, `scrubEnv`, `FIXED_GIT`; `buildChildEnv` composes them (behaviour unchanged) |
| `spikes/policy-ab-quick/lib/leak.mjs` | a `flavour: 'win32' \| 'posix'` option (default `win32`, so every existing caller is unchanged) |
| `spikes/policy-ab-quick/lib/canaries.mjs` | `canaryTargets` takes an optional `dirs` list (default: today's three) |
| `spikes/minigit-bench/lib/contamination.mjs` | new — `contaminationHits` |
| `spikes/minigit-bench/lib/token-scan.mjs` | new — `tokenHits` |
| `spikes/minigit-bench/lib/canary-plan.mjs` | new — `sessionCanaryTargets` |
| `spikes/policy-ab-quick/test/proc.test.mjs`, `test/audit.test.mjs` | new |
| `spikes/minigit-bench/test/{no-shell,contamination,token-scan,canary-plan,audit-contract}.test.mjs` | new |
| `spikes/policy-ab-quick/test/spike.test.mjs:62` | the PATH fixture is built with `path.delimiter` (it fails on Linux today for that reason alone) |

**The change — what and how.**

1. **One group kill.** `killTree(pid, { kill = process.kill.bind(process), log = () => {} } = {})`: no pid → return; Windows →
   `taskkill /PID <pid> /T /F` as today; POSIX → `kill(-pid, 'SIGKILL')`, then `kill(pid, 'SIGKILL')` (a pid that is not a
   group leader has no group: ESRCH on the first is expected), each ESRCH swallowed, any other code reported through `log`,
   never thrown (timers call it). Every spawn whose pid reaches `killTree` is detached: `proc.mjs:8`, `run-cell.mjs:56`
   (`minigit-bench/lib/run.mjs:46` and `capture-run.mjs:131` already are). `run.mjs`'s `killGroup` and the capture's
   `killTree` call it; `minigit-bench` importing from `policy-ab-quick/lib` is the allowed direction (E1 §3).
2. **No shell route (alert #8).** `npmInvocation(args)`: when `npm-cli.js` is not beside the running node, **throw**
   `Error("npm-cli.js not found at <path>: run with a node that ships npm")` — an unexpected environment, not an expected
   outcome (PROJECT §5); the `cmd.exe` branch and its comment go. Callers (`prepare-cell.mjs:209`, `tree.mjs:44`, `:55`,
   `commands.mjs:15`) are unchanged.
3. **The alert itself.** After the merge, read the analysis of the merge commit: `gh api
   repos/{owner}/{repo}/code-scanning/alerts/8 --jq .state`. `fixed` → done. Still `open` → it is the class of #9 (CodeQL
   flags a `spawn(exe, args, { shell: false })` whose args derive from an absolute path), and it is dismissed
   `false positive` WITH a comment naming the code and the control: *"exe + argv with `shell: false` (`proc.mjs:8`); no spike
   launcher reaches a shell interpreter — enforced by `test/no-shell.test.mjs`"*. Alert #9 gets the same comment
   (`PATCH …/alerts/9` with `state: dismissed`, `dismissed_reason: "false positive"`, `dismissed_comment`): a dismissal without
   a reason breaks `common.automated-checks`. The code-scanning count goes into the PR description.
4. **Audit hook.** `--out <file>` (`parseArgs`, strict); absent → today's `<repo>/.git/audit.ndjson`. The record adds
   `tool_use_id: str(payload.tool_use_id)`, `session_id: str(payload.session_id)`, `duration_ms: Number.isFinite(payload.duration_ms)
   ? payload.duration_ms : null` (absent is null, never 0 — whether 2.1.310's payload carries it is a verdict of E2.S2's
   smoke), and for `Bash` `input.run_in_background: typeof i.run_in_background === 'boolean' ? i.run_in_background : null`.
   `ts` stays ISO with milliseconds (`spans.mjs`'s `AUDIT_TIME_FIELD`). Every event is recorded as it comes, so `PostToolUse`,
   `PostToolUseFailure`, `SubagentStart`, `SubagentStop` need no code here — E2.S2's settings register them. Still observe-only:
   any failure exits 0.
5. **Env export.** `export const DROP`, `export const KEEP`, `export const scrubEnv = parent => …` (the filter at `:23`),
   `export const FIXED_GIT = Object.freeze({ GIT_CONFIG_NOSYSTEM: '1', GIT_TERMINAL_PROMPT: '0', GIT_AUTHOR_NAME: 'bench', … })`
   (the block at `:34-37`, `GIT_CONFIG_GLOBAL` stays a parameter); `buildChildEnv` = `{ ...scrubEnv, ...filtered PATH, …,
   GIT_CONFIG_GLOBAL, ...FIXED_GIT }`, output byte-identical to today's (the existing tests prove it). `filterPath` is not used
   by the new spike (it would drop `/usr/bin`, parent §4.2).
6. **POSIX paths.** `findOutsideAccesses(calls, { treeRoot, home, flavour = 'win32', allow = [] })`. `posix`: `path.posix`
   resolution; candidates are absolute `/…` paths, `~`, `$HOME`/`${HOME}`, `../` chains (the existing shell/file/search tool
   split); allowed = inside `treeRoot`, or under an `allow` prefix (`/usr/`, `/bin/`, `/etc/`, `/dev/null`, `/proc/self/`, the
   toolchain dirs — passed by the caller, never a default that hides a reach).
7. **`contaminationHits({ calls, mcpResults, ctx })`** — `calls` = `[{ agent, tool, input }]` from the stream's `tool_use`
   blocks (main and forwarded children) and the audit; `mcpResults` = the text of every `tool_result` whose `tool_use` named
   an `mcp__` tool; `ctx = { rid, runId, runIdPattern, ownPrefix: 'run/<rid>/', ownOrigin, ownPrs, otherStack, partnerPaths,
   harnessPaths, treeRoot, home, allow }`. Each hit `{ severity: 'high' | 'low', rule, source, where, excerpt }` (excerpt ≤ 120
   chars, never a credential — `tokenHits` runs first and its spans are masked). Rules, each its own named entry so a test
   iterates them:
   - `foreign-run-ref` (high): `run/<x>/` with `<x> ≠ rid`; `other-run-id` (high): `runIdPattern` match ≠ `runId`;
     `foreign-pr` (high): `pull/<n>` or `gh pr … <n>` with `n ∉ ownPrs` (empty `ownPrs` = every number is foreign; the
     caller passes E3's list);
   - `other-stack-template` (high): `template/<arm>-<otherStack>`; `upstream-solutions` (high): `mame/ai-coding-lang-bench`,
     `banteg/minigit-bench`, and the fork's `data` branch in a git or URL context (`refs/heads/data`, `/tree/data`,
     `fetch|checkout|switch|ls-remote|clone … data`) — §2: it holds upstream's TypeScript solutions;
   - `minigit-elsewhere` (high): `github.com/<owner>/<repo>` or `raw.githubusercontent.com` naming `minigit` other than
     `ownOrigin` restricted to `ownPrefix` refs; `harness-name` (high): `dew_flow_benchmark`;
   - `outside-path` (high when under `partnerPaths`/`harnessPaths`/`home`, low otherwise): `findOutsideAccesses` with
     `flavour: 'posix'`;
   - `web` (low): `WebFetch`/`WebSearch` tools, `curl|wget` to a host outside the registry allow-list
     (ONE source: S1 creates `spikes/minigit-bench/lib/egress-hosts.mjs` exporting `EGRESS_HOSTS` (per arm) and
     `KNOWN_REFUSED`; S2 and this rule import it — plan gate E2, finding on the circular dependency, 2026-10-29).
8. **`tokenHits(texts, ownSecrets)`** — shapes `ghp_[A-Za-z0-9]{36}`, `github_pat_[A-Za-z0-9_]{60,}`, `gh[ousr]_[A-Za-z0-9]{36}`,
   `sk-ant-[A-Za-z0-9_-]{20,}`, plus each `ownSecrets` value (exact, ≥ 8 chars). A hit is `{ source, kind, offset }` — never the
   matched text, not even a prefix (`scanTokens`' canary kind prints six characters, `scan.mjs`; it is not used for secrets).
9. **Canary targets.** `canaryTargets(home, rand, token, slug, dirs)`; `sessionCanaryTargets({ operatorHome, partnerConfigDir,
   existingProjectDirs })` → one canary in each existing `~/.claude/projects/<slug>/` and `…/memory/` (passed in, not walked
   here) and one in the partner's `CLAUDE_CONFIG_DIR/projects/` — planting and unplanting stay `canaries.mjs`'s `plant`/
   `unplant` (manifest first). The canary text names nothing about the bench (`canaries.mjs:30`'s sentence is replaced by a
   neutral one through a parameter).

**Test scenarios.**

| Test | Input | Expected |
|---|---|---|
| `A_timeout_ends_a_grandchild_and_run_resolves` (**T-P1, RED first**) | `run('sh', ['-c', 'sleep 300 & echo $! > <tmp>/gc; wait'], { timeoutMs: 500, env: { …, BENCH_MARK: <rand> } })` raced against a 5 s test deadline | RED on `proc.mjs:35` as it stands: the race reports *"run() did not resolve 5 s after its 0.5 s timeout; grandchild <pid> alive"* (the grandchild holds stdout). GREEN: resolves within 3 s; `/proc/<gc>` gone; no `/proc/*/environ` carries the mark. `finally` kills the recorded pid by pid |
| `A_kill_of_a_group_that_is_gone_is_quiet` | `killTree(4242, { kill: throwing ESRCH })`; then `EPERM` | no throw; ESRCH not logged; EPERM logged once |
| `The_three_spikes_kill_through_one_function` | import graph: `run.mjs`, `capture-run.mjs` | both import `killTree` from `policy-ab-quick/lib/proc.mjs`; no `process.kill(-` elsewhere in non-test code (scan) |
| `No_spike_launcher_can_reach_a_shell_interpreter` (**RED first**) | scan of every non-`test/` `.mjs` of both spikes for a `'cmd.exe'`/`"cmd.exe"` literal, `shell:\s*true`, `exec`/`execSync` imported from `node:child_process`, `spawn('sh'\|'bash'\|'cmd'` with a `-c`/`/c` element | RED today: `proc.mjs:56`. GREEN after step 2: none |
| `The_shell_scan_still_finds_a_planted_route` | the same scan over a temp file holding `spawn('cmd.exe', ['/c', 'x'])` and `{ shell: true }` | two hits — the scan is alive |
| `npm_without_its_cli_script_is_an_error_not_a_shell` | `npmInvocation` with an injected exists() false | throws naming the path; never returns `cmd.exe` |
| `The_audit_record_carries_what_the_partition_pairs_by` | Pre and Post payloads of one Bash call with `tool_use_id`, `session_id`, `run_in_background: true`, Post with `duration_ms: 812` | both records carry `tool_use_id`; Post `duration_ms: 812`; Pre `duration_ms: null`; `input.run_in_background: true` |
| `An_absent_duration_or_flag_is_null_never_zero` | payloads without them | `duration_ms: null`, `input.run_in_background: null` |
| `The_audit_writes_where_it_is_told` | spawn `node hooks/audit.mjs --out <tmp>/a.ndjson` with a payload on stdin | one line in `<tmp>/a.ndjson`; exit 0; nothing written under any `.git` |
| `A_broken_payload_still_exits_zero` | stdin `not json` | exit 0, no line |
| `Audit_records_become_spans_in_E1s_builder` (the two-implementation check) | `auditRecord` output for a Pre/Post pair of `dotnet build` and of a background Bash, fed to `spansFrom({ audit })` | one `build_test` span with that `toolUseId` and the exact ms between the two `ts`; one `BACKGROUND` span and its note; `unpaired` empty |
| `The_env_export_changes_no_child_env` | the existing `buildChildEnv` fixtures | equal output; `FIXED_GIT` keys ⊂ the output |
| `POSIX_paths_outside_the_tree_are_found` (T-P2) | `cat /home/u/.config/x`, `ls ~/`, `cd ../../other`, `Read /srv/partner/box/work/a.cs` with `treeRoot /srv/me/box/work` | four hits with resolved POSIX paths; `node /usr/bin/x` and `/dev/null` with `allow` → none |
| `The_windows_flavour_is_unchanged` | `review.test.mjs`'s existing cases | still green |
| `Each_contamination_rule_flags_its_own_shape` (T-P2) | one call per rule, iterated from the exported rule list: `git fetch origin run/zz9999/main`, a foreign RUN_ID, `gh pr view 7` with `ownPrs [3]`, `template/process-csharp` in a node cell, `git fetch origin data`, `https://github.com/mame/ai-coding-lang-bench`, `raw.githubusercontent.com/x/minigit`, `dew_flow_benchmark`, a partner path, `curl https://example.com` | exactly one hit per call, with that rule and its severity |
| `Own_refs_and_the_registries_are_not_contamination` | `git push origin run/<rid>/feat`, `npm ci`, `dotnet restore`, `gh pr view 3` with `ownPrs [3]` | no hit |
| `A_reviewers_answer_that_names_a_foreign_run_is_a_hit` | `mcpResults` text `see run/zz9999/main` | one `foreign-run-ref`, `source: 'mcp'` |
| `Every_token_shape_is_found_and_never_echoed` | each shape and an own secret embedded in text | one hit each; `JSON.stringify(hits)` contains none of the planted strings nor a 6-char prefix |
| `A_secret_in_an_excerpt_is_masked` | a contamination hit whose line also holds `sk-ant-…` | `excerpt` carries `[secret]`, not the value |
| `Session_canaries_cover_the_operator_projects_and_the_partner` | two project dirs and a partner config dir | 5 targets (2 + 2 memory + 1); none under the session's own box |

**Commands.** `node .agents/conventions/tools/low-priority.mjs node --test --test-concurrency=4 spikes/policy-ab-quick/test/`;
`node spikes/minigit-bench/run-tests.mjs test/no-shell.test.mjs test/contamination.test.mjs test/token-scan.test.mjs
test/canary-plan.test.mjs test/audit-contract.test.mjs`; after the merge, `gh api
repos/{owner}/{repo}/code-scanning/alerts?state=open --jq length`.

**Done when.** T-P1 recorded RED with its message on the unchanged `proc.mjs`, then green; the shell scan RED on `proc.mjs:56`,
then green, with its alive check; T-P2 green; `policy-ab-quick`'s suite has no failure but the three named Windows-fixture
ones (`review2.test.mjs:300`, `:353`, `review4.test.mjs:149`), listed in the PR with a proposal to make them platform-neutral;
alert #8 `fixed` on the merge commit's analysis, or dismissed with the comment above — and #9 carries a comment too; the open
code-scanning count is in the PR description.

---

## E2.S2 — the agent sandbox and the launch: a box with its OWN network namespace, egress through a host-side CONNECT proxy, the credential broker holding the cell's budget, argv, settings, env, watchdogs, a graceful stop, idempotent cleanup, janitor, read-back, the bare-session smoke

**Cornerstone — model: fable. Spec: Fable** (this text replaces Opus's draft of 2026-10-29, after the design was run on this machine;
revised 2026-10-29 after the plan gate's `revise`, with the six accepted findings below and the broker proven by a run before it entered
the text). Area: isolation and treatment fidelity (parent `## Epics`, third-ranked; the one mark of E2). It is the story every bare
session rests on: the first paid cells run inside this box, so a hole here is a hole in every number. Code Sonnet · review Opus.

**Goal.** One function builds the box a session runs in; one builds what is launched inside it; one launches, watches, stops and
collects. Inside: Claude Code with a **dummy** token, effort `high`, an explicit allow-list, OTel to E1's receiver, transcripts
persisted — and no operator home, no `/mnt`, no `/run`, no host `/tmp`, no Windows interop, no `gh` in the bare arm, no partner, no
harness file it was not given, **no network except the hosts the arm names**, reached through one door the harness holds outside the
box, **and no credential**: the subscription token is read by the harness, held by a broker on the host, and never enters the box's
environment, argv, files or process listings. Every API call leaves the box as plain HTTP on its own loopback and is signed on the
host, under the cell's own budget. Each barrier is OBSERVED on this machine with a control, never asserted from the argv
(`common.security` — *a measure you have not observed is a comment*). A bare-session smoke proves the whole path end to end and that
E1's counters read what it leaves.

### The gate's findings and where each lands (2026-10-29)

| # | Finding (reviewer) | Where it lands |
|---|---|---|
| F-1 | the OAuth token in the box's environment is readable by any tool call (codex, blocking) | D-16 broker, D-17 budget, P-1…P-9, tests *The_box_holds_only_the_dummy…* (three) |
| F-2 | `api.anthropic.com` on the CONNECT allow-list lets a compromised session spend outside the budget (codex) | D-3: removed from every arm; D-17 |
| F-3 | unconditional SIGKILL (antigravity, blocking) | D-14: SIGTERM to the inner pids, grace, then `killTree`; P-10…P-12 say what that buys |
| F-4 | the plain-HTTP policy undefined (antigravity) | D-19, test *Plain_HTTP_is_refused_and_the_restore_does_not_need_it* |
| F-5 | `EGRESS_HOSTS` circular between S1 and S2 (antigravity) | S1 creates `lib/egress-hosts.mjs`; S2 imports it (D-3, Files) |
| F-6 | interrupted launch leaves listeners and a session dir (codex) | D-20 cleanup stack, D-21 state file and stale retirement; tests *Every_exit_path…*, *A_stale_session…* |

### The network decision, and what was run to take it (2026-10-29, this machine, CLI 2.1.310)

**Adopted: the agent box gets its own network namespace (`--unshare-net`, the same `isolation()` the scoring box uses) and
egress ONLY through an HTTP `CONNECT` proxy the harness runs on the host, over a unix socket bound into the box.** Inside the box
a small supervisor listens on `127.0.0.1:3128` (proxy), `127.0.0.1:8787` (the API door) and `127.0.0.1:4318` (OTel) and pipes each
to its unix socket; `HTTPS_PROXY`, `HTTP_PROXY` and `NO_PROXY=127.0.0.1,localhost` steer `npm`, `dotnet` and `git`, and
`ANTHROPIC_BASE_URL=http://127.0.0.1:8787` steers the CLI's API calls to the broker. The proxy allows `CONNECT <host>:443` for a
per-arm host list and refuses everything else with `403`, logging every request. A shared network namespace was rejected: a box in
the host's namespace reaches the host's loopback services (three were listening), every abstract unix socket, and any host on the
internet — including GitHub's `data` branches (D21) — and nothing bound or unbound changes that.

Every claim below was observed, not read from a manual. The design run (one bare session, `--max-budget-usd 0.2`, the smoke prompt,
out dir `~/rsd/minigit/smoke/20261029T215923Z-design/`, outside git, kept as evidence) cost **$0.1119485, 13.7 s wall, exit 0,
`num_turns` 3, two models**; it still carried the real token in the box's environment, which is what the broker below removes.
Pilot spend (parent §13).

| # | Claim | How it was checked | Result |
|---|---|---|---|
| N-1 | With `--unshare-net` the box reaches no host loopback service, no abstract unix socket and no internet address | a host abstract listener and `127.0.0.1:35841`, `1.1.1.1:443` probed from inside; the same probes without `--unshare-net` as the control | isolated: `ECONNREFUSED`, `Connection refused`, `Network is unreachable`; control: all three reached |
| N-2 | A unix socket file bound into the box (read-only bind of its directory) is connectable across the namespace boundary | the proxy and OTel sockets lived in `<s>/net`, bound `--ro-bind`; the supervisor connected to both | both connected (the whole paid run went through them) |
| N-3 | `bwrap` brings `lo` up in the new namespace, so an in-box listener on `127.0.0.1` works | `ip link show lo` inside; the supervisor's listeners | `LOOPBACK,UP,LOWER_UP`; the listeners accepted connections |
| N-4 | Claude Code 2.1.310 honours `HTTPS_PROXY` for the API | the design run: the box had no other route; the proxy log | 7 `CONNECT api.anthropic.com:443` allowed; the session completed. **Superseded by P-4: the API now leaves through the broker, and `api.anthropic.com` is on no allow-list** |
| N-5 | The CLI's OTel exporter honours `NO_PROXY` and reaches E1's receiver through the in-box port → unix socket → host relay | the receiver's `liveSum()` after the run; the proxy log holds no `POST` | 5 `api_request` records, `usd` 0.1119485 = the envelope's `total_cost_usd`; `misconfigured` 0, `undecodable` 0; no non-CONNECT line at the proxy |
| N-6 | What else the CLI tries to reach | the refusal log of the design run; a string scan of the binary (`api.anthropic.com`, `downloads.claude.ai`, `status.anthropic.com`, `o1158394.ingest.us.sentry.io`, `datadoghq`) | one refusal: `http-intake.logs.us5.datadoghq.com:443` — refused, and the session was unaffected. Sentry and `downloads.claude.ai` were not contacted. With the broker (P-4, P-6) the CLI also tries `CONNECT api.anthropic.com:443` directly, 5–8 times per session, refused and unaffected; so the bare CONNECT list for the CLI is **empty**, and `api.anthropic.com` is a KNOWN refused host |
| N-7 | `npm install` works through the proxy inside the box | `npm install --prefix /tmp/ts typescript@7.0.2`, `HTTPS_PROXY` set, `--unshare-net` | 13 `CONNECT registry.npmjs.org:443`; `tsc --version` → 7.0.2, 2 s |
| N-8 | `dotnet restore` works through the proxy inside the box | a `net10.0` console project with `FluentAssertions 7.2.2`, `dotnet restore -m:4 --disable-build-servers` | 2 `CONNECT api.nuget.org:443`; restored in 2.2 s. dotnet also sent **58 plain-HTTP CRL/OCSP requests** (`crl3.digicert.com`, `ocsp.digicert.com`, `s.symcb.com`, …) which the proxy refused with `405`; the restore succeeded anyway (this is D-19's evidence) |
| N-9 | The box has no DNS and needs none | `getent hosts api.anthropic.com`, `/etc/resolv.conf` inside (it links into the hidden `/mnt`) | `no-dns`, `absent`; every client sent the hostname to the proxy, which resolves on the host. The draft's `resolverBinds` is **dropped** |
| N-10 | The token is on no process listing while the session runs | every `/proc/*/cmdline` read once a second during the design run, compared with the token's value in memory | 10 samples, 0 hits (the token travelled in the spawn `env`, never `--setenv`). With the broker it travels nowhere near the box: P-7 |
| N-11 | Nothing survives the box | after exit + 3 s, every `/proc/*/environ` scanned for `BENCH_CELL=<cell>` | none |
| N-12 | Hooks run inside the box, persistence is on, the subagent tool works | `hooks.ndjson` in `box/out`; `box/config/projects/**`; the stream | 8 hook records over `PreToolUse`, `PostToolUse`, `SubagentStart`, `SubagentStop` (the last two carry `agent_id`); a main transcript and `subagents/agent-a0dd13317fcbe4120.jsonl`; one `Agent` call, two `Bash` calls |
| N-13 | Effort of a CHILD is readable | OTel `api_request` attributes per record | `effort: "high"` on every record, main (`query_source: sdk`) and child (`agent:builtin:general-purpose`, `agent_id` set); `subagent_completed` names the child's model. **B-A3 is answered by O, not by the audit** |
| N-14 | The init read-back | the stream's first line | `cwd` = `box/work`; `mcp_servers: []`; `permissionMode: default`; `apiKeySource: none`; `claude_code_version: 2.1.310`; `memory_paths.auto` under `box/config`; `messaging_socket_path: /tmp/cc-socks/9.sock` (the box's own `/tmp`, pid 9 of the pid namespace); **`tools` = `Task, Bash, Edit, Glob, Grep, Read, Write` — `TodoWrite` was passed in `--tools` and is not reported** |
| N-15 | The bare box's other barriers, no model | probes in a box built as below | `gh --version` → `Permission denied` (`/dev/null` over `/usr/bin/gh`); `/mnt` empty; `/run/WSL` absent; `WSL_INTEROP` empty; a copy of `/mnt/c/Windows/System32/whoami.exe` executed in `box/work` → the WSL interop handler starts and dies at `UtilGetPpid` on `/proc/1/stat` (pid 1 is `bwrap`), **no Windows process, no name printed** (control outside: `jinx\strug`); the operator home shows only the bound dirs, the token file is absent; `tsc` 7.0.2 from the read-only toolchain; `touch` into the toolchain → `Read-only file system`; `node t.ts` strips types; `dotnet --list-sdks` → `10.0.113` |
| N-16 | The TypeScript toolchain exists | `npm install --prefix ~/rsd/minigit/toolchains/typescript-7.0.2 typescript@7.0.2`, then `chmod -R a-w` (no sudo) | installed, 31 MB, `tsc` 7.0.2; it is what N-15 bound |

**Not set, deliberately:** `CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC`. The allow-list already fails nonessential traffic closed and
the refusal log says what was tried; a second switch on the same thing would also change CLI behaviour the capture ran without.
`CLAUDE_CODE_PROXY_RESOLVES_HOSTS` exists in the binary and is not needed (N-9). `CLAUDE_CODE_GATEWAY_HINT_HEADERS` is not set: the
hint headers are off by default on a custom base URL (the gateway compatibility guide, read 2026-10-29) and the broker forwards whatever
arrives, so nothing depends on them.

### The credential broker, and what was run to prove it (2026-10-29, this machine, CLI 2.1.310, paid total $0.018452)

**Why a broker.** F-1 is right and the design run shows it: the token sat in the CLI's environment inside the box, so `printenv`,
`/proc/<pid>/environ` and a `Write` of `$CLAUDE_CODE_OAUTH_TOKEN` into `box/out` were all one tool call away, and the sampler watched
only `/proc/*/cmdline`. **An `apiKeyHelper` that returns the real credential is NOT a fix**: the CLI runs it as a command inside the
box, so Bash can run the same command and read the same value. Nor is a scanner: a scan of outputs for the token's value detects a
leak after the fact and prevents nothing. The fix is that the box never holds the credential at all.

**Shape.** The CLI gets `ANTHROPIC_BASE_URL=http://127.0.0.1:8787` and a **dummy** `CLAUDE_CODE_OAUTH_TOKEN` (`sk-ant-oat01-dummy-<rand>`,
fresh per session). `box-init` forwards `127.0.0.1:8787` to `<s>/net/api.sock`, where the harness's broker listens on the host. The broker
reads the real token from `~/.config/minigit-bench/claude-oauth-token` through `secrets.mjs` (mode 600 in a mode-700 dir, never the
environment), replaces `Authorization` with `Bearer <real>`, forwards every other request header unchanged — the gateway guide (read
2026-10-29) requires `anthropic-beta` and `anthropic-version` verbatim and says the OAuth capability travels in `anthropic-beta`
(*"stripping it fails those requests with 401"*; the value observed is `oauth-2025-04-20`) — to `https://api.anthropic.com` with no
redirect followed (a 3xx becomes `502`), and streams the response back chunk by chunk. `api.anthropic.com` is on **no** CONNECT
allow-list, so the broker is the only road to the API. The consultant's correction (codex-2, 2026-10-29) is what makes this more than a
relay: Bash can `curl -X POST http://127.0.0.1:8787/v1/messages` and the broker would sign it, so **the broker holds the cell's own
budget** (D-17) — a session cannot spend past its cap whatever it does, through the CLI or around it.

The proof, as the consultant priced it, in order; each stage's prediction was written before its run and is compared here. The harness
was `~/rsd/minigit/smoke/broker-probe-harness/` (`run.mjs`, `recorder.mjs`, `broker.mjs`, `signals.mjs`, beside the design run's `box-init.mjs`, `bench-proxy.mjs` and `unix-to-tcp.mjs`; throwaway, kept with the evidence) over the design run's box, `box-init.mjs` and CONNECT proxy
(`--allow=` empty: every CONNECT refused). The out dirs are under `~/rsd/minigit/smoke/` and are kept as evidence until the smoke's
fixture replaces them.

| # | Stage / claim | How it was checked | Predicted | Observed |
|---|---|---|---|---|
| P-1 | **Stage 1, free.** The CLI accepts a plain-http loopback base URL and sends its inference there | a recorder on `net/api.sock` answering `403 probe-reached` to everything; fresh `CLAUDE_CONFIG_DIR`; the dummy token; `claude -p --model sonnet --max-turns 1 --tools "" "Reply OK"`; 30 s wall (`~/rsd/minigit/smoke/20261029T221707Z-broker-stage1/api.ndjson`) | one `POST /v1/messages…` to the recorder, then an error result within 30 s | **holds**: one `POST /v1/messages?beta=true`, 7,589-byte body (`model: claude-sonnet-5-5`, `stream: true`); the result `Failed to authenticate. API Error: 403 probe-reached`, `api_error_status: 403`, `is_error: true`, 191 ms wall — and **exit code 0** (the read-back must not take exit 0 as success; the envelope's `is_error` decides) |
| P-2 | The request carries the dummy as a Bearer, the OAuth capability in `anthropic-beta`, no `x-api-key` | the recorder's header NAMES and an equality test against the dummy (values never logged) | `Authorization` = `Bearer <dummy>`; `anthropic-beta` holds an oauth value; no `x-api-key` | **holds**: 21 header names (`accept, accept-encoding, anthropic-beta, anthropic-dangerous-direct-browser-access, anthropic-version, authorization, connection, content-length, content-type, host, user-agent, x-app, x-claude-code-session-id, x-stainless-*` ×8); `authorizationIsDummy: true`, scheme `Bearer`; `anthropic-beta` = `claude-code-20250219,oauth-2025-04-20,…,extended-cache-ttl-2025-04-11` (10 values); `hasXApiKey: false` |
| P-3 | Nothing else got out | the proxy log of stage 1 | at most refused CONNECTs; no `HEAD /api/hello` (skipped under a proxy per the guide) | **holds**: 5 × `CONNECT api.anthropic.com:443` refused, 0 allowed, no `HEAD` anywhere; the session was unaffected |
| P-4 | **Stage 2, paid.** The minimal relay: fixed upstream, `Authorization` overwritten, `anthropic-beta` preserved, streaming forwarded | the broker at `--budget-usd 0.08`; OTel through `net/otel.sock`; the same argv and prompt (`~/rsd/minigit/smoke/20261029T221750Z-broker-stage2/`, `~/rsd/minigit/smoke/20261029T221858Z-broker-stage2b/`) | one forwarded request, `200` SSE, the result `OK`, cost $0.02–0.05, no CONNECT allowed | **holds** twice: 1 forwarded `POST /v1/messages?beta=true` with `anthropicBetaForwarded: true`; `200`, `text/event-stream`; result `OK`, `is_error: false`, `num_turns` 1; **$0.00922** and **$0.009232** (below the band: 2 input, 4 output, ~2,295 cache-write-1h tokens on `claude-sonnet-5-5`); 8 refused `CONNECT api.anthropic.com:443` each run, 0 allowed |
| P-5 | The broker's ledger is a 4th cost view that reconciles with the envelope and O | the `settled` line's usage priced by `inputs/prices-2026-10-29.json` (`asTheCliBills(table, '2.1.310')`) against the envelope's `total_cost_usd` and the receiver's `liveSum()` | equal within $0.000001 | **holds** on the second run: broker `0.009232` = envelope `0.009232` = O `0.009232`, from SSE `message_start` (input side, model) + `message_delta` (output); the first run's ledger was lost to a probe bug (the pricing call was handed the outcome, not the table — fixed, with a catch-all so a pricing failure logs instead of killing the broker), its envelope `0.00922` reprices from the table to exactly `0.00922` |
| P-6 | **The budget boundary, free.** A direct POST at zero remaining budget is refused and the upstream count stays unchanged | the broker at `--budget-usd 0`; from INSIDE the box, `sh -c 'curl -X POST http://127.0.0.1:8787/v1/messages …'` (Bash's shape, no CLI); then one from the host with `curl --unix-socket` (`~/rsd/minigit/smoke/20261029T221710Z-broker-boundary/`) | both `429`; `upstreamRequests` 0 | **holds**: in-box `http=429` `{"type":"error","error":{"type":"rate_limit_error","message":"broker: the cell's budget (0 USD) is spent"}}`; host `429`; the summary line `upstreamRequests: 0, refused: 2` |
| P-7 | The real token is in no process's cmdline or environ while a brokered session runs; the box holds only the dummy | host samplers once a second over every `/proc/*/{cmdline,environ}` counting the real value and the dummy (values compared in memory, never written) over P-1, P-4, P-6; inside the box `printenv CLAUDE_CODE_OAUTH_TOKEN \| sha256sum` and every readable `/proc/*/environ`'s value hashed, against `hashes.json` | real: 0 everywhere; dummy: in the box's environs; the in-box hashes equal the dummy's | **holds**: `realInCmdline 0, realInEnviron 0` on 6 samples; `dummyInEnviron` 4–12; in-box `env-token-sha256` and the one `environ-token-sha256` both equal `dummySha256`, 3 environs carry the variable (the shell, `curl`, `box-init`); P-1's `dummyInCmdline: 1` is the recorder's own `--dummy=` argv on the host, not the box |
| P-8 | The CLI's own cap still works beside the broker's | `--max-budget-usd` on the argv in every stage | both caps in force, the broker's the one that holds against Bash | not exercised beyond parsing: no stage reached either cap. The watchdog test table covers the broker's; the CLI's is belt beside braces |
| P-9 | Disconnect accounting | a client that closes mid-stream keeps the upstream billed | the broker keeps reading upstream to the end and settles what it parsed, else the reservation | **not exercised** by a run (every client read to the end); it is the test *A_client_that_disconnects_is_still_charged* over an injected stub upstream |

**What the broker run does not settle.** Why the CLI opens `CONNECT api.anthropic.com:443` 5–8 times beside the base URL: the guide
names fast mode's availability check and WebFetch's domain check as calls that bypass `ANTHROPIC_BASE_URL`; the refusals changed
nothing in three runs, which is all this licenses. Whether the CLI behaves the same on a later version with `allowedProviders` or
`CLAUDE_CODE_USE_GATEWAY` in play (both exist in the binary; neither is set here). Concurrency: every stage had one request in flight.

**Fallback, if a future CLI refuses the loopback base URL or the dummy.** The token goes back into the spawn `env` (N-10's shape, never
argv), and every output the session can write — the box's stdout and stderr, `box/out/**`, `box/work/**`, the transcripts, and every
`git push` the process arm makes — is scanned with S1's `tokenHits` for the token's value before anything is kept or scored, with a
`void: harness` outcome on a hit. That fallback **detects and does not prevent**: a session could still send the token to a host on the
allow-list inside TLS, and the only answer to that is the broker. It is written down so the choice is visible, not so it is taken.

### Design decisions (N-* replace the draft's X-*; D-16…D-21 are the gate's)

| # | Decision | Why |
|---|---|---|
| D-1 | `hiddenPaths({ operatorHome, exists })` is extracted from `scoreBoxArgv` and both boxes call it: `[operatorHome, /mnt, /run, /tmp, /var/tmp]` | one hide list; parent §7.1; E1's bus escape and `/mnt/c` |
| D-2 | **Both boxes use the unchanged `isolation()`** (`--unshare-net --unshare-pid --die-with-parent --new-session`); the agent box adds `--unshare-ipc`. No `resolverBinds`, no host-listener allow-list, no `host-allow.json`: with its own namespace the box cannot reach a host listener, so there is nothing to compare | N-1, N-9; a primitive reused rather than widened |
| D-3 | **Egress = `egressProxy`** on the host, in the harness process: listens on `<s>/net/proxy.sock` (mode 600), forwards only `CONNECT <host>:443` for `host ∈ EGRESS_HOSTS[arm]` (exact names, no suffixes), answers `403` otherwise and `405` to any non-CONNECT line (D-19), and logs every request as `{ t, verdict, host, port, line? }` to `<s>/egress.ndjson`. **`EGRESS_HOSTS` lives in `lib/egress-hosts.mjs`, which S1 creates and both S1's `contaminationHits` and this story import** (F-5: one module, no cycle). `EGRESS_HOSTS.bare = ['registry.npmjs.org', 'api.nuget.org']` — **`api.anthropic.com` is in no arm's list** (F-2, P-4): the API leaves through the broker only. The same module carries `KNOWN_REFUSED = ['api.anthropic.com', 'http-intake.logs.us5.datadoghq.com']`, the hosts the CLI is known to try and is refused without effect (N-6). `EGRESS_HOSTS.process` is **S4's to settle in P0** (`github.com`, `api.github.com`, `codeload.github.com` are the expected three; `raw.githubusercontent.com` is never in it — S1's contamination rule names it) | prevention by host, not audit (D21): in the bare arm GitHub and every other host are unreachable by construction, and a compromised session cannot make unmetered API calls |
| D-4 | **The box's supervisor** `box-init.mjs` (one file, node builtins only, copied into `<s>/harness/` like the audit hook) is what `bwrap` runs: it listens on `127.0.0.1:BOX_PROXY_PORT` (3128) → `<s>/net/proxy.sock`, `127.0.0.1:BOX_API_PORT` (8787) → `<s>/net/api.sock` and `127.0.0.1:BOX_OTEL_PORT` (4318) → `<s>/net/otel.sock`, then spawns `claude` with inherited stdio and exits with its code; a child ended by a signal exits `128 + that signal's number` (today's copy maps every signal to 137 — corrected here, P-12 shows the CLI exits 143 by itself). The ports are constants of the box's own namespace | N-3, P-1; exe + argv, no shell inside the box either |
| D-5 | **The receiver listens on a unix socket too:** `startReceiver({ dir, port, socketPath })` — when `socketPath` is given the same `http.Server` also listens there (mode 600). `otelEnv(BOX_OTEL_PORT)` is unchanged (its `127.0.0.1:4318` is the box's loopback) | N-5; widening E1's primitive instead of a relay process (the design run and the probes used a relay; the receiver's own listener is one hop fewer) |
| D-6 | **Bind an explicit list, never the session dir.** rw: `box/work` (cwd), `box/home` (HOME), `box/config` (`CLAUDE_CONFIG_DIR`), `box/tmp` at `/tmp` (`TMPDIR=/tmp`), `box/nuget`, `box/npm`, `box/out`; ro: `harness/` (settings, mcp.json, `audit.mjs`, `box-init.mjs`), `net/` (the three sockets), the `claude` binary FILE, the node dir, the TypeScript toolchain dir; the bare arm `--ro-bind /dev/null /usr/bin/gh`. `extraRo` / `extraRw` are S3's and S4's slots | the session dir also holds the stream, OTel, transcripts, `egress.ndjson`, `broker.ndjson`, `coai/` (S3) and `score/`: an agent that reads its own instruments is a contamination path; N-15 |
| D-7 | **Secrets never enter the box.** `bwrap` is spawned with `env = agentEnv(…)` and no `--clearenv`/`--setenv` at all (bwrap hands its environment to the child unchanged, so every variable travels the same way and none is on argv — N-10); and the one credential in that env is the **dummy** (D-16). The real token is read once by the harness process and held by the broker; `argv.json` records env KEYS only | F-1, P-7; `--setenv` lands in `/proc/<pid>/cmdline`, readable by every user; an env value lands in `/proc/<pid>/environ`, readable by the box |
| D-8 | `agentEnv` is a fixed allow-list; `PATH` is constructed (`<node>/bin:<ts>/node_modules/.bin:/usr/local/bin:/usr/bin:/bin`), so no `/mnt` entry and no `WSL_INTEROP` can exist; `CLAUDE_CODE_ENTRYPOINT` not set; the proxy triple and `ANTHROPIC_BASE_URL` set | parent §7.1; N-15; P-1 |
| D-9 | The CLI is the resolved file `~/.local/share/claude/versions/2.1.310` (realpath of `~/.local/bin/claude`), run by absolute path; `inputs/stacks.json` pins `claude` 2.1.310, node 26.12.1, dotnet SDK 10.0.113, typescript 7.0.2 and how each is probed | V4 |
| D-10 | TypeScript 7.0.2 lives read-only in `~/rsd/minigit/toolchains/typescript-7.0.2/`; **the preflight installs it when absent** (`npm install --prefix <dir> typescript@7.0.2`, then `chmod -R a-w`, no sudo) and refuses to start if `tsc --version` from inside a box is not 7.0.2 | N-16 |
| D-11 | Settings: `PINNED_EFFORT` imported from `settings.mjs:27-30`; `permissions.allow` per arm (`BARE_ALLOW = ['Bash', 'Read', 'Edit', 'Write', 'Glob', 'Grep', 'TodoWrite', 'Agent', 'Task']` — plain `Bash`, because a bare session runs arbitrary build commands and nobody answers a prompt); hooks `PreToolUse`, `PostToolUse`, `PostToolUseFailure`, `SubagentStart`, `SubagentStop` → `node <s>/harness/audit.mjs --out <s>/box/out/audit.ndjson`; no `defaultMode`, never `bypassPermissions`, **never `apiKeyHelper`** (a test asserts the key is absent). `mcp.json = { mcpServers: {} }` (bare) | D11, D17; N-12 (four of the five events observed firing; `PostToolUseFailure` registered, not exercised); F-1 |
| D-12 | argv: `-p --model opus --output-format stream-json --verbose --forward-subagent-text --include-hook-events --setting-sources project --settings <s>/harness/settings.json --strict-mcp-config --mcp-config <s>/harness/mcp.json --max-budget-usd <cap> --tools Agent,Bash,Read,Edit,Write,Glob,Grep,TodoWrite`; prompt on stdin; persistence ON. `BARE_TOOLS` is that list; `ToolSearch` and `mcp__coai__*` join in S3. The CLI's `--max-budget-usd` stays beside the broker's cap (P-8): the first is the CLI's own stop, the second holds against everything else | parent §8; N-12, N-14 |
| D-13 | Read-back tolerates exactly `INIT_UNREPORTED = ['TodoWrite']` (dated 2.1.310) missing from `init.tools`; everything else in `--tools` must be reported, with `Agent` ≡ `Task`. **A session's outcome is read from the envelope (`is_error`, `api_error_status`, `subtype`), never from the exit code: P-1 exited 0 on a 403** | N-14; whether the model can call `TodoWrite` is not exercised and the smoke says so (B-R2); P-1 |
| D-14 | **The stop is graceful first, by the only road that reaches inside.** `endCell({ cellId, bwrapPid, graceMs = 5000, killTree, procApi })`: (1) SIGTERM to every INNER process of the cell — a host pid whose `/proc/<pid>/environ` holds `BENCH_CELL=<cell>` exactly and whose `argv[0]` is not `bwrap` (the outer `bwrap` and its pid-namespace init carry the marker too); (2) wait `graceMs` for the `bwrap` child to close; (3) S1's `killTree(bwrapPid)` (SIGKILL to the group, `--die-with-parent` and the pid namespace end the rest); (4) the janitor ends any host process still carrying the marker. **A SIGTERM to the process group is not step 1 and never will be:** the box runs under `--new-session`, so the group holds only the outer `bwrap`, which dies on SIGTERM and takes the inside down by SIGKILL through `--die-with-parent` (P-10). What the grace buys, measured: the CLI exits itself with code 143 in under a second (P-12), its transcript is on disk; **no OTel batch followed the SIGTERM** in the one run that could show it, so "a graceful stop lets the CLI flush OTel" is a hope this story records as B-K2, not a property it claims | F-3; P-10, P-11, P-12; `common.security` — write what you asked for, not what it achieves |
| D-15 | Children's effort comes from O (`api_request.effort` per `agent_id`), the audit's `SubagentStart` joins `agent_id` to the `Agent` call's `tool_use_id`; D17's "read back from the audit" is amended to "read back from O, joined through the audit" | N-13 |
| D-16 | **The credential broker** `brokerServer({ socketPath, token, upstream = { host: 'api.anthropic.com', port: 443, tls: true }, budget, prices, cliVersion, log })` in the harness process, on `<s>/net/api.sock` (mode 600): request headers forwarded as an open list minus `authorization`, `x-api-key`, `host`, `connection`, `content-length`, `transfer-encoding`, `accept-encoding` (dropped so the usage parser reads plain SSE; `host` rewritten to the upstream); `Authorization: Bearer <token>` set; method and path verbatim (`/v1/messages?beta=true` observed; `count_tokens` and anything else pass the same way); a 3xx from upstream becomes `502` and is logged `redirect-refused`; response status, headers and body relayed as they arrive, SSE `ping`s included; every request logged `{ t, seq, event: 'forwarding' \| 'settled' \| 'refused' \| 'redirect-refused' \| 'upstream-error', method, path, headerNames, status, model, usage, chargedUsd, spentUsd, elapsedMs, clientClosedEarly }` to `<s>/broker.ndjson` — never a header value, never a body. `upstream` is injectable so tests run a local stub; the smoke runs the real one | F-1, F-2; P-1…P-7; the gateway guide's header rules |
| D-17 | **The broker holds the cell's budget** (`lib/budget.mjs`, pure): `admit({ spentUsd, reservedUsd, reserveUsd, capUsd })` → `{ admit: true } \| { admit: false, why }` when `spent + reserved + reserve > cap`; a refused request answers `429` with an API-shaped `rate_limit_error` naming the cap (the CLI retries and then reports; Bash sees the same); each admitted request reserves `reserveUsd` (`BARE_RESERVE_USD = 0.05`, a constant per arm, so N concurrent subagents hold N reservations) until it settles; settlement prices the parsed usage (`message_start` + `message_delta` for SSE, the JSON body otherwise) with `priceUsage` over `asTheCliBills(table, cliVersion)` — **the same table E1's views use**, so a disagreement is a bug report and not a harness note; nothing parsed → the reservation stands as the charge, never 0; a client that disconnects mid-stream is still charged: the broker reads the upstream to its end (it is billed whatever the client does), settles, and logs `clientClosedEarly: true`. The ledger is the **4th cost view, `B`** beside E/O/T: `brokerView(lines)` → `{ forwarded, refused, spentUsd, settled: [...] }`, and `crosscheck` gains T5: `B.spentUsd = E.total_cost_usd` within $0.000001 when every request settled, else `B` is the price of what the session did spend — which is the cost of a killed session, the number F-3 cares about | the consultant's correction; P-5, P-6, P-9; `common.measurement` §2 |
| D-18 | **`apiKeyHelper` is not a fix** and is never set: the CLI runs it inside the box, and Bash can run it too. The read-back refuses a settings file carrying the key | F-1 |
| D-19 | **Plain HTTP is not proxied.** The proxy answers `405` to every non-CONNECT request line (a `GET http://…`, dotnet's CRL/OCSP fetches) and logs it as `nonConnect`; the only plain-HTTP destinations a box has are its own loopback forwarders (`NO_PROXY=127.0.0.1,localhost`: the broker door and OTel). Evidence that nothing needs more: N-8's 58 refused CRL/OCSP requests with the restore succeeding, N-7's npm install with none, and B-N2 watching every smoke for a non-CONNECT line from the CLI. A build that turns out to need a plain-HTTP host is a new decision with its own evidence, not a widening of the proxy | F-4; N-7, N-8 |
| D-20 | **Cleanup is a stack, run on every exit path, idempotent.** `launchSession` pushes a disposer for each thing it creates, in order — the state file, the receiver, the broker, the proxy, the three sockets, the `bwrap` child (`endCell`), the samplers and timers, the transcripts copy — and `finally` runs them LIFO whatever the path: success, a read-back kill, a watchdog kill, a startup failure (a port taken, the preflight refusing, the toolchain absent), a thrown error, or cancellation (`AbortSignal`; `smoke-session.mjs` and the runner wire SIGINT/SIGTERM into it, `common.reliability` — every CLI handles Ctrl+C). Each disposer is idempotent (a second call is a no-op), each is wrapped so one failing cannot skip the next, and the failures are collected into `meta.json.cleanup`. A session dir that holds **nothing paid** — no `stream.ndjson` line, no OTel record, `box/out` and `box/config/projects` empty — is removed on a startup failure; any other dir is kept with `state.json.phase = 'failed'` (paid work is written as it is earned and never deleted by the thing that failed) | F-6; `common.reliability` — cancellation is real, shutdown is planned |
| D-21 | **A session claims its directory atomically and says what it is doing.** `sessionLayout` creates `<rid>/` with a non-recursive `mkdir` — `EEXIST` is a refusal, a rid is never reused — and `launchSession` writes `<s>/state.json` `{ phase: 'starting' \| 'running' \| 'closed' \| 'failed' \| 'retired', hostPid, cell, startedAt, heartbeatAt }`, heartbeating `heartbeatAt` on every watchdog tick (5 s). **Stale detection** runs before any session starts under a runs root (`retireStale({ root, now, procApi, endCell })`, called by `smoke-session.mjs` and the runner's prepare): a `state.json` whose phase is `starting` or `running` and whose `hostPid` is gone or whose `heartbeatAt` is older than `STALE_AFTER_MS = 60000` is stale; retirement ends the cell's survivors through `endCell` (by marker, never by name), unlinks `net/*.sock`, sets `phase: 'retired', retiredAt, retiredBy: <hostPid>`, and leaves everything else (its artefacts are evidence of what a crash cost). A stale dir is never reused and never deleted here; `retire.mjs` (E4) is the one deleter. The residual: a host whose clock jumps can mark a live session stale; the heartbeat is a timestamp, so a wedged holder stops being one without noticing — the shape `common.reliability` asks for | F-6; `common.reliability` — name the atomic operation, heartbeat not deadline |

### Files

| Path | Change |
|---|---|
| `spikes/minigit-bench/lib/bwrap.mjs` | `hiddenPaths({ operatorHome, exists })` extracted; `scoreBoxArgv` calls it (its argv unchanged — E1's tests prove it); `isolation()` unchanged |
| `spikes/minigit-bench/lib/egress-hosts.mjs` | **S1 creates it** (F-5): `EGRESS_HOSTS`, `KNOWN_REFUSED`; S2 imports and does not define them. `sandbox.mjs` exports no host list |
| `spikes/minigit-bench/lib/sandbox.mjs` | new — `sessionLayout(root, rid)` (atomic claim, D-21), `agentBoxArgv({ layout, arm, toolchains, claudeBinary, nodeDir, extraRo = [], extraRw = [] })`, `agentEnv({ layout, dummyToken, arm, cellId, extra = {} })`, `AGENT_ENV_KEYS` (the one literal key list), `BOX_PROXY_PORT`, `BOX_API_PORT`, `BOX_OTEL_PORT`, `INIT_UNREPORTED`, `dummyToken(rand)` |
| `spikes/minigit-bench/lib/egress.mjs` | new — pure `connectDecision(requestLine, allow)` → `{ allow: true, host, port } \| { allow: false, status: 403 \| 405, why }`; `egressProxy({ socketPath, allow, log })` (the `net` server; the one I/O here); `egressSummary(lines)` → `{ allowed: {host: n}, refused: {host: n}, nonConnect: n }` |
| `spikes/minigit-bench/lib/broker.mjs` | new — `brokerServer(…)` (D-16; the `http`/`https` I/O), pure `forwardHeaders(headers, upstreamHost)`, `parseSseUsage()` (a feeder with `result()`), `brokerView(lines)` |
| `spikes/minigit-bench/lib/budget.mjs` | new — pure `admit(…)`, `settle({ state, entry, usage, model, table, cliVersion })` → the new state and the ledger line, `BARE_RESERVE_USD` |
| `spikes/minigit-bench/harness/box-init.mjs` | new — the in-box supervisor (D-4); copied into `<s>/harness/` by `launchSession`; signal exit mapped to `128 + signal` |
| `spikes/minigit-bench/lib/launch.mjs` | new — `claudeArgv({ layout, cap, tools })`, `settingsFor({ arm, layout })`, `mcpConfigFor(arm)`, `BARE_TOOLS`, `BARE_ALLOW`, `HOOK_EVENTS`, `FORBIDDEN_SETTINGS = ['apiKeyHelper', 'permissions.defaultMode']` |
| `spikes/minigit-bench/lib/secrets.mjs` | new — `readSecretFile({ home, parts })`, `secretStatus({ home, parts })`, extracted from `capture/capture-env.mjs:15-34` (which imports them; `TOKEN_FILE_PARTS` stays there); `PAT_FILE_PARTS = ['.config', 'minigit-bench', 'github-pat']` for S4 |
| `spikes/minigit-bench/lib/otel-receiver.mjs` | `startReceiver({ dir, port, socketPath })` — the optional unix listener (D-5) |
| `spikes/minigit-bench/lib/host-checks.mjs` | new — `toolchainFacts(probes, pins)` against `inputs/stacks.json`; `toolchainInstallPlan({ dir, exists })` → the npm argv and the chmod when absent, `null` when present; `bwrapFacts(probes)` |
| `spikes/minigit-bench/lib/watchdogs.mjs` | new — pure `watchdogDecision({ now, startedAt, lastStreamAt, lastOtelAt, cpuTicks, prevCpuTicks, liveUsd, brokerUsd, meterUsd, capUsd, wallMs, stallMs })` → `{ kill: false } \| { kill: true, reason: 'wall' \| 'cost' \| 'stall' }` |
| `spikes/minigit-bench/lib/janitor.mjs` | new — `cellProcesses(cellId, procApi, { inner = false })` (NUL-split `environ`, exact `BENCH_CELL=<id>`; `inner` drops `argv[0] === 'bwrap'`; unreadable entries skipped and counted), `cpuTicksOf(pids, procApi)`, `endCell({ cellId, bwrapPid, graceMs, killTree, procApi, sleep })` (D-14), `endSurvivors(cellId, { killTree })` |
| `spikes/minigit-bench/lib/session-state.mjs` | new — pure `stateOf(json, { now, pidAlive })` → `'live' \| 'stale' \| 'closed' \| 'malformed'`, `STALE_AFTER_MS`; `writeState`, `heartbeat`, `retireStale({ root, … })` (D-21) |
| `spikes/minigit-bench/lib/readback.mjs` | new — `readBackInit(init, expected)` → `{ ok: true } \| { ok: false, mismatches: [{ field, expected, observed }] }`; `sessionOutcome(envelope, exit)` (D-13: the envelope decides) |
| `spikes/minigit-bench/lib/transcripts.mjs` | new — `walkJsonl`, `copyTranscripts(fromConfigDir, toDir)` moved out of `capture/capture-run.mjs:34-66` (which imports them) |
| `spikes/minigit-bench/lib/crosscheck.mjs` | T5 — `B` against `E` (D-17); `crosscheck-policy.json` gains its row |
| `spikes/minigit-bench/lib/session-launch.mjs` | new — `launchSession(plan, deps, { signal })`: the state file, the receiver with its socket, the broker, the proxy, the copies into `harness/`, `bwrap` spawned detached with `env`, every stream line stamped `{ t, line }` on arrival (`mapClock`, `timeline.mjs:160`) into `stream.stamped.ndjson` beside the raw `stream.ndjson`, the init read-back, the watchdogs and heartbeat every 5 s, the samplers (cmdline AND environ, real token AND dummy) every 1 s, `endCell`, 3 s for the last OTel batch, the disposer stack (D-20), the janitor, transcripts copy, `argv.json` (bwrap argv, claude argv, env KEYS, sha256 of settings and mcp.json), `egress.json` (`egressSummary`), `broker.json` (`brokerView`), `meta.json` |
| `spikes/minigit-bench/smoke-session.mjs` | new — CLI: `--out <dir> [--dry-run]`; SIGINT/SIGTERM → abort; `retireStale` first; exit 0 · 3 environment · 4 usage · 5 failed · 130/143 cancelled (the capture's contract, widened) |
| `spikes/minigit-bench/inputs/stacks.json` | new — the pins and their probes |
| `spikes/minigit-bench/test/{agent-box,egress,broker,budget,box-init,launch,secrets,host-checks,watchdogs,janitor,session-state,readback,session-launch}.test.mjs` | new |
| `spikes/minigit-bench/test/fixtures/real/claude-2.1.310-bare-smoke/` | new — the sanitised smoke (E1's `sanitiseCapture`, `capture/capture-files.mjs:53`) with `SMOKE.md` |
| `research/module_minigit_bench.md` | the agent box, the egress proxy, the broker and its budget, the layout, the facts N-1…N-16 and P-1…P-12 and the smoke; "What it does not do yet" updated |

### The session layout (outside every git tree, under the runs root `~/rsd/minigit/<series>/<rid>/`)

```
<rid>/                     harness-only unless listed as bound; claimed by a non-recursive mkdir (D-21)
  state.json               phase, hostPid, cell, startedAt, heartbeatAt (D-21)
  harness/                 ro in the box: settings.json, mcp.json, audit.mjs, box-init.mjs
  net/                     ro in the box: proxy.sock, api.sock, otel.sock (all mode 600, created by the harness, unlinked by its disposers)
  box/work/                rw, the agent's cwd (bare: empty for the smoke; the runner fills it)
  box/home/  box/config/  box/tmp/  box/nuget/  box/npm/  box/out/      rw
  stream.ndjson  stream.stamped.ndjson  stderr.txt  otel/  egress.ndjson  egress.json  broker.ndjson  broker.json  transcripts/
  argv.json  meta.json  readback.json  watchdog.ndjson
```

The directory name is the `rid` only — nothing in a path the model sees names the arm or the stack. A socket path must stay
within `sun_path`'s probed limit — **108 bytes accepted, 109 refused with `EINVAL`** on this machine (kernel 6.18.54, Node 26.12.1,
measured while building this; the first design probe failed exactly this way with a 140-byte path): `SOCKET_PATH_MAX_BYTES = 108`
in `sandbox.mjs`, counted in UTF-8 bytes, and `sessionLayout` refuses a runs root that would push `<rid>/net/proxy.sock` past it,
naming the length. Its boundary tests are 108 (accepted), 109 (refused) and 108 characters carrying one two-byte character (refused).

### The change — what and how

1. **Box** (`agentBoxArgv`, pure; `exists` injected): `bwrap` + `roRoot()` + `devProc()` + `hiddenPaths().flatMap(tmpfs)` +
   `--bind box/tmp /tmp` **before** any bind that lives under the host's `/tmp` + ro binds (node dir, toolchains, the claude
   file, `harness/`, `net/`) + rw binds (`box/*`) + `extraRo`/`extraRw` + bare `--ro-bind /dev/null /usr/bin/gh` + `isolation()`
   + `--unshare-ipc` + `--chdir <box/work>` + `--` + `<node> <s>/harness/box-init.mjs 3128:<s>/net/proxy.sock 8787:<s>/net/api.sock
   4318:<s>/net/otel.sock -- <claude> <claudeArgv>`. No `--setenv`, no `--clearenv`.
2. **Env** (`agentEnv`): `PATH` (D-8), `HOME`, `TMPDIR=/tmp`, `LANG=C.UTF-8`, **`CLAUDE_CODE_OAUTH_TOKEN=<dummy>`**,
   **`ANTHROPIC_BASE_URL=http://127.0.0.1:8787`**, `CLAUDE_CONFIG_DIR`, `CLAUDE_CODE_DISABLE_BACKGROUND_TASKS=1`, `DISABLE_AUTOUPDATER=1`,
   `...otelEnv(BOX_OTEL_PORT)` (its `OTEL_LOG_TOOL_DETAILS=1`, `OTEL_LOG_USER_PROMPTS=0`), `HTTPS_PROXY=HTTP_PROXY=http://127.0.0.1:3128`,
   `NO_PROXY=127.0.0.1,localhost`, `DOTNET_CLI_HOME`, `DOTNET_NOLOGO=1`, `DOTNET_CLI_TELEMETRY_OPTOUT=1`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`,
   `MSBUILDDISABLENODEREUSE=1`, `UseSharedCompilation=false`, `NUGET_PACKAGES`, `npm_config_cache`, `BENCH_CELL=<cellId>`,
   `GIT_CONFIG_GLOBAL=<s>/harness/gitconfig` (empty file in the bare arm), `...FIXED_GIT` (S1). `extra` is S4's slot (`GH_TOKEN`,
   `GH_CONFIG_DIR`, `GH_PROMPT_DISABLED` — S4 decides whether the PAT gets a broker of its own; the shape is here). The key set equals
   `AGENT_ENV_KEYS` (plus `extra`'s keys), the one literal list; a test diffs them. **The real token is not a parameter of `agentEnv`.**
3. **Egress** (`egress.mjs`): `connectDecision` parses the first line only (`CONNECT host:port HTTP/1.1`); exact host match, port
   `443`; a header block over 64 KiB closes the connection. The proxy connects upstream only after the decision and replies
   `200 Connection Established`; an upstream error is logged `{ verdict: 'upstream-error', code }`. The log is append-only, one
   JSON line per request. Nothing in the log is a secret: the proxy never sees inside TLS. Plain HTTP: D-19.
4. **Broker and budget** (`broker.mjs`, `budget.mjs`): as D-16 and D-17. The token comes from `secrets.mjs` in the harness process;
   `brokerServer` receives it as a value and the log never carries it. The broker's cap is the cell's cap; `watchdogDecision` reads
   `brokerUsd` (settled + reserved) as its first cost source, `liveUsd` (O) second, `meterUsd` (the stream meter) third — the first is
   synchronous with the response, the second arrives a second later, the third is an estimate.
5. **Preflight** (`host-checks.mjs`, called by `smoke-session.mjs` and later by E3's `preflight.mjs`): `bwrap` present and
   `unshare -Ur true` ok; the toolchain install plan executed when the dir is absent (D-10); then, **inside a box built by
   `agentBoxArgv`** with no proxy and no broker running (so the probes cannot reach anything): `claude --version` 2.1.310, `node --version`
   26.12.1, `dotnet --list-sdks` exactly one line `10.0.113`, `tsc --version` 7.0.2 — each against `stacks.json`; the token file's
   status (`secretStatus`), and in the process arm the PAT file's. `retireStale` over the runs root (D-21). A listener scan is no longer
   part of the preflight (D-2).
6. **Launch, read-back, watchdogs, stop, collect** (`launchSession`): as in the Files table. Read-back on the init event, before the
   first paid turn completes: `cwd` = `box/work`; `tools` = `--tools` with `Agent`≡`Task`, minus `INIT_UNREPORTED`, nothing else;
   `mcp_servers = []` (bare); `permissionMode = default`; `claude_code_version` = `stacks.json`; `apiKeySource = none`;
   `memory_paths.auto` under `CLAUDE_CONFIG_DIR`; `model` an id `prices-*.json` prices. A mismatch stops the session at once through
   `endCell`; the outcome is `void: harness` with `readback.json` naming the field. Watchdogs: wall; cost (`brokerUsd ≥ cap`, else
   `liveSum().usd ≥ 1.1 × cap`, else the stream meter `streamMeterUsd`, `claude-usage.mjs:115`, at published prices); stall (no stream
   line, no OTel record, no broker line and no CPU-tick growth of the cell's processes for `stallMs`); each decision is a line in
   `watchdog.ndjson`. Every stop — watchdog, read-back, cancellation — is `endCell` (D-14). The samplers read every `/proc/*/cmdline`
   AND `/proc/*/environ` once a second and count matches of the REAL token's value and of the dummy's; the counts go to `meta.json`,
   the values nowhere. The outcome is `sessionOutcome(envelope, exit)` (D-13).
7. **Smoke** (`smoke-session.mjs`): out dir outside every git tree (E1's `outProblems`, `capture-env.mjs:57`); `retireStale`; preflight;
   token from the file into the broker only; layout; `--max-budget-usd 0.2` on the CLI and `cap 0.2` on the broker; wall 5 min; `stallMs`
   2 min; prompt: *"Launch one subagent with the Agent tool (`model: "sonnet"`, subagent_type general-purpose) told to run `ls -A` in the
   current directory and reply with what it printed. When it has returned, run `printenv CLAUDE_CODE_OAUTH_TOKEN > token.txt` with Bash
   yourself and reply DONE."* — `box/work` holds `a.txt` and `b.txt`. The session is told to persist its token on purpose: `token.txt`
   must hold the dummy and nothing else (B-S3). Then, over the out dir, E1's instruments: `envelopeView`, `otelView`, `transcriptView`,
   `brokerView`, `crosscheck` (T1–T5), `sessionCost`, `aliasRecord`, `spansFrom` + `partition`; S1's `contaminationHits` and `tokenHits`
   (with the real token as an own secret, over every file of the out dir); `egressSummary`. `SMOKE.md` is computed, never typed (the
   `verdicts` pattern).

### Smoke verdicts (computed into `SMOKE.md`; `holds` / `refuted` / `not exercised`, none blank)

| Id | Assumption | Holds → | Refuted → |
|---|---|---|---|
| B-R1 | the init read-back matches every field of step 6 | the runner may rely on it | fixed before any bare cell |
| B-R2 | `init.tools` lacks exactly `INIT_UNREPORTED` | the tolerance stays as dated | the list is corrected and dated again |
| B-N1 | every allowed CONNECT names a host of `EGRESS_HOSTS.bare`; every refused host is in `KNOWN_REFUSED` or is named in the report | the lists stand | a new host is classified (CLI telemetry or a direct API reach → `KNOWN_REFUSED`; needed → the arm list with a reason) before any cell |
| B-N2 | the proxy log holds no non-CONNECT line from the CLI (a plain-HTTP reach would mean `NO_PROXY` or the exporter changed); the only plain-HTTP traffic is the box's own loopback (D-19) | — | investigated |
| B-S2 | every API call went through the broker: `broker.ndjson` holds every `api_request` O saw (by count and, where the CLI sends it, `x-claude-code-session-id`), and `api.anthropic.com` was never allowed | the broker is the one road | stop: a second road exists |
| B-S3 | the box held only the dummy: the samplers counted the real token 0 times in cmdline and environ over ≥ 5 samples; `box/work/token.txt` equals the dummy; `tokenHits` over the out dir with the real token as an own secret: none | — | stop |
| B-$1 | `B.spentUsd = E.total_cost_usd` within $0.000001 (T5), and `B` = O's `usd` | the broker's ledger prices a killed session | investigated before any cell: the two price the same tokens from the same table |
| B-O1 | OTel records reached the receiver through the socket; T2 passes | the cost watchdog reads the live sum | no paid cell runs until fixed (parent §9's last fallback) |
| B-T1 | `box/config/projects/**` holds main and one subagent transcript; T3 and T4 pass under `crosscheck-policy.json` | T's coverage as in E1 | per E1 amendment (2): unchecked, investigated |
| B-E1 | an envelope; T1 passes; cost ≤ $0.20 | — | stop |
| B-A1 | audit Pre/Post pairs by `tool_use_id`; `partition` Σ = wall to the ms; `unpaired` empty | — | fix S1/S2 |
| B-A2 | the `PostToolUse` payload carries `duration_ms` | `duration_ms` recorded | spans from Pre/Post `ts` only (E1's method); O's `tool_result.duration_ms` (a string, A-O1) beside it — recorded |
| B-A3 | every O `api_request` carries `effort`, and the child's (by `agent_id`) is `high`; `SubagentStart` carries the same `agent_id` | D17 read back from O through the audit join | children's effort `unknown` in the report; recorded as a limit |
| B-H1 | hook events still carry no timestamp (E1 A-H1) | `HOOK_TIME_SOURCE` stays `arrival` | E1's constant is revisited |
| B-K1 | after exit, no host process carries `BENCH_CELL=<cell>`; the janitor ended 0; `state.json.phase = 'closed'`; no socket file left | — | find the escape before any cell |
| B-K2 | (only when a session was stopped by `endCell`) an `api_request` record or a broker `settled` line for the in-flight request arrived after the SIGTERM | the grace flushes a killed session's last price | `not exercised` on a clean smoke; `refuted` → B is the price of a killed session and D-14 says so |
| B-C1 | `contaminationHits` and `tokenHits` over the smoke: none | — | read by hand |

### Test scenarios

Effect tests run the real `bwrap` (`sandboxGate`, E1 `test/helpers.mjs:48`) with E1's `probe`/`withoutPair` shape: every "cannot"
has a control that shows the same probe CAN without the barrier. The hide list is iterated from `hiddenPaths()`, never retyped.
A test that applies to both boxes is written once over `[scoreBoxArgv, agentBoxArgv]`. Egress tests start a real `egressProxy`
on a socket under a `testsToDelete-mgb-…` dir with a SHORT path, and a real `box-init.mjs` inside the box. The proxy forwards
only to port 443, so the positive control of an allowed host is `api.nuget.org:443` (a real host, no auth, no cost; the test
skips with its reason when the host itself has no network). Broker tests use an injected stub upstream (`upstream: { host:
'127.0.0.1', port, tls: false }`) that replays a recorded SSE body with `message_start`/`message_delta`/`ping`; a FAKE token
`sk-ant-fake-<rand>` stands in for the real one everywhere a test needs a value.

| Test | Input | Expected |
|---|---|---|
| `Both_boxes_hide_the_same_paths_and_isolate_the_same_way` | `hiddenPaths()`; both argvs | each hidden path appears as `--tmpfs` in both; both carry `isolation()`'s four flags; `scoreBoxArgv`'s argv equals E1's (`bwrap.test.mjs:62`'s assertions still green) |
| `The_agent_box_reaches_no_host_loopback_abstract_socket_or_address` | a host abstract listener, a host `127.0.0.1` TCP listener, `1.1.1.1:443`; probed inside the agent box and in the same argv `withoutPair('--unshare-net')` | isolated: all three refused/unreachable; control: all three reached (N-1) |
| `A_bound_unix_socket_crosses_the_namespace_through_a_read_only_bind` | a host listener on `<tmp>/net/x.sock`, `net/` bound ro; `box-init.mjs 5000:<sock> -- sh -c 'exec 3<>/dev/tcp/127.0.0.1/5000'` | connected and answered (N-2, N-3); control: without the bind, `ENOENT` |
| `The_proxy_allows_only_the_listed_hosts_on_443` (effect) | `egressProxy` with `['api.nuget.org']`; from inside a box through `box-init`: `curl -s -o /dev/null -w %{http_code} https://api.nuget.org/v3/index.json`, then `https://example.com/`, then `https://api.anthropic.com/` | `200`; curl exit 7 with a `403` line in the log for each refused host; `egressSummary` → `{ allowed: { 'api.nuget.org': 1 }, refused: { 'example.com': 1, 'api.anthropic.com': 1 }, nonConnect: 0 }` |
| `Plain_HTTP_is_refused_and_the_restore_does_not_need_it` (D-19; effect, needs network; skip with reason) | from inside the box: `curl http://example.com/` (a `GET` line at the proxy); `curl http://127.0.0.1:3128/` with a `GET`; then the N-8 `dotnet restore` | both `GET`s → `405`, logged `nonConnect`; the restore exits 0 with its CRL lines counted under `nonConnect` and no allowed host but `api.nuget.org` |
| `The_connect_decision_is_exact_and_deny_by_default` (pure, iterated over a table) | `CONNECT api.nuget.org:443`, `CONNECT api.nuget.org:80`, `CONNECT evil.api.nuget.org:443`, `CONNECT API.NUGET.ORG:443`, `CONNECT api.anthropic.com:443` with the bare list, `GET http://x/ HTTP/1.1`, an empty line, a 70 KiB line | only the first allowed; port → 403; suffix → 403; case → 403; the API host → 403; GET → 405; empty → 405; oversize → closed |
| `No_arm_lists_the_API_host_and_the_lists_come_from_one_module` | `EGRESS_HOSTS` from `lib/egress-hosts.mjs`; an import-graph scan of `lib/` and `capture/` | no arm contains `api.anthropic.com`; `sandbox.mjs` and `contamination.mjs` both import the module and define no host list of their own |
| `Npm_and_dotnet_restore_through_the_proxy_from_inside_the_box` (effect, needs network; skip with reason) | the N-7 and N-8 probes with `EGRESS_HOSTS.bare` | `tsc` 7.0.2 installed under `/tmp`; `dotnet restore` exit 0; the log names only `registry.npmjs.org` and `api.nuget.org` as allowed |
| `The_box_has_no_dns_and_that_is_the_point` | `getent hosts api.anthropic.com` inside; outside | inside fails; control outside resolves (N-9) |
| `The_agent_box_has_no_operator_home_and_no_key` | decoy home with a decoy `heldout.key` and decoy `claude-oauth-token` (E1's shape, `bwrap.test.mjs:113`); the real home | empty; unreadable; control: readable outside |
| `The_agent_box_has_no_windows_drive_and_no_interop` | `ls -A /mnt`; `echo $WSL_INTEROP`; `ls /run/WSL`; a COPY of `/mnt/c/Windows/System32/whoami.exe` in `box/work`, executed | empty; empty; absent; the exe prints no `\` user name and exits non-zero (N-15: the interop handler dies on `/proc/1/stat`). Control: the same copy outside prints a name; skip with reason when `/mnt/c` is absent |
| `The_agent_box_cannot_reach_the_session_bus_or_the_user_manager` | E1's two tests (`:155`, `:172`) over the agent argv | as E1, with their controls |
| `The_agent_box_sees_only_its_own_tmp` | E1's `:182` over the agent argv; plus `/tmp` shows `box/tmp`'s content | as E1 |
| `The_agent_box_cannot_see_its_instruments_or_its_partner` | a second session's layout beside it; probes for `<s>/stream.ndjson`, `<s>/otel`, `<s>/egress.ndjson`, `<s>/broker.ndjson`, `<s>/state.json`, `<s>/score`, `<s>/coai`, `<partner>/box/work/x`, `~/rsd/minigit/score-tmp` | none visible; control: `box/work` and `harness/settings.json` visible; `touch` in `harness/` and in `net/` fails |
| `The_bare_box_has_no_gh` | `gh --version`; `/usr/bin/gh --version` inside | both `Permission denied`; control: `/usr/bin/gh --version` outside works |
| `The_toolchain_is_read_only_and_tsc_is_the_pinned_one` | the real toolchain dir bound ro; `tsc --version`; `touch <dir>/x` | `7.0.2`; `Read-only file system`; skip with reason when the dir is absent (the preflight installs it) |
| `The_box_holds_only_the_dummy_in_printenv` (F-1; effect) | a box from `agentBoxArgv` + `agentEnv({ dummyToken: D })` while a host-side `brokerServer` holds the fake real token R, running `printenv CLAUDE_CODE_OAUTH_TOKEN` and `env` | the output holds D and not R. **Positive control:** the same box with R placed in the env by hand prints R — the probe can see a token when one is there |
| `The_box_holds_only_the_dummy_in_proc_environ` (F-1; effect) | the same box running `cat /proc/*/environ \| tr '\0' '\n' \| grep -c <R>` and the same for D (the values passed as argv of the in-box `sh` only — a test, with a fake) | 0 for R, ≥ 1 for D; control as above: ≥ 1 for R when R is in the env |
| `The_box_holds_only_the_dummy_in_what_it_persists` (F-1; effect) | the same box running `printenv CLAUDE_CODE_OAUTH_TOKEN > box/out/t.txt; cp /proc/self/environ box/work/e.bin`; then `tokenHits` over the whole session dir with `[R]` as own secrets | `t.txt` = D; `tokenHits` → none for R; control: with R in the env, `tokenHits` names both files (the scan is alive) |
| `The_real_token_is_on_no_host_process_listing_or_environ_while_a_session_runs` (effect) | `launchSession` with a stub `claude` (below) and R in the broker; the samplers | `meta.samplers.realInCmdline = 0`, `realInEnviron = 0`, `dummyInEnviron ≥ 1` over ≥ 3 samples (P-7's shape) |
| `Setting_a_secret_with_setenv_would_list_it_and_that_is_the_point` | the dummy passed by `--setenv` | found in bwrap's cmdline — the refuted approach, shaped |
| `The_broker_signs_with_the_real_token_and_forwards_the_open_list` (D-16) | the stub upstream; a request with `authorization: Bearer <D>`, `x-api-key: x`, `anthropic-beta: a,oauth-2025-04-20`, `anthropic-version: 2023-06-01`, `x-claude-code-session-id`, `accept-encoding: gzip`, `host: 127.0.0.1:8787` | the stub saw `authorization: Bearer <R>`, no `x-api-key`, `anthropic-beta` and `anthropic-version` byte-equal, the session id present, no `accept-encoding`, `host` = the upstream; the log line names headers and never a value (`JSON.stringify(log)` contains neither R nor D) |
| `The_broker_streams_and_prices_an_sse_response` (D-16, D-17) | the stub replays the recorded body of `~/rsd/minigit/smoke/20261029T221858Z-broker-stage2b` (a `message_start` with 2 input / 2,297 cache-write-1h tokens, pings, a `message_delta` with 4 output tokens) in 5 chunks 50 ms apart | the client receives the first chunk before the last is sent (streaming, not buffering); `settled.usage` = the body's; `chargedUsd` = `priceUsage` of it = **0.009232** at `asTheCliBills(table, '2.1.310')` |
| `The_broker_follows_no_redirect` | the stub answers `302 Location: https://evil.example/` | the client gets `502`; the log `redirect-refused`; the stub saw one request |
| `A_direct_request_at_zero_budget_leaves_the_upstream_count_unchanged` (D-17; P-6's shape) | `brokerServer` with `budget.capUsd = 0`; a `POST /v1/messages` with no CLI (host `curl --unix-socket`), and one from inside a box through `box-init` | both `429` with the API-shaped body; the stub saw **0** requests; `brokerView` → `{ forwarded: 0, refused: 2 }` |
| `Admission_reserves_per_in_flight_request_and_refuses_at_the_cap` (pure, a table) | `admit` over `{ spent, reserved, reserve, cap }` rows: `0/0/0.05/0.2`, `0.16/0/0.05/0.2`, `0.1/0.1/0.05/0.2`, `0/0.2/0.05/0.2`, `0.2/0/0.05/0.2` | admit, refuse (`0.21 > 0.2`), refuse, refuse, refuse — exactly those |
| `Three_concurrent_requests_hold_three_reservations` | the stub holds its answers; three requests at once with `cap = 0.12`, `reserve = 0.05` | the third is refused while two are in flight; after both settle (each priced below the reserve) a fourth is admitted |
| `A_client_that_disconnects_is_still_charged` (D-17; P-9) | the stub streams slowly; the client destroys its socket after the first chunk | the stub's body was read to its end; `settled` with `clientClosedEarly: true` and `chargedUsd` = the body's price; the stub saw one request |
| `Nothing_parsed_is_charged_the_reservation_never_zero` | the stub answers `200` with an empty body | `chargedUsd` = `reserveUsd`; `priced.known: false` |
| `An_api_key_helper_is_refused_and_absent` (D-18) | `settingsFor('bare', …)`; `readBackInit` over a settings object carrying `apiKeyHelper` | the key absent; the read-back `ok: false` naming `apiKeyHelper` |
| `The_env_is_the_allow_list_and_its_path_has_no_mnt` | `agentEnv` with a parent env holding `GH_TOKEN`, `WSL_INTEROP`, `/mnt/c/x` on PATH, `CLAUDE_CODE_OAUTH_TOKEN=<R>` | key set equals `AGENT_ENV_KEYS`; no `/mnt` PATH entry; bare: no `GH_*`; `NO_PROXY` names `127.0.0.1`; `ANTHROPIC_BASE_URL` = `http://127.0.0.1:8787`; `CLAUDE_CODE_OAUTH_TOKEN` = the dummy, never R |
| `The_box_argv_carries_no_setenv_and_no_secret` | `agentBoxArgv` + an env with the dummy, R in the broker | no `--setenv`, no `--clearenv`; neither value in any element |
| `Box_init_runs_the_command_and_returns_its_code` | `box-init.mjs` with no forwarders and `-- sh -c 'exit 7'`; `-- sh -c 'kill -TERM $$'`; with a forwarder spec whose port is taken | exit 7; exit 143; a listen failure exits non-zero before spawning (the command never ran) |
| `A_group_SIGTERM_never_reaches_inside_the_box` (D-14; P-10's shape) | a box running `sh -c 'trap "echo got-TERM >&2; exit 3" TERM; echo ready; sleep 60 & wait $!'`; `process.kill(-bwrapPid, 'SIGTERM')` | `bwrap` closes by `SIGTERM` within 100 ms; stderr has no `got-TERM`; no cell process left — the refuted approach, shaped |
| `A_SIGTERM_to_the_inner_pid_is_delivered_and_its_code_comes_out` (D-14; P-11) | the same box; `cellProcesses(cell, procApi, { inner: true })` → the shell's host pid; SIGTERM to it | stderr `got-TERM`; `bwrap` closes with **code 3**, no signal; `inner: true` excluded the two `bwrap` pids (asserted) |
| `endCell_is_graceful_then_final` (D-14) | (a) the same box: `endCell({ graceMs: 2000 })` | `bwrap` closed with code 3 before the grace ran out; `killTree` NOT called (spy) — (b) a box whose shell traps TERM and ignores it: `endCell({ graceMs: 500 })` → `killTree` called once after ≥ 500 ms; within 3 s no cell process; the janitor ended 0 |
| `The_CLI_stopped_mid_request_exits_itself_within_the_grace` (effect; local only; P-12's shape, free) | the hanging recorder as the base URL; the real CLI with the dummy; `endCell({ graceMs: 10000 })` once the recorder logged the POST | `bwrap` closes with code 143 within the grace; `killTree` not called; the transcript file exists under `box/config/projects`; the count of OTel records after the SIGTERM is recorded into the test's output (B-K2's shape: today 0) |
| `Killing_bwrap_leaves_no_cell_process` | box runs `sh -c 'sleep 300 & sleep 300'` with `BENCH_CELL=<rand>`; `killTree(bwrapPid)` | within 3 s `cellProcesses` finds none; control: before the kill it finds ≥ 2 |
| `The_janitor_ends_a_survivor_by_its_marker_never_by_name` | a host process outside any box with `BENCH_CELL=<rand>` and one named `claude` without it | the first ended, the second untouched |
| `Every_exit_path_runs_every_disposer_once` (D-20) | `launchSession` with injected deps whose disposers are spies, driven down each path: success; read-back mismatch; watchdog `wall`; a receiver whose `listen` throws (startup failure); a proxy socket path already taken; an `AbortSignal` aborted mid-run; a dep that throws inside the loop | on every path each disposer called exactly once, in LIFO order; a disposer that throws does not stop the next; the failures are in `meta.cleanup`; calling the stack twice calls nothing twice |
| `A_startup_failure_leaves_no_listener_and_no_empty_dir` (D-20; effect) | `launchSession` whose stub `claude` is a path that does not exist | the receiver port and the three sockets are gone (`connect` → `ECONNREFUSED`/`ENOENT`); the session dir removed (nothing paid in it); exit `5` |
| `A_cancelled_launch_stops_the_cell_and_closes_everything` (D-20; effect) | the stub `claude` sleeping; `signal.abort()` after 1 s | `endCell` ran (SIGTERM first, spy); no cell process; sockets removed; `state.json.phase = 'closed'`; `meta.json.stopped = 'cancelled'`; the dir kept (a stream line was written) |
| `A_stale_session_is_detected_and_retired_before_reuse` (D-21) | a runs root with four dirs: `running` with a dead `hostPid`; `running` with a live pid and a heartbeat 61 s old; `running` live and fresh; `closed` — plus a live survivor process carrying the first dir's marker | `stateOf` → `stale`, `stale`, `live`, `closed`; `retireStale` ends the survivor (spy on `endCell` with that cell), unlinks the two stale dirs' `net/*.sock`, sets their phase `retired` with `retiredBy`, touches the live and closed dirs not at all, deletes nothing |
| `A_session_dir_is_claimed_once` (D-21) | `sessionLayout(root, rid)` twice | the second refused naming `EEXIST`; a rid holding a `state.json` is never written over |
| `The_toolchain_is_probed_inside_the_box_and_installed_when_absent` | fake probe outputs: two SDK lines; node 25; tsc 6; `toolchainInstallPlan` with `exists` false/true | each refused naming the pin; the plan is the npm argv + chmod, or `null` |
| `Watchdogs_kill_for_wall_cost_and_stall_and_only_then` | a table of states, including cost from the broker when O is silent, cost from the meter when both are silent, and stall reset by a broker line or CPU growth alone | the one expected decision per row |
| `A_readback_mismatch_kills_before_the_work` | init fixtures: an `mcp_servers` entry in bare; `tools` missing `Bash`; version 2.1.311; `Task` instead of `Agent`; `TodoWrite` absent; `TodoWrite` AND `Grep` absent | the first three `ok: false` naming the field; the fourth and fifth `ok`; the sixth `ok: false` naming `Grep` |
| `The_outcome_is_the_envelopes_not_the_exit_code` (D-13; P-1's shape) | `sessionOutcome` over P-1's result line (`is_error: true`, `api_error_status: 403`) with exit `{ code: 0 }`; a clean result with exit 0; no result with exit 143 | `void: api-error 403`; `ok`; `void: stopped` |
| `The_settings_pin_effort_and_never_bypass` | `settingsFor('bare', …)` | `effortLevel` and the three `modelSettings` `high` (equal to `PINNED_EFFORT`); `permissions.allow` = `BARE_ALLOW`; no `defaultMode`; no `apiKeyHelper`; five hook events pointing at the harness copy with `--out` |
| `The_argv_persists_the_session_and_names_no_secret` | `claudeArgv` | no `--no-session-persistence`; `--strict-mcp-config`; the prompt not in argv; neither token value anywhere |
| `The_receiver_also_listens_on_a_socket` | `startReceiver({ socketPath })`; a POST of a real OTLP body through the socket and one through the port | both land in `otel.ndjson`; the socket is mode 600; `close()` removes it |
| `The_secrets_reader_is_the_captures` | modes 644 file, 755 dir, a link, empty; `CLAUDE_CODE_OAUTH_TOKEN` set in env with no file | each refused naming why; the env is ignored; `capture-env.mjs` imports it (E1's capture tests stay green) |
| `The_launch_stamps_every_line_and_collects_what_E1_reads` (stub `claude` = a script that POSTs one request to `ANTHROPIC_BASE_URL` with the dummy, prints the capture's `stream.ndjson`, POSTs one OTLP body to `OTEL_EXPORTER_OTLP_ENDPOINT` and writes a transcript under `CLAUDE_CONFIG_DIR`, inside a real box; the stub upstream behind the broker) | `launchSession` | `stream.stamped.ndjson` has one `{ t, line }` per line, `t` increasing; the request reached the stub upstream signed with R; the OTel body arrived through the socket; transcripts copied; `argv.json` carries env KEYS only; `egress.json`, `broker.json` written; `state.json.phase = 'closed'`; receiver closed; janitor ran; the sockets removed |
| `The_real_bare_smoke_holds` (needs the token; local only) | the committed `claude-2.1.310-bare-smoke` fixture | `SMOKE.md` equals `renderVerdicts` of the fixture; B-R1, B-R2, B-N1, B-N2, B-S2, B-S3, B-$1, B-O1, B-T1, B-E1, B-A1, B-A3, B-K1, B-C1 `holds`; B-K2 `not exercised`; leak scan clean |

### Extension points for S3 and S4 (process arm)

- `agentBoxArgv({ extraRo, extraRw })` and `agentEnv({ extra })` — S3 adds `<s>/bridge` (ro) and the `mcp.json` with the bridge
  client; S4 adds `GH_*` and the gitconfig with the credential helper. **S4 inherits F-1's shape for the PAT:** a `GH_TOKEN` in the env
  is one `printenv` away, so S4 decides between a git credential helper that talks to a host-side broker over `net/` and a scanned
  fallback, with this story's probes as the template.
- `EGRESS_HOSTS.process` — S4 settles it in P0 with the refusal log as the evidence (D-3); the proxy itself does not change, and
  `api.anthropic.com` stays off it.
- `box-init.mjs` takes any number of `<port>:<socket>` pairs — S3's bridge socket is reached the same way if it ever needs a
  TCP face; today it is a unix socket the MCP client script opens directly.
- `settingsFor('process', …)` adds `mcp__coai__*` and `ToolSearch` to the allow; `claudeArgv({ tools })` adds `ToolSearch`.
- The coai box of S3 is written as "network shared"; the leak N-1 names (host loopback, abstract sockets, any host) applies to
  it too, and so does F-1: the vendor sign-ins (`~/.codex/auth.json`, the antigravity token, `~/.vibe/.env`) are credentials in a box
  that runs vendor code. S3 should put coai in its own namespace with this proxy and a vendor host list, and say which of its
  credentials can be brokered — a decision for S3's spec, named here.

### Commands

`node spikes/minigit-bench/run-tests.mjs test/agent-box.test.mjs test/egress.test.mjs test/broker.test.mjs test/budget.test.mjs
test/box-init.test.mjs test/launch.test.mjs test/secrets.test.mjs test/host-checks.test.mjs test/watchdogs.test.mjs
test/janitor.test.mjs test/session-state.test.mjs test/readback.test.mjs test/session-launch.test.mjs`;
`node spikes/minigit-bench/smoke-session.mjs --out ~/rsd/minigit/smoke/$(date -u +%Y%m%dT%H%M%SZ)` (it installs the toolchain when
absent — it is present since 2026-10-29) and the sanitise step into the fixture; `BENCH_REQUIRE_SANDBOX=1 node
spikes/minigit-bench/run-tests.mjs` (the whole spike).

### Done when

- Every effect test green with none skipped on this machine (`BENCH_REQUIRE_SANDBOX=1`), each "cannot" beside its control — the
  three token tests with their positive controls, the broker and budget tests, the two signal tests, the cleanup and stale tests and
  the egress tests included.
- The bare-session smoke ran once on CLI 2.1.310 through the broker, cost ≤ $0.20 (pilot spend, beside the design run's $0.1119 and
  the broker proof's $0.0185); the sanitised fixture and `SMOKE.md` committed; every B-* verdict computed, the required ones
  `holds`, B-A2 and B-K2 recorded either way. E1's counters read the smoke: T1–T5 as the policy says, `sessionCost` known,
  `partition` Σ = wall, `B` = `E`.
- `api.anthropic.com` appears in no `EGRESS_HOSTS` arm and the real token's value appears in no sampler count, no file of the
  smoke's out dir and no test fixture (the sanitiser's leak scan carries it as an own secret).
- `scoreBoxArgv`'s argv unchanged (E1's tests green); the capture's tests green after the extractions; `startReceiver` without
  `socketPath` behaves as before; S1's `contaminationHits` imports `lib/egress-hosts.mjs` (no second list).
- `research/module_minigit_bench.md` describes the agent box, the egress proxy, the broker and its budget, the stop, the cleanup and
  state file, the layout and the smoke's verdicts, and the facts tables N-1…N-16 and P-1…P-12 with their dates.
- The summary names who wrote the spec (Fable), the coder and the reviewer.

### The signal probes behind D-14 (2026-10-29, free; `~/rsd/minigit/smoke/broker-probe-harness/signals.mjs`)

| # | Claim | How it was checked | Predicted | Observed |
|---|---|---|---|---|
| P-10 | A SIGTERM to the process group does not reach inside a `--new-session` box | a box running a shell that traps TERM and prints `got-TERM`; `kill(-bwrapPid, SIGTERM)` (`~/rsd/minigit/smoke/20261029T222221Z-sig-delivery2/`, step A1) | the outer `bwrap` dies on it and `--die-with-parent` SIGKILLs the inside; no `got-TERM` | **holds**: `bwrap` closed by `SIGTERM` after 10 ms; stderr empty; no cell process left |
| P-11 | A SIGTERM to the inner process's HOST pid is delivered, and its exit code comes out through `box-init` and `bwrap` | the same box; the shell's host pid found by marker with `argv[0] ≠ bwrap`; SIGTERM to it (step A2) | `got-TERM`, exit code 3 | **holds**: stderr `got-TERM`; `bwrap` closed with code 3, no signal, after 9 ms. A first run of this probe also signalled the outer `bwrap` (its cmdline carries the inner command, so a cmdline match hit it) and was confounded — hence `inner: true` in `cellProcesses` |
| P-12 | The CLI signalled directly mid-request exits itself within a grace, with its transcript on disk; whether OTel flushes | the hanging recorder as the base URL; the real CLI with the dummy and OTel to the receiver; SIGTERM to the CLI's host pid 3.3 s in, 10 s grace (`~/rsd/minigit/smoke/20261029T222227Z-sig-cli2/`) | exit within the grace, by signal or a non-zero code; transcript present; OTel after the signal unknown | **holds, and OTel refuted**: exit **code 143** after 751 ms (the CLI's own exit, not a signal death); transcript of 16 lines under `box/config/projects`; OTel records 5 before the SIGTERM (`managed_settings_resolved`, 3 × `plugin_loaded`, `user_prompt`), **0 after**; no cell process left |

---

## E2.S3 — coai in its own box, the stdio↔socket bridge refusing out-of-session paths, the coai settings snapshot (T-B1)

**Model:** scenarios + brief Opus (this text) · code Sonnet · review Opus. Process arm only; not needed by the bare runner.

**Goal.** The process session reaches coai through ONE door that refuses any path outside the session, and coai — which runs
vendor CLIs that "read anywhere and no flag removes the shell" (`dew_flow_connect_other_ais ·
src_mcp/tests/ConfinementPlannerTests.cs:69-90`) — runs in a box that holds only the session's tree, its own data dir and the
vendor sign-ins. coai's settings are a pinned snapshot with the disk-reading question prompts off (D14).

**Vendor sign-ins on this machine** (read from `dew_flow_connect_other_ais` and listed by name, 2026-10-29; contents never read):

| Runtime | Rows in today's settings | Sign-in | Binary and runtime files | coai's own folders |
|---|---|---|---|---|
| codex (codex-cli 0.164.0) | `codex`, `codex-3`, consultant `codex-2` | `~/.codex/auth.json` (`$CODEX_HOME`, else `~/.codex` — `src_mcp/runners/Reviewers/CodexConfigFile.cs:28-33`); config `~/.codex/config.toml` | `~/.local/bin/codex` → `~/.codex/packages/standalone/releases/0.164.0-x86_64-unknown-linux-musl/bin/codex` | — (codex writes sessions and sqlite logs into `~/.codex`) |
| antigravity (`agy` 1.4.10) | `antigravity`, `antigravity-2` | `~/.gemini/antigravity-cli/antigravity-oauth-token`; settings `~/.gemini/antigravity-cli/settings.json` (`research/RESULTS_agy_allow_rule.md:23`); coai reads `~/.gemini/config/mcp_config.json` (`NoMcpServers.cs:156-163`) | `~/.local/bin/agy` (`AntigravityRuntime.cs:207-213`) | `~/.local/share/coai-agy/<h…>` (`AntigravityReadOnly.cs:48`, LocalApplicationData) |
| vibe (vibe 3.0.2) | `vibe`, question rows | `~/.vibe/.env`, LINKED by coai into a coai-owned `VIBE_HOME` on WSL2 (`src_mcp/runners/Vibe/VibeSignIn.cs:11-41`; `research/architecture.md:39`) | `~/.local/bin/vibe` → `~/.local/share/uv/tools/mistral-vibe/bin/vibe` (python of that venv), `~/.local/bin/uv` | the owned `VIBE_HOME` under coai's data dir |

None of the three uses a D-Bus keyring on WSL2 (all three are files), so hiding `/run/user` does not cut a sign-in — E2.S3's
live check confirms it.

**Files.**

| Path | Change |
|---|---|
| `spikes/minigit-bench/lib/coai-box.mjs` | new — `coaiBoxArgv({ layout, vendorHomes, coaiDir, operatorHome })`, `coaiEnv(…)`, `VENDOR_HOMES` (the table above as relative parts; realpaths resolved at build time so a symlinked binary's target is bound) |
| `spikes/minigit-bench/lib/coai-settings.mjs` | new — `snapshotSettings(operatorJson)`, `committedView(snapshot)`, `expectedProviders(snapshot, stage)` |
| `spikes/minigit-bench/lib/coai-bridge.mjs` | new — pure `checkMessage(msg, { sessionRoot, realpath, tools })` → `{ forward: true } \| { reply: <JSON-RPC error> }` |
| `spikes/minigit-bench/coai-bridge-server.mjs` | new — host side: listens on `<s>/bridge/coai.sock` (mode 0600, one client), starts coai-mcp in the coai box over stdio, forwards newline-delimited JSON-RPC both ways, checks every client message, logs `{ t, id, method, tool, verdict, ms }` to `<s>/bridge.ndjson` (the `coai_gate` spans "timed at the bridge too", parent §10) |
| `spikes/minigit-bench/harness/coai-bridge-client.mjs` | new — inside the agent box: stdio ↔ the socket, ~40 lines, node builtins only; copied into `<s>/harness/` like the audit hook |
| `spikes/minigit-bench/lib/launch.mjs`, `lib/sandbox.mjs` | the process arm's `mcpConfigFor('process')` = `{ mcpServers: { coai: { type: 'stdio', command: <node abs>, args: [<s>/harness/coai-bridge-client.mjs, <s>/bridge/coai.sock] } } }`; `PROCESS_TOOLS` adds `ToolSearch`; the allow adds `mcp__coai__*` and `ToolSearch`; `extraRo` gets `<s>/bridge` |
| `spikes/minigit-bench/inputs/coai-settings.json` | new — the committed view: non-secret fields of the snapshot + `sha256` of the full runtime snapshot (the full file is mode 600 under the runs root, never in git) |
| `spikes/minigit-bench/test/fixtures/real/coai-mcp-0.63.0-tools-list.json` | new — the real `tools/list` answer, captured through the box (no model) |
| `spikes/minigit-bench/test/{coai-box,coai-settings,coai-bridge}.test.mjs` | new |

**The change — what and how.**

1. **The coai box.** `hiddenPaths()` (E2.S2) tmpfs'd, `resolverBinds`; ro: the session's `box/repo` and `box/wt` (where the
   process arm's clone and worktrees live, E2.S4), the coai-mcp directory, `~/.gemini/config`, `~/.vibe/.env`, the vendor
   binaries and the uv venv (realpaths); rw: `<s>/coai` (`COAI_DATA_DIR`), `<s>/coai/tmp` at `/tmp`. **Vendor sign-in
   stores are never bound read-write** (plan gate E2, 2026-10-29): each vendor's store is bound read-only where its CLI
   tolerates that, otherwise a per-session copy under `<s>/coai/vendor/<name>` is bound — and a copy is used only if the
   live check shows the CLI does not rotate a refresh token into it (a rotated copy would invalidate the operator's
   original). Tests: a write from inside the box to each operator store fails, the operator stores' hashes are unchanged
   after a live round, and no credential bytes appear in session-visible output. Superseded list kept for the record:
   ~~rw `~/.codex`, `~/.gemini/antigravity-cli`, `~/.local/share/coai-agy`~~; network: its OWN namespace with E2.S2's proxy and a vendor host list
   (amended 2026-10-29 after the E2.S2 spec: "shared" leaked host loopback, abstract sockets and any host, GitHub's `data`
   included; S3 names the vendor hosts from a proxy refusal log of one live round); `--unshare-pid --die-with-parent`. `HOME` stays the
   operator's real path (the CLIs resolve `~` there) but the home is a tmpfs holding only those binds. Env allow-list: `HOME`,
   `PATH` (`~/.local/bin` is NOT on it — each binary is invoked through the bound realpaths; coai's own lookup of `agy` falls
   back to `$HOME/.local/bin/agy`, so that one file is bound), `LANG`, `TMPDIR=/tmp`, `COAI_DATA_DIR=<s>/coai`,
   `COAI_DATA_SIDE=bench`, `COAI_ON_EXHAUSTED=good_enough` (D5). Never the held-out key or the Claude token (they are under the
   hidden home and in no env).
2. **The settings snapshot.** `snapshotSettings` from the operator's settings file: `COAI_QCONSULT_ROOTS` **removed**; every
   `COAI_QCONSULT_PROMPTS` entry whose capability is `disk` removed, and every row that only served such a prompt disabled
   (D14); `COAI_ON_EXHAUSTED=good_enough`; written to `<s>/coai/bench/settings.json` (the path `SettingsFile.cs:168-188`
   resolves for `COAI_DATA_DIR`+`COAI_DATA_SIDE`), its sha256 equal to the committed view's. `committedView` keeps vendor ids,
   runtimes, models and stage flags; any field whose name matches `/key|token|secret|password|cred/i` or whose value matches
   S1's `tokenHits` is refused from the committed file (a value, naming the field). `expectedProviders(snapshot, 'plan'|'code')`
   derives the count a round should have answered — today **2** for plan and code (codex, antigravity), not 5.
3. **The bridge policy** (`checkMessage`), deny by default:
   - `initialize`, `notifications/*`, `tools/list`, `prompts/list`, `ping` → forward;
   - `tools/call` with a tool name not in the fixture's list → refused (a new coai tool is a code change here, not a pass);
   - `repoPath` must be absolute and `realpath(repoPath)` inside `<s>/box` (`repo` or `wt/*`); repository-relative arguments —
     `committedPlan`, `documentPath`, `planPath`, `document`, `plan`, `feature`, each element of the `suspectedFiles` JSON
     array — must be relative, contain no `..` segment after normalisation, and pass a LEXICAL containment check
     (`path.resolve(repoPath, arg)` under `repoPath`); `realpath` is then applied to the LOWEST EXISTING ancestor and must
     stay inside `repoPath`, so a not-yet-existing path (a new plan file) is accepted while a symlink leading out is refused
     (plan gate E2, 2026-10-29);
   - which argument names are path-shaped is DERIVED from the fixture's input schemas by one rule (a property named
     `repoPath`, `*Path`, `committedPlan`, `suspectedFiles`, or `document`/`plan`/`feature` of type string) and a test pins the
     derived set against the bridge's table;
   - a refusal is `{ jsonrpc: '2.0', id, error: { code: -32001, message: "bench bridge: '<arg>' resolves outside this session
     (<rule>)" } }`; malformed JSON → `-32700` and the bridge stays up; a line over 8 MB → refused, connection kept.
4. **Read-back additions** (E2.S2's `readBackInit` with the process arm's expectations): `mcp_servers` = `[{ name: 'coai',
   status: 'connected' }]`; the init `tools` include `mcp__coai__review_plan` (the deferred-schema case: a name may arrive only
   after `ToolSearch` — then the read-back accepts its absence at init and E3's model smoke settles it). The server key `coai`
   and `classify.mjs:14`'s `COAI_PREFIX` derive from one exported constant.

**Test scenarios (T-B1).**

| Test | Input | Expected |
|---|---|---|
| `A_repoPath_outside_the_session_is_refused` | `consult` with `repoPath` = this repository's top level | refused, `-32001`, names `repoPath`; nothing reaches coai (a spy on the forward) |
| `Dot_dot_and_symlinks_out_are_refused` | `repoPath` `<s>/box/repo/../../other`; a symlink `<s>/box/repo/l → /home/jinx/git` used as `<s>/box/repo/l` | both refused |
| `Every_repository_relative_argument_is_checked` | iterated over the derived set: `committedPlan: '../../x.md'`, `documentPath: '/etc/passwd'`, `planPath: '../p.md'`, `document: '../../d'`, `suspectedFiles: '["ok.cs","../../x"]'` | each refused naming the argument |
| `Arguments_inside_the_session_pass` | `review_plan` with `repoPath <s>/box/repo`, `committedPlan 'todo/PLAN_x.md'`; `consult` with `suspectedFiles '["src/a.cs"]'` | forwarded unchanged (byte-equal) |
| `An_unknown_tool_is_refused` | `tools/call` `delete_everything` | refused |
| `The_path_parameter_set_is_derived_from_coais_own_schema` | the fixture | derived set equals the bridge table; adding a fake `fooPath` to a copy of the fixture makes the test fail (alive) |
| `Malformed_and_oversized_lines_do_not_stop_the_bridge` | `{not json`, a 9 MB line, then a valid `tools/list` | `-32700`, refusal, then the list forwarded |
| `The_snapshot_turns_off_disk_reading_and_drops_the_roots` | a synthetic operator settings with ROOTS, a disk prompt, a web prompt | no `COAI_QCONSULT_ROOTS`; no disk prompt; the web prompt kept |
| `The_committed_view_holds_no_secret` | a synthetic settings with a `keyRef` field and a `sk-ant-…` value | refused naming both; the real committed `inputs/coai-settings.json` passes `tokenHits` |
| `The_expected_provider_count_is_derived` | today's shape (5 rows, 2 with plan/code) | `plan: 2`, `code: 2`, `feature: 5` |
| `The_coai_box_hides_the_home_but_the_sign_ins` (needs bwrap) | in the coai box: `ls -A ~` | exactly the bound entries (`.codex`, `.gemini`, `.vibe`, `.local`); `~/git`, `~/.config/minigit-bench`, `~/.claude` absent; control: present outside |
| `The_coai_box_cannot_see_another_session` | two layouts; probe the other's `box/repo` | absent |
| `The_real_coai_lists_its_tools_through_the_bridge_from_inside_an_agent_box` (needs coai-mcp; no model) | the client script in a real agent box sends `initialize` + `tools/list` | the fixture's names; every name the classifier's `GATE_TOOLS`/`CONSULT_TOOLS` hold is present (`classify.mjs:12-13`) |
| `The_real_coai_sees_every_vendor_signed_in_from_its_box` (live, vendor quota only) | `providers` through the bridge | every row of the snapshot answers with its version and signed in; the count of plan/code providers equals `expectedProviders` |
| `A_document_round_runs_in_the_box_while_a_consult_on_this_repository_is_refused` (live, vendor quota only) | a scratch repo in `<s>/box/repo` with a three-line `NOTE.md`; `open` + `review_document` through the bridge; meanwhile `consult` with `repoPath` = `dew_flow_benchmark` | the round completes with `expectedProviders(…,'document')` reviewers answering; the consult is refused by the bridge |

**Commands.** `node spikes/minigit-bench/run-tests.mjs test/coai-box.test.mjs test/coai-settings.test.mjs
test/coai-bridge.test.mjs`; the two live tests with `BENCH_LIVE_COAI=1`.

**Done when.** T-B1 green; the two live tests run (not skipped) and green, their output (provider ids, versions, verdict) in
the PR; `inputs/coai-settings.json` committed and clean under `tokenHits`; the fixture `tools/list` committed.

---

## E2.S4 — the treatment pinned: conventions snapshot branch and its boundary plan, one-source templates, lint gate, tags, GitHub setup

**Model:** scenarios + brief Opus (this text) · code Sonnet · review Opus. Not marked (the epic's one mark is E2.S2); a strict
spec instead. Process arm only. Three of its steps act outside this repository and need the operator's go, each named.

**Goal.** What each process session is given — conventions, instructions, settings, CI, stack files — is one source tree,
proven equal across stacks outside the marked regions, frozen by tags and rulesets, on a fork that holds nothing a session
should not read.

### The token decision (`common.test-leftovers`)

The snapshot is cut from the CURRENT `release` (`e29883f`). That release's `test-leftovers.md` has no `## Tokens` table and
no owner or repository segment (§2): names are `testsToDelete-<what>-<id>`, and **no token is registered** — `minigitcs` and
`minigitnode` (parent §15 step 1) are not needed. Were the snapshot ever cut from a release that carries the newer rule, **one
token suffices** for the one fork: a token names a repository, not a language (one helper per language, both with the same
constant), and the two concurrent cells cannot sweep each other anyway — each box has its own `/tmp`, its own home, its
worktrees inside its own clone, and no Docker socket (`/run` is hidden). The token would then be `minigit`, registered in the
conventions repository's `## Tokens` table and its private twin, before first use.

### Boundary with `dew_flow_conventions` (named on both sides, `common.planning-docs`)

| Item | Built by | The other side's part |
|---|---|---|
| Branch `bench/minigit-2026-11` at the current `release` sha, never updated | `dew_flow_conventions` (its plan below), pushed by this story with the operator's go | this repository: the templates' `.gitmodules` name the branch; E3's harness checks its tip before and after every session (D9) |
| A ruleset on `bench/minigit-2026-11`: no update, no deletion, no force-push, no bypass | `dew_flow_conventions` | — |
| The boundary plan `dew_flow_conventions · todo/PLAN_minigit_bench_snapshot_branch.md` (status line, this table, "retire: kept until the series report is promoted, then the branch stays as provenance") and its `todo/README.md` row | `dew_flow_conventions` (a PR there) | this document, which carries the same table |
| Tokens | nobody (decision above) | — |

Order: the conventions plan and the branch first (the templates' `.gitmodules` point at it); disjoint otherwise — nothing in
the conventions repository changes its rules for the bench.

**Files.**

| Path | Change |
|---|---|
| `spikes/minigit-bench/templates/shared/**`, `templates/csharp/**`, `templates/node/**`, `templates/bare/README.md` | new — the one source tree (parent §6, minus every CodeRabbit item, D19) |
| `spikes/minigit-bench/templates/build.mjs` | new — renders the four branch trees into a scratch dir from the source and `inputs/stacks.json` |
| `spikes/minigit-bench/template-diff.mjs`, `lib/template-diff.mjs` | new — shared files byte-equal; stack files differ only between `# --- stack:begin/end ---` (or `// ---`) markers; output into `inputs/treatment.md` |
| `spikes/minigit-bench/inputs/treatment.md` | new — each stack's strictness, the identical-set digest, the lint parser decision (V12), the four template shas and tags |
| `spikes/minigit-bench/lib/github-session.mjs` | new — the process arm's session-side credentials: `gitconfigFor({ layout })` (`credential.https://github.com.helper = !gh auth git-credential`, fixed author), `ghEnv({ pat, layout })` → `GH_TOKEN`, `GH_CONFIG_DIR=<s>/box/gh`, `GH_PROMPT_DISABLED=1` for E2.S2's `agentEnv({ extra })`; the PAT from `readSecretFile({ parts: PAT_FILE_PARTS })` |
| `spikes/minigit-bench/test/{template-diff,templates,github-session}.test.mjs` | new |
| fork `oleksandrdubyna88/minigit-bench` | the four `template/*` branches (normal commits on top — the ruleset forbids force-push), tags, rulesets, settings, the `data` branch removed |

**The change — what and how.**

1. **Conventions side** (operator's go: approved per D9). In a worktree of `dew_flow_conventions`: the boundary plan and its
   README row, PR, merged by its own rules. Then `git ls-remote origin release` (re-read at cut time; `e29883f` today), `git
   push origin <sha>:refs/heads/bench/minigit-2026-11`, the ruleset (`update`, `deletion`, `non_fast_forward`, no bypass
   actors). Check: a consumer with `branch = bench/minigit-2026-11` and the pin at that sha gets `pin-check` green
   (`tools/pin-check.mjs:9-12`).
2. **Fork hygiene** (operator's go, each listed in the PR): delete branch `data` (upstream's generated solutions, TypeScript
   among them — one `git fetch origin data` from a session's own remote; upstream still has it, so S1's `upstream-solutions`
   rule keeps auditing it); `has_wiki` and `has_projects` off; merge commits off (squash + rebase); `delete_branch_on_merge`
   on; Actions limited to GitHub-owned plus `amannn/action-semantic-pull-request@48f256284bd46cdaab1048c3721360e808335d50`
   (`wsl_care/.github/workflows/pr-title.yml:24`); default workflow token read-only; ruleset `run-main` on
   `refs/heads/run/*/main` (no deletion, no force-push, linear history, PR with 0 approvals and thread resolution, required
   checks `ci · build · test (ubuntu-24.04)`, family checks, PR title; bypass = the deploy key); a tag ruleset on
   `refs/tags/bench-template-*`; one deploy key with write, the rulesets' only bypass actor (its private half mode 600 in
   `~/.config/minigit-bench/`). The PAT already exists and was verified (D-facts of the brief: push to the fork, no workflow
   files, no other repository, public check runs read).
3. **Template contents** (parent §6 with these corrections): the four branches today carry `SPEC-v1.txt`, `SPEC-v2.txt` and the
   UPSTREAM test scripts (§2) — a v1 session would see the v2 SPEC and the unfixed tests. The template commit **removes** all
   four; per parent §6 they arrive per session in the run base's harness commit (fixed scripts from the `inputs` branch).
   `template/bare-*` keeps only its README (the bare workspace starts empty; the branch is the root of the later `bare/<rid>`
   pushes, §7.7). Process templates: `AGENTS.md` (from `dew_flow_vscode_kit/AGENTS.md`), `CLAUDE.md` (`@AGENTS.md`),
   `.gitmodules` (`.agents/conventions`, `branch = bench/minigit-2026-11`, the gitlink at that sha), `.claude/settings.json` and
   `.claude/hooks/*` byte-equal to the pinned `settings/`, `.gitattributes`, `.editorconfig`, `.gitignore`, `LICENSE`,
   `POST_DEPLOY.md`, `todo/README.md`, `research/README.md`, stub `research/architecture.md` and `research/module_tests.md`,
   `.github/workflows/{family-checks,pr-title}.yml` from `wsl_care` with `branches: [main, 'run/**']`
   (`wsl_care/.github/workflows/family-checks.yml:13-17`), `ci.yml` (required job + non-required acceptance job, parent §6),
   `.agents/PROJECT.md` with the identical precedence sentence; C#: `global.json` (10.0.113, `rollForward: disable`, MTP),
   `Directory.Build.props` (from `wsl_care`), `Directory.Build.rsp`, `Directory.Packages.props`, `nuget.config`, empty
   `minigit.slnx`, `scripts/run-tests.sh`; Node: `package.json`, lockfile, `tsconfig.json`, `eslint.config.mjs`,
   `scripts/run-tests.mjs`. No `.coderabbit.yaml`, no `coderabbit-review.yml` (D19).
4. **Lint gate** (decided by running, parent §6): `@typescript-eslint/parser` on `typescript@7.0.2`; if it fails, the npm
   `overrides` entry with a private 6.0.3 for the parser only. Gate: a planted violation in a `.ts` file → `npm run lint` red;
   a file with a type-only import, an annotation and `satisfies` → clean. C#: a planted analyzer violation → `dotnet build
   -c Release -m:4` red (warnings as errors).
5. **Tags**: annotated `bench-template-1/process-csharp`, `…/process-node`, `…/bare-csharp`, `…/bare-node`, one per branch tip
   (one tag cannot name four commits; parent §6's single `bench-template-1` came from the two-repository design). Shas and the
   identical-set digest into `treatment.md`.

**Test scenarios.**

| Test | Input | Expected |
|---|---|---|
| `Shared_files_are_byte_equal_across_stacks` | `build.mjs` output for both process stacks | `template-diff` clean; one shared file edited in one stack's copy → fails naming it |
| `Stack_files_differ_only_inside_their_markers` | `.agents/PROJECT.md`, `README.md`, `ci.yml` | outside the markers equal; a change outside → fails naming the line |
| `The_diff_scan_still_sees_a_marked_region` | the real `PROJECT.md` | at least one marked region found per stack file (alive) |
| `No_template_carries_a_task_input_or_a_reviewer_bot` | the four rendered trees | no `SPEC-v*.txt`, `test-v*.sh`, `.coderabbit.yaml`, `coderabbit-review.yml` |
| `The_settings_copies_equal_the_pinned_conventions` | `.claude/settings.json`, `.claude/hooks/*` vs `.agents/conventions/settings/` at the snapshot sha | byte-equal |
| `Workflows_trigger_on_run_bases` | parsed `family-checks.yml`, `pr-title.yml`, `ci.yml` | `run/**` in push and pull_request filters; acceptance job not in the required set |
| `The_gitmodules_follow_the_snapshot_branch` | `.gitmodules` | `branch = bench/minigit-2026-11` |
| `The_node_lint_parses_ts_on_typescript_7` (needs npm; local) | the rendered node tree, `npm ci` | planted violation red; TS-only syntax clean; `tsc --noEmit` on 7.0.2 |
| `The_csharp_build_fails_on_an_analyzer_warning` (needs dotnet; local) | the rendered C# tree with a planted warning | build exit ≠ 0 naming the rule; without it, exit 0 |
| `The_process_arm_gets_its_pat_only_through_env` | `ghEnv` + `agentEnv({ extra })` + `agentBoxArgv` | the PAT in no argv; `GH_TOKEN` in env; the bare arm's env has no `GH_*` |
| `The_session_gitconfig_authenticates_with_gh_and_a_fixed_author` | `gitconfigFor` | the helper line and the bench author; no `[include]` |

**Commands.** `node spikes/minigit-bench/run-tests.mjs test/template-diff.test.mjs test/templates.test.mjs
test/github-session.test.mjs`; `node spikes/minigit-bench/templates/build.mjs --out <scratch>`; `node
spikes/minigit-bench/template-diff.mjs <scratch>`; `gh api repos/oleksandrdubyna88/minigit-bench/{branches,rulesets,tags}`.

**Protections are read back, not assumed** (plan gate E2, 2026-10-29): after configuration the harness reads every
effective ruleset, its bypass actors, and the branch and tag targets back from the API and asserts them; before AND after
every session it re-reads the refs the session depends on (its `template/*` tag, the conventions snapshot sha, `inputs`)
and voids the session (`void: treatment moved`) if any differs.

**Done when.** The conventions plan merged there and the branch at the release sha with its ruleset; `pin-check` green for a
template checkout; the four template branches committed (inputs removed), tagged; `template-diff` clean and its output in
`treatment.md`; the lint and analyzer gates red on their plants and clean without; `ci · build · test` green on a push of each
process template branch; the `data` branch gone, the fork settings and rulesets as listed (read back with `gh api` into the
PR); `treatment.md` names each stack's strictness.

---

## 4. Build order inside the epic

S1 → S2 (S2 uses S1's `killTree` and the audit hook) → **the thin bare runner** (outside this document) and, in parallel, S3 →
S4. S3 needs S2's layout and box; S4 needs S2's secrets reader and `agentEnv({ extra })`, and S3 only for the process
template's settings (the `mcp__coai__*` allow). One branch per story off `feat/minigit-bench-e2`, merged in that order.

## 5. Test plan — ids by story

| Id | Story |
|---|---|
| T-P1, T-P2 | S1 |
| the box effect tests, `Setting_a_secret_with_setenv_…`, the read-back, watchdog and janitor tests, `The_real_bare_smoke_holds` | S2 |
| T-B1 and the two live coai tests | S3 |
| the template, lint and analyzer gates | S4 |

## 6. Growth

| Surface | Size | Retired by / interrupted |
|---|---|---|
| The smoke's out dir (`~/rsd/minigit/smoke/<ts>/`) | ≈ 2–5 MB | kept until the report; `retire.mjs` (E4); a crashed smoke leaves its dir, the janitor ends its processes |
| `<runs root>/host-allow.json` | < 1 KB | replaced by an explicit `--record-host-allow`; kept with the series |
| `~/rsd/minigit/toolchains/typescript-7.0.2/` | ≈ 25 MB | kept for the series; `retire.mjs` |
| `<s>/bridge.ndjson`, `<s>/coai/` | ≤ 20 MB per process session (parent §14) | with the session dir |
| Template branches, four tags, `run-main` and tag rulesets, the deploy key | — | branches, tags and rulesets kept (audit trail); the deploy key and the ruleset bypass torn down in E4 (parent §14) |
| `bench/minigit-2026-11` in `dew_flow_conventions` | one ref | kept as provenance |
| Vendor CLI stores written from coai's box (`~/.codex`, `~/.gemini/antigravity-cli`, `~/.local/share/coai-agy`) | a few MB per session | the operator's stores, not pruned (parent §14); growth per session measured in E3 |

## 7. Definition of Done

- [ ] E2.S1–E2.S4 merged; `BENCH_REQUIRE_SANDBOX=1 node spikes/minigit-bench/run-tests.mjs` green with no skip among the tests
      the done-whens name; `policy-ab-quick`'s suite with only the three named pre-existing failures.
- [ ] T-P1 and the shell scan observed RED, then green, both observations in the PRs; alert #8 fixed or dismissed with a reason,
      #9 with a reason, the count in the PR.
- [ ] The bare-session smoke ran ≤ $0.20 and its verdicts are committed; E1's counters read it.
- [ ] E2.S2's spec was rewritten by Fable before its code, and the summary says so (or that Opus stood in).
- [ ] The bridge refuses a `consult` on this repository while a document round runs with the sign-ins outside the agent box.
- [ ] The snapshot branch is in place with its boundary plan on the conventions side; the templates are tagged; `template-diff`
      clean; the lint gate red on its plant on typescript 7.0.2; `treatment.md` names each stack's strictness.
- [ ] `research/module_minigit_bench.md` describes the boxes, the bridge and the templates; the parent's E2 entry and ledger
      match this document; `todo/README.md` lists it.
