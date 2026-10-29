# PLAN — E2 stories: isolation and the treatment of the minigit stack bench (process fixes, the agent sandbox, coai's box and bridge, the templates)

> Status: **plan only, nothing implemented yet, 2026-10-29.** Scope: epic E2 of
> [PLAN_minigit_stack_bench.md](PLAN_minigit_stack_bench.md) (§15 steps 1, 4–6a) — fixes in `spikes/policy-ab-quick/`, new
> modules in `spikes/minigit-bench/`, the fork `oleksandrdubyna88/minigit-bench` (template branches, rulesets, settings) and
> one snapshot branch plus a short boundary plan in `dew_flow_conventions`. Four stories, E2.S1–E2.S4; cornerstone **E2.S2**
> (`model: fable`, the agent sandbox and launch — its text below is an Opus DRAFT for Fable to rewrite before any code); the
> parent's ledger reads `Cornerstones: 2 of 2 — E1.S3, E2.S2`. Money spent here, all pilot spend: the E2.S2 design run (2026-10-29,
> $0.112) and E2.S2's bare-session smoke (≤ $0.20); E2.S3's live check spends vendor quota, not the ceiling.

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
     (ONE source with E2.S2's `EGRESS_HOSTS`: S2 exports it, and S1 adopts it when S2 lands, instead of a second list).
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

## E2.S2 — the agent sandbox and the launch: a box with its OWN network namespace, egress through a host-side CONNECT proxy, argv, settings, env, credentials, watchdogs, janitor, read-back, the bare-session smoke

**Cornerstone — model: fable. Spec: Fable** (this text replaces Opus's draft of 2026-10-29, after the design was run on this machine;
the design run's figures are below). Area: isolation and treatment fidelity (parent `## Epics`, third-ranked; the one mark of E2).
It is the story every bare session rests on: the first paid cells run inside this box, so a hole here is a hole in every number.
Code Sonnet · review Opus.

**Goal.** One function builds the box a session runs in; one builds what is launched inside it; one launches, watches and
collects. Inside: Claude Code with the subscription token, effort `high`, an explicit allow-list, OTel to E1's receiver,
transcripts persisted — and no operator home, no `/mnt`, no `/run`, no host `/tmp`, no Windows interop, no `gh` in the bare arm,
no partner, no harness file it was not given, **and no network except the hosts the arm names**, reached through one door the
harness holds outside the box. Each barrier is OBSERVED on this machine with a control, never asserted from the argv
(`common.security` — *a measure you have not observed is a comment*). A bare-session smoke proves the whole path end to end and
that E1's counters read what it leaves.

### The network decision, and what was run to take it (2026-10-29, this machine, CLI 2.1.310)

**Adopted: the agent box gets its own network namespace (`--unshare-net`, the same `isolation()` the scoring box uses) and
egress ONLY through an HTTP `CONNECT` proxy the harness runs on the host, over a unix socket bound into the box.** Inside the box
a small supervisor listens on `127.0.0.1:3128` (proxy) and `127.0.0.1:4318` (OTel) and pipes each to its unix socket; `HTTPS_PROXY`,
`HTTP_PROXY` and `NO_PROXY=127.0.0.1,localhost` steer `claude`, `npm`, `dotnet` and `git`. The proxy allows `CONNECT <host>:443`
for a per-arm host list and refuses everything else with `403`, logging every request. A shared network namespace was rejected: a
box in the host's namespace reaches the host's loopback services (three were listening), every abstract unix socket, and any
host on the internet — including GitHub's `data` branches (D21) — and nothing bound or unbound changes that.

Every claim below was observed, not read from a manual. The paid run is one bare session, `--max-budget-usd 0.2`, prompt =
the smoke prompt below, out dir `~/rsd/minigit/smoke/20261029T215923Z-design/` (outside git; kept as evidence until the real
smoke's fixture replaces it): **$0.1119485, 13.7 s wall, exit 0, `num_turns` 3, two models**. Pilot spend (parent §13).

| # | Claim | How it was checked | Result |
|---|---|---|---|
| N-1 | With `--unshare-net` the box reaches no host loopback service, no abstract unix socket and no internet address | a host abstract listener and `127.0.0.1:35841`, `1.1.1.1:443` probed from inside; the same probes without `--unshare-net` as the control | isolated: `ECONNREFUSED`, `Connection refused`, `Network is unreachable`; control: all three reached |
| N-2 | A unix socket file bound into the box (read-only bind of its directory) is connectable across the namespace boundary | the proxy and OTel sockets lived in `<s>/net`, bound `--ro-bind`; the supervisor connected to both | both connected (the whole paid run went through them) |
| N-3 | `bwrap` brings `lo` up in the new namespace, so an in-box listener on `127.0.0.1` works | `ip link show lo` inside; the supervisor's two listeners | `LOOPBACK,UP,LOWER_UP`; both listeners accepted connections |
| N-4 | Claude Code 2.1.310 honours `HTTPS_PROXY` for the API | the paid run: the box had no other route; the proxy log | 7 `CONNECT api.anthropic.com:443` allowed; the session completed (`DONE`, cost in the envelope) |
| N-5 | The CLI's OTel exporter honours `NO_PROXY` and reaches E1's receiver through the in-box port → unix socket → host relay | the receiver's `liveSum()` after the run; the proxy log holds no `POST` | 5 `api_request` records, `usd` 0.1119485 = the envelope's `total_cost_usd`; `misconfigured` 0, `undecodable` 0; no non-CONNECT line at the proxy |
| N-6 | What else the CLI tries to reach | the refusal log of the paid run; a string scan of the binary (`api.anthropic.com`, `downloads.claude.ai`, `status.anthropic.com`, `o1158394.ingest.us.sentry.io`, `datadoghq`) | one refusal: `http-intake.logs.us5.datadoghq.com:443` — refused, and the session was unaffected. Sentry and `downloads.claude.ai` were not contacted in this run. So the bare list is **`api.anthropic.com` alone** for the CLI |
| N-7 | `npm install` works through the proxy inside the box | `npm install --prefix /tmp/ts typescript@7.0.2`, `HTTPS_PROXY` set, `--unshare-net` | 13 `CONNECT registry.npmjs.org:443`; `tsc --version` → 7.0.2, 2 s |
| N-8 | `dotnet restore` works through the proxy inside the box | a `net10.0` console project with `FluentAssertions 7.2.2`, `dotnet restore -m:4 --disable-build-servers` | 2 `CONNECT api.nuget.org:443`; restored in 2.2 s. dotnet also sent **58 plain-HTTP CRL/OCSP requests** (`crl3.digicert.com`, `ocsp.digicert.com`, `s.symcb.com`, …) which the proxy refused with `405`; the restore succeeded anyway |
| N-9 | The box has no DNS and needs none | `getent hosts api.anthropic.com`, `/etc/resolv.conf` inside (it links into the hidden `/mnt`) | `no-dns`, `absent`; every client sent the hostname to the proxy, which resolves on the host. The draft's `resolverBinds` is **dropped** |
| N-10 | The token is on no process listing while the session runs | every `/proc/*/cmdline` read once a second during the paid run, compared with the token's value in memory | 10 samples, 0 hits (the token travelled in the spawn `env`, never `--setenv`) |
| N-11 | Nothing survives the box | after exit + 3 s, every `/proc/*/environ` scanned for `BENCH_CELL=<cell>` | none |
| N-12 | Hooks run inside the box, persistence is on, the subagent tool works | `hooks.ndjson` in `box/out`; `box/config/projects/**`; the stream | 8 hook records over `PreToolUse`, `PostToolUse`, `SubagentStart`, `SubagentStop` (the last two carry `agent_id`); a main transcript and `subagents/agent-a0dd13317fcbe4120.jsonl`; one `Agent` call, two `Bash` calls |
| N-13 | Effort of a CHILD is readable | OTel `api_request` attributes per record | `effort: "high"` on every record, main (`query_source: sdk`) and child (`agent:builtin:general-purpose`, `agent_id` set); `subagent_completed` names the child's model. **B-A3 is answered by O, not by the audit** |
| N-14 | The init read-back | the stream's first line | `cwd` = `box/work`; `mcp_servers: []`; `permissionMode: default`; `apiKeySource: none`; `claude_code_version: 2.1.310`; `memory_paths.auto` under `box/config`; `messaging_socket_path: /tmp/cc-socks/9.sock` (the box's own `/tmp`, pid 9 of the pid namespace); **`tools` = `Task, Bash, Edit, Glob, Grep, Read, Write` — `TodoWrite` was passed in `--tools` and is not reported** |
| N-15 | The bare box's other barriers, no model | probes in a box built as below | `gh --version` → `Permission denied` (`/dev/null` over `/usr/bin/gh`); `/mnt` empty; `/run/WSL` absent; `WSL_INTEROP` empty; a copy of `/mnt/c/Windows/System32/whoami.exe` executed in `box/work` → the WSL interop handler starts and dies at `UtilGetPpid` on `/proc/1/stat` (pid 1 is `bwrap`), **no Windows process, no name printed** (control outside: `jinx\strug`); the operator home shows only the bound dirs, the token file is absent; `tsc` 7.0.2 from the read-only toolchain; `touch` into the toolchain → `Read-only file system`; `node t.ts` strips types; `dotnet --list-sdks` → `10.0.113` |
| N-16 | The TypeScript toolchain exists | `npm install --prefix ~/rsd/minigit/toolchains/typescript-7.0.2 typescript@7.0.2`, then `chmod -R a-w` (no sudo) | installed, 31 MB, `tsc` 7.0.2; it is what N-15 bound |

**Not set, deliberately:** `CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC`. The allow-list already fails nonessential traffic closed and
the refusal log says what was tried; a second switch on the same thing would also change CLI behaviour the capture ran without.
`CLAUDE_CODE_PROXY_RESOLVES_HOSTS` exists in the binary and is not needed (N-9).

### Design decisions (N-* replace the draft's X-*)

| # | Decision | Why |
|---|---|---|
| D-1 | `hiddenPaths({ operatorHome, exists })` is extracted from `scoreBoxArgv` and both boxes call it: `[operatorHome, /mnt, /run, /tmp, /var/tmp]` | one hide list; parent §7.1; E1's bus escape and `/mnt/c` |
| D-2 | **Both boxes use the unchanged `isolation()`** (`--unshare-net --unshare-pid --die-with-parent --new-session`); the agent box adds `--unshare-ipc`. No `resolverBinds`, no host-listener allow-list, no `host-allow.json`: with its own namespace the box cannot reach a host listener, so there is nothing to compare | N-1, N-9; a primitive reused rather than widened |
| D-3 | **Egress = `egressProxy`** on the host, in the harness process: listens on `<s>/net/proxy.sock` (mode 600), forwards only `CONNECT <host>:443` for `host ∈ EGRESS_HOSTS[arm]` (exact names, no suffixes), answers `403` otherwise and `405` to any non-CONNECT line, and logs every request as `{ t, verdict, host, port, line? }` to `<s>/egress.ndjson`. `EGRESS_HOSTS.bare = ['api.anthropic.com', 'registry.npmjs.org', 'api.nuget.org']` (N-6, N-7, N-8). `EGRESS_HOSTS.process` is **S4's to settle in P0** (`github.com`, `api.github.com`, `codeload.github.com` are the expected three; `raw.githubusercontent.com` is never in it — S1's contamination rule names it) | prevention by host, not audit (D21): in the bare arm GitHub and every other host are unreachable by construction |
| D-4 | **The box's supervisor** `box-init.mjs` (one file, node builtins only, copied into `<s>/harness/` like the audit hook) is what `bwrap` runs: it listens on `127.0.0.1:BOX_PROXY_PORT` (3128) → `<s>/net/proxy.sock` and `127.0.0.1:BOX_OTEL_PORT` (4318) → `<s>/net/otel.sock`, then spawns `claude` with inherited stdio and exits with its code. The ports are constants of the box's own namespace | N-3; exe + argv, no shell inside the box either |
| D-5 | **The receiver listens on a unix socket too:** `startReceiver({ dir, port, socketPath })` — when `socketPath` is given the same `http.Server` also listens there (mode 600). `otelEnv(BOX_OTEL_PORT)` is unchanged (its `127.0.0.1:4318` is the box's loopback) | N-5; widening E1's primitive instead of a relay process (the design run used a relay; the receiver's own listener is one hop fewer) |
| D-6 | **Bind an explicit list, never the session dir.** rw: `box/work` (cwd), `box/home` (HOME), `box/config` (`CLAUDE_CONFIG_DIR`), `box/tmp` at `/tmp` (`TMPDIR=/tmp`), `box/nuget`, `box/npm`, `box/out`; ro: `harness/` (settings, mcp.json, `audit.mjs`, `box-init.mjs`), `net/` (the two sockets), the `claude` binary FILE, the node dir, the TypeScript toolchain dir; the bare arm `--ro-bind /dev/null /usr/bin/gh`. `extraRo` / `extraRw` are S3's and S4's slots | the session dir also holds the stream, OTel, transcripts, `egress.ndjson`, `coai/` (S3) and `score/`: an agent that reads its own instruments is a contamination path; N-15 |
| D-7 | **Secrets in the spawn `env` only.** `bwrap` is spawned with `env = agentEnv(…)` and no `--clearenv`/`--setenv` at all: bwrap hands its environment to the child unchanged, so every variable travels the same way and none is on argv | N-10; `--setenv` lands in `/proc/<pid>/cmdline`, readable by every user |
| D-8 | `agentEnv` is a fixed allow-list; `PATH` is constructed (`<node>/bin:<ts>/node_modules/.bin:/usr/local/bin:/usr/bin:/bin`), so no `/mnt` entry and no `WSL_INTEROP` can exist; `CLAUDE_CODE_ENTRYPOINT` not set; the proxy triple set | parent §7.1; N-15 |
| D-9 | The CLI is the resolved file `~/.local/share/claude/versions/2.1.310` (realpath of `~/.local/bin/claude`), run by absolute path; `inputs/stacks.json` pins `claude` 2.1.310, node 26.12.1, dotnet SDK 10.0.113, typescript 7.0.2 and how each is probed | V4 |
| D-10 | TypeScript 7.0.2 lives read-only in `~/rsd/minigit/toolchains/typescript-7.0.2/`; **the preflight installs it when absent** (`npm install --prefix <dir> typescript@7.0.2`, then `chmod -R a-w`, no sudo) and refuses to start if `tsc --version` from inside a box is not 7.0.2 | N-16 |
| D-11 | Settings: `PINNED_EFFORT` imported from `settings.mjs:27-30`; `permissions.allow` per arm (`BARE_ALLOW = ['Bash', 'Read', 'Edit', 'Write', 'Glob', 'Grep', 'TodoWrite', 'Agent', 'Task']` — plain `Bash`, because a bare session runs arbitrary build commands and nobody answers a prompt); hooks `PreToolUse`, `PostToolUse`, `PostToolUseFailure`, `SubagentStart`, `SubagentStop` → `node <s>/harness/audit.mjs --out <s>/box/out/audit.ndjson`; no `defaultMode`, never `bypassPermissions`. `mcp.json = { mcpServers: {} }` (bare) | D11, D17; N-12 (four of the five events observed firing; `PostToolUseFailure` registered, not exercised) |
| D-12 | argv: `-p --model opus --output-format stream-json --verbose --forward-subagent-text --include-hook-events --setting-sources project --settings <s>/harness/settings.json --strict-mcp-config --mcp-config <s>/harness/mcp.json --max-budget-usd <cap> --tools Agent,Bash,Read,Edit,Write,Glob,Grep,TodoWrite`; prompt on stdin; persistence ON. `BARE_TOOLS` is that list; `ToolSearch` and `mcp__coai__*` join in S3 | parent §8; N-12, N-14 |
| D-13 | Read-back tolerates exactly `INIT_UNREPORTED = ['TodoWrite']` (dated 2.1.310) missing from `init.tools`; everything else in `--tools` must be reported, with `Agent` ≡ `Task` | N-14; whether the model can call `TodoWrite` is not exercised and the smoke says so (B-R2) |
| D-14 | Kill = S1's `killTree` on the detached `bwrap` pid; `--die-with-parent` and the pid namespace end the rest; the janitor then ends any host process whose `/proc/<pid>/environ` holds `BENCH_CELL=<cell>` exactly — never by name | parent §8; N-11 |
| D-15 | Children's effort comes from O (`api_request.effort` per `agent_id`), the audit's `SubagentStart` joins `agent_id` to the `Agent` call's `tool_use_id`; D17's "read back from the audit" is amended to "read back from O, joined through the audit" | N-13 |

### Files

| Path | Change |
|---|---|
| `spikes/minigit-bench/lib/bwrap.mjs` | `hiddenPaths({ operatorHome, exists })` extracted; `scoreBoxArgv` calls it (its argv unchanged — E1's tests prove it); `isolation()` unchanged |
| `spikes/minigit-bench/lib/sandbox.mjs` | new — `sessionLayout(sessionDir)`, `agentBoxArgv({ layout, arm, toolchains, claudeBinary, nodeDir, extraRo = [], extraRw = [] })`, `agentEnv({ layout, token, arm, cellId, extra = {} })`, `AGENT_ENV_KEYS` (the one literal key list), `BOX_PROXY_PORT`, `BOX_OTEL_PORT`, `EGRESS_HOSTS`, `INIT_UNREPORTED` |
| `spikes/minigit-bench/lib/egress.mjs` | new — pure `connectDecision(requestLine, allow)` → `{ allow: true, host, port } \| { allow: false, status: 403 \| 405, why }`; `egressProxy({ socketPath, allow, log })` (the `net` server; the one I/O here); `egressSummary(lines)` → `{ allowed: {host: n}, refused: {host: n}, nonConnect: n }` |
| `spikes/minigit-bench/harness/box-init.mjs` | new — the in-box supervisor (D-4); copied into `<s>/harness/` by `launchSession` |
| `spikes/minigit-bench/lib/launch.mjs` | new — `claudeArgv({ layout, cap, tools })`, `settingsFor({ arm, layout })`, `mcpConfigFor(arm)`, `BARE_TOOLS`, `BARE_ALLOW`, `HOOK_EVENTS` |
| `spikes/minigit-bench/lib/secrets.mjs` | new — `readSecretFile({ home, parts })`, `secretStatus({ home, parts })`, extracted from `capture/capture-env.mjs:15-34` (which imports them; `TOKEN_FILE_PARTS` stays there); `PAT_FILE_PARTS = ['.config', 'minigit-bench', 'github-pat']` for S4 |
| `spikes/minigit-bench/lib/otel-receiver.mjs` | `startReceiver({ dir, port, socketPath })` — the optional unix listener (D-5) |
| `spikes/minigit-bench/lib/host-checks.mjs` | new — `toolchainFacts(probes, pins)` against `inputs/stacks.json`; `toolchainInstallPlan({ dir, exists })` → the npm argv and the chmod when absent, `null` when present; `bwrapFacts(probes)` |
| `spikes/minigit-bench/lib/watchdogs.mjs` | new — pure `watchdogDecision({ now, startedAt, lastStreamAt, lastOtelAt, cpuTicks, prevCpuTicks, liveUsd, meterUsd, capUsd, wallMs, stallMs })` → `{ kill: false } \| { kill: true, reason: 'wall' \| 'cost' \| 'stall' }` |
| `spikes/minigit-bench/lib/janitor.mjs` | new — `cellProcesses(cellId, procApi)` (NUL-split `environ`, exact `BENCH_CELL=<id>`; unreadable entries skipped and counted), `cpuTicksOf(pids, procApi)`, `endSurvivors(cellId, { killTree })` |
| `spikes/minigit-bench/lib/readback.mjs` | new — `readBackInit(init, expected)` → `{ ok: true } \| { ok: false, mismatches: [{ field, expected, observed }] }` |
| `spikes/minigit-bench/lib/transcripts.mjs` | new — `walkJsonl`, `copyTranscripts(fromConfigDir, toDir)` moved out of `capture/capture-run.mjs:34-66` (which imports them) |
| `spikes/minigit-bench/lib/session-launch.mjs` | new — `launchSession(plan, deps)`: the receiver with its socket, the proxy, the copies into `harness/`, `bwrap` spawned detached with `env`, every stream line stamped `{ t, line }` on arrival (`mapClock`, `timeline.mjs:160`) into `stream.stamped.ndjson` beside the raw `stream.ndjson`, the init read-back, the watchdogs every 5 s, the token sampler every 1 s, kill, 3 s for the last OTel batch, close, janitor, transcripts copy, `argv.json` (bwrap argv, claude argv, env KEYS, sha256 of settings and mcp.json), `egress.json` (`egressSummary`), `meta.json` |
| `spikes/minigit-bench/smoke-session.mjs` | new — CLI: `--out <dir> [--dry-run]`; exit 0 · 3 environment · 4 usage · 5 failed (the capture's contract) |
| `spikes/minigit-bench/inputs/stacks.json` | new — the pins and their probes |
| `spikes/minigit-bench/test/{agent-box,egress,box-init,launch,secrets,host-checks,watchdogs,janitor,readback,session-launch}.test.mjs` | new |
| `spikes/minigit-bench/test/fixtures/real/claude-2.1.310-bare-smoke/` | new — the sanitised smoke (E1's `sanitiseCapture`, `capture/capture-files.mjs:53`) with `SMOKE.md` |
| `research/module_minigit_bench.md` | the agent box, the egress proxy, the layout, the facts N-1…N-16 and the smoke; "What it does not do yet" updated |

### The session layout (outside every git tree, under the runs root `~/rsd/minigit/<series>/<rid>/`)

```
<rid>/                     harness-only unless listed as bound
  harness/                 ro in the box: settings.json, mcp.json, audit.mjs, box-init.mjs
  net/                     ro in the box: proxy.sock, otel.sock (both mode 600, created by the harness)
  box/work/                rw, the agent's cwd (bare: empty for the smoke; the runner fills it)
  box/home/  box/config/  box/tmp/  box/nuget/  box/npm/  box/out/      rw
  stream.ndjson  stream.stamped.ndjson  stderr.txt  otel/  egress.ndjson  egress.json  transcripts/
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
   + `--unshare-ipc` + `--chdir <box/work>` + `--` + `<node> <s>/harness/box-init.mjs 3128:<s>/net/proxy.sock 4318:<s>/net/otel.sock
   -- <claude> <claudeArgv>`. No `--setenv`, no `--clearenv`.
2. **Env** (`agentEnv`): `PATH` (D-8), `HOME`, `TMPDIR=/tmp`, `LANG=C.UTF-8`, `CLAUDE_CODE_OAUTH_TOKEN`, `CLAUDE_CONFIG_DIR`,
   `CLAUDE_CODE_DISABLE_BACKGROUND_TASKS=1`, `DISABLE_AUTOUPDATER=1`, `...otelEnv(BOX_OTEL_PORT)` (its `OTEL_LOG_TOOL_DETAILS=1`,
   `OTEL_LOG_USER_PROMPTS=0`), `HTTPS_PROXY=HTTP_PROXY=http://127.0.0.1:3128`, `NO_PROXY=127.0.0.1,localhost`, `DOTNET_CLI_HOME`,
   `DOTNET_NOLOGO=1`, `DOTNET_CLI_TELEMETRY_OPTOUT=1`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`, `MSBUILDDISABLENODEREUSE=1`,
   `UseSharedCompilation=false`, `NUGET_PACKAGES`, `npm_config_cache`, `BENCH_CELL=<cellId>`, `GIT_CONFIG_GLOBAL=<s>/harness/gitconfig`
   (empty file in the bare arm), `...FIXED_GIT` (S1). `extra` is S4's slot (`GH_TOKEN`, `GH_CONFIG_DIR`, `GH_PROMPT_DISABLED`). The
   key set equals `AGENT_ENV_KEYS` (plus `extra`'s keys), the one literal list; a test diffs them.
3. **Egress** (`egress.mjs`): `connectDecision` parses the first line only (`CONNECT host:port HTTP/1.1`); exact host match, port
   `443`; a header block over 64 KiB closes the connection. The proxy connects upstream only after the decision and replies
   `200 Connection Established`; an upstream error is logged `{ verdict: 'upstream-error', code }`. The log is append-only, one
   JSON line per request. Nothing in the log is a secret: the proxy never sees inside TLS.
4. **Preflight** (`host-checks.mjs`, called by `smoke-session.mjs` and later by E3's `preflight.mjs`): `bwrap` present and
   `unshare -Ur true` ok; the toolchain install plan executed when the dir is absent (D-10); then, **inside a box built by
   `agentBoxArgv`** with no proxy running (so the probes cannot reach anything): `claude --version` 2.1.310, `node --version`
   26.12.1, `dotnet --list-sdks` exactly one line `10.0.113`, `tsc --version` 7.0.2 — each against `stacks.json`; the token file's
   status (`secretStatus`), and in the process arm the PAT file's. A listener scan is no longer part of the preflight (D-2).
5. **Launch, read-back, watchdogs, collect** (`launchSession`): as in the Files table. Read-back on the init event, before the
   first paid turn completes: `cwd` = `box/work`; `tools` = `--tools` with `Agent`≡`Task`, minus `INIT_UNREPORTED`, nothing else;
   `mcp_servers = []` (bare); `permissionMode = default`; `claude_code_version` = `stacks.json`; `apiKeySource = none`;
   `memory_paths.auto` under `CLAUDE_CONFIG_DIR`; `model` an id `prices-*.json` prices. A mismatch kills at once; the outcome is
   `void: harness` with `readback.json` naming the field. Watchdogs: wall; cost (`liveSum().usd ≥ 1.1 × cap`, else the stream
   meter `streamMeterUsd`, `claude-usage.mjs:115`, at published prices); stall (no stream line, no OTel record and no CPU-tick
   growth of the cell's processes for `stallMs`); each decision is a line in `watchdog.ndjson`. The token sampler reads every
   `/proc/*/cmdline` once a second and counts matches of the token's value; the count goes to `meta.json`, the value nowhere.
6. **Smoke** (`smoke-session.mjs`): out dir outside every git tree (E1's `outProblems`, `capture-env.mjs:57`); preflight; token
   from the file; layout; `--max-budget-usd 0.2`; wall 5 min; `stallMs` 2 min; prompt: *"Launch one subagent with the Agent tool
   (`model: "sonnet"`, subagent_type general-purpose) told to run `ls -A` in the current directory and reply with what it printed.
   When it has returned, run `echo smoke-ok` with Bash yourself and reply DONE."* — `box/work` holds `a.txt` and `b.txt`. Then,
   over the out dir, E1's instruments: `envelopeView`, `otelView`, `transcriptView`, `crosscheck` (T1–T4), `sessionCost`,
   `aliasRecord`, `spansFrom` + `partition`; S1's `contaminationHits` and `tokenHits`; `egressSummary`. `SMOKE.md` is computed,
   never typed (the `verdicts` pattern).

### Smoke verdicts (computed into `SMOKE.md`; `holds` / `refuted` / `not exercised`, none blank)

| Id | Assumption | Holds → | Refuted → |
|---|---|---|---|
| B-R1 | the init read-back matches every field of step 5 | the runner may rely on it | fixed before any bare cell |
| B-R2 | `init.tools` lacks exactly `INIT_UNREPORTED` | the tolerance stays as dated | the list is corrected and dated again |
| B-N1 | every allowed CONNECT names a host of `EGRESS_HOSTS.bare`; every refused host is in `KNOWN_CLI_TELEMETRY` (`http-intake.logs.us5.datadoghq.com` today) or is named in the report | the list stands | a new host is classified (CLI telemetry → added to the known list; needed → added to the arm list with a reason) before any cell |
| B-N2 | the proxy log holds no non-CONNECT line from the CLI (a plain-HTTP reach would mean `NO_PROXY` or the exporter changed) | — | investigated |
| B-O1 | OTel records reached the receiver through the socket; T2 passes | the cost watchdog reads the live sum | no paid cell runs until fixed (parent §9's last fallback) |
| B-T1 | `box/config/projects/**` holds main and one subagent transcript; T3 and T4 pass under `crosscheck-policy.json` | T's coverage as in E1 | per E1 amendment (2): unchecked, investigated |
| B-E1 | an envelope; T1 passes; cost ≤ $0.20 | — | stop |
| B-A1 | audit Pre/Post pairs by `tool_use_id`; `partition` Σ = wall to the ms; `unpaired` empty | — | fix S1/S2 |
| B-A2 | the `PostToolUse` payload carries `duration_ms` | `duration_ms` recorded | spans from Pre/Post `ts` only (E1's method); O's `tool_result.duration_ms` (a string, A-O1) beside it — recorded |
| B-A3 | every O `api_request` carries `effort`, and the child's (by `agent_id`) is `high`; `SubagentStart` carries the same `agent_id` | D17 read back from O through the audit join | children's effort `unknown` in the report; recorded as a limit |
| B-H1 | hook events still carry no timestamp (E1 A-H1) | `HOOK_TIME_SOURCE` stays `arrival` | E1's constant is revisited |
| B-K1 | after exit, no host process carries `BENCH_CELL=<cell>`; the janitor ended 0 | — | find the escape before any cell |
| B-S1 | the token's value was in no `/proc/*/cmdline` sample (≥ 5 samples), and in no file the sanitiser saw | — | stop |
| B-C1 | `contaminationHits` and `tokenHits` over the smoke: none | — | read by hand |

### Test scenarios

Effect tests run the real `bwrap` (`sandboxGate`, E1 `test/helpers.mjs:48`) with E1's `probe`/`withoutPair` shape: every "cannot"
has a control that shows the same probe CAN without the barrier. The hide list is iterated from `hiddenPaths()`, never retyped.
A test that applies to both boxes is written once over `[scoreBoxArgv, agentBoxArgv]`. Egress tests start a real `egressProxy`
on a socket under a `testsToDelete-mgb-…` dir with a SHORT path, and a real `box-init.mjs` inside the box. The proxy forwards
only to port 443, so the positive control of an allowed host is `api.nuget.org:443` (a real host, no auth, no cost; the test
skips with its reason when the host itself has no network).

| Test | Input | Expected |
|---|---|---|
| `Both_boxes_hide_the_same_paths_and_isolate_the_same_way` | `hiddenPaths()`; both argvs | each hidden path appears as `--tmpfs` in both; both carry `isolation()`'s four flags; `scoreBoxArgv`'s argv equals E1's (`bwrap.test.mjs:62`'s assertions still green) |
| `The_agent_box_reaches_no_host_loopback_abstract_socket_or_address` | a host abstract listener, a host `127.0.0.1` TCP listener, `1.1.1.1:443`; probed inside the agent box and in the same argv `withoutPair('--unshare-net')` | isolated: all three refused/unreachable; control: all three reached (N-1) |
| `A_bound_unix_socket_crosses_the_namespace_through_a_read_only_bind` | a host listener on `<tmp>/net/x.sock`, `net/` bound ro; `box-init.mjs 5000:<sock> -- sh -c 'exec 3<>/dev/tcp/127.0.0.1/5000'` | connected and answered (N-2, N-3); control: without the bind, `ENOENT` |
| `The_proxy_allows_only_the_listed_hosts_on_443` (effect) | `egressProxy` with `['api.nuget.org']`; from inside a box through `box-init`: `curl -s -o /dev/null -w %{http_code} https://api.nuget.org/v3/index.json`, then `https://example.com/`, then `http://127.0.0.1:3128/` with a `GET` | `200`; curl exit 7 with a `403` line in the log; `405` logged as `nonConnect`; `egressSummary` → `{ allowed: { 'api.nuget.org': 1 }, refused: { 'example.com': 1 }, nonConnect: 1 }` |
| `The_connect_decision_is_exact_and_deny_by_default` (pure, iterated over a table) | `CONNECT api.nuget.org:443`, `CONNECT api.nuget.org:80`, `CONNECT evil.api.nuget.org:443`, `CONNECT API.NUGET.ORG:443`, `GET http://x/ HTTP/1.1`, an empty line, a 70 KiB line | only the first allowed; port → 403; suffix → 403; case → 403; GET → 405; empty → 405; oversize → closed |
| `Npm_and_dotnet_restore_through_the_proxy_from_inside_the_box` (effect, needs network; skip with reason) | the N-7 and N-8 probes with `EGRESS_HOSTS.bare` | `tsc` 7.0.2 installed under `/tmp`; `dotnet restore` exit 0; the log names only `registry.npmjs.org` and `api.nuget.org` as allowed; the dotnet CRL lines are `nonConnect` and the restore still succeeded (the refuted-looking shape, recorded) |
| `The_box_has_no_dns_and_that_is_the_point` | `getent hosts api.anthropic.com` inside; outside | inside fails; control outside resolves (N-9) |
| `The_agent_box_has_no_operator_home_and_no_key` | decoy home with a decoy `heldout.key` and decoy `claude-oauth-token` (E1's shape, `bwrap.test.mjs:113`); the real home | empty; unreadable; control: readable outside |
| `The_agent_box_has_no_windows_drive_and_no_interop` | `ls -A /mnt`; `echo $WSL_INTEROP`; `ls /run/WSL`; a COPY of `/mnt/c/Windows/System32/whoami.exe` in `box/work`, executed | empty; empty; absent; the exe prints no `\` user name and exits non-zero (N-15: the interop handler dies on `/proc/1/stat`). Control: the same copy outside prints a name; skip with reason when `/mnt/c` is absent |
| `The_agent_box_cannot_reach_the_session_bus_or_the_user_manager` | E1's two tests (`:155`, `:172`) over the agent argv | as E1, with their controls |
| `The_agent_box_sees_only_its_own_tmp` | E1's `:182` over the agent argv; plus `/tmp` shows `box/tmp`'s content | as E1 |
| `The_agent_box_cannot_see_its_instruments_or_its_partner` | a second session's layout beside it; probes for `<s>/stream.ndjson`, `<s>/otel`, `<s>/egress.ndjson`, `<s>/score`, `<s>/coai`, `<partner>/box/work/x`, `~/rsd/minigit/score-tmp` | none visible; control: `box/work` and `harness/settings.json` visible; `touch` in `harness/` and in `net/` fails |
| `The_bare_box_has_no_gh` | `gh --version`; `/usr/bin/gh --version` inside | both `Permission denied`; control: `/usr/bin/gh --version` outside works |
| `The_toolchain_is_read_only_and_tsc_is_the_pinned_one` | the real toolchain dir bound ro; `tsc --version`; `touch <dir>/x` | `7.0.2`; `Read-only file system`; skip with reason when the dir is absent (the preflight installs it) |
| `The_token_is_on_no_process_listing` | box started with a FAKE token `sk-ant-fake-<rand>` in env running `sleep 2`; the host scans every `/proc/*/cmdline` meanwhile | not found; inside the box `printenv CLAUDE_CODE_OAUTH_TOKEN` prints it (positive) |
| `Setting_a_secret_with_setenv_would_list_it_and_that_is_the_point` | the same with the value passed by `--setenv` | found in bwrap's cmdline — the refuted approach, shaped |
| `The_env_is_the_allow_list_and_its_path_has_no_mnt` | `agentEnv` with a parent env holding `GH_TOKEN`, `WSL_INTEROP`, `/mnt/c/x` on PATH | key set equals `AGENT_ENV_KEYS`; no `/mnt` PATH entry; bare: no `GH_*`; `NO_PROXY` names `127.0.0.1` |
| `The_box_argv_carries_no_setenv_and_no_secret` | `agentBoxArgv` + an env with a fake token | no `--setenv`, no `--clearenv`; the token's value in no element |
| `Box_init_runs_the_command_and_returns_its_code` | `box-init.mjs` with no forwarders and `-- sh -c 'exit 7'`; with a forwarder spec whose port is taken | exit 7; a listen failure exits non-zero before spawning (the command never ran) |
| `Killing_bwrap_leaves_no_cell_process` | box runs `sh -c 'sleep 300 & sleep 300'` with `BENCH_CELL=<rand>`; `killTree(bwrapPid)` | within 3 s `cellProcesses` finds none; control: before the kill it finds ≥ 2 |
| `The_janitor_ends_a_survivor_by_its_marker_never_by_name` | a host process outside any box with `BENCH_CELL=<rand>` and one named `claude` without it | the first ended, the second untouched |
| `The_toolchain_is_probed_inside_the_box_and_installed_when_absent` | fake probe outputs: two SDK lines; node 25; tsc 6; `toolchainInstallPlan` with `exists` false/true | each refused naming the pin; the plan is the npm argv + chmod, or `null` |
| `Watchdogs_kill_for_wall_cost_and_stall_and_only_then` | a table of states, including cost from the meter when OTel is silent, and stall reset by CPU growth alone | the one expected decision per row |
| `A_readback_mismatch_kills_before_the_work` | init fixtures: an `mcp_servers` entry in bare; `tools` missing `Bash`; version 2.1.311; `Task` instead of `Agent`; `TodoWrite` absent; `TodoWrite` AND `Grep` absent | the first three `ok: false` naming the field; the fourth and fifth `ok`; the sixth `ok: false` naming `Grep` |
| `The_settings_pin_effort_and_never_bypass` | `settingsFor('bare', …)` | `effortLevel` and the three `modelSettings` `high` (equal to `PINNED_EFFORT`); `permissions.allow` = `BARE_ALLOW`; no `defaultMode`; five hook events pointing at the harness copy with `--out` |
| `The_argv_persists_the_session_and_names_no_secret` | `claudeArgv` | no `--no-session-persistence`; `--strict-mcp-config`; the prompt not in argv; the token's value nowhere |
| `The_receiver_also_listens_on_a_socket` | `startReceiver({ socketPath })`; a POST of a real OTLP body through the socket and one through the port | both land in `otel.ndjson`; the socket is mode 600; `close()` removes it |
| `The_secrets_reader_is_the_captures` | modes 644 file, 755 dir, a link, empty; `CLAUDE_CODE_OAUTH_TOKEN` set in env with no file | each refused naming why; the env is ignored; `capture-env.mjs` imports it (E1's capture tests stay green) |
| `The_launch_stamps_every_line_and_collects_what_E1_reads` (stub `claude` = a script printing the capture's `stream.ndjson`, POSTing one OTLP body to `OTEL_EXPORTER_OTLP_ENDPOINT` and writing a transcript under `CLAUDE_CONFIG_DIR`, inside a real box) | `launchSession` | `stream.stamped.ndjson` has one `{ t, line }` per line, `t` increasing; the OTel body arrived through the socket; transcripts copied; `argv.json` carries env KEYS only; `egress.json` written; receiver closed; janitor ran; the sockets removed |
| `The_real_bare_smoke_holds` (needs the token; local only) | the committed `claude-2.1.310-bare-smoke` fixture | `SMOKE.md` equals `renderVerdicts` of the fixture; B-R1, B-R2, B-N1, B-N2, B-O1, B-T1, B-E1, B-A1, B-A3, B-K1, B-S1, B-C1 `holds`; leak scan clean |

### Extension points for S3 and S4 (process arm)

- `agentBoxArgv({ extraRo, extraRw })` and `agentEnv({ extra })` — S3 adds `<s>/bridge` (ro) and the `mcp.json` with the bridge
  client; S4 adds `GH_*` and the gitconfig with the credential helper.
- `EGRESS_HOSTS.process` — S4 settles it in P0 with the refusal log as the evidence (D-3); the proxy itself does not change.
- `box-init.mjs` takes any number of `<port>:<socket>` pairs — S3's bridge socket is reached the same way if it ever needs a
  TCP face; today it is a unix socket the MCP client script opens directly.
- `settingsFor('process', …)` adds `mcp__coai__*` and `ToolSearch` to the allow; `claudeArgv({ tools })` adds `ToolSearch`.
- The coai box of S3 is written as "network shared"; the leak N-1 names (host loopback, abstract sockets, any host) applies to
  it too. S3 should put coai in its own namespace with this proxy and a vendor host list — a decision for S3's spec, named here.

### Commands

`node spikes/minigit-bench/run-tests.mjs test/agent-box.test.mjs test/egress.test.mjs test/box-init.test.mjs test/launch.test.mjs
test/secrets.test.mjs test/host-checks.test.mjs test/watchdogs.test.mjs test/janitor.test.mjs test/readback.test.mjs
test/session-launch.test.mjs`; `node spikes/minigit-bench/smoke-session.mjs --out ~/rsd/minigit/smoke/$(date -u +%Y%m%dT%H%M%SZ)`
(it installs the toolchain when absent — it is present since 2026-10-29) and the sanitise step into the fixture;
`BENCH_REQUIRE_SANDBOX=1 node spikes/minigit-bench/run-tests.mjs` (the whole spike).

### Done when

- Every effect test green with none skipped on this machine (`BENCH_REQUIRE_SANDBOX=1`), each "cannot" beside its control, the
  egress tests included.
- The bare-session smoke ran once on CLI 2.1.310 through the proxy, cost ≤ $0.20 (pilot spend, beside the design run's $0.1119);
  the sanitised fixture and `SMOKE.md` committed; every B-* verdict computed, the required ones `holds`; B-A2 recorded either way.
  E1's counters read the smoke: T1–T4 as the policy says, `sessionCost` known, `partition` Σ = wall.
- `scoreBoxArgv`'s argv unchanged (E1's tests green); the capture's tests green after the extractions; `startReceiver` without
  `socketPath` behaves as before.
- `research/module_minigit_bench.md` describes the agent box, the egress proxy, the layout and the smoke's verdicts, and the
  facts table N-1…N-16 with their date.
- The summary names who wrote the spec (Fable), the coder and the reviewer.

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
   binaries and the uv venv (realpaths); rw: `<s>/coai` (`COAI_DATA_DIR`), `~/.codex`, `~/.gemini/antigravity-cli`,
   `~/.local/share/coai-agy`, `<s>/coai/tmp` at `/tmp`; network: its OWN namespace with E2.S2's proxy and a vendor host list
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
     array — must be relative, contain no `..` segment after normalisation, and when they exist resolve (realpath) inside
     `repoPath`; a symlink leading out is refused;
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
