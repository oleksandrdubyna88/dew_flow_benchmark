// The commands around the loop: `init` (the draw is made and recorded), `status`, `prune` (the reproducible copies go, the records stay), `quiet`, `leakcheck` (the held-out outputs as v1's needles), and the
// command line's usage errors. Each runs over a temp home; nothing here touches the operator's files.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { chmodSync, existsSync, mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { leakcheck } from '../../minigit-bench/lib/heldout-leak.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { main } from '../explore.mjs';
import { EXIT, cmdInit, cmdPrune, cmdQuiet, leakNeedles, statusLines } from '../lib/commands.mjs';
import { calibrationFile, exploreRoot } from '../lib/paths.mjs';
import { createSeries, drawMask, loadSeries } from '../lib/series.mjs';
import { loadExplore } from '../lib/inputs.mjs';

const EXPLORE = loadExplore().value;
const CALIBRATION = { limits: { timeMs: 2000, memoryBytes: 536870912, buildTimeoutMs: 600000 } };
const io = (home, over = {}) => { const said = []; return { home, said, say: l => said.push(l), ...over }; };

function homeWithCalibration() {
  const home = tmpDir('cmd-home');
  mkdirSync(exploreRoot(home), { recursive: true });
  writeFileSync(calibrationFile(home), JSON.stringify(CALIBRATION));
  return home;
}

