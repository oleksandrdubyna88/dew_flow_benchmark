// The public note (plan section 5 and D-X10, T-X9): `research/lang-v2/EXPLORE_task1_<run date>.md`, GENERATED from the records and nothing else, so no number is typed. It states every result against the prediction
// recorded before the run, names every difference from v2's harness, and carries the banner. It gives counts and rates only: no case id, no requirement breakdown, no input or expected value, no trap name, no
// agent code or transcript (the private detail stays in `~/rsd/lang-v2/explore/<series>/private/`). Pure: records in, text out.

export const BANNER = '> **Exploratory, not evidence.** Three pairs of the easiest task on a harness that differs from v2\'s. This note licenses sentences about THIS run only; it settles nothing about C# against TypeScript (`common.measurement` rule 3).';
const DASH = '—';

const leaf = (record, dotted) => {
  const node = dotted.split('.').reduce((o, k) => (o === undefined || o === null ? undefined : o[k]), record);
  return node?.known === true ? node.value : null;
};
const num = (n, digits) => (typeof n === 'number' && Number.isFinite(n) ? n.toFixed(digits) : DASH);
const usd = n => (typeof n === 'number' ? `$${n.toFixed(4)}` : DASH);
const secs = ms => (typeof ms === 'number' ? `${(ms / 1000).toFixed(1)} s` : DASH);
const pct = n => (typeof n === 'number' ? `${n}%` : DASH);
const median = xs => (xs.length === 0 ? null : [...xs].sort((a, b) => a - b)[Math.floor(xs.length / 2)]);
const cap = s => (s === 'csharp' ? 'C#' : 'TypeScript');
const row = cells => `| ${cells.join(' | ')} |`;
const table = (head, rows) => [row(head), row(head.map(() => '---')), ...rows.map(row)];

/** The cross-check statuses of a record as one cell: `T1-T5 pass` or the ones that did not. */
function checksCell(record) {
  const checks = leaf(record, 'cost.checks');
  if (!checks) return DASH;
  const bad = Object.entries(checks).filter(([, c]) => c.status !== 'pass');
  return bad.length === 0 ? 'T1-T5 pass' : bad.map(([k, c]) => `${k.toUpperCase()} ${c.status}`).join(', ');
}

const tokensCell = record => {
  const t = leaf(record, 'cost.tokens');
  if (!t) return DASH;
  const sum = key => Object.values(t).reduce((a, m) => a + (m[key] ?? 0), 0);
  return `in ${sum('input')}, out ${sum('output')}, cache read ${sum('cacheRead')}, cache write ${sum('cacheWrite')}`;
};

/** The 1 h share of the cache writes across models (the 5m/1h split), as a cell. */
function splitCell(record) {
  const per = leaf(record, 'cost.perModel');
  if (!per) return DASH;
  const shares = Object.values(per).filter(m => m.known && m.share1h?.known).map(m => m.share1h.value);
  return shares.length === 0 ? DASH : shares.map(s => `${Math.round(s * 100)}% 1h`).join(' / ');
}

const partitionSum = record => {
  const p = leaf(record, 'time.partition');
  return p ? Object.values(p.categories).reduce((a, b) => a + b, 0) : null;
};

/** One session as the cells of the per-session tables. */
export function sessionCells({ entry, record }) {
  const explore = record.explore ?? {};
  return {
    pair: entry.pair, stack: cap(entry.stack), label: entry.label, attempt: entry.attempt, cls: record.class, flags: record.flags ?? [],
    e: leaf(record, 'cost.cliTotalUsd'), b: leaf(record, 'cost.brokerUsd'), headline: leaf(record, 'cost.headlineUsd')?.usd ?? null, checks: checksCell(record), tokens: tokensCell(record), split: splitCell(record),
    wall: leaf(record, 'time.wallMs'), modelApi: leaf(record, 'time.modelApiMs'), active: leaf(record, 'time.activeMs'), diff: leaf(record, 'time.activeMinusModelApiMs'),
    partition: leaf(record, 'time.partition'), partitionSum: partitionSum(record), buildTest: leaf({ explore }, 'explore.buildTest'), rate: leaf({ explore }, 'explore.passRate'),
    accepted: leaf({ explore }, 'explore.accepted'), trap: leaf({ explore }, 'explore.trapHits'), data: record.data === true, stop: leaf(record, 'cost.stopRuleUsd'),
  };
}

