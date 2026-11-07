// `explore.mjs repartition --series <dir>` (F1.S1 (b)): the time partition of a kept session, recomputed from its retained evidence with the classifier as it is now, into a NEW dated result file. The sessions
// here are invented: stream lines, an audit log and a record made in an owned temp folder. No record is ever changed and no earlier result is ever touched.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync, readdirSync, renameSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { formatOwner, ownedFor, thisOwner, writeOwner } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { EXIT } from '../lib/commands.mjs';
import { RESULT_DIR, repartitionSeries, sumCheck } from '../lib/repartition.mjs';
import { main } from '../explore.mjs';

const T0 = Date.parse('2026-11-05T10:00:00Z');
const WALL_MS = 60_000;
const NOW = () => new Date('2026-11-07T08:09:10.123Z');
const owned = ownedFor({ token: 'bench' });
const OLD_OTHER = Object.freeze({ build_test: 0, model_api: 0, other_tool: 15_000, hooks: 0, coai_gate: 0, consultant: 0, ci_wait: 0, coderabbit_wait: 0, sleep300: 0, unattributed: 45_000 });

const auditLine = (event, id, atMs, command) => JSON.stringify({ ts: new Date(T0 + atMs).toISOString(), event, tool_use_id: id, tool_name: 'Bash', input: { command } });

/** One invented session under `<series>/s/<rid>/`: the evidence the partition reads, and a record holding the partition as the OLD classifier made it. */
function writeSession(series, rid, { stack = 'csharp', command = 'bash build.sh', omit = [], calls = 1 } = {}) {
  const dir = path.join(series, 's', rid);
  mkdirSync(path.join(dir, 'otel'), { recursive: true });
  mkdirSync(path.join(dir, 'box', 'out'), { recursive: true });
  const files = {
    'meta.json': JSON.stringify({ startedAt: new Date(T0).toISOString(), exitedAt: new Date(T0 + WALL_MS).toISOString() }),
    'stream.stamped.ndjson': `${JSON.stringify({ t: T0 + 1000, line: '{}' })}\n`,
    'stream.ndjson': '',
    'otel/otel-raw.ndjson': '',
    'box/out/audit.ndjson': `${Array.from({ length: calls }, (_, i) => [auditLine('PreToolUse', `c${i}`, 10_000 + i * 20_000, command), auditLine('PostToolUse', `c${i}`, 25_000 + i * 20_000, command)].join('\n')).join('\n')}\n`,
    'record.json': JSON.stringify({ ids: { rid, stack }, time: { partition: { known: true, value: { categories: OLD_OTHER, unattributed: 45_000, waits: 0 } } } }),
  };
  for (const [rel, text] of Object.entries(files)) if (!omit.includes(rel)) writeFileSync(path.join(dir, rel), text);
  return dir;
}

const seriesWith = (sessions) => {
  const series = tmpDir('repart');
  for (const [rid, options] of Object.entries(sessions)) writeSession(series, rid, options);
  return series;
};

const resultsOf = series => (readdirSync(path.join(series, RESULT_DIR), { withFileTypes: true }).filter(e => e.name.endsWith('.json')).map(e => e.name).sort());
const readResult = (series, name) => JSON.parse(readFileSync(path.join(series, RESULT_DIR, name), 'utf8'));
const run = (series, over = {}) => repartitionSeries({ seriesDir: series, now: NOW, owned, ...over });