test('Init_creates_a_series_with_the_mask_draw_recorded_and_refuses_without_a_calibration', async () => {
  const empty = io(tmpDir('no-cal'));
  assert.equal(await cmdInit({}, empty), EXIT.config);
  assert.match(empty.said[0], /explore\.mjs calibrate/);
  const home = homeWithCalibration();
  const ok = io(home);
  assert.equal(await cmdInit({}, ok), EXIT.done);
  assert.match(ok.said[0], /^series x-\d{8}-[0-9a-f]{4} created at /);
  assert.match(ok.said[1], /the mask draw \(recorded\): C# starts on CPUs (8-13|0-5) in pair 1/);
  const dir = ok.said[0].split(' created at ')[1];
  const series = loadSeries(dir).value;
  assert.equal(typeof series.state.draw.csharpHigh, 'boolean');
  assert.match(series.state.draw.method, /randomInt/);
});

test('Status_lists_the_series_the_pairs_and_every_session', () => {
  const home = homeWithCalibration();
  const made = createSeries({ home, explore: EXPLORE, calibration: CALIBRATION, draw: drawMask(() => 0) });
  const lines = statusLines({ dir: made.value.dir, ...loadSeries(made.value.dir).value });
  assert.match(lines[0], /^series x-.* spend \$0\.0000; draw: C# 0-5 in pair 1$/);
  assert.deepEqual(lines.slice(1), ['pair 1: csharp none, node none', 'pair 2: csharp none, node none', 'pair 3: csharp none, node none']);
});

test('Prune_removes_the_cache_and_scoring_copies_of_every_session_and_keeps_the_records', () => {
  const home = homeWithCalibration();
  const made = createSeries({ home, explore: EXPLORE, calibration: CALIBRATION });
  const session = path.join(made.value.dir, 's', 'abc123');
  for (const rel of ['box/nuget/a/f', 'box/npm/_cacache/x', 'box/home/.dotnet/state', 'score/judge/tree/f', 'box/work/src/main.ts', 'record.json', 'stream.ndjson']) {
    mkdirSync(path.dirname(path.join(session, rel)), { recursive: true });
    writeFileSync(path.join(session, rel), 'x');
  }
  chmodSync(path.join(session, 'box/nuget/a/f'), 0o444);
  chmodSync(path.join(session, 'box/nuget/a'), 0o555);
  const run = io(home);
  assert.equal(cmdPrune({ series: made.value.dir }, run), EXIT.done);
  assert.match(run.said[0], /^pruned 4 directories: /);
  for (const gone of ['box/nuget', 'box/npm', 'box/home/.dotnet', 'score']) assert.equal(existsSync(path.join(session, gone)), false, gone);
  for (const kept of ['box/work/src/main.ts', 'record.json', 'stream.ndjson']) assert.equal(existsSync(path.join(session, kept)), true, kept);
  const again = io(home);
  cmdPrune({ series: made.value.dir }, again);
  assert.match(again.said[0], /^pruned 0 directories: nothing to remove$/);
  assert.equal(cmdPrune({}, io(tmpDir('nothing'))), EXIT.config, 'no series is a refusal');
});

const QUIET = { hostCpus: 0.1, harnessCpus: 0.05, foreignCpus: 0, foreignAgents: [], memAvailablePct: 80, liveSessions: [], spinMs: 20 };

test('Quiet_prints_each_verdict_and_exits_3_when_the_machine_never_is_and_0_when_it_is_or_when_accepted_noisy', async () => {
  const home = homeWithCalibration();
  writeFileSync(path.join(exploreRoot(home), 'spin.json'), JSON.stringify({ medianMs: 20 }));
  const seams = sample => ({ samplerFn: async () => async () => sample, sleepFn: async () => {}, now: (() => { let t = 0; return () => (t += 7000); })() });
  const loud = io(home, { machineSeams: seams({ ...QUIET, hostCpus: 6, foreignCpus: 5.9 }) });
  assert.equal(await cmdQuiet({}, loud), EXIT.halt);
  assert.ok(loud.said.length > 3 && /not quiet/.test(loud.said[0]));
  assert.match(loud.said.at(-1), /the machine was not quiet/);
  const noisy = io(home, { machineSeams: seams({ ...QUIET, hostCpus: 6, foreignCpus: 5.9 }) });
  assert.equal(await cmdQuiet({ 'accept-noisy': 'testing' }, noisy), EXIT.done);
  assert.match(noisy.said.at(-1), /started anyway: started not quiet \(testing\)/);
  const quiet = io(home, { machineSeams: seams(QUIET) });
  assert.equal(await cmdQuiet({}, quiet), EXIT.done);
  assert.equal(quiet.said.at(-1), 'the machine is quiet');
  assert.equal(await cmdQuiet({}, io(tmpDir('no-spin'))), EXIT.config, 'an uncalibrated spin probe is a refusal');
});

test('The_leak_needles_are_the_held_out_output_lines_of_8_or_more_characters_and_v1s_leak_check_finds_them', async () => {
  const casesDir = tmpDir('leak-cases');
  writeFileSync(path.join(casesDir, 'a.out'), '1\n');
  writeFileSync(path.join(casesDir, 'b.out'), '27 82 41 124 62 31 94\n');
  writeFileSync(path.join(casesDir, 'c.out'), '27 82 41 124 62 31 94\n');
  assert.equal(leakNeedles({ casesDir }), '27 82 41 124 62 31 94', 'a short line is no needle, a repeated one is one');
  const repo = tmpDir('leak-repo');
  const git = (...args) => execFileSync('git', ['-C', repo, '-c', 'user.name=t', '-c', 'user.email=t@invalid', ...args], { encoding: 'utf8' });
  git('init', '-q');
  writeFileSync(path.join(repo, 'clean.md'), 'nothing here\n');
  git('add', '.');
  git('commit', '-q', '-m', 'clean');
  const clean = await leakcheck({ repoRoot: repo, tokensText: leakNeedles({ casesDir }), range: null });
  assert.equal(clean.ok, true, clean.reason);
  writeFileSync(path.join(repo, 'note.md'), 'a result: 27 82 41 124 62 31 94 and more\n');
  git('add', '.');
  git('commit', '-q', '-m', 'leak');
  const dirty = await leakcheck({ repoRoot: repo, tokensText: leakNeedles({ casesDir }), range: null });
  assert.equal(dirty.ok, false);
  assert.match(dirty.reason, /held-out text found in .*note\.md/);
  assert.ok(!dirty.reason.includes('27 82 41'), 'a hit names the file, never the needle');
});

test('The_command_line_refuses_an_unknown_command_and_an_unknown_flag_with_the_usage_exit', async () => {
  const quiet = process.stderr.write.bind(process.stderr);
  const written = [];
  process.stderr.write = chunk => { written.push(String(chunk)); return true; };
  try {
    assert.equal(await main(['frobnicate']), EXIT.config);
    assert.equal(await main(['status', '--nonsense']), EXIT.config);
    assert.equal(await main([]), EXIT.config);
  } finally {
    process.stderr.write = quiet;
  }
  assert.match(written.join(''), /the commands are prepare, calibrate, rehearse, init, quiet, run, status, note, prune, leakcheck/);
  assert.match(written.join(''), /usage error/);
});
