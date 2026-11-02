// Fixtures of the olympiad tests: a held-out folder in a temp home, written the way `gen.py` leaves one (directories 0700, files 0600, a manifest of every file's sha256, brute.py and handcheck.md beside it), so no test ever
// reads or writes a real `heldout-NN/`. Not a test file (no `.test.mjs`), so the suite's glob does not run it.
import { createHash } from 'node:crypto';
import { chmodSync, mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { materialOf } from '../lib/material.mjs';
import { resolveTask, taskPaths } from '../lib/tasks.mjs';
import { loadWorld } from '../lib/world.mjs';

const sha256 = text => createHash('sha256').update(text).digest('hex');

/** Write `files` (`{ rel: text }`) under `dir`, private. */
function writeFiles(dir, files) {
  for (const [rel, text] of Object.entries(files)) {
    const target = path.join(dir, rel);
    for (let d = path.dirname(target); d.length >= dir.length; d = path.dirname(d)) { mkdirSync(d, { recursive: true, mode: 0o700 }); chmodSync(d, 0o700); }
    writeFileSync(target, text, { mode: 0o600 });
  }
}

/** The ids `h001`, `h002`, ... for `n` cases. */
export const caseIdsOf = n => Array.from({ length: n }, (_, i) => `h${String(i + 1).padStart(3, '0')}`);

/**
 * A held-out folder for `task` in `home`: `cases` is `[{ id, input, output }]`, `refs` `{ csharp: { rel: text }, node: { rel: text } }`, `mutants` `{ id: { stack, expectedFail, files } }`, `brute` and `handcheck` the texts
 * of those two files or null (absent). `traps` are case ids. -> `{ dir }`.
 */
export function writeHeldout({ home, task, cases, gen = '# gen\n', refs = { csharp: { 'src/Program.cs': '// ref' }, node: { 'src/main.ts': '// ref' } }, mutants = {}, brute = null, handcheck = null, traps = [] }) {
  const dir = taskPaths(task, home).heldoutDir;
  const files = {
    ...Object.fromEntries(cases.flatMap(c => [[`cases/${c.id}.in`, c.input], [`cases/${c.id}.out`, c.output]])),
    'cases/cases.txt': cases.map(c => `${c.id} R1 byte-exact\n`).join(''), 'gen.py': gen,
    ...Object.fromEntries(Object.entries(refs).flatMap(([stack, fs]) => Object.entries(fs).map(([rel, text]) => [`refs/${stack}/${rel}`, text]))),
    ...Object.fromEntries(Object.entries(mutants).flatMap(([id, m]) => Object.entries(m.files ?? { 'src/x': '// m' }).map(([rel, text]) => [`mutants/${id}/${rel}`, text]))),
  };
  const listed = { ...files, ...(brute === null ? {} : { 'brute.py': brute }), ...(handcheck === null ? {} : { 'handcheck.md': handcheck }) };
  const manifest = {
    caseCount: cases.length, cases: Object.fromEntries(cases.map(c => [c.id, { rids: ['R1'], trap: traps.includes(c.id) }])), traps,
    mutants: Object.fromEntries(Object.entries(mutants).map(([id, m]) => [id, { stack: m.stack, expectedFail: m.expectedFail, ...(m.strict === undefined ? {} : { strict: m.strict }) }])), files: Object.fromEntries(Object.entries(listed).map(([rel, text]) => [rel, sha256(text)])),
  };
  writeFiles(dir, { ...files, 'manifest.json': `${JSON.stringify(manifest, null, 2)}\n`, ...(brute === null ? {} : { 'brute.py': brute }), ...(handcheck === null ? {} : { 'handcheck.md': handcheck }) });
  return { dir };
}

/** A handcheck.md of `n` cases: a table with one row per case, each naming a case id (`h001`, `h002`, ...). */
export const handcheckOf = n => `# Hand check\n\n| case | input | answer |\n|---|---|---|\n${Array.from({ length: n }, (_, i) => `| h${String(i + 1).padStart(3, '0')} | worked by hand | the answer |`).join('\n')}\n`;

/** A brute.py over the toy task "print twice the number": exit 3 (refused) for a number over `limit`, `wrong` ids' answers one too high. */
export const bruteTwice = ({ limit = 1_000_000, wrongFor = [] } = {}) => `import sys
n = int(open(sys.argv[1]).read())
if n > ${limit}:
    sys.exit(3)
wrong = ${JSON.stringify(wrongFor)}
print(2 * n + (1 if n in wrong else 0))
`;

/** `count` cases of the toy task (input n, output 2n), the numbers 1..count, the last one \`bigLast\` times bigger when asked. */
export const toyCases = (count, { bigLast = 1 } = {}) => caseIdsOf(count).map((id, i) => {
  const n = (i + 1) * (i === count - 1 ? bigLast : 1);
  return { id, input: `${n}\n`, output: `${2 * n}\n` };
});

// ---- a toy task for the end-to-end tests ------------------------------------------------------------------------------------------------------
// "Print twice the number": a spec in a temp git repository (pinned by sha256 like a real one), a held-out folder with working references in both stacks, a calibration file and private notes, all in ONE temp directory.
// Task entries point at it through path parts that climb out of the home (`taskPaths` joins parts under the home), so the real toolchains, snapshots and pins of this host are used while no real `heldout-NN/`,
// calibration file or private-notes file is read or written. A test needing a toy task for ids 02, 03 or 09 gets its entry replaced in a COPY of the pins.
import { execFileSync } from 'node:child_process';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';

export const TOY_SPEC = `# Task 77 — Doubler

| | |
|---|---|
| Kind | a program that reads standard input once and writes standard output once |
| Gate verdict | not yet gated |

## 1. What the program does

**R1.** The program prints twice the number it reads.

## 6. Visible samples

The same two cases are in \`samples/\`.

| Id | Standard input | Expected standard output |
|---|---|---|
| \`s01\` | \`1⏎\` | \`2⏎\` |
| \`s02\` | \`21⏎\` | \`42⏎\` |

## 7. Hidden tests

**R2.** Hidden tests exist.
`;
const TOY_FORMAT = '# FORMAT\n\n## 3. Visible samples\n\ntoy\n\n## 6. Worked examples\n';
export const TOY_REFS = {
  csharp: { 'src/Program.cs': 'var n = long.Parse(Console.In.ReadToEnd().Trim());\nConsole.Out.Write($"{n * 2}\\n");\n' },
  node: { 'src/main.ts': "import { readFileSync } from 'node:fs';\n\nconst n: bigint = BigInt(readFileSync(0, 'utf8').trim());\nprocess.stdout.write(`${n * 2n}\\n`);\n" },
};

/** The parts that lead from `home` to `target` (they climb out with `..`), as `taskPaths` joins them. */
const partsTo = (home, target) => path.relative(home, target).split(path.sep);

/** A toy task repository: one commit holding the toy spec and FORMAT. -> `{ repo, spec, format }` pins (sha, path, sha256). */
function toyRepo() {
  const repo = tmpDir('o-toy-repo');
  const git = (...args) => execFileSync('git', ['-C', repo, '-c', 'user.name=t', '-c', 'user.email=t@invalid', ...args], { encoding: 'utf8' });
  git('init', '-q');
  mkdirSync(path.join(repo, 'todo', 'tasks'), { recursive: true });
  writeFileSync(path.join(repo, 'todo', 'tasks', 'toy.md'), TOY_SPEC);
  writeFileSync(path.join(repo, 'todo', 'tasks', 'FORMAT.md'), TOY_FORMAT);
  git('add', '.');
  git('commit', '-q', '-m', 'toy');
  const sha = git('rev-parse', 'HEAD').trim();
  const pin = (file, text) => ({ sha, path: `todo/tasks/${file}`, sha256: sha256(text) });
  return { repo, spec: pin('toy.md', TOY_SPEC), format: { ...pin('FORMAT.md', TOY_FORMAT), label: 'v2.1' } };
}

/**
 * A copy of the pins in which each of `ids` is a toy task (its own held-out folder with working references, calibration file and private notes, all under one temp directory), and a `worldFn` that loads the real
 * toolchains with the toy spec. `home` is the home the commands run with (the real one: toolchains, snapshots and the pinned CLI are read from it). `planTasks` replaces the pause point's task list.
 * -> `{ explore, worldFn, exploreFn, repo, root }`.
 */
export function toyExplore({ real, home, ids, planTasks = ids }) {
  const root = tmpDir('o-toy');
  const { repo, spec, format } = toyRepo();
  const explore = JSON.parse(JSON.stringify(real));
  explore.olympiad.planTasks = planTasks;
  for (const id of ids) {
    const notes = path.join(root, `notes-${id}.md`);
    writeFileSync(notes, `notes of toy task ${id}\n`);
    const calibration = path.join(root, `calibration-${id}.json`);
    writeFileSync(calibration, JSON.stringify({ task: id, limits: { timeMs: 4000, memoryBytes: 536870912, buildTimeoutMs: 600000 } }));
    explore.tasks[id] = {
      title: `Toy ${id}`, spec, format, privateNotes: { relativePath: partsTo(home, notes).join('/'), sha256: sha256(`notes of toy task ${id}\n`) },
      heldoutDirParts: partsTo(home, path.join(root, `heldout-${id}`)), calibrationParts: partsTo(home, calibration), noteLabel: id, draft: false,
    };
  }
  return { explore, repo, root, exploreFn: () => ok(explore), worldFn: a => loadWorld({ ...a, exploreFn: () => ok(explore), candidates: [repo] }) };
}

/** The toy held-out folder of `task` (working references, six cases of "print twice"), written where `toyExplore` pointed it. */
export function writeToyHeldout({ home, task }) {
  return writeHeldout({ home, task, cases: toyCases(6), refs: TOY_REFS });
}

/** A home with a held-out folder for `id` and a calibration file bound to it (what `calibrate` leaves), written without running a calibration. */
export function calibratedHome(id, real) {
  const home = tmpDir(`o-cal-${id}`);
  const task = resolveTask(real, id).value;
  writeHeldout({ home, task, cases: toyCases(15), brute: bruteTwice(), handcheck: handcheckOf(6) });
  const file = taskPaths(task, home).calibrationFile;
  mkdirSync(path.dirname(file), { recursive: true });
  writeFileSync(file, JSON.stringify({ task: id, limits: { timeMs: 2000, memoryBytes: 536870912, buildTimeoutMs: 600000 }, material: materialOf(task, home) }));
  return home;
}
