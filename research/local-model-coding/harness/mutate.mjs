#!/usr/bin/env node
// Frozen behavioural checks (mutation checks): break one decision of the spec in an arm's FINAL code and see whether the
// arm's own tests notice. A test that stays green against broken code is proven unable to catch what it claims to.
//   node mutate.mjs <arm>      → results/<arm>/mutation.json
// Per arm: a clean worktree of the arm's HEAD (the arm's clone is never touched); the scenario test classes run once
// unmutated (the baseline); then each mutant: applied, the test project built, the same classes run, the tree restored.
// KILLED = at least one test that passed in the baseline now fails; the intended tests (the scenarios the mutant breaks)
// are reported apart. A mutant whose anchor is missing or that does not compile is NOT APPLICABLE — never counted caught.
// The mutants are frozen here (2026-10-26) and the same for every arm. M6 was added and M1's intended list narrowed the same
// evening, after validation on T3 and C2 showed the rootless check disposes through a second call M1 does not touch.
import { execFileSync, spawnSync } from 'node:child_process';
import { existsSync, readFileSync, writeFileSync, mkdirSync, rmSync, symlinkSync } from 'node:fs';
import { join } from 'node:path';

const arm = process.argv[2];
const E = '/home/jinx/git/coai-exp';
const ARM = `${E}/arm-${arm}`;
const WT = `${E}/mut/${arm}`;
const OUT = `${E}/results/${arm}`;
const git = (dir, ...a) => execFileSync('git', ['-C', dir, ...a], { encoding: 'utf8', maxBuffer: 1 << 28 });
const LP = (dir) => `${dir}/.agents/conventions/tools/low-priority.mjs`;
const KNOWN_FAILING = /KimiRuntimeTests\.ALaunchThatNamesItsHome|KimiHomeTests\.AHomeWhoseDataPathPasses260|TheRoundsNumstatsAreCountedTests\.AnUnknownCommit/;

