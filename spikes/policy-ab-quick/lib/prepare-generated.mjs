// Scoring copies have no git checkout of the conventions, so the story's `npm run compile` (prepare:gate -> rules.mjs check) cannot
// run in them. This writes the git-ignored src/generated/*.ts modules exactly as the story's own prepare-gate.mjs does, WITHOUT its
// `rules.mjs check` step (recipe: stories/<id>/inputs/commands.json commands.prepareGenerated). Run as a child process:
//   node lib/prepare-generated.mjs <copy root> <source folder>
// because it imports the CANDIDATE's scripts/prepare-gate.mjs (importing does not run it).
import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

export const HEADER = '// Generated from pinned conventions; do not edit.\n';

/** Pure: the four generated modules from the six rule bodies. Keys are file names under src/generated. */
export function renderGenerated({ gate, document, caller, consultant, feature, question }) {
  const one = (name, body) => `${HEADER}export const ${name} = ${JSON.stringify(body)};\n`;
  return {
    'gateRule.ts': `${HEADER}export const GATE_RULE = ${JSON.stringify(gate)};\nexport const DOCUMENT_RULE = ${JSON.stringify(document)};\nexport const CALLER_RULE = ${JSON.stringify(caller)};\n`,
    'consultantRule.ts': one('CONSULTANT_RULE', consultant),
    'featureRule.ts': one('FEATURE_RULE', feature),
    'questionRule.ts': one('QUESTION_RULE', question),
  };
}

export async function prepareGenerated(root, srcDir) {
  const gate = await import(pathToFileURL(path.join(root, srcDir, 'scripts', 'prepare-gate.mjs')).href);
  const read = rel => fs.readFileSync(path.join(root, rel), 'utf8');
  const files = renderGenerated({
    gate: gate.gateBody(read(gate.SOURCE)), document: gate.documentBody(read(gate.DOCUMENT_SOURCE)), caller: gate.callerBody(read(gate.CALLER_SOURCE)),
    consultant: gate.consultantBody(read(gate.CONSULTANT_SOURCE)), feature: gate.featureBody(read(gate.FEATURE_SOURCE)), question: gate.questionBody(read(gate.QUESTION_SOURCE)),
  });
  const dir = path.join(root, srcDir, 'src', 'generated');
  fs.mkdirSync(dir, { recursive: true });
  for (const [name, text] of Object.entries(files)) fs.writeFileSync(path.join(dir, name), text);
  return Object.keys(files);
}

if (process.argv[1]?.endsWith('prepare-generated.mjs')) {
  const [root, srcDir] = process.argv.slice(2);
  if (!root || !srcDir) { console.error('usage: node lib/prepare-generated.mjs <copy root> <source folder>'); process.exit(2); }
  prepareGenerated(path.resolve(root), srcDir).then(names => console.log(`generated ${names.join(', ')}`)).catch(e => { console.error(e.message); process.exit(1); });
}
