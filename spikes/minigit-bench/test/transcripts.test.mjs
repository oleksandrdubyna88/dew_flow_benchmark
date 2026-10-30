import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { copyTranscripts, walkJsonl } from '../lib/transcripts.mjs';
import { tmpDir } from './helpers.mjs';

/** A config dir with `projects/-p/{a.jsonl,sub/b.jsonl,notes.txt}`. */
function configDir() {
  const config = tmpDir('tr-config');
  const projects = path.join(config, 'projects', '-p');
  mkdirSync(path.join(projects, 'sub'), { recursive: true });
  writeFileSync(path.join(projects, 'a.jsonl'), '{"a":1}\n');
  writeFileSync(path.join(projects, 'sub', 'b.jsonl'), '{"b":1}\n');
  writeFileSync(path.join(projects, 'notes.txt'), 'not a transcript');
  return { config, projects };
}

test('Transcripts_are_copied_from_a_config_dir_to_a_target_dir_keeping_their_relative_paths', () => {
  const { config } = configDir();
  const to = tmpDir('tr-to');
  const r = copyTranscripts(config, to);
  assert.equal(r.ok, true, r.reason);
  assert.deepEqual([...r.value].sort(), [path.join('-p', 'a.jsonl'), path.join('-p', 'sub', 'b.jsonl')]);
  assert.equal(readFileSync(path.join(to, '-p', 'sub', 'b.jsonl'), 'utf8'), '{"b":1}\n');
  assert.equal(existsSync(path.join(to, '-p', 'notes.txt')), false);
});

test('A_config_dir_without_projects_is_an_empty_copy_and_a_link_is_refused_with_nothing_copied', () => {
  assert.deepEqual(copyTranscripts(tmpDir('tr-empty'), tmpDir('tr-to2')), { ok: true, value: [] });
  assert.deepEqual(walkJsonl(path.join(tmpDir('tr-none'), 'nothing')), { files: [], refused: [] });
  const { config, projects } = configDir();
  symlinkSync(tmpDir('tr-outside'), path.join(projects, 'linked'));
  const to = tmpDir('tr-to3');
  const r = copyTranscripts(config, to);
  assert.equal(r.ok, false);
  assert.match(r.reason, /not a regular file or a real directory/);
  assert.equal(existsSync(path.join(to, '-p', 'a.jsonl')), false, 'not even the real files were copied');
});

test('The_capture_copies_its_transcripts_through_the_shared_module', () => {
  const src = readFileSync(path.join(import.meta.dirname, '..', 'capture', 'capture-run.mjs'), 'utf8');
  assert.match(src, /from '\.\.\/lib\/transcripts\.mjs'/);
  assert.equal(src.includes('function walkJsonl'), false, 'the walk lives in lib/transcripts.mjs only');
});