test('The_recomputed_partition_moves_a_build_sh_call_from_other_tool_to_build_test_and_still_sums_to_the_wall', async () => {
  const series = seriesWith({ aaa111: {}, bbb222: { stack: 'node', command: 'python3 -I runner/run_samples.py samples/' } });
  const r = await run(series);
  assert.equal(r.ok, true, r.reason);
  const body = readResult(series, resultsOf(series)[0]);
  assert.equal(body.complete, true);
  for (const file of ['lib/classify.mjs', 'lib/smoke-verdicts.mjs', 'capture/verdict-rules.mjs']) assert.ok(body.codeSha256[file], `${file} is part of the fingerprint`);
  const csharp = body.sessions.find(s => s.rid === 'aaa111');
  assert.equal(csharp.stack, 'csharp');
  assert.deepEqual([csharp.old.categories.build_test, csharp.old.categories.other_tool], [0, 15_000], 'the old categories are read from record.json');
  assert.deepEqual([csharp.new.categories.build_test, csharp.new.categories.other_tool], [15_000, 0], 'the call is build_test now');
  const node = body.sessions.find(s => s.rid === 'bbb222');
  assert.equal(node.new.categories.build_test, 15_000, 'the visible samples runner is build_test too');
  for (const s of body.sessions) {
    assert.equal(s.sumsToWall, true, `${s.rid}: the categories sum to the wall`);
    assert.equal(Object.values(s.new.categories).reduce((a, b) => a + b, 0), s.new.wallMs);
  }
});

test('The_recompute_writes_a_new_dated_file_and_never_changes_a_record_or_an_earlier_file', async () => {
  const series = seriesWith({ aaa111: {} });
  const recordFile = path.join(series, 's', 'aaa111', 'record.json');
  const recordBefore = readFileSync(recordFile);
  mkdirSync(path.join(series, RESULT_DIR));
  const earlier = path.join(series, RESULT_DIR, '20261106T000000Z-deadbeef.json');
  const earlierText = '{"complete":true,"fingerprint":"an-earlier-classifier","sessions":[]}\n';
  writeFileSync(earlier, earlierText);
  const r = await run(series);
  assert.equal(r.ok, true, r.reason);
  assert.equal(r.value.wrote, true);
  assert.match(path.basename(r.value.file), /^20261107T080910Z-[0-9a-f]{8}\.json$/, 'a UTC timestamp and an 8 hex digit generation id');
  assert.deepEqual(readFileSync(recordFile), recordBefore, 'the session record keeps its bytes');
  assert.equal(readFileSync(earlier, 'utf8'), earlierText, 'the earlier result keeps its bytes and its name');
  assert.equal(resultsOf(series).length, 2, 'the new result sits beside the earlier one');
});

test('Two_runs_in_the_same_second_with_changed_inputs_write_two_results_with_two_names', async () => {
  const series = seriesWith({ aaa111: {} });
  const first = await run(series);
  const firstText = readFileSync(first.value.file, 'utf8');
  writeSession(series, 'aaa111', { calls: 2 });
  const second = await run(series);
  assert.equal(second.ok, true, second.reason);
  assert.equal(second.value.wrote, true, 'the evidence moved, so the fingerprint did');
  assert.notEqual(second.value.file, first.value.file);
  assert.notEqual(path.basename(second.value.file).split('-')[1], path.basename(first.value.file).split('-')[1], 'the generation ids differ');
  assert.equal(path.basename(second.value.file).split('-')[0], path.basename(first.value.file).split('-')[0], 'the same fixed second');
  assert.equal(readFileSync(first.value.file, 'utf8'), firstText, 'no file is replaced');
});

test('A_run_over_unchanged_inputs_reports_the_existing_complete_result_and_writes_nothing', async () => {
  const series = seriesWith({ aaa111: {}, bbb222: { stack: 'node' } });
  const first = await run(series);
  const listing = readdirSync(path.join(series, RESULT_DIR)).sort();
  const again = await run(series);
  assert.equal(again.ok, true, again.reason);
  assert.equal(again.value.wrote, false);
  assert.equal(again.value.file, first.value.file, 'its path is reported');
  assert.deepEqual(readdirSync(path.join(series, RESULT_DIR)).sort(), listing, 'the folder holds the same entries');
});

/** A claim naming a process that is gone: a child that has already exited. */
function deadClaim(target) {
  const gone = spawnSync(process.execPath, ['-e', '']);
  writeFileSync(`${target}.owner`, `${formatOwner({ ...thisOwner(), pid: gone.pid, start: 1 })}\n`);
}

