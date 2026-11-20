// A stub `claude` that reports the version the CURRENT `explore.json` pins, for the tests that run a pair over v1's stub CLI. v1's `stubClaude` writes a stub whose init event says CLI 2.1.310; the pilot's init read-back
// compares that with `explore.cli.version`, so a pre-registration that moves the CLI pin (2.1.310 to 2.1.329) makes every stub session `void: harness` ("the init read-back mismatched"). The stub is made by v1's writer and only the
// version it reports is replaced, and only where it says exactly the one thing this replaces: a stub that changes shape (no version marker, or two) is an error here, never a silent pass. Not a test file (no `.test.mjs`).
import { readFileSync, writeFileSync } from 'node:fs';
import { stubClaude } from '../../minigit-bench/lib/stub-cli.mjs';
import { loadExplore } from '../lib/inputs.mjs';

/** What v1's stub says in its init event, and so what this replaces. */
export const STUB_VERSION_TEXT = "claude_code_version: '2.1.310'";

/** `text` with the stub's one version marker replaced by `version`. Throws when the marker is not there exactly once. */
export function withVersion(text, version) {
  const parts = text.split(STUB_VERSION_TEXT);
  if (parts.length !== 2) throw new Error(`the stub CLI has ${parts.length - 1} version marker(s) (${STUB_VERSION_TEXT}), not 1: v1's stub changed, so this replacement has to follow it`);
  return parts.join(`claude_code_version: ${JSON.stringify(version)}`);
}

/** A stub CLI in `dir` that reports `explore.cli.version` (the live pins by default). -> its path. */
export function stubClaudeForPins(dir, { explore = loadExplore().value, make = stubClaude } = {}) {
  const file = make(dir);
  writeFileSync(file, withVersion(readFileSync(file, 'utf8'), explore.cli.version), { mode: 0o755 });
  return file;
}
