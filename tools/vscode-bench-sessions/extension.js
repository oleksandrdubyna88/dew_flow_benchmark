'use strict';

/**
 * Bench Sessions — opens SCOPED agent task sessions, and shows what each one is doing.
 *
 * The scoping is the point. Every session this extension opens carries its own BENCH_TASK_ID, so a
 * trace belongs to one named piece of work instead of to "everything that happened today". The plan
 * link is set BY HAND for the same reason the collector refuses to infer it: what a session is about
 * is a claim only its operator can make, and a heuristic that guessed it from the first file read
 * would attach confident wrong labels to a corpus whose whole value is being trustworthy.
 *
 * Plain JavaScript, no build step. This is a local operator tool that has to work the moment it is
 * copied into an extensions folder; a bundler between the source and that would be a second thing to
 * keep working for no gain.
 */

const vscode = require('vscode');
const fs = require('fs');
const path = require('path');
const http = require('http');

/** Written by the window that starts a task, read by the window that opens for it. */
const HANDOFF_FILE = 'pending-tasks.json';

/** A handoff older than this is stale — a new window that never opened, a folder pick abandoned. */
const HANDOFF_TTL_MS = 5 * 60 * 1000;

let state;

function activate(context) {
    state = {
        context,
        sessions: [],
        error: '',
        task: null,
        timer: null,
    };

    const provider = new SessionTreeProvider();
    state.provider = provider;

    context.subscriptions.push(
        vscode.window.registerTreeDataProvider('benchSessions.list', provider),
        vscode.commands.registerCommand('benchSessions.startHere', () => startHere()),
        vscode.commands.registerCommand('benchSessions.startInNewWindow', () => startInNewWindow()),
        vscode.commands.registerCommand('benchSessions.linkPlan', () => linkPlan()),
        vscode.commands.registerCommand('benchSessions.refresh', () => refresh()),
        vscode.commands.registerCommand('benchSessions.openInConsole', (item) => openInConsole(item)),
        vscode.commands.registerCommand('benchSessions.installHooks', () => installHooks()));

    state.status = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);
    state.status.command = 'benchSessions.linkPlan';
    context.subscriptions.push(state.status);

    claimHandoff();
    schedule();
    refresh();
}

function deactivate() {
    if (state && state.timer) {
        clearInterval(state.timer);
    }
}

// ---- starting a session ------------------------------------------------------------------------

/**
 * A task terminal in THIS window's folder.
 *
 * The environment is set on the terminal rather than exported inside it, so the variables exist
 * before the agent's first byte — a hook that fired before an `export` would record an unattributed
 * call, and the first call of a session is usually the most interesting one.
 */
async function startHere() {
    const folder = await pickFolder();
    if (!folder) {
        return;
    }

    const task = await composeTask(folder);
    if (!task) {
        return;
    }

    openTerminal(folder, task);
}

/**
 * A task in a NEW window.
 *
 * The environment cannot cross a window boundary, so the task is written to a handoff file that the
 * new window's activation reads and CLAIMS — deleting it as it does, so a third window opened later
 * on the same folder does not inherit somebody else's task id.
 */
async function startInNewWindow() {
    const picked = await vscode.window.showOpenDialog({
        canSelectFolders: true,
        canSelectFiles: false,
        canSelectMany: false,
        openLabel: 'Open as a task window',
    });

    if (!picked || picked.length === 0) {
        return;
    }

    const folder = picked[0].fsPath;
    const task = await composeTask(folder);
    if (!task) {
        return;
    }

    writeHandoff(folder, task);
    await vscode.commands.executeCommand('vscode.openFolder', picked[0], { forceNewWindow: true });
}

/** Asks for the name and the plan. Returns null when the operator backed out at either step. */
async function composeTask(folder) {
    const name = await vscode.window.showInputBox({
        title: 'Task name',
        prompt: 'What is this session for? It labels the trace, so make it something you would search for.',
        placeHolder: 'find the ingest duplicate bug',
        ignoreFocusOut: true,
    });

    if (!name) {
        return null;
    }

    const plan = await pickPlan(folder);

    return {
        id: taskId(name),
        name,
        // An empty plan is a legitimate answer, not a skipped step: plenty of work has no plan
        // document, and an unlinked session is unattributed rather than wrong.
        plan: plan || '',
        folder,
        at: Date.now(),
    };
}

