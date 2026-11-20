// Runs the stub `claude` that `stubClaude` wrote, as the box would, and gives back what it said. The stub talks to two loopback ports only, both answered by one stub upstream, so nothing leaves the machine and nothing is paid.
// Not a test file (no `.test.mjs`), so the suite's glob does not run it; the tests of v1 and of the language comparison import it.
import { runBounded } from '../lib/run.mjs';
import { chunksOf, sseAnswer, sseBody, stubUpstream } from '../lib/stub-upstream.mjs';
import { tmpDir } from './helpers.mjs';

const TIMEOUT_MS = 30_000;

/** The environment the stub reads: the base URL and the OTel endpoint (one stub upstream answers both), a fake token and a config folder of its own. */
const envOf = port => ({
  PATH: process.env.PATH, ANTHROPIC_BASE_URL: `http://127.0.0.1:${port}`, OTEL_EXPORTER_OTLP_ENDPOINT: `http://127.0.0.1:${port}`,
  CLAUDE_CODE_OAUTH_TOKEN: 'sk-ant-fake-for-the-test', CLAUDE_CONFIG_DIR: tmpDir('stub-run-config'),
});

/** The stdout of a bounded run that ended well; a launch failure, a timeout or a non-zero exit throws with what the run said. */
function stdoutOf(ran) {
  if (!ran.ok) throw new Error(ran.reason);
  const { code, timedOut, stdout, stderr } = ran.value;
  if (timedOut || code !== 0) throw new Error(`the stub did not end well (code ${code}, timed out ${timedOut}): ${stderr}`);
  return stdout;
}

/** Every event the stub `binary` writes to its stdout, in order. It runs bounded (v1's `runBounded`: exe + argv, a timeout, the whole process group killed); a stub that fails or hangs throws. */
export async function stubEvents(binary) {
  const upstream = await stubUpstream(sseAnswer(chunksOf(sseBody(), 3), 5));
  try {
    const ran = await runBounded(binary, ['--tools', 'Read'], { env: envOf(upstream.upstream.port), cwd: tmpDir('stub-run-cwd'), timeoutMs: TIMEOUT_MS });
    return stdoutOf(ran).trim().split('\n').map(line => JSON.parse(line));
  } finally {
    await upstream.close();
  }
}

/** The init event of the stub `binary`: the one the init read-back compares with the pin. */
export const stubInit = async binary => (await stubEvents(binary)).find(e => e.type === 'system' && e.subtype === 'init');
