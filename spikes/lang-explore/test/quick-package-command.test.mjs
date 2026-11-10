// The command `package` (F2.S1, section 2.7, scenarios 34 and 35, and the plan round's finding 5): the lock, the quiet check, the resume from the records and the order register, then seal, over an INVENTED package and fake seams. No box.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { main } from '../explore.mjs';
import { acquireSeriesLock } from '../../minigit-bench/lib/series-lock.mjs';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { casesTreeHash } from '../lib/heldout-quick.mjs';
import { exploreRoot } from '../lib/paths.mjs';
import { packageRoot } from '../lib/package-record.mjs';
import { TOKEN, commandEnv, filesOf, sealSeams } from './quick-package-fixture.mjs';

const ALL_CAUGHT = ({ id, n }) => (id === 'MXX-alpha-c' || id === 'MXX-alpha-n' ? { failing: { h001: 'wrong answer', h002: 'wrong answer' } } : (id === 'MXX-beta-n' ? (n % 5 === 1 ? { failing: { h003: 'wrong answer' } } : 'clean') : 'clean'));
const argv = (...more) => ['package', '--task', '05', ...more];
const recordsOf = env => readdirSync(packageRoot(env.io.home, env.p.task)).filter(n => /^\d{8}T/.test(n)).map(n => path.join(packageRoot(env.io.home, env.p.task), n));

test('package_is_a_free_command_that_runs_on_the_scoring_CPUs_under_the_series_lock', async () => {
  const env = commandEnv({ plan: ALL_CAUGHT });
  const lockFile = path.join(exploreRoot(env.io.home), 'series.lock');
  let heldWhileRunning = null;
  env.io.quietFn = async () => { heldWhileRunning = existsSync(lockFile) ? JSON.parse(readFileSync(lockFile, 'utf8')).pid : null; return ok({ quiet: true }); };
  const code = await main(argv(), env.io);
  assert.equal(code, 0, env.lines.join('\n'));
  assert.equal(heldWhileRunning, process.pid, 'the lock of the explore root is held while the command works');
  assert.equal(existsSync(lockFile), false, 'and released after it');
  assert.equal(env.world.log.length > 0, true);
  const holder = acquireSeriesLock({ seriesDir: exploreRoot(env.io.home) });
  const busy = commandEnv({ plan: ALL_CAUGHT, over: { home: env.io.home } });
  const refused = await main(argv(), { ...busy.io, home: env.io.home, exploreFn: env.io.exploreFn });
  holder.value.release();
  assert.equal(refused, 3, 'a held lock is a resumable halt');
  assert.ok(busy.lines.some(l => l.includes(String(process.pid))), 'the holder is named');
  assert.equal(busy.world.log.length, 0, 'nothing ran beside the holder');
});

test('A_machine_that_is_not_quiet_halts_the_command_resumably_before_anything_is_scored', async () => {
  const env = commandEnv({ plan: ALL_CAUGHT, over: { quietFn: async () => ({ ok: false, reason: 'the load average is 9.1' }) } });
  assert.equal(await main(argv(), env.io), 3);
  assert.ok(env.lines.some(l => /not quiet: the load average is 9\.1/.test(l)), env.lines.join('\n'));
  assert.equal(env.world.log.length, 0);
});

test('A_whole_package_run_is_green_and_writes_one_record_with_counts_and_no_case_text', async () => {
  const env = commandEnv({ plan: ALL_CAUGHT });
  assert.equal(await main(argv(), env.io), 0, env.lines.join('\n'));
  const [run] = recordsOf(env);
  const record = JSON.parse(readFileSync(path.join(run, 'record.json'), 'utf8'));
  assert.deepEqual(Object.keys(record.items).sort(), ['gate', 'load', 'refs', 'suite', 'witnesses'].sort().filter(k => k !== 'load' || record.items.load));
  assert.equal(record.items.witnesses.caught, 3);
  assert.equal(record.items.refs.csharp.verdict, 'accepted');
  assert.equal(record.referenceVerdicts.node, 'accepted');
  assert.equal(record.memoryBytes > 0 && Number.isInteger(record.memoryBytes), true);
  assert.equal(env.lines.join('\n').includes(TOKEN), false, 'what the command says holds no case text');
  assert.ok(env.lines.some(l => /witness 1 pass 1 .*started/.test(l)), 'the progress lines of the witnesses');
});

