# `http/` — the benchmark's contract suite

One folder per route group, per
[`.claude/rules/shared/common/http-contracts.md`](../.claude/rules/shared/common/http-contracts.md).

| Folder | Routes | Host |
|---|---|---|
| [`bench/`](bench) | `/api/bench/health`, `/plan`, `/runs`, `/runs/{id}/scoreboard`, `/runs/{id}/metrics`, `/runs/{id}/report`, `/arms`, `/arms/metrics` | the read API |
| [`sessions/`](sessions) | `/api/bench/sessions` and `/{id}` on the API; `/events` and `/health` on the **collector** | both |

Two origins, because the boundary is the contract: the collector is the one process an agent's hook
may reach, and the read API registers no write port. `sessions/` asserts both halves — the API refuses
the ingest verb (405) and has no read route on that path (404).

## The environment this suite expects

| What | Why |
|---|---|
| A Postgres the API can reach, **with the schema applied** | This host applies no migrations by design. Any CLI verb that migrates will create it — `dotnet run --project hosts/Cli -- prune --db "<conn>" --hit-retention-days 30` is the cheapest. |
| The collector running | Its four session requests address it directly; `BENCH_COLLECTOR_URL` overrides the default `http://127.0.0.1:5177`. |

An empty database is fine and expected: every read here asserts the shape of an **empty** answer,
which is the shape a fresh install actually serves.

## Running it

```bash
npm ci --prefix http

docker run -d --name bench-suite-pg -e POSTGRES_PASSWORD=suite -e POSTGRES_USER=suite \
  -e POSTGRES_DB=bench -p 55432:5432 postgres:17
CONN="Host=127.0.0.1;Port=55432;Database=bench;Username=suite;Password=suite"
dotnet run --project hosts/Cli -- prune --db "$CONN" --hit-retention-days 30   # creates the schema

ConnectionStrings__bench="$CONN" ASPNETCORE_URLS=http://127.0.0.1:5411 dotnet run --project hosts/Api &
ConnectionStrings__bench="$CONN" dotnet run --project hosts/Collector &        # binds 5177 itself

node .claude/rules/shared/tools/http-run.mjs --env local --target http://127.0.0.1:5411
```

The verdict is the exit code — `0` pass · `1` contract regression · `3` environment · `4`
configuration · `5` no valid report.

## What the suite will not stage

Every 200 that needs **real data** — a scoreboard with scores, a report with a comparison, a populated
arms table, an ingested session trace. Producing them means running a campaign, which is the
benchmark's own subject and takes a GPU and hours. What is asserted instead is the shape those routes
answer in *when empty*, which is the shape a fresh install serves and the one nobody checks.
