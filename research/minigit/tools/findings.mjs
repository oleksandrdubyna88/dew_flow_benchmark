// The prediction check and the instrument-defect table of the results record.
// The prediction thresholds are the registered ones (spikes/minigit-bench/inputs/prediction.md, items 1, 2, 5, 7);
// the observed values come only from research/minigit/data/<series>.json.
// The defect list is hand-written (names, finders, effects), and every commit and harness identity in it is
// CHECKED here: the commit must exist in git and the harness must have a dated line in inputs/amendments.md.
import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { ratio, table, usd } from './format.mjs';

const verdict = (held, why) => `${held === true ? '**held**' : held === false ? '**not held**' : '**not decidable**'} — ${why}`;
const inRange = (x, lo, hi) => x >= lo && x <= hi;

/** Items 1, 2, 5 and 7 against the pairs reached. n is the number of complete pairs; 3 were registered. */
export function prediction(d) {
  const a = d.summary.a.per;
  const n = a.costUsd.n;
  const missing = 3 - n;
  const kOf = (k, need) => (k >= need ? true : k + missing < need ? false : null);
  const cells = st => d.pairs.map(p => p.cells[st]).filter(c => !c.missing && c.values.costUsd !== null).map(c => c.values.costUsd);
  const [cs, ts] = [cells('csharp'), cells('node')];
  const q = d.quality;
  const scored = Object.values(q).reduce((x, v) => x + v.scored, 0);
  const visibleAll = d.sessions.filter(s => s.kind === 'data' && s.quality?.scored).every(s => s.quality.visible.every(v => v.passed === v.expectedTotal));
  const minPct = Math.min(...Object.values(q).filter(v => v.scored).map(v => v.percentMin));
  const anyFailure = d.sessions.some(s => s.kind === 'data' && s.quality?.heldout && s.quality.heldout.percent < 100);
  const defDiff = Math.max(...['v1', 'v2'].map(ph => Math.abs((q[`csharp-${ph}`].defectsMedian ?? 0) - (q[`node-${ph}`].defectsMedian ?? 0))));
  const tsGe = d.pairs.filter(p => p.ratios.costUsd.known && p.ratios.costUsd.value <= 1).length;
  const k1 = kOf(a.costUsd.csharpHigher, 2);
  const range = m => `n = ${m.n}, range ${ratio(m.min)}–${ratio(m.max)}`;
  const cellsOk = ts.every(x => inRange(x, 0.8, 2.5)) && cs.every(x => inRange(x, 1.0, 3.5));
  const allBelow = ts.every(x => x < 0.8) && cs.every(x => x < 1.0);
  const unscored = Object.values(q).flatMap(v => v.unscored);
  const rows = [
    ['1', 'C# > TS in ≥ 2 of 3 pairs (cost)', `${a.costUsd.csharpHigher} of ${n}`, verdict(k1, missing === 0 ? `all ${n} pairs present` : k1 === false ? `${missing} pair(s) missing; with them k could still not reach 2` : `${missing} pair(s) missing`)],
    ['1', 'median C#/TS cost 1.15–1.5×', ratio(a.costUsd.median), verdict(inRange(a.costUsd.median, 1.15, 1.5), range(a.costUsd))],
    ['1', 'per cell: TS $0.8–2.5, C# $1.0–3.5', `TS ${ts.map(x => usd(x, 2)).join(', ')}; C# ${cs.map(x => usd(x, 2)).join(', ')}`, verdict(cellsOk, allBelow ? 'every complete cell is below its predicted range' : 'see the cells')],
    ['2', 'wall C#/TS 1.3–1.8×', ratio(a.wallMs.median), verdict(inRange(a.wallMs.median, 1.3, 1.8), range(a.wallMs))],
    ['2', 'API-time ratio 1.1–1.4×', ratio(a.modelApiMs.median), verdict(inRange(a.modelApiMs.median, 1.1, 1.4), range(a.modelApiMs))],
    ['5', 'every bare session passes the visible tests', `${scored} scored data sessions`, verdict(visibleAll, `${visibleAll ? 'every' : 'not every'} scored session passes; not scored: ${unscored.join(', ') || 'none'}`)],
    ['5', 'held-out median percent ≥ 90 % in both stacks', `lowest per-phase median ${Math.min(...Object.values(q).filter(v => v.scored).map(v => v.percentMedian))} %`, verdict(minPct >= 90, `lowest scored session ${minPct} %`)],
    ['5', '… with at least one bare failure', anyFailure ? 'a failure' : 'no failure', verdict(anyFailure, anyFailure ? 'a scored data session failed a held-out case' : 'no scored data session failed a held-out case')],
    ['5', 'no stack difference > 1 defect', `largest per-phase median difference ${defDiff}`, verdict(defDiff <= 1, 'per-phase medians of `defects`')],
    ['7', 'surprise: TS costing ≥ C# in ≥ 2 bare pairs', `${tsGe} of ${n}`, tsGe >= 2 ? '**the named surprise occurred**' : verdict(null, 'fewer than 2 pairs')],
  ];
  return table(['item', 'registered expectation', 'observed (series pairs reached)', 'reading'], rows);
}

