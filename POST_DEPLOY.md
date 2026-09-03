# Post-deploy checks — dew_flow_benchmark

Per [`.claude/rules/shared/common/post-deploy-checks.md`](.claude/rules/shared/common/post-deploy-checks.md).

Nothing is deployed anywhere: the API, the collector and the CLI are started on the machine that runs
a campaign. So "prod" is that machine's installation, and this list runs at every release against it —
the rule's second row.

Everything here is decided by the environment rather than the code: which database the process reached,
whether its schema exists, and whether the collector an agent's hook writes to is up at all.

Target: the read API, as an origin — `--target http://127.0.0.1:5411`
Last verified: 2026-09-03 · http://127.0.0.1:5411 against a throwaway `postgres:17` on 55432 · all three automated items PASS. Items 1 and 2 were then watched FAIL with the database container stopped — while `/api/bench/health` kept answering `ok`, which is exactly why neither of them uses it.

| # | What a person loses if this is broken | Check | Auto |
|---|---|---|---|
| 1 | Every read answers as if the campaign never happened. **`/api/bench/health` cannot tell you this** — it is a constant `ok` that never touches the store, so the probe must be a route that does | `node -e "fetch(process.env.TARGET+'/api/bench/runs?limit=1').then(r=>process.exitCode=+(r.ok?0:1))"` | auto |
| 2 | The schema is absent or older than the code. This host applies **no** migrations on purpose, so an unmigrated database answers "no run" — which reads as "your id is wrong" when the truth is "this is the wrong database" | `node -e "fetch(process.env.TARGET+'/api/bench/arms?metric=recall_at_10').then(r=>r.json()).then(a=>process.exitCode=+(a.metricName==='recall_at_10'?0:1))"` | auto |
| 3 | An agent's hooks write their session traces nowhere, silently, for a whole campaign — and the loss is only discovered when somebody goes looking for the trace | `node -e "fetch((process.env.BENCH_COLLECTOR_URL\|\|'http://127.0.0.1:5177')+'/api/bench/sessions/health').then(r=>r.json()).then(h=>process.exitCode=+(h.status==='ok'?0:1))"` | auto |
| 4 | Results are read from a **different database than the campaign wrote to**, and every number is plausible. Measured in this family: the port an AppHost pins and the port the container actually publishes drifted apart | Read `ConnectionStrings__bench` on the running host and confirm the port against `docker ps` — the published port, not the configured one | manual |

## Why item 1 is not `/api/bench/health`

Because that route answers `{"status":"ok"}` from a literal and touches nothing. It is a liveness
probe for an orchestrator, and using it here would be the exact failure
[`reliability.md`](.claude/rules/shared/common/reliability.md) names: a health endpoint that computes
from a constant tells you the route is mapped and nothing else. `/api/bench/runs` goes to the store,
so it fails when the store is unreachable — which is the thing this list is for.

## Running it

```bash
node .claude/rules/shared/tools/post-deploy-check.mjs --target http://127.0.0.1:5411
```
