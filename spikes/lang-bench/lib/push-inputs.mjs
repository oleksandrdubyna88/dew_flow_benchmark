// What the push check reads at run time, all of it by path: the private aids and the literal tokens (outside git, in the private
// area), the known-task needle file, the credit terms and, once E1.S3 writes it, the template's file set. A file that cannot be read is
// a failure with a kind - `env` for the aids (the check must never pass without them), `config` for a needle file that is not valid.
import { existsSync, readFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { parseCredits } from './push-credits.mjs';
import { aidNeedles, parseKnownNeedles } from './push-needles.mjs';
import { parseOwnSecrets } from './push-secrets.mjs';
import { readPublicPlans } from './push-public.mjs';

const spikePath = relative => fileURLToPath(new URL(`../${relative}`, import.meta.url));
export const DEFAULT_AIDS = Object.freeze(['PLAN-trap-table.md', 'PLAN-risky-scenarios.md']);
/** The literal tokens' default home: this file in the aids folder, used when it is present (the plan names the folder; the tool reads, never a person). */
export const DEFAULT_TOKENS_FILE = 'leak-tokens.txt';
export const DEFAULT_AIDS_DIR = () => path.join(os.homedir(), 'rsd', 'lang-v2', 'test-design');
const DEFAULT_CREDITS = spikePath('inputs/credits-lang.json');
const DEFAULT_NEEDLES = spikePath('inputs/needles-lang.json');
const DEFAULT_TEMPLATE_FILES = spikePath('templates/stack-files.json');
const BENCH_REPO = spikePath('');
// The two real secret files of this series (the fine-grained PAT, the deploy key). Read at run time, by path, when no --secret-file is named.
export const DEFAULT_SECRET_FILES = () => ['github-pat', 'deploy_key_lang_v2'].map(name => path.join(os.homedir(), '.config', 'lang-v2', name));

function readText(file, kind) {
  try {
    return ok(readFileSync(file, 'utf8'));
  } catch (error) {
    return fail(`${file} cannot be read (${error.code ?? error.message})`, { kind });
  }
}

/** The public texts that exempt shingles: the files named, else the published plans (origin/main of the bench repository, fail closed). */
async function loadPublicTexts({ publicTexts, benchRepo }) {
  if (publicTexts.length === 0) return readPublicPlans({ repo: benchRepo ?? BENCH_REPO });
  const texts = publicTexts.map(f => readText(f, 'config'));
  return texts.find(t => !t.ok) ?? ok(texts.map(t => t.value));
}

/** The default token file when it exists, else null (`existsSync` on the one name: the folder is never listed). */
function defaultTokenFile(aidsDir) {
  const file = path.join(aidsDir ?? DEFAULT_AIDS_DIR(), DEFAULT_TOKENS_FILE);
  return existsSync(file) ? file : null;
}

async function loadAids({ aid, aidsDir, tokens, publicTexts, benchRepo }) {
  const files = aid.length > 0 ? aid : DEFAULT_AIDS.map(name => path.join(aidsDir ?? DEFAULT_AIDS_DIR(), name));
  const aids = [];
  for (const file of files) {
    const text = readText(file, 'env');
    if (!text.ok) return fail(`aid file ${text.reason}: the check does not pass without its aids`, { kind: 'env' });
    aids.push({ name: path.basename(file), text: text.value });
  }
  const tokenFile = tokens ?? defaultTokenFile(aidsDir);
  const tokensText = tokenFile === null ? ok('') : readText(tokenFile, 'env');
  if (!tokensText.ok) return tokensText;
  const exempt = await loadPublicTexts({ publicTexts, benchRepo });
  if (!exempt.ok) return exempt;
  const needles = aidNeedles({ aids, tokensText: tokensText.value, publicTexts: exempt.value });
  return needles.ok ? ok({ ...needles.value, tokenInfo: tokenFile === null ? 'no token file in the aids folder' : `from ${path.basename(tokenFile)}` }) : fail(needles.reason, { kind: 'env' });
}

function loadKnown(file) {
  const text = readText(file, 'config');
  if (!text.ok) return text;
  try {
    const parsed = parseKnownNeedles(JSON.parse(text.value));
    return parsed.ok ? parsed : fail(parsed.reason, { kind: 'config' });
  } catch { return fail(`${file} is not JSON`, { kind: 'config' }); }
}

function loadTemplateFiles(explicit) {
  const file = explicit ?? DEFAULT_TEMPLATE_FILES;
  if (explicit === undefined && !existsSync(file)) return ok([]);
  const text = readText(file, 'config');
  if (!text.ok) return text;
  try {
    const list = JSON.parse(text.value);
    return Array.isArray(list) && list.every(p => typeof p === 'string') ? ok(list) : fail(`${file} must be a JSON list of path patterns`, { kind: 'config' });
  } catch { return fail(`${file} is not JSON`, { kind: 'config' }); }
}

/** ok(inputs) for checkBranch / checkText / checkSelf, or a fail with a `kind`. The credit terms are parsed lazily by the check: a bad file is a value. */
export async function loadInputs(options) {
  const aids = await loadAids(options);
  if (!aids.ok) return aids;
  const known = loadKnown(options.needles ?? DEFAULT_NEEDLES);
  if (!known.ok) return known;
  const templateFiles = loadTemplateFiles(options.templateFiles);
  if (!templateFiles.ok) return templateFiles;
  const creditsText = readText(options.credits ?? DEFAULT_CREDITS, 'config');
  const credits = creditsText.ok ? parseCredits(creditsText.value) : fail(creditsText.reason);
  const ownSecrets = loadOwnSecrets(options.secretFiles ?? []);
  if (!ownSecrets.ok) return ownSecrets;
  return ok({ aids: aids.value, known: known.value, credits, templateFiles: templateFiles.value, ownSecrets: ownSecrets.value });
}

/** ok([{ label, value }]): the values of the named secret files, or of the default two that exist; a NAMED file that cannot be read is an `env` fail. */
export function loadOwnSecrets(named) {
  const files = named.length > 0 ? named : DEFAULT_SECRET_FILES().filter(f => existsSync(f));
  const texts = files.map(f => ({ label: path.basename(f), read: readText(f, 'env') }));
  const unreadable = texts.find(t => !t.read.ok);
  return unreadable ? fail(`secret file ${unreadable.read.reason}: the check does not run without it`, { kind: 'env' }) : parseOwnSecrets(texts.map(t => ({ label: t.label, text: t.read.value })));
}