/** Hand-written: what each defect was. Checked below: commit exists, harness has a dated amendment line. */
export const DEFECTS = [
  { what: 'TypeScript toolchain flagged as an outside path', found: 'Node pilot 9804fc (series 92a9)', commits: ['5249048'], harness: 'c825944d587d',
    effect: 'every TypeScript session runs `tsc` from the pinned toolchain outside the work tree, so every TS session would be `void: contaminated` while C# (toolchain under /usr) was never flagged: no TS data point, and the second void stops the series. Against TS only.' },
  { what: 'contamination skeleton too wide', found: 'Opus review of the fix (B1)', commits: ['a382519'], harness: '500e09206354',
    effect: 'the allowed skeleton covered an ancestor of the harness dir and the home, so `grep -r` or `cd ../..` could reach the harness with no hit: a contaminated session would count as data.' },
  { what: '`~` and `$HOME` read as the operator home', found: 'Opus review of the fix (M1)', commits: ['fc6a226'], harness: '500e09206354',
    effect: 'ordinary commands on the box home (package caches) would be high hits; the review judged it C#-leaning. False voids.' },
  { what: 'a walk through a symbolic link', found: 'coai code round', commits: ['653a79c', 'c1bd510'], harness: '20ee8034dddc',
    effect: '`ln -s ../.. up && grep -r up` reached outside with no hit (false negative). The first fix (ee66350f9112) flagged every link walk, `/usr` links included, which would void sessions of both stacks; the second keeps high only under a protected root.' },
  { what: 'a script symlink made after a `cd`', found: 'coai code round', commits: ['4cb10ce'], harness: 'de9091b54e2b',
    effect: 'the link was resolved from the work tree, not from the `cd` target: false negative.' },
  { what: '`cd /` read as a walk from `/`', found: 'Node pilot c9b413 (series 0c1a)', commits: ['c475c99'], harness: '965745a2adcc',
    effect: 'a clean-up `cd / && rm -rf /tmp/…` voided the pilot. Any session that cleans up this way would be a false void.' },
  { what: 'heredoc bodies, `rm -rf` and a `//` in a quoted string read as walks', found: 'replay of A/A twin d3f571 after the `cd` fix', commits: ['179838c'], harness: '47bbc9f8f642',
    effect: 'a Makefile written by heredoc with `rm -rf` and a C# `//` comment would have voided a C# A/A twin. Mostly against C#.' },
  { what: 'a walk of a glob not judged by its prefix', found: 'team lead, during the replay', commits: ['27966ff'], harness: '7aacd09cee84',
    effect: '`grep -r token /*` was not seen as a walk of `/`: false negative.' },
  { what: 'a walker behind `xargs` (and other wrappers)', found: 'coai code round', commits: ['244bc47'], harness: '3129eeadfa8d',
    effect: '`… | xargs grep …` was not judged by the producer of the pipe: false negative.' },
  { what: 'quadratic scan', found: 'coai code round', commits: ['e5823c2', '9699634'], harness: '3129eeadfa8d',
    effect: 'heredoc and step building copied per line; a long session would scan slowly. No effect on a value.' },
  { what: 'the wall included the 3 s harness settle', found: 'A/A of series bf89 (G-6)', commits: ['edca65c'], harness: '179624f5fec2',
    effect: 'every wall of 92a9, 0c1a and the bf89 A/A and pilot is about 3 s too long and its `unattributed` too large (G-6). The bf89 data sessions ran after the fix. On a ~45 s session it moves each C#/TS wall ratio towards 1.' },
  { what: 'the stub CLI wrote no transcripts', found: 'the test suite after the T3 commit 255484e', commits: ['4687abe'], harness: 'eff387f2f438',
    effect: 'under `t3 = stop` the free v2 rehearsal would end as `T3 unchecked`, a series stop. No paid effect.' },
  { what: 'box supervision timers left running', found: 'coai code round', commits: ['97c9a78'], harness: 'faaf55b4ecf7',
    effect: 'a box that failed to start left its timers running. No session hit it.' },
  { what: 'per-call `cwd` in the contamination scan', found: 'data sessions ebd0b0 (pair 2) and 3407d8 (pair 3), series bf89', commits: ['6b09da2'], harness: '9475b1fed23f',
    effect: 'the scan started every Bash call at the work tree. Both C# v2 sessions ran `cd src` in one call and `cd .. && bash test-v2.sh` in the next, so the scan put `test-v2.sh` one level above the work tree: a high hit and a void. Two false voids, one C# re-run `ran unpaired`, and series bf89 stopped at the second. Against C# in this series.' },
  { what: 'the printed "Shell cwd was reset to" line chose the start', found: 'coai code round on the `cwd` fix', commits: ['52bdec2'], harness: '814d82e66e53',
    effect: 'a command can print that line and choose where the scan thinks it is: false negative. Fixed before series b82e.' },
  { what: 'a heredoc run by an interpreter (`python3 - <<\'EOF\'`) parsed as shell', found: 'data session 97168d (pair 2 TS v2), series b82e', commits: ['9dac39e'], harness: '198106f80a8b',
    effect: 'the agent edited its TypeScript through an inline Python script; the scan read the script body as shell, and a quoted `"/"` string as a walk of `/`: a high hit and a false void. Its re-run `6848b0` ran unpaired, under `foreign load`. Against TS in this series. Fixed after series B, on `feat/minigit-bench-e3b-fix2`.' },
  { what: 'the ledger charged a session once per rid, not once per series and rid', found: 'the pair-1 read-back of series b82e (G-1, Opus judge)', commits: ['1bdbe75'], harness: '198106f80a8b',
    effect: 'series b82e reused the registered seed, so its rids repeat those of bf89, whose lines `init` had copied in; every b82e session with a repeated rid was not appended to its own ledger, and the money gates read too little (§ 8). No gate was near its limit. The same commit makes a by-hand verdict apply only to the series its evidence names.' },
  { what: 'an interpreter or shell launched through a wrapper or by its path did not get the heredoc rule', found: 'coai code round on the fix above', commits: ['1769c95'], harness: 'db8d360cf9a8',
    effect: '`env python3 - <<EOF` or `/usr/bin/python3 - <<EOF` would still be parsed as shell: the same false void as above.' },
];

