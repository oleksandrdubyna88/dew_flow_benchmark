// Every host-side read of a path the agent could write refuses a link (F4): one planted link to a host decoy per site. The decoy is a file
// in a temp dir holding a fake token-shaped value, never a real secret; a site is red when the decoy's content reaches anything it wrote.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, readdirSync, renameSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { readCaptureFiles } from '../capture/capture-files.mjs';
import { scanSessionDir } from '../lib/session-scan.mjs';
import { stageSmoke } from '../lib/smoke-load.mjs';
import { tokenTxtFact } from '../lib/token-txt.mjs';
import { copyTranscripts } from '../lib/transcripts.mjs';
import { fakeToken } from './broker-stub.mjs';
import { tmpDir } from './helpers.mjs';

const DECOY = `HOST-DECOY ${fakeToken()}\n`;
const decoy = () => { const f = path.join(tmpDir('af-decoy'), 'host-file.txt'); writeFileSync(f, DECOY); return f; };
const write = (file, text) => { mkdirSync(path.dirname(file), { recursive: true }); writeFileSync(file, text); };

/** A session dir holding every file the smoke stages as required, plus `extra` written by the caller. */
function sessionDir() {
  const dir = tmpDir('af-session');
  for (const rel of ['stream.ndjson', 'otel/otel-raw.ndjson', 'broker.ndjson', 'egress.ndjson']) write(path.join(dir, rel), '');
  for (const rel of ['argv.json', 'meta.json', 'readback.json', 'checks.json']) write(path.join(dir, rel), '{}\n');
  write(path.join(dir, 'box', 'out', 'audit.ndjson'), '{"event":"PreToolUse"}\n');
  for (const d of ['work', 'tmp']) mkdirSync(path.join(dir, 'box', d), { recursive: true });
  return dir;
}
const everyFileHolds = (root, text) => readdirSync(root, { recursive: true, withFileTypes: true }).filter(e => e.isFile())
  .some(e => readFileSync(path.join(e.parentPath, e.name), 'utf8').includes(text));

test('The_scan_lists_a_link_or_a_pipe_anywhere_in_the_session_dir_as_a_high_severity_finding_and_does_not_read_through_it', () => {
  const dir = sessionDir();
  const target = decoy();
  const real = fakeToken();
  writeFileSync(target, `x ${real} y`);
  symlinkSync(target, path.join(dir, 'box', 'work', 'notes.txt'));
  symlinkSync(target, path.join(dir, 'box', 'out', 'link.ndjson'));
  assert.equal(spawnSync('mkfifo', [path.join(dir, 'box', 'tmp', 'pipe')]).status, 0);
  const scan = scanSessionDir(dir, { real });
  assert.deepEqual(scan.links.map(l => [l.rel, l.kind, l.severity]).sort(), [
    [path.join('box', 'out', 'link.ndjson'), 'symlink', 'high'], [path.join('box', 'tmp', 'pipe'), 'other', 'high'], [path.join('box', 'work', 'notes.txt'), 'symlink', 'high'],
  ]);
  assert.deepEqual(scan.hits, [], 'the target was not read: the finding is the link');
  assert.equal(scanSessionDir(sessionDir(), { real }).links.length, 0, 'control: a tree of regular files has no link finding');
});

test('Staging_refuses_a_link_at_a_staged_path_and_copies_nothing_of_its_target', () => {
  const dir = sessionDir();
  const target = decoy();
  const audit = path.join(dir, 'box', 'out', 'audit.ndjson');
  rmSync(audit);
  symlinkSync(target, audit);
  const to = path.join(tmpDir('af-stage'), 'staged');
  const r = stageSmoke(dir, to);
  assert.equal(r.ok, false, 'the review planted exactly this link and the staging copied the host file as the audit');
  assert.match(r.reason, /box.out.audit\.ndjson.*symlink/);
  assert.equal(existsSync(to) && everyFileHolds(to, 'HOST-DECOY'), false);
});

test('Staging_refuses_a_link_inside_the_transcripts_and_a_linked_transcripts_directory', () => {
  const target = decoy();
  const outside = tmpDir('af-outside');
  write(path.join(outside, 'x.jsonl'), DECOY);
  const a = sessionDir();
  write(path.join(a, 'transcripts', 'p', 'ok.jsonl'), '{}\n');
  symlinkSync(target, path.join(a, 'transcripts', 'p', 'leak.jsonl'));
  const ra = stageSmoke(a, path.join(tmpDir('af-stage-a'), 's'));
  assert.equal(ra.ok, false);
  assert.match(ra.reason, /transcripts.p.leak\.jsonl/);
  const b = sessionDir();
  symlinkSync(outside, path.join(b, 'transcripts'));
  const to = path.join(tmpDir('af-stage-b'), 's');
  const rb = stageSmoke(b, to);
  assert.equal(rb.ok, false);
  assert.equal(existsSync(to) && everyFileHolds(to, 'HOST-DECOY'), false, 'a linked directory was not walked');
  const c = sessionDir();
  write(path.join(c, 'transcripts', 'p', 'ok.jsonl'), '{}\n');
  assert.equal(stageSmoke(c, path.join(tmpDir('af-stage-c'), 's')).ok, true, 'control: regular transcripts stage');
});

