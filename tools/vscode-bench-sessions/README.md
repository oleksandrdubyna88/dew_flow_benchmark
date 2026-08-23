# Bench Sessions — a VS Code extension

Opens **scoped** agent task sessions, and shows what each one is doing.

The scoping is the whole point. Every session this extension opens carries its own `BENCH_TASK_ID`, so a
trace belongs to one named piece of work rather than to "everything that happened today" — and each one can
be linked, **by hand**, to the plan document it is working through.

Part of [todo/ai_math/PLAN_session_measurement.md](../../todo/ai_math/PLAN_session_measurement.md) §6.

## What it does

| Command | What happens |
|---|---|
| **Bench: Start Task Session Here** | Asks for a task name, offers this repository's plans, opens a terminal whose environment carries the task — then launches the agent |
| **Bench: Start Task Session in a New Window** | The same, but the folder opens in a **new VS Code window** and the terminal starts there. The task crosses the window boundary through a handoff file that the new window claims and deletes |
| **Bench: Link This Window's Task to a Plan** | Re-links the task. Takes effect on the **next** terminal, and says so — a terminal's environment is fixed when it is created, and claiming otherwise would be a lie about the one thing this tool exists to get right |
| **Bench: Install Recording Hooks in This Folder** | Runs `bench sessions install` against the folder. It does not reimplement the settings merge: there is one place that knows what a hook entry looks like, and it is not this extension |
| **Bench Sessions** view | Every recorded session, its current phase, and its research·execution·verification split. An unreachable collector renders as a warning row, never as an empty list |
| Status bar | This window's task, its live phase, and its call split |

## Install

No build step — it is plain JavaScript on purpose, so it works the moment it is copied.

```powershell
# Windows
$dst = "$env:USERPROFILE\.vscode\extensions\dewflow.bench-sessions-0.1.0"
New-Item -ItemType Directory -Force $dst | Out-Null
Copy-Item tools\vscode-bench-sessions\* $dst -Recurse -Force
```

Then reload VS Code (**Developer: Reload Window**).

## Before it can record anything

Three things have to be true, and the extension tells you which one is missing rather than rendering an
empty list:

1. **The database has the schema.** `bench sessions list --db <connection>` — the CLI owns migrations, and
   the collector refuses to start against a database that is behind.
2. **`bench-collector` is running.** It is an AppHost resource (`dotnet run --project hosts/AppHost`), and
   it also runs on its own with `ConnectionStrings__bench` set. It listens on `127.0.0.1:5177` — loopback
   only, because this endpoint accepts unauthenticated writes describing every file you touched.
3. **The repository has the hooks.** *Bench: Install Recording Hooks in This Folder*, or
   `bench sessions install --repo <path>`. They land in `.claude/settings.local.json`, never in the shared
   `settings.json`: the command line holds this machine's absolute path to the hook binary.

## Settings

| Setting | Default | Notes |
|---|---|---|
| `benchSessions.collectorUrl` | `http://127.0.0.1:5177` | Pinned rather than discovered — the same address is written into every instrumented repository |
| `benchSessions.consoleUrl` | *(empty)* | The console that mounts the Benchmarking section, so a session opens on its own page. Empty disables that command |
| `benchSessions.benchExecutable` | *(empty)* | Path to `bench.exe`; you are asked once and it is remembered |
| `benchSessions.agentCommand` | `claude` | What a task terminal runs. Empty opens the terminal and launches nothing |
| `benchSessions.refreshSeconds` | `10` | Zero disables polling; the refresh button always works |

## What it deliberately does not do

- **It never guesses the plan.** A session's subject is a claim only its operator can make, and a heuristic
  that inferred it from the first file read would attach confident wrong labels to a corpus whose entire
  value is being trustworthy.
- **It computes nothing.** Every number it renders comes from the collector. The extension holds no state
  worth losing.
- **It does not change a running terminal's environment.** Nothing can.
