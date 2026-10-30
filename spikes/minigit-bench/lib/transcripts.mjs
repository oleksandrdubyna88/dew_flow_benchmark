// The CLI's transcripts: walk `<config>/projects` and copy the `*.jsonl` files out. Extracted from capture/capture-run.mjs.
import { lstatSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { copyAgentFile } from './agent-files.mjs';
import { fail, ok } from './result.mjs';

/**
 * Every `*.jsonl` under `root` as a path relative to it, plus `refused`: entries that are neither a real directory nor a regular
 * file (a link, a device, a pipe). The layout is an assumption (A-T1), so it is walked, never built; nothing is followed, because
 * the config directory is written by the CLI under test and a link in it must not lead the copy outside the session.
 */
export function walkJsonl(root) {
  const files = [];
  const refused = [];
  const walk = dir => {
    for (const name of readdirSync(dir)) {
      const full = path.join(dir, name);
      const stat = lstatSync(full);
      if (stat.isDirectory()) walk(full);
      else if (!stat.isFile()) refused.push(path.relative(root, full));
      else if (name.endsWith('.jsonl')) files.push(path.relative(root, full));
    }
  };
  try {
    walk(root);
  } catch (e) {
    if (e.code !== 'ENOENT') throw e;
  }
  return { files, refused };
}

/** Copy the CLI's transcripts from `<fromConfigDir>/projects` into `toDir`: ok(relative paths), or a failure naming every refused entry and copying nothing. */
export function copyTranscripts(fromConfigDir, toDir) {
  const from = path.join(fromConfigDir, 'projects');
  const { files, refused } = walkJsonl(from);
  if (refused.length > 0) return fail(`the transcript tree holds ${refused.length} entr${refused.length === 1 ? 'y' : 'ies'} that ${refused.length === 1 ? 'is' : 'are'} not a regular file or a real directory (${refused.join(', ')}): nothing was copied`);
  const copies = files.map(rel => copyAgentFile(from, rel, path.join(toDir, rel)));
  const bad = copies.find(c => !c.ok);
  return bad ? fail(`a transcript could not be copied (${bad.reason}): the copy is incomplete`) : ok(files);
}