test('A_temporary_file_left_by_a_killed_run_is_never_read_as_a_result_and_a_retry_completes', async () => {
  const series = seriesWith({ aaa111: {} });
  const done = await run(series);
  const fullBody = readFileSync(done.value.file, 'utf8');
  const folder = path.join(series, RESULT_DIR);
  const live = path.join(folder, `${owned.ownedName('partition')}.tmp`);
  renameSync(done.value.file, live);
  writeOwner(live);
  const stale = path.join(folder, `${owned.ownedName('partition')}.tmp`);
  writeFileSync(stale, fullBody.slice(0, 30));
  deadClaim(stale);
  const retry = await run(series);
  assert.equal(retry.ok, true, retry.reason);
  assert.equal(retry.value.wrote, true, 'a temporary file is never read as a result, even a complete one');
  assert.equal(resultsOf(series).length, 1, 'one complete result under a new name');
  assert.deepEqual(retry.value.stale.removed, [stale], 'the stale file, whose owner is gone, is removed');
  assert.deepEqual(readdirSync(folder).filter(n => n.startsWith(path.basename(stale))), [], 'with its claim');
  assert.deepEqual(retry.value.stale.live, [live], 'a file whose owner still runs is named');
  assert.equal(readFileSync(live, 'utf8'), fullBody, 'and left exactly as it is');
});

test('A_session_whose_stamped_stream_is_missing_is_listed_with_its_reason_and_never_as_zero', async () => {
  const series = seriesWith({ aaa111: {}, ccc333: { omit: ['stream.stamped.ndjson'] } });
  const r = await run(series);
  assert.equal(r.ok, true, r.reason);
  const body = readResult(series, resultsOf(series)[0]);
  assert.deepEqual(body.sessions.map(s => s.rid), ['aaa111'], 'only a session that was recomputed has a partition');
  assert.equal(body.skipped.length, 1);
  assert.equal(body.skipped[0].rid, 'ccc333');
  assert.match(body.skipped[0].why, /stream\.stamped\.ndjson/);
});

test('The_repartition_verb_prints_the_result_and_a_folder_that_is_not_a_series_is_refused', async () => {
  const series = seriesWith({ aaa111: {} });
  const said = [];
  const code = await main(['repartition', '--series', series], { home: tmpDir('repart-home'), say: line => said.push(line) });
  assert.equal(code, EXIT.done);
  assert.match(said.join('\n'), new RegExp(`${RESULT_DIR}/\\d{8}T\\d{6}Z-[0-9a-f]{8}\\.json`));
  const refused = [];
  assert.equal(await main(['repartition', '--series', tmpDir('repart-empty')], { home: tmpDir('repart-home'), say: line => refused.push(line) }), EXIT.config);
  assert.equal(await main(['repartition'], { home: tmpDir('repart-home'), say: () => {} }), EXIT.config, 'the series is required');
});

test('An_entry_under_s_that_is_a_link_or_a_file_is_listed_as_not_recomputed_and_never_dropped', async () => {
  const series = seriesWith({ aaa111: {} });
  symlinkSync(path.join(series, 's', 'aaa111'), path.join(series, 's', 'linked'));
  writeFileSync(path.join(series, 's', 'stray.txt'), 'x');
  const r = await run(series);
  assert.equal(r.ok, true, r.reason);
  const body = readResult(series, resultsOf(series)[0]);
  assert.deepEqual(body.sessions.map(s => s.rid), ['aaa111'], 'a link is not followed into a second copy of the session');
  assert.deepEqual(body.skipped.map(s => s.rid), ['linked', 'stray.txt']);
  assert.ok(body.skipped.every(s => /not a folder/.test(s.why)));
});

test('A_partition_that_does_not_sum_to_the_wall_says_by_how_much', () => {
  assert.deepEqual(sumCheck({ wallMs: 10, categories: { a: 3, b: 7 } }), { sumsToWall: true });
  const off = sumCheck({ wallMs: 10, categories: { a: 3, b: 4 } });
  assert.equal(off.sumsToWall, false);
  assert.match(off.why, /sum to 7 ms.*wall is 10 ms/);
});