const kindLine = c => `${c.cls}${c.label === 'ran unpaired' ? ' (ran unpaired)' : ''}`;

function costTable(cells) {
  return table(['pair', 'stack', 'class', 'CLI total (E)', 'broker (B)', 'recompute', 'cross-checks', 'cache write split', 'tokens'],
    cells.map(c => [c.pair, c.stack, kindLine(c), usd(c.e), usd(c.b), usd(c.headline), c.checks, c.split, c.tokens]));
}

function timeTable(cells) {
  return table(['pair', 'stack', 'wall', 'model_api', 'active', 'active - model_api', 'build/test (own calls)', 'sums to the wall'],
    cells.map(c => [c.pair, c.stack, secs(c.wall), secs(c.modelApi), secs(c.active), secs(c.diff), c.buildTest ? `${secs(c.buildTest.ms)} in ${c.buildTest.calls} call(s)` : DASH, sums(c)]));
}

const sums = c => (c.partitionSum === null || c.wall === null ? DASH : (Math.abs(c.partitionSum - c.wall) <= 1 ? 'yes' : `no (${c.partitionSum} ms of ${c.wall} ms)`));

function partitionTable(cells) {
  const names = [...new Set(cells.flatMap(c => (c.partition ? Object.keys(c.partition.categories) : [])))];
  return table(['pair', 'stack', ...names], cells.map(c => [c.pair, c.stack, ...names.map(n => secs(c.partition?.categories[n]))]));
}

function qualityTable(cells) {
  return table(['pair', 'stack', 'visible pass rate', 'held-out pass rate', 'accepted', 'trap hit (a count; detail private)'],
    cells.map(c => [c.pair, c.stack, pct(c.rate?.visible), pct(c.rate?.heldout), c.accepted === null ? DASH : (c.accepted ? 'yes' : 'no'), c.trap === null ? DASH : `${c.trap.hits} of ${c.trap.trapCases} trap cases`]));
}

function pairTable(pairs) {
  const ratio = (p, m) => (p.ratios[m].with.known ? `${p.ratios[m].with.value}${p.ratios[m].with.bound ?? ''}` : DASH);
  return table(['pair', 'C# / TS cost', 'C# / TS wall', 'C# / TS model_api', 'C# / TS active - model_api', 'masks', 'start skew'],
    pairs.map(p => [p.pair, ratio(p, 'costUsd'), ratio(p, 'wallMs'), ratio(p, 'modelApiMs'), ratio(p, 'activeMinusModelApiMs'), Object.values(p.masks).join(' / '), Object.values(p.skewsMs).map(ms => `${ms} ms`).join(' / ') || DASH]));
}

const cpcValue = c => {
  if (!c.cpc.known) return DASH;
  return c.cpc.value === null ? `no correct solution (spend ${usd(c.cpc.spendUsd)})` : `${usd(c.cpc.value)}${c.cpc.bound ?? ''}`;
};
const cpcLine = c => `${c.accepted} accepted of ${c.sessions} session(s), spend ${c.spendUsd.known ? `${usd(c.spendUsd.value)}${c.spendUsd.bound ?? ''}` : DASH}, cost per correct solution ${cpcValue(c)}`;

/** Per stack, both figures, each labelled: CPC over the data attempts (D24) and CPC_all over every session including the voids. */
function cpcLines(cpc) {
  return Object.entries(cpc).flatMap(([stack, c]) => [
    `- ${cap(stack)}, CPC (data attempts only, D24): ${cpcLine(c.cpc)}`,
    `- ${cap(stack)}, CPC_all (every session, voids included): ${cpcLine(c.cpcAll)}`,
  ]);
}

// ---- the prediction against the observation ---------------------------------------------------------------------------------------------