/**
 * The plan documents this repository holds, offered as a list rather than typed.
 *
 * Typed paths go stale the moment a plan is promoted from todo/ to research/ — which this family does
 * routinely — and a link that does not resolve is worse than no link at all.
 */
async function pickPlan(folder) {
    const files = [...listPlans(path.join(folder, 'todo')), ...listPlans(path.join(folder, 'research'))];

    const items = [
        { label: '$(circle-slash) No plan', description: 'leave this session unlinked', value: '' },
        ...files.map((file) => ({
            label: path.basename(file),
            description: path.relative(folder, file).split(path.sep).join('/'),
            value: path.relative(folder, file).split(path.sep).join('/'),
        })),
    ];

    const chosen = await vscode.window.showQuickPick(items, {
        title: 'Link this session to a plan',
        placeHolder: 'The plan this work belongs to — set by hand, never guessed from the files you open',
        ignoreFocusOut: true,
    });

    return chosen ? chosen.value : '';
}

/** Plan documents, one directory deep plus its immediate subfolders — which is where `ai_math/` lives. */
function listPlans(root) {
    const found = [];

    const walk = (dir, depth) => {
        let entries;
        try {
            entries = fs.readdirSync(dir, { withFileTypes: true });
        } catch {
            return;
        }

        for (const entry of entries) {
            const full = path.join(dir, entry.name);
            if (entry.isDirectory() && depth > 0) {
                walk(full, depth - 1);
            } else if (entry.isFile() && entry.name.endsWith('.md') && entry.name !== 'README.md') {
                found.push(full);
            }
        }
    };

    walk(root, 1);

    return found.sort();
}

function openTerminal(folder, task) {
    const settings = vscode.workspace.getConfiguration('benchSessions');

    const terminal = vscode.window.createTerminal({
        name: task.name,
        cwd: folder,
        env: {
            BENCH_TASK_ID: task.id,
            BENCH_TASK_NAME: task.name,
            BENCH_PLAN_PATH: task.plan,
            BENCH_COLLECTOR_URL: settings.get('collectorUrl'),
        },
    });

    terminal.show();

    const agent = settings.get('agentCommand');
    if (agent) {
        terminal.sendText(agent);
    }

    state.task = task;
    render();
    vscode.window.showInformationMessage(
        `Task "${task.name}" is recording${task.plan ? ` against ${task.plan}` : ' (no plan linked)'}.`);
}

/**
 * A readable id that is still unique. The name is what a human recognises in a list; the timestamp is
 * what keeps two sessions of the same task apart, which is the normal case rather than the exotic one
 * — the second attempt at a task is exactly what anyone comparing traces wants to find.
 */
function taskId(name) {
    const slug = name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '').slice(0, 32);
    const stamp = new Date().toISOString().replace(/[-:T]/g, '').slice(0, 14);

    return `${slug || 'task'}-${stamp}`;
}

// ---- the handoff between windows ---------------------------------------------------------------

function handoffPath() {
    const dir = state.context.globalStorageUri.fsPath;
    fs.mkdirSync(dir, { recursive: true });

    return path.join(dir, HANDOFF_FILE);
}

function writeHandoff(folder, task) {
    const all = readHandoffs();
    all[normalise(folder)] = task;

    try {
        fs.writeFileSync(handoffPath(), JSON.stringify(all, null, 2), 'utf8');
    } catch (error) {
        vscode.window.showWarningMessage(`Bench: could not hand the task to the new window — ${error.message}`);
    }
}

function readHandoffs() {
    try {
        return JSON.parse(fs.readFileSync(handoffPath(), 'utf8'));
    } catch {
        return {};
    }
}

/**
 * Takes the task written for THIS folder, if one is waiting, and removes it in the same breath.
 *
 * The removal is what keeps ids scoped. Leaving the record behind would give every window later
 * opened on this folder the same task id, and a trace that merged three afternoons into one session
 * is the exact failure this extension exists to prevent.
 */