test('The_token_txt_fact_never_reads_through_a_link_and_hashes_only_a_regular_file', () => {
  const work = tmpDir('af-work');
  const dummy = 'sk-ant-oat01-dummy-abc123';
  assert.deepEqual(tokenTxtFact(work, dummy), { present: false, equalsDummy: false });
  write(path.join(work, 'token.txt'), `${dummy}\n`);
  assert.equal(tokenTxtFact(work, dummy).equalsDummy, true);
  write(path.join(work, 'token.txt'), `CLAUDE_CODE_OAUTH_TOKEN=${dummy}\n`);
  assert.equal(tokenTxtFact(work, dummy).equalsDummy, true, 'the environ line the probe writes is compared by its value');
  write(path.join(work, 'token.txt'), 'CLAUDE_CODE_OAUTH_TOKEN=sk-ant-oat01-other\n');
  assert.equal(tokenTxtFact(work, dummy).equalsDummy, false);
  write(path.join(work, 'token.txt'), '');
  assert.equal(tokenTxtFact(work, dummy).bytes, 0);
  rmSync(path.join(work, 'token.txt'));
  symlinkSync(decoy(), path.join(work, 'token.txt'));
  const fact = tokenTxtFact(work, dummy);
  assert.deepEqual([fact.present, fact.equalsDummy, fact.unreadable], [true, false, 'symlink']);
  assert.equal(JSON.stringify(fact).includes('HOST-DECOY'), false);
  assert.equal('sha256' in fact, false, 'a link target is not hashed');
});

test('The_transcript_copy_refuses_a_file_link_and_copies_nothing', () => {
  const config = tmpDir('af-config');
  write(path.join(config, 'projects', 'p', 'ok.jsonl'), '{}\n');
  symlinkSync(decoy(), path.join(config, 'projects', 'p', 'leak.jsonl'));
  const to = tmpDir('af-tr-to');
  const r = copyTranscripts(config, to);
  assert.equal(r.ok, false);
  assert.match(r.reason, /leak\.jsonl/);
  assert.equal(everyFileHolds(to, 'HOST-DECOY') || readdirSync(to).length > 0, false);
});

test('The_capture_reader_refuses_a_linked_transcript', () => {
  const from = tmpDir('af-capture');
  for (const f of ['stream.ndjson', 'otlp-raw.ndjson']) write(path.join(from, f), '');
  for (const f of ['argv.json', 'meta.json']) write(path.join(from, f), '{}\n');
  assert.equal(readCaptureFiles(from).ok, true, 'control');
  symlinkSync(decoy(), path.join(from, 'transcripts-link.jsonl'));
  mkdirSync(path.join(from, 'transcripts'));
  symlinkSync(decoy(), path.join(from, 'transcripts', 'leak.jsonl'));
  const r = readCaptureFiles(from);
  assert.equal(r.ok, false);
  assert.match(r.reason, /transcripts.leak\.jsonl.*symlink/);
});

test('The_scan_uses_no_byte_of_a_file_whose_parent_directory_was_swapped_for_a_link_after_the_listing', () => {
  const dir = sessionDir();
  const real = fakeToken();
  write(path.join(dir, 'box', 'out', 'd', 'f.txt'), 'clean\n');
  const outside = tmpDir('af-outside');
  writeFileSync(path.join(outside, 'f.txt'), `HOST ${real}\n`); // what the swapped directory would show: a token-shaped decoy outside the tree
  const swap = () => { renameSync(path.join(dir, 'box', 'out', 'd'), path.join(dir, 'moved-d')); symlinkSync(outside, path.join(dir, 'box', 'out', 'd')); };
  const scan = scanSessionDir(dir, { real, hooks: { afterList: swap } });
  assert.deepEqual(scan.hits, [], 'nothing was read through the swapped directory');
  const found = scan.links.find(l => l.rel === path.join('box', 'out', 'd', 'f.txt'));
  assert.equal(found?.severity, 'high', JSON.stringify(scan.links));
  assert.equal(found.kind, 'swapped');
});