const range = (xs, f) => (xs.length === 0 ? DASH : `${f(Math.min(...xs))} to ${f(Math.max(...xs))}, median ${f(median(xs))}`);
const count = (xs, test) => xs.filter(test).length;

/** The six rows of the plan's section 5, each `[measure, predicted, observed]`, the observed text computed from the sessions. */
export function predictionRows(cells, pairs) {
  const data = cells.filter(c => c.data);
  const costs = data.map(c => c.stop).filter(x => typeof x === 'number');
  const walls = data.map(c => c.wall).filter(x => typeof x === 'number');
  const both = pairs.map(p => p.cells).filter(c => c.csharp.activeMinusModelApiMs.known && c.node.activeMinusModelApiMs.known);
  const csharpMore = count(both, c => c.csharp.activeMinusModelApiMs.value > c.node.activeMinusModelApiMs.value);
  const accepted = count(cells.filter(c => c.accepted !== null), c => c.accepted);
  const scored = cells.filter(c => c.accepted !== null).length;
  return [
    ['cost per session', '$0.15 to $0.60, median about $0.25', range(costs, v => `$${v.toFixed(2)}`)],
    ['wall per session', '1 to 4 min', range(walls, v => `${(v / 60000).toFixed(1)} min`)],
    ['active - model_api', 'C# > TS in most sessions', both.length === 0 ? DASH : `C# > TS in ${csharpMore} of ${both.length} pair(s)`],
    ['visible pass rate', '100% in every session', cells.length === 0 ? DASH : `${count(cells, c => c.rate?.visible === 100)} of ${cells.length} session(s) at 100%`],
    ['held-out', 'accepted in at least 5 of 6 sessions; trap hits 0 to 1 per stack', `accepted in ${accepted} of ${scored} scored session(s); trap hits ${trapText(cells)}`],
    ['harness', '0 voids; T1-T5 hold; the partition sums to the wall; at most $50 in every session by two orders of magnitude', harnessText(cells)],
  ];
}

function trapText(cells) {
  return ['csharp', 'node'].map(s => `${cap(s)} ${count(cells.filter(c => c.stack === cap(s) && c.trap), c => c.trap.hits > 0)} session(s) with a hit`).join(', ');
}

function harnessText(cells) {
  const voids = count(cells, c => String(c.cls).startsWith('void'));
  const checksOk = count(cells, c => c.checks === 'T1-T5 pass');
  const summed = count(cells, c => sums(c) === 'yes');
  const maxCost = Math.max(0, ...cells.map(c => c.stop).filter(x => typeof x === 'number'));
  return `${voids} void(s); T1-T5 pass in ${checksOk} of ${cells.length}; the partition sums to the wall in ${summed} of ${cells.length}; the most expensive session cost ${usd(maxCost)}`;
}

/** The differences from v2's harness the plan names (section 8, risk 4): each stated, none implied. */
export const DIFFERENCES = Object.freeze([
  'No execution layer (v2 D43) and no shared network namespace (not needed for an olympiad task).',
  '`runner/run_samples.py` is the pilot\'s olympiad-only judge, not v2\'s E1.S8a runner (D37).',
  'SDK 10.0.114, not 10.0.403; Node 26.12.1, not 26.13.0.',
  'The time and memory limits are 2 s and 512 MB, uncalibrated beyond the pilot\'s own calibration (v2 D14, E2.S3 not built).',
  'Build and test time is reported beside the time partition, not inside it (v1\'s classifier knows `test-v*.sh`, not `build.sh` or `run.sh`, and is not widened).',
  'No pre-registration, no seal and no A/A.',
  'CLI 2.1.310 is not re-pinned (v2 D8).',
  'v1\'s contamination rules run as they are (the rules that name minigit are idle; the path rules are active).',
]);

const NOT_SETTLED = 'What it does not settle: anything about C# against TypeScript. Three pairs of the easiest task, on a harness that differs from v2\'s, license sentences about this run only.';

