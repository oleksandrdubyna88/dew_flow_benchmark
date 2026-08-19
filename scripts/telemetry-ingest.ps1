# The scheduled consumer of the MCP telemetry spool.
#
# Why this exists: the spool's producers (dew_flow_rag_qln, dew_flow_mcp) deliberately never prune —
# ownership of the files' lifecycle was handed to THIS repository's ingester, and the 2026-08-19
# stability audit found the gap in that design: a named owner that nothing ever invokes. On a genuinely
# unattended machine the spool grew forever. This script is the invocation; register-telemetry-ingest.ps1
# puts it on a daily schedule.
#
# What it does, in order:
#   1. finds the bench Postgres container the way scripts/bank-thirds.sh does (same env overrides:
#      BENCH_PG_CONTAINER, BENCH_PG_PASSWORD; the published port changes on every container restart, so
#      it is read from Docker each time, never remembered);
#   2. `bench telemetry ingest --spool <dir> --db <conn>` — drained files become *.ingested;
#   3. `bench telemetry prune --spool <dir> --older-than <days>` — only *.ingested is ever deleted.
#
# "Environment not ready" states — Docker down, container absent, bench.exe not built — exit 0 with the
# reason logged: the spool is append-only and simply waits, and a task history full of daily reds for a
# machine whose bench stack is cold teaches the reader to ignore the one red that matters. A real ingest
# failure exits non-zero.

param(
    [string] $Spool = (Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'dew_flow_rag_qln\spool'),
    [int] $OlderThanDays = 14
)

$repo = Split-Path -Parent $PSScriptRoot
$bench = Join-Path $repo 'hosts\Cli\bin\Release\net10.0\bench.exe'
if (-not (Test-Path $bench)) { $bench = Join-Path $repo 'hosts\Cli\bin\Debug\net10.0\bench.exe' }

function Skip([string] $reason) {
    Write-Host "telemetry-ingest: skipped — $reason. The spool waits; nothing is lost."
    exit 0
}

if (-not (Test-Path $bench)) { Skip 'bench.exe is not built (dotnet build dew_flow_benchmark.slnx -c Release)' }
if (-not (Test-Path $Spool)) { Skip "no spool directory at $Spool — the producer has not written yet" }

$container = if ($env:BENCH_PG_CONTAINER) { $env:BENCH_PG_CONTAINER } else { 'postgres-fb96952c' }
docker start $container 2>$null | Out-Null
$portLine = docker port $container 2>$null | Select-Object -First 1
if (-not $portLine -or $portLine -notmatch ':(\d+)$') { Skip "the Postgres container '$container' is not running" }
$port = $Matches[1]
$password = if ($env:BENCH_PG_PASSWORD) { $env:BENCH_PG_PASSWORD } else { 'bench-local-dev' }
$db = "Host=127.0.0.1;Port=$port;Database=bench;Username=postgres;Password=$password"

& $bench telemetry ingest --spool $Spool --db $db
if ($LASTEXITCODE -ne 0) {
    Write-Error "telemetry-ingest: ingest failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

& $bench telemetry prune --spool $Spool --older-than $OlderThanDays
exit $LASTEXITCODE
