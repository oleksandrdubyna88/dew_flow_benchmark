# Bare-session smoke - claude 2.1.310, 2026-10-29

One bare session through the agent box, the egress proxy and the credential broker (E2.S2), kept sanitised as a replay fixture. Every figure below is computed by `lib/smoke-verdicts.mjs`; nothing is typed.

- Command: `node spikes/minigit-bench/smoke-session.mjs --out ~/rsd/minigit/smoke/<UTC stamp>`
- Session id: eded342a-9aab-4cb4-90b5-30b3098ea22c
- Envelope cost: $0.11484459999999999 - listed as pilot spend (parent plan section 13); the broker's ledger: $0.1148446
- Outcome: ok; wall 17375 ms; stopped: no
- Verdicts: 15 holds, 1 refuted, 2 not exercised

## Verdicts

<!-- verdicts:begin -->
| Id | Assumption | Verdict | Observed |
|---|---|---|---|
| B-R1 | the init read-back matches every field of step 6 | holds | every checked field matched (cwd, tools, mcp_servers, permissionMode, version, apiKeySource, memory path, model) |
| B-R2 | init.tools lacks exactly INIT_UNREPORTED | holds | --tools asked for 8, init reported 7; not reported: TodoWrite (INIT_UNREPORTED: TodoWrite) |
| B-N1 | every allowed CONNECT names a host of EGRESS_HOSTS.bare; every refused host is in KNOWN_REFUSED or is named in the report | holds | allowed: {}; refused: {"api.anthropic.com":14}; allowed outside the bare list: none; refused outside KNOWN_REFUSED: none |
| B-N2 | the proxy log holds no non-CONNECT line from the CLI; the only plain-HTTP traffic is the box's own loopback | holds | 0 non-CONNECT line(s) at the proxy |
| B-S2 | every API call went through the broker: broker.ndjson holds every api_request O saw, and api.anthropic.com was never allowed | holds | O saw 5 api_request record(s); the broker forwarded 5 and settled 5; api.anthropic.com allowed at the proxy: false |
| B-S3 | the host side held no real token in the box: counted 0 times in cmdline and environ over at least 5 samples, the dummy seen in an environ at least once (the positive control), and no file of the session dir holds it and no link or pipe sits in the agent-written tree | refuted | 14 samples: real in cmdline 0, in environ 0; positive control: dummy in environ 56; real-token hits over 32 file(s): 0; links or pipes in the tree: not scanned |
| B-S3b | the in-box half: the CLI's own environ, read by the session's Bash, held the dummy (RETIRED: not exercised by design, see its row) | not exercised | models refuse the probe (smoke 20261030T002834Z, Opus 5.5 then fallback Opus 4.8); B-S3 with its positive control covers the CLI's environ from the host; token.txt is empty |
| B-$1 | B.spentUsd = E.total_cost_usd within $0.000001 (T5), and B = O's usd | holds | T5 pass: the broker's ledger 0.1148446 against total_cost_usd 0.1148446 (limit 0.000001); 5 request(s) forwarded, 0 refused; B 0.11484459999999999, O 0.11484459999999999; models charged at the dearest known rates (unpriced): none |
| B-O1 | OTel records reached the receiver through the socket; T2 passes | holds | 5 api_request record(s) decoded; T2 pass |
| B-T1 | box/config/projects holds main and one subagent transcript; T3 and T4 pass under crosscheck-policy.json | holds | 1 main and 1 subagent transcript file(s); T3 pass, T4 pass |
| B-E1 | an envelope; T1 passes; cost at most $0.20 | holds | total_cost_usd 0.11484459999999999 (cap 0.2, the 1.1 x gate 0.22000000000000003); T1 pass |
| B-A1 | audit Pre/Post pair by tool_use_id; partition sums to the wall to the ms; unpaired is empty | holds | 8 audit record(s); unpaired 0; partition sums to 13377 ms of a 13377 ms wall |
| B-A2 | the PostToolUse payload carries duration_ms | holds | 2 of 2 PostToolUse record(s) carry duration_ms; where absent, spans come from the Pre/Post timestamps (E1's method) beside O's tool_result.duration_ms |
| B-A3 | every O api_request carries effort, the child's (by agent_id) is high, and SubagentStart carries the same agent_id | holds | 5 api_request record(s), efforts high; 2 from a subagent (by agent_id); SubagentStart records 1; every child joined: true |
| B-H1 | hook events still carry no timestamp (E1 A-H1) | holds | 16 hook event(s), 0 with a timestamp; 3 hook tool_use_id(s), 0 not in the stream |
| B-K1 | after exit no host process carries the cell marker, the janitor ended 0, state.json.phase is closed, no socket file is left | holds | processes carrying the marker after exit 0; janitor ended 0; state.json.phase closed; socket files left 0 |
| B-K2 | (only when a session was stopped by endCell) a record for the in-flight request arrived after the SIGTERM | not exercised | the smoke ended by itself: no endCell, so no SIGTERM to measure |
| B-C1 | contaminationHits and tokenHits over the smoke: none | holds | contaminationHits 0, tokenHits 0 |
<!-- verdicts:end -->