function commitExists(repo, sha) {
  try { execFileSync('git', ['-C', repo, 'cat-file', '-e', `${sha}^{commit}`], { stdio: 'ignore' }); return true; } catch { return false; }
}

const AMENDMENTS = 'spikes/minigit-bench/inputs/amendments.md';

/** amendments.md as committed at `ref` (the branch that holds every dated line), or the working file when ref is empty. */
export function amendmentsText(repo, ref) {
  if (!ref) return readFileSync(join(repo, AMENDMENTS), 'utf8');
  return execFileSync('git', ['-C', repo, 'show', `${ref}:${AMENDMENTS}`], { encoding: 'utf8' });
}

export function amendmentsSource(repo, ref) {
  if (!ref) return `the working tree's \`${AMENDMENTS}\``;
  const sha = execFileSync('git', ['-C', repo, 'rev-parse', '--short=12', ref], { encoding: 'utf8' }).trim();
  return `\`${AMENDMENTS}\` at \`${ref}\` = \`${sha}\``;
}

/**
 * The proof of a defect, where data can give it: a voided session read again by the replay (data/replay.json, the CURRENT
 * scan), or a series' charges from the records against its own ledger (data/<series>.json `charges`). A defect with no
 * triggering session names its tests instead (hand-written: which tests, and that they were watched RED and GREEN).
 */