// ── the mutants ────────────────────────────────────────────────────────────────────────────────────────────────────────
const NEVER = 'if (DateTime.UtcNow.Year < 0) '; // a guard the compiler cannot prove false, so no unreachable-code warning
const editJson = (row, capability, standing) => (dir) => {
  const f = join(dir, 'shared/runtime-capabilities.json');
  const doc = JSON.parse(readFileSync(f, 'utf8'));
  const r = (doc.rows ?? doc.capabilities ?? []).find((x) => x.runtime === row && x.capability === capability);
  if (!r) return 'no kimi row for ' + capability;
  if (r.standing === standing) return `already ${standing}`;
  r.standing = standing; writeFileSync(f, JSON.stringify(doc, null, 2)); return null;
};
const MUTANTS = [
  { id: 'M1-no-junction-first', breaks: 'one-shot Kimi homes are no longer disposed junction-first (base code)',
    intended: ['AfterTheCheck_TheHomeIsGone_JunctionFirst', 'AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind'], // NOT the rootless check: it disposes through ConsultCheckScratch (M6)
    apply: (dir) => { const f = join(dir, 'src_mcp/runners/Vibe/VibeHomes.cs'); const s = readFileSync(f, 'utf8'); const a = '        KimiHome.DisposeAllUnder(folder, said =>';
      if (!s.includes(a)) return 'anchor missing'; writeFileSync(f, s.replace(a, `        ${NEVER}KimiHome.DisposeAllUnder(folder, said =>`)); return null; } },
  { id: 'M6-no-junction-first-rootless', breaks: "a rootless check's Kimi homes are no longer disposed junction-first (base code, ConsultCheckScratch)",
    intended: ['ARootlessCheck_KeepsTheHomeInItsScratch_AndDisposesItJunctionFirst'],
    apply: (dir) => { const f = join(dir, 'src_mcp/src/Server/Consultation/ConsultCheckScratch.cs'); const s = readFileSync(f, 'utf8'); const a = 'Runners.Kimi.KimiHome.DisposeAllUnder(folder, said =>';
      if (!s.includes(a)) return 'anchor missing'; writeFileSync(f, s.replace(a, NEVER + a)); return null; } },
  { id: 'M2-no-planner-arm', breaks: 'the planner has no kimi arm (the default arm: no confinement)',
    intended: ['ThePlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb', 'AKimiQuestionLaunch_IsTheShimRequest_InItsFolderOrItsCopy_OnTheRowsHome'],
    apply: (dir) => { const f = join(dir, 'src_mcp/core/QuestionConsult/ConfinementPlanner.cs'); const lines = readFileSync(f, 'utf8').split('\n');
      const i = lines.findIndex((l) => /"kimi"\s*=>/.test(l)); if (i < 0) return 'no "kimi" => arm';
      lines[i] = lines[i].replace('"kimi"', '"kimi-mutated"'); writeFileSync(f, lines.join('\n')); return null; } },
  { id: 'M3-disk-confined', breaks: 'kimi × disk stands "confined" instead of "unconfined"', intended: ['KimiHasThreeRows_EachCitingItsCells', 'AdmissionFlagsBothRows'],
    apply: editJson('kimi', 'disk', 'confined') },
  { id: 'M4-web-confined', breaks: 'kimi × web stands "confined" instead of "unmeasured"', intended: ['KimiHasThreeRows_EachCitingItsCells', 'ThePlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb'],
    apply: editJson('kimi', 'web', 'confined') },
  { id: 'M5-flags-accepted', breaks: 'a kimi plan with flags is no longer refused', intended: ['APlanWithFlags_IsRefusedForKimi'],
    apply: (dir) => { const f = join(dir, 'src_mcp/runners/Kimi/KimiConsultant.cs');
      const added = new Set(git(dir, 'diff', '-U0', 'b4e2ea96', 'HEAD', '--', 'src_mcp/runners/Kimi/KimiConsultant.cs').split('\n')
        .filter((l) => l.startsWith('+') && /throw new ArgumentException\(/.test(l)).map((l) => l.slice(1)));
      if (!added.size) return 'no added ArgumentException throw in KimiConsultant.cs';
      const lines = readFileSync(f, 'utf8').split('\n'); let n = 0;
      const out = lines.map((l) => (added.has(l) && /^\s*throw new ArgumentException\(/.test(l) ? (n++, l.replace(/^(\s*)throw /, `$1${NEVER}throw `)) : l));
      if (!n) return 'the added throw is an expression, not a statement'; writeFileSync(f, out.join('\n')); return null; } },
];

// ── the run ────────────────────────────────────────────────────────────────────────────────────────────────────────────
const head = git(ARM, 'rev-parse', 'HEAD').trim();
rmSync(WT, { recursive: true, force: true });
try { git(ARM, 'worktree', 'prune'); } catch { /* none */ }
mkdirSync(`${E}/mut`, { recursive: true });
git(ARM, 'worktree', 'add', '-q', '--detach', WT, head);
const name = git(ARM, 'config', '-f', '.gitmodules', '--get-regexp', '^submodule\\..*\\.path$').split('\n')
  .map((l) => l.split(' ')).find(([, p]) => p === '.agents/conventions')?.[0].replace(/^submodule\./, '').replace(/\.path$/, '');
git(ARM, 'config', `submodule.${name}.url`, `${ARM}/.agents/conventions`);
execFileSync('git', ['-c', 'protocol.file.allow=always', 'submodule', 'update', '--init', '-q', '.agents/conventions'], { cwd: WT });
if (existsSync(`${ARM}/.agents/conventions/node_modules`)) symlinkSync(`${ARM}/.agents/conventions/node_modules`, `${WT}/.agents/conventions/node_modules`);

const classes = [...new Set([
  ...git(WT, 'diff', '--name-only', 'b4e2ea96', 'HEAD', '--', 'src_mcp/tests').split('\n').filter((f) => /\.cs$/.test(f) && existsSync(join(WT, f)))
    .map((f) => f.split('/').pop().replace(/\.cs$/, '')),
  'CapabilityMatrixTests', 'ConfinementPlannerTests',
])].filter((c) => readFileSync(join(WT, 'src_mcp/tests', `${c}.cs`), 'utf8').includes(`class ${c}`));

const build = () => spawnSync('node', [LP(WT), 'dotnet', 'build', 'src_mcp/tests/CoaiMcp.Tests.csproj', '-c', 'Debug', '-m:4', '--disable-build-servers'],
  { cwd: WT, encoding: 'utf8', maxBuffer: 1 << 28 }).status === 0;
const run = (tag) => {
  const args = classes.flatMap((c) => ['--filter-class', `CoaiMcp.Tests.${c}`]);
  spawnSync('node', [LP(WT), './src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests', ...args, '--results-directory', `${WT}/.mut`, '--report-xunit-ctrf',
    '--report-xunit-ctrf-filename', `${tag}.json`], { cwd: WT, encoding: 'utf8', maxBuffer: 1 << 28 });
  const f = `${WT}/.mut/${tag}.json`;
  return existsSync(f) ? JSON.parse(readFileSync(f, 'utf8')).results.tests.map((t) => ({ name: t.name.replace('CoaiMcp.Tests.', ''), status: t.status })) : null;
};

const report = { arm, head, classes, started_utc: new Date().toISOString(), mutants: [] };
if (!build()) { report.error = 'the unmutated arm does not build'; writeFileSync(`${OUT}/mutation.json`, JSON.stringify(report, null, 2)); console.log(`${arm}: ${report.error}`); process.exit(1); }
const base = run('baseline');
const passing = new Set((base ?? []).filter((t) => t.status === 'passed').map((t) => t.name));
report.baseline = { tests: base?.length ?? 0, passed: passing.size, failed: (base ?? []).filter((t) => t.status === 'failed' && !KNOWN_FAILING.test(t.name)).map((t) => t.name) };
for (const m of MUTANTS) {
  git(WT, 'checkout', '-q', '--', '.');
  const why = m.apply(WT);
  if (why) { report.mutants.push({ id: m.id, breaks: m.breaks, verdict: 'not applicable', why }); continue; }
  if (!build()) { report.mutants.push({ id: m.id, breaks: m.breaks, verdict: 'not applicable', why: 'the mutant does not compile' }); continue; }
  const res = run(m.id) ?? [];
  const failedNow = res.filter((t) => t.status !== 'passed' && passing.has(t.name)).map((t) => t.name);
  const intended = m.intended.map((n) => {
    const cases = res.filter((t) => new RegExp(`\\.${n}(\\(|$)`).test(t.name));
    return { test: n, present: cases.length > 0, caught: cases.some((t) => t.status !== 'passed' && passing.has(t.name)) };
  });
  report.mutants.push({ id: m.id, breaks: m.breaks, verdict: failedNow.length ? 'killed' : 'SURVIVED', killedBy: failedNow, intended });
}
git(WT, 'checkout', '-q', '--', '.');
report.ended_utc = new Date().toISOString();
writeFileSync(`${OUT}/mutation.json`, JSON.stringify(report, null, 2));
try { git(ARM, 'worktree', 'remove', '--force', WT); } catch { /* left for a look */ }
const k = report.mutants.filter((m) => m.verdict === 'killed').length; const app = report.mutants.filter((m) => m.verdict !== 'not applicable').length;
console.log(`${arm}: ${k}/${app} applicable mutants killed (${report.mutants.map((m) => `${m.id.split('-')[0]} ${m.verdict}${m.intended ? ' [' + m.intended.filter((x) => x.present).map((x) => (x.caught ? '+' : '-')).join('') + ']' : ''}`).join(', ')})`);