test('A_halted_package_check_resumes_from_its_records_and_runs_only_what_is_missing', async () => {
  const ctl = new AbortController();
  let calls = 0;
  const plan = args => { calls += 1; if (args.stack === 'csharp' && args.id === 'reference' && args.n === 6) ctl.abort(); return ALL_CAUGHT(args); };
  const env = commandEnv({ plan });
  const first = await main(argv(), { ...env.io, signal: ctl.signal });
  assert.equal(first, 3, 'a stop after the sixth pass of the C# reference is a resumable halt');
  const [run] = recordsOf(env);
  assert.equal(existsSync(path.join(run, 'record.json')), false, 'a halted run has no record');
  assert.equal(existsSync(path.join(run, 'halt-1.json')), true);
  assert.equal(calls, 6);
  const attemptsBefore = filesOf(path.join(run, 'refs-csharp', 'passes'));
  assert.equal(Object.keys(attemptsBefore).length, 6);
  const second = await main(argv(), env.io);
  assert.equal(second, 0, env.lines.join('\n'));
  assert.deepEqual(recordsOf(env), [run], 'the second run resumed the same folder');
  const csharpRef = env.world.log.filter(l => l.stack === 'csharp' && l.owner !== undefined);
  assert.equal(calls - 6 > 0, true);
  const attemptsAfter = filesOf(path.join(run, 'refs-csharp', 'passes'));
  for (const [name, text] of Object.entries(attemptsBefore)) assert.equal(attemptsAfter[name], text, `${name} is written once and never rewritten`);
  assert.equal(Object.keys(attemptsAfter).length, 10);
  const ranAgain = env.world.log.slice(6).filter(l => l.stack === 'csharp' && l.n <= 6 && !String(l.tree).includes('witness'));
  assert.equal(csharpRef.length > 0, true);
  assert.equal(ranAgain.filter(l => l.owner === env.world.log[0].owner && l.n <= 6).length, 0, 'the passes already recorded are not run again');
  assert.equal(existsSync(path.join(run, 'record.json')), true);
});

test('A_run_halted_in_the_witnesses_resumes_without_running_the_references_or_the_finished_witnesses_again', async () => {
  const ctl = new AbortController();
  const plan = args => { if (args.id === 'MXX-beta-n' && args.n === 3) ctl.abort(); return ALL_CAUGHT(args); };
  const env = commandEnv({ plan });
  assert.equal(await main(argv(), { ...env.io, signal: ctl.signal }), 3);
  const before = env.world.log.length;
  assert.equal(await main(argv(), env.io), 0, env.lines.join('\n'));
  const again = env.world.log.slice(before);
  assert.deepEqual([...new Set(again.map(l => l.id))], ['MXX-beta-n'], 'only the witness that was interrupted runs again: the references and the finished witnesses are read from their step files');
  assert.equal(again.filter(l => l.n <= 2).length, 0, 'and of it only the passes not recorded');
});

test('A_red_item_stops_before_registration_and_nothing_is_sealed', async () => {
  const env = commandEnv({ plan: ({ id, n }) => (id === 'MXX-alpha-c' ? 'clean' : ALL_CAUGHT({ id, n })) });
  env.io.heldout = sealSeams({ frozenFile: env.files.frozenFile, packagesFile: env.files.packagesFile });
  const code = await main(argv('--register', '--seal'), env.io);
  assert.equal(code, 1, 'a witness that is not caught is a red item');
  assert.equal(existsSync(env.files.packagesFile), false, 'nothing is registered');
  assert.equal(existsSync(path.join(env.p.dir, 'suite.enc')), false, 'nothing is sealed');
  assert.equal(existsSync(env.p.casesDir), true);
});

test('A_failed_seal_after_registration_leaves_the_clear_cases_and_a_second_run_resumes_it', async () => {
  const env = commandEnv({ plan: ALL_CAUGHT });
  const seams = sealSeams({ frozenFile: env.files.frozenFile, packagesFile: env.files.packagesFile, hooks: { afterArchive: () => { throw new Error('killed between the archive and its entry'); } } });
  const before = casesTreeHash(env.p.casesDir).value;
  const first = await main(argv('--register', '--seal'), { ...env.io, heldout: seams });
  assert.equal(first, 5, 'the interrupted seal is a failure with a sentence');
  assert.ok(env.lines.some(l => /seal of task 05 was interrupted/.test(l) && /package --task 05 --seal/.test(l)), env.lines.join('\n'));
  const registered = JSON.parse(readFileSync(env.files.packagesFile, 'utf8'))['05'];
  assert.equal(registered.archiveSha256, undefined, 'the package was registered first, before the archive existed');
  assert.equal(existsSync(path.join(env.p.dir, 'suite.enc')), true, 'the archive was published');
  assert.equal(existsSync(env.files.frozenFile), false, 'its entry was not written');
  assert.equal(casesTreeHash(env.p.casesDir).value, before, 'the clear cases are exactly as they were');
  const second = await main(argv('--seal'), { ...env.io, heldout: { ...seams, hooks: {} } });
  assert.equal(second, 0, env.lines.join('\n'));
  const sealed = JSON.parse(readFileSync(env.files.frozenFile, 'utf8')).tasks['05'];
  const entry = JSON.parse(readFileSync(env.files.packagesFile, 'utf8'))['05'];
  assert.equal(entry.archiveSha256, sealed.archiveSha256, 'the archive\'s hash joined the registered entry, and it is the registry\'s');
  assert.notEqual(entry.identity, registered.identity, 'a sealed package is a new identity');
  assert.equal(casesTreeHash(env.p.casesDir).value, before);
  assert.ok(env.lines.some(l => /sealed: the archive of task 05 is resumed/.test(l)), env.lines.join('\n'));
});

test('A_seal_alone_refuses_a_package_with_no_complete_record', async () => {
  const env = commandEnv({ plan: ALL_CAUGHT });
  env.io.heldout = sealSeams({ frozenFile: env.files.frozenFile, packagesFile: env.files.packagesFile });
  assert.equal(await main(argv('--seal'), env.io), 4);
  assert.ok(env.lines.some(l => /no complete record/.test(l)), env.lines.join('\n'));
  assert.equal(existsSync(path.join(env.p.dir, 'suite.enc')), false);
});
