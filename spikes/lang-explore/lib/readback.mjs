// The read-back of a session's inputs (`common.measurement` rule 1: read back what was actually sent, for at least one cell per arm): the plan a session was launched with is written beside it
// (`plan.json`: no token, no credential), and `readbackOf` compares it with the registered prompt, the CLI's transcript (the first user message, when the CLI wrote one) and the environment keys the
// box really had (`argv.json`). Pure but for the reads of one session directory.
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';

const sha256 = text => createHash('sha256').update(text).digest('hex');

/** What a plan is, without any secret: the fields a reader compares with the registered inputs. */
export function planRecord(plan) {
  return {
    cellId: plan.cellId, arm: plan.arm, promptSha256: sha256(plan.prompt), promptBytes: Buffer.byteLength(plan.prompt), allow: plan.allow, capUsd: plan.capUsd, wallMs: plan.wallMs, stallMs: plan.stallMs,
    locale: plan.locale ?? null, envExtra: plan.envExtra, toolchains: plan.toolchains, cliVersion: plan.cliVersion, claudeBinary: plan.claudeBinary, nodeDir: plan.nodeDir, tsDir: plan.tsDir,
  };
}

/** The plan for `launchSession`, recorded beside the session (its layout is claimed before the plan is made). Returns the plan unchanged. */
export function recordPlan(plan) {
  writeFileSync(path.join(plan.layout.dir, 'plan.json'), `${JSON.stringify(planRecord(plan), null, 2)}\n`);
  return plan;
}

const textOf = content => (typeof content === 'string' ? content : (content ?? []).map(b => b?.text ?? '').join(''));

const transcriptFiles = dir => (existsSync(dir) ? readdirSync(dir, { recursive: true }).map(String).filter(f => f.endsWith('.jsonl')).map(f => path.join(dir, f)) : []);

/** The first user message of one transcript file, or null. */
function userTextIn(file) {
  const events = readFileSync(file, 'utf8').split('\n').filter(Boolean).map(line => JSON.parse(line));
  const user = events.find(e => e.type === 'user' && e.message?.role === 'user');
  return user ? textOf(user.message.content) : null;
}

/** The first user message of the session's transcripts, or null (a stub writes none). */
export function firstUserMessage(sessionDir) {
  return transcriptFiles(path.join(sessionDir, 'transcripts')).map(userTextIn).find(t => t !== null) ?? null;
}

const hasLocaleKeys = (argv, plan) => ['LANG', 'LC_ALL'].every(k => argv.envKeys.includes(k)) && plan.locale === 'de_DE.UTF-8';

/** The four things a read-back compares (`ok: null` is not applicable, e.g. a stub wrote no transcript). */
function readbackChecks({ plan, argv, transcriptSha, registered }) {
  return [
    { id: 'the planned prompt is the registered one', ok: plan.promptSha256 === registered.sha256 },
    { id: 'the prompt the CLI recorded is the registered one', ok: transcriptSha === null ? null : transcriptSha === registered.sha256 },
    { id: 'LANG and LC_ALL are in the box environment', ok: hasLocaleKeys(argv, plan) },
    { id: 'no host is allowed', ok: Array.isArray(plan.allow) && plan.allow.length === 0 },
  ];
}

/**
 * One session's read-back: the prompt's sha256 as planned, as registered and as the CLI recorded it (trailing whitespace ignored), the locale and environment keys, the allowed hosts. `ok` needs the planned
 * prompt to be the registered one, the transcript's (when there is one) to agree, the locale keys in the box's environment and no allowed host.
 */
export function readbackOf({ sessionDir, registered }) {
  const plan = JSON.parse(readFileSync(path.join(sessionDir, 'plan.json'), 'utf8'));
  const argv = JSON.parse(readFileSync(path.join(sessionDir, 'argv.json'), 'utf8'));
  const sent = firstUserMessage(sessionDir);
  const transcriptSha = sent === null ? null : sha256(sent.trimEnd());
  const checks = readbackChecks({ plan, argv, transcriptSha, registered });
  return { plan, envKeys: argv.envKeys, transcriptPromptSha256: transcriptSha, checks, ok: checks.every(c => c.ok !== false) };
}
