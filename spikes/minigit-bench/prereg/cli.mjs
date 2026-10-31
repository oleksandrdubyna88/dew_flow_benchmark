// The flags the pre-registration's extraction scripts share (E3b.S3): `--parent-commit <sha on origin/main>` (required) and the script's own options.
import { parseArgs } from 'node:util';
import { fail, ok } from '../lib/result.mjs';

/** `ok(values)` of the flags (`--parent-commit` and `extra` options), or `fail(usage error)`. */
export function parseParentFlags(argv, extra = {}) {
  try {
    const { values } = parseArgs({ args: argv, options: { 'parent-commit': { type: 'string' }, out: { type: 'string' }, ...extra } });
    return values['parent-commit'] ? ok(values) : fail('usage error: --parent-commit <sha on origin/main> is required');
  } catch (e) {
    return fail(`usage error: ${e.message}`);
  }
}
