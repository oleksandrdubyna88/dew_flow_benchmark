# Probe transcript fixtures — SYNTHETIC until S5's hand-check

These files pin the three verdict readers of `Bench.Domain.Probes.ProbeTranscripts` (plan
`todo/PLAN_question_consultant_probes.md`, S1 acceptance 1–2):

| file | grammar | what the reader must extract |
|---|---|---|
| `claude-web-search.json` | `claude -p --output-format json` result object | `usage.server_tool_use.web_search_requests = 1` → web search **yes**; `permission_denials = []` → read attempted **no** |
| `claude-read-denied.json` | same | `web_search_requests = 0` → **no**; a `permission_denials` entry for `Read` → read attempted **yes** |
| `claude-missing-fields.json` | same, fields absent | no `server_tool_use`, no `permission_denials` → both **not captured** |
| `codex-web-search.jsonl` | `codex exec --json` JSONL events | an `item` of type `web_search` → **yes**; no `command_execution` item in a completed turn → **no** |
| `codex-read-denied.jsonl` | same | a `command_execution` item → read attempted **yes**; no `web_search` item → **no** |
| `codex-missing-fields.jsonl` | same, truncated | no `turn.completed` event → both **not captured** |
| `agy-web-search.ndjson` | `agy --output-format stream-json` NDJSON | a `tool_use` of `google_web_search` → **yes**; no read tool → **no** |
| `agy-read-denied.ndjson` | same | a `tool_use` of `read_file` → read attempted **yes**; no web search → **no** |
| `agy-missing-fields.ndjson` | same, truncated | no `result` event → both **not captured** |

**They were written by hand from each CLI's documented output shape, not recorded from a live run.** Until S5's
hand-check (§4, gate round 1 finding 6) reads the first live cell of every runtime against what its reader
extracted — and replaces or confirms these files — a runtime's `toolEvidence` and `readAttempted` are reported as
*not captured* in every write-up table, never as *no*. A reader found wrong is fixed RED-first on the live
transcript, which then becomes the fixture here.