function claimHandoff() {
    const folder = currentFolder();
    if (!folder) {
        return;
    }

    const all = readHandoffs();
    const key = normalise(folder);
    const task = all[key];

    // Stale records are dropped rather than honoured: a window that never opened should not hand its
    // task to one opened for something else an hour later.
    const fresh = task && Date.now() - task.at < HANDOFF_TTL_MS;

    delete all[key];
    try {
        fs.writeFileSync(handoffPath(), JSON.stringify(all, null, 2), 'utf8');
    } catch {
        // Losing the file costs one handoff, never the session.
    }

    if (fresh) {
        openTerminal(folder, task);
    }
}

const normalise = (folder) => path.resolve(folder).toLowerCase();

function currentFolder() {
    const folders = vscode.workspace.workspaceFolders;

    return folders && folders.length > 0 ? folders[0].uri.fsPath : '';
}

async function pickFolder() {
    const folders = vscode.workspace.workspaceFolders || [];

    if (folders.length === 0) {
        vscode.window.showWarningMessage('Bench: open a folder first — a session is scoped to a repository.');
        return '';
    }

    if (folders.length === 1) {
        return folders[0].uri.fsPath;
    }

    const chosen = await vscode.window.showQuickPick(
        folders.map((f) => ({ label: f.name, description: f.uri.fsPath, value: f.uri.fsPath })),
        { title: 'Which folder is this session about?' });

    return chosen ? chosen.value : '';
}

// ---- linking a plan afterwards ------------------------------------------------------------------

/**
 * Re-links the current window's task.
 *
 * It takes effect on the NEXT terminal, and says so rather than pretending otherwise: a terminal's
 * environment is fixed when it is created, and an extension that claimed to have changed a running
 * agent's variables would be lying about the one thing this tool exists to get right.
 */
async function linkPlan() {
    const folder = currentFolder();
    if (!folder) {
        vscode.window.showWarningMessage('Bench: open a folder first.');
        return;
    }

    if (!state.task) {
        await startHere();
        return;
    }

    const plan = await pickPlan(folder);
    state.task = { ...state.task, plan };
    render();

    vscode.window.showInformationMessage(
        plan
            ? `Next terminal in this window links to ${plan}. The running one keeps the environment it was created with.`
            : 'Next terminal in this window will be unlinked.');
}

// ---- reading the collector ----------------------------------------------------------------------

function schedule() {
    const seconds = vscode.workspace.getConfiguration('benchSessions').get('refreshSeconds');

    if (state.timer) {
        clearInterval(state.timer);
        state.timer = null;
    }

    if (seconds > 0) {
        state.timer = setInterval(refresh, seconds * 1000);
    }
}

async function refresh() {
    const base = vscode.workspace.getConfiguration('benchSessions').get('collectorUrl');

    try {
        state.sessions = await getJson(`${base.replace(/\/$/, '')}/api/bench/sessions?limit=50`);
        state.error = '';
    } catch (error) {
        // An unreachable collector and an empty one are opposite facts, and the tree renders them as
        // two different rows rather than as one empty list.
        state.sessions = [];
        state.error = `${base} could not be read — ${error.message}`;
    }

    render();
}

function render() {
    state.provider.fire();
    renderStatus();
}

function renderStatus() {
    if (!state.task) {
        state.status.hide();
        return;
    }

    const mine = state.sessions.find((s) => s.task && s.task.id === state.task.id);
    const phase = mine ? mine.phase : 'waiting';
    const calls = mine ? `${mine.research}·${mine.execution}·${mine.verification}` : '—';

    state.status.text = `$(beaker) ${state.task.name} · ${phase} · ${calls}`;
    state.status.tooltip = state.task.plan
        ? `Task ${state.task.id}\nPlan ${state.task.plan}\nresearch·execution·verification calls`
        : `Task ${state.task.id}\nNo plan linked — click to link one\nresearch·execution·verification calls`;
    state.status.show();
}

function getJson(url) {
    return new Promise((resolve, reject) => {
        const request = http.get(url, { timeout: 2000 }, (response) => {
            if (response.statusCode !== 200) {
                response.resume();
                reject(new Error(`answered ${response.statusCode}`));
                return;
            }

            let body = '';
            response.setEncoding('utf8');
            response.on('data', (chunk) => (body += chunk));
            response.on('end', () => {
                try {
                    resolve(JSON.parse(body));
                } catch (error) {
                    reject(new Error(`unreadable body: ${error.message}`));
                }
            });
        });

        request.on('timeout', () => request.destroy(new Error('did not answer in time')));
        request.on('error', reject);
    });
}