/** The pins and the subject, by sha, harness path and date (`common.measurement` rule 6). */
function pinLines({ input }) {
  const e = input.pins;
  return [
    `- Date: ${input.date}; series \`${input.seriesId}\`${input.rehearsal ? ' (a REHEARSAL: stub CLI, fake token, $0)' : ''}.`,
    `- Subject: branch \`${input.subject.branch}\` at \`${input.subject.sha}\`; harness \`${input.subject.harnessPath}\` (imports \`spikes/minigit-bench\`), identity \`${input.subject.harnessIdentity}\`, pilot tree \`${input.subject.pilotIdentity}\`.`,
    `- Agent: CLI ${e.cli.version} (sha256 \`${e.cli.sha256.slice(0, 12)}\`), model \`${e.cli.model}\`, effort \`${e.cli.effort}\`; caps $${e.caps.capUsd} per session, ${e.caps.wallMs / 60000} min wall, ${e.caps.stallMs / 60000} min stall.`,
    `- Stacks: .NET SDK ${e.dotnet.sdk}, Node ${e.node.version}, TypeScript ${e.typescript.version}; locale \`${e.locale}\`; python3 ${e.python3.version} (sha256 \`${e.python3.sha256.slice(0, 12)}\`).`,
    `- Task: spec 01 at \`${e.specs.spec01.sha}\` (sha256 \`${e.specs.spec01.sha256.slice(0, 12)}\`), FORMAT v2 at \`${e.specs.format.sha}\` (sha256 \`${e.specs.format.sha256.slice(0, 12)}\`).`,
    `- Limits: time limit ${input.limits.timeMs} ms, memory limit ${input.limits.memoryBytes} bytes (calibrated on this host before pair 1).`,
  ];
}

/** Everything the note states about the floors: how many passing runs would have failed the spec's own floors. */
function floorLine(sessions) {
  const b = sessions.map(s => leaf({ explore: s.record.explore ?? {} }, 'explore.floorBreaches')).filter(Boolean);
  if (b.length === 0) return `${DASH}`;
  const sum = k => b.reduce((a, x) => a + x[k], 0);
  return `${sum('time')} of ${sum('passing')} passing run(s) took longer than the spec's 1 s floor and ${sum('memory')} used more than its 256 MB floor`;
}

/**
 * The note. `input` = `{ date, seriesId, rehearsal, subject, pins, limits, sessions: [{ entry, record }], pairs: [pairRecord], cpc: { csharp, node } }`.
 * -> the Markdown text.
 */
export function renderNote(input) {
  const cells = input.sessions.map(sessionCells);
  return [
    `# Exploratory pilot: task 1 on the v1 bare harness, ${input.sessions.length} session(s) (${input.date})`, '', BANNER, '',
    '## What was run', '', ...pinLines({ input }), '',
    '## Prediction against observation (recorded before the run)', '', ...table(['measure', 'predicted', 'observed'], predictionRows(cells, input.pairs)), '',
    '## Cost: the views and their cross-checks', '', ...costTable(cells), '',
    '## Time: wall, model_api, active - model_api, and the build/test time of the agent\'s own calls', '', ...timeTable(cells), '',
    '## The time partition, every category', '', ...partitionTable(cells), '',
    '## Quality: pass rates and trap hits (counts only)', '', ...qualityTable(cells), '', floorLine(input.sessions), '',
    '## Pairs', '', ...pairTable(input.pairs), '',
    '## Cost per correct solution', '', ...cpcLines(input.cpc), '',
    '## Voids, contamination and timeouts', '', ...incidentLines(cells), '',
    '## Differences from v2\'s harness', '', ...DIFFERENCES.map(d => `- ${d}`), '',
    '## What this does not settle', '', NOT_SETTLED, '',
  ].join('\n');
}

const isIncident = c => c.flags.length > 0 || !c.data || /^(void|killed|stop|crashed)/.test(c.cls);

function incidentLines(cells) {
  const flagged = cells.filter(isIncident);
  if (flagged.length === 0) return ['No void, no kill, no timeout and no flagged session.'];
  return flagged.map(c => `- pair ${c.pair} ${c.stack} attempt ${c.attempt}: ${kindLine(c)}${c.flags.length > 0 ? `; flags: ${c.flags.join(', ')}` : ''}`);
}
