// What the agents under test are given must not name the plan: no plan item, decision, story or review id, and no "plan change" (a comment says what the code does). Every file the workspace copies from the repository is read:
// the runner (`lang-bench/runner/`, copied whole into a quick workspace), the templates of both stacks and kinds, and the olympiad runner. A pattern that finds nothing proves nothing, so the scan is shown to find one.
import assert from 'node:assert/strict';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { test } from 'node:test';

const SPIKES = fileURLToPath(new URL('../..', import.meta.url));
const ROOTS = ['lang-bench/runner', 'lang-bench/templates', 'lang-explore/runner'];
const SKIP = new Set(['package-lock.json', 'node_modules', 'bin', 'obj', '__pycache__']);
/** An id of the plan or of a review: a story (`E1.S8a`), a decision (`D56`), a numbered rule or finding (`V11`, `B4`, `F1`, `P2`, `O7`), a test id (`T-L14`), the words "plan change", a plan file or a path of the private side. */
export const PLAN_ID = /\bE\d\.S\d+[a-z0-9-]*|\bD\d{2,3}\b|\bV(?:1|2|6|9|11|12)\b|\b[BPFONKQ]\d\b|\bT-[A-Z]+\d+\b|plan change|\bPLAN_|\bpart ii?\b|rsd\/|lang-v2|heldout-\d/;

function filesUnder(dir) {
  return readdirSync(dir, { withFileTypes: true }).filter(e => !SKIP.has(e.name)).flatMap(e => (e.isDirectory() ? filesUnder(path.join(dir, e.name)) : [path.join(dir, e.name)]));
}

const isText = file => !readFileSync(file).subarray(0, 4096).includes(0);

test('No_file_an_agent_is_given_names_a_plan_item_a_decision_a_story_or_a_review_finding', () => {
  const found = [];
  const files = ROOTS.flatMap(root => filesUnder(path.join(SPIKES, root))).filter(f => statSync(f).size < 2_000_000 && isText(f));
  assert.ok(files.length > 60, `${files.length} files are read`);
  for (const file of files) {
    readFileSync(file, 'utf8').split('\n').forEach((line, i) => { const m = PLAN_ID.exec(line); if (m) found.push(`${path.relative(SPIKES, file)}:${i + 1} ${m[0]}`); });
  }
  assert.deepEqual(found, []);
});

test('The_pattern_finds_the_ids_it_is_meant_to_find_and_leaves_ordinary_words_and_codes_alone', () => {
  for (const hit of ['(E1.S8b)', 'parent D56', 'the V11 environment', 'findings B4', 'the F1 control', 'plan change 5.1', 'part ii of the story', 'T-L14', 'rsd/lang-v2']) assert.ok(PLAN_ID.test(hit), hit);
  for (const clean of ['flake F401', 'V8 heap', 'HTTP/1.1', 'a 64-bit value', 'sha256 b1c2', 'x2 y3', 'FORMAT v2 5.2', 'grammar 2']) assert.equal(PLAN_ID.test(clean), false, clean);
});
