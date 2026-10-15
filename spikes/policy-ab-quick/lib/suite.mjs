// Run a story's compiled suite with node --test and read its junit report. Shared by scoring and by the arm-parity probe.
import fs from 'node:fs';
import { parseJunit, verdictOf } from './junit.mjs';
import { run } from './proc.mjs';

const tail = buf => buf.toString('utf8').slice(-1500);

/** failedCases are case KEYS (file::suites::name). */
export async function nodeTest(webDir, targets, xmlPath) {
  const r = await run(process.execPath, ['--test', '--test-reporter=junit', `--test-reporter-destination=${xmlPath}`, ...targets], { cwd: webDir, env: process.env });
  const parsed = fs.existsSync(xmlPath) ? parseJunit(fs.readFileSync(xmlPath, 'utf8')) : parseJunit('');
  return {
    verdict: verdictOf(parsed, r.code), exitCode: r.code, tests: parsed.tests, passed: parsed.passed, failed: parsed.failed, skipped: parsed.skipped,
    failedCases: parsed.cases.filter(c => c.status === 'fail').map(c => c.key), detail: parsed.tests === 0 ? tail(Buffer.concat([r.stdout, r.stderr])) : '',
  };
}
