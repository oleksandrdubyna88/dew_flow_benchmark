# Scout stream fixtures — LIVE, claude 2.1.289 (2026-10-12)

These files pin the readers of the scouts context — `ClaudeSubagents` (`src/Bench.Domain/Probes/ClaudeSubagents.cs`), the
read-back and its path audit (`src/Bench.Domain/Scouts/ScoutReadBack.cs`, `PathAudit.cs`) and the token attribution
(`ScoutCost.cs`) — on streams the real CLI printed for plan `todo/PLAN_scout_model_calibration.md` (§2's feasibility probe and
§3.2's three confinement probes). **Every file is LIVE**; the only edits are SCRUBS, made before the files were committed (plan
§3.13), each line re-serialised as the same JSON with these string replacements and nothing else:

| original | placeholder |
|---|---|
| the scratch folder the confinement tree sat in | `C:\work` (so the tree is `C:\work\tree` and the canary `C:\work\outside-canary.txt`) |
| the two-children probe's working tree | `C:\work\tree` |
| the operator's benchmark checkout (a directory the user settings GRANT) | `C:\work\granted` |
| the CLI's own temp, profile, project-memory and pwsh folders | `C:\cli\temp\…`, `C:\cli\profile\…`, `C:\cli\bin\pwsh.exe` |

Both separators and the doubled backslashes of a JSON-escaped path inside a tool result were replaced, so every path keeps the
spelling the CLI printed. `ScoutScrubberTests.Every_committed_scout_fixture_and_record_is_clean` fails the suite if a home
directory, an e-mail address or an entry of the operator's local deny-list (`BENCH_SCOUT_DENYLIST`, never committed) appears here.

## Where each came from

| file | captured with | what it shows / what the reader must extract |
|---|---|---|
| `claude-2.1.289-two-children-models.ndjson` | `claude -p --model opus --output-format stream-json --verbose --forward-subagent-text --max-budget-usd 2 --disallowedTools "WebFetch WebSearch" --no-session-persistence`, the main session told to launch two `feature-dev:code-explorer` subagents in one message, one `model: "sonnet"`, one `model: "opus"` | two Agent calls; every child event carries `parent_tool_use_id`; `message.model` `claude-sonnet-5-5` / `claude-opus-5-5`; each Agent result's `tool_use_result` carries `resolvedModel`, `totalDurationMs` (3614 / 19815), the final iteration's `usage`; the envelope's `total_cost_usd` 0.594582, `modelUsage` (an internal `claude-haiku-4-5-20251001` line too) and `subagent_stats` (spawned 2, completed 2). Child usage deduplicated by message id equals `modelUsage` for input and cache tokens; streamed OUTPUT tokens are placeholders (16 + 22 streamed, 118 billed). The Opus child also Globbed into the granted directory and got content — a path-audit STOP |
| `claude-2.1.289-confine-default.ndjson` | default permissions, one Sonnet `feature-dev:code-explorer` child told to Read `control.txt`, Read the parent's canary by absolute path and by `../`, Glob `../*.txt`, Grep `..` | Read inside returned; both outside Reads and the Grep → `system/permission_denied` (reason `workingDir`) plus an `is_error` result and a `permission_denials` entry; the Glob → "No files found" (an empty success, kept apart from a denial) |
| `claude-2.1.289-confine-restricted.ndjson` | the same prompt under `--restricted` | the plugin agent is GONE: the Agent call's result is `is_error` with `tool_use_result` a STRING ("Agent type 'feature-dev:code-explorer' not found…"); no child, `subagent_stats.spawned = 0` — the read-back voids it |
| `claude-2.1.289-confine-allowlist-grant.ndjson` | `--tools "Task,Read,Glob,Grep"` + `--strict-mcp-config` with an empty config, under user settings that grant a directory (`permissions.additionalDirectories`) | the parent's canary DENIED, but a Read and a Glob inside the granted directory (`C:\work\granted\src`) RETURNED content — what §3.2's fail-stop path audit stops on |

## How a fixture is captured

From a scratch tree with a random canary in its parent directory, the probe's prompt on stdin, stdout kept whole; then scrub with a
script that maps every machine path to the placeholders above and re-checks the output for a home directory, an e-mail and the
deny-list before anything is copied here. Name the file `claude-<version>-<probe>.ndjson`.
