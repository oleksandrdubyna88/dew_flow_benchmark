// The two dated input files the verdicts read: the price table and the crosscheck policy (paths overridable on the command line).
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { loadPolicy } from '../lib/crosscheck.mjs';
import { loadPrices } from '../lib/prices.mjs';
import { fail, ok } from '../lib/result.mjs';

export const DEFAULT_PRICES = 'prices-2026-10-29.json';
export const DEFAULT_POLICY = 'crosscheck-policy.json';

function readJson(file) {
  try {
    return ok(JSON.parse(readFileSync(file, 'utf8')));
  } catch (e) {
    return fail(`cannot read ${file}: ${e.message}`);
  }
}

/** `{ ok, value: { table, policy } }`: both files loaded and validated, or the first failure. */
export function crosscheckPolicyFrom(spikeDir, { prices, policy } = {}) {
  const pricesJson = readJson(prices ?? path.join(spikeDir, 'inputs', DEFAULT_PRICES));
  if (!pricesJson.ok) return pricesJson;
  const table = loadPrices(pricesJson.value);
  if (!table.ok) return fail(`the price file is refused: ${table.reason}`);
  const policyJson = readJson(policy ?? path.join(spikeDir, 'inputs', DEFAULT_POLICY));
  if (!policyJson.ok) return policyJson;
  const loaded = loadPolicy(policyJson.value);
  return loaded.ok ? ok({ table: table.value, policy: loaded.value }) : fail(`the policy file is refused: ${loaded.reason}`);
}