const PROOFS = {
  1: { replay: [['s-20261030-92a9', '9804fc']] },
  6: { replay: [['s-20261030-0c1a', 'c9b413']] },
  14: { replay: [['s-20261030-bf89', 'ebd0b0'], ['s-20261030-bf89', '3407d8']] },
  16: { replay: [['s-20261030-b82e', '97168d']] },
  17: { ledger: 's-20261030-b82e' },
  18: { tests: 'no triggering session (a coai finding). Proof: `spikes/minigit-bench/test/contamination-interpreter.test.mjs`, `A_wrapped_or_path_named_interpreter_reads_its_body_like_the_bare_one` and `A_wrapped_or_path_named_shell_parses_its_body_as_shell`: watched failing on the lib before `1769c95` and passing after (2026-10-31); `A_word_named_like_an_interpreter_that_runs_nothing_leaves_the_body_data` is a guard that passes on both' },
};

function proofOf(n, all, replay) {
  const p = PROOFS[n];
  if (!p) return '—';
  if (p.tests) return p.tests;
  if (p.ledger) {
    const d = all.find(x => x.series.seriesId === p.ledger);
    return `the records against the ledger (§ 10): ${d.charges.missingFromLedger.length} paid sessions of ${p.ledger} charged in the run-map, the record and the broker alike (${usd(d.charges.missingUsd)}) have no line in its own ledger; disagreements between run-map, record and broker: ${d.charges.disagree.length}`;
  }
  return p.replay.map(([series, rid]) => {
    const v = replay.summary.recordedVoids.find(x => x.series === series && x.rid === rid);
    if (!v) throw new Error(`replay.json has no recorded void ${series} ${rid}`);
    return `${series.slice(-4)} \`${rid}\` (recorded \`${v.recordedClass}\`): replay at the current lib, ${v.high} high and ${v.low} low hits`;
  }).join('; ') + ' (data/replay.json, § 5.5)';
}

export function defects(repo, ref, all, replay) {
  const amendments = amendmentsText(repo, ref);
  const problems = [];
  const rows = DEFECTS.map((x, i) => {
    for (const c of x.commits) if (!commitExists(repo, c)) problems.push(`commit ${c} not found`);
    if (!amendments.includes(`harness:${x.harness}`)) problems.push(`harness ${x.harness} has no amendment line`);
    return [i + 1, x.what, x.found, x.commits.map(c => `\`${c}\``).join(', '), `\`${x.harness}\``, x.effect, proofOf(i + 1, all, replay)];
  });
  if (problems.length) throw new Error(`defect table does not check: ${problems.join('; ')}`);
  return table(['#', 'defect', 'found by', 'fix commit', 'harness identity line', 'what it would have done to the result', 'proof'], rows);
}

/** Every dated instrument line of amendments.md, as registered. */
export function amendmentLines(repo, ref) {
  const lines = amendmentsText(repo, ref).split('\n').filter(l => /^\d{4}-\d{2}-\d{2} \| /.test(l));
  return table(['date', 'kind', 'subject', 'reason or evidence'], lines.map(l => { const [date, kind, subj, ...rest] = l.split(' | '); return [date, kind, subj, rest.join(' | ').replace(/\/home\/[^/]+\//g, '~/')]; }));
}
