// What a run is pinned to, read from DISK now and compared with the run-map (E3b review M2). The run-map records the pins at `init`; a resume or a run days later must
// see what is there today: the stacks file and its pins, the prices file, the registered snapshots, the prereg tag, the seed hash and the harness. The first version compared the
// map with itself, so no drift could ever show. A harness change is accepted only by `--accept-harness <identity> --amendment <date>` and a dated amendments.md line;
// an accepted identity is written to the run-map and to the ledger once, so the guard and the records see it.
import { createHash } from 'node:crypto';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { limitsProblem } from './bare-limits.mjs';
import { appendLedger } from './ledger.mjs';
import { harnessIdentity } from './prereg-guard.mjs';
import { fail, ok } from './result.mjs';
import { checkResumable, writeRunMap } from './run-map.mjs';
import { runBounded } from './run.mjs';

const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
const readIf = file => (existsSync(file) ? readFileSync(file) : null);

/** The sha256 of a file of the inputs folder, or null when it is absent. */
const hashOf = file => { const bytes = readIf(file); return bytes === null ? null : sha256(bytes); };

async function localTags(repo) {
  const r = await runBounded('git', ['tag', '--list', 'prereg/minigit-bare-*'], { cwd: repo, timeoutMs: 30_000 });
  return r.ok && r.value.code === 0 ? r.value.stdout.split('\n').filter(Boolean) : [];
}

/**
 * The fields the run-map pinned, as the disk holds them NOW: `{ pins, stacksSha256, pricesSha256, snapshots, preregTag, seedSha256, harnessIdentity }`. A file that is absent reads
 * as null (a difference), never as the map's own value.
 */
export async function currentFromDisk({ map, inputsDir, repo }) {
  const file = name => path.join(inputsDir, name);
  const stacks = readIf(file('stacks.json'));
  const registered = readIf(file('cache-snapshots.json'));
  const snapshots = registered === null ? {} : JSON.parse(registered.toString('utf8'));
  const seed = readIf(file('seed.sha256'));
  const tags = await localTags(repo);
  const identity = await harnessIdentity(repo);
  return {
    pins: stacks === null ? null : JSON.parse(stacks.toString('utf8')).pins ?? null,
    stacksSha256: stacks === null ? null : sha256(stacks),
    pricesSha256: map.pricesFile ? hashOf(file(map.pricesFile)) : null,
    snapshots: Object.fromEntries(Object.keys(map.snapshots ?? {}).map(stack => [stack, snapshots[stack]?.treeSha256 ?? null])),
    preregTag: tags.includes(map.preregTag) ? map.preregTag : tags.join(', ') || null,
    seedSha256: seed === null ? null : seed.toString('utf8').trim(),
    harnessIdentity: identity.ok ? identity.value : `unreadable: ${identity.reason}`,
  };
}

/** Write the accepted identity into the run-map and one `accept-harness` line into the ledger; an identity accepted before is not written again. -> the new map. */
function recordAcceptance({ map, accepted, runMapFile, ledgerFile, now }) {
  if ((map.harnessAccepted ?? []).includes(accepted.to)) return map;
  const next = Object.freeze({ ...map, harnessAccepted: [...(map.harnessAccepted ?? []), accepted.to] });
  writeRunMap(runMapFile, next);
  appendLedger(ledgerFile, { event: 'accept-harness', from: accepted.from, to: accepted.to, amendment: accepted.amendmentDate, t: new Date(now()).toISOString() });
  return next;
}

/**
 * The pin check of a run or a resume: the run-map's caps and budgets against the registered ones (review m2); what is on disk against the run-map; a series stop refuses; a changed harness is accepted only through `acceptHarness` and a dated
 * amendment line, and then recorded. -> `ok({ map, current })` | `fail(reason)`.
 */
export async function checkPins({ map, inputsDir, repo, acceptHarness = null, runMapFile, ledgerFile, now = Date.now }) {
  const limits = limitsProblem(map);
  if (limits) return fail(limits);
  const current = await currentFromDisk({ map, inputsDir, repo });
  const amendments = readIf(path.join(inputsDir, 'amendments.md'))?.toString('utf8') ?? '';
  const checked = checkResumable({ map, current, acceptHarness, amendments });
  if (!checked.ok) return fail(checked.reason);
  const next = checked.value.accepted ? recordAcceptance({ map, accepted: checked.value.accepted, runMapFile, ledgerFile, now }) : map;
  return ok({ map: next, current });
}

/**
 * A halt passed with `--after-stop <rule> --reason <text>` is recorded in the run-map (`afterStops`) and as one `after-stop` event line of the ledger (review m8); the same rule and reason
 * recorded before are not written again. The series stops of section 5 never get here (`afterStopRefusal` refuses them). -> the new map.
 */
export function recordAfterStop({ map, rule, reason, runMapFile, ledgerFile, now = Date.now }) {
  if (!rule || (map.afterStops ?? []).some(x => x.rule === rule && x.reason === reason)) return map;
  const at = new Date(now()).toISOString();
  const next = Object.freeze({ ...map, afterStops: [...(map.afterStops ?? []), { rule, reason, at }] });
  writeRunMap(runMapFile, next);
  appendLedger(ledgerFile, { event: 'after-stop', rule, reason, t: at });
  return next;
}