// ---- the tree ------------------------------------------------------------------------------------

class SessionTreeProvider {
    constructor() {
        this.emitter = new vscode.EventEmitter();
        this.onDidChangeTreeData = this.emitter.event;
    }

    fire() {
        this.emitter.fire();
    }

    getTreeItem(item) {
        return item;
    }

    getChildren() {
        if (state.error) {
            const item = new vscode.TreeItem(state.error, vscode.TreeItemCollapsibleState.None);
            item.iconPath = new vscode.ThemeIcon('warning');
            item.tooltip = 'Is bench-collector running? It is an AppHost resource, and can also be run on its own.';
            return [item];
        }

        if (state.sessions.length === 0) {
            const item = new vscode.TreeItem('No sessions recorded yet', vscode.TreeItemCollapsibleState.None);
            item.iconPath = new vscode.ThemeIcon('info');
            item.tooltip = 'Install the hooks in a repository, then start a task session.';
            return [item];
        }

        return state.sessions.map(toItem);
    }
}

function toItem(session) {
    const name = (session.task && session.task.name) || (session.task && session.task.id) || session.sessionKey;
    const item = new vscode.TreeItem(name, vscode.TreeItemCollapsibleState.None);

    item.description = `${session.phase} · ${session.research}·${session.execution}·${session.verification}`;
    item.contextValue = 'session';
    item.id = session.sessionId;
    item.iconPath = new vscode.ThemeIcon(phaseIcon(session.phase));
    item.tooltip = new vscode.MarkdownString(
        [
            `**${name}**`,
            '',
            `- phase **${session.phase}**`,
            `- calls **${session.calls}** — research ${session.research}, execution ${session.execution}, verification ${session.verification}`,
            `- unfinished **${session.unfinished}** · compile failures **${session.compileFailures}** · taxonomy disagreements **${session.disagreements}**`,
            `- plan \`${(session.task && session.task.plan_path) || (session.task && session.task.planPath) || '— not linked'}\``,
            `- workspace \`${session.workspacePath}\` [${session.branch || '?'}]`,
            `- last event ${session.lastEventAt}`,
        ].join('\n'));

    return item;
}

/** Verification is the "pass" icon because reaching it is the shape of a session that finished something. */
function phaseIcon(phase) {
    switch (phase) {
        case 'Research':
            return 'search';
        case 'Execution':
            return 'edit';
        case 'Verification':
            return 'pass';
        default:
            return 'circle-outline';
    }
}

async function openInConsole(item) {
    const base = vscode.workspace.getConfiguration('benchSessions').get('consoleUrl');

    if (!base) {
        vscode.window.showWarningMessage(
            'Bench: set benchSessions.consoleUrl to the console that mounts the Benchmarking section.');
        return;
    }

    await vscode.env.openExternal(
        vscode.Uri.parse(`${base.replace(/\/$/, '')}/benchmarking/sessions/${item.id}`));
}

// ---- installing the hooks -------------------------------------------------------------------------

/**
 * Runs `bench sessions install` against this folder — the same verb an operator would type, rather
 * than a second implementation of the settings merge. There is one place that knows what a hook entry
 * looks like, and it is not this file.
 */
async function installHooks() {
    const folder = await pickFolder();
    if (!folder) {
        return;
    }

    const settings = vscode.workspace.getConfiguration('benchSessions');
    let bench = settings.get('benchExecutable');

    if (!bench) {
        bench = await vscode.window.showInputBox({
            title: 'Path to bench.exe',
            prompt: 'Built by `dotnet build dew_flow_benchmark.slnx -c Release`',
            ignoreFocusOut: true,
        });

        if (!bench) {
            return;
        }

        await settings.update('benchExecutable', bench, vscode.ConfigurationTarget.Global);
    }

    const terminal = vscode.window.createTerminal({ name: 'bench sessions install', cwd: folder });
    terminal.show();
    terminal.sendText(
        `& "${bench}" sessions install --repo "${folder}" --collector "${settings.get('collectorUrl')}"`);
}

module.exports = { activate, deactivate };
